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
/// nov/2026, o dono: "no momento do lançamento já deve ser entendido que entrou dinheiro". O particular lançado por
/// QUALQUER porta (aqui: o Concluir do Consultório e o registro da Recepção) entra no caixa RECEBIDO, sem forma de
/// pagamento, no mesmo commit — uma vez só, nunca com pacote, nunca com preço inventado. Dados fictícios.
/// </summary>
public class ParticularNoLancamentoTests : IDisposable
{
    private const string CodigoParticular = "Particular";
    private readonly SqliteConnection _conn;
    private readonly ClinicaDbContext _db;
    private readonly ClinicaRepositorio _repo;
    private readonly AgendaService _agenda;
    private readonly FinanceiroService _financeiro;
    private readonly PrecoParticularService _precos;
    private readonly FechamentoSessaoService _fechamento;
    private readonly int _prof;
    private int _marcados;
    private static readonly DateTime Sessao = new(2026, 10, 20, 9, 0, 0);
    private static DateOnly Dia => DateOnly.FromDateTime(Sessao);

    public ParticularNoLancamentoTests()
    {
        _conn = new SqliteConnection("DataSource=:memory:"); _conn.Open();
        _db = new ClinicaDbContext(new DbContextOptionsBuilder<ClinicaDbContext>().UseSqlite(_conn).Options);
        _db.Database.EnsureCreated();
        var prof = new Profissional { Nome = "Dra. Fictícia" }; _db.Profissionais.Add(prof); _db.SaveChanges(); _prof = prof.Id;
        _repo = new ClinicaRepositorio(_db);
        _agenda = new AgendaService(_repo, new AtendimentoService(_repo));
        _financeiro = new FinanceiroService(_repo);
        _precos = new PrecoParticularService(_repo);
        _fechamento = new FechamentoSessaoService(_repo, _agenda, new PacoteService(_repo), new EstoqueService(_repo), _financeiro, _precos, new ContasService(_repo));
        CatalogoConvenios.Atualizar(
        [
            new EntradaConvenio(CodigoParticular, "Particular", Convenio.Personalizado, true,
                new ConfiguracaoRegraGenerica { FazEletro = true, TemSegundoCodigo = true }, FormatoNumeroGuia.SemValidacao, GeraGuia: false),
            new EntradaConvenio("Amil", "Amil", Convenio.Amil, true, GeraGuia: true)
        ]);
    }

    public void Dispose() { _db.Dispose(); _conn.Dispose(); }

    async Task<int> PacienteAsync(string convenio = CodigoParticular)
    {
        var p = new Paciente { Nome = "Paciente Fictícia " + (++_marcados), Convenio = convenio == "Amil" ? Convenio.Amil : Convenio.Personalizado, ConvenioCodigo = convenio, Sexo = Sexo.Feminino };
        _db.Pacientes.Add(p); await _db.SaveChangesAsync(); return p.Id;
    }

    async Task<int> AgendarAsync(int pacienteId, bool particular = false)
        => (await _agenda.AgendarAsync(pacienteId, Sessao.AddHours(_marcados++), ModalidadeAtendimento.AcupunturaComEletro, null,
            profissionalId: _prof, particular: particular)).Id;

    Task PrecoAsync(decimal valor) => _precos.SalvarAsync(new PrecoParticular { ModalidadeCodigo = nameof(ModalidadeAtendimento.AcupunturaComEletro), Valor = valor });

    List<LancamentoFinanceiro> Entradas() => _db.Lancamentos.AsNoTracking().Where(l => l.Tipo == TipoLancamento.Entrada).ToList();

    [Fact]
    public async Task Medico_conclui_o_particular_e_ele_ja_entra_no_caixa_recebido_sem_forma()
    {
        await PrecoAsync(180m);
        var agendamentoId = await AgendarAsync(await PacienteAsync());
        var fim = await _agenda.ConcluirAtendimentoClinicoAsync(agendamentoId, "medica", default);
        _db.ChangeTracker.Clear();

        var e = Entradas().Should().ContainSingle().Subject;
        e.Status.Should().Be(StatusLancamento.Realizado);
        e.Valor.Should().Be(180m);
        e.DataPagamento.Should().Be(Dia);
        e.FormaPagamento.Should().BeNull("o dono decidiu não perguntar como pagou");
        e.AtendimentoId.Should().Be(fim.Atendimento.Id);
        e.AgendamentoId.Should().Be(agendamentoId);
        (await _financeiro.SessoesParticularesSemReceitaAsync(Dia, Dia)).Should().BeEmpty();
    }

