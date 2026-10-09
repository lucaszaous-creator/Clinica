using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using Clinica.Desktop.Controls;
using Clinica.Desktop.Shell;
using Clinica.Desktop.Shell.Modulos;
using Clinica.Domain.Entities;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;

internal static class Program
{
    [STAThread] private static void Main()
    {
        var app = new System.Windows.Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        app.Resources.MergedDictionaries.Add(new ResourceDictionary { Source = new Uri("pack://application:,,,/Clinica.Desktop.Shell;component/Styles/Suite.xaml") });
        app.Startup += async (_, _) =>
        {
            try { await Executar(); }
            catch (Exception e) { Console.WriteLine(e); Environment.ExitCode = 1; }
            finally { app.Shutdown(); }
        };
        app.Run();
    }

    private static async Task Esperar(Func<bool> condicao, string descricao)
    {
        var limite = DateTime.UtcNow.AddSeconds(8);
        while (!condicao())
        {
            if (DateTime.UtcNow > limite) throw new Exception(descricao);
            await Task.Delay(50);
        }
    }

    private static async Task Executar()
    {
        var saida = Path.GetFullPath("artifacts/notificacao-web"); Directory.CreateDirectory(saida);
        using var servicos = new ServiceCollection().AddSingleton<SnackbarService>().AddSingleton<SessaoUsuario>().BuildServiceProvider();
        servicos.GetRequiredService<SessaoUsuario>().Entrar(new UsuarioSistema { Id = 91500, Login = "qa.notificacao", Nome = "Pessoa fictícia · teste", Perfil = PerfilAcesso.Gerente });
        var avisos = servicos.GetRequiredService<SnackbarService>();
        using var navegador = new WebView2();
        var modelo = new ShellViewModel("Clínica · teste sintético de notificação", Array.Empty<IModuloApp>(), servicos) { TelaAtual = navegador };
        var janela = new ShellWindow { DataContext = modelo, Width = 1000, Height = 700, ShowInTaskbar = false, WindowStartupLocation = WindowStartupLocation.CenterScreen };
        var externa = new Window { Title = "Outra janela fictícia", Width = 400, Height = 240, ShowInTaskbar = false, Content = new TextBlock { Text = "Teste: notificação não deve acompanhar esta janela.", Margin = new Thickness(24), TextWrapping = TextWrapping.Wrap } };
        janela.Show(); janela.Activate();
        try
        {
            var ambiente = await CoreWebView2Environment.CreateAsync(userDataFolder: Path.Combine(saida, "perfil-web"));
            await navegador.EnsureCoreWebView2Async(ambiente);
            var pronto = new TaskCompletionSource();
            navegador.CoreWebView2.NavigationCompleted += (_, _) => pronto.TrySetResult();
            navegador.NavigateToString("""
                <!doctype html><html lang="pt-BR"><meta charset="utf-8"><style>
                *{box-sizing:border-box}body{margin:0;padding:28px;background:#eef3fc;color:#172b4d;font:16px Segoe UI}
                input{padding:12px;border:1px solid #667799;border-radius:8px;width:80%}.linha{padding:24px;background:white;border:1px solid #d7e0ee;border-radius:10px;margin-top:18px}
                </style><h1>Agenda · dados fictícios</h1><p>Conteúdo WebView2 para validar a sobreposição da notificação.</p>
                <input id="busca" aria-label="Buscar paciente fictício" value="Texto preservado no campo"><div class="linha">08:00 — Paciente fictício A · Consulta</div>
                <div class="linha">09:00 — Paciente fictício B · Atendimento concluído</div><div class="linha">10:00 — Paciente fictício C · Agendado</div>
                <div class="linha">Nenhuma conexão com banco de pacientes é usada neste teste.</div></html>
                """);
            await pronto.Task.WaitAsync(TimeSpan.FromSeconds(20));
            await Task.Delay(300);
            var dono = new WindowInteropHelper(janela).Handle;
            AtivarParaCaptura(dono); janela.Activate();
            await Esperar(() => GetForegroundWindow() == dono, "O sistema não trouxe a janela sintética ao primeiro plano para capturá-la com segurança.");
            navegador.Focus(); await navegador.CoreWebView2.ExecuteScriptAsync("document.getElementById('busca').focus()");
            await Task.Delay(150);
            var foco = Keyboard.FocusedElement;
            var popup = (SnackbarPopup)janela.FindName("Notificacao");
            var conteudo = (Border)janela.FindName("ConteudoNotificacao");
            const string mensagem = "Notificação de teste: a clínica está pronta para atender. Esta mensagem completa deve aparecer por cima da agenda, preservando o campo de busca e o foco atual.";
            avisos.Info(mensagem);
            await Esperar(() => popup.IsOpen && conteudo.ActualHeight > 0, "O aviso não abriu na janela ativa.");
            await Task.Delay(150);
            if (!janela.IsActive || GetForegroundWindow() != dono || Keyboard.FocusedElement != foco)
                throw new Exception($"A notificação alterou foco: ativa={janela.IsActive}, foreground={GetForegroundWindow()}, dono={dono}, foco antes={foco?.GetType().Name}, depois={Keyboard.FocusedElement?.GetType().Name}, mesmoFoco={Keyboard.FocusedElement == foco}.");
            if (await navegador.CoreWebView2.ExecuteScriptAsync("document.activeElement.id==='busca'") != "true")
                throw new Exception("O campo da agenda perdeu o foco.");
            var hwndPopup = ((HwndSource)PresentationSource.FromVisual(conteudo)).Handle;
            if ((GetWindowLongPtr(hwndPopup, -20).ToInt64() & 0x8) != 0) throw new Exception("A notificação permaneceu topmost global.");
            CapturarEConferir(janela, conteudo, hwndPopup, Path.Combine(saida, "notificacao-integral-sobre-web.png"));
            Console.WriteLine("OK: notificação integral acima do HWND web, sem topmost global e sem roubar foco; captura real gravada.");

            janela.Left += 25; janela.Top += 15; janela.Width = 920; janela.Height = 620;
            await Task.Delay(200);
            var origem = conteudo.PointToScreen(new Point());
            var interior = janela.PointToScreen(new Point());
            if (origem.X < interior.X || origem.Y < interior.Y || origem.X + conteudo.ActualWidth > interior.X + janela.ActualWidth)
                throw new Exception("Aviso não acompanhou o redimensionamento da janela.");
            externa.Show(); externa.Activate();
            await Esperar(() => !popup.IsOpen, "Aviso continuou sobre outra janela.");
            avisos.Erro("Erro fictício recebido com a clínica inativa; deve permanecer no histórico.");
            if (popup.IsOpen) throw new Exception("Uma mensagem ativou o popup enquanto outro aplicativo estava ativo.");
            janela.Activate();
            await Esperar(() => popup.IsOpen, "Aviso ainda válido não retornou com sua janela.");
            janela.WindowState = WindowState.Minimized;
            await Esperar(() => !popup.IsOpen, "Aviso ficou aberto com a clínica minimizada.");
            janela.WindowState = WindowState.Normal; janela.Activate();
            await Esperar(() => popup.IsOpen, "Aviso não retornou ao restaurar a janela.");
            await Esperar(() => !avisos.EstaVisivel && !popup.IsOpen, "Aviso não respeitou o prazo do serviço.");
            if (avisos.Historico.Count != 2 || avisos.NaoLidos != 2 || avisos.Historico[1].Mensagem != mensagem)
                throw new Exception("O histórico da notificação mudou ao ocultar o popup.");
            avisos.Sucesso("Teste de fechamento da janela.");
            await Esperar(() => popup.IsOpen, "Aviso de fechamento não abriu.");
            janela.Close();
            if (popup.IsOpen) throw new Exception("Popup permaneceu após fechar o shell.");
            Console.WriteLine("OK: mover/redimensionar, desativar, mensagem em segundo plano, minimizar/restaurar, expiração de 4s, histórico e fechamento.");
        }
        finally { externa.Close(); if (janela.IsVisible) janela.Close(); }
    }

