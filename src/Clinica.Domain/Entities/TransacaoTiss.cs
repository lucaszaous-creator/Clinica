namespace Clinica.Domain.Entities;

/// <summary>O que a clínica pede ao web service TISS de uma operadora.</summary>
public enum TipoTransacaoTiss
{
    /// <summary>Perguntar se a carteira está ativa (<c>tissVerificaElegibilidade</c>). Só lê.</summary>
    Elegibilidade,

    /// <summary>Enviar UMA guia à operadora (<c>tissLoteGuias</c>). Escreve na operadora.</summary>
    EnvioGuia,

    /// <summary>Perguntar a situação de um protocolo já recebido (<c>tissSolicitacaoStatusProtocolo</c>). Só lê.</summary>
    StatusProtocolo,

    /// <summary>Cancelar uma guia já enviada (<c>tissCancelaGuia</c>). Escreve na operadora.</summary>
    CancelamentoGuia
}

/// <summary>Onde a transação está na fila.</summary>
public enum EstadoTransacaoTiss
{
    /// <summary>Na fila, à espera do conector.</summary>
    Pendente,

    /// <summary>Reservada por um conector, que está falando (ou vai falar) com a operadora.</summary>
    EmEnvio,

    /// <summary>A operadora respondeu e o resultado está gravado.</summary>
    Concluida,

    /// <summary>Não deu certo e não será tentada de novo sozinha: alguém precisa olhar.</summary>
    Falhou,

    /// <summary>Desistida por quem pediu, ou recusada pelo conector antes de ir à operadora.</summary>
    Cancelada
}

/// <summary>
/// Uma conversa com o web service TISS de uma operadora, do pedido ao resultado (nov/2026).
///
/// Por que existe uma FILA no banco
/// --------------------------------
/// Quem pede é a tela (um app desktop, que grava direto no banco e não fica ligado o dia todo);
/// quem fala com a operadora é o conector, um serviço no servidor. Os dois já se encontram no
/// banco — por isso a fila é uma tabela, e não um agendador novo.
///
/// O que NÃO é guardado aqui
/// -------------------------
/// A senha do web service, em forma nenhuma: nem em claro, nem em MD5. O XML enviado é gravado
/// com a tag da senha mascarada (<c>FilaTissService</c> mascara de novo ao gravar, mesmo que
/// quem chamou já tenha mascarado).
///
/// A regra que esta entidade protege
/// ---------------------------------
/// <see cref="EnvioIniciadoEm"/> é carimbado ANTES de a mensagem sair para a operadora. Uma
/// transação que ESCREVE na operadora (<see cref="EscreveNaOperadora"/>) e já tem esse carimbo
/// nunca volta sozinha para a fila: se a rede caiu no meio, ninguém sabe se a operadora
/// recebeu, e reenviar é arriscar a guia em duplicidade (glosa 1308, "guia já apresentada").
/// </summary>
public class TransacaoTiss
{
    public int Id { get; set; }

    public TipoTransacaoTiss Tipo { get; set; }

    /// <summary>Código do convênio no catálogo (o mesmo de <c>Paciente.ConvenioCodigo</c>, ou o nome da família embutida).</summary>
    public string ConvenioCodigo { get; set; } = string.Empty;

    /// <summary>A guia a que a transação se refere, quando há uma.</summary>
    public int? CodigoId { get; set; }

    /// <summary>O paciente a que a transação se refere, quando há um.</summary>
    public int? PacienteId { get; set; }

    public EstadoTransacaoTiss Estado { get; set; } = EstadoTransacaoTiss.Pendente;

    /// <summary>Quantas vezes o conector reservou esta transação.</summary>
    public int Tentativas { get; set; }

    /// <summary>Quem pediu: o operador que fez login (nunca o usuário do Windows).</summary>
    public string PedidoPor { get; set; } = string.Empty;

    public DateTime PedidoEm { get; set; }

    /// <summary>Quando a transação chegou a um estado final (concluída, falhou ou cancelada).</summary>
    public DateTime? ProcessadaEm { get; set; }

    // ---------- Dados do pedido ----------

    /// <summary>Elegibilidade: a carteira a conferir.</summary>
    public string? NumeroCarteira { get; set; }

    /// <summary>Elegibilidade: a validade da carteira, quando a clínica a tem.</summary>
    public DateOnly? ValidadeCarteira { get; set; }

    /// <summary>Cancelamento: o número da guia no prestador.</summary>
    public string? NumeroGuiaPrestador { get; set; }

    // ---------- Resultado ----------

    /// <summary>
    /// O protocolo. No envio de guia é o que a operadora DEVOLVEU; na consulta de status é o
    /// protocolo que se quer consultar (dado do pedido).
    /// </summary>
    public string? Protocolo { get; set; }

    /// <summary>O número da guia na operadora, quando ela o devolve (é opcional no padrão).</summary>
    public string? NumeroGuiaOperadora { get; set; }

    /// <summary>Uma frase curta para a tela ("Elegível.", "Recebida, protocolo 123").</summary>
    public string? Resultado { get; set; }

    /// <summary>O motivo da falha, para quem vai investigar. Nunca leva credencial.</summary>
    public string? Erro { get; set; }

    /// <summary>A mensagem enviada, com a senha mascarada.</summary>
    public string? XmlEnviado { get; set; }

    /// <summary>A resposta da operadora, como veio.</summary>
    public string? XmlRecebido { get; set; }

    // ---------- Controle da fila ----------

    /// <summary>Qual conector reservou (nome da máquina e do processo).</summary>
    public string? Consumidor { get; set; }

    public DateTime? ReservadaEm { get; set; }

    /// <summary>Carimbado imediatamente ANTES de a mensagem sair para a operadora.</summary>
    public DateTime? EnvioIniciadoEm { get; set; }

    /// <summary>Não reservar antes desta hora (espera entre tentativas).</summary>
    public DateTime? ProximaTentativaEm { get; set; }

    /// <summary>Sobe a cada mudança de estado: é com ela que dois conectores não reservam a mesma linha.</summary>
    public int Versao { get; set; }

    /// <summary>
    /// Envio e cancelamento MUDAM algo na operadora; elegibilidade e status só perguntam.
    /// É esta diferença que decide se uma transação pode ser repetida sozinha.
    /// </summary>
    public bool EscreveNaOperadora => Tipo is TipoTransacaoTiss.EnvioGuia or TipoTransacaoTiss.CancelamentoGuia;

    /// <summary>Concluída, falhou ou cancelada: não muda mais.</summary>
    public bool Encerrada => Estado is EstadoTransacaoTiss.Concluida or EstadoTransacaoTiss.Falhou or EstadoTransacaoTiss.Cancelada;
}

/// <summary>Os tetos de texto da transação: o mapeamento do banco e o serviço cortam pelo MESMO número.</summary>
public static class TransacaoTissLimites
{
    public const int Resultado = 300;
    public const int Erro = 1000;
}
