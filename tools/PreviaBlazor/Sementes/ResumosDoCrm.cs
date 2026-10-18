// Dados FICTÍCIOS da prévia — só contagens, como o CRM manda.
using Clinica.Domain.Entities;
using Clinica.Infrastructure;

namespace PreviaBlazor.Sementes;

/// <summary>
/// Oito dias de resumo do CRM (caminho A) para o bloco "Atendimento pelo CRM" da Mesa da
/// direção: sete fechados e o de hoje, parcial. Um dia sem resposta humana (mediana nula)
/// mostra que ele não puxa o tempo típico para zero.
/// </summary>
public sealed class SementeResumosDoCrm : ISemente
{
    public int Ordem => 70;

    public void Semear(ClinicaDbContext db)
    {
        var hoje = DateOnly.FromDateTime(DateTime.Today);
        var agora = DateTime.Now;
        for (var i = 7; i >= 0; i--)
        {
            var dia = hoje.AddDays(-i);
            var conversas = 14 + (i * 3) % 9;
            var parcial = i == 0;
            db.Add(new ResumoCrmDiario
            {
                Dia = dia,
                GeradoEm = parcial ? agora.AddMinutes(-12) : dia.ToDateTime(new TimeOnly(23, 40)),
                RecebidoEm = parcial ? agora.AddMinutes(-12) : dia.ToDateTime(new TimeOnly(23, 40)),
                ConversasIniciadas = parcial ? 6 : conversas,
                ConversasEncerradas = parcial ? 4 : conversas - 1,
                ConversasAbertas = parcial ? 3 : 2,
                MensagensRecebidas = conversas * 4,
                MensagensEnviadas = conversas * 3,
                RespostaMedianaSegundos = i == 3 ? null : 180 + i * 40,
                RespostasMedidas = i == 3 ? 0 : conversas - 3,
                SmsEnviados = 8 + i,
                SmsSegmentos = 8 + i + (i % 3),
                SmsFalhas = i == 5 ? 1 : 0,
                ConfirmacoesPedidas = 10,
                ConfirmacoesRespondidas = 6 + i % 4,
                Resolvidas = conversas / 2,
                Agendadas = 3,
                SemResposta = 1
            });
        }
        db.SaveChanges();
    }
}
