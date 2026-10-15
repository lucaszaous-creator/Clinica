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
    // Marca da última alteração lida (null = ainda não leu). Mudou → a página de pedidos relê na hora.
    private int? _marcaPedidos;
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
        Clinica.Infrastructure.OuvinteDeMudancas.Mudou += MudouNoBanco;
        if (Snackbar is not null) Snackbar.PropertyChanged += MudouSnackbar;
        // 15 s, não 30: o pedido da enfermagem é feito com a técnica esperando a resposta, e a
        // batida custa três leituras pequenas (acesso, contagem e marca). A página aberta deixa de
        // ter relógio próprio de 1 min: ela relê quando esta batida vê a marca mudar.
        _relogioAvisos ??= new DispatcherTimer { Interval = TimeSpan.FromSeconds(15) };
        _relogioAvisos.Tick -= BatidaAvisos; _relogioAvisos.Tick += BatidaAvisos;
        _relogioAvisos.Start(); _ = AtualizarPedidosGlobaisAsync();
    }
    public void PararAvisosGlobais()
    {
        _avisosEmCena = false; ++_geracaoPedidos; _relogioAvisos?.Stop(); _marcaPedidos = null;
        NotificacoesEnfermagem.Mudaram -= MudaramPedidos;
        Clinica.Infrastructure.OuvinteDeMudancas.Mudou -= MudouNoBanco;
        if (Snackbar is not null) Snackbar.PropertyChanged -= MudouSnackbar;
    }

    /// <summary>
    /// O banco avisou que um pedido mudou (out/2026): o cabeçalho relê na hora — e, se a marca
    /// mudou, a página aberta relê junto pelo sinal de sempre. Chega numa thread do Npgsql.
    /// </summary>
    private void MudouNoBanco(string tabela)
    {
        if (tabela is not ("SolicitacoesEnfermagem" or "EventosSolicitacaoEnfermagem" or "*")) return;
        System.Windows.Application.Current?.Dispatcher.BeginInvoke(() => { _ = AtualizarPedidosGlobaisAsync(); });
    }
    private void MudouSnackbar(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != "NaoLidos") return;
        OnPropertyChanged(nameof(TotalAvisos)); OnPropertyChanged(nameof(ContadorAvisos)); OnPropertyChanged(nameof(TemAvisos));
    }
    private void BatidaAvisos(object? sender, EventArgs e)
    {
        // Com o banco avisando, o relógio é só a rede para aviso perdido: 1 min em vez de 15 s
        // (cada batida são três leituras, em todo posto, o dia inteiro).
        var intervalo = Clinica.Infrastructure.OuvinteDeMudancas.Ativo ? TimeSpan.FromMinutes(1) : TimeSpan.FromSeconds(15);
        if (_relogioAvisos is { } r && r.Interval != intervalo) r.Interval = intervalo;
        _ = AtualizarPedidosGlobaisAsync();
    }
    private void MudaramPedidos() => _ = AtualizarPedidosGlobaisAsync();
    public async Task AtualizarPedidosGlobaisAsync()
    {
        if (!_avisosEmCena || _consultandoPedidos || !PodeVerPedidos) return;
        var g = _geracaoPedidos; var usuario = _sessaoClinica!.UsuarioId; _consultandoPedidos = true;
        var mudou = false;
        try
        {
            using var scope = _servicos.CreateScope();
            var resumo = await scope.ServiceProvider.GetRequiredService<IPlanejamentoEnfermagemService>().ResumoAsync(usuario);
            if (!_avisosEmCena || g != _geracaoPedidos || usuario != _sessaoClinica.UsuarioId) return;
            mudou = _marcaPedidos is { } anterior && anterior != resumo.UltimoEvento;
            _marcaPedidos = resumo.UltimoEvento;
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
        // Fora do try: o próprio cabeçalho escuta o sinal, e a releitura dele acha a marca igual.
        if (mudou) NotificacoesEnfermagem.SinalizarMudanca();
    }
    [RelayCommand]
    private void AbrirPedidosEnfermagem()
    {
        if (!PodeVerPedidos) return;
        AvisosAbertos = false;
        IrPara(ChavesSuite.PedidosEnfermagem, false);
    }
}
