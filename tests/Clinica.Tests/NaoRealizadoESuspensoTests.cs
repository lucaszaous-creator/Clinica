using Clinica.Application.Assinatura;
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
/// ITEM NÃO REALIZADO e ITEM SUSPENSO — o relato da clínica de 20/08/2026: "a enfermagem
/// marca não realizou ou suspenso e o sistema não pede a assinatura".
///
/// Os testes da 2ª assinatura só exercitavam a folha TODA realizada, e é sempre o caminho
/// não exercitado que a clínica encontra. Estes cinco fixam que <b>o destino do item não
/// muda o ciclo de vida da folha</b>: realizado, não realizado, suspenso, nada realizado e
/// tudo suspenso — em todos, a folha encerrada continua pedindo a assinatura de quem
/// executou.
/// </summary>
public class NaoRealizadoESuspensoTests : IDisposable
{
    private readonly SqliteConnection _conn;
    private readonly ClinicaDbContext _db;
    private readonly ClinicaRepositorio _repo;
    private readonly PrescricaoInternaService _prescricoes;
    private readonly ChecagemPrescricaoService _checagens;

    private static readonly IdentificacaoExecutante Tecnica =
        new(null, "Joana Técnica", "COREN-SP 999999");

    public NaoRealizadoESuspensoTests()
    {
        _conn = new SqliteConnection("DataSource=:memory:");
        _conn.Open();
        _db = new ClinicaDbContext(
            new DbContextOptionsBuilder<ClinicaDbContext>().UseSqlite(_conn).Options);
        _db.Database.EnsureCreated();
        _repo = new ClinicaRepositorio(_db);
        var conferencia = new PrescricaoService(_repo);
        _prescricoes = new PrescricaoInternaService(_repo, conferencia);
        _checagens = new ChecagemPrescricaoService(_repo, () => DateTime.Today.AddHours(12));
    }

    private async Task<PrescricaoInterna> PreparadaAsync(int itens = 2)
    {
        var paciente = new Paciente { Nome = "Maria", Convenio = Convenio.UnimedIntercambio };
        var medica = new Profissional { Nome = "Dra. Ana", RegistroConselho = "CRM 1", Cpf = "12345678909" };
        _db.Pacientes.Add(paciente); _db.Profissionais.Add(medica);
        await _db.SaveChangesAsync();

        var p = await _prescricoes.CriarAsync(paciente.Id, medica.Id);
        await _prescricoes.SalvarRascunhoAsync(p.Id, "Crise álgica", null,
            Enumerable.Range(1, itens)
                .Select(i => new ItemPrescricaoInterna { Descricao = $"Item {i}", Dose = "1 g" })
                .ToList(),
            exigeAssinaturaEletronicaDaExecucao: true);

        // Assina "à mão" no domínio, sem passar pelo motor de assinatura (o foco aqui é o
        // ciclo de vida da folha, não o PKCS#7).
        var carregada = (await _repo.ObterPrescricaoInternaAsync(p.Id))!;
        carregada.Situacao = SituacaoPrescricao.Assinada;
        carregada.Assinaturas.Add(new AssinaturaDocumento
        {
            Papel = PapelAssinatura.Prescritor,
            NomeAssinante = "Dra. Ana",
            CpfAssinante = "12345678909",
            AssinadoEm = DateTime.Now
        });
        await _repo.SalvarAsync();
        return carregada;
    }

    [Fact]
    public async Task Tudo_realizado_pede_a_assinatura()
    {
        var p = await PreparadaAsync();
        foreach (var item in (await _repo.ObterPrescricaoInternaAsync(p.Id))!.Itens)
            await _checagens.ChecarAsync(item.Id, SituacaoChecagem.Realizado, new TimeOnly(9, 30), Tecnica);

        await _checagens.EncerrarAsync(p.Id, Tecnica);

        (await _repo.ObterPrescricaoInternaAsync(p.Id))!
            .AguardaAssinaturaDaExecucao.Should().BeTrue();
    }

