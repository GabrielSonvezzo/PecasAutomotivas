namespace PecasAutomotivas.Helpers;

/// <summary>Validação e formatação de CPF/CNPJ com cálculo real dos dígitos verificadores.</summary>
public static class Documento
{
    private static readonly int[] PesosCnpj1 = [5, 4, 3, 2, 9, 8, 7, 6, 5, 4, 3, 2];
    private static readonly int[] PesosCnpj2 = [6, 5, 4, 3, 2, 9, 8, 7, 6, 5, 4, 3, 2];

    public static string SomenteDigitos(string? valor)
        => string.IsNullOrEmpty(valor) ? string.Empty : new string(valor.Where(char.IsAsciiDigit).ToArray());

    public static bool CpfOuCnpjValido(string? valor)
    {
        var d = SomenteDigitos(valor);
        return d.Length switch
        {
            11 => CpfValido(d),
            14 => CnpjValido(d),
            _ => false
        };
    }

    public static bool CpfValido(string? valor)
    {
        var d = SomenteDigitos(valor);
        if (d.Length != 11 || d.Distinct().Count() == 1) return false;
        return d[9..] == CalcularDigitosCpf(d[..9]);
    }

    public static bool CnpjValido(string? valor)
    {
        var d = SomenteDigitos(valor);
        if (d.Length != 14 || d.Distinct().Count() == 1) return false;

        var dv1 = DigitoModulo11(d[..12], PesosCnpj1);
        var dv2 = DigitoModulo11(d[..12] + dv1, PesosCnpj2);
        return d[12] - '0' == dv1 && d[13] - '0' == dv2;
    }

    /// <summary>Recebe os 9 primeiros dígitos e devolve os 2 verificadores.</summary>
    public static string CalcularDigitosCpf(string noveDigitos)
    {
        var soma = 0;
        for (var i = 0; i < 9; i++) soma += (noveDigitos[i] - '0') * (10 - i);
        var resto = soma % 11;
        var dv1 = resto < 2 ? 0 : 11 - resto;

        var dez = noveDigitos + dv1;
        soma = 0;
        for (var i = 0; i < 10; i++) soma += (dez[i] - '0') * (11 - i);
        resto = soma % 11;
        var dv2 = resto < 2 ? 0 : 11 - resto;

        return $"{dv1}{dv2}";
    }

    public static string Formatar(string? valor)
    {
        var d = SomenteDigitos(valor);
        return d.Length switch
        {
            11 => $"{d[..3]}.{d[3..6]}.{d[6..9]}-{d[9..]}",
            14 => $"{d[..2]}.{d[2..5]}.{d[5..8]}/{d[8..12]}-{d[12..]}",
            _ => d
        };
    }

    /// <summary>"Carlos Eduardo Mendes" → "Carlos M." (exibição pública mínima, LGPD).</summary>
    public static string MascararNomePublico(string? nomeCompleto)
    {
        if (string.IsNullOrWhiteSpace(nomeCompleto)) return "Cliente";
        var partes = nomeCompleto.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return partes.Length == 1 ? partes[0] : $"{partes[0]} {char.ToUpperInvariant(partes[^1][0])}.";
    }

    private static int DigitoModulo11(string digitos, int[] pesos)
    {
        var soma = 0;
        for (var i = 0; i < pesos.Length; i++) soma += (digitos[i] - '0') * pesos[i];
        var resto = soma % 11;
        return resto < 2 ? 0 : 11 - resto;
    }
}
