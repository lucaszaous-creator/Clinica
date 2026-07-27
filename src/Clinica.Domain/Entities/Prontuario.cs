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

    /// <summary>O que foi feito: pontos, técnica, tempo de agulhamento…</summary>
    public string? Conduta { get; set; }

    /// <summary>Evolução em texto livre — a leitura clínica da sessão.</summary>
    public string? TextoEvolucao { get; set; }

    /// <summary>Orientações dadas ao paciente ao final.</summary>
    public string? Orientacoes { get; set; }

    public DateTime CriadoEm { get; set; } = DateTime.Now;

    public string? CriadoPor { get; set; }

    public DateTime? AtualizadoEm { get; set; }

    public List<AnexoProntuario> Anexos { get; set; } = new();

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
