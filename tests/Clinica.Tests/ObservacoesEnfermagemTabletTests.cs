using Clinica.Application.Tablet;
using Clinica.Application.Servicos;
using Clinica.Application.Modelos;
using System.Text.Json;
using Clinica.Domain;
using Clinica.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Clinica.Tests;

public sealed partial class AtendimentoTabletTests
{
    private ObservacoesEnfermagemTablet Observacoes() => new(Guid.NewGuid(), svc.Hoje, horario.Id,
        [new(new(8, 0), "Primeira observação fictícia", false),
         new(new(8, 30), "Segunda observação fictícia", false),
         new(new(9, 0), "Intercorrência fictícia", true)]);

    [Theory]
    [InlineData(ModalidadeAtendimento.BsvApenas)]
    [InlineData(ModalidadeAtendimento.BsvComAcupuntura)]
    public async Task Observacoes_preservam_horarios_autoria_vinculo_e_guias_sem_duplicacao(ModalidadeAtendimento modalidade)
    {
        await PrepararBSV(); horario.ModalidadePrevista = modalidade; await db.SaveChangesAsync();
        await svc.SalvarAsync(sessao, horario.Id, (await Pedido()) with { ConcluirAoSalvar = true, Consumo = new([], true) }, default);
        var fim = horario.FimAtendimentoEm;
        var guias = await db.Codigos.Select(g => g.Id).ToArrayAsync();
        await Enfermeira();
        var pedido = Observacoes();
        var salvo = await Posto.RegistrarObservacoesEnfermagemAsync(sessao, horario.PacienteId, pedido, default);
        var repetido = await Posto.RegistrarObservacoesEnfermagemAsync(sessao, horario.PacienteId, pedido, default);
        Assert.Equal(salvo.Ids, repetido.Ids);
        var registros = await db.EvolucoesEnfermagem.OrderBy(e => e.Hora).ToArrayAsync();
        Assert.Equal(3, registros.Length);
        Assert.Equal(pedido.Observacoes.Select(o => o.Hora), registros.Select(e => e.Hora));
        Assert.All(registros, e => { Assert.Equal(horario.Id, e.AgendamentoId); Assert.Equal(usuario.Id, e.AutorUsuarioId); Assert.Equal(pedido.Data, e.Data); });
        Assert.True(registros[2].Intercorrencia);
        Assert.Equal(fim, horario.FimAtendimentoEm);
        Assert.Equal(guias, await db.Codigos.Select(g => g.Id).ToArrayAsync());
    }

    [Fact]
    public async Task Observacao_invalida_desfaz_todo_o_envio_inclusive_registro_anterior_valido()
    {
        await PrepararBSV(); await Enfermeira();
        var pedido = Observacoes();
        pedido.Observacoes[1] = pedido.Observacoes[1] with { Texto = "" };
        await Assert.ThrowsAnyAsync<Exception>(() => Posto.RegistrarObservacoesEnfermagemAsync(sessao, horario.PacienteId, pedido, default));
        db.ChangeTracker.Clear();
        Assert.Empty(await db.EvolucoesEnfermagem.ToListAsync());
        Assert.False(await db.Set<OperacaoClinicaTablet>().AnyAsync(o => o.Id == pedido.Idempotencia));
        Assert.Empty(await db.Codigos.ToListAsync());
    }

