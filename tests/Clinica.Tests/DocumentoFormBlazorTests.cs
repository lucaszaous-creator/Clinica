using AngleSharp.Dom;
using Bunit;
using Bunit.Rendering;
using Clinica.Application;
using Clinica.Application.Modelos;
using Clinica.Application.Servicos;
using Clinica.Domain;
using Clinica.Domain.Entities;
using Clinica.Infrastructure;
using Clinica.Ui.Blazor.Formularios;
using Clinica.Ui.Blazor.Hospedagem;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Clinica.Tests;

/// <summary>
/// A folha de emissão de documento escrito (<see cref="DocumentoForm"/>, out/2026 — era a
/// <c>DocumentoWindow</c> WPF) RENDERIZADA (bUnit) sobre os serviços do app e o banco semeado das
/// telas, e clicada de verdade. A afirmação é sobre o BANCO, nunca só sobre a frase:
/// - o atestado sai com o CID autorizado e os dias de afastamento;
/// - a receita que bate com a alergia registrada NÃO sai sem o "eu vi", e sai com ele;
/// - sem endereço, desistir da pergunta NÃO gasta número, e responder grava o endereço na ficha;
/// - a nova versão do laudo cancela o anterior no mesmo gravar;
/// - sem paciente (os Exames), a folha pergunta QUEM e o pedido sai com o exame escrito;
/// - a prévia não grava nada.
/// </summary>
[Collection(ColecaoDiagnostico.Nome)]
public sealed class DocumentoFormBlazorTests : IClassFixture<BancoDasTelas>, IDisposable
{
    private static readonly TimeSpan Prazo = TimeSpan.FromSeconds(20);
    private readonly BancoDasTelas _banco;

    public DocumentoFormBlazorTests(BancoDasTelas banco)
    {
        _banco = banco;
        var ptBr = new System.Globalization.CultureInfo("pt-BR");
        System.Globalization.CultureInfo.CurrentCulture = ptBr;
        System.Globalization.CultureInfo.CurrentUICulture = ptBr;
    }

    public void Dispose() => new SessaoUsuario().Entrar(new UsuarioSistema());

    [Fact]
    public void Atestado_sai_com_o_CID_autorizado_e_os_dias_e_a_previa_nao_grava_nada()
    {
        var (prof, paciente) = Semear("Atestado", endereco: null);
        using var ctx = Contexto();
        DocumentoForm.Emitido? emitido = null;
        var form = ctx.RenderComponent<DocumentoForm>(p => p
            .Add(x => x.PacienteId, paciente.Id).Add(x => x.Tipo, TipoDocumentoClinico.Atestado)
            .Add(x => x.AoConcluir, e => emitido = e));
        EsperarCarregado(form);

        Agir(form, f => CampoDoRotulo(f, "Dias de afastamento"), e => e.InputAsync(new ChangeEventArgs { Value = "3" }));
        Agir(form, f => f.Find("input[aria-label='CID-10']"), e => e.InputAsync(new ChangeEventArgs { Value = "M54.5" }));
        Agir(form, f => f.FindAll(".df-confirmacao input").First(), e => e.ChangeAsync(new ChangeEventArgs { Value = true }));

        Clicar(form, "Pré-visualizar");
        Esperar(() => Contem(form, EmissaoDeDocumentoNaTela.PreviaAberta));
        using (var db = Db()) Assert.Empty(db.DocumentosClinicos.Where(d => d.PacienteId == paciente.Id));

        Clicar(form, "Emitir e imprimir");
        EsperarEmitido(form, () => emitido is not null);

        using var db2 = Db();
        var doc = db2.DocumentosClinicos.Single(d => d.PacienteId == paciente.Id);
        Assert.Equal(TipoDocumentoClinico.Atestado, doc.Tipo);
        Assert.Equal("M54.5", doc.Cid);
        Assert.True(doc.CidAutorizado);
        Assert.Equal(3, doc.DiasAfastamento);
        Assert.Equal(prof.Id, doc.ProfissionalId);
        Assert.Equal(doc.Numero, emitido!.Numero);
    }

