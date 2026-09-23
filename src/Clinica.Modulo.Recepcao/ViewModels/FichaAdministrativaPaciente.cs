using System.Windows;
using System.Windows.Input;
using Clinica.Desktop.Controls;
using Clinica.Desktop.Shell.Modulos;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;

namespace Clinica.Recepcao.ViewModels;

/// <summary>As funções do balcão dentro da ficha única. Carrega somente ao consultar uma seção.</summary>
public sealed class FichaAdministrativaPaciente : IFichaAdministrativaPaciente
{
    private readonly FichaPacienteViewModel _ficha;
    private int _pacienteId;
    private Task? _carga;
    public bool PodeEditar => Clinica.Domain.Entities.SessaoUsuario.Atual.Pode(Clinica.Domain.Entities.Permissao.EditarPaciente);
    public object Resumo { get; }
    public object Convenio { get; }
    public object Relacionamento { get; }
    public object Privacidade { get; }
    public object Termos { get; }
    public ICommand EditarCommand { get; }
    public ICommand WhatsAppCommand { get; }

    public FichaAdministrativaPaciente(IServiceScopeFactory escopos, ISnackbarService snackbar, IDialogoService dialogo)
    {
        _ficha = new FichaPacienteViewModel(escopos, snackbar, dialogo) { SomenteAdministrativo = true };
        Resumo = Preparar(new Views.ResumoAdministrativoPacienteView());
        Convenio = Preparar(new Views.ConvenioPacienteView());
        Relacionamento = Preparar(new Views.RelacionamentoPacienteView());
        Privacidade = Preparar(new Views.PrivacidadePacienteView());
        Termos = Preparar(new Views.TermosPacienteView());
        EditarCommand = new AsyncRelayCommand(async () => { await CarregarAsync(); await _ficha.EditarCommand.ExecuteAsync(null); }, () => PodeEditar);
        WhatsAppCommand = new AsyncRelayCommand(async () => { await CarregarAsync(); _ficha.AbrirWhatsappCommand.Execute(null); });
    }

    private FrameworkElement Preparar(FrameworkElement view)
    {
        view.DataContext = _ficha;
        view.Loaded += async (_, _) => await CarregarAsync();
        return view;
    }

    public void DefinirPaciente(int pacienteId)
    {
        if (_pacienteId != 0 && _pacienteId != pacienteId)
            throw new InvalidOperationException("Abra uma nova ficha para consultar outro paciente.");
        _pacienteId = pacienteId;
    }

    private Task CarregarAsync() => _carga ??= _ficha.AbrirAsync(_pacienteId);
}
