using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;
using Clinica.Desktop.Controls;
using DFormato = System.Drawing.Imaging.PixelFormat;
using DRet = System.Drawing.Rectangle;
using Clinica.Desktop.Shell;
using Clinica.Desktop.Shell.Modulos;
using Clinica.Domain;
using Clinica.Domain.Entities;
using Clinica.Infrastructure;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PreviaBlazor.Sementes;

/// <summary>
/// O FLASH PRETO, MEDIDO NA TELA DE VERDADE (out/2026). Uso:
///   dotnet run --project tools/ValidarLayoutWindows -- --blazor
///
/// É a única medição que vê o que a clínica viu no vídeo: o preto da janela nativa do
/// WebView2, que nasce antes do navegador existir. O Chromium do Linux não tem essa janela
/// (lá o teste é o trocas.js da prévia), e o bUnit não tem pixel nenhum.
///
/// Abre o shell DE VERDADE (a ShellWindow, o módulo Financeiro, o navegador único por
/// janela) NA TELA — a captura é da tela composta, então a janela não pode estar fora dela —
/// e troca entre as telas Blazor, com Pacotes e Ajuda (WPF) no meio, enquanto uma linha de
/// fundo fotografa a área de trabalho em laço. Cada foto ganha a fração de pixels escuros;
/// foto com bloco escuro é o flash. As fotos piores e uma amostra vão para
/// artifacts/trocas-blazor, para olhar.
///
/// ⚠️ Antes de contar flash, confere que a captura VÊ o WebView2: tela Blazor assentada
/// fotografada como um retângulo de uma cor só quer dizer que a foto não alcança o
/// navegador — e aí "zero flash" seria a garantia aparente, não uma medida.
/// </summary>
static class TrocasBlazor
{
    const string Saida = "artifacts/trocas-blazor";
    const double LimiarEscuro = 0.20;   // fração da área com luma < 50: texto fica em ~2–8%

    sealed record Foto(double Ms, double Escuro, double Uniforme, Bitmap? Imagem);
    sealed record Passo(double Ms, string Chave, string Fase);

