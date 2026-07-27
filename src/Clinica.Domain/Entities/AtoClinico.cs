namespace Clinica.Domain.Entities;

/// <summary>
/// Qual figura do mapa recebeu a marcação. São duas porque é o que a sessão de
/// acupuntura precisa: o profissional marca de frente e de costas. Vista lateral só
/// entraria se houvesse desenho para ela — campo sem tela é campo que ninguém preenche.
/// </summary>
public enum FaceCorpo
{
    Frente,
    Costas
}

/// <summary>O que foi aplicado no ponto. A conduta em texto continua existindo; isto é
/// o que dá para contar depois (quantas ventosas, quanta moxa) sem ler prontuário.</summary>
public enum TecnicaPonto
{
    Agulha,
    Eletroacupuntura,
    Moxa,
    Ventosa,
    Auriculoterapia,
    Laser,
    Outra
}

/// <summary>
/// Um ponto marcado no mapa corporal de uma sessão.
///
/// As coordenadas são NORMALIZADAS (0 a 1) sobre a figura, nunca pixels: a tela pode
/// crescer, mudar de resolução ou ser redesenhada que a marcação continua no mesmo
/// lugar do corpo. Guardar pixel aqui seria amarrar o prontuário ao tamanho da janela
/// do dia em que ele foi escrito.
/// </summary>
public class PontoMapa
{
    public int Id { get; set; }

    public int MapaCorporalId { get; set; }
    public MapaCorporal? Mapa { get; set; }

    public FaceCorpo Face { get; set; }

    /// <summary>Posição horizontal na figura, de 0 (esquerda) a 1 (direita).</summary>
    public double X { get; set; }

    /// <summary>Posição vertical na figura, de 0 (topo) a 1 (base).</summary>
    public double Y { get; set; }

    /// <summary>Nome do ponto/meridiano, como o profissional escreve (ex.: "IG4", "VB20").</summary>
    public string? Nome { get; set; }

    public TecnicaPonto Tecnica { get; set; } = TecnicaPonto.Agulha;

    public string? Observacao { get; set; }

    /// <summary>Ordem de marcação — é ela que numera as bolinhas na tela e no PDF.</summary>
    public int Ordem { get; set; }
}

/// <summary>
/// O mapa corporal de UMA sessão: onde as agulhas (ou ventosas, ou moxa) foram
/// aplicadas. Feature 06 da proposta.
///
/// Existe UM mapa por evolução — o mapa não é um documento à parte, é a parte
/// desenhada da mesma sessão. Por isso o vínculo é 1:1 e apagar a evolução leva o
/// mapa junto: mapa órfão não diz de quem nem de quando.
/// </summary>
public class MapaCorporal
{
    public int Id { get; set; }

    public int EvolucaoId { get; set; }
    public Evolucao? Evolucao { get; set; }

    /// <summary>
    /// Protocolo de onde os pontos vieram, quando o mapa nasceu de um. Guardado para a
    /// tela poder dizer "aplicado do protocolo X" — depois de aplicado, os pontos são
    /// do paciente e podem ser editados sem mexer no protocolo.
    /// </summary>
    public int? ProtocoloOrigemId { get; set; }

    public string? Observacoes { get; set; }

    public DateTime CriadoEm { get; set; } = DateTime.Now;

    public string? CriadoPor { get; set; }

    public DateTime? AtualizadoEm { get; set; }

    public List<PontoMapa> Pontos { get; set; } = new();

    /// <summary>
    /// Teto de marcações num mapa. Não é limitação técnica: é o sinal de que alguém
    /// está clicando à toa — uma sessão de acupuntura não passa disto.
    /// </summary>
    public const int MaximoPontos = 80;

    /// <summary>Coordenada dentro da figura (0 a 1).</summary>
    public static bool CoordenadaValida(double valor) => valor >= 0 && valor <= 1;
}

/// <summary>
/// Um conjunto de pontos guardado para ser reaplicado. É o "protocolo reutilizável"
/// que a proposta vendeu com o mapa corporal.
///
/// Serve a dois usos que parecem um só: o protocolo DA CLÍNICA
/// (<see cref="PacienteId"/> nulo — "Lombalgia — padrão", vale para qualquer paciente)
/// e o protocolo DE UM PACIENTE ("o esquema da dona Maria"). É o mesmo objeto porque
/// é o mesmo gesto: marcar uma vez e repetir nas próximas sessões.
/// </summary>
public class ProtocoloCorporal
{
    public int Id { get; set; }

    public string Nome { get; set; } = string.Empty;

    public string? Descricao { get; set; }

    /// <summary>Dono do protocolo. Null = protocolo da clínica, vale para todo mundo.</summary>
    public int? PacienteId { get; set; }
    public Paciente? Paciente { get; set; }

    /// <summary>Aposentado sai das listas sem sumir dos mapas que já o aplicaram.</summary>
    public bool Ativo { get; set; } = true;

    public DateTime CriadoEm { get; set; } = DateTime.Now;

