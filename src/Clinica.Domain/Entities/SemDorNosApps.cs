namespace Clinica.Domain.Entities;

// O @SemDor com IA nos apps (out/2026 — Clinica#307, docs/semdor-assistente.md, "IA nos apps").
// A IA mora só no CRM (VPS 2); a chave nunca vem às máquinas da clínica. O que liga os dois lados
// são estas três tabelas, lidas e gravadas pela ponte:
// - a pergunta do chat dos apps que as regras não entenderam espera aqui até o CRM reescrevê-la;
// - o aviso "Atenção" do sino do CRM (o que o @SemDor leu como urgente) chega ao sino da Recepção;
// - o pulso diz se o CRM está de fato passando — sem ele, o app não promete uma releitura.

/// <summary>Uma pergunta ao @SemDor que as regras não entenderam, à espera da releitura pelo CRM.</summary>
public sealed class PerguntaAoSemDor
{
    public int Id { get; set; }

    /// <summary>Quem perguntou: a resposta sai com as permissões DESTE login.</summary>
    public int UsuarioId { get; set; }

    /// <summary>Onde a resposta vai (a conversa com o sistema, ou o canal quando todos ali podem vê-la).</summary>
    public string Conversa { get; set; } = "";

    /// <summary>O texto como foi escrito (sem a menção ao @SemDor). É o que vai à IA.</summary>
    public string Texto { get; set; } = "";

    public DateTime CriadaEm { get; set; }

    /// <summary>aguardando · lendo (o CRM pegou) · respondida · sem-resposta.</summary>
    public string Situacao { get; set; } = "aguardando";

    /// <summary>A pergunta conhecida que a IA devolveu; nula quando ela não achou nenhuma.</summary>
    public string? Reescrita { get; set; }

    public DateTime? ResolvidaEm { get; set; }
}

/// <summary>
/// Um aviso "Atenção" do CRM no sino da Recepção: a mensagem de paciente que o @SemDor leu como
/// sintoma grave, reação ao tratamento ou reclamação, e que a clínica ainda não respondeu. A tabela
/// guarda só os que valem AGORA (o CRM manda a lista inteira e o que saiu dela sai daqui): não é
/// registro clínico, é aviso — a conversa continua no CRM.
/// </summary>
public sealed class AvisoDoSemDor
{
    /// <summary>A chave do aviso no CRM ("semdor:…"); é também a chave do visto/lido no sino.</summary>
    public string Chave { get; set; } = "";

    /// <summary>Só o primeiro nome: o sino aparece na tela do balcão.</summary>
    public string PrimeiroNome { get; set; } = "";

    /// <summary>O assunto como o CRM o escreve ("Sintoma grave", "Reclamação").</summary>
    public string Assunto { get; set; } = "";

    /// <summary>Quando o paciente escreveu (hora da clínica).</summary>
    public DateTime Em { get; set; }

    /// <summary>O endereço da conversa no CRM (https), que o clique abre no navegador.</summary>
    public string Link { get; set; } = "";
}

/// <summary>
/// O pulso do CRM, numa linha só (<see cref="Id"/> = 1): quando ele leu a fila de perguntas e quando
/// mandou os avisos. O app só promete "o @SemDor está relendo" se o CRM passou há pouco.
/// </summary>
public sealed class SemDorNoCrm
{
    public const int Unico = 1;

    public int Id { get; set; } = Unico;

    /// <summary>A última vez que o CRM leu a fila de perguntas (só lê com a IA ligada).</summary>
    public DateTime? PerguntasLidasEm { get; set; }

    /// <summary>A última lista de avisos recebida (o sino da Recepção mostra a hora quando ela envelhece).</summary>
    public DateTime? AvisosRecebidosEm { get; set; }
}
