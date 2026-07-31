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
/// A conversa entre os quatro módulos (parcela 27).
///
/// O sistema já tinha as pontes de IDA: a Recepção conclui a sessão e nasce a guia; a
/// guia baixada aparece na Conciliação do Financeiro; o Financeiro alimenta a
/// rentabilidade e o painel do Gerente. O que faltava era a VOLTA — o que acontece
/// quando o convênio diz não, e o que o balcão precisa saber sobre dinheiro e glosa
/// enquanto o paciente está na frente dele.
///
/// O que estes testes protegem:
///
/// 1. **A glosa derruba a receita PREVISTA e não toca na REALIZADA.** Previsto é
///    promessa, e promessa recusada pelo convênio não é promessa; realizado é dinheiro
///    que entrou na conta, e cancelá-lo faria o caixa parar de bater com o extrato.
/// 2. **Cancelar não apaga.** O lançamento fica no histórico com o motivo, e a guia
///    REAPARECE sozinha na conciliação — que é o caminho de volta quando o recurso é
///    aceito, sem uma linha de código para isso.
/// 3. **A guia glosada não some da conciliação: aparece MARCADA.** Sumir faria o balcão
///    procurar a tarde inteira a guia que viu ontem.
/// 4. **O balcão passa a saber da dívida e da glosa**, que estavam gravadas havia
///    parcelas e só eram lidas por quem não atende ninguém.
/// </summary>
public class ComunicacaoEntreModulosTests : IDisposable
{
    private readonly SqliteConnection _conn;
    private readonly ClinicaDbContext _db;
    private readonly ClinicaRepositorio _repo;
    private readonly AtendimentoService _atendimentos;
    private readonly FinanceiroService _financeiro;
    private readonly GlosaService _glosas;
    private readonly ReceitaGlosadaService _receitaGlosada;

    private static readonly DateOnly Hoje = new(2026, 8, 20);
    private static readonly DateOnly DiaDaSessao = new(2026, 8, 3);

    public ComunicacaoEntreModulosTests()
    {
        _conn = new SqliteConnection("DataSource=:memory:");
        _conn.Open();
        var options = new DbContextOptionsBuilder<ClinicaDbContext>().UseSqlite(_conn).Options;
        _db = new ClinicaDbContext(options);
        _db.Database.EnsureCreated();
        _repo = new ClinicaRepositorio(_db);
        _atendimentos = new AtendimentoService(_repo);
        _financeiro = new FinanceiroService(_repo);
        _glosas = new GlosaService(_repo, new ParametrosService(_repo));
        _receitaGlosada = new ReceitaGlosadaService(_repo, _financeiro);
    }

    private async Task<int> CriarPacienteAsync(string nome = "Maria")
    {
        var p = new Paciente
        {
            Nome = nome,
            Convenio = Convenio.UnimedIntercambio,
            Sexo = Sexo.Feminino,
            Carteirinha = "123456"
        };
        _db.Pacientes.Add(p);
        await _db.SaveChangesAsync();
        return p.Id;
    }

    /// <summary>
    /// O caminho inteiro: a Recepção conclui a sessão, o faturamento baixa a guia, o
    /// financeiro lança a receita. É o estado em que a clínica fica todo dia.
    /// </summary>
    private async Task<(int PacienteId, CodigoFaturamento Codigo)> GuiaBaixadaAsync(
        string paciente = "Maria")
    {
        var pacienteId = await CriarPacienteAsync(paciente);

        // Recepção → Faturamento: concluir a sessão cria o atendimento e os códigos.
        await _atendimentos.LancarAsync(
            pacienteId, DiaDaSessao, ModalidadeAtendimento.AcupunturaComEletro);

        var codigo = (await _repo.CodigosNoPeriodoAsync(DiaDaSessao, DiaDaSessao))
            .First(c => c.Atendimento!.PacienteId == pacienteId);

        // Faturamento: a secretária efetivou a guia no sistema do convênio.
        codigo.DarBaixa(DiaDaSessao.AddDays(2), "G-1001", "secretaria", null);
        await _db.SaveChangesAsync();

        return (pacienteId, codigo);
    }

    private async Task<GuiaSemLancamento> PrimeiraDaConciliacaoAsync()
        => (await _financeiro.GuiasSemLancamentoAsync(
                new DateOnly(2026, 8, 1), new DateOnly(2026, 8, 31)))
            .First();

