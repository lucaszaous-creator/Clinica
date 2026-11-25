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
/// O que cada papel exige da ficha (nov/2026, "o mesmo modus operandi do receituário" em todos os documentos): uma regra
/// só para a folha e o portal. Dados fictícios.
/// </summary>
public sealed class DadosDoPacienteNoDocumentoTests : IDisposable
{
    private readonly SqliteConnection _conn = new("DataSource=:memory:");
    private readonly ClinicaDbContext _db;
    private readonly ClinicaRepositorio _repo;

    public DadosDoPacienteNoDocumentoTests()
    {
        _conn.Open();
        _db = new ClinicaDbContext(new DbContextOptionsBuilder<ClinicaDbContext>().UseSqlite(_conn).Options);
        _db.Database.EnsureCreated();
        _repo = new ClinicaRepositorio(_db);
    }

    public void Dispose() { _db.Dispose(); _conn.Dispose(); }

    [Fact]
    public void Cada_papel_pede_so_o_que_exige()
    {
        var semNada = new Paciente { Nome = "Paciente Fictícia" };
        DadosDoPacienteNoDocumento.Faltas(TipoDocumentoClinico.Receita, semNada).Select(f => f.Chave).Should().Equal("endereco");
        DadosDoPacienteNoDocumento.Faltas(TipoDocumentoClinico.ReceituarioControleEspecial, semNada).Select(f => f.Chave)
            .Should().Equal("rg", "orgao", "endereco", "cidade", "uf", "telefone");
        foreach (var tipo in new[] { TipoDocumentoClinico.Atestado, TipoDocumentoClinico.Comparecimento, TipoDocumentoClinico.PedidoExame,
                     TipoDocumentoClinico.LaudoMedico, TipoDocumentoClinico.RelatorioEvolucao, TipoDocumentoClinico.Anamnese })
            DadosDoPacienteNoDocumento.Faltas(tipo, semNada).Should().BeEmpty($"{tipo} não exige nada além do nome");

        DadosDoPacienteNoDocumento.Faltas(TipoDocumentoClinico.Receita, new Paciente { Nome = "X", Endereco = "Rua Fictícia, 1" }).Should().BeEmpty();
        DadosDoPacienteNoDocumento.Faltas(TipoDocumentoClinico.Receita, null).Should().BeEmpty();
    }

    [Fact]
    public async Task A_receita_completa_o_endereco_e_nao_grava_o_que_nao_pediu()
    {
        var paciente = new Paciente { Nome = "Paciente Fictícia", Convenio = Convenio.UnimedIntercambio };
        _db.Add(paciente);
        await _db.SaveChangesAsync();

        await new PacienteService(_repo).CompletarParaDocumentoAsync(paciente.Id, TipoDocumentoClinico.Receita,
            new PacienteService.DadosDoComprador("00.000.000-0", "DETRAN-RJ", "Rua Fictícia, 10, Centro", "Macaé", "RJ", null), "medica.teste");

        _db.ChangeTracker.Clear();
        var salvo = (await _repo.ObterPacienteAsync(paciente.Id))!;
        salvo.Endereco.Should().Be("Rua Fictícia, 10, Centro");
        salvo.Rg.Should().BeNull("a receita não pede RG: o que veio a mais não entra na ficha");
        salvo.Cidade.Should().BeNull();
        var evento = await _db.Set<EventoAuditoria>().SingleAsync();
        evento.Acao.Should().Be("DadosPacienteCompletadosNaEmissao");
        evento.Detalhe.Should().Contain("Receita").And.Contain("endereço").And.NotContain("Rua Fictícia");
    }

    [Fact]
    public void Aviso_da_folha_fala_do_paciente_ou_do_comprador()
    {
        EmissaoDeDocumentoNaTela.PacientePendente(TipoDocumentoClinico.Receita, ["Endereço completo"])
            .Should().Be("Falta na ficha do paciente: Endereço completo. Complete para emitir receita.");
        EmissaoDeDocumentoNaTela.PacientePendente(TipoDocumentoClinico.ReceituarioControleEspecial, ["RG", "UF"])
            .Should().StartWith("Faltam na ficha do comprador: RG e UF.");
    }
}
