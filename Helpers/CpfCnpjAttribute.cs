using System.ComponentModel.DataAnnotations;

namespace PecasAutomotivas.Helpers;

/// <summary>Valida CPF ou CNPJ pelos dígitos verificadores (aceita com ou sem máscara).</summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Parameter)]
public sealed class CpfCnpjAttribute : ValidationAttribute
{
    public CpfCnpjAttribute() : base("Informe um CPF ou CNPJ válido.") { }

    public override bool IsValid(object? value)
        => value is null || (value is string s && (string.IsNullOrWhiteSpace(s) || Documento.CpfOuCnpjValido(s)));
}
