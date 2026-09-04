using Clinica.Clinico.ViewModels;
using Clinica.Desktop.Shell.Componentes;
using Clinica.Clinico.Views;
using Clinica.Desktop.Shell.Modulos;
using Clinica.Domain.Entities;
using Microsoft.Extensions.DependencyInjection;

namespace Clinica.Clinico.Modulo;

/// <summary>
/// Módulo do CONSULTÓRIO — as telas da máquina de quem atende (parcela 36).
///
/// Por que um módulo novo, e não mais telas na Recepção
/// ----------------------------------------------------
/// Quem instala o app na sala do médico não instala a agenda do balcão, o cadastro da
/// equipe, o caixa nem a central de documentos. A pergunta que a arquitetura multi-exe
/// existe para responder é "quem instala o quê" (ver docs/arquitetura-multi-exe.md), e a
/// resposta aqui é: o consultório instala o consultório. O Gerente Geral, que carrega
/// tudo, ganha estas telas de graça — como ganhou as dos outros três.
///
/// A ordem dos itens é a do atendimento: o dia (quem vem), o atendimento (o que estou
/// fazendo agora), a dor (como está indo), as escalas (o que a especialidade mede) e a
/// carteira (quem eu acompanho).
/// </summary>
public sealed class ModuloClinico : IModuloApp
{
    // Chave do CONTRATO: o painel da direção navega até aqui, e por isso ela não pode
    // ser um literal repetido do outro lado, onde renomear compila e só falha na clínica.
    public const string ChaveMeuDia = ChavesSuite.ConsultorioMeuDia;
    public const string ChaveAtendimento = "consultorio-atendimento";

    /// <summary>
    /// O ATENDIMENTO DE ENFERMAGEM (parcela 88) — a seção de escrita do lado Y.
    ///
    /// ⚠️ Ela mora em <see cref="ChavesSuite"/> porque atravessa módulo: quem manda a
    /// enfermeira para cá é o "Atender" da tela da Enfermagem, que é do SHELL e é
    /// publicada também pela Recepção. Literal escrito à mão dos dois lados sempre
    /// compila — e o botão simplesmente deixaria de abrir, em silêncio, no dia em que
    /// alguém renomeasse um dos dois (a regressão da parcela 37, 4ª rodada).
    /// </summary>
    public const string ChaveAtendimentoEnfermagem = ChavesSuite.AtendimentoEnfermagem;
    public const string ChaveProntuario = "consultorio-prontuario";
    public const string ChaveEvolucaoDor = "consultorio-evolucao-dor";
    public const string ChaveMedidas = "consultorio-medidas";
    public const string ChaveAvaliacoes = "consultorio-avaliacoes";
    public const string ChavePacientesDaClinica = ChavesSuite.ConsultorioPacientes;

    /// <summary>
    /// A dívida de prontuário. É tela, e não um bloco do "Meu dia", porque numa base real
    /// são dezenas de linhas: espremidas numa caixa de 180 px acima do quadro, elas
    /// cortavam um nome ao meio e roubavam do dia a altura que ele precisa.
    /// </summary>
    public const string ChaveRegistrosPendentes = "consultorio-registros-pendentes";

    /// <summary>
    /// A semana de quem atende. "Meu dia" responde o que acontece hoje e não responde as
    /// perguntas que se fazem COM o paciente na frente — "quando eu tenho espaço?",
    /// "quinta está cheia?". A recepção tem visão de semana desde a parcela 26.
    /// </summary>
    public const string ChaveMinhaSemana = ChavesSuite.ConsultorioSemana;

    /// <summary>
    /// Prescrições. O fluxo de emissão existia inteiro e a única porta estava no módulo da
    /// RECEPÇÃO — quem prescreve, atesta e pede exame é quem ATENDE.
    /// </summary>
    public const string ChavePrescricoes = ChavesSuite.ConsultorioPrescricoes;

    /// <summary>
    /// A folha de infusão (parcela 42) — multi-item, executada aqui dentro e checada pela
    /// enfermagem. Item SEPARADO de <see cref="ChavePrescricoes"/>, e não um tipo a mais no
    /// seletor daquela tela: lá saem os quatro papéis que o paciente LEVA, aqui fica a
    /// folha que a equipe EXECUTA. Juntá-las faria a decisão mais frequente do dia (dar um
    /// atestado) dividir espaço com a mais rara.
    /// </summary>
    public const string ChavePrescricaoInfusao = ChavesSuite.ConsultorioPrescricaoInfusao;

