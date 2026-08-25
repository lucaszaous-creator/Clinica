namespace Clinica.Domain.Prontuario;

/// <summary>Um diagnóstico de enfermagem do catálogo, com os cuidados que ele costuma pedir.</summary>
/// <param name="Codigo">Estável — é ele que a colheita copia como procedência.</param>
/// <param name="Titulo">O problema, na redação que a enfermeira vai ver e pode editar.</param>
/// <param name="CausasComuns">Sugestões para o "relacionado a" — a causa provável.</param>
/// <param name="EvidenciasComuns">Sugestões para o "evidenciado por" — o achado que sustenta.</param>
/// <param name="ResultadoEsperado">O planejamento sugerido (etapa 3).</param>
/// <param name="Cuidados">Os códigos dos cuidados que este diagnóstico costuma pedir.</param>
public sealed record DiagnosticoCatalogo(
    string Codigo,
    string Titulo,
    IReadOnlyList<string> CausasComuns,
    IReadOnlyList<string> EvidenciasComuns,
    string ResultadoEsperado,
    IReadOnlyList<string> Cuidados);

/// <summary>Um cuidado de enfermagem do catálogo.</summary>
public sealed record CuidadoCatalogo(
    string Codigo,
    string Descricao,
    string FrequenciaSugerida);

/// <summary>
/// O CATÁLOGO DO PROCESSO DE ENFERMAGEM (parcela 73) — atalho com conferência, nunca a
/// lista fechada do que a enfermagem pode diagnosticar.
///
/// Onde ele mora, e por quê
/// ------------------------
/// Em CÓDIGO, pelo desenho das escalas clínicas (parcela 36) e do <c>CatalogoMedidas</c>
/// (37): a redação de um diagnóstico de enfermagem não é configuração da clínica, e deixar
/// editar o texto produziria um registro que continua se chamando "risco de infecção" sem
/// ser. O que a clínica faz é ESCOLHER e ajustar na colheita — e o que ela ajusta fica
/// COPIADO naquele registro, sem reescrever os anteriores.
///
/// ⚠️ O QUE ELE NÃO É — e a tela diz isso
/// --------------------------------------
/// <b>Não é a NANDA-I.</b> A taxonomia NANDA International é licenciada, e importá-la sem
/// licença é o mesmo defeito do carimbo escaneado da parcela 3, vestido de conformidade:
/// pareceria a taxonomia oficial sem ser. Esta é a <b>lista desta clínica</b> — os
/// diagnósticos que uma clínica de infusão, acupuntura e reabilitação de fato usa, com a
/// redação em três partes que a COFEN cobra.
///
/// <b>Não é lista fechada.</b> O campo aceita texto livre, e diagnóstico escrito à mão
/// grava com <c>Codigo</c> nulo. Recusar o que está fora da lista seria a regra apertada
/// demais que o projeto já rejeitou no formato do número da guia e no CID-10: ela recusa o
/// registro legítimo e trava quem está com o paciente na frente.
///
/// <b>Não decide nada clinicamente.</b> Ele oferece a redação e os cuidados que costumam
/// acompanhar; quem diagnostica é quem tem o COREN.
/// </summary>
public static class CatalogoEnfermagem
{
    // ---- Cuidados ----

    private static readonly IReadOnlyList<CuidadoCatalogo> ListaCuidados =
    [
        new("ACESSO-VER", "Verificar o acesso venoso: retorno, sinais flogísticos e fixação",
            "a cada 1h durante a infusão"),
        new("ACESSO-TROCA", "Trocar o dispositivo de acesso periférico", "a cada 72–96h ou se sinal de flebite"),
        new("SV-MONITOR", "Aferir sinais vitais", "antes, durante e ao término da infusão"),
        new("REACAO-VIGIAR", "Observar sinais de reação: prurido, rubor, dispneia, náusea",
            "durante toda a infusão e 20 min após"),
        new("CURATIVO-TROCA", "Trocar o curativo com técnica asséptica", "a cada 24h ou se sujidade"),
        new("FERIDA-AVALIAR", "Avaliar a ferida: leito, bordas, exsudato e odor", "a cada troca"),
        new("PELE-INSPECAO", "Inspecionar a pele em áreas de pressão e proeminências ósseas", "a cada turno"),
        new("DOR-AVALIAR", "Avaliar a dor pela escala numérica (0–10)", "a cada 2h e após intervenção"),
        new("POSICAO-CONFORTO", "Posicionar em posição de conforto e orientar mudança de decúbito",
            "a cada 2h"),
        new("MEMBRO-ELEVAR", "Manter o membro puncionado elevado e em repouso relativo", "contínuo"),
        new("HIDRATACAO", "Estimular a ingesta hídrica, salvo restrição", "ao longo do atendimento"),
        new("ORIENTAR-SINAIS", "Orientar paciente e acompanhante sobre sinais de alerta e quando retornar",
            "na alta"),
        new("ORIENTAR-AUTOCUIDADO", "Orientar o autocuidado no domicílio e conferir a compreensão",
            "na alta"),
        new("QUEDA-PREVENIR", "Manter grades elevadas, campainha ao alcance e piso seco", "contínuo"),
        new("DEAMBULAR-APOIO", "Auxiliar a deambulação e a transferência", "a cada saída do leito"),
        new("ANSIEDADE-ACOLHER", "Acolher, explicar cada etapa do procedimento e permitir acompanhante",
            "durante o atendimento"),
        new("JEJUM-CONFERIR", "Conferir e registrar o tempo de jejum antes do procedimento", "na admissão"),
        new("ALERGIA-CONFERIR", "Conferir alergias registradas antes de qualquer administração",
            "antes de cada item"),
        new("BALANCO-REGISTRAR", "Registrar o volume infundido e as eliminações", "ao término")
    ];

