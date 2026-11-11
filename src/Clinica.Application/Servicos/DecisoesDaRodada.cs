using Clinica.Application.Modelos;
using Clinica.Domain;
using Clinica.Domain.Entities;
using Clinica.Domain.Regras;

namespace Clinica.Application.Servicos;

/// <summary>Uma linha da rodada de pendências: a guia e a decisão que a pessoa tomou sobre ela.</summary>
public sealed class LinhaDaRodada
{
    public required int CodigoId { get; init; }
    public required string Descricao { get; init; }
    public required string Situacao { get; init; }
    public FormatoNumeroGuia Formato { get; init; } = FormatoNumeroGuia.SemValidacao;

    /// <summary>Preenchido = dar baixa com este número.</summary>
    public string? NumeroGuia { get; set; }

    /// <summary>Preenchida (e sem número) = registrar como não conformidade.</summary>
    public string? Justificativa { get; set; }

    public bool SemDecisao => string.IsNullOrWhiteSpace(NumeroGuia) && string.IsNullOrWhiteSpace(Justificativa);

    /// <summary>
    /// A baixa desta guia pede a conferência da guia ANS (consulta da Saúde Petrobras, nov/2026),
    /// e isso só existe no formulário de baixa — uma guia por vez. Aqui (lote e rodada) ela NÃO é
    /// baixada: o número digitado é recusado antes de gravar. Na rodada, a decisão dela é a
    /// JUSTIFICATIVA (decisão da direção, 11/11/2026: "avisa sobre a pendência Petrobras com
    /// justificativa") — e essa justificativa é gravada como ANOTAÇÃO da pendência, não como não
    /// conformidade: a guia continua no painel, à espera da baixa pelo formulário. Marcá-la como
    /// não conformidade a tiraria do painel, que é justamente onde ela se resolve.
    /// </summary>
    public bool PedeGuiaAns { get; init; }

    /// <summary>
    /// A anotação que a guia já tinha quando a rodada abriu (só nas linhas <see cref="PedeGuiaAns"/>).
    /// Confirmar a MESMA justificativa não regrava nada: a data do "anotado em" tem de continuar
    /// dizendo desde quando o motivo vale, e não a última vez que alguém clicou em Concluir.
    /// </summary>
    public string? JustificativaJaAnotada { get; init; }

    /// <summary>Há número digitado numa linha que só pode ser baixada pelo formulário de baixa.</summary>
    public bool BaixaForaDoFormulario => PedeGuiaAns && !string.IsNullOrWhiteSpace(NumeroGuia);

    /// <summary>A crítica do número contra o formato DESTE convênio (nula = serve).</summary>
    public string? CriticaDoNumero => RegraNumeroGuia.Criticar(NumeroGuia, Formato);
}

/// <summary>
/// A rodada de pendências inteira — montar as linhas, o texto do topo, a conferência antes de
/// concluir e a APLICAÇÃO linha a linha — num lugar só (out/2026).
///
/// Ela morava na janela WPF e no fluxo que a abria. Com o Faturamento em Blazor passaram a ser
/// DUAS portas: a janela BLOQUEANTE da abertura do app (continua WPF — ela trava o sistema antes
/// de a área de trabalho existir, e um formulário dentro do navegador não trava nada) e o botão
/// "Rodar pendências" do painel. Duas cópias de "como se aplica a rodada" divergiriam na
/// primeira correção, e a que ficasse para trás baixaria uma guia que a outra recusaria.
/// </summary>
public static class DecisoesDaRodada
{
    public static IReadOnlyList<LinhaDaRodada> Montar(IEnumerable<PendenciaCodigo> itens)
        => itens.Select(i => new LinhaDaRodada
        {
            CodigoId = i.CodigoId,
            // Rótulo central, nunca o enum cru (parcela 41).
            Descricao = $"{i.PacienteNome} — {RotulosEnum.De(i.Tipo)} ({RotulosEnum.De(i.Ordem)})",
            Formato = CatalogoConvenios.FormatoDoNumeroDaGuia(i.ConvenioCodigo ?? i.Convenio.ToString()),
            PedeGuiaAns = GuiaAnsNaBaixa.Exige(i.Tipo, i.ConvenioCodigo, i.Convenio),
            // A justificativa já anotada volta preenchida: a rodada bloqueante relista a guia a cada
            // abertura enquanto a baixa não for dada, e ninguém deve redigitar o mesmo motivo todo dia.
            Justificativa = GuiaAnsNaBaixa.Exige(i.Tipo, i.ConvenioCodigo, i.Convenio) ? i.ObservacaoPendencia : null,
            JustificativaJaAnotada = GuiaAnsNaBaixa.Exige(i.Tipo, i.ConvenioCodigo, i.Convenio) ? i.ObservacaoPendencia : null,
            // Nome do CATÁLOGO, nunca o enum (parcela 50).
            Situacao = (i.DiasEmAtraso > 0
                ? $"{CatalogoConvenios.Nome(i.ConvenioCodigo, i.Convenio)} · atrasada há {i.DiasEmAtraso} dia(s)"
                : $"{CatalogoConvenios.Nome(i.ConvenioCodigo, i.Convenio)} · vence hoje")
                // A linha diz ONDE resolver: o campo do número daqui não serve para ela.
                + (GuiaAnsNaBaixa.Exige(i.Tipo, i.ConvenioCodigo, i.Convenio)
                    ? " · baixa só pelo \"Dar baixa\" do painel (guia ANS) — aqui, justifique por que ainda não foi baixada"
                    : string.Empty)
        }).ToList();