    /// <summary>
    /// A sala de infusão: as folhas assinadas do dia esperando execução. Sob permissão
    /// PRÓPRIA (<c>ChecarPrescricao</c>), porque quem checa não é quem prescreve — e é
    /// serem duas pessoas que dá valor à conferência. A chave mora em
    /// <see cref="ChavesSuite"/> porque a Recepção publica a MESMA (parcela 48): duas
    /// strings à mão divergiriam na primeira renomeação, em silêncio.
    /// </summary>
    public const string ChaveSalaInfusao = ChavesSuite.SalaInfusao;

    /// <summary>
    /// A tela da ENFERMAGEM (parcela 71). Terceira tela do SHELL publicada por DOIS
    /// módulos, pela razão da sala de infusão acima — e a chave mora em ChavesSuite
    /// porque literal à mão dos dois lados sempre compila e some em silêncio.
    /// </summary>
    public const string ChaveEnfermagem = ChavesSuite.Enfermagem;

    /// <summary>
    /// A produtividade do profissional, na tela dele. <c>ProdutividadeProfissional</c> e
    /// <c>CompletudeProntuario</c> só eram lidos pelo BI do Gerente: o sistema media quem
    /// atende e a pessoa medida não via o próprio número.
    /// </summary>
    public const string ChaveMeusNumeros = ChavesSuite.ConsultorioMeusNumeros;

    /// <summary>
    /// A lista plana de PRONTUÁRIOS (set/2026 — o handoff): evoluções, anamneses e as
    /// sessões sem registro, com busca por paciente. A situação segue o que cada linha É
    /// no domínio — anamnese tem assinatura de verdade; o pendente da evolução é a
    /// sessão sem registro ("A escrever"), nunca uma assinatura inventada.
    /// </summary>
    /// <remarks>
    /// ⚠️ Vem da <see cref="ChavesSuite"/> desde set/2026: a tela virou SUB-ABA do item
    /// "Prontuário" publicado pela Recepção, e chave que atravessa módulo escrita à mão
    /// dos dois lados sempre compila — a divergência só aparece na clínica, com a aba
    /// PULADA em silêncio (a checagem 28). A tela continua sendo um ITEM: quem a esconde
    /// do menu é o item pai, e só enquanto o pai está presente.
    /// </remarks>
    public const string ChaveProntuarios = ChavesSuite.ConsultorioProntuarios;

    /// <summary>
    /// A tela de EXAMES (set/2026 — o handoff): os pedidos com a situação derivada dos
    /// resultados REGISTRADOS amarrados a cada um. É a mesma família do 2º código — o
    /// que foi pedido e ainda não voltou some da cabeça sem uma lista que cobre.
    /// </summary>
    public const string ChaveExames = "consultorio-exames";

    /// <summary>
    /// A seção "Exames e anexos" do paciente, navegável por chave (oculta do menu — só
    /// existe COM um paciente escolhido). É o destino do "Ver resultados" da tela de
    /// Exames; sem item declarado, <c>NavegacaoSuite.Ir</c> devolveria false EM SILÊNCIO
    /// (a regressão da parcela 37, vigiada pela checagem 19).
    /// </summary>
    public const string ChaveExamesDoPaciente = "consultorio-exames-anexos";

    /// <summary>
    /// A tela do PACIENTE — identidade no topo, as seções num rail à esquerda.
    ///
    /// As chaves clínicas acima continuam válidas e caem todas aqui, cada uma na
    /// sua aba (ver <see cref="AbaDe"/>): a fila do dia e o painel da direção navegam por
    /// elas, e renomear contrato de navegação para arrumar leiaute quebraria o que
    /// funciona noutro módulo.
    /// </summary>
    public const string ChavePaciente = "consultorio-paciente";

    /// <summary>
    /// Ajuda e suporte — tela do SHELL; os QUATRO módulos publicam a MESMA chave
    /// (<see cref="ChavesSuite.Ajuda"/>), e a dedupe do shell funde no Gerente.
    /// </summary>
    public const string ChaveAjuda = ChavesSuite.Ajuda;

