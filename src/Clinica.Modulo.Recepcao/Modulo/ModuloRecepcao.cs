using Clinica.Desktop.Shell.Componentes;
using Clinica.Desktop.Shell.Modulos;
using Clinica.Domain.Entities;
using Clinica.Recepcao.ViewModels;
using Clinica.Recepcao.Views;
using Microsoft.Extensions.DependencyInjection;

namespace Clinica.Recepcao.Modulo;

/// <summary>
/// Módulo da Recepção. Publica os itens de menu e sabe construir as telas
/// correspondentes — o shell não conhece nenhuma delas.
///
/// A ordem dos itens é a do dia de trabalho: primeiro o Painel (como está o dia),
/// depois a Agenda (o que vai acontecer), a Fila (o que está acontecendo agora) e,
/// por último, o cadastro da equipe — que se mexe raramente, mas destrava tudo o mais.
/// </summary>
public sealed class ModuloRecepcao : IModuloApp
{
    public const string ChavePainel = ChavesSuite.PainelRecepcao;
    public const string ChaveAgenda = ChavesSuite.AgendaRecepcao;
    public const string ChaveFila = "fila";
    public const string ChaveNovoAtendimento = "novo-atendimento";

    /// <summary>
    /// A aba "Marcar" do item "Atendimento" (set/2026). A criação de horário mora no Novo
    /// atendimento desde a parcela 70; o que era um rádio QUANDO no meio do formulário
    /// virou duas abas — Lançar (o paciente está aqui) e Marcar (dia e horário) —, cada
    /// uma só com os campos do seu modo. A agenda navega para ESTA chave.
    /// </summary>
    public const string ChaveMarcarHorario = "marcar-horario";
    public const string ChaveConsultas = "consultas";

    /// <summary>A conferência do que foi lançado — aba de "Atendimento" (set/2026).</summary>
    public const string ChaveLancamentos = "lancamentos";

    /// <summary>
    /// A fila "Retornos a marcar" — aba de "Atendimento" (set/2026): quem saiu do
    /// atendimento com pedido de retorno e ainda não tem horário. O Consultório grava a
    /// data sugerida desde a parcela 77 e o balcão não tinha por onde lê-la.
    /// </summary>
    public const string ChaveRetornosAMarcar = "retornos-a-marcar";
    public const string ChavePacientes = ChavesSuite.PacientesRecepcao;
    public const string ChaveProntuario = "prontuario";
    public const string ChavePrescricoes = ChavesSuite.PrescricoesRecepcao;
    public const string ChaveRetorno = ChavesSuite.RetornoPacientes;

    /// <summary>
    /// A sala de infusão (parcela 48). A chave é a MESMA que o Consultório publica —
    /// é a mesma tela, e chave diferente faria a navegação da suíte abrir duas. Por ser
    /// a única chave publicada por DOIS módulos, ela mora em <see cref="ChavesSuite"/>:
    /// escrita à mão de cada lado, renomear uma compilava dos dois e quebrava em produção.
    /// </summary>
    public const string ChaveSalaInfusao = ChavesSuite.SalaInfusao;

    /// <summary>
    /// A tela da ENFERMAGEM (parcela 71). Terceira tela do SHELL publicada por DOIS
    /// módulos, pela razão da sala de infusão acima — e a chave mora em ChavesSuite
    /// porque literal à mão dos dois lados sempre compila e some em silêncio.
    /// </summary>
    public const string ChaveEnfermagem = ChavesSuite.Enfermagem;
    public const string ChaveDocumentos = ChavesSuite.Documentos;

    /// <summary>
    /// Pacotes de sessões (parcela 60). A chave é a MESMA que o Financeiro publica — é a
    /// mesma tela, e chave diferente faria o Gerente, que carrega os dois, mostrar a linha
    /// duas vezes. Mora em <see cref="ChavesSuite"/> desde a parcela 62, pela razão da
    /// sala de infusão: string à mão dos dois lados sempre compila.
    /// </summary>
    public const string ChavePacotes = ChavesSuite.Pacotes;
    public const string ChaveEquipe = "equipe";

