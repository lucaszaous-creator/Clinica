using Clinica.Application.Servicos;
using Clinica.Domain;
using Clinica.Domain.Entities;
using Clinica.Infrastructure;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Clinica.Tests;

/// <summary>
/// A ESCUTA PÓS-SESSÃO (nov/2026, Fila 360): quem pede escuta, as barreiras do dado de saúde, o registro que não se
/// duplica nem se apaga, e o aviso de piora que chega ao profissional da sessão. Consultas EXECUTADAS (SQLite).
/// </summary>
public sealed class EscutaPosSessaoTests : IDisposable
{
    private static readonly DateTime Agora = new(2026, 11, 18, 10, 0, 0);
    private static readonly DateOnly Hoje = DateOnly.FromDateTime(Agora);

    private readonly SqliteConnection conexao = new("Data Source=:memory:");
    private readonly ClinicaDbContext db;
    private readonly EscutaPosSessaoService svc;
    private readonly UsuarioSistema patrycia = new() { Nome = "Patrycia", Login = "patrycia", Perfil = PerfilAcesso.Gerente };
    private readonly UsuarioSistema semProntuario = new() { Nome = "Sem prontuário", Login = "semprontuario", Perfil = PerfilAcesso.Gerente, PermissoesNegadas = Permissao.VerProntuario };
    private readonly UsuarioSistema medica = new() { Nome = "Dra. Ana", Login = "dra.ana", Perfil = PerfilAcesso.Profissional };
    private readonly UsuarioSistema outroMedico = new() { Nome = "Dr. Beto", Login = "dr.beto", Perfil = PerfilAcesso.Profissional };
    private readonly Paciente alzira = new() { Nome = "Alzira Pinto", Telefone = "22995012020", Convenio = Convenio.UnimedPadrao };
    private readonly Paciente bento = new() { Nome = "Bento Lima", Telefone = "22995013030", Convenio = Convenio.UnimedPadrao };

    public EscutaPosSessaoTests()
    {
        conexao.Open();
        db = new(new DbContextOptionsBuilder<ClinicaDbContext>().UseSqlite(conexao).Options);
        db.Database.EnsureCreated();
        svc = new(db, new ClinicaRepositorio(db), () => Agora);
        medica.Profissional = new() { Nome = "Dra. Ana", Ativo = true };
        outroMedico.Profissional = new() { Nome = "Dr. Beto", Ativo = true };
        db.Usuarios.AddRange(patrycia, semProntuario, medica, outroMedico);
        db.Pacientes.AddRange(alzira, bento);
        db.SaveChanges();
    }

    public void Dispose() { db.Dispose(); conexao.Dispose(); }

    private int Sessao(Paciente p, ModalidadeAtendimento m, int diasAtras, UsuarioSistema? profissional = null)
    {
        var data = Hoje.AddDays(-diasAtras);
        var a = new Atendimento { PacienteId = p.Id, Modalidade = m, Data = data, RealizadoEm = data.ToDateTime(new TimeOnly(9, 0)) };
        db.Atendimentos.Add(a);
        db.SaveChanges();
        db.Agendamentos.Add(new Agendamento { PacienteId = p.Id, DataHora = data.ToDateTime(new TimeOnly(9, 0)), ModalidadePrevista = m,
            Status = StatusAgendamento.Realizado, AtendimentoId = a.Id, ProfissionalId = (profissional ?? medica).ProfissionalId });
        db.SaveChanges();
        return a.Id;
    }

