using Clinica.Application.Servicos;
using Clinica.Domain;
using Clinica.Domain.Avaliacoes;
using Clinica.Domain.Entities;
using Clinica.Domain.Medidas;
using Clinica.Infrastructure;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Clinica.Tests;

/// <summary>
/// Antropometria e sinais vitais seriados (parcela 37).
///
/// A regra que carrega a parcela é a mesma da escala aplicada e do preço por convênio:
/// **o que descreve o tipo é COPIADO na colheita, nunca referenciado**. A segunda é a que
/// dá o nome à tela: **o IMC não se digita** — ele é derivado do peso com a altura
/// vigente, e a data dessa altura vai junto porque um IMC calculado com altura de três
/// anos atrás continua sendo a melhor leitura disponível, desde que quem lê saiba disso.
/// </summary>
public class MedidasClinicasTests : IDisposable
{
    private readonly SqliteConnection _conn;
    private readonly ClinicaDbContext _db;
    private readonly ClinicaRepositorio _repo;
    private readonly MedidaClinicaService _medidas;

    private static readonly DateOnly Hoje = new(2026, 8, 3);

    public MedidasClinicasTests()
    {
        _conn = new SqliteConnection("DataSource=:memory:");
        _conn.Open();
        var options = new DbContextOptionsBuilder<ClinicaDbContext>().UseSqlite(_conn).Options;
        _db = new ClinicaDbContext(options);
        _db.Database.EnsureCreated();
        _repo = new ClinicaRepositorio(_db);
        _medidas = new MedidaClinicaService(_repo);
    }

    private async Task<int> CriarPacienteAsync(Sexo sexo = Sexo.Feminino)
    {
        var p = new Paciente { Nome = "Paciente", Convenio = Convenio.UnimedIntercambio, Sexo = sexo };
        _db.Pacientes.Add(p);
        await _db.SaveChangesAsync();
        return p.Id;
    }

    private Task<MedidaClinica> ColherAsync(
        int pacienteId, string tipo, decimal valor, DateOnly dia,
        decimal? segundo = null)
        => _medidas.RegistrarAsync(new MedidaClinica
        {
            PacienteId = pacienteId,
            Data = dia,
            TipoCodigo = tipo,
            Valor = valor,
            ValorSecundario = segundo
        }, "dra.ana");

    // ------------------------------------------------------------ o que se copia

    [Fact]
    public async Task A_colheita_copia_nome_unidade_e_faixa_do_catalogo()
    {
        var p = await CriarPacienteAsync();

        var medida = await ColherAsync(p, CatalogoMedidas.GlicemiaJejum, 140, Hoje);

        medida.TipoNome.Should().Be("Glicemia de jejum");
        medida.Unidade.Should().Be("mg/dL");
        medida.FaixaNome.Should().Be("Faixa de diabete");
        medida.FaixaGravidade.Should().Be(GravidadeFaixa.Alerta);
        // A interpretação publicada vem junto — é o que mantém legível uma colheita de um
        // tipo que amanhã pode sair do catálogo.
        medida.FaixaInterpretacao.Should().Contain("não fecha diagnóstico");
    }

    [Fact]
    public async Task Sem_faixa_publicada_a_colheita_nao_inventa_normal()
    {
        // Peso isolado não se interpreta: 80 kg é obesidade numa pessoa e magreza noutra.
        // "Não há leitura publicada" e "está normal" são coisas diferentes.
        var p = await CriarPacienteAsync();

        var medida = await ColherAsync(p, CatalogoMedidas.Peso, 80, Hoje);

        medida.FaixaNome.Should().BeNull();
        medida.TemFaixa.Should().BeFalse();
    }

    [Fact]
    public async Task A_faixa_da_cintura_segue_o_sexo_do_paciente()
    {
        // Os cortes do FINDRISC — a escala que já pergunta esta medida. Usar aqui um corte
        // diferente do dela faria a mesma clínica dar duas leituras da mesma fita métrica.
        var mulher = await CriarPacienteAsync(Sexo.Feminino);
        var homem = await CriarPacienteAsync(Sexo.Masculino);

        var dela = await ColherAsync(mulher, CatalogoMedidas.Cintura, 90, Hoje);
        var dele = await ColherAsync(homem, CatalogoMedidas.Cintura, 90, Hoje);

        dela.FaixaNome.Should().Be("Risco muito aumentado");
        dele.FaixaNome.Should().Be("Dentro da referência");
    }

