using Clinica.Domain;

namespace Clinica.Application.Tablet;

/// <summary>
/// A fila da enfermagem (BSV e BSV + acupuntura) vista de fora da Infrastructure (out/2026):
/// a tela Blazor do Consultório e o portal do tablet leem pela mesma interface, e quem consulta
/// o banco continua sendo o <c>SessoesEnfermagemService</c>.
/// </summary>
public interface ISessoesEnfermagem
{
    Task<SessoesEnfermagemPagina> ListarAsync(int usuarioId, FiltroSessoesEnfermagem filtro, DateOnly hoje, CancellationToken ct = default);
}

public sealed record FiltroSessoesEnfermagem(string? Paciente = null, DateOnly? Inicio = null,
    DateOnly? Fim = null, string? Medico = null, string Situacao = "Hoje", int Pagina = 0);
public sealed record SessaoEnfermagemItem(int Id, int PacienteId, string Paciente, DateTime DataHora,
    string Medico, ModalidadeAtendimento Modalidade, int? AtendimentoId, bool ChegadaRegistrada,
    bool AposAplicacaoRegistrada, bool RegistroLegado, bool Concluida)
{
    public bool Registrada => RegistroLegado || ChegadaRegistrada && AposAplicacaoRegistrada;
    public string ModalidadeTexto => Modalidade == ModalidadeAtendimento.BsvApenas ? "BSV" : "BSV + acupuntura";
    public string Situacao => RegistroLegado ? "Registro anterior às fases — conferir no prontuário"
        : Registrada ? "Chegada e após aplicação registrados"
        : ChegadaRegistrada ? "Chegada registrada · após aplicação pendente"
        : AposAplicacaoRegistrada ? "Após aplicação registrada · chegada pendente"
        : "Chegada e após aplicação pendentes";
    public string Atendimento => Concluida ? "Concluído — permite registro tardio" : "Aberto";
}
public sealed record SessoesEnfermagemPagina(IReadOnlyList<SessaoEnfermagemItem> Itens, bool Mais);


/// <summary>O que a tela de Sessões de enfermagem AFIRMA: as situações do filtro, o tom do selo e o rodapé da página.</summary>
public static class SessoesEnfermagemNaTela
{
    public const string SituacaoPadrao = "DiaEPendentes";

    /// <summary>Os códigos são os que o serviço aceita; o nome é o que a tela mostra.</summary>
    public static readonly IReadOnlyList<(string Codigo, string Nome)> Situacoes =
    [
        ("DiaEPendentes", "Hoje e pendentes anteriores"),
        ("Hoje", "Sessões de hoje"),
        ("Pendentes", "Evoluções pendentes"),
        ("Registradas", "Evoluções registradas"),
        ("Todas", "Todas no período"),
    ];

    /// <summary>Registrada é sucesso; o registro anterior às fases pede conferência; o resto falta.</summary>
    public static string Tom(SessaoEnfermagemItem s)
        => s.RegistroLegado ? "info" : s.Registrada ? "sucesso" : "aviso";

    /// <summary>"Página 2 · 50 sessões · há mais" — a página cheia diz que existe a seguinte.</summary>
    public static string Resumo(int pagina, int quantidade, bool mais)
        => $"Página {pagina + 1} · {quantidade} sessão(ões){(mais ? " · há mais na próxima página" : "")}";
}
