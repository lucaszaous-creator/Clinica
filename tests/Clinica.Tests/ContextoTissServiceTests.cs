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
/// Do banco ao XML aceito pelo esquema da ANS (nov/2026): o profissional vem do horário da
/// agenda, o valor vem da tabela de preços do convênio, e o que falta vira pendência com nome.
/// Executa a leitura nova do repositório (<c>ProfissionaisDosAtendimentosAsync</c>).
/// </summary>
public class ContextoTissServiceTests : IDisposable
{
    private readonly SqliteConnection _conn;
    private readonly ClinicaDbContext _db;
    private readonly ClinicaRepositorio _repo;
    private readonly ContextoTissService _contextos;

    public ContextoTissServiceTests()
    {
        _conn = new SqliteConnection("DataSource=:memory:");
        _conn.Open();
        _db = new ClinicaDbContext(new DbContextOptionsBuilder<ClinicaDbContext>().UseSqlite(_conn).Options);
        _db.Database.EnsureCreated();
        _repo = new ClinicaRepositorio(_db);
        _contextos = new ContextoTissService(_repo, new PrecoConvenioService(_repo));
    }

    public void Dispose()
    {
        _db.Dispose();
        _conn.Dispose();
    }

    private static readonly DateOnly Dia = new(2026, 7, 10);

    private async Task<CodigoFaturamento> GuiaBaixadaAsync(Profissional? profissional, string guia = "9001")
    {
        var p = new Paciente { Nome = "Maria " + guia, Convenio = Convenio.UnimedIntercambio, Carteirinha = "00641234567890123", Sexo = Sexo.Feminino };
        _db.Pacientes.Add(p);
        await _db.SaveChangesAsync();
        var r = await new AtendimentoService(_repo).LancarAsync(p.Id, Dia, ModalidadeAtendimento.AcupunturaSimples);
        var codigo = r.Atendimento.Codigos.First();
        await new FaturamentoService(_repo).DarBaixaAsync(codigo.Id, Dia.AddDays(1), guia, "sec", null);
        if (profissional is not null)
        {
            _db.Agendamentos.Add(new Agendamento
            {
                PacienteId = p.Id, DataHora = Dia.ToDateTime(new TimeOnly(9, 0)), Profissional = profissional,
                AtendimentoId = r.Atendimento.Id, ModalidadePrevista = ModalidadeAtendimento.AcupunturaSimples,
                Status = StatusAgendamento.Realizado
            });
            await _db.SaveChangesAsync();
        }
        return codigo;
    }

    private static DadosPrestador Prestador() => new()
    {
        CodigoNaOperadora = "12345", RegistroAnsOperadora = "326305", Cnes = "1234567", RazaoSocial = "Clínica SemDor",
        CodigosTuss = { [TipoCodigo.Acupuntura] = "31601014" }
    };

    [Fact]
    public async Task Profissional_da_agenda_e_preco_da_tabela_chegam_ao_XML_e_o_esquema_aceita()
    {
        var ana = new Profissional { Nome = "Dra. Ana Souza", RegistroConselho = "CRM-RJ 52.1234", EspecialidadeCodigo = "Acupuntura" };
        var codigo = await GuiaBaixadaAsync(ana);
        _db.PrecosConvenio.Add(new PrecoConvenio { ConvenioCodigo = nameof(Convenio.UnimedIntercambio), Tipo = TipoCodigo.Acupuntura, Valor = 92.4m });
        await _db.SaveChangesAsync();

        var contexto = await _contextos.MontarAsync([codigo]);

        contexto.Pendencias.Should().BeEmpty();
        contexto.ProfissionalDe(codigo).Should().Be(new ProfissionalTiss("Dra. Ana Souza", "06", "521234", "33", "225105"));
        contexto.ValorDe(codigo).Should().Be(92.4m);
        var xml = new TissExportService().GerarLoteXml([codigo], Prestador(), "1", contexto);
        ExportacaoLoteTiss.ValidarXml(xml).Should().BeEmpty();
        xml.Should().Contain("<ans:valorTotalGeral>92.40</ans:valorTotalGeral>");
    }

