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
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PreviaBlazor.Sementes;
using Xunit;

namespace Clinica.Tests;

/// <summary>
/// A BAIXA QUE ENVIA (nov/2026, correção de fluxo da direção: "a guia só é gerada APÓS o envio").
/// Com o envio ligado para o convênio, o formulário de baixa não pede o número: UM clique confere
/// a guia e pede o envio dela; o formulário acompanha o pedido e, quando a operadora responde que
/// recebeu, registra a baixa SOZINHO com o número devolvido. Clicando de verdade (bUnit) sobre o
/// serviço de verdade, e afirmando o estado do banco. A resposta da operadora é gravada pelo mesmo
/// serviço que o conector usa (<see cref="FilaTissService.ConcluirAsync"/>/<c>FalharAsync</c>):
/// nenhum teste fala com operadora.
/// </summary>
[Collection(ColecaoDiagnostico.Nome)]
public sealed class BaixaQueEnviaBlazorTests : IClassFixture<BancoDasTelas>, IDisposable
{
    private const string Petrobras = "Petrobras";
    private static readonly TimeSpan Prazo = TimeSpan.FromSeconds(30);
    private readonly BancoDasTelas _banco;

    public BaixaQueEnviaBlazorTests(BancoDasTelas banco)
    {
        _banco = banco;
        var ptBr = new System.Globalization.CultureInfo("pt-BR");
        System.Globalization.CultureInfo.CurrentCulture = ptBr;
        System.Globalization.CultureInfo.CurrentUICulture = ptBr;
        new SessaoUsuario().Entrar(banco.Gerente);
        DefinirEnvio(true);
    }

    public void Dispose() => new SessaoUsuario().Entrar(new UsuarioSistema());

    // ==================== o clique único ====================

    [Fact]
    public void Um_clique_confere_a_guia_e_pede_o_envio_dela_sem_pedir_numero_e_sem_dar_baixa()
    {
        var marca = Marca();
        var codigoId = Pendente(marca);
        using var ctx = Contexto();
        var tela = AbrirBaixa(ctx, marca);

        // Antes de enviar: o número NÃO é pedido, e a tela diz de onde ele vem.
        Assert.Empty(tela.FindAll("[data-baixa-no-sistema] input.numero-guia"));
        Assert.Contains("não é digitado", tela.Find("[data-numero-vem-da-operadora]").TextContent);
        Assert.Equal("Enviar para Petrobras e dar baixa", tela.Find("[data-enviar-e-baixar]").TextContent.Trim());
        Assert.DoesNotContain("Duas saídas", tela.Markup);
        Assert.Contains("gera o número", tela.Markup); // o subtítulo novo
        CompletarGuia(tela);

        tela.Find("[data-enviar-e-baixar]").Click();
        Esperar(() => tela.FindAll("[role=alertdialog]").Count > 0);
        var pergunta = tela.Find("[role=alertdialog]").TextContent;
        Assert.Contains("COBRANÇA", pergunta);
        Assert.Contains($"Envio {marca}", pergunta);
        Assert.Empty(EnviosDa(codigoId)); // nada entrou na fila antes do "sim"
        ConfirmarDialogos(tela);
        Esperar(() => tela.FindAll("[data-acompanhamento]").Count == 1);

        using (var db = Db())
        {
            var codigo = db.Codigos.AsNoTracking().Include(c => c.GuiaAns).Single(c => c.Id == codigoId);
            Assert.Equal(_banco.Gerente.Login, codigo.GuiaAns!.ConferidaPor);
            Assert.Equal(StatusCodigo.Aberto, codigo.Status); // pedir o envio NÃO é dar baixa
            Assert.Null(codigo.NumeroGuiaReal);
            var envio = db.TransacoesTiss.AsNoTracking().Include(t => t.Guias).Single(t => t.Guias.Any(g => g.CodigoId == codigoId));
            Assert.Equal(EstadoTransacaoTiss.Pendente, envio.Estado);
            Assert.Equal(_banco.Gerente.Login, envio.PedidoPor);
            Assert.Single(envio.Guias);
        }

        // O formulário NÃO fechou: acompanha o pedido, com os campos da guia só leitura e sem ação de enviar.
        Assert.Equal("NaFila", tela.Find("[data-acompanhamento]").GetAttribute("data-acompanhamento"));
        Assert.Equal(new[] { "feito", "atual", "pendente", "pendente" }, tela.FindAll(".fat-baixa-passos li").Select(li => li.ClassName!.Trim()));
        Assert.True(tela.Find("fieldset.fat-guia-campos").HasAttribute("disabled"));
        Assert.Empty(tela.FindAll("[data-enviar-e-baixar]"));
        Assert.Empty(tela.FindAll("[data-conferir-para-envio]"));
        Assert.Empty(tela.FindAll("[data-baixa-no-sistema]"));
    }

