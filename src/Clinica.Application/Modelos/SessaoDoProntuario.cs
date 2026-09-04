using Clinica.Domain.Entities;

namespace Clinica.Application.Modelos;

/// <summary>Um bloco da sessão aberta: rótulo + texto. Só existe se foi escrito.</summary>
public sealed record BlocoDaSessao(string Rotulo, string Texto);

/// <summary>
/// UMA SESSÃO DO PRONTUÁRIO, aberta por inteiro (set/2026 — o pedido do cliente:
/// <i>"ao abrir o prontuário não conseguimos abrir o prontuário daquela sessão"</i>).
///
/// O buraco
/// --------
/// A sessão tem DOZE campos de conteúdo desde as parcelas 73, 75 e 77 — queixa, história
/// da doença atual, exame físico, hipótese, CID, conduta, evolução, orientações, plano
/// terapêutico, retorno sugerido, encaminhamento e a EVA — e nenhuma tela os mostrava
/// todos para uma sessão PASSADA:
///
/// <list type="bullet">
///   <item>a lista do Consultório (<c>ProntuarioClinicoView</c>) mostra QUATRO, truncados,
///   e a linha só oferece "Anexos" e "Correções";</item>
///   <item>o modal de leitura rápida (<c>ResumoProntuarioWindow</c>) compõe uma frase a
///   partir do texto da evolução — e é justamente ele que o botão "Abrir" da lista plana
///   de Prontuários abre;</item>
///   <item>a aba das últimas sessões (<see cref="ResumoSessaoAnterior"/>) traz as CINCO
///   mais recentes, rotuladas — e só elas: o prontuário inteiro é esta tela.</item>
/// </list>
///
/// Os outros oito campos estavam gravados e não tinham leitor em lugar nenhum — o defeito
/// recorrente do projeto, na variante em que o dado existe, a tela existe, e nada falha.
///
/// Por que mora AQUI e não na ViewModel
/// ------------------------------------
/// ⚠️ A composição TEM decisões — a hipótese com o CID entre parênteses, o CID sozinho
/// quando é só ele, o bloco que SOME quando não foi escrito, a sessão cancelada que diz o
/// motivo —, e decisão que mora em projeto WPF não é alcançada pelo <c>dotnet test</c>. É
/// a regra da <c>GradeSemana</c> (parcela 69) e do <see cref="ResumoSessaoAnterior"/>
/// (parcela 77).
///
/// ⚠️ A LIÇÃO que este tipo cobra da próxima parcela
/// -------------------------------------------------
/// <b>Campo novo de evolução entra também em quem ABRE a sessão.</b> É o décimo lugar da
/// lista dos oito (o nono foi o <c>ModeloEvolucao</c>, parcela 76; o painel da sessão
/// anterior foi a 77) — e, como os outros dois, esquecê-lo não quebra build, não quebra
/// teste, e o campo simplesmente não aparece para quem lê o prontuário.
/// <c>SessaoDoProntuarioTests.Todo_campo_escrito_aparece_em_algum_bloco</c> falha no
/// commit em que alguém esquecer.
/// </summary>
public sealed record SessaoDoProntuario(
    int EvolucaoId,
    string Titulo,
    string Data,
    string Profissional,
    string Eva,
    IReadOnlyList<BlocoDaSessao> Blocos,
    string Procedencia,
    string? AvisoCancelamento,
    int Anexos,
    int Correcoes)
{
    public bool Cancelada => AvisoCancelamento is not null;
    public bool TemAnexos => Anexos > 0;
    public bool Retificada => Correcoes > 0;

    public string AnexosTexto => Anexos switch
    {
        0 => "Anexos",
        1 => "1 anexo",
        _ => $"{Anexos} anexos"
    };

    public string CorrecoesTexto => Correcoes switch
    {
        1 => "1 correção",
        _ => $"{Correcoes} correções"
    };

    /// <summary>
    /// Monta a sessão a partir da entidade.
    /// </summary>
    /// <param name="anexos">Quantos arquivos — a lista os conta em UMA consulta em lote.</param>
    /// <param name="correcoes">
    /// Quantas versões anteriores existem (parcela 52). Vem de fora porque
    /// <c>Evolucao.Versoes</c> depende de <c>Include</c> e chegaria VAZIA em produção
    /// enquanto o teste passaria pelo relationship fixup do EF (a lição da parcela 68).
    /// </param>
    public static SessaoDoProntuario De(Evolucao e, int anexos, int correcoes)
    {
        var blocos = new List<BlocoDaSessao>();

        // A ordem é a do S-O-A-P, a MESMA da ficha de atendimento (parcela 77): o que a
        // pessoa DIZ, o que se acha nela, o que isso É, o que se vai fazer. Ler a sessão
        // com a forma com que ela foi escrita é o que faz o prontuário parecer um sistema
        // só — e é por isso que os rótulos repetem os da tela de escrita, palavra por
        // palavra.
        Somar(blocos, "Queixa principal", e.QueixaPrincipal);
        Somar(blocos, "História da doença atual", e.HistoriaDoencaAtual);
        Somar(blocos, "Exame físico", e.ExameFisico);
        Somar(blocos, "Hipótese diagnóstica", DaHipotese(e));
        Somar(blocos, "Conduta", e.Conduta);
        Somar(blocos, "Evolução", e.TextoEvolucao);
        Somar(blocos, "Plano terapêutico", e.PlanoTerapeutico);
        Somar(blocos, "Orientações ao paciente", e.Orientacoes);
        Somar(blocos, "Retorno sugerido", DoRetorno(e));
        Somar(blocos, "Encaminhamento", e.Encaminhamento);

        return new SessaoDoProntuario(
            EvolucaoId: e.Id,
            Titulo: $"Sessão de {e.Data:dd/MM/yyyy}",
            Data: e.Data.ToString("dd/MM/yyyy"),
            Profissional: e.Profissional?.Nome ?? e.CriadoPor ?? "sem autor registrado",
            Eva: e.TemParEva
                ? $"EVA {e.EvaAntes} → {e.EvaDepois}"
                : e.EvaAntes is { } antes ? $"EVA {antes} (só antes)" : "EVA não medida",
            Blocos: blocos,
            Procedencia: DaProcedencia(e),
            AvisoCancelamento: DoCancelamento(e),
            Anexos: anexos,
            Correcoes: correcoes);
    }

    private static void Somar(List<BlocoDaSessao> blocos, string rotulo, string? texto)
    {
        // ⚠️ Bloco vazio SOME, nunca sai como "—". Numa janela de leitura, dez rótulos
        // sem conteúdo empurram para fora da vista os dois que foram escritos — e a
        // sessão de acupuntura mais comum da casa tem dois. É a regra do vão em branco
        // (parcela 47) aplicada a texto do prontuário.
        if (!string.IsNullOrWhiteSpace(texto))
            blocos.Add(new BlocoDaSessao(rotulo, texto.Trim()));
    }

    /// <summary>A hipótese com o CID entre parênteses — e o CID sozinho quando é só ele.
    /// MESMA regra do <see cref="ResumoSessaoAnterior"/>: duas leituras do mesmo par
    /// divergiriam, e a que ficasse para trás mostraria um CID sem o que ele significa.</summary>
    private static string? DaHipotese(Evolucao e)
    {
        var tem = !string.IsNullOrWhiteSpace(e.HipoteseDiagnostica);
        var cid = !string.IsNullOrWhiteSpace(e.CidSessao);

        return (tem, cid) switch
        {
            (true, true) => $"{e.HipoteseDiagnostica!.Trim()} ({e.CidSessao!.Trim()})",
            (true, false) => e.HipoteseDiagnostica!.Trim(),
            (false, true) => $"CID {e.CidSessao!.Trim()}",
            _ => null
        };
    }

    private static string? DoRetorno(Evolucao e)
    {
        if (e.RetornoSugeridoEm is not { } quando) return null;

        return string.IsNullOrWhiteSpace(e.RetornoSugeridoNota)
            ? $"Voltar em {quando:dd/MM/yyyy}"
            : $"Voltar em {quando:dd/MM/yyyy} — {e.RetornoSugeridoNota!.Trim()}";
    }

    /// <summary>
    /// Quem escreveu e quando — a procedência que a Lei 13.787/2018 pede (art. 3º) e que
    /// a lista não tinha onde mostrar. A data de ATUALIZAÇÃO sai ao lado da de criação:
    /// as duas juntas são o que diz que o registro foi mexido depois de escrito.
    /// </summary>
    private static string DaProcedencia(Evolucao e)
    {
        var quem = string.IsNullOrWhiteSpace(e.CriadoPor) ? "autor não registrado" : e.CriadoPor!.Trim();
        var texto = $"Registrado por {quem} em {e.CriadoEm:dd/MM/yyyy 'às' HH:mm}";

        if (e.AtualizadoEm is { } atualizado)
            texto += $" · última alteração em {atualizado:dd/MM/yyyy 'às' HH:mm}";

        return texto;
    }

    /// <summary>
    /// A sessão cancelada aparece MARCADA, nunca sumindo — ela esteve no prontuário, e o
    /// registro clínico não se apaga (parcela 52). O motivo vai JUNTO: linha cancelada sem
    /// o porquê deixa o próximo leitor sem saber se ela era falsa ou se foi um engano.
    /// </summary>
    private static string? DoCancelamento(Evolucao e)
    {
        if (e.CanceladaEm is not { } quando) return null;

        var quem = string.IsNullOrWhiteSpace(e.CanceladaPor) ? "?" : e.CanceladaPor!.Trim();
        var motivo = string.IsNullOrWhiteSpace(e.MotivoCancelamento)
            ? "sem motivo registrado"
            : e.MotivoCancelamento!.Trim();

        return $"Sessão CANCELADA por {quem} em {quando:dd/MM/yyyy} — {motivo}. "
             + "O registro fica no prontuário: ele não se apaga.";
    }
}
