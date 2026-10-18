// Dados FICTÍCIOS da prévia — nenhuma pessoa real.
using Clinica.Domain;
using Clinica.Domain.Entities;
using Clinica.Infrastructure;

namespace PreviaBlazor.Sementes;

/// <summary>
/// As duas telas de "Pacientes e relacionamento" do Gerente que saíram do WPF na etapa 6
/// (out/2026): Campanhas e De onde vêm os pacientes. As outras sementes já gravam a rodada de
/// confirmação e o NPS respondido; esta acrescenta só o que nenhuma delas tinha:
/// - contatos A ENVIAR de NPS e de recall na janela padrão ("Semana que vem"), um deles SEM
///   telefone (a linha fica, o WhatsApp apaga), e um dispensado com o motivo escrito;
/// - a ORIGEM dos pacientes ("como conheceu a clínica?"), com metade da base "não perguntada"
///   — o selo "colher no cadastro" — e indicações com o MESMO nome digitado de dois jeitos,
///   para o ranking mostrar que eles somam.
/// Sem Id fixo: tudo se acha pelo que as outras sementes gravaram.
/// </summary>
public sealed class SementeCampanhasEOrigens : ISemente
{
    public int Ordem => 96;

    public void Semear(ClinicaDbContext db)
    {
        var hoje = DateOnly.FromDateTime(DateTime.Today);
        SemearOrigens(db);
        SemearContatos(db, hoje);
    }

    private static void SemearOrigens(ClinicaDbContext db)
    {
        var pacientes = db.Set<Paciente>().OrderBy(p => p.Id).ToList();
        string[] indicadores = ["Marina Albuquerque", "marina albuquerque ", "Carlos Eduardo Prado", "Dra. Helena Duarte"];
        for (var i = 0; i < pacientes.Count; i++)
        {
            var p = pacientes[i];
            // Metade fica sem a pergunta respondida — a base "anterior ao campo" de toda clínica.
            switch (i % 10)
            {
                case 0: case 1: case 2:
                    p.Origem = OrigemPaciente.Indicacao;
                    p.IndicadoPor = indicadores[i % indicadores.Length];
                    break;
                case 3: p.Origem = OrigemPaciente.Internet; break;
                case 4: p.Origem = OrigemPaciente.Convenio; break;
                default: p.Origem = null; break;
            }
        }
        db.SaveChanges();
    }

    private static void SemearContatos(ClinicaDbContext db, DateOnly hoje)
    {
        var pacientes = db.Set<Paciente>().Where(p => p.Telefone != null).OrderByDescending(p => p.Id).Take(5).ToList();
        if (pacientes.Count < 5) return;

        void Contato(Paciente p, TipoContato tipo, DateOnly referencia, StatusContato status = StatusContato.Pendente,
            string? comentario = null, int indice = 0)
            => db.Add(new ContatoCampanha
            {
                PacienteId = p.Id, Tipo = tipo, Canal = CanalContato.WhatsApp, Status = status,
                Origem = $"PREVIA-CAMP:{tipo}:{indice}", Referencia = referencia, Comentario = comentario,
                RespondidoEm = status == StatusContato.Dispensado ? DateTime.Now.AddHours(-2) : null
            });

        Contato(pacientes[0], TipoContato.Nps, hoje.AddDays(-1), indice: 1);
        Contato(pacientes[1], TipoContato.Nps, hoje.AddDays(-1), indice: 2);
        Contato(pacientes[2], TipoContato.Recall, hoje.AddDays(-3), indice: 3);
        Contato(pacientes[3], TipoContato.Recall, hoje.AddDays(-2), indice: 4);
        Contato(pacientes[4], TipoContato.Nps, hoje.AddDays(-2), StatusContato.Dispensado,
            "Paciente pediu para não receber pesquisa por mensagem.", indice: 5);

        // Um paciente sem telefone: a linha continua na fila, o WhatsApp é que apaga.
        // Paciente NOVO (fictício), para não tirar o telefone de quem as outras telas usam.
        var semFone = new Paciente { Nome = "Otávio Ramos Fictício", Convenio = Convenio.UnimedPadrao, DataNascimento = new DateOnly(1971, 2, 14) };
        db.Add(semFone);
        db.SaveChanges();
        Contato(semFone, TipoContato.Recall, hoje.AddDays(-1), indice: 6);

        db.SaveChanges();
    }
}
