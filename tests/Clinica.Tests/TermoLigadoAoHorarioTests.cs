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
/// O TERMO LIGADO À SESSÃO (set/2026) — o pedido da direção: "se o paciente está no
/// consultório, o termo fica linkado àquela sessão; se está na recepção, pergunte a qual
/// sessão ligar".
///
/// O que estes testes prendem é o CIRCUITO, e não a tela: o vínculo sobrevive à emissão
/// (que copia campo a campo — o lugar 3 da lista de conferência), a leitura o traz de volta
/// num contexto NOVO como em produção, e a regra de COBERTURA continua a mesma. Este último
/// é o mais importante: a coluna nova é procedência, e um termo assinado de manhã não pode
/// deixar de valer à tarde por causa dela.
/// </summary>
public class TermoLigadoAoHorarioTests : IDisposable
{
    private readonly SqliteConnection _conn;
    private readonly ClinicaDbContext _db;
    private readonly ClinicaRepositorio _repo;
    private readonly DocumentoClinicoService _documentos;
    private readonly TermoProcedimentoService _termos;

    private readonly DateOnly _hoje = DateOnly.FromDateTime(DateTime.Today);

    public TermoLigadoAoHorarioTests()
    {
        _conn = new SqliteConnection("DataSource=:memory:");
        _conn.Open();
        _db = new ClinicaDbContext(
            new DbContextOptionsBuilder<ClinicaDbContext>().UseSqlite(_conn).Options);
        _db.Database.EnsureCreated();
        _repo = new ClinicaRepositorio(_db);
        _documentos = new DocumentoClinicoService(
            _repo, new ProntuarioService(_repo), new ConsentimentoService(_repo));
        _termos = new TermoProcedimentoService(_repo);
    }

    public void Dispose()
    {
        _db.Dispose();
        _conn.Dispose();
        GC.SuppressFinalize(this);
    }

    // ==================== cenário ====================

    private Paciente Paciente()
    {
        var p = new Paciente { Nome = "Maria da Silva", Convenio = Convenio.UnimedPadrao };
        _db.Pacientes.Add(p);
        _db.SaveChanges();
        return p;
    }

    private ModeloDocumento Modelo(string nome = "Termo do BSV")
    {
        var m = new ModeloDocumento
        {
            Tipo = TipoDocumentoClinico.TermoProcedimento,
            Nome = nome,
            Titulo = "Consentimento para Bloqueio Simpático Venoso",
            Corpo = "Fui informado(a) dos riscos e concordo com o procedimento.",
            Itens = [new ItemModelo { Ordem = 1, Descricao = "Estou em jejum de 8 horas" }]
        };
        _db.ModelosDocumento.Add(m);
        _db.SaveChanges();
        return m;
    }

    private Agendamento Horario(
        int pacienteId, DateTime quando,
        ModalidadeAtendimento modalidade = ModalidadeAtendimento.BsvApenas,
        StatusAgendamento status = StatusAgendamento.Agendado,
        int? profissionalId = null)
    {
        var a = new Agendamento
        {
            PacienteId = pacienteId,
            DataHora = quando,
            DuracaoMinutos = 30,
            ModalidadePrevista = modalidade,
            Status = status,
            ProfissionalId = profissionalId
        };
        _db.Agendamentos.Add(a);
        _db.SaveChanges();
        return a;
    }

    // ==================== o vínculo ====================

    /// <summary>
    /// O caso do consultório: o horário está aberto na tela e o termo nasce ligado a ele.
    ///
    /// ⚠️ Reprova no código anterior por DOIS motivos, e o segundo é o que importa: além de
    /// a coluna não existir, a emissão COPIA campo a campo — um vínculo que não estivesse
    /// naquela lista seria descartado em silêncio, com a criação funcionando e ninguém
    /// notando (o lugar 3 da lista de conferência).
    /// </summary>
    [Fact]
    public async Task Termo_emitido_com_a_sessao_nasce_ligado_a_ela()
    {
        var paciente = Paciente();
        var modelo = Modelo();
        var horario = Horario(paciente.Id, DateTime.Today.AddHours(9));

        var termo = await _documentos.EmitirTermoProcedimentoAsync(
            paciente.Id, modelo.Id, agendamentoId: horario.Id, operador: "Ana");

        termo.AgendamentoId.Should().Be(horario.Id);
    }

    /// <summary>
    /// O termo AVULSO continua existindo, e é a maioria: o paciente que assina semanas
    /// antes, sem procedimento marcado. Nulo quer dizer "não se refere a um horário", e
    /// não "o horário se perdeu".
    /// </summary>
    [Fact]
    public async Task Termo_avulso_continua_sem_sessao()
    {
        var paciente = Paciente();
        var modelo = Modelo();

        var termo = await _documentos.EmitirTermoProcedimentoAsync(
            paciente.Id, modelo.Id, operador: "Ana");

        termo.AgendamentoId.Should().BeNull();
    }

