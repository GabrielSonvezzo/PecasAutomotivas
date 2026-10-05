using Microsoft.Extensions.Options;
using PecasAutomotivas.Configuration;

namespace PecasAutomotivas.Services;

public interface IUploadImagemService
{
    Task<ResultadoOperacao<string>> SalvarAsync(IFormFile? arquivo, CancellationToken ct = default);
    void Remover(string? urlRelativa);
}

/// <summary>
/// Upload anti-RCE: tamanho máximo, whitelist de extensão, verificação da assinatura binária (magic bytes)
/// coerente com a extensão, nome aleatório (Guid) e gravação fora de qualquer pasta executável.
/// </summary>
public sealed class UploadImagemService(
    IWebHostEnvironment env,
    IOptions<UploadOptions> opcoes,
    ILogger<UploadImagemService> logger) : IUploadImagemService
{
    private static readonly Dictionary<string, string> ExtensoesPermitidas = new(StringComparer.OrdinalIgnoreCase)
    {
        [".jpg"] = ".jpg",
        [".jpeg"] = ".jpg",
        [".png"] = ".png",
        [".webp"] = ".webp"
    };

    private readonly UploadOptions _opcoes = opcoes.Value;

    public async Task<ResultadoOperacao<string>> SalvarAsync(IFormFile? arquivo, CancellationToken ct = default)
    {
        if (arquivo is null || arquivo.Length == 0)
            return ResultadoOperacao<string>.Falha("Selecione uma imagem.");

        if (arquivo.Length > _opcoes.TamanhoMaximoBytes)
            return ResultadoOperacao<string>.Falha($"A imagem deve ter no máximo {_opcoes.TamanhoMaximoBytes / 1024 / 1024} MB.");

        var extensaoInformada = Path.GetExtension(arquivo.FileName);
        if (string.IsNullOrEmpty(extensaoInformada) || !ExtensoesPermitidas.TryGetValue(extensaoInformada, out var extensaoNormalizada))
            return ResultadoOperacao<string>.Falha("Formato não permitido. Envie .jpg, .png ou .webp.");

        var cabecalho = new byte[12];
        int lidos;
        await using (var leitura = arquivo.OpenReadStream())
        {
            lidos = await leitura.ReadAtLeastAsync(cabecalho, cabecalho.Length, throwOnEndOfStream: false, ct);
        }

        var extensaoReal = DetectarExtensao(cabecalho.AsSpan(0, lidos));
        if (extensaoReal is null)
            return ResultadoOperacao<string>.Falha("O conteúdo do arquivo não é uma imagem válida.");

        if (extensaoReal != extensaoNormalizada)
            return ResultadoOperacao<string>.Falha("A extensão do arquivo não corresponde ao seu conteúdo real.");

        var pastaRelativa = _opcoes.Pasta.Trim('/', '\\');
        var pastaFisica = Path.GetFullPath(Path.Combine(env.WebRootPath, pastaRelativa));
        Directory.CreateDirectory(pastaFisica);

        var nomeArquivo = Guid.NewGuid().ToString() + extensaoReal;
        var caminho = Path.GetFullPath(Path.Combine(pastaFisica, nomeArquivo));
        if (!caminho.StartsWith(pastaFisica + Path.DirectorySeparatorChar, StringComparison.Ordinal))
            return ResultadoOperacao<string>.Falha("Caminho de destino inválido.");

        await using (var destino = new FileStream(caminho, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, useAsync: true))
        {
            await arquivo.CopyToAsync(destino, ct);
        }

        logger.LogInformation("Imagem de peça salva: {Arquivo} ({Bytes} bytes)", nomeArquivo, arquivo.Length);
        return ResultadoOperacao<string>.Ok($"/{pastaRelativa.Replace('\\', '/')}/{nomeArquivo}", "Imagem enviada.");
    }

    public void Remover(string? urlRelativa)
    {
        if (string.IsNullOrWhiteSpace(urlRelativa)) return;

        var prefixo = "/" + _opcoes.Pasta.Trim('/', '\\').Replace('\\', '/') + "/";
        if (!urlRelativa.StartsWith(prefixo, StringComparison.OrdinalIgnoreCase)) return;

        var nome = Path.GetFileName(urlRelativa);
        if (!Guid.TryParse(Path.GetFileNameWithoutExtension(nome), out _)) return;
        if (!ExtensoesPermitidas.ContainsKey(Path.GetExtension(nome))) return;

        var caminho = Path.Combine(env.WebRootPath, _opcoes.Pasta.Trim('/', '\\'), nome);
        try
        {
            if (File.Exists(caminho)) File.Delete(caminho);
        }
        catch (IOException ex)
        {
            logger.LogWarning(ex, "Não foi possível remover a imagem {Arquivo}", nome);
        }
    }

    private static string? DetectarExtensao(ReadOnlySpan<byte> h)
    {
        // JPEG: FF D8 FF
        if (h.Length >= 3 && h[0] == 0xFF && h[1] == 0xD8 && h[2] == 0xFF) return ".jpg";

        // PNG: 89 50 4E 47 0D 0A 1A 0A
        if (h.Length >= 8 && h[..8].SequenceEqual(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A })) return ".png";

        // WEBP: "RIFF" .... "WEBP"
        if (h.Length >= 12 && h[..4].SequenceEqual("RIFF"u8) && h.Slice(8, 4).SequenceEqual("WEBP"u8)) return ".webp";

        return null;
    }
}