    public string? CriadoPor { get; set; }

    public DateTime? AtualizadoEm { get; set; }

    public List<PontoProtocolo> Pontos { get; set; } = new();

    /// <summary>Protocolo da clínica (sem dono), que aparece para qualquer paciente.</summary>
    public bool EhDaClinica => PacienteId is null;
}

/// <summary>
/// Um ponto de um protocolo. Tem os mesmos campos de <see cref="PontoMapa"/> de
/// propósito: aplicar um protocolo é copiar ponto a ponto para a sessão, e a partir daí
/// os dois seguem vidas separadas — editar o mapa do paciente não pode reescrever o
/// protocolo da clínica.
/// </summary>
public class PontoProtocolo
{
    public int Id { get; set; }

    public int ProtocoloCorporalId { get; set; }
    public ProtocoloCorporal? Protocolo { get; set; }

    public FaceCorpo Face { get; set; }

    public double X { get; set; }

    public double Y { get; set; }

    public string? Nome { get; set; }

    public TecnicaPonto Tecnica { get; set; } = TecnicaPonto.Agulha;

    public string? Observacao { get; set; }

    public int Ordem { get; set; }
}

/// <summary>
/// Os documentos clínicos da página 21 da proposta que saem da Recepção.
///
/// Os quatro primeiros são ESCRITOS pelo profissional; os três últimos são MONTADOS a
/// partir do que o sistema já tem (prontuário, cadastro, consentimentos). A diferença
/// importa na emissão, não na impressão: todos viram o mesmo registro numerado.
/// </summary>
public enum TipoDocumentoClinico
{
    Receita,
    Atestado,
    Comparecimento,
    PedidoExame,
    RelatorioEvolucao,
    Consentimento,
    Anamnese
}

/// <summary>Rótulos e natureza de cada tipo de documento, para a tela e o PDF.</summary>
public static class TipoDocumentoInfo
{
    public static string Rotular(TipoDocumentoClinico tipo) => tipo switch
    {
        TipoDocumentoClinico.Receita => "Receita",
        TipoDocumentoClinico.Atestado => "Atestado",
        TipoDocumentoClinico.Comparecimento => "Declaração de comparecimento",
        TipoDocumentoClinico.PedidoExame => "Pedido de exame",
        TipoDocumentoClinico.RelatorioEvolucao => "Relatório de evolução",
        TipoDocumentoClinico.Consentimento => "Termo de consentimento",
        TipoDocumentoClinico.Anamnese => "Anamnese",
        _ => tipo.ToString()
    };

    /// <summary>
    /// O documento é montado pelo sistema a partir do prontuário/cadastro (em vez de
    /// digitado). São os três que a parcela 2 deixou prontos no domínio e sem papel.
    /// </summary>
    public static bool EhMontadoDoProntuario(TipoDocumentoClinico tipo)
        => tipo is TipoDocumentoClinico.RelatorioEvolucao
               or TipoDocumentoClinico.Consentimento
               or TipoDocumentoClinico.Anamnese;

    /// <summary>O documento é uma lista de itens (medicamentos, exames) e exige ao menos um.</summary>
    public static bool ExigeItens(TipoDocumentoClinico tipo)
        => tipo is TipoDocumentoClinico.Receita or TipoDocumentoClinico.PedidoExame;

    public static IReadOnlyList<TipoDocumentoClinico> Todos { get; } =
    [
        TipoDocumentoClinico.Receita,
        TipoDocumentoClinico.Atestado,
        TipoDocumentoClinico.Comparecimento,
        TipoDocumentoClinico.PedidoExame,
        TipoDocumentoClinico.RelatorioEvolucao,
        TipoDocumentoClinico.Consentimento,
        TipoDocumentoClinico.Anamnese
    ];
}

/// <summary>
/// Um documento clínico EMITIDO: receita, atestado, declaração, pedido de exame,
/// relatório, termo ou anamnese. Feature 07 e a página 21 da proposta.
///
/// Documento emitido é FATO, como o consentimento da parcela 2: uma vez impresso e
/// entregue, ele existe no mundo. Por isso não se apaga nem se reescreve — corrige-se
/// CANCELANDO com motivo e emitindo outro. Por isso também o conteúdo fica gravado
/// aqui em vez de ser remontado na hora de reimprimir: a segunda via tem de sair
/// idêntica à que o paciente levou, mesmo que o prontuário tenha andado desde então.
/// </summary>
public class DocumentoClinico
{
    public int Id { get; set; }

    /// <summary>Número sequencial por ano, no formato <c>2026/0001</c>.</summary>
    public string Numero { get; set; } = string.Empty;

    /// <summary>
    /// Código curto impresso no rodapé para conferir a autenticidade da via em papel
    /// (a clínica procura por ele na lista de documentos do paciente).
    /// </summary>
    public string CodigoVerificacao { get; set; } = string.Empty;

    public TipoDocumentoClinico Tipo { get; set; }

