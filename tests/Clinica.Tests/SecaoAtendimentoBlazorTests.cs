using AngleSharp.Dom;
using Bunit;
using Clinica.Application;
using Clinica.Application.Modelos;
using Clinica.Application.Servicos;
using Clinica.Domain;
using Clinica.Domain.Entities;
using Clinica.Infrastructure;
using Clinica.Ui.Blazor.Componentes;
using Clinica.Ui.Blazor.Componentes.SecoesDoPaciente;
using Clinica.Ui.Blazor.Hospedagem;
using Microsoft.AspNetCore.Components;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Clinica.Tests;

/// <summary>
/// A FOLHA DO MÉDICO em Blazor (out/2026): as regras que a FolhaDaSessaoViewModel decidia
/// (<see cref="RascunhoDaSessao"/>, <see cref="MapaDaSessao"/>), o editor de texto clínico (o
/// texto simples continua CANÔNICO; a formatação é a do domínio) e a seção RENDERIZADA (bUnit)
/// sobre o banco semeado das telas, clicada de verdade: salvar grava no prontuário, os
/// formulários abrem, e o rascunho sobrevive à troca de seção.
/// </summary>
[Collection(ColecaoDiagnostico.Nome)]
public sealed class SecaoAtendimentoBlazorTests : IClassFixture<BancoDasTelas>, IDisposable
{
    private static readonly TimeSpan Prazo = TimeSpan.FromSeconds(20);
    private readonly BancoDasTelas _banco;

    public SecaoAtendimentoBlazorTests(BancoDasTelas banco)
    {
        _banco = banco;
        var ptBr = new System.Globalization.CultureInfo("pt-BR");
        System.Globalization.CultureInfo.CurrentCulture = ptBr;
        System.Globalization.CultureInfo.CurrentUICulture = ptBr;
        new SessaoUsuario().Entrar(banco.Gerente);
    }

    public void Dispose() => new SessaoUsuario().Entrar(new UsuarioSistema());

    // ==================== As regras do rascunho ====================

    [Fact]
    public void Sessao_so_com_EVA_e_mapa_esta_em_branco_para_PERGUNTAR_e_tem_o_que_GRAVAR()
    {
        var r = new RascunhoDaSessao { EvaAntes = 8, EvaDepois = 3 };
        Assert.True(r.SessaoEmBranco);
        Assert.True(r.TemAlgoParaGravar(mapaTemConteudo: false));

        var vazio = new RascunhoDaSessao();
        Assert.False(vazio.TemAlgoParaGravar(mapaTemConteudo: false));
        Assert.True(vazio.TemAlgoParaGravar(mapaTemConteudo: true));
    }

    [Fact]
    public void Plano_retorno_e_encaminhamento_contam_como_sessao_escrita()
    {
        Assert.False(new RascunhoDaSessao { PlanoTerapeutico = "reavaliar" }.SessaoEmBranco);
        Assert.False(new RascunhoDaSessao { RetornoSugeridoEm = new DateOnly(2026, 11, 1) }.SessaoEmBranco);
        Assert.False(new RascunhoDaSessao { Encaminhamento = "psiquiatria" }.SessaoEmBranco);
        // O CID é medida: não tira do "em branco", mas é algo a gravar.
        var cid = new RascunhoDaSessao { CidSessao = "M54.5" };
        Assert.True(cid.SessaoEmBranco);
        Assert.True(cid.TemAlgoParaGravar(false));
    }

    [Fact]
    public void O_selo_do_detalhe_conta_os_campos_escondidos_e_nao_os_personalizados()
    {
        var r = new RascunhoDaSessao { QueixaPrincipal = "dor", Conduta = "agulhas" };
        r.Campos.Add(new CampoDaSessaoNaTela { Id = 1, Rotulo = "Agulhas", Tipo = TipoCampoPersonalizado.Numero, Opcoes = [], Resposta = "6" });
        Assert.Equal(2, r.CamposDetalhados);
        Assert.Equal("2 campos preenchidos", r.SeloDetalhe);
        Assert.Equal("1 campo preenchido", new RascunhoDaSessao { Encaminhamento = "x" }.SeloDetalhe);
    }

