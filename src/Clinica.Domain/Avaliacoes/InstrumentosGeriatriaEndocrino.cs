namespace Clinica.Domain.Avaliacoes;

/// <summary>
/// Katz — independência nas atividades básicas de vida diária (ABVD).
///
/// É a pergunta que a geriatria faz e que nenhuma outra tela do sistema respondia: não
/// "o que ele tem", mas "o que ele ainda faz sozinho". Seis itens, cada um valendo 1 para
/// independente e 0 para dependente.
///
/// **Aqui o escore ALTO é o bom** — ao contrário de todas as outras escalas do catálogo.
/// Por isso as faixas invertem a gravidade, e é bom que a leitura fique escrita: uma tela
/// que pintasse "6 pontos" de vermelho por ser o maior número diria exatamente o
/// contrário do que o resultado significa.
/// </summary>
public sealed class InstrumentoKatz : IInstrumentoAvaliacao
{
    public const string CodigoInstrumento = "KATZ";

    private static readonly IReadOnlyList<OpcaoItem> SimNao =
    [
        new(1, "Independente — faz sozinho"),
        new(0, "Dependente — precisa de ajuda")
    ];

    public string Codigo => CodigoInstrumento;
    public string Nome => "Katz — atividades básicas de vida diária";
    public string Descricao => "Seis atividades do dia a dia; escore de 0 a 6 (6 = totalmente independente).";
    public string Fonte => "Index of Independence in Activities of Daily Living (Katz e cols.).";
    public string Janela => "Considere o que a pessoa faz DE FATO no dia a dia, não o que conseguiria fazer.";

    public IReadOnlyList<string> Especialidades { get; } = [nameof(Especialidade.Geriatria)];

    public IReadOnlyList<ItemInstrumento> Itens { get; } =
    [
        new("KATZ_1", "Banho — entra e sai do chuveiro e se lava sozinho", SimNao),
        new("KATZ_2", "Vestir-se — pega a roupa e se veste sozinho, inclusive fechos e sapatos", SimNao),
        new("KATZ_3", "Uso do banheiro — vai ao vaso, se limpa e se arruma sozinho", SimNao),
        new("KATZ_4", "Transferência — deita e levanta da cama e da cadeira sem ajuda", SimNao),
        new("KATZ_5", "Continência — controla urina e fezes sozinho", SimNao),
        new("KATZ_6", "Alimentação — leva a comida do prato à boca sozinha", SimNao)
    ];

    /// <summary>
    /// Faixas de escore ALTO para BAIXO em qualidade: 6 é o melhor resultado possível.
    /// A ordem da lista continua crescente para casar com <c>FaixaDe</c>, que devolve a
    /// primeira faixa que contém o escore.
    /// </summary>
    public IReadOnlyList<FaixaInstrumento> Faixas { get; } =
    [
        new(0, 2, "Dependência importante", "Depende de terceiros na maior parte das ABVD; avaliar rede de apoio e segurança em casa.", GravidadeFaixa.Alerta),
        new(3, 4, "Dependência moderada", "Perda funcional instalada; investigar causa reversível e planejar reabilitação.", GravidadeFaixa.Alerta),
        new(5, 5, "Dependência leve", "Uma atividade comprometida — o momento de intervir, antes de virar duas.", GravidadeFaixa.Atencao),
        new(6, 6, "Independente", "Independente nas seis atividades básicas.", GravidadeFaixa.Normal)
    ];

    /// <summary>
    /// Único instrumento do catálogo em que SUBIR é melhorar. Sem isto, a curva de
    /// evolução mostraria a recuperação funcional do paciente como piora.
    /// </summary>
    public bool MelhorQuandoMenor => false;
}

