namespace Clinica.Domain.Entities;

// O acesso do tablet é diferente de um agendamento clínico. Só o paciente e o
// documento são obrigatórios; o cookie transporta um segredo, nunca estes dados.
public sealed class SessaoTablet
{
    public string Id { get; set; } = ""; // SHA-256 do token aleatório
    public int UsuarioId { get; set; }
    public UsuarioSistema? Usuario { get; set; }
    public string CredencialVersao { get; set; } = "";
    public string Dispositivo { get; set; } = "";
    public string Modo { get; set; } = "equipe";
    public long ExpiraEm { get; set; } // Unix milliseconds, UTC
    public long? AtividadeClinicaEm { get; set; }
    public Guid Versao { get; set; } = Guid.NewGuid();
}

public sealed class ColetaTablet
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string SessaoId { get; set; } = "";
    public SessaoTablet? Sessao { get; set; }
    public int DocumentoId { get; set; }
    public DocumentoClinico? Documento { get; set; }
    public int PacienteId { get; set; }
    public Paciente? Paciente { get; set; }
    public string Operadora { get; set; } = "";
    public string IdentidadeConferida { get; set; } = "";
    public string? ChaveAtiva { get; set; }
    public string Estado { get; set; } = "preparado";
    public string ConteudoJson { get; set; } = "";
    public string ConteudoHash { get; set; } = "";
    public string? SubmissaoJson { get; set; }
    public string? SubmissaoHash { get; set; }
    public byte[]? TracoPng { get; set; }
    public Guid? Idempotencia { get; set; }
    public long PreparadoEm { get; set; }
    public long ExpiraEm { get; set; }
    public long? RecebidoEm { get; set; }
    public long? FinalizadoEm { get; set; }
    public int Tentativas { get; set; }
    public string? Falha { get; set; } // código operacional, sem conteúdo clínico
    public Guid Versao { get; set; } = Guid.NewGuid();
}

/// <summary>Via original do paciente. Não representa certificado do profissional.</summary>
public sealed class ViaAssinadaPaciente
{
    public int DocumentoId { get; set; }
    public DocumentoClinico? Documento { get; set; }
    public Guid ColetaId { get; set; }
    public ColetaTablet? Coleta { get; set; }
    public byte[] Conteudo { get; set; } = [];
    public string Sha256 { get; set; } = "";
    public string EvidenciaJson { get; set; } = "";
    public string EvidenciaSha256 { get; set; } = "";
    public long ArquivadoEm { get; set; }
}