    [Fact]
    public void Receita_que_bate_com_a_alergia_so_sai_com_o_eu_vi()
    {
        var (_, paciente) = Semear("Alergia", endereco: "Rua das Flores, 10, Centro, Cidade");
        using (var db = Db())
        {
            db.Add(new ProblemaPaciente { PacienteId = paciente.Id, Natureza = NaturezaProblema.Alergia, Descricao = "Dipirona" });
            db.SaveChanges();
        }
        using var ctx = Contexto();
        DocumentoForm.Emitido? emitido = null;
        var form = ctx.RenderComponent<DocumentoForm>(p => p
            .Add(x => x.PacienteId, paciente.Id).Add(x => x.Tipo, TipoDocumentoClinico.Receita)
            .Add(x => x.AoConcluir, e => emitido = e));
        EsperarCarregado(form);
        Esperar(() => form.FindAll(".df-alerta").Any(a => a.TextContent.Contains("Dipirona")));

        Escrever(form, "Dipirona 500mg, 1 comprimido de 6 em 6 horas");
        Clicar(form, "Emitir e imprimir");
        Esperar(() => Contem(form, EmissaoDeDocumentoNaTela.RecusaAlergia));
        using (var db = Db()) Assert.Empty(db.DocumentosClinicos.Where(d => d.PacienteId == paciente.Id));

        // O "eu vi" aparece só com a colisão — e com ele a receita sai.
        Agir(form, f => f.FindAll(".df-confirmacao").First(l => l.TextContent.Contains("Eu vi o alerta")).QuerySelector("input")!,
            e => e.ChangeAsync(new ChangeEventArgs { Value = true }));
        Clicar(form, "Emitir e imprimir");
        EsperarEmitido(form, () => emitido is not null);

        using var db2 = Db();
        var doc = db2.DocumentosClinicos.Single(d => d.PacienteId == paciente.Id);
        Assert.Equal(TipoDocumentoClinico.Receita, doc.Tipo);
        Assert.Contains("Dipirona", doc.Corpo);
    }

    [Fact]
    public void Receita_sem_endereco_pergunta_e_desistir_nao_gasta_numero()
    {
        var (_, paciente) = Semear("Endereco", endereco: null);
        using var ctx = Contexto();
        DocumentoForm.Emitido? emitido = null;
        var form = ctx.RenderComponent<DocumentoForm>(p => p
            .Add(x => x.PacienteId, paciente.Id).Add(x => x.AoConcluir, e => emitido = e));
        EsperarCarregado(form);
        var dialogos = ctx.Services.GetRequiredService<Dialogos>();

        Escrever(form, "Paracetamol 750mg, 1 comprimido de 8 em 8 horas");
        ClicarSemEsperar(form, "Emitir e imprimir");
        Esperar(() => dialogos.Atual?.Titulo == EmissaoDeDocumentoNaTela.TituloPerguntaEndereco);
        form.InvokeAsync(() => dialogos.Responder(dialogos.Atual!, confirmou: false)).GetAwaiter().GetResult();
        Esperar(() => Contem(form, EmissaoDeDocumentoNaTela.EmissaoAdiada));
        using (var db = Db()) Assert.Empty(db.DocumentosClinicos.Where(d => d.PacienteId == paciente.Id));

        ClicarSemEsperar(form, "Emitir e imprimir");
        Esperar(() => dialogos.Atual?.Titulo == EmissaoDeDocumentoNaTela.TituloPerguntaEndereco);
        form.InvokeAsync(() =>
        {
            dialogos.Atual!.Texto = "Rua Nova, 22, Centro, Cidade";
            dialogos.Responder(dialogos.Atual!, confirmou: true);
        }).GetAwaiter().GetResult();
        EsperarEmitido(form, () => emitido is not null);

        using var db2 = Db();
        Assert.Single(db2.DocumentosClinicos.Where(d => d.PacienteId == paciente.Id));
        Assert.Equal("Rua Nova, 22, Centro, Cidade", db2.Pacientes.Single(p => p.Id == paciente.Id).Endereco);
    }

