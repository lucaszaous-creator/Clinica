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
using Clinica.Desktop.Shell.Componentes;
using Clinica.Domain.Entities;



using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;

namespace Clinica.Desktop.Shell.Web;

/// <summary>
/// Interface local de todas as páginas e formulários do aplicativo. Somente mensagens explícitas alcançam os comandos existentes;
/// HTML não recebe objetos do host, credenciais, conexão de banco nem acesso a serviços.
/// </summary>
public sealed class SuiteWebView : UserControl, IDisposable
{
    public const string Origem = "https://suite.clinica.local/";
    private const string HostVirtual = "suite.clinica.local";
    private readonly IServiceProvider _services;
    private readonly PaginasWebController _paginas;
    private readonly DialogosWebController _dialogos;
    private readonly SemaphoreSlim _filaPagina = new(1, 1);
    private readonly Dictionary<string, SemaphoreSlim> _filasDialogos = [];
    private readonly Dictionary<string, HashSet<string>> _camposInvalidos = [];
    private readonly CancellationTokenSource _cancelamento = new();
    private readonly int _usuarioInicial = SessaoUsuario.Atual.UsuarioId;
    private readonly Permissao _permissoesIniciais = SessaoUsuario.Atual.Efetivas;
    private static readonly JsonSerializerOptions JsonOpcoes = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
    private readonly ItemMenuModulo[] _menu;
    private readonly string _titulo;
    private readonly string _inicial;
    private readonly Stack<string> _historico = new();
    private readonly SuiteFerramentasWeb _ferramentas;
    private readonly DispatcherTimer _relogioFerramentas;
    private CatalogoAulasSuiteWebDto? _catalogoAulas;
    private AulaSuiteWebDto? _aulaAtual;
    private string? _videoUrl;
    private readonly string _pastaAulas;
    private string? _navegacaoPendente;

    private readonly SnackbarService? _snackbar;
    private readonly WebView2 _browser = new();
    private readonly DispatcherTimer _envio;
    private readonly TaskCompletionSource _pronto = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private bool _iniciou;
    private bool _paginaPronta;
    private bool _executando;
    private bool _encerrando;

    public bool PodeFechar => _descartado || _encerrando || !SessaoUsuario.Atual.Autenticado ||
        SessaoUsuario.Atual.UsuarioId != _usuarioInicial || SessaoUsuario.Atual.Efetivas != _permissoesIniciais ||
        (_dialogos.EstadoAtual is { } dialogo ? !dialogo.Ocupado && dialogo.PodeFechar : !_executando);

    public void AvisarOperacaoEmAndamento() =>
        _snackbar?.Info("Aguarde a conclusão da operação antes de fechar.");
    private bool _descartado;
    private bool _falhou;
    private int _envioPendente;
    private DateTime _janelaMensagens = DateTime.UtcNow;
    private int _mensagensNaJanela;
    private string? _erro;


    public Task QuandoPronto => _pronto.Task;
    public DialogosWebController Dialogos => _dialogos;
    public async Task ExecutarComDialogosAsync(Func<Task> operacao)
    { await QuandoPronto; ExigirSessao(); using var contexto=DialogosDaSessao.Usar(_dialogos); await operacao(); AgendarEstado(); }

