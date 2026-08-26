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
    /// <summary>
    /// Há quantos minutos o paciente espera (da chegada até ser chamado). Null antes do
    /// check-in — no consultório isso significa "ainda não está no prédio".
    /// </summary>
    public int? EsperaMinutos { get; init; }

    /// <summary>
    /// Há quantos minutos o profissional chamou e o paciente não entrou (parcela 38).
    /// Null quando não há chamada pendente.
    /// </summary>
    public int? ChamadoHaMinutos { get; init; }

    /// <summary>
    /// O recado que a recepção escreveu no horário ("chega 10 min atrasada", "trazer o
    /// laudo"). Nulo quando não há. Viajava até o balcão e nunca chegava a quem atende —
    /// o campo existia no <c>Agendamento</c> desde a parcela 1 e esta projeção não o
    /// carregava, então o médico trabalhava com uma foto parcial do que o balcão sabia.
    /// </summary>
    public string? Observacoes { get; init; }

    /// <summary>
    /// Duração prevista em minutos. É o que faz a grade da semana saber até onde a
    /// sessão cobre — sem ela, uma sessão de uma hora deixaria a meia hora seguinte
    /// parecendo vaga.
    /// </summary>
    public int DuracaoMinutos { get; init; } = Agendamento.DuracaoPadraoMinutos;

    /// <summary>Fim previsto da sessão.</summary>
    public DateTime FimPrevisto => DataHora.AddMinutes(DuracaoMinutos);

    /// <summary>Cancelado ou falta: a sessão saiu do fluxo do dia, mas a linha fica.</summary>
    public bool ForaDoDia => Status is StatusAgendamento.Cancelado or StatusAgendamento.Faltou;

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

    /// <summary>Já no prédio, esperando ser chamado — é sobre estes que se decide o próximo.</summary>
    public int NaRecepcao => Sessoes.Count(s => s.Etapa == EtapaFila.Chegou);

    /// <summary>Chamados e ainda não entraram (parcela 38).</summary>
    public int Chamados => Sessoes.Count(s => s.Etapa == EtapaFila.Chamado);

    /// <summary>
    /// O próximo a chamar: quem já chegou, pelo horário. Null quando não há ninguém no
    /// balcão — e aí o botão de chamar fica desabilitado em vez de chamar o ar.
    /// </summary>
    public SessaoDoDia? ProximoAChamar => Sessoes
        .Where(s => s.Etapa == EtapaFila.Chegou)
        .OrderBy(s => s.DataHora)
        .FirstOrDefault();

    /// <summary>Sessões que aconteceram e ainda não têm evolução escrita.</summary>
    public int RegistrosPendentes => Sessoes.Count(s => s.RegistroPendente);
}

/// <summary>
/// A SEMANA do profissional (parcela 39): sete dias, cada um com as sessões dele.
///
/// "Meu dia" responde o que acontece hoje; ele não responde "quando eu tenho espaço",
/// "quantos pacientes vêm quinta" nem "vale marcar o retorno dele para sexta" — que são
/// as perguntas que se fazem COM o paciente na frente, no fim da consulta. A agenda do
/// balcão tem a visão de semana desde a parcela 26, e o app de quem atende não tinha.
///
/// Começa na SEGUNDA, como a da recepção, e pela mesma razão: a clínica pensa a semana em
/// bloco. Começar no dia escolhido daria uma janela diferente a cada clique.
/// </summary>
public sealed record SemanaDoProfissional(
    DateOnly Inicio,
    int? ProfissionalId,
    string ProfissionalNome,
    IReadOnlyList<DiaDoProfissional> Dias)
{
    public DateOnly Fim => Inicio.AddDays(6);

    public int Sessoes => Dias.Sum(d => d.Sessoes.Count);

    public int Atendidos => Dias.Sum(d => d.Atendidos);

    /// <summary>Sessões da semana ainda sem evolução escrita — a dívida em formação.</summary>
    public int RegistrosPendentes => Dias.Sum(d => d.RegistrosPendentes);

    /// <summary>
    /// Pessoas distintas na semana. Não é o mesmo que sessões, e a diferença é a leitura:
    /// vinte sessões de oito pacientes é tratamento em série; vinte de vinte é primeira
    /// consulta atrás de primeira consulta, e o dia cansa de outro jeito.
    /// </summary>
    public int PacientesDistintos => Dias
        .SelectMany(d => d.Sessoes)
        .Select(s => s.PacienteId)
        .Distinct()
        .Count();

    /// <summary>
    /// O dia mais cheio da semana, em número de sessões. Null quando a semana está vazia —
    /// nunca zero: "o dia mais cheio tem 0 sessões" é uma frase que não quer dizer nada.
    /// </summary>
    public DiaDoProfissional? DiaMaisCheio => Sessoes == 0
        ? null
        : Dias.OrderByDescending(d => d.Sessoes.Count).First();
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
    /// <summary>
    /// Quem atendeu a sessão. Nulo quando o agendamento não tem profissional. Importa no
    /// modo SEM VÍNCULO do Consultório, em que a lista é da clínica inteira e precisa
    /// dizer (e filtrar) de quem é cada dívida.
    /// </summary>
    public string? Profissional { get; init; }

    /// <summary>Há quantos dias a sessão aconteceu.</summary>
    public int DiasEmAberto(DateOnly hoje)
        => hoje.DayNumber - DateOnly.FromDateTime(DataHora).DayNumber;
}

/// <summary>
/// Um paciente na carteira da CLÍNICA, com a leitura do tratamento pronta.
///
/// ⚠️ Ele se chamava <c>PacienteDoProfissional</c> (parcela 88, 3ª rodada). A clínica disse
/// a frase que apaga a premissa: <b>"não existe 'meu paciente', todos atendem todos"</b>.
/// A autoria não se perdeu — quem atendeu está no agendamento e quem escreveu assina a
/// evolução —; o que deixou de existir é a noção de DONO na lista de pacientes, e o nome
/// do tipo tinha de deixar de afirmá-la.
/// </summary>
public sealed record PacienteDaCarteira(
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
