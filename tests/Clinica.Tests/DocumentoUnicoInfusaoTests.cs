using Clinica.Application.Assinatura;
using Clinica.Domain.Entities;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using PdfSharp.Pdf.Advanced;
using PdfSharp.Pdf.IO;
using Xunit;

namespace Clinica.Tests;

public partial class SegundaAssinaturaExecucaoTests
{
    [Theory]
    [InlineData(SituacaoChecagem.Realizado)]
    [InlineData(SituacaoChecagem.NaoRealizado)]
    [InlineData(SituacaoChecagem.NaoExecutavel)]
    public async Task Documento_unico_preserva_medico_e_inclui_execucao_assinada(SituacaoChecagem situacao)
    {
        var cenario = await CenarioAsync();
        var p = await AssinadaPelaMedicaAsync(cenario, exigir: true);
        var item = (await _repo.ObterPrescricaoInternaAsync(p.Id))!.Itens.First();
        var data = DateOnly.FromDateTime(DateTime.Today.AddDays(-1));
        var checagem = await _checagens.ChecarAsync(item.Id, situacao, new(10, 37), Tecnica,
            justificativa: situacao == SituacaoChecagem.Realizado ? null : "Paciente recusou; médico comunicado.", dataRealizacao: data);
        _db.ChangeTracker.Clear();
        var relida = await _db.ChecagensPrescricao.SingleAsync(x => x.Id == checagem.Id);
        relida.NaoExecutavel.Should().Be(situacao == SituacaoChecagem.NaoExecutavel);
        // Leitores antigos só conhecem Realizado e NaoRealizado: a coluna mantém esse contrato.
        relida.Situacao.Should().Be(situacao == SituacaoChecagem.Realizado
            ? SituacaoChecagem.Realizado : SituacaoChecagem.NaoRealizado);
        await _checagens.EncerrarAsync(p.Id, Tecnica);
        var original = (await _orquestra.FolhaAsync(p.Id, FolhaPrescricao.Prescricao)).Pdf;
        var antes = await _db.ArquivosAssinados.CountAsync();
        await _orquestra.AssinarExecucaoAsync(p.Id, ECpfDeTeste("Joana Técnica", CpfEnfermeira), cenario.UsuarioEnfermeiraId);
        var folha = await _orquestra.DocumentoInfusaoAsync(p.Id);
        folha.Pdf.AsSpan(0, original.Length).SequenceEqual(original).Should().BeTrue();
        _assinador.ConferirTodas(folha.Pdf).Should().HaveCount(2).And.OnlyContain(c => c.Conferida && c.Integra);
        (await _db.ArquivosAssinados.CountAsync()).Should().Be(antes + 1);
        var salvo = (await _repo.ObterPrescricaoInternaAsync(p.Id))!;
        salvo.AssinaturaDaExecucao!.ArquivoRegistroId.Should().Be(salvo.AssinaturaDaExecucao.ArquivoId);
        using var stream = new MemoryStream(folha.Pdf);
        using var pdf = PdfReader.Open(stream, PdfDocumentOpenMode.Import);
        var campos = pdf.Internals.Catalog.Elements.GetDictionary("/AcroForm")!.Elements.GetArray("/Fields")!;
        var valores = campos.Elements.OfType<PdfReference>().Select(r => r.Value).OfType<PdfSharp.Pdf.PdfDictionary>()
            .Where(f => f.Elements.GetName("/FT") == "/Tx").ToDictionary(f => f.Elements.GetString("/T"), f => f.Elements.GetString("/V"));
        valores[$"hora_{item.Id}"].Should().Be("10:37");
        valores[$"situacao_{item.Id}"].Should().Be(situacao switch
        {
            SituacaoChecagem.Realizado => "Sim",
            SituacaoChecagem.NaoRealizado => "Não",
            _ => "NE"
        });
        valores[$"detalhe_{item.Id}"].Should().Contain($"{data:dd/MM/yyyy}").And.Contain("Joana Técnica");
        if (situacao != SituacaoChecagem.Realizado) valores[$"detalhe_{item.Id}"].Should().Contain(checagem.Justificativa);
        if (Environment.GetEnvironmentVariable("CLINICA_DUMP_PDF") is { Length: > 0 } pasta)
        {
            Directory.CreateDirectory(pasta);
            File.WriteAllBytes(Path.Combine(pasta, $"unico-{situacao}.pdf"), folha.Pdf);
            File.WriteAllBytes(Path.Combine(pasta, $"medico-{situacao}.pdf"), original);
        }
    }

    [Fact]
    public async Task Documento_principal_assinado_sem_espelho_separado_nao_volta_a_fila()
    {
        var c = await CenarioAsync();
        var p = await EncerradaAsync(c, exigir: true);
        await _orquestra.AssinarExecucaoAsync(p.Id, ECpfDeTeste("Joana Técnica", CpfEnfermeira), c.UsuarioEnfermeiraId);
        var original = (await _orquestra.FolhaAsync(p.Id, FolhaPrescricao.Prescricao)).Pdf;
        var assinatura = await _db.AssinaturasDocumento.SingleAsync(a => a.PrescricaoInternaId == p.Id && a.Papel == PapelAssinatura.Executante);
        assinatura.ArquivoRegistroId = null;
        await _db.SaveChangesAsync();
        _db.ChangeTracker.Clear();
        (await _checagens.AguardandoAssinaturaAsync()).Should().NotContain(x => x.Id == p.Id);
        (await _checagens.DoDiaAsync(p.Data)).Should().NotContain(x => x.Id == p.Id);
        (await _orquestra.DocumentoInfusaoAsync(p.Id)).Pdf.Should().Equal(original);
    }

