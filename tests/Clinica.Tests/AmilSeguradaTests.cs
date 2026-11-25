using System.Text.RegularExpressions;
using AngleSharp.Dom;
using Bunit;
using Clinica.Application;
using Clinica.Application.Modelos;
using Clinica.Application.Servicos;
using Clinica.Domain;
using Clinica.Domain.Entities;
using Clinica.Infrastructure;
using Clinica.Ui.Blazor.Componentes;
using Clinica.Ui.Blazor.Hospedagem;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Clinica.Tests;

/// <summary>
/// A AMIL SOBE SEGURADA (nov/2026): a Amil desativou o token e só aceita QR Code, que existe só no
/// portal. Até a resposta dela, o #363 vai para a main com a Amil SÓ na elegibilidade: o envio de guias
/// (<see cref="OperadoraTiss.EnvioDeGuiaNasTelas"/> + a chave <c>envio</c>) e a autorização pelo sistema
/// (a chave <c>autorizacao</c>) nascem DESLIGADOS — e, desligados, a baixa de uma guia Amil e a aba Dia
/// de um paciente Amil são as de antes do #363. Dados fictícios; nenhuma rede.
/// </summary>
public sealed class AmilSeguradaTests : IDisposable
{
    private const string Amil = "Amil";
    private readonly TissBancoDeTeste _b = new();

    public void Dispose() => _b.Dispose();

    // ==================== 1. os dois interruptores ====================

    [Fact]
    public async Task Os_dois_interruptores_da_Amil_nascem_desligados_em_banco_novo_e_em_banco_ja_configurado()
    {
        OperadorasTiss.Amil.EnvioDeGuiaNasTelas.Should().BeFalse("a baixa da Amil não pede a guia ANS nem oferece o envio");
        GuiaAnsNaBaixa.Exige(TipoCodigo.Consulta, Amil, Convenio.Amil).Should().BeFalse();
        GuiaAnsNaBaixa.Exige(TipoCodigo.Acupuntura, Amil, Convenio.Amil).Should().BeFalse();

        // Banco novo.
        var credenciais = _b.Credenciais();
        (await credenciais.EnvioLigadoAsync(Amil)).Should().BeFalse();
        (await credenciais.AutorizacaoLigadaAsync(Amil)).Should().BeFalse();

        // Banco já configurado como o de produção: Petrobras com envio ligado, Amil com credencial,
        // endereço e código — nada disso liga a Amil.
        await _b.PrestadorAsync();
        await credenciais.SalvarAsync(TissBancoDeTeste.Petrobras, "000111", "senha-ficticia", TissBancoDeTeste.Operador);
        await credenciais.DefinirEnvioAsync(TissBancoDeTeste.Petrobras, true, TissBancoDeTeste.Operador);
        await credenciais.SalvarAsync(Amil, "000123", "senha-ficticia", TissBancoDeTeste.Operador);
        await credenciais.DefinirEnderecoAsync(Amil, TipoTransacaoTiss.Elegibilidade, "https://exemplo.invalid/eleg", TissBancoDeTeste.Operador);
        var relida = _b.Credenciais(new ClinicaRepositorio(_b.NovoContexto()));
        (await relida.EnvioLigadoAsync(TissBancoDeTeste.Petrobras)).Should().BeTrue();
        (await relida.EnvioLigadoAsync(Amil)).Should().BeFalse();
        (await relida.AutorizacaoLigadaAsync(Amil)).Should().BeFalse();
        (await _b.NovoContexto().Configuracoes.AsNoTracking().Select(c => c.Chave).ToListAsync())
            .Should().NotContain(c => c.StartsWith("TissAutorizacao:") || c == CredenciaisTissService.ChaveEnvio(Amil));
    }

