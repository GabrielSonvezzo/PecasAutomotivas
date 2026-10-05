using System.ComponentModel.DataAnnotations;
using PecasAutomotivas.Helpers;

namespace PecasAutomotivas.ViewModels;

public sealed class LoginViewModel
{
    [Required(ErrorMessage = "Informe o e-mail.")]
    [EmailAddress(ErrorMessage = "E-mail inválido.")]
    [StringLength(256)]
    [Display(Name = "E-mail")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Informe a senha.")]
    [DataType(DataType.Password)]
    [StringLength(100)]
    [Display(Name = "Senha")]
    public string Senha { get; set; } = string.Empty;

    [Display(Name = "Manter conectado neste dispositivo")]
    public bool LembrarMe { get; set; }

    [StringLength(500)]
    public string? ReturnUrl { get; set; }
}

public sealed class RegisterViewModel
{
    [Required(ErrorMessage = "Informe seu nome completo.")]
    [StringLength(150, MinimumLength = 5, ErrorMessage = "O nome deve ter entre 5 e 150 caracteres.")]
    [RegularExpression(@"^[\p{L}][\p{L} '\.\-]+$", ErrorMessage = "Use apenas letras no nome.")]
    [Display(Name = "Nome completo ou razão social")]
    public string NomeCompleto { get; set; } = string.Empty;

    [Required(ErrorMessage = "Informe o e-mail.")]
    [EmailAddress(ErrorMessage = "E-mail inválido.")]
    [StringLength(256)]
    [Display(Name = "E-mail")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Informe o CPF ou CNPJ.")]
    [RegularExpression(@"^(\d{3}\.?\d{3}\.?\d{3}-?\d{2}|\d{2}\.?\d{3}\.?\d{3}/?\d{4}-?\d{2})$", ErrorMessage = "Formato de CPF/CNPJ inválido.")]
    [CpfCnpj]
    [Display(Name = "CPF ou CNPJ")]
    public string CPF_CNPJ { get; set; } = string.Empty;

    [Required(ErrorMessage = "Informe o telefone.")]
    [RegularExpression(@"^\(?\d{2}\)?\s?9?\d{4}-?\d{4}$", ErrorMessage = "Use o formato (41) 99999-9999.")]
    [StringLength(20)]
    [Display(Name = "Telefone / WhatsApp")]
    public string Telefone { get; set; } = string.Empty;

    [Required(ErrorMessage = "Informe a cidade.")]
    [StringLength(100, MinimumLength = 2)]
    [Display(Name = "Cidade")]
    public string Cidade { get; set; } = string.Empty;

    [Required(ErrorMessage = "Selecione o estado.")]
    [RegularExpression(Estados.RegexUf, ErrorMessage = "UF inválida.")]
    [Display(Name = "Estado")]
    public string Estado { get; set; } = string.Empty;

    [Required(ErrorMessage = "Crie uma senha.")]
    [StringLength(100, MinimumLength = 10, ErrorMessage = "A senha deve ter pelo menos 10 caracteres.")]
    [DataType(DataType.Password)]
    [Display(Name = "Senha")]
    public string Senha { get; set; } = string.Empty;

    [Required(ErrorMessage = "Confirme a senha.")]
    [DataType(DataType.Password)]
    [Compare(nameof(Senha), ErrorMessage = "As senhas não conferem.")]
    [Display(Name = "Confirmar senha")]
    public string ConfirmarSenha { get; set; } = string.Empty;

    [Range(typeof(bool), "true", "true", ErrorMessage = "É preciso aceitar os termos de uso e a política de privacidade.")]
    [Display(Name = "Li e aceito os termos de uso e a política de privacidade")]
    public bool AceitoTermos { get; set; }
}