    [Fact]
    public async Task O_fechamento_da_recepcao_nao_cobra_de_novo()
    {
        await PrecoAsync(180m);
        var agendamentoId = await AgendarAsync(await PacienteAsync());
        var registro = await _fechamento.RegistrarAtendimentoAsync(agendamentoId, "recepcao");
        registro.Proposta.SugereLancamento.Should().BeFalse("a sessão já entrou no caixa no lançamento");
        var segunda = await _fechamento.ConcluirAsync(new DecisaoFechamento(agendamentoId, GerarLancamento: true, Valor: 180m, Forma: FormaPagamento.Pix), "recepcao");
        segunda.Avisos.Should().ContainSingle(a => a.Contains("já entrou no caixa"));
        _db.ChangeTracker.Clear();
        Entradas().Should().ContainSingle();
    }

    [Fact]
    public async Task Sem_preco_nao_inventa_valor_e_a_sessao_fica_na_conciliacao()
    {
        var agendamentoId = await AgendarAsync(await PacienteAsync());
        await _agenda.ConcluirAtendimentoClinicoAsync(agendamentoId, "medica", default);
        _db.ChangeTracker.Clear();
        Entradas().Should().BeEmpty();
        (await _financeiro.SessoesParticularesSemReceitaAsync(Dia, Dia)).Should().ContainSingle();
    }

    [Fact]
    public async Task Convenio_nao_entra_no_caixa_mas_o_marcado_particular_entra()
    {
        await PrecoAsync(150m);
        await _agenda.ConcluirAtendimentoClinicoAsync(await AgendarAsync(await PacienteAsync("Amil")), "medica", default);
        _db.ChangeTracker.Clear();
        Entradas().Should().BeEmpty("o convênio entra pela conciliação da guia");
        await _agenda.ConcluirAtendimentoClinicoAsync(await AgendarAsync(await PacienteAsync("Amil"), particular: true), "medica", default);
        _db.ChangeTracker.Clear();
        Entradas().Should().ContainSingle().Which.Valor.Should().Be(150m);
    }

    [Fact]
    public async Task Recuperacao_simula_sem_gravar_e_aplica_uma_vez()
    {
        // Sessões "antigas": lançadas sem preço (nenhuma entrada nasceu); depois a tabela foi cadastrada.
        var a1 = await AgendarAsync(await PacienteAsync()); await _agenda.ConcluirAtendimentoClinicoAsync(a1, "medica", default);
        var a2 = await AgendarAsync(await PacienteAsync()); await _agenda.ConcluirAtendimentoClinicoAsync(a2, "medica", default);
        _db.ChangeTracker.Clear();
        var servico = new ReceitaDoParticularService(_repo);

        var semPreco = await servico.RecuperarAsync(Dia.AddDays(-30), Dia);
        semPreco.Should().HaveCount(2).And.OnlyContain(s => s.SemPreco);

        await PrecoAsync(200m);
        var simulada = await servico.RecuperarAsync(Dia.AddDays(-30), Dia);
        simulada.Should().HaveCount(2).And.OnlyContain(s => s.Valor == 200m);
        _db.ChangeTracker.Clear();
        Entradas().Should().BeEmpty("simular não grava");

        await servico.RecuperarAsync(Dia.AddDays(-30), Dia, aplicar: true);
        _db.ChangeTracker.Clear();
        Entradas().Should().HaveCount(2).And.OnlyContain(e => e.Status == StatusLancamento.Realizado && e.FormaPagamento == null);
        (await servico.RecuperarAsync(Dia.AddDays(-30), Dia)).Should().BeEmpty("aplicar de novo não grava duas vezes");
    }
}
