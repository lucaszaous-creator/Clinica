using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Clinica.Application.Servicos;
using Clinica.Clinico.Modulo;
using Clinica.Clinico.ViewModels;
using Clinica.Clinico.Views;
using Clinica.Desktop.Controls;
using Clinica.Desktop.Shell.Modulos;
using Clinica.Domain;
using Clinica.Domain.Entities;
using Clinica.Infrastructure;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using Clinica.Desktop.Shell;
using Clinica.Desktop.Shell.Componentes;
static class Program
{
    static string Saida = "artifacts/layout-windows";
    static int falhas;
    static bool completo;
    static bool somenteRetornos;
    static bool somenteGestao;
    [STAThread]
    static int Main(string[] args)
    {
        somenteGestao = args.Contains("--gestao"); completo = args.Contains("--completo"); somenteRetornos = args.Contains("--retornos"); Directory.CreateDirectory(Saida);
        using var log = new StreamWriter(Saida + "/bindings.log"); PresentationTraceSources.DataBindingSource.Listeners.Add(new TextWriterTraceListener(log)); PresentationTraceSources.DataBindingSource.Switch.Level = SourceLevels.Error;
        var app = new System.Windows.Application { ShutdownMode = ShutdownMode.OnExplicitShutdown }; app.Resources.MergedDictionaries.Add(new ResourceDictionary { Source = new Uri("pack://application:,,,/Clinica.Desktop.Shell;component/Styles/Suite.xaml") });
        int code = 0; app.Dispatcher.BeginInvoke(async () => { try { await Executar(); if (falhas > 0) throw new Exception($"{falhas} cortes encontrados. Consulte artifacts/layout-windows."); Console.WriteLine("TELAS CONFERIDAS"); } catch (Exception e) { Console.WriteLine(e); code = 1; } finally { PresentationTraceSources.DataBindingSource.Flush(); app.Shutdown(); } }); app.Run(); return code;
    }
    static async Task Executar()
    {
        using var conexao = new SqliteConnection("Data Source=:memory:"); conexao.Open(); var options = new DbContextOptionsBuilder<ClinicaDbContext>().UseSqlite(conexao).Options;
        IModuloApp[] modulos = [new Clinica.Recepcao.Modulo.ModuloRecepcao(), new ModuloClinico(), new Clinica.Financeiro.Modulo.ModuloFinanceiro(), new Clinica.Gerente.Modulo.ModuloGerente()];
        var services = new ServiceCollection(); services.AddClinica("Host=127.0.0.1;Database=nao_usado;Username=nao_usado"); services.AddScoped(_ => new ClinicaDbContext(options)); services.AddSingleton<SessaoUsuario>(); services.AddSingleton<SnackbarService>(); services.AddSingleton<ISnackbarService>(s => s.GetRequiredService<SnackbarService>()); services.AddSingleton<IDialogoService, DialogoTeste>(); foreach (var m in modulos) m.Registrar(services);
        using var sp = services.BuildServiceProvider(); using var scope = sp.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<ClinicaDbContext>(); db.Database.EnsureCreated();
        var prof = new Profissional { Nome = "Profissional demonstrativo de nome comprido", RegistroConselho = "CRM-RJ 123456", Ativo = true }; var pac = new Paciente { Nome = "Paciente fictício com nome completo e sobrenomes para validar leitura", Documento = "12345678909", Telefone = "22999990000", Convenio = Convenio.UnimedIntercambio }; db.AddRange(prof, pac); await db.SaveChangesAsync();
        var usuario = new UsuarioSistema { Nome = prof.Nome, Login = "qa", Perfil = PerfilAcesso.Gerente, ProfissionalId = prof.Id, Profissional = prof }; db.Add(usuario); await db.SaveChangesAsync(); sp.GetRequiredService<SessaoUsuario>().Entrar(usuario);
        if (somenteGestao) { await ValidarGestao(sp, pac, usuario); return; }
        if (somenteRetornos) { await ValidarRetornos(sp, usuario); return; }
        for (var i = 0; i < 12; i++) db.Add(new Agendamento { PacienteId = pac.Id, ProfissionalId = prof.Id, DataHora = DateTime.Today.AddHours(8 + i / 2.0), ModalidadePrevista = ModalidadeAtendimento.AcupunturaComEletro }); await db.SaveChangesAsync();
        sp.GetRequiredService<PacienteEmFoco>().Definir(pac.Id, pac.Nome, 1, null, DateOnly.FromDateTime(DateTime.Today));
        var vm = new ShellViewModel("Gerente", modulos, sp); var win = new ShellWindow { DataContext = vm, ShowInTaskbar = false, ShowActivated = false, WindowStartupLocation = WindowStartupLocation.Manual, Left = -30000, Top = -30000, Width = 960, Height = 600 }; win.Show();
        foreach (var item in vm.Itens.Where(i => !i.Oculto && (completo || new[] { "agenda", "consultorio-agenda", "receituario", "consultorio-prescricoes", "configuracoes" }.Contains(i.Chave))))
        {
            vm.NavegarCommand.Execute(item); await Task.Delay(350);
            var abas = vm.TelaAtual is TelaComAbas t ? Descendentes(t).OfType<TabControl>().First() : null;
            int n = abas?.Items.Count ?? 1;
            for (var a = 0; a < n; a++)
            {
                if (abas != null) abas.SelectedIndex = a; await Task.Delay(200);
                foreach (int largura in new[] { 1920, 1366, 880, 960, 1024 })
                {
                    win.Width = largura; win.UpdateLayout(); await Task.Delay(100); win.UpdateLayout();
                    string nome = item.Chave + "-" + a + "-" + largura; Console.WriteLine("TELA " + nome);
                    foreach (var el in Descendentes(win).OfType<FrameworkElement>().Where(e => e.IsVisible && (e is Button || e is TextBox || e is ComboBox || e is DataGrid)))
                    {
                        var pt = el.TranslatePoint(new Point(), win); if (pt.X < -1 || pt.X + el.ActualWidth > win.ActualWidth - 15 + 1) { falhas++; Console.WriteLine($"VAZA {nome} {el.GetType().Name} {(el as Button)?.Content} x={pt.X:0} w={el.ActualWidth:0}"); }
                    }
                    foreach (var grade in Descendentes(win).OfType<DataGrid>())
                    {
                        var sv = Descendentes(grade).OfType<ScrollViewer>().FirstOrDefault();
                        if (sv != null && sv.ScrollableWidth > 0.01) { falhas++; Console.WriteLine($"COLUNAS {nome} excedente={sv.ScrollableWidth:0}"); }
                        if (grade.Name is "TabelaAgenda" or "TabelaMeuDia")
                        {
                            var ocupada = grade.Columns.Where(c => c.Visibility == Visibility.Visible).Sum(c => c.ActualWidth);
                            if (ocupada < grade.ActualWidth - 30) { falhas++; Console.WriteLine($"ESPAÇO PERDIDO {nome}: {ocupada}/{grade.ActualWidth}"); }
                            foreach (var botao in Descendentes(grade).OfType<Button>().Where(b => b.IsVisible && b.Content is string texto && texto is not ""))
                            {
                                DependencyObject? pai = botao; while (pai is not null && pai is not DataGridCell) pai = VisualTreeHelper.GetParent(pai);
                                if (pai is DataGridCell celula) { var ponto = botao.TranslatePoint(new Point(), celula); if (ponto.X < -1 || ponto.X + botao.ActualWidth > celula.ActualWidth + 1) { falhas++; Console.WriteLine($"AÇÃO CORTADA {nome}: {botao.Content}"); } }
                            }
                        }
                    }
                    if (largura == 1366 || largura == 960) Foto(win, nome);
                }
            }
        }
        win.Close();
        // Regressão de produção: a direção não precisa de cadastro como médico para
        // concluir uma sessão já escrita. Exercita a view real, inclusive o binding.
        usuario.ProfissionalId = null; usuario.Profissional = null;
        await db.SaveChangesAsync(); sp.GetRequiredService<SessaoUsuario>().Entrar(usuario);
        var posto = new PacienteWorkspaceViewModel(sp, sp.GetRequiredService<PacienteEmFoco>(), ModuloClinico.AbaDe(ModuloClinico.ChaveAtendimento));
        var janela = new Window { Content = new PacienteWorkspaceView { DataContext = posto },
            ShowInTaskbar = false, ShowActivated = false, WindowStartupLocation = WindowStartupLocation.Manual,
            Left = -30000, Top = -30000, Width = 1366, Height = 700 };
        janela.Show();
        for (int i = 0; i < 30 && !posto.TemSessao; i++) await Task.Delay(100);
        foreach (var largura in new[] { 1366, 1024 })
        {
            janela.Width = largura; janela.UpdateLayout(); await Task.Delay(200); janela.UpdateLayout();
            var botoes = Descendentes(janela).OfType<Button>().ToArray();
            var concluir = botoes.Single(b => ReferenceEquals(b.Command, posto.FinalizarSessaoCommand));
            var ponto = concluir.TranslatePoint(new Point(), janela);
            if (!concluir.IsVisible || !concluir.IsEnabled || ponto.X < 0 ||
                ponto.X + concluir.ActualWidth > janela.ActualWidth || ponto.Y + concluir.ActualHeight > janela.ActualHeight)
                throw new Exception("Gerente sem vínculo médico perdeu a ação visível de salvar sessão.");
            Foto(janela, "gerente-finalizar-" + largura);
            Console.WriteLine("GERENTE SEM VÍNCULO: Concluir sessão visível e habilitado em " + largura);
        }
        janela.Close();
        usuario.Perfil = PerfilAcesso.Enfermagem; usuario.ProfissionalId = prof.Id; usuario.Profissional = prof;
        var bsv = await db.Agendamentos.FirstAsync(); bsv.ModalidadePrevista = ModalidadeAtendimento.BsvComAcupuntura;
        await db.SaveChangesAsync(); sp.GetRequiredService<SessaoUsuario>().Entrar(usuario);
        var fila = sp.GetRequiredService<SessoesEnfermagemViewModel>();
        var janelaEnfermagem = new Window { Content = new SessoesEnfermagemView { DataContext = fila },
            ShowInTaskbar = false, ShowActivated = false, WindowStartupLocation = WindowStartupLocation.Manual,
            Left = -30000, Top = -30000, Width = 1366, Height = 768 };
        janelaEnfermagem.Show(); await fila.CarregarAsync();
        foreach (var largura in new[] { 1366, 1024 })
        {
            janelaEnfermagem.Width = largura; janelaEnfermagem.UpdateLayout(); await Task.Delay(200);
            if (fila.Sessoes.Count != 1) throw new Exception("A fila BSV não carregou a sessão fictícia.");
            Foto(janelaEnfermagem, "sessoes-enfermagem-" + largura);
        }
        janelaEnfermagem.Close();
    }
    static async Task ValidarRetornos(ServiceProvider sp, UsuarioSistema usuario)
    {
        foreach (var perfil in new[] { PerfilAcesso.Recepcao, PerfilAcesso.Gerente })
        {
            usuario.Perfil = perfil; sp.GetRequiredService<SessaoUsuario>().Entrar(usuario);
            var vm = sp.GetRequiredService<Clinica.Recepcao.ViewModels.RetornosAMarcarViewModel>();
            vm.Resumo = "12 retornos a marcar · dados fictícios para conferir o layout";
            for (var i = 0; i < 12; i++) vm.Linhas.Add(new(i + 1,
                "Paciente demonstrativo com nome comprido para conferir a leitura", i == 0 ? null : "22999990000",
                DateOnly.FromDateTime(DateTime.Today), DateOnly.FromDateTime(DateTime.Today.AddDays(i)), null,
                1, "Profissional demonstrativo com nome comprido", -i));
            var janela = new Window { Content = new Clinica.Recepcao.Views.RetornosAMarcarView { DataContext = vm },
                ShowInTaskbar = false, ShowActivated = false, Left = -30000, Top = -30000, Width = 1366, Height = 680 };
            janela.Show();
            foreach (var largura in new[] { 1920, 1366, 1024, 880 })
            {
                janela.Width = largura; janela.UpdateLayout(); await Task.Delay(150); janela.UpdateLayout();
                var grid = Descendentes(janela).OfType<DataGrid>().Single();
                var scroll = Descendentes(grid).OfType<ScrollViewer>().First();
                if (scroll.ScrollableWidth > 1) throw new Exception("Retornos exige rolagem horizontal em " + largura);
                var botoes = Descendentes(grid).OfType<Button>().Where(b => b.IsVisible && b.Content is string t && t is "Marcar horário" or "WhatsApp").ToArray();
                if (!botoes.Any(b => Equals(b.Content, "Marcar horário")) || !botoes.Any(b => Equals(b.Content, "WhatsApp"))) throw new Exception("Ações de retorno ausentes");
                foreach (var b in botoes)
                {
                    var ponto = b.TranslatePoint(new Point(), janela);
                    if (ponto.X < 0 || ponto.X + b.ActualWidth > janela.ActualWidth) throw new Exception("Ação fora da tela: " + b.Content);
                    if (Equals(b.Content, "Marcar horário") && !b.IsEnabled) throw new Exception("Perfil perdeu permissão de marcar");
                    DependencyObject? pai = b; while (pai is not null && pai is not DataGridCell) pai = VisualTreeHelper.GetParent(pai);
                    if (pai is DataGridCell celula && b.TranslatePoint(new Point(), celula).X + b.ActualWidth > celula.ActualWidth + 1) throw new Exception("Botão cortado: " + b.Content);
                }
                Foto(janela, $"retornos-{perfil}-{largura}");
                Console.WriteLine($"RETORNOS {perfil} {largura}: ações visíveis, sem rolagem horizontal");
            }
            janela.Close();
        }
    }
    static async Task ValidarGestao(ServiceProvider sp, Paciente paciente, UsuarioSistema usuario)
    {
        _ = new ShellViewModel("Gerente", [new Clinica.Recepcao.Modulo.ModuloRecepcao(), new ModuloClinico(),
            new Clinica.Financeiro.Modulo.ModuloFinanceiro(), new Clinica.Gerente.Modulo.ModuloGerente()], sp);
        var escopos = sp.GetRequiredService<IServiceScopeFactory>();
        var hoje = DateOnly.FromDateTime(DateTime.Today);
        LancamentoFinanceiro pendente;
        ItemEstoque item;
        using (var scope = sp.CreateScope())
        {
            var financeiro = scope.ServiceProvider.GetRequiredService<Clinica.Application.Servicos.FinanceiroService>();
            pendente = await financeiro.LancarAsync(hoje.AddDays(-10), TipoLancamento.Entrada,
                "Sessão particular de acompanhamento — descrição comprida para conferência no balcão", 1250,
                StatusLancamento.Previsto, pacienteId: paciente.Id, dataVencimento: hoje.AddDays(-2));
            await financeiro.LancarAsync(hoje, TipoLancamento.Entrada, "Sessão recebida por Pix", 400,
                formaPagamento: FormaPagamento.Pix, pacienteId: paciente.Id);
            var estoque = scope.ServiceProvider.GetRequiredService<Clinica.Application.Servicos.EstoqueService>();
            item = await estoque.SalvarItemAsync(new ItemEstoque { Nome = "Agulha demonstrativa", Unidade = "un", EstoqueMinimo = 10 });
            await estoque.EntrarAsync(item.Id, 2, custoUnitario: 3, data: hoje, validade: hoje.AddDays(10));
            var painel = await scope.ServiceProvider.GetRequiredService<Clinica.Application.Servicos.PainelDirecaoService>().MontarAsync(hoje);
            if (painel.NaoVerificados.Count != 0 || painel.SaldoMes != 400
                || !painel.Alertas.Any(a => a.Assunto == Clinica.Application.Servicos.AssuntoDirecao.EstoqueMinimo)
                || !painel.Alertas.Any(a => a.Assunto == Clinica.Application.Servicos.AssuntoDirecao.EstoqueValidade))
                throw new Exception("Gerente não consolidou corretamente caixa e estoque.");
        }
        var pagamentos = sp.GetRequiredService<Clinica.Recepcao.ViewModels.PagamentosViewModel>();
        var view = new Clinica.Recepcao.Views.PagamentosView { DataContext = pagamentos };
        var janela = new Window { Content = view, Width = 960, Height = 650 };
        await ConferirJanela(janela, "pagamentos-busca", [620, 960]);
        pagamentos.Seletor.SelecionarGarantindoNaLista(paciente);
        await pagamentos.CarregarAsync();
        if (pagamentos.Linhas.Count != 2 || pagamentos.NaoVerificado) throw new Exception("Consulta de pagamentos falhou.");
        await ConferirJanela(janela, "pagamentos-lista", [620, 960, 1366]);
        janela.Close();
        var receber = new Clinica.Recepcao.ViewModels.ReceberPagamentoViewModel(escopos, pendente)
            { Forma = FormaPagamento.CartaoCredito, Adquirente = "Maquininha", Bandeira = "Visa", Parcelas = "3" };
        var receberJanela = new Clinica.Recepcao.Janelas.ReceberPagamentoWindow(receber);
        await ConferirJanela(receberJanela, "receber-cartao", [480, 600]); receberJanela.Close();
        var baixar = new Clinica.Financeiro.Janelas.BaixarLancamentoWindow(new Clinica.Financeiro.ViewModels.BaixarLancamentoViewModel(escopos, pendente)
            { Forma = FormaPagamento.CartaoCredito });
        await ConferirJanela(baixar, "financeiro-baixa", [420, 560]); baixar.Close();
        var compra = new Clinica.Financeiro.Janelas.MovimentoEstoqueWindow(new Clinica.Financeiro.ViewModels.MovimentoEstoqueViewModel(escopos, item.Id, item.Nome)
            { GerarContaCompra = true, CompraPaga = true, Fornecedor = "Fornecedor demonstrativo", Quantidade = "100", CustoUnitario = "2,50", Lote = "L-2026" });
        await ConferirJanela(compra, "estoque-compra", [480, 560]); compra.Close();
        var gerente = sp.GetRequiredService<Clinica.Gerente.ViewModels.PainelDirecaoViewModel>();
        await gerente.CarregarCommand.ExecuteAsync(null);
        var direcao = new Window { Content = new Clinica.Gerente.Views.PainelDirecaoView { DataContext = gerente }, Height = 700 };
        await ConferirJanela(direcao, "gerente-gestao", [960, 1366]); direcao.Close();
        var taxaVm = new Clinica.Financeiro.ViewModels.TaxaEdicaoViewModel(escopos, 0)
            { Adquirente = "Maquininha / contrato mensal", Modalidade = ModalidadeCartao.CreditoParcelado,
              LiquidacaoMensal = true, ParcelasDe = "2", ParcelasAte = "12", Percentual = "3" };
        var contrato = new Clinica.Financeiro.Janelas.TaxaWindow(taxaVm);
        await ConferirJanela(contrato, "contrato-cartao", [460, 600]); contrato.Close();
        var pacoteVm = new PacoteVendaViewModel(escopos, paciente)
            { Forma = FormaPagamento.CartaoCredito, Adquirente = "Contrato mensal", Bandeira = "Visa", ParcelasCartao = "3" };
        var pacoteJanela = new PacoteVendaWindow(pacoteVm);
        await ConferirJanela(pacoteJanela, "pacote-cartao", [680, 860]); pacoteJanela.Close();
        using (var scope = sp.CreateScope())
        {
            await scope.ServiceProvider.GetRequiredService<TaxaService>().SalvarAsync(new TaxaCartao
                { Adquirente = "Contrato mensal", Modalidade = ModalidadeCartao.CreditoParcelado,
                  Percentual = 3, DiasParaReceber = 0, LiquidacaoMensal = true });
            await scope.ServiceProvider.GetRequiredService<PagamentosRecepcaoService>().ReceberAsync(paciente.Id,
                pendente.Id, pendente.Valor, hoje, FormaPagamento.CartaoCredito, adquirente: "Contrato mensal", bandeira: "Visa", parcelas: 3);
        }
        var recebiveisVm = sp.GetRequiredService<Clinica.Financeiro.ViewModels.RecebiveisViewModel>();
        await recebiveisVm.CarregarAsync();
        var recebiveisJanela = new Window { Content = new Clinica.Financeiro.Views.RecebiveisView { DataContext = recebiveisVm }, Height = 700 };
        await ConferirJanela(recebiveisJanela, "recebiveis-parcelas", [960, 1366]); recebiveisJanela.Close();
        foreach (var perfil in new[] { PerfilAcesso.Recepcao, PerfilAcesso.Profissional })
        {
            usuario.Perfil = perfil; sp.GetRequiredService<SessaoUsuario>().Entrar(usuario);
            if (pagamentos.PodeReceber != (perfil == PerfilAcesso.Recepcao)) throw new Exception("Permissão de pagamentos fora do perfil.");
        }
        Console.WriteLine("GESTÃO: saldos, alertas, permissões, pagamentos e compras conferidos.");
    }

