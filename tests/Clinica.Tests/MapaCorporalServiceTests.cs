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
/// Mapa corporal e protocolos reutilizáveis (parcela 3).
///
/// A regra que carrega a suíte: aplicar um protocolo é COPIAR pontos para a sessão,
/// nunca apontar para ele. Se fosse referência, editar o mapa de hoje reescreveria o
/// protocolo da clínica — e, pior, a sessão da semana passada.
/// </summary>
public class MapaCorporalServiceTests : IDisposable
{
    private readonly SqliteConnection _conn;
    private readonly ClinicaDbContext _db;
    private readonly ClinicaRepositorio _repo;
    private readonly ProntuarioService _prontuario;
    private readonly MapaCorporalService _mapas;

    private static readonly DateOnly Dia = new(2026, 8, 10);

    public MapaCorporalServiceTests()
    {
        _conn = new SqliteConnection("DataSource=:memory:");
        _conn.Open();
        var options = new DbContextOptionsBuilder<ClinicaDbContext>().UseSqlite(_conn).Options;
        _db = new ClinicaDbContext(options);
        _db.Database.EnsureCreated();
        _repo = new ClinicaRepositorio(_db);
        _prontuario = new ProntuarioService(_repo);
        _mapas = new MapaCorporalService(_repo);
    }

    private async Task<int> CriarPacienteAsync(string nome = "Paciente")
    {
        var p = new Paciente { Nome = nome, Convenio = Convenio.UnimedIntercambio, Sexo = Sexo.Feminino };
        _db.Pacientes.Add(p);
        await _db.SaveChangesAsync();
        return p.Id;
    }

    private async Task<int> CriarSessaoAsync(int pacienteId, DateOnly data)
    {
        var evolucao = await _prontuario.SalvarAsync(new Evolucao
        {
            PacienteId = pacienteId,
            Data = data,
            QueixaPrincipal = "dor lombar"
        }, "secretaria");
        return evolucao.Id;
    }

    private static PontoMapa Ponto(double x, double y, FaceCorpo face = FaceCorpo.Frente,
        string? nome = null, TecnicaPonto tecnica = TecnicaPonto.Agulha)
        => new() { X = x, Y = y, Face = face, Nome = nome, Tecnica = tecnica };

    [Fact]
    public async Task Salvar_grava_os_pontos_numerados_na_ordem_da_marcacao()
    {
        var pacienteId = await CriarPacienteAsync();
        var sessaoId = await CriarSessaoAsync(pacienteId, Dia);

        await _mapas.SalvarAsync(sessaoId, new[]
        {
            Ponto(0.5, 0.3, nome: "VG20"),
            Ponto(0.4, 0.6, FaceCorpo.Costas, "B23", TecnicaPonto.Ventosa)
        }, observacoes: "sessão tranquila", operador: "secretaria");

        var mapa = await _mapas.DaEvolucaoAsync(sessaoId);

        mapa.Should().NotBeNull();
        mapa!.Observacoes.Should().Be("sessão tranquila");
        mapa.Pontos.Should().HaveCount(2);
        mapa.Pontos.OrderBy(p => p.Ordem).Select(p => p.Ordem).Should().Equal(1, 2);
        mapa.Pontos.Single(p => p.Ordem == 2).Face.Should().Be(FaceCorpo.Costas);
        mapa.Pontos.Single(p => p.Ordem == 2).Tecnica.Should().Be(TecnicaPonto.Ventosa);
    }

    [Fact]
    public async Task Salvar_de_novo_substitui_os_pontos_em_vez_de_acumular()
    {
        var pacienteId = await CriarPacienteAsync();
        var sessaoId = await CriarSessaoAsync(pacienteId, Dia);

        await _mapas.SalvarAsync(sessaoId, new[] { Ponto(0.5, 0.3), Ponto(0.5, 0.4) });
        await _mapas.SalvarAsync(sessaoId, new[] { Ponto(0.2, 0.8) });

        var mapa = await _mapas.DaEvolucaoAsync(sessaoId);

        mapa!.Pontos.Should().HaveCount(1);
        mapa.Pontos.Single().X.Should().Be(0.2);
        // O mapa é da sessão, não uma coleção que cresce: continua existindo UM registro.
        _db.MapasCorporais.Count(m => m.EvolucaoId == sessaoId).Should().Be(1);
    }

    [Fact]
    public async Task Ponto_fora_da_figura_e_recusado()
    {
        var pacienteId = await CriarPacienteAsync();
        var sessaoId = await CriarSessaoAsync(pacienteId, Dia);

        var marcar = () => _mapas.SalvarAsync(sessaoId, new[] { Ponto(1.4, 0.2) });

        await marcar.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*fora da figura*");
    }

