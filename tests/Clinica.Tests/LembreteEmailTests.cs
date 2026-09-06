using Clinica.Application.Email;
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
/// O lembrete automático da sessão por e-mail (set/2026 — o item 6 da agenda).
///
/// O enviador é FALSO de propósito: o que se prova aqui é quem recebe, o que fica
/// registrado e o que NÃO acontece — sem servidor configurado, para quem já foi avisado,
/// para quem não tem e-mail, para a sessão que já passou. O SMTP de verdade se prova pelo
/// botão "Enviar e-mail de teste", na clínica.
/// </summary>
public class LembreteEmailTests : IDisposable
{
    // Quinta-feira, 10/09/2026, 8h.
    private static readonly DateTime Agora = new(2026, 9, 10, 8, 0, 0);
    private static readonly DateOnly Hoje = DateOnly.FromDateTime(Agora);
    private static readonly DateOnly Amanha = Hoje.AddDays(1);

    private readonly SqliteConnection _conn;
    private readonly ClinicaDbContext _db;
    private readonly ClinicaRepositorio _repo;
    private readonly CampanhaService _campanhas;
    private readonly ParametrosService _parametros;
    private readonly EnviadorFalso _enviador = new();

    public LembreteEmailTests()
    {
        _conn = new SqliteConnection("DataSource=:memory:");
        _conn.Open();
        var options = new DbContextOptionsBuilder<ClinicaDbContext>().UseSqlite(_conn).Options;
        _db = new ClinicaDbContext(options);
        _db.Database.EnsureCreated();
        _repo = new ClinicaRepositorio(_db);
        _campanhas = new CampanhaService(_repo);
        _parametros = new ParametrosService(_repo);
    }

    public void Dispose()
    {
        _db.Dispose();
        _conn.Dispose();
        GC.SuppressFinalize(this);
    }

    private LembreteEmailService Servico(DateTime? agora = null)
        => new(_repo, _campanhas, _parametros, _enviador, () => agora ?? Agora);

    private Task LigarSmtpAsync()
        => _parametros.SalvarCamposEmailAsync(new CamposEmail(
            "smtp.teste.com.br", "587", "clinica", "segredo", "contato@clinica.com.br", "Clínica Teste", true));

    private async Task<Paciente> PacienteAsync(string nome, string? email)
    {
        var p = new Paciente { Nome = nome, Email = email, Telefone = "11999990000", Convenio = Convenio.UnimedIntercambio, Sexo = Sexo.Feminino };
        _db.Pacientes.Add(p);
        await _db.SaveChangesAsync();
        return p;
    }

    private async Task<Agendamento> SessaoAsync(int pacienteId, DateTime quando, StatusAgendamento status = StatusAgendamento.Agendado)
    {
        var ag = new Agendamento
        {
            PacienteId = pacienteId, DataHora = quando, DuracaoMinutos = 30, Status = status,
            ModalidadePrevista = ModalidadeAtendimento.Consulta, ModalidadeCodigo = "Consulta"
        };
        _db.Agendamentos.Add(ag);
        await _db.SaveChangesAsync();
        return ag;
    }

    private Task<ContatoCampanha?> ContatoDeAsync(int agendamentoId)
        => _db.Contatos.AsNoTracking().FirstOrDefaultAsync(c => c.AgendamentoId == agendamentoId);

    // ==================== O serviço ====================

