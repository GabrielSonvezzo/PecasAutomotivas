using System.ComponentModel.DataAnnotations;

namespace PecasAutomotivas.Models;

public enum CategoriaPeca
{
    [Display(Name = "Motor")] Motor = 1,
    [Display(Name = "Freios")] Freios = 2,
    [Display(Name = "Suspensão")] Suspensao = 3,
    [Display(Name = "Elétrica")] Eletrica = 4,
    [Display(Name = "Transmissão")] Transmissao = 5,
    [Display(Name = "Carroceria")] Carroceria = 6
}

public enum StatusPeca
{
    [Display(Name = "Disponível")] Disponivel = 1,
    [Display(Name = "Esgotado")] Esgotado = 2
}

public enum StatusPedido
{
    [Display(Name = "Pendente")] Pendente = 1,
    [Display(Name = "Separação na fábrica")] SeparacaoFabrica = 2,
    [Display(Name = "Enviado")] Enviado = 3,
    [Display(Name = "Entregue")] Entregue = 4,
    [Display(Name = "Cancelado")] Cancelado = 5
}

public static class Roles
{
    public const string Admin = "Admin";
    public const string Cliente = "Cliente";

    public static readonly string[] Todas = [Admin, Cliente];
}