    // ==================== a) recebida ====================

    [Fact]
    public void Recebida_com_numero_a_baixa_e_registrada_sozinha_com_o_numero_devolvido_e_a_tela_o_mostra()
    {
        var marca = Marca();
        var codigoId = Pendente(marca);
        using var ctx = Contexto();
        var tela = AbrirBaixa(ctx, marca);
        CompletarGuia(tela);
        Enviar(tela);

        Responder(codigoId, item => new ConclusaoTransacaoTiss
        {
            Resultado = "Lote protocolado.", Protocolo = "20269900010",
            Guias = [new ResultadoDaGuiaTiss(item.NumeroGuiaPrestador, "PB900010", SituacaoGuiaTiss.Recebida)]
        });

        // SEM segundo clique: o relógio do formulário relê, vê a guia recebida e registra a baixa.
        Esperar(() => tela.FindAll("[data-acompanhamento='Baixada']").Count == 1);
        Assert.Equal("Guia nº PB900010 gerada por Petrobras · protocolo 20269900010. Baixa registrada.", tela.Find("[data-desfecho]").TextContent.Trim());
        Assert.Single(tela.FindAll("[data-copiar-numero]"));
        Assert.Empty(tela.FindAll("[data-enviar-e-baixar]"));
        Assert.Equal("Fechar", tela.Find("[data-fechar]").TextContent.Trim());

        using (var db = Db())
        {
            var codigo = db.Codigos.AsNoTracking().Single(c => c.Id == codigoId);
            Assert.Equal(StatusCodigo.Baixado, codigo.Status);
            Assert.Equal("PB900010", codigo.NumeroGuiaReal);
            Assert.Equal(DateOnly.FromDateTime(DateTime.Today), codigo.DataBaixa);
            Assert.Equal(_banco.Gerente.Login, codigo.UsuarioBaixa);
        }

        // Fechar devolve ao painel, e a guia baixada saiu das pendências.
        Clicar(tela, tela.Find("[data-fechar]"));
        ConfirmarDialogos(tela);
        Esperar(() => tela.FindAll("[data-guia-ans]").Count == 0);
        Esperar(() => !tela.FindAll("tr").Any(tr => tr.TextContent.Contains(marca)));
    }

    [Fact]
    public void Recebida_sem_numero_a_baixa_fica_com_o_nosso_numero_e_a_frase_diz_isso()
    {
        var marca = Marca();
        var codigoId = Pendente(marca);
        using var ctx = Contexto();
        var tela = AbrirBaixa(ctx, marca);
        CompletarGuia(tela);
        Enviar(tela);

        string nosso = string.Empty;
        Responder(codigoId, item =>
        {
            nosso = item.NumeroGuiaPrestador;
            return new ConclusaoTransacaoTiss
            {
                Resultado = "Lote protocolado.", Protocolo = "20269900011",
                Guias = [new ResultadoDaGuiaTiss(item.NumeroGuiaPrestador, null, SituacaoGuiaTiss.Recebida)]
            };
        });

        Esperar(() => tela.FindAll("[data-acompanhamento='Baixada']").Count == 1);
        Assert.Equal($"Petrobras recebeu a guia (protocolo 20269900011) e não informou o número dela; a baixa ficou com o nosso número {nosso}.",
            tela.Find("[data-desfecho]").TextContent.Trim());
        Assert.DoesNotContain("gerada por", tela.Find("[data-acompanhamento]").TextContent);

        using var db = Db();
        var codigo = db.Codigos.AsNoTracking().Single(c => c.Id == codigoId);
        Assert.Equal(StatusCodigo.Baixado, codigo.Status);
        Assert.Equal(nosso, codigo.NumeroGuiaReal);
    }