    [Fact]
    public async Task Item_nao_realizado_tambem_pede_a_assinatura()
    {
        var p = await PreparadaAsync();
        var itens = (await _repo.ObterPrescricaoInternaAsync(p.Id))!.Itens.ToList();

        await _checagens.ChecarAsync(itens[0].Id, SituacaoChecagem.Realizado, new TimeOnly(9, 30), Tecnica);
        await _checagens.ChecarAsync(itens[1].Id, SituacaoChecagem.NaoRealizado, new TimeOnly(9, 40), Tecnica,
            justificativa: "Paciente recusou.");

        await _checagens.EncerrarAsync(p.Id, Tecnica);

        (await _repo.ObterPrescricaoInternaAsync(p.Id))!
            .AguardaAssinaturaDaExecucao.Should().BeTrue();
    }

    [Fact]
    public async Task Item_suspenso_tambem_pede_a_assinatura()
    {
        var p = await PreparadaAsync();
        var itens = (await _repo.ObterPrescricaoInternaAsync(p.Id))!.Itens.ToList();

        await _checagens.ChecarAsync(itens[0].Id, SituacaoChecagem.Realizado, new TimeOnly(9, 30), Tecnica);
        await _prescricoes.SuspenderItemAsync(itens[1].Id, "Contraindicado", "Dra. Ana");

        await _checagens.EncerrarAsync(p.Id, Tecnica);

        (await _repo.ObterPrescricaoInternaAsync(p.Id))!
            .AguardaAssinaturaDaExecucao.Should().BeTrue();
    }

    [Fact]
    public async Task Nada_realizado_tambem_pede_a_assinatura()
    {
        var p = await PreparadaAsync();
        foreach (var item in (await _repo.ObterPrescricaoInternaAsync(p.Id))!.Itens)
            await _checagens.ChecarAsync(item.Id, SituacaoChecagem.NaoRealizado, new TimeOnly(9, 40), Tecnica,
                justificativa: "Paciente passou mal.");

        await _checagens.EncerrarAsync(p.Id, Tecnica);

        (await _repo.ObterPrescricaoInternaAsync(p.Id))!
            .AguardaAssinaturaDaExecucao.Should().BeTrue();
    }

    [Fact]
    public async Task Tudo_suspenso_tambem_pede_a_assinatura()
    {
        var p = await PreparadaAsync();
        foreach (var item in (await _repo.ObterPrescricaoInternaAsync(p.Id))!.Itens.ToList())
            await _prescricoes.SuspenderItemAsync(item.Id, "Contraindicado", "Dra. Ana");

        await _checagens.EncerrarAsync(p.Id, Tecnica);

        (await _repo.ObterPrescricaoInternaAsync(p.Id))!
            .AguardaAssinaturaDaExecucao.Should().BeTrue();
    }

    [Fact]
    public async Task Gera_os_PDFs_com_nao_realizado_e_suspenso()
    {
        var pasta = Environment.GetEnvironmentVariable("CLINICA_DUMP_PDF");
        if (string.IsNullOrWhiteSpace(pasta)) return;

        var p = await PreparadaAsync(itens: 3);
        var itens = (await _repo.ObterPrescricaoInternaAsync(p.Id))!.Itens.OrderBy(i => i.Ordem).ToList();

        await _checagens.ChecarAsync(itens[0].Id, SituacaoChecagem.Realizado, new TimeOnly(9, 30), Tecnica);
        await _checagens.ChecarAsync(itens[1].Id, SituacaoChecagem.NaoRealizado, new TimeOnly(9, 40), Tecnica,
            justificativa: "Paciente apresentou reação alérgica e recusou.");
        await _prescricoes.SuspenderItemAsync(itens[2].Id, "Contraindicado — PA 90x60", "Dra. Ana");

        await _checagens.EncerrarAsync(p.Id, Tecnica);

        var conferencia = new PrescricaoService(_repo);
        var pdfs = new PrescricaoInternaPdfService(_repo, conferencia);

        File.WriteAllBytes(Path.Combine(pasta, "nr-prescricao.pdf"),
            await pdfs.GerarPrescricaoAsync(p.Id));
        File.WriteAllBytes(Path.Combine(pasta, "nr-registro.pdf"),
            await pdfs.GerarRegistroExecucaoAsync(p.Id));
    }

    public void Dispose() { _db.Dispose(); _conn.Dispose(); GC.SuppressFinalize(this); }
}