    [Fact]
    public async Task Sem_servidor_configurado_nao_gera_contato_nem_envia_nada()
    {
        var maria = await PacienteAsync("Maria Souza", "maria@teste.com.br");
        await SessaoAsync(maria.Id, Amanha.ToDateTime(new TimeOnly(14, 0)));

        var r = await Servico().EnviarConfirmacoesAsync(Amanha);

        r.Desligado.Should().BeTrue();
        r.Descricao.Should().Contain("desligado").And.Contain("Configurações");
        _enviador.Enviados.Should().BeEmpty();
        // A rodada do balcão continua sendo gerada por quem clica — nada nasce sozinho.
        (await _db.Contatos.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Envia_so_a_quem_tem_email_valido_e_registra_o_canal_no_contato()
    {
        await LigarSmtpAsync();
        var maria = await PacienteAsync("Maria Souza", "maria@teste.com.br");
        var joao = await PacienteAsync("João Lima", null);
        var ana = await PacienteAsync("Ana Prado", "ana@");   // inválido: conta como sem e-mail
        var sessaoMaria = await SessaoAsync(maria.Id, Amanha.ToDateTime(new TimeOnly(14, 0)));
        await SessaoAsync(joao.Id, Amanha.ToDateTime(new TimeOnly(15, 0)));
        await SessaoAsync(ana.Id, Amanha.ToDateTime(new TimeOnly(16, 0)));

        var r = await Servico().EnviarConfirmacoesAsync(Amanha);

        r.Enviados.Should().Be(1);
        r.SemEmail.Should().Be(2);
        r.Falhas.Should().Be(0);
        r.Descricao.Should().Contain("1 e-mail(s) enviado(s)").And.Contain("2 sem e-mail");

        var envio = _enviador.Enviados.Should().ContainSingle().Subject;
        envio.Destinatario.Should().Be("maria@teste.com.br");
        envio.Assunto.Should().Contain("amanhã às 14:00").And.Contain("Clínica Teste");
        envio.Corpo.Should().Contain("Olá, Maria!").And.Contain("amanhã às 14:00").And.NotContain("Consulta");

        var contato = await ContatoDeAsync(sessaoMaria.Id);
        contato.Should().NotBeNull();
        contato!.Status.Should().Be(StatusContato.Enviado);
        contato.Canal.Should().Be(CanalContato.Email);
        contato.EnviadoPor.Should().Be(LembreteEmailService.OperadorAutomatico);
        contato.EnviadoEm.Should().NotBeNull();

        // Os outros dois ficam PENDENTES na rodada do balcão — o WhatsApp de um clique
        // continua sendo o caminho deles.
        (await _db.Contatos.CountAsync(c => c.Status == StatusContato.Pendente)).Should().Be(2);
    }

    [Fact]
    public async Task Rodar_de_novo_nao_reenvia_a_quem_ja_recebeu()
    {
        await LigarSmtpAsync();
        var maria = await PacienteAsync("Maria Souza", "maria@teste.com.br");
        await SessaoAsync(maria.Id, Amanha.ToDateTime(new TimeOnly(14, 0)));

        await Servico().EnviarConfirmacoesAsync(Amanha);
        var segunda = await Servico().EnviarConfirmacoesAsync(Amanha);

        segunda.Enviados.Should().Be(0);
        segunda.JaTratados.Should().Be(1);
        _enviador.Enviados.Should().HaveCount(1);
    }

    [Fact]
    public async Task Quem_ja_foi_avisado_pelo_WhatsApp_nao_recebe_email()
    {
        await LigarSmtpAsync();
        var maria = await PacienteAsync("Maria Souza", "maria@teste.com.br");
        var sessao = await SessaoAsync(maria.Id, Amanha.ToDateTime(new TimeOnly(14, 0)));

        await _campanhas.GerarConfirmacoesAsync(Amanha);
        var contato = await ContatoDeAsync(sessao.Id);
        await _campanhas.RegistrarEnvioAsync(contato!.Id, "ana");   // WhatsApp, pela recepção

        var r = await Servico().EnviarConfirmacoesAsync(Amanha);

        r.Enviados.Should().Be(0);
        r.JaTratados.Should().Be(1);
        _enviador.Enviados.Should().BeEmpty();
        // E o canal continua sendo o do aviso que aconteceu de verdade.
        (await ContatoDeAsync(sessao.Id))!.Canal.Should().Be(CanalContato.WhatsApp);
    }

    [Fact]
    public async Task Falha_do_servidor_conta_e_deixa_o_contato_pendente_para_a_proxima_tentativa()
    {
        await LigarSmtpAsync();
        var maria = await PacienteAsync("Maria Souza", "maria@teste.com.br");
        var sessao = await SessaoAsync(maria.Id, Amanha.ToDateTime(new TimeOnly(14, 0)));

        _enviador.FalhaPara = "maria@teste.com.br";
        var r = await Servico().EnviarConfirmacoesAsync(Amanha);

        r.Falhas.Should().Be(1);
        r.Enviados.Should().Be(0);
        r.TeveFalha.Should().BeTrue();
        r.Descricao.Should().Contain("FALHA");
        (await ContatoDeAsync(sessao.Id))!.Status.Should().Be(StatusContato.Pendente);

        // O servidor voltou: a próxima abertura manda.
        _enviador.FalhaPara = null;
        var depois = await Servico().EnviarConfirmacoesAsync(Amanha);
        depois.Enviados.Should().Be(1);
        (await ContatoDeAsync(sessao.Id))!.Status.Should().Be(StatusContato.Enviado);
    }

    [Fact]
    public async Task Sessao_de_hoje_que_ja_passou_nao_recebe_e_a_que_ainda_vem_diz_hoje()
    {
        await LigarSmtpAsync();
        var maria = await PacienteAsync("Maria Souza", "maria@teste.com.br");
        var joao = await PacienteAsync("João Lima", "joao@teste.com.br");
        await SessaoAsync(maria.Id, Hoje.ToDateTime(new TimeOnly(9, 0)));    // já passou às 15h
        await SessaoAsync(joao.Id, Hoje.ToDateTime(new TimeOnly(16, 0)));    // ainda vem

        var r = await Servico(Hoje.ToDateTime(new TimeOnly(15, 0))).EnviarConfirmacoesAsync(Hoje);

        r.Enviados.Should().Be(1);
        r.ForaDaAgenda.Should().Be(1);
        _enviador.Enviados.Single().Corpo.Should().Contain("hoje às 16:00");
    }

    [Fact]
    public async Task Sessao_cancelada_nao_entra_e_o_resultado_diz_que_nao_ha_o_que_lembrar()
    {
        await LigarSmtpAsync();
        var maria = await PacienteAsync("Maria Souza", "maria@teste.com.br");
        await SessaoAsync(maria.Id, Amanha.ToDateTime(new TimeOnly(14, 0)), StatusAgendamento.Cancelado);

        var r = await Servico().EnviarConfirmacoesAsync(Amanha);

        r.Enviados.Should().Be(0);
        r.Descricao.Should().Be("Nenhuma sessão a lembrar em 11/09.");
        _enviador.Enviados.Should().BeEmpty();
    }

    [Fact]
    public async Task A_abertura_cobre_hoje_amanha_e_o_fim_de_semana_ate_a_segunda()
    {
        // Sexta 11/09 → sáb 12, dom 13, seg 14. Quinta → só quinta e sexta.
        LembreteEmailService.DiasDaAbertura(new DateOnly(2026, 9, 11)).Should().Equal(
            new DateOnly(2026, 9, 11), new DateOnly(2026, 9, 12), new DateOnly(2026, 9, 13), new DateOnly(2026, 9, 14));
        LembreteEmailService.DiasDaAbertura(new DateOnly(2026, 9, 10)).Should().Equal(
            new DateOnly(2026, 9, 10), new DateOnly(2026, 9, 11));
        LembreteEmailService.DiasDaAbertura(new DateOnly(2026, 9, 12)).Should().Equal(
            new DateOnly(2026, 9, 12), new DateOnly(2026, 9, 13), new DateOnly(2026, 9, 14));

        await LigarSmtpAsync();
        var maria = await PacienteAsync("Maria Souza", "maria@teste.com.br");
        var sexta = new DateTime(2026, 9, 11, 8, 0, 0);
        await SessaoAsync(maria.Id, new DateTime(2026, 9, 14, 10, 0, 0));   // segunda

        var resultados = await Servico(sexta).EnviarLembretesDaAberturaAsync();

        resultados.Should().HaveCount(4);
        resultados.Sum(r => r.Enviados).Should().Be(1);
        _enviador.Enviados.Single().Corpo.Should().Contain("segunda-feira, 14/09 às 10:00");
    }

    [Fact]
    public async Task Desligado_a_abertura_para_na_primeira_leitura()
    {
        var resultados = await Servico().EnviarLembretesDaAberturaAsync();
        resultados.Should().ContainSingle().Which.Desligado.Should().BeTrue();
    }

    [Fact]
    public async Task O_teste_de_envio_exige_servidor_e_destino_e_nao_toca_em_paciente_nenhum()
    {
        await Servico().Invoking(s => s.EnviarTesteAsync("direcao@clinica.com.br"))
            .Should().ThrowAsync<InvalidOperationException>().WithMessage("*desligado*");

        await LigarSmtpAsync();
        await Servico().Invoking(s => s.EnviarTesteAsync("direcao@"))
            .Should().ThrowAsync<InvalidOperationException>().WithMessage("*destino válido*");

        await Servico().EnviarTesteAsync("direcao@clinica.com.br");

        var envio = _enviador.Enviados.Should().ContainSingle().Subject;
        envio.Destinatario.Should().Be("direcao@clinica.com.br");
        envio.Assunto.Should().Contain("Teste");
        (await _db.Contatos.CountAsync()).Should().Be(0);
    }

    // ==================== As opções ====================

    [Fact]
    public void Meia_configuracao_e_configuracao_nenhuma()
    {
        OpcoesEmail.De("smtp.x.com", "587", null, null, null, null, true).Should().BeNull("sem remetente");
        OpcoesEmail.De(null, "587", null, null, "a@b.com.br", null, true).Should().BeNull("sem servidor");
        OpcoesEmail.De("smtp.x.com", "587", null, null, "remetente-sem-arroba", null, true).Should().BeNull("remetente inválido");

        var ok = OpcoesEmail.De(" smtp.x.com ", "abc", " u ", "s ", " a@b.com.br ", "  ", false)!;
        ok.Host.Should().Be("smtp.x.com");
        ok.Porta.Should().Be(OpcoesEmail.PortaPadrao, "porta ilegível cai no padrão");
        ok.Usuario.Should().Be("u");
        ok.Senha.Should().Be("s ", "espaço no fim de senha é parte dela");
        ok.NomeRemetente.Should().BeNull();
        ok.UsarTls.Should().BeFalse();
        ok.Descricao.Should().Be("a@b.com.br por smtp.x.com:587 (sem TLS)");

        OpcoesEmail.PortaValida("465").Should().Be(465);
        OpcoesEmail.PortaValida("70000").Should().Be(OpcoesEmail.PortaPadrao);
    }

    [Fact]
    public async Task Os_campos_gravados_voltam_como_foram_escritos_e_TLS_ausente_e_ligado()
    {
        (await _parametros.ObterCamposEmailAsync()).UsarTls.Should().BeTrue("ninguém decidiu: vale o padrão seguro");
        (await _parametros.ObterOpcoesEmailAsync()).Should().BeNull();

        await _parametros.SalvarCamposEmailAsync(new CamposEmail(
            "smtp.x.com", "465", "u", "senha com espaço ", "a@b.com.br", "Clínica", false));

        var c = await _parametros.ObterCamposEmailAsync();
        c.Senha.Should().Be("senha com espaço ");
        c.UsarTls.Should().BeFalse();
        (await _parametros.ObterOpcoesEmailAsync())!.Porta.Should().Be(465);
    }

    // ==================== O texto ====================

    [Fact]
    public void A_frase_diz_hoje_amanha_ou_o_dia_da_semana_em_portugues()
    {
        var hoje = new DateOnly(2026, 9, 10);
        MensagensDeContato.QuandoPorExtenso(new DateTime(2026, 9, 10, 14, 0, 0), hoje).Should().Be("hoje às 14:00");
        MensagensDeContato.QuandoPorExtenso(new DateTime(2026, 9, 11, 14, 0, 0), hoje).Should().Be("amanhã às 14:00");
        MensagensDeContato.QuandoPorExtenso(new DateTime(2026, 9, 14, 9, 30, 0), hoje).Should().Be("segunda-feira, 14/09 às 09:30");

        MensagensDeContato.PrimeiroNome("Maria da Silva").Should().Be("Maria");
        MensagensDeContato.ConfirmacaoDeSessao("Maria da Silva", new DateTime(2026, 9, 11, 14, 0, 0), hoje, "Clínica SemDor")
            .Should().Be("Olá, Maria! Estamos confirmando sua sessão amanhã às 14:00. Se tiver algum imprevisto, é só responder por aqui. — Clínica SemDor");
        MensagensDeContato.AssuntoConfirmacao(new DateTime(2026, 9, 11, 14, 0, 0), hoje)
            .Should().Be("Confirmação da sua sessão amanhã às 14:00");
        MensagensDeContato.ConviteDeRetorno("João Lima", new DateOnly(2026, 9, 20), "Dra. Ana")
            .Should().Contain("João!").And.Contain("Dra. Ana").And.Contain("20/09");
    }

    [Fact]
    public void Endereco_de_email_valida_e_normaliza()
    {
        EnderecoDeEmail.Valido("maria@teste.com.br").Should().BeTrue();
        EnderecoDeEmail.Valido(" maria@teste.com.br ").Should().BeTrue();
        EnderecoDeEmail.Valido("maria@").Should().BeFalse();
        EnderecoDeEmail.Valido("maria").Should().BeFalse();
        EnderecoDeEmail.Valido("maria@localhost").Should().BeFalse("sem domínio nenhum servidor entrega");
        EnderecoDeEmail.Valido("").Should().BeFalse();
        EnderecoDeEmail.Valido(null).Should().BeFalse();
        EnderecoDeEmail.Normalizar("  ").Should().BeNull();
        EnderecoDeEmail.SeValido(" maria@teste.com.br ").Should().Be("maria@teste.com.br");
    }

    /// <summary>Guarda o que enviaria; falha para um destino quando mandado.</summary>
    private sealed class EnviadorFalso : IEnviadorDeEmail
    {
        public List<(string Destinatario, string Assunto, string Corpo)> Enviados { get; } = [];
        public string? FalhaPara { get; set; }

        public Task EnviarAsync(OpcoesEmail opcoes, string destinatario, string assunto, string corpo, CancellationToken ct = default)
        {
            if (destinatario == FalhaPara) throw new InvalidOperationException("servidor recusou");
            Enviados.Add((destinatario, assunto, corpo));
            return Task.CompletedTask;
        }
    }
}