    // ==================== b) glosada ====================

    [Fact]
    public void Glosada_nada_e_baixado_o_motivo_aparece_e_os_campos_voltam_para_corrigir_e_enviar_de_novo()
    {
        var marca = Marca();
        var codigoId = Pendente(marca);
        using var ctx = Contexto();
        var tela = AbrirBaixa(ctx, marca);
        CompletarGuia(tela);
        Enviar(tela);

        Responder(codigoId, item => new ConclusaoTransacaoTiss
        {
            Resultado = "Lote protocolado.", Protocolo = "20269900012",
            Guias = [new ResultadoDaGuiaTiss(item.NumeroGuiaPrestador, null, SituacaoGuiaTiss.Glosada, "1001 - Número da carteira inválido")]
        });

        Esperar(() => tela.FindAll("[data-acompanhamento='Glosada']").Count == 1);
        var bloco = tela.Find("[data-acompanhamento]").TextContent;
        Assert.Contains("recusou esta guia", bloco);
        Assert.Contains("1001 - Número da carteira inválido", bloco);
        Assert.Contains("Nada foi baixado", bloco);
        Assert.False(tela.Find("fieldset.fat-guia-campos").HasAttribute("disabled"));
        Assert.Equal("Corrigir e enviar de novo para Petrobras", tela.Find("[data-enviar-e-baixar]").TextContent.Trim());
        using (var db = Db())
        {
            var codigo = db.Codigos.AsNoTracking().Single(c => c.Id == codigoId);
            Assert.Equal(StatusCodigo.Aberto, codigo.Status);
            Assert.Null(codigo.NumeroGuiaReal);
        }

        // E o reenvio da guia corrigida é um pedido NOVO, pela mesma porta.
        Enviar(tela);
        Esperar(() => EnviosDa(codigoId).Count == 2);
        Assert.Equal("NaFila", tela.Find("[data-acompanhamento]").GetAttribute("data-acompanhamento"));
    }

    // ==================== c) não se sabe se chegou ====================

    [Fact]
    public void Nao_se_sabe_se_chegou_nao_ha_botao_de_reenvio_e_a_saida_e_a_baixa_com_o_numero_do_portal()
    {
        var marca = Marca();
        var codigoId = Pendente(marca);
        using var ctx = Contexto();
        var tela = AbrirBaixa(ctx, marca);
        CompletarGuia(tela);
        Enviar(tela);

        Falhar(codigoId, "A operadora não respondeu (tempo esgotado).");

        Esperar(() => tela.FindAll("[data-acompanhamento='NaoSeSabeSeChegou']").Count == 1);
        Assert.Contains("Confira no portal da operadora ANTES de qualquer novo envio", tela.Find("[data-acompanhamento]").TextContent);
        // Nenhuma ação de envio — nem a principal, nem o lote, nem "tentar de novo".
        Assert.Empty(tela.FindAll("[data-enviar-e-baixar]"));
        Assert.Empty(tela.FindAll("[data-conferir-para-envio]"));
        Assert.DoesNotContain(tela.FindAll("button"), b => b.TextContent.Contains("nviar"));
        Assert.True(tela.Find("fieldset.fat-guia-campos").HasAttribute("disabled"));
        Assert.Single(EnviosDa(codigoId));

        // Reabrir o formulário não muda isso: ele volta ao mesmo aviso.
        Clicar(tela, tela.Find("[data-fechar]"));
        Esperar(() => tela.FindAll("[data-guia-ans]").Count == 0);
        AbrirBaixaNaTela(tela, marca);
        Assert.Single(tela.FindAll("[data-acompanhamento='NaoSeSabeSeChegou']"));
        Assert.Empty(tela.FindAll("[data-enviar-e-baixar]"));

        // A saída oferecida: a baixa manual, com o número conferido no portal.
        Clicar(tela, tela.Find("[data-baixa-manual]"));
        tela.Find("[data-baixa-no-sistema] input.numero-guia").Input("PB900013");
        Assentar(tela);
        Clicar(tela, tela.Find("[data-confirmar-baixa-manual]"));
        ConfirmarDialogos(tela);
        Esperar(() => tela.FindAll("[data-guia-ans]").Count == 0);
        using var db = Db();
        var codigo = db.Codigos.AsNoTracking().Single(c => c.Id == codigoId);
        Assert.Equal(StatusCodigo.Baixado, codigo.Status);
        Assert.Equal("PB900013", codigo.NumeroGuiaReal);
        Assert.Single(EnviosDa(codigoId)); // e nada novo foi à fila
    }

