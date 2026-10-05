using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PecasAutomotivas.Services;
using PecasAutomotivas.ViewModels;

namespace PecasAutomotivas.Controllers;

/// <summary>Aba pública "Peças já vendidas e lotes entregues".</summary>
[AllowAnonymous]
public sealed class HistoricoController(IHistoricoService historico) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Index(int pagina = 1, CancellationToken ct = default)
    {
        var model = new HistoricoViewModel
        {
            Estatisticas = await historico.ObterEstatisticasAsync(ct),
            Entregas = await historico.ListarEntregasAsync(pagina, 9, ct)
        };
        return View(model);
    }
}
