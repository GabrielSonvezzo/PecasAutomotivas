using System.ComponentModel.DataAnnotations;
using System.Reflection;
using PecasAutomotivas.Models;

namespace PecasAutomotivas.Helpers;

/// <summary>
/// Classes Tailwind usadas dinamicamente. Este arquivo está no "content" do tailwind.config.js
/// para que as classes sejam incluídas no CSS compilado.
/// </summary>
public static class UiHelper
{
    public static string NomeExibicao(this Enum valor)
        => valor.GetType().GetMember(valor.ToString()).FirstOrDefault()
               ?.GetCustomAttribute<DisplayAttribute>()?.GetName()
           ?? valor.ToString();

    public static string ClasseStatusPedido(StatusPedido status) => status switch
    {
        StatusPedido.Pendente => "bg-amber-400/10 text-amber-300 ring-amber-400/30",
        StatusPedido.SeparacaoFabrica => "bg-sky-400/10 text-sky-300 ring-sky-400/30",
        StatusPedido.Enviado => "bg-violet-400/10 text-violet-300 ring-violet-400/30",
        StatusPedido.Entregue => "bg-emerald-400/10 text-emerald-300 ring-emerald-400/30",
        StatusPedido.Cancelado => "bg-red-400/10 text-red-300 ring-red-400/30",
        _ => "bg-white/5 text-tecnico ring-white/10"
    };

    public static string IconeCategoria(CategoriaPeca categoria) => categoria switch
    {
        CategoriaPeca.Motor => "cog",
        CategoriaPeca.Freios => "disc-3",
        CategoriaPeca.Suspensao => "move-vertical",
        CategoriaPeca.Eletrica => "zap",
        CategoriaPeca.Transmissao => "settings-2",
        CategoriaPeca.Carroceria => "car-front",
        _ => "package"
    };

    public static string IconeStatusPedido(StatusPedido status) => status switch
    {
        StatusPedido.Pendente => "clock",
        StatusPedido.SeparacaoFabrica => "factory",
        StatusPedido.Enviado => "truck",
        StatusPedido.Entregue => "package-check",
        StatusPedido.Cancelado => "x-circle",
        _ => "circle"
    };

    public static string Compatibilidade(string montadora, string modelo, int anoInicial, int anoFinal, string? motorizacao)
    {
        var anos = anoInicial == anoFinal ? $"{anoInicial}" : $"{anoInicial}–{anoFinal}";
        return string.IsNullOrWhiteSpace(motorizacao)
            ? $"{montadora} {modelo} ({anos})"
            : $"{montadora} {modelo} {motorizacao} ({anos})";
    }
}