    // ==================== Ida: a ponte que já existia ====================

    [Fact]
    public async Task Sessao_concluida_vira_guia_e_a_guia_baixada_chega_ao_financeiro()
    {
        var (_, codigo) = await GuiaBaixadaAsync();

        var conciliacao = await _financeiro.GuiasSemLancamentoAsync(
            new DateOnly(2026, 8, 1), new DateOnly(2026, 8, 31));

        conciliacao.Should().ContainSingle(g => g.CodigoId == codigo.Id);
        conciliacao[0].Paciente.Should().Be("Maria");
        conciliacao[0].GlosaEmAberto.Should().BeFalse();
    }

    // ==================== Volta: a glosa derruba a receita ====================

    [Fact]
    public async Task Glosa_de_guia_com_receita_prevista_aparece_para_o_financeiro()
    {
        var (_, codigo) = await GuiaBaixadaAsync();
        await _financeiro.LancarReceitaDaGuiaAsync(await PrimeiraDaConciliacaoAsync(), 150m);

        await _glosas.RegistrarAsync(codigo.Id, Hoje, "Falta assinatura do paciente");

        var pendentes = await _receitaGlosada.PendentesAsync(
            new DateOnly(2026, 8, 1), new DateOnly(2026, 8, 31));

        pendentes.Should().ContainSingle();
        pendentes[0].Valor.Should().Be(150m);
        pendentes[0].AindaPrevisto.Should().BeTrue();
        pendentes[0].MotivoGlosa.Should().Contain("assinatura");
    }

    [Fact]
    public async Task Guia_glosada_sem_receita_lancada_nao_entra_na_lista()
    {
        // Nada a derrubar: ninguém contou esse dinheiro. A guia aparece marcada na
        // conciliação, que é outro assunto (e outro teste).
        var (_, codigo) = await GuiaBaixadaAsync();

        await _glosas.RegistrarAsync(codigo.Id, Hoje, "Sem senha");

        (await _receitaGlosada.PendentesAsync(
                new DateOnly(2026, 8, 1), new DateOnly(2026, 8, 31)))
            .Should().BeEmpty();
    }

    [Fact]
    public async Task Derrubar_a_receita_cancela_o_lancamento_sem_apagar_o_historico()
    {
        var (_, codigo) = await GuiaBaixadaAsync();
        var lancamento = await _financeiro.LancarReceitaDaGuiaAsync(
            await PrimeiraDaConciliacaoAsync(), 150m);
        await _glosas.RegistrarAsync(codigo.Id, Hoje, "Falta assinatura");

        await _receitaGlosada.CancelarReceitaAsync(
            codigo.Id, "convênio recusou e não vamos recorrer", "financeiro");

        var depois = await _repo.ObterLancamentoAsync(lancamento.Id);
        depois.Should().NotBeNull("lançamento é fato datado: sai do total, não do histórico");
        depois!.Status.Should().Be(StatusLancamento.Cancelado);
        depois.Observacoes.Should().Contain("glosa");
        depois.Observacoes.Should().Contain("não vamos recorrer");
    }

    [Fact]
    public async Task Receita_derrubada_faz_a_guia_voltar_para_a_conciliacao()
    {
        // É o caminho de volta, e ele funciona sem código nenhum: cancelado o vínculo, a
        // guia deixa de ter receita ativa e reaparece esperando ser lançada — que é
        // exatamente o que a clínica precisa se o recurso for aceito.
        var (_, codigo) = await GuiaBaixadaAsync();
        await _financeiro.LancarReceitaDaGuiaAsync(await PrimeiraDaConciliacaoAsync(), 150m);

        (await _financeiro.GuiasSemLancamentoAsync(
                new DateOnly(2026, 8, 1), new DateOnly(2026, 8, 31)))
            .Should().BeEmpty("a guia saiu da conciliação ao virar receita");

        await _glosas.RegistrarAsync(codigo.Id, Hoje, "Falta assinatura");
        await _receitaGlosada.CancelarReceitaAsync(codigo.Id, "recusada", "financeiro");

        (await _financeiro.GuiasSemLancamentoAsync(
                new DateOnly(2026, 8, 1), new DateOnly(2026, 8, 31)))
            .Should().ContainSingle(g => g.CodigoId == codigo.Id);
    }

