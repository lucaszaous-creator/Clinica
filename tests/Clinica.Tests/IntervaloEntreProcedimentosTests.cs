using Clinica.Application.Servicos;
using Clinica.Domain;
using Clinica.Domain.Entities;
using Clinica.Domain.Regras;
using Clinica.Infrastructure;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Clinica.Tests;

/// <summary>
/// Sul América (nov/2026): acupuntura + eletroacupuntura no MESMO dia; a acupuntura libera
/// nova com 1 dia de intervalo e a eletro só autoriza a cada 7 dias. A guia que cairia antes
/// nasce com a data em que o convênio a libera.
///
/// A regra pura está em <see cref="RegraGenericaTests"/>; aqui é o caminho de verdade —
/// <c>AtendimentoService</c> lendo o histórico do paciente pelo repositório
/// (<c>CodigosDoPacienteDesdeAsync</c>), cruzando a virada do mês, na prévia e no lançamento.
/// </summary>
public class IntervaloEntreProcedimentosTests : IDisposable
{
    private const string CodigoSulAmerica = "SulAmericaDoTeste";

    private readonly SqliteConnection _conn;
    private readonly ClinicaDbContext _db;
    private readonly AtendimentoService _atendimentos;

    public IntervaloEntreProcedimentosTests()
    {
        _conn = new SqliteConnection("DataSource=:memory:");
        _conn.Open();
        var options = new DbContextOptionsBuilder<ClinicaDbContext>().UseSqlite(_conn).Options;
        _db = new ClinicaDbContext(options);
        _db.Database.EnsureCreated();
        _atendimentos = new AtendimentoService(new ClinicaRepositorio(_db));

        // O catálogo é um cache estático: cada teste monta o seu.
        CatalogoConvenios.Atualizar(
        [
            new EntradaConvenio(CodigoSulAmerica, "Sul América", Convenio.Personalizado, true,
                new ConfiguracaoRegraGenerica
                {
                    FazEletro = true, EletroNoMesmoDia = true,
                    IntervaloAcupunturaDias = 1, IntervaloEletroDias = 7
                },
                FormatoNumeroGuia.SemValidacao)
        ]);
    }

    public void Dispose()
    {
        CatalogoConvenios.Atualizar([]);
        _db.Dispose();
        _conn.Dispose();
        GC.SuppressFinalize(this);
    }

    private async Task<int> PacienteAsync()
    {
        var p = new Paciente
        {
            Nome = "Paciente Sul América", Convenio = Convenio.Personalizado,
            ConvenioCodigo = CodigoSulAmerica, Sexo = Sexo.Feminino
        };
        _db.Pacientes.Add(p);
        await _db.SaveChangesAsync();
        return p.Id;
    }

    [Fact]
    public async Task Acupuntura_e_eletro_saem_no_dia_e_a_eletro_seguinte_espera_os_7_dias_atravessando_o_mes()
    {
        var id = await PacienteAsync();

        var primeira = await _atendimentos.LancarAsync(id, new DateOnly(2026, 10, 29), ModalidadeAtendimento.AcupunturaComEletro);
        primeira.Atendimento.Codigos.Should().HaveCount(2)
            .And.OnlyContain(c => c.DataPrevistaFaturamento == new DateOnly(2026, 10, 29) && c.Ordem == OrdemCodigo.Primeiro);

        // 02/11: a acupuntura sai no dia; a eletro só a partir de 05/11 (29/10 + 7).
        var previa = await _atendimentos.PreverAsync(id, new DateOnly(2026, 11, 2), ModalidadeAtendimento.AcupunturaComEletro);
        previa.Codigos.Single(c => c.Tipo == TipoCodigo.Eletroacupuntura).DataPrevistaFaturamento
            .Should().Be(new DateOnly(2026, 11, 5), "a prévia mostra o mesmo que o lançamento vai gravar");

        var segunda = await _atendimentos.LancarAsync(id, new DateOnly(2026, 11, 2), ModalidadeAtendimento.AcupunturaComEletro);
        segunda.Atendimento.Codigos.Single(c => c.Tipo == TipoCodigo.Acupuntura).DataPrevistaFaturamento
            .Should().Be(new DateOnly(2026, 11, 2));
        var eletro = segunda.Atendimento.Codigos.Single(c => c.Tipo == TipoCodigo.Eletroacupuntura);
        eletro.DataPrevistaFaturamento.Should().Be(new DateOnly(2026, 11, 5));
        eletro.Ordem.Should().Be(OrdemCodigo.Segundo);
        segunda.Avisos.Should().Contain(a => a.Contains("05/11/2026"));
    }
}
