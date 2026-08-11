using Clinica.Domain.Entities;

namespace Clinica.Desktop.ViewModels;

/// <summary>
/// Um agendamento já preparado para a tela: nomes resolvidos pelo catálogo, retrato do
/// paciente e a situação quebrada em sinalizadores, para o XAML só ligar gatilhos em vez
/// de comparar enums.
/// </summary>
public sealed record CartaoAgendamento(
    Agendamento Item,
    string Hora,
    string Paciente,
    string Modalidade,
    string Situacao,
    byte[]? Foto,
    string Convenio,
    bool CarteirinhaVencida,
    string? Observacoes,
    bool Agendado,
    bool Realizado,
    bool Faltou,
    bool Cancelado,
    string? SeloConsulta = null,
    string? AvisoConsulta = null,
    bool ConsultaVencida = false)
{
    /// <summary>Cancelado ou falta: some do primeiro plano (opacidade menor no cartão).</summary>
    public bool Encerrado => Cancelado || Faltou;

    /// <summary>
    /// Há consulta renovável a cobrar deste paciente. A frase vem pronta do
    /// <c>StatusConsultaPaciente</c> — a tela não decide o que é "a renovar", senão a
    /// agenda passaria a discordar da aba Consultas sobre o mesmo paciente.
    /// </summary>
    public bool TemAvisoConsulta => SeloConsulta is not null;

    /// <summary>
    /// Quem LANÇOU o horário, e quando (parcela 58) — na dica do cartão.
    ///
    /// A direção pediu para ver de quem é cada lançamento. A trilha de auditoria responde
    /// isso desde a parcela 21, mas ela é outra tela, filtrada por período; a pergunta que
    /// se faz olhando a agenda é sobre AQUELA linha, agora.
    ///
    /// Horário anterior à parcela 58 não guarda o dado, e a frase DIZ isso: em branco não
    /// se distingue de "não carregou".
    /// </summary>
    public string Lancamento => string.IsNullOrWhiteSpace(Item.CriadoPor)
        ? "Marcado antes de o sistema passar a registrar quem lança — sem autoria."
        : Item.CriadoEm is { } quando
            ? $"Marcado por {Item.CriadoPor} em {quando:dd/MM/yyyy HH:mm}"
            : $"Marcado por {Item.CriadoPor}";
}

/// <summary>
/// Uma faixa de horário da visão de dia. Sem agendamento, a faixa vira um convite a
/// marcar — clicar nela abre o cadastro já com o horário preenchido.
/// </summary>
public sealed record FaixaHorario(
    string Hora,
    DateTime Inicio,
    IReadOnlyList<CartaoAgendamento> Itens)
{
    public bool Livre => Itens.Count == 0;

    /// <summary>Faixa de hora cheia (:00) ganha divisória mais forte na grade.</summary>
    public bool HoraCheia => Inicio.Minute == 0;
}

/// <summary>Uma coluna da visão de semana (um dia, com seus agendamentos empilhados).</summary>
public sealed record ColunaDia(
    string DiaSemana,
    string DataCurta,
    DateTime Data,
    bool EhHoje,
    IReadOnlyList<CartaoAgendamento> Itens)
{
    public bool Vazio => Itens.Count == 0;
}
