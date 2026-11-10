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
/// A TRAVA DE CONSULTA DO CONVÊNIO e a LIBERAÇÃO DA GERÊNCIA (nov/2026). O pedido: "se a
/// secretária tentar agendar uma consulta antes do prazo, vai ter uma box para pedir a
/// liberação da gerência; chega uma mensagem no WhatsApp da gerência para liberar sim ou não;
/// quando liberar, chega a mensagem para o paciente e para a secretária".
///
/// O que estes testes prendem é a promessa inteira, e principalmente o que NÃO pode acontecer:
/// a trava ligar sozinha (ela nasce desligada), a consulta particular ser barrada, quem não é
/// aprovador decidir, dois gerentes marcarem dois horários, e um "não" de conversa negar um pedido.
/// </summary>
public class TravaDeConsultaTests : IDisposable
{
    private readonly SqliteConnection _conn;
    private readonly ClinicaDbContext _db;
    private readonly ClinicaRepositorio _repo;
    private readonly ParametrosService _parametros;
    private readonly AgendaService _agenda;
    private readonly int _medica;
    private DateTime _agora = new(2026, 11, 10, 9, 0, 0);

    private static readonly DateTime Primeira = new(2026, 11, 10, 14, 0, 0);

    public TravaDeConsultaTests()
    {
        _conn = new SqliteConnection("DataSource=:memory:");
        _conn.Open();
        _db = new ClinicaDbContext(new DbContextOptionsBuilder<ClinicaDbContext>().UseSqlite(_conn).Options);
        _db.Database.EnsureCreated();
        var medica = new Profissional { Nome = "Dra. Patrícia" };
        _db.Profissionais.Add(medica);
        _db.SaveChanges();
        _medica = medica.Id;
        _repo = new ClinicaRepositorio(_db);
        _parametros = new ParametrosService(_repo);
        _agenda = new AgendaService(_repo, new AtendimentoService(_repo, parametros: _parametros), _parametros);

        // A Sul América é personalizada: o prazo (15 dias) vem do cadastro dela.
        CatalogoConvenios.Atualizar(
        [
            new EntradaConvenio("SulAmerica", "Sul América", Convenio.Personalizado, true,
                new ConfiguracaoRegraGenerica { ValidadeConsultaDias = 15 }, FormatoNumeroGuia.SemValidacao, GeraGuia: true),
            new EntradaConvenio(ConvenioCadastro.CodigoParticular, "Particular", Convenio.Personalizado, true,
                new ConfiguracaoRegraGenerica(), FormatoNumeroGuia.SemValidacao, GeraGuia: false)
        ]);
    }

    public void Dispose()
    {
        CatalogoConvenios.Atualizar([]);
        _db.Dispose();
        _conn.Dispose();
        GC.SuppressFinalize(this);
    }

    // ==================== Cenário ====================

    private async Task<Paciente> PacienteAsync(string nome = "Mariane Carvalho", string? telefone = "(22) 99888-7766")
    {
        var p = new Paciente
        {
            Nome = nome, Convenio = Convenio.Personalizado, ConvenioCodigo = "SulAmerica", Sexo = Sexo.Feminino, Telefone = telefone
        };
        _db.Pacientes.Add(p);
        await _db.SaveChangesAsync();
        return p;
    }

    private async Task<UsuarioSistema> UsuarioAsync(string nome, PerfilAcesso perfil, bool aprova = false, string? whatsapp = null)
    {
        var u = new UsuarioSistema
        {
            Nome = nome, Login = nome.ToLowerInvariant().Replace(' ', '.'), Perfil = perfil, Ativo = true,
            AprovaLiberacaoDeTrava = aprova, TelefoneWhatsApp = whatsapp, WhatsAppLigadoEm = whatsapp is null ? null : _agora.AddDays(-1)
        };
        _db.Usuarios.Add(u);
        await _db.SaveChangesAsync();
        _db.ChangeTracker.Clear();
        return u;
    }

    private Task<Agendamento> ConsultaAsync(int pacienteId, DateTime quando, bool particular = false)
        => _agenda.AgendarAsync(pacienteId, quando, ModalidadeAtendimento.Consulta, null,
            especialidadeConsultaCodigo: nameof(Especialidade.Endocrinologia), profissionalId: _medica,
            operador: "recepcao", particular: particular);

