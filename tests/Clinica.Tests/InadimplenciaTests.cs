using Clinica.Application.Servicos;
using Clinica.Domain;
using Clinica.Domain.Entities;
using Clinica.Infrastructure;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Clinica.Tests;

/// <summary>
/// Inadimplência por paciente (parcela 23).
///
/// A parcela 12 deu ao sistema o vencimento, e com ele "o que vence esta semana". Ficou
/// faltando a pergunta que vem depois, e que é a que a clínica de fato faz: **quem me
/// deve?** A conta vencida existia, mas dissolvida na lista de contas — uma linha por
/// lançamento, misturada com o que a clínica tem a PAGAR. Para saber que o mesmo paciente
/// tinha três sessões em aberto era preciso ler a lista inteira e somar de cabeça.
///
/// O que estes testes protegem:
/// 1. **Só conta de PACIENTE entra.** Entrada prevista vencida sem paciente é a receber,
///    não inadimplência — juntar as duas faria a clínica cobrar quem não deve.
/// 2. **Agrupa por paciente e SOMA.** É a coisa que a lista de contas não fazia.
/// 3. **A ordem padrão é o mais ANTIGO**, não o maior valor: a chance de receber cai com
///    o tempo.
/// 4. **Todas as faixas aparecem, mesmo vazias.** Um aging sem a faixa de 90+ dias se lê
///    como "não há dívida velha".
/// 5. **Receber tira da lista** — e passa pelo `FinanceiroService`, que grava a auditoria.
/// </summary>
public class InadimplenciaTests : IDisposable
{
    private readonly SqliteConnection _conn;
    private readonly ClinicaDbContext _db;
    private readonly ClinicaRepositorio _repo;
    private readonly ContasService _contas;
    private readonly InadimplenciaService _inadimplencia;

    private static readonly DateOnly Hoje = new(2026, 9, 15);

    public InadimplenciaTests()
    {
        _conn = new SqliteConnection("DataSource=:memory:");
        _conn.Open();
        var options = new DbContextOptionsBuilder<ClinicaDbContext>().UseSqlite(_conn).Options;
        _db = new ClinicaDbContext(options);
        _db.Database.EnsureCreated();
        _repo = new ClinicaRepositorio(_db);
        _contas = new ContasService(_repo);
        _inadimplencia = new InadimplenciaService(_repo, new FinanceiroService(_repo));
    }

    private async Task<Paciente> PacienteAsync(string nome, string? telefone = "11988887777")
    {
        var p = new Paciente { Nome = nome, Telefone = telefone };
        await _repo.AdicionarPacienteAsync(p);
        await _repo.SalvarAsync();
        return p;
    }

    private Task<LancamentoFinanceiro> AReceberAsync(
        int? pacienteId, decimal valor, DateOnly vencimento, string descricao = "Sessão")
        => _contas.LancarContaAsync(
            TipoLancamento.Entrada, descricao, valor, vencimento,
            pacienteId: pacienteId, operador: "ana");

    // ---------------- O que entra ----------------

    [Fact]
    public async Task SoEntraContaVENCIDADePACIENTE()
    {
        var maria = await PacienteAsync("Maria Souza");

        await AReceberAsync(maria.Id, 300m, Hoje.AddDays(-10));      // vencida: entra
        await AReceberAsync(maria.Id, 500m, Hoje.AddDays(10));       // a vencer: não é atraso
        await AReceberAsync(null, 900m, Hoje.AddDays(-20));          // sem paciente: é a receber
        // Saída vencida é conta A PAGAR: nada a ver com inadimplência de paciente.
        await _contas.LancarContaAsync(
            TipoLancamento.Saida, "Aluguel", 3_200m, Hoje.AddDays(-5), operador: "ana");

        var devedores = await _inadimplencia.PorPacienteAsync(Hoje);

        devedores.Should().ContainSingle();
        devedores[0].Nome.Should().Be("Maria Souza");
        devedores[0].Total.Should().Be(300m);
        devedores[0].Contas.Should().Be(1);
    }

    [Fact]
    public async Task ContaJaRECEBIDA_SaiDaLista()
    {
        var maria = await PacienteAsync("Maria");
        var conta = await AReceberAsync(maria.Id, 300m, Hoje.AddDays(-10));

        await _inadimplencia.ReceberAsync(conta.Id, Hoje, FormaPagamento.Pix, "ana");

        (await _inadimplencia.PorPacienteAsync(Hoje)).Should().BeEmpty();
    }

