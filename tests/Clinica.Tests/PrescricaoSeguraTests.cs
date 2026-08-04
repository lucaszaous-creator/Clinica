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
/// Conferência clínica da prescrição (parcela 40).
///
/// Estes testes cobrem o único ponto do sistema em que um defeito não custa dinheiro:
/// prescrever um alérgeno que a própria clínica registrou. Por isso as duas direções são
/// testadas com o mesmo peso — o falso NEGATIVO deixa passar o alérgeno, e o falso
/// POSITIVO treina o profissional a fechar o alerta sem ler, o que produz o falso
/// negativo na semana seguinte.
/// </summary>
public class PrescricaoSeguraTests : IDisposable
{
    private readonly SqliteConnection _conn;
    private readonly ClinicaDbContext _db;
    private readonly ClinicaRepositorio _repo;
    private readonly PrescricaoService _prescricao;
    private readonly ProblemaPacienteService _problemas;

    public PrescricaoSeguraTests()
    {
        _conn = new SqliteConnection("DataSource=:memory:");
        _conn.Open();
        var options = new DbContextOptionsBuilder<ClinicaDbContext>().UseSqlite(_conn).Options;
        _db = new ClinicaDbContext(options);
        _db.Database.EnsureCreated();
        _repo = new ClinicaRepositorio(_db);
        _prescricao = new PrescricaoService(_repo);
        _problemas = new ProblemaPacienteService(_repo);
    }

    private async Task<int> PacienteAsync()
    {
        var p = new Paciente { Nome = "Paciente", Convenio = Convenio.UnimedIntercambio, Sexo = Sexo.Feminino };
        _db.Pacientes.Add(p);
        await _db.SaveChangesAsync();
        return p.Id;
    }

    private Task RegistrarAsync(
        int pacienteId, string descricao, NaturezaProblema natureza,
        SituacaoProblema situacao = SituacaoProblema.Ativo)
        => _problemas.SalvarAsync(new ProblemaPaciente
        {
            PacienteId = pacienteId,
            Descricao = descricao,
            Natureza = natureza,
            Situacao = situacao
        });

    // ======================================================== o caso que dá nome ao serviço

    [Fact]
    public async Task Prescrever_Alergeno_Alerta()
    {
        var pacienteId = await PacienteAsync();
        await RegistrarAsync(pacienteId, "Dipirona", NaturezaProblema.Alergia);

        var conferencia = await _prescricao.ConferirAsync(
            pacienteId, ["Dipirona 500mg - 1 comprimido de 6/6h"]);

        conferencia.ExigeConfirmacao.Should().BeTrue();
        conferencia.Alertas.Should().ContainSingle(a => a.Gravidade == GravidadePrescricao.Alergia)
            .Which.Motivo.Should().Contain("Dipirona");
    }

    /// <summary>Caixa e acento não podem decidir se alguém recebe o alerta ou não.</summary>
    [Theory]
    [InlineData("DIPIRONA 500MG")]
    [InlineData("dipirona sódica")]
    [InlineData("Dipirona, 1 caixa")]
    public async Task Prescrever_Alergeno_ComOutraGrafia_Alerta(string item)
    {
        var pacienteId = await PacienteAsync();
        await RegistrarAsync(pacienteId, "dipirona", NaturezaProblema.Alergia);

        var conferencia = await _prescricao.ConferirAsync(pacienteId, [item]);

        conferencia.ExigeConfirmacao.Should().BeTrue();
    }

    [Fact]
    public async Task Prescrever_ComAcentoNoRegistro_Alerta()
    {
        var pacienteId = await PacienteAsync();
        await RegistrarAsync(pacienteId, "Penicilína", NaturezaProblema.Alergia);

        var conferencia = await _prescricao.ConferirAsync(pacienteId, ["Penicilina G benzatina"]);

        conferencia.ExigeConfirmacao.Should().BeTrue();
    }

    /// <summary>
    /// "Alergia a dipirona" casa por DIPIRONA, não pela frase: ninguém escreve a receita
    /// com as palavras que usou para anotar a alergia.
    /// </summary>
    [Fact]
    public async Task Prescrever_AlergiaAnotadaComoFrase_CasaPeloPrincipioAtivo()
    {
        var pacienteId = await PacienteAsync();
        await RegistrarAsync(pacienteId, "Alergia a amoxicilina", NaturezaProblema.Alergia);

        var conferencia = await _prescricao.ConferirAsync(pacienteId, ["Amoxicilina 875mg"]);

        conferencia.ExigeConfirmacao.Should().BeTrue();
    }