    /// <summary>
    /// A LEITURA traz o horário junto — num <c>DbContext</c> NOVO, como em produção.
    ///
    /// ⚠️ Sem o <c>Include</c>, a navegação chega nula em produção e a procedência some da
    /// ficha; com o contexto compartilhado do teste, o relationship fixup do EF a
    /// preencheria de qualquer jeito e o teste passaria mentindo (a lição da parcela 68).
    /// </summary>
    [Fact]
    public async Task A_ficha_le_a_sessao_do_documento_em_escopo_separado()
    {
        var paciente = Paciente();
        var modelo = Modelo();
        var horario = Horario(paciente.Id, DateTime.Today.AddHours(9));

        await _documentos.EmitirTermoProcedimentoAsync(
            paciente.Id, modelo.Id, agendamentoId: horario.Id, operador: "Ana");

        using var outro = new ClinicaDbContext(
            new DbContextOptionsBuilder<ClinicaDbContext>().UseSqlite(_conn).Options);
        var repoNovo = new ClinicaRepositorio(outro);

        var documentos = await repoNovo.DocumentosDoPacienteAsync(paciente.Id);

        documentos.Should().ContainSingle();
        documentos[0].Agendamento.Should().NotBeNull(
            "sem o Include a procedência sumiria da ficha em produção");
        ProcedenciaDaSessao.Descrever(documentos[0].Agendamento)
            .Should().Contain("09h00");
    }

    // ==================== a escolha da sessão ====================

    /// <summary>
    /// A lista da janela do balcão: hoje E as próximas, porque a coleta antecipada é o
    /// caso que a porta avulsa existe para atender. Cancelado e falta ficam de fora — não
    /// há procedimento a consentir num horário que não vai acontecer.
    /// </summary>
    [Fact]
    public async Task As_sessoes_oferecidas_vao_de_hoje_para_a_frente_sem_cancelado_nem_falta()
    {
        var paciente = Paciente();
        var deHoje = Horario(paciente.Id, DateTime.Today.AddHours(9));
        var daSemanaQueVem = Horario(paciente.Id, DateTime.Today.AddDays(7).AddHours(14));
        Horario(paciente.Id, DateTime.Today.AddHours(11), status: StatusAgendamento.Cancelado);
        Horario(paciente.Id, DateTime.Today.AddHours(16), status: StatusAgendamento.Faltou);
        Horario(paciente.Id, DateTime.Today.AddDays(-3).AddHours(9));

        var sessoes = await _termos.SessoesParaTermoAsync(paciente.Id, _hoje);

        sessoes.Select(s => s.AgendamentoId)
            .Should().Equal(deHoje.Id, daSemanaQueVem.Id);
        sessoes[0].Hoje.Should().BeTrue();
        sessoes[1].Hoje.Should().BeFalse();
    }

    /// <summary>
    /// O rótulo é o que a pessoa lê para desempatar. "Hoje" por extenso no dia; data curta
    /// nas próximas — e a modalidade sai do catálogo, nunca o nome do enum.
    /// </summary>
    [Fact]
    public async Task O_rotulo_da_sessao_diz_hoje_e_a_modalidade()
    {
        var paciente = Paciente();
        Horario(paciente.Id, DateTime.Today.AddHours(9));

        var sessoes = await _termos.SessoesParaTermoAsync(paciente.Id, _hoje);

        sessoes[0].Rotulo.Should().Be("Hoje, 09h00");
        sessoes[0].Detalhe.Should().NotContain("Bsv", "o enum não vaza para a tela");
    }

    /// <summary>
    /// UMA sessão da modalidade no dia: a porta amarra sozinha, sem perguntar nada.
    /// </summary>
    [Fact]
    public async Task Com_UM_horario_no_dia_a_situacao_ja_traz_a_sessao()
    {
        var paciente = Paciente();
        var modelo = Modelo();
        var horario = Horario(paciente.Id, DateTime.Today.AddHours(9));
        await _termos.ExigirAsync(ModalidadeAtendimento.BsvApenas, modelo.Id, operador: "Ana");

        var situacao = await _termos.SituacaoDoDiaAsync(paciente.Id, _hoje);

        situacao.Should().ContainSingle();
        situacao[0].AgendamentoId.Should().Be(horario.Id);
    }