    // ------------------------------------------------------------ o que se recusa

    [Fact]
    public async Task Valor_implausivel_e_recusado_e_valor_anormal_entra()
    {
        var p = await CriarPacienteAsync();

        // 2500 kg é dedo escorregado no teclado.
        var acao = () => ColherAsync(p, CatalogoMedidas.Peso, 2500, Hoje);
        await acao.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*plausibilidade*");

        // 210 kg é anormal e possível — recusá-lo esconderia justamente o paciente que
        // precisa de atenção.
        var medida = await ColherAsync(p, CatalogoMedidas.Peso, 210, Hoje);
        medida.Valor.Should().Be(210);
    }

    [Fact]
    public async Task Meia_pressao_arterial_e_recusada()
    {
        var p = await CriarPacienteAsync();

        var acao = () => ColherAsync(p, CatalogoMedidas.PressaoArterial, 130, Hoje);

        await acao.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Diastólica*");
    }

    [Fact]
    public async Task Diastolica_maior_que_a_sistolica_e_recusada_como_campo_trocado()
    {
        var p = await CriarPacienteAsync();

        var acao = () => ColherAsync(p, CatalogoMedidas.PressaoArterial, 80, Hoje, segundo: 130);

        await acao.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*trocados*");
    }

    [Fact]
    public async Task O_imc_nao_se_digita()
    {
        // Gravá-lo criaria um terceiro número livre para contradizer o peso e a altura que
        // aparecem na linha de cima, com a mesma cara de fato.
        var p = await CriarPacienteAsync();

        var acao = () => ColherAsync(p, CatalogoMedidas.Imc, 27, Hoje);

        await acao.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*não se digita*");
    }

    [Fact]
    public async Task Medida_de_data_futura_e_recusada()
    {
        var p = await CriarPacienteAsync();

        var acao = () => ColherAsync(
            p, CatalogoMedidas.Peso, 70, DateOnly.FromDateTime(DateTime.Today.AddDays(1)));

        await acao.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*futura*");
    }

    // ------------------------------------------------------------ o IMC derivado

    [Fact]
    public async Task O_imc_sai_do_peso_com_a_altura_e_diz_de_quando_ela_e()
    {
        var p = await CriarPacienteAsync();
        await ColherAsync(p, CatalogoMedidas.Altura, 165, new DateOnly(2024, 1, 10));
        await ColherAsync(p, CatalogoMedidas.Peso, 68, Hoje);

        var resumo = await _medidas.ResumoAsync(p);

        resumo.TemImc.Should().BeTrue();
        resumo.Imc.Should().Be(25.0m);
        resumo.ImcFaixa.Should().Be("Sobrepeso");
        resumo.ImcEm.Should().Be(Hoje);
        // A PROCEDÊNCIA: a altura é de dois anos atrás, e quem lê precisa saber.
        resumo.AlturaUsadaEm.Should().Be(new DateOnly(2024, 1, 10));
    }

    [Fact]
    public async Task Sem_altura_nao_ha_imc_e_isso_nao_e_zero()
    {
        var p = await CriarPacienteAsync();
        await ColherAsync(p, CatalogoMedidas.Peso, 68, Hoje);

        var resumo = await _medidas.ResumoAsync(p);

        resumo.TemImc.Should().BeFalse();
        resumo.Imc.Should().BeNull();
    }

    [Fact]
    public async Task A_curva_do_imc_usa_a_altura_vigente_de_cada_peso()
    {
        // Adolescente que cresceu no meio do tratamento: usar a altura de hoje para o peso
        // do ano passado daria um IMC que nunca existiu.
        var p = await CriarPacienteAsync();
        await ColherAsync(p, CatalogoMedidas.Altura, 150, new DateOnly(2025, 1, 1));
        await ColherAsync(p, CatalogoMedidas.Peso, 45, new DateOnly(2025, 2, 1));
        await ColherAsync(p, CatalogoMedidas.Altura, 160, new DateOnly(2026, 1, 1));
        await ColherAsync(p, CatalogoMedidas.Peso, 45, new DateOnly(2026, 2, 1));

        var serie = await _medidas.SerieAsync(p, CatalogoMedidas.Imc);

        serie.Pontos.Should().HaveCount(2);
        serie.Pontos[0].Valor.Should().Be(20.0m);   // 45 / 1,50²
        serie.Pontos[1].Valor.Should().Be(17.6m);   // 45 / 1,60²
        // Todo ponto do IMC é DERIVADO, e a tela precisa dizer isso.
        serie.Pontos.Should().OnlyContain(pt => pt.Derivado);
    }

