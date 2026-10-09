using System.Windows;
using Clinica.Domain.Entities;

namespace Clinica.Desktop.Shell.Componentes;

public partial class EscolherSessaoEnfermagemWindow : Window
{
    public static async Task<int?> PerguntarAsync(string paciente,IReadOnlyList<Agendamento> sessoes)
    {
        var vm=new EscolherSessaoEnfermagemWebViewModel(paciente,sessoes);
        var confirmou=await DialogosDaSessao.AbrirAsync("EscolherSessaoEnfermagem",vm,()=>{
            var janela=new EscolherSessaoEnfermagemWindow(paciente,sessoes){Owner=JanelaDona.Atual()};
            var resultado=janela.ShowDialog();vm.Selecionada=vm.Opcoes.FirstOrDefault(o=>o.Id==janela.Escolhida);return resultado;
        });
        return confirmou==true?vm.Selecionada?.Id:null;
    }
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
