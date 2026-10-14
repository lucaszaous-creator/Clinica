using System.ComponentModel;
using Clinica.Application.Abstracoes;
using Clinica.Desktop.Shell.Modulos;
using Clinica.Domain.Entities;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using System.Windows.Threading;

namespace Clinica.Desktop.Shell;

public sealed partial class ShellViewModel
{
    private DispatcherTimer? _relogioAvisos;
    private bool _avisosEmCena, _consultandoPedidos;
    private int _geracaoPedidos;
    [ObservableProperty, NotifyPropertyChangedFor(nameof(ContadorPedidos))]
    private bool _pedidosConsultados;
    [ObservableProperty, NotifyPropertyChangedFor(nameof(TotalAvisos), nameof(ContadorAvisos), nameof(TemAvisos), nameof(ContadorPedidos))]
    private int _pedidosNovos;
    [ObservableProperty] private string _resumoPedidos = "Consultando pedidos…";
    [ObservableProperty, NotifyPropertyChangedFor(nameof(ContadorAvisos), nameof(TemAvisos), nameof(ContadorPedidos))]
    private bool _erroPedidos;
    public bool PodeVerPedidos => Itens.Any(i => i.Chave == ChavesSuite.PedidosEnfermagem)
        && _sessaoClinica is { Autenticado: true, Perfil: PerfilAcesso.Recepcao or PerfilAcesso.Gerente }
        && _sessaoClinica.Pode(Permissao.VerAgenda | Permissao.EditarAgenda | Permissao.VerFichaPaciente);
    public int TotalAvisos => PedidosNovos + (Snackbar?.NaoLidos ?? 0);
    public string ContadorAvisos => ErroPedidos ? "!" : TotalAvisos.ToString();
    public bool TemAvisos => TotalAvisos > 0 || ErroPedidos;
    public string ContadorPedidos => ErroPedidos ? "!" : !PedidosConsultados ? "…" : $"{PedidosNovos} {(PedidosNovos == 1 ? "novo" : "novos")}";

    // O ciclo pertence à janela inteira: trocar de página não interrompe os pedidos.
    public void IniciarAvisosGlobais()
    {
        if (_avisosEmCena) return;
        _avisosEmCena = true;
        NotificacoesEnfermagem.Mudaram += MudaramPedidos;
        if (Snackbar is not null) Snackbar.PropertyChanged += MudouSnackbar;
        _relogioAvisos ??= new DispatcherTimer { Interval = TimeSpan.FromSeconds(30) };
        _relogioAvisos.Tick -= BatidaAvisos; _relogioAvisos.Tick += BatidaAvisos;
        _relogioAvisos.Start(); _ = AtualizarPedidosGlobaisAsync();
    }
    public void PararAvisosGlobais()
    {
        _avisosEmCena = false; ++_geracaoPedidos; _relogioAvisos?.Stop();
        NotificacoesEnfermagem.Mudaram -= MudaramPedidos;
        if (Snackbar is not null) Snackbar.PropertyChanged -= MudouSnackbar;
    }
    private void MudouSnackbar(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != "NaoLidos") return;
        OnPropertyChanged(nameof(TotalAvisos)); OnPropertyChanged(nameof(ContadorAvisos)); OnPropertyChanged(nameof(TemAvisos));
    }
    private void BatidaAvisos(object? sender, EventArgs e) => _ = AtualizarPedidosGlobaisAsync();
    private void MudaramPedidos() => _ = AtualizarPedidosGlobaisAsync();
    public async Task AtualizarPedidosGlobaisAsync()
    {
        if (!_avisosEmCena || _consultandoPedidos || !PodeVerPedidos) return;
        var g = _geracaoPedidos; var usuario = _sessaoClinica!.UsuarioId; _consultandoPedidos = true;
        try
        {
            using var scope = _servicos.CreateScope();
            var resumo = await scope.ServiceProvider.GetRequiredService<IPlanejamentoEnfermagemService>().ContarAsync(usuario);
            if (!_avisosEmCena || g != _geracaoPedidos || usuario != _sessaoClinica.UsuarioId) return;
            PedidosNovos = resumo.Novos; ErroPedidos = false; PedidosConsultados = true;
            ResumoPedidos = $"{resumo.Novos} novos · {resumo.Abertos} em aberto · todas as datas";
        }
        catch (Exception ex)
        {
            if (_avisosEmCena && g == _geracaoPedidos)
            {
                ErroPedidos = true; ResumoPedidos = "Não foi possível atualizar. Abra os pedidos para tentar novamente.";
                Clinica.Application.Diagnostico.Registrar("Pedidos no cabeçalho", ex);
            }
        }
        finally { _consultandoPedidos = false; }
    }
    [RelayCommand]
    private void AbrirPedidosEnfermagem()
    {
        if (!PodeVerPedidos) return;
        AvisosAbertos = false;
        IrPara(ChavesSuite.PedidosEnfermagem, false);
    }
}