    // ===== Itens COMPOSTOS (parcela 55) =====
    public const string ChaveGrupoAgenda = "agenda";
    // ⚠️ "Pacientes" e "Prescrições" vêm da ChavesSuite desde a parcela 95: o Consultório
    // publica compostos com o mesmo rótulo, e a dedupe do shell só funde por CHAVE —
    // literal à mão nos dois módulos era a duplicata da checagem 45 esperando para
    // acontecer no Gerente Geral.
    public const string ChaveGrupoPacientes = ChavesSuite.GrupoPacientes;
    public const string ChaveGrupoAtendimento = "atendimento";
    public const string ChaveGrupoPrescricoes = ChavesSuite.GrupoPrescricoes;

    /// <summary>
    /// O item composto "Prontuário" (set/2026). Junta a leitura POR PACIENTE (a tela
    /// deste módulo) com a lista plana de REGISTROS E PENDÊNCIAS do Consultório.
    ///
    /// ⚠️ Ele existe pela mesma razão do <see cref="ChaveGrupoPrescricoes"/> logo acima, e
    /// pelo mesmo defeito: os dois módulos publicavam item próprio, com chaves diferentes
    /// ("prontuario" e "consultorio-prontuarios"), então a dedupe por chave do shell não
    /// pegava e o Gerente Geral mostrava "Prontuário" e "Prontuários" lado a lado em
    /// PACIENTE — dois rótulos quase iguais que fazem a pessoa clicar nos dois para
    /// descobrir o que é cada um. As duas telas existem e respondem perguntas diferentes:
    /// viraram abas, que é onde a diferença se lê.
    /// </summary>
    public const string ChaveGrupoProntuario = "prontuario-geral";

    /// <summary>
    /// Ajuda e suporte — tela do SHELL (a única das 18 do handoff de design que não
    /// existia). Os QUATRO módulos publicam a MESMA chave (<see cref="ChavesSuite.Ajuda"/>):
    /// dúvida não tem dono, e cada exe carrega um recorte de módulos.
    /// </summary>
    public const string ChaveAjuda = ChavesSuite.Ajuda;

    public string Nome => "Recepção";