    // ===== Itens COMPOSTOS (parcela 95) =====
    //
    // A sidebar deste módulo tinha DOZE itens soltos — e nunca usou as sub-abas que o
    // shell tem desde a parcela 55 (a Recepção e o Financeiro usam). "Prescrições" e
    // "Prescrição de infusão" eram vizinhos quase homônimos, "Sala de infusão",
    // "Enfermagem" e a folha de infusão estavam espalhados por dois grupos, e
    // INTELIGÊNCIA era um grupo de um item só. A direção pediu para organizar: viraram
    // seis itens, cada um respondendo a UMA pergunta, com as telas de sempre como abas.
    //
    // ⚠️ Nenhuma tela some e nenhuma CHAVE de navegação muda: a sub-tela continua sendo um
    // item (reivindicada pelo pai, que é quem a esconde do menu), e `NavegacaoSuite.Ir`
    // com a chave dela abre o pai na aba certa. "Atender", a dívida de prontuário e o
    // painel da direção continuam abrindo o que abriam.

    /// <summary>
    /// "Minha agenda" — hoje, a semana e a dívida de prontuário. É a ABERTURA do
    /// <c>Clinica.Clinico.exe</c>: sem `Inicial` declarado o shell abre no primeiro item
    /// visível, e este é declarado primeiro de propósito (a razão de não marcar `Inicial`
    /// está no comentário do "Meu dia", abaixo). O rótulo é "Minha", e não "Agenda":
    /// no Gerente Geral a Recepção publica "Agenda", e dois itens com o mesmo rótulo em
    /// GESTÃO é a duplicata que a checagem 45 existe para pegar.
    /// </summary>
    public const string ChaveGrupoAgenda = "consultorio-agenda";

    /// <summary>
    /// "Enfermagem" — a sala de infusão (o que executar agora) e as passagens (quem eu
    /// atendi e o que escrevi). São duas perguntas e continuam duas telas; o que mudou é
    /// que moram no mesmo item, porque quem faz as duas é a mesma pessoa. As duas
    /// sub-telas são do SHELL e a Recepção as publica soltas: no Gerente, este composto
    /// as reivindica e elas saem do menu — no <c>Clinica.Recepcao.exe</c>, sem este
    /// módulo, continuam soltas, como sempre.
    /// </summary>
    public const string ChaveGrupoEnfermagem = "consultorio-enfermagem";

    /// <summary>
    /// "Pacientes" — em tratamento, registros e pendências, exames. A MESMA chave que a
    /// Recepção usa no composto dela (<see cref="ChavesSuite.GrupoPacientes"/>): no Gerente
    /// a dedupe funde os dois e vence o da Recepção, cujas abas incluem "Em tratamento".
    /// </summary>
    public const string ChaveGrupoPacientes = ChavesSuite.GrupoPacientes;

    /// <summary>
    /// "Prescrições" — receitas e documentos, e a folha de infusão. A mesma chave do
    /// composto da Recepção, pela razão do de cima; lá as abas são "Receituário · No
    /// consultório · Infusão", que contêm as duas daqui.
    /// </summary>
    public const string ChaveGrupoPrescricoes = ChavesSuite.GrupoPrescricoes;

    public string Nome => "Consultório";

