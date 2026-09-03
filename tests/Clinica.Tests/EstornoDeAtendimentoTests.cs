using Clinica.Application.Servicos;
using Clinica.Domain;
using Clinica.Domain.Entities;
using Clinica.Infrastructure;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Clinica.Tests;

/// <summary>
/// ESTORNAR UM ATENDIMENTO (parcela 94) — desfazer a sessão lançada por engano.
///
/// Por que ele não existia
/// -----------------------
/// O <c>RemarcarAsync</c> recusa horário já realizado dizendo "Estorne o atendimento
/// antes" — e não havia estorno de atendimento nenhum. A instrução mandava fazer uma coisa
/// que não existe, e a saída que a recepção encontrava era o CANCELAR, que não tem trava.
///
/// O motivo de não existir aparece quando se lista o que o lançamento FAZ: cinco efeitos
/// em três serviços, e só um é a guia. Um estorno que desfizesse apenas a guia seria um
/// meio-estorno com cara de completo.
///
/// A ARMADILHA que estes testes fixam
/// ----------------------------------
/// O estorno SOLTA o horário, para a sessão poder ser relançada limpa. Só que o backfill
/// do <c>RealizadoEm</c> carimba como realizado todo atendimento sem carimbo que não tenha
/// horário em outro estado apontando para ele — e sem horário nenhum apontando, o
/// atendimento estornado passaria a contar como sessão que aconteceu na próxima vez que a
/// chave "guia no agendamento" fosse ligada. BI, retenção e origem de pacientes
/// corrompidos, sem um sintoma sequer. É o que <c>Atendimento.EstornadoEm</c> impede, e o
/// teste <c>O_backfill_NAO_ressuscita_atendimento_estornado</c> é o que amarra isso.
/// </summary>
public class EstornoDeAtendimentoTests : IDisposable
{
    private readonly SqliteConnection _conn;
    private readonly ClinicaDbContext _db;
    private readonly ClinicaRepositorio _repo;
    private readonly ParametrosService _parametros;
    private readonly AtendimentoService _atendimentos;
    private readonly AgendaService _agenda;
    private readonly PacoteService _pacotes;
    private readonly FinanceiroService _financeiro;
    private readonly EstoqueService _estoque;
    private readonly EstornoAtendimentoService _estorno;

    public EstornoDeAtendimentoTests()
    {
        _conn = new SqliteConnection("DataSource=:memory:");
        _conn.Open();
        var options = new DbContextOptionsBuilder<ClinicaDbContext>().UseSqlite(_conn).Options;
        _db = new ClinicaDbContext(options);
        _db.Database.EnsureCreated();
        _repo = new ClinicaRepositorio(_db);
        _parametros = new ParametrosService(_repo);
        _atendimentos = new AtendimentoService(_repo, parametros: _parametros);
        _agenda = new AgendaService(_repo, _atendimentos, _parametros);
        _pacotes = new PacoteService(_repo);
        _financeiro = new FinanceiroService(_repo);
        _estoque = new EstoqueService(_repo);
        _estorno = new EstornoAtendimentoService(_repo, _pacotes, _financeiro, _estoque);
    }

    public void Dispose()
    {
        _db.Dispose();
        _conn.Dispose();
        GC.SuppressFinalize(this);
    }

    private static readonly DateTime Quando = new(2026, 9, 3, 14, 20, 0);

    private async Task<int> PacienteAsync()
    {
        var p = new Paciente
        {
            Nome = "Severino da Silva", Convenio = Convenio.UnimedIntercambio, Sexo = Sexo.Masculino
        };
        _db.Pacientes.Add(p);
        await _db.SaveChangesAsync();
        return p.Id;
    }

    /// <summary>Uma sessão lançada de verdade: horário + check-in + atendimento + guias.</summary>
    private async Task<(int PacienteId, int AgendamentoId, Atendimento Atendimento)> SessaoLancadaAsync()
    {
        var paciente = await PacienteAsync();
        var (ag, lancamento) = await _agenda.LancarAvulsoAsync(
            paciente, Quando, ModalidadeAtendimento.AcupunturaComEletro, null, operador: "flavia@");
        return (paciente, ag.Id, lancamento.Atendimento);
    }

    private static DecisaoDeEstorno So(string motivo = "Lançado no paciente errado")
        => new(motivo);

    // ================================================================
    // O NÚCLEO: anula, marca, solta — e NÃO apaga
    // ================================================================

