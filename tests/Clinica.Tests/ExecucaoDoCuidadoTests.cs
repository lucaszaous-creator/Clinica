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
/// A ETAPA 4 DO PROCESSO DE ENFERMAGEM — a IMPLEMENTAÇÃO (parcela 76).
///
/// A COFEN 358/2009 divide o processo em cinco etapas, e o sistema cobria as três primeiras
/// e a quarta só como TEXTO: a enfermeira escrevia "curativo a cada 24h" e nada registrava
/// que foi feito. Cuidado que não se registra é, para qualquer fiscalização, cuidado que
/// não aconteceu.
///
/// A asserção que carrega a suíte é a que separa este registro do da folha de infusão:
/// <b>o mesmo cuidado se executa VÁRIAS vezes</b>. Copiar de lá a guarda "item já checado
/// não se edita" impediria a segunda troca de curativo do dia — e a técnica registraria a
/// primeira e desistiria da segunda, que é como se perde uma folha inteira.
/// </summary>
public class ExecucaoDoCuidadoTests : IDisposable
{
    private readonly SqliteConnection _conn;
    private readonly ClinicaDbContext _db;
    private readonly ClinicaRepositorio _repo;
    private readonly ChecagemCuidadoService _servico;

    /// <summary>Relógio congelado ao MEIO-DIA: a recusa de hora futura precisa de um "agora"
    /// conhecido, senão o teste vira loteria perto da meia-noite.</summary>
    private static DateTime MeioDia => DateTime.Today.AddHours(12);
    private static DateOnly Hoje => DateOnly.FromDateTime(DateTime.Today);

    private static readonly IdentificacaoExecutante Tecnica =
        new(UsuarioId: null, Nome: "Joana Técnica", Conselho: "COREN-SP 999999");

    public ExecucaoDoCuidadoTests()
    {
        _conn = new SqliteConnection("DataSource=:memory:");
        _conn.Open();
        _db = new ClinicaDbContext(
            new DbContextOptionsBuilder<ClinicaDbContext>().UseSqlite(_conn).Options);
        _db.Database.EnsureCreated();
        _repo = new ClinicaRepositorio(_db);
        _servico = new ChecagemCuidadoService(_repo, () => MeioDia);
    }

    /// <summary>Paciente com plano de cuidados: um de rotina e um "se necessário".</summary>
    private async Task<(int PacienteId, int Curativo, int SeDor)> PlanoAsync(
        DateOnly? prescritoEm = null, bool cancelada = false)
    {
        var p = new Paciente { Nome = "Marisa Silva", Convenio = Convenio.UnimedIntercambio };
        _db.Pacientes.Add(p);
        await _db.SaveChangesAsync();

        var evolucao = new EvolucaoEnfermagem
        {
            PacienteId = p.Id,
            Data = prescritoEm ?? Hoje,
            Hora = new TimeOnly(8, 0),
            Texto = "Admissão. Ferida operatória em MID.",
            AutorNome = "Enfermeira Ana",
            AutorConselho = "COREN-SP 111111",
            RegistradoEm = MeioDia,
            CanceladaEm = cancelada ? MeioDia : null,
            MotivoCancelamento = cancelada ? "lançada no paciente errado" : null,
            Cuidados =
            [
                new CuidadoEnfermagem { Descricao = "Curativo em MID", Frequencia = "a cada 12h", Ordem = 1 },
                new CuidadoEnfermagem
                {
                    Descricao = "Analgésico conforme prescrição",
                    Frequencia = "se dor > 5", Ordem = 2, SeNecessario = true
                }
            ]
        };
        _db.EvolucoesEnfermagem.Add(evolucao);
        await _db.SaveChangesAsync();

        return (p.Id, evolucao.Cuidados[0].Id, evolucao.Cuidados[1].Id);
    }

    // ==================== O registro ====================

