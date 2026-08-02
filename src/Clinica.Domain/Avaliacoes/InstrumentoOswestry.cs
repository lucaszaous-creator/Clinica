namespace Clinica.Domain.Avaliacoes;

/// <summary>
/// Oswestry (ODI) — o quanto a dor lombar atrapalha a VIDA, e não o quanto ela dói.
///
/// É a escala que fecha o par com a EVA no consultório da dor: a EVA responde "quanto
/// dói agora", medida sessão a sessão; o Oswestry responde "o que você deixou de fazer",
/// medido a cada poucas semanas. Os dois números discordam com frequência — e é
/// justamente a discordância que muda conduta: o paciente que saiu de EVA 8 para 5 e
/// continua sem conseguir calçar o sapato não está respondendo ao tratamento, por mais
/// que a curva da dor esteja bonita.
///
/// **Nove seções, não dez.** A décima do instrumento original é sobre vida sexual, e a
/// prática — no Brasil e fora dele — é aplicá-la omitida na maioria dos serviços. Como a
/// própria regra do ODI manda calcular o percentual sobre as seções RESPONDIDAS, omiti-la
/// não é adaptação caseira: é o cálculo previsto. O <see cref="Fonte"/> diz isso, para
/// ninguém comparar com um ODI de dez seções achando que é o mesmo número.
/// </summary>
public sealed class InstrumentoOswestry : IInstrumentoAvaliacao
{
    public const string CodigoInstrumento = "ODI";

    /// <summary>
    /// Código da NEUROCIRURGIA no catálogo de especialidades.
    ///
    /// Ela não é valor do enum <see cref="Especialidade"/>, e isso é decisão, não
    /// esquecimento: o enum é lido pelo app de faturamento CONGELADO — que garante as
    /// embutidas no catálogo na abertura e as oferece no lançamento do atendimento —, e
    /// acrescentar um valor ali faria uma opção nova aparecer na tela de um app em
    /// produção sem ninguém ter pedido.
    ///
    /// Como código do catálogo, a especialidade só existe quando a clínica a cadastra em
    /// Configurações. Até lá o Oswestry continua sendo oferecido à acupuntura e à clínica
    /// da dor, que são embutidas; assim que ela for cadastrada, passa a valer aqui também,
    /// sem recompilar nada.
    /// </summary>
    public const string CodigoNeurocirurgia = "Neurocirurgia";

    public string Codigo => CodigoInstrumento;
    public string Nome => "Oswestry (ODI) — incapacidade por dor lombar";
    public string Descricao => "Nove seções sobre o dia a dia; resultado em % de incapacidade.";

    public string Fonte =>
        "Oswestry Disability Index (ODI 2.0), Fairbank e Pynsent — versão de nove seções "
        + "(sem a seção de vida sexual), com o percentual calculado sobre as respondidas.";

    public string Janela => "Marque, em cada bloco, a frase que descreve você HOJE.";

    public string Unidade => "%";

    public IReadOnlyList<string> Especialidades { get; } =
    [
        CodigoNeurocirurgia,
        nameof(Especialidade.Acupuntura),
        nameof(Especialidade.ClinicaDaDor)
    ];

