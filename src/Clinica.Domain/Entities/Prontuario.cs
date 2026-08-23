namespace Clinica.Domain.Entities;

/// <summary>
/// Uma sessão registrada no prontuário: o que o paciente relatou, o que foi feito e
/// como a dor respondeu.
///
/// A EVA (Escala Visual Analógica, 0–10) é medida ANTES e DEPOIS da sessão de propósito:
/// é o par que mostra se o tratamento está funcionando. Uma medida só, isolada, não
/// responde nada — e é justamente o gráfico de evolução da dor que a proposta vendeu.
///
/// O vínculo com <see cref="Atendimento"/> é opcional: a evolução pode ser escrita antes
/// de a guia existir (a sessão acontece, o faturamento vem depois), e uma sessão
/// particular pode nunca gerar guia nenhuma.
/// </summary>
/// <summary>
/// Um MODELO de evolução (parcela 63): o roteiro da sessão que se repete.
///
/// A sessão de acupuntura da clínica tem sempre a mesma forma — a mesma estrutura de
/// queixa, a mesma conduta de base, as mesmas orientações de fim — e era redigitada por
/// inteiro toda vez. <c>ModeloDocumento</c> existia desde a parcela 3 e servia só aos
/// papéis IMPRESSOS; a evolução, que é o texto escrito com mais frequência no sistema,
/// não tinha nada.
///
/// <b>Aplicar COPIA, nunca aponta</b> — a mesma regra do protocolo do mapa corporal e da
/// venda do pacote. Referência viva faria corrigir uma palavra do modelo hoje reescrever
/// o prontuário da sessão da semana passada, que é exatamente o que a Lei 13.787/2018
/// proíbe. Depois de aplicado, o texto é da SESSÃO e segue a vida dele.
///
/// Por isso o modelo <b>se apaga mesmo</b>, ao contrário de tudo o que é prontuário: ele
/// não é registro do que aconteceu com ninguém, é um rascunho de apoio — e as sessões
/// escritas com ele não mudam, porque não o referenciam. É a mesma decisão da parcela 25
/// para o protocolo e o modelo de documento.
/// </summary>
public class ModeloEvolucao
{
    public int Id { get; set; }

    /// <summary>Como o profissional o encontra na lista ("Sessão de acupuntura — lombar").</summary>
    public string Nome { get; set; } = string.Empty;

    /// <summary>
    /// De quem é o modelo. Nulo = da CLÍNICA, e todo mundo vê.
    ///
    /// A separação existe porque cada profissional escreve de um jeito, e um modelo
    /// pessoal na lista de todos vira ruído para os outros cinco. O da clínica é o
    /// padrão combinado; o pessoal é o atalho de quem o escreveu.
    /// </summary>
    public int? ProfissionalId { get; set; }
    public Profissional? Profissional { get; set; }

    public string? QueixaPrincipal { get; set; }

    public string? Conduta { get; set; }

    public string? TextoEvolucao { get; set; }

    public string? Orientacoes { get; set; }

    public bool Ativo { get; set; } = true;

    public int Ordem { get; set; }

    public DateTime CriadoEm { get; set; } = DateTime.Now;

    public string? CriadoPor { get; set; }

    public DateTime? AtualizadoEm { get; set; }

    /// <summary>Da clínica inteira, e não de uma pessoa.</summary>
    public bool DaClinica => ProfissionalId is null;

    /// <summary>
    /// Modelo sem uma única linha preenchida não serve para nada e, aplicado, apagaria o
    /// que já estava escrito na sessão. Vale a pena recusar na gravação.
    /// </summary>
    public bool TemConteudo =>
        !string.IsNullOrWhiteSpace(QueixaPrincipal)
        || !string.IsNullOrWhiteSpace(Conduta)
        || !string.IsNullOrWhiteSpace(TextoEvolucao)
        || !string.IsNullOrWhiteSpace(Orientacoes);
}

public class Evolucao
{
    public int Id { get; set; }

    public int PacienteId { get; set; }
    public Paciente? Paciente { get; set; }

    /// <summary>Quem atendeu. Null quando a clínica ainda não cadastrou a equipe.</summary>
    public int? ProfissionalId { get; set; }
    public Profissional? Profissional { get; set; }

    /// <summary>Atendimento/guia que corresponde a esta sessão, quando existe.</summary>
    public int? AtendimentoId { get; set; }
    public Atendimento? Atendimento { get; set; }

    /// <summary>Agendamento de origem, quando a evolução nasceu da fila do dia.</summary>
    public int? AgendamentoId { get; set; }
    public Agendamento? Agendamento { get; set; }

    public DateOnly Data { get; set; }

    /// <summary>Dor relatada ANTES da sessão, de 0 a 10. Null = não medida.</summary>
    public int? EvaAntes { get; set; }

