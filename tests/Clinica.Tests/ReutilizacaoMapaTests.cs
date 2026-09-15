using Clinica.Application.Servicos;
using Clinica.Domain;
using Clinica.Domain.Entities;
using Clinica.Infrastructure;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Xunit;

namespace Clinica.Tests;

public sealed class ReutilizacaoMapaTests : IDisposable
{
    private readonly SqliteConnection _conn = new("DataSource=:memory:");
    private readonly ClinicaDbContext _db;
    private readonly MapaCorporalService _mapas;
    private readonly ProntuarioService _prontuario;
    private static readonly DateOnly Dia = new(2026, 9, 15);

    public ReutilizacaoMapaTests()
    {
        _conn.Open();
        _db = new ClinicaDbContext(new DbContextOptionsBuilder<ClinicaDbContext>().UseSqlite(_conn).Options);
        _db.Database.EnsureCreated();
        var repo = new ClinicaRepositorio(_db);
        _mapas = new MapaCorporalService(repo);
        _prontuario = new ProntuarioService(repo);
    }

    private async Task<int> Paciente()
    {
        var p = new Paciente { Nome = "Paciente demonstrativo", Convenio = Convenio.UnimedIntercambio };
        _db.Add(p); await _db.SaveChangesAsync(); return p.Id;
    }

    private Task<Evolucao> Sessao(int paciente, DateOnly data)
        => _prontuario.SalvarAsync(new Evolucao { PacienteId = paciente, Data = data, TextoEvolucao = "Registro da equipe." });

    private Task<MapaCorporal> Marcar(Evolucao e, string? observacoes = null)
        => _mapas.SalvarAsync(e.Id, [new PontoMapa { Face = FaceCorpo.Costas, X = .4, Y = .6,
            Nome = "Ponto da equipe", Tecnica = TecnicaPonto.Agulha, Observacao = "Observação preservada" }], observacoes);

    [Fact]
    public async Task Historico_busca_mapas_alem_das_ultimas_doze_evolucoes_e_exclui_cancelados_e_futuros()
    {
        var paciente = await Paciente();
        var antigo = await Sessao(paciente, Dia.AddDays(-30)); await Marcar(antigo);
        for (var i = 20; i > 0; i--) await Sessao(paciente, Dia.AddDays(-i));
        var cancelada = await Sessao(paciente, Dia.AddDays(-1)); await Marcar(cancelada);
        cancelada.CanceladaEm = DateTime.Now; await _db.SaveChangesAsync();
        var futuro = await Sessao(paciente, Dia.AddDays(1)); await Marcar(futuro);
        var atual = await Sessao(paciente, Dia); await Marcar(atual);
        var outro = await Sessao(await Paciente(), Dia.AddDays(-1)); await Marcar(outro);
        var historico = await _mapas.HistoricoAsync(paciente, atual.Id);
        historico.Should().ContainSingle().Which.EvolucaoId.Should().Be(antigo.Id);
        historico[0].Pontos.Should().Be(1);
    }

    [Fact]
    public async Task Copiar_preserva_pontos_observacoes_e_origem_sem_gravar_no_destino()
    {
        var paciente = await Paciente();
        var origem = await Sessao(paciente, Dia.AddDays(-1)); await Marcar(origem, "Notas da sessão anterior.");
        var destino = await Sessao(paciente, Dia);
        var copia = await _mapas.CopiarParaEdicaoAsync(paciente, origem.Id, destino.Id);
        copia.Pontos.Single().Observacao.Should().Be("Observação preservada");
        copia.Observacoes.Should().Be("Notas da sessão anterior.");
        copia.Pontos[0].Nome = "Ajustado hoje";
        (await _mapas.DaEvolucaoAsync(origem.Id))!.Pontos.Single().Nome.Should().Be("Ponto da equipe");
        (await _mapas.DaEvolucaoAsync(destino.Id)).Should().BeNull();
        await _mapas.SalvarAsync(destino.Id, copia.Pontos, copia.Observacoes);
        _db.ChangeTracker.Clear();
        (await _mapas.DaEvolucaoAsync(destino.Id))!.Pontos.Single().Nome.Should().Be("Ajustado hoje");
        (await _mapas.DaEvolucaoAsync(origem.Id))!.Pontos.Single().Nome.Should().Be("Ponto da equipe");
    }

