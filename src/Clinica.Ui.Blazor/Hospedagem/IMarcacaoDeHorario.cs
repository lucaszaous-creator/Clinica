namespace Clinica.Ui.Blazor.Hospedagem;

/// <summary>
/// "Marcar horário" a partir de uma tela Blazor (out/2026, Acompanhamento de pacientes).
///
/// Quem marca é a aba Marcar da RECEPÇÃO (o Novo atendimento, WPF), que esta biblioteca não
/// enxerga — e não pode enxergar: `Clinica.Ui.Blazor` é `net8.0` puro e roda na prévia em
/// Linux. Por isso a tela pede a quem a hospeda, como pede o disco e a navegação ao
/// <see cref="IHostDaTela"/>. A Recepção registra a implementação de verdade (a ponte
/// `PreenchimentoNovoAtendimento` + a navegação para a aba Marcar); onde ela não existe — a
/// prévia, os testes, o app do Faturamento sem a agenda — vale
/// <see cref="MarcacaoIndisponivel"/>, e quem chama DIZ isso: botão que não navega em
/// silêncio é o defeito da parcela 37.
/// </summary>
public interface IMarcacaoDeHorario
{
    /// <summary>
    /// Abre a marcação já com o paciente (e o profissional e a modalidade, quando se sabe).
    /// Falso = a agenda não está disponível neste aplicativo, e nada ficou pendurado.
    /// </summary>
    Task<bool> MarcarAsync(int pacienteId, int? profissionalId, string? modalidadeCodigo);

    /// <summary>
    /// Marcação já num DIA (out/2026, "Retornos a marcar": a data sugerida por quem atendeu).
    /// O padrão cai na marcação sem dia — abre em hoje e a recepção troca a data; quem
    /// hospeda e sabe pré-preencher o dia (a ponte da Recepção) sobrescreve este método.
    /// </summary>
    Task<bool> MarcarAsync(int pacienteId, int? profissionalId, string? modalidadeCodigo, DateOnly dia)
        => MarcarAsync(pacienteId, profissionalId, modalidadeCodigo);
}

/// <summary>A marcação de onde não há agenda: responde "não disponível", e a tela diz.</summary>
public sealed class MarcacaoIndisponivel : IMarcacaoDeHorario
{
    public static readonly MarcacaoIndisponivel Instancia = new();

    public Task<bool> MarcarAsync(int pacienteId, int? profissionalId, string? modalidadeCodigo) => Task.FromResult(false);
}