    /// <summary>Dor relatada DEPOIS da sessão, de 0 a 10. Null = não medida.</summary>
    public int? EvaDepois { get; set; }

    /// <summary>O que o paciente relatou nesta sessão.</summary>
    public string? QueixaPrincipal { get; set; }

    // ---- O registro do ATENDIMENTO (parcela 73) ----
    //
    // ⚠️ Até aqui a sessão médica eram QUATRO caixas de texto (queixa, conduta, evolução,
    // orientações) e o par de EVA. Faltava o que um prontuário responde: como a queixa
    // evoluiu, o que o exame ACHOU, e o que o profissional CONCLUIU. Sem a hipótese, o
    // prontuário registra o que foi feito e não diz por quê — e é o "por quê" que sustenta
    // a conduta numa auditoria, num pedido de convênio ou numa perícia.
    //
    // Todos NULOS e todos ADITIVOS: a sessão curta de acupuntura continua sendo queixa +
    // conduta, e obrigar quem faz vinte sessões de manutenção por dia a preencher anamnese
    // completa faria a clínica escrever "idem" em todas — que é pior do que o campo vazio,
    // porque parece registro.

    /// <summary>
    /// A história da doença atual: desde quando, como começou, o que melhora e o que piora.
    /// É o que distingue a primeira consulta do retorno, e é dela que sai a conduta.
    /// </summary>
    public string? HistoriaDoencaAtual { get; set; }

    /// <summary>
    /// O que o exame ACHOU — inspeção, palpação, amplitude, força, o que for da
    /// especialidade. Separado da <see cref="Conduta"/> de propósito: achado e ato são
    /// coisas diferentes, e juntá-los num campo só é o que faz o prontuário parar de
    /// responder "com base em quê?".
    /// </summary>
    public string? ExameFisico { get; set; }

    /// <summary>
    /// A conclusão do profissional sobre ESTA sessão. Texto livre, porque nem toda
    /// especialidade fecha diagnóstico em toda consulta.
    /// </summary>
    public string? HipoteseDiagnostica { get; set; }

    /// <summary>
    /// O CID da hipótese, quando há. ⚠️ OPCIONAL, e é decisão: exigi-lo faria o
    /// fisioterapeuta e o acupunturista pararem de preencher a hipótese — a mesma razão
    /// pela qual ele é opcional na lista de problemas (parcela 37). O catálogo da parcela
    /// 63 é ATALHO com conferência, não recusa: o campo aceita o que for digitado.
    /// </summary>
    public string? CidSessao { get; set; }

    /// <summary>O que foi feito: pontos, técnica, tempo de agulhamento…</summary>
    public string? Conduta { get; set; }

    /// <summary>Evolução em texto livre — a leitura clínica da sessão.</summary>
    public string? TextoEvolucao { get; set; }

    /// <summary>Orientações dadas ao paciente ao final.</summary>
    public string? Orientacoes { get; set; }

    public DateTime CriadoEm { get; set; } = DateTime.Now;

    public string? CriadoPor { get; set; }

    public DateTime? AtualizadoEm { get; set; }

    /// <summary>
    /// Quando a sessão foi CANCELADA. Cancelar não apaga (parcela 52): a linha fica no
    /// prontuário, marcada, com o motivo — é o que a Lei 13.787/2018 chama de integridade
    /// e autenticidade, e é o mesmo padrão do documento clínico, da não conformidade do
    /// faturamento e da checagem de enfermagem.
    ///
    /// Até esta parcela havia <c>Remove()</c> de verdade aqui, e ele levava os anexos
    /// junto.
    /// </summary>
    public DateTime? CanceladaEm { get; set; }

    /// <summary>Por que a sessão foi cancelada. Obrigatório — cancelar sem motivo é apagar com etapa a mais.</summary>
    public string? MotivoCancelamento { get; set; }

    /// <summary>Quem cancelou (o login, nunca o usuário do Windows).</summary>
    public string? CanceladaPor { get; set; }

    public List<AnexoProntuario> Anexos { get; set; } = new();

    /// <summary>
    /// O conteúdo que esta sessão já teve antes das correções. Ver <see cref="VersaoEvolucao"/>.
    /// </summary>
    public List<VersaoEvolucao> Versoes { get; set; } = new();

    /// <summary>Sessão cancelada: continua no prontuário, fora das contas.</summary>
    public bool Cancelada => CanceladaEm is not null;

    /// <summary>
    /// A sessão já foi corrigida ao menos uma vez. A tela usa isto para oferecer o
    /// histórico — retificação que não se consegue LER não é rastreável, e rastreabilidade
    /// é o que o art. 3º da Lei 13.787 exige.
    /// </summary>
    public bool Retificada => Versoes.Count > 0;

