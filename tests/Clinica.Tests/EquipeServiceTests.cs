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
/// Cadastro da equipe (parcela 1). O ponto sensível aqui não é o CRUD: é a regra de
/// que quem já tem agenda NÃO pode ser excluído — apagar o profissional apagaria o
/// "quem atendeu" de tudo o que ele já fez.
/// </summary>
public class EquipeServiceTests : IDisposable
{
    private readonly SqliteConnection _conn;
    private readonly ClinicaDbContext _db;
    private readonly ClinicaRepositorio _repo;
    private readonly EquipeService _equipe;
    private readonly AgendaService _agenda;

    public EquipeServiceTests()
    {
        _conn = new SqliteConnection("DataSource=:memory:");
        _conn.Open();
        var options = new DbContextOptionsBuilder<ClinicaDbContext>().UseSqlite(_conn).Options;
        _db = new ClinicaDbContext(options);
        _db.Database.EnsureCreated();
        _repo = new ClinicaRepositorio(_db);
        _equipe = new EquipeService(_repo);
        _agenda = new AgendaService(_repo, new AtendimentoService(_repo));
    }

    // ===== O CPF do profissional (parcela 45) =====
    //
    // A coluna existe desde a parcela 42, com três LEITORES — as duas assinaturas e a
    // entrada por certificado — e, até esta parcela, NENHUM gravador em todo o código. O
    // efeito era que a assinatura ICP-Brasil recusava sempre, dizendo "cadastre o CPF em
    // Equipe", numa tela que não tinha o campo. Estes testes existem para que a escrita
    // nunca mais suma sem ninguém notar.

    [Fact]
    public async Task Cpf_EhGravado()
    {
        var salvo = await _equipe.SalvarProfissionalAsync(
            new Profissional { Nome = "Dra. Ana", Cpf = "123.456.789-09" });

        var lido = await _equipe.ObterProfissionalAsync(salvo.Id);

        lido!.Cpf.Should().Be("12345678909");   // só dígitos, como a comparação com o e-CPF espera
    }

    [Fact]
    public async Task Cpf_EmBranco_EhOCasoNormal()
    {
        // Quem não assina digitalmente não precisa de CPF; exigi-lo travaria o cadastro da
        // equipe inteira por causa de uma feature que nem toda clínica usa.
        var salvo = await _equipe.SalvarProfissionalAsync(new Profissional { Nome = "Recepção" });

        salvo.Cpf.Should().BeNull();
    }

    [Fact]
    public async Task Cpf_Invalido_EhRecusado()
    {
        // Aceitar "111" faria a médica descobrir o erro no dia de assinar, com o paciente
        // na sala e a mensagem dizendo que o certificado é de outra pessoa.
        var erro = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _equipe.SalvarProfissionalAsync(
                new Profissional { Nome = "Dra. Ana", Cpf = "11111111111" }));

