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
    public const string ChaveMeuDia = "consultorio-meu-dia";
    public const string ChaveAtendimento = "consultorio-atendimento";
    public const string ChaveEvolucaoDor = "consultorio-evolucao-dor";
    public const string ChaveAvaliacoes = "consultorio-avaliacoes";
    public const string ChaveMeusPacientes = "consultorio-pacientes";

    public string Nome => "Consultório";

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
            Chave = ChaveAtendimento, Rotulo = "Atendimento", Glifo = "\uE70F",
            Grupo = GrupoSidebar.Paciente, Requer = Permissao.VerProntuario
        },
        new ItemMenuModulo
        {
            Chave = ChaveEvolucaoDor, Rotulo = "Evolução da dor", Glifo = "\uEB05",
            Grupo = GrupoSidebar.Paciente, Requer = Permissao.VerProntuario
        },
        new ItemMenuModulo
        {
            Chave = ChaveAvaliacoes, Rotulo = "Avaliações", Glifo = "\uE9D9",
            Grupo = GrupoSidebar.Paciente, Requer = Permissao.VerProntuario
        },
        new ItemMenuModulo
        {
            Chave = ChaveMeusPacientes, Rotulo = "Meus pacientes", Glifo = "\uE77B",
            Grupo = GrupoSidebar.Paciente, Requer = Permissao.VerProntuario
        }
    ];

    public void Registrar(IServiceCollection servicos)
    {
        // O paciente do posto é SINGLETON: é o contexto do consultório, e as telas
        // clínicas o leem na abertura. Ver PacienteEmFoco.
        servicos.AddSingleton<PacienteEmFoco>();

        servicos.AddTransient<MeuDiaViewModel>();
        servicos.AddTransient<AtendimentoViewModel>();
        servicos.AddTransient<EvolucaoDorViewModel>();
        servicos.AddTransient<AvaliacoesViewModel>();
        servicos.AddTransient<MeusPacientesViewModel>();
        // AplicarAvaliacaoViewModel é construído à mão pela tela: ele recebe o paciente e
        // o instrumento escolhidos, como todo formulário da suíte.
    }

    public object? CriarTela(string chave, IServiceProvider servicos) => chave switch
    {
        ChaveMeuDia => new MeuDiaView { DataContext = servicos.GetRequiredService<MeuDiaViewModel>() },
        ChaveAtendimento => new AtendimentoView { DataContext = servicos.GetRequiredService<AtendimentoViewModel>() },
        ChaveEvolucaoDor => new EvolucaoDorView { DataContext = servicos.GetRequiredService<EvolucaoDorViewModel>() },
        ChaveAvaliacoes => new AvaliacoesView { DataContext = servicos.GetRequiredService<AvaliacoesViewModel>() },
        ChaveMeusPacientes => new MeusPacientesView { DataContext = servicos.GetRequiredService<MeusPacientesViewModel>() },
        _ => null
    };
}
