using Clinica.Application.Servicos;
using Clinica.Application.Tablet;
using Clinica.Domain.Entities;
using Clinica.Infrastructure.Tablet;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Clinica.Tests;

public sealed partial class AtendimentoTabletTests
{
    private async Task HabilitarSemA1()
    {
        await Preparar();
        usuario.Profissional!.RegistroConselho = "CRM-SP 123456";
        usuario.Profissional.Cpf = "11111111111";
        await repo.SalvarConfiguracaoAsync(ContinuidadeSemAssinatura.Configuracao, "true");
        await db.SaveChangesAsync();
    }
    private async Task EntrarEnfermagemSemA1()
    {
        var profissional = new Profissional { Nome = "Enfermeira fictícia", RegistroConselho = "COREN-SP 123456", Cpf = "22222222222", Ativo = true };
        db.Profissionais.Add(profissional); await db.SaveChangesAsync();
        usuario = await new AcessoService(repo).CriarAsync("Enfermeira fictícia", "enfermagem.sem.a1", "Teste#SemA12026", PerfilAcesso.Enfermagem);
        usuario.ProfissionalId = profissional.Id; usuario.DeveTrocarSenha = false;
        await db.SaveChangesAsync();
        sessao = (await portal.EntrarAsync(usuario, "aparelho-enfermagem", null, default)).Sessao;
    }
    private static LiberarInfusaoTablet Liberacao(PrescricaoInterna p, bool alergia = false)
        => new(Guid.NewGuid(), PostoTabletService.Versao(p), alergia);

    [Fact] public async Task SemA1_medico_libera_enfermagem_executa_e_conclui_sem_criar_assinaturas()
    {
        await HabilitarSemA1(); var p = await Folha(SituacaoPrescricao.Rascunho);
        var pedido = Liberacao(p);
        var resultado = await Posto.LiberarSemAssinaturaAsync(sessao, p.Id, pedido, default);
        Assert.Equal(resultado, await Posto.LiberarSemAssinaturaAsync(sessao, p.Id, pedido, default));
        Assert.Equal(SituacaoPrescricao.Liberada, p.Situacao); Assert.Null(p.AssinadaEm); Assert.False(p.EstaAssinada);
        Assert.False(p.PodeEditar); Assert.NotNull(p.LiberadaSemAssinaturaEm);
        Assert.Single(await db.Auditoria.Where(a => a.Acao == "PrescricaoLiberadaSemAssinatura").ToListAsync());
        await EntrarEnfermagemSemA1();
        var fila = Json(await Posto.FilaAsync(sessao, 0, default, "executar"));
        Assert.Equal(1, fila.GetProperty("total").GetInt32());
        Assert.Single(await repo.PrescricoesInternasDoDiaAsync(p.Data));
        await Posto.ChecarAsync(sessao, p.Id, new(Guid.NewGuid(), p.Itens[0].Id, PostoTabletService.Versao(p),
            SituacaoChecagem.Realizado, new TimeOnly(10, 0), Data: svc.Hoje.AddDays(-1)), default);
        var encerrar = new EncerrarInfusaoTablet(Guid.NewGuid(), PostoTabletService.Versao(p));
        await Posto.EncerrarAsync(sessao, p.Id, encerrar, default);
        await Posto.EncerrarAsync(sessao, p.Id, encerrar, default);
        Assert.Equal(SituacaoPrescricao.Encerrada, p.Situacao);
        Assert.Empty(p.Assinaturas); Assert.Null(p.AssinadaEm); Assert.False(p.AguardaAssinaturaDaExecucao);
        Assert.Equal("22222222222", p.Itens[0].ChecagemVigente!.ExecutanteCpf);
        Assert.Empty(Json(await Posto.FilaAsync(sessao, 0, default)).GetProperty("itens").EnumerateArray());
        Assert.All(EtapasInfusao.Da(p), e => Assert.True(e.Concluida));
        var pdf = await new PrescricaoInternaPdfService(repo, new(repo)).GerarRegistroExecucaoAsync(p.Id);
        Assert.True(pdf.Length > 1000);
        if (Environment.GetEnvironmentVariable("CLINICA_PDF_QA") is { } pasta)
        {
            Directory.CreateDirectory(pasta);
            await File.WriteAllBytesAsync(Path.Combine(pasta, "infusao-sem-assinatura.pdf"), pdf);
        }
    }

