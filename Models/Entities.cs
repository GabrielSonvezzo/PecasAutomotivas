using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.AspNetCore.Identity;
using PecasAutomotivas.Helpers;

namespace PecasAutomotivas.Models;

public class ApplicationUser : IdentityUser
{
    [Required, StringLength(150)]
    public string NomeCompleto { get; set; } = string.Empty;

    /// <summary>Armazenado somente com dígitos: 11 (CPF) ou 14 (CNPJ).</summary>
    [Required, StringLength(14, MinimumLength = 11)]
    [RegularExpression(@"^(\d{11}|\d{14})$")]
    public string CPF_CNPJ { get; set; } = string.Empty;

    [Required, StringLength(20)]
    public string Telefone { get; set; } = string.Empty;

    [Required, StringLength(100)]
    public string Cidade { get; set; } = string.Empty;

    [Required, StringLength(2, MinimumLength = 2)]
    public string Estado { get; set; } = string.Empty;

    public DateTime DataCadastro { get; set; } = DateTime.UtcNow;

    public ICollection<Pedido> Pedidos { get; set; } = new List<Pedido>();
}

public class FabricanteFabrica
{
    public int Id { get; set; }

    [Required, StringLength(100)]
    public string NomeFabrica { get; set; } = string.Empty;

    [Required, StringLength(60)]
    public string PaisOrigem { get; set; } = string.Empty;

    [StringLength(60)]
    public string? CertificacaoISO { get; set; }

    public bool SeloHomologado { get; set; }

    public DateTime DataCadastro { get; set; } = DateTime.UtcNow;

    public ICollection<Peca> Pecas { get; set; } = new List<Peca>();
}

public class Peca
{
    public int Id { get; set; }

    [Required, StringLength(150)]
    public string Nome { get; set; } = string.Empty;

    [Required, StringLength(40)]
    [RegularExpression(@"^[A-Z0-9\-\. /]+$")]
    public string CodigoOEM_PartNumber { get; set; } = string.Empty;

    [Required, StringLength(40)]
    public string SKU_Fabrica { get; set; } = string.Empty;

    [Required]
    public CategoriaPeca Categoria { get; set; }

    [Required, StringLength(60)]
    public string MontadoraCompativel { get; set; } = string.Empty;

    [Required, StringLength(80)]
    public string ModeloVeiculoCompativel { get; set; } = string.Empty;

    [Range(1950, 2100)]
    public int AnoInicial { get; set; }

    [Range(1950, 2100)]
    public int AnoFinal { get; set; }

    [StringLength(60)]
    public string? Motorizacao { get; set; }

    [Range(0.01, 9_999_999.99)]
    public decimal PrecoTabelaConcessionaria { get; set; }

    [Range(0.01, 9_999_999.99)]
    public decimal PrecoDiretoFabrica { get; set; }

    [Range(0, 1_000_000)]
    public int QuantidadeEstoque { get; set; }

    [StringLength(40)]
    public string? NumeroLoteFabrica { get; set; }

    [Range(0, 120)]
    public int GarantiaMeses { get; set; }

    [StringLength(4000)]
    public string? DescricaoTecnica { get; set; }

    [StringLength(300)]
    public string? ImagemUrl { get; set; }

    public StatusPeca Status { get; set; } = StatusPeca.Disponivel;

    [Range(0, int.MaxValue)]
    public int TotalVendidos { get; set; }

    public DateTime DataCadastro { get; set; } = DateTime.UtcNow;
    public DateTime? DataAtualizacao { get; set; }

    public int FabricanteFabricaId { get; set; }
    public FabricanteFabrica? Fabricante { get; set; }

    public ICollection<ItemPedido> ItensPedido { get; set; } = new List<ItemPedido>();

    [NotMapped]
    public decimal EconomiaUnitaria => CalculadoraEconomia.Valor(PrecoTabelaConcessionaria, PrecoDiretoFabrica);

    [NotMapped]
    public decimal PercentualEconomia => CalculadoraEconomia.Percentual(PrecoTabelaConcessionaria, PrecoDiretoFabrica);

    /// <summary>O status é sempre derivado do estoque para nunca vender o que não existe.</summary>
    public void AtualizarStatusPorEstoque()
        => Status = QuantidadeEstoque > 0 ? StatusPeca.Disponivel : StatusPeca.Esgotado;
}

public class Pedido
{
    public int Id { get; set; }

    [Required, StringLength(450)]
    public string UsuarioId { get; set; } = string.Empty;
    public ApplicationUser? Usuario { get; set; }

    [StringLength(40)]
    public string? CodigoRastreio { get; set; }

    public decimal ValorTotal { get; set; }
    public decimal EconomiaGerada { get; set; }

    public StatusPedido StatusPedido { get; set; } = StatusPedido.Pendente;

    public DateTime DataPedido { get; set; } = DateTime.UtcNow;
    public DateTime? DataEntrega { get; set; }

    [Range(1, 5)]
    public int? AvaliacaoCliente { get; set; }

    [StringLength(1000)]
    public string? DepoimentoCliente { get; set; }

    public DateTime? DataAvaliacao { get; set; }

    /// <summary>LGPD: consentimento explícito para exibir primeiro nome + depoimento no histórico público.</summary>
    public bool AutorizaPublicacao { get; set; }

    [StringLength(120)]
    public string? VeiculoAplicado { get; set; }

    [Required, StringLength(100)]
    public string CidadeEntrega { get; set; } = string.Empty;

    [Required, StringLength(2)]
    public string EstadoEntrega { get; set; } = string.Empty;

    /// <summary>Marcado pelo botão "Confirmar entrega" do painel: só pedidos reais entram no histórico.</summary>
    public bool PublicadoNoHistorico { get; set; }

    public ICollection<ItemPedido> Itens { get; set; } = new List<ItemPedido>();
}

public class ItemPedido
{
    public int Id { get; set; }

    public int PedidoId { get; set; }
    public Pedido? Pedido { get; set; }

    public int PecaId { get; set; }
    public Peca? Peca { get; set; }

    [Range(1, 999)]
    public int Quantidade { get; set; }

    /// <summary>Preço congelado no momento da compra (nunca vem do cliente).</summary>
    public decimal PrecoUnitario { get; set; }

    public decimal PrecoConcessionariaUnitario { get; set; }

    [NotMapped]
    public decimal Subtotal => PrecoUnitario * Quantidade;
}
