using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using Clinica.Application.Abstracoes;
using Clinica.Desktop.Shell.Modulos;
using Clinica.Domain.Entities;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using System.Windows.Threading;

namespace Clinica.Desktop.Shell;

/// <summary>Um pedido da enfermagem na lista do sino (out/2026).</summary>
public sealed record PedidoNoSino(int Id, string Titulo, string Quando, string Origem, bool Novo);

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
    // O NÚMERO do sino é o que é novo para quem está logado (out/2026 — antes ele somava as
    // pendências, "21 para ligar", e nunca sumia): pedidos e respostas ainda não VISTOS
    // (LeituraDoSinoShell) mais os avisos da sessão não lidos. Abrir o sino zera.
    public int TotalAvisos => AvisosNaoVistos + (Snackbar?.NaoLidos ?? 0);
    public string ContadorAvisos => ErroPedidos || ErroRespostas ? "!" : TotalAvisos.ToString();
    public bool TemAvisos => TotalAvisos > 0 || ErroPedidos || ErroRespostas;
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
        // A primeira leitura dos avisos espera a tela inicial ter os dados (AberturaDoApp): o
        // sino não disputa as conexões da abertura com o que a pessoa veio ver.
        _ = LerAvisosDepoisDaPrimeiraTelaAsync();
    }

    private async Task LerAvisosDepoisDaPrimeiraTelaAsync()
    {
        await Clinica.Application.AberturaDoApp.DepoisDaPrimeiraTelaAsync();
        if (!_avisosEmCena) return;
        // As marcas antes das listas: sem elas não se sabe o que é novo.
        await CarregarMarcasAsync();
        if (!_avisosEmCena) return;
        _relogioAvisos?.Start(); _ = AtualizarPedidosGlobaisAsync();
        IniciarAvisosConfirmacao();
        IniciarAvisosDoSemDor();
        IniciarVigiaDoWhatsApp();
    }
    public void PararAvisosGlobais()
    {
        _avisosEmCena = false; ++_geracaoPedidos; _relogioAvisos?.Stop(); _marcaPedidos = null;
        _marcasCarregadas = false; _marcas = Clinica.Application.Modelos.MarcasDoSino.Vazias;
        NotificacoesEnfermagem.Mudaram -= MudaramPedidos;
        Clinica.Infrastructure.OuvinteDeMudancas.Mudou -= MudouNoBanco;
        if (Snackbar is not null) Snackbar.PropertyChanged -= MudouSnackbar;
        PararAvisosConfirmacao();
        PararAvisosDoSemDor();
        PararVigiaDoWhatsApp();
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
        // (cada batida são três leituras, em todo posto, o dia inteiro). Pergunta pela TABELA,
        // não pela escuta: na clínica os pedidos podem não ter gatilho (outro dono), e
        // espaçar o relógio aí deixaria o pedido esperando 1 min sem aviso nenhum.
        var intervalo = Clinica.Infrastructure.OuvinteDeMudancas.Avisa("SolicitacoesEnfermagem")
            ? TimeSpan.FromMinutes(1) : TimeSpan.FromSeconds(15);
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
            var primeiraLeitura = _marcaPedidos is null;
            mudou = _marcaPedidos is { } anterior && anterior != resumo.UltimoEvento;
            _marcaPedidos = resumo.UltimoEvento;
            PedidosNovos = resumo.Novos; ErroPedidos = false; PedidosConsultados = true;
            if (resumo.Novos == 0) PedidosNovosMudaram([]);
            ResumoPedidos = $"{resumo.Novos} novos · {resumo.Abertos} em aberto · todas as datas";

            // O pedido novo vira NOTIFICAÇÃO (out/2026, pedido da clínica): a contagem no sino
            // diz que há algo, e a notificação diz O QUÊ, na hora, com o aviso passageiro e a
            // linha no histórico. As linhas só são lidas quando há novo E algo mudou (ou é a
            // primeira leitura): a batida de 15 s continua custando só a contagem.
            if (resumo.Novos > 0 && (primeiraLeitura || mudou))
            {
                var linhas = await scope.ServiceProvider.GetRequiredService<IPlanejamentoEnfermagemService>().ListarAsync(usuario);
                if (!_avisosEmCena || g != _geracaoPedidos || usuario != _sessaoClinica.UsuarioId) return;
                PedidosNovosMudaram(linhas.Where(l => l.Ativa && l.Situacao == "Novo").Select(l => l.Id));
                AvisarPedidosNovos(linhas, DateTime.Now);
            }
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
    /// <summary>Pedidos que já viraram notificação nesta sessão — cada um avisa UMA vez.</summary>
    private readonly HashSet<int> _pedidosNotificados = [];

    /// <summary>
    /// Cada pedido NOVO ainda não avisado entra nas notificações (o aviso passageiro e o
    /// histórico do sino). Uma vez por pedido: a batida relê a cada 15 s, e repetir o mesmo
    /// aviso a cada batida ensinaria a ignorá-lo. Teto de cinco por leitura: na abertura do
    /// app, uma fila acumulada não pode virar uma rajada.
    /// </summary>
    private void AvisarPedidosNovos(IReadOnlyList<LinhaSolicitacaoEnfermagem> linhas, DateTime agora)
    {
        if (Snackbar is null) return;
        var novos = linhas.Where(l => l.Ativa && l.Situacao == "Novo" && !_pedidosNotificados.Contains(l.Id))
            .OrderBy(l => l.CriadaEm ?? l.Inicio).ToList();
        foreach (var l in novos) _pedidosNotificados.Add(l.Id);
        foreach (var l in novos.TakeLast(5)) Snackbar.Info(FraseDoPedidoNovo(NoSino(l, agora)));
    }

    /// <summary>"Pedido da enfermagem · Até 1 atendimento a mais · BSV — hoje 14:00–15:00 (Joana)".</summary>
    public static string FraseDoPedidoNovo(PedidoNoSino p)
    {
        var quem = p.Origem.Split(" · ")[0].Trim();
        return $"Pedido da enfermagem · {p.Titulo} — {p.Quando}" + (quem.Length > 0 ? $" ({quem})" : "");
    }

    // ==================== A lista do sino (out/2026) ====================
    //
    // O sino virou a porta ÚNICA dos pedidos (mockup aprovado): havia o sino E um botão
    // "Pedidos da enfermagem" lado a lado no cabeçalho, dois caminhos para a mesma caixa.
    // A lista só é lida quando o sino ABRE — a batida de 15 s continua contando, e uma
    // leitura de linhas a cada batida, em todo posto, seria custo para ninguém olhar.

    public ObservableCollection<PedidoNoSino> PedidosNoSino { get; } = [];
    [ObservableProperty] private string _situacaoSino = "";
    private int _geracaoSino;

    private async Task CarregarPedidosDoSinoAsync()
    {
        if (!PodeVerPedidos) return;
        var g = ++_geracaoSino; var usuario = _sessaoClinica!.UsuarioId;
        SituacaoSino = PedidosNoSino.Count == 0 ? "Consultando pedidos…" : SituacaoSino;
        try
        {
            using var scope = _servicos.CreateScope();
            var linhas = await scope.ServiceProvider.GetRequiredService<IPlanejamentoEnfermagemService>().ListarAsync(usuario);
            if (g != _geracaoSino || usuario != _sessaoClinica.UsuarioId) return;
            PedidosNovosMudaram(linhas.Where(l => l.Ativa && l.Situacao == "Novo").Select(l => l.Id));
            var ativos = linhas.Where(l => l.Ativa)
                .OrderByDescending(l => l.Situacao == "Novo").ThenByDescending(l => l.CriadaEm ?? l.Inicio)
                .ToList();
            var novos = new List<PedidoNoSino>();
            foreach (var l in ativos.Take(5)) novos.Add(NoSino(l, DateTime.Now));
            // Entre o Clear e o último Add não há await.
            PedidosNoSino.Clear();
            foreach (var n in novos) PedidosNoSino.Add(n);
            SituacaoSino = ativos.Count == 0 ? "Nenhum pedido em aberto."
                : ativos.Count > novos.Count ? $"Mostrando {novos.Count} de {ativos.Count} em aberto." : "";
        }
        catch (Exception ex)
        {
            if (g != _geracaoSino) return;
            // Terceiro estado: lista vazia por falha não pode parecer "nenhum pedido".
            PedidosNoSino.Clear();
            SituacaoSino = "Não foi possível consultar os pedidos. Abra a caixa para tentar de novo.";
            Clinica.Application.Diagnostico.Registrar("Pedidos no sino", ex);
        }
    }

    private static readonly CultureInfo PtBrSino = CultureInfo.GetCultureInfo("pt-BR");

    /// <summary>A frase de um pedido no sino — o que, quando e quem pediu.</summary>
    public static PedidoNoSino NoSino(LinhaSolicitacaoEnfermagem l, DateTime agora)
    {
        var quantos = l.Quantidade == 1 ? "1 atendimento a mais" : $"{l.Quantidade} atendimentos a mais";
        var dia = l.Inicio.Date == agora.Date ? "hoje"
            : l.Inicio.Date == agora.Date.AddDays(1) ? "amanhã"
            : l.Inicio.ToString("ddd, dd/MM", PtBrSino);
        var chegou = l.CriadaEm is not { } c ? "" : (agora - c) switch
        {
            var d when d.TotalMinutes < 1 => " · agora",
            var d when d.TotalMinutes < 60 => $" · há {(int)d.TotalMinutes} min",
            var d when c.Date == agora.Date => $" · há {(int)d.TotalHours} h",
            _ => $" · {c:dd/MM}"
        };
        return new PedidoNoSino(l.Id, $"Até {quantos} · {l.Modalidade}",
            $"{dia} {l.Inicio:HH:mm}–{l.Fim:HH:mm}", $"{l.Autora}{chegou}", l.Situacao == "Novo");
    }

    [RelayCommand]
    private void AbrirPedidoDoSino(PedidoNoSino? pedido)
    {
        if (pedido is null || !PodeVerPedidos) return;
        LerAviso(Clinica.Application.Modelos.LeituraDoSino.ChavePedido(pedido.Id));
        NotificacoesEnfermagem.PedirAbertura(pedido.Id);
        AvisosAbertos = false;
        if (!IrPara(ChavesSuite.PedidosEnfermagem, false)) { NotificacoesEnfermagem.ConsumirAbertura(); return; }
        // A caixa já aberta relê pelo sinal; a recém-aberta lê ao entrar.
        NotificacoesEnfermagem.SinalizarMudanca();
    }

    [RelayCommand]
    private void AbrirPedidosEnfermagem()
    {
        if (!PodeVerPedidos) return;
        AvisosAbertos = false;
        IrPara(ChavesSuite.PedidosEnfermagem, false);
    }
}