    [Fact]
    public void As_migrations_do_363_so_acrescentam_colunas_anulaveis_e_uma_tabela_sem_dado()
    {
        var pasta = Path.Combine(Raiz(), "src", "Clinica.Infrastructure", "Migrations");
        string[] doPr = ["20261117004935_GuiaSpSadtDaAmil.cs", "20261117182816_EquipeEConfirmacaoDaAmil.cs", "20261117191102_AutorizacaoTissDaAmil.cs"];
        foreach (var nome in doPr)
        {
            var up = Regex.Match(File.ReadAllText(Path.Combine(pasta, nome)), @"void Up\(.*?void Down\(", RegexOptions.Singleline).Value;
            up.Should().NotBeEmpty(nome);
            // Nenhum dado é escrito nem mudado: nem SQL solto, nem InsertData/UpdateData/DeleteData.
            up.Should().NotContainAny(["migrationBuilder.Sql(", "InsertData(", "UpdateData(", "DeleteData(", "DropColumn(", "AlterColumn(", "RenameColumn(", "DropTable("], nome);
            // Toda coluna acrescentada a uma tabela que já existe é anulável (a versão em produção não a escreve).
            foreach (Match m in Regex.Matches(up, @"AddColumn<[^>]+>\((?<corpo>.*?)\);", RegexOptions.Singleline))
                m.Groups["corpo"].Value.Should().Contain("nullable: true", $"{nome}: {m.Value}");
        }
    }

    // ==================== 2. a baixa de uma guia Amil ====================

    [Fact]
    public async Task Com_tudo_desligado_a_guia_Amil_baixa_so_com_o_numero_do_portal_sem_recusa_nova()
    {
        // Atendida há 120 dias (passou dos 90 da Amil) e repetida no mesmo dia, com o mesmo procedimento.
        var ha120 = DateOnly.FromDateTime(_b.Agora).AddDays(-120);
        var primeira = await GuiaAmilAsync(ha120);
        var repetida = await GuiaAmilAsync(ha120);

        var proposta = await new GuiaAnsNaBaixaService(_b.Repo, new ParametrosService(_b.Repo, _b.Protecao), new PrecoConvenioService(_b.Repo), _b.Credenciais())
            .PrepararAsync(primeira);
        proposta.Exige.Should().BeFalse();
        proposta.Dados.Should().BeNull();
        proposta.Avisos.Should().BeEmpty("nada de prazo, janela, CPF, token ou SP/SADT");
        proposta.AlertaDePrazo.Should().BeNull();

        var faturamento = new FaturamentoService(new ClinicaRepositorio(_b.NovoContexto()));
        await faturamento.DarBaixaAsync(primeira, DateOnly.FromDateTime(_b.Agora), "AMX778899", "secretaria", null);
        await new FaturamentoService(new ClinicaRepositorio(_b.NovoContexto()))
            .DarBaixaAsync(repetida, DateOnly.FromDateTime(_b.Agora), "AMX778900", "secretaria", null);

        await using var db = _b.NovoContexto();
        var baixadas = await db.Codigos.AsNoTracking().Include(c => c.GuiaAns).Where(c => c.Id == primeira || c.Id == repetida).ToListAsync();
        baixadas.Should().OnlyContain(c => c.Status == StatusCodigo.Baixado && c.GuiaAns == null);
        baixadas.Select(c => c.NumeroGuiaReal).Should().BeEquivalentTo(["AMX778899", "AMX778900"]);
        (await db.GuiasTissConsulta.CountAsync()).Should().Be(0, "a baixa manual não cria guia ANS");
        (await db.TransacoesTiss.CountAsync()).Should().Be(0);
        (await db.Auditoria.AsNoTracking().Where(a => a.CodigoId == primeira).Select(a => a.Acao).ToListAsync()).Should().Equal("BaixaGuia");
    }

    // ==================== 3. a agenda de um paciente Amil ====================

