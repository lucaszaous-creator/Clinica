using System.Windows;
using Clinica.Domain.Entities;

namespace Clinica.Desktop.Shell.Componentes;

public partial class EscolherSessaoEnfermagemWindow : Window
{
    private sealed record Opcao(int Id, string Rotulo);
    public int? Escolhida { get; private set; }

    public EscolherSessaoEnfermagemWindow(string paciente, IReadOnlyList<Agendamento> sessoes)
    {
        InitializeComponent();
        Paciente.Text = paciente;
        Sessoes.ItemsSource = sessoes.Where(a => a.Status is StatusAgendamento.Agendado or StatusAgendamento.Realizado)
            .OrderBy(a => a.DataHora).Select(a => new Opcao(a.Id,
                $"{a.DataHora:dd/MM/yyyy HH:mm} · sessão #{a.Id} · {a.Profissional?.Rotulo ?? "Responsável não informado"} · "
                + (a.FimAtendimentoEm is not null ? "Concluída" : "Em aberto"))).ToList();
    }

    private void Confirmar(object sender, RoutedEventArgs e)
    {
        if (Sessoes.SelectedItem is not Opcao opcao) return;
        Escolhida = opcao.Id;
        DialogResult = true;
    }
}
