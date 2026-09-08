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
/// A IDADE IMPOSSÍVEL — "1851 anos" no crachá da tela de atendimento (set/2026).
///
/// A direção fotografou a tela e o número estava lá. A conta estava CERTA; errada estava
/// a DATA, vinda da importação do Smart Clinic ou de um dedo no teclado — e o cabeçalho
/// imprimia o que a subtração devolvia, sem perguntar se aquilo podia ser uma pessoa viva.
///
/// ⚠️ O que a varredura achou é maior que o crachá: a conta existia em OITO cópias, todas
/// escritas à mão, todas imprimindo o mesmo absurdo — o crachá, as duas fichas (balcão e
/// faturamento), a busca do Novo atendimento, o contexto da Enfermagem, a lista do
/// faturamento, a capa do paciente (morta) e o <b>PDF que o paciente leva embora</b>. A
/// oitava, o aniversário, já tinha percebido o defeito e o corrigira com um limiar
/// PRÓPRIO (<c>Year &gt; 1900</c>) — que é exatamente como duas definições da mesma regra
/// divergem, e por que as outras sete continuaram erradas.
///
/// A regra é a da MEDIDA CLÍNICA: recusa-se o implausível, nunca o anormal. O teto é 130
/// porque o recorde humano verificado é 122 — um teto apertado apagaria a idade da
/// paciente de 103 anos, que é justamente aquela em que a idade MUDA a conduta.
///
/// E AUSENTE não é IMPLAUSÍVEL: ficha sem data é o caso normal da clínica e a linha só
/// pula a idade; data errada é defeito de CADASTRO, e some-la em silêncio a deixaria
/// errada para sempre. Quem a conserta é o balcão, e ele precisa vê-la.
/// </summary>
public class IdadeImplausivelTests : IDisposable
{
    private static readonly DateOnly Hoje = new(2026, 9, 8);

    private readonly SqliteConnection _conn;
    private readonly ClinicaDbContext _db;
    private readonly ConsultorioService _consultorio;

    public IdadeImplausivelTests()
    {
        _conn = new SqliteConnection("DataSource=:memory:");
        _conn.Open();
        var options = new DbContextOptionsBuilder<ClinicaDbContext>().UseSqlite(_conn).Options;
        _db = new ClinicaDbContext(options);
        _db.Database.EnsureCreated();
        _consultorio = new ConsultorioService(new ClinicaRepositorio(_db));
    }

    // ===================== A REGRA, no Domínio =====================

    [Fact]
    public void A_data_da_foto_nao_produz_idade()
    {
        // 1851: é o caso REAL que a direção fotografou.
        var nascimento = new DateOnly(1851, 3, 12);

        IdadeDoPaciente.Anos(nascimento, Hoje).Should().BeNull();
        IdadeDoPaciente.Implausivel(nascimento, Hoje).Should().BeTrue();
        IdadeDoPaciente.Texto(nascimento, Hoje).Should().Be("data a conferir");
    }

    [Fact]
    public void Ficha_SEM_data_nao_e_erro_de_ninguem()
    {
        // Criança, paciente cadastrado pela carteirinha, quem chegou sem documento: o
        // caso NORMAL. Nada a dizer, e nada a consertar.
        IdadeDoPaciente.Anos(null, Hoje).Should().BeNull();
        IdadeDoPaciente.Implausivel(null, Hoje).Should().BeFalse();
        IdadeDoPaciente.Texto(null, Hoje).Should().BeNull();
    }

    [Fact]
    public void A_paciente_de_103_anos_continua_com_a_idade_na_tela()
    {
        // A asserção que impede a correção de virar o defeito oposto. Um teto apertado
        // apagaria justamente a idade que mais muda conduta.
        var nascimento = new DateOnly(1923, 1, 1);

        IdadeDoPaciente.Anos(nascimento, Hoje).Should().Be(103);
        IdadeDoPaciente.Implausivel(nascimento, Hoje).Should().BeFalse();
    }

    [Theory]
    [InlineData(130, true)]   // no teto: ainda é gente
    [InlineData(131, false)]  // um ano acima: só pode ser a data errada
    public void O_teto_e_130_anos(int anos, bool plausivel)
    {
        var nascimento = Hoje.AddYears(-anos);

        IdadeDoPaciente.Implausivel(nascimento, Hoje).Should().Be(!plausivel);
    }

    [Fact]
    public void Data_FUTURA_e_implausivel()
    {
        // Digitar 2062 no lugar de 2026 dá um "-36 anos" que nenhuma tela sabe desenhar.
        var nascimento = Hoje.AddYears(36);

        IdadeDoPaciente.Anos(nascimento, Hoje).Should().BeNull();
        IdadeDoPaciente.Implausivel(nascimento, Hoje).Should().BeTrue();
    }

    [Fact]
    public void A_conta_e_pelo_ANIVERSARIO_nao_pela_subtracao_dos_anos()
    {
        // Faz 40 amanhã: hoje ainda tem 39. A subtração dos anos erra metade do ano de
        // todo mundo, e num crachá clínico a idade errada muda conduta.
        IdadeDoPaciente.Anos(new DateOnly(1986, 9, 9), Hoje).Should().Be(39);
        IdadeDoPaciente.Anos(new DateOnly(1986, 9, 8), Hoje).Should().Be(40);
    }

    [Fact]
    public void Um_ano_no_singular()
        => IdadeDoPaciente.Texto(Hoje.AddYears(-1), Hoje).Should().Be("1 ano");

    // ===================== O CRACHÁ, de ponta a ponta =====================

    private async Task<int> CriarAsync(DateOnly? nascimento)
    {
        var p = new Paciente
        {
            Nome = "Marisa Silva",
            Convenio = Convenio.UnimedIntercambio,
            Sexo = Sexo.Feminino,
            DataNascimento = nascimento
        };
        _db.Pacientes.Add(p);
        await _db.SaveChangesAsync();
        return p.Id;
    }

    [Fact]
    public async Task O_cracha_nao_afirma_a_idade_impossivel_e_PEDE_o_conserto()
    {
        var id = await CriarAsync(new DateOnly(1851, 3, 12));

        var c = (await _consultorio.CabecalhoAsync(id))!;

        c.Idade.Should().BeNull();
        c.NascimentoImplausivel.Should().BeTrue();
        // O terceiro estado ESCRITO: nem o absurdo, nem o silêncio.
        c.Linha.Should().Contain("idade a conferir");
        c.Linha.Should().NotContain("1851");
        c.Linha.Should().NotContain("175 anos");
    }

    [Fact]
    public async Task O_cracha_de_quem_nao_tem_data_apenas_PULA_a_idade()
    {
        var id = await CriarAsync(null);

        var c = (await _consultorio.CabecalhoAsync(id))!;

        c.Idade.Should().BeNull();
        c.NascimentoImplausivel.Should().BeFalse();
        // Sem data não há defeito a apontar: a linha começa direto no sexo.
        c.Linha.Should().NotContain("conferir");
        c.Linha.Should().StartWith("feminino");
    }

    [Fact]
    public async Task O_cracha_de_quem_tem_data_boa_continua_dizendo_a_idade()
    {
        var id = await CriarAsync(new DateOnly(1988, 3, 12));

        var c = (await _consultorio.CabecalhoAsync(id))!;

        c.NascimentoImplausivel.Should().BeFalse();
        c.Linha.Should().StartWith("38 anos");
    }

    public void Dispose()
    {
        _db.Dispose();
        _conn.Dispose();
    }
}
