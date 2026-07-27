using Clinica.Application.Modelos;
using Clinica.Application.Servicos;
using Clinica.Domain;
using Clinica.Domain.Entities;
using Clinica.Infrastructure;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Clinica.Tests;

/// <summary>
/// Rodada de pendências ("rodar as pendências"): prazo de decisão POR ATENDIMENTO. Passados N dias
/// (padrão 10) desde o atendimento sem baixa, a guia exige decisão — a secretária baixa o que puder e
/// justifica o resto como NÃO CONFORMIDADE, que silencia a pendência (sai do painel), fica no relatório
/// e só volta a ser pendência se for reaberta.
/// </summary>
public class RodadaPendenciasTests : IDisposable
{
    private readonly SqliteConnection _conn;
    private readonly ClinicaDbContext _db;
    private readonly ClinicaRepositorio _repo;
    private readonly ParametrosService _parametros;
    private readonly PendenciaService _pendencias;
    private readonly RodadaPendenciasService _rodada;

    public RodadaPendenciasTests()
    {
        _conn = new SqliteConnection("DataSource=:memory:");
        _conn.Open();
        var options = new DbContextOptionsBuilder<ClinicaDbContext>().UseSqlite(_conn).Options;
        _db = new ClinicaDbContext(options);
        _db.Database.EnsureCreated();
        _repo = new ClinicaRepositorio(_db);
        _parametros = new ParametrosService(_repo);
        _pendencias = new PendenciaService(_repo);
        _rodada = new RodadaPendenciasService(_repo, _pendencias, _parametros);
    }

    /// <summary>Cria um 2º código pendente (o historicamente esquecido) a partir de um atendimento.</summary>
    private async Task<CodigoFaturamento> CriarSegundoCodigoAsync()
    {
        var p = new Paciente { Nome = "Paciente", Convenio = Convenio.UnimedIntercambio, Sexo = Sexo.Feminino };
        _db.Pacientes.Add(p);
        await _db.SaveChangesAsync();
        var r = await new AtendimentoService(_repo).LancarAsync(
            p.Id, new DateOnly(2026, 7, 10), ModalidadeAtendimento.AcupunturaComEletro);
        return r.Atendimento.Codigos.First(c => c.Ordem == OrdemCodigo.Segundo);
    }

    private static readonly DateOnly Ref = new(2026, 7, 21);

    [Fact]
    public async Task MarcarNaoConformidade_SilenciaPendenciaEGravaAuditoria()
    {
        var codigo = await CriarSegundoCodigoAsync();

        await _rodada.MarcarNaoConformidadeAsync(codigo.Id, "Portal fora do ar; paciente não retornou", "maria");

        var salvo = await _repo.ObterCodigoAsync(codigo.Id);
        salvo!.Status.Should().Be(StatusCodigo.NaoConformidade);
        salvo.NaoConformidadeJustificativa.Should().Contain("Portal fora do ar");
        salvo.NaoConformidadeEm.Should().NotBeNull();
        salvo.EstaPendente(Ref).Should().BeFalse();

        // Sai do painel de pendências.
        var pendentes = await _pendencias.CodigosPendentesAsync(Ref);
        pendentes.Should().NotContain(p => p.CodigoId == codigo.Id);

        var evento = (await _repo.EventosAuditoriaAsync()).Single(e => e.Acao == "NaoConformidade");
        evento.Operador.Should().Be("maria");
        evento.CodigoId.Should().Be(codigo.Id);
    }

    [Fact]
    public async Task NaoConformidade_ApareceComoPendenciaCinza()
    {
        var codigo = await CriarSegundoCodigoAsync();
        await _rodada.MarcarNaoConformidadeAsync(codigo.Id, "aguardando o paciente", "maria");

        var cinzas = await _pendencias.NaoConformidadesComoPendenciaAsync(Ref);

        var linha = cinzas.Single(p => p.CodigoId == codigo.Id);
        linha.Urgencia.Should().Be(NivelUrgencia.Cinza);
        linha.EhNaoConformidade.Should().BeTrue();
        linha.ObservacaoPendencia.Should().Be("aguardando o paciente");
        // Não aparece entre as pendências ativas (fica só na lista cinza).
        (await _pendencias.CodigosPendentesAsync(Ref)).Should().NotContain(p => p.CodigoId == codigo.Id);
    }