/// <summary>
/// FINDRISC — risco de desenvolver diabetes tipo 2 nos próximos dez anos.
///
/// É a escala da endocrinologia que não depende de exame: responde-se no consultório, em
/// dois minutos, e dá o número que faz o paciente aceitar a coleta. Duas particularidades
/// que a redação dos itens precisa carregar:
///
/// - a circunferência abdominal tem cortes DIFERENTES por sexo, e as duas medidas vão no
///   rótulo da mesma alternativa — é assim que o formulário em papel faz, e separar em
///   dois itens obrigaria o sistema a saber o sexo para montar o questionário;
/// - "glicemia alta detectada alguma vez" vale 5 pontos sozinho, quase um quinto da
///   escala. Não é erro de peso: é o item com maior valor preditivo do instrumento.
/// </summary>
public sealed class InstrumentoFindrisc : IInstrumentoAvaliacao
{
    public const string CodigoInstrumento = "FINDRISC";

    public string Codigo => CodigoInstrumento;
    public string Nome => "FINDRISC — risco de diabetes tipo 2";
    public string Descricao => "Oito perguntas sem exame de sangue; escore de 0 a 26.";
    public string Fonte => "Finnish Diabetes Risk Score (FINDRISC), Lindström e Tuomilehto.";
    public string Janela => "Responda sobre a situação ATUAL do paciente.";

    public IReadOnlyList<string> Especialidades { get; } = [nameof(Especialidade.Endocrinologia)];

    public IReadOnlyList<ItemInstrumento> Itens { get; } =
    [
        new("FDR_IDADE", "Idade",
        [
            new(0, "Menos de 45 anos"),
            new(2, "45 a 54 anos"),
            new(3, "55 a 64 anos"),
            new(4, "Mais de 64 anos")
        ]),
        new("FDR_IMC", "Índice de massa corporal (peso ÷ altura²)",
        [
            new(0, "Menos de 25 kg/m²"),
            new(1, "25 a 30 kg/m²"),
            new(3, "Mais de 30 kg/m²")
        ]),
        new("FDR_CINTURA", "Circunferência abdominal, medida na altura do umbigo",
        [
            new(0, "Homens: menos de 94 cm · Mulheres: menos de 80 cm"),
            new(3, "Homens: 94 a 102 cm · Mulheres: 80 a 88 cm"),
            new(4, "Homens: mais de 102 cm · Mulheres: mais de 88 cm")
        ]),
        new("FDR_ATIVIDADE", "Faz pelo menos 30 minutos de atividade física por dia, no trabalho ou no lazer?",
        [
            new(0, "Sim"),
            new(2, "Não")
        ]),
        new("FDR_ALIMENTACAO", "Come frutas, verduras ou legumes todos os dias?",
        [
            new(0, "Sim"),
            new(1, "Não")
        ]),
        new("FDR_PRESSAO", "Já tomou remédio para pressão alta com regularidade?",
        [
            new(0, "Não"),
            new(2, "Sim")
        ]),
        new("FDR_GLICEMIA", "Já teve glicose alta detectada alguma vez (exame de rotina, doença, gravidez)?",
        [
            new(0, "Não"),
            new(5, "Sim")
        ]),
        new("FDR_FAMILIA", "Algum familiar tem ou teve diabetes?",
        [
            new(0, "Não"),
            new(3, "Sim — avós, tios ou primos de primeiro grau"),
            new(5, "Sim — pais, irmãos ou filhos")
        ])
    ];

    public IReadOnlyList<FaixaInstrumento> Faixas { get; } =
    [
        new(0, 6, "Risco baixo", "Cerca de 1 em 100 desenvolve diabetes em dez anos.", GravidadeFaixa.Normal),
        new(7, 11, "Risco levemente elevado", "Cerca de 1 em 25 em dez anos; orientação de hábitos.", GravidadeFaixa.Normal),
        new(12, 14, "Risco moderado", "Cerca de 1 em 6 em dez anos; considerar glicemia de jejum.", GravidadeFaixa.Atencao),
        new(15, 20, "Risco alto", "Cerca de 1 em 3 em dez anos; investigação laboratorial indicada.", GravidadeFaixa.Alerta),
        new(21, 26, "Risco muito alto", "Cerca de 1 em 2 em dez anos; investigação laboratorial prioritária.", GravidadeFaixa.Alerta)
    ];
}