    /// <summary>
    /// DUAS sessões da mesma modalidade no dia: a situação vem SEM sessão, de propósito.
    ///
    /// Escolher a primeira seria gravar procedência inventada. Nulo aqui é o que faz a
    /// porta abrir a janela de escolha — quem desempata é quem está com o paciente na
    /// frente.
    /// </summary>
    [Fact]
    public async Task Com_DOIS_horarios_no_dia_a_situacao_nao_escolhe_por_ninguem()
    {
        var paciente = Paciente();
        var modelo = Modelo();
        Horario(paciente.Id, DateTime.Today.AddHours(9));
        Horario(paciente.Id, DateTime.Today.AddHours(15));
        await _termos.ExigirAsync(ModalidadeAtendimento.BsvApenas, modelo.Id, operador: "Ana");

        var situacao = await _termos.SituacaoDoDiaAsync(paciente.Id, _hoje);

        situacao.Should().ContainSingle();
        situacao[0].AgendamentoId.Should().BeNull();
    }

    /// <summary>
    /// Dois horários da mesma modalidade com PROFISSIONAIS DIFERENTES continuam sem
    /// sessão — e é a falha que o teste anterior não pegava.
    ///
    /// ⚠️ O agrupamento incluía o profissional, então dois médicos viravam dois grupos de
    /// UM, e cada um trazia o próprio horário: a porta gravaria uma procedência escolhida
    /// por acidente, que é exatamente o que a coluna nova existe para não fazer. O teste
    /// de cima passava porque os dois horários dele não tinham profissional, e por isso
    /// caíam no mesmo grupo.
    /// </summary>
    [Fact]
    public async Task Dois_horarios_com_medicos_diferentes_tambem_nao_escolhem_por_ninguem()
    {
        var paciente = Paciente();
        var modelo = Modelo();
        var helena = new Profissional { Nome = "Helena Prado", Ativo = true };
        var rafael = new Profissional { Nome = "Rafael Nunes", Ativo = true };
        _db.Profissionais.AddRange(helena, rafael);
        _db.SaveChanges();

        Horario(paciente.Id, DateTime.Today.AddHours(9), profissionalId: helena.Id);
        Horario(paciente.Id, DateTime.Today.AddHours(15), profissionalId: rafael.Id);
        await _termos.ExigirAsync(ModalidadeAtendimento.BsvApenas, modelo.Id, operador: "Ana");

        var situacao = await _termos.SituacaoDoDiaAsync(paciente.Id, _hoje);

        situacao.Should().ContainSingle();
        situacao[0].AgendamentoId.Should().BeNull(
            "com dois horários a procedência é escolha de quem está com o paciente na frente");
    }

    // ==================== o que NÃO mudou ====================

    /// <summary>
    /// A COBERTURA continua sendo por paciente + modelo + validade, e não por sessão.
    ///
    /// ⚠️ É a asserção mais importante do arquivo. Amarrar a cobertura ao horário faria um
    /// termo assinado de manhã deixar de valer na sessão da tarde — mudança de
    /// comportamento numa clínica em produção, e decisão da direção, não efeito colateral
    /// de uma coluna nova.
    /// </summary>
    [Fact]
    public async Task Um_termo_assinado_continua_cobrindo_as_duas_sessoes_do_dia()
    {
        var paciente = Paciente();
        var modelo = Modelo();
        var manha = Horario(paciente.Id, DateTime.Today.AddHours(9));
        Horario(paciente.Id, DateTime.Today.AddHours(15));
        await _termos.ExigirAsync(ModalidadeAtendimento.BsvApenas, modelo.Id, operador: "Ana");

        var termo = await _documentos.EmitirTermoProcedimentoAsync(
            paciente.Id, modelo.Id, agendamentoId: manha.Id, operador: "Ana");

        // Assina o da manhã, à moda do serviço de assinatura (o traço em si é outro teste).
        var gravado = await _db.DocumentosClinicos.FirstAsync(d => d.Id == termo.Id);
        gravado.PacienteAssinadoEm = DateTime.Today.AddHours(8);
        gravado.PacienteAssinaturaMeio = MeioAssinaturaPaciente.NaClinica;
        await _db.SaveChangesAsync();

        var situacao = await _termos.SituacaoDoDiaAsync(paciente.Id, _hoje);

        situacao.Should().ContainSingle();
        situacao[0].Assinado.Should().BeTrue(
            "a sessão da tarde continua coberta pelo termo assinado de manhã");
        situacao[0].Pendente.Should().BeFalse();
    }

    // ==================== a frase da tela ====================

    [Fact]
    public void Documento_sem_sessao_nao_inventa_procedencia()
        => ProcedenciaDaSessao.Descrever(null).Should().BeNull();

    [Fact]
    public void A_procedencia_diz_a_data_a_hora_e_a_modalidade()
    {
        var horario = new Agendamento
        {
            DataHora = new DateTime(2026, 9, 8, 9, 0, 0),
            ModalidadePrevista = ModalidadeAtendimento.BsvApenas
        };

        var frase = ProcedenciaDaSessao.Descrever(horario);

        frase.Should().StartWith("Sessão de 08/09/2026, 09h00");
        frase.Should().NotContain("Bsv", "o enum não vaza para a tela");
    }
}
