using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using System.Windows.Interop;
using System.Windows.Controls;
using Clinica.Application.Abstracoes;
using Clinica.Application.Servicos;
using Clinica.Clinico.Janelas;
using Clinica.Clinico.ViewModels;
using Clinica.Clinico.Modulo;
using Clinica.Desktop.Controls;
using Clinica.Desktop.Shell.Componentes;
using Clinica.Desktop.Shell.Modulos;
using Clinica.Domain;
using Clinica.Domain.Entities;
using Clinica.Infrastructure;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

static class Program
{
    static readonly string Saida = Path.GetFullPath("artifacts/qa-melhorias-clinicas");
    [STAThread]
    static int Main()
    {
        Directory.CreateDirectory(Saida);
        using var bindings = new StreamWriter(Path.Combine(Saida, "bindings.log"));
        PresentationTraceSources.DataBindingSource.Listeners.Add(new TextWriterTraceListener(bindings));
        PresentationTraceSources.DataBindingSource.Switch.Level = SourceLevels.Error;
        var app = new System.Windows.Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        app.Resources.MergedDictionaries.Add(new ResourceDictionary { Source = new Uri("pack://application:,,,/Clinica.Desktop.Shell;component/Styles/Suite.xaml") });
        var code = 0;
        app.Dispatcher.BeginInvoke(async () =>
        {
            try { await ConferirAsync(); Console.WriteLine("QA OK: cópias editáveis nas telas atuais, paciente, confirmação, formatação e preparo da infusão."); }
            catch (Exception ex) { Console.WriteLine(ex); code = 1; }
            finally { PresentationTraceSources.DataBindingSource.Flush(); app.Shutdown(); }
        });
        app.Run();
        return code;
    }

    static void Exigir(bool valor, string erro) { if (!valor) throw new InvalidOperationException(erro); }

    static async Task ConferirAsync()
    {
        using var conn = new SqliteConnection("DataSource=:memory:");
        conn.Open();
        var options = new DbContextOptionsBuilder<ClinicaDbContext>().UseSqlite(conn).Options;
        var dialogo = new DialogoTeste();
        var services = new ServiceCollection();
        // O último registro substitui a fábrica da aplicação. Toda operação usa somente
        // o SQLite em memória criado acima; a suíte de produção não é iniciada.
        services.AddClinica("Host=127.0.0.1;Database=nao_usado;Username=nao_usado");
        services.AddScoped(_ => new ClinicaDbContext(options));
        services.AddSingleton<IDialogoService>(dialogo);
        services.AddSingleton<ISnackbarService, SnackbarService>();
        new ModuloClinico().Registrar(services);
        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ClinicaDbContext>();
        db.Database.EnsureCreated();
        var paciente = new Paciente { Nome = "Paciente demonstrativo", Convenio = Convenio.UnimedIntercambio };
        var profissional = new Profissional { Nome = "Profissional demonstrativo", RegistroConselho = "CRM-SP 123456" };
        db.AddRange(paciente, profissional);
        await db.SaveChangesAsync();
        var usuario = new UsuarioSistema { Nome = profissional.Nome, Login = "qa.sintetico", Perfil = PerfilAcesso.Profissional,
            ProfissionalId = profissional.Id, Profissional = profissional };
        db.Add(usuario); await db.SaveChangesAsync(); new SessaoUsuario().Entrar(usuario);
        await ConferirCopiasDoPaciente(provider, provider.GetRequiredService<IServiceScopeFactory>(), dialogo, profissional);
    }

