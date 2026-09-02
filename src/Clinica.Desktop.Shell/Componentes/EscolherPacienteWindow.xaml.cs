using System.Windows;
using System.Windows.Input;
using Clinica.Domain.Entities;
using Microsoft.Extensions.DependencyInjection;

namespace Clinica.Desktop.Shell.Componentes;

/// <summary>
/// Diálogo de escolher UM paciente, para atos que nascem em tela sem paciente em foco.
/// Devolve a escolha por <see cref="Perguntar"/>; nulo = desistiu. A busca é o
/// <see cref="SeletorPacienteViewModel"/> de sempre — esta janela não reescreve nada.
/// </summary>
public partial class EscolherPacienteWindow : Window
{
    private readonly SeletorPacienteViewModel _seletor;

    /// <summary>O paciente escolhido — só vale quando o diálogo devolve true.</summary>
    public Paciente? Escolhido { get; private set; }

    public EscolherPacienteWindow(string assunto, IServiceScopeFactory escopos)
    {
        InitializeComponent();
        TituloTexto.Text = assunto;

        _seletor = new SeletorPacienteViewModel(escopos);
        DataContext = _seletor;

        // O botão acende com a escolha — botão aceso sem paciente é o clique que não
        // faz nada (parcela 41).
        _seletor.SelecaoMudou += p => BtnEscolher.IsEnabled = p is not null;

        // Abre com os primeiros da carteira, não com uma caixa vazia (a lição das telas
        // clínicas da parcela 37): quem procura já vê gente para clicar.
        Loaded += (_, _) =>
        {
            CampoBusca.Focus();
            _ = _seletor.BuscarAsync(imediato: true);
        };
    }

    private void Escolher_Click(object sender, RoutedEventArgs e) => Concluir();

    private void Lista_MouseDoubleClick(object sender, MouseButtonEventArgs e) => Concluir();

    private void Concluir()
    {
        if (_seletor.Selecionado is not { } paciente) return;
        Escolhido = paciente;
        DialogResult = true;
    }

    /// <summary>Pergunta e devolve o paciente escolhido; nulo quando a pessoa desiste.</summary>
    public static Paciente? Perguntar(string assunto, Window? dono, IServiceScopeFactory escopos)
    {
        var janela = new EscolherPacienteWindow(assunto, escopos) { Owner = dono };
        return janela.ShowDialog() == true ? janela.Escolhido : null;
    }
}
