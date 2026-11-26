using Clinica.Domain;
using Clinica.Domain.Entities;

namespace Clinica.Application.Modelos;

/// <summary>O que a recepção informa para reservar sem ficha: só o mínimo (LGPD) — sem CPF, sem carteirinha.</summary>
public sealed record NovaReserva(
    string Nome, string Telefone, string? ConversaId,
    int ProfissionalId, int? SalaId, DateTime DataHora, int? DuracaoMinutos,
    string ModalidadeCodigo, string? EspecialidadeConsultaCodigo,
    bool Particular, string? ConvenioNome);

/// <summary>Como a reserva vira paciente: uma ficha que já existe ("Já tem ficha") OU uma nova ("Paciente novo").</summary>
public sealed record ComoVirarPaciente(int? PacienteExistenteId, Paciente? Novo);

/// <summary>
/// As regras da RESERVA DE HORÁRIO SEM FICHA (nov/2026), num lugar só e com teste — a tela dos apps, a ponte e o CRM
/// perguntam aqui. Ver <see cref="ReservaDeHorario"/>.
/// </summary>
public static class ReservaDeHorarioRegras
{
    /// <summary>
    /// Quantas reservas ABERTAS (futuras, reservadas) um telefone pode ter ao mesmo tempo. Sugestão do Design SemDor (2),
    /// pendente da decisão do dono — mude só aqui.
    /// </summary>
    public const int LimiteAbertasPorTelefone = 2;

    /// <summary>O nome como fica gravado: sem espaços duplicados, até <see cref="ReservaDeHorario.NomeMaximo"/>.</summary>
    public static string NomeLimpo(string? nome)
        => string.Join(' ', (nome ?? "").Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));

    /// <summary>A frase da recusa, ou nula quando o pedido é válido (o conflito de agenda é conferido depois, no banco).</summary>
    public static string? Recusa(NovaReserva n, DateTime agora)
    {
        var nome = NomeLimpo(n.Nome);
        if (nome.Length == 0) return "Informe o nome que a pessoa deu na conversa.";
        if (nome.Length > ReservaDeHorario.NomeMaximo) return $"O nome aceita até {ReservaDeHorario.NomeMaximo} caracteres.";
        if (FichaPeloTelefone.Normalizar(n.Telefone).Length == 0) return "A reserva precisa do telefone da conversa.";
        if (n.DataHora <= agora) return "Escolha um horário que ainda não passou.";
        if (n.DuracaoMinutos is < 5 or > 240) return "Duração inválida para a reserva.";
        if (string.IsNullOrWhiteSpace(n.ModalidadeCodigo)) return "Escolha o atendimento.";
        if (!n.Particular && string.IsNullOrWhiteSpace(n.ConvenioNome)) return "Diga se é particular ou qual convênio.";
        if (n.Particular && !string.IsNullOrWhiteSpace(n.ConvenioNome)) return "Particular não leva convênio.";
        if ((n.ConvenioNome?.Trim().Length ?? 0) > ReservaDeHorario.ConvenioMaximo)
            return $"O convênio aceita até {ReservaDeHorario.ConvenioMaximo} caracteres.";
        return null;
    }

    /// <summary>A frase quando o telefone já tem o máximo de reservas abertas.</summary>
    public static string? RecusaPeloLimite(int abertasDoTelefone)
        => abertasDoTelefone >= LimiteAbertasPorTelefone
            ? $"Este telefone já tem {abertasDoTelefone} reservas abertas. Desmarque uma ou faça o cadastro (\"Virar paciente\")."
            : null;

    /// <summary>
    /// Os choques que RECUSAM a reserva: outro horário do mesmo profissional ou da mesma sala, outra reserva e agenda
    /// fechada. Fora da jornada só recusa na agenda protegida (o que já vem como <c>ImpedeMarcar</c>) — a mesma
    /// tolerância da marcação comum. Reserva não é encaixe: ela não fica por cima de ninguém.
    /// </summary>
    public static bool RecusaReserva(ConflitoAgenda c)
        => c.ImpedeMarcar || c.Recurso is RecursoAgenda.Profissional or RecursoAgenda.Sala
            or RecursoAgenda.Reserva or RecursoAgenda.Bloqueio;

    /// <summary>O motivo vale? (só os três combinados com a tela).</summary>
    public static bool MotivoValido(string? motivo) => motivo is not null && ReservaDeHorario.MotivosDeDesmarcar.Contains(motivo);

    /// <summary>Particular ou o convênio dito, para a grade e a conversa.</summary>
    public static string Atendimento(ReservaDeHorario r) => r.Particular ? "Particular" : r.ConvenioNome ?? "Convênio";

    /// <summary>
    /// A trilha de auditoria SEM o nome nem o telefone da pessoa (dado mínimo): o número da reserva, o horário e quem
    /// atende. Quem precisa do nome abre a reserva — e abrir também fica na trilha.
    /// </summary>
    public static string Trilha(ReservaDeHorario r, string acao)
        => $"Reserva sem ficha nº {r.Id}: {acao} — {r.DataHora:dd/MM/yyyy HH:mm} com {r.Profissional?.Rotulo ?? $"profissional {r.ProfissionalId}"}.";
}

/// <summary>
/// A reserva de horário sem ficha (nov/2026). O serviço usa o banco direto, numa transação, porque converte numa tacada
/// só (ficha + agendamento + reserva) — o padrão do <c>LiberacaoDeTravaService</c>. Roda nos apps e na ponte do CRM.
/// </summary>
public interface IReservaDeHorarioService
{
    Task<ReservaDeHorario> ReservarAsync(NovaReserva nova, string operador, CancellationToken ct = default);
    Task<ReservaDeHorario> RemarcarAsync(int reservaId, DateTime novoInicio, string operador, CancellationToken ct = default);
    Task<ReservaDeHorario> DesmarcarAsync(int reservaId, string motivo, string operador, CancellationToken ct = default);
    Task<ReservaDeHorario> NaoCompareceuAsync(int reservaId, string operador, CancellationToken ct = default);
    Task<Agendamento> VirarPacienteAsync(int reservaId, ComoVirarPaciente como, string operador, CancellationToken ct = default);
    Task<IReadOnlyList<ReservaDeHorario>> DoDiaAsync(DateOnly dia, CancellationToken ct = default);
    Task<IReadOnlyList<ReservaDeHorario>> DoTelefoneAsync(string telefone, string operador, CancellationToken ct = default);
}
