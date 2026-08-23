using Clinica.Domain.Entities;

namespace Clinica.Domain.Prontuario;

/// <summary>
/// As NATUREZAS do registro clínico — a lista que quatro leitores compartilham.
///
/// ⚠️ Ela nasce porque o defeito recorrente do projeto já foi cometido DUAS vezes neste
/// exato assunto, e os comentários confessam: <c>GuardaProntuarioService</c> esqueceu a
/// folha de infusão na primeira versão ("o que contrariava a própria regra 8 do
/// CLAUDE.md") e <c>ExportacaoProntuarioService</c> esqueceu a lista de problemas — onde
/// moram as ALERGIAS. E <c>SituacaoGuarda</c> contava cinco naturezas enquanto o prazo era
/// calculado sobre sete: a tela que responde ao auditor <i>"o que vocês guardam por 20
/// anos"</i> mostrava "0 sessão · 0 medida · 0 prescrição" com uma data de guarda vinda de
/// lugar nenhum visível, para a ficha cujo único registro era enfermagem. <b>Número errado
/// com cara de exato, na tela de conformidade.</b>
///
/// A regra que fica: <b>entidade clínica nova entra em UMA lista, e quatro leitores a
/// consomem</b> — a linha do tempo do paciente, a guarda, a exportação e o direito do
/// titular. Sem isso, o que um esquecer aparece como <b>lista limpa</b>, indistinguível de
/// "não houve nada". <c>ConjuntoClinicoTests</c> falha no commit em que a próxima nascer.
///
/// ⚠️ Isto é uma lista de NOMES, não de consultas. Cada leitor continua sabendo ler o que
/// é dele; o que o catálogo garante é que nenhum deles esqueça uma natureza inteira.
/// </summary>
public enum NaturezaRegistroClinico
{
    /// <summary>A sessão escrita pelo profissional: queixa, conduta, evolução, EVA.</summary>
    SessaoMedica,

    /// <summary>A passagem escrita por quem executa: sinais vitais, intercorrência.</summary>
    EvolucaoEnfermagem,

    /// <summary>A folha de infusão e a execução dela.</summary>
    PrescricaoInterna,

    /// <summary>Receita, atestado, comparecimento, pedido, relatório, termo.</summary>
    DocumentoClinico,

    /// <summary>Escala aplicada (PHQ-9, GAD-7, Oswestry, Katz, FINDRISC).</summary>
    AvaliacaoClinica,

    /// <summary>Peso, altura, pressão, glicemia — a colheita seriada.</summary>
    MedidaClinica,

    /// <summary>A lista de problemas: diagnósticos e ALERGIAS.</summary>
    ProblemaPaciente,

    /// <summary>Laudo, foto de lesão — o que a sessão carrega junto.</summary>
    Anexo,

    /// <summary>Os pontos marcados na figura, o registro gráfico do que foi feito.</summary>
    MapaCorporal
}

/// <summary>O que o sistema sabe sobre uma natureza: como se chama e quem pode lê-la.</summary>
/// <param name="Singular">"sessão", "evolução de enfermagem"…</param>
/// <param name="Plural">Para a contagem: "3 sessões".</param>
/// <param name="PermissaoVer">
/// O PISO para alcançar esta natureza — nunca o teto.
///
/// Quase toda natureza clínica é dado de saúde (art. 5º, II) e exige
/// <see cref="Permissao.VerProntuario"/>. O campo existe declarado, e não implícito, porque
/// é ele que o montador da linha do tempo consulta: regra de LGPD repetida em três telas é
/// regra que a quarta esquece, e o erro aparece como uma linha a mais numa lista, que
/// ninguém percebe.
///
/// ⚠️ <see cref="NaturezaRegistroClinico.DocumentoClinico"/> é a exceção, e ela é o motivo
/// de este campo ser PISO: o acesso do documento é do PAPEL, não da natureza — a declaração
/// de comparecimento e o termo de consentimento LGPD não carregam dado de saúde e saem do
/// balcão o dia inteiro (parcela 59). Declará-la como <c>VerProntuario</c> fazia o portão
/// engolir os dois ANTES de o filtro por folha rodar, e quem tem só o cadastro recebia
/// lista vazia. Onde a natureza tem regra POR ITEM, quem decide é o filtro do item.
/// </param>
public sealed record InfoRegistroClinico(
    NaturezaRegistroClinico Natureza,
    string Singular,
    string Plural,
    Permissao PermissaoVer);

public static class CatalogoRegistroClinico
{
    private static readonly IReadOnlyList<InfoRegistroClinico> Lista =
    [
        new(NaturezaRegistroClinico.SessaoMedica,
            "sessão", "sessões", Permissao.VerProntuario),
        new(NaturezaRegistroClinico.EvolucaoEnfermagem,
            "evolução de enfermagem", "evoluções de enfermagem", Permissao.VerProntuario),
        new(NaturezaRegistroClinico.PrescricaoInterna,
            "prescrição de infusão", "prescrições de infusão", Permissao.VerProntuario),
        // ⚠️ PISO, não teto — ver o comentário de InfoRegistroClinico.PermissaoVer. Quem
        // decide papel a papel é FolhaCatalogo.PermissaoVer, no montador.
        new(NaturezaRegistroClinico.DocumentoClinico,
            "documento emitido", "documentos emitidos", Permissao.VerFichaPaciente),
        new(NaturezaRegistroClinico.AvaliacaoClinica,
            "avaliação", "avaliações", Permissao.VerProntuario),
        new(NaturezaRegistroClinico.MedidaClinica,
            "medida", "medidas", Permissao.VerProntuario),
        new(NaturezaRegistroClinico.ProblemaPaciente,
            "problema anotado", "problemas anotados", Permissao.VerProntuario),
        new(NaturezaRegistroClinico.Anexo,
            "anexo", "anexos", Permissao.VerProntuario),
        new(NaturezaRegistroClinico.MapaCorporal,
            "mapa corporal", "mapas corporais", Permissao.VerProntuario)
    ];

    /// <summary>Todas as naturezas, na ordem em que se lê o prontuário.</summary>
    public static IReadOnlyList<InfoRegistroClinico> Todas => Lista;

    public static InfoRegistroClinico Obter(NaturezaRegistroClinico natureza)
        => Lista.First(i => i.Natureza == natureza);

    public static string Rotular(NaturezaRegistroClinico natureza)
        => Obter(natureza).Singular;

    /// <summary>"3 sessões", "1 medida" — com o plural certo e sem "(s)".</summary>
    public static string Contar(NaturezaRegistroClinico natureza, int quantas)
    {
        var info = Obter(natureza);
        return $"{quantas} {(quantas == 1 ? info.Singular : info.Plural)}";
    }
}
