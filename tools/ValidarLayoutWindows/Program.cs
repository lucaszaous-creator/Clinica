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
    static bool somenteAcompanhamento;
    static bool somenteInfusao;
    [STAThread]
    static int Main(string[] args)
    {
        somenteAcompanhamento = args.Contains("--acompanhamento");
        somenteInfusao = args.Contains("--infusao");
        somenteGestao = args.Contains("--gestao"); completo = args.Contains("--completo"); somenteRetornos = args.Contains("--retornos"); Directory.CreateDirectory(Saida);
        using var log = new StreamWriter(Saida + "/bindings.log"); PresentationTraceSources.DataBindingSource.Listeners.Add(new TextWriterTraceListener(log)); PresentationTraceSources.DataBindingSource.Switch.Level = SourceLevels.Error;
        var app = new System.Windows.Application { ShutdownMode = ShutdownMode.OnExplicitShutdown }; app.Resources.MergedDictionaries.Add(new ResourceDictionary { Source = new Uri("pack://application:,,,/Clinica.Desktop.Shell;component/Styles/Suite.xaml") });
        int code = 0; app.Dispatcher.BeginInvoke(async () => { try { await Executar(); if (falhas > 0) throw new Exception($"{falhas} cortes encontrados. Consulte artifacts/layout-windows."); Console.WriteLine("TELAS CONFERIDAS"); } catch (Exception e) { Console.WriteLine(e); code = 1; } finally { PresentationTraceSources.DataBindingSource.Flush(); app.Shutdown(); } }); app.Run(); return code;
    }
    static async Task Executar()
    {
        var pausaAgenda = new PausaLeituraAgenda();
        using var conexao = new SqliteConnection("Data Source=:memory:"); conexao.Open(); var options = new DbContextOptionsBuilder<ClinicaDbContext>().UseSqlite(conexao).AddInterceptors(pausaAgenda).Options;
        IModuloApp[] modulos = [new Clinica.Recepcao.Modulo.ModuloRecepcao(), new ModuloClinico(), new Clinica.Financeiro.Modulo.ModuloFinanceiro(), new Clinica.Faturamento.Modulo.ModuloFaturamento(), new Clinica.Gerente.Modulo.ModuloGerente()];
        var services = new ServiceCollection(); services.AddClinica("Host=127.0.0.1;Database=nao_usado;Username=nao_usado"); services.AddScoped(_ => new ClinicaDbContext(options)); services.AddSingleton<SessaoUsuario>(); services.AddSingleton<SnackbarService>(); services.AddSingleton<ISnackbarService>(s => s.GetRequiredService<SnackbarService>()); services.AddSingleton<IDialogoService, DialogoTeste>(); foreach (var m in modulos) m.Registrar(services);
        using var sp = services.BuildServiceProvider(); using var scope = sp.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<ClinicaDbContext>(); db.Database.EnsureCreated();
        var prof = new Profissional { Nome = "Profissional demonstrativo de nome comprido", RegistroConselho = "CRM-RJ 123456", Ativo = true }; var pac = new Paciente { Nome = "Paciente fictício com nome completo e sobrenomes para validar leitura", Documento = "12345678909", Telefone = "22999990000", Convenio = Convenio.UnimedIntercambio }; db.AddRange(prof, pac); await db.SaveChangesAsync();
        var usuario = new UsuarioSistema { Nome = prof.Nome, Login = "qa", Perfil = PerfilAcesso.Gerente, ProfissionalId = prof.Id, Profissional = prof }; db.Add(usuario); await db.SaveChangesAsync(); sp.GetRequiredService<SessaoUsuario>().Entrar(usuario);
        if (somenteAcompanhamento) { await ValidarAcompanhamento(sp, db, pac, usuario); return; }
        if (somenteInfusao) { await AuditarInfusao(sp, usuario); await ConferirProgressoInfusao(sp, pac, prof); return; }
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
        // Modelo A: a nova organização continua abrindo o mesmo formulário, sem gravar
        // um horário ao apenas consultar uma vaga. Exercita a View e os bindings reais.
        NavegacaoSuite.Ir(Clinica.Recepcao.Modulo.ModuloRecepcao.ChaveAgenda);
        await Task.Delay(250);
        var planejamento = Descendentes(win).OfType<FrameworkElement>().Select(e => e.DataContext)
            .OfType<Clinica.Recepcao.ViewModels.AgendaViewModel>().First();
        planejamento.Dia = DateTime.Today.AddDays(8 - (int)DateTime.Today.DayOfWeek);
        await planejamento.CarregarAsync();
        planejamento.FiltroProfissional = planejamento.FiltroProfissionais.First(p => p.Id == prof.Id);
        planejamento.DuracaoPlanejamento = "60";
        await planejamento.CarregarAsync();
        if (planejamento.VagasPlanejamento.Count == 0) throw new Exception("Planejamento: profissional sem horários deve oferecer vagas.");
        win.UpdateLayout(); await System.Windows.Threading.Dispatcher.Yield(System.Windows.Threading.DispatcherPriority.ApplicationIdle);
        var botaoVaga = Descendentes(win).OfType<Button>().First(b => b.DataContext is Clinica.Recepcao.ViewModels.BlocoAgendaVisual { Disponivel: true });
        if (!botaoVaga.IsEnabled || botaoVaga.Command is null || !botaoVaga.Command.CanExecute(botaoVaga.CommandParameter))
            throw new Exception("Planejamento: vaga visível não permite abrir a marcação no primeiro carregamento.");
        // Sala e profissional são recursos independentes: a ocupação por outro médico
        // deve aparecer na consulta da sala, sem mudar a vaga oferecida sem filtro de sala.
        var salaA = new Sala { Nome = "Sala A", Capacidade = 1, Ativa = true };
        var salaB = new Sala { Nome = "Sala B", Capacidade = 1, Ativa = true };
        var outroProfissional = new Profissional { Nome = "Outro profissional fictício", Ativo = true };
        prof.AgendaProtegida = false;
        db.AddRange(salaA, salaB, outroProfissional); await db.SaveChangesAsync();
        var inicioPlanejado = planejamento.Dia.Date.AddHours(8);
        db.AddRange(new Agendamento { PacienteId = pac.Id, ProfissionalId = prof.Id, SalaId = salaA.Id,
                DataHora = inicioPlanejado, DuracaoMinutos = 60 },
            new Agendamento { PacienteId = pac.Id, ProfissionalId = outroProfissional.Id, SalaId = salaB.Id,
                DataHora = inicioPlanejado.AddHours(1), DuracaoMinutos = 60 });
        await db.SaveChangesAsync();
        planejamento.AgruparPorSala = true; await planejamento.CarregarAsync();
        var problemasPlanejamento = new List<string>();
        var blocosPorSala = planejamento.ColunasPlanejamento.SelectMany(c => c.Blocos).ToArray();
        if (!blocosPorSala.Any(b => b.Disponivel && b.Celula?.SalaId == salaA.Id && b.Celula.Quando == inicioPlanejado.AddHours(1))
            || blocosPorSala.Any(b => b.Disponivel && b.Celula?.SalaId == salaB.Id && b.Celula.Quando == inicioPlanejado.AddHours(1)))
            problemasPlanejamento.Add("Por sala: profissional e ocupação da sala não foram combinados.");
        if (planejamento.VagasPlanejamento.Select(v => v.Inicio).Distinct().Count() != planejamento.VagasPlanejamento.Count)
            problemasPlanejamento.Add("Por sala: painel de vagas repetiu o mesmo horário.");
        win.UpdateLayout(); await System.Windows.Threading.Dispatcher.Yield(System.Windows.Threading.DispatcherPriority.ApplicationIdle);
        if (!Descendentes(win).OfType<Button>().Any(b => b.IsVisible && b.IsEnabled
                && ReferenceEquals(b.Command, planejamento.AgendarNaFaixaCommand)
                && b.CommandParameter is Clinica.Recepcao.ViewModels.CelulaAgenda { Livre: false, NoPassado: false }))
            problemasPlanejamento.Add("Horário ocupado: atalho de marcar mais um paciente desapareceu com a trava desligada.");
        if (problemasPlanejamento.Count > 0) throw new Exception(string.Join("\n", problemasPlanejamento));
        Foto(win, "planejamento-salas-e-sobreposicao");
        prof.AgendaProtegida = true; await db.SaveChangesAsync(); await planejamento.CarregarAsync();
        win.UpdateLayout(); await System.Windows.Threading.Dispatcher.Yield(System.Windows.Threading.DispatcherPriority.ApplicationIdle);
        if (Descendentes(win).OfType<Button>().Any(b => b.IsVisible && b.IsEnabled
                && ReferenceEquals(b.Command, planejamento.AgendarNaFaixaCommand)
                && b.CommandParameter is Clinica.Recepcao.ViewModels.CelulaAgenda { Livre: false, NoPassado: false }))
            throw new Exception("Atalho de sobreposição liberado com trava ativa.");
        planejamento.AgruparPorSala = false;
        planejamento.ModoSemana = true; await planejamento.CarregarAsync();
        if (planejamento.ProfissionalEmFocoId != prof.Id || planejamento.Colunas.Any(c => c.ProfissionalId != prof.Id))
            throw new Exception("Planejamento: a semana perdeu o profissional escolhido.");
        pausaAgenda.Armar();
        var cargaPausada = planejamento.CarregarAsync();
        try
        {
            await pausaAgenda.Entrou.Task.WaitAsync(TimeSpan.FromSeconds(10));
            planejamento.DuracaoPlanejamento = "45";
            if (!planejamento.DisponibilidadeNaoVerificada || planejamento.VagasPlanejamento.Count != 0
                || planejamento.ColunasPlanejamento.Any(c => c.Blocos.Any(b => b.Disponivel)))
                throw new Exception("Leitura parcial: vagas foram liberadas antes de consultar os horários da semana.");
        }
        finally { pausaAgenda.Liberar.TrySetResult(); await cargaPausada; }
        planejamento.DisponibilidadeNaoVerificada = true;
        planejamento.DuracaoPlanejamento = "45";
        if (planejamento.VagasPlanejamento.Count != 0 || planejamento.ColunasPlanejamento.Any(c => c.Blocos.Any(b => b.Disponivel)))
            throw new Exception("Planejamento: disponibilidade não verificada anunciou vaga.");
        planejamento.DuracaoPlanejamento = "60"; await planejamento.CarregarAsync();
        var vagaEscolhida = planejamento.VagasPlanejamento.First();
        var quantidadeAntes = await db.Agendamentos.CountAsync();
        await planejamento.EscolherVagaPlanejamentoCommand.ExecuteAsync(vagaEscolhida);
        await Task.Delay(500);
        var formulario = Descendentes(win).OfType<FrameworkElement>().Select(e => e.DataContext)
            .OfType<Clinica.Recepcao.ViewModels.NovoAtendimentoViewModel>().First();
        if (formulario.Profissional?.Id != prof.Id || formulario.Duracao != "60"
            || formulario.Data.Date != vagaEscolhida.Inicio.Date || formulario.Hora != vagaEscolhida.Inicio.ToString("HH:mm"))
            throw new Exception("Planejamento: o formulário perdeu o contexto da vaga.");
        if (await db.Agendamentos.CountAsync() != quantidadeAntes) throw new Exception("Consultar vaga gravou um agendamento.");
        Console.WriteLine("PLANEJAMENTO: profissional, semana, falha de leitura e formulário preservados; nenhuma gravação.");
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
    static async Task ConferirProgressoInfusao(ServiceProvider sp, Paciente paciente, Profissional medico)
    {
        var escopos = sp.GetRequiredService<IServiceScopeFactory>();
        var dialogo = sp.GetRequiredService<IDialogoService>();
        var folha = new FolhaExecucaoViewModel(escopos, dialogo, 0);
        await folha.CarregarAsync();
        folha.Paciente = paciente.Nome;
        folha.Numero = "PRE DEMONSTRAÇÃO";
        folha.NaoVerificado = false;
        folha.Itens.Add(new LinhaExecucaoItem {
            ItemId = 1, Ordem = 1, Descricao = "Medicamento fictício para conferência visual",
            Detalhe = "Dose e via demonstrativas", Situacao = "Não executável",
            Marca = "○ 30/09/2026 às 10:37", Justificativa = "Justificativa de teste; médico comunicado.",
            Executante = "Profissional de teste · COREN de teste", Pendente = false,
            Realizado = false, NaoRealizado = true, Suspenso = false, SeNecessario = false, AlertaAlergia = null
        });
        var encerrada = new PrescricaoInterna { Situacao = SituacaoPrescricao.Encerrada,
            ExigeAssinaturaEletronicaDaExecucao = true };
        encerrada.Assinaturas.Add(new AssinaturaDocumento { Papel = PapelAssinatura.Executante, ArquivoId = 1 });
        var linhaEncerrada = LinhaSalaInfusao.De(encerrada, DateOnly.FromDateTime(DateTime.Today));
        if (linhaEncerrada.RegistroPendente || linhaEncerrada.AguardaAssinatura)
            throw new Exception("Documento principal assinado continua aparecendo como pendência no desktop.");
        folha.Mensagem = null;
        var janela = new FolhaExecucaoWindow(folha) { ShowInTaskbar = false, ShowActivated = false,
            WindowStartupLocation = WindowStartupLocation.Manual, Left = -30000, Top = -30000, Width = 880, Height = 600 };
        janela.Show();
        folha.Carregando = true;
        await System.Windows.Threading.Dispatcher.Yield(System.Windows.Threading.DispatcherPriority.ApplicationIdle);
        janela.UpdateLayout();
        if (!Descendentes(janela).OfType<ProgressBar>().Any(p => p.IsVisible && p.IsIndeterminate))
            throw new Exception("Folha sem progresso visível.");
        if (Descendentes(janela).OfType<Button>().Any(b => b.IsVisible && b.IsEnabled))
            throw new Exception("Folha aceita ações durante carregamento.");
        Foto(janela, "infusao-folha-carregando");
        folha.Carregando = false;
        folha.Mensagem = "✓ Registro confirmado. A checagem foi salva no prontuário.";
        folha.MensagemEhErro = false;
        await System.Windows.Threading.Dispatcher.Yield(System.Windows.Threading.DispatcherPriority.ApplicationIdle);
        janela.UpdateLayout();
        if (Descendentes(janela).OfType<ProgressBar>().Any(p => p.IsVisible))
            throw new Exception("Folha permanece carregando ao concluir.");
        var impressoes = Descendentes(janela).OfType<Button>()
            .Where(b => b.IsVisible && b.Content is string texto && texto.StartsWith("Imprimir")).ToList();
        if (impressoes.Count != 1 || !Equals(impressoes[0].Content, "Imprimir infusão"))
            throw new Exception("A folha deve oferecer apenas a impressão do documento da infusão.");
        Foto(janela, "infusao-folha-confirmada");
        folha.AvisoPdfHistorico = "PDF histórico: as assinaturas foram feitas sem incorporar os dados de execução. "
            + "Consulte os horários, resultados e justificativas nesta tela. A impressão preserva o PDF originalmente assinado.";
        await System.Windows.Threading.Dispatcher.Yield(System.Windows.Threading.DispatcherPriority.ApplicationIdle);
        janela.UpdateLayout();
        if (!Descendentes(janela).OfType<TextBlock>().Any(t => t.IsVisible && t.Text == folha.AvisoPdfHistorico))
            throw new Exception("Aviso do PDF histórico oculto.");
        Foto(janela, "infusao-pdf-historico");
        janela.Close();
        var prescricao = new PrescricaoInternaEdicaoViewModel(escopos, dialogo, paciente.Id, paciente.Nome, medico.Id);
        var editor = new Clinica.Clinico.Janelas.PrescricaoInternaWindow(prescricao) { ShowInTaskbar = false, ShowActivated = false,
            WindowStartupLocation = WindowStartupLocation.Manual, Left = -30000, Top = -30000, Width = 880, Height = 600 };
        editor.Show();
        await System.Windows.Threading.Dispatcher.Yield(System.Windows.Threading.DispatcherPriority.ApplicationIdle);
        prescricao.Ocupado = true; prescricao.TextoOperacao = "Assinando e arquivando prescrição…";
        editor.UpdateLayout();
        if (!Descendentes(editor).OfType<ProgressBar>().Any(p => p.IsVisible && p.IsIndeterminate)
            || Descendentes(editor).OfType<Button>().Any(b => b.IsVisible && b.IsEnabled))
            throw new Exception("Editor de infusão não protege a operação em andamento.");
        Foto(editor, "infusao-prescricao-assinando");
        prescricao.Ocupado = false;
        editor.Close();
        Console.WriteLine("INFUSÃO: progresso visível, ações protegidas e confirmação sem indicador preso.");
    }

    static async Task AuditarInfusao(ServiceProvider sp, UsuarioSistema usuario)
    {
        usuario.Perfil = PerfilAcesso.Profissional;
        sp.GetRequiredService<SessaoUsuario>().Entrar(usuario);
        using var vm = new SalaInfusaoViewModel(sp.GetRequiredService<IServiceScopeFactory>(), sp.GetRequiredService<IDialogoService>());
        await vm.CarregarAsync();
        for (int i = 1; i <= 12; i++) vm.Validacoes.Add(new LinhaSalaInfusao {
            PrescricaoId = i, PacienteId = i, Paciente = $"Paciente fictício {i:00} com nome comprido",
            Etapas = EtapasInfusao.Da(new PrescricaoInterna { OrigemEnfermagem = true, Situacao = SituacaoPrescricao.Encerrada,
                Assinaturas = [new() { Papel = PapelAssinatura.Executante, ArquivoId = 1, ArquivoRegistroId = 1 }] }),
            Numero = $"PRE TESTE/{i:000}", Hora = "09:30", Prescritor = "Médico fictício", Progresso = "Aguardando avaliação médica",
            Itens = "Infusão fictícia", TemPendencia = false, Encerrada = true, Devolvida = false,
            AguardaAssinatura = false, RegistroPendente = false, Dia = "" });
        vm.ResumoValidacoes = "12 infusões fictícias aguardando avaliação e assinatura";
        var win = new Window { Content = new SalaInfusaoView { DataContext = vm }, ShowInTaskbar = false, ShowActivated = false,
            WindowStartupLocation = WindowStartupLocation.Manual, Left = -30000, Top = -30000, Width = 1024, Height = 768 };
        win.Show();
        await ConferirJanela(win, "infusao-fila-medico", [880, 1024, 1366]);
        var rolagem = Descendentes(win).OfType<ScrollViewer>().Single(s => s.Name == "RolagemValidacoes");
        var botoes = Descendentes(win).OfType<Button>().Where(b => Equals(b.Content, "Avaliar e assinar")).ToArray();
        if (botoes.Length != 12) throw new Exception("A fila deve manter todas as 12 pendências.");
        foreach (var altura in new[] { 600, 768 })
        foreach (var largura in new[] { 880, 1024, 1366 })
        {
            win.Width = largura; win.Height = altura; win.UpdateLayout();
            foreach (var botao in botoes.Reverse())
            {
                botao.Focus(); botao.BringIntoView();
                await System.Windows.Threading.Dispatcher.Yield(System.Windows.Threading.DispatcherPriority.ApplicationIdle);
                win.UpdateLayout();
                var ponto = botao.TranslatePoint(new Point(), rolagem);
                if (ponto.Y < -1 || ponto.Y + botao.ActualHeight > rolagem.ActualHeight + 1)
                    throw new Exception($"Pendência {((LinhaSalaInfusao)botao.DataContext).Numero} inacessível em {largura}x{altura}.");
            }
            Console.WriteLine($"INFUSÃO {largura}x{altura}: 12 ações alcançáveis por foco e rolagem.");
        }
        botoes[^1].BringIntoView(); await Task.Delay(50);
        Foto(win, "infusao-fila-ultima-pendencia");
        win.Close();
    }

    static async Task ValidarAcompanhamento(ServiceProvider sp, ClinicaDbContext db, Paciente paciente, UsuarioSistema usuario)
    {
        db.Acompanhamentos.Add(new() { PacienteId = paciente.Id, Tipo = TipoAcompanhamento.Recall,
            Modalidade = ModalidadeAtendimento.BsvApenas, ReferenciaEm = DateTime.Today.AddDays(-90),
            CriadoEm = DateTime.Today, CriadoPor = usuario.Login, ResponsavelId = usuario.Id,
            ProximoContato = DateOnly.FromDateTime(DateTime.Today.AddDays(-2)) });
        await db.SaveChangesAsync();
        var vm = sp.GetRequiredService<Clinica.Recepcao.ViewModels.AcompanhamentoViewModel>();
        var view = new Clinica.Recepcao.Views.AcompanhamentoView { DataContext = vm };
        var win = new Window { Content = view, ShowInTaskbar = false, ShowActivated = false,
            WindowStartupLocation = WindowStartupLocation.Manual, Left = -30000, Top = -30000, Width = 1920, Height = 1080 };
        win.Show(); await vm.CarregarAsync();
        if (vm.Pacientes.Count != 1 || vm.NaoVerificado) throw new Exception("Acompanhamento não carregou o paciente fictício: " + vm.Mensagem);
        await ConferirJanela(win, "acompanhamento-primeira-abertura", [1920]);
        win.Height = 768;
        await ConferirJanela(win, "acompanhamento-lista", [880, 1024, 1366]);
        vm.AbrirFiltrosCommand.Execute(null);
        await ConferirJanela(win, "acompanhamento-filtros", [880, 1024, 1366]);
        await vm.VoltarCommand.ExecuteAsync(null);
        await vm.AbrirCommand.ExecuteAsync(vm.Pacientes[0]);
        await ConferirJanela(win, "acompanhamento-contato", [880, 1024, 1366]);
        if (Descendentes(view).OfType<ComboBox>().Any(c => c.IsVisible && System.Windows.Automation.AutomationProperties.GetName(c).Contains("Responsável")))
            throw new Exception("Contato ainda permite selecionar outra responsável.");
        if (!Descendentes(view).OfType<TextBlock>().Any(t => t.IsVisible && t.Text == usuario.Nome + " (usuário conectado)"))
            throw new Exception("Contato não identifica o usuário conectado.");
        await vm.VoltarCommand.ExecuteAsync(null); vm.ConfigurarCommand.Execute(null);
        var medicoCombo = Descendentes(view).OfType<ComboBox>().Single(c => System.Windows.Automation.AutomationProperties.GetName(c) == "Profissional das indicações BSV");
        medicoCombo.SelectedValue = usuario.ProfissionalId!.Value;
        await vm.SalvarConfiguracaoCommand.ExecuteAsync(null);
        for (var volta = 0; volta < 3; volta++)
        {
            vm.ConfigurarCommand.Execute(null); await Task.Delay(80); win.UpdateLayout();
            if (medicoCombo.SelectedValue is not int id || id != usuario.ProfissionalId || Validation.GetHasError(medicoCombo))
                throw new Exception("Configuração perdeu o médico salvo ao reabrir: " + vm.Mensagem);
            await vm.VoltarCommand.ExecuteAsync(null);
        }
        var antigo = new Paciente { Nome = "Paciente fictício sem retorno", Convenio = Convenio.UnimedPadrao };
        db.Pacientes.Add(antigo); await db.SaveChangesAsync();
        db.Atendimentos.Add(new() { PacienteId = antigo.Id, Modalidade = ModalidadeAtendimento.BsvApenas,
            Data = DateOnly.FromDateTime(DateTime.Today.AddDays(-100)), RealizadoEm = DateTime.Today.AddDays(-100) });
        await db.SaveChangesAsync();
        vm.AtalhoCommand.Execute("atrasados"); vm.DiasRecall = "60";
        await vm.GerarCommand.ExecuteAsync(null);
        if (!vm.Pacientes.Any(p => p.PacienteId == antigo.Id) || vm.Prazo != "Todos os prazos")
            throw new Exception("Buscar pacientes deixou o novo recall oculto pelo filtro anterior: " + vm.Mensagem);
        var vmNovo = sp.GetRequiredService<Clinica.Recepcao.ViewModels.AcompanhamentoViewModel>();
        db.Atendimentos.Add(new() { PacienteId = antigo.Id, Modalidade = ModalidadeAtendimento.BsvComAcupuntura,
            Data = DateOnly.FromDateTime(DateTime.Today.AddDays(-80)), RealizadoEm = DateTime.Today.AddDays(-80) });
        await db.SaveChangesAsync();
        await vmNovo.CarregarAsync();
        if (!vmNovo.Pacientes.Any(p => p.PacienteId == antigo.Id && p.Modalidade == ModalidadeAtendimento.BsvComAcupuntura))
            throw new Exception("Recall não busca pacientes elegíveis automaticamente ao abrir a tela.");
        await ConferirJanela(win, "acompanhamento-busca-recall", [880, 1024, 1366]);
        win.Height = 600;
        await ConferirJanela(win, "acompanhamento-lista-compacta", [880, 1024, 1366]);
        var gradeRecall = Descendentes(win).OfType<DataGrid>().Single(g => g.IsVisible);
        if (gradeRecall.ActualHeight < 130) throw new Exception("Cabeçalho deixou pouco espaço para pacientes.");
        win.Height = 768;
        vm.ConfigurarCommand.Execute(null);
        await ConferirJanela(win, "acompanhamento-configuracao", [880, 1024, 1366]);
        await vm.VoltarCommand.ExecuteAsync(null);
        var exemplo = vm.Pacientes[0];
        for (var i = 0; i < 180; i++) vm.Pacientes.Add(exemplo with { Paciente = $"Paciente fictício de teste {i:000}", Pendente = i % 2 == 0 });
        win.Height = 1080;
        await ConferirJanela(win, "acompanhamento-lista-longa", [1920, 880, 1366]);
        var grade = Descendentes(win).OfType<DataGrid>().Single(g => g.IsVisible);
        grade.ScrollIntoView(vm.Pacientes.Last());
        await ConferirJanela(win, "acompanhamento-lista-rolada", [1366, 880, 1920]);
        win.Close();
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
            new Clinica.Financeiro.Modulo.ModuloFinanceiro(), new Clinica.Faturamento.Modulo.ModuloFaturamento(), new Clinica.Gerente.Modulo.ModuloGerente()], sp);
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
        var estoqueVm = sp.GetRequiredService<Clinica.Financeiro.ViewModels.EstoqueViewModel>();
        await estoqueVm.CarregarCommand.ExecuteAsync(null);
        if (estoqueVm.ModoMateriaisSelecionado != ModoMateriais.Desativado) throw new Exception("Materiais começaram ativados.");
        var estoqueView = new Clinica.Financeiro.Views.EstoqueView { DataContext = estoqueVm };
        var estoqueJanela = new Window { Content = estoqueView, Height = 750 };
        await ConferirJanela(estoqueJanela, "estoque-itens", [960, 1366]);
        Descendentes(estoqueView).OfType<TabControl>().Single().SelectedItem = Descendentes(estoqueView).OfType<TabItem>()
            .Single(t => Equals(t.Header, "Materiais dos atendimentos"));
        await ConferirJanela(estoqueJanela, "materiais-desativados", [960, 1366]);
        estoqueVm.ModoMateriaisSelecionado = ModoMateriais.Gestao;
        await estoqueVm.SalvarModoMateriaisCommand.ExecuteAsync(null);
        using (var scope = sp.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ClinicaDbContext>();
            var fim = PoliticaMateriaisService.Agora.AddSeconds(1);
            var atendimento = new Atendimento { PacienteId = paciente.Id, Data = hoje };
            var horario = new Agendamento { PacienteId = paciente.Id, DataHora = fim.AddMinutes(-30),
                Atendimento = atendimento, Status = StatusAgendamento.Realizado, FimAtendimentoEm = fim };
            db.Add(horario); await db.SaveChangesAsync();
            await scope.ServiceProvider.GetRequiredService<EstoqueService>().RegistrarMateriaisAsync(horario.Id, usuario.Id, new([new(item.Id, 5)]));
        }
        await estoqueVm.CarregarMateriaisCommand.ExecuteAsync(null);
        if (estoqueVm.AtendimentosMateriais.Single().Situacao != "Baixa pendente") throw new Exception("Pendência de saldo invisível.");
        await ConferirJanela(estoqueJanela, "materiais-acompanhamento", [960, 1366]); estoqueJanela.Close();
        var pagamentos = sp.GetRequiredService<Clinica.Recepcao.ViewModels.PagamentosViewModel>();
        var view = new Clinica.Recepcao.Views.PagamentosView { DataContext = pagamentos };
        var janela = new Window { Content = view, Width = 960, Height = 650 };
        await ConferirJanela(janela, "pagamentos-busca", [620, 960]);
        pagamentos.Seletor.SelecionarGarantindoNaLista(paciente);
        await pagamentos.CarregarAsync();
        if (pagamentos.Linhas.Count != 2 || pagamentos.NaoVerificado) throw new Exception("Consulta de pagamentos falhou.");
        await ConferirJanela(janela, "pagamentos-lista", [620, 960, 1366]);
        janela.Close();
        var receber = new Clinica.Desktop.Shell.Componentes.ReceberPagamentoViewModel(escopos, pendente)
            { Forma = FormaPagamento.CartaoCredito, Adquirente = "Maquininha", Bandeira = "Visa", Parcelas = "3" };
        var receberJanela = new Clinica.Desktop.Shell.Componentes.RecebimentoWindow(receber);
        await ConferirJanela(receberJanela, "receber-cartao", [480, 600]); receberJanela.Close();
        var baixar = new Clinica.Desktop.Shell.Componentes.RecebimentoWindow(new Clinica.Financeiro.ViewModels.BaixarLancamentoViewModel(escopos, pendente)
            { Forma = FormaPagamento.CartaoCredito });
        await ConferirJanela(baixar, "financeiro-baixa", [420, 560]); baixar.Close();
        var compra = new Clinica.Financeiro.Janelas.MovimentoEstoqueWindow(new Clinica.Financeiro.ViewModels.MovimentoEstoqueViewModel(escopos, item.Id, item.Nome)
            { GerarContaCompra = true, CompraPaga = true, Fornecedor = "Fornecedor demonstrativo", Quantidade = "100", CustoUnitario = "2,50", Lote = "L-2026" });
        await ConferirJanela(compra, "estoque-compra", [480, 560]); compra.Close();
        var cadastroItem = new Clinica.Financeiro.Janelas.ItemEstoqueWindow(new Clinica.Financeiro.ViewModels.ItemEstoqueEdicaoViewModel(escopos, item.Id));
        await ConferirJanela(cadastroItem, "estoque-catalogo", [460, 600]); cadastroItem.Close();
        var contaNova = new Clinica.Financeiro.Janelas.ContaWindow(new Clinica.Financeiro.ViewModels.ContaEdicaoViewModel(escopos)
            { Descricao = "Compra de materiais", Contraparte = "Fornecedor", DocumentoReferencia = "NF-123", Valor = "100", QuantidadeParcelas = "3" });
        await ConferirJanela(contaNova, "contas-parcelamento", [440, 600]); contaNova.Close();
        var materiaisVm = new MateriaisProcedimentoViewModel([new MaterialProcedimentoLinha { ItemId = item.Id, Nome = "Material de procedimento com apresentação e nome compridos", Saldo = "Disponível: 10,125 un" }]);
        await materiaisVm.ConfirmarCommand.ExecuteAsync(null);
        if (materiaisVm.Pedido != null || string.IsNullOrWhiteSpace(materiaisVm.Erro)) throw new Exception("Consumo vazio passou sem declaração.");
        materiaisVm.Itens[0].Quantidade = "1,125";
        materiaisVm.Itens[0].Lote = "L-2026";
        materiaisVm.Busca = "não encontrado";
        await materiaisVm.ConfirmarCommand.ExecuteAsync(null);
        if (materiaisVm.Pedido?.Materiais.Single().Quantidade != 1.125m) throw new Exception("Filtro apagou material preenchido.");
        materiaisVm.Busca = null;
        var materiaisJanela = new MateriaisProcedimentoWindow(materiaisVm);
        await ConferirJanela(materiaisJanela, "materiais-procedimento", [550, 700]); materiaisJanela.Close();
        var culturaAnterior = System.Globalization.CultureInfo.CurrentCulture;
        try
        {
            foreach (var cultura in new[] { "pt-BR", "en-US" })
            {
                System.Globalization.CultureInfo.CurrentCulture = System.Globalization.CultureInfo.GetCultureInfo(cultura);
                if (!Valores.TentarLerNumeroExato("1,125", out var virgula) || virgula != 1.125m
                    || !Valores.TentarLerNumeroExato("1.125", out var ponto) || ponto != 1.125m)
                    throw new Exception("Quantidade fracionada depende da cultura do Windows.");
            }
        }
        finally { System.Globalization.CultureInfo.CurrentCulture = culturaAnterior; }
        var contasVm = sp.GetRequiredService<Clinica.Financeiro.ViewModels.ContasViewModel>();
        await contasVm.CarregarCommand.ExecuteAsync(null);
        var contasJanela = new Window { Content = new Clinica.Financeiro.Views.ContasView { DataContext = contasVm }, Height = 700 };
        await ConferirJanela(contasJanela, "contas-historico", [960, 1366]); contasJanela.Close();
        var caixaVm = sp.GetRequiredService<Clinica.Financeiro.ViewModels.CaixaViewModel>();
        await caixaVm.CarregarCommand.ExecuteAsync(null);
        var caixaJanela = new Window { Content = new Clinica.Financeiro.Views.CaixaView { DataContext = caixaVm }, Height = 700 };
        await ConferirJanela(caixaJanela, "caixa-historico", [960, 1366]); caixaJanela.Close();
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
            {
                if (nome == "acompanhamento-lista-compacta" && grade.ActualHeight < 180)
                    throw new Exception($"Lista de pacientes com altura insuficiente: {largura} {grade.ActualHeight}");
                if (Descendentes(grade).OfType<ScrollViewer>().FirstOrDefault()?.ScrollableWidth > 0.1)
                    throw new Exception($"Tabela ultrapassa a tela: {nome} {largura}");
            }
            foreach (var botao in Descendentes(janela).OfType<Button>().Where(b => b.IsVisible && b.Content is string))
            {
                if (nome.StartsWith("acompanhamento-") && botao.Content is "Registrar contato" or "Ver acompanhamento")
                {
                    var texto = new FormattedText((string)botao.Content, System.Globalization.CultureInfo.CurrentUICulture,
                        botao.FlowDirection, new Typeface(botao.FontFamily, botao.FontStyle, botao.FontWeight, botao.FontStretch),
                        botao.FontSize, Brushes.Black, VisualTreeHelper.GetDpi(botao).PixelsPerDip);
                    if (botao.ActualWidth + 1 < texto.WidthIncludingTrailingWhitespace + botao.Padding.Left + botao.Padding.Right)
                        throw new Exception($"Texto do botão de contato cortado: {nome} {largura} {botao.ActualWidth}");
                }
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
sealed class PausaLeituraAgenda : Microsoft.EntityFrameworkCore.Diagnostics.DbCommandInterceptor
{
    bool armada, viuBloqueios;
    public TaskCompletionSource Entrou { get; private set; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public TaskCompletionSource Liberar { get; private set; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public void Armar()
    {
        armada = true; viuBloqueios = false;
        Entrou = new(TaskCreationOptions.RunContinuationsAsynchronously);
        Liberar = new(TaskCreationOptions.RunContinuationsAsynchronously);
    }
    public override async ValueTask<Microsoft.EntityFrameworkCore.Diagnostics.InterceptionResult<System.Data.Common.DbDataReader>> ReaderExecutingAsync(
        System.Data.Common.DbCommand command, Microsoft.EntityFrameworkCore.Diagnostics.CommandEventData eventData,
        Microsoft.EntityFrameworkCore.Diagnostics.InterceptionResult<System.Data.Common.DbDataReader> result, CancellationToken cancellationToken = default)
    {
        if (!armada) return result;
        if (viuBloqueios)
        {
            armada = false; Entrou.TrySetResult();
            await Liberar.Task.WaitAsync(cancellationToken);
        }
        else if (command.CommandText.Contains("\"BloqueiosAgenda\"")) viuBloqueios = true;
        return result;
    }
}
sealed class SnackbarTeste : ISnackbarService
{
    public void Sucesso(string mensagem) { }
    public void Erro(string mensagem) { }
    public void Info(string mensagem) { }
}


