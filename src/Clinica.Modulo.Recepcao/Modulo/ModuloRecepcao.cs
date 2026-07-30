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
    public const string ChavePainel = "painel-recepcao";
    public const string ChaveAgenda = "agenda-recepcao";
    public const string ChaveFila = "fila";
    public const string ChavePacientes = "pacientes-recepcao";
    public const string ChaveProntuario = "prontuario";
    public const string ChavePrescricoes = "prescricoes";
    public const string ChaveDocumentos = ChavesSuite.Documentos;
    public const string ChaveEquipe = "equipe";

    public string Nome => "Recepção";

    // A permiss\u00E3o exigida por item entrou na parcela 5: quem n\u00E3o a tem n\u00E3o v\u00EA o item
    // na sidebar. Perfis de Recep\u00E7\u00E3o e Profissional j\u00E1 nascem com as daqui.
    public IReadOnlyList<ItemMenuModulo> Itens { get; } =
    [
        new ItemMenuModulo
        {
            Chave = ChavePainel, Rotulo = "In\u00EDcio", Glifo = "\uE80F",
            Grupo = GrupoSidebar.Gestao, Requer = Permissao.VerAgenda
        },
        new ItemMenuModulo
        {
            Chave = ChaveAgenda, Rotulo = "Agenda", Glifo = "\uE787",
            Grupo = GrupoSidebar.Gestao, Requer = Permissao.VerAgenda
        },
        new ItemMenuModulo
        {
            Chave = ChaveFila, Rotulo = "Recep\u00E7\u00E3o / Check-in", Glifo = "\uE8FD",
            Grupo = GrupoSidebar.Gestao, Requer = Permissao.VerAgenda
        },
        new ItemMenuModulo
        {
            Chave = ChavePacientes, Rotulo = "Pacientes / CRM", Glifo = "\uE77B",
            Grupo = GrupoSidebar.Paciente, Requer = Permissao.VerProntuario
        },
        // Prontu\u00E1rio e Prescri\u00E7\u00F5es s\u00E3o itens de PRIMEIRO N\u00CDVEL na proposta e, at\u00E9 aqui,
        // s\u00F3 existiam por dentro da ficha do paciente \u2014 sem entrada de menu em lugar
        // nenhum. Quem abria o app procurando "Prontu\u00E1rio" n\u00E3o achava, e concluiu com
        // raz\u00E3o que n\u00E3o tinha sido entregue.
        new ItemMenuModulo
        {
            Chave = ChaveProntuario, Rotulo = "Prontu\u00E1rio", Glifo = "\uE7C3",
            Grupo = GrupoSidebar.Paciente, Requer = Permissao.VerProntuario
        },
        new ItemMenuModulo
        {
            Chave = ChavePrescricoes, Rotulo = "Prescri\u00E7\u00F5es", Glifo = "\uE8A5",
            Grupo = GrupoSidebar.Paciente, Requer = Permissao.VerProntuario
        },
        // As nove folhas do mockup num lugar só (parcela 24). Existiam todas e nenhuma
        // estava no mesmo lugar: quatro dentro da ficha do paciente, três no botão certo
        // da aba certa dessa ficha, o recibo no Caixa, o orçamento só dentro de um pacote
        // vendido e o fechamento do período só no app de faturamento. Quem foi treinado no
        // mockup procurava "Documentos" e não achava.
        new ItemMenuModulo
        {
            Chave = ChaveDocumentos, Rotulo = "Documentos", Glifo = "\uE8B7",
            Grupo = GrupoSidebar.Paciente, Requer = Permissao.VerProntuario
        },
        // Cadastro da equipe \u00E9 gest\u00E3o da cl\u00EDnica, n\u00E3o do paciente: quem mexe aqui est\u00E1
        // organizando quem atende e onde, n\u00E3o atendendo algu\u00E9m.
        new ItemMenuModulo
        {
            Chave = ChaveEquipe, Rotulo = "Profissionais e salas", Glifo = "\uE716",
            Grupo = GrupoSidebar.Gestao, Requer = Permissao.GerenciarEquipe
        }
    ];

    public void Registrar(IServiceCollection servicos)
    {
        servicos.AddTransient<PainelViewModel>();
        servicos.AddTransient<AgendaViewModel>();
        servicos.AddTransient<DocumentosViewModel>();
        servicos.AddTransient<FilaViewModel>();
        servicos.AddTransient<PacientesViewModel>();
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
        ChaveProntuario => new ProntuarioView { DataContext = servicos.GetRequiredService<ProntuarioViewModel>() },
        ChavePrescricoes => new PrescricoesView { DataContext = servicos.GetRequiredService<PrescricoesViewModel>() },
        ChaveDocumentos => new DocumentosView { DataContext = servicos.GetRequiredService<DocumentosViewModel>() },
        ChaveEquipe => new EquipeView { DataContext = servicos.GetRequiredService<EquipeViewModel>() },
        _ => null
    };
}