    static async Task ConferirCopiasDoPaciente(IServiceProvider provider, IServiceScopeFactory factory,
        DialogoTeste dialogo, Profissional profissional)
    {
        using var scope = factory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ClinicaDbContext>();
        var paciente = new Paciente { Nome = "Paciente da cópia", Convenio = Convenio.UnimedIntercambio };
        var outro = new Paciente { Nome = "Paciente sem histórico", Convenio = Convenio.UnimedIntercambio };
        db.AddRange(paciente, outro);
        await db.SaveChangesAsync();
        var prontuario = scope.ServiceProvider.GetRequiredService<ProntuarioService>();
        var catalogoCampos = scope.ServiceProvider.GetRequiredService<CampoPersonalizadoService>();
        var campoAnterior = await catalogoCampos.SalvarAsync(new CampoPersonalizadoProntuario { Rotulo = "Campo presente no histórico" });
        var campoAusente = await catalogoCampos.SalvarAsync(new CampoPersonalizadoProntuario { Rotulo = "Campo sem resposta anterior" });
        var dia = DateOnly.FromDateTime(DateTime.Today);
        var anterior = await prontuario.SalvarAsync(new Evolucao
        {
            PacienteId = paciente.Id, ProfissionalId = profissional.Id, Data = dia.AddDays(-2),
            QueixaPrincipal = "Queixa anterior", HistoriaDoencaAtual = "História anterior",
            ExameFisico = "Exame anterior", HipoteseDiagnostica = "Hipótese anterior", CidSessao = "M54.5",
            Conduta = "Conduta anterior", TextoEvolucao = "Evolução anterior", TextoEvolucaoFormatado = TextoFormatado.Guardar([new("Evolução anterior", true)]), Orientacoes = "Orientações anteriores",
            PlanoTerapeutico = "Plano anterior", Encaminhamento = "Encaminhamento anterior", EvaAntes = 8, EvaDepois = 5,
            CamposPersonalizados = CampoPersonalizadoService.Montar([campoAnterior, campoAusente],
                new Dictionary<int, string?> { [campoAnterior.Id] = "Resposta anterior" }).ToList()
        });
        var foco = new PacienteEmFoco();
        // Os 84 históricos alheios são mais recentes que o do paciente em foco.
        // Um homônimo impede que uma seleção acidental pelo nome passe despercebida.
        var concorrentes = Enumerable.Range(1, 84).Select(n => new Paciente
        {
            Nome = n == 1 ? paciente.Nome : $"Paciente concorrente {n:00}",
            Convenio = Convenio.UnimedIntercambio
        }).ToArray();
        db.Pacientes.AddRange(concorrentes);
        await db.SaveChangesAsync();
        for (var n = 0; n < concorrentes.Length; n++)
        {
            var texto = $"Evolução exclusiva concorrente {n + 1:00}";
            db.Evolucoes.Add(new Evolucao
            {
                PacienteId = concorrentes[n].Id, ProfissionalId = profissional.Id, Data = dia.AddDays(-1),
                TextoEvolucao = texto, TextoEvolucaoFormatado = TextoFormatado.Guardar([new(texto, false, true)]),
                CamposPersonalizados = CampoPersonalizadoService.Montar([campoAnterior, campoAusente],
                    new Dictionary<int, string?> { [campoAnterior.Id] = $"Resposta concorrente {n + 1:00}" }).ToList()
            });
            db.PrescricoesInternas.Add(new PrescricaoInterna
            {
                PacienteId = concorrentes[n].Id, ProfissionalId = profissional.Id,
                Numero = $"QA84/{n + 1:000}", CodigoVerificacao = $"QA84{n + 1:000}",
                Data = dia, Hora = new TimeOnly(23, 59), Situacao = SituacaoPrescricao.Liberada,
                LiberadaSemAssinaturaEm = dia.ToDateTime(new TimeOnly(23, 59)),
                Indicacao = $"Indicação concorrente {n + 1:00}",
                Itens = [new ItemPrescricaoInterna { Ordem = 1, GrupoInfusao = 1,
                    Descricao = $"Item exclusivo concorrente {n + 1:00}", Dose = $"Dose concorrente {n + 1:00}" }]
            });
        }
        await db.SaveChangesAsync();
        Exigir(await db.Evolucoes.CountAsync(e => e.PacienteId != paciente.Id) == 84
            && await db.PrescricoesInternas.CountAsync(p => p.PacienteId != paciente.Id) == 84,
            "O cenário precisa conter exatamente 84 históricos alheios.");
        // 85 registros legados da mesma alergia, com três observações diferentes,
        // mais duas alergias distintas: o atendimento deve ter uma única entrada.
        string[] nomesAlergias = ["Substância de demonstração", "Outra substância demonstrativa", "Látex demonstrativo"];
        string[] observacoesAlergia = ["Relato anterior.", "Reação registrada pela enfermagem.", "Informação confirmada pelo paciente."];
        db.ProblemasPaciente.AddRange(Enumerable.Range(0, 85).Select(n => new ProblemaPaciente
        {
            PacienteId = paciente.Id, Natureza = NaturezaProblema.Alergia,
            Descricao = nomesAlergias[0], Observacoes = observacoesAlergia[n % observacoesAlergia.Length]
        }));
        db.ProblemasPaciente.AddRange(nomesAlergias.Skip(1).Select(nome => new ProblemaPaciente
        {
            PacienteId = paciente.Id, Natureza = NaturezaProblema.Alergia, Descricao = nome
        }));
        await db.SaveChangesAsync();
        foco.Definir(paciente.Id, paciente.Nome);
        var atendimento = new AtendimentoViewModel(factory, provider.GetRequiredService<ISnackbarService>(), foco);
        await Esperar(() => !atendimento.Carregando && atendimento.Mapa is not null);
        Exigir(!atendimento.MensagemEhErro, "Carga de atendimento falhou: " + atendimento.Mensagem);
        var alertasAlergia = atendimento.AlertasClinicos.Where(a => a.Texto.StartsWith("ALERGIA", StringComparison.OrdinalIgnoreCase)).ToArray();
        Exigir(alertasAlergia.Length == 1, "Atendimento deve reunir todas as alergias em uma única entrada.");
        var resumoAlergias = alertasAlergia.Single().Texto;
        foreach (var nome in nomesAlergias)
            Exigir(resumoAlergias.Split(nome, StringSplitOptions.None).Length == 2,
                "Alergia ausente ou repetida no resumo: " + nome);
        foreach (var observacao in observacoesAlergia)
            Exigir(alertasAlergia.Single().Detalhes?.Contains(observacao, StringComparison.Ordinal) == true,
                "Agrupamento omitiu observação clínica distinta: " + observacao);
        Exigir(await db.ProblemasPaciente.CountAsync(p => p.PacienteId == paciente.Id && p.Natureza == NaturezaProblema.Alergia) == 87,
            "Agrupar a apresentação removeu registros do histórico.");
        Console.WriteLine("QA ALERGIAS OK: 85 registros repetidos + 2 alergias distintas viraram uma entrada; cada nome aparece uma vez e as três observações foram preservadas.");
        atendimento.TextoEvolucao = "Texto atual preservado";
        atendimento.CamposPersonalizados.Single(c => c.Id == campoAnterior.Id).Resposta = "Resposta atual A";
        atendimento.CamposPersonalizados.Single(c => c.Id == campoAusente.Id).Resposta = "Resposta atual B";
        atendimento.EvaAntes = 3;
        var dataAtual = atendimento.Data;
        var evolucoesAntes = await db.Evolucoes.CountAsync();
        var confirmacoesAntes = dialogo.Confirmacoes;
        atendimento.CopiarUltimaEvolucaoCommand.Execute(null);
        Exigir(dialogo.Confirmacoes == confirmacoesAntes + 1 && atendimento.TextoEvolucao == "Texto atual preservado",
            "Cancelar cópia de evolução não preservou o texto.");
        Exigir(atendimento.CamposPersonalizados.Single(c => c.Id == campoAnterior.Id).Resposta == "Resposta atual A"
            && atendimento.CamposPersonalizados.Single(c => c.Id == campoAusente.Id).Resposta == "Resposta atual B",
            "Cancelar cópia modificou os campos personalizados.");
        dialogo.Confirmacao = true;
        atendimento.CopiarUltimaEvolucaoCommand.Execute(null);
        dialogo.Confirmacao = false;
        Exigir(atendimento.TextoEvolucao == anterior.TextoEvolucao && atendimento.HistoriaDoencaAtual == anterior.HistoriaDoencaAtual
            && atendimento.Encaminhamento == anterior.Encaminhamento && atendimento.PlanoTerapeutico == anterior.PlanoTerapeutico,
            "Cópia de evolução incompleta.");
        Exigir(atendimento.TextoEvolucaoFormatado == anterior.TextoEvolucaoFormatado, "Cópia perdeu negrito/itálico.");
        Exigir(atendimento.CamposPersonalizados.Single(c => c.Id == campoAnterior.Id).Resposta == "Resposta anterior"
            && atendimento.CamposPersonalizados.Single(c => c.Id == campoAusente.Id).Resposta is null,
            "Cópia misturou respostas anteriores e atuais dos campos personalizados.");
        Exigir(atendimento.EvolucaoId == 0 && atendimento.EvaAntes == 3 && atendimento.EvaDepois is null && atendimento.Data == dataAtual,
            "Cópia de evolução transferiu identidade, medidas ou data.");
        Exigir(await db.Evolucoes.CountAsync() == evolucoesAntes, "Copiar evolução gravou sem ação de salvar.");
        await Desenhar(new Window { Content = new Clinica.Clinico.Views.AtendimentoView { DataContext = atendimento } },
            "atendimento-copiar-ultima.png", 1180, 760);
        await Desenhar(new Window { Content = new Clinica.Clinico.Views.AtendimentoView { DataContext = atendimento } },
            "atendimento-notebook.png", 900, 650);
        foco.Definir(concorrentes[0].Id, concorrentes[0].Nome);
        await atendimento.CarregarAsync();
        dialogo.Confirmacao = true;
        atendimento.CopiarUltimaEvolucaoCommand.Execute(null);
        Exigir(atendimento.TextoEvolucao == "Evolução exclusiva concorrente 01"
            && atendimento.CamposPersonalizados.Single(c => c.Id == campoAnterior.Id).Resposta == "Resposta concorrente 01",
            "Trocar para paciente homônimo copiou evolução de outra pessoa.");
        foco.Definir(paciente.Id, paciente.Nome);
        await atendimento.CarregarAsync();
        atendimento.CopiarUltimaEvolucaoCommand.Execute(null);
        Exigir(atendimento.TextoEvolucao == anterior.TextoEvolucao
            && atendimento.TextoEvolucaoFormatado == anterior.TextoEvolucaoFormatado
            && atendimento.CamposPersonalizados.Single(c => c.Id == campoAnterior.Id).Resposta == "Resposta anterior",
            "Voltar ao paciente alvo reutilizou o histórico do homônimo.");
        dialogo.Confirmacao = false;
        foco.Definir(outro.Id, outro.Nome);
        await atendimento.CarregarAsync();
        atendimento.CopiarUltimaEvolucaoCommand.Execute(null);
        Exigir(string.IsNullOrWhiteSpace(atendimento.TextoEvolucao) && atendimento.Mensagem?.Contains("Não há evolução") == true,
            "Cópia de evolução atravessou a troca de paciente.");

        var prescricoes = scope.ServiceProvider.GetRequiredService<PrescricaoInternaService>();
        var anteriorInfusao = await prescricoes.CriarAsync(paciente.Id, profissional.Id);
        await prescricoes.SalvarRascunhoAsync(anteriorInfusao.Id, "Indicação anterior", "Orientações anteriores",
            [new ItemPrescricaoInterna { Descricao = "Item demonstrativo anterior", Dose = "Dose demonstrativa",
                Diluente = "Diluente demonstrativo", TempoInfusao = "45 minutos", HoraPrevista = new TimeOnly(8, 30) }]);
        // Exercita a liberação utilizada na tela atual, somente neste banco em memória.
        var repositorio = scope.ServiceProvider.GetRequiredService<IClinicaRepositorio>();
        await repositorio.SalvarConfiguracaoAsync(ContinuidadeSemAssinatura.Configuracao, "true");
        await repositorio.SalvarAsync();
        await prescricoes.LiberarSemAssinaturaAsync(anteriorInfusao.Id, SessaoUsuario.Atual.UsuarioId, confirmouAlergia: true);
        anteriorInfusao.Data = dia.AddDays(-1);
        await db.SaveChangesAsync();
        Exigir(anteriorInfusao.Situacao == SituacaoPrescricao.Liberada && anteriorInfusao.AssinadaEm is null,
            "O cenário não exerceu a liberação sem assinatura.");
        var prescricoesAntes = await db.PrescricoesInternas.CountAsync();
        var infusao = new PrescricaoInternaEdicaoViewModel(factory, dialogo, paciente.Id, paciente.Nome, profissional.Id);
        await Esperar(() => !infusao.Ocupado);
        Exigir(infusao.RotuloLiberacao == "Liberar para enfermagem", "O editor não carregou o modo atual de continuidade.");
        var alertasAlergiaInfusao = infusao.Alertas.Where(a => a.StartsWith("ALERGIA", StringComparison.OrdinalIgnoreCase)).ToArray();
        Exigir(alertasAlergiaInfusao.Length == 1, "A infusão deve reunir todas as alergias em uma única entrada.");
        foreach (var nome in nomesAlergias)
            Exigir(alertasAlergiaInfusao.Single().Split(nome, StringSplitOptions.None).Length == 2,
                "Alergia ausente ou repetida no resumo da infusão: " + nome);
        Console.WriteLine("QA ALERGIAS INFUSÃO OK: uma entrada com os três nomes sem repetição, entre 87 registros legados.");
        infusao.Itens[0].Descricao = "Texto de infusão atual preservado";
        confirmacoesAntes = dialogo.Confirmacoes;
        await infusao.CopiarUltimaPrescricaoCommand.ExecuteAsync(null);
        Exigir(dialogo.Confirmacoes == confirmacoesAntes + 1 && infusao.Itens[0].Descricao == "Texto de infusão atual preservado",
            "Cancelar cópia de infusão não preservou o texto.");
        dialogo.Confirmacao = true;
        await infusao.CopiarUltimaPrescricaoCommand.ExecuteAsync(null);
        dialogo.Confirmacao = false;
        var item = infusao.Infusoes.Single().Preparar().Single();
        Exigir(item.Descricao == "Item demonstrativo anterior" && item.Dose == "Dose demonstrativa"
            && item.Diluente == "Diluente demonstrativo" && item.TempoInfusao == "45 minutos" && item.Id == 0 && item.HoraPrevista is null && item.Checagens.Count == 0,
            "Cópia de infusão perdeu conteúdo ou transferiu identidade/execução.");
        Exigir(await db.PrescricoesInternas.CountAsync() == prescricoesAntes, "Copiar infusão criou prescrição sem salvar.");
        await Desenhar(new PrescricaoInternaWindow(infusao), "infusao-copiar-ultima.png", 1180, 720);
        await Desenhar(new PrescricaoInternaWindow(infusao), "infusao-notebook.png", 900, 600);
        var alertasOriginais = infusao.Alertas.ToArray();
        infusao.Alertas.Clear();
        for (var n = 1; n <= 85; n++) infusao.Alertas.Add($"Alerta clínico fictício {n:00}: informação do paciente para conferência.");
        infusao.TemAlertas = true;
        await Desenhar(new PrescricaoInternaWindow(infusao), "infusao-85-alertas-notebook.png", 900, 600);
        infusao.Alertas.Clear();
        foreach (var alerta in alertasOriginais) infusao.Alertas.Add(alerta);
        infusao.TemAlertas = infusao.Alertas.Count > 0;
        var homonimo = new PrescricaoInternaEdicaoViewModel(factory, dialogo, concorrentes[0].Id, concorrentes[0].Nome, profissional.Id);
        await Esperar(() => !homonimo.Ocupado);
        await homonimo.CopiarUltimaPrescricaoCommand.ExecuteAsync(null);
        Exigir(homonimo.Infusoes.Single().Preparar().Single().Descricao == "Item exclusivo concorrente 01",
            "Editor do paciente homônimo copiou prescrição do paciente anterior.");
        dialogo.Confirmacao = true;
        await infusao.CopiarUltimaPrescricaoCommand.ExecuteAsync(null);
        dialogo.Confirmacao = false;
        Exigir(infusao.Infusoes.Single().Preparar().Single().Descricao == "Item demonstrativo anterior",
            "Recopiar no editor original usou o paciente do outro editor.");
        var semHistorico = new PrescricaoInternaEdicaoViewModel(factory, dialogo, outro.Id, outro.Nome, profissional.Id);
        await Esperar(() => !semHistorico.Ocupado);
        await semHistorico.CopiarUltimaPrescricaoCommand.ExecuteAsync(null);
        Exigir(string.IsNullOrWhiteSpace(semHistorico.Itens.Single().Descricao)
            && semHistorico.Mensagem?.Contains("Não há prescrição") == true, "Infusão copiou histórico de outro paciente.");
        semHistorico.Mensagem = null;
        await Desenhar(new PrescricaoInternaWindow(semHistorico), "infusao-atual-ampliada.png", 1918, 1030);
        Exigir(await db.Evolucoes.CountAsync() == evolucoesAntes
            && await db.PrescricoesInternas.CountAsync() == prescricoesAntes,
            "Alternar os pacientes e copiar gravou novos registros sem salvar.");
        Console.WriteLine("QA 84 OK: evolução e prescrição corretas entre 84 históricos alheios mais recentes, homônimo, retorno ao paciente original e paciente sem histórico.");
        Console.WriteLine("QA CÓPIAS OK: confirmação, cancelamento, paciente, conteúdo editável e ausência de gravação automática.");
    }

