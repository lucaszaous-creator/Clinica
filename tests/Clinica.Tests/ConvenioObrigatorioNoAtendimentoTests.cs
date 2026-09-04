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
/// SEM CONVÊNIO NÃO SE LANÇA ATENDIMENTO (parcela 92).
///
/// O defeito que motivou a parcela
/// -------------------------------
/// A importação do Smart Clinic (set/2026) trouxe <b>2.021 das 2.238 fichas sem
/// convênio</b>, todas no código <see cref="ConvenioCadastro.CodigoADefinir"/> — um
/// convênio de catálogo que NÃO gera guia. A decisão de então foi deliberada e continua
/// certa: ninguém decide 2.021 fichas antes de importar, e a escolha acontece com o
/// paciente na frente.
///
/// O que não funcionou foi o AVISO. A única defesa era o alerta vermelho do
/// <c>ElegibilidadeService</c>, e esse serviço "NUNCA impede o atendimento: quem decide é
/// a clínica" — é o contrato dele, escrito no próprio arquivo. Na prática a sessão era
/// lançada por cima do vermelho, os códigos nasciam <see cref="StatusCodigo.NaoAplicavel"/>,
/// a tela dizia "Atendimento registrado" e o faturamento não via guia nenhuma. A diferença
/// aparecia no fim do mês, quando não há mais o que fazer.
///
/// A regra que a direção fixou, e que estes testes amarram
/// ------------------------------------------------------
/// <b>O convênio é condição do lançamento.</b> A recusa mora em
/// <c>AtendimentoService.MontarAsync</c> — a montagem por onde TODAS as portas passam —,
/// e não numa tela: o avulso do balcão, o lançamento sobre o horário do dia, a confirmação
/// de presença da Fila e a marcação com "guia no agendamento" ligado desembocam ali.
/// A tela pergunta ANTES (a janela de convênios do Novo atendimento) para que a recusa
/// chegue como pergunta respondível; estes testes provam que ela não depende disso.
///
/// ⚠️ PARTICULAR NÃO É "SEM CONVÊNIO", e o teste que fixa a diferença está aqui: o
/// particular é uma escolha REGISTRADA (<c>ConvenioCadastro.GeraGuia</c> desmarcado,
/// parcela 60) e continua lançando normalmente. O que se recusa é a ficha em que ninguém
/// escolheu ainda — que é uma pergunta em aberto, não uma resposta.
/// </summary>
public class ConvenioObrigatorioNoAtendimentoTests : IDisposable
{
    private const string CodigoParticular = "Particular";

    private readonly SqliteConnection _conn;
    private readonly ClinicaDbContext _db;
    private readonly ClinicaRepositorio _repo;
    private readonly ParametrosService _parametros;
    private readonly AtendimentoService _atendimentos;
    private readonly AgendaService _agenda;
    private readonly PacienteService _pacientes;


    /// <summary>
    /// Todo horário MARCADO precisa de dono desde a parcela 95 — o fixture cria um para os
    /// cenários que não se importam com QUEM atende.
    /// </summary>
    private readonly int _profPadrao;

