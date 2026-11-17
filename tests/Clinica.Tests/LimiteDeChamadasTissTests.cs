using Clinica.Application.Servicos;
using Clinica.Domain.Entities;
using Clinica.Infrastructure;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Clinica.Tests;

/// <summary>
/// O LIMITE DE CHAMADAS do web service TISS (nov/2026, docs/tiss-conector.md): a mesma carteira no
/// mesmo dia não vai de novo à operadora sem pedido explícito, e o teto diário de elegibilidade conta
/// só as idas REAIS (a transação recusada antes da rede não conta). Carteiras fictícias.
/// </summary>
public sealed class LimiteDeChamadasTissTests : IDisposable
{
    private readonly TissBancoDeTeste _b = new();
    private const string Conector = "conector-de-teste";
    private const string Carteira = "000123456789";

    public void Dispose() => _b.Dispose();

    private Task<TransacaoTiss> Pedir(string carteira = Carteira, bool deNovo = false)
        => _b.Fila().EnfileirarElegibilidadeAsync(TissBancoDeTeste.Petrobras, carteira, null, null, TissBancoDeTeste.Operador, consultarDeNovo: deNovo);

    /// <summary>Uma ida real e respondida: reserva, carimbo de saída e conclusão.</summary>
    private async Task<TransacaoTiss> IdaConcluidaAsync(int id)
    {
        var fila = _b.Fila();
        var t = await fila.ReservarProximaAsync(Conector);
        t!.Id.Should().Be(id);
        await fila.MarcarEnvioIniciadoAsync(id, "<xml/>");
        await fila.ConcluirAsync(id, new ConclusaoTransacaoTiss { Resultado = "Elegível." });
        return await GravadaAsync(id);
    }

    private Task<TransacaoTiss> GravadaAsync(int id) => _b.NovoContexto().TransacoesTiss.AsNoTracking().SingleAsync(t => t.Id == id);

    // ---------- a mesma carteira no mesmo dia ----------

    [Fact]
    public async Task Mesma_carteira_concluida_hoje_devolve_a_de_hoje_e_nao_cria_outra()
    {
        var primeira = await Pedir();
        await IdaConcluidaAsync(primeira.Id);
        _b.Agora = _b.Agora.AddHours(3);

        var segunda = await Pedir();

        segunda.Id.Should().Be(primeira.Id);
        segunda.Estado.Should().Be(EstadoTransacaoTiss.Concluida);
        segunda.Resultado.Should().Be("Elegível.");
        EnvioTissNaTela.DaElegibilidade(segunda, _b.Agora).ConsultadoEm.Should().Be("consultado hoje às 09:00");
        (await _b.Db.TransacoesTiss.CountAsync()).Should().Be(1, "nada novo entra na fila");
        (await _b.Fila().ReservarProximaAsync(Conector)).Should().BeNull("e nada vai à operadora");
    }