    [Fact]
    public void O_CID_fora_do_catalogo_e_dito_e_nao_recusado()
    {
        Assert.Equal("Dor lombar baixa", new RascunhoDaSessao { CidSessao = "M54.5" }.DescricaoCid);
        Assert.Contains("fora do catálogo", new RascunhoDaSessao { CidSessao = "Z99.9" }.DescricaoCid);
        Assert.Null(new RascunhoDaSessao().DescricaoCid);
    }

    [Fact]
    public void Aplicar_modelo_traz_o_que_ele_tem_e_remove_o_estilo_do_texto_substituido()
    {
        var r = new RascunhoDaSessao
        {
            QueixaPrincipal = "dor no ombro", TextoEvolucao = "antigo", TextoEvolucaoFormatado = "[{\"texto\":\"antigo\",\"negrito\":true}]"
        };
        r.AplicarModelo(new ModeloEvolucao { Nome = "x", TextoEvolucao = "roteiro", PlanoTerapeutico = "10 sessões" });
        Assert.Equal("dor no ombro", r.QueixaPrincipal); // o modelo não traz queixa: fica
        Assert.Equal("roteiro", r.TextoEvolucao);
        Assert.Equal(string.Empty, r.TextoEvolucaoFormatado);
        Assert.Equal("10 sessões", r.PlanoTerapeutico);
    }

    [Fact]
    public void Reutilizar_nunca_escreve_por_cima_e_diz_quando_nao_trouxe_nada()
    {
        var anterior = new ResumoSessaoAnterior(1, "01/10/2026", "EVA 7 → 4", true, string.Empty,
        [
            new CampoDaSessaoAnterior(ResumoSessaoAnterior.RotuloConduta, "agulhamento lombar"),
            new CampoDaSessaoAnterior(ResumoSessaoAnterior.RotuloEvolucao, "texto da folha")
        ]);
        var r = new RascunhoDaSessao { TextoEvolucao = "já escrevi" };
        var (frase, erro) = r.Reutilizar(anterior);
        Assert.False(erro);
        Assert.Equal("agulhamento lombar", r.Conduta); // sem o rótulo dentro ("Conduta: …")
        Assert.Equal("já escrevi", r.TextoEvolucao);
        Assert.Contains("a conduta", frase);

        var (nada, erroNada) = r.Reutilizar(anterior);
        Assert.True(erroNada);
        Assert.Contains("Nada a trazer", nada);
        Assert.True(new RascunhoDaSessao().Reutilizar(null).Erro);
    }

    [Fact]
    public void A_gravacao_manda_formato_vazio_nunca_nulo_e_os_personalizados_copiados()
    {
        var r = new RascunhoDaSessao { TextoEvolucao = "simples", Data = new DateOnly(2026, 10, 1) };
        r.Campos.Add(new CampoDaSessaoNaTela { Id = 9, Rotulo = "Agulhas", Tipo = TipoCampoPersonalizado.Texto, Opcoes = [], Resposta = "6" });
        var e = r.ParaGravacao(5, 2, 33, null,
            [new CampoPersonalizadoProntuario { Id = 9, Rotulo = "Agulhas", Tipo = TipoCampoPersonalizado.Texto, Ativo = true }]);
        Assert.Equal(string.Empty, e.TextoEvolucaoFormatado);
        Assert.Equal(33, e.AgendamentoId);
        Assert.Single(e.CamposPersonalizados);
        Assert.Equal("Agulhas", e.CamposPersonalizados[0].Rotulo);
    }