    /// <summary>
    /// A recusa das linhas com número digitado que só podem ser baixadas pelo formulário de baixa
    /// (nula = nenhuma). Dita ANTES de processar, pelo mesmo motivo da crítica do formato: no meio
    /// da rodada ela deixaria parte decidida.
    /// </summary>
    public static string? ImpedimentoDaGuiaAns(IReadOnlyList<LinhaDaRodada> linhas, string verbo)
    {
        var fora = linhas.Where(l => l.BaixaForaDoFormulario).ToList();
        return fora.Count == 0
            ? null
            : $"{fora.Count} guia(s) de consulta da Saúde Petrobras pedem a conferência da guia ANS e não podem ser "
              + "baixadas por aqui. Apague o número dessas linhas e dê baixa em cada uma pelo botão \"Dar baixa\" do "
              + $"painel de pendências — {verbo}.\n\n"
              + string.Join("\n", fora.Select(l => $"• {l.Descricao}"));
    }

    public static string Resumo(int quantidade, RodadaPendenciasStatus status, bool bloqueante)
    {
        var extras = new List<string>();
        if (status.AplicaConsultas && status.ConsultasParaRevisar > 0)
            extras.Add($"{status.ConsultasParaRevisar} consulta(s) a renovar");
        if (status.AplicaCarteirinhas && status.CarteirinhasParaRevisar > 0)
            extras.Add($"{status.CarteirinhasParaRevisar} carteirinha(s) a vencer");
        var lembrete = extras.Count > 0 ? $" Reveja também, no painel: {string.Join(" e ", extras)}." : string.Empty;

        return bloqueante
            ? $"Estas {quantidade} guia(s) estão pendentes há mais de {status.PrazoDias} dias sem resolução." +
              " Decida cada uma para continuar — dê baixa ou justifique como não conformidade." + lembrete
            : $"{quantidade} guia(s) pendente(s). Dê baixa no que puder e justifique o restante como não conformidade." + lembrete;
    }

