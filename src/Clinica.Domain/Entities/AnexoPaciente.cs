namespace Clinica.Domain.Entities;

/// <summary>
/// Um ARQUIVO DA FICHA do paciente (set/2026): a receita, o laudo, o exame em PDF que
/// pertence à PESSOA e não a uma sessão.
///
/// Por que uma entidade nova, e não o anexo que já existia
/// -------------------------------------------------------
/// O <see cref="AnexoProntuario"/> pende de uma <see cref="Evolucao"/> por desenho —
/// <c>EvolucaoId</c> é obrigatório e a leitura resolve o paciente ATRAVÉS da sessão. Serve
/// ao laudo que chega junto da consulta; não serve ao que chega SEM consulta: o PDF que o
/// laboratório manda por WhatsApp, e o acervo inteiro do sistema anterior (756 receitas de
/// 113 pacientes, emitidas lá, sem sessão correspondente aqui). Pendurá-los numa evolução
/// inventaria uma consulta que não houve; alterar a FK do anexo é migration não aditiva
/// num app em produção (a lição medida ao construir o laudo em arquivo).
///
/// As decisões, todas herdadas do laudo em arquivo (<see cref="ResultadoExame"/>):
///
/// 1. <b>Metadados na linha, BYTES em tabela 1:1</b> (<see cref="ArquivoAnexoPaciente"/>),
///    carregada só quando alguém abre — a lista é lida a cada abertura de ficha, e um PDF
///    por linha tornaria a leitura impraticável num banco remoto (parcela 74).
/// 2. <b>Registro clínico NÃO SE APAGA</b> (Lei 13.787/2018): não há edição nem exclusão —
///    o arquivo errado se CANCELA com motivo escrito, e a linha fica, marcada.
/// 3. <b>A data é a do DOCUMENTO, informada — nunca a de hoje.</b> A receita de 2024 entra
///    com a data de 2024; o relógio de quem anexou fica em <see cref="CriadoEm"/>, ao lado.
/// 4. <b>Idempotente pela chave de importação</b> (<see cref="ChaveImportacao"/>, índice
///    único): o mesmo ZIP do sistema anterior importado duas vezes não duplica arquivo.
/// </summary>
public class AnexoPaciente
{
    public int Id { get; set; }

    public int PacienteId { get; set; }
    public Paciente? Paciente { get; set; }

    /// <summary>A data do DOCUMENTO (a da receita, a do laudo) — a data clínica, informada.</summary>
    public DateOnly Data { get; set; }

    /// <summary>O que é o arquivo, como a clínica o chama: "Receita #164001527", "Ressonância lombar".</summary>
    public string Titulo { get; set; } = string.Empty;

    /// <summary>Nome do arquivo, como veio. É por ele que a folha é achada depois.</summary>
    public string NomeArquivo { get; set; } = string.Empty;

    /// <summary>MIME (application/pdf, image/jpeg…) — decide como abrir.</summary>
    public string? TipoConteudo { get; set; }

    /// <summary>Tamanho em bytes, para a tela dizer o peso sem abrir o arquivo.</summary>
    public int Tamanho { get; set; }

    /// <summary>Procedência e nota: "Importado do Smart Clinic · emitido em 26/10/2024".</summary>
    public string? Observacoes { get; set; }

    /// <summary>
    /// <c>IMPORT:smartclinic:arquivo:{id_arquivo}</c> para o que veio do sistema anterior;
    /// nulo para o que a clínica anexou por aqui. Índice ÚNICO: é o que faz a segunda
    /// importação do mesmo ZIP pular o que já entrou.
    /// </summary>
    public string? ChaveImportacao { get; set; }

    public DateTime CriadoEm { get; set; } = DateTime.Now;
    public string? CriadoPor { get; set; }

    public DateTime? CanceladoEm { get; set; }
    public string? CanceladoPor { get; set; }
    public string? MotivoCancelamento { get; set; }

    /// <summary>Os bytes. Só é carregado quando explicitamente pedido.</summary>
    public ArquivoAnexoPaciente? Arquivo { get; set; }

    /// <summary>⚠️ Derivada — em consulta traduzida use a COLUNA (CanceladoEm == null).</summary>
    public bool Cancelado => CanceladoEm is not null;

    /// <summary>⚠️ Derivada — "importado do sistema anterior" é procedência, não estado.</summary>
    public bool Importado => ChaveImportacao is not null;
}

/// <summary>
/// Os BYTES do arquivo da ficha, em tabela 1:1 — o padrão do retrato do paciente e do laudo
/// em arquivo. Não há exclusão: o arquivo é registro clínico e segue a linha que o
/// descreve — o anexo se CANCELA com motivo, e os bytes ficam junto (Lei 13.787/2018).
/// </summary>
public class ArquivoAnexoPaciente
{
    /// <summary>Chave primária e estrangeira: cada anexo da ficha tem exatamente um arquivo.</summary>
    public int AnexoPacienteId { get; set; }

    public byte[] Conteudo { get; set; } = Array.Empty<byte>();

    public AnexoPaciente? Anexo { get; set; }
}
