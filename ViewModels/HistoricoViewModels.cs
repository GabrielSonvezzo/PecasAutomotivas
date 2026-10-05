using PecasAutomotivas.Models;

namespace PecasAutomotivas.ViewModels;

public sealed class EstatisticasHistoricoViewModel
{
    public int PedidosEntregues { get; init; }
    public int PecasDespachadas { get; init; }
    public decimal EconomiaGerada { get; init; }
    public double MediaAvaliacao { get; init; }
    public int TotalAvaliacoes { get; init; }
    public int CidadesAtendidas { get; init; }
    public int FabricantesAtendidos { get; init; }
}

public sealed class HistoricoItemViewModel
{
    public string Nome { get; init; } = string.Empty;
    public string CodigoOEM { get; init; } = string.Empty;
    public string Fabricante { get; init; } = string.Empty;
    public CategoriaPeca Categoria { get; init; }
    public int Quantidade { get; init; }
}

public sealed class HistoricoEntregaViewModel
{
    public int PedidoId { get; init; }
    public DateTime DataEntrega { get; init; }
    public string Cidade { get; init; } = string.Empty;
    public string Estado { get; init; } = string.Empty;
    public string? Veiculo { get; init; }
    public int? Nota { get; init; }
    public string? Depoimento { get; init; }
    public string? ClientePublico { get; init; }
    public decimal Economia { get; init; }
    public IReadOnlyList<HistoricoItemViewModel> Itens { get; init; } = [];
}

public sealed class HistoricoViewModel
{
    public EstatisticasHistoricoViewModel Estatisticas { get; init; } = new();
    public ResultadoPaginado<HistoricoEntregaViewModel> Entregas { get; init; } = new();
}
