using Clinica.Application.Modelos;
using Clinica.Application.Servicos;
using Clinica.Domain;
using Clinica.Domain.Entities;
using Clinica.Infrastructure;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Clinica.Tests;

/// <summary>
/// O envio em LOTE pelo web service TISS (nov/2026, pedido do dono: "se for envio em lote
/// precisamos nos certificar da baixa em lote também"). A transação de envio leva de 1 a 100
/// guias; a operadora responde POR GUIA; e a baixa em lote só alcança o que ela recebeu.
/// Uma guia só é o lote com um item.
/// </summary>
public sealed class EnvioEmLoteTissTests : IDisposable
{
    private readonly TissBancoDeTeste _b = new();
    private const string P = TissBancoDeTeste.Petrobras, Op = TissBancoDeTeste.Operador, C1 = "conector-1";

    public void Dispose() => _b.Dispose();

    private async Task<TransacaoTiss> EnviadoAsync(IReadOnlyList<int> ids)
    {
        var fila = _b.Fila();
        var t = await fila.EnfileirarEnvioAsync(P, ids, Op);
        await fila.ReservarProximaAsync(C1);
        await fila.MarcarEnvioIniciadoAsync(t.Id, "<lote/>");
        return t;
    }

    private Task<TransacaoTiss> GravadaAsync(int id)
        => _b.NovoContexto().TransacoesTiss.AsNoTracking().Include(t => t.Guias).SingleAsync(t => t.Id == id);

    // ---------- o pedido ----------

    [Fact]
    public async Task O_lote_entra_com_um_item_por_guia_cada_um_com_o_numero_que_vai_a_operadora()
    {
        var ids = await _b.GuiasAsync(3);

        var t = await _b.Fila().EnfileirarEnvioAsync(P, ids, Op);

        var gravada = await GravadaAsync(t.Id);
        gravada.Tipo.Should().Be(TipoTransacaoTiss.EnvioGuia);
        gravada.Estado.Should().Be(EstadoTransacaoTiss.Pendente);
        gravada.CodigoId.Should().BeNull("com várias guias, elas estão nos itens");
        gravada.Guias.Select(g => g.CodigoId).Should().BeEquivalentTo(ids.Select(i => (int?)i));
        gravada.Guias.Select(g => g.NumeroGuiaPrestador).Should().BeEquivalentTo(ids.Select(i => $"C{i}"));
        gravada.Guias.Should().OnlyContain(g => g.Situacao == SituacaoGuiaTiss.Pendente);
        var trilha = await _b.Db.Auditoria.AsNoTracking().SingleAsync(a => a.Acao == "PedidoEnvioGuiaTiss");
        trilha.Operador.Should().Be(Op);
        trilha.Detalhe.Should().Contain("3 guias");
    }

    [Fact]
    public async Task Uma_guia_so_e_o_lote_com_um_item_e_a_transacao_aponta_para_ela()
    {
        var (paciente, guia) = await _b.GuiaAsync();

        var t = await _b.Fila().EnfileirarEnvioGuiaAsync(P, guia.Id, Op);

        var gravada = await GravadaAsync(t.Id);
        gravada.CodigoId.Should().Be(guia.Id);
        gravada.PacienteId.Should().Be(paciente.Id);
        gravada.Guias.Should().ContainSingle().Which.NumeroGuiaPrestador.Should().Be($"C{guia.Id}");
    }

    [Fact]
    public async Task Cem_guias_cabem_no_lote_e_cento_e_uma_nao()
    {
        var ids = await _b.GuiasAsync(101);

        (await _b.Fila().ConferirEnvioAsync(P, ids.Take(100).ToList())).PodeEnviar.Should().BeTrue();
        var demais = () => _b.Fila().EnfileirarEnvioAsync(P, ids, Op);

        var recusa = await demais.Should().ThrowAsync<EnvioTissRecusadoException>();
        recusa.Which.Conferencia.Impedimento.Should().Contain("até 100 guias").And.Contain("101");
        (await _b.Db.TransacoesTiss.CountAsync()).Should().Be(0);
    }