    [Fact]
    public async Task Impressao_unica_no_regime_papel_com_checagem_entrega_execucao_sem_alegar_assinatura()
    {
        var c = await CenarioAsync();
        var p = await EncerradaAsync(c, exigir: false);
        var folha = await _orquestra.DocumentoInfusaoAsync(p.Id);
        folha.NomeArquivo.Should().Contain("execucao");
        folha.Assinatura.Should().BeNull();
        folha.Conferencia.Should().BeNull();
        folha.Pdf.Should().NotBeEmpty();
        Despejar("desktop-regime-papel.pdf", folha.Pdf);
    }

    [Fact]
    public async Task Impressao_unica_nao_regenera_documento_quando_arquivo_assinado_esta_ausente()
    {
        var c = await CenarioAsync();
        var p = await EncerradaAsync(c, exigir: true);
        await _orquestra.AssinarExecucaoAsync(p.Id, ECpfDeTeste("Joana Técnica", CpfEnfermeira), c.UsuarioEnfermeiraId);
        var assinatura = await _db.AssinaturasDocumento.SingleAsync(a => a.PrescricaoInternaId == p.Id && a.Papel == PapelAssinatura.Executante);
        assinatura.ArquivoId = null;
        await _db.SaveChangesAsync();
        (await _checagens.AguardandoAssinaturaAsync()).Should().Contain(x => x.Id == p.Id);
        var acao = () => _orquestra.DocumentoInfusaoAsync(p.Id);
        await acao.Should().ThrowAsync<InvalidOperationException>().WithMessage("*indisponível*");
    }

    [Theory]
    [InlineData(SituacaoChecagem.NaoRealizado, null)]
    [InlineData(SituacaoChecagem.NaoRealizado, "   ")]
    [InlineData(SituacaoChecagem.NaoExecutavel, null)]
    [InlineData(SituacaoChecagem.NaoExecutavel, "   ")]
    public async Task Ambas_respostas_negativas_exigem_justificativa_no_servidor(SituacaoChecagem situacao, string? motivo)
    {
        var cenario = await CenarioAsync();
        var p = await AssinadaPelaMedicaAsync(cenario, exigir: true);
        var item = (await _repo.ObterPrescricaoInternaAsync(p.Id))!.Itens.First();
        await Assert.ThrowsAsync<InvalidOperationException>(() => _checagens.ChecarAsync(item.Id, situacao, new(10, 0), Tecnica, motivo));
        (await _db.ChecagensPrescricao.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Documento_unico_com_varias_paginas_preserva_justificativa_completa()
    {
        var cenario = await CenarioAsync();
        var p = await _prescricoes.CriarAsync(cenario.PacienteId, cenario.ProfissionalMedicaId);
        await _prescricoes.SalvarRascunhoAsync(p.Id, "Teste de paginação", null,
            Enumerable.Range(1, 6).Select(i => new ItemPrescricaoInterna
                { Descricao = $"Medicamento de teste {i}", Dose = "1 unidade" }).ToList(),
            exigeAssinaturaEletronicaDaExecucao: true);
        await _orquestra.AssinarPrescricaoAsync(p.Id, ECpfDeTeste("Dra. Ana Souza", CpfMedica));
        var original = (await _orquestra.FolhaAsync(p.Id, FolhaPrescricao.Prescricao)).Pdf;
        var motivo = string.Concat(Enumerable.Repeat("Justificativa longa com acentuação. ", 40))[..1000];
        foreach (var item in (await _repo.ObterPrescricaoInternaAsync(p.Id))!.Itens)
            await _checagens.ChecarAsync(item.Id, SituacaoChecagem.NaoExecutavel,
                new(10, 37), Tecnica, motivo);
        var checagensSalvas = await _db.ChecagensPrescricao.AsNoTracking().ToListAsync();
        checagensSalvas.Should().HaveCount(6).And.OnlyContain(c => c.Justificativa == motivo);
        var eventos = await _db.Auditoria.AsNoTracking()
            .Where(e => e.Acao == "PrescricaoItemNaoRealizado").ToListAsync();
        eventos.Should().HaveCount(6).And.OnlyContain(e => e.Detalhe != null && e.Detalhe.Contains(motivo));
        await _checagens.EncerrarAsync(p.Id, Tecnica);
        await _orquestra.AssinarExecucaoAsync(p.Id, ECpfDeTeste("Joana Técnica", CpfEnfermeira), cenario.UsuarioEnfermeiraId);
        var final = (await _orquestra.FolhaAsync(p.Id, FolhaPrescricao.Prescricao)).Pdf;
        final.AsSpan(0, original.Length).SequenceEqual(original).Should().BeTrue();
        _assinador.ConferirTodas(final).Should().HaveCount(2).And.OnlyContain(c => c.Integra);
        using var stream = new MemoryStream(final);
        using var pdf = PdfReader.Open(stream, PdfDocumentOpenMode.Import);
        pdf.PageCount.Should().BeGreaterThan(1);
        var campos = pdf.Internals.Catalog.Elements.GetDictionary("/AcroForm")!.Elements.GetArray("/Fields")!;
        var detalhes = campos.Elements.OfType<PdfReference>().Select(r => r.Value).OfType<PdfSharp.Pdf.PdfDictionary>()
            .Where(f => f.Elements.GetString("/T").StartsWith("detalhe_")).ToList();
        detalhes.Should().HaveCount(6).And.OnlyContain(f => f.Elements.GetString("/V").Contains(motivo));
        Despejar("unico-multiplas-paginas.pdf", final);
    }
}