    [Fact]
    public async Task Recusa_copiar_outro_paciente_sessao_cancelada_atual_ou_futura()
    {
        var paciente = await Paciente();
        var atual = await Sessao(paciente, Dia); await Marcar(atual);
        var outro = await Sessao(await Paciente(), Dia.AddDays(-1)); await Marcar(outro);
        var futura = await Sessao(paciente, Dia.AddDays(1)); await Marcar(futura);
        var cancelada = await Sessao(paciente, Dia.AddDays(-1)); await Marcar(cancelada);
        cancelada.CanceladaEm = DateTime.Now; await _db.SaveChangesAsync();
        foreach (var origem in new[] { outro, atual, futura, cancelada })
            await FluentActions.Awaiting(() => _mapas.CopiarParaEdicaoAsync(paciente, origem.Id, atual.Id))
                .Should().ThrowAsync<InvalidOperationException>();
        await FluentActions.Awaiting(() => _mapas.HistoricoAsync(paciente, outro.Id))
            .Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task Sessao_nova_retroativa_so_oferece_mapas_ate_a_data_informada()
    {
        var paciente = await Paciente();
        var antigo = await Sessao(paciente, Dia.AddDays(-3)); await Marcar(antigo);
        var recente = await Sessao(paciente, Dia); await Marcar(recente);
        (await _mapas.HistoricoAsync(paciente, dataSessao: Dia.AddDays(-2)))
            .Should().ContainSingle().Which.EvolucaoId.Should().Be(antigo.Id);
    }

    [Fact]
    public async Task Modelo_cadastrado_com_pontos_aceita_as_observacoes_completas_do_mapa()
    {
        var paciente = await Paciente();
        var pontos = new[] { new PontoMapa { X = .5, Y = .4, Nome = "Ponto cadastrado", Observacao = new string('p', 200) } };
        var notas = new string('n', 900);
        var modelo = await _mapas.SalvarComoProtocoloAsync(paciente, "Modelo da equipe", pontos, true, notas, "profissional");
        _db.ChangeTracker.Clear();
        var salvo = (await _mapas.ObterProtocoloAsync(modelo.Id))!;
        salvo.Descricao.Should().Be(notas);
        salvo.Pontos.Single().Observacao.Should().HaveLength(200);
        (await _mapas.ProtocolosAsync(await Paciente())).Should().ContainSingle();
        (await _db.MapasCorporais.CountAsync()).Should().Be(0, "cadastrar um modelo não registra atendimento");
    }

    [Fact]
    public async Task Sessao_com_apenas_mapa_e_gravada_com_vinculo_sem_inventar_texto()
    {
        var paciente = await Paciente();
        var mapa = new MapaCorporal { Pontos = [new PontoMapa { X = .4, Y = .5 }], Observacoes = "Mapa confirmado pela equipe" };
        var salva = await _prontuario.SalvarAsync(new Evolucao { PacienteId = paciente, Data = Dia }, "profissional", mapa: mapa);
        _db.ChangeTracker.Clear();
        var registro = await _db.Evolucoes.SingleAsync();
        registro.TextoEvolucao.Should().BeNull();
        var gravado = (await _mapas.DaEvolucaoAsync(salva.Id))!;
        gravado.EvolucaoId.Should().Be(registro.Id);
        gravado.Pontos.Should().ContainSingle();
    }

    [Fact]
    public async Task Falha_na_gravacao_do_mapa_nao_deixa_evolucao_parcial()
    {
        var paciente = await Paciente();
        using var falha = new ClinicaDbContext(new DbContextOptionsBuilder<ClinicaDbContext>()
            .UseSqlite(_conn).AddInterceptors(new RecusarMapa()).Options);
        var servico = new ProntuarioService(new ClinicaRepositorio(falha));
        await FluentActions.Awaiting(() => servico.SalvarAsync(new Evolucao
            { PacienteId = paciente, Data = Dia, TextoEvolucao = "Texto junto do mapa" }, mapa:
            new MapaCorporal { Pontos = [new PontoMapa { X = .4, Y = .5 }] }))
            .Should().ThrowAsync<InvalidOperationException>().WithMessage("Falha simulada do mapa");
        (await _db.Evolucoes.CountAsync()).Should().Be(0);
        (await _db.MapasCorporais.CountAsync()).Should().Be(0);
    }

    private sealed class RecusarMapa : SaveChangesInterceptor
    {
        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData,
            InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            if (eventData.Context!.ChangeTracker.Entries<MapaCorporal>().Any(e => e.State == EntityState.Added))
                throw new InvalidOperationException("Falha simulada do mapa");
            return ValueTask.FromResult(result);
        }
    }

    public void Dispose() { _db.Dispose(); _conn.Dispose(); }
}
