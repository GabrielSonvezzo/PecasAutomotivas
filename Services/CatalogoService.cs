using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using PecasAutomotivas.Data;
using PecasAutomotivas.Models;
using PecasAutomotivas.ViewModels;

namespace PecasAutomotivas.Services;

public interface ICatalogoService
{
    Task<ResultadoPaginado<PecaCardViewModel>> BuscarAsync(CatalogoFiltroViewModel filtro, int tamanhoPagina = CatalogoService.TamanhoPaginaPadrao, CancellationToken ct = default);
    Task<PecaDetalhesViewModel?> ObterDetalhesAsync(int id, CancellationToken ct = default);
    Task<PecaCardViewModel?> BuscarPorCodigoOemAsync(string codigo, CancellationToken ct = default);
    Task<IReadOnlyList<PecaCardViewModel>> DestaquesAsync(int quantidade, CancellationToken ct = default);
    Task<IReadOnlyList<string>> ListarMontadorasAsync(CancellationToken ct = default);
    Task<IReadOnlyList<string>> ListarModelosAsync(string montadora, CancellationToken ct = default);
    Task<IReadOnlyList<OpcaoViewModel>> ListarFabricantesAsync(CancellationToken ct = default);
    Task<IReadOnlyList<FabricanteVitrineViewModel>> ListarFabricantesVitrineAsync(CancellationToken ct = default);
    IReadOnlyList<int> ListarAnos();
}

/// <summary>Busca avançada 100% LINQ/EF Core — todos os valores viram parâmetros SQL.</summary>
public sealed class CatalogoService(ApplicationDbContext db) : ICatalogoService
{
    public const int TamanhoPaginaPadrao = 12;
    private const string CaractereEscape = "\\";

    private static readonly Expression<Func<Peca, PecaCardViewModel>> ProjecaoCard = p => new PecaCardViewModel
    {
        Id = p.Id,
        Nome = p.Nome,
        CodigoOEM = p.CodigoOEM_PartNumber,
        Sku = p.SKU_Fabrica,
        Categoria = p.Categoria,
        Fabricante = p.Fabricante!.NomeFabrica,
        FabricanteHomologado = p.Fabricante.SeloHomologado,
        Montadora = p.MontadoraCompativel,
        Modelo = p.ModeloVeiculoCompativel,
        AnoInicial = p.AnoInicial,
        AnoFinal = p.AnoFinal,
        Motorizacao = p.Motorizacao,
        PrecoConcessionaria = p.PrecoTabelaConcessionaria,
        PrecoFabrica = p.PrecoDiretoFabrica,
        Estoque = p.QuantidadeEstoque,
        Status = p.Status,
        GarantiaMeses = p.GarantiaMeses,
        ImagemUrl = p.ImagemUrl,
        TotalVendidos = p.TotalVendidos,
        NumeroLote = p.NumeroLoteFabrica
    };

    public async Task<ResultadoPaginado<PecaCardViewModel>> BuscarAsync(
        CatalogoFiltroViewModel filtro, int tamanhoPagina = TamanhoPaginaPadrao, CancellationToken ct = default)
    {
        tamanhoPagina = Math.Clamp(tamanhoPagina, 1, 50);
        var consulta = AplicarFiltros(db.Pecas.AsNoTracking(), filtro);

        consulta = filtro.Ordenacao switch
        {
            OrdenacaoCatalogo.MenorPreco => consulta.OrderBy(p => p.PrecoDiretoFabrica).ThenBy(p => p.Id),
            OrdenacaoCatalogo.MaiorPreco => consulta.OrderByDescending(p => p.PrecoDiretoFabrica).ThenBy(p => p.Id),
            OrdenacaoCatalogo.MaiorEconomia => consulta
                .OrderByDescending(p => (p.PrecoTabelaConcessionaria - p.PrecoDiretoFabrica) / p.PrecoTabelaConcessionaria)
                .ThenBy(p => p.Id),
            OrdenacaoCatalogo.Recentes => consulta.OrderByDescending(p => p.DataCadastro).ThenBy(p => p.Id),
            _ => consulta.OrderByDescending(p => p.QuantidadeEstoque > 0).ThenByDescending(p => p.TotalVendidos).ThenBy(p => p.Nome)
        };

        var total = await consulta.CountAsync(ct);
        var totalPaginas = Math.Max(1, (int)Math.Ceiling(total / (double)tamanhoPagina));
        var pagina = Math.Clamp(filtro.Pagina, 1, totalPaginas);

        var itens = await consulta
            .Skip((pagina - 1) * tamanhoPagina)
            .Take(tamanhoPagina)
            .Select(ProjecaoCard)
            .ToListAsync(ct);

        return new ResultadoPaginado<PecaCardViewModel> { Itens = itens, Pagina = pagina, TamanhoPagina = tamanhoPagina, Total = total };
    }