    [Fact]
    public async Task A_fila_traz_a_primeira_sessao_e_a_volta_pela_retencao_e_mais_nada()
    {
        var primeira = Sessao(alzira, ModalidadeAtendimento.AcupunturaSimples, 2);
        Sessao(alzira, ModalidadeAtendimento.AcupunturaComEletro, 1);           // mesma família: não é a primeira
        Sessao(alzira, ModalidadeAtendimento.Consulta, 1);                      // consulta não é tratamento
        Sessao(bento, ModalidadeAtendimento.BsvApenas, 90);                     // o tratamento antigo do Bento
        var volta = Sessao(bento, ModalidadeAtendimento.BsvApenas, 3);          // a volta pela retenção
        db.Acompanhamentos.Add(new AcompanhamentoPaciente { PacienteId = bento.Id, Tipo = TipoAcompanhamento.Recall, Modalidade = ModalidadeAtendimento.BsvApenas,
            ReferenciaEm = Hoje.AddDays(-90).ToDateTime(TimeOnly.MinValue), ProximoContato = Hoje.AddDays(-30), CriadoEm = Agora.AddDays(-30) });
        var antiga = Sessao(alzira, ModalidadeAtendimento.BsvApenas, EscutaNaTela.DiasParaEscutar + 1); // fora da janela
        await db.SaveChangesAsync();

        var fila = await svc.ParaEscutarAsync(patrycia.Id);

        Assert.Equal([(volta, MotivoDaEscuta.VoltouPelaRetencao), (primeira, MotivoDaEscuta.PrimeiraSessao)],
            fila.Select(s => (s.AtendimentoId, s.Motivo)));
        Assert.DoesNotContain(fila, s => s.AtendimentoId == antiga);
        Assert.Equal("Dra. Ana", fila[0].Profissional);
        Assert.Equal("Bento Lima", fila[0].Paciente);
        Assert.True(fila[0].PodeMensagem, "sem recusa registrada, a mensagem está liberada");
    }