    [Fact]
    public async Task Receber_GravaAuditoria()
    {
        var maria = await PacienteAsync("Maria");
        var conta = await AReceberAsync(maria.Id, 300m, Hoje.AddDays(-10));

        await _inadimplencia.ReceberAsync(conta.Id, Hoje, FormaPagamento.Dinheiro, "ana");

        // Recebimento é dinheiro que se move: quem gravou foi o FinanceiroService, e é por
        // isso que a auditoria vem de graça. Duplicar a gravação aqui daria dois caminhos
        // para o mesmo fato, e só um receberia a próxima correção.
        var trilha = await new AuditoriaService(_repo).ConsultarAsync(
            new Application.Modelos.FiltroAuditoria { Acao = "LancamentoRealizado" });
        trilha.Should().ContainSingle();
        trilha[0].Operador.Should().Be("ana");
    }

    // ---------------- O agrupamento ----------------

    [Fact]
    public async Task AgrupaPorPacienteESoma()
    {
        var maria = await PacienteAsync("Maria");

        await AReceberAsync(maria.Id, 150m, Hoje.AddDays(-40), "Sessão de agosto");
        await AReceberAsync(maria.Id, 150m, Hoje.AddDays(-10), "Sessão de setembro");
        await AReceberAsync(maria.Id, 200m, Hoje.AddDays(-70), "Sessão de julho");

        var devedores = await _inadimplencia.PorPacienteAsync(Hoje);

        var maria2 = devedores.Should().ContainSingle().Subject;
        maria2.Total.Should().Be(500m);
        maria2.Contas.Should().Be(3);
        maria2.DiasMaiorAtraso.Should().Be(70);
        maria2.VencimentoMaisAntigo.Should().Be(Hoje.AddDays(-70));
        // Dentro do paciente, a mais antiga primeiro: é a que explica a conversa
        // ("desde julho").
        maria2.Detalhe[0].Descricao.Should().Be("Sessão de julho");
    }

    [Fact]
    public async Task Telefone_VemJuntoDaLinha()
    {
        await PacienteAsync("Maria", "11988887777");
        var maria = (await _repo.BuscarPacientesAsync("Maria", null)).Single();
        await AReceberAsync(maria.Id, 300m, Hoje.AddDays(-5));

        var devedores = await _inadimplencia.PorPacienteAsync(Hoje);

        // A próxima ação depois de ler a linha é ligar. Buscar o contato numa segunda tela
        // é o que faz a cobrança não acontecer.
        devedores[0].Telefone.Should().Be("11988887777");
    }

    // ---------------- A ordem ----------------

    [Fact]
    public async Task OrdemPadrao_EOMaisANTIGO()
    {
        var joao = await PacienteAsync("João");
        var ana = await PacienteAsync("Ana");

        await AReceberAsync(joao.Id, 100m, Hoje.AddDays(-120));  // pouco, muito antigo
        await AReceberAsync(ana.Id, 2_000m, Hoje.AddDays(-3));   // muito, recente

        var devedores = await _inadimplencia.PorPacienteAsync(Hoje);

        // A chance de receber cai com o tempo, e o caso de quatro meses é o que precisa de
        // DECISÃO — acordo, ou parar de contar com o dinheiro —, não de mais um lembrete.
        devedores[0].Nome.Should().Be("João");
    }

    [Fact]
    public async Task OrdemPorValor_QuandoPedida()
    {
        var joao = await PacienteAsync("João");
        var ana = await PacienteAsync("Ana");

        await AReceberAsync(joao.Id, 100m, Hoje.AddDays(-120));
        await AReceberAsync(ana.Id, 2_000m, Hoje.AddDays(-3));

        var devedores = await _inadimplencia.PorPacienteAsync(Hoje, OrdemInadimplencia.MaiorValor);

        devedores[0].Nome.Should().Be("Ana");
    }

    // ---------------- O resumo ----------------