    static async Task Esperar(Func<bool> condicao)
    {
        for (var i = 0; i < 500 && !condicao(); i++) await Task.Delay(20);
        Exigir(condicao(), "Tempo excedido ao carregar a tela.");
    }

    static async Task Desenhar(Window window, string nome, int largura, int altura)
    {
        window.WindowStartupLocation = WindowStartupLocation.Manual;
        window.Left = -30000; window.Top = -30000;
        window.ShowInTaskbar = false; window.ShowActivated = false;
        window.WindowStyle = WindowStyle.None; window.ResizeMode = ResizeMode.NoResize;
        window.Width = largura; window.Height = altura;
        window.Show();
        try
        {
            window.UpdateLayout();
            await Task.Delay(350);
            if (nome.StartsWith("atendimento-", StringComparison.Ordinal))
            {
                var vm = (AtendimentoViewModel)((FrameworkElement)window.Content).DataContext;
                var alertas = Descendentes<ItemsControl>(window).Single(i => ReferenceEquals(i.ItemsSource, vm.AlertasClinicos));
                var textosAlergia = Descendentes<TextBlock>(alertas)
                    .Where(t => t.IsVisible && t.Text.StartsWith("ALERGIA", StringComparison.OrdinalIgnoreCase)).ToArray();
                Exigir(textosAlergia.Length == 1 && textosAlergia[0].IsVisible,
                    "A tela desenhou mais de uma entrada de alergias ou escondeu o resumo.");
                Exigir(textosAlergia[0].ActualHeight < window.ActualHeight / 4,
                    "Os 85 registros repetidos criaram um bloco vertical excessivo na tela.");
                var limites = textosAlergia[0].TransformToAncestor(window).TransformBounds(new Rect(textosAlergia[0].RenderSize));
                Exigir(limites.Top >= 0 && limites.Bottom <= window.ActualHeight,
                    "Resumo das alergias ficou fora da área visível inicial.");
                var detalhes = Descendentes<Expander>(alertas).Single(e => e.DataContext is LinhaAlertaClinico a
                    && a.Texto.StartsWith("ALERGIA", StringComparison.OrdinalIgnoreCase));
                Exigir(detalhes.IsVisible && !detalhes.IsExpanded,
                    "Os detalhes da alergia devem estar disponíveis e começar recolhidos.");
                detalhes.IsExpanded = true;
                window.UpdateLayout();
                var textoDetalhes = ((LinhaAlertaClinico)detalhes.DataContext).Detalhes;
                Exigir(Descendentes<TextBlock>(detalhes).Any(t => t.IsVisible && t.Text == textoDetalhes),
                    "Expandir detalhes não exibiu as observações preservadas.");
                detalhes.IsExpanded = false;
                window.UpdateLayout();
                Console.WriteLine($"QA VISUAL ALERGIAS OK: uma entrada em {largura}x{altura}, altura {textosAlergia[0].ActualHeight:0.0}px, sem repetição vertical.");
            }
            if (nome == "infusao-85-alertas-notebook.png")
            {
                var vm = (PrescricaoInternaEdicaoViewModel)window.DataContext;
                var alertas = Descendentes<ScrollViewer>(window).Single(s => s.Content is ItemsControl itens
                    && ReferenceEquals(itens.ItemsSource, vm.Alertas));
                Exigir(alertas.ScrollableHeight > 0 && alertas.ComputedVerticalScrollBarVisibility == Visibility.Visible
                    && alertas.ActualHeight <= 160, "85 alertas não ficaram acessíveis em região limitada com rolagem.");
                var barra = Descendentes<System.Windows.Controls.Primitives.ScrollBar>(alertas)
                    .Single(b => b.Orientation == Orientation.Vertical && b.IsVisible);
                var seta = (System.Windows.Controls.Primitives.RepeatButton)barra.Template.FindName("proximo", barra);
                var comando = (System.Windows.Input.RoutedCommand)seta.Command;
                comando.Execute(seta.CommandParameter, seta.CommandTarget);
                await Task.Delay(50);
                window.UpdateLayout();
                Exigir(alertas.VerticalOffset > 0, "Seta não rolou os alertas da tela real de prescrição.");
                alertas.ScrollToBottom();
                await Task.Delay(50);
                window.UpdateLayout();
                Exigir(Math.Abs(alertas.VerticalOffset - alertas.ScrollableHeight) < 1, "Último dos 85 alertas ficou inacessível.");
                var botoesRodape = Descendentes<Button>(window).Where(b => b.Content is string texto
                    && texto is "Salvar rascunho" or "Liberar para enfermagem").ToArray();
                Exigir(botoesRodape.Length == 2, "Ações esperadas do rodapé não foram encontradas.");
                foreach (var botao in botoesRodape)
                {
                    var limites = botao.TransformToAncestor(window).TransformBounds(new Rect(botao.RenderSize));
                    Exigir(botao.IsVisible && limites.Top >= 0 && limites.Bottom <= window.ActualHeight,
                        "Lista extensa de alertas escondeu ação do rodapé.");
                }
                Console.WriteLine("QA ROLAGEM CLÍNICA OK: seta alcança os 85 alertas e rodapé permanece visível em 900x600.");
            }
            var bmp = new RenderTargetBitmap((int)window.ActualWidth, (int)window.ActualHeight, 96, 96, PixelFormats.Pbgra32);
            bmp.Render(window);
            var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bmp));
            using var arquivo = File.Create(Path.Combine(Saida, nome)); encoder.Save(arquivo);
        }
        finally { window.Close(); }
    }

    static IEnumerable<T> Descendentes<T>(DependencyObject raiz) where T : DependencyObject
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(raiz); i++)
        {
            var filho = VisualTreeHelper.GetChild(raiz, i);
            if (filho is T encontrado) yield return encontrado;
            foreach (var descendente in Descendentes<T>(filho)) yield return descendente;
        }
    }
}

sealed class DialogoTeste : IDialogoService
{
    public int Confirmacoes; public bool Confirmacao;
    public string? PerguntarTexto(string titulo, string pergunta, string? textoInicial = null, bool obrigatorio = true) => null;
    public bool Confirmar(string titulo, string mensagem) => false;
    public bool ConfirmarPerigo(string titulo, string mensagem) { Confirmacoes++; return Confirmacao; }
    public void Aviso(string titulo, string mensagem) { }
}
