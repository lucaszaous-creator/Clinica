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
    public const string ChaveConsultas = "consultas";
    public const string ChavePacientes = ChavesSuite.PacientesRecepcao;
    public const string ChaveProntuario = "prontuario";
    public const string ChavePrescricoes = ChavesSuite.PrescricoesRecepcao;
    public const string ChaveRetorno = ChavesSuite.RetornoPacientes;

    /// <summary>
    /// A sala de infusão (parcela 48). A chave é a MESMA que o Consultório publica —
    /// é a mesma tela, e chave diferente faria a navegação da suíte abrir duas.
    /// </summary>
    // A MESMA chave que o Consultório publica — a dedupe do shell funde as duas linhas no
    // Gerente. Vem de ChavesSuite porque atravessa módulo (parcela 48).
    public const string ChaveSalaInfusao = Clinica.Desktop.Shell.Modulos.ChavesSuite.SalaInfusao;
    public const string ChaveDocumentos = ChavesSuite.Documentos;
    public const string ChaveEquipe = "equipe";

    // ===== Itens COMPOSTOS (parcela 55) =====
    public const string ChaveGrupoAgenda = "agenda";
    public const string ChaveGrupoPacientes = "pacientes";
    public const string ChaveGrupoAtendimento = "atendimento";
    public const string ChaveGrupoPrescricoes = "receituario";

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
            Chave = ChaveFila, Rotulo = "Recep\u00E7\u00E3o / Check-in", Glifo = "\uE8FD",
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
        // Cadastro da equipe \u00E9 gest\u00E3o da cl\u00EDnica, n\u00E3o do paciente: quem mexe aqui est\u00E1
        // organizando quem atende e onde, n\u00E3o atendendo algu\u00E9m.
        new ItemMenuModulo
        {
            Chave = ChaveEquipe, Rotulo = "Profissionais e salas", Glifo = "\uE716",
            Grupo = GrupoSidebar.Gestao, Requer = Permissao.GerenciarEquipe
        },

        // ===== PACIENTE =====
        // A lista do balcão e a carteira de quem atende são a mesma lista com recortes
        // diferentes — e apareciam como dois itens quase homônimos no Gerente Geral.
        new ItemMenuModulo
        {
            Chave = ChaveGrupoPacientes, Rotulo = "Pacientes", Glifo = "\uE77B",
            Grupo = GrupoSidebar.Paciente, Requer = Permissao.VerFichaPaciente,
            Abas =
            [
                new AbaMenu("Todos", ChavePacientes),
                new AbaMenu("Meus pacientes", ChavesSuite.ConsultorioPacientes)
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
            Abas =
            [
                new AbaMenu("Novo atendimento", ChaveNovoAtendimento),
                new AbaMenu("Consultas de conv\u00EAnio", ChaveConsultas)
            ]
        },
        // Prontu\u00E1rio e Prescri\u00E7\u00F5es s\u00E3o itens de PRIMEIRO N\u00CDVEL na proposta e, at\u00E9 a
        // parcela 24, s\u00F3 existiam por dentro da ficha do paciente.
        new ItemMenuModulo
        {
            Chave = ChaveProntuario, Rotulo = "Prontu\u00E1rio", Glifo = "\uE7C3",
            Grupo = GrupoSidebar.Paciente, Requer = Permissao.VerProntuario
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
            Chave = ChaveNovoAtendimento, Rotulo = "Novo atendimento", Glifo = "\uEB51",
            Grupo = GrupoSidebar.Paciente, Requer = Permissao.LancarAtendimento
        },
        new ItemMenuModulo
        {
            Chave = ChaveConsultas, Rotulo = "Consultas (conv\u00EAnio)", Glifo = "\uE8A5",
            Grupo = GrupoSidebar.Paciente, Requer = Permissao.LancarAtendimento
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
        }
    ];

    public void Registrar(IServiceCollection servicos)
    {
        servicos.AddTransient<PainelViewModel>();
        servicos.AddTransient<AgendaViewModel>();
        servicos.AddTransient<DocumentosViewModel>();
        servicos.AddTransient<FilaViewModel>();
        servicos.AddTransient<PacientesViewModel>();
        servicos.AddTransient<NovoAtendimentoViewModel>();
        servicos.AddTransient<ConsultasViewModel>();
        servicos.AddTransient<RetornoViewModel>();
        servicos.AddTransient<SalaInfusaoViewModel>();
        servicos.AddTransient<ProntuarioViewModel>();
        servicos.AddTransient<PrescricoesViewModel>();
        servicos.AddTransient<EquipeViewModel>();
        // Os ViewModels de formulário (agendamento, lista de espera, profissional,
        // sala, paciente, evolução) são construídos à mão pelas telas: cada janela abre
        // com o formulário limpo e, quando é edição, precisa receber o id no construtor.
    }

    public object? CriarTela(string chave, IServiceProvider servicos) => chave switch
    {
        ChavePainel => new PainelView { DataContext = servicos.GetRequiredService<PainelViewModel>() },
        ChaveAgenda => new AgendaView { DataContext = servicos.GetRequiredService<AgendaViewModel>() },
        ChaveFila => new FilaView { DataContext = servicos.GetRequiredService<FilaViewModel>() },
        ChavePacientes => new PacientesView { DataContext = servicos.GetRequiredService<PacientesViewModel>() },
        ChaveNovoAtendimento => new NovoAtendimentoView
        {
            DataContext = servicos.GetRequiredService<NovoAtendimentoViewModel>()
        },
        ChaveConsultas => new ConsultasView { DataContext = servicos.GetRequiredService<ConsultasViewModel>() },
        ChaveRetorno => new RetornoView { DataContext = servicos.GetRequiredService<RetornoViewModel>() },
        ChaveSalaInfusao => new SalaInfusaoView
        {
            DataContext = servicos.GetRequiredService<SalaInfusaoViewModel>()
        },
        ChaveProntuario => new ProntuarioView { DataContext = servicos.GetRequiredService<ProntuarioViewModel>() },
        ChavePrescricoes => new PrescricoesView { DataContext = servicos.GetRequiredService<PrescricoesViewModel>() },
        ChaveDocumentos => new DocumentosView { DataContext = servicos.GetRequiredService<DocumentosViewModel>() },
        ChaveEquipe => new EquipeView { DataContext = servicos.GetRequiredService<EquipeViewModel>() },
        _ => null
    };
}
