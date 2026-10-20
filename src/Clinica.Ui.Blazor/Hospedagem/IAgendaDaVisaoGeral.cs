namespace Clinica.Ui.Blazor.Hospedagem;

/// <summary>O "abra a agenda neste DIA" que a ficha do paciente deixa na ponte (<c>PedidoAgenda</c> do WPF).</summary>
public sealed record PedidoDeDiaDaAgenda(DateOnly Dia, bool AplicarFiltro, int? ProfissionalId);

/// <summary>
/// O que a VISÃO GERAL DA AGENDA (tela Blazor, out/2026) pede a quem a hospeda: a NAVEGAÇÃO,
/// que só o módulo conhece — a aba Marcar já com o dia e o profissional, a aba Dia num dia, e o
/// pedido de dia que a ficha do paciente deixa.
///
/// A janela do horário e as ações de "Mais ações" (fechar a agenda, lista de espera, conciliar,
/// imprimir a folha) eram pedidas aqui ao <c>AgendaViewModel</c> WPF, que ficava montado sem
/// desenho só para isso. Desde out/2026 a tela abre os MESMOS formulários Blazor da Grade
/// (<c>HorarioDaAgendaForm</c>, <c>BloqueioForm</c>, <c>ListaEsperaPainelForm</c>,
/// <c>ConciliacaoAgendaForm</c>), e a ponte encolheu para o que é navegação. Onde a agenda não
/// existe (a prévia, os testes) vale <see cref="AgendaIndisponivel"/>, e a tela DIZ isso.
/// </summary>
public interface IAgendaDaVisaoGeral
{
    /// <summary>A aba Marcar existe neste aplicativo — sem ela, "Marcar atendimento" nasce apagado.</summary>
    bool MarcacaoDisponivel { get; }

    /// <summary>
    /// Abre a aba Marcar já com o dia (e a hora do vão, quando houver) e o profissional.
    /// Falso = não navegou, e nada ficou pendurado na ponte.
    /// </summary>
    Task<bool> MarcarAsync(DateTime dataHora, int? profissionalId);

    /// <summary>
    /// A marcação a partir da GRADE (out/2026): o vão clicado leva também a SALA da coluna, a
    /// DURAÇÃO e a MODALIDADE escolhidas no topo — redigitar é onde a hora sai errada. O padrão
    /// cai na marcação simples; a ponte da Recepção sobrescreve e leva tudo.
    /// </summary>
    Task<bool> MarcarAsync(DateTime dataHora, int? profissionalId, int? salaId, int? duracaoMinutos, string? modalidadeCodigo)
        => MarcarAsync(dataHora, profissionalId);

    /// <summary>Consome (LIMPA) o pedido de dia deixado por outra tela. Nulo quando não há.</summary>
    PedidoDeDiaDaAgenda? ConsumirPedidoDeDia();

    /// <summary>
    /// A célula da matriz (out/2026): abre a aba DIA da Agenda naquele dia, com o profissional
    /// filtrado. Falso = a aba não existe neste aplicativo (e nada ficou pendurado na ponte) — a
    /// tela então abre o dia nela mesma.
    /// </summary>
    Task<bool> AbrirDiaAsync(DateOnly dia, int? profissionalId);
}

/// <summary>A agenda de onde ela não existe: tudo responde "não disponível", e a tela diz.</summary>
public sealed class AgendaIndisponivel : IAgendaDaVisaoGeral
{
    public static readonly AgendaIndisponivel Instancia = new();

    public bool MarcacaoDisponivel => false;
    public Task<bool> MarcarAsync(DateTime dataHora, int? profissionalId) => Task.FromResult(false);
    public PedidoDeDiaDaAgenda? ConsumirPedidoDeDia() => null;
    public Task<bool> AbrirDiaAsync(DateOnly dia, int? profissionalId) => Task.FromResult(false);
}
