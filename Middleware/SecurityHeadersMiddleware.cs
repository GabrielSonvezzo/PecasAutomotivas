namespace PecasAutomotivas.Middleware;

/// <summary>
/// Injeta os cabeçalhos de segurança em todas as respostas (HTML, JSON e arquivos estáticos).
/// A CSP é estrita: nenhum script/estilo inline e nenhum domínio externo — todo JS/CSS/fonte é servido de wwwroot.
/// </summary>
public sealed class SecurityHeadersMiddleware(RequestDelegate next)
{
    private const string ContentSecurityPolicy =
        "default-src 'self'; " +
        "base-uri 'self'; " +
        "object-src 'none'; " +
        "frame-ancestors 'none'; " +
        "form-action 'self'; " +
        "img-src 'self' data: blob:; " +
        "script-src 'self'; " +
        "style-src 'self'; " +
        "font-src 'self'; " +
        "connect-src 'self'; " +
        "manifest-src 'self'; " +
        "worker-src 'self'; " +
        "upgrade-insecure-requests";

    public Task InvokeAsync(HttpContext context)
    {
        context.Response.OnStarting(() =>
        {
            var headers = context.Response.Headers;
            headers["X-Frame-Options"] = "DENY";
            headers["X-Content-Type-Options"] = "nosniff";
            headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
            headers["Permissions-Policy"] = "camera=(), microphone=(), geolocation=(), payment=(), usb=()";
            headers["Cross-Origin-Opener-Policy"] = "same-origin";
            headers["Content-Security-Policy"] = ContentSecurityPolicy;
            headers.Remove("X-Powered-By");
            return Task.CompletedTask;
        });

        return next(context);
    }
}

public static class SecurityHeadersExtensions
{
    public static IApplicationBuilder UseSecurityHeaders(this IApplicationBuilder app)
        => app.UseMiddleware<SecurityHeadersMiddleware>();
}
