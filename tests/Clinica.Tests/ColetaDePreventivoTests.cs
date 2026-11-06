using System.Xml.Linq;
using Clinica.Application.Modelos;
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
/// A COLETA DE PREVENTIVO (out/2026): a agenda paralela com teto por dia e a 2ª guia da
/// consulta. O pedido da ginecologia, nas palavras dela: "se no dia 25 já tem cinco
/// preventivos agendados, a secretária já tem que saber" — e a consulta com preventivo são
/// duas guias, uma de consulta e uma de preventivo.
/// </summary>
public class ColetaDePreventivoTests : IDisposable
{
    private static readonly XNamespace Ans = "http://www.ans.gov.br/padroes/tiss/schemas";
    private static readonly DateTime Dia25 = new(2026, 11, 25, 8, 0, 0); // quarta-feira

    private readonly SqliteConnection _conn;
    private readonly ClinicaDbContext _db;
    private readonly ClinicaRepositorio _repo;
    private readonly AgendaService _agenda;
    private readonly int _gineco;

    public ColetaDePreventivoTests()
    {
        _conn = new SqliteConnection("DataSource=:memory:");
        _conn.Open();
        var options = new DbContextOptionsBuilder<ClinicaDbContext>().UseSqlite(_conn).Options;
        _db = new ClinicaDbContext(options);
        _db.Database.EnsureCreated();
        var gineco = new Profissional { Nome = "Dra. Helena", EspecialidadeCodigo = "Ginecologia" };
        _db.Profissionais.Add(gineco);
        _db.SaveChanges();
        _gineco = gineco.Id;
        _repo = new ClinicaRepositorio(_db);
        _agenda = new AgendaService(_repo, new AtendimentoService(_repo));
    }

    public void Dispose()
    {
        _db.Dispose();
        _conn.Dispose();
    }

    private async Task<int> PacienteAsync(Convenio convenio = Convenio.UnimedPadrao)
    {
        var p = new Paciente { Nome = "Paciente", Convenio = convenio, Sexo = Sexo.Feminino };
        _db.Pacientes.Add(p);
        await _db.SaveChangesAsync();
        return p.Id;
    }

    private async Task<Agendamento> MarcarComPreventivoAsync(DateTime quando, int? profissional = null)
        => await _agenda.AgendarAsync(await PacienteAsync(), quando, ModalidadeAtendimento.Consulta, null,
            especialidadeConsultaCodigo: "Ginecologia", profissionalId: profissional ?? _gineco,
            coletaPreventivo: true);

    // ==================== a 2ª guia ====================

    [Theory]
    [InlineData(Convenio.UnimedPadrao)]
    [InlineData(Convenio.UnimedIntercambio)]
    [InlineData(Convenio.Amil)]
    public void Consulta_com_preventivo_gera_duas_guias_do_dia(Convenio convenio)
    {
        var paciente = new Paciente { Nome = "Teste", Convenio = convenio, Sexo = Sexo.Feminino };
        var atendimento = new Atendimento
        {
            Data = new DateOnly(2026, 11, 25), Modalidade = ModalidadeAtendimento.Consulta,
            EspecialidadeConsulta = Especialidade.Ginecologia, ColetaPreventivo = true
        };

        var r = new RegistroRegras().Para(convenio).Gerar(paciente, atendimento, ContextoFaturamento.Vazio);

        r.Codigos.Select(c => c.Tipo).Should().BeEquivalentTo([TipoCodigo.Consulta, TipoCodigo.Preventivo]);
        r.Codigos.Should().OnlyContain(c => c.DataPrevistaFaturamento == atendimento.Data
                                            && c.FormaObtencao == FormaObtencao.NaoAplica
                                            && c.Ordem == OrdemCodigo.Primeiro);
    }

