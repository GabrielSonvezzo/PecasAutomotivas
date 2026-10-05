using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using PecasAutomotivas.Helpers;
using PecasAutomotivas.Models;
using PecasAutomotivas.Services;
using PecasAutomotivas.ViewModels;

namespace PecasAutomotivas.Controllers;

[Authorize]
public sealed class PedidosController(
    IPedidoService pedidos,
    ICatalogoService catalogo,
    UserManager<ApplicationUser> userManager) : Controller
{
    private string UsuarioId => User.FindFirstValue(ClaimTypes.NameIdentifier)!;

    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken ct)
        => View(await pedidos.ListarDoUsuarioAsync(UsuarioId, ct));

    [HttpGet]
    public async Task<IActionResult> Detalhes(int id, CancellationToken ct)
    {
        var pedido = await pedidos.ObterDoUsuarioAsync(id, UsuarioId, ct);
        return pedido is null ? NotFound() : View(pedido);
    }

    [HttpGet]
    public async Task<IActionResult> Checkout(int pecaId, int quantidade = 1, CancellationToken ct = default)
    {
        var peca = await catalogo.ObterDetalhesAsync(pecaId, ct);
        if (peca is null) return NotFound();

        if (!peca.Disponivel)
        {
            TempData["Erro"] = "Esta peça está esgotada no momento.";
            return RedirectToAction("Detalhes", "Catalogo", new { id = pecaId });
        }

        var usuario = await userManager.GetUserAsync(User);
        var model = new CheckoutViewModel
        {
            PecaId = pecaId,
            Quantidade = Math.Clamp(quantidade, 1, Math.Min(99, peca.Estoque)),
            Cidade = usuario?.Cidade ?? string.Empty,
            Estado = usuario?.Estado ?? string.Empty,
            VeiculoAplicado = $"{peca.Montadora} {peca.Modelo}",
            Peca = peca
        };
        return View(model);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Criar(CheckoutViewModel model, CancellationToken ct)
    {
        if (!ModelState.IsValid)
        {
            model.Peca = await catalogo.ObterDetalhesAsync(model.PecaId, ct);
            return model.Peca is null ? NotFound() : View(nameof(Checkout), model);
        }

        var resultado = await pedidos.CriarPedidoAsync(
            UsuarioId,
            [new ItemSolicitado(model.PecaId, model.Quantidade)],
            new DadosEntrega(model.Cidade, model.Estado, model.VeiculoAplicado),
            ct);

        if (!resultado.Sucesso)
        {
            ModelState.AddModelError(string.Empty, resultado.Mensagem);
            model.Peca = await catalogo.ObterDetalhesAsync(model.PecaId, ct);
            return model.Peca is null ? NotFound() : View(nameof(Checkout), model);
        }

        TempData["Sucesso"] = resultado.Mensagem;
        return RedirectToAction(nameof(Detalhes), new { id = resultado.Valor });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Avaliar(AvaliacaoViewModel model, CancellationToken ct)
    {
        if (!ModelState.IsValid)
        {
            TempData["Erro"] = string.Join(" ", ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage));
            return RedirectToAction(nameof(Detalhes), new { id = model.PedidoId });
        }

        var resultado = await pedidos.AvaliarAsync(UsuarioId, model, ct);
        TempData[resultado.Sucesso ? "Sucesso" : "Erro"] = resultado.Mensagem;
        return RedirectToAction(nameof(Detalhes), new { id = model.PedidoId });
    }
}
