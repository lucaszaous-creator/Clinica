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
/// O CIRCUITO CLÍNICO de ponta a ponta (parcela 61) — o irmão do
/// <see cref="CircuitoCompletoTests"/>, que cobre agenda → faturamento → financeiro e não
/// tinha um elo clínico sequer.
///
/// Por que um arquivo próprio: o que liga a prescrição à sala, a sala à alergia e a
/// alergia à próxima prescrição não é chamada de método, é CHAVE ESTRANGEIRA sobre o
/// mesmo banco. Cada serviço tem os seus testes e todos podem passar com o circuito
/// partido — elo partido aqui não vira erro, vira LISTA VAZIA, que é indistinguível de
/// um dia sem infusão. Estes testes existem para a lista vazia ter quem a acuse.
///
/// Os quatro circuitos cobertos:
/// 1. Prescrição assinada → fila da sala → checagem → encerramento (e a folha some da fila).
/// 2. Sala de infusão → painel da DIREÇÃO (o alerta de folha aguardando, e o zero honesto).
/// 3. Reação observada na sala → ALERGIA na lista de problemas → a PRÓXIMA prescrição é
///    recusada na assinatura.
/// 4. Acesso ao prontuário registrado por uma porta → respondido por "quem abriu este
///    prontuário" (a leitura própria da parcela 52, que ficou sem chamador até aqui).
/// E, do lado do domínio puro, a regra ÚNICA de mover a fila nos dois quadros (parcela 61).
/// </summary>
public class CircuitoClinicoTests : IDisposable
{
    private readonly SqliteConnection _conn;
    private readonly ClinicaDbContext _db;
    private readonly ClinicaRepositorio _repo;
    private readonly PrescricaoInternaService _prescricoes;
    private readonly ChecagemPrescricaoService _checagens;
    private readonly ProblemaPacienteService _problemas;

    private static DateTime MeioDia => DateTime.Today.AddHours(12);
    private static DateOnly Hoje => DateOnly.FromDateTime(DateTime.Today);

    private static readonly IdentificacaoExecutante Tecnica =
        new(UsuarioId: null, Nome: "Joana Técnica", Conselho: "COREN-SP 999999");

    public CircuitoClinicoTests()
    {
        _conn = new SqliteConnection("DataSource=:memory:");
        _conn.Open();
        var options = new DbContextOptionsBuilder<ClinicaDbContext>().UseSqlite(_conn).Options;
        _db = new ClinicaDbContext(options);
        _db.Database.EnsureCreated();
        _repo = new ClinicaRepositorio(_db);

        _prescricoes = new PrescricaoInternaService(_repo, new PrescricaoService(_repo));
        // Relógio congelado ao meio-dia, como em PrescricaoInternaTests: a recusa de hora
        // futura precisa de um "agora" conhecido para o teste não virar loteria à noite.
        _checagens = new ChecagemPrescricaoService(_repo, () => MeioDia);
        _problemas = new ProblemaPacienteService(_repo);
    }

    // =====================================================================
    // Circuito 1 — prescrição → sala → checagem → encerramento
    // =====================================================================

    [Fact]
    public async Task Folha_assinada_atravessa_a_sala_e_o_encerramento_a_tira_da_fila()
    {
        var prescricao = await AssinadaAsync();

        // A folha assinada ENTRA na fila da sala e conta como pendente do dia.
        (await _checagens.PendentesDoDiaAsync(Hoje)).Should().Be(1,
            "folha assinada com item sem checagem é o que a sala existe para mostrar");
        (await _checagens.DoDiaAsync(Hoje)).Should().ContainSingle(p => p.Id == prescricao.Id);

        // A técnica checa o único item e encerra.
        var folha = await CarregarAsync(prescricao.Id);
        await _checagens.ChecarAsync(
            folha.Itens[0].Id, SituacaoChecagem.Realizado, Agora(), Tecnica);
        await _checagens.EncerrarAsync(prescricao.Id, Tecnica);

        // Encerrada, ela SAI dos pendentes e da fila normal — e continua alcançável na
        // conferência do fim do dia (incluirEncerradas), porque encerrar não apaga nada.
        (await _checagens.PendentesDoDiaAsync(Hoje)).Should().Be(0);
        (await _checagens.DoDiaAsync(Hoje)).Should().BeEmpty();
        (await _checagens.DoDiaAsync(Hoje, incluirEncerradas: true))
            .Should().ContainSingle(p => p.Id == prescricao.Id);
    }