    public ConvenioObrigatorioNoAtendimentoTests()
    {
        _conn = new SqliteConnection("DataSource=:memory:");
        _conn.Open();
        var options = new DbContextOptionsBuilder<ClinicaDbContext>().UseSqlite(_conn).Options;
        _db = new ClinicaDbContext(options);
        _db.Database.EnsureCreated();
        var profPadrao = new Profissional { Nome = "Dra. Padrão" };
        _db.Profissionais.Add(profPadrao);
        _db.SaveChanges();
        _profPadrao = profPadrao.Id;
        _repo = new ClinicaRepositorio(_db);
        _parametros = new ParametrosService(_repo);
        _atendimentos = new AtendimentoService(_repo, parametros: _parametros);
        _agenda = new AgendaService(_repo, _atendimentos, _parametros);
        _pacientes = new PacienteService(_repo);

        // O catálogo é um cache estático: cada teste monta o seu.
        CatalogoConvenios.Atualizar(
        [
            new EntradaConvenio(ConvenioCadastro.CodigoADefinir, "A definir (importado sem convênio)",
                Convenio.Personalizado, true, new ConfiguracaoRegraGenerica(),
                FormatoNumeroGuia.SemValidacao, GeraGuia: false),

            new EntradaConvenio(CodigoParticular, "Particular", Convenio.Personalizado, true,
                new ConfiguracaoRegraGenerica { FazEletro = true, TemSegundoCodigo = true },
                FormatoNumeroGuia.SemValidacao, GeraGuia: false),

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

    private static readonly DateOnly Dia = new(2026, 9, 3);

    private static DateTime SemanaQueVem => DateTime.Today.AddDays(7).AddHours(14);

    /// <summary>A ficha como a importação a deixa: nome, e um convênio que é uma pergunta.</summary>
    private async Task<int> PacienteSemConvenioAsync(string nome = "Maria")
    {
        var p = new Paciente
        {
            Nome = nome,
            Convenio = Convenio.Personalizado,
            ConvenioCodigo = ConvenioCadastro.CodigoADefinir,
            Sexo = Sexo.Feminino,
            ChaveImportacao = "IMPORT:smartclinic:pacientes:1234"
        };
        _db.Pacientes.Add(p);
        await _db.SaveChangesAsync();
        return p.Id;
    }

    private async Task<int> PacienteComConvenioAsync(string codigo, Convenio familia)
    {
        var p = new Paciente
        {
            Nome = "João", Convenio = familia, ConvenioCodigo = codigo, Sexo = Sexo.Masculino
        };
        _db.Pacientes.Add(p);
        await _db.SaveChangesAsync();
        return p.Id;
    }

    // ================================================================
    // A RECUSA — as quatro portas
    // ================================================================

    /// <summary>
    /// O caso central: o lançamento avulso do balcão. Recusado, e recusado com o NOME do
    /// paciente na mensagem — quem está no balcão precisa saber de quem é a ficha.
    /// </summary>
    [Fact]
    public async Task Lancar_atendimento_de_paciente_sem_convenio_e_recusado()
    {
        var pacienteId = await PacienteSemConvenioAsync();

        var erro = await Assert.ThrowsAsync<ConvenioNaoDefinidoException>(
            () => _atendimentos.LancarAsync(pacienteId, Dia, ModalidadeAtendimento.AcupunturaComEletro));

        erro.PacienteId.Should().Be(pacienteId);
        erro.PacienteNome.Should().Be("Maria");
        erro.Message.Should().Contain("Maria");
    }

    /// <summary>
    /// E nada fica gravado pela metade. É a exigência que separa "recusou" de "recusou
    /// depois de sujar o banco": um atendimento sem código, ou um código órfão, seria pior
    /// do que a sessão sem guia que a parcela veio impedir.
    /// </summary>
    [Fact]
    public async Task A_recusa_nao_grava_atendimento_nem_codigo()
    {
        var pacienteId = await PacienteSemConvenioAsync();

        await Assert.ThrowsAsync<ConvenioNaoDefinidoException>(
            () => _atendimentos.LancarAsync(pacienteId, Dia, ModalidadeAtendimento.AcupunturaComEletro));

        _db.Atendimentos.Should().BeEmpty();
        _db.Codigos.Should().BeEmpty();
    }

    /// <summary>
    /// A porta da FILA: o paciente que já tinha horário e chegou. A confirmação de
    /// presença é o outro caminho que monta atendimento, e ela é recusada pela mesma
    /// regra — sem isso, bastaria marcar por telefone para furar a fila da regra.
    /// </summary>
    [Fact]
    public async Task Confirmar_presenca_de_paciente_sem_convenio_e_recusado()
    {
        var pacienteId = await PacienteSemConvenioAsync();

        // A chave fica DESLIGADA: sem ela a marcação não cria atendimento, e é o check-in
        // que monta — que é justamente o vão que este teste cobre.
        var ag = await _agenda.AgendarAsync(
            pacienteId, SemanaQueVem, ModalidadeAtendimento.AcupunturaComEletro, null, profissionalId: _profPadrao);

        await Assert.ThrowsAsync<ConvenioNaoDefinidoException>(
            () => _agenda.ConfirmarPresencaAsync(ag.Id));

        var depois = await _repo.ObterAgendamentoAsync(ag.Id);
        depois!.Status.Should().NotBe(StatusAgendamento.Realizado,
            "a presença não pode ficar carimbada sobre um atendimento que não nasceu");
        depois.AtendimentoId.Should().BeNull();
    }

    /// <summary>
    /// A porta da MARCAÇÃO, com "guia no agendamento" ligado (parcela 70): aí o
    /// atendimento nasce junto do horário, e a recusa precisa alcançá-lo lá.
    /// </summary>
    [Fact]
    public async Task Com_guia_na_marcacao_marcar_paciente_sem_convenio_e_recusado()
    {
        await _parametros.DefinirGuiaNoAgendamentoAsync(true);
        var pacienteId = await PacienteSemConvenioAsync();

        await Assert.ThrowsAsync<ConvenioNaoDefinidoException>(
            () => _agenda.AgendarAsync(
                pacienteId, SemanaQueVem, ModalidadeAtendimento.AcupunturaComEletro, null, profissionalId: _profPadrao));

        _db.Agendamentos.Should().BeEmpty("o horário e a guia nascem no mesmo grafo — ou os dois, ou nenhum");
    }

    // ================================================================
    // O QUE NÃO É "SEM CONVÊNIO"
    // ================================================================

    /// <summary>
    /// O PARTICULAR continua lançando. É a assimetria que a parcela precisa manter: quem
    /// paga do bolso tem escolha registrada, e recusá-lo seria devolver a clínica às duas
    /// saídas ruins que a parcela 60 aposentou.
    /// </summary>
    [Fact]
    public async Task Particular_nao_e_sem_convenio_e_continua_lancando()
    {
        var pacienteId = await PacienteComConvenioAsync(CodigoParticular, Convenio.Personalizado);

        var r = await _atendimentos.LancarAsync(
            pacienteId, Dia, ModalidadeAtendimento.AcupunturaComEletro);

        r.Atendimento.Id.Should().BeGreaterThan(0);
        r.Atendimento.Codigos.Should().NotBeEmpty();
        r.Atendimento.Codigos.Should().OnlyContain(c => c.Status == StatusCodigo.NaoAplicavel,
            "particular registra a sessão sem mandar guia a operadora nenhuma");
    }

    /// <summary>O convênio de verdade, que é o caso da imensa maioria, segue intocado.</summary>
    [Fact]
    public async Task Convenio_comum_continua_gerando_guia()
    {
        var pacienteId = await PacienteComConvenioAsync("Amil", Convenio.Amil);

        var r = await _atendimentos.LancarAsync(
            pacienteId, Dia, ModalidadeAtendimento.AcupunturaComEletro);

        r.Atendimento.Codigos.Should().Contain(c => c.Status != StatusCodigo.NaoAplicavel);
    }

    // ================================================================
    // A SAÍDA — vincular o convênio e seguir
    // ================================================================

    /// <summary>
    /// A parcela inteira em um teste: recusado, escolhido o convênio, lançado — e a guia
    /// nasce. Sem esta metade, a regra seria um balcão travado.
    /// </summary>
    [Fact]
    public async Task Depois_de_vincular_o_convenio_o_lancamento_passa_e_gera_guia()
    {
        var pacienteId = await PacienteSemConvenioAsync();

        await Assert.ThrowsAsync<ConvenioNaoDefinidoException>(
            () => _atendimentos.LancarAsync(pacienteId, Dia, ModalidadeAtendimento.AcupunturaComEletro));

        await _pacientes.DefinirConvenioAsync(pacienteId, "Amil", operador: "flavia@");

        var r = await _atendimentos.LancarAsync(
            pacienteId, Dia, ModalidadeAtendimento.AcupunturaComEletro);

        r.Atendimento.Codigos.Should().Contain(c => c.Status != StatusCodigo.NaoAplicavel,
            "escolhido o convênio, a sessão volta a chegar ao faturamento");
    }

    /// <summary>
    /// O vínculo grava o PAR que toda entidade carrega — o código (a operadora) e a
    /// família (a REGRA de faturamento) —, e a família sai do CATÁLOGO, nunca de um enum
    /// digitado pela tela. Resolver só pelo enum faria toda operadora cadastrada pela
    /// clínica virar "Personalizado".
    /// </summary>
    [Fact]
    public async Task Vincular_grava_codigo_familia_e_recalcula_a_categoria()
    {
        var pacienteId = await PacienteSemConvenioAsync();

        await _pacientes.DefinirConvenioAsync(pacienteId, CodigoParticular, operador: "flavia@");

        var p = await _repo.ObterPacienteAsync(pacienteId);
        p!.ConvenioCodigo.Should().Be(CodigoParticular);
        p.Convenio.Should().Be(Convenio.Personalizado);
        p.ConvenioADefinir.Should().BeFalse();
        p.ConvenioNome.Should().Be("Particular");
        p.Categoria.Should().Be(CategoriaConvenio.Base(Convenio.Personalizado, p.PossuiApp),
            "a categoria é derivada do convênio + app, como no cadastro");
    }

    /// <summary>
    /// A carteirinha é OPCIONAL e vem junto porque é o mesmo instante e a mesma pessoa —
    /// mas em branco PRESERVA o que a ficha já tinha. Apagar em silêncio um número que
    /// alguém digitou seria a janela do balcão regravando a ficha que ela não abriu.
    /// </summary>
    [Fact]
    public async Task Vincular_sem_carteirinha_preserva_a_que_a_ficha_ja_tinha()
    {
        var pacienteId = await PacienteSemConvenioAsync();
        var antes = await _repo.ObterPacienteAsync(pacienteId);
        antes!.Carteirinha = "0123456789";
        antes.ValidadeCarteirinha = new DateOnly(2027, 1, 31);
        await _repo.SalvarAsync();

        await _pacientes.DefinirConvenioAsync(pacienteId, "Amil");

        var p = await _repo.ObterPacienteAsync(pacienteId);
        p!.Carteirinha.Should().Be("0123456789");
        p.ValidadeCarteirinha.Should().Be(new DateOnly(2027, 1, 31));
    }

    /// <summary>E informada, ela é gravada — é o número que vai NA guia.</summary>
    [Fact]
    public async Task Vincular_com_carteirinha_grava_numero_e_validade()
    {
        var pacienteId = await PacienteSemConvenioAsync();

        await _pacientes.DefinirConvenioAsync(
            pacienteId, "Amil", " 9988776655 ", new DateOnly(2028, 6, 30), "flavia@");

        var p = await _repo.ObterPacienteAsync(pacienteId);
        p!.Carteirinha.Should().Be("9988776655");
        p.ValidadeCarteirinha.Should().Be(new DateOnly(2028, 6, 30));
    }

    /// <summary>
    /// "A definir" é a PERGUNTA, não uma resposta. Aceitá-lo daria uma tela de sucesso e
    /// um lançamento recusado logo em seguida — o pior par possível.
    /// </summary>
    [Fact]
    public async Task Vincular_A_DEFINIR_de_novo_e_recusado()
    {
        var pacienteId = await PacienteSemConvenioAsync();

        var erro = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _pacientes.DefinirConvenioAsync(pacienteId, ConvenioCadastro.CodigoADefinir));

        erro.Message.Should().Contain("Escolha a operadora");
    }

    [Fact]
    public async Task Vincular_sem_codigo_e_recusado()
    {
        var pacienteId = await PacienteSemConvenioAsync();

        await Assert.ThrowsAsync<ArgumentException>(
            () => _pacientes.DefinirConvenioAsync(pacienteId, "   "));
    }

    /// <summary>
    /// A leitura que a Fila usa antes de perguntar: ela tem o ID do cartão, não a ficha.
    /// A ficha SOZINHA basta, e é meio megabyte a menos por pergunta do que arrastar o
    /// histórico inteiro — que era a única leitura disponível.
    /// </summary>
    [Fact]
    public async Task Obter_traz_a_ficha_sozinha_e_ela_responde_pelo_convenio()
    {
        var pacienteId = await PacienteSemConvenioAsync();

        var antes = await _pacientes.ObterAsync(pacienteId);
        antes!.ConvenioADefinir.Should().BeTrue();
        antes.Atendimentos.Should().BeEmpty("a ficha vem sem o histórico — quem o quer pede o outro método");

        await _pacientes.DefinirConvenioAsync(pacienteId, "Amil");

        (await _pacientes.ObterAsync(pacienteId))!.ConvenioADefinir.Should().BeFalse();
    }

    [Fact]
    public async Task Obter_de_ficha_inexistente_devolve_nulo()
        => (await _pacientes.ObterAsync(9999)).Should().BeNull();

    /// <summary>
    /// Trocar o convênio de um paciente é decisão que muda o faturamento dele daí para a
    /// frente — a trilha registra quem trocou, de quê para quê. É exigência de LGPD para
    /// dado de saúde, e é o que responde "quem pôs a Amil nesta ficha?".
    /// </summary>
    [Fact]
    public async Task Vincular_deixa_rastro_na_trilha_de_auditoria()
    {
        var pacienteId = await PacienteSemConvenioAsync();

        await _pacientes.DefinirConvenioAsync(pacienteId, "Amil", operador: "flavia@");

        var evento = _db.Auditoria.Single(e => e.Acao == "ConvenioDefinido");
        evento.Operador.Should().Be("flavia@");
        evento.PacienteId.Should().Be(pacienteId);
        evento.Detalhe.Should().Contain("Amil");
        evento.Detalhe.Should().Contain("A definir", "a trilha diz de ONDE veio, não só para onde foi");
    }
}