    [Fact]
    public void A_nova_versao_do_laudo_cancela_o_anterior_no_mesmo_gravar()
    {
        var (prof, paciente) = Semear("Laudo", endereco: null);
        using var ctx = Contexto();
        DocumentoClinico anterior;
        using (var escopo = ctx.Services.GetRequiredService<IServiceScopeFactory>().CreateScope())
        {
            var servico = escopo.ServiceProvider.GetRequiredService<DocumentoClinicoService>();
            var primeiro = servico.EmitirAsync(new DocumentoClinico
            {
                Tipo = TipoDocumentoClinico.LaudoMedico, PacienteId = paciente.Id, ProfissionalId = prof.Id,
                Data = DateOnly.FromDateTime(DateTime.Today), Corpo = "Quadro clínico estável."
            }, "teste").GetAwaiter().GetResult();
            anterior = servico.ObterAsync(primeiro.Id).GetAwaiter().GetResult()!;
        }

        DocumentoForm.Emitido? emitido = null;
        var form = ctx.RenderComponent<DocumentoForm>(p => p
            .Add(x => x.PacienteId, paciente.Id).Add(x => x.Tipo, TipoDocumentoClinico.LaudoMedico)
            .Add(x => x.VersaoDe, anterior).Add(x => x.AoConcluir, e => emitido = e));
        EsperarCarregado(form);
        Assert.True(Contem(form, $"substitui o {anterior.Numero}"));
        Assert.Empty(form.FindAll(".df-tipos"));

        Escrever(form, "Quadro clínico estável. Acrescento: melhora da dor lombar.");
        Clicar(form, "Emitir e imprimir");
        EsperarEmitido(form, () => emitido is not null);

        using var db = Db();
        var docs = db.DocumentosClinicos.Where(d => d.PacienteId == paciente.Id).OrderBy(d => d.Id).ToList();
        Assert.Equal(2, docs.Count);
        Assert.True(docs[0].Cancelado);
        Assert.False(docs[1].Cancelado);
        Assert.Contains("melhora da dor lombar", docs[1].Corpo);
    }

    [Fact]
    public void Sem_paciente_a_folha_pergunta_quem_e_o_pedido_sai_com_o_exame()
    {
        var (_, paciente) = Semear("Pedido", endereco: null);
        using var ctx = Contexto();
        DocumentoForm.Emitido? emitido = null;
        var form = ctx.RenderComponent<DocumentoForm>(p => p
            .Add(x => x.Tipo, TipoDocumentoClinico.PedidoExame)
            .Add(x => x.TituloDaEscolha, "Novo pedido de exame — para quem?")
            .Add(x => x.AoConcluir, e => emitido = e));
        Assert.True(Contem(form, "Novo pedido de exame — para quem?"));

        Agir(form, f => f.Find("input[placeholder='Digite o nome ou o CPF']"), e => e.InputAsync(new ChangeEventArgs { Value = paciente.Nome }));
        Esperar(() =>
        {
            if (form.FindAll(".df-folha").Count > 0) return true;
            var linha = form.FindAll(".seletor-resultados li").FirstOrDefault(li => li.TextContent.Contains(paciente.Nome));
            if (linha is null) return false;
            try { linha.ClickAsync(new MouseEventArgs()).GetAwaiter().GetResult(); } catch (UnknownEventHandlerIdException) { }
            return form.FindAll(".df-folha").Count > 0;
        });
        EsperarCarregado(form);

        Agir(form, f => f.Find("textarea[aria-label='Exame / procedimento']"), e => e.InputAsync(new ChangeEventArgs { Value = "Hemograma completo" }));
        Clicar(form, "Emitir e imprimir");
        EsperarEmitido(form, () => emitido is not null);

        Assert.Equal(paciente.Nome, emitido!.Paciente);
        using var db = Db();
        var doc = db.DocumentosClinicos.Include(d => d.Itens).Single(d => d.PacienteId == paciente.Id);
        Assert.Equal(TipoDocumentoClinico.PedidoExame, doc.Tipo);
        Assert.Contains(doc.Itens, i => i.Descricao == "Hemograma completo");
    }

    // ==================== montagem ====================

