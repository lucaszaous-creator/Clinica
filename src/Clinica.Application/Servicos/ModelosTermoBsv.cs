using Clinica.Domain;
using Clinica.Domain.Entities;

namespace Clinica.Application.Servicos;

/// <summary>
/// Os dois termos do BSV, prontos para a clínica REVISAR e usar (parcela 67).
///
/// ⚠️ Isto NÃO é termo de fábrica, e a diferença é a que decide tudo o mais neste arquivo.
/// Termo de fábrica é o que o sistema aplica sozinho, sem ninguém ler; o que existe aqui é
/// um RASCUNHO que alguém pede, lê e edita antes de valer — e que não é exigido de ninguém
/// enquanto a clínica não amarrar a modalidade a ele.
///
/// Por que o rascunho existe
/// -------------------------
/// A regra do projeto continua sendo "o texto é da clínica, não nosso": o conteúdo de um
/// consentimento é responsabilidade técnica de quem faz o procedimento. Só que a lista
/// nascer vazia, na prática, produziu o pior desfecho possível — a clínica que não tem um
/// texto pronto não escreve um do zero no meio do expediente, e o BSV continua acontecendo
/// **sem termo nenhum**. Um rascunho que alguém revisa é melhor do que uma folha em branco
/// que ninguém preenche.
///
/// O que o desenho preserva:
///
/// 1. **Ninguém aplica sozinho.** Nada é semeado em migration nem criado na abertura do app.
///    Alguém abre Configurações → "Escrever termos…" e CLICA. É o padrão do
///    `FechamentoSessaoService`: o sistema faz o trabalho, o clique continua sendo de quem
///    responde por ele.
/// 2. **Nasce dizendo que é rascunho.** O nome traz "(rascunho — revisar)" e a primeira
///    linha do corpo manda o responsável técnico conferir. Quem apagar essa linha está
///    afirmando que leu — e é exatamente essa a decisão que se quer explícita.
/// 3. **Não exige nada de ninguém.** Criar o modelo não amarra modalidade nenhuma; a
///    exigência continua sendo um segundo ato, na mesma tela. Enquanto ela não existe, o
///    balcão não cobra o termo e nada muda no dia de trabalho.
/// 4. **Aplicar COPIA.** Editar o rascunho depois não reescreve termo já assinado — é a
///    regra do protocolo do mapa corporal, e aqui ela é a Lei 13.787/2018.
///
/// Por que DOIS termos, e não um
/// -----------------------------
/// É a distinção que desenhou a parcela 66 inteira. O **consentimento** ganha em ser lido
/// com calma, dias antes, e vale a partir da assinatura. A **declaração de jejum** afirma
/// "ESTOU em jejum": assinada na véspera, ela é uma declaração sobre o futuro, e por isso
/// nasce marcada para valer **só no dia**. Um termo só obrigaria a escolher entre pedir o
/// jejum uma vez por ano ou o consentimento toda semana; o primeiro erro é perigoso.
/// </summary>
public static class ModelosTermoBsv
{
    /// <summary>
    /// A marca que diz, no próprio nome, que o texto ainda não foi conferido. Fica no NOME
    /// e não num campo à parte de propósito: ele aparece na lista da tela, no seletor da
    /// coleta avulsa e na exigência — quem for amarrar a modalidade lê o aviso no caminho.
    /// </summary>
    public const string MarcaRascunho = "(rascunho — revisar)";

    private const string AvisoDeRevisao =
        "⚠️ RASCUNHO — o responsável técnico deve revisar este texto antes do primeiro uso, "
        + "e apagar esta linha ao aprová-lo.";

