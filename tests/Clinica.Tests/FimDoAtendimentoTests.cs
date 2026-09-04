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
/// O FIM do atendimento clínico (parcela 74).
///
/// A distinção que esta suíte fixa, e que é a decisão inteira: <b>encerrar não é
/// concluir</b>. Concluir são QUATRO fatos do mesmo ato — a guia nasce, o pacote debita, o
/// insumo sai, o dinheiro entra — e três deles são do balcão (parcela 61). O que o
/// profissional afirma ao encerrar é só <i>"terminei com esta pessoa"</i>.
///
/// Se alguém um dia fizer o encerramento marcar <c>Realizado</c> "para simplificar", os
/// três fatos do balcão deixam de acontecer em silêncio: o pacote não debita, o insumo não
/// sai e o caixa fecha sem a sessão. Nada falha — o dia só não bate. É esse desfecho que os
/// testes abaixo impedem.
/// </summary>
public class FimDoAtendimentoTests : IDisposable
{
    private readonly SqliteConnection _conn;
    private readonly ClinicaDbContext _db;
    private readonly ClinicaRepositorio _repo;
    private readonly AgendaService _agenda;

    private static readonly DateTime Manha = new(2026, 8, 24, 9, 0, 0);


    /// <summary>
    /// Todo horário MARCADO precisa de dono desde a parcela 95 — o fixture cria um para os
    /// cenários que não se importam com QUEM atende.
    /// </summary>
    private readonly int _profPadrao;

    public FimDoAtendimentoTests()
    {
        _conn = new SqliteConnection("DataSource=:memory:");
        _conn.Open();
        var options = new DbContextOptionsBuilder<ClinicaDbContext>().UseSqlite(_conn).Options;
        _db = new ClinicaDbContext(options);
        _db.Database.EnsureCreated();
        var profPadrao = new Profissional { Nome = "Dra. Padrão" };
        _db.Profissionais.Add(profPadrao);
        _db.SaveChanges();
        _profPadrao = profPadrao.Id;
        _repo = new ClinicaRepositorio(_db);
        _agenda = new AgendaService(_repo, new AtendimentoService(_repo));
    }

    private async Task<int> CriarPacienteAsync()
    {
        var p = new Paciente
        {
            Nome = "Paciente",
            Convenio = Convenio.UnimedIntercambio,
            Sexo = Sexo.Feminino
        };
        _db.Pacientes.Add(p);
        await _db.SaveChangesAsync();
        return p.Id;
    }

    private async Task<Agendamento> NaSalaAsync()
    {
        var ag = await _agenda.AgendarAsync(await CriarPacienteAsync(), Manha,
            ModalidadeAtendimento.AcupunturaSimples, null, profissionalId: _profPadrao);
        await _agenda.RegistrarChegadaAsync(ag.Id, "recepcao", Manha);
        await _agenda.IniciarAtendimentoAsync(ag.Id, "medica", Manha.AddMinutes(5));
        return ag;
    }

    // ===== A distinção central =====

    [Fact]
    public async Task Encerrar_NAO_conclui_o_atendimento()
    {
        var ag = await NaSalaAsync();

        await _agenda.EncerrarAtendimentoAsync(ag.Id, "medica", Manha.AddMinutes(35));

        var lido = (await _agenda.ObterAsync(ag.Id))!;
        lido.FimAtendimentoEm.Should().NotBeNull();

        // O horário continua ABERTO. O SERVIÇO só carimba — quem conclui é o passo
        // seguinte, e ele é explícito (parcela 95: a tela do Consultório encadeia
        // encerrar → concluir, e a fila do balcão continua concluindo pelo botão dela).
        // Fundir as duas coisas aqui dentro tornaria a conclusão um efeito colateral do
        // carimbo: toda porta que só quer registrar o fim passaria a gerar guia.
        lido.Status.Should().Be(StatusAgendamento.Agendado);
        lido.AtendimentoId.Should().BeNull();
        lido.AtendimentoEncerrado.Should().BeTrue();
    }

    [Fact]
    public async Task O_balcao_ainda_consegue_concluir_depois_de_encerrado()
    {
        var ag = await NaSalaAsync();
        await _agenda.EncerrarAtendimentoAsync(ag.Id, "medica", Manha.AddMinutes(35));

        // É o caminho normal: o médico termina, o paciente vai ao balcão, a recepção fecha.
        await _agenda.ConfirmarPresencaAsync(ag.Id);

        var lido = (await _agenda.ObterAsync(ag.Id))!;
        lido.Status.Should().Be(StatusAgendamento.Realizado);
        lido.AtendimentoId.Should().NotBeNull();
        // Concluído, o selo apaga: ele existe para dizer "está pronto para fechar".
        lido.AtendimentoEncerrado.Should().BeFalse();
        lido.Etapa.Should().Be(EtapaFila.Finalizado);
    }

