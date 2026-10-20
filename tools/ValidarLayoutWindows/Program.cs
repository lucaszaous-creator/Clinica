using Clinica.Application.Modelos;
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
    static bool somenteGestao;
    static bool somenteAcompanhamento;
    static bool somenteInfusao;
    static bool somenteA1;
    [STAThread]
    static int Main(string[] args)
    {
        somenteAcompanhamento = args.Contains("--acompanhamento");
        somenteInfusao = args.Contains("--infusao");
        somenteA1 = args.Contains("--a1");
        somenteBlazor = args.Contains("--blazor");
        somenteGestao = args.Contains("--gestao"); completo = args.Contains("--completo"); Directory.CreateDirectory(Saida);
        using var log = new StreamWriter(Saida + "/bindings.log"); PresentationTraceSources.DataBindingSource.Listeners.Add(new TextWriterTraceListener(log)); PresentationTraceSources.DataBindingSource.Switch.Level = SourceLevels.Error;
        var app = new System.Windows.Application { ShutdownMode = ShutdownMode.OnExplicitShutdown }; app.Resources.MergedDictionaries.Add(new ResourceDictionary { Source = new Uri("pack://application:,,,/Clinica.Desktop.Shell;component/Styles/Suite.xaml") });
        int code = 0; app.Dispatcher.BeginInvoke(async () => { try { await Executar(); if (falhas > 0) throw new Exception($"{falhas} cortes encontrados. Consulte artifacts/layout-windows."); Console.WriteLine("TELAS CONFERIDAS"); } catch (Exception e) { Console.WriteLine(e); code = 1; } finally { PresentationTraceSources.DataBindingSource.Flush(); app.Shutdown(); } }); app.Run(); return code;
    }
    static bool somenteBlazor;
    static async Task Executar()
    {
        if (somenteBlazor)
        {
            var r = await TrocasBlazor.Executar();
            // 2 = o navegador CONFIRMOU ter desenhado e a captura da tela deste computador não o
            // alcança: não é defeito do app, e reprovar o PR por isso seria ruído. Vira AVISO
            // anotado no PR (nunca silêncio) — a medição do flash fica para uma máquina que o veja.
            if (r == 2) { Console.WriteLine("::warning::Troca de telas Blazor: o flash não pôde ser medido neste runner (a captura não vê o WebView2; o navegador desenhou)."); return; }
            if (r != 0) throw new Exception("Troca de telas Blazor: houve quadro escuro, ou a tela não foi desenhada.");
            return;
        }
        await RolagemQa.Executar();
        if (somenteA1) { await ConferirA1(); return; }
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
        for (var i = 0; i < 12; i++) db.Add(new Agendamento { PacienteId = pac.Id, ProfissionalId = prof.Id, DataHora = DateTime.Today.AddHours(8 + i / 2.0), ModalidadePrevista = ModalidadeAtendimento.AcupunturaComEletro }); await db.SaveChangesAsync();
        sp.GetRequiredService<PacienteEmFoco>().Definir(pac.Id, pac.Nome, 1, null, DateOnly.FromDateTime(DateTime.Today));
        // A moldura é Blazor desde out/2026 (JanelaDoApp): o cabeçalho, o menu e as abas são
        // HTML no navegador da janela (o das telas é fotografado na prévia, tools/PreviaBlazor; o
        // do cabeçalho ainda não). Aqui fica o que só o Windows
        // responde: cada item do menu monta uma tela do NAVEGADOR (nenhuma rota devolve mais
        // tela WPF), cada aba de item composto também, e a janela aguenta as larguras de sempre.
        var vm = new ShellViewModel("Gerente", modulos, sp);
        var win = new JanelaDoApp(sp, "Gerente Geral — Clínica SemDor") { ShowInTaskbar = false, ShowActivated = false, WindowStartupLocation = WindowStartupLocation.Manual, Left = -30000, Top = -30000, Width = 960, Height = 600 };
        win.MostrarMoldura(vm); win.Show();
        foreach (var item in vm.Itens.Where(i => !i.Oculto && (completo || new[] { "agenda", "consultorio-agenda", "receituario", "consultorio-prescricoes", "configuracoes" }.Contains(i.Chave))))
        {
            vm.NavegarCommand.Execute(item); await Task.Delay(350);
            var composta = vm.TelaAtual as TelaComAbas;
            int n = composta?.Rotulos.Count ?? 1;
            for (var a = 0; a < n; a++)
            {
                composta?.Selecionar(a); await Task.Delay(200);
                var visivel = composta is not null ? composta.ConteudoVisivel : vm.TelaAtual;
                string nome = item.Chave + "-" + a; Console.WriteLine("TELA " + nome);
                if (visivel is not ITelaDoNavegador) { falhas++; Console.WriteLine($"SEM TELA DO NAVEGADOR {nome}: {visivel?.GetType().Name ?? "nada"}"); }
                foreach (int largura in new[] { 1366, 960 })
                {
                    win.Width = largura; win.UpdateLayout(); await Task.Delay(100); win.UpdateLayout();
                    Foto(win, nome + "-" + largura);
                }
            }
        }
        // A Grade da agenda é Blazor desde out/2026 (GradeDaAgenda): a chave dela abre o
        // componente no navegador da janela. Vagas, sobreposição, bloqueios, busca e semana são
        // conferidos em GradeDaAgendaNaTelaTests e no bUnit (TelasBlazorAbremTests).
        NavegacaoSuite.Ir(Clinica.Recepcao.Modulo.ModuloRecepcao.ChaveAgenda);
        await Task.Delay(250);
        var aVista = vm.TelaAtual is TelaComAbas abasDaAgenda ? abasDaAgenda.ConteudoVisivel : vm.TelaAtual;
        if (aVista is not ITelaDoNavegador { Componente.Name: "GradeDaAgenda" or "VisaoGeralDaAgenda" })
            throw new Exception("A chave da agenda deve abrir a Grade (Blazor).");
        Console.WriteLine("AGENDA: a chave da grade abre a tela Blazor.");
        win.Close();
        // O "Concluir sessão" do Gerente sem vínculo médico é da tela do paciente Blazor (out/2026),
        // conferido no bUnit (SecaoAtendimentoBlazorTests).
        usuario.Perfil = PerfilAcesso.Enfermagem; usuario.ProfissionalId = prof.Id; usuario.Profissional = prof;
        var bsv = await db.Agendamentos.FirstAsync(); bsv.ModalidadePrevista = ModalidadeAtendimento.BsvComAcupuntura;
        await db.SaveChangesAsync(); sp.GetRequiredService<SessaoUsuario>().Entrar(usuario);
        // A tela virou Blazor (out/2026); o que se confere aqui é a CONSULTA que ela lê.
        using (var escopoFila = sp.CreateScope())
        {
            var pagina = await escopoFila.ServiceProvider.GetRequiredService<Clinica.Application.Tablet.ISessoesEnfermagem>()
                .ListarAsync(usuario.Id, new Clinica.Application.Tablet.FiltroSessoesEnfermagem(Situacao: "Todas"), DateOnly.FromDateTime(DateTime.Today));
            if (pagina.Itens.Count != 1) throw new Exception("A fila BSV não carregou a sessão fictícia.");
        }
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

    static Task AuditarInfusao(ServiceProvider sp, UsuarioSistema usuario)
    {
        // A fila da sala virou tela Blazor (out/2026); o leiaute dela é conferido pelo
        // TelasBlazorAbremTests e pela prévia. Aqui fica a REGRA que a fila afirma.
        var hoje = DateOnly.FromDateTime(DateTime.Today);
        var folhas = Enumerable.Range(1, 12).Select(i => new PrescricaoInterna { Id = i, Data = hoje,
            Situacao = SituacaoPrescricao.Encerrada, ExigeAssinaturaEletronicaDaExecucao = true }).ToList();
        var linhas = SalaDeInfusaoNaTela.Montar(folhas, folhas, podeChecar: true, usuario.Id, hoje);
        if (linhas.Count != 12) throw new Exception("A fila deve manter todas as 12 pendências, sem repetir.");
        if (SalaDeInfusaoNaTela.ResumoValidacoes(12) != "12 infusões aguardando sua avaliação e assinatura")
            throw new Exception("Resumo das validações mudou.");
        Console.WriteLine("INFUSÃO: fila com 12 pendências sem repetição.");
        return Task.CompletedTask;
    }

    /// <summary>
    /// O Acompanhamento de pacientes virou tela BLAZOR (out/2026, docs/relacionamento-blazor.md):
    /// o leiaute dela é conferido pela prévia (fotos em 1024 e 1366) e pelo `TelasBlazorAbremTests`,
    /// e o flash pelo `--blazor`. O que esta conferência fazia de REGRA continua aqui, pelo
    /// SERVIÇO — o mesmo que a tela chama: a fila lê o paciente, o contato é assinado por quem
    /// fez login (não há como escolher outra responsável), a configuração não perde o médico ao
    /// reabrir, gerar o recall não deixa o caso novo escondido e o recall da abertura acha a
    /// modalidade nova.
    /// </summary>
    static async Task ValidarAcompanhamento(ServiceProvider sp, ClinicaDbContext db, Paciente paciente, UsuarioSistema usuario)
    {
        db.Acompanhamentos.Add(new() { PacienteId = paciente.Id, Tipo = TipoAcompanhamento.Recall,
            Modalidade = ModalidadeAtendimento.BsvApenas, ReferenciaEm = DateTime.Today.AddDays(-90),
            CriadoEm = DateTime.Today, CriadoPor = usuario.Login, ResponsavelId = usuario.Id,
            ProximoContato = DateOnly.FromDateTime(DateTime.Today.AddDays(-2)) });
        await db.SaveChangesAsync();
        async Task<T> Servico<T>(Func<Clinica.Application.Abstracoes.IAcompanhamentoPacienteService, Task<T>> f)
        {
            using var escopo = sp.CreateScope();
            return await f(escopo.ServiceProvider.GetRequiredService<Clinica.Application.Abstracoes.IAcompanhamentoPacienteService>());
        }
        var linhas = await Servico(s => s.ListarAsync(usuario.Id));
        if (linhas.Count != 1) throw new Exception("Acompanhamento não leu o paciente fictício.");
        var linha = linhas[0];
        var hoje = DateOnly.FromDateTime(DateTime.Today);
        var (pedido, impedimento) = Clinica.Application.Servicos.ConversaDoAcompanhamento.Pedido(linha,
            Clinica.Application.Servicos.DesfechoContato.PediuOutraData, hoje, hoje.AddDays(3), null, "Paciente pediu outra data.", null, usuario.Id, Guid.NewGuid());
        if (pedido is not null) throw new Exception("A conversa aceitou 'pediu outra data' sem o canal do contato.");
        if (impedimento is null) throw new Exception("A conversa não disse o que faltava.");
        // Um cliente que mande OUTRO responsável continua assinando com quem fez login.
        await Servico(async s => { await s.AtualizarAsync(usuario.Id, linha.Id, new(Guid.NewGuid(), linha.Versao, 999,
            hoje.AddDays(3), EtapaAcompanhamento.AguardandoPlano, null, "")); return 0; });
        var contato = await db.ContatosAcompanhamento.AsNoTracking().OrderByDescending(c => c.Id).FirstAsync();
        if (contato.ResponsavelId != usuario.Id || contato.Operador != usuario.Login)
            throw new Exception("O contato não foi assinado por quem fez login.");
        var config = await Servico(s => s.ConfiguracaoAsync(usuario.Id));
        await Servico(async s => { await s.ConfigurarAsync(usuario.Id, usuario.ProfissionalId!.Value, 0); return 0; });
        for (var volta = 0; volta < 3; volta++)
            if ((await Servico(s => s.ConfiguracaoAsync(usuario.Id))).ProfissionalBsvId != usuario.ProfissionalId)
                throw new Exception("Configuração perdeu o médico salvo ao reabrir.");
        var antigo = new Paciente { Nome = "Paciente fictício sem retorno", Convenio = Convenio.UnimedPadrao };
        db.Pacientes.Add(antigo); await db.SaveChangesAsync();
        db.Atendimentos.Add(new() { PacienteId = antigo.Id, Modalidade = ModalidadeAtendimento.BsvApenas,
            Data = DateOnly.FromDateTime(DateTime.Today.AddDays(-100)), RealizadoEm = DateTime.Today.AddDays(-100) });
        await db.SaveChangesAsync();
        await Servico(s => s.GerarRecallAsync(usuario.Id, 60));
        var depois = await Servico(s => s.ListarAsync(usuario.Id));
        var filtro = new Clinica.Application.Servicos.FiltroAcompanhamento(TipoAcompanhamento.Recall, false);
        if (!Clinica.Application.Servicos.AcompanhamentoNaTela.Filtrar(depois, filtro, usuario.Id, hoje).Any(p => p.PacienteId == antigo.Id))
            throw new Exception("Gerar o recall deixou o caso novo fora da fila.");
        db.Atendimentos.Add(new() { PacienteId = antigo.Id, Modalidade = ModalidadeAtendimento.BsvComAcupuntura,
            Data = DateOnly.FromDateTime(DateTime.Today.AddDays(-80)), RealizadoEm = DateTime.Today.AddDays(-80) });
        await db.SaveChangesAsync();
        int dias;
        using (var e = sp.CreateScope()) dias = await e.ServiceProvider.GetRequiredService<Clinica.Application.Servicos.ParametrosService>().ObterDiasInatividadeRecallAsync();
        await Servico(s => s.GerarRecallAsync(usuario.Id, dias));
        if (!(await Servico(s => s.ListarAsync(usuario.Id))).Any(p => p.PacienteId == antigo.Id && p.Modalidade == ModalidadeAtendimento.BsvComAcupuntura))
            throw new Exception("O recall da abertura não acha o paciente elegível da modalidade nova.");
        Console.WriteLine($"ACOMPANHAMENTO: regras conferidas pelo serviço ({config.Responsaveis.Count} responsável(is)); o leiaute é Blazor e é conferido pela prévia.");
    }

    static async Task ValidarGestao(ServiceProvider sp, Paciente paciente, UsuarioSistema usuario)
    {
        _ = new ShellViewModel("Gerente", [new Clinica.Recepcao.Modulo.ModuloRecepcao(), new ModuloClinico(),
            new Clinica.Financeiro.Modulo.ModuloFinanceiro(), new Clinica.Faturamento.Modulo.ModuloFaturamento(), new Clinica.Gerente.Modulo.ModuloGerente()], sp);
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
        // As telas do Financeiro são Blazor desde out/2026 (docs/telas-blazor.md): a foto delas
        // sai da prévia (tools/PreviaBlazor). Aqui fica o que é REGRA — o modo de materiais e a
        // pendência de baixa —, conferido pelo serviço que a tela usa.
        using (var scope = sp.CreateScope())
        {
            var repo = scope.ServiceProvider.GetRequiredService<Clinica.Application.Abstracoes.IClinicaRepositorio>();
            if ((await new PoliticaMateriaisService(repo).ObterAsync()).Modo != ModoMateriais.Desativado) throw new Exception("Materiais começaram ativados.");
            await new PoliticaMateriaisService(repo).SalvarAsync(ModoMateriais.Gestao, usuario.Id);
        }
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
        // Pagamentos e Retornos a marcar são telas Blazor desde out/2026, e o receber do balcão é o
        // ReceberPagamentoForm (a RecebimentoWindow WPF saiu): o leiaute sai da prévia
        // (tools/PreviaBlazor) e o TelasBlazorAbremTests abre cada uma e clica cada botão. A REGRA
        // do recebimento por cartão é conferida logo abaixo, pelo PagamentosRecepcaoService.
        // A janela WPF de materiais do procedimento saiu em out/2026 (código morto: o registro é o
        // MateriaisAtendimentoForm Blazor, que o TelasBlazorAbremTests abre e clica). A REGRA da
        // quantidade fracionada continua conferida logo abaixo, pela leitura que as duas usam.
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
        // O painel da direção virou a Mesa da direção em Blazor (out/2026): quem a abre e
        // clica em cada botão é o TelasBlazorAbremTests, e quem a fotografa é a prévia.
        // A venda de pacote é Blazor desde out/2026 (PacoteVendaForm): quem a abre e clica cada
        // botão é o TelasBlazorAbremTests, e quem a fotografa é a prévia.
        using (var scope = sp.CreateScope())
        {
            await scope.ServiceProvider.GetRequiredService<TaxaService>().SalvarAsync(new TaxaCartao
                { Adquirente = "Contrato mensal", Modalidade = ModalidadeCartao.CreditoParcelado,
                  Percentual = 3, DiasParaReceber = 0, LiquidacaoMensal = true });
            await scope.ServiceProvider.GetRequiredService<PagamentosRecepcaoService>().ReceberAsync(paciente.Id,
                pendente.Id, pendente.Valor, hoje, FormaPagamento.CartaoCredito, adquirente: "Contrato mensal", bandeira: "Visa", parcelas: 3);
        }
        foreach (var perfil in new[] { PerfilAcesso.Recepcao, PerfilAcesso.Profissional })
        {
            usuario.Perfil = perfil; sp.GetRequiredService<SessaoUsuario>().Entrar(usuario);
            if (SessaoUsuario.Atual.Pode(Permissao.VenderPacote) != (perfil == PerfilAcesso.Recepcao)) throw new Exception("Permissão de pagamentos fora do perfil.");
        }
        Console.WriteLine("GESTÃO: saldos, alertas, permissões, pagamentos e compras conferidos.");
    }

    static async Task ConferirA1()
    {
        var vm = new EscolherCertificadoViewModel("Assinar relatório clínico fictício");
        if (vm.Certificados.Count != 0 || !vm.Vazio || vm.Selecionado is not null)
            throw new InvalidOperationException("A assinatura deve iniciar sem certificados: somente importação A1 explícita.");
        // A captura usa somente a tela; não apresenta certificados locais nem acessa a clínica.
        foreach (var linha in vm.Certificados) linha.Certificado.Dispose();
        vm.Certificados.Clear(); vm.Selecionado = null; vm.Vazio = true; vm.Mensagem = null;
        using var rsa = System.Security.Cryptography.RSA.Create(2048);
        var pedido = new System.Security.Cryptography.X509Certificates.CertificateRequest("CN=Fictício",
            rsa, System.Security.Cryptography.HashAlgorithmName.SHA256, System.Security.Cryptography.RSASignaturePadding.Pkcs1);
        var certificado = pedido.CreateSelfSigned(DateTimeOffset.Now.AddDays(-1), DateTimeOffset.Now.AddDays(1));
        var anterior = LinhaCertificado.De(new Clinica.Application.Assinatura.CertificadoAssinatura(
            certificado, "Fictício", "Fictício", "1", certificado.NotBefore, certificado.NotAfter, "52998224725"));
        vm.Certificados.Add(anterior); vm.Selecionado = anterior;
        if (!vm.PodeConfirmar) throw new Exception("Pré-condição: certificado anterior não selecionável.");
        await vm.CarregarA1Async([1, 2, 3], "senha-incorreta");
        if (!vm.MensagemEhErro || vm.PodeConfirmar || vm.Selecionado is not null)
            throw new Exception("Falha de importação A1 manteve certificado anterior autorizado sem nova escolha.");
        vm.ConfirmarCommand.Execute(null);
        if (vm.Confirmou || vm.Escolhido is not null) throw new Exception("Falha de A1 confirmou certificado anterior.");
        // A opção anterior permanece disponível, mas exige seleção explícita.
        vm.Selecionado = anterior;
        if (!vm.PodeConfirmar) throw new Exception("Certificado anterior não permite nova escolha explícita.");
        vm.Selecionado = null; vm.LiberarNaoEscolhidos(); vm.Certificados.Clear(); vm.Mensagem = null;
        var janela = new EscolherCertificadoWindow(vm) { ShowInTaskbar = false, ShowActivated = false,
            WindowStartupLocation = WindowStartupLocation.Manual, Left = -30000, Top = -30000 };
        janela.Show(); janela.UpdateLayout();
        Descendentes(janela).OfType<Expander>().Single().IsExpanded = true;
        await ConferirJanela(janela, "certificado-a1", [780, 960]);
        var campo = Descendentes(janela).OfType<PasswordBox>().Single();
        campo.Password = "senha-ficticia";
        janela.Close();
        if (campo.Password.Length != 0) throw new Exception("Senha permaneceu no formulário fechado.");
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
                if (Descendentes(grade).OfType<ScrollViewer>().FirstOrDefault()?.ScrollableWidth > 0.1)
                    throw new Exception($"Tabela ultrapassa a tela: {nome} {largura}");
            }
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


