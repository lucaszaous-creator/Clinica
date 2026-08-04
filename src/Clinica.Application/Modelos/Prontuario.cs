using Clinica.Domain.Entities;

namespace Clinica.Application.Modelos;

/// <summary>
/// Um anexo do prontuário SEM os bytes. A lista de anexos usa este resumo, e o corte é
/// feito no SQL: materializar o arquivo para depois descartá-lo puxaria megabytes pela
/// rede a cada abertura de prontuário (o banco é remoto).
/// </summary>
public sealed record AnexoResumo(
    int Id,
    int EvolucaoId,
    string NomeArquivo,
    TipoAnexo Tipo,
    string? TipoConteudo,
    int Tamanho,
    string? Descricao,
    DateTime CriadoEm)
{
    /// <summary>Tamanho legível para a tela ("340 KB").</summary>
    public string TamanhoFormatado => Tamanho < 1024
        ? $"{Tamanho} B"
        : Tamanho < 1024 * 1024
            ? $"{Tamanho / 1024} KB"
            : $"{Tamanho / (1024.0 * 1024):0.#} MB";

    /// <summary>
    /// A linha de apoio do arquivo, num texto só (parcela 37): tipo, tamanho, data e
    /// descrição.
    ///
    /// Sai daqui, e não de três TextBlocks lado a lado na tela, porque texto em
    /// <c>StackPanel</c> horizontal nunca dobra a linha — é a checagem 17 do
    /// <c>verificar-suite.py</c>, e um nome de arquivo de laboratório é comprido o
    /// bastante para empurrar a data para fora da janela.
    /// </summary>
    public string Resumo
    {
        get
        {
            var texto = $"{Tipo} · {TamanhoFormatado} · {CriadoEm:dd/MM/yyyy HH:mm}";
            return string.IsNullOrWhiteSpace(Descricao) ? texto : $"{texto} · {Descricao}";
        }
    }
}

/// <summary>
/// Um ponto da evolução da dor: o par EVA de uma sessão.
///
/// Só entram sessões com o par completo — uma medida solta não diz se melhorou, e
/// misturá-la ao gráfico faria a linha oscilar por falta de dado, não por dor.
/// </summary>
public sealed record PontoEva(
    DateOnly Data,
    int Antes,
    int Depois)
{
    /// <summary>Quanto a dor caiu na sessão. Positivo = melhorou.</summary>
    public int Variacao => Antes - Depois;
}

/// <summary>Como a dor do paciente andou ao longo do tratamento.</summary>
public sealed record EvolucaoDaDor(
    IReadOnlyList<PontoEva> Pontos,
    int SessoesRegistradas)
{
    /// <summary>Sessões com o par EVA completo — a base do que dá para afirmar.</summary>
    public int SessoesComMedida => Pontos.Count;

    /// <summary>Dor antes da PRIMEIRA sessão medida. Null quando não há medida nenhuma.</summary>
    public int? DorInicial => Pontos.Count == 0 ? null : Pontos[0].Antes;

    /// <summary>Dor depois da ÚLTIMA sessão medida.</summary>
    public int? DorAtual => Pontos.Count == 0 ? null : Pontos[^1].Depois;

    /// <summary>
    /// Ganho acumulado do tratamento: dor inicial menos dor atual. É a leitura que
    /// interessa ao paciente ("comecei em 8, estou em 3"), diferente do alívio de
    /// uma sessão isolada.
    /// </summary>
    public int? GanhoAcumulado
        => DorInicial is { } inicial && DorAtual is { } atual ? inicial - atual : null;

    /// <summary>Alívio médio por sessão (média das variações), arredondado.</summary>
    public double AlivioMedioPorSessao
        => Pontos.Count == 0 ? 0 : Math.Round(Pontos.Average(p => p.Variacao), 1);
}

/// <summary>Por que um paciente não está liberado para ser atendido hoje.</summary>
public enum ImpedimentoElegibilidade
{
    /// <summary>Carteirinha do convênio vencida — a guia seria recusada na origem.</summary>
    CarteirinhaVencida,

    /// <summary>Carteirinha vence em poucos dias.</summary>
    CarteirinhaAVencer,

    /// <summary>A cota de sessões autorizada pelo convênio acabou (glosa 2006).</summary>
    CotaEsgotada,

    /// <summary>A autorização vigente está perto do fim.</summary>
    CotaQuaseNoFim,

    /// <summary>
    /// A consulta renovável do paciente venceu — o convênio recusa o que for faturado sem
    /// consulta vigente.
    /// </summary>
    ConsultaVencida,

    /// <summary>
    /// A consulta renovável vence dentro da janela de alerta: dá para renovar agora, com o
    /// paciente presente, em vez de correr atrás dele depois.
    /// </summary>
    ConsultaARenovar,

    /// <summary>Não há autorização vigente registrada para o convênio.</summary>
    SemAutorizacaoVigente,

    /// <summary>Falta o consentimento LGPD de tratamento de dados.</summary>
    SemConsentimentoLgpd,

    /// <summary>
    /// O paciente tem conta vencida na clínica (parcela 27). Vem do Financeiro, e é a
    /// resposta que o balcão não tinha: o paciente está aqui, na frente da recepção — o
    /// único momento em que cobrar é barato.
    /// </summary>
    PacienteEmDebito,

    /// <summary>
    /// Guia deste paciente glosada pelo convênio e ainda em aberto (parcela 27). Vem do
    /// Faturamento. Glosa por assinatura ou documento faltando se resolve com o paciente
    /// presente; descobri-la depois custa o recurso inteiro.
    /// </summary>
    GuiaGlosada
}

/// <summary>Um alerta de elegibilidade, com a gravidade que a recepção precisa ver.</summary>
public sealed record AlertaElegibilidade(
    ImpedimentoElegibilidade Motivo,
    NivelUrgencia Urgencia,
    string Descricao);

/// <summary>
/// Resposta a "este paciente pode ser atendido hoje?".
///
/// Existe para a recepção descobrir no BALCÃO o que hoje só aparece na hora de faturar:
/// carteirinha vencida e cota estourada viram glosa depois do serviço prestado — e aí o
/// prejuízo já aconteceu.
/// </summary>
public sealed record Elegibilidade(
    int PacienteId,
    string PacienteNome,
    IReadOnlyList<AlertaElegibilidade> Alertas)
{
    /// <summary>Nada impede nem preocupa.</summary>
    public bool Liberado => Alertas.Count == 0;

    /// <summary>Há alerta vermelho: atender assim provavelmente vira glosa.</summary>
    public bool TemImpedimento => Alertas.Any(a => a.Urgencia == NivelUrgencia.Vermelho);
}