    [Fact]
    public async Task O_cartao_continua_em_atendimento_ate_o_balcao_fechar()
    {
        var ag = await NaSalaAsync();
        await _agenda.EncerrarAtendimentoAsync(ag.Id, "medica", Manha.AddMinutes(35));

        // Não há sexta raia: o encerramento é SELO. Uma coluna permanente para um estado
        // que dura minutos é a faixa vazia comendo a tela que o README condena.
        (await _agenda.ObterAsync(ag.Id))!.Etapa.Should().Be(EtapaFila.EmAtendimento);
    }

    // ===== As guardas =====

    [Fact]
    public async Task Encerrar_sem_ter_comecado_e_recusado()
    {
        var ag = await _agenda.AgendarAsync(await CriarPacienteAsync(), Manha,
            ModalidadeAtendimento.AcupunturaSimples, null, profissionalId: _profPadrao);
        await _agenda.RegistrarChegadaAsync(ag.Id, "recepcao", Manha);

        // Fim sem começo produziria duração negativa e um cartão que sai da sala sem
        // nunca ter entrado nela.
        var acao = () => _agenda.EncerrarAtendimentoAsync(ag.Id, "medica");
        await acao.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*não entrou na sala*");
    }

    [Fact]
    public async Task Encerrar_de_novo_NAO_reescreve_a_hora()
    {
        var ag = await NaSalaAsync();
        await _agenda.EncerrarAtendimentoAsync(ag.Id, "medica", Manha.AddMinutes(35));
        await _agenda.EncerrarAtendimentoAsync(ag.Id, "medica", Manha.AddMinutes(50));

        // A razão é a do "chamar de novo" (parcela 38): quem clica duas vezes precisa
        // continuar vendo a HORA em que terminou, e o segundo clique esconderia justamente
        // o atendimento demorado.
        (await _agenda.ObterAsync(ag.Id))!.FimAtendimentoEm
            .Should().Be(Manha.AddMinutes(35));
    }

    [Fact]
    public async Task Encerrar_duas_vezes_grava_UMA_linha_de_trilha()
    {
        var ag = await NaSalaAsync();
        await _agenda.EncerrarAtendimentoAsync(ag.Id, "medica", Manha.AddMinutes(35));
        await _agenda.EncerrarAtendimentoAsync(ag.Id, "medica", Manha.AddMinutes(50));

        // Movimento idempotente que não mudou nada não grava linha: trilha com duplicata a
        // cada clique é trilha que ninguém lê (parcela 69).
        var trilha = await _db.Auditoria
            .Where(e => e.Acao == "FilaAtendimentoEncerrado").ToListAsync();
        trilha.Should().HaveCount(1);
        trilha[0].Operador.Should().Be("medica");
        trilha[0].Detalhe.Should().Contain("durou 30 min");
    }

    // ===== Voltar etapa =====

    [Fact]
    public async Task Voltar_etapa_MOVE_o_cartao_e_leva_o_encerramento_junto()
    {
        var ag = await NaSalaAsync();
        await _agenda.EncerrarAtendimentoAsync(ag.Id, "medica", Manha.AddMinutes(35));

        await _agenda.VoltarEtapaAsync(ag.Id, "medica");

        var lido = (await _agenda.ObterAsync(ag.Id))!;
        // ⚠️ UM clique, UMA coluna. FimAtendimentoEm não é coluna nenhuma, então gastá-lo
        // num passo próprio consumia o clique SEM mover o cartão enquanto as duas telas
        // afirmavam que ele tinha voltado. Sair da sala é o fato: quem não está mais em
        // atendimento não tem fim de atendimento.
        lido.Etapa.Should().Be(EtapaFila.Chamado);
        lido.InicioAtendimentoEm.Should().BeNull();
        lido.FimAtendimentoEm.Should().BeNull();

        // Os dois carimbos apagados vão ESCRITOS na trilha, com os valores.
        var trilha = await _db.Auditoria
            .Where(e => e.Acao == "FilaEtapaVoltada").FirstAsync();
        trilha.Detalhe.Should().Contain("entrada na sala").And.Contain("encerramento");
    }

