using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using PecasAutomotivas.Models;

namespace PecasAutomotivas.ViewModels;

public sealed class PecaFormViewModel : IValidatableObject
{
    public int Id { get; set; }

    [Required(ErrorMessage = "Informe o nome da peça.")]
    [StringLength(150, MinimumLength = 3)]
    [Display(Name = "Nome da peça")]
    public string Nome { get; set; } = string.Empty;

    [Required(ErrorMessage = "Informe o código OEM / part number.")]
    [StringLength(40)]
    [RegularExpression(@"^[A-Za-z0-9\-\. /]+$", ErrorMessage = "Use apenas letras, números, espaço, ponto, barra e hífen.")]
    [Display(Name = "Código OEM / Part number")]
    public string CodigoOEM_PartNumber { get; set; } = string.Empty;

    [Required(ErrorMessage = "Informe o SKU da fábrica.")]
    [StringLength(40)]
    [RegularExpression(@"^[A-Za-z0-9\-\._/]+$", ErrorMessage = "SKU inválido.")]
    [Display(Name = "SKU da fábrica")]
    public string SKU_Fabrica { get; set; } = string.Empty;

    [Required(ErrorMessage = "Selecione a categoria.")]
    [Display(Name = "Categoria")]
    public CategoriaPeca? Categoria { get; set; }

    [Required(ErrorMessage = "Selecione o fabricante.")]
    [Range(1, int.MaxValue, ErrorMessage = "Selecione o fabricante.")]
    [Display(Name = "Fabricante")]
    public int FabricanteFabricaId { get; set; }

    [Required(ErrorMessage = "Informe a montadora.")]
    [StringLength(60)]
    [Display(Name = "Montadora compatível")]
    public string MontadoraCompativel { get; set; } = string.Empty;

    [Required(ErrorMessage = "Informe o modelo.")]
    [StringLength(80)]
    [Display(Name = "Modelo compatível")]
    public string ModeloVeiculoCompativel { get; set; } = string.Empty;

    [Range(1950, 2100, ErrorMessage = "Ano inválido.")]
    [Display(Name = "Ano inicial")]
    public int AnoInicial { get; set; } = DateTime.UtcNow.Year - 5;

    [Range(1950, 2100, ErrorMessage = "Ano inválido.")]
    [Display(Name = "Ano final")]
    public int AnoFinal { get; set; } = DateTime.UtcNow.Year;

    [StringLength(60)]
    [Display(Name = "Motorização")]
    public string? Motorizacao { get; set; }

    [Range(0.01, 9_999_999.99, ErrorMessage = "Preço inválido.")]
    [Display(Name = "Preço de tabela (concessionária)")]
    public decimal PrecoTabelaConcessionaria { get; set; }

    [Range(0.01, 9_999_999.99, ErrorMessage = "Preço inválido.")]
    [Display(Name = "Preço direto da fábrica")]
    public decimal PrecoDiretoFabrica { get; set; }

    [Range(0, 1_000_000, ErrorMessage = "Quantidade inválida.")]
    [Display(Name = "Quantidade em estoque")]
    public int QuantidadeEstoque { get; set; }

    [StringLength(40)]
    [RegularExpression(@"^[A-Za-z0-9\-\._/]*$", ErrorMessage = "Lote inválido.")]
    [Display(Name = "Número do lote de fábrica")]
    public string? NumeroLoteFabrica { get; set; }

    [Range(0, 120, ErrorMessage = "Garantia entre 0 e 120 meses.")]
    [Display(Name = "Garantia (meses)")]
    public int GarantiaMeses { get; set; } = 12;

    [StringLength(4000)]
    [Display(Name = "Descrição técnica")]
    public string? DescricaoTecnica { get; set; }

    [Display(Name = "Foto da peça (.jpg, .png ou .webp até 5 MB)")]
    public IFormFile? Imagem { get; set; }

    [BindNever]
    public string? ImagemUrlAtual { get; set; }

    [BindNever]
    public IReadOnlyList<OpcaoViewModel> Fabricantes { get; set; } = [];

    public bool Edicao => Id > 0;

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (AnoFinal < AnoInicial)
            yield return new ValidationResult("O ano final não pode ser menor que o inicial.", [nameof(AnoFinal)]);

        if (PrecoDiretoFabrica > PrecoTabelaConcessionaria)
            yield return new ValidationResult("O preço direto da fábrica não pode ser maior que o de concessionária.", [nameof(PrecoDiretoFabrica)]);
    }
}

public sealed class FabricanteFormViewModel
{
    public int Id { get; set; }

    [Required(ErrorMessage = "Informe o nome da fábrica.")]
    [StringLength(100, MinimumLength = 2)]
    [Display(Name = "Nome da fábrica")]
    public string NomeFabrica { get; set; } = string.Empty;

    [Required(ErrorMessage = "Informe o país de origem.")]
    [StringLength(60, MinimumLength = 2)]
    [Display(Name = "País de origem")]
    public string PaisOrigem { get; set; } = string.Empty;

    [StringLength(60)]
    [Display(Name = "Certificação ISO / IATF")]
    public string? CertificacaoISO { get; set; }

    [Display(Name = "Fábrica homologada (exibe selo no catálogo)")]
    public bool SeloHomologado { get; set; }

    public bool Edicao => Id > 0;
}

public sealed class NovoLoteViewModel
{
    [Range(1, int.MaxValue)]
    public int PecaId { get; set; }

    [Range(1, 100_000, ErrorMessage = "Informe de 1 a 100.000 unidades.")]
    public int Quantidade { get; set; }

    [Required(ErrorMessage = "Informe o número do lote.")]
    [StringLength(40)]
    [RegularExpression(@"^[A-Za-z0-9\-\._/]+$", ErrorMessage = "Lote inválido.")]
    public string NumeroLote { get; set; } = string.Empty;
}
