using Clinica.Desktop.Shell.Modulos;
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
    public const string ChavePainel = "painel-recepcao";
    public const string ChaveAgenda = "agenda-recepcao";
    public const string ChaveFila = "fila";
    public const string ChavePacientes = "pacientes-recepcao";
    public const string ChaveEquipe = "equipe";

    public string Nome => "Recepção";

    public IReadOnlyList<ItemMenuModulo> Itens { get; } =
    [
        new ItemMenuModulo { Chave = ChavePainel, Rotulo = "Painel", Glifo = "\uE80F" },
        new ItemMenuModulo { Chave = ChaveAgenda, Rotulo = "Agenda", Glifo = "\uE787" },
        new ItemMenuModulo { Chave = ChaveFila, Rotulo = "Fila de hoje", Glifo = "\uE8FD" },
        new ItemMenuModulo { Chave = ChavePacientes, Rotulo = "Pacientes", Glifo = "\uE77B" },
        new ItemMenuModulo { Chave = ChaveEquipe, Rotulo = "Profissionais e salas", Glifo = "\uE716" }
    ];

    public void Registrar(IServiceCollection servicos)
    {
        servicos.AddTransient<PainelViewModel>();
        servicos.AddTransient<AgendaViewModel>();
        servicos.AddTransient<FilaViewModel>();
        servicos.AddTransient<PacientesViewModel>();
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
        ChaveEquipe => new EquipeView { DataContext = servicos.GetRequiredService<EquipeViewModel>() },
        _ => null
    };
}