        erro.Message.Should().Contain("não é válido");
    }

    [Fact]
    public async Task Cpf_Repetido_EhRecusado()
    {
        // A junção com o certificado é por VALOR, não por chave estrangeira: dois
        // profissionais com o mesmo CPF tornam ambígua a resposta a "quem assinou?".
        await _equipe.SalvarProfissionalAsync(
            new Profissional { Nome = "Dra. Ana", Cpf = "12345678909" });

        var erro = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _equipe.SalvarProfissionalAsync(
                new Profissional { Nome = "Outra pessoa", Cpf = "12345678909" }));

        erro.Message.Should().Contain("Dra. Ana");
    }

    [Fact]
    public async Task Cpf_DoProprioProfissional_NaoContaComoRepetido()
    {
        var salvo = await _equipe.SalvarProfissionalAsync(
            new Profissional { Nome = "Dra. Ana", Cpf = "12345678909" });

        salvo.Nome = "Dra. Ana Souza";
        var regravado = await _equipe.SalvarProfissionalAsync(salvo);

        regravado.Cpf.Should().Be("12345678909");
    }

    private async Task<int> CriarPacienteAsync()
    {
        var p = new Paciente { Nome = "Paciente", Convenio = Convenio.UnimedIntercambio, Sexo = Sexo.Feminino };
        _db.Pacientes.Add(p);
        await _db.SaveChangesAsync();
        return p.Id;
    }

    [Fact]
    public async Task SalvarProfissional_CriaEDepoisAtualiza()
    {
        var criado = await _equipe.SalvarProfissionalAsync(new Profissional
        {
            Nome = "  Ana Souza  ",
            RegistroConselho = "CRM-SP 123456",
            DuracaoPadraoMinutos = 45
        });

        criado.Id.Should().BeGreaterThan(0);
        criado.Nome.Should().Be("Ana Souza", "o nome é aparado ao salvar");

        await _equipe.SalvarProfissionalAsync(new Profissional
        {
            Id = criado.Id,
            Nome = "Ana Souza",
            NomeCurto = "Dra. Ana",
            DuracaoPadraoMinutos = 60,
            Ativo = true
        });

        var todos = await _equipe.ProfissionaisAsync();
        todos.Should().ContainSingle("editar não pode criar um segundo registro");
        todos[0].NomeCurto.Should().Be("Dra. Ana");
        todos[0].DuracaoPadraoMinutos.Should().Be(60);
    }

    [Fact]
    public async Task SalvarProfissional_SemNome_Falha()
    {
        var acao = () => _equipe.SalvarProfissionalAsync(new Profissional { Nome = "   " });
        await acao.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task SalvarProfissional_DuracaoZero_Falha()
    {
        var acao = () => _equipe.SalvarProfissionalAsync(
            new Profissional { Nome = "Ana", DuracaoPadraoMinutos = 0 });
        await acao.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task ProfissionaisAtivos_NaoTrazemOsDesligados()
    {
        await _equipe.SalvarProfissionalAsync(new Profissional { Nome = "Ana", Ativo = true });
        await _equipe.SalvarProfissionalAsync(new Profissional { Nome = "Bruno", Ativo = false });

        (await _equipe.ProfissionaisAtivosAsync()).Should().ContainSingle()
            .Which.Nome.Should().Be("Ana");
        (await _equipe.ProfissionaisAsync()).Should().HaveCount(2, "o inativo continua no cadastro");
    }

    [Fact]
    public async Task ExcluirProfissional_ComAgenda_Recusa()
    {
        var prof = await _equipe.SalvarProfissionalAsync(new Profissional { Nome = "Ana" });
        var pacienteId = await CriarPacienteAsync();
        await _agenda.AgendarAsync(pacienteId, new DateTime(2026, 8, 3, 9, 0, 0),
            ModalidadeAtendimento.AcupunturaSimples, null, profissionalId: prof.Id);

        var acao = () => _equipe.ExcluirProfissionalAsync(prof.Id);

        await acao.Should().ThrowAsync<InvalidOperationException>(
            "apagar quem já atendeu apagaria o histórico — o caminho é desativar");
    }

    [Fact]
    public async Task ExcluirProfissional_SemUso_Funciona()
    {
        var prof = await _equipe.SalvarProfissionalAsync(new Profissional { Nome = "Ana" });

        await _equipe.ExcluirProfissionalAsync(prof.Id);

        (await _equipe.ProfissionaisAsync()).Should().BeEmpty();
    }

    /// <summary>
    /// A guarda promete "não apagar o histórico" e olhava só agenda e lista de espera.
    /// O histórico de um profissional é bem maior: evolução e documento clínico
    /// (prontuário, cuja guarda é obrigação legal), o repasse já pago a ele, a regra de
    /// repasse acordada, a meta do mês e as férias marcadas.
    ///
    /// Cada caso aqui é uma tabela que, antes, deixava a exclusão passar.
    /// </summary>
    [Theory]
    [InlineData("evolucao")]
    [InlineData("documento")]
    [InlineData("repasse-apurado")]
    [InlineData("regra-repasse")]
    [InlineData("meta")]
    [InlineData("bloqueio")]
    [InlineData("usuario")]
    public async Task ExcluirProfissional_ComHistorico_Recusa(string rastro)
    {
        var prof = await _equipe.SalvarProfissionalAsync(new Profissional { Nome = "Ana" });
        var pacienteId = await CriarPacienteAsync();

        switch (rastro)
        {
            case "evolucao":
                _db.Evolucoes.Add(new Evolucao
                {
                    PacienteId = pacienteId,
                    Data = new DateOnly(2026, 8, 3),
                    ProfissionalId = prof.Id
                });
                break;
            case "documento":
                _db.DocumentosClinicos.Add(new DocumentoClinico
                {
                    PacienteId = pacienteId,
                    Tipo = TipoDocumentoClinico.Receita,
                    Numero = "2026/0001",
                    CodigoVerificacao = "ABC123",
                    Data = new DateOnly(2026, 8, 3),
                    ProfissionalId = prof.Id
                });
                break;
            case "repasse-apurado":
                _db.RepassesApurados.Add(new RepasseApurado
                {
                    ProfissionalId = prof.Id,
                    Inicio = new DateOnly(2026, 8, 1),
                    Fim = new DateOnly(2026, 8, 31),
                    Valor = 1200m
                });
                break;
            case "regra-repasse":
                _db.RegrasRepasse.Add(new RegraRepasse
                {
                    ProfissionalId = prof.Id,
                    Percentual = 50m,
                    VigenteDe = new DateOnly(2026, 1, 1)
                });
                break;
            case "meta":
                _db.Metas.Add(new MetaMensal
                {
                    Ano = 2026,
                    Mes = 8,
                    Indicador = IndicadorMeta.Sessoes,
                    Valor = 80m,
                    ProfissionalId = prof.Id
                });
                break;
            case "bloqueio":
                _db.BloqueiosAgenda.Add(new BloqueioAgenda
                {
                    Inicio = new DateTime(2026, 8, 10, 0, 0, 0),
                    Fim = new DateTime(2026, 8, 20, 23, 59, 0),
                    Motivo = "Férias",
                    ProfissionalId = prof.Id
                });
                break;
            case "usuario":
                _db.Usuarios.Add(new UsuarioSistema
                {
                    Login = "ana",
                    Nome = "Ana",
                    SenhaHash = "x",
                    SenhaSalt = "y",
                    Perfil = PerfilAcesso.Profissional,
                    ProfissionalId = prof.Id
                });
                break;
        }

        await _db.SaveChangesAsync();

        var acao = () => _equipe.ExcluirProfissionalAsync(prof.Id);

        await acao.Should().ThrowAsync<InvalidOperationException>();
    }

    /// <summary>
    /// Reforma marcada na sala é registro de agenda tanto quanto um atendimento —
    /// apagar a sala levaria o bloqueio junto, e a agenda voltaria a aceitar horário
    /// numa sala que está interditada.
    /// </summary>
    [Fact]
    public async Task ExcluirSala_ComBloqueio_Recusa()
    {
        var sala = await _equipe.SalvarSalaAsync(new Sala { Nome = "Sala 1" });
        _db.BloqueiosAgenda.Add(new BloqueioAgenda
        {
            Inicio = new DateTime(2026, 8, 10, 0, 0, 0),
            Fim = new DateTime(2026, 8, 12, 23, 59, 0),
            Motivo = "Reforma",
            SalaId = sala.Id
        });
        await _db.SaveChangesAsync();

        var acao = () => _equipe.ExcluirSalaAsync(sala.Id);

        await acao.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task SalvarSala_NomeRepetido_Falha()
    {
        await _equipe.SalvarSalaAsync(new Sala { Nome = "Consultório 1" });

        var acao = () => _equipe.SalvarSalaAsync(new Sala { Nome = "consultório 1" });

        await acao.Should().ThrowAsync<InvalidOperationException>(
            "duas salas com o mesmo nome tornam a agenda ambígua");
    }

    [Fact]
    public async Task SalvarSala_CapacidadeInvalida_Falha()
    {
        var acao = () => _equipe.SalvarSalaAsync(new Sala { Nome = "Sala", Capacidade = 0 });
        await acao.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task ExcluirSala_ComAgenda_Recusa()
    {
        var sala = await _equipe.SalvarSalaAsync(new Sala { Nome = "Consultório 1" });
        var pacienteId = await CriarPacienteAsync();
        await _agenda.AgendarAsync(pacienteId, new DateTime(2026, 8, 3, 9, 0, 0),
            ModalidadeAtendimento.AcupunturaSimples, null, salaId: sala.Id);

        var acao = () => _equipe.ExcluirSalaAsync(sala.Id);

        await acao.Should().ThrowAsync<InvalidOperationException>();
    }

    public void Dispose()
    {
        _db.Dispose();
        _conn.Dispose();
    }
}