    [Fact]
    public async Task Chegada_e_apos_aplicacao_sao_salvas_em_momentos_separados_na_mesma_sessao()
    {
        await PrepararBSV(); await Enfermeira();
        var chegada = new ObservacoesEnfermagemTablet(Guid.NewGuid(), svc.Hoje, horario.Id,
            [new(new(8, 0), "Estado inicial fictício", false,
                new SinaisVitais(120, 80, 72, 18, SaturacaoOxigenio: 98),
                NegaAlergia: true, FaseAtendimento: "Chegada")]);
        await Posto.RegistrarObservacoesEnfermagemAsync(sessao, horario.PacienteId, chegada, default);
        Assert.Single(await db.EvolucoesEnfermagem.ToListAsync());
        var final = new ObservacoesEnfermagemTablet(Guid.NewGuid(), svc.Hoje, horario.Id,
            [new(new(9, 0), "Estado após aplicação fictício", false,
                new SinaisVitais(118, 78, 70, 17, SaturacaoOxigenio: 99),
                FaseAtendimento: "AposAplicacao")]);
        await Posto.RegistrarObservacoesEnfermagemAsync(sessao, horario.PacienteId, final, default);
        var registros = await db.EvolucoesEnfermagem.OrderBy(e => e.Hora).ToArrayAsync();
        Assert.Equal(["Chegada", "AposAplicacao"], registros.Select(e => e.FaseAtendimento).ToArray());
        Assert.All(registros, e => { Assert.Equal(horario.Id, e.AgendamentoId); Assert.Null(e.Temperatura); Assert.Null(e.Dor); });
        Assert.Equal(120, registros[0].PressaoSistolica);
        Assert.Equal(118, registros[1].PressaoSistolica);
        Assert.StartsWith("CHEGADA", registros[0].Texto);
        Assert.StartsWith("APÓS APLICAÇÃO", registros[1].Texto);
        var ficha = Json(await Posto.FichaAsync(sessao, horario.PacienteId, 0, default));
        Assert.Equal(2, ficha.GetProperty("enfermagem").GetArrayLength());
        Assert.Contains(ficha.GetProperty("enfermagem").EnumerateArray(),
            e => e.GetProperty("faseAtendimento").GetString() == "AposAplicacao");
        var contexto = Json(await Posto.ContextoEnfermagemAsync(sessao, horario.PacienteId, svc.Hoje, default));
        var sessaoContexto = Assert.Single(contexto.GetProperty("sessoes").EnumerateArray().Where(e => e.GetProperty("id").GetInt32() == horario.Id));
        Assert.StartsWith("08:00", sessaoContexto.GetProperty("chegadaHora").GetString());
        Assert.StartsWith("09:00", sessaoContexto.GetProperty("aposAplicacaoHora").GetString());
        Assert.Empty(await db.Codigos.ToListAsync());
        await Assert.ThrowsAsync<InvalidOperationException>(() => Posto.RegistrarObservacoesEnfermagemAsync(
            sessao, horario.PacienteId, chegada with { Idempotencia = Guid.NewGuid() }, default));
        Assert.Equal(2, await db.EvolucoesEnfermagem.CountAsync());
    }

    [Fact]
    public async Task Observacoes_recusam_outro_paciente()
    {
        await PrepararBSV(); await Enfermeira();
        await Assert.ThrowsAsync<InvalidOperationException>(() => Posto.RegistrarObservacoesEnfermagemAsync(sessao, outro.PacienteId, Observacoes(), default));
        Assert.Empty(await db.EvolucoesEnfermagem.ToListAsync());
    }

    [Fact]
    public async Task Observacoes_recusam_modalidade_nao_BSV()
    {
        await PrepararBSV(); await Enfermeira();
        horario.ModalidadePrevista = ModalidadeAtendimento.Consulta; await db.SaveChangesAsync();
        await Assert.ThrowsAsync<InvalidOperationException>(() => Posto.RegistrarObservacoesEnfermagemAsync(sessao, horario.PacienteId, Observacoes(), default));
        Assert.Empty(await db.EvolucoesEnfermagem.ToListAsync());
    }

