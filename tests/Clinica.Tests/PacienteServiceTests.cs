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
/// Cadastro do paciente: retrato e consultas. O ponto sensível do retrato é o
/// armazenamento partido — miniatura na linha do paciente (alimenta os avatares) e foto
/// cheia numa tabela à parte, para a busca de pacientes não arrastar imagem.
/// </summary>
public class PacienteServiceTests : IDisposable
{
    private readonly SqliteConnection _conn;
    private readonly ClinicaDbContext _db;
    private readonly PacienteService _pacientes;

    private static readonly byte[] FotoCheia = new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 };
    private static readonly byte[] Miniatura = new byte[] { 9, 9, 9 };

    public PacienteServiceTests()
    {
        _conn = new SqliteConnection("DataSource=:memory:");
        _conn.Open();
        var options = new DbContextOptionsBuilder<ClinicaDbContext>().UseSqlite(_conn).Options;
        _db = new ClinicaDbContext(options);
        _db.Database.EnsureCreated();
        _pacientes = new PacienteService(new ClinicaRepositorio(_db));
    }

    private async Task<Paciente> CriarPacienteAsync(string nome = "Maria da Silva")
    {
        var p = new Paciente { Nome = nome, Convenio = Convenio.UnimedIntercambio, Sexo = Sexo.Feminino };
        _db.Pacientes.Add(p);
        await _db.SaveChangesAsync();
        return p;
    }

    // ================= CPF ÚNICO (parcela 57) =================
    //
    // Duas fichas da mesma pessoa partem o histórico em dois: metade dos atendimentos,
    // das guias e do prontuário em cada uma. Não é erro de digitação, é a mesma pessoa
    // cadastrada de novo por quem não achou a ficha antiga — e por isso a recusa DIZ de
    // quem é o CPF.
    //
    // A recusa mora no serviço, e não num índice único: a migration roda na abertura do
    // app, inclusive do faturamento em produção, e falharia se a base já tivesse duplicata.

    private static Paciente Nova(string nome, string? cpf) => new()
    {
        Nome = nome, Documento = cpf,
        Convenio = Convenio.UnimedIntercambio, Sexo = Sexo.Feminino
    };

    [Fact]
    public async Task SalvarNovo_ComCpfJaCadastrado_Recusa_EDizDeQuemE()
    {
        await _pacientes.SalvarNovoAsync(Nova("Maria da Silva", "529.982.247-25"));

        var erro = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _pacientes.SalvarNovoAsync(Nova("Maria S.", "52998224725")));

        // O nome de quem já tem o CPF é o que transforma o erro em instrução.
        erro.Message.Should().Contain("Maria da Silva");
        erro.Message.Should().Contain("529.982.247-25");
        _db.Pacientes.Count().Should().Be(1);
    }

    /// <summary>
    /// A ficha ANTIGA pode estar com máscara: a coluna aceita 30 caracteres e só passou a
    /// ser normalizada quando o serviço começou a fazê-lo. Comparar o texto cru deixaria
    /// passar justamente o duplicado que já está na base.
    /// </summary>
    [Fact]
    public async Task SalvarNovo_QuandoAFichaAntigaEstaComMascara_AindaAssimRecusa()
    {
        _db.Pacientes.Add(Nova("João Antigo", "529.982.247-25"));   // gravado direto, sem passar pelo serviço
        await _db.SaveChangesAsync();

        var erro = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _pacientes.SalvarNovoAsync(Nova("João A.", "52998224725")));

        erro.Message.Should().Contain("João Antigo");
    }

    [Fact]
    public async Task Atualizar_APropriaFichaNaoContaComoDuplicata()
    {
        var p = await _pacientes.SalvarNovoAsync(Nova("Ana", "529.982.247-25"));

        p.Nome = "Ana Paula";
        var salvar = () => _pacientes.AtualizarAsync(p);

        await salvar.Should().NotThrowAsync();
        (await _db.Pacientes.FindAsync(p.Id))!.Nome.Should().Be("Ana Paula");
    }

    /// <summary>
    /// CPF em branco é o caso NORMAL — criança, paciente cadastrado pela carteirinha, quem
    /// chegou sem documento. Vários pacientes sem CPF não são duplicatas uns dos outros, e
    /// exigir o CPF travaria o cadastro no balcão com o paciente na frente.
    /// </summary>
    [Fact]
    public async Task SemCpf_VariosPacientesConvivem_EODocumentoVaiNuloParaOBanco()
    {
        await _pacientes.SalvarNovoAsync(Nova("Sem documento 1", null));
        await _pacientes.SalvarNovoAsync(Nova("Sem documento 2", "   "));

        _db.Pacientes.Count().Should().Be(2);
        // Vazio vira NULO: dois documentos "" seriam iguais para qualquer comparação.
        _db.Pacientes.Select(p => p.Documento).Should().OnlyContain(d => d == null);
    }

    /// <summary>
    /// Duplicata ANTERIOR à regra também é recusada ao salvar.
    ///
    /// A direção dispensou a exceção que deixava editar essas fichas: as duplicatas que
    /// já existem serão apagadas direto no banco, e daí em diante só precisa existir o
    /// impedimento. O efeito colateral fica REGISTRADO aqui em vez de descoberto no
    /// balcão — enquanto a limpeza não acontece, a ficha duplicada não salva.
    /// </summary>
    [Fact]
    public async Task Atualizar_FichaComCpfDeOutra_ERecusadaAindaQueADuplicataSejaAntiga()
    {
        _db.Pacientes.Add(Nova("Carlos A", "52998224725"));
        _db.Pacientes.Add(Nova("Carlos B", "529.982.247-25"));   // duplicata anterior à regra
        await _db.SaveChangesAsync();

        var b = _db.Pacientes.Single(p => p.Nome == "Carlos B");
        b.Telefone = "(22) 98888-0000";

        var salvar = () => _pacientes.AtualizarAsync(b);

        (await salvar.Should().ThrowAsync<InvalidOperationException>())
            .WithMessage("*Carlos A*");
    }

    /// <summary>
    /// O que continua recusado na edição: a ficha que PASSA A USAR o CPF de outra pessoa.
    /// Esse é o ato de duplicar, feito por edição em vez de por cadastro novo.
    /// </summary>
    [Fact]
    public async Task Atualizar_TrocandoOCpfParaODeOutroPaciente_Recusa()
    {
        await _pacientes.SalvarNovoAsync(Nova("Dona do CPF", "529.982.247-25"));
        var outro = await _pacientes.SalvarNovoAsync(Nova("Outro", null));

        outro.Documento = "52998224725";
        var salvar = () => _pacientes.AtualizarAsync(outro);

        (await salvar.Should().ThrowAsync<InvalidOperationException>())
            .WithMessage("*Dona do CPF*");
    }

    [Fact]
    public async Task SalvarNovo_ComCpfInvalido_Recusa()
    {
        var salvar = () => _pacientes.SalvarNovoAsync(Nova("Fulano", "111.111.111-11"));

        await salvar.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task DefinirFoto_GuardaMiniaturaNaLinhaEFotoCheiaNaTabelaAParte()
    {
        var paciente = await CriarPacienteAsync();

        await _pacientes.DefinirFotoAsync(paciente.Id, FotoCheia, Miniatura);

        var salvo = await _db.Pacientes.AsNoTracking().FirstAsync(p => p.Id == paciente.Id);
        salvo.FotoMiniatura.Should().Equal(Miniatura);
        salvo.FotoAtualizadaEm.Should().NotBeNull();
        salvo.TemFoto.Should().BeTrue();

        (await _pacientes.ObterFotoAsync(paciente.Id)).Should().Equal(FotoCheia);
    }

    [Fact]
    public async Task DefinirFoto_SubstituiORetratoAnteriorSemDuplicarLinha()
    {
        var paciente = await CriarPacienteAsync();
        await _pacientes.DefinirFotoAsync(paciente.Id, FotoCheia, Miniatura);

        var nova = new byte[] { 7, 7 };
        await _pacientes.DefinirFotoAsync(paciente.Id, nova, nova);

        (await _db.PacientesFotos.AsNoTracking().CountAsync()).Should().Be(1);
        (await _pacientes.ObterFotoAsync(paciente.Id)).Should().Equal(nova);
    }

    [Fact]
    public async Task DefinirFoto_RecusaConteudoVazio()
    {
        var paciente = await CriarPacienteAsync();

        var acao = async () => await _pacientes.DefinirFotoAsync(paciente.Id, Array.Empty<byte>(), Miniatura);

        await acao.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task RemoverFoto_LimpaMiniaturaEFotoCheia()
    {
        var paciente = await CriarPacienteAsync();
        await _pacientes.DefinirFotoAsync(paciente.Id, FotoCheia, Miniatura);

        await _pacientes.RemoverFotoAsync(paciente.Id);

        var salvo = await _db.Pacientes.AsNoTracking().FirstAsync(p => p.Id == paciente.Id);
        salvo.FotoMiniatura.Should().BeNull();
        salvo.FotoAtualizadaEm.Should().BeNull();
        salvo.TemFoto.Should().BeFalse();
        (await _pacientes.ObterFotoAsync(paciente.Id)).Should().BeNull();
    }

    [Fact]
    public async Task RemoverPaciente_LevaAFotoJunto()
    {
        var paciente = await CriarPacienteAsync();
        await _pacientes.DefinirFotoAsync(paciente.Id, FotoCheia, Miniatura);

        await _pacientes.RemoverAsync(paciente.Id);

        (await _db.PacientesFotos.AsNoTracking().CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Buscar_NaoTrazAFotoCheiaJuntoDaLista()
    {
        var paciente = await CriarPacienteAsync();
        await _pacientes.DefinirFotoAsync(paciente.Id, FotoCheia, Miniatura);
        _db.ChangeTracker.Clear();

        var lista = await _pacientes.BuscarAsync(null);

        lista.Should().HaveCount(1);
        lista[0].FotoMiniatura.Should().Equal(Miniatura, "o avatar da lista vem da miniatura");
        lista[0].Foto.Should().BeNull("a foto cheia só é carregada sob demanda");
    }

    [Fact]
    public async Task CarteirinhaVencida_ComparaComODiaDeHoje()
    {
        var paciente = await CriarPacienteAsync();
        paciente.ValidadeCarteirinha = DateOnly.FromDateTime(DateTime.Today).AddDays(-1);
        paciente.CarteirinhaVencida.Should().BeTrue();

        paciente.ValidadeCarteirinha = DateOnly.FromDateTime(DateTime.Today);
        paciente.CarteirinhaVencida.Should().BeFalse("vence no fim do dia, não no começo");

        paciente.ValidadeCarteirinha = null;
        paciente.CarteirinhaVencida.Should().BeFalse("sem validade cadastrada não há o que vencer");
    }

    [Fact]
    public async Task Consultas_VemDaMaisRecenteParaAMaisAntiga()
    {
        var paciente = await CriarPacienteAsync();
        _db.Consultas.AddRange(
            new Consulta
            {
                PacienteId = paciente.Id, Convenio = Convenio.UnimedIntercambio,
                DataEmissao = new DateOnly(2026, 3, 1), ValidadeDias = 22,
                DataVencimento = new DateOnly(2026, 3, 23)
            },
            new Consulta
            {
                PacienteId = paciente.Id, Convenio = Convenio.UnimedIntercambio,
                DataEmissao = new DateOnly(2026, 7, 1), ValidadeDias = 22,
                DataVencimento = new DateOnly(2026, 7, 23)
            });
        await _db.SaveChangesAsync();

        var consultas = await _pacientes.ConsultasAsync(paciente.Id);

        consultas.Should().HaveCount(2);
        consultas[0].DataEmissao.Should().Be(new DateOnly(2026, 7, 1));
    }

    // ---- Limite da busca ----
    // O corte tem de sair no SQL: os seletores da UI mostram poucas linhas, e trazer a base
    // inteira pela rede a cada tecla para descartar em memória é o que existia antes.

    [Fact]
    public async Task Buscar_SemLimite_TrazTodos()
    {
        for (var i = 1; i <= 5; i++) await CriarPacienteAsync($"Paciente {i:00}");

        var encontrados = await _pacientes.BuscarAsync(null);

        encontrados.Should().HaveCount(5);
    }

    [Fact]
    public async Task Buscar_ComLimite_CortaOResultado()
    {
        for (var i = 1; i <= 5; i++) await CriarPacienteAsync($"Paciente {i:00}");

        var encontrados = await _pacientes.BuscarAsync(null, limite: 2);

        encontrados.Should().HaveCount(2);
    }

    [Fact]
    public async Task Buscar_ComLimite_MantemAOrdemAlfabetica()
    {
        await CriarPacienteAsync("Zulmira Nunes");
        await CriarPacienteAsync("Ana Paula Souza");
        await CriarPacienteAsync("Beatriz Lima");

        var encontrados = await _pacientes.BuscarAsync(null, limite: 2);

        // O corte é depois da ordenação: quem aparece são os primeiros nomes, não dois quaisquer.
        encontrados.Select(p => p.Nome).Should().Equal("Ana Paula Souza", "Beatriz Lima");
    }

    [Fact]
    public async Task Buscar_ComLimite_AplicaOTermoAntesDeCortar()
    {
        await CriarPacienteAsync("Ana Paula Souza");
        await CriarPacienteAsync("Ana Clara Dias");
        await CriarPacienteAsync("Beatriz Lima");

        var encontrados = await _pacientes.BuscarAsync("ana", limite: 5);

        encontrados.Should().HaveCount(2);
        encontrados.Should().OnlyContain(p => p.Nome.StartsWith("Ana"));
    }

    [Fact]
    public async Task Buscar_LimiteZeroOuNegativo_EhIgnorado()
    {
        await CriarPacienteAsync("Ana Paula Souza");
        await CriarPacienteAsync("Beatriz Lima");

        // Guarda de sanidade: um limite inválido nunca pode esvaziar a tela.
        (await _pacientes.BuscarAsync(null, limite: 0)).Should().HaveCount(2);
        (await _pacientes.BuscarAsync(null, limite: -1)).Should().HaveCount(2);
    }

    public void Dispose()
    {
        _db.Dispose();
        _conn.Dispose();
    }
}
