using Clinica.Domain.Avaliacoes;

namespace Clinica.Domain.Entities;

/// <summary>
/// Um instrumento de avaliação APLICADO a um paciente num dia.
///
/// A regra que governa a entidade inteira: **tudo o que descreve o instrumento é COPIADO
/// na aplicação, nunca referenciado.** Nome, enunciado de cada item, rótulo da alternativa
/// escolhida, faixa e interpretação ficam gravados aqui. É a mesma decisão do protocolo do
/// mapa corporal, do preço por convênio e do documento clínico — e pelo mesmo motivo:
/// corrigir a redação de uma pergunta amanhã não pode reescrever o que o paciente
/// respondeu hoje. Prontuário descreve o que ACONTECEU.
///
/// O que NÃO é copiado é a pontuação: ela é recalculada pelo instrumento na hora de
/// aplicar e gravada como resultado. Guardar a fórmula não faria sentido; guardar o
/// resultado, sim.
///
/// O vínculo com <see cref="Evolucao"/> é opcional e N:1 — o psiquiatra aplica PHQ-9 e
/// GAD-7 na mesma consulta, e o paciente pode responder uma escala sem que haja sessão
/// escrita (retorno curto só para reavaliar o escore).
/// </summary>
public class AvaliacaoClinica
{
    public int Id { get; set; }

    public int PacienteId { get; set; }
    public Paciente? Paciente { get; set; }

    /// <summary>Quem aplicou. Null quando a clínica ainda não cadastrou a equipe.</summary>
    public int? ProfissionalId { get; set; }
    public Profissional? Profissional { get; set; }

    /// <summary>Sessão do prontuário a que a avaliação pertence, quando há uma.</summary>
    public int? EvolucaoId { get; set; }
    public Evolucao? Evolucao { get; set; }

    public DateOnly Data { get; set; }

    /// <summary>Código do instrumento no <c>RegistroInstrumentos</c> (ex.: <c>PHQ9</c>).</summary>
    public string InstrumentoCodigo { get; set; } = string.Empty;

    /// <summary>Nome do instrumento no dia da aplicação — copiado.</summary>
    public string InstrumentoNome { get; set; } = string.Empty;

    /// <summary>
    /// Especialidade em que a avaliação foi aplicada, pelo código do catálogo. Guardada
    /// porque o mesmo instrumento é oferecido a mais de uma (o Oswestry serve a
    /// neurocirurgia e à acupuntura), e a leitura por especialidade não teria como
    /// reconstruir isso depois.
    /// </summary>
    public string? EspecialidadeCodigo { get; set; }

    /// <summary>Escore apurado. Na unidade do instrumento (o Oswestry grava porcentagem).</summary>
    public int Pontuacao { get; set; }

    /// <summary>Teto do escore no dia da aplicação — copiado, para a tela dizer "12 de 27".</summary>
    public int PontuacaoMaxima { get; set; }

    /// <summary>Unidade do escore ("pontos", "%") — copiada.</summary>
    public string Unidade { get; set; } = "pontos";

    /// <summary>Nome da faixa em que o escore caiu — copiado.</summary>
    public string? FaixaNome { get; set; }

    /// <summary>Leitura publicada da faixa — copiada.</summary>
    public string? FaixaInterpretacao { get; set; }

    /// <summary>Gravidade da faixa no dia — copiada.</summary>
    public GravidadeFaixa FaixaGravidade { get; set; } = GravidadeFaixa.Normal;

    /// <summary>
    /// Alerta vindo de um item isolado (o item 9 do PHQ-9), copiado da aplicação.
    ///
    /// É gravado, e não recalculado na leitura, porque a lista do prontuário precisa
    /// mostrá-lo sem reabrir item por item — e porque ele é parte do que aconteceu
    /// naquele dia, como o resto.
    /// </summary>
    public string? AlertaItem { get; set; }

    /// <summary>O que o profissional escreveu sobre este resultado.</summary>
    public string? Observacoes { get; set; }

    public DateTime CriadoEm { get; set; } = DateTime.Now;

    public string? CriadoPor { get; set; }

    public List<RespostaAvaliacao> Respostas { get; set; } = new();

    /// <summary>
    /// Quando a avaliação foi CANCELADA. Não se apaga (parcela 52): a escala aplicada é
    /// registro de saúde e responde por uma conduta tomada na época. Vale mais aqui do
    /// que na evolução, inclusive — é o escore que decidiu encaminhar (ou não) um
    /// paciente que marcou o item 9 do PHQ-9.
    /// </summary>
    public DateTime? CanceladaEm { get; set; }

    public string? MotivoCancelamento { get; set; }

    public string? CanceladaPor { get; set; }

    public bool Cancelada => CanceladaEm is not null;

    /// <summary>Escore como a tela mostra: "12 de 27 pontos", "38 %".</summary>
    public string PontuacaoFormatada => Unidade == "%"
        ? $"{Pontuacao} %"
        : $"{Pontuacao} de {PontuacaoMaxima} {Unidade}";

    /// <summary>Há alerta de item — o que nunca pode ficar escondido dentro do total.</summary>
    public bool TemAlertaDeItem => !string.IsNullOrWhiteSpace(AlertaItem);
}

/// <summary>
/// Uma resposta dada a um item, com o enunciado e o rótulo da alternativa COPIADOS.
///
/// Guardar só o par (código do item, peso) faria a segunda via da avaliação depender da
/// versão atual do questionário: bastaria alguém corrigir uma redação para o prontuário
/// passar a afirmar que o paciente respondeu a uma pergunta que nunca lhe foi feita.
/// </summary>
public class RespostaAvaliacao
{
    public int Id { get; set; }

    public int AvaliacaoClinicaId { get; set; }
    public AvaliacaoClinica? Avaliacao { get; set; }

    /// <summary>Posição do item no questionário, para a leitura sair na ordem aplicada.</summary>
    public int Ordem { get; set; }

    public string ItemCodigo { get; set; } = string.Empty;

    /// <summary>Pergunta como ela estava escrita no dia — copiada.</summary>
    public string Enunciado { get; set; } = string.Empty;

    /// <summary>Peso da alternativa escolhida.</summary>
    public int Valor { get; set; }

    /// <summary>Texto da alternativa escolhida — copiado.</summary>
    public string OpcaoRotulo { get; set; } = string.Empty;
}