    // ======================================================== o falso positivo, que é igual de grave

    /// <summary>
    /// Se a palavra "alergia" casasse, TODA receita de um paciente com qualquer alergia
    /// anotada acenderia — e o alerta viraria ruído que se fecha sem ler.
    /// </summary>
    [Fact]
    public async Task Prescrever_OutroMedicamento_NaoAlerta()
    {
        var pacienteId = await PacienteAsync();
        await RegistrarAsync(pacienteId, "Alergia a dipirona", NaturezaProblema.Alergia);

        var conferencia = await _prescricao.ConferirAsync(
            pacienteId, ["Paracetamol 750mg", "Uso contínuo, 1 comprimido"]);

        conferencia.ExigeConfirmacao.Should().BeFalse();
        conferencia.Alertas.Should().NotContain(a => a.Gravidade == GravidadePrescricao.Alergia);
    }

    /// <summary>
    /// Palavra inteira, nunca trecho: "sal" dentro de "salbutamol" é coincidência de
    /// letras, não coincidência clínica.
    /// </summary>
    [Fact]
    public async Task Prescrever_PalavraContidaEmOutra_NaoAlerta()
    {
        var pacienteId = await PacienteAsync();
        await RegistrarAsync(pacienteId, "Sulfa", NaturezaProblema.Alergia);

        var conferencia = await _prescricao.ConferirAsync(pacienteId, ["Sulfametoxazol"]);

        // Deliberado: o serviço NÃO tenta adivinhar família de fármaco por prefixo. Ele
        // compara o que a clínica escreveu — e a tela mostra a lista de alergias ao lado
        // justamente porque a comparação textual não substitui quem lê.
        conferencia.ExigeConfirmacao.Should().BeFalse();
        conferencia.Alergias.Should().ContainSingle();
    }

    /// <summary>Termo curto demais não é comparável — ele acenderia em qualquer coisa.</summary>
    [Fact]
    public async Task Prescrever_AlergiaComTermoCurto_NaoDisparaSozinha()
    {
        var pacienteId = await PacienteAsync();
        await RegistrarAsync(pacienteId, "AAS", NaturezaProblema.Alergia);

        var conferencia = await _prescricao.ConferirAsync(pacienteId, ["Losartana 50mg"]);

        conferencia.ExigeConfirmacao.Should().BeFalse();
        // Mas ela continua VISÍVEL na tela: o que se perde é o casamento automático.
        conferencia.Alergias.Should().ContainSingle().Which.Descricao.Should().Be("AAS");
    }

    // ======================================================== as regras da lista de problemas

    /// <summary>
    /// Alergia "resolvida" continua alertando — é a regra da parcela 37, e aqui ela é o
    /// que impede que um registro dado por superado deixe de aparecer no dia em que volta.
    /// </summary>
    [Fact]
    public async Task Prescrever_AlergenoDadoPorResolvido_AindaAlerta()
    {
        var pacienteId = await PacienteAsync();

        // Resolver é operação PRÓPRIA: o problema nasce ativo e alguém o encerra depois.
        var problema = await _problemas.SalvarAsync(new ProblemaPaciente
        {
            PacienteId = pacienteId,
            Descricao = "Dipirona",
            Natureza = NaturezaProblema.Alergia
        });
        await _problemas.ResolverAsync(problema.Id);

        var conferencia = await _prescricao.ConferirAsync(pacienteId, ["Dipirona 500mg"]);

        conferencia.ExigeConfirmacao.Should().BeTrue();
        conferencia.Alertas.Single(a => a.Gravidade == GravidadePrescricao.Alergia)
            .Motivo.Should().Contain("resolvida");
    }

    /// <summary>Descartado é registro DESDITO: não alerta e não aparece como contexto.</summary>
    [Fact]
    public async Task Prescrever_AlergiaDescartada_NaoAlerta()
    {
        var pacienteId = await PacienteAsync();
        var problema = await _problemas.SalvarAsync(new ProblemaPaciente
        {
            PacienteId = pacienteId,
            Descricao = "Dipirona",
            Natureza = NaturezaProblema.Alergia
        });
        await _problemas.DescartarAsync(problema.Id, "Registrado no paciente errado");

        var conferencia = await _prescricao.ConferirAsync(pacienteId, ["Dipirona 500mg"]);

        conferencia.ExigeConfirmacao.Should().BeFalse();
        conferencia.Alergias.Should().BeEmpty();
    }