    /// <summary>Um profissional e um paciente PRÓPRIOS do teste, e quem "entrou" ligado ao profissional.</summary>
    private (Profissional Prof, Paciente Paciente) Semear(string sufixo, string? endereco)
    {
        var marca = $"{sufixo}{Guid.NewGuid():N}"[..14];
        using var db = Db();
        var prof = new Profissional { Nome = $"Dra. Docs {marca}", NomeCurto = $"Docs {marca}", RegistroConselho = "CRM-SP 12345", Ativo = true };
        var paciente = new Paciente { Nome = $"Zilda Docteste {marca}", ConvenioCodigo = "Particular", Endereco = endereco };
        db.AddRange(prof, paciente);
        db.SaveChanges();
        new SessaoUsuario().Entrar(new UsuarioSistema
        {
            Nome = prof.Nome, Login = $"docs.{marca}", Perfil = PerfilAcesso.Gerente, ProfissionalId = prof.Id, Ativo = true
        });
        return (prof, paciente);
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

    /// <summary>A carga terminou: o esqueleto saiu e a folha (ou a recusa) está à vista.</summary>
    private static void EsperarCarregado(IRenderedFragment form)
        => Esperar(() => form.FindAll(".df-folha").Count > 0 && form.FindAll(".esqueleto").Count == 0);

    /// <summary>
    /// O campo de texto da folha: o editor é desenhado por um módulo JS que o bUnit não roda, então
    /// o que a pessoa digita chega como o módulo o entregaria — pelo <c>Mudou</c> do editor.
    /// </summary>
    private static void Escrever(IRenderedComponent<DocumentoForm> form, string texto)
    {
        Esperar(() => form.FindComponents<Clinica.Ui.Blazor.Componentes.EditorTextoClinico>().Count > 0);
        var editor = form.FindComponent<Clinica.Ui.Blazor.Componentes.EditorTextoClinico>();
        editor.InvokeAsync(() => editor.Instance.Mudou([new TrechoTexto(texto)])).GetAwaiter().GetResult();
    }

    private static IElement CampoDoRotulo(IRenderedFragment form, string rotulo)
        => form.FindAll(".campo-bloco").First(l => l.TextContent.Contains(rotulo)).QuerySelector("input")!;

    private static bool Contem(IRenderedFragment form, string texto)
        => form.FindAll(".modal").Any(m => m.TextContent.Contains(texto, StringComparison.Ordinal));

    /// <summary>Age e repete SÓ quando o id de evento estava morto (a folha re-renderiza sozinha na carga).</summary>
    private static void Agir(IRenderedFragment form, Func<IRenderedFragment, IElement> achar, Func<IElement, Task> acao)
    {
        Esperar(() =>
        {
            try { acao(achar(form)).GetAwaiter().GetResult(); return true; }
            catch (UnknownEventHandlerIdException) { return false; }
        });
    }

    private static void Clicar(IRenderedFragment form, string rotulo)
    {
        Esperar(() => form.FindAll("button").Any(b => b.TextContent.Trim() == rotulo && !b.HasAttribute("disabled")));
        Agir(form, f => f.FindAll("button").First(b => b.TextContent.Trim() == rotulo), e => e.ClickAsync(new MouseEventArgs()));
    }

    /// <summary>
    /// Clica SEM esperar o fim do clique: a emissão para numa pergunta (o endereço) que só o teste
    /// responde — esperar o clique terminar seria esperar a resposta que ele mesmo vai dar.
    /// </summary>
    private static void ClicarSemEsperar(IRenderedFragment form, string rotulo)
    {
        Esperar(() => form.FindAll("button").Any(b => b.TextContent.Trim() == rotulo && !b.HasAttribute("disabled")));
        _ = form.FindAll("button").First(b => b.TextContent.Trim() == rotulo).ClickAsync(new MouseEventArgs());
    }

    /// <summary>
    /// Espera a folha concluir. Depois de emitir, o papel que pode sair como arquivo pergunta
    /// "Enviar agora ao paciente?" (EnvioAoPacienteForm) e a folha só conclui com a resposta —
    /// o teste responde "Agora não", que é o que não envia nada.
    /// </summary>
    private static void EsperarEmitido(IRenderedFragment form, Func<bool> concluiu,
        [System.Runtime.CompilerServices.CallerLineNumber] int linha = 0)
    {
        var prazo = DateTime.UtcNow + Prazo;
        while (DateTime.UtcNow < prazo)
        {
            if (concluiu()) return;
            try
            {
                if (form.FindAll("button").FirstOrDefault(b => b.TextContent.Trim() == "Agora não") is { } agoraNao)
                    agoraNao.Click();
            }
            catch (Exception e) when (e is Bunit.ElementNotFoundException or ArgumentException) { } // o botão sumiu entre achar e clicar
            Thread.Sleep(25);
        }
        throw new TimeoutException($"A folha não concluiu a tempo (linha {linha}).");
    }

    private static void Esperar(Func<bool> condicao,
        [System.Runtime.CompilerServices.CallerArgumentExpression(nameof(condicao))] string? qual = null,
        [System.Runtime.CompilerServices.CallerLineNumber] int linha = 0)
    {
        var prazo = DateTime.UtcNow + Prazo;
        while (DateTime.UtcNow < prazo)
        {
            try { if (condicao()) return; }
            catch (Microsoft.Data.Sqlite.SqliteException e) when (e.SqliteErrorCode is 5 or 6) { }
            catch (Bunit.ElementNotFoundException) { }
            Thread.Sleep(25);
        }
        throw new TimeoutException($"A condição não aconteceu a tempo (linha {linha}): {qual}");
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
