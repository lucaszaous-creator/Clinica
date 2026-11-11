using AngleSharp.Dom;
using Bunit;
using Clinica.Application;
using Clinica.Application.Servicos;
using Clinica.Domain;
using Clinica.Domain.Entities;
using Clinica.Infrastructure;
using Clinica.Ui.Blazor.Componentes;
using Clinica.Ui.Blazor.Formularios;
using Clinica.Ui.Blazor.Hospedagem;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Clinica.Tests;

/// <summary>
/// O REGISTRO PARA GUIAS DE CONVÊNIO nas TELAS (nov/2026), clicando de verdade (bUnit, sobre os
/// serviços do app e as sementes da prévia) e afirmando o ESTADO do banco, não a frase:
///
/// 1. o formulário do profissional grava os quatro campos pelo CÓDIGO do padrão (a lista mostra a
///    sigla), não os apaga quando a pessoa mexe em outro campo, e PROPÕE o que leu do registro em
///    texto livre sem gravar nada sozinho;
/// 2. no lote e na rodada do painel, a linha da Saúde Petrobras troca o campo de número pelo
///    botão "Abrir a guia", que abre o MESMO formulário de baixa — a guia já vem com o
///    executante do cadastro — e, baixada ali, a linha fica marcada SEM perder o que estava
///    digitado nas outras;
/// 3. a segunda barreira do botão devolve recusa limpa (o bUnit clica botão apagado).
/// </summary>
[Collection(ColecaoDiagnostico.Nome)]
public sealed class RegistroParaGuiasBlazorTests : IClassFixture<BancoDasTelas>, IDisposable
{
    private static readonly TimeSpan Prazo = TimeSpan.FromSeconds(20);
    private readonly BancoDasTelas _banco;

    public RegistroParaGuiasBlazorTests(BancoDasTelas banco)
    {
        _banco = banco;
        var ptBr = new System.Globalization.CultureInfo("pt-BR");
        System.Globalization.CultureInfo.CurrentCulture = ptBr;
        System.Globalization.CultureInfo.CurrentUICulture = ptBr;
        new SessaoUsuario().Entrar(banco.Gerente);
    }

    public void Dispose() => new SessaoUsuario().Entrar(new UsuarioSistema());

    // ==================== o formulário do profissional ====================

    [Fact]
    public void Profissional_novo_grava_os_quatro_campos_pelo_codigo_e_a_lista_mostra_a_sigla()
    {
        var nome = $"Dra. Registro {Marca()}";
        using var ctx = Contexto();
        var concluiu = false;
        var form = ctx.RenderComponent<ProfissionalForm>(p => p.Add(x => x.AoConcluir, () => concluiu = true));
        Esperar(() => form.FindAll("[data-registro-guias]").Count > 0);

        // A tela mostra a SIGLA e guarda o CÓDIGO: "RJ" na lista, "33" no valor.
        Assert.Equal("RJ", form.Find("#pf-UfConselho option[value='33']").TextContent.Trim());
        Assert.Equal("CRM", form.Find("#pf-ConselhoProfissional option[value='06']").TextContent.Trim());
        Assert.Equal("Médico acupunturista (225105)", form.Find("#pf-Cbos option[value='225105']").TextContent.Trim());

        form.Find("input.campo[maxlength='120']").Change(nome);
        form.Find("#pf-ConselhoProfissional").Change("06");
        form.Find("#pf-NumeroConselho").Input("52.4321");
        form.Find("#pf-UfConselho").Change("33");
        form.Find("#pf-Cbos").Change("225105");
        Assert.Empty(form.FindAll("[data-registro-guias] .campo-erro"));

        Salvar(form);
        Esperar(() => concluiu);

        using var db = Db();
        var p = db.Profissionais.AsNoTracking().Single(x => x.Nome == nome);
        Assert.Equal(("06", "524321", "33", "225105"), (p.ConselhoProfissional, p.NumeroConselho, p.UfConselho, p.Cbos));
    }

