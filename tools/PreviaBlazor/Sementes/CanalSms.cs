// Dados FICTÍCIOS da prévia — nenhuma pessoa real.
using Clinica.Domain;
using Clinica.Domain.Entities;
using Clinica.Infrastructure;

namespace PreviaBlazor.Sementes;

/// <summary>
/// O canal de SMS (out/2026): o histórico do mês (confirmações enviadas, uma campanha, uma
/// falha do provedor e um teste), sessões REALIZADAS nos últimos dois meses com modalidade e
/// especialidade (é delas que saem as opções do público da campanha) e o consentimento de
/// comunicação de parte dos pacientes, para a prévia contar quem fica de fora.
///
/// ⚠️ O PROVEDOR fica DESLIGADO de propósito: a prévia e o teste não têm provedor de verdade,
/// e é exatamente o estado que a tela precisa explicar ("desligado — configure").
/// </summary>
public sealed class SementeCanalSms : ISemente
{
    public int Ordem => 81;

    public void Semear(ClinicaDbContext db)
    {
        var helena = db.Profissionais.First(p => p.Nome == "Dra. Helena Duarte");
        var marcos = db.Profissionais.First(p => p.Nome == "Dr. Marcos Vieira");
        var hoje = DateTime.Today;
        var pacientes = db.Pacientes.OrderBy(p => p.Id).Take(10).ToList();

        // Convênios variados na ficha — são as opções do primeiro seletor do público.
        for (var i = 0; i < pacientes.Count; i++)
        {
            pacientes[i].Convenio = (i % 3) switch { 0 => Convenio.UnimedPadrao, 1 => Convenio.Amil, _ => Convenio.Petrobras };
            // Metade consentiu comunicação: a prévia conta a outra metade como "sem consentimento".
            if (i % 2 == 0)
                db.Add(new ConsentimentoLgpd
                {
                    PacienteId = pacientes[i].Id, Finalidade = FinalidadeConsentimento.ComunicacaoEMarketing,
                    Concedido = true, RegistradoEm = hoje.AddDays(-300), RegistradoPor = "previa"
                });
        }
        db.SaveChanges();

        // Sessões realizadas: acupuntura e consultas de psiquiatria/geriatria nos últimos 60 dias.
        for (var i = 0; i < pacientes.Count; i++)
        {
            var consulta = i % 4 == 0;
            db.Add(new Agendamento
            {
                PacienteId = pacientes[i].Id, ProfissionalId = consulta ? marcos.Id : helena.Id,
                DataHora = hoje.AddDays(-5 - i * 5).AddHours(9 + i % 6), DuracaoMinutos = 30,
                Status = StatusAgendamento.Realizado,
                ModalidadePrevista = consulta ? ModalidadeAtendimento.Consulta
                    : i % 2 == 0 ? ModalidadeAtendimento.AcupunturaComEletro : ModalidadeAtendimento.AcupunturaSimples,
                EspecialidadeConsultaCodigo = consulta ? (i % 8 == 0 ? Especialidade.Psiquiatria : Especialidade.Geriatria).ToString() : null
            });
        }
        db.SaveChanges();

        var inicioMes = new DateTime(hoje.Year, hoje.Month, 1);
        var n = 0;
        void Sms(Paciente? p, FinalidadeSms finalidade, SituacaoSms situacao, int segmentos, string texto, string? erro = null, string por = "sistema (SMS automático)")
        {
            var quando = inicioMes.AddHours(9 + n * 7 % 200);
            if (quando > DateTime.Now) quando = DateTime.Now.AddMinutes(-n);
            db.Add(new MensagemSms
            {
                PacienteId = p?.Id, Finalidade = finalidade, Situacao = situacao,
                ChaveIdempotencia = $"PREVIA:{++n}", Telefone = p is null ? "+5521999990000" : Clinica.Domain.TextoSms.CelularE164(p.Telefone) ?? "+5521999990000",
                Texto = Clinica.Domain.TextoSms.ParaGsm(texto), Segmentos = situacao == SituacaoSms.Enviada ? segmentos : 0,
                IdNoProvedor = situacao == SituacaoSms.Enviada ? $"prov-{n:000}" : null, Erro = erro,
                Tentativas = situacao == SituacaoSms.Falhou ? 3 : 1, CriadaEm = quando,
                EnviadaEm = situacao == SituacaoSms.Enviada ? quando.AddSeconds(4) : null, EnviadaPor = por
            });
        }

        foreach (var p in pacientes.Take(6))
            Sms(p, FinalidadeSms.ConfirmacaoSessao, SituacaoSms.Enviada, 1,
                $"Ola, {p.Nome.Split(' ')[0]}! Confirmando sua sessao amanha as 14:00. Confirme ou avise: https://clinica.exemplo/c/ABCDEFGH2345");
        Sms(pacientes[6], FinalidadeSms.ConfirmacaoSessao, SituacaoSms.Falhou, 0, "Ola! Confirmando sua sessao amanha as 09:00.",
            "O provedor recusou: número de destino inválido (código 21614).");
        foreach (var p in pacientes.Where((_, i) => i % 2 == 0))
            Sms(p, FinalidadeSms.Campanha, SituacaoSms.Enviada, 2,
                "Ola! A clinica abriu horarios aos sabados em novembro para acupuntura. Responda para marcar o seu. Nao quer receber? Avise a clinica.",
                por: "Recepção (prévia)");
        Sms(null, FinalidadeSms.Teste, SituacaoSms.Enviada, 1, "Teste do SMS da clinica. Se chegou, os lembretes vao sair por este provedor.", por: "Direção (prévia)");
        db.SaveChanges();
    }
}