    /// <summary>
    /// DUAS portas de entrada, e só.
    ///
    /// As cinco seções clínicas saíram do menu na 4ª rodada da parcela 37, e a razão é a
    /// mesma que fez o cliente reprovar o leiaute: elas só existem COM paciente. Como
    /// itens de menu, cada uma abria em branco carregando a mesma lista de pacientes numa
    /// coluna de 300 px — mestre-detalhe espremido numa tela só, repetido seis vezes, e
    /// metade da largura útil gasta com a mesma lista em todas elas.
    ///
    /// Agora são duas telas de LISTA, com a largura inteira ("quem eu vejo hoje" e "quem
    /// eu acompanho"), e a tela do paciente atrás de um clique. Item de menu que só
    /// funciona depois de você ter passado por outro lugar é item que ensina a errar.
    /// </summary>
    public IReadOnlyList<ItemMenuModulo> Itens { get; } =
    [
        // A abertura do Clinica.Clinico.exe é este item — e ele NÃO declara `Inicial`.
        //
        // Parece descuido, e não é. `Inicial` marca a abertura do APP, e o shell escolhe o
        // PRIMEIRO item marcado entre TODOS os módulos carregados (`ShellViewModel`). Este
        // executável carrega um módulo só, então o primeiro item já é a abertura sem marca
        // nenhuma. Marcá-lo, ao contrário, quebraria o Gerente Geral: lá o Consultório é
        // carregado antes do módulo da direção, e a marca daqui venceria a do painel — que
        // é exatamente o defeito que a parcela 22 corrigiu (quem manda na clínica entrava
        // no sistema e caía na fila do balcão).
        new ItemMenuModulo
        {
            Chave = ChaveGrupoAgenda, Rotulo = "Minha agenda", Glifo = "\uE787",
            Grupo = GrupoSidebar.Gestao, Requer = Permissao.VerAgenda,
            Abas =
            [
                new AbaMenu("Hoje", ChaveMeuDia),
                new AbaMenu("Semana", ChaveMinhaSemana),
                new AbaMenu("Sem evolu\u00E7\u00E3o", ChaveRegistrosPendentes)
            ]
        },
        new ItemMenuModulo
        {
            Chave = ChaveMeuDia, Rotulo = "Meu dia", Glifo = "\uE787",
            Grupo = GrupoSidebar.Gestao, Requer = Permissao.VerAgenda
        },
        new ItemMenuModulo
        {
            Chave = ChaveRegistrosPendentes, Rotulo = "Sess\u00F5es sem evolu\u00E7\u00E3o", Glifo = "\uE73E",
            Grupo = GrupoSidebar.Gestao, Requer = Permissao.VerProntuario
        },
        new ItemMenuModulo
        {
            Chave = ChaveMinhaSemana, Rotulo = "Minha semana", Glifo = "\uE8BD",
            Grupo = GrupoSidebar.Gestao, Requer = Permissao.VerAgenda
        },
        new ItemMenuModulo
        {
            Chave = ChaveGrupoPacientes, Rotulo = "Pacientes", Glifo = "\uE77B",
            Grupo = GrupoSidebar.Paciente, Requer = Permissao.VerProntuario,
            Abas =
            [
                new AbaMenu("Em tratamento", ChavePacientesDaClinica),
                new AbaMenu("Registros e pend\u00EAncias", ChaveProntuarios),
                new AbaMenu("Exames", ChaveExames)
            ]
        },
        new ItemMenuModulo
        {
            // "Meus pacientes" morreu com a premissa (parcela 88, 3ª rodada): não existe
            // "meu paciente", todos atendem todos. A CHAVE não muda — ela é contrato de
            // navegação de outros módulos, e renomeá-la para arrumar rótulo quebraria o
            // que funciona lá (a regressão da parcela 37, 4ª rodada).
            Chave = ChavePacientesDaClinica, Rotulo = "Pacientes", Glifo = "\uE77B",
            Grupo = GrupoSidebar.Paciente, Requer = Permissao.VerProntuario
        },
        // As duas telas planas do handoff (set/2026). Sob VerProntuario: as listas
        // carregam nome de paciente com dado de sa\u00FAde ao lado (o que foi pedido, o que
        // foi escrito) \u2014 \u00E9 o corte da parcela 49.
        new ItemMenuModulo
        {
            Chave = ChaveProntuarios, Rotulo = "Prontu\u00E1rios", Glifo = "\uE7C3",
            Grupo = GrupoSidebar.Paciente, Requer = Permissao.VerProntuario
        },
        new ItemMenuModulo
        {
            Chave = ChaveExames, Rotulo = "Exames", Glifo = "\uE9D2",
            Grupo = GrupoSidebar.Paciente, Requer = Permissao.VerProntuario
        },
        new ItemMenuModulo
        {
            Chave = ChaveGrupoPrescricoes, Rotulo = "Prescri\u00E7\u00F5es", Glifo = "\uE8A5",
            Grupo = GrupoSidebar.Paciente, Requer = Permissao.VerProntuario,
            Abas =
            [
                new AbaMenu("Receitas e documentos", ChavePrescricoes),
                new AbaMenu("Infus\u00E3o", ChavePrescricaoInfusao)
            ]
        },
        new ItemMenuModulo
        {
            Chave = ChavePrescricoes, Rotulo = "Prescri\u00E7\u00F5es", Glifo = "\uE8A5",
            Grupo = GrupoSidebar.Paciente, Requer = Permissao.VerProntuario
        },
        new ItemMenuModulo
        {
            Chave = ChavePrescricaoInfusao, Rotulo = "Prescri\u00E7\u00E3o de infus\u00E3o",
            Glifo = "\uE95E", Grupo = GrupoSidebar.Paciente, Requer = Permissao.Prescrever
        },
        // Na GESTAO, e nao em PACIENTE: a sala responde "o que falta fazer hoje", que e
        // pergunta do dia de trabalho -- e ela e a unica tela do Consultorio que abre sem
        // paciente escolhido, porque a fila e de todos eles.
        // ⚠️ `Requer = VerAgenda`, e não os bits da enfermagem: `Pode` com bits somados é
        // um E, e a técnica que só checa (sem registrar passagem) ficaria sem o item. O
        // filtro de verdade são as ABAS — cada sub-tela entra em `Itens` só para quem tem
        // o bit dela, e o composto sem aba nenhuma some sozinho (`ShellViewModel`). Para
        // o médico, que não tem nenhum dos dois, o item simplesmente não existe.
        new ItemMenuModulo
        {
            Chave = ChaveGrupoEnfermagem, Rotulo = "Enfermagem", Glifo = "\uE95E",
            Grupo = GrupoSidebar.Gestao, Requer = Permissao.VerAgenda,
            Abas =
            [
                new AbaMenu("Sala de infus\u00E3o", ChaveSalaInfusao),
                new AbaMenu("Passagens", ChaveEnfermagem)
            ]
        },
        new ItemMenuModulo
        {
            Chave = ChaveSalaInfusao, Rotulo = "Sala de infus\u00E3o", Glifo = "\uE9D5",
            Grupo = GrupoSidebar.Gestao, Requer = Permissao.ChecarPrescricao
        },

        // A tela da ENFERMAGEM: TODOS os pacientes cadastrados e a evolução de cada
        // um. SEPARADA da sala de infusão de propósito — a sala responde "o que
        // executar agora" e só mostra as folhas do dia; esta responde "quem eu atendi
        // e o que escrevi", e a clínica disse que todo paciente passa pela enfermagem.
        // Terceira pergunta, terceira tela.
        new ItemMenuModulo
        {
            Chave = ChaveEnfermagem, Rotulo = "Enfermagem", Glifo = "\uE95E",
            Grupo = GrupoSidebar.Paciente, Requer = Permissao.RegistrarEvolucaoEnfermagem
        },
        new ItemMenuModulo
        {
            Chave = ChaveMeusNumeros, Rotulo = "Meus n\u00FAmeros", Glifo = "\uE9D9",
            Grupo = GrupoSidebar.Inteligencia, Requer = Permissao.VerAgenda
        },

        // ===== Navegáveis, mas fora do menu =====
        //
        // A tela do paciente e as chaves clínicas continuam sendo DESTINO: é por
        // elas que "Atender" na fila do dia, os atalhos da carteira e o painel da direção
        // abrem alguém. Tirá-las da lista — e não só do menu — foi o que quebrou todos
        // esses botões de uma vez: o shell navega procurando a chave em `Itens`, e o que
        // não está lá simplesmente não abre.
        new ItemMenuModulo
        {
            Chave = ChavePaciente, Rotulo = "Paciente", Glifo = "\uE77B",
            Grupo = GrupoSidebar.Paciente, Requer = Permissao.VerProntuario, Oculto = true
        },
        new ItemMenuModulo
        {
            Chave = ChaveAtendimento, Rotulo = "Atendimento", Glifo = "\uE70F",
            Grupo = GrupoSidebar.Paciente, Requer = Permissao.VerProntuario, Oculto = true
        },
        // O destino do "Atender" da enfermagem. Ele PRECISA estar nesta lista, e não só
        // no rail: o shell navega procurando a chave em `Itens`, e o que não está aqui
        // simplesmente não abre — sem erro, sem log, sem exceção (parcela 37, 4ª rodada).
        //
        // ⚠️ `Requer` é `VerProntuario` e não `RegistrarEvolucaoEnfermagem`: a seção é
        // XY — o médico LÊ a passagem que a técnica escreveu, e a técnica lê a conduta
        // dele. Exigir o bit de ESCREVER aqui faria a seção sumir para quem tem todo o
        // direito de lê-la, que é a metade que a parcela 72 existe para garantir.
        new ItemMenuModulo
        {
            Chave = ChaveAtendimentoEnfermagem, Rotulo = "Atendimento de enfermagem",
            Glifo = "\uE95E", Grupo = GrupoSidebar.Paciente,
            Requer = Permissao.VerProntuario, Oculto = true
        },
        new ItemMenuModulo
        {
            Chave = ChaveProntuario, Rotulo = "Prontuário", Glifo = "\uE7C3",
            Grupo = GrupoSidebar.Paciente, Requer = Permissao.VerProntuario, Oculto = true
        },
        new ItemMenuModulo
        {
            Chave = ChaveEvolucaoDor, Rotulo = "Evolução da dor", Glifo = "\uEB05",
            Grupo = GrupoSidebar.Paciente, Requer = Permissao.VerProntuario, Oculto = true
        },
        new ItemMenuModulo
        {
            Chave = ChaveMedidas, Rotulo = "Medidas", Glifo = "\uE9D2",
            Grupo = GrupoSidebar.Paciente, Requer = Permissao.VerProntuario, Oculto = true
        },
        new ItemMenuModulo
        {
            Chave = ChaveAvaliacoes, Rotulo = "Avaliações", Glifo = "\uE9D9",
            Grupo = GrupoSidebar.Paciente, Requer = Permissao.VerProntuario, Oculto = true
        },
        // O destino do "Ver resultados" da tela de Exames: a seção "Exames e anexos" do
        // paciente. Oculto porque só existe COM alguém escolhido — como seção ela diz
        // sozinha a quem pertence; como item abriria em branco (a regra de leiaute).
        new ItemMenuModulo
        {
            Chave = ChaveExamesDoPaciente, Rotulo = "Exames e anexos", Glifo = "\uE9D2",
            Grupo = GrupoSidebar.Paciente, Requer = Permissao.VerProntuario, Oculto = true
        },

        // AJUDA E SUPORTE — tela do shell, sem `Requer` de propósito (o padrão é
        // "sempre visível"): fechar o manual por permissão trancaria quem mais precisa.
        new ItemMenuModulo
        {
            Chave = ChaveAjuda, Rotulo = "Ajuda e suporte", Glifo = "\uE897",
            Grupo = GrupoSidebar.Gestao
        }
    ];