    // A permiss\u00E3o exigida por item entrou na parcela 5: quem n\u00E3o a tem n\u00E3o v\u00EA o item
    // na sidebar. Perfis de Recep\u00E7\u00E3o e Profissional j\u00E1 nascem com as daqui.
    public IReadOnlyList<ItemMenuModulo> Itens { get; } =
    [
        // O painel do balcão abre o `Clinica.Recepcao.exe`, e é ABA de "Painel" no Gerente
        // Geral — onde a marca de abertura é a do painel da DIREÇÃO, como a parcela 22
        // estabeleceu. Aqui ele é o primeiro item porque, sem a Direção carregada, o item
        // pai não existe e este volta a ser menu: é ele que a recepção vê ao entrar.
        new ItemMenuModulo
        {
            Chave = ChavePainel, Rotulo = "In\u00EDcio", Glifo = "\uE80F",
            Grupo = GrupoSidebar.Gestao, Requer = Permissao.VerAgenda, Inicial = true
        },
        // ===== GESTÃO =====
        // A agenda do balcão e a semana de quem atende respondem a MESMA pergunta ("quando
        // cabe"), em recortes diferentes. No `Clinica.Recepcao.exe` o Consultório não é
        // carregado, sobra uma aba só, e o shell mostra a tela direto — sem régua de uma
        // aba só.
        new ItemMenuModulo
        {
            Chave = ChaveGrupoAgenda, Rotulo = "Agenda", Glifo = "\uE787",
            Grupo = GrupoSidebar.Gestao, Requer = Permissao.VerAgenda,
            Abas =
            [
                new AbaMenu("Dia", ChaveAgenda),
                new AbaMenu("Semana do profissional", ChavesSuite.ConsultorioSemana)
            ]
        },
        new ItemMenuModulo
        {
            // "Fila do dia" (parcela 95): era "Recepção / Check-in" — item que precisa de dois
            // nomes é item que ainda não decidiu o que é, e a direção pediu simples.
            Chave = ChaveFila, Rotulo = "Fila do dia", Glifo = "\uE8FD",
            Grupo = GrupoSidebar.Gestao, Requer = Permissao.VerAgenda
        },
        // A SALA DE INFUSÃO, onde a ENFERMAGEM alcança (parcela 48).
        //
        // `PerfilAcesso.Enfermagem` e `Permissao.ChecarPrescricao` existem desde a parcela
        // 42, e a única tela para checar estava no `Clinica.Modulo.Clinico` — carregado
        // pelo exe do MÉDICO. A técnica que administra a infusão teria de usar o app dele.
        //
        // A tela não foi copiada: ela SUBIU para o shell (`Componentes/SalaInfusaoView`),
        // como o mapa corporal e a emissão de documento na parcela 36. Os dois módulos
        // publicam a MESMA chave, e quem PRESCREVE continua no Consultório — é a divisão
        // que dá valor à conferência: são duas pessoas.
        new ItemMenuModulo
        {
            Chave = ChaveSalaInfusao, Rotulo = "Sala de infusão", Glifo = "\uE9D5",
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
        // Cadastro da equipe \u00E9 gest\u00E3o da cl\u00EDnica, n\u00E3o do paciente: quem mexe aqui est\u00E1
        // organizando quem atende e onde, n\u00E3o atendendo algu\u00E9m.
        new ItemMenuModulo
        {
            Chave = ChaveEquipe, Rotulo = "Profissionais e salas", Glifo = "\uE716",
            Grupo = GrupoSidebar.Gestao, Requer = Permissao.GerenciarEquipe
        },

        // ===== PACIENTE =====
        // A lista do balcão e a de quem atende são a mesma gente com recortes diferentes —
        // e apareciam como dois itens quase homônimos no Gerente Geral.
        //
        // ⚠️ Os rótulos dizem a PERGUNTA de cada aba, e não o recorte de quem a abre
        // (parcela 88, 3ª rodada): depois de "não existe 'meu paciente'", as duas listam
        // todo mundo. "Cadastro" responde *quem é essa pessoa?* — ordem de nome, telefone
        // e convênio, para achar quem ligou; "Em tratamento" responde *como está indo o
        // tratamento dela?* — de quem veio por último ao mais antigo, com a leitura da dor.
        // Chamar as duas de "Pacientes" faria a pessoa abrir as duas para descobrir a
        // diferença.
        new ItemMenuModulo
        {
            Chave = ChaveGrupoPacientes, Rotulo = "Pacientes", Glifo = "\uE77B",
            Grupo = GrupoSidebar.Paciente, Requer = Permissao.VerFichaPaciente,
            Abas =
            [
                new AbaMenu("Cadastro", ChavePacientes),
                new AbaMenu("Em tratamento", ChavesSuite.ConsultorioPacientes)
            ]
        },
        // Novo atendimento e Consultas vieram do app de FATURAMENTO na parcela 46.
        //
        // Nenhum dos dois era feature nova: os dois existiam, no posto errado. Lançar
        // atendimento AVULSO (quem chegou sem horário) e renovar a consulta do convênio
        // são atos que se fazem com o PACIENTE NA FRENTE, e moravam na máquina de quem
        // não recebe ninguém.
        //
        // Os dois viraram abas do mesmo item porque são o mesmo ato visto em dois tempos:
        // lançar a sessão de hoje e cuidar da consulta que a autoriza.
        new ItemMenuModulo
        {
            Chave = ChaveGrupoAtendimento, Rotulo = "Atendimento", Glifo = "\uEB51",
            Grupo = GrupoSidebar.Paciente, Requer = Permissao.LancarAtendimento,
            // "Lançar" e "Marcar" são o MESMO ViewModel com o modo fixado (set/2026, "quanto
            // mais simples, melhor"): a pergunta QUANDO saiu do meio do formulário e virou
            // a escolha da aba. A aba Marcar só aparece para quem tem `EditarAgenda` (o
            // `Requer` do item dela) — é a metade visível da guarda do Salvar.
            Abas =
            [
                new AbaMenu("Lan\u00E7ar", ChaveNovoAtendimento),
                new AbaMenu("Marcar", ChaveMarcarHorario),
                new AbaMenu("Retornos a marcar", ChaveRetornosAMarcar),
                new AbaMenu("Lan\u00E7amentos", ChaveLancamentos),
                new AbaMenu("Consultas de conv\u00EAnio", ChaveConsultas)
            ]
        },
        // Prontu\u00E1rio e Prescri\u00E7\u00F5es s\u00E3o itens de PRIMEIRO N\u00CDVEL na proposta e, at\u00E9 a
        // parcela 24, s\u00F3 existiam por dentro da ficha do paciente.
        //
        // \u26A0\uFE0F A tela CONTINUA sendo um item, e isso n\u00E3o \u00E9 sobra: quem a esconde do menu
        // \u00E9 o item composto abaixo, e s\u00F3 enquanto ele existe. No `Clinica.Recepcao.exe` o
        // m\u00F3dulo Cl\u00EDnico n\u00E3o \u00E9 carregado, o composto fica com UMA aba e o shell mostra a
        // tela direto \u2014 e no `Clinica.Clinico.exe` \u00E9 o contr\u00E1rio: sem este m\u00F3dulo n\u00E3o h\u00E1
        // composto, e "Prontu\u00E1rios" volta a ser item de menu comum. \u00C9 o mecanismo da tela
        // \u00F3rf\u00E3 (parcela 55): esconder por decreto faria a tela sumir do \u00FAnico app onde
        // algu\u00E9m a usa todo dia.
        new ItemMenuModulo
        {
            Chave = ChaveProntuario, Rotulo = "Prontu\u00E1rio", Glifo = "\uE7C3",
            Grupo = GrupoSidebar.Paciente, Requer = Permissao.VerProntuario
        },
        // \u26A0\uFE0F A SEGUNDA duplicata da sidebar, e ela \u00E9 a MESMA hist\u00F3ria das "Prescri\u00E7\u00F5es"
        // logo abaixo (set/2026 \u2014 o cliente mandou o print do Gerente com os dois itens):
        // "Prontu\u00E1rio" (deste m\u00F3dulo) e "Prontu\u00E1rios" (do Consult\u00F3rio) apareciam um do
        // lado do outro em PACIENTE, com chaves diferentes \u2014 e a dedupe do
        // `ShellViewModel` casa por CHAVE, ent\u00E3o ela n\u00E3o pegava.
        //
        // As duas telas respondem perguntas DIFERENTES, e \u00E9 por isso que nenhuma foi
        // apagada: a daqui \u00E9 "abra o prontu\u00E1rio DESTE paciente"; a de l\u00E1 \u00E9 "o que foi
        // escrito e o que ainda FALTA escrever na cl\u00EDnica" \u2014 uma fila de trabalho. Como
        // abas, cada r\u00F3tulo diz qual \u00E9 qual.
        new ItemMenuModulo
        {
            Chave = ChaveGrupoProntuario, Rotulo = "Prontu\u00E1rio", Glifo = "\uE7C3",
            Grupo = GrupoSidebar.Paciente, Requer = Permissao.VerProntuario,
            Abas =
            [
                new AbaMenu("Por paciente", ChaveProntuario),
                new AbaMenu("Registros e pend\u00EAncias", ChavesSuite.ConsultorioProntuarios)
            ]
        },
        // ⚠️ Aqui morava uma DUPLICATA: "Prescrições" era publicado por este módulo e
        // pelo Consultório com chaves diferentes (`prescricoes` e
        // `consultorio-prescricoes`), então a dedupe por chave do `ShellViewModel` não
        // pegava, e o Gerente Geral mostrava dois itens com o MESMO rótulo, um do lado do
        // outro, em PACIENTE. As duas telas existem e fazem coisas próximas de postos
        // diferentes — viraram abas, que é onde a diferença se lê.
        new ItemMenuModulo
        {
            Chave = ChaveGrupoPrescricoes, Rotulo = "Prescri\u00E7\u00F5es", Glifo = "\uE8A5",
            Grupo = GrupoSidebar.Paciente, Requer = Permissao.VerFichaPaciente,
            Abas =
            [
                new AbaMenu("Receitu\u00E1rio", ChavePrescricoes),
                new AbaMenu("No consult\u00F3rio", ChavesSuite.ConsultorioPrescricoes),
                new AbaMenu("Infus\u00E3o", ChavesSuite.ConsultorioPrescricaoInfusao)
            ]
        },
        // As nove folhas do mockup num lugar só (parcela 24). Existiam todas e nenhuma
        // estava no mesmo lugar: quatro dentro da ficha do paciente, três no botão certo
        // da aba certa dessa ficha, o recibo no Caixa, o orçamento só dentro de um pacote
        // vendido e o fechamento do período só no app de faturamento.
        new ItemMenuModulo
        {
            Chave = ChaveDocumentos, Rotulo = "Documentos", Glifo = "\uE8B7",
            // \u26A0\uFE0F A PORTA \u00E9 `VerDocumentos` desde a parcela 59, a pedido da dire\u00E7\u00E3o \u2014 antes
            // era `VerFichaPaciente`, que todo perfil de balc\u00E3o tem, e por isso a
            // recepcionista alcan\u00E7ava as dez folhas. O bit fecha a SE\u00C7\u00C3O; o que decide o
            // que aparece DENTRO dela \u00E9 o acesso de cada folha
            // (`FolhaCatalogo.PermissaoVer`) \u2014 sem isso, fechar a porta levaria junto o
            // recibo e a declara\u00E7\u00E3o de comparecimento que o balc\u00E3o emite todo dia.
            Grupo = GrupoSidebar.Paciente, Requer = Permissao.VerDocumentos
        },

        // PACOTES DE SESSÕES (parcela 60). A tela existe desde a parcela 4 e a única porta
        // estava no app do FINANCEIRO — mas quem vende dez sessões ao paciente é o BALCÃO,
        // com ele na frente. É o defeito recorrente do projeto na variante "a porta está no
        // módulo de quem não usa", e ele bloqueava o caso que motivou o PARTICULAR: o
        // paciente sem convênio que compra um pacote.
        //
        // A tela não foi copiada: SUBIU para o shell (`Componentes/PacotesView`), como a
        // sala de infusão na parcela 48, e os dois módulos publicam a MESMA chave.
        new ItemMenuModulo
        {
            Chave = ChavePacotes, Rotulo = "Pacotes", Glifo = "",
            Grupo = GrupoSidebar.Paciente, Requer = Permissao.VenderPacote
        },

        // ===== Sub-telas =====
        // Continuam sendo itens: `NavegacaoSuite` navega para várias delas por chave, e a
        // dedupe do shell só some com a linha quando o item PAI está presente. Sem o pai
        // (num exe que não carrega quem o publica), elas voltam a ser menu.
        new ItemMenuModulo
        {
            Chave = ChaveAgenda, Rotulo = "Agenda", Glifo = "\uE787",
            Grupo = GrupoSidebar.Gestao, Requer = Permissao.VerAgenda
        },
        new ItemMenuModulo
        {
            Chave = ChavePacientes, Rotulo = "Pacientes / CRM", Glifo = "\uE77B",
            Grupo = GrupoSidebar.Paciente, Requer = Permissao.VerFichaPaciente
        },
        new ItemMenuModulo
        {
            Chave = ChaveNovoAtendimento, Rotulo = "Lan\u00E7ar atendimento", Glifo = "\uEB51",
            Grupo = GrupoSidebar.Paciente, Requer = Permissao.LancarAtendimento
        },
        // A aba Marcar. Item declarado pela checagem 28 (toda `AbaMenu` aponta para item
        // de algum m\u00F3dulo); quem o esconde da sidebar \u00E9 o PAI. `EditarAgenda`, e n\u00E3o
        // `LancarAtendimento`: marcar hor\u00E1rio \u00E9 mexer na agenda \u2014 com a chave "guia no
        // agendamento" ligada o Salvar exige os DOIS bits (rel\u00EA a chave no ato).
        new ItemMenuModulo
        {
            Chave = ChaveMarcarHorario, Rotulo = "Marcar hor\u00E1rio", Glifo = "\uE787",
            Grupo = GrupoSidebar.Paciente, Requer = Permissao.EditarAgenda
        },
        new ItemMenuModulo
        {
            Chave = ChaveConsultas, Rotulo = "Consultas (conv\u00EAnio)", Glifo = "\uE8A5",
            Grupo = GrupoSidebar.Paciente, Requer = Permissao.LancarAtendimento
        },
        // A confer\u00EAncia do que foi lan\u00E7ado. Item declarado porque a checagem 28 exige
        // que toda chave de `AbaMenu` seja item de algum m\u00F3dulo \u2014 quem o esconde da
        // sidebar \u00E9 o PAI ("Atendimento"), e s\u00F3 onde o pai existe.
        new ItemMenuModulo
        {
            Chave = ChaveLancamentos, Rotulo = "Lan\u00E7amentos", Glifo = "\uE9D5",
            Grupo = GrupoSidebar.Paciente, Requer = Permissao.LancarAtendimento
        },
        // Retornos a marcar: LEITURA da agenda (VerAgenda). Marcar, dentro dela, exige
        // EditarAgenda no comando — as duas metades em cada linha.
        new ItemMenuModulo
        {
            Chave = ChaveRetornosAMarcar, Rotulo = "Retornos a marcar", Glifo = "\uE823",
            Grupo = GrupoSidebar.Paciente, Requer = Permissao.VerAgenda
        },
        new ItemMenuModulo
        {
            Chave = ChavePrescricoes, Rotulo = "Receitu\u00E1rio", Glifo = "\uE8A5",
            // \u26A0\uFE0F `VerProntuario` desde a parcela 59. A tela LISTA os documentos cl\u00EDnicos do
            // paciente \u2014 receita, atestado, pedido de exame \u2014 e emite qualquer um deles
            // pela janela gen\u00E9rica. Deix\u00E1-la em `VerFichaPaciente` faria a porta nova da
            // central de documentos ser cosm\u00E9tica: bastaria a pessoa clicar no item ao
            // lado para ler as mesmas receitas. Checagem de acesso que s\u00F3 existe numa
            // porta \u00E9 o defeito recorrente do projeto, com o agravante de PARECER coberta.
            Grupo = GrupoSidebar.Paciente, Requer = Permissao.VerProntuario
        },
        // Chamar de volta quem parou de vir (parcela 48). Quem telefona é o BALCÃO — e é
        // por isso que ela continua aqui, virando aba de "Marketing / Recall" só onde a
        // Direção está carregada.
        new ItemMenuModulo
        {
            Chave = ChaveRetorno, Rotulo = "Retorno de pacientes", Glifo = "\uE8AF",
            Grupo = GrupoSidebar.Paciente, Requer = Permissao.GerenciarCampanhas
        },

        // AJUDA E SUPORTE \u2014 sem `Requer` de prop\u00F3sito (o padr\u00E3o \u00E9 "sempre vis\u00EDvel"):
        // fechar o manual por permiss\u00E3o trancaria justamente quem mais precisa dele.
        // Fica ao FIM da lista deste m\u00F3dulo: ajuda n\u00E3o \u00E9 passo do dia de trabalho. (No
        // Gerente Geral, que carrega os quatro, a posi\u00E7\u00E3o em GEST\u00C3O segue a ordem de
        // carregamento dos m\u00F3dulos \u2014 a dedupe fica com a publica\u00E7\u00E3o do primeiro.)
        new ItemMenuModulo
        {
            Chave = ChaveAjuda, Rotulo = "Ajuda e suporte", Glifo = "\uE897",
            Grupo = GrupoSidebar.Gestao
        }
    ];

    public void Registrar(IServiceCollection servicos)
    {
        // A ponte agenda → novo atendimento (parcela 70): singleton de UM pedido de
        // pré-preenchimento, definido por quem navega e consumido pela tela ao abrir.
        servicos.AddSingleton<PreenchimentoNovoAtendimento>();
        servicos.AddSingleton<PedidoAgenda>();

        servicos.AddTransient<PainelViewModel>();
        servicos.AddTransient<AgendaViewModel>();
        servicos.AddTransient<DocumentosViewModel>();
        servicos.AddTransient<FilaViewModel>();
        servicos.AddTransient<PacientesViewModel>();
        servicos.AddTransient<NovoAtendimentoViewModel>();
        servicos.AddTransient<ConsultasViewModel>();
        servicos.AddTransient<LancamentosViewModel>();
        servicos.AddTransient<RetornosAMarcarViewModel>();
        servicos.AddTransient<RetornoViewModel>();
        servicos.AddTransient<SalaInfusaoViewModel>();
        servicos.AddTransient<EnfermagemViewModel>();
        // Tela do SHELL, como a sala de infusão: quem publica o item REGISTRA e CONSTRÓI.
        // Faltavam as duas coisas — o item acendia na sidebar e nada abria (parcela 62).
        servicos.AddTransient<PacotesViewModel>();
        servicos.AddTransient<ProntuarioViewModel>();
        servicos.AddTransient<PrescricoesViewModel>();
        servicos.AddTransient<EquipeViewModel>();
        // Os ViewModels de formulário (agendamento, lista de espera, profissional,
        // sala, paciente, evolução) são construídos à mão pelas telas: cada janela abre
        // com o formulário limpo e, quando é edição, precisa receber o id no construtor.
    }

    /// <summary>
    /// As duas abas do "Atendimento" sobre o MESMO ViewModel, com o modo fixado ANTES de a
    /// tela aparecer — o rádio QUANDO da parcela 70 virou a escolha da aba (set/2026).
    /// </summary>
    private static NovoAtendimentoView NovoAtendimento(IServiceProvider servicos, bool marcar)
    {
        var vm = servicos.GetRequiredService<NovoAtendimentoViewModel>();
        vm.FixarModo(marcar);
        return new NovoAtendimentoView { DataContext = vm };
    }

    public object? CriarTela(string chave, IServiceProvider servicos) => chave switch
    {
        ChavePainel => new PainelView { DataContext = servicos.GetRequiredService<PainelViewModel>() },
        ChaveAgenda => new AgendaView { DataContext = servicos.GetRequiredService<AgendaViewModel>() },
        ChaveFila => new FilaView { DataContext = servicos.GetRequiredService<FilaViewModel>() },
        ChavePacientes => new PacientesView { DataContext = servicos.GetRequiredService<PacientesViewModel>() },
        ChaveNovoAtendimento => NovoAtendimento(servicos, marcar: false),
        ChaveMarcarHorario => NovoAtendimento(servicos, marcar: true),
        ChaveConsultas => new ConsultasView { DataContext = servicos.GetRequiredService<ConsultasViewModel>() },
        ChaveLancamentos => new LancamentosView
        {
            DataContext = servicos.GetRequiredService<LancamentosViewModel>()
        },
        ChaveRetornosAMarcar => new RetornosAMarcarView
        {
            DataContext = servicos.GetRequiredService<RetornosAMarcarViewModel>()
        },
        ChaveRetorno => new RetornoView { DataContext = servicos.GetRequiredService<RetornoViewModel>() },
        ChaveSalaInfusao => new SalaInfusaoView
        {
            DataContext = servicos.GetRequiredService<SalaInfusaoViewModel>()
        },
        ChaveEnfermagem => new EnfermagemView
        {
            DataContext = servicos.GetRequiredService<EnfermagemViewModel>()
        },
        // Tela do SHELL (parcela 60), como a sala de infusão acima. O item era publicado
        // e este `case` NÃO existia: o shell marcava o item como ativo, `MontarTela`
        // devolvia null e a navegação saía em silêncio — menu aceso, tela parada. Item
        // publicado sem `case` é o "botão que não faz nada" da parcela 41 na sidebar, e
        // nenhuma rede o via: a chave era string à mão, que sempre compila.
        ChavePacotes => new PacotesView
        {
            DataContext = servicos.GetRequiredService<PacotesViewModel>()
        },
        ChaveProntuario => new ProntuarioView { DataContext = servicos.GetRequiredService<ProntuarioViewModel>() },
        ChavePrescricoes => new PrescricoesView { DataContext = servicos.GetRequiredService<PrescricoesViewModel>() },
        ChaveDocumentos => new DocumentosView { DataContext = servicos.GetRequiredService<DocumentosViewModel>() },
        ChaveEquipe => new EquipeView { DataContext = servicos.GetRequiredService<EquipeViewModel>() },
        // Tela do shell, ESTÁTICA: conteúdo literal, sem ViewModel — não há o que resolver.
        ChaveAjuda => new AjudaView(),
        _ => null
    };
}
