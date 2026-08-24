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
/// OS ITENS NOVOS DAS DUAS FICHAS (parcela 77).
///
/// MÉDICO — o <b>retorno sugerido</b> e o <b>encaminhamento</b>. O plano terapêutico já
/// dizia "reavaliar em 4 semanas" em texto livre e nada podia agir sobre a frase; e o
/// encaminhamento entre as cinco especialidades da casa era conversa de corredor.
///
/// ENFERMAGEM — o <b>acesso venoso</b>. A clínica tem sala de infusão e o acesso morava em
/// texto corrido, de onde não se conta há quantos dias ele está no paciente.
///
/// O que estes testes fixam não são os campos: são os LUGARES por onde um campo de
/// prontuário some sem quebrar nada — a cópia do serviço, o versionamento, a porta que
/// não o edita, a busca e a exportação.
/// </summary>
public class RetornoEncaminhamentoEAcessoTests : IDisposable
{
    private readonly SqliteConnection _conn;
    private readonly ClinicaDbContext _db;
    private readonly ClinicaRepositorio _repo;
    private readonly ProntuarioService _prontuario;

    private static readonly DateOnly Dia = new(2026, 8, 20);
    private static readonly DateOnly Retorno = new(2026, 8, 27);

    private static readonly IdentificacaoExecutante Tecnica =
        new(UsuarioId: null, Nome: "Joana Técnica", Conselho: "COREN-SP 999999");

    public RetornoEncaminhamentoEAcessoTests()
    {
        _conn = new SqliteConnection("DataSource=:memory:");
        _conn.Open();
        _db = new ClinicaDbContext(
            new DbContextOptionsBuilder<ClinicaDbContext>().UseSqlite(_conn).Options);
        _db.Database.EnsureCreated();
        _repo = new ClinicaRepositorio(_db);
        _prontuario = new ProntuarioService(_repo);
    }

    private async Task<int> PacienteAsync()
    {
        var p = new Paciente { Nome = "Marisa Silva", Convenio = Convenio.UnimedIntercambio };
        _db.Pacientes.Add(p);
        await _db.SaveChangesAsync();
        return p.Id;
    }

    // ==================== O médico ====================

    /// <summary>
    /// ⚠️ O lugar 3 da auditoria de linha: <c>SalvarAsync</c> copia CAMPO A CAMPO, e o que
    /// ficar de fora da lista some — enquanto tudo o mais continua funcionando.
    /// </summary>
    [Fact]
    public async Task O_retorno_e_o_encaminhamento_sao_gravados()
    {
        var pacienteId = await PacienteAsync();

        var e = await _prontuario.SalvarAsync(new Evolucao
        {
            PacienteId = pacienteId,
            Data = Dia,
            Conduta = "Agulhamento lombar.",
            RetornoSugeridoEm = Retorno,
            RetornoSugeridoNota = "reavaliar a EVA",
            Encaminhamento = "psiquiatria daqui, para avaliar humor"
        });

        var gravada = await _repo.ObterEvolucaoAsync(e.Id);

        gravada!.RetornoSugeridoEm.Should().Be(Retorno);
        gravada.RetornoSugeridoNota.Should().Be("reavaliar a EVA");
        gravada.Encaminhamento.Should().Be("psiquiatria daqui, para avaliar humor");
    }

    /// <summary>
    /// A sessão que só decide o RETORNO é uma sessão. Sem os três na conta de "registro
    /// vazio", ela seria recusada — nomeando campos que a pessoa preencheu.
    /// </summary>
    [Fact]
    public async Task Sessao_SO_com_retorno_e_aceita()
    {
        var pacienteId = await PacienteAsync();

        var e = await _prontuario.SalvarAsync(new Evolucao
        {
            PacienteId = pacienteId, Data = Dia, RetornoSugeridoEm = Retorno
        });

        e.Id.Should().BeGreaterThan(0);
    }

