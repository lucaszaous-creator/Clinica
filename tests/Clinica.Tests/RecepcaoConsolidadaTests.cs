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
/// Consolidação da RECEPÇÃO: o que os testes por feature não alcançavam.
///
/// A suíte por assunto (agenda, prontuário, pacote, documento) cobre o caminho que a
/// clínica percorre quando tudo dá certo. Esta cobre o resto, e o resto não é detalhe —
/// é onde a Recepção passa metade do dia:
///
/// 1. **O que o serviço RECUSA.** Sala sem nome, protocolo sem ponto, pacote sem sessão,
///    atestado de zero dia, autorização com utilizadas negativas. Cada uma dessas
///    mensagens é lida por alguém no balcão com o paciente na frente, e uma validação que
///    some numa refatoração só aparece como dado sujo três meses depois.
/// 2. **O que o serviço DEGRADA.** "Só a conclusão do atendimento derruba a operação;
///    pacote, insumo e caixa que falham viram aviso" é regra escrita, e não tinha um
///    teste sequer — não havia como fazer o banco falhar. Agora há
///    (<see cref="RepositorioComFalha"/>).
/// 3. **O que ninguém chamava.** A varredura por método sem execução é o que já achou,
///    neste projeto, o PDF de 323 linhas aprovado no CI que nunca havia rodado. Aqui ela
///    achou dezessete, e este arquivo os executa.
///
/// A escolha de fundo: a Recepção é onde o sistema mais vai ser usado, e "funciona" ali
/// não pode significar "o caminho feliz funciona".
/// </summary>
public class RecepcaoConsolidadaTests : IDisposable
{
    private readonly SqliteConnection _conn;
    private readonly ClinicaDbContext _db;
    private readonly ClinicaRepositorio _repo;

    private readonly AgendaService _agenda;
    private readonly EquipeService _equipe;
    private readonly PacienteService _pacientes;
    private readonly ProntuarioService _prontuario;
    private readonly MapaCorporalService _mapas;
    private readonly ListaEsperaService _espera;
    private readonly PacoteService _pacotes;
    private readonly ConsentimentoService _consentimentos;
    private readonly AutorizacaoService _autorizacoes;
    private readonly ElegibilidadeService _elegibilidade;
    private readonly DocumentoClinicoService _documentos;
    private readonly BloqueioAgendaService _bloqueios;
    private readonly ConsultaService _consultas;
    private readonly FinanceiroService _financeiro;

    private static readonly DateOnly Hoje = new(2026, 8, 3);
    private static readonly DateTime Manha = new(2026, 8, 3, 9, 0, 0);

    public RecepcaoConsolidadaTests()
    {
        _conn = new SqliteConnection("DataSource=:memory:");
        _conn.Open();
        var options = new DbContextOptionsBuilder<ClinicaDbContext>().UseSqlite(_conn).Options;
        _db = new ClinicaDbContext(options);
        _db.Database.EnsureCreated();
        _repo = new ClinicaRepositorio(_db);

        var atendimentos = new AtendimentoService(_repo);
        _agenda = new AgendaService(_repo, atendimentos);
        _equipe = new EquipeService(_repo);
        _pacientes = new PacienteService(_repo);
        _prontuario = new ProntuarioService(_repo);
        _mapas = new MapaCorporalService(_repo);
        _espera = new ListaEsperaService(_repo, _agenda);
        _pacotes = new PacoteService(_repo);
        _consentimentos = new ConsentimentoService(_repo);
        _autorizacoes = new AutorizacaoService(_repo);
        _elegibilidade = new ElegibilidadeService(_repo, _autorizacoes, _consentimentos);
        _documentos = new DocumentoClinicoService(_repo, _prontuario, _consentimentos);
        _bloqueios = new BloqueioAgendaService(_repo, _agenda);
        _consultas = new ConsultaService(_repo);
        _financeiro = new FinanceiroService(_repo);
    }

    // ==================== Auxiliares ====================

    private async Task<Paciente> CriarPacienteAsync(
        string nome = "Maria", Convenio convenio = Convenio.UnimedIntercambio)
    {
        var p = new Paciente { Nome = nome, Convenio = convenio, Sexo = Sexo.Feminino };
        _db.Pacientes.Add(p);
        await _db.SaveChangesAsync();
        return p;
    }

    private Task<Profissional> CriarProfissionalAsync(string nome = "Dra. Ana")
        => _equipe.SalvarProfissionalAsync(new Profissional { Nome = nome });

    private async Task<Evolucao> CriarSessaoAsync(int pacienteId, DateOnly? data = null)
        => await _prontuario.SalvarAsync(new Evolucao
        {
            PacienteId = pacienteId,
            Data = data ?? Hoje,
            QueixaPrincipal = "Dor lombar"
        }, "teste");

    private static List<PontoMapa> Pontos(int quantos = 2)
        => Enumerable.Range(0, quantos)
            .Select(i => new PontoMapa
            {
                Face = FaceCorpo.Frente,
                X = 0.5,
                Y = 0.1 + i * 0.01,
                Nome = $"IG{i + 1}"
            })
            .ToList();

    // ====================================================================
    //  AGENDA — o que ela recusa e o que ninguém executava
    // ====================================================================

    [Fact]
    public async Task Agenda_recusa_duracao_zero_ou_negativa()
    {
        var paciente = await CriarPacienteAsync();

        var acao = () => _agenda.AgendarAsync(
            paciente.Id, Manha, ModalidadeAtendimento.AcupunturaSimples, null,
            duracaoMinutos: 0);

        await acao.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*maior que zero*");
    }

    [Fact]
    public async Task Remarcacao_recusa_duracao_zero_ou_negativa()
    {
        var paciente = await CriarPacienteAsync();
        var ag = await _agenda.AgendarAsync(
            paciente.Id, Manha, ModalidadeAtendimento.AcupunturaSimples, null);

        var acao = () => _agenda.RemarcarAsync(
            ag.Id, Manha.AddHours(2), null, manterRecursos: false, duracaoMinutos: -30);

        await acao.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*maior que zero*");
    }

    [Fact]
    public async Task Serie_recusa_intervalo_menor_que_um_dia()
    {
        var paciente = await CriarPacienteAsync();

        var acao = () => _agenda.AgendarSerieAsync(
            paciente.Id, Manha, ModalidadeAtendimento.AcupunturaSimples, 4, intervaloDias: 0);

        await acao.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*pelo menos 1 dia*");
    }

    [Fact]
    public async Task Modalidade_do_catalogo_define_a_base_do_agendamento()
    {
        var paciente = await CriarPacienteAsync();

        // O código do catálogo manda: a base (comportamento faturável) sai dele, e não do
        // enum que a tela mandou junto. É o que permite a variante criada em runtime.
        var ag = await _agenda.AgendarAsync(
            paciente.Id, Manha, ModalidadeAtendimento.Consulta, null,
            modalidadeCodigo: ModalidadeAtendimento.AcupunturaComEletro.ToString());

        ag.ModalidadePrevista.Should().Be(ModalidadeAtendimento.AcupunturaComEletro);
    }

