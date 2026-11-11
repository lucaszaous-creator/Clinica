using Clinica.Domain.Entities;
using Clinica.Domain.Regras;

namespace Clinica.Application.Servicos;

/// <summary>
/// O que a tela de glosas decide ao gerar o recurso (nov/2026). O padrão TISS amarra o recurso
/// de glosa a UM lote e ao protocolo que a operadora devolveu no envio — então sai um arquivo
/// por lote, e a glosa que não tem como ser amarrada fica de fora COM o motivo escrito, em vez
/// de entrar num arquivo que a operadora recusaria inteiro.
/// </summary>
public static class RecursoDeGlosaTiss
{
    /// <summary>As glosas em aberto de um lote: um XML de recurso.</summary>
    public sealed record Recurso(LoteTiss Lote, string NomeOperadora, IReadOnlyList<CodigoFaturamento> Glosas);

    /// <summary>Os recursos que saem e o que ficou de fora, já em frases.</summary>
    public sealed record Plano(IReadOnlyList<Recurso> Recursos, IReadOnlyList<string> DeFora);

    public static Plano Planejar(IEnumerable<CodigoFaturamento> glosadas)
    {
        var emAberto = glosadas.Where(c => c.GlosaEmAberto).ToList();
        var deFora = new List<string>();

        var semLote = emAberto.Count(c => c.Lote is null);
        if (semLote > 0)
            deFora.Add($"{semLote} glosa(s) de guia que não entrou em lote TISS — o recurso responde a um lote enviado.");

        var semCodigo = emAberto.Count(c => c.Lote is not null && string.IsNullOrWhiteSpace(c.MotivoGlosaCodigo));
        if (semCodigo > 0)
            deFora.Add($"{semCodigo} glosa(s) sem o código do motivo (tabela ANS) — registre o motivo na glosa.");

        var recursos = new List<Recurso>();
        foreach (var grupo in emAberto
                     .Where(c => c.Lote is not null && !string.IsNullOrWhiteSpace(c.MotivoGlosaCodigo))
                     .GroupBy(c => c.Lote!.Id).OrderBy(g => g.First().Lote!.Numero))
        {
            var lote = grupo.First().Lote!;
            if (!(lote.ProtocoloOperadora ?? string.Empty).Any(char.IsDigit))
            {
                deFora.Add($"{grupo.Count()} glosa(s) do lote nº {lote.Numero}: falta o protocolo da operadora — informe-o em Lotes TISS, ao marcar o envio.");
                continue;
            }
            var convenio = lote.ConvenioCodigo ?? LoteTissService.ConvenioDaGuia(grupo.First());
            recursos.Add(new Recurso(lote, CatalogoConvenios.Nome(convenio), grupo.ToList()));
        }

        return new Plano(recursos, deFora);
    }

    public static string NomeDoArquivo(Recurso recurso, DateOnly dia)
        => $"TISS-Recurso-Glosa-Lote-{recurso.Lote.Numero:000000}-{dia:yyyy-MM-dd}.xml";

    /// <summary>
    /// O identificador do recurso: o instante e o lote, em 12 dígitos — é também o sequencial
    /// da transação, que o esquema limita a 12 caracteres. Dois lotes no mesmo minuto não colidem.
    /// </summary>
    public static string NumeroDoRecurso(Recurso recurso, DateTime agora)
        => $"{agora:MMddHHmm}{recurso.Lote.Numero % 10000:0000}";
}