    /// <summary>
    /// ⚠️ O lugar 4: alterar registro clínico GUARDA o que ele dizia antes (art. 3º da Lei
    /// 13.787/2018). Sem os três na versão, corrigir a sessão apagaria o retorno anterior
    /// sem deixar rastro — e a trilha diria "EvolucaoAlterada" sobre um texto que sumiu.
    /// </summary>
    [Fact]
    public async Task Corrigir_a_sessao_GUARDA_o_retorno_anterior()
    {
        var pacienteId = await PacienteAsync();

        var e = await _prontuario.SalvarAsync(new Evolucao
        {
            PacienteId = pacienteId, Data = Dia, Conduta = "primeira",
            RetornoSugeridoEm = Retorno, RetornoSugeridoNota = "reavaliar a EVA",
            Encaminhamento = "ortopedia"
        });

        // Como a produção faz: uma entidade NOVA com o mesmo Id, nunca a rastreada.
        await _prontuario.SalvarAsync(new Evolucao
        {
            Id = e.Id, PacienteId = pacienteId, Data = Dia, Conduta = "segunda",
            RetornoSugeridoEm = Retorno.AddDays(7), RetornoSugeridoNota = "trazer a ressonância",
            Encaminhamento = null
        }, motivoDaCorrecao: "a data estava errada");

        var versao = (await _repo.VersoesDaEvolucaoAsync(e.Id)).Should().ContainSingle().Subject;

        versao.RetornoSugeridoEm.Should().Be(Retorno, "a versão guarda o que a sessão dizia ANTES");
        versao.RetornoSugeridoNota.Should().Be("reavaliar a EVA");
        versao.Encaminhamento.Should().Be("ortopedia",
            "encaminhamento apagado na correção continua provável de ter existido");
    }

    /// <summary>
    /// A busca do prontuário é UMA definição (parcela 77) e varre os campos novos. Campo
    /// que a busca não varre é campo que o profissional não reencontra — e "para quem eu
    /// mandei esta paciente?" é a pergunta que se faz seis meses depois.
    /// </summary>
    [Fact]
    public void A_busca_acha_pelo_encaminhamento_e_pela_nota_do_retorno()
    {
        var e = new Evolucao
        {
            Data = Dia,
            Encaminhamento = "Psiquiatria daqui, para avaliar humor",
            RetornoSugeridoNota = "trazer a ressonância"
        };

        BuscaNoProntuario.Casa(e, "psiquiatria").Should().BeTrue("sem caixa");
        BuscaNoProntuario.Casa(e, "ressonancia").Should().BeTrue("sem acento");
        BuscaNoProntuario.Casa(e, "fisioterapia").Should().BeFalse();
    }

    [Fact]
    public async Task O_retorno_sai_na_exportacao_e_no_relatorio()
    {
        var pacienteId = await PacienteAsync();
        await _prontuario.SalvarAsync(new Evolucao
        {
            PacienteId = pacienteId, Data = Dia, Conduta = "Agulhamento.",
            RetornoSugeridoEm = Retorno, RetornoSugeridoNota = "reavaliar a EVA",
            Encaminhamento = "ortopedia do convênio"
        });

        var arquivos = await new ExportacaoProntuarioService(_repo).ExportarAsync(pacienteId);
        var sessoes = arquivos.Single(a => a.Nome == "prontuario-sessoes.csv");

        sessoes.Conteudo.Should().Contain("reavaliar a EVA")
            .And.Contain("ortopedia do convênio")
            .And.Contain("2026-08-27", "a exportação usa ISO: é CSV que outro sistema lê, não papel");
    }

    // ==================== A enfermagem ====================

    [Fact]
    public async Task O_acesso_venoso_e_gravado_e_conta_os_dias()
    {
        var pacienteId = await PacienteAsync();
        var servico = new EvolucaoEnfermagemService(_repo, () => Dia.ToDateTime(new TimeOnly(12, 0)));

        var e = await servico.RegistrarAsync(
            pacienteId, Dia, new TimeOnly(9, 0), "Passagem de plantão.", Tecnica,
            acesso: new AcessoVenoso("MSD, antecubital", "20G", Dia.AddDays(-3)));

        e.AcessoLocal.Should().Be("MSD, antecubital");
        e.AcessoCalibre.Should().Be("20G");
        e.TemAcesso.Should().BeTrue();
        e.DiasDeAcesso.Should().Be(3, "é a conta que decide a troca do cateter");
        e.AcessoResumo.Should().Contain("20G").And.Contain("há 3 dias");
    }

