using Clinica.Domain.Entities;

namespace Clinica.Application.Modelos;

/// <summary>
/// O que a BOX da trava de consulta afirma nas telas de marcar (nov/2026). A frase do prazo é
/// do domínio (<c>TravaDeConsulta.Frase</c>); aqui ficam os rótulos e o que se diz do pedido —
/// num lugar só, porque são duas telas e a promessa ("o horário é marcado quando a gerência
/// liberar") não pode ter duas redações.
/// </summary>
public static class TravaDeConsultaNaTela
{
    public const string Titulo = "Trava de consulta do convênio";
    public const string RotuloDoPedido = "Pedir liberação à gerência…";
    public const string DicaDoPedido = "Envia o pedido à gerência, pelo WhatsApp e pela conversa com o sistema";

    public const string Nota =
        "Com o pedido enviado, não é preciso salvar: o horário é marcado sozinho quando a gerência liberar, "
        + "e você recebe o aviso na conversa com o sistema.";

    public const string TituloDoMotivo = "Pedir liberação à gerência";
    public const string PerguntaDoMotivo =
        "Por que esta consulta precisa acontecer antes do prazo do convênio? A gerência lê este motivo para decidir.";

    /// <summary>O pedido que já está aberto para este paciente neste dia — em vez de pedir de novo.</summary>
    public static string Pendente(LiberacaoDeTrava l)
        => $"Pedido de liberação nº {l.Id} aguardando a gerência — feito por {l.PedidoPorNome} em {l.PedidoEm:dd/MM} às {l.PedidoEm:HH:mm}, "
           + $"para {l.DataHora:dd/MM/yyyy} às {l.DataHora:HH:mm}. O horário é marcado sozinho quando liberarem; "
           + $"sem resposta até {l.ExpiraEm:dd/MM} às {l.ExpiraEm:HH:mm}, o pedido expira.";

    /// <summary>A confirmação diz A QUEM o pedido chegou — "enviado" sem destinatário não é resposta.</summary>
    public static string PedidoEnviado(int id, int aprovadores, int comWhatsApp)
    {
        var quem = aprovadores == 1 ? "1 pessoa da gerência" : $"{aprovadores} pessoas da gerência";
        var canal = comWhatsApp == 0
            ? "pela conversa com o sistema, no app (ninguém da gerência tem o WhatsApp do sistema ligado em Acessos)"
            : comWhatsApp == aprovadores
                ? "pelo WhatsApp e pela conversa com o sistema"
                : $"pela conversa com o sistema — {comWhatsApp} também pelo WhatsApp";
        return $"Pedido de liberação nº {id} enviado a {quem}, {canal}. O horário é marcado quando liberarem.";
    }

    /// <summary>O Salvar apagado diz por quê — botão morto sem explicação é o defeito de sempre.</summary>
    public const string SalvarTravado = "A trava de consulta do convênio impede salvar: escolha outra data, marque como particular ou peça a liberação.";

    /// <summary>O pedido guarda o horário inteiro: sem hora válida não há o que a gerência liberar.</summary>
    public const string FaltaHora = "Informe a hora da consulta antes de pedir a liberação — é esse horário que será marcado.";
    public const string FaltaProfissional = "Escolha quem atende antes de pedir a liberação — é esse horário que será marcado.";
    public const string FaltaEspecialidade = "Informe a especialidade da consulta antes de pedir a liberação.";
}