    /// <summary>Faixa mínima e máxima aceitas na escala de dor.</summary>
    public const int EvaMinima = 0;
    public const int EvaMaxima = 10;

    /// <summary>
    /// Quanto a dor caiu nesta sessão (antes − depois). Null quando falta uma das
    /// medidas. Positivo = melhorou; negativo = piorou.
    /// </summary>
    public int? VariacaoEva
        => EvaAntes is { } antes && EvaDepois is { } depois ? antes - depois : null;

    /// <summary>A sessão registrou o par completo de medidas de dor.</summary>
    public bool TemParEva => EvaAntes is not null && EvaDepois is not null;

    /// <summary>Valor válido na escala (ou nulo, que é "não medido").</summary>
    public static bool EvaValida(int? valor)
        => valor is null || (valor >= EvaMinima && valor <= EvaMaxima);
}

/// <summary>Natureza do arquivo anexado ao prontuário.</summary>
public enum TipoAnexo
{
    /// <summary>Foto da região, do exame, do mapa em papel…</summary>
    Imagem,

    /// <summary>Laudo, exame, encaminhamento — PDF ou documento.</summary>
    Documento,

    Outro
}

/// <summary>
/// Arquivo anexado a uma evolução (foto da região, laudo, exame).
///
/// Fica em tabela própria pelo mesmo motivo de <see cref="PacienteFoto"/>: abrir o
/// prontuário de um paciente não pode arrastar megabytes pela rede — o banco é remoto.
/// A LISTA de anexos vem por projeção, sem o campo <see cref="Conteudo"/> (o corte é
/// feito no SQL, nunca materializando os bytes para descartá-los depois); quem quer o
/// arquivo pede o conteúdo de um anexo específico.
/// </summary>
public class AnexoProntuario
{
    public int Id { get; set; }

    public int EvolucaoId { get; set; }
    public Evolucao? Evolucao { get; set; }

    public string NomeArquivo { get; set; } = string.Empty;

    public TipoAnexo Tipo { get; set; }

    /// <summary>Tipo MIME, quando conhecido (ex.: <c>image/jpeg</c>).</summary>
    public string? TipoConteudo { get; set; }

    /// <summary>Bytes do arquivo. Só sai do banco quando alguém pede este anexo.</summary>
    public byte[] Conteudo { get; set; } = Array.Empty<byte>();

    /// <summary>Tamanho em bytes, replicado para a lista não precisar ler o conteúdo.</summary>
    public int Tamanho { get; set; }

    public string? Descricao { get; set; }

    public DateTime CriadoEm { get; set; } = DateTime.Now;

    public string? CriadoPor { get; set; }

    /// <summary>
    /// Quando o anexo foi RETIRADO do prontuário. Como a evolução, ele não se apaga
    /// (parcela 52): o laudo que sustentou uma conduta continua sendo parte da prova de
    /// que a conduta era razoável, mesmo depois de alguém concluir que o arquivo estava
    /// errado.
    /// </summary>
    public DateTime? CanceladoEm { get; set; }

    public string? MotivoCancelamento { get; set; }

    public string? CanceladoPor { get; set; }

    public bool Cancelado => CanceladoEm is not null;
}

/// <summary>
/// O CONTEÚDO ANTERIOR de uma evolução, guardado antes de ser sobrescrito (parcela 52).
///
/// O problema que ela resolve
/// --------------------------
/// Até aqui, corrigir uma sessão sobrescrevia os campos no lugar. A auditoria gravava
/// <c>"EvolucaoAlterada — Sessão de 12/03/2026"</c> e mais nada: o texto anterior
/// desaparecia. Isso é o oposto do que a Lei 13.787/2018 (art. 3º) pede — integridade,
/// autenticidade e, havendo retificação, RASTREABILIDADE —, e é a única pergunta que
/// uma perícia faz sobre um prontuário eletrônico: <i>"o que estava escrito antes?"</i>.
/// Trilha que registra QUE mudou sem registrar o QUE mudou não responde nada.
///
/// Por que uma tabela de versões, e não o padrão de retificação da checagem
/// -----------------------------------------------------------------------
/// A checagem de enfermagem retifica gravando uma LINHA NOVA que aponta a anterior, e as
/// duas aparecem na folha. Ali funciona porque a checagem é um fato pontual, feito uma
/// vez. A evolução não: ela é escrita, relida e completada — o profissional salva várias
/// vezes a mesma sessão. Aplicar o mesmo padrão criaria seis "sessões" no prontuário
/// para uma consulta que houve uma vez, e o prontuário passaria a MENTIR sobre quantas
/// vezes o paciente veio, que é pior do que o problema que se quis resolver.
///
/// Então a sessão continua sendo UMA linha — a atual, a que se lê — e o que ela já foi
/// fica ao lado, em ordem, sem poluir o prontuário de quem só quer ler o tratamento.
///
/// <b>Guarda-se o conteúdo ANTES da alteração</b>, e não depois: a versão N é o que o
/// registro dizia até o momento em que alguém o mudou, e é isso que se quer recuperar.
/// A versão vigente é a própria <see cref="Evolucao"/>, e não se duplica aqui — duas
/// cópias do texto atual dariam duas verdades sobre o mesmo registro.
/// </summary>
public class VersaoEvolucao
{
    public int Id { get; set; }

