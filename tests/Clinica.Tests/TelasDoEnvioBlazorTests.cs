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
/// AS TELAS DO ENVIO À OPERADORA (nov/2026), clicando de verdade (bUnit) sobre o serviço de verdade
/// (FilaTissService, FaturamentoService, CredenciaisTissService) e afirmando o ESTADO do banco:
///
/// 1. "Conferir e deixar pronta para envio" grava a conferência e NÃO dá baixa;
/// 2. enviar duas guias enfileira UMA transação com dois itens, depois da pergunta;
/// 3. guia impedida aparece NA LINHA e nada entra na fila;
/// 4. o resultado da operadora (gravado pelo mesmo serviço que o conector usa) vira selo por guia,
///    e "Dar baixa nas recebidas" baixa só a recebida, com o número devolvido;
/// 5. a credencial é gravada e a senha não volta ao markup;
/// 6. com a chave de envio desligada, as ações de envio somem;
/// 7. sem o acesso, o clique devolve a frase e nada é pedido.
///
/// Nenhum teste fala com operadora: na prévia e aqui não há serviço de envio.
/// </summary>
[Collection(ColecaoDiagnostico.Nome)]
public sealed class TelasDoEnvioBlazorTests : IClassFixture<BancoDasTelas>, IDisposable
{
    private const string Petrobras = "Petrobras";
    private static readonly TimeSpan Prazo = TimeSpan.FromSeconds(20);
    private readonly BancoDasTelas _banco;

    public TelasDoEnvioBlazorTests(BancoDasTelas banco)
    {
        _banco = banco;
        var ptBr = new System.Globalization.CultureInfo("pt-BR");
        System.Globalization.CultureInfo.CurrentCulture = ptBr;
        System.Globalization.CultureInfo.CurrentUICulture = ptBr;
        new SessaoUsuario().Entrar(banco.Gerente);
    }

    public void Dispose() => new SessaoUsuario().Entrar(new UsuarioSistema());

    // ==================== 1. pronta para envio ====================

    [Fact]
    public void Conferir_e_deixar_pronta_para_envio_grava_a_conferencia_e_NAO_da_baixa()
    {
        var marca = Marca();
        var codigoId = Pendente(marca);
        using var ctx = Contexto();
        var tela = Abrir(ctx, typeof(Clinica.Ui.Blazor.Telas.PendenciasFaturamento));
        // Antes de conferir, a linha diz que a guia não foi enviada.
        Assert.Equal("Não enviada", tela.Find($"[data-selo-envio='{codigoId}']").TextContent.Trim());

        var linha = tela.FindAll("tr").First(tr => tr.TextContent.Contains(marca));
        Clicar(tela, linha.QuerySelectorAll("button").First(b => b.TextContent.Trim() == "Dar baixa"));
        Esperar(() => tela.FindAll("[data-conferir-para-envio]").Count == 1);
        // O rótulo diz o que o botão faz: deixa para o lote — não envia.
        Assert.Equal("Deixar para enviar em lote", tela.Find("[data-conferir-para-envio]").TextContent.Trim());
        CompletarGuia(tela);

        Clicar(tela, tela.Find("[data-conferir-para-envio]"));
        Esperar(() => tela.FindAll("[data-guia-ans]").Count == 0);

        using (var db = Db())
        {
            var codigo = db.Codigos.AsNoTracking().Include(c => c.GuiaAns).Single(c => c.Id == codigoId);
            Assert.NotNull(codigo.GuiaAns);
            Assert.Equal(_banco.Gerente.Login, codigo.GuiaAns!.ConferidaPor);
            // A guia continua PENDENTE: sem baixa, sem número.
            Assert.Equal(StatusCodigo.Aberto, codigo.Status);
            Assert.Null(codigo.DataBaixa);
            Assert.Null(codigo.NumeroGuiaReal);
            Assert.Empty(db.TransacoesTissGuias.AsNoTracking().Where(g => g.CodigoId == codigoId));
        }
        // E o painel, relido, passa a dizer "pronta para envio" — não "enviada".
        Esperar(() => tela.FindAll($"[data-selo-envio='{codigoId}']").Any(s => s.TextContent.Trim() == "Pronta para envio"));
    }

    // ==================== 2 e 3. enviar ====================

