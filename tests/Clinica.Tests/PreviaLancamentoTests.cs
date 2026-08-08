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
/// A PRÉVIA do lançamento (parcela 47): o que a regra do convênio vai gerar, mostrado antes
/// de gerar.
///
/// O que estes testes protegem
/// ---------------------------
/// Uma prévia que não bate com o lançamento é PIOR do que não ter prévia: ela promete duas
/// guias, a pessoa confirma, e sai uma. O primeiro teste é o que importa — prévia e
/// lançamento têm de produzir exatamente os mesmos códigos, nas mesmas datas.
///
/// O segundo risco é a prévia GRAVAR alguma coisa. Ela roda a cada troca de modalidade, e
/// a recepcionista compara três opções antes de decidir: se ela tivesse efeito, o paciente
/// teria três atendimentos, três consultas renovadas e três linhas na agenda.
/// </summary>
public class PreviaLancamentoTests : IDisposable
{
    private readonly SqliteConnection _conn;
    private readonly ClinicaDbContext _db;
    private readonly ClinicaRepositorio _repo;

    public PreviaLancamentoTests()
    {
        _conn = new SqliteConnection("DataSource=:memory:");
        _conn.Open();
        var options = new DbContextOptionsBuilder<ClinicaDbContext>().UseSqlite(_conn).Options;
        _db = new ClinicaDbContext(options);
        _db.Database.EnsureCreated();
        _repo = new ClinicaRepositorio(_db);
    }

    /// <summary>
    /// O teste central: o que a prévia promete é o que o lançamento entrega — mesmo tipo,
    /// mesma ordem, mesma data prevista, mesma forma de obtenção.
    /// </summary>
    [Fact]
    public async Task Previa_promete_exatamente_o_que_o_lancamento_entrega()
    {
        var paciente = await SemearAsync(Convenio.UnimedIntercambio);
        var servico = new AtendimentoService(_repo);
        var data = new DateOnly(2026, 8, 8);

        var previa = await servico.PreverAsync(
            paciente.Id, data, ModalidadeAtendimento.AcupunturaComEletro);

        var lancado = await servico.LancarAsync(
            paciente.Id, data, ModalidadeAtendimento.AcupunturaComEletro);

        previa.Codigos
            .Select(c => new { c.Tipo, c.Ordem, c.DataPrevistaFaturamento, c.FormaObtencao })
            .Should().BeEquivalentTo(lancado.Atendimento.Codigos
                .Select(c => new { c.Tipo, c.Ordem, c.DataPrevistaFaturamento, c.FormaObtencao }));

        previa.Categoria.Should().Be(lancado.Atendimento.Categoria);
    }

    /// <summary>
    /// A prévia NÃO grava. Ela roda a cada troca de modalidade — e a recepcionista compara
    /// as opções antes de decidir.
    /// </summary>
    [Fact]
    public async Task Previa_nao_grava_nada()
    {
        var paciente = await SemearAsync(Convenio.UnimedIntercambio);
        var servico = new AtendimentoService(_repo);

        for (var i = 0; i < 3; i++)
            await servico.PreverAsync(
                paciente.Id, new DateOnly(2026, 8, 8), ModalidadeAtendimento.AcupunturaComEletro);

        (await _db.Atendimentos.CountAsync()).Should().Be(0);
        (await _db.Codigos.CountAsync()).Should().Be(0);
        (await _db.Agendamentos.CountAsync()).Should().Be(0);
    }

    /// <summary>
    /// O 2º código é o assunto do produto, e a prévia tem de dizer a DATA dele antes de ele
    /// existir — é o que faz a recepcionista saber, no momento da escolha, que este
    /// atendimento deixa trabalho para depois de amanhã.
    /// </summary>
    [Fact]
    public async Task Previa_anuncia_o_segundo_codigo_com_a_data()
    {
        var paciente = await SemearAsync(Convenio.UnimedIntercambio);

        var previa = await new AtendimentoService(_repo).PreverAsync(
            paciente.Id, new DateOnly(2026, 8, 8), ModalidadeAtendimento.AcupunturaComEletro);

        previa.GuiasHoje.Should().Be(1);
        previa.GuiasDepois.Should().Be(1);
        previa.LiberaEm.Should().Be(new DateOnly(2026, 8, 9), "o 2º código nasce previsto para +24h");
    }