    [Fact]
    public async Task Registra_com_a_hora_do_FATO_e_o_relogio_ao_lado()
    {
        var (_, curativo, _) = await PlanoAsync();

        var c = await _servico.ChecarAsync(
            curativo, SituacaoChecagem.Realizado, Hoje, new TimeOnly(9, 30), Tecnica,
            observacao: "Ferida limpa, sem secreção.");

        c.HoraRealizacao.Should().Be(new TimeOnly(9, 30), "é a hora em que a técnica executou");
        c.RegistradoEm.Should().Be(MeioDia, "o relógio fica AO LADO, e é o INJETADO");
        c.ExecutanteConselho.Should().Be("COREN-SP 999999");
        c.Observacao.Should().Be("Ferida limpa, sem secreção.");
        c.AtrasoDoRegistro.Should().Be(TimeSpan.FromHours(2.5));
    }

    /// <summary>
    /// ⚠️ A asserção que carrega a suíte. "Curativo a cada 12h" acontece DUAS vezes no dia,
    /// e a guarda "item já checado não se edita" — correta na folha de infusão, onde o item
    /// é de administração única — impediria a segunda.
    /// </summary>
    [Fact]
    public async Task O_MESMO_cuidado_se_executa_varias_vezes_no_dia()
    {
        var (pacienteId, curativo, _) = await PlanoAsync();

        await _servico.ChecarAsync(curativo, SituacaoChecagem.Realizado, Hoje, new TimeOnly(8, 0), Tecnica);
        await _servico.ChecarAsync(curativo, SituacaoChecagem.Realizado, Hoje, new TimeOnly(11, 0), Tecnica);

        var plano = await _servico.PlanoDoDiaAsync(pacienteId, Hoje);

        plano!.Cuidados.Single(c => c.CuidadoId == curativo)
            .Checagens.Should().HaveCount(2)
            .And.BeInAscendingOrder(c => c.HoraRealizacao);
    }

    // ==================== As recusas ====================

    /// <summary>Regra de SEGURANÇA: registrar adiantado é o hábito que faz aparecer como
    /// executado um cuidado num paciente que saiu antes de recebê-lo.</summary>
    [Fact]
    public async Task Hora_no_futuro_e_recusada()
    {
        var (_, curativo, _) = await PlanoAsync();

        var erro = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _servico.ChecarAsync(
                curativo, SituacaoChecagem.Realizado, Hoje, new TimeOnly(14, 0), Tecnica));

