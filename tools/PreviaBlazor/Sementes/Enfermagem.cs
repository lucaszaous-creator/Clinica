// Dados FICTÍCIOS da prévia — nenhuma pessoa real.
using Clinica.Domain;
using Clinica.Domain.Entities;
using Clinica.Infrastructure;

namespace PreviaBlazor.Sementes;

/// <summary>
/// A ENFERMAGEM (out/2026 — a tela Enfermagem e a seção "Atendimento de enfermagem" viraram
/// Blazor). Uma paciente de BSV com a sessão de HOJE em atendimento (o cronômetro corre e o
/// Registrar nasce aceso, porque a data tem sessão BSV), com:
/// <list type="bullet">
/// <item>uma passagem de enfermagem de hoje — consulta COFEN com diagnóstico e dois cuidados,
/// um deles "se necessário" (que NÃO conta como pendente) — e sinais vitais, ligada à sessão
/// (o selo DESTA SESSÃO acende);</item>
/// <item>uma passagem antiga cancelada (aparece marcada, nunca sumindo);</item>
/// <item>a sessão médica de ontem (a aba "Conduta médica e infusões" tem o que "Ver");</item>
/// <item>uma alergia e uma medicação contínua (o alerta e a linha de contexto).</item>
/// </list>
/// Paciente PRÓPRIA, para não mexer no que outras telas contam. Sem Id fixo.
/// </summary>
public sealed class SementeEnfermagem : ISemente
{
    public int Ordem => 99;

    public void Semear(ClinicaDbContext db)
    {
        var hoje = DateOnly.FromDateTime(DateTime.Today);
        var helena = db.Set<Profissional>().First(p => p.NomeCurto == "Dra. Helena");

        var marta = new Paciente { Nome = "Marta Vasconcelos Rocha", Telefone = "(21) 98333-0201", DataNascimento = new DateOnly(1964, 7, 21) };
        db.Add(marta);
        db.SaveChanges();

        var sessao = new Agendamento
        {
            PacienteId = marta.Id, ProfissionalId = helena.Id, DataHora = hoje.ToDateTime(new TimeOnly(8, 0)),
            ModalidadePrevista = ModalidadeAtendimento.BsvApenas, ModalidadeCodigo = nameof(ModalidadeAtendimento.BsvApenas),
            Status = StatusAgendamento.Agendado, DuracaoMinutos = 60,
            CriadoPor = "recepcao", CriadoEm = hoje.ToDateTime(TimeOnly.MinValue).AddDays(-3),
            ChegadaEm = hoje.ToDateTime(new TimeOnly(7, 52)),
            InicioAtendimentoEm = hoje.ToDateTime(new TimeOnly(8, 3))
        };
        db.Add(sessao);

        db.Add(new Evolucao
        {
            PacienteId = marta.Id, ProfissionalId = helena.Id, Data = hoje.AddDays(-1),
            QueixaPrincipal = "Dor neuropática em membro superior direito",
            HipoteseDiagnostica = "Síndrome dolorosa complexa regional", CidSessao = "G90.5",
            Conduta = "Bloqueio simpático venoso amanhã; manter gabapentina",
            PlanoTerapeutico = "Série de três bloqueios, reavaliar a EVA na terceira", EvaAntes = 8, EvaDepois = 6,
            CriadoPor = "previa"
        });

        db.Add(new ProblemaPaciente
        {
            PacienteId = marta.Id, Natureza = NaturezaProblema.Alergia, Descricao = "Dipirona",
            Situacao = SituacaoProblema.Ativo, CriadoPor = "previa"
        });
        db.Add(new ProblemaPaciente
        {
            PacienteId = marta.Id, Natureza = NaturezaProblema.MedicacaoContinua, Descricao = "Gabapentina 300 mg",
            Situacao = SituacaoProblema.Ativo, CriadoPor = "previa"
        });
        db.SaveChanges();

        var passagem = new EvolucaoEnfermagem
        {
            PacienteId = marta.Id, AgendamentoId = sessao.Id, Data = hoje, Hora = new TimeOnly(8, 10),
            Texto = "Admitida para BSV. Acesso venoso periférico em MSE, sem intercorrências na punção.",
            PressaoSistolica = 124, PressaoDiastolica = 78, FrequenciaCardiaca = 76, SaturacaoOxigenio = 98,
            Temperatura = 36.4m,
            AcessoLocal = "Antebraço esquerdo", AcessoCalibre = "22G", AcessoPuncionadoEm = hoje,
            Historico = "Refere dor 8/10 em MSD, em uso de gabapentina. Nega jejum incompleto.",
            ExameFisico = "Corada, hidratada, eupneica.",
            AutorNome = "Joana Técnica", AutorConselho = "COREN-RJ 000000",
            RegistradoEm = hoje.ToDateTime(new TimeOnly(8, 12)),
            Diagnosticos =
            [
                new DiagnosticoEnfermagem
                {
                    Titulo = "Dor crônica", RelacionadoA = "lesão neuropática", EvidenciadoPor = "relato verbal de dor 8/10",
                    ResultadoEsperado = "Dor ≤ 4/10 ao fim da sessão", Ordem = 0
                }
            ],
            Cuidados =
            [
                new CuidadoEnfermagem { Descricao = "Aferir sinais vitais", Frequencia = "a cada 15 min durante a infusão", Ordem = 0 },
                new CuidadoEnfermagem { Descricao = "Aplicar compressa fria no local da punção", Frequencia = "se dor no acesso", SeNecessario = true, Ordem = 1 }
            ]
        };
        db.Add(passagem);

        db.Add(new EvolucaoEnfermagem
        {
            PacienteId = marta.Id, Data = hoje.AddDays(-7), Hora = new TimeOnly(9, 30),
            Texto = "Registro lançado na paciente errada.",
            AutorNome = "Joana Técnica", AutorConselho = "COREN-RJ 000000",
            RegistradoEm = hoje.ToDateTime(new TimeOnly(9, 35)).AddDays(-7),
            CanceladaEm = hoje.ToDateTime(new TimeOnly(9, 40)).AddDays(-7),
            MotivoCancelamento = "Lançado na ficha errada", CanceladaPor = "joana"
        });
        db.SaveChanges();
    }
}