    [Fact]
    public void O_mapa_marca_numera_desfaz_e_recusa_fora_da_figura()
    {
        var m = new MapaDaSessao();
        Assert.Null(m.Marcar(FaceCorpo.Frente, 0.5, 0.2, " IG4 ", TecnicaPonto.Agulha));
        Assert.Null(m.Marcar(FaceCorpo.Costas, 0.5, 0.5, null, TecnicaPonto.Ventosa));
        Assert.Equal(2, m.Pontos[1].Numero);
        Assert.Equal("IG4", m.Pontos[0].Nome);
        Assert.NotNull(m.Marcar(FaceCorpo.Frente, 1.4, 0.2, null, TecnicaPonto.Agulha));
        Assert.True(m.Desfazer());
        Assert.Single(m.Pontos);
        Assert.False(m.Desfazer()); // um passo só
        Assert.True(m.TemConteudo);
        Assert.Single(m.ParaGravacao().Pontos);
    }

    [Fact]
    public void Finalizar_diz_o_desfecho_com_os_recados()
    {
        Assert.Equal("Sessão concluída — particular, sem guia a faturar.", FinalizacaoDaSessao.Concluida(0, []));
        Assert.Equal("Sessão concluída — 2 guia(s) no faturamento. · NC reaberta",
            FinalizacaoDaSessao.Concluida(2, ["NC reaberta"]));
        Assert.Contains("não crie outro lançamento", FinalizacaoDaSessao.Falhou(gravou: true, "x"));
        Assert.Equal("x", FinalizacaoDaSessao.Falhou(gravou: false, "x"));
    }

    // ==================== O editor de texto clínico ====================

    [Fact]
    public void Texto_simples_continua_canonico_e_sai_sem_formato()
    {
        var (texto, formato) = EditorTextoClinico.Canonizar([new TrechoTexto("linha 1\r\nlinha 2")]);
        Assert.Equal("linha 1\nlinha 2", texto);
        Assert.Equal(string.Empty, formato);
    }

    [Fact]
    public void Negrito_e_italico_saem_no_formato_do_dominio_e_recompoem_o_texto()
    {
        var (texto, formato) = EditorTextoClinico.Canonizar(
        [
            new TrechoTexto("Paciente "), new TrechoTexto("melhor", Negrito: true), new TrechoTexto(" hoje", Italico: true)
        ]);
        Assert.Equal("Paciente melhor hoje", texto);
        Assert.Equal(TextoFormatado.Guardar(TextoFormatado.Ler(texto, formato)), formato);
        var lidos = TextoFormatado.Ler(texto, formato);
        Assert.Contains(lidos, t => t.Texto == "melhor" && t.Negrito);
        Assert.Contains(lidos, t => t.Texto == " hoje" && t.Italico);
    }

    // ==================== A seção renderizada ====================

    [Fact]
    public void A_folha_abre_salva_no_prontuario_e_os_formularios_abrem_sem_quebrar()
    {
        var (paciente, agendamento) = Semear();
        using var ctx = Contexto();
        var tela = new TelaFalsa();
        var contexto = new ContextoDoPaciente(paciente.Id, paciente.Nome, agendamento.Id, null,
            DateOnly.FromDateTime(agendamento.DataHora), 1);
        var secao = Renderizar(ctx, contexto, tela);
        Esperar(() => Botao(secao, "Salvar sessão") is { } b && !b.HasAttribute("disabled"));
        Assert.Contains("Evolução da sessão", secao.Markup);
        Assert.Contains("EMITIR", secao.Markup);

        Digitar(secao, new TrechoTexto("Paciente relata melhora da dor lombar."));
        Botao(secao, "Salvar sessão")!.Click();
        Esperar(() =>
        {
            using var db = Db();
            return db.Evolucoes.Any(e => e.PacienteId == paciente.Id && e.AgendamentoId == agendamento.Id
                                         && e.TextoEvolucao == "Paciente relata melhora da dor lombar.");
        });
        Assert.True(tela.Avisos > 0);

        // Os formulários da folha abrem e fecham.
        foreach (var rotulo in new[] { "Campos complementares…", "Usar modelo", "Mapa corporal" })
        {
            Esperar(() => secao.FindAll("button").Any(b => b.TextContent.Trim().StartsWith(rotulo, StringComparison.Ordinal) && !b.HasAttribute("disabled")));
            secao.FindAll("button").First(b => b.TextContent.Trim().StartsWith(rotulo, StringComparison.Ordinal)).Click();
            Esperar(() => secao.FindAll(".modal").Count > 0);
            Assert.DoesNotContain("Algo deu errado", secao.Markup);
            secao.FindAll(".modal .fechar").Last().Click();
            Esperar(() => secao.FindAll(".modal").Count == 0);
        }

        // O mapa: clicar na figura marca um ponto (fração da figura nativa).
        secao.FindAll("button").First(b => b.TextContent.Trim().StartsWith("Mapa corporal", StringComparison.Ordinal)).Click();
        Esperar(() => secao.FindAll(".at-mapa-area svg").Count == 2);
        secao.FindAll(".at-mapa-area svg")[0].Click(new Microsoft.AspNetCore.Components.Web.MouseEventArgs { OffsetX = 110, OffsetY = 230 });
        Esperar(() => secao.FindAll(".at-mapa-ponto").Count == 1);
        secao.FindAll(".modal .fechar").Last().Click();

        // O histórico abre ao lado.
        Botao(secao, "Consultar histórico")!.Click();
        Esperar(() => secao.FindAll(".at-historico").Count == 1);
    }