    public static async Task<int> Executar()
    {
        Directory.CreateDirectory(Saida);
        foreach (var f in Directory.GetFiles(Saida)) File.Delete(f);

        using var conexao = new SqliteConnection("Data Source=:memory:"); conexao.Open();
        var options = new DbContextOptionsBuilder<ClinicaDbContext>().UseSqlite(conexao).Options;
        // O Financeiro E o Faturamento: o mesmo navegador da janela tem de servir aos dois sem flash.
        IModuloApp[] modulos = [new Clinica.Financeiro.Modulo.ModuloFinanceiro(), new Clinica.Faturamento.Modulo.ModuloFaturamento()];
        var services = new ServiceCollection();
        services.AddClinica("Host=127.0.0.1;Database=nao_usado;Username=nao_usado");
        services.AddScoped(_ => new ClinicaDbContext(options));
        services.AddSingleton<SessaoUsuario>();
        services.AddSingleton<SnackbarService>(); services.AddSingleton<ISnackbarService>(s => s.GetRequiredService<SnackbarService>());
        services.AddSingleton<IDialogoService, DialogoTeste>();
        foreach (var m in modulos) m.Registrar(services);
        using var sp = services.BuildServiceProvider();
        using (var scope = sp.CreateScope())
        {
            // As MESMAS sementes fictícias da prévia e do dotnet test: tela com dados, não vazia.
            var db = scope.ServiceProvider.GetRequiredService<ClinicaDbContext>();
            db.Database.EnsureCreated();
            foreach (var s in typeof(ISemente).Assembly.GetTypes()
                         .Where(t => typeof(ISemente).IsAssignableFrom(t) && t is { IsAbstract: false, IsInterface: false })
                         .Select(t => (ISemente)Activator.CreateInstance(t)!).OrderBy(s => s.Ordem))
            { s.Semear(db); db.ChangeTracker.Clear(); }
            var gerente = new UsuarioSistema { Nome = "Direção (QA)", Login = "qa", Perfil = PerfilAcesso.Gerente, SenhaHash = "-", SenhaSalt = "-", Ativo = true };
            db.Add(gerente); db.SaveChanges();
            sp.GetRequiredService<SessaoUsuario>().Entrar(gerente);
        }

        double larguraTela = SystemParameters.PrimaryScreenWidth, alturaTela = SystemParameters.PrimaryScreenHeight;
        var escala = GetDpiForSystem() / 96.0;
        double largura = Math.Min(1280, larguraTela), altura = Math.Min(760, alturaTela - 40);
        Console.WriteLine($"BLAZOR monitor {larguraTela}x{alturaTela} DIP · escala {escala:0.##}");

        // A foto cobre a janela INTEIRA desde ANTES de ela aparecer: o instante que mais
        // interessa é o nascimento do navegador, junto com a primeira tela. O recorte da
        // área de trabalho só se conhece depois do leiaute — até lá as fotos ficam guardadas
        // e são medidas no fim.
        var janela = new DRet(0, 0, (int)(largura * escala), (int)(altura * escala));
        DRet? area = null;
        var relogio = Stopwatch.StartNew();
        var fotos = new List<Foto>();
        var pendentes = new List<(double Ms, Bitmap Imagem)>();
        var passos = new List<Passo>();
        using var parar = new CancellationTokenSource();
        var fotografo = Task.Run(() =>
        {
            while (!parar.IsCancellationRequested)
            {
                var bmp = new Bitmap(janela.Width, janela.Height, DFormato.Format24bppRgb);
                using (var g = Graphics.FromImage(bmp)) g.CopyFromScreen(janela.Location, System.Drawing.Point.Empty, janela.Size);
                var ms = relogio.Elapsed.TotalMilliseconds;
                DRet? recorte;
                lock (fotos) { recorte = area; if (recorte is null) { pendentes.Add((ms, bmp)); continue; } }
                Guardar(fotos, ms, bmp, recorte.Value);
            }
        });

        var vm = new ShellViewModel("Financeiro", modulos, sp);
        var win = new ShellWindow
        {
            DataContext = vm, ShowInTaskbar = true, Topmost = true, WindowStartupLocation = WindowStartupLocation.Manual,
            Left = 0, Top = 0, Width = largura, Height = altura
        };
        passos.Add(new Passo(relogio.Elapsed.TotalMilliseconds, "(abertura do app)", "abrir"));
        win.Show(); win.Activate();
        await Task.Delay(1500);

        // A área de trabalho em pixels FÍSICOS, relativa à foto da janela.
        var camada = win.CamadaDaJanela;
        var canto = camada.PointToScreen(new System.Windows.Point(0, 0));
        var fim = camada.PointToScreen(new System.Windows.Point(camada.ActualWidth, camada.ActualHeight));
        var recorteFinal = DRet.Intersect(new DRet((int)canto.X, (int)canto.Y, (int)(fim.X - canto.X), (int)(fim.Y - canto.Y)), janela);
        Console.WriteLine($"BLAZOR janela {janela} · área de trabalho {recorteFinal}");
        List<(double Ms, Bitmap Imagem)> atrasadas;
        lock (fotos) { area = recorteFinal; atrasadas = [.. pendentes]; pendentes.Clear(); }
        foreach (var (ms, img) in atrasadas) Guardar(fotos, ms, img, recorteFinal);

        string[] blazor = [ChavesSuite.Caixa, ChavesSuite.FechamentoCaixa, "fluxo-caixa", ChavesSuite.Contas, ChavesSuite.Inadimplencia,
            "plano-contas", ChavesSuite.Recebiveis, ChavesSuite.Conciliacao, "extrato-banco", "taxas", "estoque", "repasses",
            ChavesSuite.Resultado, ChavesSuite.Producao,
            // Faturamento de guias (out/2026)
            "faturamento-pendencias", "faturamento-guias", "faturamento-faturados", "faturamento-glosas",
            "faturamento-nc", "faturamento-tiss", "faturamento-relatorios", "faturamento-parametros"];
        string[] wpf = [ChavesSuite.Pacotes, ChavesSuite.Ajuda];
        async Task Ir(string chave, string fase, int esperaMs)
        {
            passos.Add(new Passo(relogio.Elapsed.TotalMilliseconds, chave, fase));
            if (!NavegacaoSuite.Ir(chave)) Console.WriteLine($"BLAZOR aviso: a chave {chave} não navegou");
            await Task.Delay(esperaMs);
        }

        // 1. a primeira abertura de cada tela (é aqui que o desenho antigo piscava, uma vez por tela)
        Sonda? sonda = null;
        foreach (var c in blazor)
        {
            await Ir(c, "abrir", 1500);
            // Depois da 1ª tela assentada, pergunta ao PRÓPRIO navegador o que ele desenhou: é o
            // que separa "a foto da tela não alcança o WebView2" de "o navegador não desenhou".
            sonda ??= await Sondar(win);
        }
        await Ir(wpf[0], "abrir", 800);
        await Ir(wpf[1], "abrir", 800);
        // 2. trocas: aleatórias, intervalos de 0 a 600 ms, a WPF uma vez em cinco
        var sorteio = new Random(20261016);
        for (var i = 0; i < 150; i++)
        {
            var chave = sorteio.Next(5) == 0 ? wpf[sorteio.Next(2)] : blazor[sorteio.Next(blazor.Length)];
            await Ir(chave, "trocar", sorteio.Next(5) == 0 ? 30 : sorteio.Next(600));
        }
        await Task.Delay(1200);
        parar.Cancel(); await fotografo;
        fotos.Sort((x, y) => x.Ms.CompareTo(y.Ms));
        win.Close();

        // ===== a captura vê o navegador? =====
        // Logo depois de assentar a 1ª abertura do Caixa (1,2 s depois do clique), a área tem
        // de ter texto: nem só fundo, nem uma cor só.
        var assentou = passos[1].Ms; // a 1ª tela navegada já teve 1,5 s para assentar ao fim da espera
        var amostra = fotos.Where(f => f.Ms > assentou + 1100 && f.Ms < assentou + 1450).ToList();
        var veNavegador = amostra.Count > 0 && amostra.Average(f => f.Uniforme) < 0.97;
        Console.WriteLine($"BLAZOR {fotos.Count} fotos em {relogio.Elapsed.TotalSeconds:0} s ({fotos.Count / relogio.Elapsed.TotalSeconds:0} por segundo) · {passos.Count} navegações");
        Console.WriteLine($"BLAZOR captura vê o navegador: {(veNavegador ? "sim" : "NÃO")} (uniformidade na tela assentada: {(amostra.Count > 0 ? amostra.Average(f => f.Uniforme) : double.NaN):0.000})");

        var escuras = new List<(Foto Foto, Passo Passo)>();
        foreach (var f in fotos)
        {
            var p = passos.LastOrDefault(x => x.Ms <= f.Ms);
            if (p is not null && f.Escuro >= LimiarEscuro) escuras.Add((f, p));
        }
        // Antes da 1ª navegação o navegador ainda NÃO EXISTE (ele nasce com a 1ª tela Blazor):
        // o escuro ali é a janela WPF do shell se compondo, não o WebView2. Conta e mostra à
        // parte, para não esconder nem atribuir ao lugar errado.
        var daJanela = escuras.Where(e => e.Passo.Fase == "abrir" && e.Passo.Chave == "(abertura do app)").ToList();
        if (daJanela.Count > 0)
            Console.WriteLine($"BLAZOR abertura da janela WPF (antes do navegador existir): {daJanela.Count} foto(s) escura(s), até {daJanela.Max(e => e.Foto.Ms - e.Passo.Ms):0} ms depois do Show");
        escuras = escuras.Except(daJanela).ToList();
        var pior = fotos.Count > 0 ? fotos.Max(f => f.Escuro) : 0;
        Console.WriteLine($"BLAZOR maior fração escura numa foto: {pior:P1} · fotos escuras: {escuras.Count} (abrir: {escuras.Count(e => e.Passo.Fase == "abrir")}, trocar: {escuras.Count(e => e.Passo.Fase == "trocar")})");
        foreach (var (f, p) in escuras.Take(20))
            Console.WriteLine($"BLAZOR   escura {f.Escuro:P0} a {f.Ms - p.Ms:0} ms de {p.Fase} {p.Chave}");

        var n = 0;
        foreach (var f in fotos.Where(f => f.Imagem is not null).OrderByDescending(f => f.Escuro).Take(12))
            f.Imagem!.Save($"{Saida}/pior-{n++:00}-{f.Escuro:0.000}.png", ImageFormat.Png);
        n = 0;
        foreach (var f in fotos.Where(f => f.Imagem is not null && f.Escuro < LimiarEscuro).Take(30))
            f.Imagem!.Save($"{Saida}/amostra-{n++:00}.png", ImageFormat.Png);
        foreach (var f in fotos) f.Imagem?.Dispose();
        File.WriteAllLines($"{Saida}/fotos.csv", fotos.Select(f =>
        {
            var p = passos.LastOrDefault(x => x.Ms <= f.Ms);
            return FormattableString.Invariant($"{f.Ms:0.0};{f.Escuro:0.0000};{f.Uniforme:0.0000};{p?.Fase};{p?.Chave};{(p is null ? 0 : f.Ms - p.Ms):0}");
        }).Prepend("ms;escuro;uniforme;fase;chave;ms_desde_clique"));

        if (!veNavegador)
        {
            // O navegador desenhou a tela (a sonda leu o texto e a própria foto dele tem
            // conteúdo) e a foto da TELA não vê: é limite da captura deste computador, não
            // defeito do app. Sem a sonda confirmar, a tela em branco é defeito.
            if (sonda is { Desenhou: true })
            {
                Console.WriteLine("BLAZOR INCONCLUSIVO: o navegador desenhou a tela, mas a captura da tela deste computador não alcança o WebView2 — o flash não pode ser medido aqui.");
                return 2;
            }
            Console.WriteLine("BLAZOR REPROVADO: a tela Blazor assentada está em branco e o próprio navegador não confirma ter desenhado.");
            return 1;
        }
        if (escuras.Count > 0) { Console.WriteLine("BLAZOR REPROVADO: houve quadro escuro na troca de telas."); return 1; }
        Console.WriteLine("BLAZOR SEM FLASH: nenhuma foto escura em " + passos.Count + " navegações.");
        return 0;
    }