    public int PacienteId { get; set; }
    public Paciente? Paciente { get; set; }

    /// <summary>Quem assina. Null quando a clínica ainda não cadastrou a equipe.</summary>
    public int? ProfissionalId { get; set; }
    public Profissional? Profissional { get; set; }

    /// <summary>Sessão de origem, quando o documento nasceu de uma evolução.</summary>
    public int? EvolucaoId { get; set; }
    public Evolucao? Evolucao { get; set; }

    public DateOnly Data { get; set; }

    /// <summary>Título impresso. Vazio usa o rótulo do tipo.</summary>
    public string? Titulo { get; set; }

    /// <summary>Corpo do documento: o texto que vai impresso acima da lista de itens.</summary>
    public string? Corpo { get; set; }

    public string? Observacoes { get; set; }

    // ---- Atestado ----

    /// <summary>Dias de afastamento do atestado.</summary>
    public int? DiasAfastamento { get; set; }

    /// <summary>CID informado no atestado.</summary>
    public string? Cid { get; set; }

    /// <summary>
    /// O paciente autorizou imprimir o CID. Sem autorização expressa o diagnóstico NÃO
    /// vai no atestado — é sigilo do paciente, e o PDF respeita isto mesmo com o campo
    /// preenchido.
    /// </summary>
    public bool CidAutorizado { get; set; }

    // ---- Comparecimento e relatório ----

    public DateOnly? PeriodoInicio { get; set; }
    public DateOnly? PeriodoFim { get; set; }

    public TimeOnly? HoraChegada { get; set; }
    public TimeOnly? HoraSaida { get; set; }

    // ---- Rastro ----

    public DateTime CriadoEm { get; set; } = DateTime.Now;

    public string? CriadoPor { get; set; }

    /// <summary>Quando foi cancelado. Cancelar NÃO apaga: a via em papel continua no mundo.</summary>
    public DateTime? CanceladoEm { get; set; }

    public string? MotivoCancelamento { get; set; }

    public List<ItemDocumento> Itens { get; set; } = new();

    public bool Cancelado => CanceladoEm is not null;

    /// <summary>Título que vai impresso: o informado ou, na falta, o rótulo do tipo.</summary>
    public string TituloImpresso
        => string.IsNullOrWhiteSpace(Titulo) ? TipoDocumentoInfo.Rotular(Tipo) : Titulo!;

    /// <summary>O CID só sai impresso quando o paciente autorizou.</summary>
    public string? CidImpresso
        => CidAutorizado && !string.IsNullOrWhiteSpace(Cid) ? Cid : null;
}

/// <summary>
/// Uma linha do documento. É genérica de propósito, porque as sete impressões são a
/// mesma forma com nomes diferentes: medicamento + posologia na receita, exame +
/// indicação no pedido, sessão + evolução no relatório, pergunta + resposta na
/// anamnese. Uma tabela por tipo daria sete tabelas com as mesmas três colunas.
/// </summary>
public class ItemDocumento
{
    public int Id { get; set; }

    public int DocumentoClinicoId { get; set; }
    public DocumentoClinico? Documento { get; set; }

    public int Ordem { get; set; }

    /// <summary>A linha em si: o medicamento, o exame, a data da sessão, a pergunta.</summary>
    public string Descricao { get; set; } = string.Empty;

    /// <summary>O que a acompanha: posologia, indicação clínica, resposta, evolução.</summary>
    public string? Detalhe { get; set; }

    /// <summary>Quantidade, quando faz sentido ("1 caixa", "2 frascos").</summary>
    public string? Quantidade { get; set; }
}

/// <summary>
/// Modelo reutilizável de documento — os "modelos de receita e orientação" da feature 07.
///
/// Nasce de um documento que o profissional acabou de escrever ("salvar como modelo"),
/// que é o gesto real: ninguém senta para cadastrar modelos antes de precisar deles.
/// Por isso não há tela própria de cadastro; o modelo se cria e se aplica de dentro da
/// janela do documento.
/// </summary>
public class ModeloDocumento
{
    public int Id { get; set; }

    public TipoDocumentoClinico Tipo { get; set; }

    public string Nome { get; set; } = string.Empty;

    public string? Titulo { get; set; }

    public string? Corpo { get; set; }

    public bool Ativo { get; set; } = true;

    public int Ordem { get; set; }

    public DateTime CriadoEm { get; set; } = DateTime.Now;

    public string? CriadoPor { get; set; }

    public DateTime? AtualizadoEm { get; set; }

    public List<ItemModelo> Itens { get; set; } = new();
}

/// <summary>Uma linha de um modelo, com os mesmos campos de <see cref="ItemDocumento"/>.</summary>
public class ItemModelo
{
    public int Id { get; set; }

    public int ModeloDocumentoId { get; set; }
    public ModeloDocumento? Modelo { get; set; }

    public int Ordem { get; set; }

    public string Descricao { get; set; } = string.Empty;

    public string? Detalhe { get; set; }

    public string? Quantidade { get; set; }
}