    // ---------- cada recusa, com a frase da linha ----------

    [Fact]
    public async Task Lista_vazia_e_recusada()
    {
        var pedir = () => _b.Fila().EnfileirarEnvioAsync(P, [], Op);

        (await pedir.Should().ThrowAsync<EnvioTissRecusadoException>()).Which.Conferencia.Impedimento.Should().Contain("ao menos uma guia");
    }

    [Fact]
    public async Task Guia_sem_a_guia_ANS_conferida_e_recusada_na_linha_dela_e_o_lote_inteiro_nao_entra()
    {
        var boas = await _b.GuiasAsync(2);
        var (_, semConferencia) = await _b.GuiaAsync(conferida: false);

        var pedir = () => _b.Fila().EnfileirarEnvioAsync(P, [.. boas, semConferencia.Id], Op);

        var conferencia = (await pedir.Should().ThrowAsync<EnvioTissRecusadoException>()).Which.Conferencia;
        conferencia.Impedimento.Should().BeNull();
        conferencia.PorGuia.Should().ContainSingle().Which.Should().Be(
            new KeyValuePair<int, string>(semConferencia.Id, "Confira a guia no padrão ANS antes de enviar: esta ainda não foi conferida."));
        (await _b.Db.TransacoesTiss.CountAsync()).Should().Be(0, "lote enviado pela metade sem a pessoa saber é pior que lote não enviado");
        (await _b.Db.Auditoria.CountAsync(a => a.Acao == "PedidoEnvioGuiaTiss")).Should().Be(0);
    }

    [Fact]
    public async Task Guia_ja_baixada_e_recusada()
    {
        var (_, guia) = await _b.GuiaAsync();
        guia.DarBaixa(DateOnly.FromDateTime(_b.Agora), "ABC123", Op, null);
        await _b.Db.SaveChangesAsync();

        var conferencia = await _b.Fila().ConferirEnvioAsync(P, [guia.Id]);

        conferencia.PodeEnviar.Should().BeFalse();
        conferencia.PorGuia[guia.Id].Should().Contain("já tem baixa");
    }

    [Fact]
    public async Task Guias_de_convenios_diferentes_nao_vao_na_mesma_transacao()
    {
        var (_, petrobras) = await _b.GuiaAsync();
        var (_, amil) = await _b.GuiaAsync(convenio: Convenio.Amil);

        var conferencia = await _b.Fila().ConferirEnvioAsync(P, [petrobras.Id, amil.Id]);

        conferencia.PorGuia.Keys.Should().Equal(amil.Id);
        conferencia.PorGuia[amil.Id].Should().Contain("outro convênio").And.Contain("Amil");
    }

    [Fact]
    public async Task Guia_de_consulta_e_guia_de_procedimento_nao_vao_no_mesmo_lote()
    {
        var (_, consulta) = await _b.GuiaAsync();
        var (_, bsv) = await _b.GuiaAsync(tipo: TipoCodigo.Bsv);

        var conferencia = await _b.Fila().ConferirEnvioAsync(P, [consulta.Id, bsv.Id]);

        conferencia.PorGuia.Keys.Should().Equal(bsv.Id);
        conferencia.PorGuia[bsv.Id].Should().Contain("um só tipo de guia");
        (await _b.Fila().ConferirEnvioAsync(P, [bsv.Id])).PodeEnviar.Should().BeTrue("sozinhas, as de procedimento formam o lote delas");
    }

    [Fact]
    public async Task Guia_repetida_na_lista_e_guia_que_nao_existe_sao_recusadas()
    {
        var (_, guia) = await _b.GuiaAsync();

        var conferencia = await _b.Fila().ConferirEnvioAsync(P, [guia.Id, guia.Id, 999_999]);

        conferencia.PorGuia[guia.Id].Should().Contain("mais de uma vez");
        conferencia.PorGuia[999_999].Should().Contain("não encontrada");
    }