    [Fact]
    public void Lote_tiss_separa_a_guia_de_consulta_da_guia_do_preventivo()
    {
        var paciente = new Paciente { Id = 1, Nome = "Maria", Carteirinha = "0865.0001", Convenio = Convenio.UnimedPadrao };
        var atendimento = new Atendimento { Id = 10, Data = new DateOnly(2026, 11, 25), Paciente = paciente };
        CodigoFaturamento Guia(int id, TipoCodigo tipo, string numero) => new()
        {
            Id = id, AtendimentoId = 10, Atendimento = atendimento, Tipo = tipo, Ordem = OrdemCodigo.Primeiro,
            NumeroGuiaReal = numero, DataBaixa = new DateOnly(2026, 11, 26)
        };
        var prestador = new DadosPrestador
        {
            CodigoNaOperadora = "P", RegistroAnsOperadora = "326305", Cnpj = "12345678000190",
            CodigosTuss = { [TipoCodigo.Consulta] = "10101012", [TipoCodigo.Preventivo] = "40311210" }
        };

        var doc = XDocument.Parse(new TissExportService().GerarLoteXml(
            [Guia(1, TipoCodigo.Consulta, "G-CONSULTA"), Guia(2, TipoCodigo.Preventivo, "G-PREV")], prestador, "1"));

        doc.Descendants(Ans + "guiaConsulta").Should().ContainSingle()
            .Which.Descendants(Ans + "numeroGuiaPrestador").Single().Value.Should().Be("G-CONSULTA");
        var sadt = doc.Descendants(Ans + "guiaSP-SADT").Should().ContainSingle().Subject;
        sadt.Descendants(Ans + "numeroGuiaPrestador").Single().Value.Should().Be("G-PREV");
        sadt.Descendants(Ans + "codigoProcedimento").Single().Value.Should().Be("40311210");
    }

    [Fact]
    public void Atendimento_sem_mistura_continua_com_uma_guia_so()
    {
        // As duplas (acupuntura + eletro) eram uma SP/SADT com dois procedimentos e continuam.
        var a = new CodigoFaturamento { Tipo = TipoCodigo.Acupuntura };
        var b = new CodigoFaturamento { Tipo = TipoCodigo.Eletroacupuntura };
        TissExportService.SepararGuias([a, b]).Should().ContainSingle().Which.Should().HaveCount(2);
    }

    [Fact]
    public async Task Confirmar_a_presenca_do_horario_com_preventivo_gera_as_duas_guias()
    {
        var ag = await MarcarComPreventivoAsync(Dia25);

        var resultado = await _agenda.ConfirmarPresencaAsync(ag.Id);

        resultado.Atendimento.ColetaPreventivo.Should().BeTrue();
        resultado.Atendimento.Codigos.Select(c => c.Tipo)
            .Should().BeEquivalentTo([TipoCodigo.Consulta, TipoCodigo.Preventivo]);
    }

    // ==================== o teto do dia ====================

    [Fact]
    public async Task O_sexto_preventivo_do_dia_e_recusado_e_a_consulta_sem_coleta_passa()
    {
        for (var i = 0; i < Profissional.LimitePreventivoPadrao; i++)
            await MarcarComPreventivoAsync(Dia25.AddMinutes(30 * i));

        var sexto = () => MarcarComPreventivoAsync(Dia25.AddHours(6));
        (await sexto.Should().ThrowAsync<InvalidOperationException>())
            .Which.Message.Should().Contain("25/11").And.Contain("5 de 5");

        // A consulta continua cabendo — o teto é da COLETA, não da agenda.
        var soConsulta = await _agenda.AgendarAsync(await PacienteAsync(), Dia25.AddHours(6),
            ModalidadeAtendimento.Consulta, null, especialidadeConsultaCodigo: "Ginecologia", profissionalId: _gineco);
        soConsulta.ColetaPreventivo.Should().BeFalse();
    }

    [Fact]
    public async Task Cancelar_uma_coleta_devolve_a_vaga()
    {
        var marcados = new List<Agendamento>();
        for (var i = 0; i < 5; i++) marcados.Add(await MarcarComPreventivoAsync(Dia25.AddMinutes(30 * i)));

        await _agenda.CancelarAsync(marcados[0].Id);

        var vagas = (await _agenda.VagasDePreventivoAsync(_gineco, DateOnly.FromDateTime(Dia25), DateOnly.FromDateTime(Dia25)))[0];
        vagas.Marcados.Should().Be(4);
        await MarcarComPreventivoAsync(Dia25.AddHours(6));
    }