    [Fact]
    public async Task Com_a_autorizacao_desligada_chegou_cancelamento_e_falta_nao_enfileiram_nada()
    {
        await _b.PrestadorAsync();
        await _b.Credenciais().SalvarAsync(Amil, "000123", "senha-ficticia", TissBancoDeTeste.Operador);
        var chegou = await HorarioAmilAsync();
        var cancelado = await HorarioAmilAsync();
        var faltou = await HorarioAmilAsync();

        await Agenda().RegistrarChegadaAsync(chegou.Id, TissBancoDeTeste.Operador);
        await Agenda().CancelarAsync(cancelado.Id, TissBancoDeTeste.Operador);
        await Agenda().MarcarFaltaAsync(faltou.Id, TissBancoDeTeste.Operador);

        await using var db = _b.NovoContexto();
        (await db.TransacoesTiss.CountAsync()).Should().Be(0);
        (await db.AutorizacoesTiss.CountAsync()).Should().Be(0);
        (await db.Auditoria.AsNoTracking().Select(a => a.Acao).ToListAsync())
            .Should().NotContain(a => a.Contains("Autorizacao"));

        // E a leitura da aba Dia não traz nada: sem selo, sem campo do token, sem botão, sem aviso.
        var horarios = await db.Agendamentos.AsNoTracking().Include(a => a.Paciente).ToListAsync();
        var daAgenda = await Servico().LerDaAgendaAsync(horarios, DateOnly.FromDateTime(_b.Agora));
        daAgenda.HorariosQuePedem.Should().BeEmpty();
        daAgenda.PorHorario.Should().BeEmpty();
        daAgenda.AvisoDeToken.Should().BeNull();
    }

    // ==================== peças ====================

    private ParametrosService Parametros(ClinicaRepositorio repo) => new(repo, _b.Protecao);

    private AutorizacaoTissService Servico(ClinicaRepositorio? repo = null)
    {
        repo ??= new ClinicaRepositorio(_b.NovoContexto());
        return new AutorizacaoTissService(repo, Parametros(repo), _b.Credenciais(repo), () => _b.Agora);
    }

    private AgendaService Agenda()
    {
        var repo = new ClinicaRepositorio(_b.NovoContexto());
        return new AgendaService(repo, new AtendimentoService(repo), Parametros(repo), Servico(repo));
    }

    private async Task<Agendamento> HorarioAmilAsync()
    {
        var profissional = new Profissional
        {
            Nome = "Dra. Segurada Ficticia", NomeCurto = "Dra. S " + Guid.NewGuid().ToString("N")[..6], ConselhoProfissional = "06",
            NumeroConselho = "520001", UfConselho = "33", Cbos = "225105", Ativo = true
        };
        var paciente = new Paciente { Nome = "Paciente Amil Segurada", Convenio = Convenio.Amil, ConvenioCodigo = Amil, Carteirinha = TissBancoDeTeste.CarteiraFicticia };
        _b.Db.AddRange(profissional, paciente);
        await _b.Db.SaveChangesAsync();
        var ag = new Agendamento
        {
            PacienteId = paciente.Id, ProfissionalId = profissional.Id, DataHora = _b.Agora.AddMinutes(30), DuracaoMinutos = 30,
            ModalidadePrevista = ModalidadeAtendimento.Consulta, ModalidadeCodigo = nameof(ModalidadeAtendimento.Consulta),
            CriadoPor = "recepcao", CriadoEm = _b.Agora.AddDays(-3)
        };
        _b.Db.Add(ag);
        await _b.Db.SaveChangesAsync();
        return ag;
    }

    private async Task<int> GuiaAmilAsync(DateOnly data)
    {
        var paciente = new Paciente { Nome = "Paciente Amil Baixa", Convenio = Convenio.Amil, ConvenioCodigo = Amil, Carteirinha = TissBancoDeTeste.CarteiraFicticia };
        _b.Db.Add(paciente);
        await _b.Db.SaveChangesAsync();
        var atendimento = new Atendimento { PacienteId = paciente.Id, Data = data, Modalidade = ModalidadeAtendimento.Consulta };
        _b.Db.Add(atendimento);
        await _b.Db.SaveChangesAsync();
        var guia = new CodigoFaturamento { AtendimentoId = atendimento.Id, Tipo = TipoCodigo.Consulta, Ordem = OrdemCodigo.Primeiro, DataPrevistaFaturamento = data };
        _b.Db.Add(guia);
        await _b.Db.SaveChangesAsync();
        return guia.Id;
    }

