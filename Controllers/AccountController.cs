using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using PecasAutomotivas.Configuration;
using PecasAutomotivas.Helpers;
using PecasAutomotivas.Models;
using PecasAutomotivas.ViewModels;

namespace PecasAutomotivas.Controllers;

public sealed class AccountController(
    SignInManager<ApplicationUser> signInManager,
    UserManager<ApplicationUser> userManager,
    ILogger<AccountController> logger) : Controller
{
    [HttpGet, AllowAnonymous]
    public IActionResult Login(string? returnUrl = null)
    {
        if (signInManager.IsSignedIn(User)) return RedirecionarLocal(returnUrl);
        return View(new LoginViewModel { ReturnUrl = returnUrl });
    }

    [HttpPost, AllowAnonymous, ValidateAntiForgeryToken, EnableRateLimiting(PoliticasRateLimit.Login)]
    public async Task<IActionResult> Login(LoginViewModel model)
    {
        if (!ModelState.IsValid) return View(model);

        // lockoutOnFailure: true → após 5 falhas a conta fica bloqueada por 15 minutos (Program.cs).
        var resultado = await signInManager.PasswordSignInAsync(model.Email.Trim(), model.Senha, model.LembrarMe, lockoutOnFailure: true);

        if (resultado.Succeeded)
        {
            logger.LogInformation("Login efetuado: {Email}", model.Email);
            return RedirecionarLocal(model.ReturnUrl);
        }

        if (resultado.IsLockedOut)
        {
            logger.LogWarning("Conta bloqueada por excesso de tentativas: {Email}", model.Email);
            ModelState.AddModelError(string.Empty, "Conta bloqueada por 15 minutos após 5 tentativas sem sucesso. Aguarde e tente novamente.");
            return View(model);
        }

        if (resultado.IsNotAllowed)
        {
            ModelState.AddModelError(string.Empty, "Seu acesso ainda não foi liberado. Fale com o atendimento.");
            return View(model);
        }

        // Mensagem genérica: não revela se o e-mail existe.
        ModelState.AddModelError(string.Empty, "E-mail ou senha incorretos.");
        return View(model);
    }

    [HttpGet, AllowAnonymous]
    public IActionResult Register()
    {
        if (signInManager.IsSignedIn(User)) return RedirectToAction("Index", "Home");
        return View(new RegisterViewModel());
    }

    [HttpPost, AllowAnonymous, ValidateAntiForgeryToken, EnableRateLimiting(PoliticasRateLimit.Login)]
    public async Task<IActionResult> Register(RegisterViewModel model)
    {
        if (!ModelState.IsValid) return View(model);

        var documento = Documento.SomenteDigitos(model.CPF_CNPJ);
        if (await userManager.Users.AnyAsync(u => u.CPF_CNPJ == documento))
        {
            ModelState.AddModelError(nameof(model.CPF_CNPJ), "Já existe uma conta com este CPF/CNPJ.");
            return View(model);
        }

        var email = model.Email.Trim();
        var usuario = new ApplicationUser
        {
            UserName = email,
            Email = email,
            NomeCompleto = model.NomeCompleto.Trim(),
            CPF_CNPJ = documento,
            Telefone = model.Telefone.Trim(),
            PhoneNumber = model.Telefone.Trim(),
            Cidade = model.Cidade.Trim(),
            Estado = model.Estado.Trim().ToUpperInvariant(),
            DataCadastro = DateTime.UtcNow,
            LockoutEnabled = true
        };

        var resultado = await userManager.CreateAsync(usuario, model.Senha);
        if (!resultado.Succeeded)
        {
            foreach (var erro in resultado.Errors)
            {
                var campo = erro.Code.StartsWith("Password", StringComparison.Ordinal) ? nameof(model.Senha)
                    : erro.Code.Contains("Email", StringComparison.Ordinal) || erro.Code.Contains("UserName", StringComparison.Ordinal) ? nameof(model.Email)
                    : string.Empty;
                ModelState.AddModelError(campo, erro.Description);
            }
            return View(model);
        }

        await userManager.AddToRoleAsync(usuario, Roles.Cliente);
        await signInManager.SignInAsync(usuario, isPersistent: false);

        logger.LogInformation("Novo cliente cadastrado: {UsuarioId}", usuario.Id);
        TempData["Sucesso"] = $"Conta criada. Bem-vindo(a), {usuario.NomeCompleto.Split(' ')[0]}!";
        return RedirectToAction("Index", "Catalogo");
    }

    [HttpPost, Authorize, ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        await signInManager.SignOutAsync();
        TempData["Sucesso"] = "Você saiu da sua conta.";
        return RedirectToAction("Index", "Home");
    }

    [HttpGet, AllowAnonymous]
    public IActionResult AcessoNegado() => View();

    private IActionResult RedirecionarLocal(string? returnUrl)
        => !string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl)
            ? LocalRedirect(returnUrl)
            : RedirectToAction("Index", "Home");
}