    private LiberacaoDeTravaService Liberacoes() => new(_db, _agenda, () => _agora);

    private NovoPedidoDeLiberacao Pedido(int pacienteId, DateTime quando, string motivo = "Retorno com exames, pedido da médica")
        => new(pacienteId, quando, nameof(ModalidadeAtendimento.Consulta), nameof(Especialidade.Endocrinologia),
            _medica, null, null, false, false, null, motivo);

    /// <summary>A paciente consultou hoje, a trava está ligada e a secretária quer marcar outra em 5 dias.</summary>
    private async Task<(Paciente Paciente, UsuarioSistema Secretaria, DateTime Segunda)> CenarioTravadoAsync()
    {
        await _parametros.DefinirTravaDeConsultaAsync(true);
        var paciente = await PacienteAsync();
        await ConsultaAsync(paciente.Id, Primeira);
        var secretaria = await UsuarioAsync("Rayssa Recepcao", PerfilAcesso.Recepcao);
        return (paciente, secretaria, Primeira.AddDays(5));
    }

    // ==================== A regra ====================

    [Fact]
    public void A_trava_olha_para_tras_e_para_a_frente_e_solta_no_dia_do_prazo()
    {
        var hoje = new DateOnly(2026, 11, 10);
        ConsultaParaTrava[] feita = [new(1, hoje.ToDateTime(new TimeOnly(14, 0)), "2026-000123")];

        TravaDeConsulta.Conferir(hoje.AddDays(5), 15, feita).Should().NotBeNull();
        TravaDeConsulta.Conferir(hoje.AddDays(14), 15, feita).Should().NotBeNull("14 dias ainda é dentro do prazo de 15");
        TravaDeConsulta.Conferir(hoje.AddDays(15), 15, feita).Should().BeNull("no 15º dia o convênio aceita outra");
        TravaDeConsulta.Conferir(hoje.AddDays(-3), 15, feita).Should().NotBeNull("marcar ANTES de uma consulta já marcada também esbarra");
        TravaDeConsulta.Conferir(hoje.AddDays(5), 0, feita).Should().BeNull("convênio sem prazo não trava");
    }

    [Fact]
    public async Task Desligada_por_padrao_a_trava_nao_muda_nada()
    {
        var paciente = await PacienteAsync();
        await ConsultaAsync(paciente.Id, Primeira);

        var segunda = await ConsultaAsync(paciente.Id, Primeira.AddDays(5));

        segunda.Id.Should().BeGreaterThan(0, "sem a direção ligar a chave, a agenda continua avisando e não impedindo");
    }