    [Fact]
    public async Task Altura_medida_depois_do_peso_ainda_serve_ao_imc()
    {
        // O caso comum: o adulto é pesado toda consulta e medido uma vez, muitas vezes
        // depois. Devolver curva vazia justamente para quem tem os dois dados seria pior
        // que usar uma altura de adulto colhida um mês adiante.
        var p = await CriarPacienteAsync();
        await ColherAsync(p, CatalogoMedidas.Peso, 80, new DateOnly(2026, 6, 1));
        await ColherAsync(p, CatalogoMedidas.Altura, 180, new DateOnly(2026, 7, 1));

        var serie = await _medidas.SerieAsync(p, CatalogoMedidas.Imc);

        serie.Pontos.Should().HaveCount(1);
        serie.Pontos[0].Valor.Should().Be(24.7m);
    }

    // ------------------------------------------------------------ a série

    [Fact]
    public async Task Uma_colheita_so_nao_e_variacao_nenhuma()
    {
        // Mostrar 0 diria que o paciente estacionou — e ele nem foi medido duas vezes.
        var p = await CriarPacienteAsync();
        await ColherAsync(p, CatalogoMedidas.Peso, 80, Hoje);

        var serie = await _medidas.SerieAsync(p, CatalogoMedidas.Peso);

        serie.TemDados.Should().BeTrue();
        serie.Variacao.Should().BeNull();
        serie.Melhorou.Should().BeNull();
    }

    [Fact]
    public async Task A_serie_sai_em_ordem_cronologica_e_le_a_direcao_da_variacao()
    {
        var p = await CriarPacienteAsync();
        await ColherAsync(p, CatalogoMedidas.Peso, 88, new DateOnly(2026, 1, 10));
        await ColherAsync(p, CatalogoMedidas.Peso, 84, new DateOnly(2026, 4, 10));
        await ColherAsync(p, CatalogoMedidas.Peso, 81, new DateOnly(2026, 7, 10));

        var serie = await _medidas.SerieAsync(p, CatalogoMedidas.Peso);

        serie.Pontos.Select(x => x.Valor).Should().ContainInOrder(88m, 84m, 81m);
        serie.Primeiro.Should().Be(88);
        serie.Atual.Should().Be(81);
        serie.Variacao.Should().Be(-7);
        serie.Melhorou.Should().BeTrue();
    }

    [Fact]
    public async Task Cancelar_tira_da_serie_e_guarda_a_linha()
    {
        var p = await CriarPacienteAsync();
        var medida = await ColherAsync(p, CatalogoMedidas.Peso, 80, Hoje);

        await _medidas.CancelarAsync(medida.Id, "dedo no teclado", "dra.ana");

        // Fora da curva, dentro do banco: sem o registro da retirada não há como
        // distinguir "não foi pesado" de "alguém apagou o peso que não gostou".
        (await _medidas.DoPacienteAsync(p)).Should().BeEmpty();
        _db.MedidasClinicas.Single(m => m.Id == medida.Id)
            .MotivoCancelamento.Should().Be("dedo no teclado");
        _db.Auditoria.Should().Contain(e => e.Acao == "MedidaCancelada");
    }

    // ------------------------------------------------------------ o catálogo

    [Fact]
    public void O_catalogo_de_colheita_nao_oferece_o_que_e_derivado()
    {
        MedidaClinicaService.Registraveis.Should()
            .NotContain(t => t.Codigo == CatalogoMedidas.Imc);
        CatalogoMedidas.TodasAsMedidas.Should()
            .Contain(t => t.Codigo == CatalogoMedidas.Imc);
    }

    [Fact]
    public void Tipo_fora_do_catalogo_devolve_nulo_em_vez_de_estourar()
    {
        // A medida gravada continua legível sem ele: nome, unidade e faixa foram COPIADOS.
        CatalogoMedidas.Obter("NAOEXISTE").Should().BeNull();
    }

    public void Dispose()
    {
        _db.Dispose();
        _conn.Dispose();
        GC.SuppressFinalize(this);
    }
}
