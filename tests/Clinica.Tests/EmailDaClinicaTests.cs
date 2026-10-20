using Clinica.Application.Email;
using Clinica.Application.Modelos;
using Clinica.Application.Servicos;
using Clinica.Domain;
using Clinica.Infrastructure;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Clinica.Tests;

/// <summary>
/// O e-mail da clínica (out/2026: só para o documento ao paciente — o lembrete de sessão por
/// e-mail saiu). Fica aqui o que sobrou: o teste do servidor, as opções e as frases.
///
/// O enviador é FALSO de propósito: o SMTP de verdade se prova pelo botão "Enviar e-mail de
/// teste", na clínica.
/// </summary>
public class EmailDaClinicaTests : IDisposable
{
    private readonly SqliteConnection _conn;
    private readonly ClinicaDbContext _db;
    private readonly ParametrosService _parametros;
    private readonly EnviadorFalso _enviador = new();

    public EmailDaClinicaTests()
    {
        _conn = new SqliteConnection("DataSource=:memory:");
        _conn.Open();
        var options = new DbContextOptionsBuilder<ClinicaDbContext>().UseSqlite(_conn).Options;
        _db = new ClinicaDbContext(options);
        _db.Database.EnsureCreated();
        _parametros = new ParametrosService(new ClinicaRepositorio(_db),
            new ProtecaoSegredoGlobal(Convert.ToBase64String(new byte[32])));
    }

    public void Dispose()
    {
        _db.Dispose();
        _conn.Dispose();
        GC.SuppressFinalize(this);
    }

    private EmailDaClinicaService Servico() => new(_parametros, _enviador);

    private Task LigarSmtpAsync()
        => _parametros.SalvarCamposEmailAsync(new CamposEmail(
            "smtp.teste.com.br", "587", "clinica", "segredo", "contato@clinica.com.br", "Clínica Teste", true));

    [Fact]
    public async Task O_teste_de_envio_exige_servidor_e_destino_e_nao_toca_em_paciente_nenhum()
    {
        await Servico().Invoking(s => s.EnviarTesteAsync("direcao@clinica.com.br"))
            .Should().ThrowAsync<InvalidOperationException>().WithMessage("*não está configurado*");

        await LigarSmtpAsync();
        await Servico().Invoking(s => s.EnviarTesteAsync("direcao@"))
            .Should().ThrowAsync<InvalidOperationException>().WithMessage("*destino válido*");

        await Servico().EnviarTesteAsync("direcao@clinica.com.br");

        var envio = _enviador.Enviadas.Should().ContainSingle().Subject;
        envio.Destinatario.Should().Be("direcao@clinica.com.br");
        envio.Mensagem.Assunto.Should().Contain("Teste");
        envio.Mensagem.Html.Should().NotBeNull("o teste mostra a aparência do documento ao paciente");
        envio.Mensagem.Texto.Should().Contain("Nenhum paciente recebeu nada");
        (await _db.Contatos.CountAsync()).Should().Be(0);
    }

    [Fact]
    public void O_nome_que_assina_cai_do_fantasia_para_a_razao_e_por_ultimo_o_remetente()
    {
        EmailDaClinicaService.NomeDaClinica("SemDor", "SemDor Ltda", "Contato").Should().Be("SemDor");
        EmailDaClinicaService.NomeDaClinica("  ", "SemDor Ltda", "Contato").Should().Be("SemDor Ltda");
        EmailDaClinicaService.NomeDaClinica(null, null, "Contato").Should().Be("Contato");
        EmailDaClinicaService.NomeDaClinica(null, null).Should().BeNull();
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

    // ==================== O texto (WhatsApp e SMS continuam usando) ====================

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

    /// <summary>Guarda a mensagem inteira (texto e HTML) que enviaria.</summary>
    private sealed class EnviadorFalso : IEnviadorDeEmail
    {
        public List<(string Destinatario, MensagemDeEmail Mensagem)> Enviadas { get; } = [];

        public Task EnviarAsync(OpcoesEmail opcoes, string destinatario, string assunto, string corpo, CancellationToken ct = default)
            => throw new InvalidOperationException("O teste do servidor vai no modelo do documento, com HTML.");

        public Task EnviarMensagemAsync(OpcoesEmail opcoes, string destinatario, MensagemDeEmail mensagem, CancellationToken ct = default)
        {
            Enviadas.Add((destinatario, mensagem));
            return Task.CompletedTask;
        }
    }
}