    [Fact]
    public void O_rascunho_sobrevive_a_troca_de_secao()
    {
        var (paciente, agendamento) = Semear();
        using var ctx = Contexto();
        var tela = new TelaFalsa();
        var contexto = new ContextoDoPaciente(paciente.Id, paciente.Nome, agendamento.Id, null,
            DateOnly.FromDateTime(agendamento.DataHora), 1);
        var secao = Renderizar(ctx, contexto, tela);
        Esperar(() => Botao(secao, "Salvar sessão") is { } b && !b.HasAttribute("disabled"));
        Digitar(secao, new TrechoTexto("rascunho "), new TrechoTexto("não gravado", Negrito: true));
        secao.Dispose();

        // A seção remonta (o rail voltou a ela): a folha guardada pela TELA traz o que se digitou.
        var de_novo = Renderizar(ctx, contexto, tela);
        Esperar(() => de_novo.FindComponents<EditorTextoClinico>().Count > 0);
        var editor = de_novo.FindComponent<EditorTextoClinico>().Instance;
        Assert.Equal("rascunho não gravado", editor.Texto);
        Assert.Contains("\"negrito\":true", editor.Formato);
        var folha = tela.Guardado(FolhaDoAtendimento.Chave, () => (FolhaDoAtendimento)null!);
        Assert.True(folha.TemAlgoParaGravar);
        Assert.Equal(0, folha.EvolucaoId);
    }

    private static IRenderedFragment Renderizar(TestContext ctx, ContextoDoPaciente contexto, ITelaDoPaciente tela)
        => ctx.Render(b =>
        {
            b.OpenComponent<CascadingValue<ContextoDoPaciente>>(0);
            b.AddAttribute(1, "Value", contexto);
            b.AddAttribute(2, "IsFixed", true);
            b.AddAttribute(3, "ChildContent", (RenderFragment)(b2 =>
            {
                b2.OpenComponent<CascadingValue<ITelaDoPaciente>>(0);
                b2.AddAttribute(1, "Value", tela);
                b2.AddAttribute(2, "IsFixed", true);
                b2.AddAttribute(3, "ChildContent", (RenderFragment)(b3 =>
                {
                    b3.OpenComponent<SecaoAtendimento>(0);
                    b3.CloseComponent();
                }));
                b2.CloseComponent();
            }));
            b.CloseComponent();
        });

    /// <summary>O que o módulo JS do editor faz a cada tecla: devolve os trechos lidos do campo.</summary>
    private static void Digitar(IRenderedFragment f, params TrechoTexto[] trechos)
    {
        Esperar(() => f.FindComponents<EditorTextoClinico>().Count > 0);
        var editor = f.FindComponent<EditorTextoClinico>();
        editor.InvokeAsync(() => editor.Instance.Mudou(trechos.ToList())).GetAwaiter().GetResult();
    }