    [Fact]
    public async Task Protocolo_aplicado_copia_os_pontos_e_a_edicao_nao_volta_para_ele()
    {
        var pacienteId = await CriarPacienteAsync();
        var primeira = await CriarSessaoAsync(pacienteId, Dia);

        var protocolo = await _mapas.SalvarComoProtocoloAsync(
            pacienteId, "Lombalgia — padrão",
            new[] { Ponto(0.5, 0.5, nome: "B23"), Ponto(0.6, 0.5, nome: "B25") },
            daClinica: true, operador: "secretaria");

        await _mapas.AplicarProtocoloAsync(primeira, protocolo.Id, "secretaria");

        var mapa = await _mapas.DaEvolucaoAsync(primeira);
        mapa!.Pontos.Should().HaveCount(2);
        mapa.ProtocoloOrigemId.Should().Be(protocolo.Id);

        // Sessão editada depois: o protocolo continua com os dois pontos originais.
        await _mapas.SalvarAsync(primeira, new[] { Ponto(0.1, 0.1) });

        var recarregado = await _mapas.ObterProtocoloAsync(protocolo.Id);
        recarregado!.Pontos.Should().HaveCount(2);
        recarregado.Pontos.Select(p => p.Nome).Should().BeEquivalentTo(new[] { "B23", "B25" });
    }

    [Fact]
    public async Task Protocolo_do_paciente_nao_aparece_para_outro_e_o_da_clinica_aparece_para_todos()
    {
        var ana = await CriarPacienteAsync("Ana");
        var bruno = await CriarPacienteAsync("Bruno");

        await _mapas.SalvarComoProtocoloAsync(
            ana, "Esquema da Ana", new[] { Ponto(0.5, 0.5) }, daClinica: false);
        await _mapas.SalvarComoProtocoloAsync(
            ana, "Cervicalgia — padrão", new[] { Ponto(0.5, 0.2) }, daClinica: true);

        var deAna = await _mapas.ProtocolosAsync(ana);
        var deBruno = await _mapas.ProtocolosAsync(bruno);

        deAna.Select(p => p.Nome).Should().BeEquivalentTo(
            new[] { "Esquema da Ana", "Cervicalgia — padrão" });
        deBruno.Select(p => p.Nome).Should().Equal("Cervicalgia — padrão");
    }

    [Fact]
    public async Task Repetir_a_sessao_anterior_traz_o_mapa_mais_recente_que_veio_antes()
    {
        var pacienteId = await CriarPacienteAsync();

        var maisAntiga = await CriarSessaoAsync(pacienteId, Dia.AddDays(-14));
        var doMeio = await CriarSessaoAsync(pacienteId, Dia.AddDays(-7));
        var hoje = await CriarSessaoAsync(pacienteId, Dia);

        await _mapas.SalvarAsync(maisAntiga, new[] { Ponto(0.1, 0.1, nome: "antigo") });
        await _mapas.SalvarAsync(doMeio, new[] { Ponto(0.9, 0.9, nome: "recente") });

        var pontos = await _mapas.PontosDaSessaoAnteriorAsync(pacienteId, hoje);

        pontos.Should().HaveCount(1);
        pontos.Single().Nome.Should().Be("recente");
    }

    [Fact]
    public async Task Repetir_pula_as_sessoes_que_nao_tem_mapa()
    {
        var pacienteId = await CriarPacienteAsync();

        var comMapa = await CriarSessaoAsync(pacienteId, Dia.AddDays(-20));
        await CriarSessaoAsync(pacienteId, Dia.AddDays(-10)); // sem mapa nenhum
        var hoje = await CriarSessaoAsync(pacienteId, Dia);

        await _mapas.SalvarAsync(comMapa, new[] { Ponto(0.3, 0.3, nome: "único") });

        var pontos = await _mapas.PontosDaSessaoAnteriorAsync(pacienteId, hoje);

        pontos.Single().Nome.Should().Be("único");
    }

    [Fact]
    public async Task Sem_sessao_anterior_com_mapa_a_copia_nao_inventa_nada()
    {
        var pacienteId = await CriarPacienteAsync();
        var hoje = await CriarSessaoAsync(pacienteId, Dia);

        (await _mapas.PontosDaSessaoAnteriorAsync(pacienteId, hoje)).Should().BeEmpty();

        var copiar = () => _mapas.CopiarDaSessaoAnteriorAsync(hoje);
        await copiar.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Nenhuma sessão anterior*");
    }

    [Fact]
    public async Task Cancelar_a_sessao_NAO_apaga_o_mapa()
    {
        var pacienteId = await CriarPacienteAsync();
        var sessaoId = await CriarSessaoAsync(pacienteId, Dia);
        await _mapas.SalvarAsync(sessaoId, new[] { Ponto(0.5, 0.5) });

        await _prontuario.CancelarAsync(sessaoId, "sessão duplicada", "secretaria");

        // O mapa é 1:1 com a evolução e a acompanha: cancelada a sessão, ele fica
        // guardado com ela pelos 20 anos da Lei 13.787/2018.
        (await _mapas.DaEvolucaoAsync(sessaoId)).Should().NotBeNull();
    }

    [Fact]
    public async Task Mapa_salvo_deixa_rastro_na_auditoria()
    {
        var pacienteId = await CriarPacienteAsync();
        var sessaoId = await CriarSessaoAsync(pacienteId, Dia);

        await _mapas.SalvarAsync(sessaoId, new[] { Ponto(0.5, 0.5) }, operador: "secretaria");

        var eventos = await _repo.EventosAuditoriaAsync();
        eventos.Should().Contain(e => e.Acao == "MapaCorporalRegistrado" && e.PacienteId == pacienteId);
    }

    public void Dispose()
    {
        _db.Dispose();
        _conn.Dispose();
        GC.SuppressFinalize(this);
    }
}
