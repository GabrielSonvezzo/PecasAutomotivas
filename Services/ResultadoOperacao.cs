namespace PecasAutomotivas.Services;

public record ResultadoOperacao(bool Sucesso, string Mensagem)
{
    public static ResultadoOperacao Ok(string mensagem) => new(true, mensagem);
    public static ResultadoOperacao Falha(string mensagem) => new(false, mensagem);
}

public sealed record ResultadoOperacao<T>(bool Sucesso, string Mensagem, T? Valor) : ResultadoOperacao(Sucesso, Mensagem)
{
    public static ResultadoOperacao<T> Ok(T valor, string mensagem) => new(true, mensagem, valor);
    public static new ResultadoOperacao<T> Falha(string mensagem) => new(false, mensagem, default);
}
