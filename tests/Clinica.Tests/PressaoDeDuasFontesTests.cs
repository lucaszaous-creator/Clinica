using Clinica.Application.Servicos;
using Clinica.Domain;
using Clinica.Domain.Entities;
using Clinica.Domain.Avaliacoes;
using Clinica.Domain.Medidas;
using Clinica.Infrastructure;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Clinica.Tests;

/// <summary>
/// A CURVA DE PRESSÃO ARTERIAL VEM DE DUAS FONTES (parcela 72).
///
/// O buraco que estes testes fecham
/// --------------------------------
/// A PA está declarada em <see cref="CatalogoMedidas"/> desde a parcela 37, com as faixas
/// publicadas — e a série <b>nascia vazia e continuava vazia</b>. A razão é estrutural: a
/// única porta de ESCRITA de <c>MedidaClinica</c> está no app do MÉDICO, enquanto a
/// pressão de verdade é aferida na ENFERMAGEM, toda sessão, e vai para
/// <c>EvolucaoEnfermagem</c>. E <b>curva vazia se lê como "este paciente nunca teve a
/// pressão aferida"</b>, que é falso.
///
/// ⚠️ A ponte é de LEITURA, e isso é decisão: destravar a colheita de <c>MedidaClinica</c>
/// pela enfermagem daria DOIS lugares para gravar a mesma aferição, sem nada na tela
/// dizendo qual. A decisão de onde ela mora já está tomada — com HORA, que
/// <c>MedidaClinica</c> não tem.
/// </summary>
public class PressaoDeDuasFontesTests : IDisposable
{
    private readonly SqliteConnection _conn;
    private readonly ClinicaDbContext _db;
    private readonly ClinicaRepositorio _repo;
    private readonly MedidaClinicaService _medidas;
    private readonly EvolucaoEnfermagemService _enfermagem;

    private static readonly DateOnly Dia = new(2026, 8, 3);
    /// <summary>
    /// Relógio fixo no FIM do dia: a recusa de hora futura é regra de segurança e vale
    /// aqui igual (parcela 42). Com o relógio ao meio-dia, aferir "às 15h" seria recusado
    /// — e o teste estaria medindo a guarda em vez da curva.
    /// </summary>
    private static readonly DateTime FimDoDia = Dia.ToDateTime(new TimeOnly(20, 0));

    private static readonly IdentificacaoExecutante Tecnica =
        new(null, "Joana Técnica", "COREN-SP 999999");

    public PressaoDeDuasFontesTests()
    {
        _conn = new SqliteConnection("DataSource=:memory:");
        _conn.Open();
        _db = new ClinicaDbContext(
            new DbContextOptionsBuilder<ClinicaDbContext>().UseSqlite(_conn).Options);
        _db.Database.EnsureCreated();
        _repo = new ClinicaRepositorio(_db);
        _medidas = new MedidaClinicaService(_repo);
        _enfermagem = new EvolucaoEnfermagemService(_repo, () => FimDoDia);
    }

    private async Task<int> PacienteAsync()
    {
        var p = new Paciente
        {
            Nome = "Maria", Convenio = Convenio.UnimedIntercambio, Sexo = Sexo.Feminino
        };
        _db.Pacientes.Add(p);
        await _db.SaveChangesAsync();
        return p.Id;
    }

    private Task AferirNaEnfermagemAsync(
        int pacienteId, DateOnly data, TimeOnly hora, int sistolica, int diastolica)
        => _enfermagem.RegistrarAsync(
            pacienteId, data, hora, "Sinais vitais aferidos.", Tecnica,
            sinais: new SinaisVitais
            {
                PressaoSistolica = sistolica,
                PressaoDiastolica = diastolica
            });

    [Fact]
    public async Task A_pressao_aferida_na_enfermagem_APARECE_na_curva()
    {
        var pacienteId = await PacienteAsync();
        await AferirNaEnfermagemAsync(pacienteId, Dia, new TimeOnly(9, 0), 118, 76);

        var serie = await _medidas.SerieAsync(pacienteId, CatalogoMedidas.PressaoArterial);

        // Antes desta parcela isto era ZERO — e "nenhum ponto" se lê como "nunca aferida".
        serie.Pontos.Should().HaveCount(1);
        serie.Pontos[0].Valor.Should().Be(118);
        serie.Pontos[0].ValorSecundario.Should().Be(76);
    }

    [Fact]
    public async Task Cada_ponto_DIZ_de_onde_veio()
    {
        var pacienteId = await PacienteAsync();

        await _medidas.RegistrarAsync(new MedidaClinica
        {
            PacienteId = pacienteId,
            TipoCodigo = CatalogoMedidas.PressaoArterial,
            Data = Dia,
            Valor = 130,
            ValorSecundario = 85
        }, "dra.ana");

        await AferirNaEnfermagemAsync(pacienteId, Dia, new TimeOnly(14, 30), 150, 95);

        var serie = await _medidas.SerieAsync(pacienteId, CatalogoMedidas.PressaoArterial);

        // ⚠️ Sem a procedência, a curva teria dois pontos do MESMO DIA sem dizer que um é
        // de antes da consulta e o outro de meia hora depois da bomba — e a diferença
        // entre os dois é justamente a leitura clínica.
        serie.Pontos.Should().HaveCount(2);
        serie.Pontos.Select(p => p.Procedencia)
            .Should().BeEquivalentTo(["consultório", "enfermagem"]);
    }