    [Fact]
    public async Task O_que_falta_vira_pendencia_com_nome_e_nao_e_inventado()
    {
        // Registro sem UF, especialidade sem CBO único, convênio sem preço e um atendimento sem agenda.
        var beto = new Profissional { Nome = "Dr. Beto Lima", RegistroConselho = "CRM 99887", EspecialidadeCodigo = "ClinicaDaDor" };
        var comAgenda = await GuiaBaixadaAsync(beto, "9001");
        var semAgenda = await GuiaBaixadaAsync(null, "9002");

        var contexto = await _contextos.MontarAsync([comAgenda, semAgenda]);

        contexto.Pendencias.Should().Contain(p => p.StartsWith("Dr. Beto Lima: o registro do conselho"));
        contexto.Pendencias.Should().Contain(p => p.StartsWith("Dr. Beto Lima: sem CBO"));
        contexto.Pendencias.Should().Contain(p => p.StartsWith("1 atendimento(s) sem profissional"));
        contexto.Pendencias.Should().ContainSingle(p => p.Contains("sem preço de Acupuntura"), "a mesma falta não se repete por guia");
        contexto.ProfissionalDe(semAgenda).Should().BeNull();
        contexto.ValorDe(comAgenda).Should().Be(0m);
    }

    [Fact]
    public async Task O_CBO_cadastrado_em_Equipe_e_gravado_so_com_seis_digitos()
    {
        var equipe = new EquipeService(_repo);
        var salvo = await equipe.SalvarProfissionalAsync(new Profissional { Nome = "Dra. Carla", CodigoCbo = "225.151" });
        (await _db.Profissionais.AsNoTracking().SingleAsync(p => p.Id == salvo.Id)).CodigoCbo.Should().Be("225151");

        var meio = () => equipe.SalvarProfissionalAsync(new Profissional { Nome = "Dr. Davi", CodigoCbo = "2251" });
        await meio.Should().ThrowAsync<InvalidOperationException>().WithMessage("O CBO tem 6 dígitos*");
    }

    [Fact]
    public async Task O_lote_criado_so_com_as_guias_pedidas_deixa_as_outras_para_o_proximo()
    {
        var a = await GuiaBaixadaAsync(null, "9001");
        var b = await GuiaBaixadaAsync(null, "9002");
        var lotes = new LoteTissService(_repo, new ParametrosService(_repo));

        var primeiro = await lotes.CriarAsync(new DateOnly(2026, 7, 1), new DateOnly(2026, 7, 31), "326305", somenteCodigos: [a.Id]);
        var segundo = await lotes.CriarAsync(new DateOnly(2026, 7, 1), new DateOnly(2026, 7, 31), "326305", somenteCodigos: [b.Id]);

        primeiro!.Codigos.Select(c => c.Id).Should().Equal(a.Id);
        segundo!.Codigos.Select(c => c.Id).Should().Equal(b.Id);
    }

    [Fact]
    public async Task A_glosa_carrega_o_lote_para_o_recurso()
    {
        var codigo = await GuiaBaixadaAsync(null);
        var lotes = new LoteTissService(_repo, new ParametrosService(_repo));
        var lote = await lotes.CriarAsync(new DateOnly(2026, 7, 1), new DateOnly(2026, 7, 31), "326305");
        await lotes.MarcarEnviadoAsync(lote!.Id, new DateOnly(2026, 7, 15), "123456");
        codigo.RegistrarGlosa(new DateOnly(2026, 7, 20), "carteira", "1001");
        await _db.SaveChangesAsync();
        _db.ChangeTracker.Clear();

        var glosadas = await _repo.CodigosGlosadosAsync(somenteEmAberto: true);

        var plano = RecursoDeGlosaTiss.Planejar(glosadas);
        plano.DeFora.Should().BeEmpty();
        plano.Recursos.Should().ContainSingle().Which.Lote.ProtocoloOperadora.Should().Be("123456");
    }
}