    /// <summary>
    /// "Quem não edita, PRESERVA": abrir a ficha, mexer no telefone e salvar devolve os quatro
    /// campos como estavam — e o texto livre do conselho, que vai nos documentos, também.
    /// </summary>
    [Fact]
    public void Editar_outro_campo_nao_apaga_o_registro_para_guias_nem_o_texto_livre()
    {
        var id = Semear(new Profissional
        {
            Nome = $"Dr. Preserva {Marca()}", RegistroConselho = "CRM-RJ 52.1111",
            ConselhoProfissional = "06", NumeroConselho = "521111", UfConselho = "33", Cbos = "225133"
        });
        using var ctx = Contexto();
        var concluiu = false;
        var form = ctx.RenderComponent<ProfissionalForm>(p => p.Add(x => x.ProfissionalId, id).Add(x => x.AoConcluir, () => concluiu = true));
        Esperar(() => form.FindAll("[data-registro-guias]").Count > 0);

        // Abre com o que está gravado, e sem proposta: já há cadastro.
        Assert.Equal("06", form.Find("#pf-ConselhoProfissional").GetAttribute("value"));
        Assert.Equal("521111", form.Find("#pf-NumeroConselho").GetAttribute("value"));
        Assert.Equal("33", form.Find("#pf-UfConselho").GetAttribute("value"));
        Assert.Equal("225133", form.Find("#pf-Cbos").GetAttribute("value"));
        Assert.Empty(form.FindAll("[data-usar-proposta]"));

        form.Find("input[inputmode='tel']").Change("(21) 95555-0101");
        Salvar(form);
        Esperar(() => concluiu);

        using var db = Db();
        var p = db.Profissionais.AsNoTracking().Single(x => x.Id == id);
        Assert.Equal("(21) 95555-0101", p.Telefone);
        Assert.Equal(("06", "521111", "33", "225133"), (p.ConselhoProfissional, p.NumeroConselho, p.UfConselho, p.Cbos));
        Assert.Equal("CRM-RJ 52.1111", p.RegistroConselho);
    }

    /// <summary>
    /// Com o texto livre preenchido e os campos novos em branco, o formulário PROPÕE o que leu —
    /// e não grava sozinho: só entra na tela quando a pessoa aceita, e só vai ao banco no Salvar.
    /// </summary>
    [Fact]
    public void Proposta_do_texto_livre_aparece_so_entra_ao_aceitar_e_so_grava_ao_salvar()
    {
        var id = Semear(new Profissional { Nome = $"Dr. Proposta {Marca()}", RegistroConselho = "CRM-RJ 77.2468" });
        using var ctx = Contexto();
        var concluiu = false;
        var form = ctx.RenderComponent<ProfissionalForm>(p => p.Add(x => x.ProfissionalId, id).Add(x => x.AoConcluir, () => concluiu = true));
        Esperar(() => form.FindAll("[data-usar-proposta]").Count > 0);

        // A proposta está ESCRITA, e os campos continuam em branco.
        Assert.Contains("772468", form.Find("[data-registro-guias] .aviso").TextContent);
        Assert.Equal(string.Empty, form.Find("#pf-ConselhoProfissional").GetAttribute("value") ?? string.Empty);
        Assert.Equal(string.Empty, form.Find("#pf-NumeroConselho").GetAttribute("value") ?? string.Empty);
        using (var antes = Db())
            Assert.Null(antes.Profissionais.AsNoTracking().Single(x => x.Id == id).ConselhoProfissional);

        form.Find("[data-usar-proposta]").Click();
        Esperar(() => form.Find("#pf-NumeroConselho").GetAttribute("value") == "772468");
        Assert.Equal("06", form.Find("#pf-ConselhoProfissional").GetAttribute("value"));
        Assert.Equal("33", form.Find("#pf-UfConselho").GetAttribute("value"));
        // O CBO não está no texto: continua por escolher. E a proposta, aceita, sai da tela.
        Assert.Equal(string.Empty, form.Find("#pf-Cbos").GetAttribute("value") ?? string.Empty);
        Assert.Empty(form.FindAll("[data-usar-proposta]"));
        using (var aindaNao = Db())
            Assert.Null(aindaNao.Profissionais.AsNoTracking().Single(x => x.Id == id).NumeroConselho);

        Salvar(form);
        Esperar(() => concluiu);

        using var db = Db();
        var p = db.Profissionais.AsNoTracking().Single(x => x.Id == id);
        Assert.Equal(("06", "772468", "33", (string?)null), (p.ConselhoProfissional, p.NumeroConselho, p.UfConselho, p.Cbos));
        Assert.Equal("CRM-RJ 77.2468", p.RegistroConselho); // o texto livre não é reescrito
    }