    [System.Runtime.InteropServices.DllImport("user32.dll")] static extern uint GetDpiForSystem();

    sealed record Sonda(bool Desenhou);

    /// <summary>Pergunta ao navegador da janela: quantos caracteres a página tem e o que a foto DELE mostra.</summary>
    static async Task<Sonda> Sondar(Window win)
    {
        var visor = Descendentes(win).OfType<Microsoft.AspNetCore.Components.WebView.Wpf.BlazorWebView>().FirstOrDefault();
        if (visor is null) { Console.WriteLine("BLAZOR sonda: nenhum navegador na janela"); return new Sonda(false); }
        var core = visor.WebView?.CoreWebView2;
        Console.WriteLine($"BLAZOR sonda: navegador {visor.Visibility} {visor.ActualWidth:0}x{visor.ActualHeight:0} · núcleo {(core is null ? "NÃO inicializado" : "inicializado " + core.Environment.BrowserVersionString)}");
        if (core is null) return new Sonda(false);
        try
        {
            // A leitura do texto é uma CORRIDA com a primeira renderização: a foto do navegador
            // já mostrava a tela (uniformidade 0,51) e o texto vinha 0. Por isso relê por até
            // 5 s, e soma TODAS as telas visíveis do palco (com a página inteira como caminho de
            // baixo) — a primeira pode ser uma casca ainda vazia ao lado da que está desenhada.
            const string Script = "(() => { const t = [...document.querySelectorAll('.palco-tela:not([hidden])')]" +
                ".reduce((s, e) => s + (e.innerText ?? '').trim().length, 0);" +
                " return t > 0 ? t : (document.body?.innerText ?? '').trim().length; })()";
            var caracteres = 0;
            for (var tentativa = 0; tentativa < 10 && caracteres == 0; tentativa++)
            {
                if (tentativa > 0) await Task.Delay(500);
                var texto = await core.ExecuteScriptAsync(Script);
                caracteres = int.TryParse(texto, out var n) ? n : 0;
            }
            using var ms = new MemoryStream();
            await core.CapturePreviewAsync(Microsoft.Web.WebView2.Core.CoreWebView2CapturePreviewImageFormat.Png, ms);
            ms.Position = 0;
            using var foto = new Bitmap(ms);
            foto.Save($"{Saida}/sonda-navegador.png", ImageFormat.Png);
            var (_, uniforme) = Medir(foto);
            Console.WriteLine($"BLAZOR sonda: tela à vista com {caracteres} caracteres · foto do próprio navegador {foto.Width}x{foto.Height}, uniformidade {uniforme:0.000}");
            return new Sonda(caracteres > 0 && uniforme < 0.97);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"BLAZOR sonda falhou: {ex.GetType().Name}: {ex.Message}");
            return new Sonda(false);
        }
    }

    static IEnumerable<DependencyObject> Descendentes(DependencyObject raiz)
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(raiz); i++)
        {
            var filho = VisualTreeHelper.GetChild(raiz, i);
            yield return filho;
            foreach (var d in Descendentes(filho)) yield return d;
        }
    }

    /// <summary>Mede a área de trabalho da foto; guarda a imagem só das escuras e de uma a cada 40 (memória).</summary>
    static void Guardar(List<Foto> fotos, double ms, Bitmap janela, DRet recorte)
    {
        var bmp = janela.Clone(recorte, DFormato.Format24bppRgb);
        janela.Dispose();
        var (escuro, uniforme) = Medir(bmp);
        lock (fotos)
        {
            var guardar = escuro >= LimiarEscuro || fotos.Count % 40 == 0;
            if (!guardar) bmp.Dispose();
            fotos.Add(new Foto(ms, escuro, uniforme, guardar ? bmp : null));
        }
    }

    /// <summary>Fração de pixels escuros (luma &lt; 50) e fração do pixel mais comum (uniformidade), por amostragem.</summary>
    static (double Escuro, double Uniforme) Medir(Bitmap bmp)
    {
        var dados = bmp.LockBits(new DRet(0, 0, bmp.Width, bmp.Height), ImageLockMode.ReadOnly, DFormato.Format24bppRgb);
        try
        {
            var bytes = new byte[dados.Stride * bmp.Height];
            System.Runtime.InteropServices.Marshal.Copy(dados.Scan0, bytes, 0, bytes.Length);
            int total = 0, escuros = 0;
            var contagem = new Dictionary<int, int>();
            for (var y = 0; y < bmp.Height; y += 4)
            {
                var linha = y * dados.Stride;
                for (var x = 0; x < bmp.Width; x += 4)
                {
                    byte b = bytes[linha + x * 3], g = bytes[linha + x * 3 + 1], r = bytes[linha + x * 3 + 2];
                    total++;
                    if (0.299 * r + 0.587 * g + 0.114 * b < 50) escuros++;
                    var cor = (r << 16) | (g << 8) | b;
                    contagem[cor] = contagem.GetValueOrDefault(cor) + 1;
                }
            }
            return (total == 0 ? 0 : (double)escuros / total, total == 0 ? 1 : (double)contagem.Values.Max() / total);
        }
        finally { bmp.UnlockBits(dados); }
    }
}