    /// <summary>
    /// Modalidade simples não deixa nada para depois — e a tela precisa saber dizer isso
    /// ("tudo hoje"), não só omitir.
    /// </summary>
    [Fact]
    public async Task Modalidade_simples_nao_deixa_guia_para_depois()
    {
        var paciente = await SemearAsync(Convenio.UnimedIntercambio);

        var previa = await new AtendimentoService(_repo).PreverAsync(
            paciente.Id, new DateOnly(2026, 8, 8), ModalidadeAtendimento.AcupunturaSimples);

        previa.GuiasDepois.Should().Be(0);
        previa.LiberaEm.Should().BeNull();
    }

    /// <summary>
    /// A prévia de VÁRIAS modalidades — é ela que escreve a consequência em cada cartão da
    /// tela. Uma leitura só de banco, N simulações em memória.
    /// </summary>
    [Fact]
    public async Task Previa_de_varias_modalidades_responde_por_todas()
    {
        var paciente = await SemearAsync(Convenio.UnimedIntercambio);

        var previas = await new AtendimentoService(_repo).PreverModalidadesAsync(
            paciente.Id, new DateOnly(2026, 8, 8),
            ["AcupunturaSimples", "AcupunturaComEletro"]);

        previas.Should().ContainKeys("AcupunturaSimples", "AcupunturaComEletro");
        previas["AcupunturaComEletro"].GuiasDepois.Should().Be(1);
        previas["AcupunturaSimples"].GuiasDepois.Should().Be(0);

        // E continua sem gravar, mesmo simulando várias.
        (await _db.Atendimentos.CountAsync()).Should().Be(0);
    }

    /// <summary>
    /// O convênio MUDA a resposta: a mesma modalidade não gera o mesmo em toda operadora.
    /// É por isso que a frase do cartão sai do motor de regras e não de um texto fixo — um
    /// texto decorado envelheceria no dia em que a regra de um convênio mudasse.
    /// </summary>
    [Fact]
    public async Task O_convenio_do_paciente_muda_a_previa()
    {
        var intercambio = await SemearAsync(Convenio.UnimedIntercambio);
        var amil = await SemearAsync(Convenio.Amil);
        var servico = new AtendimentoService(_repo);
        var data = new DateOnly(2026, 8, 8);

        var pIntercambio = await servico.PreverAsync(
            intercambio.Id, data, ModalidadeAtendimento.AcupunturaComEletro);
        var pAmil = await servico.PreverAsync(
            amil.Id, data, ModalidadeAtendimento.AcupunturaComEletro);

        pIntercambio.GuiasDepois.Should().Be(1, "o Intercâmbio gera o 2º código em +24h");
        pAmil.GuiasDepois.Should().Be(0, "a Amil não faz eletro nem 2º código");
    }

    /// <summary>
    /// O que os CARTÕES da tela têm de mostrar para a Unimed Intercâmbio — o caso exato
    /// que o cliente fotografou com o número errado.
    ///
    /// Na tela apareceu "Acupuntura + eletroacupuntura · 1 guia · tudo hoje" e
    /// "BSV + acupuntura · 2 guias · tudo hoje". As duas frases estão erradas e, pior,
    /// se contradizem: o motor gera DOIS códigos nas duas modalidades, e o segundo só
    /// libera em +24h. O defeito não era do motor — era a tela escrevendo o resultado de
    /// uma leitura velha —, e este teste fixa a resposta certa para que a próxima
    /// divergência apareça aqui, e não numa captura de tela.
    /// </summary>
    [Theory]
    [InlineData("AcupunturaComEletro")]
    [InlineData("BsvComAcupuntura")]
    public async Task Modalidade_dupla_no_Intercambio_gera_duas_guias_e_a_segunda_libera_depois(
        string modalidade)
    {
        var paciente = await SemearAsync(Convenio.UnimedIntercambio);
        var data = new DateOnly(2026, 8, 8);

        var previas = await new AtendimentoService(_repo)
            .PreverModalidadesAsync(paciente.Id, data, [modalidade]);

        var p = previas[modalidade];

        (p.GuiasHoje + p.GuiasDepois).Should().Be(2, "o cartão precisa dizer \"2 guias\"");
        p.GuiasDepois.Should().Be(1, "uma delas só libera depois");
        p.LiberaEm.Should().Be(data.AddDays(1),
            "sem isto o cartão diz \"tudo hoje\" sobre a guia que é o assunto do produto");
    }

    private async Task<Paciente> SemearAsync(Convenio convenio)
    {
        var paciente = new Paciente
        {
            Nome = $"Paciente {convenio}",
            Convenio = convenio,
            Sexo = Sexo.Feminino,
            PossuiApp = true
        };
        _db.Add(paciente);
        await _db.SaveChangesAsync();
        return paciente;
    }

    public void Dispose()
    {
        _db.Dispose();
        _conn.Dispose();
    }
}
