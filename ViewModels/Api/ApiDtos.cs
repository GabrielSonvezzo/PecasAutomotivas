using System.ComponentModel.DataAnnotations;
using PecasAutomotivas.Helpers;

namespace PecasAutomotivas.ViewModels.Api;

public sealed class LoginApiRequest
{
    [Required, EmailAddress, StringLength(256)]
    public string Email { get; set; } = string.Empty;

    [Required, StringLength(100)]
    public string Senha { get; set; } = string.Empty;
}

public sealed record TokenApiResponse(
    string AccessToken,
    string TokenType,
    DateTime ExpiraEmUtc,
    string Nome,
    string Email,
    IReadOnlyList<string> Roles);

public sealed class ItemPedidoApiRequest
{
    [Range(1, int.MaxValue)]
    public int PecaId { get; set; }

    [Range(1, 99)]
    public int Quantidade { get; set; }
}

public sealed class CriarPedidoApiRequest
{
    [Required, MinLength(1), MaxLength(20)]
    public List<ItemPedidoApiRequest> Itens { get; set; } = [];

    [StringLength(120)]
    public string? VeiculoAplicado { get; set; }

    [Required, StringLength(100, MinimumLength = 2)]
    public string Cidade { get; set; } = string.Empty;

    [Required, RegularExpression(Estados.RegexUf)]
    public string Estado { get; set; } = string.Empty;
}

public sealed record PerfilApiResponse(string Id, string Nome, string Email, string Telefone, string Cidade, string Estado, IReadOnlyList<string> Roles);
