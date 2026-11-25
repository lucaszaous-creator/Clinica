using Clinica.Domain;
using Clinica.Domain.Entities;
using Clinica.Gestao.Web.Hospedagem;
using Clinica.Infrastructure;
using Clinica.Ui.Blazor.Hospedagem;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;
using Xunit;

namespace Clinica.Gestao.Web.Tests;

/// <summary>
/// Nov/2026: a gestão web assina receitas e documentos (autorização do dono, 24/11/2026). O C# só leva o pedido ao
/// navegador (assinatura-na-gestao.js), que usa as MESMAS rotas do Consultório. Aqui o navegador é FALSO — nenhuma
/// assinatura real (cada uma é cobrada pelo PSC). Dados fictícios.
/// </summary>
public sealed class AssinaturaNaGestaoTests : IDisposable
{
    private readonly SqliteConnection _conn = new("DataSource=:memory:");
    private readonly ServiceProvider _sp;

    public AssinaturaNaGestaoTests()
    {
        _conn.Open();
        _sp = new ServiceCollection().AddDbContext<ClinicaDbContext>(o => o.UseSqlite(_conn)).BuildServiceProvider();
        using var escopo = _sp.CreateScope();
        escopo.ServiceProvider.GetRequiredService<ClinicaDbContext>().Database.EnsureCreated();
    }

    public void Dispose() { _sp.Dispose(); _conn.Dispose(); }

