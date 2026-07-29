using Clinica.Domain.Entities;

namespace Clinica.Application.Modelos;

// ==================== BI (feature 12) ====================

/// <summary>
/// Números da agenda num período. Ocupação e no-show são as duas perguntas que a
/// direção faz — e as duas que o sistema não sabia responder antes da parcela 1,
/// porque não existia profissional para agrupar.
/// </summary>
public sealed record IndicadoresAgenda(
    int Agendados,
    int Atendidos,
    int Faltas,
    int Cancelados,
    int Encaixes,
    int PacientesDistintos,
    int MinutosOcupados,
    int MinutosDisponiveis)
{
    /// <summary>Horários que chegaram ao fim: atendidos + faltas (o denominador honesto do no-show).</summary>
    public int Fechados => Atendidos + Faltas;

    /// <summary>
    /// % de falta sobre os horários fechados. Cancelamento avisado NÃO entra: quem
    /// desmarcou deu à clínica a chance de reocupar o horário — misturar os dois
    /// esconde exatamente o problema que o indicador existe para mostrar.
    /// </summary>
    public double TaxaNoShow
        => Fechados == 0 ? 0 : Math.Round(Faltas * 100.0 / Fechados, 1);

    /// <summary>
    /// % da jornada efetivamente ocupada. NULL quando não há base para medir — a tela
    /// mostra "não medido" em vez de 0%, que pareceria agenda vazia.
    /// </summary>
    public double? OcupacaoPercentual
        => MinutosDisponiveis == 0 ? null : Math.Round(MinutosOcupados * 100.0 / MinutosDisponiveis, 1);
}

/// <summary>
/// Produtividade de um profissional no período. <c>ProfissionalId</c> nulo é a linha
/// dos horários sem profissional informado — o caminho do faturamento, que marca sem
/// dizer quem atende. Ela aparece de propósito: sumir com esses horários faria os
/// totais da tela não fecharem com a agenda.
/// </summary>
public sealed record ProdutividadeProfissional(
    int? ProfissionalId,
    string Nome,
    int Agendados,
    int Atendidos,
    int Faltas,
    int Cancelados,
    int Encaixes,
    int PacientesDistintos,
    int MinutosOcupados,
    int DiasComAgenda,
    int MinutosDisponiveis,
    int Evolucoes,
    double? MelhoraMediaEva)
{
    public int Fechados => Atendidos + Faltas;

    public double TaxaNoShow
        => Fechados == 0 ? 0 : Math.Round(Faltas * 100.0 / Fechados, 1);

    public double? OcupacaoPercentual
        => MinutosDisponiveis == 0 ? null : Math.Round(MinutosOcupados * 100.0 / MinutosDisponiveis, 1);

    /// <summary>Horas ocupadas, para a tela não fazer a conta.</summary>
    public double HorasOcupadas => Math.Round(MinutosOcupados / 60.0, 1);
}

/// <summary>Um mês da série gerencial (evolução de ocupação e no-show).</summary>
public sealed record MesGerencial(
    int Ano,
    int Mes,
    string Rotulo,
    int Atendidos,
    int Faltas,
    int Cancelados,
    int MinutosOcupados,
    int MinutosDisponiveis)
{
    public int Fechados => Atendidos + Faltas;

    public double TaxaNoShow
        => Fechados == 0 ? 0 : Math.Round(Faltas * 100.0 / Fechados, 1);

    public double? OcupacaoPercentual
        => MinutosDisponiveis == 0 ? null : Math.Round(MinutosOcupados * 100.0 / MinutosDisponiveis, 1);
}

/// <summary>
/// O painel gerencial inteiro. Traz <see cref="JornadaDiariaMinutos"/> junto porque a
/// ocupação só significa alguma coisa se a tela puder dizer contra o que ela foi medida.
/// </summary>
public sealed record PainelGerencial(
    DateOnly Inicio,
    DateOnly Fim,
    int JornadaDiariaMinutos,
    IndicadoresAgenda Agenda,
    IReadOnlyList<ProdutividadeProfissional> Produtividade,
    IReadOnlyList<MesGerencial> Serie);

// ==================== Campanhas (features 11 e 02) ====================

/// <summary>
/// Resultado de uma rodada de campanha. Os três motivos de exclusão são devolvidos de
/// propósito: gerar 4 contatos numa clínica com 40 pacientes precisa dizer o que
/// aconteceu com os outros 36 — campanha que filtra em silêncio parece quebrada.
/// </summary>
public sealed record ResultadoCampanha(
    TipoContato Tipo,
    int Gerados,
    int JaExistiam,
    int SemConsentimento,
    int SemTelefone)
{
    /// <summary>Quantos candidatos a rodada examinou.</summary>
    public int Examinados => Gerados + JaExistiam + SemConsentimento + SemTelefone;
}

/// <summary>
/// Resultado do NPS num período. <see cref="Medido"/> separa "nota 0" de "ninguém
/// respondeu" — sem isso a tela exibiria 0 e a direção leria péssima reputação onde
/// há apenas ausência de dado.
/// </summary>
public sealed record ResumoNps(
    int Gerados,
    int Enviados,
    int Respondidos,
    int Promotores,
    int Neutros,
    int Detratores)
{
    public bool Medido => Respondidos > 0;

    /// <summary>NPS = %promotores − %detratores, de −100 a +100. Só vale se <see cref="Medido"/>.</summary>
    public int Pontuacao
        => Respondidos == 0 ? 0 : (int)Math.Round((Promotores - Detratores) * 100.0 / Respondidos);

    /// <summary>% dos enviados que responderam.</summary>
    public double TaxaResposta
        => Enviados == 0 ? 0 : Math.Round(Respondidos * 100.0 / Enviados, 1);
}

/// <summary>
/// Projeção de inatividade de um paciente, resolvida no SQL: quando veio pela última
/// vez e se já tem horário marcado. É a base do recall — e vem por projeção porque a
/// pergunta é sobre a base INTEIRA de pacientes, e trazer todos com histórico junto
/// arrastaria a tabela de atendimentos pela rede.
/// </summary>
public sealed record InatividadePaciente(
    int PacienteId,
    string Nome,
    string? Telefone,
    DateOnly? UltimoAtendimento,
    DateTime? ProximoAgendamento);

/// <summary>
/// Um paciente que sumiu, já com o que a tela precisa mostrar para decidir chamar.
/// <see cref="TemConsentimento"/> é exibido, não escondido: quem não autorizou
/// comunicação aparece marcado, para a clínica saber que precisa pedir o consentimento
/// — não some da lista sem explicação.
/// </summary>
public sealed record CandidatoRecall(
    int PacienteId,
    string Nome,
    string? Telefone,
    DateOnly? UltimoAtendimento,
    int DiasSemVir,
    bool TemConsentimento,
    bool TemTelefone);

// ==================== Caixa ====================

/// <summary>
/// Projeção mínima de um lançamento, para somar os totais do caixa sem trazer a linha
/// inteira (nem a categoria, nem o paciente) do banco.
/// </summary>
public sealed record ValorLancamento(
    TipoLancamento Tipo,
    StatusLancamento Status,
    decimal Valor,
    decimal? ValorTaxa = null,
    decimal? ValorImposto = null)
{
    /// <summary>O que sobra depois da maquininha e do fisco.</summary>
    public decimal Liquido => Valor - (ValorTaxa ?? 0m) - (ValorImposto ?? 0m);
}