    /// <summary>O número com símbolo é acusado a cada tecla, e o Salvar recusa sem gravar nada.</summary>
    [Fact]
    public void Numero_com_simbolo_e_acusado_no_campo_e_o_salvar_nao_grava()
    {
        var id = Semear(new Profissional { Nome = $"Dra. Recusa {Marca()}", Telefone = "(21) 91111-0000" });
        using var ctx = Contexto();
        var concluiu = false;
        var form = ctx.RenderComponent<ProfissionalForm>(p => p.Add(x => x.ProfissionalId, id).Add(x => x.AoConcluir, () => concluiu = true));
        Esperar(() => form.FindAll("[data-registro-guias]").Count > 0);

        form.Find("#pf-NumeroConselho").Input("12#45");
        Esperar(() => form.FindAll("[data-registro-guias] .campo-erro").Count == 1);
        form.Find("input[inputmode='tel']").Change("(21) 92222-0000");
        Salvar(form);

        Esperar(() => form.Markup.Contains("Registro para guias de convênio: O número no conselho leva só letras e números."));
        Assert.False(concluiu);
        using var db = Db();
        var p = db.Profissionais.AsNoTracking().Single(x => x.Id == id);
        Assert.Equal("(21) 91111-0000", p.Telefone);
        Assert.Null(p.NumeroConselho);
    }

    // ==================== o botão "Abrir a guia" no lote e na rodada ====================

    /// <summary>
    /// LOTE: a linha da Saúde Petrobras não tem campo de número — tem o botão. Ele abre a guia
    /// ANS já com o executante do CADASTRO; a baixa dada ali marca a linha, o número digitado na
    /// outra linha continua onde estava, e o lote conclui com ele.
    /// </summary>
    [Fact]
    public void Lote_abre_a_guia_da_Petrobras_baixa_por_ela_e_nao_perde_o_numero_das_outras_linhas()
    {
        var marca = Marca();
        var (petrobras, comum) = SemearDuasPendencias(marca);
        using var ctx = Contexto();
        var tela = Abrir(ctx, typeof(Clinica.Ui.Blazor.Telas.PendenciasFaturamento));

        MarcarLinha(tela, $"Zuleica {marca}");
        MarcarLinha(tela, $"Zacarias {marca}");
        Clicar(tela, tela.FindAll("button").First(b => b.TextContent.Trim().StartsWith("Baixa em lote", StringComparison.Ordinal)));
        Esperar(() => tela.FindAll("[data-abrir-guia]").Count == 1);

        // Uma linha com o botão (Petrobras), a outra com o campo de número — e só uma de cada.
        Assert.Single(tela.FindAll(".fat-lote input.numero-guia"));
        tela.Find(".fat-lote input.numero-guia").Input("7788991");
        Assentar(tela);

        Clicar(tela, tela.Find($"[data-abrir-guia='{petrobras}']"));
        Esperar(() => tela.FindAll("[data-guia-ans]").Count > 0);
        // O executante vem do CADASTRO do profissional — sem ninguém digitar.
        Assert.Equal("06", tela.Find("#ga-ConselhoProfissional").GetAttribute("value"));
        Assert.Equal("524680", tela.Find("#ga-NumeroConselho").GetAttribute("value"));
        Assert.Equal("33", tela.Find("#ga-UfConselho").GetAttribute("value"));
        Assert.Equal("225105", tela.Find("#ga-Cbos").GetAttribute("value"));

        BaixarPelaGuia(tela, "PB445566");
        Esperar(() => Contar(c => c.Id == petrobras && c.Status == StatusCodigo.Baixado) == 1);

        // De volta ao lote: a linha baixada está marcada, sem botão, e a outra guarda o número.
        Assert.Contains("Baixa registrada", tela.Find(".fat-lote").TextContent);
        Assert.Empty(tela.FindAll("[data-abrir-guia]"));
        Assert.Equal("7788991", tela.Find(".fat-lote input.numero-guia").GetAttribute("value"));
        Assert.Equal(0, Contar(c => c.Id == comum && c.Status == StatusCodigo.Baixado));

        Clicar(tela, tela.FindAll(".modal-rodape button.primario").Last());
        ConfirmarDialogos(tela);
        Esperar(() => Contar(c => c.Id == comum && c.NumeroGuiaReal == "7788991" && c.Status == StatusCodigo.Baixado) == 1);

        using var db = Db();
        var guia = db.GuiasTissConsulta.AsNoTracking().Single(g => g.CodigoId == petrobras);
        Assert.Equal(("06", "524680", "33", "225105"), (guia.ConselhoProfissional, guia.NumeroConselho, guia.UfConselho, guia.Cbos));
        Assert.Equal("PB445566", db.Codigos.AsNoTracking().Single(c => c.Id == petrobras).NumeroGuiaReal);
    }

