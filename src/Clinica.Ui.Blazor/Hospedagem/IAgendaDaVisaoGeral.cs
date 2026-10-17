namespace Clinica.Ui.Blazor.Hospedagem;

/// <summary>As ações de "Mais ações" da Visão geral da agenda — cada uma tem dono no WPF.</summary>
public enum AcaoDaAgenda
{
    /// <summary>Fechar a agenda (férias, feriado, folga).</summary>
    FecharAgenda,
    /// <summary>Horários e travas dos profissionais.</summary>
    HorariosETravas,
    /// <summary>Lista de espera.</summary>
    ListaDeEspera,
    /// <summary>Conciliar horários parados.</summary>
    ConciliarHorarios,
    /// <summary>Imprimir a folha do dia.</summary>
    ImprimirFolhaDoDia
}

/// <summary>
/// O desfecho de uma ação feita pela agenda do WPF: se ela aconteceu, e o recado que a agenda
/// deixou (a mesma <c>Mensagem</c>/<c>MensagemEhErro</c> do <c>AgendaViewModel</c>).
/// </summary>
public sealed record RespostaDaAgenda(bool Executou, string? Mensagem = null, bool EhErro = false)
{
    public static readonly RespostaDaAgenda Indisponivel =
        new(false, "Esta ação da agenda não está disponível neste aplicativo.", true);
}

/// <summary>O "abra a agenda neste DIA" que a ficha do paciente deixa na ponte (<c>PedidoAgenda</c> do WPF).</summary>
public sealed record PedidoDeDiaDaAgenda(DateOnly Dia, bool AplicarFiltro, int? ProfissionalId);

/// <summary>
/// O que a VISÃO GERAL DA AGENDA (tela Blazor, out/2026) pede a quem a hospeda.
///
/// A janela do horário (remarcar, reabrir, comprovante, WhatsApp…), as cinco ações de
/// "Mais ações" e a marcação no vão livre têm DONO no WPF — o <c>AgendaViewModel</c> e a aba
/// Marcar (Novo atendimento). Reescrevê-las aqui seria a segunda cópia de sete ações com
/// permissão, aviso e recarga (a regra do "dono sem desenho", out/2026). Por isso a tela
/// pede ao hospedeiro, como pede o disco ao <see cref="IHostDaTela"/>; onde a agenda não
/// existe (a prévia, os testes) vale <see cref="AgendaIndisponivel"/>, e a tela DIZ isso.
/// </summary>
public interface IAgendaDaVisaoGeral
{
    /// <summary>A aba Marcar existe neste aplicativo — sem ela, "Marcar atendimento" nasce apagado.</summary>
    bool MarcacaoDisponivel { get; }

    /// <summary>A janela do horário. <c>Executou=false</c> com recado quando o horário saiu do dia.</summary>
    Task<RespostaDaAgenda> AbrirHorarioAsync(DateOnly dia, int agendamentoId);

    /// <summary>Uma ação de "Mais ações", com a agenda posicionada no dia e no profissional da tela.</summary>
    Task<RespostaDaAgenda> ExecutarAsync(AcaoDaAgenda acao, DateOnly dia, int? profissionalId);

    /// <summary>
    /// Abre a aba Marcar já com o dia (e a hora do vão, quando houver) e o profissional.
    /// Falso = não navegou, e nada ficou pendurado na ponte.
    /// </summary>
    Task<bool> MarcarAsync(DateTime dataHora, int? profissionalId);

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
    public Task<RespostaDaAgenda> AbrirHorarioAsync(DateOnly dia, int agendamentoId) => Task.FromResult(RespostaDaAgenda.Indisponivel);
    public Task<RespostaDaAgenda> ExecutarAsync(AcaoDaAgenda acao, DateOnly dia, int? profissionalId) => Task.FromResult(RespostaDaAgenda.Indisponivel);
    public Task<bool> MarcarAsync(DateTime dataHora, int? profissionalId) => Task.FromResult(false);
    public PedidoDeDiaDaAgenda? ConsumirPedidoDeDia() => null;
    public Task<bool> AbrirDiaAsync(DateOnly dia, int? profissionalId) => Task.FromResult(false);
}