    [Fact]
    public void Enviar_duas_guias_enfileira_uma_transacao_com_dois_itens_depois_da_pergunta()
    {
        var a = Conferida(Marca());
        var b = Conferida(Marca());
        using var ctx = Contexto();
        var tela = AbrirEnvio(ctx);

        Marcar(tela, a);
        Marcar(tela, b);
        Assert.Empty(tela.FindAll("[data-recusa]"));
        tela.Find("[data-enviar]").Click();
        // A pergunta diz quantas e que é cobrança de verdade.
        Esperar(() => tela.FindAll("[role=alertdialog]").Count > 0);
        var pergunta = tela.Find("[role=alertdialog]").TextContent;
        Assert.Contains("2 guia(s)", pergunta);
        Assert.Contains("COBRANÇA", pergunta);
        Assert.Empty(EnviosDa(a)); // nada entrou na fila antes do "sim"
        ConfirmarDialogos(tela);
        Esperar(() => EnviosDa(a).Count == 1);

        using var db = Db();
        var envio = db.TransacoesTiss.AsNoTracking().Include(t => t.Guias).Single(t => t.Guias.Any(g => g.CodigoId == a));
        Assert.Equal(TipoTransacaoTiss.EnvioGuia, envio.Tipo);
        Assert.Equal(EstadoTransacaoTiss.Pendente, envio.Estado);
        Assert.Equal(_banco.Gerente.Login, envio.PedidoPor);
        Assert.Equal(new[] { a, b }.OrderBy(x => x), envio.Guias.Select(g => g.CodigoId!.Value).OrderBy(x => x));

        // A tela mostra o envio NA FILA (não "enviado"), e as duas guias saíram das prontas.
        Esperar(() => tela.FindAll($"[data-envio='{envio.Id}']").Count == 1);
        var bloco = tela.Find($"[data-envio='{envio.Id}']");
        Assert.Contains("Na fila", bloco.TextContent);
        Assert.Contains("Ainda não foi à operadora", bloco.TextContent);
        Assert.Empty(tela.FindAll($"[data-pronta='{a}']"));
        Assert.NotEmpty(bloco.QuerySelectorAll($"[data-cancelar-pedido='{envio.Id}']"));
    }

    /// <summary>
    /// A guia ganhou baixa noutra máquina depois de a lista ser lida: ao enviar, a recusa aparece NA
    /// LINHA dela, o envio não é pedido — nem para a outra guia, que estava em ordem — e nada grava.
    /// </summary>
    [Fact]
    public void Guia_impedida_aparece_na_linha_e_nada_entra_na_fila()
    {
        var boa = Conferida(Marca());
        var impedida = Conferida(Marca());
        using var ctx = Contexto();
        var tela = AbrirEnvio(ctx);
        Marcar(tela, boa);
        Marcar(tela, impedida);

        using (var db = Db())
        {
            var repo = new ClinicaRepositorio(db);
            var codigo = repo.ObterCodigoAsync(impedida).GetAwaiter().GetResult()!;
            new FaturamentoService(repo).DarBaixaAsync(impedida, DateOnly.FromDateTime(DateTime.Today), "PB700001", "outra.maquina", null,
                guiaAns: GuiaAnsNaBaixa.DaGuia(codigo.GuiaAns!)).GetAwaiter().GetResult();
        }

        Clicar(tela, tela.Find("[data-enviar]"));

        Esperar(() => tela.FindAll($"[data-recusa='{impedida}']").Count == 1);
        Assert.Contains("já tem baixa", tela.Find($"[data-recusa='{impedida}']").TextContent);
        Assert.Empty(tela.FindAll($"[data-recusa='{boa}']"));
        Assert.Contains("Nada foi enviado", tela.Find("[data-prontas]").TextContent);
        Assert.Empty(tela.FindAll("[role=alertdialog]")); // nem chegou a perguntar
        Assert.Empty(EnviosDa(boa));
        Assert.Empty(EnviosDa(impedida));
    }