    // ---- Diagnósticos ----

    private static readonly IReadOnlyList<DiagnosticoCatalogo> ListaDiagnosticos =
    [
        new("DOR-AGUDA", "Dor aguda",
            ["espasmo muscular", "processo inflamatório", "procedimento invasivo", "lesão tecidual"],
            ["relato verbal de dor", "escala numérica ≥ 4", "postura antálgica", "fácies de dor"],
            "Paciente refere redução da dor para ≤ 3 na escala numérica ao final do atendimento.",
            ["DOR-AVALIAR", "POSICAO-CONFORTO", "ORIENTAR-SINAIS"]),

        new("DOR-CRONICA", "Dor crônica",
            ["doença degenerativa", "condição musculoesquelética de longa data"],
            ["relato de dor há mais de três meses", "limitação funcional referida"],
            "Paciente relata melhora funcional e adesão às orientações entre as sessões.",
            ["DOR-AVALIAR", "ORIENTAR-AUTOCUIDADO"]),

        new("INFECCAO-RISCO", "Risco de infecção",
            ["procedimento invasivo", "acesso venoso periférico", "solução de continuidade da pele"],
            ["presença de dispositivo intravenoso", "ferida aberta"],
            "Paciente permanece sem sinais flogísticos no sítio de punção durante o atendimento.",
            ["ACESSO-VER", "CURATIVO-TROCA", "ALERGIA-CONFERIR"]),

        new("INTEGRIDADE-PELE", "Integridade da pele prejudicada",
            ["punção venosa", "pressão prolongada", "processo cicatricial"],
            ["presença de ferida", "hiperemia em proeminência óssea", "extravasamento"],
            "Ferida com redução do exsudato e bordas sem sinais de infecção na reavaliação.",
            ["FERIDA-AVALIAR", "CURATIVO-TROCA", "PELE-INSPECAO"]),

        new("PERFUSAO-RISCO", "Risco de perfusão tissular periférica ineficaz",
            ["infiltração ou extravasamento", "compressão do membro puncionado"],
            ["edema no sítio de punção", "redução do retorno venoso", "palidez ou frialdade distal"],
            "Membro puncionado sem edema, com perfusão distal preservada ao término.",
            ["ACESSO-VER", "MEMBRO-ELEVAR", "SV-MONITOR"]),

        new("REACAO-RISCO", "Risco de resposta alérgica",
            ["administração de medicamento endovenoso", "história prévia de alergia"],
            ["alergia registrada no prontuário", "relato de reação anterior"],
            "Paciente conclui a infusão sem manifestação alérgica.",
            ["ALERGIA-CONFERIR", "REACAO-VIGIAR", "SV-MONITOR"]),

        new("NAUSEA", "Náusea",
            ["efeito de medicamento", "ansiedade", "jejum prolongado"],
            ["relato de náusea", "episódio de vômito", "palidez e sudorese"],
            "Paciente refere ausência de náusea e tolera a infusão até o término.",
            ["SV-MONITOR", "POSICAO-CONFORTO", "HIDRATACAO"]),

        new("ANSIEDADE", "Ansiedade",
            ["procedimento desconhecido", "medo de agulha", "expectativa quanto ao resultado"],
            ["relato de apreensão", "inquietação", "taquicardia sem causa clínica"],
            "Paciente verbaliza sentir-se mais seguro e coopera com o procedimento.",
            ["ANSIEDADE-ACOLHER", "ORIENTAR-SINAIS"]),

        new("QUEDA-RISCO", "Risco de queda",
            ["idade avançada", "sedação", "hipotensão postural", "limitação de marcha"],
            ["marcha instável", "história de queda", "uso de dispositivo de auxílio"],
            "Paciente permanece sem eventos de queda durante a permanência na clínica.",
            ["QUEDA-PREVENIR", "DEAMBULAR-APOIO", "SV-MONITOR"]),

        new("MOBILIDADE", "Mobilidade física prejudicada",
            ["dor", "lesão musculoesquelética", "pós-operatório"],
            ["amplitude de movimento reduzida", "necessidade de auxílio para transferência"],
            "Paciente realiza a transferência com menor auxílio ao final do plano.",
            ["DEAMBULAR-APOIO", "POSICAO-CONFORTO", "ORIENTAR-AUTOCUIDADO"]),

        new("CONHECIMENTO", "Conhecimento deficiente sobre o tratamento",
            ["primeira sessão", "informação insuficiente", "barreira de compreensão"],
            ["perguntas repetidas sobre o procedimento", "relato de dúvida quanto ao uso da medicação"],
            "Paciente explica com as próprias palavras o cuidado e os sinais de alerta.",
            ["ORIENTAR-AUTOCUIDADO", "ORIENTAR-SINAIS"]),

        new("ADESAO", "Risco de adesão prejudicada ao plano terapêutico",
            ["esquema terapêutico complexo", "faltas anteriores", "dificuldade de deslocamento"],
            ["sessões perdidas", "relato de dificuldade em manter o tratamento"],
            "Paciente comparece às sessões programadas do período.",
            ["ORIENTAR-AUTOCUIDADO", "ANSIEDADE-ACOLHER"]),

        new("VOLUME-DESEQUILIBRIO", "Risco de desequilíbrio no volume de líquidos",
            ["infusão endovenosa", "restrição hídrica", "perdas aumentadas"],
            ["balanço hídrico alterado", "edema", "mucosas ressecadas"],
            "Paciente mantém balanço hídrico adequado ao término da infusão.",
            ["BALANCO-REGISTRAR", "SV-MONITOR", "HIDRATACAO"])
    ];