    [Fact]
    public async Task O_limite_e_do_profissional_e_zero_quer_dizer_que_ele_nao_faz()
    {
        var outra = new Profissional { Nome = "Dra. Ana", LimitePreventivoPorDia = 0 };
        _db.Profissionais.Add(outra);
        await _db.SaveChangesAsync();

        var marcar = () => MarcarComPreventivoAsync(Dia25, outra.Id);
        (await marcar.Should().ThrowAsync<InvalidOperationException>()).Which.Message.Should().Contain("não faz coleta");
    }

    [Fact]
    public async Task Remarcar_uma_coleta_para_um_dia_lotado_e_recusado_e_o_horario_nao_disputa_consigo()
    {
        for (var i = 0; i < 5; i++) await MarcarComPreventivoAsync(Dia25.AddMinutes(30 * i));
        var outra = await MarcarComPreventivoAsync(Dia25.AddDays(1));

        var remarcar = () => _agenda.RemarcarAsync(outra.Id, Dia25.AddHours(6), null);
        await remarcar.Should().ThrowAsync<InvalidOperationException>();

        // No MESMO dia (só muda a hora), a vaga já é dela: passa.
        await _agenda.RemarcarAsync(outra.Id, Dia25.AddDays(1).AddHours(2), null);
    }

    [Fact]
    public async Task Fora_da_consulta_a_coleta_nao_existe()
    {
        var ag = await _agenda.AgendarAsync(await PacienteAsync(), Dia25, ModalidadeAtendimento.AcupunturaSimples, null,
            profissionalId: _gineco, coletaPreventivo: true);
        ag.ColetaPreventivo.Should().BeFalse();
    }

    // ==================== o que a tela diz ====================

    [Fact]
    public void O_medico_ve_na_lista_do_dia_que_a_paciente_vem_para_colher()
    {
        var ag = new Agendamento
        {
            ModalidadePrevista = ModalidadeAtendimento.Consulta, ModalidadeCodigo = "Consulta",
            EspecialidadeConsultaCodigo = "Ginecologia", ColetaPreventivo = true
        };
        CatalogoModalidades.NomeDoHorario(ag).Should().EndWith(" + preventivo").And.Contain("Ginecologia");

        ag.ColetaPreventivo = false;
        CatalogoModalidades.NomeDoHorario(ag).Should().NotContain("preventivo");
    }

    [Fact]
    public void A_tela_oferece_os_proximos_dias_em_que_o_profissional_atende_e_cabe_coleta()
    {
        var prof = new Profissional { Id = 1, Nome = "Dra.", DiasDeAtendimento = Profissional.BitDe(DayOfWeek.Wednesday) | Profissional.BitDe(DayOfWeek.Thursday) };
        var de = new DateOnly(2026, 11, 25);
        var dias = new[]
        {
            new VagasDePreventivoNoDia(de, 5, 5),
            new VagasDePreventivoNoDia(de.AddDays(1), 2, 5),   // qui: 3 vagas
            new VagasDePreventivoNoDia(de.AddDays(2), 0, 5),   // sex: não atende
            new VagasDePreventivoNoDia(de.AddDays(7), 5, 5),   // qua seguinte: lotado
            new VagasDePreventivoNoDia(de.AddDays(8), 4, 5),   // qui seguinte: 1 vaga
        };

        var proximos = PreventivoNaTela.ProximosComVaga(prof, dias, de);

        proximos.Select(PreventivoNaTela.Dia).Should().Equal("qui 26/11 · 3 vagas", "qui 03/12 · 1 vaga");
        PreventivoNaTela.Lotado(dias[0]).Should().Be("O dia 25/11 já tem os 5 preventivos.");
    }

