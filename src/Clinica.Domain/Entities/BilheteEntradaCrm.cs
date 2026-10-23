namespace Clinica.Domain.Entities;

// O bilhete de entrada no CRM pela Recepção (out/2026, docs/crm-na-recepcao.md): o app grava,
// a ponte confere e marca como usado. O bilhete em si só existe na URL da moldura; aqui fica o
// SHA-256 dele, para quem só lê o banco não conseguir entrar no CRM com o que leu.

/// <summary>Um bilhete de uso único que leva quem já entrou no app para dentro do CRM.</summary>
public sealed class BilheteEntradaCrm
{
    public int Id { get; set; }

    /// <summary>SHA-256 (hex minúsculo) do bilhete.</summary>
    public string TokenHash { get; set; } = "";

    /// <summary>Quem estava logado no app quando a aba abriu — e o único login que o bilhete abre.</summary>
    public int UsuarioId { get; set; }

    public DateTime CriadoEm { get; set; }

    /// <summary>Depois disso (mais a folga do relógio) o bilhete não abre mais.</summary>
    public DateTime VenceEm { get; set; }

    /// <summary>Quando a ponte o aceitou. Preenchido uma vez só: é o "uso único".</summary>
    public DateTime? UsadoEm { get; set; }
}
