namespace Clinica.Domain.Entities;

/// <summary>
/// A ANAMNESE DO PACIENTE — o que se pergunta UMA vez e se revisa (parcela 75).
///
/// Por que ela não é a <see cref="Evolucao"/> com mais campos
/// ---------------------------------------------------------
/// A sessão responde <i>"o que aconteceu hoje"</i>; esta responde <i>"quem é esta
/// pessoa"</i>. São eixos diferentes, e é por isso que a anamnese da parcela 73 (história
/// da doença atual, exame físico, hipótese) ficou na sessão e esta fica aqui: a HDA é do
/// EPISÓDIO — muda a cada queixa nova —, enquanto "mãe hipertensa, pai infartou aos 58" e
/// "apendicectomia em 2015" valem para o tratamento inteiro.
///
/// ⚠️ Repetir isto em toda sessão foi medido e recusado: além de o profissional escrever
/// "idem" — pior do que o campo vazio, porque parece registro —, a pergunta que ele faz na
/// consulta 12 é <i>"o que eu já sei sobre este paciente?"</i>, e a resposta não pode
/// depender de abrir a sessão 1 e ler.
///
/// Por que não é o <see cref="ProblemaPaciente"/>
/// ---------------------------------------------
/// Aquele é uma LISTA de itens, e está certo para o que é item: "apendicectomia 2015",
/// "alergia a dipirona", "losartana 50mg". Esta é NARRATIVA, e há coisas que não viram item
/// sem perder o sentido — a história familiar, o padrão de sono, o contexto social. As duas
/// convivem, e a tela mostra as duas: a lista alerta, o texto explica.
///
/// ⚠️ ALERGIA continua sendo SÓ da lista de problemas, e isso é decisão. Ela é o único dado
/// clínico que ACENDE alerta em quatro telas e RECUSA a assinatura de uma prescrição
/// (parcela 40). Um campo de texto "alergias" aqui seria uma segunda verdade sobre a mesma
/// coisa — e a que ninguém lembraria de atualizar é justamente a que o alerta lê.
///
/// As regras que ela herda do prontuário
/// -------------------------------------
/// - <b>Não se apaga.</b> É registro clínico: guarda de 20 anos (Lei 13.787/2018).
/// - <b>Alterar guarda o que ela dizia antes</b> (<see cref="VersaoAnamnese"/>) — ponto 2 do
///   compromisso de conformidade. Sobrescrever no lugar é apagar devagar.
/// - <b>Quem assina é quem fez LOGIN</b>, nunca o usuário do Windows.
/// </summary>
public class AnamnesePaciente
{
    public int Id { get; set; }

    /// <summary>O dono. UMA anamnese por paciente — índice único.</summary>
    public int PacienteId { get; set; }
    public Paciente? Paciente { get; set; }

    /// <summary>
    /// História patológica pregressa: doenças prévias, internações, cirurgias, fraturas.
    /// </summary>
    public string? AntecedentesPessoais { get; set; }

    /// <summary>
    /// História familiar. O que decide conduta é o PARENTESCO e a IDADE do evento — "pai
    /// infartou aos 58" pesa diferente de "avô infartou aos 89" —, e é por isso que o campo
    /// é narrativo em vez de uma lista de doenças marcadas.
    /// </summary>
    public string? AntecedentesFamiliares { get; set; }

    /// <summary>
    /// Hábitos de vida: tabagismo, etilismo, atividade física, sono, alimentação.
    ///
    /// ⚠️ Narrativo de propósito. A versão estruturada (fumante sim/não, doses por semana)
    /// foi medida e recusada: numa clínica de dor e acupuntura ela viraria uma fileira de
    /// caixinhas em branco em toda ficha — o campo que vira paisagem, e paisagem não se
    /// preenche. Quem quiser o número tem as escalas do catálogo (o FINDRISC pergunta
    /// atividade física com corte publicado).
    /// </summary>
    public string? HabitosDeVida { get; set; }