    private static void CapturarEConferir(Window janela, Border aviso, IntPtr popup, string destino)
    {
        // Captura SOMENTE o retângulo da janela sintética ativa, incluindo o HWND filho
        // web e o popup. RenderTargetBitmap/CapturePreview isolados não provam airspace.
        var handle = new WindowInteropHelper(janela).Handle;
        if (GetForegroundWindow() != handle) throw new Exception("Captura cancelada: janela sintética não está ativa.");
        GetClientRect(handle, out var tamanhoCliente);
        var cliente = new Ponto(0, 0); ClientToScreen(handle, ref cliente);
        var r = new Retangulo { Esquerda = cliente.X, Topo = cliente.Y,
            Direita = cliente.X + tamanhoCliente.Direita, Baixo = cliente.Y + tamanhoCliente.Baixo };
        var inicio = aviso.PointToScreen(new Point());
        var fim = aviso.PointToScreen(new Point(aviso.ActualWidth, aviso.ActualHeight));
        var topo = new Ponto((int)inicio.X + 8, (int)inicio.Y + 8);
        var baixo = new Ponto((int)inicio.X + 8, (int)fim.Y - 8);
        GetWindowRect(popup, out var areaPopup);
        if (areaPopup.Topo > topo.Y || areaPopup.Baixo < baixo.Y)
            throw new Exception("A janela da notificação não comporta a mensagem completa.");
        using var bitmap = new System.Drawing.Bitmap(r.Direita - r.Esquerda, r.Baixo - r.Topo);
        using (var desenho = System.Drawing.Graphics.FromImage(bitmap))
            desenho.CopyFromScreen(r.Esquerda, r.Topo, 0, 0, bitmap.Size);
        bitmap.Save(destino, System.Drawing.Imaging.ImageFormat.Png);
        foreach (var ponto in new[] { topo, baixo })
        {
            var pixel = bitmap.GetPixel(ponto.X - r.Esquerda, ponto.Y - r.Topo);
            if (pixel.R > 85 || pixel.G > 85 || pixel.B > 100)
                throw new Exception("A captura não contém o fundo escuro completo da notificação sobre o web.");
        }
    }