    static async Task ConferirJanela(Window janela, string nome, int[] larguras)
    {
        janela.ShowInTaskbar = false; janela.ShowActivated = false; janela.WindowStartupLocation = WindowStartupLocation.Manual;
        janela.Left = -30000; janela.Top = -30000; janela.Show();
        foreach (var largura in larguras)
        {
            janela.Width = largura; janela.UpdateLayout(); await Task.Delay(120); janela.UpdateLayout();
            foreach (var grade in Descendentes(janela).OfType<DataGrid>().Where(g => g.IsVisible))
                if (Descendentes(grade).OfType<ScrollViewer>().FirstOrDefault()?.ScrollableWidth > 0.1)
                    throw new Exception($"Tabela ultrapassa a tela: {nome} {largura}");
            foreach (var botao in Descendentes(janela).OfType<Button>().Where(b => b.IsVisible && b.Content is string))
            {
                var ponto = botao.TranslatePoint(new Point(), janela);
                if (ponto.X < -1 || ponto.X + botao.ActualWidth > janela.ActualWidth + 1)
                    throw new Exception($"Ação cortada: {nome} {largura} {botao.Content}");
            }
            Foto(janela, $"{nome}-{largura}");
            Console.WriteLine($"GESTÃO {nome} {largura}: sem cortes horizontais");
        }
    }

