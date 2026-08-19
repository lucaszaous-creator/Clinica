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
/// Chamar de volta quem parou de vir é RECALL, e recall exige consentimento (parcela 64).
///
/// O defeito que estes testes fixam
/// --------------------------------
/// A tela "Quem parou de vir" do Gerente (<c>RetencaoViewModel</c>) abre o WhatsApp com um
/// convite para retomar as sessões. É a MESMA mensagem que a rodada de recall manda — e a
/// rodada, desde a parcela 5, recusa quem não tem <see cref="FinalidadeConsentimento.
/// ComunicacaoEMarketing"/> vigente e ainda CONTA quantos ficaram de fora. A tela não
/// perguntava nada: a lista vinha do <see cref="RetencaoPacienteService"/>, que responde
/// "quem sumiu" e não tem por que saber de consentimento.
///
/// Era o mesmo ato com duas regras, e a de baixo não tinha regra nenhuma — a variante mais
/// cara desse defeito, porque a metade sem regra é a que ninguém lembra de conferir e aqui
/// ela é justamente a garantia que a cliente audita.
///
/// Por que o teste é ESTE
/// ----------------------
/// A correção mora no ViewModel (WPF, `net8.0-windows`), que não compila neste projeto de
/// teste. O que dá para fixar — e o que importa — é o CONTRATO que a tela passou a usar:
/// <see cref="IClinicaRepositorio.PacientesComConsentimentoVigenteAsync"/>, a mesma leitura
/// em lote da campanha. Se ela deixar de responder como a rodada responde, os dois lados
/// voltam a divergir e é aqui que se descobre.
///
/// ⚠️ Quem NÃO consentiu continua NA LISTA de sumidos, e isso é decisão: quem some da lista
/// some da cabeça, e "12 pacientes" que na verdade eram 30 faria a clínica concluir que
/// quase ninguém está sumindo. O que muda é o botão de chamar, que fica apagado dizendo o
/// motivo — a instrução é colher o consentimento no balcão.
/// </summary>
public class RetencaoConsentimentoTests : IDisposable
{
    private readonly SqliteConnection _conn;
    private readonly ClinicaDbContext _db;
    private readonly ClinicaRepositorio _repo;
    private readonly RetencaoPacienteService _retencao;
    private readonly CampanhaService _campanhas;
    private readonly ConsentimentoService _consentimentos;

    private static readonly DateOnly Hoje = new(2026, 8, 10);

    public RetencaoConsentimentoTests()
    {
        _conn = new SqliteConnection("DataSource=:memory:");
        _conn.Open();
        var options = new DbContextOptionsBuilder<ClinicaDbContext>().UseSqlite(_conn).Options;
        _db = new ClinicaDbContext(options);
        _db.Database.EnsureCreated();
        _repo = new ClinicaRepositorio(_db);
        _retencao = new RetencaoPacienteService(_repo, new PacoteService(_repo));
        _campanhas = new CampanhaService(_repo);
        _consentimentos = new ConsentimentoService(_repo);
    }

    private async Task<Paciente> SumidoAsync(string nome, int diasSemVir, bool consentiu)
    {
        var p = new Paciente
        {
            Nome = nome,
            Telefone = "11988887777",
            Convenio = Convenio.UnimedIntercambio,
            Sexo = Sexo.Feminino
        };
        _db.Pacientes.Add(p);
        await _db.SaveChangesAsync();

        _db.Atendimentos.Add(new Atendimento
        {
            PacienteId = p.Id,
            Data = Hoje.AddDays(-diasSemVir),
                RealizadoEm = Hoje.AddDays(-diasSemVir).ToDateTime(TimeOnly.MinValue),
            Modalidade = ModalidadeAtendimento.AcupunturaSimples
        });
        await _db.SaveChangesAsync();

        if (consentiu)
            await _consentimentos.RegistrarAsync(
                p.Id, FinalidadeConsentimento.ComunicacaoEMarketing, concedido: true);

        return p;
    }

    [Fact]
    public async Task Sumido_sem_consentimento_continua_na_lista()
    {
        // A lista responde "quem sumiu" — e some-se da lista some-se da cabeça. Filtrar
        // aqui esconderia da direção justamente o paciente cujo consentimento falta
        // colher, que é a tarefa que destrava o telefonema.
        await SumidoAsync("Sem consentimento", diasSemVir: 120, consentiu: false);

        var sumidos = await _retencao.SumidosAsync(Hoje);

        sumidos.Should().ContainSingle()
            .Which.Nome.Should().Be("Sem consentimento");
    }

    [Fact]
    public async Task Consentimento_da_lista_responde_igual_ao_da_rodada_de_recall()
    {
        // A amarra da parcela: os dois lados perguntam a MESMA coisa e têm de receber a
        // MESMA resposta. Se alguém mudar a regra de um lado só, este teste cai.
        var com = await SumidoAsync("Consentiu", diasSemVir: 120, consentiu: true);
        var sem = await SumidoAsync("Nao consentiu", diasSemVir: 120, consentiu: false);

        // O que a TELA passou a perguntar (leitura em lote, como a campanha).
        var permitidosNaTela = await _repo.PacientesComConsentimentoVigenteAsync(
            FinalidadeConsentimento.ComunicacaoEMarketing, [com.Id, sem.Id]);

        // O que a RODADA de recall decide, pelo caminho dela.
        var candidatos = await _campanhas.CandidatosRecallAsync(Hoje, diasInatividade: 60);

        permitidosNaTela.Should().BeEquivalentTo([com.Id]);
        candidatos.Single(c => c.PacienteId == com.Id).TemConsentimento.Should().BeTrue();
        candidatos.Single(c => c.PacienteId == sem.Id).TemConsentimento.Should().BeFalse();
    }

    [Fact]
    public async Task Consentimento_revogado_tira_o_direito_de_chamar()
    {
        // Revogar é o caso que mais importa: o paciente consentiu um dia e mudou de ideia.
        // Vale o registro MAIS RECENTE, e a tela precisa enxergar a revogação — senão o
        // sistema continuaria chamando exatamente quem pediu para não ser chamado.
        var p = await SumidoAsync("Revogou depois", diasSemVir: 120, consentiu: true);

        await _consentimentos.RegistrarAsync(
            p.Id, FinalidadeConsentimento.ComunicacaoEMarketing, concedido: false);

        var permitidos = await _repo.PacientesComConsentimentoVigenteAsync(
            FinalidadeConsentimento.ComunicacaoEMarketing, [p.Id]);

        permitidos.Should().BeEmpty();

        // E ele continua na lista de sumidos: o que ele perdeu foi a mensagem, não a
        // visibilidade para a direção.
        (await _retencao.SumidosAsync(Hoje)).Should().ContainSingle();
    }

    public void Dispose()
    {
        _db.Dispose();
        _conn.Dispose();
        GC.SuppressFinalize(this);
    }
}
