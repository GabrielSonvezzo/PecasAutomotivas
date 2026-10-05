using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using PecasAutomotivas.Helpers;
using PecasAutomotivas.Models;

namespace PecasAutomotivas.ViewModels;

public sealed class CheckoutViewModel
{
    [Range(1, int.MaxValue)]
    public int PecaId { get; set; }

    [Range(1, 99, ErrorMessage = "Quantidade entre 1 e 99.")]
    [Display(Name = "Quantidade")]
    public int Quantidade { get; set; } = 1;

    [StringLength(120)]
    [Display(Name = "Veículo em que a peça será aplicada")]
    public string? VeiculoAplicado { get; set; }

    [Required(ErrorMessage = "Informe a cidade de entrega.")]
    [StringLength(100, MinimumLength = 2)]
    [Display(Name = "Cidade de entrega")]
    public string Cidade { get; set; } = string.Empty;

    [Required(ErrorMessage = "Selecione o estado.")]
    [RegularExpression(Estados.RegexUf, ErrorMessage = "UF inválida.")]
    [Display(Name = "Estado")]
    public string Estado { get; set; } = string.Empty;

    [BindNever]
    public PecaDetalhesViewModel? Peca { get; set; }
}

public sealed class PedidoResumoViewModel
{
    public int Id { get; init; }
    public DateTime DataPedido { get; init; }
    public DateTime? DataEntrega { get; init; }
    public StatusPedido Status { get; init; }
    public decimal ValorTotal { get; init; }
    public decimal Economia { get; init; }
    public int QuantidadeItens { get; init; }
    public string PrimeiraPeca { get; init; } = string.Empty;
    public string? CodigoRastreio { get; init; }
}

public sealed class ItemPedidoViewModel
{
    public int PecaId { get; init; }
    public string Nome { get; init; } = string.Empty;
    public string CodigoOEM { get; init; } = string.Empty;
    public string Fabricante { get; init; } = string.Empty;
    public string? NumeroLote { get; init; }
    public int Quantidade { get; init; }
    public decimal PrecoUnitario { get; init; }
    public decimal PrecoConcessionariaUnitario { get; init; }
    public decimal Subtotal => PrecoUnitario * Quantidade;
}

public sealed class PedidoDetalhesViewModel
{
    public int Id { get; init; }
    public DateTime DataPedido { get; init; }
    public DateTime? DataEntrega { get; init; }
    public StatusPedido Status { get; init; }
    public string? CodigoRastreio { get; init; }
    public decimal ValorTotal { get; init; }
    public decimal Economia { get; init; }
    public string? VeiculoAplicado { get; init; }
    public string Cidade { get; init; } = string.Empty;
    public string Estado { get; init; } = string.Empty;
    public int? Nota { get; init; }
    public string? Depoimento { get; init; }
    public bool AutorizaPublicacao { get; init; }
    public IReadOnlyList<ItemPedidoViewModel> Itens { get; init; } = [];

    public bool PodeAvaliar => Status == StatusPedido.Entregue && Nota is null;
}

public sealed class AvaliacaoViewModel
{
    [Range(1, int.MaxValue)]
    public int PedidoId { get; set; }

    [Required(ErrorMessage = "Escolha uma nota.")]
    [Range(1, 5, ErrorMessage = "A nota vai de 1 a 5.")]
    public int? Nota { get; set; }

    [StringLength(1000, MinimumLength = 10, ErrorMessage = "Escreva entre 10 e 1000 caracteres.")]
    [Display(Name = "Conte como foi a compra")]
    public string? Depoimento { get; set; }

    [Display(Name = "Autorizo exibir meu primeiro nome, cidade e depoimento no histórico público de entregas")]
    public bool AutorizaPublicacao { get; set; }
}