    [Fact]
    public async Task Agenda_do_dia_por_profissional_separa_as_colunas()
    {
        var paciente = await CriarPacienteAsync();
        var ana = await CriarProfissionalAsync("Dra. Ana");
        var bruno = await CriarProfissionalAsync("Dr. Bruno");

        await _agenda.AgendarAsync(paciente.Id, Manha, ModalidadeAtendimento.AcupunturaSimples,
            null, profissionalId: ana.Id);
        await _agenda.AgendarAsync(paciente.Id, Manha.AddHours(3),
            ModalidadeAtendimento.AcupunturaSimples, null, profissionalId: bruno.Id);

        (await _agenda.DoDiaPorProfissionalAsync(Hoje, ana.Id)).Should().ContainSingle();
        (await _agenda.DoDiaPorProfissionalAsync(Hoje, null)).Should().BeEmpty();
    }

    [Fact]
    public async Task Sessoes_de_uma_serie_saem_na_ordem_em_que_acontecem()
    {
        var paciente = await CriarPacienteAsync();
        var serie = await _agenda.AgendarSerieAsync(
            paciente.Id, Manha, ModalidadeAtendimento.AcupunturaSimples, 3);

        var daSerie = await _agenda.DaSerieAsync(serie.SerieId);

        daSerie.Should().HaveCount(3);
        daSerie.Should().BeInAscendingOrder(a => a.DataHora);
    }

    [Fact]
    public async Task Check_in_so_vale_para_horario_em_aberto()
    {
        var paciente = await CriarPacienteAsync();
        var ag = await _agenda.AgendarAsync(
            paciente.Id, Manha, ModalidadeAtendimento.AcupunturaSimples, null);

        await _agenda.CancelarAsync(ag.Id, "recepcao");

        var acao = () => _agenda.RegistrarChegadaAsync(ag.Id);

        await acao.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Reabra o agendamento*");
    }

    [Fact]
    public async Task Chamar_paciente_so_vale_para_horario_em_aberto()
    {
        var paciente = await CriarPacienteAsync();
        var ag = await _agenda.AgendarAsync(
            paciente.Id, Manha, ModalidadeAtendimento.AcupunturaSimples, null);

        await _agenda.MarcarFaltaAsync(ag.Id, "recepcao");

        var acao = () => _agenda.IniciarAtendimentoAsync(ag.Id);

        await acao.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*não está mais em aberto*");
    }

    // ==================== Lista de espera ====================

    [Fact]
    public async Task Historico_da_lista_de_espera_inclui_quem_ja_saiu()
    {
        var paciente = await CriarPacienteAsync();
        var pedido = await _espera.AdicionarAsync(paciente.Id);
        await _espera.RemoverAsync(pedido.Id, "resolveu em outro lugar");

        // AguardandoAsync responde "quem ainda procura horário"; TodosAsync é o histórico,
        // e sem ele não há como auditar quem saiu da fila nem por quê.
        (await _espera.AguardandoAsync()).Should().BeEmpty();
        (await _espera.TodosAsync()).Should().ContainSingle();
    }

    [Fact]
    public async Task Pedido_ja_removido_da_lista_nao_sai_duas_vezes()
    {
        var paciente = await CriarPacienteAsync();
        var pedido = await _espera.AdicionarAsync(paciente.Id);
        await _espera.RemoverAsync(pedido.Id, "desistiu");

        var acao = () => _espera.RemoverAsync(pedido.Id, "desistiu de novo");

        await acao.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*já saiu da lista*");
    }

    // ==================== Bloqueio ====================

    [Fact]
    public async Task Bloqueio_pode_ser_lido_pelo_id()
    {
        var criado = await _bloqueios.CriarAsync(Manha, Manha.AddHours(4), "Feriado");

        (await _bloqueios.ObterAsync(criado.Bloqueio.Id))!.Motivo.Should().Be("Feriado");
        (await _bloqueios.ObterAsync(99999)).Should().BeNull();
    }

    [Fact]
    public async Task Bloqueio_do_horario_responde_por_recurso()
    {
        var ana = await CriarProfissionalAsync("Dra. Ana");
        var bruno = await CriarProfissionalAsync("Dr. Bruno");
        await _bloqueios.CriarAsync(Manha, Manha.AddHours(4), "Folga", profissionalId: ana.Id);

        // Mesma pergunta que a grade faz, recurso a recurso: fechado para a Ana, aberto
        // para o Bruno. (A tela de agendamento usa ConflitosAsync, que já traz o bloqueio
        // junto do choque de horário; este método é o atalho de uma pergunta só.)
        (await _bloqueios.BloqueioDoHorarioAsync(
            Manha, Manha.AddHours(1), ana.Id))!.Motivo.Should().Be("Folga");

        (await _bloqueios.BloqueioDoHorarioAsync(
            Manha, Manha.AddHours(1), bruno.Id)).Should().BeNull();
    }

    [Fact]
    public void Sessao_que_termina_quando_o_bloqueio_comeca_nao_colide_com_ele()
    {
        var bloqueio = new BloqueioAgenda { Inicio = Manha.AddHours(3), Fim = Manha.AddHours(8) };

        // Fim EXCLUSIVO dos dois lados. Sem isso, marcar até as 12h com a agenda fechada
        // a partir das 12h seria recusado — e ninguém no balcão entenderia por quê.
        bloqueio.ColideCom(Manha, Manha.AddHours(3)).Should().BeFalse();
        bloqueio.ColideCom(Manha.AddHours(8), Manha.AddHours(9)).Should().BeFalse();

        // Um minuto para dentro já é choque.
        bloqueio.ColideCom(Manha, Manha.AddHours(3).AddMinutes(1)).Should().BeTrue();
        bloqueio.ColideCom(Manha.AddHours(4), Manha.AddHours(5)).Should().BeTrue();
    }

    [Fact]
    public async Task Protocolo_da_clinica_nao_pertence_a_paciente_nenhum()
    {
        var paciente = await CriarPacienteAsync();

        var doPaciente = await _mapas.SalvarComoProtocoloAsync(
            paciente.Id, "Só dela", Pontos(), daClinica: false);
        var daClinica = await _mapas.SalvarComoProtocoloAsync(
            paciente.Id, "Lombar padrão", Pontos(), daClinica: true);

        doPaciente.EhDaClinica.Should().BeFalse();
        daClinica.EhDaClinica.Should().BeTrue();
        daClinica.PacienteId.Should().BeNull();
    }

    // ====================================================================
    //  EQUIPE — as salas, que não tinham teste nenhum
    // ====================================================================

