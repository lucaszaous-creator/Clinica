namespace Clinica.Domain.Avaliacoes;

/// <summary>
/// As quatro alternativas de frequência que PHQ-9 e GAD-7 compartilham. Ficam num lugar
/// só porque são o MESMO conjunto nas duas escalas — duplicá-las faria a redação divergir
/// com o tempo, e a mesma pergunta apareceria escrita de dois jeitos no mesmo prontuário.
/// </summary>
internal static class FrequenciaDuasSemanas
{
    public static IReadOnlyList<OpcaoItem> Opcoes { get; } =
    [
        new(0, "Nenhum dia"),
        new(1, "Vários dias"),
        new(2, "Mais da metade dos dias"),
        new(3, "Quase todos os dias")
    ];

    public static ItemInstrumento Item(string codigo, string enunciado)
        => new(codigo, enunciado, Opcoes);
}

/// <summary>
/// PHQ-9 — rastreio e acompanhamento de sintomas depressivos.
///
/// É o instrumento que dá ao psiquiatra o que a EVA dá ao acupunturista: um número
/// comparável entre consultas. "Melhorou?" respondido de memória depende de como o
/// paciente está hoje; 18 → 9 em oito semanas é outra conversa.
///
/// O item 9 tem tratamento próprio (<see cref="AlertaDeItem"/>) e é o motivo de aquele
/// método existir.
/// </summary>
public sealed class InstrumentoPhq9 : IInstrumentoAvaliacao
{
    public const string CodigoInstrumento = "PHQ9";

    /// <summary>Item de ideação suicida — o que nunca pode se dissolver no total.</summary>
    public const string ItemIdeacao = "PHQ9_9";

    public string Codigo => CodigoInstrumento;
    public string Nome => "PHQ-9 — sintomas depressivos";
    public string Descricao => "Nove perguntas sobre as duas últimas semanas; escore de 0 a 27.";
    public string Fonte => "Patient Health Questionnaire (PHQ-9), Spitzer, Kroenke e Williams.";
    public string Janela => "Nas ÚLTIMAS DUAS SEMANAS, com que frequência você foi incomodado por…";

    public IReadOnlyList<string> Especialidades { get; } =
        [nameof(Especialidade.Psiquiatria), nameof(Especialidade.Geriatria)];

    public IReadOnlyList<ItemInstrumento> Itens { get; } =
    [
        FrequenciaDuasSemanas.Item("PHQ9_1", "Pouco interesse ou pouco prazer em fazer as coisas"),
        FrequenciaDuasSemanas.Item("PHQ9_2", "Sentir-se para baixo, deprimido ou sem perspectiva"),
        FrequenciaDuasSemanas.Item("PHQ9_3", "Dificuldade para pegar no sono, para continuar dormindo ou dormir demais"),
        FrequenciaDuasSemanas.Item("PHQ9_4", "Sentir-se cansado ou com pouca energia"),
        FrequenciaDuasSemanas.Item("PHQ9_5", "Falta de apetite ou comer demais"),
        FrequenciaDuasSemanas.Item("PHQ9_6", "Sentir-se mal consigo mesmo, achar que é um fracasso ou que decepcionou a família"),
        FrequenciaDuasSemanas.Item("PHQ9_7", "Dificuldade de se concentrar nas coisas (ler, ver televisão)"),
        FrequenciaDuasSemanas.Item("PHQ9_8", "Lentidão para se mover ou falar, ou o contrário — inquietação incomum"),
        FrequenciaDuasSemanas.Item(ItemIdeacao, "Pensar em se ferir de alguma maneira ou que seria melhor estar morto")
    ];

    public IReadOnlyList<FaixaInstrumento> Faixas { get; } =
    [
        new(0, 4, "Sintomas mínimos", "Sem indicação de acompanhamento por este escore.", GravidadeFaixa.Normal),
        new(5, 9, "Sintomas leves", "Acompanhar e repetir a escala na próxima consulta.", GravidadeFaixa.Atencao),
        new(10, 14, "Sintomas moderados", "Faixa em que a conduta costuma mudar; avaliar plano de tratamento.", GravidadeFaixa.Atencao),
        new(15, 19, "Sintomas moderadamente graves", "Escore alto; reavaliar tratamento e frequência das consultas.", GravidadeFaixa.Alerta),
        new(20, 27, "Sintomas graves", "Escore no topo da escala; reavaliação clínica prioritária.", GravidadeFaixa.Alerta)
    ];

    /// <summary>
    /// Qualquer resposta diferente de "nenhum dia" no item 9 vira alerta, mesmo com escore
    /// total baixo. A escala publica o item somado; quem atende precisa dele separado.
    /// </summary>
    public string? AlertaDeItem(IReadOnlyDictionary<string, int> respostas)
        => respostas.TryGetValue(ItemIdeacao, out var v) && v > 0
            ? "O paciente respondeu ao item 9 (pensamentos de morte ou de se ferir). "
              + "Este item exige avaliação de risco AGORA, independentemente do escore total."
            : null;
}

/// <summary>
/// GAD-7 — sintomas de ansiedade. Sete itens, mesma janela e as mesmas alternativas do
/// PHQ-9, o que na prática significa que as duas escalas são aplicadas na mesma consulta
/// sem o paciente ter de reaprender a responder.
/// </summary>
public sealed class InstrumentoGad7 : IInstrumentoAvaliacao
{
    public const string CodigoInstrumento = "GAD7";

    public string Codigo => CodigoInstrumento;
    public string Nome => "GAD-7 — sintomas de ansiedade";
    public string Descricao => "Sete perguntas sobre as duas últimas semanas; escore de 0 a 21.";
    public string Fonte => "Generalized Anxiety Disorder 7-item scale (GAD-7), Spitzer e cols.";
    public string Janela => "Nas ÚLTIMAS DUAS SEMANAS, com que frequência você foi incomodado por…";

    public IReadOnlyList<string> Especialidades { get; } =
        [nameof(Especialidade.Psiquiatria), nameof(Especialidade.Geriatria)];

    public IReadOnlyList<ItemInstrumento> Itens { get; } =
    [
        FrequenciaDuasSemanas.Item("GAD7_1", "Sentir-se nervoso, ansioso ou muito tenso"),
        FrequenciaDuasSemanas.Item("GAD7_2", "Não conseguir parar ou controlar as preocupações"),
        FrequenciaDuasSemanas.Item("GAD7_3", "Preocupar-se muito com diversas coisas"),
        FrequenciaDuasSemanas.Item("GAD7_4", "Dificuldade para relaxar"),
        FrequenciaDuasSemanas.Item("GAD7_5", "Ficar tão agitado que se torna difícil permanecer sentado"),
        FrequenciaDuasSemanas.Item("GAD7_6", "Ficar facilmente aborrecido ou irritado"),
        FrequenciaDuasSemanas.Item("GAD7_7", "Sentir medo como se algo terrível fosse acontecer")
    ];

    public IReadOnlyList<FaixaInstrumento> Faixas { get; } =
    [
        new(0, 4, "Ansiedade mínima", "Sem indicação de acompanhamento por este escore.", GravidadeFaixa.Normal),
        new(5, 9, "Ansiedade leve", "Acompanhar e repetir a escala na próxima consulta.", GravidadeFaixa.Atencao),
        new(10, 14, "Ansiedade moderada", "Faixa em que a conduta costuma mudar; avaliar plano de tratamento.", GravidadeFaixa.Atencao),
        new(15, 21, "Ansiedade grave", "Escore no topo da escala; reavaliação clínica prioritária.", GravidadeFaixa.Alerta)
    ];
}