    private static IElement? Botao(IRenderedFragment f, string rotulo)
        => f.FindAll("button").FirstOrDefault(b => b.TextContent.Trim() == rotulo);

    /// <summary>Um paciente e um horário de HOJE, PRÓPRIOS do teste.</summary>
    private (Paciente Paciente, Agendamento Agendamento) Semear()
    {
        var marca = Guid.NewGuid().ToString("N")[..10];
        using var db = Db();
        var prof = new Profissional { Nome = $"Dra. Folha {marca}", NomeCurto = $"Folha {marca}" };
        var paciente = new Paciente { Nome = $"Yolanda Folhateste {marca}", ConvenioCodigo = "Particular" };
        db.AddRange(prof, paciente);
        db.SaveChanges();
        var ag = new Agendamento
        {
            PacienteId = paciente.Id, ProfissionalId = prof.Id, DataHora = DateTime.Today.AddHours(9),
            ModalidadePrevista = ModalidadeAtendimento.AcupunturaSimples,
            ModalidadeCodigo = nameof(ModalidadeAtendimento.AcupunturaSimples), Status = StatusAgendamento.Agendado,
            DuracaoMinutos = 30, CriadoPor = "teste", CriadoEm = DateTime.Today
        };
        db.Add(ag);
        db.SaveChanges();
        return (paciente, ag);
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
        return ctx;
    }

    private static void Esperar(Func<bool> condicao)
    {
        var prazo = DateTime.UtcNow + Prazo;
        while (DateTime.UtcNow < prazo)
        {
            try { if (condicao()) return; }
            catch (Microsoft.Data.Sqlite.SqliteException e) when (e.SqliteErrorCode is 5 or 6) { }
            catch (Bunit.ElementNotFoundException) { }
            Thread.Sleep(25);
        }
        throw new TimeoutException("A condição não aconteceu a tempo.");
    }

    /// <summary>A tela do paciente, só com o que a seção usa: o estado guardado por paciente.</summary>
    private sealed class TelaFalsa : ITelaDoPaciente
    {
        private readonly Dictionary<string, object> _guardados = [];
        public int Avisos { get; private set; }
        public Task RelerCabecalhoAsync() => Task.CompletedTask;
        public void IrPara(string secao, int subAba = 0) { }
        public T Guardado<T>(string chave, Func<T> criar) where T : class
        {
            if (_guardados.TryGetValue(chave, out var o) && o is T t) return t;
            var novo = criar();
            _guardados[chave] = novo;
            return novo;
        }
        public void AvisarMudanca() => Avisos++;
    }

    private sealed class HostMudo : IHostDaTela
    {
        public Task<string?> ExportarCsvAsync(IReadOnlyList<string> c, IReadOnlyList<IReadOnlyList<string>> l, string n) => Task.FromResult<string?>(null);
        public Task<string?> SalvarEAbrirAsync(byte[] c, string n, string f, string e) => Task.FromResult<string?>(null);
        public Task<ArquivoEscolhido?> EscolherArquivoAsync(string f, string t) => Task.FromResult<ArquivoEscolhido?>(null);
        public Task<bool> CopiarAsync(string texto) => Task.FromResult(true);
        public Task<bool> NavegarAsync(string chave) => Task.FromResult(false);
        public Task<bool> AbrirEnderecoAsync(string url) => Task.FromResult(false);
        public Task<bool> AbrirFichaDoPacienteAsync(int pacienteId) => Task.FromResult(false);
        public Task<string?> PerguntarOndeSalvarAsync(string n, string f, string e) => Task.FromResult<string?>(null);
        public Task<string?> PerguntarPastaAsync(string t) => Task.FromResult<string?>(null);
        public Task<string?> GravarArquivoAsync(string c, byte[] b) => Task.FromResult<string?>(null);
    }
}