    [Fact]
    public async Task O_mapa_mostra_a_linha_de_preventivo_abaixo_da_consulta_de_ginecologia()
    {
        for (var i = 0; i < 3; i++) await MarcarComPreventivoAsync(Dia25.AddMinutes(30 * i));

        var mapa = await new OcupacaoPorEspecialidadeService(_repo).ConsultarAsync(new DateOnly(2026, 11, 23));

        mapa.Linhas.Select(l => l.Titulo).Should().Equal("Consulta · Ginecologia", "Coleta de preventivo");
        var prev = mapa.Linhas[1];
        prev.Preventivo.Should().BeTrue();
        prev.Dias.Single(c => c.Dia == new DateOnly(2026, 11, 25)).Livres.Should().Be(2);
        // Domingo sem jornada declarada: ninguém atende.
        prev.Dias.Single(c => c.Dia == new DateOnly(2026, 11, 29)).Atende.Should().BeFalse();
        // A consulta conta os horários livres (as 3 consultas ocupam 3 deles) e as marcadas.
        var gineco = mapa.Linhas[0].Dias.Single(c => c.Dia == new DateOnly(2026, 11, 25));
        gineco.Livres.Should().Be(gineco.Total - 3);
        gineco.Marcados.Should().Be(3);
        mapa.Livres.Should().Be(mapa.Total - 3);
    }

    [Fact]
    public void Os_tratamentos_contam_so_quem_faz_o_tipo()
    {
        var de = new DateOnly(2026, 11, 23);
        var acupunturista = new Profissional
        {
            Id = 1, Nome = "Dr. Marcos", Ativo = true,
            HabilitacoesAtendimentoJson = System.Text.Json.JsonSerializer.Serialize(new[]
            {
                new AtendimentoHabilitado("AcupunturaSimples", null), new AtendimentoHabilitado("BsvApenas", null)
            })
        };
        var psiquiatra = new Profissional { Id = 2, Nome = "Dr. Caio", Ativo = true, EspecialidadeCodigo = "Psiquiatria" };
        var enfermeira = new Profissional { Id = 3, Nome = "Enf. Abigail", Ativo = true };
        var bsv = new Agendamento
        {
            Id = 10, ProfissionalId = 1, DataHora = de.ToDateTime(new TimeOnly(9, 0)),
            ModalidadePrevista = ModalidadeAtendimento.BsvApenas, ModalidadeCodigo = "BsvApenas",
            Status = StatusAgendamento.Agendado
        };

        var mapa = OcupacaoPorEspecialidade.Montar([acupunturista, psiquiatra, enfermeira], [bsv], [], de, de.AddDays(13));

        var titulos = mapa.Linhas.Select(l => (l.Grupo, l.Titulo)).ToList();
        titulos.Should().Contain((GrupoDeOcupacao.Tratamentos, CatalogoModalidades.Nome("AcupunturaSimples", ModalidadeAtendimento.AcupunturaSimples)));
        titulos.Should().Contain((GrupoDeOcupacao.Consultas, "Consulta · " + CatalogoEspecialidades.Nome("Psiquiatria")));
        // Ninguém faz eletro nem BSV + acupuntura: as linhas não aparecem.
        titulos.Should().NotContain(t => t.Titulo == CatalogoModalidades.Nome("AcupunturaComEletro", ModalidadeAtendimento.AcupunturaComEletro));
        // A enfermeira sem habilitação e sem nada marcado não entra em linha nenhuma.
        mapa.Linhas.Should().OnlyContain(l => l.Detalhe == null || !l.Detalhe.Contains("Abigail"));
        var linhaBsv = mapa.Linhas.Single(l => l.Familia == ModalidadeAtendimento.BsvApenas);
        linhaBsv.Dias.Single(c => c.Dia == de).Marcados.Should().Be(1);
        mapa.Linhas.Where(l => l.Grupo == GrupoDeOcupacao.Tratamentos).Should().OnlyContain(l => l.Detalhe == "Dr. Marcos");
    }
}
