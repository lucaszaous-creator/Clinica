using Clinica.Application.Servicos;
using Clinica.Domain;
using Clinica.Domain.Entities;

namespace Clinica.Application.Modelos;

/// <summary>
/// Uma linha do dia do profissional — a do "Meu dia" (out/2026: era a <c>LinhaSessao</c> do
/// ViewModel WPF; a tela virou Blazor e o que a linha AFIRMA desceu para cá, com teste).
/// </summary>
public sealed record LinhaMeuDia
{
    public required int AgendamentoId { get; init; }

    /// <summary>Dia do horário — viaja para o posto porque o casamento sessão × evolução cai para paciente + data.</summary>
    public required DateOnly Data { get; init; }
    public required int PacienteId { get; init; }
    public required string Paciente { get; init; }

    /// <summary>"08:00–08:30": o término é a metade que decide se cabe um encaixe.</summary>
    public required string Hora { get; init; }
    public required string Modalidade { get; init; }
    public required ModalidadeAtendimento ModalidadeFamilia { get; init; }

    /// <summary>A sala, ou VAZIO quando o horário não tem uma — nunca o travessão.</summary>
    public required string Local { get; init; }
    public required bool EvolucaoEscrita { get; init; }
    public required bool Encaixe { get; init; }

    /// <summary>A paciente vem para colher preventivo (out/2026): selo na linha, ao lado do nome.</summary>
    public bool ColetaPreventivo { get; init; }
    public int? AtendimentoId { get; init; }
    public required EtapaFila Etapa { get; init; }

    /// <summary>O status GRAVADO do horário — é ele que separa cancelado e falta do resto.</summary>
    public required StatusAgendamento Estado { get; init; }

    /// <summary>O recado que a recepção escreveu no horário. Vazio quando não há.</summary>
    public required string Observacoes { get; init; }

    /// <summary>A linha é de HOJE — a fila só corre hoje (o carimbo de entrada também).</summary>
    public required bool EhHoje { get; init; }

    public DateTime? ChegadaEm { get; init; }
    public DateTime? InicioEm { get; init; }
    public DateTime? FimEm { get; init; }
    public int? EsperaMinutos { get; init; }
    public int? ChamadoHaMinutos { get; init; }

    /// <summary>
    /// A chamada está pendente há tempo demais. ⛔ DORMENTE desde set/2026 (sem os botões de fila
    /// ninguém carimba a chamada) — volta a valer com o check-in, sem uma linha nova.
    /// </summary>
    public bool ChamadaDemorada => ChamadoHaMinutos >= MeuDiaNaTela.ChamadaDemoradaMinutos;

    /// <summary>Cancelado, falta ou substituído — a linha FICA, apagada e marcada (a regra da folha do dia).</summary>
    public bool ForaDaFila => StatusDaFila.ForaDaFila(Estado);

    /// <summary>A linha sob o nome: modalidade · sala (a sala some quando não há — nunca "sala —").</summary>
    public string Contexto => string.IsNullOrWhiteSpace(Local) ? Modalidade : $"{Modalidade} · sala {Local}";

    /// <summary>A coluna STATUS — o vocabulário de <see cref="StatusDaFila"/>, o MESMO da lista do balcão.</summary>
    public string Status => StatusDaFila.Palavra(Estado, Etapa);

    /// <summary>O grupo visual da situação — a cor da linha e a pílula do filtro.</summary>
    public string GrupoVisual => SituacaoVisualAgenda.Grupo(Estado, Etapa);

    /// <summary>A hora do fato abaixo do status ("chegou às 14:40 · espera 12 min").</summary>
    public string StatusDetalhe => StatusDaFila.Detalhe(Estado, Etapa, ChegadaEm, EsperaMinutos, ChamadoHaMinutos, InicioEm, FimEm);

    /// <summary>A coluna PRONTUÁRIO — "Pendente" inclusive para o que ainda vai acontecer; travessão no cancelado.</summary>
    public string Prontuario => ForaDaFila ? "—" : EvolucaoEscrita ? "Escrito" : "Pendente";

    /// <summary>Pinta o "Pendente" — só quando há sessão para escrever.</summary>
    public bool ProntuarioPendente => !ForaDaFila && !EvolucaoEscrita;

    /// <summary>Cancelado e falta não se atendem — o botão SOME, não fica apagado.</summary>
    public bool PodeAtender => !ForaDaFila;

    /// <summary>"Escrever" e "Abrir" não são a mesma ação.</summary>
    public string RotuloRegistro => EvolucaoEscrita ? "Ver registro" : "Atender";

    /// <summary>Em atendimento: a linha ganha o fundo do acento.</summary>
    public bool EmAtendimento => Etapa == EtapaFila.EmAtendimento;
}