    private static void AtivarParaCaptura(IntPtr janela)
    {
        var atual = GetCurrentThreadId();
        var primeiroPlano = GetWindowThreadProcessId(GetForegroundWindow(), out _);
        var ligou = atual != primeiroPlano && AttachThreadInput(atual, primeiroPlano, true);
        try { SetForegroundWindow(janela); }
        finally { if (ligou) AttachThreadInput(atual, primeiroPlano, false); }
    }

    [DllImport("kernel32.dll")] private static extern uint GetCurrentThreadId();
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr janela, out uint processo);
    [DllImport("user32.dll")] private static extern bool AttachThreadInput(uint origem, uint destino, bool ligar);
    [StructLayout(LayoutKind.Sequential)] private struct Ponto(int x, int y) { public int X = x; public int Y = y; }
    [StructLayout(LayoutKind.Sequential)] private struct Retangulo { public int Esquerda, Topo, Direita, Baixo; }
    [DllImport("user32.dll")] private static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] private static extern bool SetForegroundWindow(IntPtr janela);
    [DllImport("user32.dll")] private static extern bool GetClientRect(IntPtr janela, out Retangulo retangulo);
    [DllImport("user32.dll")] private static extern bool ClientToScreen(IntPtr janela, ref Ponto ponto);
    [DllImport("user32.dll")] private static extern bool GetWindowRect(IntPtr janela, out Retangulo retangulo);
    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")] private static extern IntPtr GetWindowLongPtr(IntPtr janela, int indice);
}