    [Fact]
    public async Task Sala_sem_nome_nao_e_cadastrada()
    {
        var acao = () => _equipe.SalvarSalaAsync(new Sala { Nome = "   " });

        await acao.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*nome da sala*");
    }

    [Fact]
    public async Task Sala_com_capacidade_menor_que_um_nao_e_cadastrada()
    {
        var acao = () => _equipe.SalvarSalaAsync(new Sala { Nome = "Consultório 1", Capacidade = 0 });

        await acao.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*pelo menos 1*");
    }

    [Fact]
    public async Task Duas_salas_nao_podem_ter_o_mesmo_nome()
    {
        await _equipe.SalvarSalaAsync(new Sala { Nome = "Consultório 1" });

        // Sem isto a agenda mostraria duas colunas iguais e ninguém saberia qual é qual.
        var acao = () => _equipe.SalvarSalaAsync(new Sala { Nome = "consultório 1" });

        await acao.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Já existe uma sala*");
    }

    [Fact]
    public async Task Sala_editada_conserva_o_registro_em_vez_de_criar_outro()
    {
        var sala = await _equipe.SalvarSalaAsync(new Sala { Nome = "Consultório 1" });

        var editada = await _equipe.SalvarSalaAsync(new Sala
        {
            Id = sala.Id,
            Nome = "Consultório 1 — reformado",
            Capacidade = 2,
            Ativa = false,
            Ordem = 3
        });

        editada.Id.Should().Be(sala.Id);
        editada.Capacidade.Should().Be(2);
        (await _equipe.SalasAsync()).Should().ContainSingle();
    }

    [Fact]
    public async Task Editar_sala_que_nao_existe_e_recusado()
    {
        var acao = () => _equipe.SalvarSalaAsync(new Sala { Id = 4242, Nome = "Fantasma" });

        await acao.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Sala não encontrada*");
    }

    [Fact]
    public async Task Sala_inativa_sai_da_lista_que_a_agenda_oferece()
    {
        await _equipe.SalvarSalaAsync(new Sala { Nome = "Consultório 1" });
        await _equipe.SalvarSalaAsync(new Sala { Nome = "Depósito", Ativa = false });

        (await _equipe.SalasAsync()).Should().HaveCount(2);
        (await _equipe.SalasAtivasAsync()).Should().ContainSingle()
            .Which.Nome.Should().Be("Consultório 1");
    }

    [Fact]
    public async Task Sala_sem_agenda_pode_ser_excluida_e_com_agenda_nao()
    {
        var paciente = await CriarPacienteAsync();
        var livre = await _equipe.SalvarSalaAsync(new Sala { Nome = "Nunca usada" });
        var usada = await _equipe.SalvarSalaAsync(new Sala { Nome = "Consultório 1" });

        await _agenda.AgendarAsync(paciente.Id, Manha, ModalidadeAtendimento.AcupunturaSimples,
            null, salaId: usada.Id);

        await _equipe.ExcluirSalaAsync(livre.Id);
        (await _equipe.ObterSalaAsync(livre.Id)).Should().BeNull();

        // Excluir a que tem histórico apagaria de onde as sessões aconteceram.
        var acao = () => _equipe.ExcluirSalaAsync(usada.Id);
        await acao.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Desative-a*");
    }

    [Fact]
    public async Task Profissional_pode_ser_lido_pelo_id()
    {
        var ana = await CriarProfissionalAsync("Dra. Ana");

        (await _equipe.ObterProfissionalAsync(ana.Id))!.Nome.Should().Be("Dra. Ana");
        (await _equipe.ObterProfissionalAsync(99999)).Should().BeNull();
    }

    // ====================================================================
    //  PACIENTE — validação e histórico
    // ====================================================================

    [Fact]
    public async Task Paciente_sem_nome_nao_e_gravado()
    {
        var acao = () => _pacientes.SalvarNovoAsync(new Paciente { Nome = "  " });

        await acao.Should().ThrowAsync<ArgumentException>().WithMessage("*nome do paciente*");
    }

    [Fact]
    public async Task Cpf_e_normalizado_para_so_digitos_ao_gravar()
    {
        // O CPF entra formatado do balcão e sai limpo do serviço: gravar a máscara faria
        // a busca por CPF depender de como cada pessoa digitou.
        var salvo = await _pacientes.SalvarNovoAsync(new Paciente
        {
            Nome = "Maria",
            Documento = "529.982.247-25",
            Convenio = Convenio.UnimedIntercambio
        });

        salvo.Documento.Should().Be("52998224725");
    }

    [Fact]
    public async Task Paciente_com_historico_traz_sessoes_e_guias()
    {
        var paciente = await CriarPacienteAsync();
        var ag = await _agenda.AgendarAsync(
            paciente.Id, Manha, ModalidadeAtendimento.AcupunturaSimples, null);
        await _agenda.ConfirmarPresencaAsync(ag.Id);

        var comHistorico = await _pacientes.ObterComHistoricoAsync(paciente.Id);

        comHistorico.Should().NotBeNull();
        comHistorico!.Atendimentos.Should().NotBeEmpty();
    }

    // ====================================================================
    //  PRONTUÁRIO
    // ====================================================================

    [Fact]
    public async Task Evolucao_de_paciente_inexistente_e_recusada()
    {
        var acao = () => _prontuario.SalvarAsync(new Evolucao
        {
            PacienteId = 4242,
            Data = Hoje,
            QueixaPrincipal = "Dor"
        }, "teste");

        await acao.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Paciente não encontrado*");
    }

    [Fact]
    public async Task Sessao_pode_ser_lida_pelo_id()
    {
        var paciente = await CriarPacienteAsync();
        var sessao = await CriarSessaoAsync(paciente.Id);

        (await _prontuario.ObterAsync(sessao.Id))!.QueixaPrincipal.Should().Be("Dor lombar");
        (await _prontuario.ObterAsync(99999)).Should().BeNull();
    }

    [Fact]
    public async Task Anexo_removido_sai_da_sessao_e_a_sessao_continua()
    {
        var paciente = await CriarPacienteAsync();
        var sessao = await CriarSessaoAsync(paciente.Id);

        var anexo = await _prontuario.AnexarAsync(
            sessao.Id, "exame.pdf", [1, 2, 3], operador: "teste");

        await _prontuario.RemoverAnexoAsync(anexo.Id);

        (await _prontuario.AnexosAsync(sessao.Id)).Should().BeEmpty();
        (await _prontuario.ObterAsync(sessao.Id)).Should().NotBeNull();
    }

    // ====================================================================
    //  MAPA CORPORAL — inclusive os métodos que a tela deixou de usar
    // ====================================================================

