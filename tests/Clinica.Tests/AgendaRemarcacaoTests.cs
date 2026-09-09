using Clinica.Application.Modelos;
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

    /// <summary>
    /// CORRIGIR o que a sessão É não pode esbarrar num choque que já existia (set/2026 — a
    /// clínica trocou a especialidade de um horário e levou *"Dr. … já atende SUELLI às
    /// 14:01. Escolha outro horário ou marque como encaixe"*, sem o médico ter atendido
    /// ninguém).
    ///
    /// O cenário é o da parcela 93, e é o normal desta clínica: o horário importado ficou
    /// parado às 14h00 e a sessão foi lançada ao lado, como ENCAIXE às 14h01 — mesmo
    /// paciente, mesmo profissional. Editar a especialidade do primeiro devolve a MESMA
    /// hora, o MESMO profissional e a MESMA duração: não há sobreposição sendo criada, e a
    /// recusa era um corredor sem saída — a única forma de cumpri-la seria cancelar a
    /// sessão que aconteceu.
    /// </summary>
    [Fact]
    public async Task Corrigir_a_especialidade_nao_esbarra_no_encaixe_que_ja_existia()
    {
        var pacienteId = await CriarPacienteAsync();
        var quando = new DateTime(2026, 7, 20, 14, 0, 0);

        var importado = await _agenda.AgendarAsync(
            pacienteId, quando, ModalidadeAtendimento.Consulta, "Importado do Smart Clinic · Consulta",
            especialidadeConsulta: Especialidade.Psiquiatria, profissionalId: _profPadrao);

        // A sessão de verdade, lançada pelo balcão um minuto depois: encaixe, mesmo
        // profissional, mesmo paciente. É ela que a conferência de choque enxergava.
        await _agenda.AgendarAsync(
            pacienteId, quando.AddMinutes(1), ModalidadeAtendimento.Consulta, null,
            especialidadeConsulta: Especialidade.Psiquiatria, profissionalId: _profPadrao, encaixe: true);

        // O mesmo caminho do botão Editar da agenda do dia: hora igual, recursos iguais,
        // só a especialidade muda.
        var corrigir = async () => await _agenda.RemarcarAsync(
            importado.Id, quando, importado.Observacoes,
            modalidadeCodigo: nameof(ModalidadeAtendimento.Consulta),
            especialidadeConsultaCodigo: nameof(Especialidade.Geriatria),
            profissionalId: _profPadrao, salaId: null, duracaoMinutos: null,
            manterRecursos: false, encaixe: false);

        await corrigir.Should().NotThrowAsync(
            "a sobreposição já existia, e a edição não move o horário nem troca o recurso");

        var depois = await _agenda.ObterAsync(importado.Id);
        depois!.EspecialidadeConsultaCodigo.Should().Be(nameof(Especialidade.Geriatria));
        depois.EspecialidadeConsulta.Should().Be(Especialidade.Geriatria);
    }

    /// <summary>
    /// A outra metade: MOVER o horário para cima de outro continua recusando. A conferência
    /// existe para impedir que se crie a sobreposição, e é isso que não pode cair junto.
    /// </summary>
    [Fact]
    /// <summary>
    /// Mover para cima de outro MOVE (set/2026 — nenhum choque recusa; ver
    /// <c>AgendaService.ConflitosAsync</c>). Substituiu o
    /// <c>Mover_o_horario_para_cima_de_outro_continua_recusando</c>.
    ///
    /// O aviso continua sendo produzido: é a tela de remarcação que o mostra, criticado a
    /// cada tecla, e desde que a recusa saiu ele é a barreira inteira.
    /// </summary>
    public async Task Mover_o_horario_para_cima_de_outro_MOVE_e_avisa()
    {
        var pacienteId = await CriarPacienteAsync();
        var outro = await CriarPacienteAsync();
        var destino = new DateTime(2026, 7, 20, 16, 0, 0);

        var meu = await _agenda.AgendarAsync(
            pacienteId, new DateTime(2026, 7, 20, 14, 0, 0), ModalidadeAtendimento.AcupunturaSimples,
            null, profissionalId: _profPadrao);
        await _agenda.AgendarAsync(
            outro, destino, ModalidadeAtendimento.AcupunturaSimples,
            null, profissionalId: _profPadrao);

        await _agenda.RemarcarAsync(
            meu.Id, destino, null,
            profissionalId: _profPadrao, salaId: null, duracaoMinutos: null,
            manterRecursos: false, encaixe: false);

        (await _agenda.ObterAsync(meu.Id))!.DataHora.Should().Be(destino);

        (await _agenda.ConflitosAsync(destino, profissionalId: _profPadrao, ignorarAgendamentoId: meu.Id))
            .Should().Contain(c => c.Recurso == RecursoAgenda.Profissional);
    }

    /// <summary>
    /// E REABRIR um cancelado confere de novo: o horário tinha soltado o recurso, e o vão
    /// dele pode ter sido dado a outra pessoa nesse meio-tempo.
    /// </summary>
    [Fact]
    /// <summary>
    /// Reabrir um cancelado cujo vão já foi dado a outra pessoa REABRE — os dois ficam no
    /// mesmo horário (set/2026). Substituiu o <c>Reabrir_um_cancelado_ainda_confere_o_choque</c>.
    ///
    /// Era o caso que a conferência condicional protegia com mais razão: aqui o horário
    /// tinha SOLTADO o recurso. Com a decisão de set/2026 nem esse recusa — e é coerente
    /// com o pedido da clínica, que é justamente ter dois pacientes no mesmo horário. Quem
    /// diz que há alguém ali continua sendo o aviso da tela.
    /// </summary>
    public async Task Reabrir_um_cancelado_cujo_vao_foi_dado_a_outro_REABRE_e_avisa()
    {
        var pacienteId = await CriarPacienteAsync();
        var outro = await CriarPacienteAsync();
        var quando = new DateTime(2026, 7, 20, 14, 0, 0);

        var meu = await _agenda.AgendarAsync(
            pacienteId, quando, ModalidadeAtendimento.AcupunturaSimples, null,
            profissionalId: _profPadrao);
        await _agenda.CancelarAsync(meu.Id, "teste");

        // O vão vagou e foi dado a outra pessoa.
        await _agenda.AgendarAsync(
            outro, quando, ModalidadeAtendimento.AcupunturaSimples, null,
            profissionalId: _profPadrao);

        await _agenda.RemarcarAsync(
            meu.Id, quando, null,
            profissionalId: _profPadrao, salaId: null, duracaoMinutos: null,
            manterRecursos: false, encaixe: false);

        var reaberto = (await _agenda.ObterAsync(meu.Id))!;
        reaberto.Status.Should().Be(StatusAgendamento.Agendado);

        (await _agenda.ConflitosAsync(quando, profissionalId: _profPadrao, ignorarAgendamentoId: meu.Id))
            .Should().Contain(c => c.Recurso == RecursoAgenda.Profissional);
    }

    public void Dispose()
    {
        _db.Dispose();
        _conn.Dispose();
    }
}