    [Fact] public async Task SemA1_desabilitado_nao_libera_e_nao_marca_assinatura()
    {
        await Preparar(); var p = await Folha(SituacaoPrescricao.Rascunho);
        await Assert.ThrowsAsync<InvalidOperationException>(() => Posto.LiberarSemAssinaturaAsync(sessao, p.Id, Liberacao(p), default));
        Assert.Equal(SituacaoPrescricao.Rascunho, p.Situacao); Assert.Null(p.LiberadaSemAssinaturaEm);
    }

    [Fact] public async Task SemA1_nao_libera_prescricao_de_outro_medico_ou_pela_enfermagem()
    {
        await HabilitarSemA1(); var p = await Folha(SituacaoPrescricao.Rascunho);
        p.ProfissionalId = outro.ProfissionalId; await db.SaveChangesAsync();
        await Assert.ThrowsAsync<RecursoClinicoIndisponivel>(() => Posto.LiberarSemAssinaturaAsync(sessao, p.Id, Liberacao(p), default));
        await EntrarEnfermagemSemA1();
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => Posto.LiberarSemAssinaturaAsync(sessao, p.Id, Liberacao(p), default));
        Assert.Empty(p.Assinaturas); Assert.Null(p.LiberadaSemAssinaturaEm);
    }

    [Fact] public async Task SemA1_rejeita_tela_antiga_e_mantem_conferencia_de_alergia()
    {
        await HabilitarSemA1(); var p = await Folha(SituacaoPrescricao.Rascunho);
        var antigo = Liberacao(p); p.Itens[0].Descricao = "Dipirona"; await db.SaveChangesAsync();
        await Assert.ThrowsAsync<ConflitoClinicoTablet>(() => Posto.LiberarSemAssinaturaAsync(sessao, p.Id, antigo, default));
        await db.Entry(sessao).ReloadAsync();
        db.ProblemasPaciente.Add(new() { PacienteId = p.PacienteId, Natureza = NaturezaProblema.Alergia, Descricao = "Dipirona", Situacao = SituacaoProblema.Ativo });
        await db.SaveChangesAsync();
        await Assert.ThrowsAsync<InvalidOperationException>(() => Posto.LiberarSemAssinaturaAsync(sessao, p.Id, Liberacao(p), default));
        await db.Entry(sessao).ReloadAsync();
        await Posto.LiberarSemAssinaturaAsync(sessao, p.Id, Liberacao(p, true), default);
        Assert.Equal(SituacaoPrescricao.Liberada, p.Situacao);
    }

    [Fact] public async Task SemA1_execucao_pendente_continua_exigindo_checagem()
    {
        await HabilitarSemA1(); var p = await Folha(SituacaoPrescricao.Rascunho);
        await Posto.LiberarSemAssinaturaAsync(sessao, p.Id, Liberacao(p), default);
        await EntrarEnfermagemSemA1();
        await Assert.ThrowsAsync<InvalidOperationException>(() => Posto.EncerrarAsync(sessao, p.Id,
            new(Guid.NewGuid(), PostoTabletService.Versao(p)), default));
        Assert.Equal(SituacaoPrescricao.Liberada, p.Situacao);
    }

    [Fact] public async Task SemA1_execucao_antiga_encerra_sem_apagar_assinatura_medica()
    {
        await HabilitarSemA1(); var p = await Folha(SituacaoPrescricao.Encerrada);
        p.AssinadaEm = DateTime.Now.AddDays(-1);
        p.Assinaturas.Add(new() { Papel = PapelAssinatura.Prescritor, NomeAssinante = "Médico fictício", HashConteudo = "original" });
        p.Itens[0].Checagens.Add(new() { ExecutanteNome = "Enfermeira fictícia", ExecutanteConselho = "COREN-SP 123456", Situacao = SituacaoChecagem.Realizado });
        await db.SaveChangesAsync(); await EntrarEnfermagemSemA1();
        await Posto.ConcluirSemAssinaturaAsync(sessao, p.Id, new(Guid.NewGuid(), PostoTabletService.Versao(p)), default);
        Assert.Single(p.Assinaturas); Assert.Equal("original", p.AssinaturaDoPrescritor!.HashConteudo);
        Assert.NotNull(p.AssinadaEm); Assert.False(p.AguardaAssinaturaDaExecucao);
        Assert.True(p.EstaAssinada);
    }

    [Fact] public async Task SemA1_orientacao_externa_vai_ao_medico_sem_assinatura_da_enfermagem()
    {
        await HabilitarSemA1(); var medico = usuario; var sessaoMedico = sessao;
        await EntrarEnfermagemSemA1();
        var r = await Posto.RegistrarInfusaoExternaAsync(sessao, horario.PacienteId,
            new(Guid.NewGuid(), new(horario.PacienteId, medico.ProfissionalId!.Value, null, svc.Hoje,
                new(8, 0), "Execução fictícia", "Orientação médica externa fictícia")), default);
        var p = (await repo.ObterPrescricaoInternaAsync(r.Id))!;
        Assert.True(p.ModoSemAssinatura); Assert.True(p.AguardaValidacaoMedica);
        Assert.Empty(Json(await Posto.FilaAsync(sessao, 0, default, "assinarEnfermagem")).GetProperty("itens").EnumerateArray());
        Assert.Single(Json(await Posto.FilaAsync(sessaoMedico, 0, default, "assinarMedico")).GetProperty("itens").EnumerateArray());
        await Posto.LiberarSemAssinaturaAsync(sessaoMedico, p.Id, Liberacao(p), default);
        Assert.False(p.AguardaValidacaoMedica); Assert.Empty(p.Assinaturas); Assert.Null(p.AssinadaEm);
        Assert.Empty(Json(await Posto.FilaAsync(sessaoMedico, 0, default, "assinarMedico")).GetProperty("itens").EnumerateArray());
    }

    [Fact] public async Task SemA1_conclusao_recusa_execucao_incompleta_ou_devolvida()
    {
        await HabilitarSemA1(); var p = await Folha(SituacaoPrescricao.Encerrada);
        await EntrarEnfermagemSemA1();
        await Assert.ThrowsAsync<InvalidOperationException>(() => Posto.ConcluirSemAssinaturaAsync(sessao, p.Id,
            new(Guid.NewGuid(), PostoTabletService.Versao(p)), default));
        Assert.False(p.ModoSemAssinatura);
    }

    [Theory][InlineData("receita")][InlineData("atestado")][InlineData("exame")][InlineData("comparecimento")][InlineData("relatorio")][InlineData("anamnese")]
    public async Task SemA1_documentos_sao_salvos_sem_certificado(string tipo)
    {
        await HabilitarSemA1();
        var r = await Posto.EmitirAsync(sessao, horario.PacienteId, new(Guid.NewGuid(), tipo, "Conteúdo fictício", DiasAfastamento: 1), default);
        var d = await repo.ObterDocumentoAsync(r.Id);
        Assert.NotNull(d); Assert.Null(d.AssinadoEm);
    }

    [Fact] public async Task SemA1_migracao_recusa_reversao_depois_de_liberacao_registrada()
    {
        if (!BancoDosTestes.NoPostgres) return;
        await HabilitarSemA1(); var p = await Folha(SituacaoPrescricao.Rascunho);
        await Posto.LiberarSemAssinaturaAsync(sessao, p.Id, Liberacao(p), default);
        var migrador = Microsoft.EntityFrameworkCore.Infrastructure.AccessorExtensions
            .GetService<Microsoft.EntityFrameworkCore.Migrations.IMigrator>(db);
        var erro = await Assert.ThrowsAsync<Npgsql.PostgresException>(() =>
            migrador.MigrateAsync("20261001180000_AuditoriaPreservaDetalheCompleto"));
        Assert.Equal("P0001", erro.SqlState);
        Assert.Contains("20261002140919_ContinuidadeSemAssinatura", await db.Database.GetAppliedMigrationsAsync());
        Assert.True((await db.PrescricoesInternas.AsNoTracking().SingleAsync(x => x.Id == p.Id)).ModoSemAssinatura);
    }
}
