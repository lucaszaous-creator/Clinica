using Clinica.Application.Servicos;
using Clinica.Domain.Entities;
using Clinica.Infrastructure;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Clinica.Tests;

/// <summary>
/// A fila do web service TISS (nov/2026): a tela pede, o conector executa. As regras que estes
/// testes fixam são as que protegem o faturamento: quem pede é quem fez login, dois conectores
/// não pegam a mesma linha, e ENVIO DE GUIA NUNCA É REENVIADO SOZINHO.
/// </summary>
public sealed class FilaTissServiceTests : IDisposable
{
    private readonly TissBancoDeTeste _b = new();
    private const string C1 = "conector-1", C2 = "conector-2";

    public void Dispose() => _b.Dispose();

    // ---------- a tela pede ----------

    [Fact]
    public async Task Pedido_de_elegibilidade_entra_pendente_com_o_operador_e_a_trilha_no_mesmo_gravar()
    {
        var (paciente, _) = await _b.GuiaAsync();

        var t = await _b.Fila().EnfileirarElegibilidadeAsync(TissBancoDeTeste.Petrobras, " 000123456789 ", new DateOnly(2027, 12, 31), paciente.Id, TissBancoDeTeste.Operador);

        t.Id.Should().BePositive();
        t.Estado.Should().Be(EstadoTransacaoTiss.Pendente);
        t.Tipo.Should().Be(TipoTransacaoTiss.Elegibilidade);
        t.NumeroCarteira.Should().Be("000123456789");
        t.PedidoPor.Should().Be(TissBancoDeTeste.Operador);
        t.PedidoEm.Should().Be(_b.Agora);
        t.Tentativas.Should().Be(0);
        var trilha = await _b.Db.Auditoria.AsNoTracking().SingleAsync(a => a.Acao == "PedidoElegibilidadeTiss");
        trilha.Operador.Should().Be(TissBancoDeTeste.Operador);
        trilha.PacienteId.Should().Be(paciente.Id);
        // A carteira é dado do beneficiário: não vai para a trilha.
        trilha.Detalhe.Should().NotContain("000123456789");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Pedido_sem_operador_e_recusado_e_nada_entra_na_fila(string operador)
    {
        var pedir = () => _b.Fila().EnfileirarElegibilidadeAsync(TissBancoDeTeste.Petrobras, "000123456789", null, null, operador);

        (await pedir.Should().ThrowAsync<InvalidOperationException>()).WithMessage("*login*");
        (await _b.Db.TransacoesTiss.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Pedido_de_elegibilidade_sem_carteira_e_recusado()
    {
        var pedir = () => _b.Fila().EnfileirarElegibilidadeAsync(TissBancoDeTeste.Petrobras, " ", null, null, TissBancoDeTeste.Operador);
        await pedir.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task A_mesma_guia_nao_entra_duas_vezes_na_fila_de_envio()
    {
        var (paciente, guia) = await _b.GuiaAsync();
        var fila = _b.Fila();
        await fila.EnfileirarEnvioGuiaAsync(TissBancoDeTeste.Petrobras, guia.Id, paciente.Id, TissBancoDeTeste.Operador);

        var deNovo = () => fila.EnfileirarEnvioGuiaAsync(TissBancoDeTeste.Petrobras, guia.Id, paciente.Id, TissBancoDeTeste.Operador);

        (await deNovo.Should().ThrowAsync<InvalidOperationException>()).WithMessage("*já tem um envio*");
        (await _b.Db.TransacoesTiss.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task Guia_ja_enviada_com_protocolo_nao_e_pedida_de_novo()
    {
        var (paciente, guia) = await _b.GuiaAsync();
        var fila = _b.Fila();
        var t = await fila.EnfileirarEnvioGuiaAsync(TissBancoDeTeste.Petrobras, guia.Id, paciente.Id, TissBancoDeTeste.Operador);
        await fila.ReservarProximaAsync(C1);
        await fila.MarcarEnvioIniciadoAsync(t.Id, "<a/>");
        await fila.ConcluirAsync(t.Id, new ConclusaoTransacaoTiss { Resultado = "Recebida.", Protocolo = "987654" });

        var deNovo = () => fila.EnfileirarEnvioGuiaAsync(TissBancoDeTeste.Petrobras, guia.Id, paciente.Id, TissBancoDeTeste.Operador);

        (await deNovo.Should().ThrowAsync<InvalidOperationException>()).WithMessage("*987654*");
    }

    [Fact]
    public async Task Depois_de_um_envio_que_falhou_a_pessoa_pode_pedir_outro()
    {
        // Reenviar é decisão de GENTE, depois de conferir no portal — nunca do conector.
        var (paciente, guia) = await _b.GuiaAsync();
        var fila = _b.Fila();
        var t = await fila.EnfileirarEnvioGuiaAsync(TissBancoDeTeste.Petrobras, guia.Id, paciente.Id, TissBancoDeTeste.Operador);
        await fila.ReservarProximaAsync(C1);
        await fila.MarcarEnvioIniciadoAsync(t.Id, "<a/>");
        await fila.FalharAsync(t.Id, "sem resposta da operadora", podeTentarDeNovo: true);

        var outro = await fila.EnfileirarEnvioGuiaAsync(TissBancoDeTeste.Petrobras, guia.Id, paciente.Id, TissBancoDeTeste.Operador);

        outro.Id.Should().NotBe(t.Id);
        (await fila.DaGuiaAsync(guia.Id)).Select(x => x.Id).Should().Equal(outro.Id, t.Id);
    }

    [Fact]
    public async Task Quem_pediu_desiste_so_enquanto_a_transacao_esta_na_fila()
    {
        var fila = _b.Fila();
        var naFila = await fila.EnfileirarElegibilidadeAsync(TissBancoDeTeste.Petrobras, "000123456789", null, null, TissBancoDeTeste.Operador);
        (await fila.CancelarAsync(naFila.Id, TissBancoDeTeste.Operador)).Should().BeTrue();
        (await fila.ObterAsync(naFila.Id))!.Estado.Should().Be(EstadoTransacaoTiss.Cancelada);
        (await fila.ReservarProximaAsync(C1)).Should().BeNull("transação cancelada não vai à operadora");

        var reservada = await fila.EnfileirarElegibilidadeAsync(TissBancoDeTeste.Petrobras, "000123456789", null, null, TissBancoDeTeste.Operador);
        await fila.ReservarProximaAsync(C1);
        (await fila.CancelarAsync(reservada.Id, TissBancoDeTeste.Operador)).Should().BeFalse("o conector já a pegou");
    }

    // ---------- dois conectores ----------

    [Fact]
    public async Task Reserva_com_a_versao_ja_vista_por_outro_perde_a_corrida()
    {
        // Os dois conectores LEEM a fila antes de qualquer um gravar: é a janela que o UPDATE condicional fecha.
        var t = await _b.Fila().EnfileirarElegibilidadeAsync(TissBancoDeTeste.Petrobras, "000123456789", null, null, TissBancoDeTeste.Operador);
        var outroRepo = new ClinicaRepositorio(_b.NovoContexto());
        var vistaPeloPrimeiro = (await _b.Repo.TransacoesTissReservaveisAsync(_b.Agora, _b.Agora.AddMinutes(-5), 10)).Single();
        var vistaPeloSegundo = (await outroRepo.TransacoesTissReservaveisAsync(_b.Agora, _b.Agora.AddMinutes(-5), 10)).Single();

        var primeiro = await _b.Repo.ReservarTransacaoTissAsync(t.Id, vistaPeloPrimeiro.Versao, C1, _b.Agora);
        var segundo = await outroRepo.ReservarTransacaoTissAsync(t.Id, vistaPeloSegundo.Versao, C2, _b.Agora);

        primeiro.Should().BeTrue();
        segundo.Should().BeFalse("o segundo viu a mesma versão, que já não é a da linha");
        var gravada = await _b.NovoContexto().TransacoesTiss.AsNoTracking().SingleAsync();
        gravada.Consumidor.Should().Be(C1);
        gravada.Tentativas.Should().Be(1);
        gravada.Estado.Should().Be(EstadoTransacaoTiss.EmEnvio);
    }

    [Fact]
    public async Task Dois_conectores_recebem_transacoes_diferentes_e_o_terceiro_pedido_nao_acha_nada()
    {
        var fila1 = _b.Fila();
        var fila2 = _b.Fila(new ClinicaRepositorio(_b.NovoContexto()));
        var a = await fila1.EnfileirarElegibilidadeAsync(TissBancoDeTeste.Petrobras, "000000000001", null, null, TissBancoDeTeste.Operador);
        _b.Agora = _b.Agora.AddSeconds(1);
        var b = await fila1.EnfileirarElegibilidadeAsync(TissBancoDeTeste.Petrobras, "000000000002", null, null, TissBancoDeTeste.Operador);

        var doPrimeiro = await fila1.ReservarProximaAsync(C1);
        var doSegundo = await fila2.ReservarProximaAsync(C2);
        var doTerceiro = await fila1.ReservarProximaAsync(C1);

        doPrimeiro!.Id.Should().Be(a.Id, "a mais antiga sai primeiro");
        doSegundo!.Id.Should().Be(b.Id);
        doTerceiro.Should().BeNull();
        doPrimeiro.Estado.Should().Be(EstadoTransacaoTiss.EmEnvio);
        doPrimeiro.Tentativas.Should().Be(1);
        doSegundo.Consumidor.Should().Be(C2);
    }

    [Fact]
    public async Task So_quem_reservou_grava_o_desfecho()
    {
        var fila = _b.Fila();
        var t = await fila.EnfileirarElegibilidadeAsync(TissBancoDeTeste.Petrobras, "000123456789", null, null, TissBancoDeTeste.Operador);

        var concluirSemReserva = () => fila.ConcluirAsync(t.Id, new ConclusaoTransacaoTiss { Resultado = "Elegível." });

        (await concluirSemReserva.Should().ThrowAsync<InvalidOperationException>()).WithMessage("*não está reservada*");
    }

    // ---------- tentativas ----------

    [Fact]
    public async Task Elegibilidade_sem_resposta_volta_para_a_fila_espera_e_desiste_na_terceira()
    {
        var fila = _b.Fila();
        var t = await fila.EnfileirarElegibilidadeAsync(TissBancoDeTeste.Petrobras, "000123456789", null, null, TissBancoDeTeste.Operador);

        await fila.ReservarProximaAsync(C1);
        await fila.MarcarEnvioIniciadoAsync(t.Id, "<a/>");
        (await fila.FalharAsync(t.Id, "sem resposta da operadora", podeTentarDeNovo: true)).Should().Be(EstadoTransacaoTiss.Pendente);
        (await fila.ReservarProximaAsync(C1)).Should().BeNull("a próxima tentativa espera um minuto");

        _b.Agora = _b.Agora.AddMinutes(1);
        (await fila.ReservarProximaAsync(C1))!.Tentativas.Should().Be(2);
        (await fila.FalharAsync(t.Id, "sem resposta da operadora", podeTentarDeNovo: true)).Should().Be(EstadoTransacaoTiss.Pendente);

        _b.Agora = _b.Agora.AddMinutes(5);
        (await fila.ReservarProximaAsync(C1))!.Tentativas.Should().Be(3);
        (await fila.FalharAsync(t.Id, "sem resposta da operadora", podeTentarDeNovo: true)).Should().Be(EstadoTransacaoTiss.Falhou);

        var final = (await fila.ObterAsync(t.Id))!;
        final.Tentativas.Should().Be(FilaTiss.MaximoDeTentativas);
        final.ProcessadaEm.Should().Be(_b.Agora);
        _b.Agora = _b.Agora.AddHours(1);
        (await fila.ReservarProximaAsync(C1)).Should().BeNull("transação que falhou não volta sozinha");
    }

    [Fact]
    public async Task Resposta_negativa_da_operadora_nao_e_repetida_nem_na_elegibilidade()
    {
        // Login recusado repetido três vezes pode bloquear o acesso da clínica na operadora.
        var fila = _b.Fila();
        var t = await fila.EnfileirarElegibilidadeAsync(TissBancoDeTeste.Petrobras, "000123456789", null, null, TissBancoDeTeste.Operador);
        await fila.ReservarProximaAsync(C1);
        await fila.MarcarEnvioIniciadoAsync(t.Id, "<a/>");

        (await fila.FalharAsync(t.Id, "LoginInvalido", podeTentarDeNovo: false)).Should().Be(EstadoTransacaoTiss.Falhou);
    }

    [Theory]
    [InlineData(TipoTransacaoTiss.EnvioGuia)]
    [InlineData(TipoTransacaoTiss.CancelamentoGuia)]
    public async Task O_que_escreve_na_operadora_nunca_volta_para_a_fila_depois_que_a_mensagem_saiu(TipoTransacaoTiss tipo)
    {
        var (paciente, guia) = await _b.GuiaAsync();
        var fila = _b.Fila();
        var t = tipo == TipoTransacaoTiss.EnvioGuia
            ? await fila.EnfileirarEnvioGuiaAsync(TissBancoDeTeste.Petrobras, guia.Id, paciente.Id, TissBancoDeTeste.Operador)
            : await fila.EnfileirarCancelamentoGuiaAsync(TissBancoDeTeste.Petrobras, guia.Id, "G123", "987654", TissBancoDeTeste.Operador);
        await fila.ReservarProximaAsync(C1);
        await fila.MarcarEnvioIniciadoAsync(t.Id, "<a/>");

        // Quem chama PEDE nova tentativa (rede caiu) — e a fila não deixa.
        var estado = await fila.FalharAsync(t.Id, "sem resposta da operadora", podeTentarDeNovo: true);

        estado.Should().Be(EstadoTransacaoTiss.Falhou);
        var final = (await fila.ObterAsync(t.Id))!;
        final.Tentativas.Should().Be(1);
        final.Erro.Should().Contain("não é reenviada sozinha");
        final.Resultado.Should().Contain("portal da operadora");
        _b.Agora = _b.Agora.AddDays(1);
        (await fila.ReservarProximaAsync(C1)).Should().BeNull();
    }

    [Fact]
    public async Task Envio_que_falhou_ANTES_de_a_mensagem_sair_pode_voltar_para_a_fila()
    {
        // Nada chegou à operadora (o banco caiu no preparo): repetir não duplica guia.
        var (paciente, guia) = await _b.GuiaAsync();
        var fila = _b.Fila();
        var t = await fila.EnfileirarEnvioGuiaAsync(TissBancoDeTeste.Petrobras, guia.Id, paciente.Id, TissBancoDeTeste.Operador);
        await fila.ReservarProximaAsync(C1);

        (await fila.FalharAsync(t.Id, "Falha inesperada no conector", podeTentarDeNovo: true)).Should().Be(EstadoTransacaoTiss.Pendente);
    }

    [Fact]
    public async Task Envio_preso_com_a_mensagem_ja_saida_vira_falha_e_nao_e_entregue_a_outro_conector()
    {
        // O conector morreu DEPOIS de mandar: ninguém sabe se a operadora recebeu.
        var (paciente, guia) = await _b.GuiaAsync();
        var fila = _b.Fila();
        var presa = await fila.EnfileirarEnvioGuiaAsync(TissBancoDeTeste.Petrobras, guia.Id, paciente.Id, TissBancoDeTeste.Operador);
        await fila.ReservarProximaAsync(C1);
        await fila.MarcarEnvioIniciadoAsync(presa.Id, "<a/>");
        _b.Agora = _b.Agora.AddSeconds(1);
        var seguinte = await fila.EnfileirarElegibilidadeAsync(TissBancoDeTeste.Petrobras, "000123456789", null, null, TissBancoDeTeste.Operador);

        _b.Agora = _b.Agora + FilaTiss.ReservaVence + TimeSpan.FromMinutes(1);
        var entregue = await _b.Fila(new ClinicaRepositorio(_b.NovoContexto())).ReservarProximaAsync(C2);

        entregue!.Id.Should().Be(seguinte.Id, "a presa é encerrada e a procura segue");
        var final = await _b.NovoContexto().TransacoesTiss.AsNoTracking().SingleAsync(x => x.Id == presa.Id);
        final.Estado.Should().Be(EstadoTransacaoTiss.Falhou);
        final.Erro.Should().Be(FilaTiss.FraseEnvioInterrompido);
        final.ProcessadaEm.Should().NotBeNull();
    }

    [Fact]
    public async Task Elegibilidade_presa_e_retomada_por_outro_conector_quando_a_reserva_vence()
    {
        var fila = _b.Fila();
        var t = await fila.EnfileirarElegibilidadeAsync(TissBancoDeTeste.Petrobras, "000123456789", null, null, TissBancoDeTeste.Operador);
        await fila.ReservarProximaAsync(C1);
        var outra = _b.Fila(new ClinicaRepositorio(_b.NovoContexto()));
        (await outra.ReservarProximaAsync(C2)).Should().BeNull("a reserva do primeiro ainda vale");

        _b.Agora = _b.Agora + FilaTiss.ReservaVence + TimeSpan.FromSeconds(1);
        var retomada = await outra.ReservarProximaAsync(C2);

        retomada!.Id.Should().Be(t.Id);
        retomada.Consumidor.Should().Be(C2);
        retomada.Tentativas.Should().Be(2);
    }

    // ---------- desfecho ----------

    [Fact]
    public async Task Envio_concluido_grava_protocolo_numero_da_operadora_e_a_trilha()
    {
        var (paciente, guia) = await _b.GuiaAsync();
        var fila = _b.Fila();
        var t = await fila.EnfileirarEnvioGuiaAsync(TissBancoDeTeste.Petrobras, guia.Id, paciente.Id, TissBancoDeTeste.Operador);
        await fila.ReservarProximaAsync(C1);
        await fila.MarcarEnvioIniciadoAsync(t.Id, "<a/>");
        _b.Agora = _b.Agora.AddSeconds(3);

        await fila.ConcluirAsync(t.Id, new ConclusaoTransacaoTiss
        {
            Resultado = "Recebida pela operadora.", Protocolo = " 987654 ", NumeroGuiaOperadora = "OP123", XmlRecebido = "<resposta/>"
        });

        var final = await _b.NovoContexto().TransacoesTiss.AsNoTracking().SingleAsync();
        final.Estado.Should().Be(EstadoTransacaoTiss.Concluida);
        final.Protocolo.Should().Be("987654");
        final.NumeroGuiaOperadora.Should().Be("OP123");
        final.XmlRecebido.Should().Be("<resposta/>");
        final.ProcessadaEm.Should().Be(_b.Agora);
        final.Erro.Should().BeNull();
        var trilha = await _b.Db.Auditoria.AsNoTracking().SingleAsync(a => a.Acao == "EnvioGuiaTissConcluido");
        trilha.CodigoId.Should().Be(guia.Id);
        trilha.Operador.Should().Be(TissBancoDeTeste.Operador);
        // A baixa da guia NÃO é feita daqui: quem liga o protocolo à baixa é a tela.
        (await _b.NovoContexto().Codigos.AsNoTracking().SingleAsync()).Status.Should().Be(Clinica.Domain.StatusCodigo.Aberto);
    }

    [Fact]
    public async Task Consulta_de_status_concluida_sem_protocolo_na_resposta_conserva_o_do_pedido()
    {
        var fila = _b.Fila();
        var t = await fila.EnfileirarStatusProtocoloAsync(TissBancoDeTeste.Petrobras, "987654", null, TissBancoDeTeste.Operador);
        await fila.ReservarProximaAsync(C1);
        await fila.MarcarEnvioIniciadoAsync(t.Id, "<a/>");

        await fila.ConcluirAsync(t.Id, new ConclusaoTransacaoTiss { Resultado = "Em análise." });

        (await fila.ObterAsync(t.Id))!.Protocolo.Should().Be("987654");
    }

    [Fact]
    public async Task Recusa_do_conector_encerra_como_cancelada_e_so_vale_antes_de_a_mensagem_sair()
    {
        var (paciente, guia) = await _b.GuiaAsync();
        var fila = _b.Fila();
        var t = await fila.EnfileirarEnvioGuiaAsync(TissBancoDeTeste.Petrobras, guia.Id, paciente.Id, TissBancoDeTeste.Operador);
        await fila.ReservarProximaAsync(C1);
        await fila.RecusarAsync(t.Id, "O envio pelo web service está desligado.");
        var recusada = (await fila.ObterAsync(t.Id))!;
        recusada.Estado.Should().Be(EstadoTransacaoTiss.Cancelada);
        recusada.Resultado.Should().Contain("desligado");
        recusada.EnvioIniciadoEm.Should().BeNull();

        var outra = await fila.EnfileirarEnvioGuiaAsync(TissBancoDeTeste.Petrobras, guia.Id, paciente.Id, TissBancoDeTeste.Operador);
        await fila.ReservarProximaAsync(C1);
        await fila.MarcarEnvioIniciadoAsync(outra.Id, "<a/>");
        var recusarTarde = () => fila.RecusarAsync(outra.Id, "tarde demais");
        await recusarTarde.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task Textos_longos_sao_cortados_no_teto_da_coluna()
    {
        var fila = _b.Fila();
        var t = await fila.EnfileirarElegibilidadeAsync(TissBancoDeTeste.Petrobras, "000123456789", null, null, TissBancoDeTeste.Operador);
        await fila.ReservarProximaAsync(C1);

        await fila.FalharAsync(t.Id, new string('x', 5000), podeTentarDeNovo: false);

        var final = (await fila.ObterAsync(t.Id))!;
        final.Erro!.Length.Should().Be(TransacaoTissLimites.Erro);
        final.Resultado!.Length.Should().BeLessOrEqualTo(TransacaoTissLimites.Resultado);
    }

    // ---------- a senha nunca fica na linha ----------

    [Fact]
    public async Task O_xml_guardado_nunca_leva_a_senha_mesmo_que_quem_chamou_nao_a_tenha_mascarado()
    {
        var md5 = TissElegibilidadeWs.SenhaEmMd5("senha-ficticia-de-teste");
        var envelope = TissElegibilidadeWs.MontarEnvelope(new PedidoElegibilidadeTiss
        {
            RegistroAnsOperadora = "422631", CodigoPrestadorNaOperadora = "00000000000191", NumeroCarteira = "000123456789",
            Login = "LOGINTESTE", SenhaMd5 = md5, Sequencial = "1", Momento = _b.Agora
        });
        envelope.Should().Contain(md5, "o envelope que vai à rede leva a senha em MD5");
        var fila = _b.Fila();
        var t = await fila.EnfileirarElegibilidadeAsync(TissBancoDeTeste.Petrobras, "000123456789", null, null, TissBancoDeTeste.Operador);
        await fila.ReservarProximaAsync(C1);

        await fila.MarcarEnvioIniciadoAsync(t.Id, envelope);

        var gravado = (await _b.NovoContexto().TransacoesTiss.AsNoTracking().SingleAsync()).XmlEnviado!;
        gravado.Should().NotContain(md5).And.Contain("senhaPrestador>********").And.Contain("000123456789").And.Contain("LOGINTESTE");
    }

    [Theory]
    [InlineData("<a><senhaPrestador>abc123</senhaPrestador>")]                       // XML quebrado
    [InlineData("lixo <ans:senhaPrestador >abc123</ans:senhaPrestador > mais lixo")]  // nem XML é
    [InlineData("<r><x:senhaPrestador xmlns:x='u'>abc123</x:senhaPrestador></r>")]    // outro prefixo
    public void A_mascara_da_senha_vale_para_xml_malformado_e_para_qualquer_prefixo(string texto)
        => FilaTiss.SemSenha(texto).Should().NotContain("abc123").And.Contain("********");

    [Fact]
    public void Xml_sem_senha_passa_intacto()
        => FilaTiss.SemSenha("<?xml version=\"1.0\" encoding=\"ISO-8859-1\"?><a>José</a>").Should().Be("<?xml version=\"1.0\" encoding=\"ISO-8859-1\"?><a>José</a>");

    // ---------- a tela acompanha ----------

    [Fact]
    public async Task As_leituras_para_a_tela_vem_sem_os_xml_e_da_mais_nova_para_a_mais_velha()
    {
        var (paciente, guia) = await _b.GuiaAsync();
        var fila = _b.Fila();
        var envio = await fila.EnfileirarEnvioGuiaAsync(TissBancoDeTeste.Petrobras, guia.Id, paciente.Id, TissBancoDeTeste.Operador);
        await fila.ReservarProximaAsync(C1);
        await fila.MarcarEnvioIniciadoAsync(envio.Id, "<enviado/>");
        await fila.ConcluirAsync(envio.Id, new ConclusaoTransacaoTiss { Resultado = "Recebida.", Protocolo = "1", XmlRecebido = "<recebido/>" });
        _b.Agora = _b.Agora.AddMinutes(1);
        var outroConvenio = await fila.EnfileirarElegibilidadeAsync("Amil", "000000000009", null, null, TissBancoDeTeste.Operador);
        _b.Db.ChangeTracker.Clear();

        var daGuia = await fila.DaGuiaAsync(guia.Id);
        var recentes = await fila.RecentesAsync();
        var soPetrobras = await fila.RecentesAsync(TissBancoDeTeste.Petrobras);
        var uma = await fila.ObterAsync(envio.Id);

        daGuia.Should().ContainSingle().Which.Protocolo.Should().Be("1");
        recentes.Select(x => x.Id).Should().Equal(outroConvenio.Id, envio.Id);
        soPetrobras.Select(x => x.Id).Should().Equal(envio.Id);
        foreach (var t in daGuia.Concat(recentes).Append(uma!))
        {
            t.XmlEnviado.Should().BeNull();
            t.XmlRecebido.Should().BeNull();
        }
        uma!.Resultado.Should().Be("Recebida.");
        (await fila.ObterAsync(999_999)).Should().BeNull();
    }
}