    // =====================================================================
    // Circuito 2 — a sala chega ao painel da direção
    // =====================================================================

    /// <summary>
    /// O painel é o teste certo para o FIM do circuito porque ele não calcula nada: se o
    /// elo estiver partido, o número não some com erro — aparece ZERADO, e zero por
    /// defeito é indistinguível de um dia sem infusão. Por isso o teste também afirma
    /// que o bloco NÃO caiu em NaoVerificados.
    /// </summary>
    [Fact]
    public async Task Folha_aguardando_chega_ao_painel_da_direcao_e_sai_quando_encerrada()
    {
        var prescricao = await AssinadaAsync();
        var painel = MontarPainel();

        var antes = await painel.MontarAsync(Hoje);
        antes.NaoVerificados.Should().NotContain("Sala de infusão",
            "bloco que falha calado zeraria o alerta com cara de dia tranquilo");
        antes.FolhasInfusaoAguardando.Should().Be(1);
        antes.Alertas.Should().Contain(a => a.Assunto == AssuntoDirecao.InfusaoAguardando);

        var folha = await CarregarAsync(prescricao.Id);
        await _checagens.ChecarAsync(
            folha.Itens[0].Id, SituacaoChecagem.Realizado, Agora(), Tecnica);
        await _checagens.EncerrarAsync(prescricao.Id, Tecnica);

        var depois = await painel.MontarAsync(Hoje);
        depois.FolhasInfusaoAguardando.Should().Be(0);
        depois.Alertas.Should().NotContain(a => a.Assunto == AssuntoDirecao.InfusaoAguardando);
    }

    /// <summary>
    /// O OUTRO elo da sala com a direção: a folha encerrada que ainda deve a 2ª assinatura.
    ///
    /// A Sala de infusão passou a mostrá-la; sem esta contagem a direção continuaria sem
    /// ver a soma, que é a razão de o alerta da infusão existir ("a sala vê a própria fila,
    /// a direção é quem vê a soma"). E os dois alertas são SEPARADOS de propósito: um se
    /// resolve administrando o que falta, o outro com o certificado de quem executou.
    /// </summary>
    [Fact]
    public async Task Folha_sem_a_2a_assinatura_chega_ao_painel_da_direcao()
    {
        var prescricao = await AssinadaAsync(exigirAssinaturaDaExecucao: true);
        var painel = MontarPainel();

        var folha = await CarregarAsync(prescricao.Id);
        await _checagens.ChecarAsync(
            folha.Itens[0].Id, SituacaoChecagem.Realizado, Agora(), Tecnica);
        await _checagens.EncerrarAsync(prescricao.Id, Tecnica);

        var resumo = await painel.MontarAsync(Hoje);

        resumo.NaoVerificados.Should().NotContain("Sala de infusão",
            "bloco que falha calado zeraria o alerta com cara de dia tranquilo");
        resumo.FolhasInfusaoSemAssinatura.Should().Be(1);
        resumo.Alertas.Should().Contain(a => a.Assunto == AssuntoDirecao.InfusaoSemAssinatura);

        // Encerrada e checada: o alerta de ITEM aguardando saiu, e o da ASSINATURA ficou.
        // Somar os dois daria um número que não diz o que fazer.
        resumo.FolhasInfusaoAguardando.Should().Be(0);
    }

    // =====================================================================
    // Circuito 3 — a reação de hoje protege a prescrição de amanhã
    // =====================================================================

