using System.Globalization;
using Clinica.Application.Modelos;

namespace Clinica.Application.Servicos;

/// <summary>
/// O que a tela DOCUMENTOS EMITIDOS do Gerente AFIRMA (out/2026, etapa 6 — a tela saiu do WPF
/// para o Blazor). Era o <c>DocumentosEmitidosViewModel</c>: os quatro números já escritos, o
/// "mais emitido", o resumo do período, as linhas da tabela e as canceladas com o motivo.
///
/// ⚠️ Ela não CALCULA nada de novo: os números saem de <see cref="PainelDeDocumentos"/> e as
/// folhas do <see cref="CentralDocumentosService.EmitidasAsync"/> — o serviço dono da leitura.
/// Recontar aqui daria dois números para a mesma pergunta, e o segundo seria o que ninguém
/// lembraria de corrigir. O que mora aqui é só a ESCRITA (o travessão, o "balcão", o "(sem
/// motivo registrado)", a frase do período), porque é ela que a tela afirma, e o que a tela
/// afirma mora onde o <c>dotnet test</c> alcança.
/// </summary>
public static class DocumentosEmitidosNaTela
{
    private static readonly CultureInfo PtBr = CultureInfo.GetCultureInfo("pt-BR");
    private const string Travessao = "—";

    /// <summary>Quem assinou quando o papel não tem profissional — é do balcão.</summary>
    public const string Balcao = "balcão";

    /// <summary>A cancelada sem motivo (registro anterior à regra) — dita, nunca em branco.</summary>
    public const string SemMotivo = "(sem motivo registrado)";

    /// <param name="folhas">O que saiu no período, já filtrado pelo acesso de quem lê.</param>
    /// <param name="inicio">Início do período escolhido.</param>
    /// <param name="fim">Fim do período (sempre hoje — ver <see cref="ResumoFaturamentoNaTela.Intervalo"/>).</param>
    /// <param name="hoje">Para "no ar" — por parâmetro para ser testável.</param>
    public static PainelDosEmitidos Montar(
        IReadOnlyList<FolhaEmitida> folhas, DateOnly inicio, DateOnly fim, DateOnly hoje)
    {
        var painel = PainelDeDocumentos.Montar(folhas, hoje);
        var periodo = $"{Data(inicio)} a {Data(fim)}";

        return new PainelDosEmitidos(
            Intervalo: periodo,
            Resumo: painel.Vazio
                ? $"Nenhum papel emitido de {periodo}."
                : $"{painel.Emitidos.ToString(PtBr)} folha(s) de {periodo}.",
            Emitidos: painel.Emitidos.ToString(PtBr),
            Assinados: painel.Assinados.ToString(PtBr),
            Cancelados: painel.Cancelados.ToString(PtBr),
            NoAr: painel.NoAr.ToString(PtBr),
            // Vazio vira travessão: "— 0%" seria um número onde não há pergunta a responder.
            MaisEmitido: string.IsNullOrEmpty(painel.MaisEmitido) ? Travessao : painel.MaisEmitido,
            PorQuemAssina: painel.PorQuemAssina,
            TituloCanceladas: $"Cancelados no período · {painel.Cancelados.ToString(PtBr)}",
            Canceladas: painel.Canceladas.Select(Cancelada).ToList(),
            Linhas: folhas.Select(Linha).ToList(),
            Vazio: painel.Vazio);
    }

    /// <summary>Uma linha da tabela. Cancelada aparece MARCADA, nunca sumindo.</summary>
    public static LinhaEmitidaNaTela Linha(FolhaEmitida f) => new(
        Chave: $"{f.Natureza}:{f.DocumentoId}",
        Numero: f.Numero,
        Folha: f.FolhaRotulo,
        Para: string.IsNullOrWhiteSpace(f.Paciente) ? Travessao : f.Paciente!,
        QuemAssinou: string.IsNullOrWhiteSpace(f.Profissional) ? Balcao : f.Profissional!,
        Data: f.Data.ToString("dd/MM", PtBr),
        DataCompleta: Data(f.Data),
        Assinado: f.Assinado,
        Cancelado: f.Cancelado);

    /// <summary>Uma cancelada, com o motivo que a lei manda existir e o contexto (quem · quando).</summary>
    public static CanceladaNaTela Cancelada(FolhaEmitida c) => new(
        Chave: $"{c.Natureza}:{c.DocumentoId}",
        Numero: c.Numero,
        Folha: c.FolhaRotulo,
        Motivo: string.IsNullOrWhiteSpace(c.MotivoCancelamento) ? SemMotivo : c.MotivoCancelamento!,
        Contexto: string.Join(" · ", new[] { c.Profissional, Data(c.Data) }.Where(x => !string.IsNullOrWhiteSpace(x))));

    private static string Data(DateOnly d) => d.ToString("dd/MM/yyyy", PtBr);
}

/// <summary>Tudo o que a tela Documentos emitidos mostra, já escrito.</summary>
/// <param name="Vazio">Não houve emissão no período: a tela troca de frase em vez de mostrar zeros.</param>
public sealed record PainelDosEmitidos(
    string Intervalo,
    string Resumo,
    string Emitidos,
    string Assinados,
    string Cancelados,
    string NoAr,
    string MaisEmitido,
    IReadOnlyList<QuemAssina> PorQuemAssina,
    string TituloCanceladas,
    IReadOnlyList<CanceladaNaTela> Canceladas,
    IReadOnlyList<LinhaEmitidaNaTela> Linhas,
    bool Vazio)
{
    /// <summary>Houve cancelamento no período — a lista deles some quando não houve.</summary>
    public bool TemCanceladas => Canceladas.Count > 0;
}

/// <param name="Chave">Natureza + id: o id sozinho repete entre clínica e financeiro.</param>
/// <param name="QuemAssinou">O profissional, ou "balcão" quando o papel não tem assinatura.</param>
/// <param name="Data">"dd/MM" — a coluna é estreita e o período já diz o ano.</param>
public sealed record LinhaEmitidaNaTela(
    string Chave, string Numero, string Folha, string Para, string QuemAssinou,
    string Data, string DataCompleta, bool Assinado, bool Cancelado);

public sealed record CanceladaNaTela(string Chave, string Numero, string Folha, string Motivo, string Contexto);
