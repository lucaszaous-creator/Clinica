// Dados FICTÍCIOS da prévia — nenhuma pessoa real.
using Clinica.Application.Servicos;
using Clinica.Domain.Entities;
using Clinica.Infrastructure;

namespace PreviaBlazor.Sementes;

/// <summary>
/// O chat da equipe (out/2026): conversas de hoje entre a recepção, a direção, o médico e a
/// enfermagem — um recado curto, um laudo longo (para o "Ver inteiro"), uma menção e uma
/// direta com não lidas. A leitura da Carla fica atrás, para a bolinha mostrar o número.
/// Entra DEPOIS dos acessos (97), porque fala com os usuários que eles criam.
/// </summary>
public sealed class SementeChatDaEquipe : ISemente
{
    public int Ordem => 98;

    public void Semear(ClinicaDbContext db)
    {
        if (db.MensagensEquipe.Any()) return;
        var usuarios = db.Set<UsuarioSistema>().Where(u => u.Ativo).ToList();
        var carla = usuarios.FirstOrDefault(u => u.Login == "carla.previa");
        var otavio = usuarios.FirstOrDefault(u => u.Login == "otavio.previa");
        var medico = usuarios.FirstOrDefault(u => u.Login == "medico.previa");
        var enfermeira = usuarios.FirstOrDefault(u => u.Perfil == PerfilAcesso.Enfermagem);
        if (carla is null || otavio is null || medico is null) return;

        var hoje = DateTime.Today;
        // Quem acabou de ser cadastrado só conta como não lido o que chegou DEPOIS do cadastro;
        // a prévia põe a equipe cadastrada ontem para as mensagens de hoje contarem.
        foreach (var u in usuarios) u.CriadoEm = hoje.AddDays(-1);
        MensagemEquipe M(string conversa, UsuarioSistema autor, int hora, int minuto, string texto, string? mencoes = null) => new()
        {
            Conversa = conversa, AutorId = autor.Id, AutorNome = autor.Nome, Texto = texto,
            EnviadaEm = hoje.AddHours(hora).AddMinutes(minuto), Mencoes = mencoes
        };

        var laudo = string.Join("\n\n",
            "Paciente em acompanhamento por lombalgia crônica há oito meses, com irradiação para o membro inferior esquerdo e piora aos esforços.",
            "Ao exame: dor à palpação da musculatura paravertebral lombar, Lasègue negativo bilateralmente, força e sensibilidade preservadas.",
            "Conduta: mantida a acupuntura semanal, orientação postural e alongamentos diários. Reavaliação em trinta dias com a escala EVA.",
            "Este laudo é o texto do recado; o documento numerado e assinado sai do Prontuário, que é o que vai ao convênio.");

        var direta = ChatEquipeRegras.Direta(carla.Id, medico.Id);
        db.MensagensEquipe.AddRange(
            M(ChatEquipeRegras.CanalGeral, otavio, 7, 55, "Bom dia, equipe! Hoje a agenda está cheia à tarde — quem puder, adianta as confirmações de amanhã."),
            M(ChatEquipeRegras.CanalGeral, medico, 8, 10, "Bom dia! Vou começar 15 minutos mais tarde, aviso o primeiro paciente."),
            M(ChatEquipeRegras.CanalRecepcao, otavio, 9, 2, $"@{carla.Nome.Split(' ')[0]} consegue ligar para a dona Marlene e remarcar a sessão de quinta?",
                ChatEquipeRegras.GravarMencoes([carla.Id])),
            M(direta, medico, 9, 40, "Carla, pode avisar o paciente das 10h que o laudo já está pronto? Mando aqui o texto."),
            M(direta, medico, 9, 41, laudo),
            M(direta, medico, 9, 43, "Se ele preferir, a 2ª via do documento sai pela recepção mesmo."));
        if (enfermeira is not null)
            db.MensagensEquipe.Add(M(ChatEquipeRegras.CanalEnfermagem, enfermeira, 8, 30, "Sala de infusão pronta. Faltam duas caixas de scalp 23 — já pedi ao estoque."));

        db.LeiturasEquipe.Add(new LeituraEquipe { UsuarioId = carla.Id, Conversa = ChatEquipeRegras.CanalGeral, UltimaLidaId = 0, LidaEm = hoje });
        db.LeiturasEquipe.Add(new LeituraEquipe { UsuarioId = carla.Id, Conversa = ChatEquipeRegras.CanalRecepcao, UltimaLidaId = 0, LidaEm = hoje });
        db.LeiturasEquipe.Add(new LeituraEquipe { UsuarioId = carla.Id, Conversa = direta, UltimaLidaId = 0, LidaEm = hoje });
        var agora = DateTime.Now;
        db.PresencasEquipe.Add(new PresencaEquipe { UsuarioId = medico.Id, VistoEm = agora });
        db.PresencasEquipe.Add(new PresencaEquipe { UsuarioId = otavio.Id, VistoEm = agora });
        db.SaveChanges();
    }
}
