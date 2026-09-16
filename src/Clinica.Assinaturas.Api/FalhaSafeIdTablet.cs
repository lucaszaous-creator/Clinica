using System.Diagnostics;
using System.Text.RegularExpressions;
using Npgsql;

namespace Clinica.Assinaturas.Api;

/// <summary>Diagnóstico sem mensagens externas, documentos ou credenciais.</summary>
public sealed record FalhaSafeIdTablet(string Codigo, string Origem)
{
    public static FalhaSafeIdTablet Classificar(Exception erro)
    {
        var codigo = "INTERNO";
        for (var atual = erro; atual is not null; atual = atual.InnerException)
        {
            if (atual is PostgresException pg && Regex.IsMatch(pg.SqlState, "^[A-Z0-9]{5}$"))
            { codigo = "BANCO-" + pg.SqlState; break; }
            if (atual is OperationCanceledException) { codigo = "TEMPO-ESGOTADO"; break; }
            if (atual is HttpRequestException rede)
            { codigo = rede.StatusCode is { } status ? "HTTP-" + (int)status : "REDE"; break; }
            if (atual.Message.StartsWith("A cadeia deste certificado não é reconhecida", StringComparison.Ordinal))
            { codigo = "CADEIA-CERTIFICADO"; break; }
            if (atual.Message.StartsWith("O SafeID recusou a chamada a '", StringComparison.Ordinal))
            {
                var status = Regex.Match(atual.Message, "^O SafeID recusou a chamada a '[a-z/-]+(?:\\?from_authorization=true)?' \\(([1-5][0-9]{2}) ");
                codigo = status.Success ? "PSC-" + status.Groups[1].Value : "PSC-RECUSA";
                break;
            }
        }
        var origem = new StackTrace(erro, false).GetFrames()?
            .Select(f => f.GetMethod()?.DeclaringType)
            .FirstOrDefault(t => t?.Assembly.GetName().Name?.StartsWith("Clinica.", StringComparison.Ordinal) == true);
        return new(codigo, origem?.FullName ?? "indisponivel");
    }
}
