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
/// Os ARQUIVOS DA FICHA (set/2026): a receita, o laudo, o exame em PDF que pertence à
/// PESSOA e não a uma sessão — a décima raiz clínica. As regras fixadas aqui são as de
/// todo registro clínico da casa: linha + bytes + trilha no MESMO SaveChanges; a lista não
/// traz os bytes; o MESMO teto do anexo de prontuário; não se apaga — cancela-se com motivo,
/// e a linha fica; e a ficha cujo único registro é um arquivo NÃO é removível (a cascata da
/// parcela 60).
/// </summary>
public class AnexoPacienteTests : IDisposable
{
    private readonly SqliteConnection _conn;
    private readonly ClinicaDbContext _db;
    private readonly ClinicaRepositorio _repo;
    private readonly AnexoPacienteService _servico;

    private static readonly DateOnly Dia = new(2026, 8, 3);
    private static readonly byte[] Pdf = [0x25, 0x50, 0x44, 0x46, 0x2D]; // "%PDF-"

    public AnexoPacienteTests()
    {
        _conn = new SqliteConnection("DataSource=:memory:");
        _conn.Open();
        var options = new DbContextOptionsBuilder<ClinicaDbContext>().UseSqlite(_conn).Options;
        _db = new ClinicaDbContext(options);
        _db.Database.EnsureCreated();
        _repo = new ClinicaRepositorio(_db);
        _servico = new AnexoPacienteService(_repo);
    }

    public void Dispose()
    {
        _db.Dispose();
        _conn.Dispose();
    }

    private async Task<int> CriarPacienteAsync(string nome = "Maria")
    {
        var p = new Paciente { Nome = nome, Convenio = Convenio.UnimedIntercambio, Sexo = Sexo.Feminino };
        _db.Pacientes.Add(p);
        await _db.SaveChangesAsync();
        return p.Id;
    }

    [Fact]
    public async Task Anexar_grava_linha_bytes_e_trilha_no_mesmo_ato()
    {
        var pacienteId = await CriarPacienteAsync();

        var anexo = await _servico.AnexarAsync(
            pacienteId, Dia, "Receita do sistema anterior", "receita-1001.pdf", Pdf,
            "application/pdf", "Importado do Smart Clinic", "gerente");

        anexo.Id.Should().BePositive();
        anexo.Tamanho.Should().Be(Pdf.Length);
        anexo.CriadoPor.Should().Be("gerente");
        anexo.Cancelado.Should().BeFalse();
        anexo.Importado.Should().BeFalse("pela tela não há chave de importação");

        (await _db.ArquivosAnexoPaciente.AsNoTracking()
            .Where(a => a.AnexoPacienteId == anexo.Id).Select(a => a.Conteudo).SingleAsync())
            .Should().Equal(Pdf, "os bytes ficam na tabela 1:1, no mesmo SaveChanges da linha");

        (await _db.Set<EventoAuditoria>().AsNoTracking()
            .AnyAsync(e => e.Acao == "AnexoFichaRegistrado" && e.PacienteId == pacienteId && e.Operador == "gerente"))
            .Should().BeTrue("ação sem a linha de trilha é ação sem trilha");
    }

    [Fact]
    public async Task A_lista_da_ficha_NAO_traz_os_bytes_e_os_bytes_vem_sob_demanda()
    {
        var pacienteId = await CriarPacienteAsync();
        var a = await _servico.AnexarAsync(pacienteId, Dia, "Laudo", "laudo.pdf", Pdf);
        await _servico.AnexarAsync(pacienteId, Dia.AddDays(-30), "Receita antiga", "receita.pdf", Pdf);

        var lista = await _servico.DaFichaAsync(pacienteId);

        lista.Should().HaveCount(2);
        lista[0].Titulo.Should().Be("Laudo", "a lista sai da mais recente para a mais antiga");
        lista.Should().OnlyContain(x => x.Arquivo == null,
            "sem Include: a navegação fica nula, e é isso que mantém a lista leve");

        (await _servico.ConteudoAsync(a.Id)).Should().Equal(Pdf);
        (await _servico.ConteudoAsync(999_999)).Should().BeNull();
    }

    [Fact]
    public async Task Titulo_vazio_arquivo_vazio_teto_e_data_futura_sao_RECUSADOS()
    {
        var pacienteId = await CriarPacienteAsync();

        var semTitulo = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _servico.AnexarAsync(pacienteId, Dia, "   ", "x.pdf", Pdf));
        semTitulo.Message.Should().Contain("título");