    [Fact]
    public async Task Estorno_anula_as_guias_e_marca_o_atendimento()
    {
        var (_, _, atendimento) = await SessaoLancadaAsync();
        var quantasGuias = atendimento.Codigos.Count;

        var r = await _estorno.EstornarAsync(atendimento.Id, So(), "flavia@");

        r.GuiasAnuladas.Should().Be(quantasGuias);

        var depois = await _repo.ObterAtendimentoAsync(atendimento.Id);
        depois!.Estornado.Should().BeTrue();
        depois.EstornadoPor.Should().Be("flavia@");
        depois.MotivoEstorno.Should().Be("Lançado no paciente errado");
        depois.RealizadoEm.Should().BeNull("a sessão deixa de contar como realizada");
        depois.Codigos.Should().OnlyContain(c => c.Status == StatusCodigo.NaoAplicavel);
        depois.Codigos.Should().OnlyContain(
            c => c.ObservacaoPendencia!.StartsWith(EstornoAtendimentoService.MarcaEstorno));
    }

    /// <summary>
    /// NADA é apagado. Atendimento é lastro de faturamento — e um <c>Remove</c> deixaria o
    /// horário ÓRFÃO por <c>OnDelete(SetNull)</c>, que é o estado dos três encaixes de
    /// 12/08/2026: kanban dizendo "Finalizado" e repasse excluindo a sessão em silêncio.
    /// </summary>
    [Fact]
    public async Task Estorno_nao_apaga_o_atendimento_nem_os_codigos()
    {
        var (_, _, atendimento) = await SessaoLancadaAsync();
        var quantasGuias = atendimento.Codigos.Count;

        await _estorno.EstornarAsync(atendimento.Id, So(), "flavia@");

        _db.Atendimentos.Should().ContainSingle(a => a.Id == atendimento.Id);
        _db.Codigos.Count(c => c.AtendimentoId == atendimento.Id).Should().Be(quantasGuias);
    }

    /// <summary>
    /// O horário é SOLTO — e os carimbos da fila vão junto. Um horário reaberto que
    /// guardasse a chegada nasceria na raia "Na recepção" com o paciente em casa (a lição
    /// das parcelas 69 e 74).
    /// </summary>
    [Fact]
    public async Task Estorno_solta_o_horario_e_limpa_os_carimbos_da_fila()
    {
        var (_, agendamentoId, atendimento) = await SessaoLancadaAsync();

        var antes = await _repo.ObterAgendamentoAsync(agendamentoId);
        antes!.Status.Should().Be(StatusAgendamento.Realizado);
        antes.ChegadaEm.Should().NotBeNull();

        var r = await _estorno.EstornarAsync(atendimento.Id, So(), "flavia@");

        r.AgendamentoLiberado.Should().Be(agendamentoId);
        var depois = await _repo.ObterAgendamentoAsync(agendamentoId);
        depois!.Status.Should().Be(StatusAgendamento.Agendado);
        depois.AtendimentoId.Should().BeNull();
        depois.ChegadaEm.Should().BeNull();
        depois.ChamadoEm.Should().BeNull();
        depois.InicioAtendimentoEm.Should().BeNull();
        depois.FimAtendimentoEm.Should().BeNull();
        depois.Etapa.Should().Be(EtapaFila.Aguardando);
    }

    /// <summary>
    /// ⚠️ O TESTE DA ARMADILHA. Sem `EstornadoEm` no filtro do backfill, o atendimento
    /// estornado — que ficou sem horário apontando para ele — seria carimbado como
    /// realizado na próxima ativação da chave "guia no agendamento", e voltaria a contar
    /// como visita em BI, retenção e origem de pacientes. Silenciosamente.
    /// </summary>
    [Fact]
    public async Task O_backfill_NAO_ressuscita_atendimento_estornado()
    {
        var (_, _, atendimento) = await SessaoLancadaAsync();
        await _estorno.EstornarAsync(atendimento.Id, So(), "flavia@");

        await _repo.MarcarAtendimentosSemCarimboComoRealizadosAsync();

        var depois = await _repo.ObterAtendimentoAsync(atendimento.Id);
        depois!.RealizadoEm.Should().BeNull(
            "estornado não é sessão que aconteceu — e o backfill não pode devolvê-lo à contagem");
    }

    /// <summary>
    /// A ponta final: estornado o engano, a sessão pode ser lançada de novo pelo mesmo
    /// horário — e nasce um atendimento NOVO, com guias novas. Sem soltar o horário, o
    /// `ConfirmarNucleoAsync` reaproveitaria o atendimento anulado e a sessão nova ficaria
    /// sem guia faturável, em silêncio.
    /// </summary>
    [Fact]
    public async Task Depois_do_estorno_o_horario_pode_ser_relancado_e_gera_guia_nova()
    {
        var (_, agendamentoId, atendimento) = await SessaoLancadaAsync();
        await _estorno.EstornarAsync(atendimento.Id, So(), "flavia@");

        var (ag, novo) = await _agenda.LancarNoHorarioAsync(
            agendamentoId, null, operador: "flavia@");

        novo.Atendimento.Id.Should().NotBe(atendimento.Id, "é um atendimento novo, não o anulado");
        novo.Atendimento.Codigos.Should().Contain(c => c.Status != StatusCodigo.NaoAplicavel);
        ag.AtendimentoId.Should().Be(novo.Atendimento.Id);
        ag.Status.Should().Be(StatusAgendamento.Realizado);
    }

