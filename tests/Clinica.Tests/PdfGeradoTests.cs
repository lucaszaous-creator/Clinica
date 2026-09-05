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
/// Os PDFs saem — inclusive nos casos em que não há o que imprimir.
///
/// Eram os três únicos serviços da aplicação sem uma linha de teste, e não por acaso:
/// gerar PDF parece difícil de testar, então não se testa. Mas é a categoria em que o
/// compilador menos ajuda — QuestPDF monta o documento em tempo de EXECUÇÃO, e um
/// `Rotulo` nulo, uma coluna a mais que a largura da página ou uma lista vazia estouram
/// só na hora em que alguém clica em imprimir. Que é 7h da manhã, com a folha do dia.
///
/// O que se afere aqui não é o desenho — é que **o documento se monta**. Os casos são os
/// que a clínica produz de verdade e o desenvolvedor não experimenta: o dia sem ninguém
/// marcado, o agendamento sem profissional e sem sala, o cancelado no meio da lista.
/// </summary>
public class PdfGeradoTests : IDisposable
{
    private readonly SqliteConnection _conn;
    private readonly ClinicaDbContext _db;
    private readonly ClinicaRepositorio _repo;
    private readonly AgendaService _agenda;
    private readonly AgendaPdfService _pdf;

    private static readonly DateOnly Dia = new(2026, 8, 12);


    /// <summary>
    /// Todo horário MARCADO precisa de dono desde a parcela 95 — o fixture cria um para os
    /// cenários que não se importam com QUEM atende.
    /// </summary>
    private readonly int _profPadrao;