    [Fact]
    public async Task Resumo_SomaEEnvelheceCorretamente()
    {
        var a = await PacienteAsync("A");
        var b = await PacienteAsync("B");
        var c = await PacienteAsync("C");

        await AReceberAsync(a.Id, 100m, Hoje.AddDays(-5));    // 1 a 30
        await AReceberAsync(b.Id, 200m, Hoje.AddDays(-45));   // 31 a 60
        await AReceberAsync(c.Id, 700m, Hoje.AddDays(-200));  // mais de 90

        var r = await _inadimplencia.ResumoAsync(Hoje);

        r.Total.Should().Be(1_000m);
        r.Pacientes.Should().Be(3);
        r.Contas.Should().Be(3);
        r.MedioPorPaciente.Should().BeApproximately(333.33m, 0.01m);

        // Todas as faixas aparecem, mesmo vazias: um aging em que a de 61-90 simplesmente
        // não existe se lê como "não há dívida nessa idade".
        r.Faixas.Should().HaveCount(4);
        r.Faixas.Select(f => f.Faixa).Should().Equal(FaixasInadimplencia.Todas);

        var velha = r.Faixas.Single(f => f.Faixa == FaixasInadimplencia.Acima90);
        velha.Valor.Should().Be(700m);
        velha.Fracao.Should().BeApproximately(0.7d, 0.0001d);

        r.Faixas.Single(f => f.Faixa == FaixasInadimplencia.De61A90).Valor.Should().Be(0m);
    }

    [Fact]
    public async Task Resumo_SemDivida_NaoDividePorZero()
    {
        var r = await _inadimplencia.ResumoAsync(Hoje);

        // "R$ 0,00 por paciente" se lê como "ninguém deve quase nada"; o correto é não
        // haver média nenhuma.
        r.Vazio.Should().BeTrue();
        r.MedioPorPaciente.Should().BeNull();
        r.Faixas.Should().BeEmpty();
        await Task.CompletedTask;
    }

    [Fact]
    public async Task Faixa_ODiaExatoDaBordaFicaNaFaixaDeBaixo()
    {
        var a = await PacienteAsync("A");
        await AReceberAsync(a.Id, 100m, Hoje.AddDays(-30));

        var devedores = await _inadimplencia.PorPacienteAsync(Hoje);

        // 30 dias é "1 a 30", não "31 a 60": a faixa que o rótulo promete é a que o
        // cálculo tem de entregar.
        devedores[0].DiasMaiorAtraso.Should().Be(30);
        devedores[0].Faixa.Should().Be(FaixasInadimplencia.Ate30);
    }

    // ---------------- A dívida de um paciente ----------------

    [Fact]
    public async Task DoPaciente_TrazSoADeleENullQuandoEstaEmDia()
    {
        var maria = await PacienteAsync("Maria");
        var joao = await PacienteAsync("João");
        await AReceberAsync(maria.Id, 300m, Hoje.AddDays(-10));

        (await _inadimplencia.DoPacienteAsync(maria.Id, Hoje))!.Total.Should().Be(300m);
        // Em dia devolve null, não um devedor de zero: são estados diferentes.
        (await _inadimplencia.DoPacienteAsync(joao.Id, Hoje)).Should().BeNull();
    }

    // ---------------- A mensagem ----------------

    [Fact]
    public async Task Mensagem_UmaContaEVariasContas_SaemDiferentes()
    {
        var maria = await PacienteAsync("Maria Souza");
        await AReceberAsync(maria.Id, 300m, Hoje.AddDays(-10));

        var uma = InadimplenciaService.MensagemDeCobranca(
            (await _inadimplencia.PorPacienteAsync(Hoje))[0], "Clínica SemDor");

        uma.Should().StartWith("Olá, Maria!");
        uma.Should().Contain("R$ 300,00");
        uma.Should().Contain("Clínica SemDor");
        // Lembrete, não ameaça: quem está em atraso na clínica quase sempre esqueceu, e um
        // texto duro custa o paciente inteiro para recuperar uma sessão.
        uma.Should().Contain("Se já tiver sido pago");

        await AReceberAsync(maria.Id, 200m, Hoje.AddDays(-40));
        var varias = InadimplenciaService.MensagemDeCobranca(
            (await _inadimplencia.PorPacienteAsync(Hoje))[0]);

        varias.Should().Contain("2 valores em aberto");
        varias.Should().Contain("R$ 500,00");
    }

    [Fact]
    public async Task Mensagem_NaoLevaDadoClinico()
    {
        var maria = await PacienteAsync("Maria");
        await AReceberAsync(maria.Id, 300m, Hoje.AddDays(-10), "Acupuntura — lombalgia crônica");

        var msg = InadimplenciaService.MensagemDeCobranca(
            (await _inadimplencia.PorPacienteAsync(Hoje))[0]);

        // A descrição do lançamento pode conter o motivo do atendimento, e mensagem de
        // WhatsApp não é lugar de dado clínico: o telefone pode não ser só do paciente.
        msg.Should().NotContain("lombalgia");
    }

    public void Dispose()
    {
        _db.Dispose();
        _conn.Dispose();
        GC.SuppressFinalize(this);
    }
}
