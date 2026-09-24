using System.Windows;
using System.Windows.Input;
using System.Windows.Controls;
using System.Windows.Data;
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
    private bool _abriu;
    public event Action? Alterou;
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
        _ficha.Alterou += () => Alterou?.Invoke();
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
        var mensagem = new TextBlock { TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 6, 0, 6) };
        mensagem.SetResourceReference(TextBlock.ForegroundProperty, "Brush.Erro");
        mensagem.SetBinding(TextBlock.TextProperty, new Binding(nameof(FichaPacienteViewModel.Mensagem)));
        mensagem.SetBinding(UIElement.VisibilityProperty, new Binding(nameof(FichaPacienteViewModel.Mensagem))
            { Converter = new TextoParaVisibilidade() });
        DockPanel.SetDock(mensagem, Dock.Top);
        var painel = new DockPanel { DataContext = _ficha };
        painel.Children.Add(mensagem);
        painel.Children.Add(view);
        return painel;
    }

    public void DefinirPaciente(int pacienteId)
    {
        if (_pacienteId != 0 && _pacienteId != pacienteId)
            throw new InvalidOperationException("Abra uma nova ficha para consultar outro paciente.");
        _pacienteId = pacienteId;
    }

    private Task CarregarAsync()
    {
        if (_carga is null || (_carga.IsCompleted && _ficha.MensagemEhErro))
            _carga = CarregarFichaAsync();
        return _carga;
    }

    public async Task AtualizarAsync()
    {
        if (_carga is { IsCompleted: false }) await _carga;
        _carga = CarregarFichaAsync();
        await _carga;
    }

    private async Task CarregarFichaAsync()
    {
        try
        {
            if (_abriu) await _ficha.CarregarAsync();
            else
            {
                await _ficha.AbrirAsync(_pacienteId);
                _abriu = true;
            }
        }
        catch (Exception ex)
        {
            // Inclui a auditoria de abertura, anterior à carga da ficha. O evento
            // Loaded não pode propagar uma falha de rede ao Dispatcher do aplicativo.
            Clinica.Application.Diagnostico.Registrar("Abertura da ficha administrativa", ex);
            _ficha.Mensagem = "Não foi possível carregar esta seção. Tente Atualizar ficha.";
            _ficha.MensagemEhErro = true;
        }
    }
}