    /// <summary>
    /// RODADA do painel: o botão no lugar do número, a justificativa ao lado continua. Baixada pela
    /// guia, a linha fica marcada; o que estava digitado em outra linha fica; e FECHAR a rodada
    /// sem concluir não aplica nada do que ficou digitado — nem regrava anotação na guia baixada.
    /// </summary>
    [Fact]
    public void Rodada_abre_a_guia_da_Petrobras_marca_a_linha_e_fechar_nao_aplica_o_resto()
    {
        var marca = Marca();
        var (petrobras, comum) = SemearDuasPendencias(marca);
        using var ctx = Contexto();
        var tela = Abrir(ctx, typeof(Clinica.Ui.Blazor.Telas.PendenciasFaturamento));

        Clicar(tela, "Rodar pendências");
        Esperar(() => tela.FindAll($"[data-abrir-guia='{petrobras}']").Count == 1);

        var linhaPetrobras = LinhaDaRodada(tela, $"Zuleica {marca}");
        Assert.Empty(linhaPetrobras.QuerySelectorAll("input.numero-guia"));   // sem campo de número…
        Assert.Single(linhaPetrobras.QuerySelectorAll("textarea"));            // …e com a justificativa
        LinhaDaRodada(tela, $"Zacarias {marca}").QuerySelector("input.numero-guia")!.Input("7788992");
        Assentar(tela);

        Clicar(tela, tela.Find($"[data-abrir-guia='{petrobras}']"));
        Esperar(() => tela.FindAll("[data-guia-ans]").Count > 0);
        BaixarPelaGuia(tela, "PB445577");
        Esperar(() => Contar(c => c.Id == petrobras && c.Status == StatusCodigo.Baixado) == 1);

        linhaPetrobras = LinhaDaRodada(tela, $"Zuleica {marca}");
        Assert.Contains("Baixa registrada", linhaPetrobras.TextContent);
        Assert.True(linhaPetrobras.QuerySelector("textarea")!.HasAttribute("disabled"));
        Assert.Equal("7788992", LinhaDaRodada(tela, $"Zacarias {marca}").QuerySelector("input.numero-guia")!.GetAttribute("value"));

        // Fechar (o botão virou "Fechar"): a rodada some, e o número digitado NÃO virou baixa.
        Clicar(tela, tela.FindAll(".modal-rodape button").First(b => b.TextContent.Trim() == "Fechar"));
        Esperar(() => tela.FindAll(".fat-rodada").Count == 0);
        Assert.Equal(0, Contar(c => c.Id == comum && c.Status == StatusCodigo.Baixado));
        using var db = Db();
        Assert.Null(db.Codigos.AsNoTracking().Single(c => c.Id == petrobras).ObservacaoPendencia);
    }

