// Dados FICTÍCIOS da prévia — nenhuma pessoa real, nenhuma carteira real.
using Clinica.Application.Servicos;
using Clinica.Domain;
using Clinica.Domain.Entities;
using Clinica.Infrastructure;

namespace PreviaBlazor.Sementes;

/// <summary>
/// CONSULTAR ELEGIBILIDADE NA AGENDA (nov/2026): o botão "Consultar elegibilidade" da aba "Dia"
/// da Agenda (AgendaDoDia) só aparece quando há convênio com consulta automática (família do catálogo
/// <see cref="OperadorasTiss"/> + credencial gravada). Esta semente põe uma credencial de mentira da
/// Amil (a da Petrobras vem da semente do envio) e quatro pacientes na agenda de HOJE: um já
/// consultado hoje (elegível), um da Amil, um sem carteirinha e um de convênio sem consulta.
/// Ela NÃO cadastra convênio: uma linha "Petrobras" no banco das telas mudaria as regras da família
/// para os outros testes (o catálogo de convênios é estático).
/// </summary>
public sealed class SementeElegibilidadeNaAgenda : ISemente
{
    public int Ordem => 101;

    public void Semear(ClinicaDbContext db)
    {
        var hoje = DateTime.Today;
        var helena = db.Set<Profissional>().FirstOrDefault(p => p.NomeCurto == "Dra. Helena");
        if (helena is null) return;

        var repo = new ClinicaRepositorio(db);
        // A chave é de mentira (32 zeros), como na semente do envio: a credencial não abre em lugar nenhum.
        new CredenciaisTissService(repo, new ProtecaoSegredoGlobal(Convert.ToBase64String(new byte[32])))
            .SalvarAsync("Amil", "previa.amil", "senha-de-mentira", "Prévia").GetAwaiter().GetResult();
        db.ChangeTracker.Clear();

        Paciente P(string nome, Convenio familia, string codigo, string? carteira) => new()
        {
            Nome = nome, Convenio = familia, ConvenioCodigo = codigo, Carteirinha = carteira,
            ValidadeCarteirinha = carteira is null ? null : DateOnly.FromDateTime(hoje).AddDays(300),
            Telefone = "(21) 98222-0900", DataNascimento = new DateOnly(1980, 5, 12)
        };
        var marina = P("Marina Teixeira Lobo", Convenio.Petrobras, "Petrobras", "PB0900000001");
        var otavio = P("Otávio Brandão Reis", Convenio.Amil, "Amil", "0007770001");
        var silvia = P("Sílvia Moura Campos", Convenio.Petrobras, "Petrobras", null);
        var renato = P("Renato Assis Prado", Convenio.UnimedPadrao, "UnimedPadrao", "0099900012345");
        db.AddRange(marina, otavio, silvia, renato);
        db.SaveChanges();

        var hora = 14;
        foreach (var p in new[] { marina, otavio, silvia, renato })
            db.Add(new Agendamento
            {
                PacienteId = p.Id, ProfissionalId = helena.Id, DataHora = hoje.AddHours(hora++).AddMinutes(30),
                ModalidadePrevista = ModalidadeAtendimento.Consulta, ModalidadeCodigo = nameof(ModalidadeAtendimento.Consulta),
                CriadoPor = "recepcao", CriadoEm = hoje.AddDays(-3).AddHours(9)
            });

        // A consulta de hoje da Marina, já respondida: o sinal "Elegível hoje" na lista do painel.
        db.Add(new TransacaoTiss
        {
            Tipo = TipoTransacaoTiss.Elegibilidade, ConvenioCodigo = "Petrobras", NumeroCarteira = marina.Carteirinha,
            PacienteId = marina.Id, Estado = EstadoTransacaoTiss.Concluida, Tentativas = 1, PedidoPor = "recepcao",
            PedidoEm = hoje.AddHours(8), EnvioIniciadoEm = hoje.AddHours(8), ProcessadaEm = hoje.AddHours(8).AddSeconds(4),
            Resultado = "Elegível. Nome na operadora: MARINA TEIXEIRA LOBO."
        });
        db.SaveChanges();
    }
}