    public async Task<PecaDetalhesViewModel?> ObterDetalhesAsync(int id, CancellationToken ct = default)
    {
        var p = await db.Pecas.AsNoTracking()
            .Include(x => x.Fabricante)
            .FirstOrDefaultAsync(x => x.Id == id, ct);

        if (p is null) return null;

        var relacionadas = await db.Pecas.AsNoTracking()
            .Where(x => x.Id != id
                        && x.MontadoraCompativel == p.MontadoraCompativel
                        && x.ModeloVeiculoCompativel == p.ModeloVeiculoCompativel)
            .OrderByDescending(x => x.TotalVendidos)
            .Take(4)
            .Select(ProjecaoCard)
            .ToListAsync(ct);

        return new PecaDetalhesViewModel
        {
            Id = p.Id,
            Nome = p.Nome,
            CodigoOEM = p.CodigoOEM_PartNumber,
            Sku = p.SKU_Fabrica,
            Categoria = p.Categoria,
            Fabricante = p.Fabricante?.NomeFabrica ?? string.Empty,
            FabricanteHomologado = p.Fabricante?.SeloHomologado ?? false,
            PaisOrigem = p.Fabricante?.PaisOrigem ?? string.Empty,
            CertificacaoISO = p.Fabricante?.CertificacaoISO,
            Montadora = p.MontadoraCompativel,
            Modelo = p.ModeloVeiculoCompativel,
            AnoInicial = p.AnoInicial,
            AnoFinal = p.AnoFinal,
            Motorizacao = p.Motorizacao,
            PrecoConcessionaria = p.PrecoTabelaConcessionaria,
            PrecoFabrica = p.PrecoDiretoFabrica,
            Estoque = p.QuantidadeEstoque,
            Status = p.Status,
            GarantiaMeses = p.GarantiaMeses,
            ImagemUrl = p.ImagemUrl,
            TotalVendidos = p.TotalVendidos,
            NumeroLote = p.NumeroLoteFabrica,
            DescricaoTecnica = p.DescricaoTecnica,
            Relacionadas = relacionadas
        };
    }

    public async Task<PecaCardViewModel?> BuscarPorCodigoOemAsync(string codigo, CancellationToken ct = default)
    {
        var normalizado = NormalizarCodigo(codigo);
        if (normalizado is null) return null;

        return await db.Pecas.AsNoTracking()
            .Where(p => p.CodigoOEM_PartNumber == normalizado)
            .Select(ProjecaoCard)
            .FirstOrDefaultAsync(ct);
    }

