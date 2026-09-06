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
/// O que a remarcação NÃO pode levar embora (parcela 69).
///
/// Os dois defeitos que estes testes fixam tinham a mesma assinatura: build verde, suíte
/// verde, e o estrago aparecendo semanas depois, uma paciente por vez. Nenhum deles
/// quebrava nada na hora — e é por isso que eles precisam de teste, não de atenção.
/// </summary>
public class AgendaRemarcacaoTests : IDisposable
{
    private readonly SqliteConnection _conn;
    private readonly ClinicaDbContext _db;
    private readonly ClinicaRepositorio _repo;
    private readonly AgendaService _agenda;


    /// <summary>
    /// Todo horário MARCADO precisa de dono desde a parcela 95 — o fixture cria um para os
    /// cenários que não se importam com QUEM atende.
    /// </summary>
    private readonly int _profPadrao;

    public AgendaRemarcacaoTests()
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
        var p = new Paciente { Nome = "Paciente", Convenio = Convenio.UnimedIntercambio, Sexo = Sexo.Feminino };
        _db.Pacientes.Add(p);
        await _db.SaveChangesAsync();
        return p.Id;
    }

    /// <summary>
    /// O "Empurrar" de um bloqueio remarca em lote passando SÓ a data nova — é só isso que
    /// ele muda. Enquanto a atribuição da modalidade era incondicional, cada sessão
    /// empurrada perdia a ESPECIALIDADE da consulta: o empurrão dizia "30 sessão(ões)
    /// empurradas" e a guia nascia errada semanas depois, no dia de cada paciente.
    ///
    /// Nulo quer dizer "o chamador não sabe", nunca "desligue" (a regra da parcela 68).
    /// </summary>
    [Fact]
    public async Task Remarcar_sem_informar_os_codigos_PRESERVA_a_especialidade_da_consulta()
    {
        var pacienteId = await CriarPacienteAsync();
        var ag = await _agenda.AgendarAsync(
            pacienteId, new DateTime(2026, 7, 20, 14, 0, 0), ModalidadeAtendimento.Consulta, null,
            especialidadeConsulta: Especialidade.Psiquiatria, profissionalId: _profPadrao);

        ag.EspecialidadeConsulta.Should().Be(Especialidade.Psiquiatria, "o horário nasceu com ela");

        // Como o "Empurrar" chama: só a data nova.
        await _agenda.RemarcarAsync(ag.Id, new DateTime(2026, 7, 27, 14, 0, 0), ag.Observacoes);

        var depois = await _agenda.ObterAsync(ag.Id);
        depois!.EspecialidadeConsulta.Should().Be(
            Especialidade.Psiquiatria,
            "remarcar mexe na DATA; quem não informou a modalidade não pediu para apagar a especialidade");
    }

    /// <summary>
    /// A variante do catálogo é o NOME que a clínica vê. Trocá-la pelo nome do enum não
    /// derruba nada — só faz "Acupuntura (domiciliar)" virar "Acupuntura" em silêncio.
    /// </summary>
    [Fact]
    public async Task Remarcar_sem_informar_os_codigos_PRESERVA_a_variante_da_modalidade()
    {
        var pacienteId = await CriarPacienteAsync();
        var ag = await _agenda.AgendarAsync(
            pacienteId, new DateTime(2026, 7, 20, 14, 0, 0), ModalidadeAtendimento.AcupunturaSimples, null,
            modalidadeCodigo: "AcupunturaDomiciliar", profissionalId: _profPadrao);

        await _agenda.RemarcarAsync(ag.Id, new DateTime(2026, 7, 27, 14, 0, 0), ag.Observacoes);

        var depois = await _agenda.ObterAsync(ag.Id);
        depois!.ModalidadeCodigo.Should().Be(
            "AcupunturaDomiciliar",
            "o código do catálogo identifica a variante — perdê-lo troca o nome do que foi feito");
    }

    /// <summary>
    /// Quem INFORMA a modalidade é a autoridade sobre a especialidade que vem junto —
    /// inclusive para limpá-la. É o outro lado da regra, e sem ele trocar uma consulta por
    /// uma sessão de acupuntura deixaria a especialidade órfã pendurada no horário.
    /// </summary>
    [Fact]
    public async Task Remarcar_informando_a_modalidade_MANDA_na_especialidade()
    {
        var pacienteId = await CriarPacienteAsync();
        var ag = await _agenda.AgendarAsync(
            pacienteId, new DateTime(2026, 7, 20, 14, 0, 0), ModalidadeAtendimento.Consulta, null,
            especialidadeConsulta: Especialidade.Psiquiatria, profissionalId: _profPadrao);

        await _agenda.RemarcarAsync(
            ag.Id, new DateTime(2026, 7, 21, 14, 0, 0), null,
            modalidadeCodigo: nameof(ModalidadeAtendimento.AcupunturaSimples));

        var depois = await _agenda.ObterAsync(ag.Id);
        depois!.EspecialidadeConsulta.Should().BeNull("não é mais consulta: a especialidade não tem onde morar");
        depois.ModalidadeCodigo.Should().Be(nameof(ModalidadeAtendimento.AcupunturaSimples));
    }

    /// <summary>
    /// A etapa do kanban é DERIVADA dos carimbos de hora, não é coluna no banco. Um horário
    /// remarcado que guardasse a chegada de terça nasceria, na quinta, já em "Na recepção"
    /// — e a espera, contada da chegada até agora, apareceria em dias.
    /// </summary>
    [Fact]
    public async Task Remarcar_para_OUTRO_DIA_limpa_os_carimbos_da_fila()
    {
        var pacienteId = await CriarPacienteAsync();
        var ag = await _agenda.AgendarAsync(
            pacienteId, new DateTime(2026, 7, 20, 9, 0, 0), ModalidadeAtendimento.AcupunturaSimples, null, profissionalId: _profPadrao);

        await _agenda.RegistrarChegadaAsync(ag.Id, "teste");
        await _agenda.ChamarAsync(ag.Id, "teste");
        (await _agenda.ObterAsync(ag.Id))!.Etapa.Should().Be(EtapaFila.Chamado);

        await _agenda.RemarcarAsync(ag.Id, new DateTime(2026, 7, 23, 9, 0, 0), null);

        var depois = await _agenda.ObterAsync(ag.Id);
        depois!.ChegadaEm.Should().BeNull();
        depois.ChamadoEm.Should().BeNull();
        depois.InicioAtendimentoEm.Should().BeNull();
        depois.Etapa.Should().Be(
            EtapaFila.Aguardando,
            "na quinta ele ainda não chegou — o quadro não pode mostrá-lo esperando desde terça");
    }

    /// <summary>
    /// E o contrário: ajustar sala, duração ou observação do horário de HOJE não pode
    /// apagar o check-in de quem já está sentado no balcão.
    /// </summary>
    [Fact]
    public async Task Remarcar_no_MESMO_DIA_mantem_o_check_in_de_quem_ja_chegou()
    {
        var pacienteId = await CriarPacienteAsync();
        var ag = await _agenda.AgendarAsync(
            pacienteId, new DateTime(2026, 7, 20, 9, 0, 0), ModalidadeAtendimento.AcupunturaSimples, null, profissionalId: _profPadrao);

        await _agenda.RegistrarChegadaAsync(ag.Id, "teste");

        await _agenda.RemarcarAsync(ag.Id, new DateTime(2026, 7, 20, 10, 30, 0), null);

        var depois = await _agenda.ObterAsync(ag.Id);
        depois!.ChegadaEm.Should().NotBeNull("o paciente está no balcão; o horário é que andou");
        depois.Etapa.Should().Be(EtapaFila.Chegou);
    }

    /// <summary>
    /// Chamar alguém da lista de espera para uma CONSULTA.
    ///
    /// O formulário do balcão exige a especialidade ("Consulta precisa de especialidade") e
    /// este caminho não tinha onde recebê-la: o horário nascia sem, e a guia — que herda a
    /// especialidade do agendamento na confirmação da presença — saía sem a informação que
    /// a operadora cobra. Nenhum erro em lugar nenhum.
    /// </summary>
    [Fact]
    public async Task Chamar_da_lista_de_espera_LEVA_a_especialidade_da_consulta()
    {
        var pacienteId = await CriarPacienteAsync();
        var espera = new ListaEsperaService(_repo, _agenda);

        var pedido = await espera.AdicionarAsync(
            pacienteId, modalidadeCodigo: nameof(ModalidadeAtendimento.Consulta),
            observacoes: "quer consulta");

        var ag = await espera.ChamarAsync(
            pedido.Id, new DateTime(2026, 7, 20, 14, 0, 0), ModalidadeAtendimento.Consulta,
            profissionalId: _profPadrao,
            especialidadeConsultaCodigo: nameof(Especialidade.Psiquiatria));

        ag.EspecialidadeConsulta.Should().Be(
            Especialidade.Psiquiatria,
            "quem chamou escolheu a especialidade no formulário — ela tem de chegar ao horário");
        ag.EspecialidadeConsultaCodigo.Should().Be(nameof(Especialidade.Psiquiatria));
    }

    public void Dispose()
    {
        _db.Dispose();
        _conn.Dispose();
    }
}
