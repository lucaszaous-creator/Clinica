namespace Clinica.Domain.Entities;

/// <summary>
/// Um resultado de exame ESTRUTURADO do paciente (ago/2026 — a tela de referência do
/// handoff mostrava exames com campos tipados, e a direção decidiu construir).
///
/// Até aqui o laudo só existia como ANEXO (o PDF digitalizado) ou como texto dentro da
/// evolução — dava para guardar e para ler, e não dava para PERGUNTAR: "qual era a
/// glicada dele em março?" exigia abrir laudo por laudo. Este registro é o valor que se
/// consulta e se compara; o laudo continua sendo o anexo, e um não substitui o outro.
///
/// As decisões que não são óbvias:
///
/// 1. <b>O valor é TEXTO LIVRE, por desenho.</b> Resultado de laboratório é heterogêneo
///    ("6,1", "não reagente", "&lt; 0,01", "positivo 1/80") e um campo numérico recusaria
///    metade dos laudos reais — a regra apertada demais que o projeto já rejeitou no
///    formato do número da guia. A REFERÊNCIA vem do próprio laudo, copiada, porque cada
///    laboratório tem a sua: inventar uma faixa "normal" aqui seria o sistema opinando
///    sobre o que só o método do laboratório sabe.
///
/// 2. <b>Registro clínico NÃO SE APAGA</b> (Lei 13.787/2018; regra 1 do compromisso de
///    conformidade): não há edição nem exclusão — um valor digitado errado se CANCELA
///    com motivo escrito e se registra de novo. A linha cancelada fica, marcada.
///
/// 3. <b>A data é a do EXAME (coleta/laudo), informada — nunca a de hoje.</b> O resultado
///    chega dias depois da coleta, e é a data clínica que a curva e a guarda usam; o
///    relógio de quem digitou fica em <see cref="CriadoEm"/>, ao lado.
/// </summary>
public class ResultadoExame
{
    public int Id { get; set; }

    public int PacienteId { get; set; }
    public Paciente? Paciente { get; set; }

    /// <summary>
    /// O PEDIDO DE EXAME que este resultado responde (o DocumentoClinico de tipo
    /// PedidoExame), quando a clínica o amarrou. É este elo que faz a tela de Exames
    /// dizer "Aguardando resultado" ou "Resultado disponível" por FATO — sem ele a
    /// situação seria chute com cara de registro.
    ///
    /// Nulo é o caso normal: resultado trazido pelo paciente sem pedido nosso, ou
    /// registrado antes de o vínculo existir (set/2026). O serviço RECUSA amarrar num
    /// pedido de OUTRO paciente ou num documento que não é pedido — o vínculo errado
    /// daria baixa na espera de outra pessoa.
    /// </summary>
    public int? PedidoDocumentoId { get; set; }
    public DocumentoClinico? PedidoDocumento { get; set; }

    /// <summary>Data do exame (coleta ou laudo) — a data CLÍNICA, informada.</summary>
    public DateOnly Data { get; set; }

    /// <summary>O que foi medido — "Hemoglobina glicada", "Hemograma — leucócitos".</summary>
    public string Nome { get; set; } = string.Empty;

    /// <summary>O resultado, como o laudo o escreve.</summary>
    public string Valor { get; set; } = string.Empty;

    public string? Unidade { get; set; }

    /// <summary>A faixa de referência DO LAUDO, copiada — cada laboratório tem a sua.</summary>
    public string? Referencia { get; set; }

    /// <summary>Quem emitiu o laudo (laboratório/serviço) — a procedência do número.</summary>
    public string? Laboratorio { get; set; }

    public string? Observacoes { get; set; }

    // ===== O LAUDO EM ARQUIVO (set/2026) =====
    //
    // O laudo chega por WhatsApp, e-mail ou papel escaneado, e é ELE que o profissional
    // quer ver — o valor estruturado acima é o que se COMPARA depois. Os dois convivem:
    // dá para registrar só o número, só o arquivo, ou os dois.
    //
    // ⚠️ Os METADADOS ficam aqui e os BYTES moram em <see cref="ArquivoResultadoExame"/>,
    // tabela 1:1 carregada sob demanda — é o padrão do retrato do paciente
    // (FotoMiniatura na linha, PacientesFotos à parte). Pôr o byte[] nesta entidade faria
    // TODA leitura de resultados arrastar os PDFs pela rede, que é a lição da parcela 74.

    /// <summary>Nome do arquivo do laudo, como veio. Nulo = resultado sem arquivo.</summary>
    public string? ArquivoNome { get; set; }

    /// <summary>MIME do laudo (application/pdf, image/jpeg…) — decide como abrir.</summary>
    public string? ArquivoTipoConteudo { get; set; }

    /// <summary>Tamanho em bytes, para a tela dizer o peso sem abrir o arquivo.</summary>
    public int? ArquivoTamanho { get; set; }

    /// <summary>Os bytes do laudo. Só é carregado quando explicitamente pedido.</summary>
    public ArquivoResultadoExame? Arquivo { get; set; }

    public DateTime CriadoEm { get; set; } = DateTime.Now;
    public string? CriadoPor { get; set; }

    public DateTime? CanceladoEm { get; set; }
    public string? MotivoCancelamento { get; set; }
    public string? CanceladoPor { get; set; }

    /// <summary>⚠️ Derivada — em consulta traduzida use a COLUNA (CanceladoEm == null).</summary>
    public bool Cancelado => CanceladoEm is not null;

    /// <summary>"6,1 %" — o valor com a unidade, quando há.</summary>
    public string ValorComUnidade => string.IsNullOrWhiteSpace(Unidade)
        ? Valor
        : $"{Valor} {Unidade}";

    /// <summary>⚠️ Derivada — em consulta traduzida use a COLUNA (ArquivoNome != null).</summary>
    public bool TemArquivo => !string.IsNullOrWhiteSpace(ArquivoNome);

    /// <summary>
    /// O que a tela mostra na linha: o valor digitado, e o NOME DO ARQUIVO quando o
    /// registro é só o laudo. Vazio seria uma linha que não diz o que ela é.
    /// </summary>
    public string ResumoDoResultado => !string.IsNullOrWhiteSpace(Valor)
        ? ValorComUnidade
        : TemArquivo ? $"laudo em arquivo — {ArquivoNome}" : "(sem conteúdo)";
}

/// <summary>
/// Os BYTES do laudo, em tabela 1:1 com o resultado — o padrão do retrato do paciente:
/// a lista de resultados é lida a cada abertura de tela, e um PDF de 4 MB por linha
/// tornaria a leitura impraticável num banco remoto (a lição da parcela 74).
///
/// Não há exclusão: o laudo é registro clínico e segue a linha que o guarda — o
/// resultado se CANCELA com motivo, e o arquivo fica junto (Lei 13.787/2018).
/// </summary>
public class ArquivoResultadoExame
{
    /// <summary>Chave primária e estrangeira: cada resultado tem no máximo um laudo.</summary>
    public int ResultadoExameId { get; set; }

    /// <summary>O arquivo como veio — PDF do laboratório, foto do papel, imagem do exame.</summary>
    public byte[] Conteudo { get; set; } = Array.Empty<byte>();

    public ResultadoExame? Resultado { get; set; }
}
