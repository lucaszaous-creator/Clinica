using Clinica.Domain.Avaliacoes;
using Clinica.Domain.Entities;

namespace Clinica.Application.Modelos;

/// <summary>
/// Um horário do dia visto do lado de QUEM ATENDE — não de quem marca.
///
/// A recepção olha a mesma linha para saber se o paciente chegou; o consultório olha para
/// saber se falta escrever a evolução. Daí <see cref="EvolucaoEscrita"/> ser um campo
/// desta projeção e não da fila do balcão: o dado é o mesmo, a pergunta é outra.
/// </summary>
public sealed record SessaoDoDia(
    int AgendamentoId,
    DateTime DataHora,
    int PacienteId,
    string PacienteNome,
    string Modalidade,
    string? Sala,
    StatusAgendamento Status,
    EtapaFila Etapa,
    bool Encaixe,
    int? AtendimentoId,
    int? EvolucaoId)
{
    /// <summary>A sessão já tem evolução no prontuário.</summary>
    public bool EvolucaoEscrita => EvolucaoId is not null;

    /// <summary>
    /// O horário já aconteceu e continua sem evolução escrita — a pendência do
    /// consultório. Cancelado e falta não entram: não houve sessão para descrever.
    /// </summary>
    public bool RegistroPendente
        => Status == StatusAgendamento.Realizado && EvolucaoId is null;
}

/// <summary>
/// O dia de um profissional, como o consultório o enxerga.
///
/// O contador de pendências vem junto de propósito: a tela não recalcula "quantos faltam"
/// varrendo a lista, porque duas contagens da mesma coisa divergem no dia em que uma
/// delas ganhar um filtro.
/// </summary>
public sealed record DiaDoProfissional(
    DateOnly Dia,
    int? ProfissionalId,
    string ProfissionalNome,
    IReadOnlyList<SessaoDoDia> Sessoes)
{
    public int Atendidos => Sessoes.Count(s => s.Status == StatusAgendamento.Realizado);

    public int AEsperar => Sessoes.Count(s => s.Status == StatusAgendamento.Agendado);

    /// <summary>Sessões que aconteceram e ainda não têm evolução escrita.</summary>
    public int RegistrosPendentes => Sessoes.Count(s => s.RegistroPendente);
}

/// <summary>
/// Sessão atendida em dia ANTERIOR que continua sem evolução escrita.
///
/// É o "2º código esquecido" do prontuário: a sessão aconteceu, o paciente foi embora e o
/// registro ficou para depois — e "depois" é o dia em que ninguém mais lembra o que foi
/// feito. Diferente da guia, aqui não há prazo de convênio para vigiar; o que vigia é a
/// própria memória de quem atendeu, e ela vence rápido.
/// </summary>
public sealed record RegistroPendente(
    int AgendamentoId,
    DateTime DataHora,
    int PacienteId,
    string PacienteNome,
    string Modalidade,
    int? AtendimentoId)
{
    /// <summary>Há quantos dias a sessão aconteceu.</summary>
    public int DiasEmAberto(DateOnly hoje)
        => hoje.DayNumber - DateOnly.FromDateTime(DataHora).DayNumber;
}

/// <summary>
/// Um paciente na lista de quem o profissional atende, com o que ele precisa saber ANTES
/// de abrir o prontuário: quando veio pela última vez e como a dor estava.
/// </summary>
public sealed record PacienteDoProfissional(
    int PacienteId,
    string Nome,
    DateOnly? UltimaSessao,
    int Sessoes)
{
    /// <summary>Dor da última medida com par EVA completo. Null = nunca medida.</summary>
    public int? UltimaDor { get; init; }

    /// <summary>Dor antes da primeira sessão medida — o ponto de partida do tratamento.</summary>
    public int? DorInicial { get; init; }

    /// <summary>Ganho acumulado (inicial − atual). Null quando falta base.</summary>
    public int? GanhoAcumulado
        => DorInicial is { } inicial && UltimaDor is { } atual ? inicial - atual : null;

    /// <summary>Dias desde a última sessão. Null quando nunca houve uma.</summary>
    public int? DiasSemVir(DateOnly hoje)
        => UltimaSessao is { } dia ? hoje.DayNumber - dia.DayNumber : null;
}

/// <summary>
/// Um ponto da curva de um instrumento: o escore de uma aplicação.
///
/// Separado de <see cref="PontoEva"/> porque as duas curvas respondem coisas diferentes —
/// a EVA é medida a cada sessão e vai de 0 a 10; o escore de instrumento é medido a cada
/// poucas semanas e cada escala tem a sua faixa. Fundi-las num tipo só obrigaria a tela a
/// perguntar "de qual dos dois é este ponto?" a cada leitura.
/// </summary>
public sealed record PontoEscore(
    DateOnly Data,
    int Pontuacao,
    string? Faixa,
    GravidadeFaixa Gravidade);

/// <summary>
/// Como o escore de UM instrumento andou para um paciente.
///
/// <see cref="MelhorQuandoMenor"/> existe porque o Katz inverte: 6 pontos é o melhor
/// resultado possível, e uma tela que tratasse "subiu" como piora dali diria o contrário
/// do que aconteceu. A informação vem do instrumento, não de um palpite da tela.
/// </summary>
public sealed record EvolucaoDoEscore(
    string InstrumentoCodigo,
    string InstrumentoNome,
    string Unidade,
    int PontuacaoMaxima,
    bool MelhorQuandoMenor,
    IReadOnlyList<PontoEscore> Pontos)
{
    public int Aplicacoes => Pontos.Count;

    public int? Primeiro => Pontos.Count == 0 ? null : Pontos[0].Pontuacao;

    public int? Atual => Pontos.Count == 0 ? null : Pontos[^1].Pontuacao;

    /// <summary>
    /// Variação do primeiro ao último escore, já com o SINAL DA MELHORA: positivo é
    /// sempre "melhorou", tanto na escala em que cair é bom quanto na em que subir é bom.
    /// Null com menos de duas aplicações — uma medida solta não é evolução de nada, que é
    /// a mesma regra do par EVA.
    /// </summary>
    public int? Ganho
    {
        get
        {
            if (Pontos.Count < 2) return null;
            var bruto = Pontos[0].Pontuacao - Pontos[^1].Pontuacao;
            return MelhorQuandoMenor ? bruto : -bruto;
        }
    }
}
