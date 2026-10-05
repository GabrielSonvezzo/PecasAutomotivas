namespace PecasAutomotivas.Configuration;

public sealed class JwtOptions
{
    public const string Secao = "Jwt";

    public string Issuer { get; set; } = string.Empty;
    public string Audience { get; set; } = string.Empty;
    public string Key { get; set; } = string.Empty;
    public int ExpiracaoMinutos { get; set; } = 60;
}

public sealed class UploadOptions
{
    public const string Secao = "Upload";

    public long TamanhoMaximoBytes { get; set; } = 5 * 1024 * 1024;
    public string Pasta { get; set; } = "uploads/pecas";
}

public sealed class SeedOptions
{
    public const string Secao = "Seed";

    public string AdminEmail { get; set; } = "admin@pecasautomotivas.com.br";
    public string AdminPassword { get; set; } = string.Empty;
    public bool DadosDemonstracao { get; set; }
}

public static class PoliticasRateLimit
{
    public const string Login = "login";
    public const string Api = "api";
}