    /// <summary>O consentimento do procedimento — o texto longo, lido com calma.</summary>
    public static ModeloDocumento Consentimento() => new()
    {
        Tipo = TipoDocumentoClinico.TermoProcedimento,
        Nome = $"Termo de consentimento — BSV {MarcaRascunho}",
        Titulo = "Termo de consentimento livre e esclarecido — Bloqueio Simpático Venoso (BSV)",
        Corpo = string.Join("\n\n",
            AvisoDeRevisao,

            "Declaro que fui informado(a), em linguagem que compreendi, sobre o procedimento "
            + "de Bloqueio Simpático Venoso (BSV) que me foi indicado, e que tive a "
            + "oportunidade de fazer perguntas e recebi respostas para todas elas.",

            "O QUE É. O BSV é a aplicação de medicação por via venosa no membro afetado, com "
            + "o objetivo de reduzir a dor e melhorar a circulação da região. O membro é "
            + "mantido com um garrote durante alguns minutos, enquanto a medicação age no "
            + "local, e o procedimento é acompanhado pela equipe do início ao fim.",

            "PARA QUE SERVE. O objetivo é o alívio da dor e a melhora dos sintomas. Fui "
            + "informado(a) de que o resultado varia de pessoa para pessoa, de que costuma "
            + "ser necessária mais de uma sessão, e de que NENHUM resultado me foi prometido "
            + "ou garantido.",

            "RISCOS E EFEITOS POSSÍVEIS. Fui informado(a) de que podem ocorrer, entre outros: "
            + "dor, ardência ou formigamento durante a aplicação; hematoma, inchaço ou dor no "
            + "local da punção; tontura, náusea, queda da pressão ou desmaio; e, mais "
            + "raramente, reação alérgica à medicação, flebite ou infecção no local. Fui "
            + "orientado(a) a avisar a equipe IMEDIATAMENTE diante de qualquer mal-estar "
            + "durante ou após o procedimento.",

            "O QUE EU DEVO INFORMAR. Comprometo-me a informar à equipe todas as doenças que "
            + "tenho, as medicações que uso (incluindo anticoagulantes), alergias já "
            + "conhecidas, cirurgias anteriores e a possibilidade de gravidez. Estou "
            + "ciente de que omitir qualquer uma dessas informações pode me colocar em risco.",

            "ALTERNATIVAS. Fui informado(a) de que existem outras formas de tratamento para o "
            + "meu caso, de que elas me foram apresentadas, e de que posso optar por qualquer "
            + "uma delas ou por nenhuma.",

            "DIREITO DE DESISTIR. Estou ciente de que posso retirar este consentimento a "
            + "qualquer momento, antes ou durante o procedimento, sem que isso prejudique o "
            + "meu atendimento nesta clínica.",

            "Declaro que li e compreendi este termo, que minhas dúvidas foram esclarecidas e "
            + "que CONSINTO livremente com a realização do procedimento."),
        Itens =
        [
            new ItemModelo
            {
                Ordem = 1,
                Descricao = "Li este termo e minhas dúvidas foram esclarecidas",
                Detalhe = "Houve tempo e oportunidade de perguntar antes de assinar."
            },
            new ItemModelo
            {
                Ordem = 2,
                Descricao = "Informei todas as minhas doenças, alergias e medicações em uso",
                Detalhe = "Inclui anticoagulantes, suplementos e medicações sem receita."
            },
            new ItemModelo
            {
                Ordem = 3,
                Descricao = "Fui informado(a) de que o resultado não é garantido e de que "
                            + "pode ser necessária mais de uma sessão"
            },
            new ItemModelo
            {
                Ordem = 4,
                Descricao = "Consinto com a realização do Bloqueio Simpático Venoso"
            }
        ]
    };

    /// <summary>
    /// A declaração do dia — jejum e as condições que mudam a cada sessão.
    ///
    /// É curta de propósito: ela é lida no balcão, com o paciente de pé, minutos antes do
    /// procedimento. Um texto longo aqui seria assinado sem ser lido, e é justamente esta a
    /// folha em que a leitura importa.
    /// </summary>
    public static ModeloDocumento DeclaracaoDoDia() => new()
    {
        Tipo = TipoDocumentoClinico.TermoProcedimento,
        Nome = $"Declaração do dia — BSV (jejum) {MarcaRascunho}",
        Titulo = "Declaração do paciente no dia do procedimento — BSV",
        Corpo = string.Join("\n\n",
            AvisoDeRevisao,

            "Declaro, nesta data e imediatamente antes da realização do Bloqueio Simpático "
            + "Venoso, que as informações abaixo são verdadeiras.",

            "Estou ciente de que estas respostas orientam a equipe sobre a segurança de "
            + "realizar o procedimento HOJE, e de que responder \"não\" a qualquer uma delas "
            + "não me impede de ser atendido(a): a decisão de realizar ou adiar é de quem "
            + "executa o procedimento, e será conversada comigo."),
        Itens =
        [
            new ItemModelo
            {
                Ordem = 1,
                Descricao = "Estou em jejum conforme a orientação recebida",
                Detalhe = "Confira com o paciente quantas horas, e desde que horário."
            },
            new ItemModelo
            {
                Ordem = 2,
                Descricao = "Tomei hoje as medicações de uso contínuo, como orientado"
            },
            new ItemModelo
            {
                Ordem = 3,
                Descricao = "Não tive febre, infecção ou mal-estar nos últimos dias"
            },
            new ItemModelo
            {
                Ordem = 4,
                Descricao = "Não houve mudança nas minhas medicações, alergias ou estado de "
                            + "saúde desde o termo de consentimento",
                Detalhe = "Qualquer mudança precisa ser avisada à equipe antes de começar."
            },
            new ItemModelo
            {
                Ordem = 5,
                Descricao = "Estou acompanhado(a) para voltar para casa",
                Detalhe = "Se a orientação da clínica exigir acompanhante."
            }
        ]
    };

    /// <summary>
    /// As duas modalidades de BSV do motor de regras. Ambas exigem os dois termos: quem faz
    /// BSV com acupuntura no mesmo dia fez BSV, e o consentimento é do BSV.
    /// </summary>
    public static IReadOnlyList<ModalidadeAtendimento> ModalidadesDoBsv { get; } =
    [
        ModalidadeAtendimento.BsvApenas,
        ModalidadeAtendimento.BsvComAcupuntura
    ];
}