    /// <summary>
    /// A SEGUNDA barreira: com o lote aberto, o acesso muda e a pessoa deixa de poder dar baixa.
    /// O clique (que o bUnit faz mesmo no botão apagado) devolve a frase — a guia não abre e
    /// nada estoura no circuito da tela.
    /// </summary>
    [Fact]
    public void Abrir_a_guia_sem_o_acesso_de_baixa_e_recusado_com_a_frase_e_a_guia_nao_abre()
    {
        var marca = Marca();
        var (petrobras, _) = SemearDuasPendencias(marca);
        using var ctx = Contexto();
        var tela = Abrir(ctx, typeof(Clinica.Ui.Blazor.Telas.PendenciasFaturamento));
        MarcarLinha(tela, $"Zuleica {marca}");
        Clicar(tela, tela.FindAll("button").First(b => b.TextContent.Trim().StartsWith("Baixa em lote", StringComparison.Ordinal)));
        Esperar(() => tela.FindAll($"[data-abrir-guia='{petrobras}']").Count == 1);

        var recepcao = new UsuarioSistema { Id = 987654, Nome = "Balcão (teste)", Login = "balcao", Perfil = PerfilAcesso.Recepcao, SenhaHash = "-", SenhaSalt = "-", Ativo = true };
        new SessaoUsuario().Entrar(recepcao);
        Assert.False(SessaoUsuario.Atual.Pode(Permissao.BaixarGuia));
        Assert.Equal("Seu acesso não permite dar baixa na guia. Fale com a direção da clínica.", DecisoesDaRodada.RecusaDeAbrirAGuia());

        tela.Find($"[data-abrir-guia='{petrobras}']").Click();
        Esperar(() => tela.Markup.Contains("Seu acesso não permite dar baixa na guia."));

        Assert.Empty(tela.FindAll("[data-guia-ans]"));
        Assert.Empty(tela.FindAll(".erro-de-tela"));
        Assert.Equal(0, Contar(c => c.Id == petrobras && c.Status == StatusCodigo.Baixado));
    }

    // ==================== apoio ====================

    private static string Marca() => $"R{Guid.NewGuid():N}"[..10];

    private int Semear(Profissional p)
    {
        using var db = Db();
        db.Add(p);
        db.SaveChanges();
        return p.Id;
    }

    /// <summary>
    /// Duas guias pendentes de hoje com a mesma marca: a consulta de uma paciente da Saúde
    /// Petrobras, com horário e profissional de registro COMPLETO no cadastro, e a consulta de um
    /// paciente da Unimed (número só de dígitos). Devolve os ids das duas guias.
    /// </summary>
    private (int Petrobras, int Comum) SemearDuasPendencias(string marca)
    {
        using var db = Db();
        var repo = new ClinicaRepositorio(db);
        var zuleica = new Paciente
        {
            Nome = $"Zuleica {marca}", Convenio = Convenio.Petrobras, ConvenioCodigo = "Petrobras", Carteirinha = "010203040507", Sexo = Sexo.Feminino
        };
        var zacarias = new Paciente
        {
            Nome = $"Zacarias {marca}", Convenio = Convenio.UnimedPadrao, ConvenioCodigo = "UnimedPadrao", Carteirinha = "00320000000000001", Sexo = Sexo.Masculino
        };
        var medica = new Profissional
        {
            Nome = $"Dra. Cadastro {marca}", RegistroConselho = "CRM-RJ 52.4680",
            ConselhoProfissional = "06", NumeroConselho = "524680", UfConselho = "33", Cbos = "225105"
        };
        db.AddRange(zuleica, zacarias, medica);
        db.SaveChanges();

        var hoje = DateOnly.FromDateTime(DateTime.Today);
        var atendimentos = new AtendimentoService(repo);
        var daPetrobras = atendimentos.LancarAsync(zuleica.Id, hoje, ModalidadeAtendimento.Consulta).GetAwaiter().GetResult();
        var daUnimed = atendimentos.LancarAsync(zacarias.Id, hoje, ModalidadeAtendimento.Consulta).GetAwaiter().GetResult();
        db.Add(new Agendamento
        {
            PacienteId = zuleica.Id, DataHora = DateTime.Today.AddHours(6), ModalidadePrevista = ModalidadeAtendimento.Consulta,
            ProfissionalId = medica.Id, AtendimentoId = daPetrobras.Atendimento.Id, Status = StatusAgendamento.Realizado
        });
        db.SaveChanges();
        return (daPetrobras.Atendimento.Codigos.OrderBy(c => c.Ordem).First().Id,
                daUnimed.Atendimento.Codigos.OrderBy(c => c.Ordem).First().Id);
    }