    [Fact]
    public async Task Reabrir_desfaz_SO_o_encerramento_e_o_paciente_continua_na_sala()
    {
        var ag = await NaSalaAsync();
        await _agenda.EncerrarAtendimentoAsync(ag.Id, "medica", Manha.AddMinutes(35));

        await _agenda.ReabrirAtendimentoAsync(ag.Id, "medica");

        var lido = (await _agenda.ObterAsync(ag.Id))!;
        // É o caso do profissional que finalizou no paciente errado: ele quer desfazer o
        // encerramento SEM tirar a pessoa da sala. Por isso é ato próprio, como o
        // DesfazerChamadaAsync é separado do VoltarEtapaAsync desde a parcela 38.
        lido.FimAtendimentoEm.Should().BeNull();
        lido.InicioAtendimentoEm.Should().NotBeNull();
        lido.Etapa.Should().Be(EtapaFila.EmAtendimento);

        var trilha = await _db.Auditoria
            .Where(e => e.Acao == "FilaAtendimentoReaberto").FirstAsync();
        trilha.Detalhe.Should().Contain("Apagado o encerramento");
    }

    [Fact]
    public async Task Reabrir_o_que_nao_esta_encerrado_e_recusado()
    {
        var ag = await NaSalaAsync();

        var acao = () => _agenda.ReabrirAtendimentoAsync(ag.Id, "medica");
        await acao.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*não está encerrado*");
    }

    [Fact]
    public async Task Remarcar_para_OUTRO_DIA_apaga_o_encerramento()
    {
        var ag = await NaSalaAsync();
        await _agenda.EncerrarAtendimentoAsync(ag.Id, "medica", Manha.AddMinutes(35));

        await _agenda.RemarcarAsync(ag.Id, Manha.AddDays(3), null, operador: "recepcao");

        var lido = (await _agenda.ObterAsync(ag.Id))!;
        // Sem isto o horário de quinta nasceria com o selo verde "Encerrado às 09h40" de uma
        // sessão que não aconteceu — e cartão em "Aguardando" dizendo que já terminou é lido
        // pelo balcão como sessão pronta para fechar. É a lição da parcela 69: TODO carimbo
        // novo da fila entra no bloco que a remarcação limpa.
        lido.FimAtendimentoEm.Should().BeNull();
        lido.AtendimentoEncerrado.Should().BeFalse();
        lido.Etapa.Should().Be(EtapaFila.Aguardando);
    }

    [Fact]
    public async Task Remarcar_no_MESMO_dia_preserva_o_encerramento()
    {
        var ag = await NaSalaAsync();
        await _agenda.EncerrarAtendimentoAsync(ag.Id, "medica", Manha.AddMinutes(35));

        // Mexer em sala/duração/observação é ajuste do horário de hoje — apagar o que já
        // aconteceu seria destruir o fato pelo caminho errado.
        await _agenda.RemarcarAsync(ag.Id, Manha.AddMinutes(90), null, operador: "recepcao");

        (await _agenda.ObterAsync(ag.Id))!.FimAtendimentoEm.Should().Be(Manha.AddMinutes(35));
    }

    // ===== A duração =====

    [Fact]
    public async Task Duracao_e_nula_antes_de_o_paciente_entrar()
    {
        var ag = await _agenda.AgendarAsync(await CriarPacienteAsync(), Manha,
            ModalidadeAtendimento.AcupunturaSimples, null, profissionalId: _profPadrao);

        // Nula, nunca zero: "não começou" e "começou agora" são coisas diferentes, e zero
        // apareceria como um atendimento relâmpago para quem mede duração.
        (await _agenda.ObterAsync(ag.Id))!
            .DuracaoDoAtendimento(Manha.AddMinutes(30)).Should().BeNull();
    }

    [Fact]
    public async Task Duracao_corre_enquanto_esta_aberto_e_CONGELA_ao_encerrar()
    {
        var ag = await NaSalaAsync();
        var lido = (await _agenda.ObterAsync(ag.Id))!;

        lido.DuracaoDoAtendimento(Manha.AddMinutes(20)).Should().Be(15);

        await _agenda.EncerrarAtendimentoAsync(ag.Id, "medica", Manha.AddMinutes(35));
        lido = (await _agenda.ObterAsync(ag.Id))!;

        // Encerrado, o relógio para: a duração passa a ser um FATO, e não uma contagem.
        lido.DuracaoDoAtendimento(Manha.AddHours(3)).Should().Be(30);
    }

    [Fact]
    public async Task Relogio_para_tras_nao_produz_duracao_negativa()
    {
        var ag = await NaSalaAsync();

        // Fuso ou acerto de hora não podem escrever "há -12 min" numa tela que o médico
        // olha o tempo todo.
        (await _agenda.ObterAsync(ag.Id))!
            .DuracaoDoAtendimento(Manha.AddMinutes(-60)).Should().Be(0);
    }

    public void Dispose()
    {
        _db.Dispose();
        _conn.Dispose();
    }
}