    public SuiteWebView(IServiceProvider services, IEnumerable<PaginasWebController.Pagina> paginas,
        IEnumerable<DialogosWebController.RegistroDialogo> dialogos, ItemMenuModulo[] menu, string titulo = "Clínica SemDor", string? inicial = null, string? dadosTreinamento = null)
    {
        ExigirSessao();
        _services = services;
        _titulo = titulo;
        _menu = menu.GroupBy(i=>i.Chave).Select(g=>g.First()).ToArray();
        _paginas = new PaginasWebController(services, paginas);
        _dialogos = new DialogosWebController(dialogos);
        _ferramentas=new(services,_menu,RotasPermitidas().Select(i=>i.Chave),dadosLocais:dadosTreinamento);
        _pastaAulas=Path.Combine(dadosTreinamento??Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),Configuracao.EdicaoDeTeste.NomePasta,"Treinamento"),"videos");
        _ferramentas.Changed+=AgendarEstado;
        _relogioFerramentas=new DispatcherTimer{Interval=TimeSpan.FromMinutes(1)};
        _relogioFerramentas.Tick+=AtualizarFerramentas;
        _inicial = ResolverRota(inicial) ?? RotasPermitidas().FirstOrDefault(i=>i.Inicial)?.Chave ?? RotasPermitidas().First().Chave;
        NavegacaoSuite.Ligar((chave,conferir)=> {
            var destino=ResolverRota(chave);
            if (destino is null) return false;
            if (!conferir) { _navegacaoPendente=destino; AgendarEstado(); }
            return true;
        },()=> { if (_historico.Count==0) return false; _voltando=true; _navegacaoPendente=_historico.Pop(); AgendarEstado(); return true; });
        _paginas.Changed += AgendarEstado;
        _dialogos.Mudou += AgendarEstado;
        _snackbar = services.GetService<SnackbarService>();
        if (_snackbar is not null) _snackbar.PropertyChanged += AoMudarEstado;
        _envio = new DispatcherTimer(DispatcherPriority.Background, Dispatcher)
        {
            Interval = TimeSpan.FromMilliseconds(100)
        };
        _envio.Tick += AoEnviarPendente;


        Content = _browser;
        SetResourceReference(BackgroundProperty, "Brush.Superficie");
        Loaded += AoCarregar;
        Unloaded += AoDescarregar;
    }

    private void ExigirSessao()
    {
        if (!SessaoUsuario.Atual.Autenticado || SessaoUsuario.Atual.UsuarioId != _usuarioInicial || SessaoUsuario.Atual.Efetivas != _permissoesIniciais)
            throw new InvalidOperationException("Entre no sistema para abrir o aplicativo.");

    }

    private async void AoCarregar(object sender, RoutedEventArgs e)
    {
        if (_iniciou || _descartado) return;
        _iniciou = true;
        try
        {
            var arquivos = Path.Combine(AppContext.BaseDirectory, "WebSuite", "wwwroot");
            if (!File.Exists(Path.Combine(arquivos, "index.html")))
                throw new FileNotFoundException("Os arquivos locais da interface local não foram encontrados.");
            var perfil = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                Configuracao.EdicaoDeTeste.NomePasta, "Suite", "WebView2");
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
            Directory.CreateDirectory(_pastaAulas);
            core.SetVirtualHostNameToFolderMapping("aulas.clinica.local",_pastaAulas,CoreWebView2HostResourceAccessKind.DenyCors);
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
            _relogioFerramentas.Start();
            await _ferramentas.AtualizarInfusoesAsync();
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
    private void BloquearPermissao(object? sender, CoreWebView2PermissionRequestedEventArgs e)
    {
        e.State = e.PermissionKind==CoreWebView2PermissionKind.Camera && e.IsUserInitiated && DocumentoPermitido(e.Uri)
            && _dialogos.EstadoAtual?.Pagina.Chave=="CapturaFoto" ? CoreWebView2PermissionState.Allow : CoreWebView2PermissionState.Deny;
        e.Handled = true;
    }
    private static void BloquearDownload(object? sender, CoreWebView2DownloadStartingEventArgs e) => e.Cancel = true;
    private void AoSolicitarRecurso(object? sender, CoreWebView2WebResourceRequestedEventArgs e)
    {
        if(!_descartado && e.Request.Uri==_videoUrl && SessaoUsuario.Atual.UsuarioId==_usuarioInicial && SessaoUsuario.Atual.Efetivas==_permissoesIniciais)return;
        if (_descartado || OrigemPermitida(e.Request.Uri)) return;
        e.Response = _browser.CoreWebView2.Environment.CreateWebResourceResponse(
            Stream.Null, 403, "Recurso externo bloqueado", "Content-Type: text/plain; charset=utf-8");
    }
    private void AoConcluirNavegacao(object? sender, CoreWebView2NavigationCompletedEventArgs e)
    {
        if (!_descartado && !e.IsSuccess && e.WebErrorStatus != CoreWebView2WebErrorStatus.OperationCanceled)
            MostrarFalha(new InvalidOperationException("Não foi possível abrir a interface local do aplicativo."));
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
            if (++_mensagensNaJanela > 80) return;
            var json = e.WebMessageAsJson;
            if (json.Length > 12_000_000) throw new InvalidOperationException("O conteúdo enviado excede o tamanho permitido.");
            var m = JsonSerializer.Deserialize<Mensagem>(json);
            if (m?.Acao is null || m.Acao.Length > 32 || m.Chave?.Length > 100 ||
                m.Id?.Length > 80 || m.Contexto?.Length > 80 || m.Tabela?.Length > 100 || m.Linha?.Length > 100) return;
            ExigirSessao();
            if (m.Acao == "pronto")
            {
                if (!_paginaPronta) await _paginas.NavegarAsync(_inicial);
                _paginaPronta = true;
                EnviarEstado();
                _pronto.TrySetResult();
                return;
            }
            await System.Windows.Threading.Dispatcher.Yield(DispatcherPriority.Background);
            if (_descartado || _falhou || !_paginaPronta || !DocumentoPermitido(_browser.CoreWebView2.Source)) return;
            ExigirSessao();
            if (m.Acao.StartsWith("dlg-", StringComparison.Ordinal))
            {
                if (m.Id is null || _dialogos.EstadoAtual?.Id != m.Id) return;
                if (!_filasDialogos.TryGetValue(m.Id, out var fila))
                    _filasDialogos[m.Id] = fila = new SemaphoreSlim(1, 1);
                // Cada modal tem sua fila. Um comando do pai pode aguardar o filho sem
                // impedir que esse filho receba seus campos, confirmação e cancelamento.
                await fila.WaitAsync(_cancelamento.Token);
                try
                {
                    ExigirSessao();
                    if (_dialogos.EstadoAtual?.Id != m.Id) return;
                    using var apresentacao = DialogosDaSessao.Usar(_dialogos);
                    _erro = null;
                    switch (m.Acao)
                    {
                        case "dlg-campo" when m.Chave is not null:
                            await AtualizarCampoValidado(m.Id, m, () => _dialogos.AtualizarCampoAsync(m.Id, m.Chave, m.Valor, m.Tabela, m.Linha)); break;
                        case "dlg-acao" when m.Chave is not null:
                            if (m.Chave != "fechar") ExigirCamposValidos(m.Id);
                            await _dialogos.ExecutarAcaoAsync(m.Id, m.Chave, m.Linha, m.Tabela); break;
                        case "dlg-fechar": _dialogos.Fechar(m.Id); break;
                    }
                }
                finally
                {
                    fila.Release();
                    if (_dialogos.EstadoAtual?.Id != m.Id) { _filasDialogos.Remove(m.Id); _camposInvalidos.Remove(m.Id); }
                    AgendarEstado();
                }
                return;
            }
            if (_dialogos.EstadoAtual is not null) return;
            if (_executando && m.Acao is not ("pagina-campo" or "progresso-aula" or "reiniciar-aula")) return;
            var contextoRecebido = m.Contexto ?? _paginas.Contexto;
            await _filaPagina.WaitAsync(_cancelamento.Token);
            try
            {
                ExigirSessao();
                if (_descartado || contextoRecebido != _paginas.Contexto || _dialogos.EstadoAtual is not null) return;
                if (m.Acao is "pagina-campo" or "pagina-acao" && m.Contexto is null) return;
                using var apresentacao = DialogosDaSessao.Usar(_dialogos);
                _erro = null;
                _executando = true;
                AgendarEstado();
                switch (m.Acao)
                {
                    case "pagina-campo" when m.Chave is not null:
                        await AtualizarCampoValidado(contextoRecebido, m, () => _paginas.AtualizarCampoAsync(m.Chave, m.Valor, m.Tabela, m.Linha)); break;
                    case "pagina-acao" when m.Chave is not null:
                        ExigirCamposValidos(contextoRecebido);
                        await _paginas.ExecutarAcaoAsync(m.Chave, m.Tabela, m.Linha); break;
                    case "navegar": await NavegarAsync(m.Texto); break;
                    case "trocar-usuario": await TrocarUsuarioAsync(); break;
                    case "avisos-lidos": _ferramentas.MarcarAvisosLidos();break;
                    case "fila-infusao": await NavegarAsync(_ferramentas.RotaFilaInfusao());break;
                    case "treinamento": _catalogoAulas=_ferramentas.CatalogoAulas();break;
                    case "abrir-aula" when m.Chave is not null:
                        var video=await _ferramentas.AbrirAulaAsync(m.Chave,_cancelamento.Token);
                        var copia=Path.Combine(_pastaAulas,Path.GetFileName(video.CaminhoVideo));
                        if(!Path.GetFullPath(copia).Equals(Path.GetFullPath(video.CaminhoVideo),StringComparison.OrdinalIgnoreCase))File.Copy(video.CaminhoVideo,copia,true);
                        _aulaAtual=video.Aula;_videoUrl="https://aulas.clinica.local/"+Uri.EscapeDataString(Path.GetFileName(video.CaminhoVideo));break;
                    case "progresso-aula" when m.Chave is not null:
                        if(_aulaAtual?.Id!=m.Chave)throw new InvalidOperationException("Esta aula não está aberta.");
                        if(m.Valor.ValueKind!=JsonValueKind.Object||!m.Valor.TryGetProperty("posicao",out var posicao)||!posicao.TryGetDouble(out var segundos))throw new InvalidOperationException("Posição inválida.");
                        bool? concluida=m.Valor.TryGetProperty("concluida",out var conclusao)&&conclusao.ValueKind==JsonValueKind.True?true:null;
                        _ferramentas.SalvarProgresso(m.Chave,segundos,concluida);_catalogoAulas=_ferramentas.CatalogoAulas();break;
                    case "reiniciar-aula" when m.Chave is not null: _ferramentas.ReiniciarAula(m.Chave);_catalogoAulas=_ferramentas.CatalogoAulas();break;
                    case "trocar-senha":
                        await _dialogos.AbrirAsync("TrocaSenha", new TrocaSenhaWebViewModel(_services.GetRequiredService<IServiceScopeFactory>())); break;
                }
            }
            finally { _executando = false; _filaPagina.Release(); AgendarEstado(); }
        }
        catch (JsonException) { }
        catch (OperationCanceledException) when (_descartado) { }
        catch (Exception ex)
        {
            if (_descartado) return;
            if (!SessaoUsuario.Atual.Autenticado || SessaoUsuario.Atual.UsuarioId != _usuarioInicial ||
                SessaoUsuario.Atual.Efetivas != _permissoesIniciais)
            { MostrarAcessoNegado(); return; }
            Clinica.Application.Diagnostico.Registrar("Suíte web — ação não concluída", ex);
            _erro = ex is InvalidOperationException or UnauthorizedAccessException
                ? ex.Message : "Não foi possível concluir a ação. Atualize os dados e tente novamente.";
            AgendarEstado();
        }
    }

    private async Task AtualizarCampoValidado(string contexto, Mensagem mensagem, Func<Task> atualizar)
    {
        var chave = JsonSerializer.Serialize(new[] { mensagem.Tabela, mensagem.Linha, mensagem.Chave });
        try
        {
            await atualizar();
            if (_camposInvalidos.TryGetValue(contexto, out var invalidos)) invalidos.Remove(chave);
        }
        catch
        {
            if (!_camposInvalidos.TryGetValue(contexto, out var invalidos)) _camposInvalidos[contexto] = invalidos = [];
            invalidos.Add(chave);
            throw;
        }
    }
    private void ExigirCamposValidos(string contexto)
    {
        if (_camposInvalidos.TryGetValue(contexto, out var invalidos) && invalidos.Count > 0)
            throw new InvalidOperationException("Corrija os campos inválidos antes de continuar. Nenhuma alteração foi gravada.");
    }

    private async Task TrocarUsuarioAsync()
    {
        if (!await _dialogos.ConfirmarAsync("Trocar usuário", "O aplicativo será reaberto na tela de entrada. Deseja continuar?", false)) return;
        ExigirSessao();
        var executavel = Environment.ProcessPath ?? throw new InvalidOperationException("Feche e abra o aplicativo para trocar de usuário.");
        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(executavel) { UseShellExecute = true });
        _encerrando = true;
        System.Windows.Application.Current.Shutdown();
    }

    private static bool PodeVerMenu(ItemMenuModulo item)
    {
        var sessao = SessaoUsuario.Atual;
        return sessao.Autenticado && sessao.Pode(item.Requer)
            && (item.RequerAlgum == Permissao.Nenhuma || sessao.PodeAlgum(item.RequerAlgum))
            && (item.PerfilExclusivo is null || sessao.Perfil == item.PerfilExclusivo);
    }
    private IEnumerable<ItemMenuModulo> RotasPermitidas()
        => _menu.Where(i => i.Abas.Count == 0 && (!i.Oculto || _menu.Any(p=>!p.Oculto&&PodeVerMenu(p)&&p.Abas.Any(a=>a.Chave==i.Chave))) && PodeVerMenu(i) && _paginas.Chaves.Contains(i.Chave));
    private bool RotaPermitida(string chave) => _menu.Any(i=>i.Chave==chave && PodeVerMenu(i)) && _paginas.Chaves.Contains(chave);
    private string? ResolverRota(string? chave)
    {
        if(chave is null)return null;
        if(RotaPermitida(chave))return chave;
        var item=_menu.FirstOrDefault(i=>i.Chave==chave&&PodeVerMenu(i));
        return item?.Abas.Select(a=>a.Chave).FirstOrDefault(RotaPermitida);
    }
    private bool _voltando;
    public async Task NavegarAsync(string? chave)
    {
        ExigirSessao();
        chave=ResolverRota(chave);
        if(chave is null) throw new InvalidOperationException("Esta tela não está disponível neste aplicativo.");
        var contextoAnterior=_paginas.Contexto;
        var anterior=_paginas.ChaveAtual;
        using var apresentacao=DialogosDaSessao.Usar(_dialogos);
        await _paginas.NavegarAsync(chave);
        if(!_voltando && anterior is not null && anterior!=chave)_historico.Push(anterior);
        _voltando=false;
        _camposInvalidos.Remove(contextoAnterior);
    }

    private void AoMudarEstado(object? sender, PropertyChangedEventArgs e) => AgendarEstado();
    private void AoMudarLinhas(object? sender, NotifyCollectionChangedEventArgs e) => AgendarEstado();
    private void AgendarEstado()
    {
        if (_descartado || Interlocked.Exchange(ref _envioPendente, 1) != 0) return;
        Dispatcher.BeginInvoke(() => { if (!_descartado) _envio.Start(); });
    }
    private async void AoEnviarPendente(object? sender, EventArgs e)
    {
        _envio.Stop();
        Interlocked.Exchange(ref _envioPendente, 0);
        if (_navegacaoPendente is { } destino && !_executando && _dialogos.EstadoAtual is null)
        {
            _navegacaoPendente=null;
            try { await NavegarAsync(destino); } catch(Exception ex) { _voltando=false; _erro=ex.Message; }
        }
        EnviarEstado();
    }
    private void EnviarEstado()
    {
        if (_descartado || _falhou || !_paginaPronta ||
            !DocumentoPermitido(_browser.CoreWebView2.Source)) return;
        if (!SessaoUsuario.Atual.Autenticado || SessaoUsuario.Atual.UsuarioId != _usuarioInicial ||
            SessaoUsuario.Atual.Efetivas != _permissoesIniciais)
        { MostrarAcessoNegado(); return; }
        try
        {
            ExigirSessao();
            var estado = new
            {
                tipo = "estado", titulo = _titulo, pagina = _paginas.ObterPagina(), dialogo = _dialogos.EstadoAtual,
                usuario = SessaoUsuario.Atual.Nome, ocupado = _executando, erro = _erro,
                ferramentas=_ferramentas.Estado(),treinamento=_catalogoAulas,aula=_aulaAtual,videoUrl=_videoUrl,
                aviso = _snackbar?.EstaVisivel == true
                    ? new { texto = _snackbar.Mensagem, tipo = _snackbar.Tipo.ToString().ToLowerInvariant() }
                    : null,
                rotas = RotasPermitidas().Select(i => new { chave = i.Chave, rotulo = i.Rotulo, grupo = GruposSidebar.Rotulo(i.Grupo) }).ToArray()

            };
            _browser.CoreWebView2.PostWebMessageAsJson(JsonSerializer.Serialize(estado, JsonOpcoes));
        }
        catch (Exception ex)
        {
            Clinica.Application.Diagnostico.Registrar("Suíte web — estado não entregue", ex);
        }
    }

    private void MostrarFalha(Exception ex)
    {
        _falhou = true;
        _paginaPronta = false;
        _dialogos.Dispose();
        Clinica.Application.Diagnostico.Registrar("Suíte web — interface local indisponível", ex);
        _pronto.TrySetException(new InvalidOperationException("Interface do aplicativo indisponível.", ex));
        var painel = new StackPanel { Margin = new Thickness(32), MaxWidth = 700, HorizontalAlignment = HorizontalAlignment.Left };
        var titulo = new TextBlock { Text = "Não foi possível abrir a interface do aplicativo", FontWeight = FontWeights.SemiBold, TextWrapping = TextWrapping.Wrap };
        titulo.SetResourceReference(TextBlock.FontSizeProperty, "Fonte.H1");
        painel.Children.Add(titulo);
        painel.Children.Add(new TextBlock { Text = "Feche e abra o aplicativo. Se persistir, reinstale a versão atual para reparar o componente visual e os arquivos locais.", Margin = new Thickness(0, 12, 0, 20), TextWrapping = TextWrapping.Wrap });
        Content = painel;
    }

    private void MostrarAcessoNegado()
    {
        var anterior=Window.GetWindow(this);
        Dispose();
        Content=null;
        var aviso=new EntradaWebWindow(new(null,_titulo,"aviso","Seu acesso ao aplicativo não está disponível. Entre novamente no sistema com um usuário autorizado."));
        if(System.Windows.Application.Current.MainWindow==anterior)System.Windows.Application.Current.MainWindow=aviso;
        aviso.Show();
        anterior?.Close();
    }

    public async Task CapturarPreviewAsync(Stream destino)
    {
        await QuandoPronto;
        if (_descartado) throw new ObjectDisposedException(nameof(SuiteWebView));
        await _browser.CoreWebView2.CapturePreviewAsync(CoreWebView2CapturePreviewImageFormat.Png, destino);
    }
    private void AoDescarregar(object sender, RoutedEventArgs e) => Dispose();
    private async void AtualizarFerramentas(object? sender,EventArgs e)=>await _ferramentas.AtualizarInfusoesAsync();
    public void Dispose()
    {
        if (_descartado) return;
        _descartado = true;
        _cancelamento.Cancel();
        _relogioFerramentas.Stop();_relogioFerramentas.Tick-=AtualizarFerramentas;
        _ferramentas.Changed-=AgendarEstado;_ferramentas.Dispose();
        _dialogos.Mudou -= AgendarEstado;
        _dialogos.Dispose();
        _paginas.Changed -= AgendarEstado;
        _paginas.Dispose();
        _envio.Stop();
        _envio.Tick -= AoEnviarPendente;

        if (_snackbar is not null) _snackbar.PropertyChanged -= AoMudarEstado;

        Loaded -= AoCarregar;
        Unloaded -= AoDescarregar;
        _pronto.TrySetCanceled();
        _browser.Dispose();
    }

    private sealed class Mensagem
    {
        [JsonPropertyName("acao")] public string? Acao { get; set; }
        [JsonPropertyName("valor")] public JsonElement Valor { get; set; }
        [JsonIgnore] public string? Texto => Valor.ValueKind == JsonValueKind.String ? Valor.GetString() : null;
        [JsonPropertyName("chave")] public string? Chave { get; set; }
        [JsonPropertyName("contexto")] public string? Contexto { get; set; }
        [JsonPropertyName("tabela")] public string? Tabela { get; set; }
        [JsonPropertyName("linha")] public string? Linha { get; set; }
        [JsonPropertyName("id")] public string? Id { get; set; }
    }
}