    // ==================== d) o serviço não respondeu ====================

    /// <summary>
    /// O pedido está na fila há mais que o prazo (aqui: pedido ontem, formulário reaberto hoje — a
    /// mesma conta que o relógio faz depois de 45 s). Cancelar tira o pedido da fila e devolve a guia.
    /// </summary>
    [Fact]
    public void Servico_que_nao_respondeu_oferece_esperar_cancelar_ou_baixa_manual_e_o_cancelar_funciona()
    {
        var marca = Marca();
        var codigoId = Conferida(marca);
        int envioId;
        using (var db = Db())
        {
            envioId = new FilaTissService(new ClinicaRepositorio(db)).EnfileirarEnvioAsync(Petrobras, [codigoId], "secretaria.teste").GetAwaiter().GetResult().Id;
            var t = db.TransacoesTiss.Single(x => x.Id == envioId);
            t.PedidoEm = t.PedidoEm.AddDays(-1);
            db.SaveChanges();
        }
        using var ctx = Contexto();
        var tela = AbrirBaixa(ctx, marca);

        Assert.Equal("ServicoSemResposta", tela.Find("[data-acompanhamento]").GetAttribute("data-acompanhamento"));
        Assert.Equal("O serviço de envio não respondeu.", tela.Find("[data-desfecho]").TextContent.Trim());
        Assert.Single(tela.FindAll("[data-continuar-esperando]"));
        Assert.Single(tela.FindAll("[data-baixa-manual]"));
        Assert.Empty(tela.FindAll("[data-enviar-e-baixar]"));

        // "Continuar esperando" volta a acompanhar o MESMO pedido, sem criar outro.
        Clicar(tela, tela.Find("[data-continuar-esperando]"));
        Assert.Equal("NaFila", tela.Find("[data-acompanhamento]").GetAttribute("data-acompanhamento"));
        Assert.Single(EnviosDa(codigoId));

        Clicar(tela, tela.Find("[data-cancelar-pedido]"));
        Esperar(() => tela.FindAll("[data-acompanhamento='NaoEnviada']").Count == 1);
        using (var db = Db())
            Assert.Equal(EstadoTransacaoTiss.Cancelada, db.TransacoesTiss.AsNoTracking().Single(t => t.Id == envioId).Estado);
        // Nada foi à operadora: a guia volta a poder ser enviada.
        Assert.Single(tela.FindAll("[data-enviar-e-baixar]"));
        Assert.Equal(StatusCodigo.Aberto, CodigoDe(codigoId).Status);
    }

    // ==================== reabrir ====================