    /// <summary>
    /// Sem punção informada, os DIAS são nulos — nunca zero. Zero diria "puncionado hoje",
    /// que é uma afirmação que ninguém fez.
    /// </summary>
    [Fact]
    public async Task Sem_a_data_da_puncao_os_dias_sao_NULOS_e_nao_zero()
    {
        var pacienteId = await PacienteAsync();
        var servico = new EvolucaoEnfermagemService(_repo, () => Dia.ToDateTime(new TimeOnly(12, 0)));

        var e = await servico.RegistrarAsync(
            pacienteId, Dia, new TimeOnly(9, 0), "Curativo.", Tecnica,
            acesso: new AcessoVenoso("MSD", "22G"));

        e.DiasDeAcesso.Should().BeNull();
        e.AcessoResumo.Should().NotContain("hoje");
    }

    /// <summary>
    /// ⚠️ O que RETIFICA recebe tudo o que o que REGISTRA recebe. Sem isso a correção vira
    /// reescrita mutilada, e a versão corrigida passa a AFIRMAR que o paciente não tinha
    /// acesso venoso.
    /// </summary>
    [Fact]
    public async Task Retificar_leva_o_acesso_junto()
    {
        var pacienteId = await PacienteAsync();
        var servico = new EvolucaoEnfermagemService(_repo, () => Dia.ToDateTime(new TimeOnly(12, 0)));

        var original = await servico.RegistrarAsync(
            pacienteId, Dia, new TimeOnly(9, 0), "Passagem.", Tecnica,
            acesso: new AcessoVenoso("MSD", "20G", Dia.AddDays(-1)));

        var corrigida = await servico.RetificarAsync(
            original.Id, Dia, new TimeOnly(9, 30), "Passagem — hora corrigida.", Tecnica,
            motivoRetificacao: "digitei 9h",
            acesso: new AcessoVenoso("MSE", "22G", Dia.AddDays(-1)));

        corrigida.AcessoLocal.Should().Be("MSE");
        corrigida.AcessoCalibre.Should().Be("22G");
    }

    [Fact]
    public async Task O_acesso_sai_na_exportacao()
    {
        var pacienteId = await PacienteAsync();
        var servico = new EvolucaoEnfermagemService(_repo, () => Dia.ToDateTime(new TimeOnly(12, 0)));

        await servico.RegistrarAsync(
            pacienteId, Dia, new TimeOnly(9, 0), "Passagem.", Tecnica,
            acesso: new AcessoVenoso("MSD, antecubital", "20G", Dia.AddDays(-2)));

        var arquivos = await new ExportacaoProntuarioService(_repo).ExportarAsync(pacienteId);

        arquivos.Single(a => a.Nome == "prontuario-enfermagem.csv").Conteudo
            .Should().Contain("MSD, antecubital").And.Contain("20G");
    }

    /// <summary>
    /// ⚠️ Dado calculado sem LEITOR é o defeito recorrente do projeto, e o acesso nasceu
    /// assim: os três derivados existiam e nenhuma tela os mostrava. Este teste fixa os
    /// leitores — a trilha de auditoria e a exportação do titular (art. 18 II) —, que são
    /// os dois que não dependem de WPF para serem exercitados.
    /// </summary>
    [Fact]
    public async Task O_acesso_e_LIDO_na_trilha_e_na_exportacao_do_titular()
    {
        var pacienteId = await PacienteAsync();
        var servico = new EvolucaoEnfermagemService(_repo, () => Dia.ToDateTime(new TimeOnly(12, 0)));

        await servico.RegistrarAsync(
            pacienteId, Dia, new TimeOnly(9, 0), "Passagem.", Tecnica,
            acesso: new AcessoVenoso("MSD, antecubital", "20G", Dia.AddDays(-3)));

        var evento = (await _db.Auditoria.ToListAsync()).Should().ContainSingle().Subject;
        evento.Detalhe.Should().Contain("acesso: MSD, antecubital")
            .And.Contain("há 3 dias");

        var pacote = await new TitularDadosService(
            _repo,
            new ConsentimentoService(_repo),
            new ProntuarioService(_repo),
            new DocumentoClinicoService(
                _repo, new ProntuarioService(_repo), new ConsentimentoService(_repo)))
            .ExportarAsync(pacienteId, "gerente");
        pacote.Should().Contain("acesso venoso").And.Contain("20G");
    }

    public void Dispose()
    {
        _db.Dispose();
        _conn.Dispose();
    }
}