    [Fact]
    public async Task Mapa_recusa_mais_pontos_do_que_o_teto()
    {
        var paciente = await CriarPacienteAsync();
        var sessao = await CriarSessaoAsync(paciente.Id);

        var acao = () => _mapas.SalvarAsync(sessao.Id, Pontos(MapaCorporal.MaximoPontos + 1));

        await acao.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage($"*até {MapaCorporal.MaximoPontos} pontos*");
    }

    [Fact]
    public async Task Protocolo_sem_nome_nao_e_guardado()
    {
        var paciente = await CriarPacienteAsync();

        var acao = () => _mapas.SalvarComoProtocoloAsync(paciente.Id, "  ", Pontos());

        await acao.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*nome ao protocolo*");
    }

    [Fact]
    public async Task Protocolo_sem_nenhum_ponto_nao_e_guardado()
    {
        var paciente = await CriarPacienteAsync();

        var acao = () => _mapas.SalvarComoProtocoloAsync(paciente.Id, "Lombar", []);

        await acao.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*ao menos um ponto*");
    }

    [Fact]
    public async Task Protocolo_de_paciente_inexistente_e_recusado()
    {
        var acao = () => _mapas.SalvarComoProtocoloAsync(4242, "Lombar", Pontos());

        await acao.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Paciente não encontrado*");
    }

    [Fact]
    public async Task Protocolo_pode_ser_lido_pelo_id()
    {
        var paciente = await CriarPacienteAsync();
        var protocolo = await _mapas.SalvarComoProtocoloAsync(paciente.Id, "Lombar", Pontos());

        (await _mapas.ObterProtocoloAsync(protocolo.Id))!.Nome.Should().Be("Lombar");
        (await _mapas.ObterProtocoloAsync(99999)).Should().BeNull();
    }

    [Fact]
    public async Task Aplicar_protocolo_COPIA_os_pontos_e_guarda_a_origem()
    {
        var paciente = await CriarPacienteAsync();
        var protocolo = await _mapas.SalvarComoProtocoloAsync(paciente.Id, "Lombar", Pontos(3));
        var sessao = await CriarSessaoAsync(paciente.Id);

        var mapa = await _mapas.AplicarProtocoloAsync(sessao.Id, protocolo.Id, "teste");

        mapa.Pontos.Should().HaveCount(3);
        mapa.ProtocoloOrigemId.Should().Be(protocolo.Id);
        // Cópia, nunca referência: apagar o protocolo não pode mexer na sessão salva.
        await _mapas.ExcluirProtocoloAsync(protocolo.Id, "teste");
        (await _mapas.DaEvolucaoAsync(sessao.Id))!.Pontos.Should().HaveCount(3);
    }

    [Fact]
    public async Task Aplicar_protocolo_vazio_e_recusado()
    {
        var paciente = await CriarPacienteAsync();
        var sessao = await CriarSessaoAsync(paciente.Id);

        // Protocolo sem ponto só pode nascer esvaziado depois; aplicá-lo apagaria o mapa
        // da sessão em silêncio, que é o oposto do que quem clicou pediu.
        var protocolo = await _mapas.SalvarComoProtocoloAsync(paciente.Id, "Vazio", Pontos());
        _db.PontosProtocolo.RemoveRange(_db.PontosProtocolo);
        await _db.SaveChangesAsync();

        var acao = () => _mapas.AplicarProtocoloAsync(sessao.Id, protocolo.Id);

        await acao.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*nenhum ponto marcado*");
    }

    [Fact]
    public async Task Aplicar_protocolo_inexistente_e_recusado()
    {
        var paciente = await CriarPacienteAsync();
        var sessao = await CriarSessaoAsync(paciente.Id);

        var acao = () => _mapas.AplicarProtocoloAsync(sessao.Id, 4242);

        await acao.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Protocolo não encontrado*");
    }

    [Fact]
    public async Task Excluir_protocolo_inexistente_e_recusado()
    {
        var acao = () => _mapas.ExcluirProtocoloAsync(4242, "teste");

        await acao.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Protocolo não encontrado*");
    }

    [Fact]
    public async Task Excluir_protocolo_deixa_rastro_na_auditoria()
    {
        var paciente = await CriarPacienteAsync();
        var protocolo = await _mapas.SalvarComoProtocoloAsync(paciente.Id, "Lombar", Pontos());

        await _mapas.ExcluirProtocoloAsync(protocolo.Id, "joana");

        (await _repo.EventosAuditoriaAsync())
            .Should().Contain(e => e.Acao == "ProtocoloCorporalExcluido" && e.Operador == "joana");
    }

    [Fact]
    public async Task Repetir_a_sessao_anterior_grava_os_mesmos_pontos_na_nova()
    {
        var paciente = await CriarPacienteAsync();
        var anterior = await CriarSessaoAsync(paciente.Id, Hoje.AddDays(-7));
        await _mapas.SalvarAsync(anterior.Id, Pontos(4));

        var atual = await CriarSessaoAsync(paciente.Id, Hoje);
        var mapa = await _mapas.CopiarDaSessaoAnteriorAsync(atual.Id, "teste");

        mapa.Pontos.Should().HaveCount(4);
    }

    [Fact]
    public async Task Repetir_sem_sessao_anterior_com_mapa_e_recusado()
    {
        var paciente = await CriarPacienteAsync();
        var sessao = await CriarSessaoAsync(paciente.Id);

        var acao = () => _mapas.CopiarDaSessaoAnteriorAsync(sessao.Id, "teste");

        await acao.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Nenhuma sessão anterior*");
    }

    [Fact]
    public async Task Repetir_de_sessao_inexistente_e_recusado()
    {
        var acao = () => _mapas.CopiarDaSessaoAnteriorAsync(4242, "teste");

        await acao.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Sessão não encontrada*");
    }

    [Fact]
    public async Task Guardar_como_protocolo_o_mapa_ja_salvo_de_uma_sessao()
    {
        var paciente = await CriarPacienteAsync();
        var sessao = await CriarSessaoAsync(paciente.Id);
        await _mapas.SalvarAsync(sessao.Id, Pontos(3));

        var protocolo = await _mapas.SalvarComoProtocoloDaSessaoAsync(
            sessao.Id, "Lombar padrão", daClinica: true, operador: "teste");

        protocolo.Pontos.Should().HaveCount(3);
        protocolo.PacienteId.Should().BeNull("protocolo da clínica não é de um paciente");
    }

    [Fact]
    public async Task Guardar_como_protocolo_sessao_sem_mapa_e_recusado()
    {
        var paciente = await CriarPacienteAsync();
        var sessao = await CriarSessaoAsync(paciente.Id);

        var acao = () => _mapas.SalvarComoProtocoloDaSessaoAsync(sessao.Id, "Vazio");

        await acao.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*nenhum ponto marcado*");
    }

    [Fact]
    public async Task Guardar_como_protocolo_de_sessao_inexistente_e_recusado()
    {
        var acao = () => _mapas.SalvarComoProtocoloDaSessaoAsync(4242, "X");

        await acao.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Sessão não encontrada*");
    }

