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
    public const string ChavePagamentos = "pagamentos-recepcao";
    public const string ChavePainel = ChavesSuite.PainelRecepcao;
    public const string ChaveAgenda = ChavesSuite.AgendaRecepcao;
    public const string ChaveFila = "fila";

    /// <summary>
    /// Entrada da recepção para marcar dia e horário. A conclusão clínica fica com o médico.
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

    /// <summary>
    /// Preços do particular (set/2026) — a direção pediu que a Recepção também cadastre e
    /// edite. Tela do shell, chave da suíte: o Gerente a publica como aba da Tabela de preço.
    /// </summary>
    public const string ChavePrecosParticular = ChavesSuite.PrecosParticular;
    public const string ChaveEquipe = "equipe";

    // ===== Itens COMPOSTOS (parcela 55) =====
    public const string ChaveGrupoAgenda = "agenda";
    // ⚠️ "Pacientes" e "Prescrições" vêm da ChavesSuite desde a parcela 95: o Consultório
    // publica compostos com o mesmo rótulo, e a dedupe do shell só funde por CHAVE —
    // literal à mão nos dois módulos era a duplicata da checagem 45 esperando para
    // acontecer no Gerente Geral.
    public const string ChaveGrupoPacientes = ChavesSuite.GrupoPacientes;
    public const string ChaveGrupoAtendimento = "atendimento";

    /// <summary>
    /// O item composto "Particular e pacotes" (set/2026) — a resposta à reprovação do
    /// cliente: *"o fluxo do particular não está intuitivo e nem eu mesmo consegui
    /// entender como fazer um atendimento particular, vender pacote ou sessão
    /// particular"*.
    ///
    /// Nada de capacidade nova: as duas telas existiam e eram DOIS itens soltos no grupo
    /// PACIENTE, "Pacotes" e "Preços do particular", separados por outros itens e sem
    /// nada dizendo que respondem à mesma pergunta ("quanto custa e como se vende a
    /// sessão de quem paga do bolso"). Quem procura "particular" na sidebar achava um
    /// item que fala de PREÇO e nenhum que fale de vender.
    ///
    /// ⚠️ O rótulo diz as DUAS coisas de propósito. "Particular" sozinho mentiria sobre
    /// o pacote — pacote é venda da clínica e o paciente de convênio também compra
    /// (parcela 4) —, e "Pacotes" sozinho é o que já existia e não era achado.
    ///
    /// Chave LOCAL, e não em <see cref="ChavesSuite"/>: nenhum outro módulo publica
    /// composto com este rótulo, então não há a duplicata que a checagem 45 pega. O
    /// Financeiro continua publicando "Pacotes" solto, e no exe dele — que não carrega a
    /// Recepção — a tela volta a ser item de menu, como manda a regra da tela órfã.
    /// </summary>
    public const string ChaveGrupoParticular = "particular";

    /// <summary>
    /// O item composto "Prontuário" (set/2026). Junta a leitura POR PACIENTE (a tela
    /// deste módulo) com a lista plana de REGISTROS E PENDÊNCIAS do Consultório.
    ///
    /// ⚠️ Ele existe pela mesma razão do grupo "Pacientes" logo acima, e
    /// pelo mesmo defeito: os dois módulos publicavam item próprio, com chaves diferentes
    /// ("prontuario" e "consultorio-prontuarios"), então a dedupe por chave do shell não
    /// pegava e o Gerente Geral mostrava "Prontuário" e "Prontuários" lado a lado em
    /// PACIENTE — dois rótulos quase iguais que fazem a pessoa clicar nos dois para
    /// descobrir o que é cada um. As duas telas existem e respondem perguntas diferentes:
    /// viraram abas, que é onde a diferença se lê.
    /// </summary>
    public const string ChaveGrupoProntuario = ChavesSuite.GrupoProntuario;

    /// <summary>
    /// Ajuda e suporte — tela do SHELL (a única das 18 do handoff de design que não
    /// existia). Os QUATRO módulos publicam a MESMA chave (<see cref="ChavesSuite.Ajuda"/>):
    /// dúvida não tem dono, e cada exe carrega um recorte de módulos.
    /// </summary>
    public const string ChaveAjuda = ChavesSuite.Ajuda;

    public const string ChaveConfirmacoes = ChavesSuite.ConfirmacoesAgenda;
    public string Nome => "Recepção";

    // A permiss\u00E3o exigida por item entrou na parcela 5: quem n\u00E3o a tem n\u00E3o v\u00EA o item
    // na sidebar. Perfis de Recep\u00E7\u00E3o e Profissional j\u00E1 nascem com as daqui.
    public IReadOnlyList<ItemMenuModulo> Itens { get; } =
    [
        new ItemMenuModulo { Chave = ChaveConfirmacoes, Rotulo = "Confirmações de agenda", Glifo = "\uE73E", Grupo = GrupoSidebar.Gestao, Requer = Permissao.VerAgenda },
        new ItemMenuModulo
        {
            Chave = ChavePagamentos, Rotulo = "Pagamentos", Glifo = "\uE8C7", Icone = "recibo",
            Grupo = GrupoSidebar.Paciente, Requer = Permissao.VenderPacote
        },
        // O painel do balcão abre o `Clinica.Recepcao.exe`, e é ABA de "Painel" no Gerente
        // Geral — onde a marca de abertura é a do painel da DIREÇÃO, como a parcela 22
        // estabeleceu. Aqui ele é o primeiro item porque, sem a Direção carregada, o item
        // pai não existe e este volta a ser menu: é ele que a recepção vê ao entrar.
        new ItemMenuModulo
        {
            Chave = ChavePainel, Rotulo = "In\u00EDcio", Glifo = "\uE80F", Icone = "home",
            Grupo = GrupoSidebar.Gestao, Requer = Permissao.VerAgenda, Inicial = true
        },
        // ===== GESTÃO =====
        // A AGENDA é um item só, com as três formas de olhar o mesmo dia (set/2026 — a
        // cliente: a agenda e a fila do dia "acabam sendo uma duplicata para a mesma
        // função", e no Smart Clinic são uma tela só):
        //   · DIA   — a lista do dia com o status em cada linha, que era a "Fila do dia"
        //             em cinco raias (parcelas 26, 58, 87). É a ABERTURA do item, como o
        //             "Hoje" da Minha agenda do Consultório;
        //   · GRADE — os horários em coluna por profissional/sala, onde se marca clicando
        //             no vão e onde mora a janela do horário (remarcar, reabrir…);
        //   · SEMANA DO PROFISSIONAL — a do Consultório. No `Clinica.Recepcao.exe` ele não
        //             é carregado, sobram duas abas.
        // A "Fila do dia" deixou de ser item de menu: a chave continua declarada (abaixo),
        // porque o painel e o Consultório navegam por ela — quem a esconde é este pai.
        new ItemMenuModulo
        {
            Chave = ChaveGrupoAgenda, Rotulo = "Agenda", Glifo = "\uE787", Icone = "cal",
            Grupo = GrupoSidebar.Gestao, Requer = Permissao.VerAgenda,
            Abas =
            [
                new AbaMenu("Dia", ChaveFila),
                new AbaMenu("Grade", ChaveAgenda),
                new AbaMenu("Semana do profissional", ChavesSuite.ConsultorioSemana)
            ]
        },
        new ItemMenuModulo
        {
            // A aba DIA da Agenda (set/2026). Era o item "Fila do dia" (parcela 95: antes
            // "Recepção / Check-in"). Declarado pela checagem 28 — toda `AbaMenu` aponta
            // para item — e escondido pelo composto acima.
            Chave = ChaveFila, Rotulo = "Agenda do dia", Glifo = "\uE8FD", Icone = "dia",
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
            Chave = ChaveSalaInfusao, Rotulo = "Sala de infusão", Glifo = "\uE9D5", Icone = "gota",
            Grupo = GrupoSidebar.Atendimento, Requer = Permissao.ChecarPrescricao
        },

        // A tela da ENFERMAGEM: TODOS os pacientes cadastrados e a evolução de cada
        // um. SEPARADA da sala de infusão de propósito — a sala responde "o que
        // executar agora" e só mostra as folhas do dia; esta responde "quem eu atendi
        // e o que escrevi", e a clínica disse que todo paciente passa pela enfermagem.
        // Terceira pergunta, terceira tela.
        new ItemMenuModulo
        {
            Chave = ChaveEnfermagem, Rotulo = "Enfermagem", Glifo = "\uE95E", Icone = "coracao",
            Grupo = GrupoSidebar.Atendimento, Requer = Permissao.RegistrarEvolucaoEnfermagem
        },
        // Cadastro da equipe \u00E9 gest\u00E3o da cl\u00EDnica, n\u00E3o do paciente: quem mexe aqui est\u00E1
        // organizando quem atende e onde, n\u00E3o atendendo algu\u00E9m.
        new ItemMenuModulo
        {
            Chave = ChaveEquipe, Rotulo = "Profissionais e salas", Glifo = "\uE716", Icone = "equipe",
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
            Chave = ChaveGrupoPacientes, Rotulo = "Pacientes", Glifo = "\uE77B", Icone = "pessoa",
            Grupo = GrupoSidebar.Paciente, Requer = Permissao.VerFichaPaciente,
            Abas =
            [
                new AbaMenu("Cadastro", ChavePacientes),
                new AbaMenu("Em tratamento", ChavesSuite.ConsultorioPacientes)
            ]
        },
        // Marcação, retornos e conferência administrativa sem lançamento clínico direto.
        new ItemMenuModulo
        {
            Chave = ChaveGrupoAtendimento, Rotulo = "Marcar", Glifo = "\uE787", Icone = "cal",
            Grupo = GrupoSidebar.Atendimento, Requer = Permissao.EditarAgenda,
            // O formulário é aberto somente no modo de marcação, com EditarAgenda.
            Abas =
            [
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
            Chave = ChaveProntuario, Rotulo = "Prontu\u00E1rio", Glifo = "\uE7C3", Icone = "ficha",
            Grupo = GrupoSidebar.Atendimento, Requer = Permissao.VerProntuario
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
            Chave = ChaveGrupoProntuario, Rotulo = "Prontu\u00E1rio", Glifo = "\uE7C3", Icone = "ficha",
            Grupo = GrupoSidebar.Atendimento, Requer = Permissao.VerProntuario,
            Abas =
            [
                new AbaMenu("Por paciente", ChaveProntuario),
                new AbaMenu("Registros e pend\u00EAncias", ChavesSuite.ConsultorioProntuarios),
                new AbaMenu("Exames", ChavesSuite.ConsultorioExames)
            ]
        },
        // ⚠️ AQUI MORAVA O COMPOSTO "PRESCRIÇÕES", e ele saiu em set/2026 junto com a tela
        // "Receituário" — a TERCEIRA porta da Recepção para o mesmo ato.
        //
        // A Recepção tinha três lugares para papel: esta, a aba Documentos da ficha e a
        // central "Documentos". Cada uma fazia um SUBCONJUNTO diferente dos seis atos (a
        // central não assinava nem enviava; esta e a ficha não mexiam no link publicado), e
        // esta emitia com um botão genérico sob `Prescrever` — então a recepcionista não
        // alcançava por ela a declaração de comparecimento, que é o papel que ela entrega
        // todo dia. O cliente pediu UMA porta, e a central é a que responde a pergunta
        // inteira ("emitir um papel" + "achar o que já saiu") com a régua do catálogo, onde
        // cada folha traz o bit dela.
        //
        // Ninguém perdeu capacidade: os seis atos subiram para `AcoesDoDocumento` e a
        // central passou a ter todos; a lista POR PACIENTE continua na ficha (e a central
        // ganhou o filtro por paciente). Quem tem `VerProntuario` e NÃO tem `VerDocumentos`
        // — a técnica de enfermagem — continua alcançando os documentos do paciente pela
        // aba Documentos da ficha, que pede só `VerFichaPaciente`.
        //
        // O composto inteiro saiu porque as outras duas abas dele eram telas do CONSULTÓRIO:
        // no exe da Recepção, que não carrega aquele módulo, `AbasDisponiveis` as descarta e
        // o item ficaria sem aba nenhuma. No Gerente Geral, que carrega os dois, o composto
        // "Prescrições" agora é o do Consultório (Receitas e documentos · Infusão) — mesma
        // chave, e a dedupe do shell resolve.
        // As nove folhas do mockup num lugar só (parcela 24). Existiam todas e nenhuma
        // estava no mesmo lugar: quatro dentro da ficha do paciente, três no botão certo
        // da aba certa dessa ficha, o recibo no Caixa, o orçamento só dentro de um pacote
        // vendido e o fechamento do período só no app de faturamento.
        new ItemMenuModulo
        {
            Chave = ChaveDocumentos, Rotulo = "Documentos", Glifo = "\uE8B7", Icone = "pasta",
            // \u26A0\uFE0F A PORTA \u00E9 `VerDocumentos` desde a parcela 59, a pedido da dire\u00E7\u00E3o \u2014 antes
            // era `VerFichaPaciente`, que todo perfil de balc\u00E3o tem, e por isso a
            // recepcionista alcan\u00E7ava as dez folhas. O bit fecha a SE\u00C7\u00C3O; o que decide o
            // que aparece DENTRO dela \u00E9 o acesso de cada folha
            // (`FolhaCatalogo.PermissaoVer`) \u2014 sem isso, fechar a porta levaria junto o
            // recibo e a declara\u00E7\u00E3o de comparecimento que o balc\u00E3o emite todo dia.
            Grupo = GrupoSidebar.Paciente, Requer = Permissao.VerDocumentos
        },

        // PARTICULAR E PACOTES (set/2026) — o item que reúne o que se faz com quem paga do
        // bolso: quanto custa a sessão e como se vende o pacote. As duas telas já existiam
        // (a de Pacotes desde a parcela 4, a de Preços de set/2026) e eram dois itens
        // soltos no grupo PACIENTE — ver `ChaveGrupoParticular` para o motivo de juntá-las.
        //
        // O `Requer` é o bit MAIS FROUXO das abas (a regra da parcela 95), e aqui as duas
        // pedem o mesmo: `VenderPacote`. Combinar preço é o ato que ele já nomeia, e o
        // enum tem UM bit sobrando antes de virar `long` numa coluna de produção.
        new ItemMenuModulo
        {
            Chave = ChaveGrupoParticular, Rotulo = "Particular e pacotes",
            Glifo = "\uE719", Icone = "caixa",
            Grupo = GrupoSidebar.Paciente, Requer = Permissao.VenderPacote,
            Abas =
            [
                new AbaMenu("Pacotes", ChavePacotes),
                new AbaMenu("Pre\u00E7o da sess\u00E3o", ChavePrecosParticular)
            ]
        },

        // ===== Sub-telas =====
        // Continuam sendo itens: `NavegacaoSuite` navega para várias delas por chave, e a
        // dedupe do shell só some com a linha quando o item PAI está presente. Sem o pai
        // (num exe que não carrega quem o publica), elas voltam a ser menu.
        new ItemMenuModulo
        {
            Chave = ChaveAgenda, Rotulo = "Agenda", Glifo = "\uE787", Icone = "cal",
            Grupo = GrupoSidebar.Gestao, Requer = Permissao.VerAgenda
        },
        new ItemMenuModulo
        {
            Chave = ChavePacientes, Rotulo = "Pacientes / CRM", Glifo = "\uE77B", Icone = "pessoa",
            Grupo = GrupoSidebar.Paciente, Requer = Permissao.VerFichaPaciente
        },
        // As duas abas de "Particular e pacotes", declaradas como itens pela checagem 28 —
        // e, mais que isso, porque SEM ELAS o composto ficaria vazio no
        // `Clinica.Recepcao.exe`: `AbasDisponiveis` só enxerga aba cuja chave é item de um
        // módulo CARREGADO, e quem declara estas duas fora daqui é o Financeiro (Pacotes)
        // e o Gerente (Preços) — nenhum dos dois carregado no exe do balcão. Um composto
        // sem aba some da sidebar, e a recepcionista perderia as duas telas.
        // Quem as esconde do menu é o PAI, e só onde o pai existe.
        new ItemMenuModulo
        {
            Chave = ChavePacotes, Rotulo = "Pacotes", Glifo = "\uE719", Icone = "caixa",
            Grupo = GrupoSidebar.Paciente, Requer = Permissao.VenderPacote
        },
        new ItemMenuModulo
        {
            Chave = ChavePrecosParticular, Rotulo = "Pre\u00E7os do particular", Glifo = "\uE8EF", Icone = "etiqueta",
            Grupo = GrupoSidebar.Paciente, Requer = Permissao.VenderPacote
        },
        // A aba Marcar. Item declarado pela checagem 28 (toda `AbaMenu` aponta para item
        // de algum m\u00F3dulo); quem o esconde da sidebar \u00E9 o PAI. `EditarAgenda`, e n\u00E3o
        // `LancarAtendimento`: marcar hor\u00E1rio \u00E9 mexer na agenda \u2014 com a chave "guia no
        // agendamento" ligada o Salvar exige os DOIS bits (rel\u00EA a chave no ato).
        new ItemMenuModulo
        {
            Chave = ChaveMarcarHorario, Rotulo = "Marcar hor\u00E1rio", Glifo = "\uE787", Icone = "cal",
            Grupo = GrupoSidebar.Atendimento, Requer = Permissao.EditarAgenda
        },
        new ItemMenuModulo
        {
            Chave = ChaveConsultas, Rotulo = "Consultas (conv\u00EAnio)", Glifo = "\uE8A5", Icone = "recibo",
            Grupo = GrupoSidebar.Atendimento, Requer = Permissao.LancarAtendimento
        },
        // A confer\u00EAncia do que foi lan\u00E7ado. Item declarado porque a checagem 28 exige
        // que toda chave de `AbaMenu` seja item de algum m\u00F3dulo \u2014 quem o esconde da
        // sidebar \u00E9 o PAI ("Atendimento"), e s\u00F3 onde o pai existe.
        new ItemMenuModulo
        {
            Chave = ChaveLancamentos, Rotulo = "Lan\u00E7amentos", Glifo = "\uE9D5", Icone = "lista",
            Grupo = GrupoSidebar.Atendimento, Requer = Permissao.LancarAtendimento
        },
        // Retornos a marcar: LEITURA da agenda (VerAgenda). Marcar, dentro dela, exige
        // EditarAgenda no comando — as duas metades em cada linha.
        new ItemMenuModulo
        {
            Chave = ChaveRetornosAMarcar, Rotulo = "Retornos a marcar", Glifo = "\uE823", Icone = "volta",
            Grupo = GrupoSidebar.Atendimento, Requer = Permissao.VerAgenda
        },
        // Chamar de volta quem parou de vir (parcela 48). Quem telefona é o BALCÃO — e é
        // por isso que ela continua aqui, virando aba de "Marketing / Recall" só onde a
        // Direção está carregada.
        new ItemMenuModulo
        {
            Chave = ChaveRetorno, Rotulo = "Retorno de pacientes", Glifo = "\uE8AF", Icone = "volta",
            Grupo = GrupoSidebar.Paciente, Requer = Permissao.GerenciarCampanhas
        },

        // AJUDA E SUPORTE \u2014 sem `Requer` de prop\u00F3sito (o padr\u00E3o \u00E9 "sempre vis\u00EDvel"):
        // fechar o manual por permiss\u00E3o trancaria justamente quem mais precisa dele.
        // Fica ao FIM da lista deste m\u00F3dulo: ajuda n\u00E3o \u00E9 passo do dia de trabalho. (No
        // Gerente Geral, que carrega os quatro, a posi\u00E7\u00E3o em GEST\u00C3O segue a ordem de
        // carregamento dos m\u00F3dulos \u2014 a dedupe fica com a publica\u00E7\u00E3o do primeiro.)
        new ItemMenuModulo
        {
            Chave = ChaveAjuda, Rotulo = "Ajuda e suporte", Glifo = "\uE897", Icone = "ajuda",
            Grupo = GrupoSidebar.Gestao
        }
    ];

    public void Registrar(IServiceCollection servicos)
    {
        servicos.AddTransient<ConfirmacoesViewModel>();
        servicos.AddTransient<IFichaAdministrativaPaciente, FichaAdministrativaPaciente>();
        servicos.AddTransient<IFabricaListaPacientes, FabricaListaPacientes>();
        // A ponte agenda → novo atendimento (parcela 70): singleton de UM pedido de
        // pré-preenchimento, definido por quem navega e consumido pela tela ao abrir.
        servicos.AddSingleton<PreenchimentoNovoAtendimento>();
        servicos.AddSingleton<PedidoAgenda>();

        servicos.AddTransient<PainelViewModel>();
        servicos.AddTransient<PagamentosViewModel>();
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
        servicos.AddTransient<PrecosParticularViewModel>();
        servicos.AddTransient<ProntuarioViewModel>();
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
        ChaveConfirmacoes => new ConfirmacoesView { DataContext = servicos.GetRequiredService<ConfirmacoesViewModel>() },
        ChavePagamentos => new PagamentosView
        {
            DataContext = servicos.GetRequiredService<PagamentosViewModel>()
        },
        ChavePainel => new PainelView { DataContext = servicos.GetRequiredService<PainelViewModel>() },
        ChaveAgenda => new AgendaView { DataContext = servicos.GetRequiredService<AgendaViewModel>() },
        ChaveFila => new FilaView { DataContext = servicos.GetRequiredService<FilaViewModel>() },
        ChavePacientes => new PacientesView { DataContext = servicos.GetRequiredService<PacientesViewModel>() },
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
        ChavePrecosParticular => new PrecosParticularView
        {
            DataContext = servicos.GetRequiredService<PrecosParticularViewModel>()
        },
        ChaveProntuario => servicos.GetRequiredService<IFabricaListaPacientes>().Criar(secao: 3),
        ChaveDocumentos => new DocumentosView { DataContext = servicos.GetRequiredService<DocumentosViewModel>() },
        ChaveEquipe => new EquipeView { DataContext = servicos.GetRequiredService<EquipeViewModel>() },
        // Tela do shell, ESTÁTICA: conteúdo literal, sem ViewModel — não há o que resolver.
        ChaveAjuda => new AjudaView(),
        _ => null
    };
}
