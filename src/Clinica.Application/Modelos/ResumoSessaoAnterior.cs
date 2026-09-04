using Clinica.Domain.Entities;

namespace Clinica.Application.Modelos;

/// <summary>
/// Um campo escrito da sessão anterior, com o RÓTULO separado do valor.
///
/// ⚠️ Ele nasceu separado na rodada da ABA (set/2026), e a razão não é estética: enquanto
/// o painel morava numa coluna de 350 px, a linha saía como uma frase só —
/// <c>"Queixa: lombalgia há 3 meses"</c> —, e com seis frases assim, cada uma truncada num
/// ponto diferente, o olho não acha onde uma acaba e a outra começa. Foi exatamente essa a
/// reprovação do cliente ("essas tabelas laterais não me agradam"): o que ele viu foram
/// fragmentos de texto sem nada que dissesse o que cada um era.
///
/// Com o par, a aba desenha uma COLUNA de rótulos alinhada (um <c>SharedSizeGroup</c>) e o
/// valor ao lado, que é como se lê um prontuário no papel.
/// </summary>
public sealed record CampoDaSessaoAnterior(string Rotulo, string Valor);

/// <summary>
/// Uma sessão ANTERIOR resumida, como ela aparece para quem está escrevendo a de hoje
/// (parcela 77).
///
/// O que ela conserta
/// ------------------
/// O painel das últimas sessões nasceu com QUATRO campos e a sessão passou a ter doze
/// (parcelas 73, 75 e 77): ele mostrava data, EVA, queixa e conduta, e ficava cego para a
/// hipótese, o plano, o encaminhamento e — o que mais dói — o <b>retorno sugerido</b>, que é
/// literalmente a resposta para "por que este paciente está aqui hoje". O profissional
/// escrevia "voltar em 7 dias para reavaliar a EVA", o paciente voltava, e a tela não dizia.
/// A EVOLUÇÃO, o texto mais escrito do sistema, estava no modelo e não estava na tela.
///
/// É o mesmo defeito do modelo de evolução (parcela 76) num segundo leitor, e a lição vale
/// para o próximo campo: <b>campo novo de evolução entra também em quem MOSTRA a sessão
/// anterior.</b>
///
/// Por que mora AQUI e não na ViewModel
/// ------------------------------------
/// ⚠️ A composição tem decisões — a hipótese com o CID entre parênteses, o CID sozinho
/// quando é só ele, o campo que NÃO vira linha quando não foi escrito —, e decisão que mora
/// em projeto WPF não é alcançada pelo <c>dotnet test</c>. É a lição da grade da semana
/// (parcela 69): tudo o que decide um desenho precisa morar onde o teste chega, senão a
/// regra vive de inspeção visual.
///
/// ⚠️ O que não foi escrito NÃO entra em <see cref="Campos"/>: linha vazia — ou pior, uma
/// linha "—" — gasta o mesmo espaço de uma que informa, e numa sessão da folha única
/// (set/2026) o normal é haver UM campo escrito, não seis.
/// </summary>
public sealed record ResumoSessaoAnterior(
    int EvolucaoId,
    string Data,
    string Eva,
    bool EvaMedida,
    string Retorno,
    IReadOnlyList<CampoDaSessaoAnterior> Campos)
{
    public static ResumoSessaoAnterior De(Evolucao e) => new(
        EvolucaoId: e.Id,
        Data: e.Data.ToString("dd/MM/yyyy"),
        Eva: e.TemParEva ? $"EVA {e.EvaAntes} → {e.EvaDepois}" : "EVA não medida",
        EvaMedida: e.TemParEva,
        Retorno: DoRetorno(e),
        Campos: DosCampos(e));

    /// <summary>Os rótulos, como CONSTANTE: quem lê um campo pelo nome não pode chutá-lo.</summary>
    public const string RotuloQueixa = "Queixa";
    public const string RotuloHipotese = "Hipótese";
    public const string RotuloCid = "CID";
    public const string RotuloConduta = "Conduta";
    public const string RotuloEvolucao = "Evolução";
    public const string RotuloPlano = "Plano";
    public const string RotuloEncaminhamento = "Encaminhado";

    /// <summary>
    /// O valor de um campo escrito, ou NULO quando ele não foi escrito.
    ///
    /// ⚠️ Ele existe porque o "Repetir última conduta" lia <c>Conduta</c> como texto CRU e
    /// o texto trazia o rótulo dentro dele: desde a parcela 77 o botão copiava
    /// <c>"Conduta: agulhamento lombar"</c> para dentro do campo Conduta, e o segundo
    /// clique escreveria <c>"Conduta: Conduta: …"</c>. Não estourava nada — a sessão saía
    /// gravada assim, no prontuário e no relatório do convênio. O par rótulo/valor separado
    /// é o que torna isso impossível de cometer.
    /// </summary>
    public string? Valor(string rotulo) => Campos.FirstOrDefault(c => c.Rotulo == rotulo)?.Valor;

    /// <summary>
    /// A ÚLTIMA sessão em UMA linha, para a folha de hoje.
    ///
    /// ⚠️ Ela existe porque a coluna aberta virou ABA (set/2026): reler a sessão passada
    /// passou a custar um clique, e o que não pode custar clique nenhum é a resposta para
    /// <i>"por que este paciente está aqui hoje"</i> — a data, o par da EVA e o retorno
    /// sugerido. Sem esta linha, trocar a coluna pela aba teria custado justamente o campo
    /// que a parcela 77 existiu para pôr na tela.
    ///
    /// ⚠️ "EVA não medida" NÃO entra: numa linha quieta de contexto ela é ruído sobre o que
    /// a sessão passada deixou de fazer, e o que a linha responde é o que ela deixou dito.
    /// </summary>
    public static string ContextoDaUltima(ResumoSessaoAnterior? ultima)
    {
        if (ultima is null) return string.Empty;

        var partes = new List<string> { $"Última sessão em {ultima.Data}" };
        if (ultima.EvaMedida) partes.Add(ultima.Eva);
        if (!string.IsNullOrWhiteSpace(ultima.Retorno)) partes.Add(ultima.Retorno);

        return string.Join(" · ", partes);
    }

    /// <summary>
    /// Os campos escritos, na ordem em que se lê um prontuário: o que a pessoa DIZ, o que
    /// isso É, o que se FEZ, o que aconteceu, o que vem depois.
    ///
    /// ⚠️ A ordem e a lista são as MESMAS que o relatório do convênio imprime separado por
    /// assunto. Campo novo de evolução entra aqui no mesmo commit — é o nono lugar da lista
    /// de conferência, e esquecê-lo não quebra nada: o campo simplesmente não aparece para
    /// quem lê a sessão passada.
    /// </summary>
    private static IReadOnlyList<CampoDaSessaoAnterior> DosCampos(Evolucao e)
    {
        var campos = new List<CampoDaSessaoAnterior>();

        Acrescentar(campos, RotuloQueixa, e.QueixaPrincipal);
        if (DaHipotese(e) is { } hipotese) campos.Add(hipotese);
        Acrescentar(campos, RotuloConduta, e.Conduta);
        Acrescentar(campos, RotuloEvolucao, e.TextoEvolucao);
        Acrescentar(campos, RotuloPlano, e.PlanoTerapeutico);
        Acrescentar(campos, RotuloEncaminhamento, e.Encaminhamento);

        return campos;
    }

    private static void Acrescentar(List<CampoDaSessaoAnterior> campos, string rotulo, string? valor)
    {
        if (string.IsNullOrWhiteSpace(valor)) return;
        campos.Add(new CampoDaSessaoAnterior(rotulo, valor.Trim()));
    }

    /// <summary>A hipótese com o CID entre parênteses — e o CID sozinho quando é só ele.</summary>
    private static CampoDaSessaoAnterior? DaHipotese(Evolucao e)
    {
        var tem = !string.IsNullOrWhiteSpace(e.HipoteseDiagnostica);
        var cid = !string.IsNullOrWhiteSpace(e.CidSessao);

        return (tem, cid) switch
        {
            (true, true) => new(RotuloHipotese,
                $"{e.HipoteseDiagnostica!.Trim()} ({e.CidSessao!.Trim()})"),
            (true, false) => new(RotuloHipotese, e.HipoteseDiagnostica!.Trim()),
            (false, true) => new(RotuloCid, e.CidSessao!.Trim()),
            _ => null
        };
    }

    /// <summary>
    /// "↩ Voltar em 27/08/2026 — reavaliar a EVA". Fica FORA da lista de campos porque não
    /// é um campo entre outros: é a razão da consulta de hoje, e a aba o desenha em
    /// destaque, acima dos demais.
    /// </summary>
    private static string DoRetorno(Evolucao e)
    {
        if (e.RetornoSugeridoEm is not { } quando) return string.Empty;

        return string.IsNullOrWhiteSpace(e.RetornoSugeridoNota)
            ? $"↩ Voltar em {quando:dd/MM/yyyy}"
            : $"↩ Voltar em {quando:dd/MM/yyyy} — {e.RetornoSugeridoNota!.Trim()}";
    }
}