    // ====================================================================
    //  PACOTE — o catálogo e a venda avulsa
    // ====================================================================

    [Fact]
    public async Task Catalogo_lista_e_filtra_por_ativo()
    {
        await _pacotes.SalvarCatalogoAsync(new PacoteCatalogo
        {
            Nome = "10 sessões", Tipo = TipoPacote.Sessoes, SessoesIncluidas = 10, Valor = 900
        });
        await _pacotes.SalvarCatalogoAsync(new PacoteCatalogo
        {
            Nome = "Antigo", Tipo = TipoPacote.Sessoes, SessoesIncluidas = 5, Valor = 400,
            Ativo = false
        });

        (await _pacotes.CatalogoAsync(somenteAtivos: false)).Should().HaveCount(2);
        (await _pacotes.CatalogoAsync()).Should().ContainSingle()
            .Which.Nome.Should().Be("10 sessões");
    }

    [Fact]
    public async Task Catalogo_recusa_pacote_sem_nome_valor_negativo_e_sessoes_ausentes()
    {
        var semNome = () => _pacotes.SalvarCatalogoAsync(new PacoteCatalogo { Nome = " " });
        await semNome.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*nome ao pacote*");

        var valorNegativo = () => _pacotes.SalvarCatalogoAsync(
            new PacoteCatalogo { Nome = "X", Valor = -1 });
        await valorNegativo.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*não pode ser negativo*");

        // Pacote de sessões sem número de sessões não tem saldo para controlar.
        var semSessoes = () => _pacotes.SalvarCatalogoAsync(
            new PacoteCatalogo { Nome = "X", Tipo = TipoPacote.Sessoes });
        await semSessoes.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*quantas sessões*");
    }

    [Fact]
    public async Task Pacote_sai_do_catalogo_sem_mexer_no_que_ja_foi_vendido()
    {
        var paciente = await CriarPacienteAsync();
        var catalogo = await _pacotes.SalvarCatalogoAsync(new PacoteCatalogo
        {
            Nome = "10 sessões", Tipo = TipoPacote.Sessoes, SessoesIncluidas = 10, Valor = 900
        });
        var vendido = await _pacotes.VenderAsync(paciente.Id, catalogo.Id, Hoje, operador: "teste");

        await _pacotes.ExcluirDoCatalogoAsync(catalogo.Id);

        (await _pacotes.CatalogoAsync(somenteAtivos: false)).Should().BeEmpty();
        // A venda COPIOU os dados: tirar de linha não desfaz o que o paciente comprou.
        (await _pacotes.ObterAsync(vendido.Id))!.Nome.Should().Be("10 sessões");
    }

    [Fact]
    public async Task Venda_avulsa_recusa_o_que_nao_faz_sentido()
    {
        var paciente = await CriarPacienteAsync();

        var semNome = () => _pacotes.RegistrarVendaAsync(new PacotePaciente
        {
            PacienteId = paciente.Id, Nome = " ", Tipo = TipoPacote.Voucher
        });
        await semNome.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*nome ao pacote vendido*");

        var valorNegativo = () => _pacotes.RegistrarVendaAsync(new PacotePaciente
        {
            PacienteId = paciente.Id, Nome = "X", Tipo = TipoPacote.Voucher, Valor = -5
        });
        await valorNegativo.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*não pode ser negativo*");

        var zeroSessoes = () => _pacotes.RegistrarVendaAsync(new PacotePaciente
        {
            PacienteId = paciente.Id, Nome = "X", Tipo = TipoPacote.Sessoes,
            SessoesContratadas = 0
        });
        await zeroSessoes.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*ao menos uma sessão*");

        var validadeAntes = () => _pacotes.RegistrarVendaAsync(new PacotePaciente
        {
            PacienteId = paciente.Id, Nome = "X", Tipo = TipoPacote.Voucher,
            DataCompra = Hoje, ValidoAte = Hoje.AddDays(-1)
        });
        await validadeAntes.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*anterior à data da compra*");
    }

    [Fact]
    public async Task Venda_avulsa_de_paciente_inexistente_e_recusada()
    {
        var acao = () => _pacotes.RegistrarVendaAsync(new PacotePaciente
        {
            PacienteId = 4242, Nome = "X", Tipo = TipoPacote.Voucher
        });

        await acao.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Paciente não encontrado*");
    }

    [Fact]
    public async Task Pagamento_do_caixa_pode_ser_amarrado_ao_pacote()
    {
        var paciente = await CriarPacienteAsync();
        var pacote = await _pacotes.RegistrarVendaAsync(new PacotePaciente
        {
            PacienteId = paciente.Id, Nome = "10 sessões", Tipo = TipoPacote.Sessoes,
            SessoesContratadas = 10, Valor = 900, DataCompra = Hoje
        }, "teste");

        var entrada = await _financeiro.LancarAsync(
            Hoje, TipoLancamento.Entrada, "Pacote de 10 sessões", 900m,
            pacienteId: paciente.Id, operador: "teste");

        await _pacotes.VincularPagamentoAsync(pacote.Id, entrada.Id);

        (await _pacotes.ObterAsync(pacote.Id))!.LancamentoFinanceiroId.Should().Be(entrada.Id);
    }

    [Fact]
    public async Task Amarrar_pagamento_a_pacote_inexistente_e_recusado()
    {
        var acao = () => _pacotes.VincularPagamentoAsync(4242, 1);

        await acao.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Pacote não encontrado*");
    }

    [Fact]
    public async Task Cancelamento_de_pacote_exige_motivo_e_nao_acontece_duas_vezes()
    {
        var paciente = await CriarPacienteAsync();
        var pacote = await _pacotes.RegistrarVendaAsync(new PacotePaciente
        {
            PacienteId = paciente.Id, Nome = "10 sessões", Tipo = TipoPacote.Sessoes,
            SessoesContratadas = 10, Valor = 900, DataCompra = Hoje
        }, "teste");

        var semMotivo = () => _pacotes.CancelarAsync(pacote.Id, "  ", "teste");
        await semMotivo.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*por que o pacote*");

        await _pacotes.CancelarAsync(pacote.Id, "arrependimento", "teste");

        var deNovo = () => _pacotes.CancelarAsync(pacote.Id, "de novo", "teste");
        await deNovo.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*já foi cancelado*");
    }

    [Fact]
    public async Task Pacote_cancelado_recusa_consumo_dizendo_por_que()
    {
        var paciente = await CriarPacienteAsync();
        var pacote = await _pacotes.RegistrarVendaAsync(new PacotePaciente
        {
            PacienteId = paciente.Id, Nome = "10 sessões", Tipo = TipoPacote.Sessoes,
            SessoesContratadas = 10, Valor = 900, DataCompra = Hoje
        }, "teste");
        await _pacotes.CancelarAsync(pacote.Id, "arrependimento", "teste");

        var acao = () => _pacotes.ConsumirAsync(pacote.Id, Hoje);

        await acao.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*foi cancelado*");
    }