    [Fact]
    public void Reabrir_a_baixa_de_guia_com_pedido_em_andamento_cai_no_acompanhamento_e_nao_oferece_enviar()
    {
        var marca = Marca();
        var codigoId = Conferida(marca);
        using (var db = Db())
            new FilaTissService(new ClinicaRepositorio(db)).EnfileirarEnvioAsync(Petrobras, [codigoId], "secretaria.teste").GetAwaiter().GetResult();
        using var ctx = Contexto();
        var tela = AbrirBaixa(ctx, marca);

        Assert.Equal("NaFila", tela.Find("[data-acompanhamento]").GetAttribute("data-acompanhamento"));
        Assert.Empty(tela.FindAll("[data-enviar-e-baixar]"));
        Assert.Empty(tela.FindAll("[data-conferir-para-envio]"));
        Assert.True(tela.Find("fieldset.fat-guia-campos").HasAttribute("disabled"));

        // A resposta chega com o formulário reaberto por OUTRA abertura que não a do pedido: a tela
        // mostra a guia recebida e pede o clique — quem registra a baixa é quem está olhando.
        Responder(codigoId, item => new ConclusaoTransacaoTiss
        {
            Resultado = "Lote protocolado.", Protocolo = "20269900014",
            Guias = [new ResultadoDaGuiaTiss(item.NumeroGuiaPrestador, "PB900014", SituacaoGuiaTiss.Recebida)]
        });
        Esperar(() => tela.FindAll("[data-registrar-baixa]").Count == 1);
        Assert.Equal(StatusCodigo.Aberto, CodigoDe(codigoId).Status);
        Clicar(tela, tela.Find("[data-registrar-baixa]"));
        Esperar(() => tela.FindAll("[data-acompanhamento='Baixada']").Count == 1);
        Assert.Equal("PB900014", CodigoDe(codigoId).NumeroGuiaReal);
    }

    // ==================== a reserva: baixa manual recolhida ====================

    [Fact]
    public void A_baixa_manual_fica_recolhida_e_ainda_funciona_sem_pedir_envio_nenhum()
    {
        var marca = Marca();
        var codigoId = Pendente(marca);
        using var ctx = Contexto();
        var tela = AbrirBaixa(ctx, marca);
        CompletarGuia(tela);
        Assert.Empty(tela.FindAll("[data-confirmar-baixa-manual]"));

        Clicar(tela, tela.Find("[data-baixa-manual]"));
        Assert.Single(tela.FindAll("[data-baixa-no-sistema] input.numero-guia"));
        // Sem número, a segunda barreira responde com a frase — e nada grava.
        Clicar(tela, tela.Find("[data-confirmar-baixa-manual]"));
        Assert.Contains("Informe o número da guia", tela.Markup);
        Assert.Equal(StatusCodigo.Aberto, CodigoDe(codigoId).Status);

        tela.Find("[data-baixa-no-sistema] input.numero-guia").Input("PB900015");
        Assentar(tela);
        Clicar(tela, tela.Find("[data-confirmar-baixa-manual]"));
        ConfirmarDialogos(tela);
        Esperar(() => tela.FindAll("[data-guia-ans]").Count == 0);

        var codigo = CodigoDe(codigoId);
        Assert.Equal(StatusCodigo.Baixado, codigo.Status);
        Assert.Equal("PB900015", codigo.NumeroGuiaReal);
        Assert.Empty(EnviosDa(codigoId));
    }

    // ==================== a aba de envio em lote ====================

    /// <summary>Na aba "Envio pelo web service": a resposta chega com a tela aberta e a baixa das recebidas é feita sozinha.</summary>
    [Fact]
    public void Na_aba_de_envio_a_resposta_que_chega_com_a_tela_aberta_da_baixa_nas_recebidas_sozinha()
    {
        var codigoId = Conferida(Marca());
        using var ctx = Contexto();
        var tela = AbrirEnvio(ctx);
        Marcar(tela, codigoId);
        tela.Find("[data-enviar]").Click();
        Esperar(() => tela.FindAll("[role=alertdialog]").Count > 0);
        ConfirmarDialogos(tela);
        Esperar(() => EnviosDa(codigoId).Count == 1);

        Responder(codigoId, item => new ConclusaoTransacaoTiss
        {
            Resultado = "Lote protocolado.", Protocolo = "20269900016",
            Guias = [new ResultadoDaGuiaTiss(item.NumeroGuiaPrestador, "PB900016", SituacaoGuiaTiss.Recebida)]
        });

        Esperar(() => CodigoDe(codigoId).Status == StatusCodigo.Baixado);
        var codigo = CodigoDe(codigoId);
        Assert.Equal("PB900016", codigo.NumeroGuiaReal);
        Assert.Equal(_banco.Gerente.Login, codigo.UsuarioBaixa); // quem pediu o envio nesta tela é quem assina a baixa
    }

