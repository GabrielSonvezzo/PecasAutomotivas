using System.ComponentModel.DataAnnotations;
using PecasAutomotivas.Helpers;
using PecasAutomotivas.Models;

namespace PecasAutomotivas.ViewModels;

public static class OrdenacaoCatalogo
{
    public const string Relevancia = "relevancia";
    public const string MenorPreco = "menor-preco";
    public const string MaiorPreco = "maior-preco";
    public const string MaiorEconomia = "maior-economia";
    public const string Recentes = "recentes";

    public static readonly (string Valor, string Nome)[] Opcoes =
    [
        (Relevancia, "Mais vendidas"),
        (MaiorEconomia, "Maior economia"),
        (MenorPreco, "Menor preço"),
        (MaiorPreco, "Maior preço"),
        (Recentes, "Chegaram agora")
    ];
}

/// <summary>Somente entrada (query string). Usado pelo catálogo Web e pela API mobile.</summary>
public sealed class CatalogoFiltroViewModel
{
    [StringLength(80)]
    [Display(Name = "Código OEM, peça ou veículo")]
    public string? Termo { get; set; }

    [StringLength(60)]
    public string? Montadora { get; set; }

    [StringLength(80)]
    public string? Modelo { get; set; }

    [Range(1950, 2100)]
    public int? Ano { get; set; }

    public CategoriaPeca? Categoria { get; set; }

    [Range(1, int.MaxValue)]
    public int? FabricanteId { get; set; }

    public bool SomenteDisponiveis { get; set; }

    [RegularExpression("^(relevancia|menor-preco|maior-preco|maior-economia|recentes)$")]
    public string Ordenacao { get; set; } = OrdenacaoCatalogo.Relevancia;

    [Range(1, 10_000)]
    public int Pagina { get; set; } = 1;

    public bool TemFiltroAtivo =>
        !string.IsNullOrWhiteSpace(Termo) || !string.IsNullOrWhiteSpace(Montadora) || !string.IsNullOrWhiteSpace(Modelo)
        || Ano.HasValue || Categoria.HasValue || FabricanteId.HasValue || SomenteDisponiveis;

    /// <summary>Parâmetros de rota para paginação (mantém os filtros).</summary>
    public Dictionary<string, string> ParaRota()
    {
        var rota = new Dictionary<string, string>();
        if (!string.IsNullOrWhiteSpace(Termo)) rota[nameof(Termo)] = Termo;
        if (!string.IsNullOrWhiteSpace(Montadora)) rota[nameof(Montadora)] = Montadora;
        if (!string.IsNullOrWhiteSpace(Modelo)) rota[nameof(Modelo)] = Modelo;
        if (Ano.HasValue) rota[nameof(Ano)] = Ano.Value.ToString();
        if (Categoria.HasValue) rota[nameof(Categoria)] = ((int)Categoria.Value).ToString();
        if (FabricanteId.HasValue) rota[nameof(FabricanteId)] = FabricanteId.Value.ToString();
        if (SomenteDisponiveis) rota[nameof(SomenteDisponiveis)] = "true";
        if (Ordenacao != OrdenacaoCatalogo.Relevancia) rota[nameof(Ordenacao)] = Ordenacao;
        return rota;
    }
}

public class PecaCardViewModel
{
    public int Id { get; init; }
    public string Nome { get; init; } = string.Empty;
    public string CodigoOEM { get; init; } = string.Empty;
    public string Sku { get; init; } = string.Empty;
    public CategoriaPeca Categoria { get; init; }
    public string Fabricante { get; init; } = string.Empty;
    public bool FabricanteHomologado { get; init; }
    public string Montadora { get; init; } = string.Empty;
    public string Modelo { get; init; } = string.Empty;
    public int AnoInicial { get; init; }
    public int AnoFinal { get; init; }
    public string? Motorizacao { get; init; }
    public decimal PrecoConcessionaria { get; init; }
    public decimal PrecoFabrica { get; init; }
    public int Estoque { get; init; }
    public StatusPeca Status { get; init; }
    public int GarantiaMeses { get; init; }
    public string? ImagemUrl { get; set; }
    public int TotalVendidos { get; init; }
    public string? NumeroLote { get; init; }

    public decimal Economia => CalculadoraEconomia.Valor(PrecoConcessionaria, PrecoFabrica);
    public decimal PercentualEconomia => CalculadoraEconomia.Percentual(PrecoConcessionaria, PrecoFabrica);
    public bool Disponivel => Status == StatusPeca.Disponivel && Estoque > 0;
    public string Compatibilidade => UiHelper.Compatibilidade(Montadora, Modelo, AnoInicial, AnoFinal, Motorizacao);
}

public sealed class PecaDetalhesViewModel : PecaCardViewModel
{
    public string? DescricaoTecnica { get; init; }
    public string PaisOrigem { get; init; } = string.Empty;
    public string? CertificacaoISO { get; init; }
    public IReadOnlyList<PecaCardViewModel> Relacionadas { get; set; } = [];
}

public sealed record OpcaoViewModel(int Id, string Nome);

public sealed class ResultadoPaginado<T>
{
    public IReadOnlyList<T> Itens { get; init; } = [];
    public int Pagina { get; init; } = 1;
    public int TamanhoPagina { get; init; }
    public int Total { get; init; }

    public int TotalPaginas => TamanhoPagina <= 0 ? 1 : Math.Max(1, (int)Math.Ceiling(Total / (double)TamanhoPagina));
    public bool TemAnterior => Pagina > 1;
    public bool TemProxima => Pagina < TotalPaginas;
}

public sealed record PaginacaoViewModel(int Pagina, int TotalPaginas, string Controller, string Action, Dictionary<string, string> Rota);

public sealed class CatalogoIndexViewModel
{
    public CatalogoFiltroViewModel Filtro { get; init; } = new();
    public ResultadoPaginado<PecaCardViewModel> Resultado { get; init; } = new();
    public IReadOnlyList<string> Montadoras { get; init; } = [];
    public IReadOnlyList<string> Modelos { get; init; } = [];
    public IReadOnlyList<int> Anos { get; init; } = [];
    public IReadOnlyList<OpcaoViewModel> Fabricantes { get; init; } = [];
}

public sealed class HomeViewModel
{
    public IReadOnlyList<PecaCardViewModel> Destaques { get; init; } = [];
    public IReadOnlyList<string> Montadoras { get; init; } = [];
    public IReadOnlyList<int> Anos { get; init; } = [];
    public IReadOnlyList<FabricanteVitrineViewModel> Fabricantes { get; init; } = [];
    public EstatisticasHistoricoViewModel Estatisticas { get; init; } = new();
    public IReadOnlyList<HistoricoEntregaViewModel> UltimasEntregas { get; init; } = [];
}

public sealed record FabricanteVitrineViewModel(int Id, string Nome, string PaisOrigem, string? CertificacaoISO, bool Homologado, int TotalPecas);