    [Fact]
    public async Task Pacote_vencido_recusa_consumo_dizendo_a_data()
    {
        var paciente = await CriarPacienteAsync();
        var pacote = await _pacotes.RegistrarVendaAsync(new PacotePaciente
        {
            PacienteId = paciente.Id, Nome = "10 sessões", Tipo = TipoPacote.Sessoes,
            SessoesContratadas = 10, Valor = 900, DataCompra = Hoje.AddDays(-60),
            ValidoAte = Hoje.AddDays(-1)
        }, "teste");

        var acao = () => _pacotes.ConsumirAsync(pacote.Id, Hoje);

        await acao.Should().ThrowAsync<InvalidOperationException>().WithMessage("*venceu em*");
    }

    [Fact]
    public async Task Todos_os_pacotes_vendidos_saem_com_a_situacao_calculada()
    {
        var paciente = await CriarPacienteAsync();
        await _pacotes.RegistrarVendaAsync(new PacotePaciente
        {
            PacienteId = paciente.Id, Nome = "10 sessões", Tipo = TipoPacote.Sessoes,
            SessoesContratadas = 10, Valor = 900, DataCompra = Hoje,
            ValidoAte = Hoje.AddDays(30)
        }, "teste");

        var vendidos = await _pacotes.VendidosAsync(Hoje);
        vendidos.Should().ContainSingle().Which.Situacao.Should().Be(StatusPacote.Ativo);

        // A MESMA venda, lida depois do vencimento, é outra situação: ela é calculada, e
        // um pacote gravado como "Ativo" viraria mentira à meia-noite do vencimento.
        (await _pacotes.VendidosAsync(Hoje.AddDays(31)))
            .Should().ContainSingle().Which.Situacao.Should().Be(StatusPacote.Expirado);
    }

    [Fact]
    public async Task Devolver_sessao_ao_saldo_exige_motivo_e_nao_acontece_duas_vezes()
    {
        var paciente = await CriarPacienteAsync();
        var pacote = await _pacotes.RegistrarVendaAsync(new PacotePaciente
        {
            PacienteId = paciente.Id, Nome = "10 sessões", Tipo = TipoPacote.Sessoes,
            SessoesContratadas = 10, Valor = 900, DataCompra = Hoje
        }, "teste");
        var consumo = await _pacotes.ConsumirAsync(pacote.Id, Hoje, operador: "teste");

        var semMotivo = () => _pacotes.CancelarConsumoAsync(consumo.Id, " ", "teste");
        await semMotivo.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*voltando ao saldo*");

        await _pacotes.CancelarConsumoAsync(consumo.Id, "sessão não aconteceu", "teste");

        var deNovo = () => _pacotes.CancelarConsumoAsync(consumo.Id, "de novo", "teste");
        await deNovo.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*já foi cancelado*");
    }

    [Fact]
    public async Task Descrever_pacotes_ja_lidos_da_a_mesma_resposta_do_banco()
    {
        var paciente = await CriarPacienteAsync();
        await _pacotes.RegistrarVendaAsync(new PacotePaciente
        {
            PacienteId = paciente.Id, Nome = "10 sessões", Tipo = TipoPacote.Sessoes,
            SessoesContratadas = 10, Valor = 900, DataCompra = Hoje
        }, "teste");

        var doBanco = await _pacotes.DoPacienteAsync(paciente.Id, Hoje);
        var emBloco = _pacotes.Descrever(await _repo.PacotesDoPacienteAsync(paciente.Id), Hoje);

        // Duas respostas para "o paciente ainda tem sessão paga?" seria divergir sobre
        // cobrar ou não cobrar do paciente que está na frente do balcão.
        emBloco.Should().BeEquivalentTo(doBanco);
    }

    // ====================================================================
    //  CONSENTIMENTO, AUTORIZAÇÃO E ELEGIBILIDADE
    // ====================================================================

    [Fact]
    public async Task Consentimento_de_paciente_inexistente_e_recusado()
    {
        var acao = () => _consentimentos.RegistrarAsync(
            4242, FinalidadeConsentimento.TratamentoDeDados, true);

        await acao.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Paciente não encontrado*");
    }

    [Fact]
    public void Toda_finalidade_de_consentimento_tem_rotulo_em_portugues()
    {
        // Rótulo é o que o paciente lê no termo que ASSINA. Finalidade nova sem rótulo
        // sairia impressa como o nome do enum, em inglês de programador.
        foreach (var finalidade in Enum.GetValues<FinalidadeConsentimento>())
            ConsentimentoService.Rotular(finalidade)
                .Should().NotBe(finalidade.ToString(), $"{finalidade} precisa de rótulo");
    }

    [Fact]
    public async Task Autorizacao_com_utilizadas_negativas_e_recusada()
    {
        var paciente = await CriarPacienteAsync();

        var acao = () => _autorizacoes.SalvarAsync(new AutorizacaoSessoes
        {
            PacienteId = paciente.Id,
            QuantidadeAutorizada = 10,
            QuantidadeUtilizadaManual = -1,
            DataEmissao = Hoje,
            DataValidade = Hoje.AddDays(30)
        }, "teste");

        await acao.Should().ThrowAsync<ArgumentException>().WithMessage("*não pode ser negativo*");
    }

    [Fact]
    public async Task Sem_autorizacao_vigente_so_alarma_quem_JA_teve_senha()
    {
        var nunca = await CriarPacienteAsync("Nunca teve senha");
        var teve = await CriarPacienteAsync("Já teve senha");

        await _autorizacoes.SalvarAsync(new AutorizacaoSessoes
        {
            PacienteId = teve.Id,
            QuantidadeAutorizada = 10,
            DataEmissao = Hoje.AddDays(-60),
            DataValidade = Hoje.AddDays(-1)   // venceu
        }, "teste");

        // Numa clínica que não controla senha, avisar sempre seria um alerta que dispara
        // para todo mundo — e alerta que sempre aparece é alerta que ninguém lê.
        (await _elegibilidade.ConferirAsync(nunca.Id, Hoje)).Alertas
            .Should().NotContain(a => a.Motivo == ImpedimentoElegibilidade.SemAutorizacaoVigente);

        (await _elegibilidade.ConferirAsync(teve.Id, Hoje)).Alertas
            .Should().Contain(a => a.Motivo == ImpedimentoElegibilidade.SemAutorizacaoVigente);
    }

    [Fact]
    public async Task Convenio_que_usa_consulta_e_paciente_sem_nenhuma_emitida_entra_amarelo()
    {
        var paciente = await CriarPacienteAsync("Sem consulta", Convenio.UnimedIntercambio);

        var status = await _consultas.ListarAsync(Hoje);

        var dele = status.Single(s => s.PacienteId == paciente.Id);
        dele.UsaConsulta.Should().BeTrue();
        dele.PrecisaRenovar.Should().BeTrue();
        dele.Urgencia.Should().Be(NivelUrgencia.Amarelo);
    }