    /// <summary>
    /// História obstétrica, quando faz sentido. Anulável e SEM caixinha própria na tela para
    /// quem não a tem: campo obstétrico aberto na ficha de todo paciente é o mesmo defeito
    /// da caixinha em branco, com o agravante de ser sobre o corpo de quem não foi perguntado.
    /// </summary>
    public string? HistoriaObstetrica { get; set; }

    /// <summary>
    /// Interrogatório sintomatológico — a varredura por aparelhos que a primeira consulta
    /// faz e as seguintes não repetem.
    /// </summary>
    public string? RevisaoDeSistemas { get; set; }

    /// <summary>
    /// Contexto que muda a conduta e não cabe nos outros: profissão, com quem mora, quem
    /// cuida, o que a pessoa espera do tratamento.
    /// </summary>
    public string? Observacoes { get; set; }

    public DateTime CriadaEm { get; set; } = DateTime.Now;
    public string? CriadaPor { get; set; }

    public DateTime? AtualizadaEm { get; set; }
    public string? AtualizadaPor { get; set; }

    /// <summary>O que ela dizia antes de cada correção. Ver <see cref="VersaoAnamnese"/>.</summary>
    public List<VersaoAnamnese> Versoes { get; set; } = new();

    /// <summary>
    /// Não há nada escrito. A tela usa para dizer "ainda não foi colhida" em vez de mostrar
    /// seis rótulos sobre o vazio — que se leria como "o paciente não tem antecedentes".
    /// </summary>
    public bool EstaVazia =>
        string.IsNullOrWhiteSpace(AntecedentesPessoais)
        && string.IsNullOrWhiteSpace(AntecedentesFamiliares)
        && string.IsNullOrWhiteSpace(HabitosDeVida)
        && string.IsNullOrWhiteSpace(HistoriaObstetrica)
        && string.IsNullOrWhiteSpace(RevisaoDeSistemas)
        && string.IsNullOrWhiteSpace(Observacoes);

    /// <summary>
    /// Quando ela foi tocada pela última vez — é o que a tela mostra para quem precisa
    /// decidir se vale reperguntar. Anamnese de três anos atrás não está errada; está
    /// VELHA, e as duas coisas se tratam diferente.
    /// </summary>
    public DateTime UltimaRevisao => AtualizadaEm ?? CriadaEm;
}

/// <summary>
/// O que a anamnese dizia ANTES de uma correção (parcela 75).
///
/// Mesma razão da <see cref="VersaoEvolucao"/>, e a mesma lei: o art. 3º da Lei 13.787/2018
/// exige que a retificação seja rastreável. Sem isto, corrigir "nega tabagismo" para
/// "tabagista" apagaria a informação de que a pessoa havia negado — que é exatamente o que
/// uma perícia procura.
/// </summary>
public class VersaoAnamnese
{
    public int Id { get; set; }

    public int AnamnesePacienteId { get; set; }
    public AnamnesePaciente? Anamnese { get; set; }

    /// <summary>Numeração da versão, contada a partir das que já existem.</summary>
    public int Versao { get; set; }

    public string? AntecedentesPessoais { get; set; }
    public string? AntecedentesFamiliares { get; set; }
    public string? HabitosDeVida { get; set; }
    public string? HistoriaObstetrica { get; set; }
    public string? RevisaoDeSistemas { get; set; }
    public string? Observacoes { get; set; }

    public DateTime SubstituidaEm { get; set; } = DateTime.Now;
    public string? SubstituidaPor { get; set; }

    /// <summary>
    /// Por que foi corrigida. OPCIONAL, pela razão da <c>VersaoEvolucao.Motivo</c>: exigir
    /// justificativa a cada revisão produziria trinta "atualização" por semana, que é rastro
    /// com aparência de controle e nenhum conteúdo.
    /// </summary>
    public string? Motivo { get; set; }
}
