using System.Collections.Specialized;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Data;
using System.Windows.Threading;
using Clinica.Desktop.Controls;
using Clinica.Desktop.Shell.Modulos;
using Clinica.Domain.Entities;
using Clinica.Financeiro.Modulo;
using Clinica.Financeiro.ViewModels;
using Clinica.Financeiro.Views;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;

namespace Clinica.Financeiro.Web;

/// <summary>
/// Interface local do Caixa. Somente mensagens explícitas alcançam os comandos existentes;
/// HTML não recebe objetos do host, credenciais, conexão de banco nem acesso a serviços.
/// </summary>
public sealed class FinanceiroWebView : UserControl, IDisposable
{
    public const string Origem = "https://financeiro.clinica.local/";
    private const string HostVirtual = "financeiro.clinica.local";
    private readonly IServiceProvider _services;
    private readonly Action<string>? _navegarNativo;
    private readonly ModuloFinanceiro _modulo = new();
    private readonly CaixaViewModel _caixa;
    private readonly SnackbarService? _snackbar;
    private readonly WebView2 _browser = new();
    private readonly DispatcherTimer _envio;
    private readonly TaskCompletionSource _pronto = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private bool _iniciou;
    private bool _paginaPronta;
    private bool _executando;
    private bool _descartado;
    private bool _falhou;
    private int _envioPendente;
    private DateTime _janelaMensagens = DateTime.UtcNow;
    private int _mensagensNaJanela;
    private string? _erro;

    public CaixaViewModel Caixa => _caixa;
    public Task QuandoPronto => _pronto.Task;

    public FinanceiroWebView(IServiceProvider services, Action<string>? navegarNativo = null)
    {
        ExigirSessao();
        _services = services;
        _navegarNativo = navegarNativo;
        _caixa = services.GetRequiredService<CaixaViewModel>();
        _snackbar = services.GetService<SnackbarService>();
        if (_snackbar is not null) _snackbar.PropertyChanged += AoMudarEstado;
        _envio = new DispatcherTimer(DispatcherPriority.Background, Dispatcher)
        {
            Interval = TimeSpan.FromMilliseconds(100)
        };
        _envio.Tick += AoEnviarPendente;
        _caixa.PropertyChanged += AoMudarEstado;
        _caixa.Linhas.CollectionChanged += AoMudarLinhas;
        Content = _browser;
        SetResourceReference(BackgroundProperty, "Brush.Superficie");
        Loaded += AoCarregar;
        Unloaded += AoDescarregar;
    }

    private static void ExigirSessao()
    {
        if (!SessaoUsuario.Atual.Autenticado)
            throw new InvalidOperationException("Entre no sistema para abrir o Financeiro.");
        SessaoUsuario.Atual.Exigir(Permissao.VerFinanceiro, "abrir o Financeiro");
    }