    [Fact]
    public async Task Dentro_do_mesmo_dia_a_HORA_desempata()
    {
        var pacienteId = await PacienteAsync();

        await AferirNaEnfermagemAsync(pacienteId, Dia, new TimeOnly(15, 0), 160, 100);
        await AferirNaEnfermagemAsync(pacienteId, Dia, new TimeOnly(9, 0), 120, 80);

        var serie = await _medidas.SerieAsync(pacienteId, CatalogoMedidas.PressaoArterial);

        // A ordem é a dos FATOS, não a da gravação: a de 15h foi digitada primeiro.
        serie.Pontos.Select(p => p.Valor).Should().ContainInOrder(120m, 160m);
    }

    [Fact]
    public async Task A_faixa_sai_da_MESMA_definicao_do_catalogo()
    {
        var pacienteId = await PacienteAsync();
        await AferirNaEnfermagemAsync(pacienteId, Dia, new TimeOnly(9, 0), 150, 95);

        var serie = await _medidas.SerieAsync(pacienteId, CatalogoMedidas.PressaoArterial);

        // ⚠️ A evolução de enfermagem grava os números CRUS — não há campo de faixa nela.
        // Inventar uma segunda leitura de "pressão normal" aqui daria duas respostas para
        // a mesma aferição.
        serie.Pontos[0].FaixaNome.Should().Be("Hipertensão");
        serie.Pontos[0].Gravidade.Should().Be(GravidadeFaixa.Alerta);
    }

    [Fact]
    public async Task Cancelada_e_retificada_NAO_entram_na_curva()
    {
        var pacienteId = await PacienteAsync();

        var errada = await _enfermagem.RegistrarAsync(
            pacienteId, Dia, new TimeOnly(9, 0), "Aferição errada.", Tecnica,
            sinais: new SinaisVitais { PressaoSistolica = 200, PressaoDiastolica = 130 });

        await _enfermagem.RetificarAsync(
            errada.Id, Dia, new TimeOnly(9, 0), "Aferição corrigida.", Tecnica,
            motivoRetificacao: "Manguito errado.",
            sinais: new SinaisVitais { PressaoSistolica = 120, PressaoDiastolica = 80 });

        var cancelada = await _enfermagem.RegistrarAsync(
            pacienteId, Dia, new TimeOnly(10, 0), "Paciente trocado.", Tecnica,
            sinais: new SinaisVitais { PressaoSistolica = 190, PressaoDiastolica = 120 });

        await _enfermagem.CancelarAsync(
            cancelada.Id, "Lançada no paciente errado.", "joana");

        var serie = await _medidas.SerieAsync(pacienteId, CatalogoMedidas.PressaoArterial);

        // Registro desdito não é aferição — mas continua no prontuário, marcado (o que a
        // linha do tempo mostra). Aqui ele não pode virar ponto de curva.
        serie.Pontos.Should().HaveCount(1);
        serie.Pontos[0].Valor.Should().Be(120);
    }

    [Fact]
    public async Task Meia_pressao_nao_vira_ponto()
    {
        var pacienteId = await PacienteAsync();

        // Só a temperatura: a evolução é válida e não tem pressão nenhuma.
        await _enfermagem.RegistrarAsync(
            pacienteId, Dia, new TimeOnly(9, 0), "Temperatura aferida.", Tecnica,
            sinais: new SinaisVitais { Temperatura = 36.8m });

        var serie = await _medidas.SerieAsync(pacienteId, CatalogoMedidas.PressaoArterial);

        serie.Pontos.Should().BeEmpty();
    }

    [Fact]
    public async Task As_outras_medidas_NAO_mudaram_de_comportamento()
    {
        var pacienteId = await PacienteAsync();

        await _medidas.RegistrarAsync(new MedidaClinica
        {
            PacienteId = pacienteId,
            TipoCodigo = CatalogoMedidas.Peso,
            Data = Dia,
            Valor = 78.5m
        }, "dra.ana");
        await AferirNaEnfermagemAsync(pacienteId, Dia, new TimeOnly(9, 0), 118, 76);

        var peso = await _medidas.SerieAsync(pacienteId, CatalogoMedidas.Peso);

        // A ponte é SÓ da PA: a enfermagem não pesa ninguém no `EvolucaoEnfermagem`, e
        // alargar a mescla para os outros tipos seria inventar dado.
        peso.Pontos.Should().HaveCount(1);
        peso.Pontos[0].Valor.Should().Be(78.5m);
    }

    public void Dispose()
    {
        _db.Dispose();
        _conn.Dispose();
        GC.SuppressFinalize(this);
    }
}