    [Fact]
    public async Task Registrar_pede_o_prontuario_tira_da_fila_e_nao_duplica()
    {
        var sessao = Sessao(alzira, ModalidadeAtendimento.AcupunturaSimples, 1);

        // A fila é dado de agenda: quem não tem o prontuário VÊ a fila, mas não registra nem lê como o paciente está.
        Assert.Single(await svc.ParaEscutarAsync(semProntuario.Id));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            svc.RegistrarAsync(semProntuario.Id, new(sessao, ComoEstaPaciente.Melhor, null, CanalContato.WhatsApp)));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => svc.DoPacienteAsync(semProntuario.Id, alzira.Id));
        // O médico não tem o acesso do acompanhamento: não é ele quem registra a escuta.
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => svc.ParaEscutarAsync(medica.Id));

        var escuta = await svc.RegistrarAsync(patrycia.Id, new(sessao, ComoEstaPaciente.Melhor, " Bem melhor da dor ", CanalContato.WhatsApp));
        var deNovo = await svc.RegistrarAsync(patrycia.Id, new(sessao, ComoEstaPaciente.Melhor, "Bem melhor da dor", CanalContato.WhatsApp));

        Assert.Equal(escuta.Id, deNovo.Id);
        Assert.Equal("Bem melhor da dor", escuta.Relato);
        Assert.Equal(MotivoDaEscuta.PrimeiraSessao, escuta.Motivo);
        Assert.Equal(medica.ProfissionalId, escuta.ProfissionalId);
        Assert.Equal("patrycia", escuta.RegistradoPor);
        Assert.Empty(await svc.ParaEscutarAsync(patrycia.Id));
        Assert.Single(await db.Auditoria.Where(a => a.Acao == "EscutaRegistrada" && a.PacienteId == alzira.Id).ToListAsync());
        var outra = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            svc.RegistrarAsync(patrycia.Id, new(sessao, ComoEstaPaciente.Igual, null, CanalContato.WhatsApp)));
        Assert.Contains("cancele", outra.Message);
    }

    [Fact]
    public async Task Piora_sem_relato_e_recusada_e_cancelar_pede_motivo_e_deixa_a_linha()
    {
        var sessao = Sessao(alzira, ModalidadeAtendimento.AcupunturaSimples, 1);
        var semRelato = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            svc.RegistrarAsync(patrycia.Id, new(sessao, ComoEstaPaciente.Pior, "dor", CanalContato.WhatsApp)));
        Assert.Contains("o que piorou", semRelato.Message);

        var errada = await svc.RegistrarAsync(patrycia.Id, new(sessao, ComoEstaPaciente.Igual, null, CanalContato.WhatsApp));
        await Assert.ThrowsAsync<InvalidOperationException>(() => svc.CancelarAsync(patrycia.Id, errada.Id, "ops"));
        await svc.CancelarAsync(patrycia.Id, errada.Id, "Registrei no paciente errado");
        var certa = await svc.RegistrarAsync(patrycia.Id, new(sessao, ComoEstaPaciente.Melhor, null, CanalContato.WhatsApp));

        var todas = await svc.DoPacienteAsync(patrycia.Id, alzira.Id);
        Assert.Equal(2, todas.Count);
        Assert.Contains(todas, e => e.Id == errada.Id && e.Cancelada && e.MotivoCancelamento == "Registrei no paciente errado");
        Assert.Contains(todas, e => e.Id == certa.Id && !e.Cancelada);
    }

    [Fact]
    public async Task A_piora_vira_aviso_do_profissional_da_sessao_ate_ele_marcar_ciente()
    {
        var sessao = Sessao(alzira, ModalidadeAtendimento.AcupunturaSimples, 1);
        var escuta = await svc.RegistrarAsync(patrycia.Id, new(sessao, ComoEstaPaciente.Pior, "A dor no ombro aumentou", CanalContato.WhatsApp));

        var aviso = Assert.Single(await svc.AvisosDoProfissionalAsync(medica.Id));
        Assert.Equal(("Alzira Pinto", "A dor no ombro aumentou"), (aviso.Paciente, aviso.Relato));
        Assert.Empty(await svc.AvisosDoProfissionalAsync(outroMedico.Id));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => svc.CienteAsync(outroMedico.Id, escuta.Id));

        await svc.CienteAsync(medica.Id, escuta.Id);

        Assert.Empty(await svc.AvisosDoProfissionalAsync(medica.Id));
        Assert.Single(await db.Auditoria.Where(a => a.Acao == "EscutaCiente").ToListAsync());
    }

    [Fact]
    public void O_motivo_e_a_recusa_sao_regras_puras()
    {
        var s = new SessaoRealizada(10, 1, Hoje, ModalidadeAtendimento.BsvComAcupuntura);
        Assert.Equal(MotivoDaEscuta.PrimeiraSessao, EscutaNaTela.Motivo(s, [s], []));
        Assert.Null(EscutaNaTela.Motivo(s, [s, new(9, 1, Hoje.AddDays(-30), ModalidadeAtendimento.BsvApenas)], []));
        Assert.Equal(MotivoDaEscuta.PrimeiraSessao, EscutaNaTela.Motivo(s, [s, new(9, 1, Hoje.AddDays(-30), ModalidadeAtendimento.AcupunturaSimples)], []));
        Assert.Equal(MotivoDaEscuta.VoltouPelaRetencao, EscutaNaTela.Motivo(s, [s, new(9, 1, Hoje.AddDays(-90), ModalidadeAtendimento.BsvApenas)], [Hoje]));
        Assert.Null(EscutaNaTela.Motivo(s with { Modalidade = ModalidadeAtendimento.Consulta }, [], []));
        Assert.Null(EscutaNaTela.Recusa(ComoEstaPaciente.NaoRespondeu, null));
        Assert.NotNull(EscutaNaTela.Recusa(ComoEstaPaciente.Pior, "  "));
        Assert.NotNull(EscutaNaTela.Recusa(ComoEstaPaciente.Melhor, new string('a', EscutaNaTela.RelatoMaximo + 1)));
        Assert.Contains("primeira sessão", EscutaNaTela.Pergunta("Alzira Pinto", MotivoDaEscuta.PrimeiraSessao, "SemDor"));
        Assert.StartsWith("Oi, Alzira!", EscutaNaTela.Pergunta("Alzira Pinto", MotivoDaEscuta.VoltouPelaRetencao, null));
    }
}