    // ==================== apoio do fluxo ====================

    /// <summary>Pendências → "Dar baixa" na linha da marca, com o formulário já lido.</summary>
    private static IRenderedComponent<Raiz> AbrirBaixa(TestContext ctx, string marca)
    {
        var tela = Abrir(ctx, typeof(Clinica.Ui.Blazor.Telas.PendenciasFaturamento));
        AbrirBaixaNaTela(tela, marca);
        return tela;
    }

    private static void AbrirBaixaNaTela(IRenderedComponent<Raiz> tela, string marca)
    {
        Esperar(() => tela.FindAll("tr").Any(tr => tr.TextContent.Contains(marca)));
        var linha = tela.FindAll("tr").First(tr => tr.TextContent.Contains(marca));
        Clicar(tela, linha.QuerySelectorAll("button").First(b => b.TextContent.Trim() == "Dar baixa"));
        Esperar(() => tela.FindAll("[data-guia-ans]").Count == 1);
    }

    /// <summary>O clique único, com o "sim" da pergunta, até o formulário entrar no acompanhamento do pedido novo.</summary>
    private static void Enviar(IRenderedComponent<Raiz> tela)
    {
        tela.Find("[data-enviar-e-baixar]").Click();
        Esperar(() => tela.FindAll("[role=alertdialog]").Count > 0);
        ConfirmarDialogos(tela);
        Esperar(() => tela.FindAll("[data-acompanhamento='NaFila']").Count == 1);
    }

    /// <summary>O que o serviço de envio faria ao reservar ESTE pedido e levá-lo à operadora (reserva escrita direto, para não mexer na fila dos outros testes).</summary>
    private (FilaTissService Fila, ClinicaDbContext Db, int EnvioId, TransacaoTissGuia Item) Reservar(int codigoId)
    {
        var db = Db();
        var item = db.TransacoesTissGuias.AsNoTracking().Where(g => g.CodigoId == codigoId).OrderByDescending(g => g.Id).First();
        var t = db.TransacoesTiss.Single(x => x.Id == item.TransacaoId);
        t.Estado = EstadoTransacaoTiss.EmEnvio;
        t.Consumidor = "teste";
        t.ReservadaEm = DateTime.Now;
        t.EnvioIniciadoEm = DateTime.Now;
        db.SaveChanges();
        db.ChangeTracker.Clear();
        return (new FilaTissService(new ClinicaRepositorio(db)), db, t.Id, item);
    }

    private void Responder(int codigoId, Func<TransacaoTissGuia, ConclusaoTransacaoTiss> resposta)
    {
        var (fila, db, envioId, item) = Reservar(codigoId);
        using (db) fila.ConcluirAsync(envioId, resposta(item)).GetAwaiter().GetResult();
    }

    private void Falhar(int codigoId, string erro)
    {
        var (fila, db, envioId, _) = Reservar(codigoId);
        using (db) fila.FalharAsync(envioId, erro, podeTentarDeNovo: true).GetAwaiter().GetResult();
    }

    private CodigoFaturamento CodigoDe(int codigoId)
    {
        using var db = Db();
        return db.Codigos.AsNoTracking().Single(c => c.Id == codigoId);
    }

    // ==================== apoio ====================

    private static readonly ProtecaoSegredoGlobal ChaveDeTeste = new(Convert.ToBase64String(new byte[32]));

    private static string Marca() => $"E{Guid.NewGuid():N}"[..10];