    public IReadOnlyList<ItemInstrumento> Itens { get; } =
    [
        new("ODI_1", "Intensidade da dor",
        [
            new(0, "Não tenho dor no momento"),
            new(1, "A dor é muito leve no momento"),
            new(2, "A dor é moderada no momento"),
            new(3, "A dor é bastante intensa no momento"),
            new(4, "A dor é muito intensa no momento"),
            new(5, "A dor é a pior imaginável no momento")
        ]),
        new("ODI_2", "Cuidados pessoais (lavar-se, vestir-se)",
        [
            new(0, "Cuido de mim normalmente, sem aumentar a dor"),
            new(1, "Cuido de mim normalmente, mas isso aumenta a dor"),
            new(2, "Cuidar de mim dói e faço devagar e com cuidado"),
            new(3, "Preciso de alguma ajuda, mas faço a maior parte sozinho"),
            new(4, "Preciso de ajuda todos os dias na maior parte das tarefas"),
            new(5, "Não me visto, lavo-me com dificuldade e fico na cama")
        ]),
        new("ODI_3", "Levantar peso",
        [
            new(0, "Levanto peso sem aumentar a dor"),
            new(1, "Levanto peso, mas isso aumenta a dor"),
            new(2, "A dor me impede de levantar peso do chão, mas consigo se estiver bem posicionado (ex.: sobre a mesa)"),
            new(3, "A dor me impede de levantar peso, mas consigo com pesos leves bem posicionados"),
            new(4, "Consigo levantar apenas objetos muito leves"),
            new(5, "Não consigo levantar nem carregar nada")
        ]),
        new("ODI_4", "Andar",
        [
            new(0, "A dor não me impede de andar qualquer distância"),
            new(1, "A dor me impede de andar mais de 1,5 km"),
            new(2, "A dor me impede de andar mais de 500 m"),
            new(3, "A dor me impede de andar mais de 100 m"),
            new(4, "Só ando com bengala ou muletas"),
            new(5, "Fico na cama a maior parte do tempo e me arrasto até o banheiro")
        ]),
        new("ODI_5", "Sentar",
        [
            new(0, "Sento em qualquer cadeira pelo tempo que quiser"),
            new(1, "Sento na minha cadeira preferida pelo tempo que quiser"),
            new(2, "A dor me impede de ficar sentado mais de uma hora"),
            new(3, "A dor me impede de ficar sentado mais de meia hora"),
            new(4, "A dor me impede de ficar sentado mais de dez minutos"),
            new(5, "A dor me impede de sentar")
        ]),
        new("ODI_6", "Ficar em pé",
        [
            new(0, "Fico em pé o tempo que quiser, sem aumentar a dor"),
            new(1, "Fico em pé o tempo que quiser, mas isso aumenta a dor"),
            new(2, "A dor me impede de ficar em pé mais de uma hora"),
            new(3, "A dor me impede de ficar em pé mais de meia hora"),
            new(4, "A dor me impede de ficar em pé mais de dez minutos"),
            new(5, "A dor me impede de ficar em pé")
        ]),
        new("ODI_7", "Dormir",
        [
            new(0, "Meu sono nunca é perturbado pela dor"),
            new(1, "Meu sono é perturbado de vez em quando pela dor"),
            new(2, "Por causa da dor, durmo menos de seis horas"),
            new(3, "Por causa da dor, durmo menos de quatro horas"),
            new(4, "Por causa da dor, durmo menos de duas horas"),
            new(5, "A dor me impede de dormir")
        ]),
        new("ODI_8", "Vida social",
        [
            new(0, "Minha vida social é normal e não aumenta a dor"),
            new(1, "Minha vida social é normal, mas aumenta a dor"),
            new(2, "A dor limita apenas as atividades mais físicas (esporte, dança)"),
            new(3, "A dor limitou minha vida social e não saio tanto"),
            new(4, "A dor restringiu minha vida social ao ambiente de casa"),
            new(5, "Não tenho vida social por causa da dor")
        ]),
        new("ODI_9", "Viajar / deslocar-se",
        [
            new(0, "Viajo a qualquer lugar sem dor"),
            new(1, "Viajo a qualquer lugar, mas isso aumenta a dor"),
            new(2, "A dor é ruim, mas aguento viagens de mais de duas horas"),
            new(3, "A dor me restringe a viagens de menos de uma hora"),
            new(4, "A dor me restringe a deslocamentos curtos e necessários, de menos de 30 minutos"),
            new(5, "A dor me impede de sair, exceto para consultas")
        ])
    ];

    /// <summary>
    /// Faixas do ODI, em porcentagem. São as faixas publicadas do instrumento — a
    /// terminologia da última ("restrito ao leito") é a da escala, não um juízo do sistema.
    /// </summary>
    public IReadOnlyList<FaixaInstrumento> Faixas { get; } =
    [
        new(0, 20, "Incapacidade mínima", "Costuma dispensar tratamento além de orientação postural e atividade física.", GravidadeFaixa.Normal),
        new(21, 40, "Incapacidade moderada", "Dor e limitação em sentar, levantar peso e ficar em pé; tratamento conservador.", GravidadeFaixa.Atencao),
        new(41, 60, "Incapacidade grave", "A dor é o problema principal do dia a dia; investigação mais detalhada.", GravidadeFaixa.Alerta),
        new(61, 80, "Incapacidade muito grave", "A dor invade todos os aspectos da vida; intervenção necessária.", GravidadeFaixa.Alerta),
        new(81, 100, "Restrito ao leito", "Faixa extrema da escala — confirmar a compreensão das perguntas.", GravidadeFaixa.Alerta)
    ];

    /// <summary>
    /// Percentual sobre as seções RESPONDIDAS, que é a regra do próprio ODI — e a razão de
    /// este método existir em vez da soma padrão. Somar tratando o não respondido como
    /// zero faria "não quis responder" virar "não tenho nenhuma limitação nisso", que é o
    /// oposto do que costuma acontecer.
    ///
    /// Sem nenhuma seção respondida o resultado é 0: quem chama já garantiu que há
    /// resposta (o serviço recusa avaliação vazia), e dividir por zero aqui só produziria
    /// uma exceção longe da causa.
    /// </summary>
    public int Pontuar(IReadOnlyDictionary<string, int> respostas)
    {
        var respondidos = Itens.Where(i => respostas.ContainsKey(i.Codigo)).ToList();
        if (respondidos.Count == 0) return 0;

        var soma = respondidos.Sum(i => respostas[i.Codigo]);
        var maximo = respondidos.Sum(i => i.ValorMaximo);

        return (int)Math.Round(100.0 * soma / maximo, MidpointRounding.AwayFromZero);
    }

    /// <summary>O ODI sai em porcentagem, então o teto é 100 — não a soma dos pesos.</summary>
    public int PontuacaoMaxima => 100;

    /// <summary>
    /// Única escala do catálogo que aceita seção em branco, porque a regra do próprio ODI
    /// prevê isso — e é o que permite omitir a seção de vida sexual sem inventar cálculo.
    /// </summary>
    public bool AceitaItemOmitido => true;
}