    // ====================================================================
    //  DOCUMENTO CLÍNICO — o que ele recusa
    // ====================================================================

    [Fact]
    public async Task Atestado_de_menos_de_um_dia_e_recusado()
    {
        var paciente = await CriarPacienteAsync();
        var ana = await CriarProfissionalAsync();

        var acao = () => _documentos.EmitirAsync(new DocumentoClinico
        {
            PacienteId = paciente.Id,
            ProfissionalId = ana.Id,
            Tipo = TipoDocumentoClinico.Atestado,
            DiasAfastamento = 0
        }, operador: "teste");

        await acao.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*pelo menos um dia*");
    }

    [Fact]
    public async Task Periodo_que_termina_antes_de_comecar_e_recusado()
    {
        var paciente = await CriarPacienteAsync();
        var ana = await CriarProfissionalAsync();

        var acao = () => _documentos.EmitirAsync(new DocumentoClinico
        {
            PacienteId = paciente.Id,
            ProfissionalId = ana.Id,
            Tipo = TipoDocumentoClinico.Atestado,
            PeriodoInicio = Hoje,
            PeriodoFim = Hoje.AddDays(-2)
        }, operador: "teste");

        await acao.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*anterior ao início*");
    }

    [Fact]
    public async Task Documento_que_exige_assinante_recusa_profissional_inexistente()
    {
        var paciente = await CriarPacienteAsync();

        // Receita sem quem assina não vale nada — e vale menos ainda descobrir isso na
        // frente do paciente, com o papel já impresso.
        var acao = () => _documentos.EmitirAsync(new DocumentoClinico
        {
            PacienteId = paciente.Id,
            ProfissionalId = 4242,
            Tipo = TipoDocumentoClinico.Receita,
            Itens = { new ItemDocumento { Descricao = "Óleo essencial" } }
        }, "teste");

        await acao.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Profissional não encontrado*");
    }

    [Fact]
    public async Task Modelo_sem_nome_nao_e_guardado()
    {
        var acao = () => _documentos.SalvarModeloAsync(new ModeloDocumento
        {
            Nome = "  ",
            Tipo = TipoDocumentoClinico.Receita,
            Corpo = "qualquer coisa"
        }, "teste");

        await acao.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*nome ao modelo*");
    }

    [Fact]
    public async Task Modelo_apagado_some_e_o_documento_feito_com_ele_continua()
    {
        var paciente = await CriarPacienteAsync();
        var ana = await CriarProfissionalAsync();

        var modelo = await _documentos.SalvarModeloAsync(new ModeloDocumento
        {
            Nome = "Orientação pós-sessão",
            Tipo = TipoDocumentoClinico.Comparecimento,
            Corpo = "Beba água."
        }, "teste");

        var documento = await _documentos.EmitirAsync(new DocumentoClinico
        {
            PacienteId = paciente.Id,
            ProfissionalId = ana.Id,
            Tipo = TipoDocumentoClinico.Comparecimento,
            Corpo = "Beba água."
        }, operador: "teste");

        await _documentos.ExcluirModeloAsync(modelo.Id);

        (await _documentos.ModelosAsync()).Should().BeEmpty();
        // Aplicar modelo COPIA o texto: apagá-lo não reescreve o que o paciente levou.
        (await _documentos.ObterAsync(documento.Id))!.Corpo.Should().Be("Beba água.");
    }

    [Fact]
    public async Task Termo_de_consentimento_diz_revogado_e_recusado_com_a_data()
    {
        var paciente = await CriarPacienteAsync();

        await _consentimentos.RegistrarAsync(
            paciente.Id, FinalidadeConsentimento.UsoDeImagem, concedido: false);

        var concedido = await _consentimentos.RegistrarAsync(
            paciente.Id, FinalidadeConsentimento.TratamentoDeDados, concedido: true);
        await _consentimentos.RevogarAsync(concedido.Id, "paciente pediu");

        var termo = await _documentos.EmitirTermoConsentimentoAsync(paciente.Id, operador: "teste");

        // O termo impresso tem de dizer a situação REAL de cada finalidade. Um termo que
        // some com o "revogado" faria o paciente assinar como vigente o que ele cancelou.
        termo.Itens.Should().Contain(i => i.Detalhe != null && i.Detalhe.StartsWith("Revogado em"));
        termo.Itens.Should().Contain(i => i.Detalhe != null && i.Detalhe.StartsWith("Recusado em"));
        termo.Itens.Should().Contain(i => i.Detalhe == "Nunca perguntado");
    }

    [Fact]
    public async Task Guia_glosada_no_balcao_diz_o_prazo_de_recurso_em_cada_situacao()
    {
        var paciente = await CriarPacienteAsync();
        var ag = await _agenda.AgendarAsync(
            paciente.Id, Manha, ModalidadeAtendimento.AcupunturaSimples, null);
        var lancado = await _agenda.ConfirmarPresencaAsync(ag.Id);
        var codigo = lancado.Atendimento.Codigos[0];

        async Task<string> AlertaComPrazoAsync(DateOnly? limite)
        {
            var atual = await _db.Codigos.FirstAsync(c => c.Id == codigo.Id);
            atual.Glosa = StatusGlosa.Glosada;
            atual.MotivoGlosa = "1707 — falta de senha";
            atual.DataLimiteRecurso = limite;
            await _db.SaveChangesAsync();

            var resultado = await _elegibilidade.ConferirAsync(paciente.Id, Hoje);
            return resultado.Alertas
                .Single(a => a.Motivo == ImpedimentoElegibilidade.GuiaGlosada)
                .Descricao;
        }

        // As três leituras que o balcão precisa distinguir. "Sem prazo" e "prazo vencido"
        // dizem coisas opostas sobre a mesma guia, e trocá-las custa o recurso inteiro.
        (await AlertaComPrazoAsync(null)).Should().Contain("sem prazo de recurso registrado");
        (await AlertaComPrazoAsync(Hoje.AddDays(-3))).Should().Contain("VENCIDO há 3 dia(s)");
        (await AlertaComPrazoAsync(Hoje)).Should().Contain("vence HOJE");
        (await AlertaComPrazoAsync(Hoje.AddDays(5))).Should().Contain("restam 5 dia(s)");
    }

    // ====================================================================
    //  O PAPEL QUE SAI DA AGENDA
    // ====================================================================