    public async Task<IReadOnlyList<PecaCardViewModel>> DestaquesAsync(int quantidade, CancellationToken ct = default)
        => await db.Pecas.AsNoTracking()
            .Where(p => p.QuantidadeEstoque > 0)
            .OrderByDescending(p => (p.PrecoTabelaConcessionaria - p.PrecoDiretoFabrica) / p.PrecoTabelaConcessionaria)
            .ThenByDescending(p => p.TotalVendidos)
            .Take(Math.Clamp(quantidade, 1, 24))
            .Select(ProjecaoCard)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<string>> ListarMontadorasAsync(CancellationToken ct = default)
        => await db.Pecas.AsNoTracking()
            .Select(p => p.MontadoraCompativel)
            .Distinct()
            .OrderBy(m => m)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<string>> ListarModelosAsync(string montadora, CancellationToken ct = default)
    {
        var m = Normalizar(montadora, 60);
        if (m is null) return [];

        return await db.Pecas.AsNoTracking()
            .Where(p => p.MontadoraCompativel == m)
            .Select(p => p.ModeloVeiculoCompativel)
            .Distinct()
            .OrderBy(x => x)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<OpcaoViewModel>> ListarFabricantesAsync(CancellationToken ct = default)
        => await db.Fabricantes.AsNoTracking()
            .OrderBy(f => f.NomeFabrica)
            .Select(f => new OpcaoViewModel(f.Id, f.NomeFabrica))
            .ToListAsync(ct);

    public async Task<IReadOnlyList<FabricanteVitrineViewModel>> ListarFabricantesVitrineAsync(CancellationToken ct = default)
        => await db.Fabricantes.AsNoTracking()
            .OrderByDescending(f => f.SeloHomologado)
            .ThenBy(f => f.NomeFabrica)
            .Select(f => new FabricanteVitrineViewModel(f.Id, f.NomeFabrica, f.PaisOrigem, f.CertificacaoISO, f.SeloHomologado, f.Pecas.Count))
            .ToListAsync(ct);

    public IReadOnlyList<int> ListarAnos()
    {
        var anoMaximo = DateTime.UtcNow.Year + 1;
        return Enumerable.Range(1990, anoMaximo - 1990 + 1).Reverse().ToList();
    }

    // -----------------------------------------------------------------------

    private static IQueryable<Peca> AplicarFiltros(IQueryable<Peca> q, CatalogoFiltroViewModel f)
    {
        var termo = Normalizar(f.Termo, 80);
        if (termo is not null)
        {
            var padrao = $"%{EscaparLike(termo)}%";
            var padraoCodigo = $"%{EscaparLike(termo.ToUpperInvariant())}%";
            q = q.Where(p =>
                EF.Functions.Like(p.CodigoOEM_PartNumber, padraoCodigo, CaractereEscape) ||
                EF.Functions.Like(p.SKU_Fabrica, padraoCodigo, CaractereEscape) ||
                EF.Functions.Like(p.Nome, padrao, CaractereEscape) ||
                EF.Functions.Like(p.MontadoraCompativel, padrao, CaractereEscape) ||
                EF.Functions.Like(p.ModeloVeiculoCompativel, padrao, CaractereEscape) ||
                EF.Functions.Like(p.Fabricante!.NomeFabrica, padrao, CaractereEscape));
        }

        var montadora = Normalizar(f.Montadora, 60);
        if (montadora is not null) q = q.Where(p => p.MontadoraCompativel == montadora);

        var modelo = Normalizar(f.Modelo, 80);
        if (modelo is not null) q = q.Where(p => p.ModeloVeiculoCompativel == modelo);

        if (f.Ano is int ano and >= 1950 and <= 2100)
            q = q.Where(p => p.AnoInicial <= ano && p.AnoFinal >= ano);

        if (f.Categoria is CategoriaPeca categoria && Enum.IsDefined(categoria))
            q = q.Where(p => p.Categoria == categoria);

        if (f.FabricanteId is int fabricanteId and > 0)
            q = q.Where(p => p.FabricanteFabricaId == fabricanteId);

        if (f.SomenteDisponiveis)
            q = q.Where(p => p.QuantidadeEstoque > 0);

        return q;
    }

    private static string? Normalizar(string? valor, int tamanhoMaximo)
    {
        if (string.IsNullOrWhiteSpace(valor)) return null;
        var t = valor.Trim();
        return t.Length > tamanhoMaximo ? t[..tamanhoMaximo] : t;
    }

    private static string? NormalizarCodigo(string? codigo)
        => Normalizar(codigo, 40)?.ToUpperInvariant();

    /// <summary>Impede que % e _ digitados pelo usuário virem curingas do LIKE.</summary>
    private static string EscaparLike(string valor)
        => valor.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_").Replace("[", "\\[");
}
