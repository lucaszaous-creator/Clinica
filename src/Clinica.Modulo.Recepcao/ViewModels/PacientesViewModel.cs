using Clinica.Application.Servicos;
using Clinica.Desktop.Controls;
using Clinica.Desktop.Shell.Componentes;
using Clinica.Domain.Entities;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;

namespace Clinica.Recepcao.ViewModels;

/// <summary>
/// Pacientes na Recepção: lista à esquerda, ficha 360º à direita.
///
/// É master-detail numa tela só de propósito — o balcão trabalha com o paciente na
/// frente, e obrigar a navegar para outra seção para ver a ficha custaria um clique e
/// o contexto a cada atendimento.
///
/// A busca NÃO é reescrita aqui: usa o <see cref="SeletorPacienteViewModel"/> da suíte,
/// que já resolve limite no SQL, agrupamento das teclas e descarte de resposta fora de
/// ordem. Como esta é a tela de LISTAGEM, ela o usa com <c>limite: null</c>.
/// </summary>
public sealed partial class PacientesViewModel : ObservableObject
{
    private readonly IServiceScopeFactory _escopos;
    private readonly ISnackbarService _snackbar;

    public SeletorPacienteViewModel Seletor { get; }

    public FichaPacienteViewModel Ficha { get; }

    [ObservableProperty] private string _resumo = string.Empty;

    public PacientesViewModel(
        IServiceScopeFactory escopos, ISnackbarService snackbar, IDialogoService dialogo)
    {
        _escopos = escopos;
        _snackbar = snackbar;

        // Listagem: sem corte, é ela que precisa mostrar todo mundo.
        Seletor = new SeletorPacienteViewModel(escopos, limite: null);
        Seletor.SelecaoMudou += AoTrocarPaciente;
        Seletor.Atualizou += AtualizarResumo;

        Ficha = new FichaPacienteViewModel(escopos, snackbar, dialogo);
        // Editar o cadastro muda o nome/foto que a lista mostra.
        Ficha.Alterou += () => _ = Seletor.BuscarAsync(imediato: true);

        _ = Seletor.BuscarAsync(imediato: true);
    }

    private void AoTrocarPaciente(Paciente? paciente)
        => _ = Ficha.AbrirAsync(paciente?.Id);

    private void AtualizarResumo()
        => Resumo = Seletor.Resultados.Count switch
        {
            0 => "Nenhum paciente encontrado.",
            1 => "1 paciente.",
            var n => $"{n} pacientes."
        };

    [RelayCommand]
    private async Task NovoPacienteAsync()
    {
        var vm = new PacienteEdicaoViewModel(_escopos);
        var janela = new Janelas.PacienteWindow(vm)
        {
            Owner = System.Windows.Application.Current?.MainWindow
        };

        if (janela.ShowDialog() != true) return;

        _snackbar.Sucesso("Paciente cadastrado.");
        await Seletor.BuscarAsync(imediato: true);
    }

    [RelayCommand]
    private Task AtualizarAsync() => Seletor.BuscarAsync(imediato: true);
}