        var vazio = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _servico.AnexarAsync(pacienteId, Dia, "Laudo", "x.pdf", []));
        vazio.Message.Should().Contain("vazio");

        var grande = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _servico.AnexarAsync(pacienteId, Dia, "Laudo", "x.pdf",
                new byte[ProntuarioService.TamanhoMaximoAnexo + 1]));
        grande.Message.Should().Contain("limite", "o teto é o MESMO do anexo de prontuário");

        var futura = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _servico.AnexarAsync(pacienteId, DateOnly.FromDateTime(DateTime.Today).AddDays(1), "Laudo", "x.pdf", Pdf));
        futura.Message.Should().Contain("futura");

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _servico.AnexarAsync(999_999, Dia, "Laudo", "x.pdf", Pdf));

        (await _db.AnexosPaciente.CountAsync()).Should().Be(0, "nenhuma recusa deixa linha para trás");
    }

    [Fact]
    public async Task Cancelar_exige_motivo_e_a_linha_FICA_marcada()
    {
        var pacienteId = await CriarPacienteAsync();
        var a = await _servico.AnexarAsync(pacienteId, Dia, "Receita", "receita.pdf", Pdf);

        var semMotivo = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _servico.CancelarAsync(a.Id, "  ", "joana"));
        semMotivo.Message.Should().Contain("motivo");

        await _servico.CancelarAsync(a.Id, "anexado na ficha errada", "joana");

        var linha = await _db.AnexosPaciente.AsNoTracking().SingleAsync(x => x.Id == a.Id);
        linha.Cancelado.Should().BeTrue();
        linha.CanceladoPor.Should().Be("joana");
        linha.MotivoCancelamento.Should().Be("anexado na ficha errada");
        (await _db.ArquivosAnexoPaciente.AnyAsync(x => x.AnexoPacienteId == a.Id))
            .Should().BeTrue("registro clínico não se apaga — os bytes ficam pelos 20 anos");

        (await _servico.DaFichaAsync(pacienteId)).Should().BeEmpty("a lista vigente esconde o cancelado");
        (await _servico.DaFichaAsync(pacienteId, incluirCancelados: true)).Should().HaveCount(1,
            "a guarda e a exportação continuam enxergando-o");

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _servico.CancelarAsync(a.Id, "de novo", "joana"));
        (await _db.Set<EventoAuditoria>().CountAsync(e => e.Acao == "AnexoFichaCancelado")).Should().Be(1);
    }

    /// <summary>
    /// ⚠️ A DÉCIMA raiz clínica. Sem ela em <c>PacienteTemRegistroClinicoAsync</c>, a ficha
    /// cujo único registro é um arquivo continuaria REMOVÍVEL, e a exclusão o levaria por
    /// arrasto (a FK é cascata) — a lição da parcela 60 e da 75, com o teste da lista de
    /// métodos proibidos verde ao lado.
    /// </summary>
    [Fact]
    public async Task Excluir_paciente_que_so_tem_ARQUIVO_DA_FICHA_e_RECUSADO()
    {
        var pacienteId = await CriarPacienteAsync("Só Arquivo");
        await _servico.AnexarAsync(pacienteId, Dia, "Receita", "receita.pdf", Pdf);

        var recusa = await Assert.ThrowsAsync<InvalidOperationException>(
            () => new PacienteService(_repo).RemoverAsync(pacienteId));

        recusa.Message.Should().Contain("20 anos");
        (await _db.AnexosPaciente.AnyAsync(a => a.PacienteId == pacienteId)).Should().BeTrue();
        (await _db.Pacientes.AnyAsync(p => p.Id == pacienteId)).Should().BeTrue();
    }

    [Fact]
    public async Task O_arquivo_da_ficha_entra_na_GUARDA_com_a_data_do_documento()
    {
        // A guarda conta do ÚLTIMO registro de qualquer natureza: um laudo anexado com data
        // posterior à última sessão move o prazo — e a natureza aparece contada.
        var pacienteId = await CriarPacienteAsync();
        await _servico.AnexarAsync(pacienteId, new DateOnly(2026, 8, 20), "Laudo", "laudo.pdf", Pdf);

        var situacao = await new GuardaProntuarioService(_repo).DoPacienteAsync(pacienteId);

        situacao.UltimoRegistro.Should().Be(new DateOnly(2026, 8, 20));
        situacao.VenceEm.Should().Be(new DateOnly(2046, 8, 20));
        situacao.Contagens[Clinica.Domain.Prontuario.NaturezaRegistroClinico.ArquivoDaFicha].Should().Be(1);
    }
}