    /// <summary>
    /// As SEÇÕES da tela do paciente, na ordem em que o rail as desenha.
    ///
    /// ⚠️ Ela existe para o mapa de navegação parar de ser por ÍNDICE. Enquanto
    /// <c>AbaDe</c> devolvia um número escrito à mão, pôr uma seção no MEIO da lista
    /// empurrava todos os de baixo — e índice desatualizado não quebra build nenhum: ele
    /// abre a seção ERRADA. Foi o que a parcela 75 fez ao inserir a Anamnese em 1.
    ///
    /// Agora a chave aponta para o NOME e o índice sai daqui. Reordenar continua exigindo
    /// mexer nesta lista, mas mexer nela é ÓBVIO — e a checagem 38 casa esta lista com os
    /// rótulos do rail, posição por posição, de modo que XAML e C# não podem divergir em
    /// silêncio.
    /// </summary>
    public static readonly IReadOnlyList<string> SecoesDoPaciente =
    [
        "Atendimento",
        "Atendimento de enfermagem",
        "Anamnese",
        "Paciente",
        "Histórico",
        "Exames e anexos",
        "Prescrições e documentos",
        "Acompanhamento"
    ];

    /// <summary>
    /// O GRUPO de cada seção no rail, na mesma ordem de <see cref="SecoesDoPaciente"/>.
    ///
    /// Nove itens numa lista corrida davam a "Anamnese" — escrita uma vez na vida — o mesmo
    /// peso de "Atendimento", aberto vinte vezes por dia. Em grupos, a barra passa a dizer
    /// QUANDO se usa cada uma: o que se faz agora e o que o paciente já tem.
    ///
    /// ⚠️ "Evolução da dor", "Medidas" e "Avaliações" viraram UMA seção, "Acompanhamento",
    /// com as três como abas internas (parcela 95 — a direção: "quanto mais simples,
    /// melhor"). Eram três linhas do rail para uma pergunta só, "como está indo" — e o
    /// grupo de três itens virou um item do grupo Paciente. As três chaves de navegação
    /// continuam valendo: <see cref="AbaDe"/> leva à seção e <see cref="SubAbaDe"/> à aba
    /// de dentro.
    ///
    /// ⚠️ A lista é a MESMA de cima, casada por posição, e é daqui que o rail se monta — não
    /// há uma segunda lista de rótulos no XAML. Foi assim que a divergência entre o índice
    /// de navegação e o rótulo lido pelo usuário (o caso (b) da checagem 38) deixou de ser
    /// possível por construção, em vez de ser vigiada.
    /// </summary>
    public static readonly IReadOnlyList<string> GruposDoPaciente =
    [
        "Sessão",
        "Sessão",
        "Sessão",
        "Paciente",
        "Paciente",
        "Paciente",
        "Paciente",
        "Paciente"
    ];

