using Clinica.Application.Abstracoes;
using Clinica.Application.Servicos;
using Clinica.Domain;
using Clinica.Domain.Entities;
using Clinica.Infrastructure;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Clinica.Tests;

/// <summary>
/// O "Feliz aniversário" do painel da Recepção também é comunicação ATIVA (parcela 68).
///
/// O defeito que estes testes fixam
/// --------------------------------
/// O painel do balcão lista os aniversariantes da semana e oferece "Parabenizar", que abre
/// o WhatsApp com a mensagem da clínica. Isso não é aviso sobre uma sessão que o paciente
/// marcou (transacional, parcela 5) nem cobrança de conta vencida (transacional, parcela
/// 23): é RELACIONAMENTO, a mesma natureza do recall — e recall só sai com
/// <see cref="FinalidadeConsentimento.ComunicacaoEMarketing"/> vigente desde a parcela 5.
///
/// A tela não perguntava nada. É a TERCEIRA ocorrência do mesmo padrão: a rodada de
/// campanha tinha a regra, "Quem parou de vir" ganhou a regra na parcela 64, e o painel —
/// a tela que a recepcionista deixa aberta o dia inteiro — não tinha.
///
/// ⚠️ Por que o teste é ESTE, e não um teste do botão
/// -------------------------------------------------
/// A lição que a parcela 64 escreveu é: <b>ao achar o mesmo ato em duas telas, escreva o
/// teste que COMPARA as duas</b> — não o que prova que a que você acabou de arrumar
/// funciona. O ViewModel é WPF e não compila aqui; o que se fixa é que os três caminhos
/// perguntam a MESMA coisa e recebem a MESMA resposta, revogação incluída.
/// </summary>
public class AniversarioConsentimentoTests : IDisposable
{
    private readonly SqliteConnection _conn;
    private readonly ClinicaDbContext _db;
    private readonly ClinicaRepositorio _repo;
    private readonly RelacionamentoService _relacionamento;
    private readonly CampanhaService _campanhas;
    private readonly ConsentimentoService _consentimentos;

    private static readonly DateOnly Hoje = new(2026, 8, 10);

    public AniversarioConsentimentoTests()
    {
        _conn = new SqliteConnection("DataSource=:memory:");
        _conn.Open();
        var options = new DbContextOptionsBuilder<ClinicaDbContext>().UseSqlite(_conn).Options;
        _db = new ClinicaDbContext(options);
        _db.Database.EnsureCreated();
        _repo = new ClinicaRepositorio(_db);
        _relacionamento = new RelacionamentoService(
            _repo, new AgendaService(_repo, new AtendimentoService(_repo)));
        _campanhas = new CampanhaService(_repo);
        _consentimentos = new ConsentimentoService(_repo);
    }

    private async Task<Paciente> AniversarianteAsync(string nome, bool consentiu)
    {
        var p = new Paciente
        {
            Nome = nome,
            Telefone = "11988887777",
            Convenio = Convenio.UnimedIntercambio,
            Sexo = Sexo.Feminino,
            // Faz aniversário HOJE: a janela do painel é de seis dias a partir do dia.
            DataNascimento = new DateOnly(1980, Hoje.Month, Hoje.Day)
        };
        _db.Pacientes.Add(p);
        await _db.SaveChangesAsync();

        // Um atendimento antigo: é o que faz a rodada de recall enxergar o paciente, e o
        // teste da comparação precisa que os DOIS caminhos vejam a mesma pessoa.
        _db.Atendimentos.Add(new Atendimento
        {
            PacienteId = p.Id,
            Data = Hoje.AddDays(-200),
            Modalidade = ModalidadeAtendimento.AcupunturaSimples
        });
        await _db.SaveChangesAsync();

        if (consentiu)
            await _consentimentos.RegistrarAsync(
                p.Id, FinalidadeConsentimento.ComunicacaoEMarketing, concedido: true,
                operador: "recepcao");

        return p;
    }

    private Task<IReadOnlyList<int>> ComConsentimentoAsync(IEnumerable<int> ids) =>
        _repo.PacientesComConsentimentoVigenteAsync(
            FinalidadeConsentimento.ComunicacaoEMarketing, ids.ToList());

    // ------------------------------------------------------------------ o essencial
    [Fact]
    public async Task Quem_nao_consentiu_aparece_na_lista_e_nao_pode_ser_parabenizado()
    {
        var consentiu = await AniversarianteAsync("Maria Consentiu", consentiu: true);
        var negou = await AniversarianteAsync("Joana Nao Consentiu", consentiu: false);

        var lista = await _relacionamento.AniversariantesAsync(Hoje, 6);

        // Os DOIS aparecem: quem some da lista some da cabeça, e a tarefa que destrava o
        // telefonema — colher o consentimento no balcão — deixaria de existir.
        lista.Select(a => a.PacienteId).Should().Contain([consentiu.Id, negou.Id]);

        var podem = await ComConsentimentoAsync(lista.Select(a => a.PacienteId));
        podem.Should().Contain(consentiu.Id);
        podem.Should().NotContain(negou.Id);
    }

    // ---------------------------------------------- os três caminhos concordam
    // Se a leitura do painel deixar de responder como a da campanha responde, os lados
    // voltam a divergir — e é aqui que se descobre, não no telefone do paciente.
    [Fact]
    public async Task O_painel_e_a_campanha_perguntam_a_mesma_coisa()
    {
        var consentiu = await AniversarianteAsync("Ana Consentiu", consentiu: true);
        var negou = await AniversarianteAsync("Bia Nao Consentiu", consentiu: false);

        var aniversariantes = await _relacionamento.AniversariantesAsync(Hoje, 6);
        var pelaTelaDoPainel = (await ComConsentimentoAsync(
            aniversariantes.Select(a => a.PacienteId))).ToHashSet();

        // O que a RODADA de recall decide, pelo caminho DELA — a mesma comparação que
        // `RetencaoConsentimentoTests` faz para a tela de sumidos.
        var candidatos = await _campanhas.CandidatosRecallAsync(Hoje, diasInatividade: 0);

        candidatos.Single(c => c.PacienteId == consentiu.Id).TemConsentimento
            .Should().Be(pelaTelaDoPainel.Contains(consentiu.Id));
        candidatos.Single(c => c.PacienteId == negou.Id).TemConsentimento
            .Should().Be(pelaTelaDoPainel.Contains(negou.Id));

        pelaTelaDoPainel.Should().Contain(consentiu.Id);
        pelaTelaDoPainel.Should().NotContain(negou.Id);
    }

    // ------------------------------------------------------------------ revogação
    // "Consentiu uma vez" não é consentimento vigente: revogar tem de calar o botão.
    [Fact]
    public async Task Consentimento_revogado_tira_o_paciente_de_quem_pode_receber()
    {
        var p = await AniversarianteAsync("Carla Revogou", consentiu: true);

        (await ComConsentimentoAsync([p.Id])).Should().Contain(p.Id);

        var situacao = await _consentimentos.SituacaoAsync(p.Id);
        var vigente = situacao[FinalidadeConsentimento.ComunicacaoEMarketing];
        await _consentimentos.RevogarAsync(vigente.Id, "recepcao", "pediu para não receber");

        (await ComConsentimentoAsync([p.Id])).Should().NotContain(p.Id);
    }

    public void Dispose()
    {
        _db.Dispose();
        _conn.Dispose();
    }
}
