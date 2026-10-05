using System.ComponentModel.DataAnnotations;
using PecasAutomotivas.Models;

namespace PecasAutomotivas.ViewModels;

public sealed class DashboardAdminViewModel
{
    public decimal Faturamento { get; init; }
    public decimal FaturamentoMes { get; init; }
    public decimal EconomiaGeradaClientes { get; init; }
    public int TotalPecasEstoque { get; init; }
    public int TotalPecasVendidas { get; init; }
    public int PedidosPendentes { get; init; }
    public int PecasCadastradas { get; init; }
    public int PecasEsgotadas { get; init; }
    public IReadOnlyList<AdminPedidoLinhaViewModel> PedidosEmAberto { get; init; } = [];
    public IReadOnlyList<EstoqueBaixoViewModel> EstoqueBaixo { get; init; } = [];
    public IReadOnlyList<VendasCategoriaViewModel> VendasPorCategoria { get; init; } = [];
}

public sealed class AdminPedidoLinhaViewModel
{
    public int Id { get; init; }
    public string Cliente { get; init; } = string.Empty;
    public string? Email { get; init; }
    public string Cidade { get; init; } = string.Empty;
    public string Estado { get; init; } = string.Empty;
    public decimal ValorTotal { get; init; }
    public StatusPedido Status { get; init; }
    public DateTime DataPedido { get; init; }
    public DateTime? DataEntrega { get; init; }
    public int QuantidadeItens { get; init; }
    public string? CodigoRastreio { get; init; }
    public string? Veiculo { get; init; }
}

public sealed class EstoqueBaixoViewModel
{
    public int Id { get; init; }
    public string Nome { get; init; } = string.Empty;
    public string CodigoOEM { get; init; } = string.Empty;
    public int Estoque { get; init; }
    public string? NumeroLote { get; init; }
}

public sealed record VendasCategoriaViewModel(CategoriaPeca Categoria, int Quantidade);

public sealed class AdminPecaLinhaViewModel
{
    public int Id { get; init; }
    public string Nome { get; init; } = string.Empty;
    public string CodigoOEM { get; init; } = string.Empty;
    public string Fabricante { get; init; } = string.Empty;
    public CategoriaPeca Categoria { get; init; }
    public decimal PrecoFabrica { get; init; }
    public decimal PrecoConcessionaria { get; init; }
    public int Estoque { get; init; }
    public int Vendidos { get; init; }
    public StatusPeca Status { get; init; }
    public string? NumeroLote { get; init; }
    public string? ImagemUrl { get; init; }
}

public sealed class AdminPecasViewModel
{
    public string? Termo { get; init; }
    public ResultadoPaginado<AdminPecaLinhaViewModel> Resultado { get; init; } = new();
}

public sealed class AdminFabricanteLinhaViewModel
{
    public int Id { get; init; }
    public string NomeFabrica { get; init; } = string.Empty;
    public string PaisOrigem { get; init; } = string.Empty;
    public string? CertificacaoISO { get; init; }
    public bool SeloHomologado { get; init; }
    public int TotalPecas { get; init; }
}

public sealed class AdminPedidosViewModel
{
    public StatusPedido? Status { get; init; }
    public ResultadoPaginado<AdminPedidoLinhaViewModel> Resultado { get; init; } = new();
}

public sealed class AtualizarStatusPedidoViewModel
{
    [Range(1, int.MaxValue)]
    public int PedidoId { get; set; }

    [Required]
    public StatusPedido? NovoStatus { get; set; }

    [StringLength(40)]
    [RegularExpression(@"^[A-Za-z0-9\-]*$", ErrorMessage = "Código de rastreio inválido.")]
    public string? CodigoRastreio { get; set; }

    [StringLength(500)]
    public string? ReturnUrl { get; set; }
}