    public int EvolucaoId { get; set; }
    public Evolucao? Evolucao { get; set; }

    /// <summary>1 para o conteúdo original, 2 para o seguinte, e assim por diante.</summary>
    public int Versao { get; set; }

    // ---- O conteúdo congelado, como estava antes desta alteração ----

    public DateOnly Data { get; set; }
    public int? EvaAntes { get; set; }
    public int? EvaDepois { get; set; }
    public string? QueixaPrincipal { get; set; }
    public string? Conduta { get; set; }
    public string? TextoEvolucao { get; set; }
    public string? Orientacoes { get; set; }
    public int? ProfissionalId { get; set; }

    /// <summary>Quando esta versão foi SUBSTITUÍDA (isto é, o momento da correção).</summary>
    public DateTime SubstituidaEm { get; set; } = DateTime.Now;

    /// <summary>Quem fez a correção que aposentou esta versão.</summary>
    public string? SubstituidaPor { get; set; }

    /// <summary>
    /// Por que foi corrigida, quando informado.
    ///
    /// Não é obrigatório, e isso é decisão: a evolução é escrita em várias passadas
    /// durante o atendimento, e exigir justificativa a cada Salvar faria o profissional
    /// digitar "ajuste" trinta vezes por dia — o que produz rastro com aparência de
    /// controle e nenhum conteúdo. O que a lei pede é poder recuperar o que estava
    /// escrito, e isso a versão entrega com ou sem o motivo.
    /// </summary>
    public string? Motivo { get; set; }

    /// <summary>Resumo de uma linha, para a lista do histórico não precisar abrir cada versão.</summary>
    public string Resumo
    {
        get
        {
            var partes = new List<string>();
            if (!string.IsNullOrWhiteSpace(QueixaPrincipal)) partes.Add("queixa");
            if (!string.IsNullOrWhiteSpace(Conduta)) partes.Add("conduta");
            if (!string.IsNullOrWhiteSpace(TextoEvolucao)) partes.Add("evolução");
            if (!string.IsNullOrWhiteSpace(Orientacoes)) partes.Add("orientações");
            if (EvaAntes is not null || EvaDepois is not null) partes.Add("EVA");

            return partes.Count == 0 ? "(sem conteúdo)" : string.Join(" · ", partes);
        }
    }
}

/// <summary>Para que o paciente deu (ou negou) consentimento.</summary>
public enum FinalidadeConsentimento
{
    /// <summary>Tratar os dados pessoais e de saúde para o atendimento.</summary>
    TratamentoDeDados,

    /// <summary>Usar imagem (foto do cadastro, fotos clínicas).</summary>
    UsoDeImagem,

    /// <summary>Receber mensagens de confirmação, recall e campanhas.</summary>
    ComunicacaoEMarketing,

    /// <summary>Compartilhar dados com o convênio para faturamento.</summary>
    CompartilhamentoComConvenio
}

/// <summary>
/// Consentimento LGPD registrado para uma finalidade.
///
/// Cada registro é um FATO datado, não um interruptor: conceder, negar e revogar
/// criam linhas novas em vez de sobrescrever. Consentimento sem histórico não prova
/// nada — e provar é o motivo de a lei exigir o registro.
/// </summary>
public class ConsentimentoLgpd
{
    public int Id { get; set; }

    public int PacienteId { get; set; }
    public Paciente? Paciente { get; set; }

    public FinalidadeConsentimento Finalidade { get; set; }

    /// <summary>True = concedeu; false = negou.</summary>
    public bool Concedido { get; set; }

    /// <summary>Versão do termo apresentado (o texto muda com o tempo).</summary>
    public string? VersaoTermo { get; set; }

    public DateTime RegistradoEm { get; set; } = DateTime.Now;

    /// <summary>Quem colheu o consentimento no balcão.</summary>
    public string? RegistradoPor { get; set; }

    /// <summary>
    /// Quando foi revogado. Revogar NÃO apaga o registro: a linha continua provando
    /// que houve consentimento no período em que os dados foram tratados.
    /// </summary>
    public DateTime? RevogadoEm { get; set; }

    public string? Observacoes { get; set; }

    /// <summary>Vale agora: foi concedido e não foi revogado.</summary>
    public bool Vigente => Concedido && RevogadoEm is null;
}