    private sealed class NavegadorFalso(object? resposta) : IJSRuntime
    {
        public readonly List<(string Id, object?[]? Args, CancellationToken Ct)> Chamadas = [];
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args) => InvokeAsync<TValue>(identifier, CancellationToken.None, args);
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args)
        {
            Chamadas.Add((identifier, args, cancellationToken));
            return ValueTask.FromResult((TValue)resposta!);
        }
    }

    private int SemearDocumento()
    {
        using var escopo = _sp.CreateScope();
        var db = escopo.ServiceProvider.GetRequiredService<ClinicaDbContext>();
        var p = new Paciente { Nome = "Paciente Fictícia", Convenio = Convenio.UnimedIntercambio, Sexo = Sexo.Feminino };
        db.Pacientes.Add(p); db.SaveChanges();
        var d = new DocumentoClinico { PacienteId = p.Id, Tipo = TipoDocumentoClinico.Receita, Numero = "2026/0480", Corpo = "Receita fictícia", Data = new DateOnly(2026, 11, 24) };
        db.DocumentosClinicos.Add(d); db.SaveChanges();
        return d.Id;
    }

    private static string Json(object? o) => System.Text.Json.JsonSerializer.Serialize(o);

    [Fact]
    public async Task Do_Emitir_e_assinar_leva_documento_paciente_e_numero_ao_navegador_e_devolve_a_frase_do_A1()
    {
        var id = SemearDocumento();
        var nav = new NavegadorFalso(new AssinaturaNaGestao.RespostaDoNavegador("Documento assinado com A1 e arquivado.", false, false));
        var r = await new AssinaturaNaGestao(nav, _sp.GetRequiredService<IServiceScopeFactory>()).AssinarDocumentoPorIdAsync(id, "Receita nº 2026/0480");

        Assert.Equal(("Documento assinado com A1 e arquivado.", false), r);
        var (ident, args, _) = Assert.Single(nav.Chamadas);
        Assert.Equal("assinaturaNaGestao.assinar", ident);
        var pedido = Json(Assert.Single(args!));
        Assert.Contains($"\"documento\":{id}", pedido);
        Assert.Contains("\"numero\":\"2026/0480\"", pedido);
        Assert.Matches("\"paciente\":[1-9]", pedido);
    }

    [Fact]
    public async Task Da_central_de_Documentos_desistir_e_silencioso_e_recusa_e_erro()
    {
        var doc = new DocumentoEmitido(10, 3, "2026/0481", TipoDocumentoClinico.Receita, "Receita", false, false, false, false);
        var escopos = _sp.GetRequiredService<IServiceScopeFactory>();
        Assert.True((await new AssinaturaNaGestao(new NavegadorFalso(null), escopos).AssinarDocumentoAsync(doc)).Silencioso);
        var recusa = await new AssinaturaNaGestao(new NavegadorFalso(new AssinaturaNaGestao.RespostaDoNavegador("Senha incorreta.", true, false)), escopos).AssinarDocumentoAsync(doc);
        Assert.True(recusa.EhErro); Assert.False(recusa.Mudou); Assert.Equal("Senha incorreta.", recusa.Frase);
        var ok = await new AssinaturaNaGestao(new NavegadorFalso(new AssinaturaNaGestao.RespostaDoNavegador("Documento assinado com A1 e arquivado.", false, false)), escopos).AssinarDocumentoAsync(doc);
        Assert.True(ok.Mudou); Assert.False(ok.EhErro);
    }

    [Fact]
    public async Task SafeID_a_pagina_vai_a_Safeweb_e_a_tela_nao_segue_como_se_tivesse_assinado()
    {
        var doc = new DocumentoEmitido(10, 3, "2026/0481", TipoDocumentoClinico.Receita, "Receita", false, false, false, false);
        var tarefa = new AssinaturaNaGestao(new NavegadorFalso(new AssinaturaNaGestao.RespostaDoNavegador(null, false, true)), _sp.GetRequiredService<IServiceScopeFactory>()).AssinarDocumentoAsync(doc);
        await Task.Delay(200);
        Assert.False(tarefa.IsCompleted, "a volta é /gestao/?assinatura=, com a confirmação explícita");
    }

    [Fact]
    public async Task Infusao_nao_assina_na_gestao_os_seletores_de_certificado_devolvem_nulo()
    {
        var a = new AssinaturaNaGestao(new NavegadorFalso(null), _sp.GetRequiredService<IServiceScopeFactory>());
        Assert.Null(await a.EscolherCertificadoAsync("Prescrição"));
        Assert.Null(await a.EscolherCertificadoParaDocumentoAsync("Receita"));
    }

    [Fact]
    public void O_navegador_fala_so_com_as_rotas_do_Consultorio_e_volta_por_confirmacao_explicita()
    {
        // O motor é congelado: o JS da gestão usa as MESMAS rotas da API do portal que o Consultório (safeid.js, a1.js).
        var raiz = new DirectoryInfo(AppContext.BaseDirectory);
        while (raiz is not null && !File.Exists(Path.Combine(raiz.FullName, "Clinica.sln"))) raiz = raiz.Parent;
        var js = File.ReadAllText(Path.Combine(raiz!.FullName, "src", "Clinica.Gestao.Web", "wwwroot", "assinatura-na-gestao.js"));
        foreach (var rota in new[] { "/clinico/atendimentos/0/documento/${c.documento}/safeid", "/clinico/atendimentos/0/documento/${c.documento}/a1",
                     "/clinico/a1/${encodeURIComponent(a.id)}/assinar", "/clinico/safeid/' + encodeURIComponent(id) + '/concluir", "'/sessao'" })
            Assert.Contains(rota, js);
        Assert.Contains("'Assinar e arquivar'", js);              // confirmação explícita na volta
        Assert.DoesNotContain("window.open", js);                 // sem pop-up (Safari do iPhone)
        Assert.Contains("location.assign(url.href)", js);
        Assert.Contains("pscsafeweb.safewebpss.com.br", js);      // só para a Safeweb
        // 25/11/2026: a gestão renova o prazo clínico como o Consultório (toque/tecla, POST /clinico/atividade) e
        // nunca transforma falha em "não habilitada" (o .catch(() => null) que escondia o 401 da médica).
        Assert.Contains("api('/clinico/atividade', {})", js);
        Assert.Contains("['pointerdown', 'keydown']", js);
        Assert.DoesNotContain("api('/clinico/safeid').catch", js);
        Assert.DoesNotContain("api('/clinico/a1').catch", js);
        Assert.Contains("15 minutos sem uso", js);
    }
}