    [Fact]
    public async Task Observacoes_recusam_permissao_revogada()
    {
        await PrepararBSV(); await Enfermeira();
        usuario.PermissoesNegadas = Permissao.RegistrarEvolucaoEnfermagem; await db.SaveChangesAsync();
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => Posto.RegistrarObservacoesEnfermagemAsync(sessao, horario.PacienteId, Observacoes(), default));
        Assert.Empty(await db.EvolucoesEnfermagem.ToListAsync());
    }

    [Theory]
    [InlineData(0)]
    [InlineData(21)]
    public async Task Observacoes_exigem_quantidade_limitada(int quantidade)
    {
        await PrepararBSV(); await Enfermeira();
        var pedido = Observacoes() with { Observacoes = Enumerable.Repeat(new ObservacaoEnfermagemTablet(new(8, 0), "Observação", false), quantidade).ToArray() };
        await Assert.ThrowsAsync<InvalidOperationException>(() => Posto.RegistrarObservacoesEnfermagemAsync(sessao, horario.PacienteId, pedido, default));
        Assert.Empty(await db.EvolucoesEnfermagem.ToListAsync());
    }

    [Fact]
    public async Task Nega_alergia_fica_na_evolucao_sem_criar_ou_apagar_alergias()
    {
        await PrepararBSV(); await Enfermeira();
        var alergia = new ProblemaPaciente { PacienteId = horario.PacienteId, Natureza = NaturezaProblema.Alergia, Descricao = "Alergia anterior fictícia" };
        db.ProblemasPaciente.Add(alergia); await db.SaveChangesAsync();
        var pedido = Observacoes() with { Observacoes = [new(new(8, 0), "Observação fictícia", false, NegaAlergia: true)] };
        await Posto.RegistrarObservacoesEnfermagemAsync(sessao, horario.PacienteId, pedido, default);
        Assert.Equal("Observação fictícia\n\nAlergia: NEGA.", (await db.EvolucoesEnfermagem.SingleAsync()).Texto);
        db.ChangeTracker.Clear();
        var preservada = await db.ProblemasPaciente.SingleAsync();
        Assert.Equal(alergia.Id, preservada.Id); Assert.Equal(alergia.Descricao, preservada.Descricao);
        Assert.Equal(SituacaoProblema.Ativo, preservada.Situacao);
    }

    [Theory]
    [InlineData("Alergia fictícia", "Observação")]
    [InlineData(null, "")]
    public async Task Nega_nao_aceita_alergia_simultanea_nem_substitui_evolucao(string? alergia, string texto)
    {
        await PrepararBSV(); await Enfermeira();
        var pedido = Observacoes() with { Observacoes = [new(new(8, 0), texto, false, AlergiaObservada: alergia, NegaAlergia: true)] };
        await Assert.ThrowsAsync<InvalidOperationException>(() => Posto.RegistrarObservacoesEnfermagemAsync(sessao, horario.PacienteId, pedido, default));
        Assert.Empty(await db.EvolucoesEnfermagem.ToListAsync());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Alergia_e_horarios_acompanham_a_ficha_e_o_documento_exportado(bool nega)
    {
        await PrepararBSV(); await Enfermeira();
        usuario.Nome = "Enfermagem demonstrativa";
        usuario.Profissional!.Nome = usuario.Nome;
        usuario.Profissional.RegistroConselho = "COREN - DEMONSTRAÇÃO";
        horario.Paciente!.Nome = "Paciente demonstrativo — SEM VALIDADE CLÍNICA";
        horario.Paciente.DataNascimento = new(1980, 3, 12);
        await db.SaveChangesAsync();
        var pedido = new ObservacoesEnfermagemTablet(Guid.NewGuid(), svc.Hoje, horario.Id,
            [new(new(8, 0), "Primeira observação: registro fictício de demonstração.", false,
                AlergiaObservada: nega ? null : "Alergia fictícia informada — exemplo sem valor clínico", NegaAlergia: nega),
             new(new(8, 30), "Segunda observação: acompanhamento fictício no mesmo atendimento.", false),
             new(new(9, 0), "Terceira observação: intercorrência fictícia registrada para demonstrar o fluxo.", true)]);
        await Posto.RegistrarObservacoesEnfermagemAsync(sessao, horario.PacienteId, pedido, default);
        var documentos = new DocumentoClinicoService(repo, new(repo), new(repo));
        var documento = await documentos.EmitirRelatorioEvolucaoAsync(horario.PacienteId,
            inicio: svc.Hoje, fim: svc.Hoje, operador: usuario.Login);
        var esperado = nega ? "Alergia: NEGA." : "Alergia observada: Alergia fictícia informada";
        Assert.Contains(documento.Itens, i => i.Detalhe!.Contains(esperado));
        Assert.Equal(3, documento.Itens.Count);
        Assert.Contains(documento.Itens, i => i.Descricao.Contains("08:30"));
        Assert.Contains(documento.Itens, i => i.Detalhe!.Contains("INTERCORRÊNCIA registrada"));
        Assert.Equal(nega ? 0 : 1, await db.ProblemasPaciente.CountAsync());
        var ficha = Json(await Posto.FichaAsync(sessao, horario.PacienteId, 0, default));
        Assert.Contains(ficha.GetProperty("enfermagem").EnumerateArray(), e => e.GetProperty("texto").GetString()!.Contains(esperado));
        // Exportação opt-in para demonstração visual, somente com esta base fictícia.
        if (Environment.GetEnvironmentVariable("CLINICA_PREVIA_ENFERMAGEM") is { Length: > 0 } pasta)
        {
            Directory.CreateDirectory(pasta);
            var nome = nega ? "nega" : "alergia";
            var pdf = await new DocumentosClinicosPdfService(repo).GerarAsync(documento.Id,
                new DadosPrestador { NomeFantasia = "Clínica SemDor — DEMONSTRAÇÃO", Cnpj = "00.000.000/0000-00" });
            await File.WriteAllBytesAsync(Path.Combine(pasta, "sessao-enfermagem-" + nome + ".pdf"), pdf);
            await File.WriteAllTextAsync(Path.Combine(pasta, "ficha-" + nome + ".json"), ficha.ToString());
            await File.WriteAllTextAsync(Path.Combine(pasta, "pedido-" + nome + ".json"), JsonSerializer.Serialize(pedido, ContratoTablet.Json));
        }
    }
}
