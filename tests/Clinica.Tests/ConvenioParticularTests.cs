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
/// O PARTICULAR — o paciente que vem sem convênio (parcela 60).
///
/// Até aqui ele não tinha onde ser cadastrado: o enum <see cref="Convenio"/> não tem "sem
/// convênio", e as duas saídas que sobravam eram ruins de jeitos diferentes.
///
/// 1. Cadastrá-lo sob um convênio qualquer gera uma guia com data prevista, que entra no
///    painel de pendências, vence o prazo de decisão e abre a <b>rodada BLOQUEANTE</b> —
///    travando a tela de quem fatura por uma guia que nunca vai a operadora nenhuma.
/// 2. Não cadastrar o atendimento, e aí a sessão não existe em lugar nenhum: nem guia, nem
///    prontuário, nem caixa.
///
/// A saída é um sinalizador no CADASTRO do convênio (<c>ConvenioCadastro.GeraGuia</c>): os
/// códigos continuam nascendo, marcados <see cref="StatusCodigo.NaoAplicavel"/>.
/// </summary>
public class ConvenioParticularTests : IDisposable
{
    private const string CodigoParticular = "Particular";

    private readonly SqliteConnection _conn;
    private readonly ClinicaDbContext _db;
    private readonly ClinicaRepositorio _repo;
    private readonly AtendimentoService _atendimentos;

    public ConvenioParticularTests()
    {
        _conn = new SqliteConnection("DataSource=:memory:");
        _conn.Open();
        var options = new DbContextOptionsBuilder<ClinicaDbContext>().UseSqlite(_conn).Options;
        _db = new ClinicaDbContext(options);
        _db.Database.EnsureCreated();
        _repo = new ClinicaRepositorio(_db);
        _atendimentos = new AtendimentoService(_repo);

        // O catálogo é um cache estático: cada teste monta o seu.
        CatalogoConvenios.Atualizar(
        [
            new EntradaConvenio(CodigoParticular, "Particular", Convenio.Personalizado, true,
                new ConfiguracaoRegraGenerica { FazEletro = true, TemSegundoCodigo = true },
                FormatoNumeroGuia.SemValidacao,
                GeraGuia: false),

            new EntradaConvenio("Amil", "Amil", Convenio.Amil, true, GeraGuia: true)
        ]);
    }

    public void Dispose()
    {
        CatalogoConvenios.Atualizar([]);
        _db.Dispose();
        _conn.Dispose();
        GC.SuppressFinalize(this);
    }

    private async Task<int> PacienteAsync(string? convenioCodigo, Convenio familia)
    {
        var p = new Paciente
        {
            Nome = "Paciente",
            Convenio = familia,
            ConvenioCodigo = convenioCodigo,
            Sexo = Sexo.Feminino
        };
        _db.Pacientes.Add(p);
        await _db.SaveChangesAsync();
        return p.Id;
    }

    private static readonly DateOnly Dia = new(2026, 8, 11);

    // ================================================================
    // O QUE O PARTICULAR FAZ
    // ================================================================

    /// <summary>
    /// A sessão fica registrada. É a metade que a alternativa "não cadastrar" perdia — sem
    /// o atendimento, a clínica não sabe quantas acupunturas fez, só quantas faturou.
    /// </summary>
    [Fact]
    public async Task Particular_registra_o_atendimento_com_os_codigos()
    {
        var id = await PacienteAsync(CodigoParticular, Convenio.Personalizado);

        var r = await _atendimentos.LancarAsync(
            id, Dia, ModalidadeAtendimento.AcupunturaComEletro);

        r.Atendimento.Codigos.Should().NotBeEmpty(
            "o invariante 'não há atendimento sem código' é o que prova que LancarAsync "
            + "continua sendo ponto único");
        r.Atendimento.Codigos.Should().OnlyContain(c => c.Status == StatusCodigo.NaoAplicavel);
    }

    /// <summary>
    /// O que a parcela existe para impedir: a guia do particular <b>não</b> vira pendência,
    /// e por consequência não vence prazo nem abre a rodada bloqueante.
    /// </summary>
    [Fact]
    public async Task Particular_nao_entra_em_pendencia_nem_na_rodada()
    {
        var id = await PacienteAsync(CodigoParticular, Convenio.Personalizado);

        var r = await _atendimentos.LancarAsync(
            id, Dia, ModalidadeAtendimento.AcupunturaComEletro);

        // Um ano depois: se algum código continuasse "Aberto", ele estaria pendente há 365
        // dias e a abertura do faturamento estaria travada por ele.
        var muitoDepois = Dia.AddDays(365);
        r.Atendimento.Codigos.Should().OnlyContain(c => !c.EstaPendente(muitoDepois));
        r.Atendimento.Codigos.Should()
            .OnlyContain(c => !c.PrazoDecisaoVencido(muitoDepois, 10, null));
    }