    /// <summary>Uma linha do rail: o rótulo que se lê e o grupo em que ela mora.</summary>
    public sealed record SecaoDoPaciente(string Rotulo, string Grupo);

    /// <summary>
    /// O rail da tela do paciente, na ordem — montado das DUAS listas acima.
    ///
    /// ⚠️ É daqui que o XAML se monta, e é isso que faz a divergência entre o rótulo lido
    /// pelo usuário e o índice de navegação ser IMPOSSÍVEL por construção, em vez de
    /// vigiada por checagem (o caso (b) da checagem 38). Antes eram duas listas — a do C#
    /// e nove <c>ListBoxItem</c> escritos à mão no XAML —, e inserir uma seção no meio
    /// empurrava os índices sem quebrar build nenhum.
    /// </summary>
    public static IReadOnlyList<SecaoDoPaciente> RailDoPaciente()
    {
        if (SecoesDoPaciente.Count != GruposDoPaciente.Count)
            throw new InvalidOperationException(
                "ModuloClinico: SecoesDoPaciente e GruposDoPaciente têm tamanhos "
                + "diferentes. As duas são casadas por POSIÇÃO — uma seção sem grupo "
                + "sairia do rail sem quebrar nada.");

        // ⚠️ OS GRUPOS TÊM DE SER CONTÍGUOS, e isto não é estética.
        //
        // O rail é uma vista AGRUPADA da mesma lista, e o `SelectedIndex` dele é o índice
        // NA VISTA. Com os grupos contíguos, a vista enumera na ordem da lista e o índice
        // bate com o do TabControl ao lado. Espalhados — [Sessão, Paciente, Sessão] —, a
        // vista reordenaria para juntar os iguais, e o clique passaria a abrir a tela do
        // vizinho: sem erro, sem log, sem nada que quebre.
        var vistos = new List<string>();
        foreach (var grupo in GruposDoPaciente)
        {
            if (vistos.Count > 0 && vistos[^1] == grupo) continue;
            if (vistos.Contains(grupo))
                throw new InvalidOperationException(
                    $"ModuloClinico: o grupo \u201C{grupo}\u201D aparece em blocos separados de "
                    + "GruposDoPaciente. O rail é uma vista agrupada, e grupo partido faz a "
                    + "vista reordenar as seções — o clique passa a abrir a tela do vizinho.");
            vistos.Add(grupo);
        }

        return [.. SecoesDoPaciente.Select((nome, i) => new SecaoDoPaciente(nome, GruposDoPaciente[i]))];
    }