    /// <summary>
    /// "NÃO SE SABE SE CHEGOU" nunca vira reenvio com um clique: a guia volta à lista das prontas
    /// (a fila a libera para um pedido novo), mas TRAVADA — não se marca, o "marcar todas" a pula —
    /// até a pessoa declarar, numa pergunta, que conferiu no portal que ela não está lá.
    /// </summary>
    [Fact]
    public void Guia_de_envio_em_duvida_fica_travada_ate_a_pessoa_declarar_que_conferiu_no_portal()
    {
        var emDuvida = Conferida(Marca());
        var normal = Conferida(Marca());
        int envioId;
        using (var db = Db())
        {
            var fila = new FilaTissService(new ClinicaRepositorio(db));
            envioId = fila.EnfileirarEnvioAsync(Petrobras, [emDuvida], "secretaria.teste").GetAwaiter().GetResult().Id;
            var t = db.TransacoesTiss.Single(x => x.Id == envioId);
            t.Estado = EstadoTransacaoTiss.EmEnvio;
            t.Consumidor = "teste";
            t.ReservadaEm = DateTime.Now;
            t.EnvioIniciadoEm = DateTime.Now; // a mensagem SAIU
            db.SaveChanges();
            db.ChangeTracker.Clear();
            fila.FalharAsync(envioId, "A operadora não respondeu (tempo esgotado).", podeTentarDeNovo: true).GetAwaiter().GetResult();
        }

        using var ctx = Contexto();
        var tela = AbrirEnvio(ctx);
        // O bloco do envio: em destaque, com a ordem de conferir no portal, e SEM botão nenhum.
        var bloco = tela.Find($"[data-envio='{envioId}']");
        Assert.Contains("Não se sabe se chegou", bloco.TextContent);
        Assert.Contains("Confira no portal da operadora ANTES de enviar", bloco.TextContent);
        Assert.Empty(bloco.QuerySelectorAll("button"));

        // A guia voltou às prontas, travada: caixa apagada, aviso na linha, botão de liberar.
        var caixa = tela.Find($"[data-pronta='{emDuvida}'] input[type=checkbox]");
        Assert.True(caixa.HasAttribute("disabled"));
        Assert.Contains("Confira no portal ANTES", tela.Find($"[data-ressalva='{emDuvida}']").TextContent);
        // O clique na caixa apagada (o bUnit clica) não marca; e "marcar todas" a pula.
        caixa.Change(true);
        Assentar(tela);
        tela.Find("[data-prontas] thead input[type=checkbox]").Change(true);
        Assentar(tela);
        Assert.DoesNotContain("marcada", tela.Find($"[data-pronta='{emDuvida}']").ClassList);
        Assert.Contains("marcada", tela.Find($"[data-pronta='{normal}']").ClassList);
        tela.Find("[data-prontas] thead input[type=checkbox]").Change(false);
        Assentar(tela);

        // Liberar é uma PERGUNTA, não um clique: dizer "não" mantém a trava.
        tela.Find($"[data-liberar='{emDuvida}']").Click();
        Esperar(() => tela.FindAll("[role=alertdialog]").Count > 0);
        Assert.Contains("NÃO está lá", tela.Find("[role=alertdialog]").TextContent);
        tela.FindAll("[role=alertdialog] .modal-rodape button").First().Click();
        Assentar(tela);
        Assert.True(tela.Find($"[data-pronta='{emDuvida}'] input[type=checkbox]").HasAttribute("disabled"));

        // Dizer "conferi: não está lá" libera a guia para ser marcada — e ainda assim nada foi enviado.
        tela.Find($"[data-liberar='{emDuvida}']").Click();
        Esperar(() => tela.FindAll("[role=alertdialog]").Count > 0);
        ConfirmarDialogos(tela);
        Esperar(() => !tela.Find($"[data-pronta='{emDuvida}'] input[type=checkbox]").HasAttribute("disabled"));
        Marcar(tela, emDuvida);
        Assert.Contains("marcada", tela.Find($"[data-pronta='{emDuvida}']").ClassList);
        Assert.Single(EnviosDa(emDuvida)); // só o envio antigo: liberar e marcar não pedem envio
    }

    // ==================== 4. o resultado e a baixa das recebidas ====================

