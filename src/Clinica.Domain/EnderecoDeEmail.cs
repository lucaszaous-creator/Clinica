using System.Net.Mail;

namespace Clinica.Domain;

/// <summary>
/// Normalização e validação de endereço de e-mail — UMA definição para a ficha (que grava),
/// o lembrete automático (que envia) e as Configurações (que cadastram o remetente).
///
/// Chama-se <c>EnderecoDeEmail</c> e não <c>Email</c> de propósito: dentro de um ViewModel
/// que tem a propriedade <c>Email</c>, o nome curto resolveria para a propriedade e não
/// para esta classe — e o compilador acusaria, mas só depois de alguém tentar.
/// </summary>
public static class EnderecoDeEmail
{
    /// <summary>Tira espaços e devolve <c>null</c> para o vazio — "" e nulo são o mesmo "não tem".</summary>
    public static string? Normalizar(string? entrada)
    {
        var texto = entrada?.Trim();
        return string.IsNullOrEmpty(texto) ? null : texto;
    }

    /// <summary>
    /// Verdadeiro para um endereço que o .NET consegue entregar. Em branco é INVÁLIDO aqui —
    /// quem aceita o vazio como "não tem" pergunta antes por <see cref="Normalizar"/>.
    /// </summary>
    public static bool Valido(string? entrada)
    {
        var texto = Normalizar(entrada);
        // "maria@" e "maria" passam no TryCreate em algumas versões; o '@' com domínio depois
        // é o mínimo que um servidor de saída aceita.
        return texto is not null
            && MailAddress.TryCreate(texto, out var endereco)
            && endereco.Host.Contains('.')
            && !texto.Contains(' ');
    }

    /// <summary>O endereço normalizado quando é válido; <c>null</c> em qualquer outro caso.</summary>
    public static string? SeValido(string? entrada)
        => Valido(entrada) ? Normalizar(entrada) : null;
}