    [Fact]
    public async Task Sem_operador_o_lote_nao_entra()
    {
        var ids = await _b.GuiasAsync(2);
        var pedir = () => _b.Fila().EnfileirarEnvioAsync(P, ids, " ");

        (await pedir.Should().ThrowAsync<InvalidOperationException>()).WithMessage("*login*");
        (await _b.Db.TransacoesTiss.CountAsync()).Should().Be(0);
    }

    // ---------- a trava contra duplicidade, por guia ----------

    [Fact]
    public async Task Guia_que_esta_noutro_envio_na_fila_ou_em_andamento_nao_entra_em_outro_lote()
    {
        var ids = await _b.GuiasAsync(3);
        var fila = _b.Fila();
        await fila.EnfileirarEnvioAsync(P, [ids[0], ids[1]], Op);

        var naFila = await fila.ConferirEnvioAsync(P, [ids[1], ids[2]]);
        await fila.ReservarProximaAsync(C1);
        var emAndamento = await fila.ConferirEnvioAsync(P, [ids[1], ids[2]]);

        naFila.PorGuia.Keys.Should().Equal(ids[1]);
        naFila.PorGuia[ids[1]].Should().Contain("já está num envio");
        emAndamento.PorGuia.Keys.Should().Equal(ids[1]);
    }

    [Fact]
    public async Task Guia_recebida_pela_operadora_nao_e_enviada_de_novo_e_a_frase_diz_o_protocolo()
    {
        var ids = await _b.GuiasAsync(2);
        var t = await EnviadoAsync(ids);
        await _b.Fila().ConcluirAsync(t.Id, new ConclusaoTransacaoTiss { Resultado = "Lote recebido.", Protocolo = "987654" });

        var conferencia = await _b.Fila().ConferirEnvioAsync(P, [ids[0]]);

        conferencia.PorGuia[ids[0]].Should().Contain("já foi recebida").And.Contain("987654").And.Contain("Falta dar baixa");
    }

    [Fact]
    public async Task Guia_glosada_num_lote_recebido_pode_ser_reenviada_por_decisao_de_gente_com_numero_novo()
    {
        var ids = await _b.GuiasAsync(2);
        var t = await EnviadoAsync(ids);
        await _b.Fila().ConcluirAsync(t.Id, new ConclusaoTransacaoTiss
        {
            Resultado = "Lote recebido com glosa.", Protocolo = "987654",
            Guias =
            [
                new ResultadoDaGuiaTiss($"C{ids[0]}", "OP1", SituacaoGuiaTiss.Recebida),
                new ResultadoDaGuiaTiss($"C{ids[1]}", null, SituacaoGuiaTiss.Glosada, "1213 - CBO inválido")
            ]
        });

        var conferencia = await _b.Fila().ConferirEnvioAsync(P, ids);
        var reenvio = await _b.Fila().EnfileirarEnvioGuiaAsync(P, ids[1], Op);

        conferencia.PorGuia.Keys.Should().Equal(ids[0]);
        // A operadora nunca vê o mesmo número de guia duas vezes.
        (await GravadaAsync(reenvio.Id)).Guias.Single().NumeroGuiaPrestador.Should().Be($"C{ids[1]}R2");
    }

    [Fact]
    public async Task Lote_que_falhou_solta_as_guias_e_o_sistema_nao_reenvia_nenhuma_sozinho()
    {
        var ids = await _b.GuiasAsync(2);
        var t = await EnviadoAsync(ids);

        // A rede caiu depois de a mensagem sair: quem chama PEDE nova tentativa, e a fila não deixa.
        var estado = await _b.Fila().FalharAsync(t.Id, "sem resposta da operadora", podeTentarDeNovo: true);

        estado.Should().Be(EstadoTransacaoTiss.Falhou);
        var gravada = await GravadaAsync(t.Id);
        gravada.Guias.Should().OnlyContain(g => g.Situacao == SituacaoGuiaTiss.Falhou && g.Motivo!.Contains("não é reenviada sozinha"));
        _b.Agora = _b.Agora.AddDays(1);
        (await _b.Fila().ReservarProximaAsync(C1)).Should().BeNull("nem o lote nem guia alguma dele volta para a fila");
        (await _b.Fila().GuiasRecebidasSemBaixaAsync(t.Id)).Should().BeEmpty("lote que falhou não autoriza baixa nenhuma");
        // Reenviar é pedido novo, de gente — e aí as guias estão livres.
        (await _b.Fila().ConferirEnvioAsync(P, ids)).PodeEnviar.Should().BeTrue();
    }