    [Fact]
    public async Task Mesma_carteira_ainda_na_fila_devolve_a_mesma_mesmo_com_consultar_de_novo()
    {
        var naFila = await Pedir();

        (await Pedir()).Id.Should().Be(naFila.Id);
        (await Pedir(deNovo: true)).Id.Should().Be(naFila.Id, "pedir de novo o que ainda não voltou só dobraria a ida");
        (await _b.Db.TransacoesTiss.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task Consultar_de_novo_e_pedido_explicito_que_cria_uma_ida_nova()
    {
        var primeira = await Pedir();
        await IdaConcluidaAsync(primeira.Id);

        var nova = await Pedir(deNovo: true);

        nova.Id.Should().NotBe(primeira.Id);
        nova.Estado.Should().Be(EstadoTransacaoTiss.Pendente);
        (await _b.Db.Auditoria.CountAsync(a => a.Acao == "PedidoElegibilidadeTiss")).Should().Be(2, "cada ida pedida deixa trilha");
    }

    [Fact]
    public async Task Concluida_ontem_outra_carteira_ou_consulta_que_falhou_nao_sao_reaproveitadas()
    {
        var ontem = await Pedir();
        await IdaConcluidaAsync(ontem.Id);
        _b.Agora = _b.Agora.AddDays(1);
        (await Pedir()).Id.Should().NotBe(ontem.Id, "a resposta de ontem não vale hoje");

        (await Pedir("000000000777")).NumeroCarteira.Should().Be("000000000777");

        var fila = _b.Fila();
        var falha = await Pedir("000000000888");
        var reservada = await fila.ReservarProximaAsync(Conector);
        while (reservada is not null && reservada.Id != falha.Id) reservada = await fila.ReservarProximaAsync(Conector);
        await fila.FalharAsync(falha.Id, "LoginInvalido", podeTentarDeNovo: false);
        (await Pedir("000000000888")).Id.Should().NotBe(falha.Id, "consulta que falhou não é resposta: a pessoa pode pedir de novo");
    }

    // ---------- o teto diário de elegibilidade ----------

    [Fact]
    public async Task Teto_diario_batido_deixa_a_consulta_pendente_com_a_frase_do_limite_sem_gastar_tentativa()
    {
        for (var i = 1; i <= 2; i++) await IdaConcluidaAsync((await Pedir($"00000000000{i}")).Id);
        var terceira = await Pedir("000000000003");
        var fila = _b.Fila();
        await fila.ReservarProximaAsync(Conector);

        (await fila.AdiarPorLimiteDiarioAsync(terceira.Id, limiteDiario: 2)).Should().BeTrue();

        var t = await GravadaAsync(terceira.Id);
        t.Estado.Should().Be(EstadoTransacaoTiss.Pendente, "não é falha nem recusa da operadora");
        t.Resultado.Should().Be(FilaTiss.FraseLimiteDiario);
        t.Erro.Should().BeNull();
        t.Tentativas.Should().Be(0, "a reserva desfeita não foi ida nenhuma");
        t.EnvioIniciadoEm.Should().BeNull();
        t.ProximaTentativaEm.Should().Be(_b.Agora + FilaTiss.ReconferenciaDoLimite);
        (await fila.ReservarProximaAsync(Conector)).Should().BeNull("até a reconferência, nada sai");
    }

    [Fact]
    public async Task Abaixo_do_teto_a_consulta_segue_e_o_teto_maior_libera_a_que_estava_parada()
    {
        await IdaConcluidaAsync((await Pedir("000000000001")).Id);
        var segunda = await Pedir("000000000002");
        var fila = _b.Fila();
        await fila.ReservarProximaAsync(Conector);
        (await fila.AdiarPorLimiteDiarioAsync(segunda.Id, limiteDiario: 1)).Should().BeTrue();

        // A direção sobe o teto: na reconferência a consulta segue.
        _b.Agora = _b.Agora + FilaTiss.ReconferenciaDoLimite;
        var outra = _b.Fila();
        (await outra.ReservarProximaAsync(Conector))!.Id.Should().Be(segunda.Id);
        (await outra.AdiarPorLimiteDiarioAsync(segunda.Id, limiteDiario: 300)).Should().BeFalse();
        (await GravadaAsync(segunda.Id)).Tentativas.Should().Be(1, "agora sim é uma tentativa");
    }

    [Fact]
    public async Task O_teto_conta_so_as_idas_reais_a_recusada_antes_da_rede_nao_conta()
    {
        // O caso real da VPS: recusada antes de ir à rede (Cancelada, Tentativas=1, sem carimbo de saída).
        var recusada = await Pedir("000000000001");
        var fila = _b.Fila();
        await fila.ReservarProximaAsync(Conector);
        await fila.RecusarAsync(recusada.Id, "Convênio sem registro ANS.");
        (await GravadaAsync(recusada.Id)).Tentativas.Should().Be(1);

        await IdaConcluidaAsync((await Pedir("000000000002")).Id);

        (await _b.Fila().IdasDeElegibilidadeHojeAsync(TissBancoDeTeste.Petrobras)).Should().Be(1, "só a que chamou a operadora");
        var terceira = await Pedir("000000000003");
        var f = _b.Fila();
        await f.ReservarProximaAsync(Conector);
        (await f.AdiarPorLimiteDiarioAsync(terceira.Id, limiteDiario: 2)).Should().BeFalse("uma ida real de duas permitidas");
    }

    [Fact]
    public async Task O_teto_e_do_dia_da_clinica_e_conta_as_tentativas_de_rede_da_propria_transacao()
    {
        await IdaConcluidaAsync((await Pedir("000000000001")).Id);
        _b.Agora = _b.Agora.AddDays(1);
        (await _b.Fila().IdasDeElegibilidadeHojeAsync(TissBancoDeTeste.Petrobras)).Should().Be(0, "o dia virou");

        // Uma consulta que já foi à rede hoje e ficou sem resposta: a nova reserva não conta, a ida de antes sim.
        var t = await Pedir("000000000002");
        var fila = _b.Fila();
        await fila.ReservarProximaAsync(Conector);
        await fila.MarcarEnvioIniciadoAsync(t.Id, "<xml/>");
        await fila.FalharAsync(t.Id, "sem resposta da operadora", podeTentarDeNovo: true);
        _b.Agora = _b.Agora.AddMinutes(2);
        var f = _b.Fila();
        (await f.ReservarProximaAsync(Conector))!.Id.Should().Be(t.Id);
        (await f.AdiarPorLimiteDiarioAsync(t.Id, limiteDiario: 2)).Should().BeFalse("1 ida real de 2");
        (await f.AdiarPorLimiteDiarioAsync(t.Id, limiteDiario: 1)).Should().BeTrue("a ida de antes já gastou o único lugar do dia");
    }

    [Fact]
    public async Task O_teto_nao_vale_para_envio_status_ou_cancelamento()
    {
        var fila = _b.Fila();
        var status = await fila.EnfileirarStatusProtocoloAsync(TissBancoDeTeste.Petrobras, "123456", null, TissBancoDeTeste.Operador);
        await fila.ReservarProximaAsync(Conector);
        (await fila.AdiarPorLimiteDiarioAsync(status.Id, limiteDiario: 0)).Should().BeFalse();
    }
}