    static IEnumerable<DependencyObject> Descendentes(DependencyObject o) { for (int i = 0; i < VisualTreeHelper.GetChildrenCount(o); i++) { var c = VisualTreeHelper.GetChild(o, i); yield return c; foreach (var d in Descendentes(c)) yield return d; } }
    static void Foto(Window w, string nome)
    {
        var dpi = VisualTreeHelper.GetDpi(w);
        var bmp = new RenderTargetBitmap((int)Math.Ceiling(w.ActualWidth * dpi.DpiScaleX),
            (int)Math.Ceiling(w.ActualHeight * dpi.DpiScaleY), dpi.PixelsPerInchX, dpi.PixelsPerInchY, PixelFormats.Pbgra32);
        bmp.Render(w);
        var png = new PngBitmapEncoder(); png.Frames.Add(BitmapFrame.Create(bmp));
        using var f = File.Create(Saida + "/" + nome + ".png"); png.Save(f);
    }

}
sealed class DialogoTeste : IDialogoService
{
    public string? PerguntarTexto(string titulo, string pergunta, string? textoInicial = null, bool obrigatorio = true) => null;
    public bool Confirmar(string titulo, string mensagem) => false;
    public bool ConfirmarPerigo(string titulo, string mensagem) => false;
    public void Aviso(string titulo, string mensagem) { }
}
sealed class SnackbarTeste : ISnackbarService
{
    public void Sucesso(string mensagem) { }
    public void Erro(string mensagem) { }
    public void Info(string mensagem) { }
}


