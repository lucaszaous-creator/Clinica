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
    public const string ChaveProntuario = "consultorio-prontuario";
    public const string ChaveEvolucaoDor = "consultorio-evolucao-dor";
    public const string ChaveMedidas = "consultorio-medidas";
    public const string ChaveAvaliacoes = "consultorio-avaliacoes";
    public const string ChaveMeusPacientes = ChavesSuite.ConsultorioPacientes;

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
    /// A tela do PACIENTE — identidade no topo, as cinco seções em abas.
    ///
    /// As cinco chaves clínicas acima continuam válidas e caem todas aqui, cada uma na
    /// sua aba (ver <see cref="AbaDe"/>): a fila do dia e o painel da direção navegam por
    /// elas, e renomear contrato de navegação para arrumar leiaute quebraria o que
    /// funciona noutro módulo.
    /// </summary>
    public const string ChavePaciente = "consultorio-paciente";

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
            Chave = ChaveMeusPacientes, Rotulo = "Meus pacientes", Glifo = "\uE77B",
            Grupo = GrupoSidebar.Paciente, Requer = Permissao.VerProntuario
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
        // A tela do paciente e as cinco chaves clínicas continuam sendo DESTINO: é por
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
        }
    ];

    /// <summary>Em que aba da tela do paciente cada chave clínica cai.</summary>
    private static int AbaDe(string chave) => chave switch
    {
        ChaveProntuario => 1,
        ChaveEvolucaoDor => 2,
        ChaveMedidas => 3,
        ChaveAvaliacoes => 4,
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
        servicos.AddTransient<ProntuarioClinicoViewModel>();
        servicos.AddTransient<EvolucaoDorViewModel>();
        servicos.AddTransient<MedidasViewModel>();
        servicos.AddTransient<AvaliacoesViewModel>();
        servicos.AddTransient<MeusPacientesViewModel>();
        // AplicarAvaliacaoViewModel, AnexosSessaoViewModel, ProblemaEdicaoViewModel,
        // PrescricaoInternaEdicaoViewModel, FolhaExecucaoViewModel e
        // EscolherCertificadoViewModel são construídos à mão pela tela: eles recebem o
        // paciente, a sessão, o problema ou a prescrição escolhidos, como todo formulário
        // da suíte.
    }

    public object? CriarTela(string chave, IServiceProvider servicos) => chave switch
    {
        ChaveMeuDia => new MeuDiaView { DataContext = servicos.GetRequiredService<MeuDiaViewModel>() },
        ChaveMeusPacientes => new MeusPacientesView { DataContext = servicos.GetRequiredService<MeusPacientesViewModel>() },
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

        // A tela do paciente, e as cinco chaves clínicas que caem nela.
        ChavePaciente or ChaveAtendimento or ChaveProntuario
            or ChaveEvolucaoDor or ChaveMedidas or ChaveAvaliacoes
            => new PacienteWorkspaceView
            {
                DataContext = new PacienteWorkspaceViewModel(
                    servicos, servicos.GetRequiredService<PacienteEmFoco>(), AbaDe(chave))
            },

        _ => null
    };
}