    [Fact]
    public async Task Lote_recusado_pelo_conector_marca_cada_guia_como_nao_enviada()
    {
        var ids = await _b.GuiasAsync(2);
        var fila = _b.Fila();
        var t = await fila.EnfileirarEnvioAsync(P, ids, Op);
        await fila.ReservarProximaAsync(C1);

        await fila.RecusarAsync(t.Id, "O envio pelo web service está desligado para Petrobras. Nada foi enviado à operadora.");

        var gravada = await GravadaAsync(t.Id);
        gravada.Estado.Should().Be(EstadoTransacaoTiss.Cancelada);
        gravada.Guias.Should().OnlyContain(g => g.Situacao == SituacaoGuiaTiss.Falhou && g.Motivo!.Contains("desligado"));
    }

    // ---------- o resultado volta por guia ----------

    [Fact]
    public async Task A_operadora_recebe_o_lote_e_glosa_uma_guia_e_cada_item_guarda_o_seu_resultado()
    {
        var ids = await _b.GuiasAsync(3);
        var t = await EnviadoAsync(ids);
        _b.Agora = _b.Agora.AddSeconds(4);

        await _b.Fila().ConcluirAsync(t.Id, new ConclusaoTransacaoTiss
        {
            Resultado = "Lote recebido: 2 guias recebidas, 1 com glosa.", Protocolo = "987654",
            Guias =
            [
                new ResultadoDaGuiaTiss($"c{ids[0]} ", "OP-A", SituacaoGuiaTiss.Recebida),      // caixa e espaço não atrapalham
                new ResultadoDaGuiaTiss($"C{ids[1]}", null, SituacaoGuiaTiss.Recebida),          // a operadora não devolveu o número dela
                new ResultadoDaGuiaTiss($"C{ids[2]}", null, SituacaoGuiaTiss.Glosada, "1001 - Número da carteira inválido")
            ]
        });

        var gravada = await GravadaAsync(t.Id);
        gravada.Estado.Should().Be(EstadoTransacaoTiss.Concluida);
        gravada.Protocolo.Should().Be("987654");
        var porGuia = gravada.Guias.ToDictionary(g => g.CodigoId!.Value);
        porGuia[ids[0]].Should().BeEquivalentTo(new { Situacao = SituacaoGuiaTiss.Recebida, NumeroGuiaOperadora = "OP-A", Motivo = (string?)null, AtualizadaEm = (DateTime?)_b.Agora });
        porGuia[ids[1]].Situacao.Should().Be(SituacaoGuiaTiss.Recebida);
        porGuia[ids[1]].NumeroGuiaOperadora.Should().BeNull();
        porGuia[ids[2]].Situacao.Should().Be(SituacaoGuiaTiss.Glosada);
        porGuia[ids[2]].Motivo.Should().Be("1001 - Número da carteira inválido");
    }

    [Fact]
    public async Task Guia_que_a_resposta_nao_cita_nao_vira_recebida_por_omissao()
    {
        var ids = await _b.GuiasAsync(2);
        var t = await EnviadoAsync(ids);

        await _b.Fila().ConcluirAsync(t.Id, new ConclusaoTransacaoTiss
        {
            Resultado = "Lote recebido.", Protocolo = "987654",
            Guias = [new ResultadoDaGuiaTiss($"C{ids[0]}", "OP-A", SituacaoGuiaTiss.Recebida)]
        });

        var esquecida = (await GravadaAsync(t.Id)).Guias.Single(g => g.CodigoId == ids[1]);
        esquecida.Situacao.Should().Be(SituacaoGuiaTiss.Pendente);
        esquecida.Motivo.Should().Contain("não citou esta guia");
        (await _b.Fila().GuiasRecebidasSemBaixaAsync(t.Id)).Select(g => g.CodigoId).Should().Equal(ids[0]);
    }