    public static IReadOnlyList<DiagnosticoCatalogo> Diagnosticos => ListaDiagnosticos;

    public static IReadOnlyList<CuidadoCatalogo> Cuidados => ListaCuidados;

    public static DiagnosticoCatalogo? Diagnostico(string? codigo)
        => codigo is null ? null : ListaDiagnosticos.FirstOrDefault(d => d.Codigo == codigo);

    public static CuidadoCatalogo? Cuidado(string? codigo)
        => codigo is null ? null : ListaCuidados.FirstOrDefault(c => c.Codigo == codigo);

    /// <summary>
    /// Os cuidados que este diagnóstico costuma pedir, já resolvidos.
    ///
    /// ⚠️ SUGESTÃO, e a tela trata como tal: eles entram marcados para a enfermeira
    /// desmarcar o que não vale para este paciente. Aplicar sozinho produziria uma
    /// prescrição de enfermagem que ninguém leu — e cuidado prescrito é cuidado que alguém
    /// vai ter de checar depois.
    /// </summary>
    public static IReadOnlyList<CuidadoCatalogo> CuidadosDe(string codigoDiagnostico)
        => Diagnostico(codigoDiagnostico) is not { } d
            ? []
            : d.Cuidados.Select(Cuidado).Where(c => c is not null).Select(c => c!).ToList();

    /// <summary>Busca por trecho, sem acento e sem caixa — o mesmo desenho da busca de CID.</summary>
    public static IReadOnlyList<DiagnosticoCatalogo> BuscarDiagnosticos(string? termo)
    {
        if (string.IsNullOrWhiteSpace(termo)) return ListaDiagnosticos;
        var alvo = Normalizar(termo);
        return ListaDiagnosticos
            .Where(d => Normalizar(d.Titulo).Contains(alvo)
                        || d.CausasComuns.Any(c => Normalizar(c).Contains(alvo)))
            .ToList();
    }

    public static IReadOnlyList<CuidadoCatalogo> BuscarCuidados(string? termo)
    {
        if (string.IsNullOrWhiteSpace(termo)) return ListaCuidados;
        var alvo = Normalizar(termo);
        return ListaCuidados.Where(c => Normalizar(c.Descricao).Contains(alvo)).ToList();
    }

    private static string Normalizar(string texto)
        => new string(texto.Normalize(System.Text.NormalizationForm.FormD)
                .Where(c => System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c)
                            != System.Globalization.UnicodeCategory.NonSpacingMark)
                .ToArray())
            .ToLowerInvariant();
}