    [Fact]
    public async Task Receita_ja_realizada_nao_e_cancelada()
    {
        var (_, codigo) = await GuiaBaixadaAsync();
        var lancamento = await _financeiro.LancarReceitaDaGuiaAsync(
            await PrimeiraDaConciliacaoAsync(), 150m);

        // O convênio depositou, e só depois veio a glosa.
        await _financeiro.RealizarAsync(lancamento.Id, Hoje.AddDays(-1));
        await _glosas.RegistrarAsync(codigo.Id, Hoje, "Glosa após pagamento");

        var derrubar = () => _receitaGlosada.CancelarReceitaAsync(
            codigo.Id, "convênio recusou", "financeiro");

        // Cancelar a entrada faria o caixa parar de bater com o extrato — e levaria junto
        // a conferência do dia. Estorno é outro fato, com a data dele.
        await derrubar.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*já entrou no caixa*");

        (await _repo.ObterLancamentoAsync(lancamento.Id))!
            .Status.Should().Be(StatusLancamento.Realizado);
    }

    [Fact]
    public async Task Derrubar_receita_sem_motivo_e_recusado()
    {
        var (_, codigo) = await GuiaBaixadaAsync();
        await _financeiro.LancarReceitaDaGuiaAsync(await PrimeiraDaConciliacaoAsync(), 150m);
        await _glosas.RegistrarAsync(codigo.Id, Hoje, "Falta assinatura");

        var derrubar = () => _receitaGlosada.CancelarReceitaAsync(codigo.Id, "  ", "financeiro");

        await derrubar.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*por que*");
    }

    [Fact]
    public async Task Resumo_soma_so_a_receita_prevista()
    {
        var (_, umaGuia) = await GuiaBaixadaAsync("Maria");
        await _financeiro.LancarReceitaDaGuiaAsync(await PrimeiraDaConciliacaoAsync(), 150m);
        await _glosas.RegistrarAsync(umaGuia.Id, Hoje, "Falta assinatura");

        var (guias, valor) = await _receitaGlosada.ResumoPrevistoAsync(
            new DateOnly(2026, 8, 1), new DateOnly(2026, 8, 31));

        guias.Should().Be(1);
        valor.Should().Be(150m);
    }

    // ==================== A marca na conciliação ====================

    [Fact]
    public async Task Guia_glosada_continua_na_conciliacao_marcada()
    {
        var (_, codigo) = await GuiaBaixadaAsync();
        await _glosas.RegistrarAsync(codigo.Id, Hoje, "Falta assinatura do paciente");

        var conciliacao = await _financeiro.GuiasSemLancamentoAsync(
            new DateOnly(2026, 8, 1), new DateOnly(2026, 8, 31));

        // Some não, MARCADA sim: a linha que desaparece sem explicação faz o balcão
        // procurar a tarde inteira a guia que ele viu ontem.
        conciliacao.Should().ContainSingle(g => g.CodigoId == codigo.Id);
        conciliacao[0].GlosaEmAberto.Should().BeTrue();
        conciliacao[0].DataGlosa.Should().Be(Hoje);
        conciliacao[0].MotivoGlosa.Should().Contain("assinatura");
    }

    [Fact]
    public async Task Glosa_recuperada_deixa_de_marcar_a_guia()
    {
        var (_, codigo) = await GuiaBaixadaAsync();
        await _glosas.RegistrarAsync(codigo.Id, Hoje, "Falta assinatura");
        await _glosas.ReapresentarAsync(codigo.Id, Hoje.AddDays(3));
        await _glosas.MarcarRecuperadaAsync(codigo.Id);

        var conciliacao = await _financeiro.GuiasSemLancamentoAsync(
            new DateOnly(2026, 8, 1), new DateOnly(2026, 8, 31));

        // Recuperada é glosa resolvida: a receita dela vale, e insistir na marca faria o
        // balcão hesitar em lançar um dinheiro que o convênio já aceitou.
        conciliacao[0].GlosaEmAberto.Should().BeFalse();
    }

    public void Dispose()
    {
        _db.Dispose();
        _conn.Dispose();
        GC.SuppressFinalize(this);
    }
}
