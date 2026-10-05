using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using PecasAutomotivas.Configuration;
using PecasAutomotivas.Models;
using PecasAutomotivas.Services;
using PecasAutomotivas.ViewModels;
using PecasAutomotivas.ViewModels.Api;

namespace PecasAutomotivas.Controllers.Api;

/// <summary>
/// API REST JSON do aplicativo mobile. Autenticação exclusivamente por JWT Bearer
/// (o cookie da Web não é aceito aqui). Sem cookie de sessão não existe CSRF, por isso o
/// antiforgery é desligado apenas neste controller.
/// </summary>
[ApiController]
[Route("api/v1")]
[Produces("application/json")]
[IgnoreAntiforgeryToken]
[EnableRateLimiting(PoliticasRateLimit.Api)]
[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
public sealed class MobileApiController(
    UserManager<ApplicationUser> userManager,
    SignInManager<ApplicationUser> signInManager,
    ITokenService tokenService,
    ICatalogoService catalogo,
    IPedidoService pedidos,
    IHistoricoService historico,
    ILogger<MobileApiController> logger) : ControllerBase
{
    private string? UsuarioId => User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue(JwtRegisteredClaimNames.Sub);

    // =======================================================================
    // AUTENTICAÇÃO
    // =======================================================================

    /// <summary>Troca e-mail + senha por um token JWT. Aplica o mesmo Lockout da Web (5 falhas = 15 min).</summary>
    [HttpPost("auth/token")]
    [AllowAnonymous]
    [EnableRateLimiting(PoliticasRateLimit.Login)]
    [ProducesResponseType<TokenApiResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status423Locked)]
    public async Task<IActionResult> Autenticar([FromBody] LoginApiRequest request)
    {
        var usuario = await userManager.FindByEmailAsync(request.Email.Trim());
        if (usuario is null)
            return Problem(statusCode: StatusCodes.Status401Unauthorized, title: "E-mail ou senha incorretos.");

        var resultado = await signInManager.CheckPasswordSignInAsync(usuario, request.Senha, lockoutOnFailure: true);

        if (resultado.IsLockedOut)
        {
            logger.LogWarning("API: conta bloqueada por excesso de tentativas {UsuarioId}", usuario.Id);
            return Problem(statusCode: StatusCodes.Status423Locked,
                title: "Conta bloqueada por 15 minutos após 5 tentativas sem sucesso.");
        }

        if (!resultado.Succeeded)
            return Problem(statusCode: StatusCodes.Status401Unauthorized, title: "E-mail ou senha incorretos.");

        var roles = await userManager.GetRolesAsync(usuario);
        var token = tokenService.GerarToken(usuario, roles);

        return Ok(new TokenApiResponse(token.AccessToken, "Bearer", token.ExpiraEmUtc,
            usuario.NomeCompleto, usuario.Email ?? string.Empty, roles.ToList()));
    }

    /// <summary>Perfil do usuário autenticado.</summary>
    [HttpGet("me")]
    [ProducesResponseType<PerfilApiResponse>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Perfil()
    {
        if (UsuarioId is not { } id) return Unauthorized();
        var usuario = await userManager.FindByIdAsync(id);
        if (usuario is null) return Unauthorized();

        var roles = await userManager.GetRolesAsync(usuario);
        return Ok(new PerfilApiResponse(usuario.Id, usuario.NomeCompleto, usuario.Email ?? string.Empty,
            usuario.Telefone, usuario.Cidade, usuario.Estado, roles.ToList()));
    }

    // =======================================================================
    // CATÁLOGO (público)
    // =======================================================================

    /// <summary>Lista peças com os mesmos filtros do catálogo Web (termo, montadora, modelo, ano, categoria, fabricante).</summary>
    [HttpGet("pecas")]
    [AllowAnonymous]
    [ProducesResponseType<ResultadoPaginado<PecaCardViewModel>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> ListarPecas([FromQuery] CatalogoFiltroViewModel filtro, [FromQuery] int tamanhoPagina = 20, CancellationToken ct = default)
    {
        var resultado = await catalogo.BuscarAsync(filtro, Math.Clamp(tamanhoPagina, 1, 50), ct);
        foreach (var peca in resultado.Itens) peca.ImagemUrl = UrlAbsoluta(peca.ImagemUrl);
        return Ok(resultado);
    }

    [HttpGet("pecas/{id:int}")]
    [AllowAnonymous]
    [ProducesResponseType<PecaDetalhesViewModel>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ObterPeca(int id, CancellationToken ct)
    {
        var peca = await catalogo.ObterDetalhesAsync(id, ct);
        if (peca is null) return NotFound();

        peca.ImagemUrl = UrlAbsoluta(peca.ImagemUrl);
        foreach (var relacionada in peca.Relacionadas) relacionada.ImagemUrl = UrlAbsoluta(relacionada.ImagemUrl);
        return Ok(peca);
    }

    /// <summary>Busca exata por código OEM / part number (ignora pontos, espaços e caixa).</summary>
    [HttpGet("pecas/oem/{codigo}")]
    [AllowAnonymous]
    [ProducesResponseType<PecaCardViewModel>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> BuscarPorOem(string codigo, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(codigo) || codigo.Length > 40)
            return Problem(statusCode: StatusCodes.Status400BadRequest, title: "Código OEM inválido.");

        var peca = await catalogo.BuscarPorCodigoOemAsync(codigo, ct);
        if (peca is null) return NotFound();

        peca.ImagemUrl = UrlAbsoluta(peca.ImagemUrl);
        return Ok(peca);
    }

    [HttpGet("montadoras")]
    [AllowAnonymous]
    public async Task<IActionResult> Montadoras(CancellationToken ct) => Ok(await catalogo.ListarMontadorasAsync(ct));

    [HttpGet("montadoras/{montadora}/modelos")]
    [AllowAnonymous]
    public async Task<IActionResult> Modelos(string montadora, CancellationToken ct)
        => montadora.Length > 60 ? BadRequest() : Ok(await catalogo.ListarModelosAsync(montadora, ct));

    // =======================================================================
    // HISTÓRICO DE ENTREGAS (público)
    // =======================================================================

    [HttpGet("historico")]
    [AllowAnonymous]
    public async Task<IActionResult> Historico([FromQuery] int pagina = 1, [FromQuery] int tamanhoPagina = 10, CancellationToken ct = default)
    {
        var estatisticas = await historico.ObterEstatisticasAsync(ct);
        var entregas = await historico.ListarEntregasAsync(Math.Max(1, pagina), Math.Clamp(tamanhoPagina, 1, 30), ct);
        return Ok(new { estatisticas, entregas });
    }

    // =======================================================================
    // PEDIDOS (autenticado)
    // =======================================================================

    [HttpPost("pedidos")]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> CriarPedido([FromBody] CriarPedidoApiRequest request, CancellationToken ct)
    {
        if (UsuarioId is not { } id) return Unauthorized();

        var itens = request.Itens
            .GroupBy(i => i.PecaId)
            .Select(g => new ItemSolicitado(g.Key, g.Sum(i => i.Quantidade)))
            .ToList();

        var resultado = await pedidos.CriarPedidoAsync(id, itens,
            new DadosEntrega(request.Cidade, request.Estado, request.VeiculoAplicado), ct);

        if (!resultado.Sucesso)
            return Problem(statusCode: StatusCodes.Status422UnprocessableEntity, title: resultado.Mensagem);

        var pedido = await pedidos.ObterDoUsuarioAsync(resultado.Valor, id, ct);
        return CreatedAtAction(nameof(ObterPedido), new { id = resultado.Valor }, pedido);
    }

    [HttpGet("pedidos")]
    public async Task<IActionResult> ListarPedidos(CancellationToken ct)
        => UsuarioId is { } id ? Ok(await pedidos.ListarDoUsuarioAsync(id, ct)) : Unauthorized();

    [HttpGet("pedidos/{id:int}")]
    public async Task<IActionResult> ObterPedido(int id, CancellationToken ct)
    {
        if (UsuarioId is not { } usuarioId) return Unauthorized();
        var pedido = await pedidos.ObterDoUsuarioAsync(id, usuarioId, ct); // anti-IDOR
        return pedido is null ? NotFound() : Ok(pedido);
    }

    // =======================================================================

    private string? UrlAbsoluta(string? caminho)
    {
        if (string.IsNullOrWhiteSpace(caminho)) return null;
        if (Uri.TryCreate(caminho, UriKind.Absolute, out _)) return caminho;
        return $"{Request.Scheme}://{Request.Host}{Request.PathBase}/{caminho.TrimStart('~', '/')}";
    }
}
