using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using PecasAutomotivas.Configuration;
using PecasAutomotivas.Models;

namespace PecasAutomotivas.Services;

public sealed record TokenGerado(string AccessToken, DateTime ExpiraEmUtc);

public interface ITokenService
{
    TokenGerado GerarToken(ApplicationUser usuario, IEnumerable<string> roles);
}

public sealed class TokenService(IOptions<JwtOptions> opcoes) : ITokenService
{
    private readonly JwtOptions _jwt = opcoes.Value;

    public TokenGerado GerarToken(ApplicationUser usuario, IEnumerable<string> roles)
    {
        var expira = DateTime.UtcNow.AddMinutes(_jwt.ExpiracaoMinutos);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, usuario.Id),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new(ClaimTypes.NameIdentifier, usuario.Id),
            new(ClaimTypes.Name, usuario.Email ?? usuario.UserName ?? usuario.Id),
            new(JwtRegisteredClaimNames.Email, usuario.Email ?? string.Empty),
            new("nome", usuario.NomeCompleto)
        };
        claims.AddRange(roles.Select(r => new Claim(ClaimTypes.Role, r)));

        var credenciais = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwt.Key)),
            SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: _jwt.Issuer,
            audience: _jwt.Audience,
            claims: claims,
            notBefore: DateTime.UtcNow,
            expires: expira,
            signingCredentials: credenciais);

        return new TokenGerado(new JwtSecurityTokenHandler().WriteToken(token), expira);
    }
}
