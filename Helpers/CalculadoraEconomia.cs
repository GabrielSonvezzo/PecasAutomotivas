namespace PecasAutomotivas.Helpers;

/// <summary>Regra única de economia "direto da fábrica" x "tabela de concessionária".</summary>
public static class CalculadoraEconomia
{
    public static decimal Valor(decimal precoConcessionaria, decimal precoFabrica)
        => precoConcessionaria > precoFabrica ? precoConcessionaria - precoFabrica : 0m;

    public static decimal Percentual(decimal precoConcessionaria, decimal precoFabrica)
        => precoConcessionaria <= 0m || precoFabrica >= precoConcessionaria
            ? 0m
            : Math.Round((precoConcessionaria - precoFabrica) / precoConcessionaria * 100m, 1, MidpointRounding.AwayFromZero);
}