    /// <summary>
    /// O aviso do motor fala de faturamento ("2º código não é possível", "não fatura BSV").
    /// Para um particular isso não quer dizer nada — e quem lê é a recepcionista, com o
    /// paciente na frente dela.
    /// </summary>
    [Fact]
    public async Task Particular_diz_o_que_e_em_vez_de_avisar_sobre_guia()
    {
        var id = await PacienteAsync(CodigoParticular, Convenio.Personalizado);

        var r = await _atendimentos.LancarAsync(
            id, Dia, ModalidadeAtendimento.AcupunturaComEletro);

        r.Avisos.Should().ContainSingle().Which.Should().Contain("PARTICULAR");
    }

    /// <summary>
    /// ⚠️ A PRÉVIA e o LANÇAMENTO têm de concordar — a mesma regra que
    /// <c>Previa_promete_exatamente_o_que_o_lancamento_entrega</c> fixa desde a parcela 47.
    ///
    /// É por isso que a marcação mora no <c>AtendimentoService</c> e não dentro das regras:
    /// os dois caminhos passam por ali. Prévia dizendo "1 guia" para um particular seria
    /// prometer uma cobrança que não vai existir.
    /// </summary>
    [Fact]
    public async Task Previa_do_particular_bate_com_o_lancamento()
    {
        var id = await PacienteAsync(CodigoParticular, Convenio.Personalizado);

        var previa = await _atendimentos.PreverAsync(
            id, Dia, ModalidadeAtendimento.AcupunturaComEletro);

        previa.GuiasHoje.Should().Be(0);
        previa.GuiasDepois.Should().Be(0);
        previa.LiberaEm.Should().BeNull();

        var real = await _atendimentos.LancarAsync(
            id, Dia, ModalidadeAtendimento.AcupunturaComEletro);

        previa.Codigos.Count.Should().Be(real.Atendimento.Codigos.Count);
        real.Atendimento.Codigos.Count(c => c.EstaPendente(Dia)).Should().Be(previa.GuiasHoje);
    }

    // ================================================================
    // O QUE ELE NÃO PODE AFETAR
    // ================================================================

    /// <summary>
    /// Convênio de verdade continua gerando guia. Parece óbvio, e é a asserção mais
    /// importante do arquivo: a migration desta parcela acrescenta uma coluna
    /// não-anulável, e o EF gera <c>defaultValue: false</c> por padrão — aplicada assim,
    /// ela desligaria a guia de TODOS os convênios já cadastrados, em silêncio.
    /// </summary>
    [Fact]
    public async Task Convenio_de_verdade_continua_gerando_guia()
    {
        var id = await PacienteAsync("Amil", Convenio.Amil);

        var r = await _atendimentos.LancarAsync(
            id, Dia, ModalidadeAtendimento.AcupunturaComEletro);

        r.Atendimento.Codigos.Should().Contain(c => c.Status != StatusCodigo.NaoAplicavel);
        r.Atendimento.Codigos.Should().Contain(c => c.EstaPendente(Dia.AddDays(2)));
    }

    /// <summary>
    /// ⚠️ Código FORA do catálogo é presumido faturável, e a assimetria é deliberada.
    ///
    /// O erro nesse sentido é uma guia a mais para conferir. No sentido contrário, seria o
    /// sistema parar de gerar guia para um convênio de verdade — em silêncio, e só
    /// descoberto quando a clínica fosse faturar o mês. É o mesmo raciocínio do formato do
    /// número da guia: na dúvida, não recuse o que é legítimo.
    /// </summary>
    [Fact]
    public void Convenio_desconhecido_e_presumido_faturavel()
    {
        CatalogoConvenios.GeraGuia("NaoExisteNoCatalogo").Should().BeTrue();
        CatalogoConvenios.GeraGuia(null).Should().BeTrue();
        CatalogoConvenios.GeraGuia(CodigoParticular).Should().BeFalse();
    }

    /// <summary>
    /// O sinalizador é do CADASTRO, não da regra genérica — ele vale para QUALQUER família.
    /// Dentro de <c>ConfiguracaoRegraGenerica</c> ele seria uma caixinha que não faz nada
    /// num convênio embutido, que é o defeito do campo morto (parcela 49).
    /// </summary>
    [Fact]
    public async Task O_sinalizador_vale_para_familia_embutida_tambem()
    {
        CatalogoConvenios.Atualizar(
        [
            new EntradaConvenio("AmilCortesia", "Amil (cortesia)", Convenio.Amil, true,
                GeraGuia: false)
        ]);

        var id = await PacienteAsync("AmilCortesia", Convenio.Amil);

        var r = await _atendimentos.LancarAsync(
            id, Dia, ModalidadeAtendimento.AcupunturaComEletro);

        r.Atendimento.Codigos.Should().NotBeEmpty();
        r.Atendimento.Codigos.Should().OnlyContain(c => c.Status == StatusCodigo.NaoAplicavel);
    }
}