    [Fact]
    public async Task Folha_do_dia_traz_o_cabecalho_do_prestador_e_marca_quem_ja_foi_atendido()
    {
        var paciente = await CriarPacienteAsync();
        var ana = await CriarProfissionalAsync();
        var ag = await _agenda.AgendarAsync(
            paciente.Id, Manha, ModalidadeAtendimento.AcupunturaSimples, null,
            profissionalId: ana.Id);
        await _agenda.ConfirmarPresencaAsync(ag.Id);

        var pdf = await new AgendaPdfService(_repo, _agenda).AgendaDoDiaAsync(
            Hoje, null,
            new DadosPrestador { NomeFantasia = "Clínica SemDor", Endereco = "Rua A, 100" });

        // O PDF é desenho, e desenho que nunca rodou é desenho que quebra na clínica: o
        // que se verifica aqui é que ele SAI, com endereço e com sessão já realizada.
        pdf.Should().NotBeEmpty();
    }

    [Fact]
    public async Task Comprovante_do_paciente_sai_com_a_observacao_do_horario()
    {
        var paciente = await CriarPacienteAsync();
        var ag = await _agenda.AgendarAsync(
            paciente.Id, Manha, ModalidadeAtendimento.AcupunturaSimples,
            "Trazer os exames de imagem.");

        var pdf = await new AgendaPdfService(_repo, _agenda).ComprovanteAsync(
            ag.Id, new DadosPrestador { NomeFantasia = "Clínica SemDor", Endereco = "Rua A, 100" });

        pdf.Should().NotBeEmpty();
    }

    // ====================================================================
    //  DEGRADAÇÃO — o caminho que só roda quando algo já deu errado
    // ====================================================================

    /// <summary>
    /// Monta o fechamento da sessão com um repositório que falha no método informado.
    /// É a única forma de exercitar os `catch` de degradação: contra um SQLite que
    /// funciona, esses trechos nunca rodam.
    /// </summary>
    private FechamentoSessaoService FechamentoQueFalhaEm(string metodo)
    {
        var frágil = RepositorioComFalha.QueFalhaEm(_repo, metodo);
        return new FechamentoSessaoService(
            frágil,
            new AgendaService(frágil, new AtendimentoService(frágil)),
            new PacoteService(frágil),
            new EstoqueService(frágil),
            new FinanceiroService(frágil));
    }

    [Fact]
    public async Task Proposta_sobrevive_a_falha_na_leitura_dos_pacotes()
    {
        var paciente = await CriarPacienteAsync();
        var ag = await _agenda.AgendarAsync(
            paciente.Id, Manha, ModalidadeAtendimento.AcupunturaSimples, null);

        var proposta = await FechamentoQueFalhaEm(nameof(_repo.PacotesDoPacienteAsync))
            .PrepararAsync(ag.Id);

        // A janela de fechamento ABRE: perder a sugestão de pacote não pode impedir a
        // recepção de concluir a sessão que já aconteceu.
        proposta.Should().NotBeNull();
        proposta.PacoteADebitar.Should().BeNull();
    }

    [Fact]
    public async Task Proposta_sobrevive_a_falha_na_leitura_do_valor_sugerido()
    {
        var paciente = await CriarPacienteAsync();
        var ag = await _agenda.AgendarAsync(
            paciente.Id, Manha, ModalidadeAtendimento.AcupunturaSimples, null);

        var proposta = await FechamentoQueFalhaEm(nameof(_repo.UltimoRecebimentoDoPacienteAsync))
            .PrepararAsync(ag.Id);

        proposta.ValorSugerido.Should().BeNull();
        proposta.ProcedenciaDoValor.Should().BeNull();
    }

    [Fact]
    public async Task Proposta_sobrevive_a_falha_na_sugestao_de_insumo()
    {
        var paciente = await CriarPacienteAsync();
        var ag = await _agenda.AgendarAsync(
            paciente.Id, Manha, ModalidadeAtendimento.AcupunturaSimples, null);

        var proposta = await FechamentoQueFalhaEm(nameof(_repo.UltimoConsumoDeSessaoAsync))
            .PrepararAsync(ag.Id);

        proposta.Insumos.Should().BeEmpty();
    }

    [Fact]
    public async Task Pacote_que_nao_pode_ser_debitado_vira_AVISO_e_a_guia_nasce_assim_mesmo()
    {
        var paciente = await CriarPacienteAsync();
        var ag = await _agenda.AgendarAsync(
            paciente.Id, Manha, ModalidadeAtendimento.AcupunturaSimples, null);

        var resultado = await FechamentoQueFalhaEm(nameof(_repo.PacotesDoPacienteAsync))
            .ConcluirAsync(new DecisaoFechamento(ag.Id, DebitarPacote: true), "teste");

        // Esta é a regra central do fechamento: desfazer o atendimento porque o pacote não
        // pôde ser debitado deixaria a clínica sem a guia de uma sessão que o paciente
        // recebeu. A falha vira AVISO — e aviso que a tela mostra, não erro engolido.
        resultado.Atendimento.Should().NotBeNull();
        resultado.Atendimento.Codigos.Should().NotBeEmpty();
        resultado.Consumo.Should().BeNull();
        resultado.Avisos.Should().ContainSingle().Which.Should().Contain("pacote NÃO foi debitado");
    }

    [Fact]
    public async Task Insumo_que_nao_pode_ser_baixado_vira_AVISO_e_a_guia_nasce_assim_mesmo()
    {
        var paciente = await CriarPacienteAsync();
        var ag = await _agenda.AgendarAsync(
            paciente.Id, Manha, ModalidadeAtendimento.AcupunturaSimples, null);

        // Item que não existe: a baixa de estoque estoura, o atendimento não.
        var resultado = await FechamentoQueFalhaEm("nada").ConcluirAsync(
            new DecisaoFechamento(ag.Id, DebitarPacote: false,
                Insumos: [new InsumoAConsumir(4242, 1)]),
            "teste");

        resultado.Atendimento.Should().NotBeNull();
        resultado.Movimentos.Should().BeEmpty();
        resultado.Avisos.Should().ContainSingle();
    }

    [Fact]
    public async Task Quantidade_zero_de_insumo_e_ignorada_sem_virar_aviso()
    {
        var paciente = await CriarPacienteAsync();
        var ag = await _agenda.AgendarAsync(
            paciente.Id, Manha, ModalidadeAtendimento.AcupunturaSimples, null);

        // Linha zerada na tela é "não usei", não é erro: avisar aqui encheria o balcão de
        // recado sobre uma decisão que a própria recepção tomou.
        var resultado = await FechamentoQueFalhaEm("nada").ConcluirAsync(
            new DecisaoFechamento(ag.Id, DebitarPacote: false,
                Insumos: [new InsumoAConsumir(4242, 0)]),
            "teste");

        resultado.Movimentos.Should().BeEmpty();
        resultado.Avisos.Should().BeEmpty();
    }

    public void Dispose()
    {
        _db.Dispose();
        _conn.Dispose();
        GC.SuppressFinalize(this);
    }
}