    private static string Raiz()
    {
        var d = new DirectoryInfo(AppContext.BaseDirectory);
        while (d is not null && !File.Exists(Path.Combine(d.FullName, "Clinica.sln"))) d = d.Parent;
        return d?.FullName ?? throw new InvalidOperationException("Clinica.sln não encontrado.");
    }
}

/// <summary>
/// A MESMA prova nas TELAS, clicando (bUnit) sobre a semente da prévia — que liga a autorização da Amil
/// para fotografá-la; aqui ela é DESLIGADA no banco desta classe, como estará na clínica.
/// </summary>
[Collection(ColecaoDiagnostico.Nome)]
public sealed class AmilSeguradaNasTelasTests : IClassFixture<BancoDasTelas>, IDisposable
{
    private static readonly TimeSpan Prazo = TimeSpan.FromSeconds(30);
    private readonly BancoDasTelas _banco;

    public AmilSeguradaNasTelasTests(BancoDasTelas banco)
    {
        _banco = banco;
        var ptBr = new System.Globalization.CultureInfo("pt-BR");
        System.Globalization.CultureInfo.CurrentCulture = ptBr;
        System.Globalization.CultureInfo.CurrentUICulture = ptBr;
        new SessaoUsuario().Entrar(banco.Gerente);
        using var db = Db();
        new CredenciaisTissService(new ClinicaRepositorio(db)).DefinirAutorizacaoAsync("Amil", false, "teste").GetAwaiter().GetResult();
        new CredenciaisTissService(new ClinicaRepositorio(db)).DefinirEnvioAsync("Amil", false, "teste").GetAwaiter().GetResult();
    }

    public void Dispose() => new SessaoUsuario().Entrar(new UsuarioSistema());

    [Fact]
    public void A_baixa_de_uma_guia_Amil_no_painel_e_a_manual_de_sempre_so_com_o_numero()
    {
        var marca = $"A{Guid.NewGuid():N}"[..10];
        var codigoId = LancarAmil(marca);
        using var ctx = Contexto();
        var tela = Abrir(ctx, typeof(Clinica.Ui.Blazor.Telas.PendenciasFaturamento));
        Esperar(() => tela.FindAll("tr").Any(tr => tr.TextContent.Contains(marca)));
        var linha = tela.FindAll("tr").First(tr => tr.TextContent.Contains(marca));
        Clicar(tela, linha.QuerySelectorAll("button").First(b => b.TextContent.Trim() == "Dar baixa"));
        Esperar(() => tela.FindAll("input.numero-guia").Count == 1);

        // Nada do que o #363 acrescentou para a Amil: nem guia ANS, nem SP/SADT, nem envio, nem prazo.
        Assert.Empty(tela.FindAll("[data-guia-ans]"));
        Assert.Empty(tela.FindAll("[data-guia-sp-sadt]"));
        Assert.Empty(tela.FindAll("[data-enviar-e-baixar]"));
        Assert.Empty(tela.FindAll("[data-conferir-para-envio]"));
        Assert.DoesNotContain("Token", tela.Markup);
        Assert.DoesNotContain("90 dias", tela.Markup);

        tela.Find("input.numero-guia").Input("AMX445566");
        Assentar(tela);
        Clicar(tela, tela.FindAll(".modal-rodape button.primario").Last());
        ConfirmarDialogos(tela);
        Esperar(() => Baixada(codigoId) is not null);

        using var db = Db();
        var codigo = db.Codigos.AsNoTracking().Include(c => c.GuiaAns).Single(c => c.Id == codigoId);
        Assert.Equal(StatusCodigo.Baixado, codigo.Status);
        Assert.Equal("AMX445566", codigo.NumeroGuiaReal);
        Assert.Null(codigo.GuiaAns);
    }