    /// <summary>
    /// O que impede de concluir (nulo = pode concluir). Bloqueante: toda guia precisa de decisão.
    /// E o formato do número ANTES de processar — no modo bloqueante o sistema está travado, e
    /// uma recusa do serviço no meio deixaria parte decidida e a pessoa sem saber qual linha
    /// corrigir para voltar a trabalhar.
    /// </summary>
    public static string? ImpedimentoParaConcluir(IReadOnlyList<LinhaDaRodada> linhas, bool bloqueante)
    {
        // O número numa guia que pede a guia ANS vem ANTES da cobrança de decisão: senão a pessoa
        // leria "decida todas", digitaria o número ali e só então descobriria que ele não serve.
        if (ImpedimentoDaGuiaAns(linhas, "nada foi gravado") is { } guiaAns)
            return guiaAns;

        if (bloqueante && linhas.Any(l => l.SemDecisao))
            return "Enquanto a rodada estiver vencida, decida todas as guias: informe o número da guia " +
                   "(baixa) ou uma justificativa (não conformidade)."
                   + (linhas.Any(l => l.SemDecisao && l.PedeGuiaAns)
                       ? "\n\nNas consultas da Saúde Petrobras, escreva a justificativa: a baixa delas é pelo botão "
                         + "\"Dar baixa\" do painel, e a guia continua lá depois desta rodada."
                       : string.Empty);

        var recusadas = linhas.Where(l => l.CriticaDoNumero is not null).ToList();
        if (recusadas.Count > 0)
            return $"{recusadas.Count} guia(s) com o número fora do formato do convênio. "
                   + "Corrija antes de concluir — nada foi gravado.\n\n"
                   + string.Join("\n", recusadas.Select(l => $"• {l.Descricao}: {l.CriticaDoNumero}"));
        return null;
    }

    /// <summary>
    /// Aplica linha a linha. UMA falha (número recusado, concorrência, permissão) não aborta as
    /// demais nem passa despercebida: vai para o log e para a lista devolvida, e o resto segue.
    /// Número preenchido = baixa (vence a justificativa); só justificativa = não conformidade.
    /// </summary>
    public static async Task<IReadOnlyList<string>> AplicarAsync(
        IReadOnlyList<LinhaDaRodada> linhas, DateOnly dataBaixa,
        FaturamentoService faturamento, RodadaPendenciasService rodada, CancellationToken ct = default)
    {
        var falhas = new List<string>();
        foreach (var l in linhas)
        {
            try
            {
                if (!string.IsNullOrWhiteSpace(l.NumeroGuia))
                {
                    SessaoUsuario.Atual.Exigir(Permissao.BaixarGuia, "dar baixa na guia");
                    await faturamento.DarBaixaAsync(l.CodigoId, dataBaixa, l.NumeroGuia!.Trim(),
                        SessaoUsuario.Atual.Operador, "baixa na rodada de pendências", ct);
                }
                else if (!string.IsNullOrWhiteSpace(l.Justificativa) && l.PedeGuiaAns)
                {
                    // Consulta da Saúde Petrobras: a justificativa vira ANOTAÇÃO da pendência (a mesma do
                    // botão "Anotar" do painel, com a mesma permissão) e a guia continua pendente.
                    if (string.Equals(l.Justificativa!.Trim(), l.JustificativaJaAnotada?.Trim(), StringComparison.Ordinal))
                        continue;
                    SessaoUsuario.Atual.Exigir(Permissao.VerFaturamento, "anotar a pendência");
                    await faturamento.RegistrarObservacaoPendenciaAsync(l.CodigoId, l.Justificativa!.Trim(), SessaoUsuario.Atual.Operador, ct);
                }
                else if (!string.IsNullOrWhiteSpace(l.Justificativa))
                {
                    SessaoUsuario.Atual.Exigir(Permissao.MarcarNaoConformidade, "registrar a não conformidade");
                    await rodada.MarcarNaoConformidadeAsync(l.CodigoId, l.Justificativa!.Trim(), SessaoUsuario.Atual.Operador, ct);
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                Diagnostico.Registrar($"Rodada de pendências — falha ao aplicar guia {l.CodigoId}", ex);
                falhas.Add($"• {l.Descricao}: {ex.Message}");
            }
        }
        return falhas;
    }

    /// <summary>A frase de quem tem falhas (nula = tudo aplicado).</summary>
    public static string? ResumoDasFalhas(IReadOnlyList<string> falhas) => falhas.Count == 0
        ? null
        : $"{falhas.Count} guia(s) não puderam ser aplicadas e continuam pendentes:\n\n"
          + string.Join("\n", falhas) + "\n\nO restante foi processado. Reveja essas guias no painel.";

    /// <summary>A rodada só faz sentido para quem consegue tomar ALGUMA das duas decisões.</summary>
    public static bool PodeDecidir()
        => SessaoUsuario.Atual.Pode(Permissao.BaixarGuia) || SessaoUsuario.Atual.Pode(Permissao.MarcarNaoConformidade);
}