    [Fact]
    public async Task Reacao_observada_na_sala_recusa_a_assinatura_da_proxima_prescricao()
    {
        var prescricao = await AssinadaAsync(descricaoItem: "Dipirona sódica", dose: "1 g");
        var folha = await CarregarAsync(prescricao.Id);

        // A "rodela" com reação: a técnica registra que NÃO fez, por reação alérgica, e
        // confirma a gravação da alergia — o mesmo SaveChanges da checagem.
        await _checagens.ChecarAsync(
            folha.Itens[0].Id, SituacaoChecagem.NaoRealizado, Agora(), Tecnica,
            justificativa: "Paciente apresentou reação alérgica e recusou",
            alergiaObservada: "Dipirona");

        // O fato atravessou para a LISTA DE PROBLEMAS…
        var problemas = await _problemas.DoPacienteAsync(prescricao.PacienteId);
        problemas.Should().Contain(p => p.Natureza == NaturezaProblema.Alergia
                                        && p.Descricao.Contains("Dipirona"));

        // …e é a lista de problemas que TRAVA a próxima prescrição com o mesmo agente:
        // a assinatura é recusada até alguém confirmar de propósito.
        var nova = await _prescricoes.CriarAsync(
            prescricao.PacienteId, prescricao.ProfissionalId);
        await ComItensAsync(nova.Id, "Dipirona sódica", dose: "1 g");

        var erro = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _prescricoes.AssinarAsync(nova.Id, Assinatura()));
        erro.Message.Should().Contain("ALERGIA");
    }

    // =====================================================================
    // Circuito 4 — quem abriu o prontuário tem resposta própria
    // =====================================================================

    [Fact]
    public async Task Acesso_registrado_pela_porta_responde_quem_abriu_este_prontuario()
    {
        var pacienteId = await PacienteAsync();
        var relogio = MeioDia;
        var acessos = new AcessoProntuarioService(_repo, () => relogio);

        (await acessos.RegistrarAsync(pacienteId, "ana", OrigemAcessoProntuario.Documento))
            .Should().BeTrue();

        // Mesma pessoa, mesma porta, dentro da janela de silêncio: NÃO duplica.
        (await acessos.RegistrarAsync(pacienteId, "ana", OrigemAcessoProntuario.Documento))
            .Should().BeFalse();

        // Porta DIFERENTE é acesso de natureza diferente — gera linha própria.
        (await acessos.RegistrarAsync(pacienteId, "ana", OrigemAcessoProntuario.ProntuarioClinico))
            .Should().BeTrue();

        var trilha = await acessos.DoPacienteAsync(pacienteId);
        trilha.Should().HaveCount(2,
            "a leitura própria da parcela 52 é o que responde 'quem abriu, quando e por onde'");
        trilha.Should().OnlyContain(e => e.Operador == "ana");
        trilha.Select(e => e.Detalhe).Should().Contain("Documento do prontuário aberto");
    }

    // =====================================================================
    // Domínio — mover a fila é UM ato com UMA regra nos dois quadros
    // =====================================================================

    [Fact]
    public void Mover_a_fila_tem_uma_regra_so_e_o_profissional_a_recebe_por_padrao()
    {
        var fila = Permissao.EditarAgenda | Permissao.MovimentarFila;

        // Quem atende move a PRÓPRIA fila sem ganhar o balcão junto.
        var profissional = PerfisAcesso.Padrao(PerfilAcesso.Profissional);
        profissional.HasFlag(Permissao.MovimentarFila).Should().BeTrue();
        profissional.HasFlag(Permissao.EditarAgenda).Should().BeFalse(
            "marcar e remarcar horário de terceiros continua sendo do balcão");

        // O balcão continua movendo a fila como ontem — o bit novo não tira nada.
        PerfisAcesso.Padrao(PerfilAcesso.Recepcao)
            .HasFlag(Permissao.MovimentarFila).Should().BeTrue();

        // A enfermagem nunca moveu a fila por nenhum quadro; continua sem mover.
        var sessaoEnfermagem = new SessaoUsuario();
        sessaoEnfermagem.Entrar(new UsuarioSistema
        {
            Id = 8, Nome = "Rita", Login = "rita", Perfil = PerfilAcesso.Enfermagem
        });
        sessaoEnfermagem.PodeAlgum(fila).Should().BeFalse();
        var mover = () => sessaoEnfermagem.ExigirAlgum(fila, "mexer na fila do dia");
        mover.Should().Throw<InvalidOperationException>().WithMessage("*fila do dia*");

        // E o profissional passa — pelo bit NOVO, sem EditarAgenda.
        var sessaoMedico = new SessaoUsuario();
        sessaoMedico.Entrar(new UsuarioSistema
        {
            Id = 9, Nome = "Ana", Login = "dra.ana", Perfil = PerfilAcesso.Profissional
        });
        sessaoMedico.PodeAlgum(fila).Should().BeTrue();
    }

    // ==================== Apoio ====================

    /// <summary>
    /// Monta o painel da direção com as dependências reais, à mão — é o que prova que o
    /// grafo do Gerente fecha sem o contêiner de DI, agora com o 13º serviço
    /// (<see cref="ChecagemPrescricaoService"/>).
    /// </summary>
    private PainelDirecaoService MontarPainel()
    {
        var parametros = new ParametrosService(_repo);
        var contas = new ContasService(_repo);
        var pendencias = new PendenciaService(_repo, parametros);
        var financeiro = new FinanceiroService(_repo);

        return new PainelDirecaoService(
            _repo,
            financeiro,
            contas,
            new RecebiveisService(_repo),
            new FechamentoCaixaService(_repo),
            new RodadaPendenciasService(_repo, pendencias, parametros),
            pendencias,
            new InadimplenciaService(_repo, financeiro),
            new ReceitaGlosadaService(_repo, financeiro),
            new MetaService(_repo, financeiro, new IndicadoresService(_repo, parametros)),
            new OrcamentoService(_repo, contas),
            new ConsultorioService(_repo),
            _checagens);
    }

    private static TimeOnly Agora() => TimeOnly.FromDateTime(MeioDia);

    private static AssinaturaDocumento Assinatura()
        => new()
        {
            Tipo = TipoAssinatura.IcpBrasil,
            HashConteudo = new string('a', 64),
            NomeAssinante = "Dra. Ana Souza",
            RegistroConselho = "CRM-SP 123456",
            CpfAssinante = "12345678909",
            CertificadoTitular = "Dra. Ana Souza",
            AssinadoEm = DateTime.Now
        };

    private async Task<int> PacienteAsync()
    {
        var paciente = new Paciente { Nome = "Maria de Teste", Convenio = Convenio.UnimedIntercambio };
        _db.Pacientes.Add(paciente);
        await _db.SaveChangesAsync();
        return paciente.Id;
    }

    private Task ComItensAsync(
        int prescricaoId, string descricao, string? dose,
        bool exigirAssinaturaDaExecucao = false)
        => _prescricoes.SalvarRascunhoAsync(prescricaoId, "Circuito", null,
        [
            new ItemPrescricaoInterna
            {
                Descricao = descricao,
                Dose = dose,
                Via = ViaAdministracao.Endovenosa,
                TempoInfusao = "30 min"
            }
        ], exigeAssinaturaEletronicaDaExecucao: exigirAssinaturaDaExecucao);

    private async Task<PrescricaoInterna> AssinadaAsync(
        string descricaoItem = "Soro fisiológico 0,9%", string? dose = "500 mL",
        bool exigirAssinaturaDaExecucao = false)
    {
        var pacienteId = await PacienteAsync();
        var profissional = new Profissional { Nome = "Dra. Ana Souza", RegistroConselho = "CRM-SP 123456" };
        _db.Profissionais.Add(profissional);
        await _db.SaveChangesAsync();

        var prescricao = await _prescricoes.CriarAsync(pacienteId, profissional.Id);
        await ComItensAsync(prescricao.Id, descricaoItem, dose, exigirAssinaturaDaExecucao);
        await _prescricoes.AssinarAsync(prescricao.Id, Assinatura());
        return prescricao;
    }

    private async Task<PrescricaoInterna> CarregarAsync(int prescricaoId)
    {
        _db.ChangeTracker.Clear();
        return (await _repo.ObterPrescricaoInternaAsync(prescricaoId))!;
    }

    public void Dispose()
    {
        _db.Dispose();
        _conn.Dispose();
    }
}