    /// <summary>Uma consulta de hoje de uma paciente da Saúde Petrobras, PENDENTE e ainda não conferida.</summary>
    private int Pendente(string marca) => Lancar(marca, conferir: false);

    /// <summary>A mesma guia já CONFERIDA (pronta para envio), pelo serviço — como o formulário de baixa a deixa.</summary>
    private int Conferida(string marca) => Lancar(marca, conferir: true);

    private int Lancar(string marca, bool conferir)
    {
        using var db = Db();
        var repo = new ClinicaRepositorio(db);
        var hoje = DateOnly.FromDateTime(DateTime.Today);
        var paciente = new Paciente
        {
            Nome = $"Envio {marca}", Convenio = Convenio.Petrobras, ConvenioCodigo = Petrobras, Carteirinha = "010203040508",
            ValidadeCarteirinha = hoje.AddDays(120), Sexo = Sexo.Feminino
        };
        db.Add(paciente);
        db.SaveChanges();
        var codigo = new AtendimentoService(repo).LancarAsync(paciente.Id, hoje, ModalidadeAtendimento.Consulta).GetAwaiter().GetResult()
            .Atendimento.Codigos.OrderBy(c => c.Ordem).First();
        if (conferir)
            new FaturamentoService(repo).ConferirGuiaAnsAsync(codigo.Id, GuiaAnsDaPrevia.Para(codigo, paciente, hoje)!, "secretaria.teste").GetAwaiter().GetResult();
        return codigo.Id;
    }

    private void DefinirEnvio(bool ligado)
    {
        using var db = Db();
        new CredenciaisTissService(new ClinicaRepositorio(db), ChaveDeTeste).DefinirEnvioAsync(Petrobras, ligado, "teste").GetAwaiter().GetResult();
    }

    private List<TransacaoTissGuia> EnviosDa(int codigoId)
    {
        using var db = Db();
        return db.TransacoesTissGuias.AsNoTracking().Where(g => g.CodigoId == codigoId).ToList();
    }

    /// <summary>Completa a guia ANS aberta com valores explícitos (o teste não depende do que a semente cadastrou).</summary>
    private static void CompletarGuia(IRenderedComponent<Raiz> tela)
    {
        void Texto(string campo, string valor) { tela.Find($"#ga-{campo}").Input(valor); Assentar(tela); }
        void Escolher(string campo, string valor) { tela.Find($"#ga-{campo}").Change(valor); Assentar(tela); }
        Texto("RegistroAns", "000000");
        Texto("CodigoPrestadorNaOperadora", "00000000000100");
        Texto("Cnes", "9999999");
        Texto("NomeProfissional", "Dra. Teste do Envio");
        Escolher("ConselhoProfissional", "06");
        Texto("NumeroConselho", "529999");
        Escolher("UfConselho", "33");
        Escolher("Cbos", "225105");
        Escolher("TipoConsulta", "1");
        Escolher("CodigoTabela", "22");
        Texto("CodigoProcedimento", "10101012");
        Texto("ValorProcedimento", "139,00");
        Assert.DoesNotContain("campo-erro", tela.Find("[data-guia-ans]").InnerHtml);
    }

    /// <summary>Guias TISS → aba "Envio pelo web service", já lida.</summary>
    private static IRenderedComponent<Raiz> AbrirEnvio(TestContext ctx)
    {
        var tela = Abrir(ctx, typeof(Clinica.Ui.Blazor.Telas.LotesTiss));
        Clicar(tela, tela.Find("[data-aba-envio]"));
        Esperar(() => tela.FindAll("[data-envios]").Count == 1);
        return tela;
    }

    private static void Marcar(IRenderedComponent<Raiz> tela, int codigoId)
    {
        tela.Find($"[data-pronta='{codigoId}'] input[type=checkbox]").Change(true);
        Assentar(tela);
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
        // A chave de proteção do servidor, de mentira: é o que deixa a credencial ser gravada no teste.
        ctx.Services.AddSingleton(ChaveDeTeste);

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

    /// <summary>Responde "sim" a toda pergunta que aparecer.</summary>
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