    // ================================================================
    // AS RECUSAS
    // ================================================================

    /// <summary>
    /// Guia já baixada = o fato saiu da clínica. Mesma trava do `AjustarAoRemarcarAsync`:
    /// desfazer aqui deixaria o portal do convênio e o sistema dizendo coisas diferentes.
    /// </summary>
    [Fact]
    public async Task Recusa_quando_ha_guia_ja_baixada()
    {
        var (_, _, atendimento) = await SessaoLancadaAsync();
        var codigo = await _repo.ObterCodigoAsync(atendimento.Codigos[0].Id);
        codigo!.DarBaixa(new DateOnly(2026, 9, 3), "123456", "flavia@", null);
        await _repo.SalvarAsync();

        var erro = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _estorno.EstornarAsync(atendimento.Id, So(), "flavia@"));

        erro.Message.Should().Contain("baixada");
        erro.Message.Should().Contain("estorne a baixa", "a recusa diz o que fazer");

        (await _repo.ObterAtendimentoAsync(atendimento.Id))!.Estornado.Should().BeFalse();
    }

    [Fact]
    public async Task A_previa_avisa_do_impedimento_antes_de_abrir_a_janela()
    {
        var (_, _, atendimento) = await SessaoLancadaAsync();
        var codigo = await _repo.ObterCodigoAsync(atendimento.Codigos[0].Id);
        codigo!.DarBaixa(new DateOnly(2026, 9, 3), "123456", "flavia@", null);
        await _repo.SalvarAsync();

        var previa = await _estorno.PreverAsync(atendimento.Id);

        previa.Pode.Should().BeFalse();
        previa.Impedimento.Should().Contain("baixada");
    }

    [Fact]
    public async Task Estornar_duas_vezes_e_recusado()
    {
        var (_, _, atendimento) = await SessaoLancadaAsync();
        await _estorno.EstornarAsync(atendimento.Id, So(), "flavia@");

        var erro = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _estorno.EstornarAsync(atendimento.Id, So(), "flavia@"));

        erro.Message.Should().Contain("já foi estornado");
    }

    /// <summary>O motivo é o que fica para quem auditar — sem ele não há o que registrar.</summary>
    [Fact]
    public async Task Motivo_e_obrigatorio()
    {
        var (_, _, atendimento) = await SessaoLancadaAsync();

        await Assert.ThrowsAsync<ArgumentException>(
            () => _estorno.EstornarAsync(atendimento.Id, new DecisaoDeEstorno("   "), "flavia@"));
    }

    [Fact]
    public async Task Trilha_registra_quem_estornou_e_por_que()
    {
        var (pacienteId, _, atendimento) = await SessaoLancadaAsync();

        await _estorno.EstornarAsync(
            atendimento.Id, So("Paciente desistiu antes de entrar na sala"), "flavia@");

        var evento = _db.Auditoria.Single(e => e.Acao == "AtendimentoEstornado");
        evento.Operador.Should().Be("flavia@");
        evento.PacienteId.Should().Be(pacienteId);
        evento.Detalhe.Should().Contain("Paciente desistiu antes de entrar na sala");
    }

    // ================================================================
    // ITEM A ITEM: só desfaz o que foi marcado
    // ================================================================

    [Fact]
    public async Task Caixa_so_e_desfeito_quando_marcado()
    {
        var (pacienteId, _, atendimento) = await SessaoLancadaAsync();
        var lancamento = await _financeiro.LancarAsync(
            new DateOnly(2026, 9, 3), TipoLancamento.Entrada, "Sessão", 120m,
            pacienteId: pacienteId, atendimentoId: atendimento.Id);

        // Sem marcar: o dinheiro fica.
        await _estorno.EstornarAsync(atendimento.Id, So(), "flavia@");
        _db.Lancamentos.Single(l => l.Id == lancamento.Id)
            .Status.Should().NotBe(StatusLancamento.Cancelado);
    }

    [Fact]
    public async Task Caixa_desfeito_quando_marcado()
    {
        var (pacienteId, _, atendimento) = await SessaoLancadaAsync();
        var lancamento = await _financeiro.LancarAsync(
            new DateOnly(2026, 9, 3), TipoLancamento.Entrada, "Sessão", 120m,
            pacienteId: pacienteId, atendimentoId: atendimento.Id);

        var r = await _estorno.EstornarAsync(
            atendimento.Id, new DecisaoDeEstorno("Engano", DesfazerCaixa: true), "flavia@");

        r.CaixaDesfeito.Should().BeTrue();
        _db.Lancamentos.Single(l => l.Id == lancamento.Id)
            .Status.Should().Be(StatusLancamento.Cancelado);
    }

    [Fact]
    public async Task Sessao_do_pacote_volta_ao_saldo_quando_marcada()
    {
        var (pacienteId, _, atendimento) = await SessaoLancadaAsync();

        var catalogo = new PacoteCatalogo
        {
            Nome = "10 sessões", SessoesIncluidas = 10, ValidadeDias = 180, Valor = 1000m, Ativo = true
        };
        _db.PacotesCatalogo.Add(catalogo);
        await _db.SaveChangesAsync();
        await _pacotes.VenderAsync(pacienteId, catalogo.Id, DateOnly.FromDateTime(Quando));
        await _pacotes.ConsumirPorAtendimentoAsync(
            pacienteId, atendimento.Id, DateOnly.FromDateTime(Quando), operador: "flavia@");

        (await _pacotes.DoPacienteAsync(pacienteId))[0].SessoesUsadas.Should().Be(1);

        var r = await _estorno.EstornarAsync(
            atendimento.Id, new DecisaoDeEstorno("Engano", DevolverSessaoDoPacote: true), "flavia@");

        r.SessaoDevolvida.Should().BeTrue();
        (await _pacotes.DoPacienteAsync(pacienteId))[0].SessoesUsadas.Should().Be(0);
    }

    /// <summary>
    /// O insumo volta por ENTRADA compensatória, não por exclusão do movimento: extrato de
    /// estoque não se apaga — é ele que explica o saldo.
    /// </summary>
    [Fact]
    public async Task Insumo_volta_por_entrada_compensatoria_sem_apagar_o_movimento()
    {
        var (pacienteId, _, atendimento) = await SessaoLancadaAsync();

        var item = await _estoque.SalvarItemAsync(new ItemEstoque
        {
            Nome = "Agulha 0,25x30", Unidade = "un", Ativo = true
        });
        await _estoque.EntrarAsync(item.Id, 100m, operador: "flavia@");
        await _estoque.BaixarAsync(item.Id, 8m, atendimento.Id, pacienteId, operador: "flavia@");

        var saldoAntes = (await _estoque.SaldosAsync()).Single(s => s.ItemId == item.Id).Saldo;
        saldoAntes.Should().Be(92m);

        var r = await _estorno.EstornarAsync(
            atendimento.Id, new DecisaoDeEstorno("Engano", DevolverInsumoAoEstoque: true), "flavia@");

        r.InsumosDevolvidos.Should().Be(1);
        (await _estoque.SaldosAsync()).Single(s => s.ItemId == item.Id).Saldo.Should().Be(100m);

        // A saída CONTINUA no extrato — o saldo volta por uma entrada nova.
        var movimentos = await _repo.MovimentosDoAtendimentoAsync(atendimento.Id);
        movimentos.Should().ContainSingle(m => m.Tipo == TipoMovimentoEstoque.Saida);
    }

    // ================================================================
    // A PRÉVIA
    // ================================================================

    [Fact]
    public async Task Previa_lista_o_que_o_atendimento_produziu()
    {
        var (pacienteId, agendamentoId, atendimento) = await SessaoLancadaAsync();
        await _financeiro.LancarAsync(
            new DateOnly(2026, 9, 3), TipoLancamento.Entrada, "Sessão", 120m,
            pacienteId: pacienteId, atendimentoId: atendimento.Id);

        var previa = await _estorno.PreverAsync(atendimento.Id);

        previa.Pode.Should().BeTrue();
        previa.AtendimentoId.Should().Be(atendimento.Id);
        previa.Paciente.Should().Be("Severino da Silva");
        previa.AgendamentoId.Should().Be(agendamentoId);
        previa.GuiasAnulaveis.Should().Be(atendimento.Codigos.Count);
        previa.TemCaixa.Should().BeTrue();
        previa.EntradaNoCaixa.Should().Be(120m);
        previa.TemPacote.Should().BeFalse();
        previa.TemInsumo.Should().BeFalse();
    }

    /// <summary>
    /// A prévia de um atendimento sem nada pendurado não oferece caixinha nenhuma — e
    /// oferecer o que não existe é o defeito do botão que não faz nada.
    /// </summary>
    [Fact]
    public async Task Previa_de_sessao_sem_fechamento_nao_oferece_nada_alem_das_guias()
    {
        var (_, _, atendimento) = await SessaoLancadaAsync();

        var previa = await _estorno.PreverAsync(atendimento.Id);

        previa.TemCaixa.Should().BeFalse();
        previa.TemPacote.Should().BeFalse();
        previa.TemInsumo.Should().BeFalse();
        previa.GuiasAnulaveis.Should().BeGreaterThan(0);
    }
}