    /// <summary>
    /// Em que seção da tela do paciente cada chave clínica cai.
    ///
    /// O mapa é por NOME (ver <see cref="SecoesDoPaciente"/>); o índice é derivado. Chave
    /// que não estiver aqui cai na primeira seção, que é o Atendimento.
    ///
    /// <c>ChavePrescricoes</c> continua de FORA de propósito: ela é um item de menu de
    /// verdade, e quem entra por ali não tem paciente em foco — mandá-la para a seção
    /// abriria a tela do paciente em branco pedindo que se escolhesse alguém.
    /// </summary>
    // Público desde a parcela 95: a tela do paciente usa o MESMO mapa para saber qual
    // linha do rail esconder — um rótulo escrito à mão lá seria a segunda definição.
    public static int AbaDe(string chave)
    {
        var nome = chave switch
        {
            ChaveAtendimentoEnfermagem => "Atendimento de enfermagem",
            ChaveProntuario => "Histórico",
            ChaveExamesDoPaciente => "Exames e anexos",
            ChaveEvolucaoDor or ChaveMedidas or ChaveAvaliacoes => "Acompanhamento",
            _ => "Atendimento"
        };

        var i = SecoesDoPaciente.ToList().IndexOf(nome);
        return i < 0 ? 0 : i;
    }

    /// <summary>
    /// A ABA DE DENTRO da seção "Acompanhamento" (parcela 95): dor, medidas ou
    /// avaliações. As três chaves são contrato de navegação de outros módulos (o painel
    /// da direção, a carteira), e continuam caindo cada uma na sua tela — só que a tela
    /// agora mora dentro de uma seção só. Chave que não é de acompanhamento cai na
    /// primeira aba, que é a dor: é a que a acupuntura, a especialidade da casa, mais lê.
    /// </summary>
    public static int SubAbaDe(string chave) => chave switch
    {
        ChaveMedidas => 1,
        ChaveAvaliacoes => 2,
        _ => 0
    };