    [Fact]
    public async Task Ligada_recusa_a_segunda_consulta_dentro_do_prazo_e_diz_quando_solta()
    {
        var (paciente, _, segunda) = await CenarioTravadoAsync();

        var marcar = async () => await ConsultaAsync(paciente.Id, segunda);

        var recusa = (await marcar.Should().ThrowAsync<TravaDeConsultaException>()).Which;
        recusa.Trava.PrazoDias.Should().Be(15);
        recusa.Trava.ConvenioNome.Should().Be("Sul América");
        recusa.Trava.LiberadaAPartirDe.Should().Be(DateOnly.FromDateTime(Primeira).AddDays(15));
        recusa.Message.Should().Contain("a cada 15 dias").And.Contain("25/11/2026").And.Contain("liberação");

        // No dia em que o prazo vence, marca.
        _db.ChangeTracker.Clear();
        (await ConsultaAsync(paciente.Id, Primeira.AddDays(15))).Id.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task Particular_acupuntura_e_outro_paciente_nao_sao_travados()
    {
        var (paciente, _, segunda) = await CenarioTravadoAsync();

        // Consulta PARTICULAR: não há guia para o convênio recusar.
        (await ConsultaAsync(paciente.Id, segunda, particular: true)).Particular.Should().BeTrue();
        // A trava é só de consulta: a acupuntura da mesma paciente segue marcando.
        _db.ChangeTracker.Clear();
        (await _agenda.AgendarAsync(paciente.Id, segunda.AddHours(1), ModalidadeAtendimento.AcupunturaSimples, null,
            profissionalId: _medica, operador: "recepcao")).Id.Should().BeGreaterThan(0);
        // E a consulta particular não ACENDE a trava de ninguém: outra paciente, sem histórico, marca.
        _db.ChangeTracker.Clear();
        var outra = await PacienteAsync("Patrícia Lima");
        (await ConsultaAsync(outra.Id, segunda)).Id.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task Editar_a_consulta_que_ja_estava_marcada_nao_confere_a_trava_de_novo_mas_mudar_o_dia_confere()
    {
        // Duas consultas marcadas ANTES de a direção ligar a chave: nenhuma pode ficar presa.
        var paciente = await PacienteAsync();
        await ConsultaAsync(paciente.Id, Primeira);
        var segunda = await ConsultaAsync(paciente.Id, Primeira.AddDays(5));
        await _parametros.DefinirTravaDeConsultaAsync(true);
        _db.ChangeTracker.Clear();

        // Corrigir a observação (mesmo dia) passa.
        await _agenda.RemarcarAsync(segunda.Id, Primeira.AddDays(5), "paciente pediu a sala térrea", operador: "recepcao");

        // Mover para OUTRO dia ainda dentro do prazo é marcar de novo: trava.
        _db.ChangeTracker.Clear();
        var mover = async () => await _agenda.RemarcarAsync(segunda.Id, Primeira.AddDays(7), null, operador: "recepcao");
        await mover.Should().ThrowAsync<TravaDeConsultaException>();
    }

    // ==================== O pedido ====================

    [Fact]
    public async Task O_pedido_chega_ao_chat_e_ao_whatsapp_de_quem_a_direcao_marcou_como_aprovador()
    {
        var (paciente, secretaria, segunda) = await CenarioTravadoAsync();
        var ana = await UsuarioAsync("Ana Gerente", PerfilAcesso.Gerente, aprova: true, whatsapp: "5522999990001");
        var bia = await UsuarioAsync("Bia Gerente", PerfilAcesso.Gerente, aprova: true);            // aprova, sem WhatsApp
        await UsuarioAsync("Caio Gerente", PerfilAcesso.Gerente, aprova: false, whatsapp: "5522999990003"); // gerente que NÃO aprova

        var criado = await Liberacoes().PedirAsync(Pedido(paciente.Id, segunda), secretaria.Id);

        criado.Aprovadores.Should().Be(2);
        criado.ComWhatsApp.Should().Be(1);
        var fila = await Liberacoes().AvisosParaEquipeAsync();
        fila.Should().ContainSingle("só a Ana tem o número ligado — o Caio é gerente, mas a direção não o marcou")
            .Which.Telefone.Should().Be("5522999990001");
        fila[0].Texto.Should().Contain("Mariane Carvalho").And.Contain("Sul América").And.Contain("a cada 15 dias")
            .And.Contain("10/11/2026").And.Contain("Retorno com exames").And.Contain($"liberar {criado.Pedido.Id}");

        // As duas aprovadoras leem o pedido na conversa com o sistema, no app — é a porta que
        // funciona quando o WhatsApp não entrega.
        foreach (var gerente in new[] { ana, bia })
            (await _db.MensagensEquipe.AsNoTracking().CountAsync(m => m.Conversa == ChatEquipeRegras.Sistema(gerente.Id) && m.DoSistema))
                .Should().Be(1);
    }

    [Fact]
    public async Task Sem_motivo_sem_aprovador_ou_sem_trava_o_pedido_e_recusado_dizendo_o_que_fazer()
    {
        var (paciente, secretaria, segunda) = await CenarioTravadoAsync();

        var semAprovador = async () => await Liberacoes().PedirAsync(Pedido(paciente.Id, segunda), secretaria.Id);
        (await semAprovador.Should().ThrowAsync<InvalidOperationException>()).Which.Message.Should().Contain("Acessos");

        await UsuarioAsync("Ana Gerente", PerfilAcesso.Gerente, aprova: true);
        var semMotivo = async () => await Liberacoes().PedirAsync(Pedido(paciente.Id, segunda, motivo: "  "), secretaria.Id);
        (await semMotivo.Should().ThrowAsync<InvalidOperationException>()).Which.Message.Should().Contain("motivo");

        var semTrava = async () => await Liberacoes().PedirAsync(Pedido(paciente.Id, Primeira.AddDays(20)), secretaria.Id);
        (await semTrava.Should().ThrowAsync<InvalidOperationException>()).Which.Message.Should().Contain("marque normalmente");

        // O pedido repetido para o mesmo dia não vira dois.
        await Liberacoes().PedirAsync(Pedido(paciente.Id, segunda), secretaria.Id);
        var repetido = async () => await Liberacoes().PedirAsync(Pedido(paciente.Id, segunda.AddHours(1)), secretaria.Id);
        (await repetido.Should().ThrowAsync<InvalidOperationException>()).Which.Message.Should().Contain("Já existe o pedido");
    }

    // ==================== A decisão ====================

    [Fact]
    public async Task Sim_da_gerencia_marca_o_horario_avisa_a_secretaria_e_confirma_ao_paciente()
    {
        var (paciente, secretaria, segunda) = await CenarioTravadoAsync();
        var ana = await UsuarioAsync("Ana Gerente", PerfilAcesso.Gerente, aprova: true, whatsapp: "5522999990001");
        var criado = await Liberacoes().PedirAsync(Pedido(paciente.Id, segunda), secretaria.Id);

        var resposta = await Liberacoes().DecidirPorTextoAsync(ana, "sim", "whatsapp");

        resposta.Should().Contain("LIBERADO").And.Contain("Mariane Carvalho");
        var pedido = await _db.LiberacoesDeTrava.AsNoTracking().SingleAsync(l => l.Id == criado.Pedido.Id);
        pedido.Estado.Should().Be(LiberacaoDeTrava.Liberada);
        pedido.DecididoPorId.Should().Be(ana.Id, "quem assina a liberação é o LOGIN dono daquele celular");
        pedido.DecididoPor.Should().Be("whatsapp");

        // O horário existe, pela agenda de sempre, com a secretária como quem marcou.
        var marcado = await _db.Agendamentos.AsNoTracking().SingleAsync(a => a.Id == pedido.AgendamentoId);
        marcado.DataHora.Should().Be(segunda);
        marcado.ProfissionalId.Should().Be(_medica);
        marcado.CriadoPor.Should().Be("Rayssa Recepcao");

        // A secretária lê o desfecho na conversa com o sistema; o paciente recebe a confirmação.
        (await _db.MensagensEquipe.AsNoTracking().SingleAsync(m => m.Conversa == ChatEquipeRegras.Sistema(secretaria.Id)))
            .Texto.Should().Contain("Liberado por Ana Gerente").And.Contain("MARCADO");
        var aoPaciente = (await Liberacoes().AvisosParaPacienteAsync()).Single();
        aoPaciente.Telefone.Should().Be("5522998887766");
        aoPaciente.Texto.Should().Contain("Mariane").And.Contain("15/11/2026").And.Contain("Dra. Patrícia")
            .And.NotContain("libera", "o paciente não precisa saber do bastidor");

        // E a trilha diz quem pediu e quem liberou.
        (await _db.Auditoria.AsNoTracking().Where(e => e.PacienteId == paciente.Id).Select(e => e.Acao).ToListAsync())
            .Should().Contain(["LiberacaoDeTravaPedida", "LiberacaoDeTravaConcedida"]);
    }

    [Fact]
    public async Task Nao_da_gerencia_nao_marca_nada_e_a_secretaria_fica_sabendo()
    {
        var (paciente, secretaria, segunda) = await CenarioTravadoAsync();
        var ana = await UsuarioAsync("Ana Gerente", PerfilAcesso.Gerente, aprova: true, whatsapp: "5522999990001");
        var criado = await Liberacoes().PedirAsync(Pedido(paciente.Id, segunda), secretaria.Id);

        var resposta = await Liberacoes().DecidirPorTextoAsync(ana, $"negar {criado.Pedido.Id} já consultou na semana passada", "app");

        resposta.Should().Contain("NEGADO");
        (await _db.Agendamentos.AsNoTracking().CountAsync(a => a.PacienteId == paciente.Id)).Should().Be(1, "só a primeira consulta");
        (await Liberacoes().AvisosParaPacienteAsync()).Should().BeEmpty("nada foi marcado: nada a dizer ao paciente");
        (await _db.MensagensEquipe.AsNoTracking().SingleAsync(m => m.Conversa == ChatEquipeRegras.Sistema(secretaria.Id)))
            .Texto.Should().Contain("NEGADA").And.Contain("já consultou na semana passada");

        // E a trava continua de pé para aquele dia.
        _db.ChangeTracker.Clear();
        var marcar = async () => await ConsultaAsync(paciente.Id, segunda);
        await marcar.Should().ThrowAsync<TravaDeConsultaException>();
    }

    [Fact]
    public async Task So_decide_quem_aprova_e_so_um_decide()
    {
        var (paciente, secretaria, segunda) = await CenarioTravadoAsync();
        var ana = await UsuarioAsync("Ana Gerente", PerfilAcesso.Gerente, aprova: true);
        var bia = await UsuarioAsync("Bia Gerente", PerfilAcesso.Gerente, aprova: true);
        var caio = await UsuarioAsync("Caio Gerente", PerfilAcesso.Gerente, aprova: false);
        var criado = await Liberacoes().PedirAsync(Pedido(paciente.Id, segunda), secretaria.Id);

        // Quem não foi marcado em Acessos: o "sim" dele não é decisão (a conversa segue normal).
        (await Liberacoes().DecidirPorTextoAsync(caio, "sim", "whatsapp")).Should().BeNull();
        (await Liberacoes().DecidirPorTextoAsync(secretaria, $"liberar {criado.Pedido.Id}", "app")).Should().BeNull();

        (await Liberacoes().DecidirPorTextoAsync(ana, "sim", "whatsapp")).Should().Contain("LIBERADO");
        // A segunda gerente respondeu depois: lê quem decidiu, e NADA é marcado de novo.
        (await Liberacoes().DecidirPorTextoAsync(bia, $"liberar {criado.Pedido.Id}", "whatsapp"))
            .Should().Contain("Não achei pedido de liberação aberto");
        (await Liberacoes().DecidirAsync(criado.Pedido.Id, bia, true, null, "app")).Should().Contain("já foi decidido por Ana Gerente");
        (await _db.Agendamentos.AsNoTracking().CountAsync(a => a.PacienteId == paciente.Id)).Should().Be(2);
    }

    [Fact]
    public async Task Com_dois_pedidos_abertos_o_sim_sozinho_pergunta_qual()
    {
        var (paciente, secretaria, segunda) = await CenarioTravadoAsync();
        var outra = await PacienteAsync("Patrícia Lima", telefone: null);
        await ConsultaAsync(outra.Id, Primeira.AddHours(1));
        _db.ChangeTracker.Clear();
        var ana = await UsuarioAsync("Ana Gerente", PerfilAcesso.Gerente, aprova: true);
        var a = await Liberacoes().PedirAsync(Pedido(paciente.Id, segunda), secretaria.Id);
        var b = await Liberacoes().PedirAsync(Pedido(outra.Id, segunda.AddHours(1)), secretaria.Id);

        var resposta = await Liberacoes().DecidirPorTextoAsync(ana, "sim", "whatsapp");

        resposta.Should().Contain("Há 2 pedidos").And.Contain($"nº {a.Pedido.Id}").And.Contain($"nº {b.Pedido.Id}");
        (await _db.LiberacoesDeTrava.AsNoTracking().CountAsync(l => l.Estado == LiberacaoDeTrava.Pendente)).Should().Be(2);

        // Liberada a paciente SEM celular: marca, e não há mensagem a mandar a ela.
        (await Liberacoes().DecidirPorTextoAsync(ana, $"liberar {b.Pedido.Id}", "whatsapp")).Should().Contain("LIBERADO");
        (await Liberacoes().AvisosParaPacienteAsync()).Should().BeEmpty();
    }

    [Fact]
    public async Task Pedido_sem_resposta_expira_avisa_quem_pediu_e_nao_marca()
    {
        var (paciente, secretaria, segunda) = await CenarioTravadoAsync();
        var ana = await UsuarioAsync("Ana Gerente", PerfilAcesso.Gerente, aprova: true);
        var criado = await Liberacoes().PedirAsync(Pedido(paciente.Id, segunda), secretaria.Id);

        _agora = _agora.AddHours(49);
        (await Liberacoes().ExpirarAsync()).Should().Be(1);

        (await _db.LiberacoesDeTrava.AsNoTracking().SingleAsync(l => l.Id == criado.Pedido.Id)).Estado.Should().Be(LiberacaoDeTrava.Expirada);
        (await _db.MensagensEquipe.AsNoTracking().SingleAsync(m => m.Conversa == ChatEquipeRegras.Sistema(secretaria.Id)))
            .Texto.Should().Contain("EXPIROU");
        // O "sim" atrasado não acha mais nada para liberar — e, sem pedido aberto, nem é decisão.
        (await Liberacoes().DecidirPorTextoAsync(ana, "sim", "whatsapp")).Should().BeNull();
        (await Liberacoes().DecidirPorTextoAsync(ana, $"liberar {criado.Pedido.Id}", "whatsapp")).Should().Contain("Não achei");
    }

    [Fact]
    public async Task A_mensagem_tratada_pelo_crm_sai_da_fila_entregue_ou_nao()
    {
        var (paciente, secretaria, segunda) = await CenarioTravadoAsync();
        await UsuarioAsync("Ana Gerente", PerfilAcesso.Gerente, aprova: true, whatsapp: "5522999990001");
        await Liberacoes().PedirAsync(Pedido(paciente.Id, segunda), secretaria.Id);
        var aviso = (await Liberacoes().AvisosParaEquipeAsync()).Single();

        // Fora da janela de 24 h a Meta recusa: o aviso sai da fila assim mesmo, com o motivo guardado.
        await Liberacoes().ConfirmarAvisoAsync(aviso.Id, entregue: false, "rejected: fora da janela");

        (await Liberacoes().AvisosParaEquipeAsync()).Should().BeEmpty();
        var gravado = await _db.AvisosDeLiberacao.AsNoTracking().SingleAsync(a => a.Id == aviso.Id);
        gravado.Entregue.Should().BeFalse();
        gravado.Motivo.Should().Contain("fora da janela");
    }

    // ==================== O que conta como decisão ====================

    [Theory]
    [InlineData("sim", true, null)]
    [InlineData("Sim.", true, null)]
    [InlineData("liberar 12", true, 12)]
    [InlineData("Liberado nº 12", true, 12)]
    [InlineData("pode #7", true, 7)]
    [InlineData("não", false, null)]
    [InlineData("nao", false, null)]
    [InlineData("negar 12", false, 12)]
    public void Palavras_simples_sao_decisao(string texto, bool liberar, int? pedido)
    {
        var lida = LiberacaoDeTravaRegras.Ler(texto);
        lida.Should().NotBeNull();
        lida!.Liberar.Should().Be(liberar);
        lida.PedidoId.Should().Be(pedido);
    }

    [Theory]
    [InlineData("não vou conseguir ir hoje")]
    [InlineData("pode me mandar a agenda de amanhã?")]
    [InlineData("sim, e a agenda de hoje?")]
    [InlineData("minha agenda de hoje")]
    [InlineData("simone silva 24/10")]
    [InlineData("")]
    public void Conversa_nao_e_decisao(string texto)
        => LiberacaoDeTravaRegras.Ler(texto).Should().BeNull("negar ou liberar por causa de uma frase de conversa seria o sistema decidindo pela gerência");

    [Fact]
    public void Com_o_numero_do_pedido_o_resto_da_frase_e_o_motivo()
    {
        var lida = LiberacaoDeTravaRegras.Ler("negar 12 - já consultou semana passada");
        lida.Should().Be(new DecisaoPorTexto(false, 12, "já consultou semana passada"));
    }
}