    private async void AoCarregar(object sender, RoutedEventArgs e)
    {
        if (_iniciou || _descartado) return;
        _iniciou = true;
        try
        {
            var arquivos = Path.Combine(AppContext.BaseDirectory, "Web", "wwwroot");
            if (!File.Exists(Path.Combine(arquivos, "index.html")))
                throw new FileNotFoundException("Os arquivos locais da interface financeira não foram encontrados.");
            var perfil = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "ClinicaSemDor", "Financeiro", "WebView2");
            Directory.CreateDirectory(perfil);
            var ambiente = await CoreWebView2Environment.CreateAsync(userDataFolder: perfil);
            if (_descartado) return;
            await _browser.EnsureCoreWebView2Async(ambiente);
            if (_descartado) return;
            var core = _browser.CoreWebView2;
            core.Settings.AreHostObjectsAllowed = false;
            core.Settings.AreDevToolsEnabled = false;
            core.Settings.AreDefaultContextMenusEnabled = false;
            core.Settings.AreDefaultScriptDialogsEnabled = false;
            core.Settings.AreBrowserAcceleratorKeysEnabled = false;
            core.Settings.IsStatusBarEnabled = false;
            core.Settings.IsPasswordAutosaveEnabled = false;
            core.Settings.IsGeneralAutofillEnabled = false;
            core.Settings.IsWebMessageEnabled = true;
            core.SetVirtualHostNameToFolderMapping(HostVirtual, arquivos, CoreWebView2HostResourceAccessKind.Deny);
            core.NavigationStarting += AoNavegar;
            core.FrameNavigationStarting += BloquearFrame;
            core.NewWindowRequested += BloquearPopup;
            core.PermissionRequested += BloquearPermissao;
            core.DownloadStarting += BloquearDownload;
            core.NavigationCompleted += AoConcluirNavegacao;
            core.WebMessageReceived += AoReceberMensagem;
            core.ProcessFailed += AoFalharProcesso;
            core.AddWebResourceRequestedFilter("*", CoreWebView2WebResourceContext.All);
            core.WebResourceRequested += AoSolicitarRecurso;
            core.Navigate(Origem + "index.html");
        }
        catch (Exception ex)
        {
            if (!_descartado) MostrarFalha(ex);
        }
    }

    private static bool OrigemPermitida(string? endereco)
        => Uri.TryCreate(endereco, UriKind.Absolute, out var uri)
            && uri.Scheme == Uri.UriSchemeHttps && uri.Host.Equals(HostVirtual, StringComparison.OrdinalIgnoreCase)
            && uri.Port == 443 && string.IsNullOrEmpty(uri.UserInfo);

    private static bool DocumentoPermitido(string? endereco)
        => OrigemPermitida(endereco) && Uri.TryCreate(endereco, UriKind.Absolute, out var uri)
            && (uri.AbsolutePath == "/" || uri.AbsolutePath == "/index.html") && string.IsNullOrEmpty(uri.Query);

    private void AoNavegar(object? sender, CoreWebView2NavigationStartingEventArgs e)
    {
        e.Cancel = !DocumentoPermitido(e.Uri);
        if (!e.Cancel) _paginaPronta = false;
    }
    private static void BloquearFrame(object? sender, CoreWebView2NavigationStartingEventArgs e) => e.Cancel = true;
    private static void BloquearPopup(object? sender, CoreWebView2NewWindowRequestedEventArgs e) => e.Handled = true;
    private static void BloquearPermissao(object? sender, CoreWebView2PermissionRequestedEventArgs e)
    {
        e.State = CoreWebView2PermissionState.Deny;
        e.Handled = true;
    }
    private static void BloquearDownload(object? sender, CoreWebView2DownloadStartingEventArgs e) => e.Cancel = true;
    private void AoSolicitarRecurso(object? sender, CoreWebView2WebResourceRequestedEventArgs e)
    {
        if (_descartado || OrigemPermitida(e.Request.Uri)) return;
        e.Response = _browser.CoreWebView2.Environment.CreateWebResourceResponse(
            Stream.Null, 403, "Recurso externo bloqueado", "Content-Type: text/plain; charset=utf-8");
    }
    private void AoConcluirNavegacao(object? sender, CoreWebView2NavigationCompletedEventArgs e)
    {
        if (!_descartado && !e.IsSuccess && e.WebErrorStatus != CoreWebView2WebErrorStatus.OperationCanceled)
            MostrarFalha(new InvalidOperationException("Não foi possível abrir a interface local do Financeiro."));
    }
    private void AoFalharProcesso(object? sender, CoreWebView2ProcessFailedEventArgs e)
    {
        if (!_descartado) MostrarFalha(new InvalidOperationException("O componente visual foi interrompido."));
    }

    private async void AoReceberMensagem(object? sender, CoreWebView2WebMessageReceivedEventArgs e)
    {
        if (_descartado || _falhou || !DocumentoPermitido(e.Source) ||
            !DocumentoPermitido(_browser.CoreWebView2.Source)) return;
        try
        {
            if (DateTime.UtcNow - _janelaMensagens > TimeSpan.FromSeconds(1))
            { _janelaMensagens = DateTime.UtcNow; _mensagensNaJanela = 0; }
            if (++_mensagensNaJanela > 30) return;
            var json = e.WebMessageAsJson;
            if (json.Length > 4096) return;
            var mensagem = JsonSerializer.Deserialize<Mensagem>(json);
            if (mensagem?.Acao is null || mensagem.Acao.Length > 20 ||
                mensagem.Valor?.Length > 160 || mensagem.Id?.Length > 12) return;
            ExigirSessao();
            if (mensagem.Acao == "pronto")
            {
                _paginaPronta = true;
                EnviarEstado();
                _pronto.TrySetResult();
                return;
            }
            // Ações que abrem diálogos ficam fora da chamada WebView2: um ShowDialog
            // dentro do callback Chromium criaria um loop de mensagens reentrante.
            await System.Windows.Threading.Dispatcher.Yield(DispatcherPriority.Background);
            if (_descartado || _falhou || !_paginaPronta || _executando ||
                !DocumentoPermitido(_browser.CoreWebView2.Source)) return;
            ExigirSessao();
            _erro = null;
            switch (mensagem.Acao)
            {
                case "mes":
                    if (DateTime.TryParseExact(mensagem.Valor, "yyyy-MM", CultureInfo.InvariantCulture,
                        DateTimeStyles.None, out var mes) && mes.Year is >= 1900 and <= 2200)
                        _caixa.Mes = new DateTime(mes.Year, mes.Month, 1);
                    break;
                case "filtrar":
                    _caixa.FiltroTexto = (mensagem.Valor ?? string.Empty)[..Math.Min(80, mensagem.Valor?.Length ?? 0)];
                    break;
                case "navegar": Navegar(mensagem.Valor); break;
                case "sistema": Navegar(ModuloFinanceiro.ChaveCaixa, sistema: true); break;
                case "atualizar": await ExecutarComando(_caixa.CarregarCommand); break;
                case "novo":
                    ExigirEdicao(); await ExecutarComando(_caixa.NovoLancamentoCommand); break;
                case "pix":
                    await ExecutarComando(_caixa.CobrarPixCommand,
                        mensagem.Id is null ? null : ResolverLinha(mensagem.Id)); break;
                case "exportar": await ExecutarComando(_caixa.ExportarCommand); break;
                case "historico": await ExecutarComando(_caixa.HistoricoCommand, ResolverLinha(mensagem.Id)); break;
                case "realizar":
                    ExigirEdicao();
                    var realizar = ResolverLinha(mensagem.Id);
                    if (realizar.PodeRealizar) await ExecutarComando(_caixa.RealizarCommand, realizar);
                    break;
                case "recibo":
                    ExigirEdicao();
                    var recibo = ResolverLinha(mensagem.Id);
                    if (recibo.EhEntrada) await ExecutarComando(_caixa.EmitirReciboCommand, recibo);
                    break;
                case "cancelar":
                    ExigirEdicao();
                    var cancelar = ResolverLinha(mensagem.Id);
                    if (cancelar.PodeCancelar) await ExecutarComando(_caixa.CancelarCommand, cancelar);
                    break;
                default: return;
            }
            AgendarEstado();
        }
        catch (JsonException) { /* Mensagem fora do contrato não executa ação. */ }
        catch (Exception ex)
        {
            if (!SessaoUsuario.Atual.Autenticado || !SessaoUsuario.Atual.Pode(Permissao.VerFinanceiro))
            { MostrarAcessoNegado(); return; }
            Clinica.Application.Diagnostico.Registrar("Financeiro web — ação não concluída", ex);
            _erro = "Não foi possível concluir a ação. Confira seu acesso e atualize os lançamentos.";
            AgendarEstado();
        }
    }

    private void ExigirEdicao()
    {
        ExigirSessao();
        SessaoUsuario.Atual.Exigir(Permissao.EditarFinanceiro, "alterar lançamentos financeiros");
    }
    private LinhaCaixa ResolverLinha(string? id)
    {
        if (_caixa.Carregando || !int.TryParse(id, NumberStyles.None, CultureInfo.InvariantCulture, out var numero))
            throw new InvalidOperationException("Atualize a lista antes de escolher o lançamento.");
        return _caixa.Linhas.FirstOrDefault(l => l.Id == numero)
            ?? throw new InvalidOperationException("O lançamento não está na lista atual.");
    }
    private async Task ExecutarComando(ICommand comando, object? parametro = null)
    {
        if (_executando || _caixa.Carregando || !comando.CanExecute(parametro)) return;
        _executando = true;
        AgendarEstado();
        try
        {
            if (comando is IAsyncRelayCommand assincrono) await assincrono.ExecuteAsync(parametro);
            else comando.Execute(parametro);
        }
        finally { _executando = false; AgendarEstado(); }
    }

    private static bool PodeVerMenu(ItemMenuModulo item)
    {
        var sessao = SessaoUsuario.Atual;
        return sessao.Autenticado && sessao.Pode(item.Requer)
            && (item.RequerAlgum == Permissao.Nenhuma || sessao.PodeAlgum(item.RequerAlgum))
            && (item.PerfilExclusivo is null || sessao.Perfil == item.PerfilExclusivo);
    }
    private IEnumerable<ItemMenuModulo> RotasPermitidas()
        => _modulo.Itens.Select(OrganizacaoNavegacao.Aplicar).Where(i => i.Abas.Count == 0 && !i.Oculto
            && i.Chave != ModuloFinanceiro.ChaveAjuda && PodeVerMenu(i));

    private void Navegar(string? chave, bool sistema = false)
    {
        ExigirSessao();
        var item = RotasPermitidas().FirstOrDefault(i => i.Chave == chave)
            ?? throw new InvalidOperationException("Esta tela não está disponível no Financeiro.");
        if (_navegarNativo is not null) { _navegarNativo(item.Chave); return; }
        object? tela = item.Chave == ModuloFinanceiro.ChaveCaixa
            ? new CaixaView { DataContext = _caixa } : _modulo.CriarTela(item.Chave, _services);
        if (tela is null) return;
        var area = SystemParameters.WorkArea;
        var janela = new Window
        {
            Title = item.Rotulo + " — Clínica SemDor", Content = tela, Owner = Window.GetWindow(this),
            Width = Math.Min(1180, area.Width), Height = Math.Min(760, area.Height),
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            UseLayoutRounding = true, SnapsToDevicePixels = true
        };
        janela.Resources.MergedDictionaries.Add(new ResourceDictionary
        { Source = new Uri("/Clinica.Modulo.Financeiro;component/Styles/Financeiro.xaml", UriKind.Relative) });
        janela.ShowDialog();
    }

    private void AoMudarEstado(object? sender, PropertyChangedEventArgs e) => AgendarEstado();
    private void AoMudarLinhas(object? sender, NotifyCollectionChangedEventArgs e) => AgendarEstado();
    private void AgendarEstado()
    {
        if (_descartado || Interlocked.Exchange(ref _envioPendente, 1) != 0) return;
        Dispatcher.BeginInvoke(() => { if (!_descartado) _envio.Start(); });
    }
    private void AoEnviarPendente(object? sender, EventArgs e)
    {
        _envio.Stop();
        Interlocked.Exchange(ref _envioPendente, 0);
        EnviarEstado();
    }
    private void EnviarEstado()
    {
        if (_descartado || _falhou || !_paginaPronta ||
            !DocumentoPermitido(_browser.CoreWebView2.Source)) return;
        if (!SessaoUsuario.Atual.Autenticado || !SessaoUsuario.Atual.Pode(Permissao.VerFinanceiro))
        { MostrarAcessoNegado(); return; }
        try
        {
            var estado = new
            {
                tipo = "estado", mes = _caixa.Mes.ToString("yyyy-MM", CultureInfo.InvariantCulture),
                entradas = _caixa.Entradas, saidas = _caixa.Saidas, saldo = _caixa.Saldo,
                previsto = _caixa.Previsto, liquido = _caixa.Liquido, deducoes = _caixa.Deducoes,
                ultimoMovimento = _caixa.UltimoMovimento, detalheUltimoMovimento = _caixa.DetalheUltimoMovimento,
                graficoEntradas = _caixa.GraficoEntradas, graficoSaidas = _caixa.GraficoSaidas,
                serieDisponivel = _caixa.SerieDisponivel, situacaoSerie = _caixa.SituacaoSerie,
                linhas = _caixa.Linhas.Select(l => new
                {
                    id = l.Id.ToString(CultureInfo.InvariantCulture), data = l.Data, descricao = l.Descricao,
                    categoria = l.Categoria, situacao = l.StatusRotulo, valor = l.ValorFormatado,
                    podeRealizar = l.PodeRealizar && _caixa.PodeEditarFinanceiro,
                    podeCancelar = l.PodeCancelar && _caixa.PodeEditarFinanceiro, ehEntrada = l.EhEntrada
                }).ToArray(),
                usuario = SessaoUsuario.Atual.Nome, carregando = _caixa.Carregando, ocupado = _executando,
                erro = _erro ?? (_caixa.NaoVerificado ? "Não foi possível verificar o caixa. Atualize para tentar novamente." : null),
                aviso = _snackbar?.EstaVisivel == true
                    ? new { texto = _snackbar.Mensagem, tipo = _snackbar.Tipo.ToString().ToLowerInvariant() }
                    : null,
                podeEditar = _caixa.PodeEditarFinanceiro, truncado = _caixa.Truncado,
                resumoFiltro = _caixa.ResumoFiltro, filtroTexto = _caixa.FiltroTexto,
                rotas = RotasPermitidas().Select(i => new { chave = i.Chave, rotulo = i.Rotulo }).ToArray()
            };
            _browser.CoreWebView2.PostWebMessageAsJson(JsonSerializer.Serialize(estado));
        }
        catch (Exception ex)
        {
            Clinica.Application.Diagnostico.Registrar("Financeiro web — estado não entregue", ex);
        }
    }

    private void MostrarFalha(Exception ex)
    {
        _falhou = true;
        _paginaPronta = false;
        Clinica.Application.Diagnostico.Registrar("Financeiro web — interface local indisponível", ex);
        _pronto.TrySetException(new InvalidOperationException("Interface web indisponível; Caixa nativo disponível.", ex));
        var painel = new StackPanel { Margin = new Thickness(32), MaxWidth = 700, HorizontalAlignment = HorizontalAlignment.Left };
        var titulo = new TextBlock { Text = "O Financeiro pode continuar na interface nativa", FontWeight = FontWeights.SemiBold, TextWrapping = TextWrapping.Wrap };
        titulo.SetResourceReference(TextBlock.FontSizeProperty, "Fonte.H1");
        painel.Children.Add(titulo);
        painel.Children.Add(new TextBlock { Text = "O componente visual não está disponível neste computador. Seus dados e as funções do Caixa continuam acessíveis.", Margin = new Thickness(0, 12, 0, 20), TextWrapping = TextWrapping.Wrap });
        var nativo = new Button { Content = "Abrir Caixa nativo", HorizontalAlignment = HorizontalAlignment.Left };
        nativo.Click += (_, _) =>
        {
            if (!SessaoUsuario.Atual.Autenticado || !SessaoUsuario.Atual.Pode(Permissao.VerFinanceiro))
            { MostrarAcessoNegado(); return; }
            MostrarCaixaNativo();
        };
        painel.Children.Add(nativo);
        if (_navegarNativo is not null)
        {
            var sistema = new Button { Content = "Abrir sistema completo", Margin = new Thickness(0, 12, 0, 0), HorizontalAlignment = HorizontalAlignment.Left };
            sistema.Click += (_, _) => Navegar(ModuloFinanceiro.ChaveCaixa, sistema: true);
            painel.Children.Add(sistema);
        }
        Content = painel;
    }

    private void MostrarCaixaNativo()
    {
        var painel = new DockPanel();
        if (_snackbar is not null)
        {
            var aviso = new Border { Margin = new Thickness(16), Padding = new Thickness(12) };
            aviso.SetResourceReference(Border.BackgroundProperty, "Brush.Info.Suave");
            aviso.SetBinding(VisibilityProperty, new Binding(nameof(SnackbarService.EstaVisivel))
            { Source = _snackbar, Converter = new BooleanToVisibilityConverter() });
            var texto = new TextBlock { TextWrapping = TextWrapping.Wrap };
            texto.SetBinding(TextBlock.TextProperty, new Binding(nameof(SnackbarService.Mensagem)) { Source = _snackbar });
            aviso.Child = texto;
            DockPanel.SetDock(aviso, Dock.Bottom);
            painel.Children.Add(aviso);
        }
        painel.Children.Add(new CaixaView { DataContext = _caixa });
        Content = painel;
    }

    private void MostrarAcessoNegado()
    {
        // Descarta o DOM com dados anteriores; uma falha de autorização nunca mantém
        // o último estado financeiro visível nem oferece o fallback com esses dados.
        Dispose();
        Content = new TextBlock
        {
            Text = "Seu acesso ao Financeiro não está disponível. Entre novamente no sistema com um usuário autorizado.",
            TextWrapping = TextWrapping.Wrap, Margin = new Thickness(32)
        };
    }

    public async Task CapturarPreviewAsync(Stream destino)
    {
        await QuandoPronto;
        if (_descartado) throw new ObjectDisposedException(nameof(FinanceiroWebView));
        await _browser.CoreWebView2.CapturePreviewAsync(CoreWebView2CapturePreviewImageFormat.Png, destino);
    }
    private void AoDescarregar(object sender, RoutedEventArgs e) => Dispose();
    public void Dispose()
    {
        if (_descartado) return;
        _descartado = true;
        _envio.Stop();
        _envio.Tick -= AoEnviarPendente;
        _caixa.PropertyChanged -= AoMudarEstado;
        if (_snackbar is not null) _snackbar.PropertyChanged -= AoMudarEstado;
        _caixa.Linhas.CollectionChanged -= AoMudarLinhas;
        Loaded -= AoCarregar;
        Unloaded -= AoDescarregar;
        _pronto.TrySetCanceled();
        _browser.Dispose();
    }

    private sealed class Mensagem
    {
        [JsonPropertyName("acao")] public string? Acao { get; set; }
        [JsonPropertyName("valor")] public string? Valor { get; set; }
        [JsonPropertyName("id")] public string? Id { get; set; }
    }
}