    [Fact]
    public async Task PacienteVolta_ReabreNaoConformidadeEAvisa()
    {
        var codigo = await CriarSegundoCodigoAsync();
        var pacienteId = (await _repo.ObterCodigoAsync(codigo.Id))!.Atendimento!.PacienteId;
        await _rodada.MarcarNaoConformidadeAsync(codigo.Id, "aguardando o paciente", "maria");

        // Paciente volta: um novo atendimento reabre a não conformidade e avisa a secretária.
        var resultado = await new AtendimentoService(_repo).LancarAsync(
            pacienteId, new DateOnly(2026, 7, 25), ModalidadeAtendimento.AcupunturaComEletro);

        var salvo = await _repo.ObterCodigoAsync(codigo.Id);
        salvo!.Status.Should().Be(StatusCodigo.Aberto);
        salvo.EstaPendente(new DateOnly(2026, 7, 26)).Should().BeTrue();
        resultado.Avisos.Should().Contain(a => a.Contains("não conformidade"));
        (await _repo.EventosAuditoriaAsync())
            .Should().Contain(e => e.Acao == "NaoConformidadeReaberta" && e.Operador == "sistema");
    }

    [Fact]
    public async Task Reabrir_VoltaASerPendencia()
    {
        var codigo = await CriarSegundoCodigoAsync();
        await _rodada.MarcarNaoConformidadeAsync(codigo.Id, "aguardando o convênio", "maria");

        await _rodada.ReabrirNaoConformidadeAsync(codigo.Id, "joana");

        var salvo = await _repo.ObterCodigoAsync(codigo.Id);
        salvo!.Status.Should().Be(StatusCodigo.Aberto);
        salvo.NaoConformidadeJustificativa.Should().BeNull();
        salvo.NaoConformidadeEm.Should().BeNull();
        salvo.EstaPendente(Ref).Should().BeTrue();

        (await _pendencias.CodigosPendentesAsync(Ref)).Should().Contain(p => p.CodigoId == codigo.Id);
        (await _repo.EventosAuditoriaAsync()).Should().Contain(e => e.Acao == "NaoConformidadeReaberta");
    }