    public PdfGeradoTests()
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
        _pdf = new AgendaPdfService(_repo, _agenda);
    }

    private async Task<int> CriarPacienteAsync(string nome = "Maria Souza")
    {
        var p = new Paciente { Nome = nome, Convenio = Convenio.UnimedIntercambio, Sexo = Sexo.Feminino };
        _db.Pacientes.Add(p);
        await _db.SaveChangesAsync();
        return p.Id;
    }

    /// <summary>Um PDF de verdade começa com "%PDF" — é a assinatura do formato.</summary>
    private static void DeveSerPdf(byte[] bytes)
    {
        bytes.Should().NotBeNullOrEmpty();
        System.Text.Encoding.ASCII.GetString(bytes, 0, 4).Should().Be("%PDF");
    }

    /// <summary>
    /// O dia sem ninguém marcado é o caso que mais se esquece de testar e o que mais
    /// acontece: segunda de feriado, semana de férias. A folha tem de sair mesmo assim —
    /// uma exceção aqui faria a recepção achar que o sistema quebrou.
    /// </summary>
    [Fact]
    public async Task Folha_do_dia_sai_mesmo_sem_ninguem_marcado()
        => DeveSerPdf(await _pdf.AgendaDoDiaAsync(Dia));

    [Fact]
    public async Task Folha_do_dia_sai_com_agendamentos()
    {
        var pacienteId = await CriarPacienteAsync();
        await _agenda.AgendarAsync(pacienteId, Dia.ToDateTime(new TimeOnly(9, 0)),
            ModalidadeAtendimento.AcupunturaSimples, observacoes: null, profissionalId: _profPadrao);
        await _agenda.AgendarAsync(pacienteId, Dia.ToDateTime(new TimeOnly(10, 0)),
            ModalidadeAtendimento.AcupunturaComEletro, observacoes: null, profissionalId: _profPadrao);

        DeveSerPdf(await _pdf.AgendaDoDiaAsync(Dia));
    }

    /// <summary>
    /// Sem profissional e sem sala — que é como o faturamento marca, e como a recepção
    /// marca quando ainda não decidiu quem atende. Os dois campos viram nulo no desenho,
    /// e é aí que um `.Rotulo` desprotegido derruba a folha.
    /// </summary>
    [Fact]
    public async Task Folha_do_dia_aguenta_agendamento_sem_profissional_nem_sala()
    {
        var pacienteId = await CriarPacienteAsync();
        // Desde a parcela 95 o horário sem dono só nasce como ENCAIXE (marcar exige quem
        // atende) — e a folha continua tendo de aguentá-lo: os encaixes do dia e os
        // horários antigos, importados sem profissional, saem impressos igual.
        await _agenda.AgendarAsync(pacienteId, Dia.ToDateTime(new TimeOnly(9, 0)),
            ModalidadeAtendimento.AcupunturaSimples, observacoes: null, salaId: null,
            profissionalId: null, encaixe: true);

        DeveSerPdf(await _pdf.AgendaDoDiaAsync(Dia));
    }

    /// <summary>
    /// Cancelado e falta entram MARCADOS, nunca sumindo — quem lê a folha às 14h precisa
    /// saber que o horário das 15h vagou. O teste garante que a linha marcada também
    /// desenha (é um caminho diferente no layout).
    /// </summary>
    [Fact]
    public async Task Folha_do_dia_desenha_cancelado_e_falta()
    {
        var pacienteId = await CriarPacienteAsync();

        var cancelado = await _agenda.AgendarAsync(pacienteId,
            Dia.ToDateTime(new TimeOnly(9, 0)), ModalidadeAtendimento.AcupunturaSimples, observacoes: null, profissionalId: _profPadrao);
        await _agenda.CancelarAsync(cancelado.Id, "paciente avisou");

        var faltou = await _agenda.AgendarAsync(pacienteId,
            Dia.ToDateTime(new TimeOnly(11, 0)), ModalidadeAtendimento.AcupunturaSimples, observacoes: null, profissionalId: _profPadrao);
        await _agenda.MarcarFaltaAsync(faltou.Id);

        DeveSerPdf(await _pdf.AgendaDoDiaAsync(Dia));
    }

    /// <summary>Filtrada por profissional: o título muda, e o caminho do título é outro.</summary>
    [Fact]
    public async Task Folha_de_um_profissional_sai_com_o_nome_dele()
    {
        var prof = new Profissional { Nome = "Ana Souza" };
        _db.Profissionais.Add(prof);
        await _db.SaveChangesAsync();

        var pacienteId = await CriarPacienteAsync();
        await _agenda.AgendarAsync(pacienteId, Dia.ToDateTime(new TimeOnly(9, 0)),
            ModalidadeAtendimento.AcupunturaSimples, observacoes: null, profissionalId: prof.Id);

        DeveSerPdf(await _pdf.AgendaDoDiaAsync(Dia, profissionalId: prof.Id));
    }

    [Fact]
    public async Task Comprovante_sai_para_o_paciente()
    {
        var pacienteId = await CriarPacienteAsync();
        var ag = await _agenda.AgendarAsync(pacienteId, Dia.ToDateTime(new TimeOnly(14, 30)),
            ModalidadeAtendimento.AcupunturaComEletro, observacoes: null, profissionalId: _profPadrao);

        DeveSerPdf(await _pdf.ComprovanteAsync(ag.Id));
    }

    /// <summary>
    /// Com os dados do prestador preenchidos o cabeçalho desenha outro bloco — e é o
    /// caminho que a clínica de verdade usa, porque ela cadastra o prestador.
    /// </summary>
    [Fact]
    public async Task Comprovante_sai_com_cabecalho_do_prestador()
    {
        var pacienteId = await CriarPacienteAsync();
        var ag = await _agenda.AgendarAsync(pacienteId, Dia.ToDateTime(new TimeOnly(14, 30)),
            ModalidadeAtendimento.AcupunturaSimples, observacoes: null, profissionalId: _profPadrao);

        var prestador = new DadosPrestador
        {
            RazaoSocial = "Clínica de Acupuntura",
            Telefone = "(11) 3333-4444"
        };

        DeveSerPdf(await _pdf.ComprovanteAsync(ag.Id, prestador));
    }

    /// <summary>
    /// Comprovante de agendamento que não existe: a falha precisa ser a mensagem do
    /// serviço, não uma NullReferenceException lá dentro do desenho.
    /// </summary>
    [Fact]
    public async Task Comprovante_de_agendamento_inexistente_falha_com_mensagem()
    {
        var acao = () => _pdf.ComprovanteAsync(9999);

        await acao.Should().ThrowAsync<InvalidOperationException>();
    }

    public void Dispose()
    {
        _db.Dispose();
        _conn.Dispose();
        GC.SuppressFinalize(this);
    }
}