    // ======================================================== medicação em uso

    /// <summary>
    /// A medicação contínua é CONTEXTO, não casamento: o valor dela é o que ainda não foi
    /// escrito. Casá-la com o item produziria "você prescreveu o que ele já toma", que é
    /// exatamente o caso normal da renovação de receita.
    /// </summary>
    [Fact]
    public async Task Contexto_TrazMedicacaoEmUso_MesmoComReceitaEmBranco()
    {
        var pacienteId = await PacienteAsync();
        await RegistrarAsync(pacienteId, "Losartana 50mg", NaturezaProblema.MedicacaoContinua);
        await RegistrarAsync(pacienteId, "Dipirona", NaturezaProblema.Alergia);

        var contexto = await _prescricao.ContextoAsync(pacienteId);

        contexto.MedicacoesEmUso.Should().ContainSingle();
        contexto.Alergias.Should().ContainSingle();
        contexto.ExigeConfirmacao.Should().BeFalse("não há item escrito para conferir");
        contexto.Alertas.Should().Contain(a => a.Motivo.Contains("Losartana"));
    }

    /// <summary>Renovar a mesma medicação contínua não é alerta de alergia.</summary>
    [Fact]
    public async Task Prescrever_AMesmaMedicacaoContinua_NaoExigeConfirmacao()
    {
        var pacienteId = await PacienteAsync();
        await RegistrarAsync(pacienteId, "Losartana 50mg", NaturezaProblema.MedicacaoContinua);

        var conferencia = await _prescricao.ConferirAsync(pacienteId, ["Losartana 50mg"]);

        conferencia.ExigeConfirmacao.Should().BeFalse();
    }

    /// <summary>Diagnóstico e antecedente não entram na conferência da receita.</summary>
    [Fact]
    public async Task Prescrever_DiagnosticoComMesmoNome_NaoAlerta()
    {
        var pacienteId = await PacienteAsync();
        await RegistrarAsync(pacienteId, "Lombalgia", NaturezaProblema.Diagnostico);
        await RegistrarAsync(pacienteId, "Apendicectomia", NaturezaProblema.Antecedente);

        var conferencia = await _prescricao.ConferirAsync(pacienteId, ["Lombalgia"]);

        conferencia.ExigeConfirmacao.Should().BeFalse();
        conferencia.Alergias.Should().BeEmpty();
        conferencia.MedicacoesEmUso.Should().BeEmpty();
    }

    // ======================================================== casos de borda

    [Fact]
    public async Task Conferir_PacienteSemProblemas_NaoAlerta()
    {
        var pacienteId = await PacienteAsync();

        var conferencia = await _prescricao.ConferirAsync(pacienteId, ["Dipirona 500mg"]);

        conferencia.Alertas.Should().BeEmpty();
        conferencia.ExigeConfirmacao.Should().BeFalse();
    }

    [Fact]
    public async Task Conferir_ItemEmBranco_EhIgnorado()
    {
        var pacienteId = await PacienteAsync();
        await RegistrarAsync(pacienteId, "Dipirona", NaturezaProblema.Alergia);

        var conferencia = await _prescricao.ConferirAsync(pacienteId, ["", "   ", null!]);

        conferencia.ExigeConfirmacao.Should().BeFalse();
    }

    /// <summary>Duas alergias, dois itens: cada coincidência é um alerta próprio.</summary>
    [Fact]
    public async Task Conferir_VariosItens_AlertaCadaCoincidencia()
    {
        var pacienteId = await PacienteAsync();
        await RegistrarAsync(pacienteId, "Dipirona", NaturezaProblema.Alergia);
        await RegistrarAsync(pacienteId, "Penicilina", NaturezaProblema.Alergia);

        var conferencia = await _prescricao.ConferirAsync(
            pacienteId, ["Dipirona 500mg", "Paracetamol 750mg", "Penicilina G"]);

        conferencia.Alertas.Where(a => a.Gravidade == GravidadePrescricao.Alergia)
            .Should().HaveCount(2);
    }

    public void Dispose()
    {
        _db.Dispose();
        _conn.Dispose();
    }
}
