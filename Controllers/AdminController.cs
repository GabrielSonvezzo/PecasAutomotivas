using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PecasAutomotivas.Data;
using PecasAutomotivas.Helpers;
using PecasAutomotivas.Models;
using PecasAutomotivas.Services;
using PecasAutomotivas.ViewModels;

namespace PecasAutomotivas.Controllers;

[Authorize(Roles = Roles.Admin)]
public sealed class AdminController(
    ApplicationDbContext db,
    IPedidoService pedidos,
    ICatalogoService catalogo,
    IUploadImagemService upload,
    ILogger<AdminController> logger) : Controller
{
    private const int LimiteEstoqueBaixo = 5;
    private static readonly StatusPedido[] StatusEmAberto = [StatusPedido.Pendente, StatusPedido.SeparacaoFabrica, StatusPedido.Enviado];

    [HttpGet]
    public IActionResult Index() => RedirectToAction(nameof(Dashboard));

    // =======================================================================
    // DASHBOARD
    // =======================================================================
    [HttpGet]
    public async Task<IActionResult> Dashboard(CancellationToken ct)
    {
        var agora = DateTime.UtcNow;
        var inicioMes = new DateTime(agora.Year, agora.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var entregues = db.Pedidos.AsNoTracking().Where(p => p.StatusPedido == StatusPedido.Entregue);

        var model = new DashboardAdminViewModel
        {
            Faturamento = await entregues.SumAsync(p => p.ValorTotal, ct),
            FaturamentoMes = await entregues.Where(p => p.DataEntrega >= inicioMes).SumAsync(p => p.ValorTotal, ct),
            EconomiaGeradaClientes = await entregues.SumAsync(p => p.EconomiaGerada, ct),
            TotalPecasEstoque = await db.Pecas.SumAsync(p => p.QuantidadeEstoque, ct),
            TotalPecasVendidas = await db.Pecas.SumAsync(p => p.TotalVendidos, ct),
            PedidosPendentes = await db.Pedidos.CountAsync(p => StatusEmAberto.Contains(p.StatusPedido), ct),
            PecasCadastradas = await db.Pecas.CountAsync(ct),
            PecasEsgotadas = await db.Pecas.CountAsync(p => p.QuantidadeEstoque == 0, ct),
            PedidosEmAberto = await ConsultaLinhasPedido(db.Pedidos.Where(p => StatusEmAberto.Contains(p.StatusPedido)))
                .OrderBy(p => p.DataPedido)
                .Take(10)
                .ToListAsync(ct),
            EstoqueBaixo = await db.Pecas.AsNoTracking()
                .Where(p => p.QuantidadeEstoque <= LimiteEstoqueBaixo)
                .OrderBy(p => p.QuantidadeEstoque).ThenBy(p => p.Nome)
                .Take(8)
                .Select(p => new EstoqueBaixoViewModel
                {
                    Id = p.Id,
                    Nome = p.Nome,
                    CodigoOEM = p.CodigoOEM_PartNumber,
                    Estoque = p.QuantidadeEstoque,
                    NumeroLote = p.NumeroLoteFabrica
                })
                .ToListAsync(ct),
            VendasPorCategoria = (await db.ItensPedido.AsNoTracking()
                    .Where(i => i.Pedido!.StatusPedido == StatusPedido.Entregue)
                    .GroupBy(i => i.Peca!.Categoria)
                    .Select(g => new { Categoria = g.Key, Quantidade = g.Sum(i => i.Quantidade) })
                    .ToListAsync(ct))
                .OrderByDescending(x => x.Quantidade)
                .Select(x => new VendasCategoriaViewModel(x.Categoria, x.Quantidade))
                .ToList()
        };

        return View(model);
    }

    // =======================================================================
    // PEÇAS
    // =======================================================================
    [HttpGet]
    public async Task<IActionResult> Pecas(string? termo, int pagina = 1, CancellationToken ct = default)
    {
        const int tamanho = 15;
        var consulta = db.Pecas.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(termo) && termo.Length <= 80)
        {
            var t = termo.Trim();
            var padrao = $"%{t.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_")}%";
            consulta = consulta.Where(p =>
                EF.Functions.Like(p.Nome, padrao, "\\") ||
                EF.Functions.Like(p.CodigoOEM_PartNumber, padrao.ToUpperInvariant(), "\\") ||
                EF.Functions.Like(p.SKU_Fabrica, padrao.ToUpperInvariant(), "\\"));
        }

        var total = await consulta.CountAsync(ct);
        var totalPaginas = Math.Max(1, (int)Math.Ceiling(total / (double)tamanho));
        pagina = Math.Clamp(pagina, 1, totalPaginas);

        var itens = await consulta
            .OrderBy(p => p.Nome)
            .Skip((pagina - 1) * tamanho)
            .Take(tamanho)
            .Select(p => new AdminPecaLinhaViewModel
            {
                Id = p.Id,
                Nome = p.Nome,
                CodigoOEM = p.CodigoOEM_PartNumber,
                Fabricante = p.Fabricante!.NomeFabrica,
                Categoria = p.Categoria,
                PrecoFabrica = p.PrecoDiretoFabrica,
                PrecoConcessionaria = p.PrecoTabelaConcessionaria,
                Estoque = p.QuantidadeEstoque,
                Vendidos = p.TotalVendidos,
                Status = p.Status,
                NumeroLote = p.NumeroLoteFabrica,
                ImagemUrl = p.ImagemUrl
            })
            .ToListAsync(ct);

        return View(new AdminPecasViewModel
        {
            Termo = termo,
            Resultado = new ResultadoPaginado<AdminPecaLinhaViewModel> { Itens = itens, Pagina = pagina, TamanhoPagina = tamanho, Total = total }
        });
    }

    [HttpGet]
    public async Task<IActionResult> NovaPeca(CancellationToken ct)
        => View("PecaForm", new PecaFormViewModel { Fabricantes = await catalogo.ListarFabricantesAsync(ct) });

    [HttpPost, ValidateAntiForgeryToken, RequestSizeLimit(6 * 1024 * 1024)]
    public async Task<IActionResult> NovaPeca(PecaFormViewModel model, CancellationToken ct)
    {
        model.Id = 0;
        await ValidarPecaAsync(model, ct);
        if (!ModelState.IsValid) return await RetornarFormPecaAsync(model, ct);

        string? imagemUrl = null;
        if (model.Imagem is not null)
        {
            var envio = await upload.SalvarAsync(model.Imagem, ct);
            if (!envio.Sucesso)
            {
                ModelState.AddModelError(nameof(model.Imagem), envio.Mensagem);
                return await RetornarFormPecaAsync(model, ct);
            }
            imagemUrl = envio.Valor;
        }

        var peca = new Peca { DataCadastro = DateTime.UtcNow, ImagemUrl = imagemUrl };
        AplicarFormulario(peca, model);
        db.Pecas.Add(peca);

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex)
        {
            upload.Remover(imagemUrl);
            logger.LogError(ex, "Erro ao salvar nova peça {Oem}", model.CodigoOEM_PartNumber);
            ModelState.AddModelError(string.Empty, "Não foi possível salvar. Verifique se o código OEM já está cadastrado.");
            return await RetornarFormPecaAsync(model, ct);
        }

        TempData["Sucesso"] = $"Peça \"{peca.Nome}\" cadastrada no catálogo.";
        return RedirectToAction(nameof(Pecas));
    }

    [HttpGet]
    public async Task<IActionResult> EditarPeca(int id, CancellationToken ct)
    {
        var p = await db.Pecas.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);
        if (p is null) return NotFound();

        var model = new PecaFormViewModel
        {
            Id = p.Id,
            Nome = p.Nome,
            CodigoOEM_PartNumber = p.CodigoOEM_PartNumber,
            SKU_Fabrica = p.SKU_Fabrica,
            Categoria = p.Categoria,
            FabricanteFabricaId = p.FabricanteFabricaId,
            MontadoraCompativel = p.MontadoraCompativel,
            ModeloVeiculoCompativel = p.ModeloVeiculoCompativel,
            AnoInicial = p.AnoInicial,
            AnoFinal = p.AnoFinal,
            Motorizacao = p.Motorizacao,
            PrecoTabelaConcessionaria = p.PrecoTabelaConcessionaria,
            PrecoDiretoFabrica = p.PrecoDiretoFabrica,
            QuantidadeEstoque = p.QuantidadeEstoque,
            NumeroLoteFabrica = p.NumeroLoteFabrica,
            GarantiaMeses = p.GarantiaMeses,
            DescricaoTecnica = p.DescricaoTecnica,
            ImagemUrlAtual = p.ImagemUrl,
            Fabricantes = await catalogo.ListarFabricantesAsync(ct)
        };
        return View("PecaForm", model);
    }

    [HttpPost, ValidateAntiForgeryToken, RequestSizeLimit(6 * 1024 * 1024)]
    public async Task<IActionResult> EditarPeca(int id, PecaFormViewModel model, CancellationToken ct)
    {
        if (id != model.Id) return BadRequest();

        var peca = await db.Pecas.FirstOrDefaultAsync(p => p.Id == id, ct);
        if (peca is null) return NotFound();

        model.ImagemUrlAtual = peca.ImagemUrl;
        await ValidarPecaAsync(model, ct);
        if (!ModelState.IsValid) return await RetornarFormPecaAsync(model, ct);

        string? imagemAntiga = null;
        string? imagemNova = null;
        if (model.Imagem is not null)
        {
            var envio = await upload.SalvarAsync(model.Imagem, ct);
            if (!envio.Sucesso)
            {
                ModelState.AddModelError(nameof(model.Imagem), envio.Mensagem);
                return await RetornarFormPecaAsync(model, ct);
            }
            imagemAntiga = peca.ImagemUrl;
            imagemNova = envio.Valor;
            peca.ImagemUrl = imagemNova;
        }

        AplicarFormulario(peca, model);
        peca.DataAtualizacao = DateTime.UtcNow;

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            upload.Remover(imagemNova);
            ModelState.AddModelError(string.Empty, "O estoque desta peça foi alterado por outra operação. Recarregue a página e tente de novo.");
            return await RetornarFormPecaAsync(model, ct);
        }
        catch (DbUpdateException ex)
        {
            upload.Remover(imagemNova);
            logger.LogError(ex, "Erro ao atualizar peça {Id}", id);
            ModelState.AddModelError(string.Empty, "Não foi possível salvar. Verifique se o código OEM já está em uso.");
            return await RetornarFormPecaAsync(model, ct);
        }

        upload.Remover(imagemAntiga);
        TempData["Sucesso"] = $"Peça \"{peca.Nome}\" atualizada.";
        return RedirectToAction(nameof(Pecas));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> ExcluirPeca(int id, CancellationToken ct)
    {
        var peca = await db.Pecas.FirstOrDefaultAsync(p => p.Id == id, ct);
        if (peca is null) return NotFound();

        if (await db.ItensPedido.AnyAsync(i => i.PecaId == id, ct))
        {
            TempData["Erro"] = "Esta peça já tem pedidos e não pode ser excluída (histórico e notas). Zere o estoque para retirá-la de venda.";
            return RedirectToAction(nameof(Pecas));
        }

        var imagem = peca.ImagemUrl;
        db.Pecas.Remove(peca);
        await db.SaveChangesAsync(ct);
        upload.Remover(imagem);

        TempData["Sucesso"] = $"Peça \"{peca.Nome}\" excluída.";
        return RedirectToAction(nameof(Pecas));
    }

    /// <summary>Entrada de novo lote de fábrica: soma ao estoque e atualiza o número do lote.</summary>
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> NovoLote(NovoLoteViewModel model, string? returnUrl, CancellationToken ct)
    {
        if (!ModelState.IsValid)
        {
            TempData["Erro"] = string.Join(" ", ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage));
            return RedirecionarLocal(returnUrl, nameof(Dashboard));
        }

        var peca = await db.Pecas.FirstOrDefaultAsync(p => p.Id == model.PecaId, ct);
        if (peca is null) return NotFound();

        peca.QuantidadeEstoque += model.Quantidade;
        peca.NumeroLoteFabrica = model.NumeroLote.Trim().ToUpperInvariant();
        peca.DataAtualizacao = DateTime.UtcNow;
        peca.AtualizarStatusPorEstoque();

        try
        {
            await db.SaveChangesAsync(ct);
            TempData["Sucesso"] = $"Lote {peca.NumeroLoteFabrica} lançado: +{model.Quantidade} un. de \"{peca.Nome}\" (estoque: {peca.QuantidadeEstoque}).";
        }
        catch (DbUpdateConcurrencyException)
        {
            TempData["Erro"] = "O estoque foi alterado ao mesmo tempo por outra operação. Tente novamente.";
        }

        return RedirecionarLocal(returnUrl, nameof(Dashboard));
    }

    // =======================================================================
    // FABRICANTES
    // =======================================================================
    [HttpGet]
    public async Task<IActionResult> Fabricantes(CancellationToken ct)
        => View(await db.Fabricantes.AsNoTracking()
            .OrderBy(f => f.NomeFabrica)
            .Select(f => new AdminFabricanteLinhaViewModel
            {
                Id = f.Id,
                NomeFabrica = f.NomeFabrica,
                PaisOrigem = f.PaisOrigem,
                CertificacaoISO = f.CertificacaoISO,
                SeloHomologado = f.SeloHomologado,
                TotalPecas = f.Pecas.Count
            })
            .ToListAsync(ct));

    [HttpGet]
    public IActionResult NovoFabricante() => View("FabricanteForm", new FabricanteFormViewModel { SeloHomologado = true });

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> NovoFabricante(FabricanteFormViewModel model, CancellationToken ct)
    {
        model.Id = 0;
        await ValidarFabricanteAsync(model, ct);
        if (!ModelState.IsValid) return View("FabricanteForm", model);

        var fabricante = new FabricanteFabrica();
        AplicarFormulario(fabricante, model);
        db.Fabricantes.Add(fabricante);
        await db.SaveChangesAsync(ct);

        TempData["Sucesso"] = $"Fábrica \"{fabricante.NomeFabrica}\" cadastrada.";
        return RedirectToAction(nameof(Fabricantes));
    }

    [HttpGet]
    public async Task<IActionResult> EditarFabricante(int id, CancellationToken ct)
    {
        var f = await db.Fabricantes.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);
        if (f is null) return NotFound();

        return View("FabricanteForm", new FabricanteFormViewModel
        {
            Id = f.Id,
            NomeFabrica = f.NomeFabrica,
            PaisOrigem = f.PaisOrigem,
            CertificacaoISO = f.CertificacaoISO,
            SeloHomologado = f.SeloHomologado
        });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> EditarFabricante(int id, FabricanteFormViewModel model, CancellationToken ct)
    {
        if (id != model.Id) return BadRequest();

        var fabricante = await db.Fabricantes.FirstOrDefaultAsync(f => f.Id == id, ct);
        if (fabricante is null) return NotFound();

        await ValidarFabricanteAsync(model, ct);
        if (!ModelState.IsValid) return View("FabricanteForm", model);

        AplicarFormulario(fabricante, model);
        await db.SaveChangesAsync(ct);

        TempData["Sucesso"] = $"Fábrica \"{fabricante.NomeFabrica}\" atualizada.";
        return RedirectToAction(nameof(Fabricantes));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> ExcluirFabricante(int id, CancellationToken ct)
    {
        var fabricante = await db.Fabricantes.FirstOrDefaultAsync(f => f.Id == id, ct);
        if (fabricante is null) return NotFound();

        if (await db.Pecas.AnyAsync(p => p.FabricanteFabricaId == id, ct))
        {
            TempData["Erro"] = "Existem peças vinculadas a esta fábrica. Remova ou transfira as peças antes de excluir.";
            return RedirectToAction(nameof(Fabricantes));
        }

        db.Fabricantes.Remove(fabricante);
        await db.SaveChangesAsync(ct);
        TempData["Sucesso"] = $"Fábrica \"{fabricante.NomeFabrica}\" excluída.";
        return RedirectToAction(nameof(Fabricantes));
    }

    // =======================================================================
    // PEDIDOS
    // =======================================================================
    [HttpGet]
    public async Task<IActionResult> Pedidos(StatusPedido? status, int pagina = 1, CancellationToken ct = default)
    {
        const int tamanho = 20;
        var consulta = db.Pedidos.AsNoTracking();
        if (status is StatusPedido s && Enum.IsDefined(s)) consulta = consulta.Where(p => p.StatusPedido == s);

        var total = await consulta.CountAsync(ct);
        var totalPaginas = Math.Max(1, (int)Math.Ceiling(total / (double)tamanho));
        pagina = Math.Clamp(pagina, 1, totalPaginas);

        var itens = await ConsultaLinhasPedido(consulta)
            .OrderByDescending(p => p.DataPedido)
            .Skip((pagina - 1) * tamanho)
            .Take(tamanho)
            .ToListAsync(ct);

        return View(new AdminPedidosViewModel
        {
            Status = status,
            Resultado = new ResultadoPaginado<AdminPedidoLinhaViewModel> { Itens = itens, Pagina = pagina, TamanhoPagina = tamanho, Total = total }
        });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> AtualizarStatusPedido(AtualizarStatusPedidoViewModel model, CancellationToken ct)
    {
        if (!ModelState.IsValid || model.NovoStatus is null)
        {
            TempData["Erro"] = "Dados inválidos para atualizar o pedido.";
            return RedirecionarLocal(model.ReturnUrl, nameof(Pedidos));
        }

        var resultado = await pedidos.AtualizarStatusAsync(model.PedidoId, model.NovoStatus.Value, model.CodigoRastreio, ct);
        TempData[resultado.Sucesso ? "Sucesso" : "Erro"] = resultado.Mensagem;
        return RedirecionarLocal(model.ReturnUrl, nameof(Pedidos));
    }

    /// <summary>Botão de 1 clique "Confirmar entrega / Marcar como vendido".</summary>
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> ConfirmarEntrega(int id, string? returnUrl, CancellationToken ct)
    {
        var resultado = await pedidos.ConfirmarEntregaAsync(id, ct);
        TempData[resultado.Sucesso ? "Sucesso" : "Erro"] = resultado.Mensagem;
        return RedirecionarLocal(returnUrl, nameof(Dashboard));
    }

    // =======================================================================
    // Auxiliares
    // =======================================================================
    private static IQueryable<AdminPedidoLinhaViewModel> ConsultaLinhasPedido(IQueryable<Pedido> consulta)
        => consulta.AsNoTracking().Select(p => new AdminPedidoLinhaViewModel
        {
            Id = p.Id,
            Cliente = p.Usuario!.NomeCompleto,
            Email = p.Usuario.Email,
            Cidade = p.CidadeEntrega,
            Estado = p.EstadoEntrega,
            ValorTotal = p.ValorTotal,
            Status = p.StatusPedido,
            DataPedido = p.DataPedido,
            DataEntrega = p.DataEntrega,
            QuantidadeItens = p.Itens.Sum(i => i.Quantidade),
            CodigoRastreio = p.CodigoRastreio,
            Veiculo = p.VeiculoAplicado
        });

    private async Task ValidarPecaAsync(PecaFormViewModel model, CancellationToken ct)
    {
        var oem = model.CodigoOEM_PartNumber?.Trim().ToUpperInvariant() ?? string.Empty;
        if (oem.Length > 0 && await db.Pecas.AnyAsync(p => p.CodigoOEM_PartNumber == oem && p.Id != model.Id, ct))
            ModelState.AddModelError(nameof(model.CodigoOEM_PartNumber), "Já existe uma peça com este código OEM.");

        if (model.FabricanteFabricaId > 0 && !await db.Fabricantes.AnyAsync(f => f.Id == model.FabricanteFabricaId, ct))
            ModelState.AddModelError(nameof(model.FabricanteFabricaId), "Fabricante inválido.");

        if (model.Categoria is CategoriaPeca c && !Enum.IsDefined(c))
            ModelState.AddModelError(nameof(model.Categoria), "Categoria inválida.");
    }

    private async Task ValidarFabricanteAsync(FabricanteFormViewModel model, CancellationToken ct)
    {
        var nome = model.NomeFabrica?.Trim() ?? string.Empty;
        if (nome.Length > 0 && await db.Fabricantes.AnyAsync(f => f.NomeFabrica == nome && f.Id != model.Id, ct))
            ModelState.AddModelError(nameof(model.NomeFabrica), "Já existe uma fábrica com este nome.");
    }

    private async Task<IActionResult> RetornarFormPecaAsync(PecaFormViewModel model, CancellationToken ct)
    {
        model.Fabricantes = await catalogo.ListarFabricantesAsync(ct);
        return View("PecaForm", model);
    }

    private static void AplicarFormulario(Peca peca, PecaFormViewModel m)
    {
        peca.Nome = m.Nome.Trim();
        peca.CodigoOEM_PartNumber = m.CodigoOEM_PartNumber.Trim().ToUpperInvariant();
        peca.SKU_Fabrica = m.SKU_Fabrica.Trim().ToUpperInvariant();
        peca.Categoria = m.Categoria!.Value;
        peca.FabricanteFabricaId = m.FabricanteFabricaId;
        peca.MontadoraCompativel = m.MontadoraCompativel.Trim();
        peca.ModeloVeiculoCompativel = m.ModeloVeiculoCompativel.Trim();
        peca.AnoInicial = m.AnoInicial;
        peca.AnoFinal = m.AnoFinal;
        peca.Motorizacao = string.IsNullOrWhiteSpace(m.Motorizacao) ? null : m.Motorizacao.Trim();
        peca.PrecoTabelaConcessionaria = Math.Round(m.PrecoTabelaConcessionaria, 2);
        peca.PrecoDiretoFabrica = Math.Round(m.PrecoDiretoFabrica, 2);
        peca.QuantidadeEstoque = m.QuantidadeEstoque;
        peca.NumeroLoteFabrica = string.IsNullOrWhiteSpace(m.NumeroLoteFabrica) ? null : m.NumeroLoteFabrica.Trim().ToUpperInvariant();
        peca.GarantiaMeses = m.GarantiaMeses;
        peca.DescricaoTecnica = string.IsNullOrWhiteSpace(m.DescricaoTecnica) ? null : m.DescricaoTecnica.Trim();
        peca.AtualizarStatusPorEstoque();
    }

    private static void AplicarFormulario(FabricanteFabrica f, FabricanteFormViewModel m)
    {
        f.NomeFabrica = m.NomeFabrica.Trim();
        f.PaisOrigem = m.PaisOrigem.Trim();
        f.CertificacaoISO = string.IsNullOrWhiteSpace(m.CertificacaoISO) ? null : m.CertificacaoISO.Trim();
        f.SeloHomologado = m.SeloHomologado;
    }

    private IActionResult RedirecionarLocal(string? returnUrl, string acaoPadrao)
        => !string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl)
            ? LocalRedirect(returnUrl)
            : RedirectToAction(acaoPadrao);
}