    /// <summary>
    /// Completa a guia ANS aberta (tudo explícito, para o teste não depender do que a semente
    /// cadastrou — menos o EXECUTANTE, que é justamente o que vem do cadastro) e confirma a baixa.
    /// </summary>
    private static void BaixarPelaGuia(IRenderedComponent<Raiz> tela, string numero)
    {
        void Texto(string campo, string valor) { tela.Find($"#ga-{campo}").Input(valor); Assentar(tela); }
        void Escolher(string campo, string valor) { tela.Find($"#ga-{campo}").Change(valor); Assentar(tela); }
        Texto("RegistroAns", "000000");
        Texto("CodigoPrestadorNaOperadora", "00000000000100");
        Texto("Cnes", "9999999");
        Escolher("TipoConsulta", "1");
        Escolher("CodigoTabela", "22");
        Texto("CodigoProcedimento", "10101012");
        Texto("ValorProcedimento", "139,00");
        Assert.DoesNotContain("campo-erro", tela.Find("[data-guia-ans]").InnerHtml);

        tela.Find("input.numero-guia[placeholder='Copie do portal da operadora']").Input(numero);
        Assentar(tela);
        Clicar(tela, tela.FindAll(".modal-rodape button.primario").Last());

        // Duas perguntas em sequência, a segunda DEPOIS de a baixa gravar (a capa de conclusão da
        // fatura): responde a cada uma até o formulário da guia sair da tela.
        var prazo = DateTime.UtcNow + Prazo;
        while (tela.FindAll("[data-guia-ans]").Count > 0)
        {
            if (DateTime.UtcNow > prazo) throw new TimeoutException("O formulário da guia não fechou depois da baixa.");
            ConfirmarDialogos(tela);
            Thread.Sleep(50);
        }
        Assentar(tela);
    }

    private static void MarcarLinha(IRenderedComponent<Raiz> tela, string paciente)
    {
        tela.FindAll("tr").First(tr => tr.TextContent.Contains(paciente)).QuerySelector("td.col-marca input[type=checkbox]")!.Change(true);
        Assentar(tela);
    }

    private static IElement LinhaDaRodada(IRenderedComponent<Raiz> tela, string paciente)
        => tela.FindAll(".fat-rodada tr").First(tr => tr.TextContent.Contains(paciente));

    private static void Salvar(IRenderedComponent<ProfissionalForm> form)
        => form.FindAll(".modal-rodape button.primario").Last().Click();

    private int Contar(Func<CodigoFaturamento, bool> filtro)
    {
        using var db = Db();
        return db.Set<CodigoFaturamento>().AsNoTracking().AsEnumerable().Count(filtro);
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

        // O catálogo como a clínica o tem (o que o app carrega na abertura), lido do banco semeado.
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

    private static void Clicar(IRenderedComponent<Raiz> tela, string rotulo)
        => Clicar(tela, tela.FindAll("button").First(b => b.TextContent.Trim() == rotulo && !b.HasAttribute("disabled")));

    private static void Clicar(IRenderedComponent<Raiz> tela, IElement botao)
    {
        botao.Click();
        Assentar(tela);
    }

    /// <summary>Responde "sim" a toda pergunta que aparecer (confirmação da baixa, capa de conclusão).</summary>
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
        throw new TimeoutException("A tela não terminou de carregar.");
    }

    private static void Esperar(Func<bool> condicao)
    {
        var prazo = DateTime.UtcNow + Prazo;
        while (DateTime.UtcNow < prazo) { if (condicao()) return; Thread.Sleep(50); }
        throw new TimeoutException("A condição não aconteceu a tempo.");
    }

    /// <summary>Sem disco nem shell: cada pedido devolve nada, como na prévia.</summary>
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
