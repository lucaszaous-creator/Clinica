using Clinica.Application.Abstracoes;
using Clinica.Domain.Entities;

namespace Clinica.Application.Servicos;

/// <summary>Quem pode responder à pergunta: um login ATIVO ligado a um profissional da agenda.</summary>
public sealed record ProfissionalDoChat(int UsuarioId, string Nome, int ProfissionalId);

/// <summary>
/// "PERGUNTAR AO PROFISSIONAL" (25/10, R10): o paciente pergunta algo clínico pelo WhatsApp (dor, reação ao
/// tratamento) e a secretária leva a pergunta a quem trata num clique — uma mensagem DIRETA no chat da equipe,
/// com o paciente ANEXADO (quem recebe abre a ficha dali) e o trecho que o paciente escreveu. Antes eram ~8–10
/// cliques e um copiar-e-colar, e "de quem é esse paciente?" dependia da memória.
/// <list type="bullet">
/// <item><b>Nada sai do banco da clínica</b> e não há IA: é o chat da equipe (art. 33 não se aplica).</item>
/// <item><b>O relato NÃO vira registro de prontuário</b> (decisão de 25/10): registrar é ato médico; quem trata
/// lê no chat e, se for o caso, registra. A tela diz isso no formulário.</item>
/// <item>As barreiras do anexo de paciente são as do chat (<see cref="IChatEquipeService.EnviarAsync"/>:
/// sem <c>VerFichaPaciente</c>, o paciente não vai anexado).</item>
/// </list>
/// </summary>
public sealed class PerguntaAoProfissionalService(IClinicaRepositorio repo, IChatEquipeService chat)
{
    /// <summary>O trecho citado tem teto: a pergunta é a da conversa, não a conversa inteira.</summary>
    public const int TetoDoTrecho = 1200;

    /// <summary>A frase curta do formulário sobre o prontuário (o pedido da direção: dizer, curto).</summary>
    public const string NaoViraProntuario = "Vai só pelo chat da equipe. Não entra no prontuário: se for o caso, quem trata registra.";

    /// <summary>Os profissionais que têm login ativo (menos quem pergunta), pelo nome. Uma ida ao banco.</summary>
    public async Task<IReadOnlyList<ProfissionalDoChat>> QuemPodeResponderAsync(CancellationToken ct = default)
    {
        var eu = SessaoUsuario.Atual.UsuarioId;
        return (await repo.UsuariosAsync(ct))
            .Where(u => u.Ativo && u.ProfissionalId is not null && u.Id != eu)
            .OrderBy(u => u.Nome, StringComparer.Create(new System.Globalization.CultureInfo("pt-BR"), ignoreCase: true))
            .Select(u => new ProfissionalDoChat(u.Id, u.Nome, u.ProfissionalId!.Value))
            .ToList();
    }

    /// <summary>
    /// Quem a tela sugere: o profissional da PRÓXIMA sessão do paciente; sem ela, o do caso do Acompanhamento
    /// (quem tratava). Sem login para nenhum dos dois, ninguém: a secretária escolhe.
    /// </summary>
    public static int? Sugestao(IReadOnlyList<ProfissionalDoChat> quem, int? profissionalDaProximaSessao, int? profissionalDoCaso)
        => new[] { profissionalDaProximaSessao, profissionalDoCaso }
            .Where(p => p is not null)
            .Select(p => quem.FirstOrDefault(q => q.ProfissionalId == p)?.UsuarioId)
            .FirstOrDefault(u => u is not null);

    /// <summary>
    /// O texto que vai no chat: de onde veio, o trecho citado (com teto) e, se houver, o que a secretária quer
    /// saber. Sem a ficha conferida, o nome é o do WhatsApp e o texto diz isso — o paciente não vai anexado.
    /// </summary>
    public static string Texto(string nome, bool fichaConferida, string trecho, string? pergunta)
    {
        var t = (trecho ?? "").Replace("\r\n", "\n").Trim();
        if (t.Length > TetoDoTrecho) t = t[..TetoDoTrecho].TrimEnd() + "…";
        var linhas = new List<string>
        {
            $"Pergunta pelo WhatsApp da clínica — {nome.Trim()}{(fichaConferida ? "" : " (ficha ainda não conferida)")}:",
            string.Join("\n", t.Split('\n').Select(l => "> " + l))
        };
        if (!string.IsNullOrWhiteSpace(pergunta)) linhas.Add(pergunta.Trim());
        return string.Join("\n\n", linhas);
    }

    /// <summary>A confirmação passageira: para quem foi e onde a resposta chega.</summary>
    public static string Enviada(string profissional) => $"Pergunta enviada a {profissional} pelo chat da equipe. A resposta chega lá.";

    /// <summary>Manda a pergunta na direta com <paramref name="profissional"/>. Devolve a chave da conversa do chat.</summary>
    public async Task<string> PerguntarAsync(ProfissionalDoChat profissional, int? pacienteId, string texto, CancellationToken ct = default)
    {
        var eu = SessaoUsuario.Atual.UsuarioId;
        if (eu <= 0) throw new InvalidOperationException("Entre no sistema para usar o chat da equipe.");
        if (string.IsNullOrWhiteSpace(texto)) throw new InvalidOperationException("A pergunta está vazia.");
        var conversa = ChatEquipeRegras.Direta(eu, profissional.UsuarioId);
        await chat.EnviarAsync(eu, conversa, new NovaMensagemEquipe(texto, PacienteId: pacienteId), ct);
        return conversa;
    }
}
