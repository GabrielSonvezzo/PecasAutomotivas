using System.Globalization;

namespace PecasAutomotivas.Helpers;

/// <summary>Toda exibição de moeda/data em pt-BR passa por aqui (o binding permanece em cultura invariante).</summary>
public static class Formatacao
{
    public static readonly CultureInfo PtBr = CultureInfo.GetCultureInfo("pt-BR");
    private static readonly TimeZoneInfo Brasilia = ObterFusoBrasilia();

    public static string Moeda(decimal valor) => valor.ToString("C", PtBr);

    public static string Inteiro(int valor) => valor.ToString("N0", PtBr);

    public static string Percentual(decimal valor) => valor.ToString("0.#", PtBr) + "%";

    public static string Nota(double valor) => valor.ToString("0.0", PtBr);

    public static string NumeroPedido(int id) => $"#{id:D6}";

    public static DateTime ParaBrasilia(DateTime utc)
        => TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), Brasilia);

    public static string Data(DateTime? utc) => utc is null ? "—" : ParaBrasilia(utc.Value).ToString("dd/MM/yyyy", PtBr);

    public static string DataHora(DateTime? utc) => utc is null ? "—" : ParaBrasilia(utc.Value).ToString("dd/MM/yyyy HH:mm", PtBr);

    public static string MesAno(DateTime? utc) => utc is null ? "—" : ParaBrasilia(utc.Value).ToString("MMMM 'de' yyyy", PtBr);

    private static TimeZoneInfo ObterFusoBrasilia()
    {
        foreach (var id in new[] { "America/Sao_Paulo", "E. South America Standard Time" })
        {
            try { return TimeZoneInfo.FindSystemTimeZoneById(id); }
            catch (TimeZoneNotFoundException) { }
            catch (InvalidTimeZoneException) { }
        }
        return TimeZoneInfo.CreateCustomTimeZone("BRT", TimeSpan.FromHours(-3), "Brasília", "Brasília");
    }
}
