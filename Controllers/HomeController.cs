using System.Diagnostics;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PecasAutomotivas.Services;
using PecasAutomotivas.ViewModels;

namespace PecasAutomotivas.Controllers;

[AllowAnonymous]
public sealed class HomeController(ICatalogoService catalogo, IHistoricoService historico) : Controller
{
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        var model = new HomeViewModel
        {
            Destaques = await catalogo.DestaquesAsync(8, ct),
            Montadoras = await catalogo.ListarMontadorasAsync(ct),
            Anos = catalogo.ListarAnos(),
            Fabricantes = await catalogo.ListarFabricantesVitrineAsync(ct),
            Estatisticas = await historico.ObterEstatisticasAsync(ct),
            UltimasEntregas = (await historico.ListarEntregasAsync(1, 3, ct)).Itens
        };
        return View(model);
    }

    [IgnoreAntiforgeryToken]
    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error(int? codigo)
    {
        ViewData["RequestId"] = Activity.Current?.Id ?? HttpContext.TraceIdentifier;
        ViewData["Codigo"] = codigo is >= 400 and <= 599 ? codigo : 500;
        if (codigo is >= 400 and <= 599) Response.StatusCode = codigo.Value;
        return View();
    }
}