    [Fact]
    public void Resultado_da_operadora_vira_selo_por_guia_e_dar_baixa_nas_recebidas_baixa_so_a_recebida()
    {
        var recebida = Conferida(Marca());
        var glosada = Conferida(Marca());
        int envioId;
        using (var db = Db())
        {
            var repo = new ClinicaRepositorio(db);
            var fila = new FilaTissService(repo);
            var envio = fila.EnfileirarEnvioAsync(Petrobras, [recebida, glosada], "secretaria.teste").GetAwaiter().GetResult();
            envioId = envio.Id;
            // O que o serviço de envio faria ao reservar ESTA transação e levá-la à operadora. A reserva é
            // escrita direto (e não pelo ReservarProximaAsync) para não mexer nos outros pedidos da fila.
            var t = db.TransacoesTiss.Single(x => x.Id == envioId);
            t.Estado = EstadoTransacaoTiss.EmEnvio;
            t.Consumidor = "teste";
            t.ReservadaEm = DateTime.Now;
            t.EnvioIniciadoEm = DateTime.Now;
            db.SaveChanges();
            db.ChangeTracker.Clear();
            var itens = db.TransacoesTissGuias.AsNoTracking().Where(g => g.TransacaoId == envioId).ToList();
            fila.ConcluirAsync(envioId, new ConclusaoTransacaoTiss
            {
                Resultado = "Lote protocolado.", Protocolo = "20269900001",
                Guias =
                [
                    new ResultadoDaGuiaTiss(itens.Single(i => i.CodigoId == recebida).NumeroGuiaPrestador, "PB900001", SituacaoGuiaTiss.Recebida),
                    new ResultadoDaGuiaTiss(itens.Single(i => i.CodigoId == glosada).NumeroGuiaPrestador, null, SituacaoGuiaTiss.Glosada, "1001 - Número da carteira inválido")
                ]
            }).GetAwaiter().GetResult();
        }

        using var ctx = Contexto();
        var tela = AbrirEnvio(ctx);
        var bloco = tela.Find($"[data-envio='{envioId}']");
        // O lote: recebido com glosa, com o protocolo; cada guia com o seu selo e o motivo da glosa.
        Assert.Contains("Recebido com glosa", bloco.TextContent);
        Assert.Contains("20269900001", bloco.TextContent);
        Assert.Contains("Recebida pela operadora", bloco.QuerySelector($"[data-item-da-guia='{recebida}']")!.TextContent);
        Assert.Contains("Glosada: 1001 - Número da carteira inválido", bloco.QuerySelector($"[data-item-da-guia='{glosada}']")!.TextContent);
        // O botão oferece só a recebida.
        Assert.Contains("· 1", bloco.QuerySelector($"[data-baixar-recebidas='{envioId}']")!.TextContent);

        Clicar(tela, tela.Find($"[data-baixar-recebidas='{envioId}']"));
        ConfirmarDialogos(tela);
        Esperar(() => Contar(c => c.Id == recebida && c.Status == StatusCodigo.Baixado) == 1);

        using (var db = Db())
        {
            var baixada = db.Codigos.AsNoTracking().Single(c => c.Id == recebida);
            Assert.Equal("PB900001", baixada.NumeroGuiaReal); // o número que a operadora devolveu
            Assert.Equal(_banco.Gerente.Login, baixada.UsuarioBaixa);
            var naoBaixada = db.Codigos.AsNoTracking().Single(c => c.Id == glosada);
            Assert.Equal(StatusCodigo.Aberto, naoBaixada.Status); // a glosada pede gente
            Assert.Null(naoBaixada.DataBaixa);
        }
        // A tela diz que baixou e some com o botão: não há mais recebida sem baixa neste envio.
        Esperar(() => tela.Find($"[data-envio='{envioId}']").TextContent.Contains("Baixa registrada · nº PB900001"));
        Assert.Empty(tela.FindAll($"[data-baixar-recebidas='{envioId}']"));
    }

    // ==================== 5. a credencial ====================

    [Fact]
    public void Gravar_a_credencial_nao_devolve_a_senha_ao_markup_e_a_tela_so_diz_que_ha_senha()
    {
        const string senha = "Segredo-De-Teste-931";
        using var ctx = Contexto();
        var tela = Abrir(ctx, typeof(Clinica.Ui.Blazor.Telas.ConfiguracoesFaturamento));
        Clicar(tela, tela.Find($"[data-web-service='{Petrobras}']"));
        Esperar(() => tela.FindAll("[data-credencial]").Count == 1);

        tela.Find("#ws-Login").Input("clinica.teste");
        tela.Find("#ws-Senha").Input(senha);
        Assentar(tela);
        // Nem enquanto se digita a senha é escrita na página: o campo não tem o valor ligado de volta.
        Assert.DoesNotContain(senha, tela.Markup);

        Clicar(tela, tela.Find("[data-gravar-credencial]"));
        Esperar(() => tela.Markup.Contains("Credencial gravada"));

        Assert.DoesNotContain(senha, tela.Markup);
        Assert.Contains("Há uma senha gravada", tela.Find("[data-credencial]").TextContent);
        Assert.Equal("clinica.teste", tela.Find("#ws-Login").GetAttribute("value"));
        Assert.Null(tela.Find("#ws-Senha").GetAttribute("value"));

        using var db = Db();
        var credenciais = new CredenciaisTissService(new ClinicaRepositorio(db), ChaveDeTeste);
        var naTela = credenciais.ParaATelaAsync(Petrobras).GetAwaiter().GetResult();
        Assert.Equal(new CredencialTissNaTela("clinica.teste", true), naTela);
        // No banco fica o MD5 CIFRADO: nem a senha, nem o MD5 dela em claro.
        var gravado = db.Configuracoes.AsNoTracking().Single(c => c.Chave == CredenciaisTissService.ChaveSenha(Petrobras)).Valor;
        Assert.DoesNotContain(senha, gravado);
        Assert.DoesNotContain(TissElegibilidadeWs.SenhaEmMd5(senha), gravado);
    }