    [Fact]
    public async Task Resposta_com_protocolo_e_sem_detalhe_por_guia_conta_o_lote_inteiro_como_recebido()
    {
        var ids = await _b.GuiasAsync(2);
        var t = await EnviadoAsync(ids);

        await _b.Fila().ConcluirAsync(t.Id, new ConclusaoTransacaoTiss { Resultado = "Lote recebido.", Protocolo = "987654" });

        (await GravadaAsync(t.Id)).Guias.Should().OnlyContain(g => g.Situacao == SituacaoGuiaTiss.Recebida);
    }

    [Fact]
    public async Task Resposta_sem_protocolo_e_sem_detalhe_nao_marca_guia_nenhuma_como_recebida()
    {
        var ids = await _b.GuiasAsync(2);
        var t = await EnviadoAsync(ids);

        await _b.Fila().ConcluirAsync(t.Id, new ConclusaoTransacaoTiss { Resultado = "Resposta sem protocolo." });

        (await GravadaAsync(t.Id)).Guias.Should().OnlyContain(g => g.Situacao == SituacaoGuiaTiss.Pendente);
        (await _b.Fila().GuiasRecebidasSemBaixaAsync(t.Id)).Should().BeEmpty();
    }

    // ---------- a leitura para a baixa em lote ----------

    [Fact]
    public async Task A_leitura_para_a_baixa_traz_so_as_recebidas_que_ainda_nao_tem_baixa()
    {
        var ids = await _b.GuiasAsync(4);
        var t = await EnviadoAsync(ids);
        await _b.Fila().ConcluirAsync(t.Id, new ConclusaoTransacaoTiss
        {
            Resultado = "Lote recebido.", Protocolo = "987654",
            Guias =
            [
                new ResultadoDaGuiaTiss($"C{ids[0]}", "OP-A", SituacaoGuiaTiss.Recebida),
                new ResultadoDaGuiaTiss($"C{ids[1]}", null, SituacaoGuiaTiss.Recebida),
                new ResultadoDaGuiaTiss($"C{ids[2]}", null, SituacaoGuiaTiss.Glosada, "glosa"),
                new ResultadoDaGuiaTiss($"C{ids[3]}", "OP-D", SituacaoGuiaTiss.Recebida)
            ]
        });
        // A última já recebeu baixa (por outra tela, por exemplo).
        var jaBaixada = await _b.Db.Codigos.SingleAsync(c => c.Id == ids[3]);
        jaBaixada.DarBaixa(DateOnly.FromDateTime(_b.Agora), "OPD", Op, null);
        await _b.Db.SaveChangesAsync();

        var aBaixar = await _b.Fila(new ClinicaRepositorio(_b.NovoContexto())).GuiasRecebidasSemBaixaAsync(t.Id);

        aBaixar.Select(g => g.CodigoId).Should().Equal(ids[0], ids[1]);
        // O que a baixa precisa: o número devolvido, ou o do prestador + o protocolo quando ela não devolve.
        aBaixar[0].Should().BeEquivalentTo(new { NumeroGuiaOperadora = "OP-A", NumeroGuiaPrestador = $"C{ids[0]}", Protocolo = "987654" });
        aBaixar[1].NumeroGuiaOperadora.Should().BeNull();
        aBaixar[1].NumeroGuiaPrestador.Should().Be($"C{ids[1]}");
        aBaixar[1].Protocolo.Should().Be("987654");
        // O envio NÃO dá baixa: as guias recebidas continuam em aberto até alguém, logado, baixar.
        (await _b.NovoContexto().Codigos.AsNoTracking().CountAsync(c => c.DataBaixa == null)).Should().Be(3);
    }

    // ---------- a consulta de status atualiza os mesmos itens ----------