    public void Registrar(IServiceCollection servicos)
    {
        // O paciente do posto é SINGLETON: é o contexto do consultório, e as telas
        // clínicas o leem na abertura. Ver PacienteEmFoco.
        servicos.AddSingleton<PacienteEmFoco>();

        servicos.AddTransient<MeuDiaViewModel>();
        servicos.AddTransient<RegistrosPendentesViewModel>();
        servicos.AddTransient<MinhaSemanaViewModel>();
        servicos.AddTransient<PrescricoesClinicasViewModel>();
        servicos.AddTransient<PrescricaoInfusaoViewModel>();
        servicos.AddTransient<SalaInfusaoViewModel>();
        servicos.AddTransient<EnfermagemViewModel>();
        servicos.AddTransient<MeusNumerosViewModel>();
        servicos.AddTransient<AtendimentoViewModel>();
        servicos.AddTransient<AtendimentoEnfermagemViewModel>();
        servicos.AddTransient<PacienteCapaViewModel>();
        servicos.AddTransient<ProntuarioClinicoViewModel>();
        servicos.AddTransient<EvolucaoDorViewModel>();
        servicos.AddTransient<MedidasViewModel>();
        servicos.AddTransient<AvaliacoesViewModel>();
        servicos.AddTransient<AnamneseViewModel>();
        servicos.AddTransient<AnexosPacienteViewModel>();
        servicos.AddTransient<PacientesDaClinicaViewModel>();
        servicos.AddTransient<ProntuariosViewModel>();
        servicos.AddTransient<ExamesViewModel>();
        // AplicarAvaliacaoViewModel, AnexosSessaoViewModel, ProblemaEdicaoViewModel,
        // PrescricaoInternaEdicaoViewModel, FolhaExecucaoViewModel e
        // EscolherCertificadoViewModel são construídos à mão pela tela: eles recebem o
        // paciente, a sessão, o problema ou a prescrição escolhidos, como todo formulário
        // da suíte.
    }

    public object? CriarTela(string chave, IServiceProvider servicos) => chave switch
    {
        ChaveMeuDia => new MeuDiaView { DataContext = servicos.GetRequiredService<MeuDiaViewModel>() },
        ChavePacientesDaClinica => new PacientesDaClinicaView { DataContext = servicos.GetRequiredService<PacientesDaClinicaViewModel>() },
        ChaveProntuarios => new ProntuariosView { DataContext = servicos.GetRequiredService<ProntuariosViewModel>() },
        ChaveExames => new ExamesView { DataContext = servicos.GetRequiredService<ExamesViewModel>() },
        ChaveRegistrosPendentes => new RegistrosPendentesView
        {
            DataContext = servicos.GetRequiredService<RegistrosPendentesViewModel>()
        },
        ChaveMinhaSemana => new MinhaSemanaView
        {
            DataContext = servicos.GetRequiredService<MinhaSemanaViewModel>()
        },
        ChavePrescricoes => new PrescricoesClinicasView
        {
            DataContext = servicos.GetRequiredService<PrescricoesClinicasViewModel>()
        },
        ChavePrescricaoInfusao => new PrescricaoInfusaoView
        {
            DataContext = servicos.GetRequiredService<PrescricaoInfusaoViewModel>()
        },
        ChaveSalaInfusao => new SalaInfusaoView
        {
            DataContext = servicos.GetRequiredService<SalaInfusaoViewModel>()
        },
        ChaveEnfermagem => new EnfermagemView
        {
            DataContext = servicos.GetRequiredService<EnfermagemViewModel>()
        },
        ChaveMeusNumeros => new MeusNumerosView
        {
            DataContext = servicos.GetRequiredService<MeusNumerosViewModel>()
        },

        // A tela do paciente, e as chaves clínicas que caem nela — cada uma na sua
        // seção (ver AbaDe).
        ChavePaciente or ChaveAtendimento or ChaveAtendimentoEnfermagem or ChaveProntuario
            or ChaveEvolucaoDor or ChaveMedidas or ChaveAvaliacoes or ChaveExamesDoPaciente
            => new PacienteWorkspaceView
            {
                DataContext = new PacienteWorkspaceViewModel(
                    servicos, servicos.GetRequiredService<PacienteEmFoco>(),
                    AbaDe(chave), SubAbaDe(chave))
            },

        // Tela do shell, ESTÁTICA: conteúdo literal, sem ViewModel — não há o que resolver.
        ChaveAjuda => new AjudaView(),

        _ => null
    };
}
