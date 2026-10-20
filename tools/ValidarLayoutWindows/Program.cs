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
    [STAThread]
    static int Main(string[] args)
    {
        somenteAcompanhamento = args.Contains("--acompanhamento");
        somenteInfusao = args.Contains("--infusao");
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
                    }
                    if (largura == 1366 || largura == 960) Foto(win, nome);
                }
            }
        }
        // A Grade da agenda é Blazor desde out/2026 (GradeDaAgenda): a chave dela abre o
        // componente hospedado. Vagas, sobreposição, bloqueios, busca e semana são conferidos
        // em GradeDaAgendaNaTelaTests e no bUnit (TelasBlazorAbremTests).
        NavegacaoSuite.Ir(Clinica.Recepcao.Modulo.ModuloRecepcao.ChaveAgenda);
        await Task.Delay(250);
        if (!Descendentes(win).OfType<Clinica.Desktop.Blazor.TelaBlazor>()
                .Any(t => t.Componente.Name is "GradeDaAgenda" or "VisaoGeralDaAgenda"))
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



    static Task ConferirProgressoInfusao(ServiceProvider sp, Paciente paciente, Profissional medico)
    {
        // A folha de execução e o editor da prescrição de infusão viraram FORMULÁRIOS BLAZOR
        // (out/2026 — autorização da direção de 19/10/2026, só a camada de tela): o leiaute e os
        // botões deles são conferidos pelo TelasBlazorAbremTests (a Sala de infusão abre a folha) e
        // o estado pela Application (FolhaDeExecucaoNaTela, PrescricaoDeInfusaoEmEdicao), com teste.
        // Aqui fica a REGRA que a fila afirma sobre a folha encerrada e assinada.
        var encerrada = new PrescricaoInterna { Situacao = SituacaoPrescricao.Encerrada,
            ExigeAssinaturaEletronicaDaExecucao = true };
        encerrada.Assinaturas.Add(new AssinaturaDocumento { Papel = PapelAssinatura.Executante, ArquivoId = 1 });
        var linhaEncerrada = LinhaSalaInfusao.De(encerrada, DateOnly.FromDateTime(DateTime.Today));
        if (linhaEncerrada.RegistroPendente || linhaEncerrada.AguardaAssinatura)
            throw new Exception("Documento principal assinado continua aparecendo como pendência no desktop.");
        Console.WriteLine("INFUSÃO: folha encerrada e assinada não aparece como pendência.");
        return Task.CompletedTask;
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
        // Pagamentos e Retornos a marcar são telas Blazor desde out/2026: o leiaute sai da prévia
        // (tools/PreviaBlazor) e o TelasBlazorAbremTests abre cada uma e clica cada botão.
        var receber = new Clinica.Desktop.Shell.Componentes.ReceberPagamentoViewModel(escopos, pendente)
            { Forma = FormaPagamento.CartaoCredito, Adquirente = "Maquininha", Bandeira = "Visa", Parcelas = "3" };
        var receberJanela = new Clinica.Desktop.Shell.Componentes.RecebimentoWindow(receber);
        await ConferirJanela(receberJanela, "receber-cartao", [480, 600]); receberJanela.Close();
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