    [Fact]
    public async Task A_consulta_de_status_do_envio_atualiza_as_mesmas_guias_do_lote()
    {
        var ids = await _b.GuiasAsync(2);
        var envio = await EnviadoAsync(ids);
        var fila = _b.Fila();
        await fila.ConcluirAsync(envio.Id, new ConclusaoTransacaoTiss
        {
            Resultado = "Lote recebido.", Protocolo = "987654",
            Guias = [new ResultadoDaGuiaTiss($"C{ids[0]}", "OP-A", SituacaoGuiaTiss.Recebida)]   // a segunda ficou sem resposta
        });

        var status = await fila.EnfileirarStatusDoEnvioAsync(envio.Id, Op);
        await fila.ReservarProximaAsync(C1);
        await fila.MarcarEnvioIniciadoAsync(status.Id, "<status/>");
        await fila.ConcluirAsync(status.Id, new ConclusaoTransacaoTiss
        {
            Resultado = "Em análise.",
            Guias =
            [
                new ResultadoDaGuiaTiss($"C{ids[0]}", "OP-A", SituacaoGuiaTiss.Glosada, "1307 - Número da guia inválido"),
                new ResultadoDaGuiaTiss($"C{ids[1]}", "OP-B", SituacaoGuiaTiss.Recebida)
            ]
        });

        status.Protocolo.Should().Be("987654");
        status.TransacaoDeEnvioId.Should().Be(envio.Id);
        var itens = (await GravadaAsync(envio.Id)).Guias.ToDictionary(g => g.CodigoId!.Value);
        itens[ids[0]].Situacao.Should().Be(SituacaoGuiaTiss.Glosada);
        itens[ids[0]].Motivo.Should().Contain("1307");
        itens[ids[1]].Situacao.Should().Be(SituacaoGuiaTiss.Recebida);
        itens[ids[1]].NumeroGuiaOperadora.Should().Be("OP-B");
        (await GravadaAsync(status.Id)).Guias.Should().BeEmpty("a consulta não tem itens próprios: ela atualiza os do envio");
        (await fila.GuiasRecebidasSemBaixaAsync(envio.Id)).Select(g => g.CodigoId).Should().Equal(ids[1]);
    }

    [Fact]
    public async Task Status_de_envio_sem_protocolo_nao_e_pedido()
    {
        var ids = await _b.GuiasAsync(1);
        var envio = await _b.Fila().EnfileirarEnvioAsync(P, ids, Op);

        var pedir = () => _b.Fila().EnfileirarStatusDoEnvioAsync(envio.Id, Op);

        (await pedir.Should().ThrowAsync<InvalidOperationException>()).WithMessage("*não tem protocolo*");
    }

    // ---------- a tela acompanha ----------

    [Fact]
    public async Task A_tela_ve_o_lote_com_as_guias_sem_os_xml_e_acha_o_lote_pela_guia()
    {
        var ids = await _b.GuiasAsync(2);
        var t = await EnviadoAsync(ids);
        await _b.Fila().ConcluirAsync(t.Id, new ConclusaoTransacaoTiss { Resultado = "Lote recebido.", Protocolo = "987654", XmlRecebido = "<r/>" });
        var fila = _b.Fila(new ClinicaRepositorio(_b.NovoContexto()));

        var naTela = (await fila.ObterAsync(t.Id))!;
        var pelaGuia = await fila.DaGuiaAsync(ids[1]);

        naTela.XmlEnviado.Should().BeNull();
        naTela.XmlRecebido.Should().BeNull();
        naTela.Guias.Select(g => g.CodigoId).Should().Equal(ids.Select(i => (int?)i));
        naTela.Guias.Should().OnlyContain(g => g.Situacao == SituacaoGuiaTiss.Recebida);
        pelaGuia.Should().ContainSingle().Which.Id.Should().Be(t.Id, "a guia está num item do lote, não no campo da transação");
    }

    [Theory]
    [InlineData(12, 1, "C12")]
    [InlineData(12, 2, "C12R2")]
    [InlineData(int.MaxValue, 99, "C2147483647R99")]
    public void O_numero_da_guia_no_prestador_muda_a_cada_envio_e_cabe_em_20_caracteres(int codigoId, int vez, string esperado)
    {
        FilaTissService.NumeroDoPrestador(codigoId, vez).Should().Be(esperado);
        esperado.Length.Should().BeLessOrEqualTo(20);
    }
}
