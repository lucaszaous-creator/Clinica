using Clinica.Clinico.ViewModels;
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
    public const string ChaveMeusPacientes = "consultorio-pacientes";

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
            Chave = ChaveMeusPacientes, Rotulo = "Meus pacientes", Glifo = "\uE77B",
            Grupo = GrupoSidebar.Paciente, Requer = Permissao.VerProntuario
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
        servicos.AddTransient<AtendimentoViewModel>();
        servicos.AddTransient<ProntuarioClinicoViewModel>();
        servicos.AddTransient<EvolucaoDorViewModel>();
        servicos.AddTransient<MedidasViewModel>();
        servicos.AddTransient<AvaliacoesViewModel>();
        servicos.AddTransient<MeusPacientesViewModel>();
        // AplicarAvaliacaoViewModel, AnexosSessaoViewModel e ProblemaEdicaoViewModel são
        // construídos à mão pela tela: eles recebem o paciente, a sessão ou o problema
        // escolhidos, como todo formulário da suíte.
    }

    public object? CriarTela(string chave, IServiceProvider servicos) => chave switch
    {
        ChaveMeuDia => new MeuDiaView { DataContext = servicos.GetRequiredService<MeuDiaViewModel>() },
        ChaveMeusPacientes => new MeusPacientesView { DataContext = servicos.GetRequiredService<MeusPacientesViewModel>() },

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