    [Fact]
    public void A_aba_Dia_com_a_autorizacao_desligada_nao_mostra_selo_token_nem_botao_e_o_Chegou_nao_enfileira()
    {
        using var ctx = Contexto();
        var tela = Abrir(ctx, typeof(Clinica.Ui.Blazor.Telas.AgendaDoDia));
        Esperar(() => tela.FindAll("tr").Any(tr => tr.TextContent.Contains("Otávio Brandão Reis")));

        // Pacientes Amil na agenda de hoje, inclusive os que a semente deixou com autorização gravada.
        Assert.Contains("Gilda Ferraz Montes", tela.Markup);
        Assert.Empty(tela.FindAll("[data-autorizacao-da-linha]"));
        Assert.Empty(tela.FindAll("[data-aviso-token-faltando]"));
        Assert.Empty(tela.FindAll("[data-pedir-autorizacao]"));
        Assert.DoesNotContain("Pedir autorização", tela.Markup);
        Assert.DoesNotContain("Faltou o token", tela.Markup);
        Assert.DoesNotContain("peça o token", tela.Markup);

        int Transacoes() { using var db = Db(); return db.TransacoesTiss.Count(t => t.Tipo == TipoTransacaoTiss.Autorizacao || t.Tipo == TipoTransacaoTiss.CancelaAutorizacao); }
        int Autorizacoes() { using var db = Db(); return db.AutorizacoesTiss.Count(); }
        var (transacoesAntes, autorizacoesAntes) = (Transacoes(), Autorizacoes());

        var linha = tela.FindAll("tr").First(tr => tr.TextContent.Contains("Otávio Brandão Reis"));
        Clicar(tela, linha.QuerySelectorAll("button").First(b => b.TextContent.Trim() == "Chegou"));
        ConfirmarDialogos(tela);
        Esperar(() => { using var db = Db(); return db.Agendamentos.Any(a => a.Paciente!.Nome == "Otávio Brandão Reis" && a.ChegadaEm != null); });

        Assert.Equal(transacoesAntes, Transacoes());
        Assert.Equal(autorizacoesAntes, Autorizacoes());
        Assert.Empty(tela.FindAll("[data-autorizacao-da-linha]"));
    }

    // ==================== peças (as do BaixaQueEnviaBlazorTests) ====================

    private int LancarAmil(string marca)
    {
        using var db = Db();
        var repo = new ClinicaRepositorio(db);
        var hoje = DateOnly.FromDateTime(DateTime.Today);
        var paciente = new Paciente
        {
            Nome = $"Amil {marca}", Convenio = Convenio.Amil, ConvenioCodigo = "Amil", Carteirinha = "0007779999",
            ValidadeCarteirinha = hoje.AddDays(120), Sexo = Sexo.Feminino
        };
        db.Add(paciente);
        db.SaveChanges();
        return new AtendimentoService(repo).LancarAsync(paciente.Id, hoje, ModalidadeAtendimento.Consulta).GetAwaiter().GetResult()
            .Atendimento.Codigos.OrderBy(c => c.Ordem).First().Id;
    }

    private CodigoFaturamento? Baixada(int codigoId)
    {
        using var db = Db();
        return db.Codigos.AsNoTracking().SingleOrDefault(c => c.Id == codigoId && c.Status == StatusCodigo.Baixado);
    }

    private ClinicaDbContext Db() => new(new DbContextOptionsBuilder<ClinicaDbContext>().UseSqlite(_banco.Conexao).Options);