        erro.Message.Should().Contain("está no futuro");
    }

    /// <summary>A "rodela" do papel: circular o horário sem dizer por quê é o mesmo que não
    /// registrar nada.</summary>
    [Fact]
    public async Task Nao_realizado_SEM_justificativa_e_recusado()
    {
        var (_, curativo, _) = await PlanoAsync();

        var erro = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _servico.ChecarAsync(
                curativo, SituacaoChecagem.NaoRealizado, Hoje, new TimeOnly(9, 0), Tecnica));

        erro.Message.Should().Contain("não foi realizado");
    }

    [Fact]
    public async Task Sem_COREN_e_recusado()
    {
        var (_, curativo, _) = await PlanoAsync();

        var erro = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _servico.ChecarAsync(
                curativo, SituacaoChecagem.Realizado, Hoje, new TimeOnly(9, 0),
                new IdentificacaoExecutante(null, "Joana Técnica")));

        erro.Message.Should().Contain("conselho");
    }

    [Fact]
    public async Task Execucao_ANTES_da_prescricao_e_recusada()
    {
        var (_, curativo, _) = await PlanoAsync();

        var erro = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _servico.ChecarAsync(
                curativo, SituacaoChecagem.Realizado, Hoje.AddDays(-1), new TimeOnly(9, 0), Tecnica));

        erro.Message.Should().Contain("prescrito em");
    }

    [Fact]
    public async Task Cuidado_de_evolucao_CANCELADA_nao_se_executa()
    {
        var (_, curativo, _) = await PlanoAsync(cancelada: true);

        var erro = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _servico.ChecarAsync(
                curativo, SituacaoChecagem.Realizado, Hoje, new TimeOnly(9, 0), Tecnica));

        erro.Message.Should().Contain("cancelado");
    }

    // ==================== Retificar, nunca apagar ====================

    [Fact]
    public async Task Retificar_deixa_a_anterior_na_base_e_so_a_nova_vale()
    {
        var (pacienteId, curativo, _) = await PlanoAsync();

        var errada = await _servico.ChecarAsync(
            curativo, SituacaoChecagem.Realizado, Hoje, new TimeOnly(9, 0), Tecnica);

        await _servico.RetificarAsync(
            errada.Id, SituacaoChecagem.Realizado, Hoje, new TimeOnly(10, 0), Tecnica,
            motivo: "digitei 9h, o curativo foi às 10h");

        var todas = await _servico.HistoricoDoCuidadoAsync(curativo);
        todas.Should().HaveCount(2, "nada se apaga: a anterior fica, marcada");

        var plano = await _servico.PlanoDoDiaAsync(pacienteId, Hoje);
        plano!.Cuidados.Single(c => c.CuidadoId == curativo)
            .Checagens.Should().ContainSingle()
            .Which.HoraRealizacao.Should().Be(new TimeOnly(10, 0),
                "a retificada sai do quadro do dia — no papel ela continua, marcada");
    }

    [Fact]
    public async Task Retificar_uma_ja_retificada_e_recusado()
    {
        var (_, curativo, _) = await PlanoAsync();

        var primeira = await _servico.ChecarAsync(
            curativo, SituacaoChecagem.Realizado, Hoje, new TimeOnly(9, 0), Tecnica);
        await _servico.RetificarAsync(
            primeira.Id, SituacaoChecagem.Realizado, Hoje, new TimeOnly(10, 0), Tecnica,
            motivo: "hora errada");

        var erro = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _servico.RetificarAsync(
                primeira.Id, SituacaoChecagem.Realizado, Hoje, new TimeOnly(11, 0), Tecnica,
                motivo: "de novo"));

        erro.Message.Should().Contain("já foi corrigido");
    }

    [Fact]
    public async Task Retificar_SEM_motivo_e_recusado()
    {
        var (_, curativo, _) = await PlanoAsync();
        var c = await _servico.ChecarAsync(
            curativo, SituacaoChecagem.Realizado, Hoje, new TimeOnly(9, 0), Tecnica);

        var erro = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _servico.RetificarAsync(
                c.Id, SituacaoChecagem.Realizado, Hoje, new TimeOnly(10, 0), Tecnica,
                motivo: "   "));

        erro.Message.Should().Contain("por que o registro anterior estava errado");
    }

    // ==================== A pendência ====================

    /// <summary>
    /// ⚠️ O "se necessário" NÃO conta como pendência — a mesma sutileza do SOS da folha de
    /// infusão. Contá-lo deixaria todo plano com um cuidado eternamente atrasado, e o
    /// contador da sala, que existe para dizer o que falta fazer, passaria a apontar para
    /// nada.
    /// </summary>
    [Fact]
    public async Task O_se_necessario_nao_conta_como_pendencia()
    {
        var (pacienteId, curativo, seDor) = await PlanoAsync();

        var plano = await _servico.PlanoDoDiaAsync(pacienteId, Hoje);

        plano!.Pendentes.Should().Be(1, "só o curativo cobra uma palavra da técnica");
        plano.Cuidados.Single(c => c.CuidadoId == seDor).Pendente.Should().BeFalse();
        plano.Cuidados.Single(c => c.CuidadoId == curativo).Pendente.Should().BeTrue();

        await _servico.ChecarAsync(curativo, SituacaoChecagem.Realizado, Hoje, new TimeOnly(9, 0), Tecnica);

        (await _servico.PlanoDoDiaAsync(pacienteId, Hoje))!.Pendentes.Should().Be(0);
    }

    /// <summary>
    /// O quadro é de UM dia. O curativo de ontem não pode aparecer como feito hoje — é o
    /// mesmo cuidado, e a folha de hoje cobra a execução de hoje.
    /// </summary>
    [Fact]
    public async Task O_quadro_do_dia_mostra_so_as_execucoes_daquele_dia()
    {
        var (pacienteId, curativo, _) = await PlanoAsync(prescritoEm: Hoje.AddDays(-3));

        await _servico.ChecarAsync(
            curativo, SituacaoChecagem.Realizado, Hoje.AddDays(-1), new TimeOnly(9, 0), Tecnica);

        var hoje = await _servico.PlanoDoDiaAsync(pacienteId, Hoje);
        hoje!.Cuidados.Single(c => c.CuidadoId == curativo).Checagens.Should().BeEmpty();
        hoje.Pendentes.Should().Be(1);

        var ontem = await _servico.PlanoDoDiaAsync(pacienteId, Hoje.AddDays(-1));
        ontem!.Cuidados.Single(c => c.CuidadoId == curativo).Checagens.Should().ContainSingle();
    }

    [Fact]
    public async Task Paciente_sem_plano_devolve_NULO_e_nao_plano_vazio()
    {
        var p = new Paciente { Nome = "Sem plano", Convenio = Convenio.UnimedIntercambio };
        _db.Pacientes.Add(p);
        await _db.SaveChangesAsync();

        (await _servico.PlanoDoDiaAsync(p.Id, Hoje)).Should().BeNull();
    }

    /// <summary>A trilha grava no MESMO SaveChanges: execução que possa acontecer sem a
    /// linha correspondente é execução sem trilha.</summary>
    [Fact]
    public async Task A_execucao_deixa_linha_na_auditoria()
    {
        var (pacienteId, curativo, _) = await PlanoAsync();

        await _servico.ChecarAsync(
            curativo, SituacaoChecagem.NaoRealizado, Hoje, new TimeOnly(9, 0), Tecnica,
            justificativa: "paciente ausente para exame");

        var eventos = await _db.Auditoria.ToListAsync();

        eventos.Should().ContainSingle()
            .Which.Acao.Should().Be("CuidadoEnfermagemNaoRealizado");
        eventos[0].PacienteId.Should().Be(pacienteId);
        eventos[0].Operador.Should().Be("Joana Técnica");
        eventos[0].Detalhe.Should().Contain("paciente ausente para exame");
    }

    // ==================== O circuito: exportação e guarda ====================

    /// <summary>
    /// ⚠️ A regra 8 do compromisso de conformidade: entidade clínica nova entra na
    /// EXPORTAÇÃO. Exportar o cuidado PRESCRITO sem o que foi EXECUTADO dá um prontuário
    /// que diz o que se mandou fazer e cala sobre o que foi feito — que é exatamente a
    /// pergunta de quem audita enfermagem.
    /// </summary>
    [Fact]
    public async Task A_execucao_sai_na_exportacao_do_prontuario()
    {
        var (pacienteId, curativo, _) = await PlanoAsync();
        await _servico.ChecarAsync(
            curativo, SituacaoChecagem.NaoRealizado, Hoje, new TimeOnly(9, 0), Tecnica,
            justificativa: "paciente ausente para exame");

        var arquivos = await new ExportacaoProntuarioService(_repo).ExportarAsync(pacienteId);

        var arquivo = arquivos.Single(a => a.Nome == "prontuario-enfermagem-execucoes.csv");
        arquivo.Conteudo.Should().Contain("Curativo em MID")
            .And.Contain("Não realizado")
            .And.Contain("paciente ausente para exame")
            .And.Contain("COREN-SP 999999");

        // ⚠️ O nome do cuidado sai do que está em memória, e não da navegação: com
        // `AsNoTracking` e sem `Include` ela viria nula em produção e a coluna sairia "#12".
        arquivo.Conteudo.Should().NotContain($"#{curativo}");
    }

    /// <summary>
    /// A execução MOVE o prazo de guarda, e move depois da evolução que a prescreveu: um
    /// plano escrito em janeiro é executado por semanas. Contar só a prescrição daria o
    /// prazo calculado pelo registro ERRADO.
    /// </summary>
    [Fact]
    public async Task A_execucao_move_o_prazo_de_guarda()
    {
        var (pacienteId, curativo, _) = await PlanoAsync(prescritoEm: Hoje.AddDays(-30));
        var guarda = new GuardaProntuarioService(_repo);

        var antes = await guarda.DoPacienteAsync(pacienteId);
        antes.UltimoRegistro.Should().Be(Hoje.AddDays(-30));

        await _servico.ChecarAsync(
            curativo, SituacaoChecagem.Realizado, Hoje, new TimeOnly(9, 0), Tecnica);

        var depois = await guarda.DoPacienteAsync(pacienteId);
        depois.UltimoRegistro.Should().Be(Hoje);
        depois.Origem.Should().Contain("cuidado");
    }

    public void Dispose()
    {
        _db.Dispose();
        _conn.Dispose();
    }
}