    [Fact]
    public async Task MarcarNaoConformidade_SemJustificativa_Recusa()
    {
        var codigo = await CriarSegundoCodigoAsync();

        var acao = () => _rodada.MarcarNaoConformidadeAsync(codigo.Id, "   ", "maria");

        await acao.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task GuiaBaixada_NaoViraNaoConformidade()
    {
        var codigo = await CriarSegundoCodigoAsync();
        await new FaturamentoService(_repo).DarBaixaAsync(codigo.Id, Ref, "G-1", "maria", null);

        var acao = () => _rodada.MarcarNaoConformidadeAsync(codigo.Id, "tentando justificar", "maria");

        await acao.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task Status_DentroDoPrazo_NaoExigeDecisao()
    {
        await CriarSegundoCodigoAsync(); // atendimento em 2026-07-10; prazo padrão = 10 dias

        // 9 dias após o atendimento: pendente, mas ainda dentro do prazo.
        var status = await _rodada.ObterStatusAsync(new DateOnly(2026, 7, 19));

        status.PrazoDias.Should().Be(10);
        status.GuiasPendentes.Should().BeGreaterThan(0);
        status.GuiasParaDecisao.Should().Be(0);
        status.ExigeDecisao.Should().BeFalse();
    }

    [Fact]
    public async Task Status_AposPrazoDesdeAtendimento_ExigeDecisao()
    {
        await CriarSegundoCodigoAsync(); // atendimento em 2026-07-10

        // Exatamente 10 dias após o atendimento: o prazo venceu e o sistema exige decisão.
        var status = await _rodada.ObterStatusAsync(new DateOnly(2026, 7, 20));

        status.GuiasParaDecisao.Should().BeGreaterThan(0);
        status.ExigeDecisao.Should().BeTrue();
    }

    [Fact]
    public async Task GuiasVencidasParaDecisao_TrazSomenteAsVencidas()
    {
        var codigo = await CriarSegundoCodigoAsync(); // atendimento em 2026-07-10

        // Ainda no prazo: nenhuma guia vencida.
        (await _rodada.GuiasVencidasParaDecisaoAsync(new DateOnly(2026, 7, 19)))
            .Should().BeEmpty();

        // Prazo vencido: a guia entra na lista de decisão obrigatória.
        var vencidas = await _rodada.GuiasVencidasParaDecisaoAsync(new DateOnly(2026, 7, 21));
        vencidas.Should().Contain(p => p.CodigoId == codigo.Id);
    }

    [Fact]
    public async Task PrazoConta_DesdeOAtendimento_NaoDaDataPrevista()
    {
        // O 2º código tem data prevista = atendimento + 1 dia; o prazo deve contar desde o ATENDIMENTO.
        var codigo = await CriarSegundoCodigoAsync(); // atendimento 2026-07-10, prevista 2026-07-11
        var salvo = await _repo.ObterCodigoAsync(codigo.Id);

        salvo!.PrazoDecisaoVencido(new DateOnly(2026, 7, 19), 10).Should().BeFalse(); // 9 dias do atendimento
        salvo.PrazoDecisaoVencido(new DateOnly(2026, 7, 20), 10).Should().BeTrue();  // 10 dias do atendimento
    }

    [Fact]
    public async Task Carencia_BacklogAnteriorAAtivacao_ContaDaAtivacao()
    {
        // Guia antiga (atendimento 2026-07-10) e a rodada por atendimento só foi ativada em 2026-08-01.
        var codigo = await CriarSegundoCodigoAsync();
        var salvo = await _repo.ObterCodigoAsync(codigo.Id);
        var ativacao = new DateOnly(2026, 8, 1);

        // Sem carência (null), já venceria há muito.
        salvo!.PrazoDecisaoVencido(new DateOnly(2026, 7, 25), 10).Should().BeTrue();

        // Com carência, o prazo conta a partir da ativação: 2026-08-01 + 10 = 2026-08-11.
        salvo.PrazoDecisaoVencido(new DateOnly(2026, 8, 10), 10, ativacao).Should().BeFalse(); // 9 dias da ativação
        salvo.PrazoDecisaoVencido(new DateOnly(2026, 8, 11), 10, ativacao).Should().BeTrue();  // 10 dias da ativação

        // Ativação anterior ao atendimento não muda nada: conta do atendimento normalmente.
        salvo.PrazoDecisaoVencido(new DateOnly(2026, 7, 19), 10, new DateOnly(2026, 7, 1)).Should().BeFalse();
        salvo.PrazoDecisaoVencido(new DateOnly(2026, 7, 20), 10, new DateOnly(2026, 7, 1)).Should().BeTrue();
    }

    [Fact]
    public async Task GarantirInicio_AncoraUmaVezENaoSobrescreve()
    {
        await _rodada.GarantirInicioAsync(new DateOnly(2026, 7, 20));
        (await _parametros.ObterInicioRodadaPorAtendimentoAsync()).Should().Be(new DateOnly(2026, 7, 20));

        await _rodada.GarantirInicioAsync(new DateOnly(2026, 8, 1)); // não sobrescreve a âncora existente
        (await _parametros.ObterInicioRodadaPorAtendimentoAsync()).Should().Be(new DateOnly(2026, 7, 20));
    }

    [Fact]
    public async Task Status_ComCarencia_NaoBloqueiaBacklogNaPrimeiraAbertura()
    {
        await CriarSegundoCodigoAsync(); // atendimento 2026-07-10 (backlog)
        await _rodada.GarantirInicioAsync(new DateOnly(2026, 8, 1)); // ativação bem depois

        // No dia da ativação, o backlog NÃO deve exigir decisão (carência de 10 dias).
        (await _rodada.ObterStatusAsync(new DateOnly(2026, 8, 1))).ExigeDecisao.Should().BeFalse();

        // Passada a carência (ativação + 10), aí sim exige decisão.
        (await _rodada.ObterStatusAsync(new DateOnly(2026, 8, 11))).ExigeDecisao.Should().BeTrue();
    }

    [Fact]
    public async Task NaoConformidadesAsync_ListaOBacklogJustificado()
    {
        var codigo = await CriarSegundoCodigoAsync();
        await _rodada.MarcarNaoConformidadeAsync(codigo.Id, "paciente mudou de convênio", "maria");

        var lista = await _rodada.NaoConformidadesAsync();

        lista.Should().ContainSingle(n => n.CodigoId == codigo.Id)
            .Which.Justificativa.Should().Be("paciente mudou de convênio");
    }

    [Fact]
    public async Task Relatorio_ContaNaoConformidadeSeparadaDasPendentes()
    {
        var codigo = await CriarSegundoCodigoAsync();
        await _rodada.MarcarNaoConformidadeAsync(codigo.Id, "portal fora do ar", "maria");

        var rel = await new RelatorioService(_repo).GerarAsync(
            new DateOnly(2026, 7, 1), new DateOnly(2026, 7, 31), Ref);

        rel.Resumo.NaoConformidades.Should().Be(1);
        rel.NaoConformidades.Should().ContainSingle(n => n.CodigoId == codigo.Id);
        // Não conformidade não conta como pendência ativa.
        rel.NaoConformidades.Should().NotBeEmpty();
        rel.Resumo.Pendentes.Should().Be(rel.Resumo.TotalCodigos - rel.Resumo.Baixados - 1);
    }

    public void Dispose()
    {
        _db.Dispose();
        _conn.Dispose();
    }
}