    private TestContext Contexto()
    {
        var ctx = new TestContext();
        ctx.JSInterop.Mode = JSRuntimeMode.Loose;
        ctx.Services.AddClinica("Host=teste;Database=teste");
        foreach (var d in ctx.Services.Where(d =>
                     d.ServiceType == typeof(DbContextOptions<ClinicaDbContext>) ||
                     d.ServiceType == typeof(DbContextOptions) ||
                     d.ServiceType == typeof(ClinicaDbContext)).ToList())
            ctx.Services.Remove(d);
        ctx.Services.AddDbContext<ClinicaDbContext>(o => o.UseSqlite(_banco.Conexao));
        ctx.Services.AddTelasBlazor();
        ctx.Services.AddSingleton<IHostDaTela>(new HostMudo());
        using var escopo = ctx.Services.GetRequiredService<IServiceScopeFactory>().CreateScope();
        escopo.ServiceProvider.GetRequiredService<ConvenioCatalogoService>().RecarregarCacheAsync().GetAwaiter().GetResult();
        escopo.ServiceProvider.GetRequiredService<ModalidadeCatalogoService>().RecarregarCacheAsync().GetAwaiter().GetResult();
        escopo.ServiceProvider.GetRequiredService<EspecialidadeCatalogoService>().RecarregarCacheAsync().GetAwaiter().GetResult();
        return ctx;
    }

    private static IRenderedComponent<Raiz> Abrir(TestContext ctx, Type tela)
    {
        var r = ctx.RenderComponent<Raiz>(p => p.Add(x => x.Tela, tela));
        Assentar(r);
        return r;
    }

    private static void Clicar(IRenderedComponent<Raiz> tela, IElement botao)
    {
        botao.Click();
        Assentar(tela);
    }

    private static void ConfirmarDialogos(IRenderedComponent<Raiz> tela)
    {
        for (var i = 0; i < 6; i++)
        {
            var dialogo = tela.FindAll("[role=alertdialog] .modal-rodape button").LastOrDefault();
            if (dialogo is null) return;
            dialogo.Click();
            Assentar(tela);
        }
    }

    private static void Assentar(IRenderedComponent<Raiz> tela)
    {
        var prazo = DateTime.UtcNow + Prazo;
        var anterior = string.Empty;
        var estavel = 0;
        while (DateTime.UtcNow < prazo)
        {
            Thread.Sleep(50);
            var agora = tela.Markup;
            estavel = agora == anterior ? estavel + 1 : 0;
            anterior = agora;
            if (estavel >= 3 && !agora.Contains("class=\"esqueleto\"")) return;
        }
    }

    private static void Esperar(Func<bool> condicao)
    {
        var prazo = DateTime.UtcNow + Prazo;
        while (DateTime.UtcNow < prazo) { if (condicao()) return; Thread.Sleep(50); }
        throw new TimeoutException("A condição não aconteceu a tempo.");
    }

    private sealed class HostMudo : IHostDaTela
    {
        public Task<string?> ExportarCsvAsync(IReadOnlyList<string> cabecalho, IReadOnlyList<IReadOnlyList<string>> linhas, string nomeArquivo) => Task.FromResult<string?>(null);
        public Task<string?> SalvarEAbrirAsync(byte[] conteudo, string nomeSugerido, string filtro, string extensao) => Task.FromResult<string?>(null);
        public Task<ArquivoEscolhido?> EscolherArquivoAsync(string filtro, string titulo) => Task.FromResult<ArquivoEscolhido?>(null);
        public Task<bool> CopiarAsync(string texto) => Task.FromResult(true);
        public Task<bool> NavegarAsync(string chave) => Task.FromResult(false);
        public Task<bool> AbrirEnderecoAsync(string url) => Task.FromResult(false);
        public Task<bool> AbrirFichaDoPacienteAsync(int pacienteId) => Task.FromResult(false);
        public Task<string?> PerguntarOndeSalvarAsync(string n, string f, string e) => Task.FromResult<string?>(null);
        public Task<string?> PerguntarPastaAsync(string t) => Task.FromResult<string?>(null);
        public Task<string?> GravarArquivoAsync(string c, byte[] b) => Task.FromResult<string?>(null);
    }
}
