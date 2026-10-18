// Dados FICTÍCIOS da prévia — nenhuma pessoa, operador ou ato real.
using Clinica.Domain.Entities;
using Clinica.Infrastructure;

namespace PreviaBlazor.Sementes;

/// <summary>
/// Auditoria e Guarda do prontuário (etapa 6 do Gerente, out/2026). A base já tem pacientes e
/// sessões; esta acrescenta o que as duas telas existem para mostrar:
/// - uma TRILHA de ~120 eventos nos últimos 30 dias, com várias ações e operadores — com o
///   limite de 100 o aviso "LIMITE ATINGIDO" aparece; com o padrão (300), não;
/// - ABERTURAS de prontuário (ProntuarioAcessado:*) de um mesmo paciente por portas diferentes,
///   para "quem abriu este prontuário" e para o atalho "Só quem ABRIU";
/// - uma sessão de 2004 de outro paciente — o prontuário dele já cumpriu os 20 anos, e o
///   retrato da clínica mostra o caso "elegível" (que o sistema NÃO elimina).
/// Sem Id fixo: tudo se acha pelo que as outras sementes gravaram.
/// </summary>
public sealed class SementeTrilhaEGuarda : ISemente
{
    public int Ordem => 97;

    public void Semear(ClinicaDbContext db)
    {
        var pacientes = db.Set<Paciente>().OrderBy(p => p.Id).Select(p => p.Id).Take(40).ToList();
        if (pacientes.Count < 3) return;
        var hoje = DateTime.Today;

        string[] operadores = ["Ana (recepção)", "Bruno (faturamento)", "Dra. Helena", "Direção (prévia)", "Carla (financeiro)"];
        (string Acao, string Detalhe, bool Guia, bool Paciente)[] atos =
        [
            ("BaixaGuia", "Guia baixada com o nº 4521-778", true, true),
            ("EstornoBaixa", "Baixa estornada: número digitado errado", true, true),
            ("GlosaRegistrada", "Glosa 2006 — cota excedida", true, true),
            ("LancamentoCriado", "Receita de R$ 180,00 — sessão particular", false, true),
            ("ContaCriada", "Conta de luz — vence dia 10", false, false),
            ("ContaReagendada", "Aluguel adiado para o dia 15", false, false),
            ("PrecoConvenioAlterado", "Acupuntura · Unimed: R$ 145,00 → R$ 152,00", false, false),
            ("UsuarioCriado", "Usuário 'bia' criado com perfil Recepção", false, false),
            ("SenhaTrocada", "Senha trocada pelo próprio usuário", false, false),
            ("EvolucaoAlterada", "Sessão corrigida — acréscimo à conduta", false, true)
        ];

        var n = 0;
        for (var dia = 0; dia < 30; dia++)
            for (var k = 0; k < 4; k++, n++)
            {
                var ato = atos[n % atos.Length];
                db.Add(new EventoAuditoria
                {
                    DataHora = hoje.AddDays(-dia).AddHours(8 + k * 2).AddMinutes(n % 50),
                    Operador = operadores[n % operadores.Length],
                    Acao = ato.Acao,
                    Detalhe = ato.Detalhe,
                    PacienteId = ato.Paciente ? pacientes[n % pacientes.Count] : null
                });
            }

        // Quem abriu o prontuário do PRIMEIRO paciente: portas diferentes, pessoas diferentes.
        (string Porta, string Rotulo, string Quem)[] aberturas =
        [
            ("ProntuarioClinico", "Prontuário clínico aberto", "Dra. Helena"),
            ("FichaPaciente", "Ficha do paciente aberta", "Ana (recepção)"),
            ("Atendimento", "Tela de atendimento aberta", "Dra. Helena"),
            ("Documento", "Documento do prontuário aberto", "Ana (recepção)"),
            ("ExportacaoTitular", "Dados do titular exportados (LGPD art. 18, II)", "Direção (prévia)")
        ];
        for (var i = 0; i < 9; i++)
        {
            var a = aberturas[i % aberturas.Length];
            db.Add(new EventoAuditoria
            {
                DataHora = hoje.AddDays(-i * 2).AddHours(10).AddMinutes(i * 7),
                Operador = a.Quem, Acao = $"ProntuarioAcessado:{a.Porta}", Detalhe = a.Rotulo,
                PacienteId = pacientes[0]
            });
        }

        // Um prontuário que já cumpriu os 20 anos (último registro em 2004): de um paciente sem
        // sessão nem documento — senão o registro mais novo dele moveria o prazo.
        var antigo = db.Set<Paciente>()
            .Where(p => !db.Set<Evolucao>().Any(e => e.PacienteId == p.Id)
                        && !db.Set<DocumentoClinico>().Any(d => d.PacienteId == p.Id))
            .OrderByDescending(p => p.Id).Select(p => (int?)p.Id).FirstOrDefault();
        if (antigo is { } id)
            db.Add(new Evolucao
            {
                PacienteId = id, Data = new DateOnly(2004, 3, 15),
                QueixaPrincipal = "Cervicalgia", Conduta = "Acupuntura sistêmica"
            });

        db.SaveChanges();
    }
}
