namespace Clinica.Domain.Entities;

// O CHAT DA EQUIPE (out/2026, mockup v3 aprovado — docs/mockups/chat-da-equipe.html).
//
// Três canais fixos (# Geral, # Recepção, # Enfermagem) e conversa direta entre quaisquer
// duas pessoas. A conversa é uma CHAVE de texto, não uma tabela: os canais são fixos e a
// direta é o par de usuários ("direta:3:7", menor id primeiro) — não há conversa a "criar",
// e a mesma direta é encontrada de qualquer app em que a pessoa entre.
//
// Nada se apaga: a mensagem editada guarda a versão anterior (VersaoMensagemEquipe), e não
// existe exclusão. Mensagem pode citar paciente e anexar documento clínico; a trilha de quem
// escreveu e quando é a própria linha.

/// <summary>Uma mensagem do chat da equipe.</summary>
public class MensagemEquipe
{
    public int Id { get; set; }

    /// <summary>"canal:geral", "canal:recepcao", "canal:enfermagem" ou "direta:{menor}:{maior}".</summary>
    public string Conversa { get; set; } = string.Empty;

    public int AutorId { get; set; }
    public UsuarioSistema? Autor { get; set; }

    /// <summary>Nome do autor COPIADO no envio: renomear o usuário não reescreve o que ele disse.</summary>
    public string AutorNome { get; set; } = string.Empty;

    /// <summary>O texto, sem limite de tamanho (o laudo cabe inteiro).</summary>
    public string Texto { get; set; } = string.Empty;

    public DateTime EnviadaEm { get; set; }

    /// <summary>Preenchido quando o autor corrigiu o texto; a versão anterior fica em <see cref="Versoes"/>.</summary>
    public DateTime? EditadaEm { get; set; }

    /// <summary>Ids citados com @, entre barras ("|3|7|") — é o que decide o som de quem foi citado.</summary>
    public string? Mencoes { get; set; }

    /// <summary>Paciente anexado (quem recebe abre a ficha com um clique).</summary>
    public int? PacienteId { get; set; }
    public Paciente? Paciente { get; set; }
    public string? PacienteNome { get; set; }

    /// <summary>Documento clínico anexado (o laudo emitido; abre a 2ª via).</summary>
    public int? DocumentoClinicoId { get; set; }
    public DocumentoClinico? DocumentoClinico { get; set; }
    public string? DocumentoRotulo { get; set; }

    /// <summary>Arquivo anexado — os bytes moram em <see cref="ArquivoMensagemEquipe"/>.</summary>
    public string? ArquivoNome { get; set; }
    public long? ArquivoTamanho { get; set; }

    public ArquivoMensagemEquipe? Arquivo { get; set; }
    public List<VersaoMensagemEquipe> Versoes { get; set; } = [];
}

/// <summary>Os bytes do arquivo anexado — fora da linha da mensagem, para a lista não arrastá-los.</summary>
public class ArquivoMensagemEquipe
{
    public int MensagemId { get; set; }
    public MensagemEquipe? Mensagem { get; set; }
    public byte[] Conteudo { get; set; } = [];
}

/// <summary>O que a mensagem dizia antes de uma correção — editar não apaga.</summary>
public class VersaoMensagemEquipe
{
    public int Id { get; set; }
    public int MensagemId { get; set; }
    public MensagemEquipe? Mensagem { get; set; }
    public string Texto { get; set; } = string.Empty;
    /// <summary>Quando este texto deixou de valer.</summary>
    public DateTime SubstituidaEm { get; set; }
}

/// <summary>Até onde uma pessoa leu uma conversa — é o "Novas mensagens" e o "Lida por".</summary>
public class LeituraEquipe
{
    public int Id { get; set; }
    public int UsuarioId { get; set; }
    public string Conversa { get; set; } = string.Empty;
    public int UltimaLidaId { get; set; }
    public DateTime LidaEm { get; set; }
}

/// <summary>
/// Presença e "está escrevendo…": uma linha por pessoa, gravada no máximo a cada poucos
/// segundos e lida com validade — quem fechou o app some sozinho, sem ninguém apagar nada.
/// </summary>
public class PresencaEquipe
{
    public int UsuarioId { get; set; }
    public DateTime VistoEm { get; set; }
    public string? EscrevendoEm { get; set; }
    public DateTime? EscrevendoAte { get; set; }
}
