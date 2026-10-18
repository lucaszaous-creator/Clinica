using Clinica.Application.Servicos;
using Clinica.Crm.Bridge;
using Clinica.Domain;
using Clinica.Domain.Entities;
using Clinica.Infrastructure;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Clinica.Tests;

// A tela "Relacionamento" do CRM lê três filas daqui. Quem decide quem entra é o Clinica.
public sealed class PonteClinicRelationshipTests : IDisposable
{
    readonly SqliteConnection connection = new("Data Source=:memory:");
    readonly ClinicaDbContext db;
    readonly ClinicRelationshipBridge bridge;
    readonly DateOnly hoje = DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeBySystemTimeZoneId(DateTimeOffset.UtcNow, "America/Sao_Paulo").DateTime);

    public PonteClinicRelationshipTests()
    {
        connection.Open(); db = new(new DbContextOptionsBuilder<ClinicaDbContext>().UseSqlite(connection).Options); db.Database.EnsureCreated();
        bridge = new(db, new ConsultaService(new ClinicaRepositorio(db)));
    }

    Paciente Paciente(string nome, string fone, Convenio convenio = Convenio.Personalizado)
    {
        var p = new Paciente { Nome = nome, Telefone = fone, Convenio = convenio };
        db.Pacientes.Add(p); db.SaveChanges(); return p;
    }

    void Sessao(Paciente p, int diasAtras, ModalidadeAtendimento m = ModalidadeAtendimento.AcupunturaSimples)
    {
        var d = hoje.AddDays(-diasAtras);
        db.Atendimentos.Add(new() { PacienteId = p.Id, Data = d, Modalidade = m, RealizadoEm = d.ToDateTime(new(10, 0)) });
        db.SaveChanges();
    }

    [Fact] public async Task Sem_vir_ha_15_dias_ou_mais_entra_com_os_dias_e_a_ultima_sessao()
    {
        var sumiu = Paciente("Sumiu", "11999990001"); Sessao(sumiu, 60); Sessao(sumiu, 22);
        var recente = Paciente("Recente", "11999990002"); Sessao(recente, 10);
        var entries = (await bridge.Snapshot(default)).Entries.Where(e => e.List == "absent").ToList();
        var e = Assert.Single(entries);
        Assert.Equal(sumiu.Id, e.PatientId); Assert.Equal(22, e.Days); Assert.Equal(2, e.Sessions);
        Assert.Equal("5511999990001", e.Phone);
        Assert.Contains(hoje.AddDays(-22).ToString("dd/MM/yyyy"), e.Detail);
    }

    [Fact] public async Task Quem_tem_horario_marcado_nao_sumiu()
    {
        var p = Paciente("Volta semana que vem", "11999990003"); Sessao(p, 40);
        db.Agendamentos.Add(new() { PacienteId = p.Id, DataHora = hoje.AddDays(5).ToDateTime(new(9, 0)) }); db.SaveChanges();
        Assert.DoesNotContain((await bridge.Snapshot(default)).Entries, e => e.List == "absent");
    }

    [Fact] public async Task Estornado_nao_conta_como_sessao()
    {
        var p = Paciente("Estorno", "11999990004");
        db.Atendimentos.Add(new() { PacienteId = p.Id, Data = hoje.AddDays(-20), Modalidade = ModalidadeAtendimento.AcupunturaSimples, RealizadoEm = DateTime.Now, EstornadoEm = DateTime.Now });
        db.SaveChanges();
        Assert.Empty((await bridge.Snapshot(default)).Entries);
    }

    [Fact] public async Task Indicado_ao_BSV_sem_primeira_sessao_e_quem_comecou_ha_pouco_sao_novos_no_BSV()
    {
        var indicado = Paciente("Indicado", "11999990005");
        db.Acompanhamentos.Add(new() { PacienteId = indicado.Id, Tipo = TipoAcompanhamento.NovoBsv, Modalidade = ModalidadeAtendimento.BsvApenas,
            ReferenciaEm = hoje.AddDays(-3).ToDateTime(new(9, 0)), CriadoEm = DateTime.Now, ProximoContato = hoje });
        var comecou = Paciente("Começou", "11999990006"); Sessao(comecou, 8, ModalidadeAtendimento.BsvApenas);
        var antigo = Paciente("Antigo no BSV", "11999990007"); Sessao(antigo, 120, ModalidadeAtendimento.BsvApenas); Sessao(antigo, 5, ModalidadeAtendimento.BsvApenas);
        db.SaveChanges();
        var bsv = (await bridge.Snapshot(default)).Entries.Where(e => e.List == "new-bsv").ToDictionary(e => e.PatientId);
        Assert.Equal(2, bsv.Count);
        Assert.Contains("ainda não fez a 1ª sessão", bsv[indicado.Id].Detail); Assert.Equal("A contatar", bsv[indicado.Id].Situation);
        Assert.Contains("Começou o BSV", bsv[comecou.Id].Detail); Assert.Equal("Sem próxima sessão de BSV marcada", bsv[comecou.Id].Situation);
    }

    [Fact] public async Task Indicacao_encerrada_ou_ja_atendida_sai_da_fila()
    {
        var p = Paciente("Já fez", "11999990008");
        db.Acompanhamentos.Add(new() { PacienteId = p.Id, Tipo = TipoAcompanhamento.NovoBsv, Modalidade = ModalidadeAtendimento.BsvApenas,
            ReferenciaEm = hoje.AddDays(-60).ToDateTime(new(9, 0)), CriadoEm = DateTime.Now, ProximoContato = hoje });
        Sessao(p, 50, ModalidadeAtendimento.BsvApenas);
        var q = Paciente("Encerrado", "11999990009");
        db.Acompanhamentos.Add(new() { PacienteId = q.Id, Tipo = TipoAcompanhamento.NovoBsv, Modalidade = ModalidadeAtendimento.BsvApenas,
            ReferenciaEm = hoje.AddDays(-2).ToDateTime(new(9, 0)), CriadoEm = DateTime.Now, ProximoContato = hoje, EncerradoEm = DateTime.Now });
        db.SaveChanges();
        Assert.DoesNotContain((await bridge.Snapshot(default)).Entries, e => e.List == "new-bsv");
    }

    [Fact] public async Task Consulta_vencida_entra_com_a_mesma_frase_da_agenda()
    {
        var p = Paciente("Unimed", "11999990010", Convenio.UnimedPadrao);
        db.Consultas.Add(new() { PacienteId = p.Id, Convenio = Convenio.UnimedPadrao, DataEmissao = hoje.AddDays(-30), ValidadeDias = 22,
            DataVencimento = hoje.AddDays(-8), Status = StatusConsulta.Ativa });
        Sessao(p, 5); // trata na clínica — quem não trata não entra no relacionamento
        Paciente("Nunca emitiu", "11999990011", Convenio.UnimedPadrao);
        db.SaveChanges();
        var e = Assert.Single((await bridge.Snapshot(default)).Entries, e => e.List == "renew");
        Assert.Equal(p.Id, e.PatientId); Assert.Equal("Consulta vencida", e.Situation); Assert.StartsWith("Consulta vencida em", e.Detail);
    }

    // out/2026: o relacionamento é só de quem faz BSV, acupuntura ou os dois. Quem só passou
    // por consulta não entra, e a referência de "sumiu" é a última sessão de TRATAMENTO.
    [Fact] public async Task So_entra_quem_trata_BSV_ou_acupuntura()
    {
        var consulta = Paciente("Só consulta", "11999990020"); Sessao(consulta, 40, ModalidadeAtendimento.Consulta);
        var bsv = Paciente("BSV com acupuntura", "11999990021"); Sessao(bsv, 30, ModalidadeAtendimento.BsvComAcupuntura);
        var acup = Paciente("Acupuntura", "11999990022"); Sessao(acup, 50, ModalidadeAtendimento.AcupunturaComEletro); Sessao(acup, 3, ModalidadeAtendimento.Consulta);
        var ausentes = (await bridge.Snapshot(default)).Entries.Where(e => e.List == "absent").ToList();
        Assert.DoesNotContain(ausentes, e => e.PatientId == consulta.Id);
        Assert.Contains(ausentes, e => e.PatientId == bsv.Id);
        var a = Assert.Single(ausentes, e => e.PatientId == acup.Id);
        Assert.Equal(50, a.Days); Assert.Equal(1, a.Sessions);
    }

    [Fact] public async Task Telefone_compartilhado_e_avisado()
    {
        var a = Paciente("Mãe", "11999990012"); Sessao(a, 30);
        Paciente("Filho", "(11) 99999-0012");
        var e = Assert.Single((await bridge.Snapshot(default)).Entries);
        Assert.Equal("Telefone compartilhado: conferir paciente", e.Block);
    }

    public void Dispose() { db.Dispose(); connection.Dispose(); }
}