    // ==================== 6. a chave desligada ====================

    [Fact]
    public void Com_a_chave_de_envio_desligada_as_acoes_de_envio_somem_e_a_baixa_fica_como_era()
    {
        var marca = Marca();
        var codigoId = Pendente(marca);
        Conferida(Marca());
        DefinirEnvio(false);
        try
        {
            using var ctx = Contexto();
            var envio = AbrirEnvio(ctx);
            Assert.Contains("está desligado", envio.Find("[data-envio-operadora]").TextContent);
            Assert.Empty(envio.FindAll("[data-prontas]"));
            Assert.Empty(envio.FindAll("[data-enviar]"));

            var painel = Abrir(ctx, typeof(Clinica.Ui.Blazor.Telas.PendenciasFaturamento));
            var linha = painel.FindAll("tr").First(tr => tr.TextContent.Contains(marca));
            Clicar(painel, linha.QuerySelectorAll("button").First(b => b.TextContent.Trim() == "Dar baixa"));
            Esperar(() => painel.FindAll("[data-guia-ans]").Count > 0);
            // O formulário de baixa é o de sempre: só "Confirmar baixa".
            Assert.Empty(painel.FindAll("[data-conferir-para-envio]"));
            Assert.Empty(painel.FindAll("[data-enviar-e-baixar]"));
            Assert.Empty(painel.FindAll("[data-baixa-manual]"));
            // O número volta a ser pedido, obrigatório, e a ação é "Confirmar baixa".
            Assert.Single(painel.FindAll("input.numero-guia[placeholder='Copie do portal da operadora']"));
            Assert.Contains("Confirmar baixa", painel.FindAll(".modal-rodape").Last().TextContent);
            Assert.Equal(0, Contar(c => c.Id == codigoId && c.Status == StatusCodigo.Baixado));
        }
        finally { DefinirEnvio(true); }
    }

    // ==================== 7. sem o acesso ====================

    /// <summary>
    /// A segunda barreira: a tela está aberta e o acesso muda. O clique no botão (que o bUnit faz
    /// mesmo apagado) devolve a frase — nada é perguntado, nada entra na fila, nada estoura.
    /// </summary>
    [Fact]
    public void Sem_o_acesso_o_enviar_e_a_baixa_das_recebidas_recusam_com_a_frase_e_nada_e_pedido()
    {
        var guia = Conferida(Marca());
        using var ctx = Contexto();
        var tela = AbrirEnvio(ctx);
        Marcar(tela, guia);

        new SessaoUsuario().Entrar(new UsuarioSistema { Id = 987655, Nome = "Balcão (teste)", Login = "balcao", Perfil = PerfilAcesso.Recepcao, SenhaHash = "-", SenhaSalt = "-", Ativo = true });
        Assert.False(SessaoUsuario.Atual.Pode(Permissao.GerenciarLotesTiss));

        tela.Find("[data-enviar]").Click();
        Esperar(() => tela.Markup.Contains("Seu acesso não permite enviar guias à operadora."));
        Assert.Empty(tela.FindAll("[role=alertdialog]"));
        Assert.Empty(EnviosDa(guia));

        // O envio da semente com uma recebida aguardando baixa: o clique sem BaixarGuia não baixa.
        var botao = tela.FindAll("[data-baixar-recebidas]").FirstOrDefault();
        if (botao is not null)
        {
            var antes = Contar(c => c.Status == StatusCodigo.Baixado);
            botao.Click();
            Esperar(() => tela.Markup.Contains("Seu acesso não permite dar baixa nas guias."));
            Assert.Empty(tela.FindAll("[role=alertdialog]"));
            Assert.Equal(antes, Contar(c => c.Status == StatusCodigo.Baixado));
        }
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

    private int Contar(Func<CodigoFaturamento, bool> filtro)
    {
        using var db = Db();
        return db.Set<CodigoFaturamento>().AsNoTracking().AsEnumerable().Count(filtro);
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