/// <summary>
/// O "MEU DIA" — o que a tela afirma (out/2026; era o <c>MeuDiaViewModel</c> WPF). A lista do
/// dia de quem atende, na ordem da hora, com o status, a coluna PRONTUÁRIO e o Atender; a
/// dívida de prontuário em um número; a agenda fechada em uma linha. Puro: recebe o "hoje".
/// </summary>
public static class MeuDiaNaTela
{
    /// <summary>A partir daqui a chamada está demorando (o tempo de alguém andar até a sala).</summary>
    public const int ChamadaDemoradaMinutos = 3;

    public const string FraseSemPermissaoProntuario =
        "Abrir o atendimento mostra o prontuário, e o seu acesso não tem essa permissão. A direção libera em Acessos.";

    public const string FraseSemPermissaoPendentes =
        "A lista de sessões sem evolução é prontuário, e o seu acesso não tem essa permissão. A direção libera em Acessos.";

    public const string FraseSemPermissaoFila =
        "Prontuário aberto. O balcão não foi avisado de que o paciente entrou: o seu acesso não move a fila do dia.";

    /// <summary>O carimbo falhou — o prontuário abre assim mesmo, e a tela DIZ que o balcão não soube.</summary>
    public static string FraseFalhaDoCarimbo(string erro)
        => $"Prontuário aberto, mas o balcão não foi avisado de que o paciente entrou na sala: {erro}";

    /// <summary>A linha de uma sessão do dia.</summary>
    public static LinhaMeuDia Linha(SessaoDoDia s, DateOnly hoje) => new()
    {
        AgendamentoId = s.AgendamentoId,
        Data = DateOnly.FromDateTime(s.DataHora),
        PacienteId = s.PacienteId,
        Paciente = s.PacienteNome,
        Hora = $"{s.DataHora:HH:mm}–{s.FimPrevisto:HH:mm}",
        Modalidade = s.Modalidade,
        ModalidadeFamilia = s.ModalidadeFamilia,
        Local = s.Sala ?? string.Empty,
        Estado = s.Status,
        Etapa = s.Etapa,
        EvolucaoEscrita = s.EvolucaoEscrita,
        Encaixe = s.Encaixe,
        ColetaPreventivo = s.ColetaPreventivo,
        AtendimentoId = s.AtendimentoId,
        Observacoes = s.Observacoes ?? string.Empty,
        EhHoje = DateOnly.FromDateTime(s.DataHora) == hoje,
        EsperaMinutos = s.EsperaMinutos,
        ChamadoHaMinutos = s.ChamadoHaMinutos,
        ChegadaEm = s.ChegadaEm,
        InicioEm = s.InicioAtendimentoEm,
        FimEm = s.FimAtendimentoEm
    };

    /// <summary>
    /// As linhas do dia, na ordem da hora (o id desempata). Para o PERFIL de enfermagem só
    /// entram as sessões cuja modalidade admite passagem de enfermagem — a mesma regra que o
    /// <c>MeuDiaViewModel</c> aplicava (<see cref="EvolucaoEnfermagemService.PermiteEvolucao"/>).
    /// </summary>
    public static IReadOnlyList<LinhaMeuDia> Montar(DiaDoProfissional dia, bool perfilDeEnfermagem, DateOnly hoje)
        => dia.Sessoes
            .Where(s => !perfilDeEnfermagem || EvolucaoEnfermagemService.PermiteEvolucao(s.ModalidadeFamilia))
            .OrderBy(s => s.DataHora).ThenBy(s => s.AgendamentoId)
            .Select(s => Linha(s, hoje))
            .ToList();

    /// <summary>Os horários como o filtro os enxerga.</summary>
    public static IReadOnlyList<ItemDaAgendaDoPosto> ItensDoFiltro(IEnumerable<LinhaMeuDia> linhas)
        => linhas.Select(l => new ItemDaAgendaDoPosto(l.Paciente, l.Modalidade, l.GrupoVisual)).ToList();

    /// <summary>O que o botão da dívida diz. Zero não é "0 pendências", é "prontuário em dia".</summary>
    public static string RotuloPendentes(int n) => n switch
    {
        0 => "Prontuário em dia",
        1 => "1 sessão sem evolução",
        _ => $"{n} sessões sem evolução"
    };

    /// <summary>"Agenda fechada neste dia: Férias" — nulo = agenda aberta.</summary>
    public static string? AgendaFechada(string? motivoDoBloqueio)
        => motivoDoBloqueio is null ? null : $"Agenda fechada neste dia: {motivoDoBloqueio}";

    /// <summary>
    /// O "Atender" CARIMBA A ENTRADA NA SALA (parcela 95) — só quando há o que carimbar: o
    /// horário é de HOJE (abrir a sessão de ontem pela dívida não move fila nenhuma), está em
    /// aberto e ainda não começou. O PERFIL de enfermagem não carimba: a entrada é do
    /// profissional que consulta.
    /// </summary>
    public static bool CarimbaEntradaAoAtender(bool perfilDeEnfermagem, LinhaMeuDia l)
        => !perfilDeEnfermagem && l.EhHoje
           && l.Etapa is EtapaFila.Aguardando or EtapaFila.Chegou or EtapaFila.Chamado;
}
