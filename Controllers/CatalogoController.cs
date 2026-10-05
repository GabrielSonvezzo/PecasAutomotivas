using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PecasAutomotivas.Services;
using PecasAutomotivas.ViewModels;

namespace PecasAutomotivas.Controllers;

[AllowAnonymous]
public sealed class CatalogoController(ICatalogoService catalogo) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Index([FromQuery] CatalogoFiltroViewModel filtro, CancellationToken ct)
    {
        if (!ModelState.IsValid)
        {
            // Parâmetro inválido (ex.: ano fora da faixa) não derruba a busca: é descartado.
            filtro = new CatalogoFiltroViewModel { Termo = filtro.Termo?.Length <= 80 ? filtro.Termo : null };
        }

        var model = new CatalogoIndexViewModel
        {
            Filtro = filtro,
            Resultado = await catalogo.BuscarAsync(filtro, ct: ct),
            Montadoras = await catalogo.ListarMontadorasAsync(ct),
            Modelos = string.IsNullOrWhiteSpace(filtro.Montadora) ? [] : await catalogo.ListarModelosAsync(filtro.Montadora, ct),
            Anos = catalogo.ListarAnos(),
            Fabricantes = await catalogo.ListarFabricantesAsync(ct)
        };
        return View(model);
    }

    [HttpGet]
    public async Task<IActionResult> Detalhes(int id, CancellationToken ct)
    {
        if (id <= 0) return NotFound();
        var peca = await catalogo.ObterDetalhesAsync(id, ct);
        return peca is null ? NotFound() : View(peca);
    }

    /// <summary>Busca rápida da navbar: se o termo for exatamente um código OEM, vai direto para a peça.</summary>
    [HttpGet]
    public async Task<IActionResult> BuscaRapida(string? q, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(q) || q.Length > 80) return RedirectToAction(nameof(Index));

        var exata = await catalogo.BuscarPorCodigoOemAsync(q, ct);
        return exata is not null
            ? RedirectToAction(nameof(Detalhes), new { id = exata.Id })
            : RedirectToAction(nameof(Index), new { Termo = q.Trim() });
    }

    /// <summary>JSON do seletor em cascata Montadora → Modelo.</summary>
    [HttpGet]
    public async Task<IActionResult> Modelos(string? montadora, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(montadora) || montadora.Length > 60) return Json(Array.Empty<string>());
        return Json(await catalogo.ListarModelosAsync(montadora, ct));
    }
}
