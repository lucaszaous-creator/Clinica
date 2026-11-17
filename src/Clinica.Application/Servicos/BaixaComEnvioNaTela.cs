using Clinica.Application.Modelos;
using Clinica.Domain.Entities;

namespace Clinica.Application.Servicos;

/// <summary>Em que ponto está a baixa de uma guia que vai à operadora pelo web service.</summary>
public enum FaseDaBaixaComEnvio
{
    /// <summary>Na fila: o serviço de envio ainda não buscou o pedido.</summary>
    NaFila,
    /// <summary>O pedido está na fila há mais tempo que o razoável: o serviço de envio não respondeu.</summary>
    ServicoSemResposta,
    /// <summary>O serviço de envio está falando com a operadora.</summary>
    Enviando,
    /// <summary>A operadora recebeu a guia e a baixa ainda não foi registrada (a tela a registra em seguida).</summary>
    RecebidaSemBaixa,
    /// <summary>A operadora recebeu, e a baixa não pôde ser registrada sozinha: alguém precisa agir.</summary>
    RecebidaComBaixaRecusada,
    /// <summary>Recebida e baixada: o fim feliz.</summary>
    Baixada,
    /// <summary>A operadora respondeu com glosa nesta guia: corrigir e enviar de novo.</summary>
    Glosada,
    /// <summary>Protocolada, e a operadora ainda não disse nada sobre ESTA guia.</summary>
    SemRespostaSobreAGuia,
    /// <summary>A mensagem saiu e não há resposta em que confiar.</summary>
    NaoSeSabeSeChegou,
    /// <summary>A operadora respondeu recusando o envio inteiro, com o motivo: nada ficou lá, e a guia pode ser corrigida e reenviada.</summary>
    EnvioRecusado,
    /// <summary>Nada chegou à operadora: o pedido foi cancelado, recusado antes de sair ou falhou antes de ir.</summary>
    NaoEnviada
}

/// <summary>Um passo do andamento: o rótulo e se já foi, é o de agora ou ainda vem.</summary>
/// <param name="Estado">"feito", "atual" ou "pendente".</param>
public sealed record PassoDoEnvio(string Rotulo, string Estado);

/// <summary>
/// Tudo o que o formulário de baixa escreve e oferece enquanto acompanha um envio.
/// </summary>
/// <param name="Tom">O tom do quadro: "info", "sucesso", "atencao" ou "erro".</param>
/// <param name="NumeroDaGuia">O número a mostrar em destaque e copiável (a guia baixada).</param>
/// <param name="Acompanha">A tela continua relendo a transação.</param>
/// <param name="CamposEditaveis">A guia volta a poder ser corrigida e enviada de novo.</param>
/// <param name="PodeCancelarPedido">O pedido ainda não saiu: quem pediu pode desistir.</param>
/// <param name="PodeRegistrarBaixa">A operadora recebeu e falta a baixa: a tela a registra (ou oferece o botão).</param>
/// <param name="OfereceBaixaManual">A saída de reserva: dar baixa com o número copiado do portal.</param>
public sealed record LeituraDaBaixaComEnvio(
    FaseDaBaixaComEnvio Fase, IReadOnlyList<PassoDoEnvio> Passos, string Titulo, string? Texto, string Tom,
    string? NumeroDaGuia, bool Acompanha, bool CamposEditaveis, bool PodeCancelarPedido, bool PodeRegistrarBaixa,
    bool OfereceBaixaManual);

/// <summary>
/// A BAIXA QUE ENVIA (nov/2026, correção de fluxo pedida pela direção): "você dá baixa, o sistema
/// comunica com a operadora, fica aparecendo um processo carregando, e quando ela retorna com a
/// guia, ela aparece na tela e a gente salva". O número da guia só existe DEPOIS do envio — pedi-lo
/// antes, como obrigatório, era pedir o que a pessoa não tinha como ter.
///
/// Aqui mora o que o formulário AFIRMA em cada momento: a fase, os passos do andamento, a frase de
/// cada desfecho e o que se oferece. Puro e com teste; o Razor só desenha. O que acontece com a
/// guia continua sendo decidido pela fila (<see cref="FilaTissService"/>) e pela baixa de sempre
/// (<see cref="BaixaDasGuiasEnviadasService"/>).
/// </summary>
public static class BaixaComEnvioNaTela
{
    /// <summary>De quanto em quanto tempo o formulário relê o pedido enquanto ele anda.</summary>
    public static readonly TimeSpan Ritmo = TimeSpan.FromSeconds(2);

    /// <summary>
    /// Quanto se espera pelo serviço de envio antes de dizer que ele não respondeu. Ele olha a fila
    /// a cada poucos segundos: 45 s parado é serviço fora do ar (ou não instalado), não fila cheia.
    /// </summary>
    public static readonly TimeSpan EsperaPeloServico = TimeSpan.FromSeconds(45);

    /// <summary>O rótulo da ação principal, com o nome do convênio como está no catálogo.</summary>
    public static string AcaoPrincipal(string convenioNome, bool reenvio)
        => reenvio ? $"Corrigir e enviar de novo para {convenioNome}" : $"Enviar para {convenioNome} e dar baixa";

    /// <summary>O subtítulo do formulário com o envio ligado: diz o que vai acontecer ao clicar.</summary>
    public static string Subtitulo(string convenioNome)
        => $"Confira a guia. Ao enviar, {convenioNome} gera o número da guia e a baixa é registrada sozinha.";

    /// <summary>A linha do bloco "Baixa no sistema" no lugar do campo do número.</summary>
    public static string LinhaDoNumero(string convenioNome)
        => $"O número da guia não é digitado: {convenioNome} o devolve depois do envio, e ele é gravado na baixa.";

    /// <summary>A pergunta antes do envio: uma guia, de quem, para quem, e que é cobrança de verdade.</summary>
    public static string PerguntaDoEnvio(string? pacienteNome, string convenioNome)
        => $"Enviar a guia de {pacienteNome ?? "este paciente"} para {convenioNome} e dar baixa?\n\n"
           + "É a COBRANÇA de verdade: a operadora recebe esta guia como apresentada pela clínica. "
           + "Depois de enviada, não há como desfazer por aqui — só cancelando a guia na operadora.";

    /// <summary>
    /// A leitura do envio DESTA guia. <paramref name="esperandoDesde"/> é de quando se conta a espera
    /// pelo serviço (o pedido, ou o último "continuar esperando"); <paramref name="numeroDaBaixa"/> é
    /// o número com que a guia já tem baixa no sistema (nulo = sem baixa); <paramref name="falhaDaBaixa"/>
    /// é o que impediu a baixa automática, quando ela foi tentada e não entrou.
    /// </summary>
    public static LeituraDaBaixaComEnvio Ler(
        TransacaoTiss envio, int codigoId, string convenioNome, DateTime esperandoDesde, DateTime agora,
        string? numeroDaBaixa = null, string? falhaDaBaixa = null)
    {
        var item = envio.Guias.FirstOrDefault(g => g.CodigoId == codigoId);
        var protocolo = string.IsNullOrWhiteSpace(envio.Protocolo) ? "não informado" : envio.Protocolo;
        var motivo = string.IsNullOrWhiteSpace(item?.Motivo) ? null : item!.Motivo!.Trim();

        switch (envio.Estado)
        {
            case EstadoTransacaoTiss.Pendente when agora - esperandoDesde >= EsperaPeloServico:
                return new(FaseDaBaixaComEnvio.ServicoSemResposta, Passos(1, convenioNome, parou: true),
                    "O serviço de envio não respondeu.",
                    "O pedido continua na fila e nada foi à operadora. Você pode continuar esperando, cancelar o pedido ou dar baixa com o número do portal.",
                    "atencao", null, Acompanha: false, CamposEditaveis: false, PodeCancelarPedido: true, PodeRegistrarBaixa: false, OfereceBaixaManual: true);
            case EstadoTransacaoTiss.Pendente:
                return new(FaseDaBaixaComEnvio.NaFila, Passos(1, convenioNome),
                    "Na fila de envio", envio.Tentativas > 0 ? envio.Resultado : "Aguardando o serviço de envio. Pode fechar esta janela: o pedido continua.",
                    "info", null, Acompanha: true, CamposEditaveis: false, PodeCancelarPedido: true, PodeRegistrarBaixa: false, OfereceBaixaManual: false);
            case EstadoTransacaoTiss.EmEnvio:
                return new(FaseDaBaixaComEnvio.Enviando, Passos(2, convenioNome),
                    $"Enviando para {convenioNome}…", "Pode fechar esta janela: o envio continua, e a resposta aparece no painel.",
                    "info", null, Acompanha: true, CamposEditaveis: false, PodeCancelarPedido: false, PodeRegistrarBaixa: false, OfereceBaixaManual: false);
        }

        if (FilaTiss.NaoSeSabeSeChegou(envio))
            return new(FaseDaBaixaComEnvio.NaoSeSabeSeChegou, Passos(2, convenioNome, parou: true),
                $"Não se sabe se {convenioNome} recebeu esta guia.",
                "A mensagem saiu e não houve resposta em que confiar. Confira no portal da operadora ANTES de qualquer novo envio: "
                + "se a guia estiver lá, dê baixa com o número do portal. Se não estiver, o novo envio é pedido em Guias TISS → "
                + "Envio pelo web service, declarando a conferência.",
                "erro", null, Acompanha: false, CamposEditaveis: false, PodeCancelarPedido: false, PodeRegistrarBaixa: false, OfereceBaixaManual: true);

        // A operadora RESPONDEU que não (o lote inteiro recusado, com o motivo): não há dúvida a
        // conferir no portal — nada ficou lá. A guia volta para ser corrigida e enviada de novo.
        if (envio is { Estado: EstadoTransacaoTiss.Falhou, EnvioIniciadoEm: not null })
            return new(FaseDaBaixaComEnvio.EnvioRecusado, Passos(3, convenioNome, parou: true),
                $"{convenioNome} recusou o envio.",
                $"{(envio.Erro ?? envio.Resultado ?? "A operadora não informou o motivo.").TrimEnd()} Nada foi baixado. Corrija a guia e envie de novo.",
                "erro", null, Acompanha: false, CamposEditaveis: true, PodeCancelarPedido: false, PodeRegistrarBaixa: false, OfereceBaixaManual: true);

        if (envio.Estado != EstadoTransacaoTiss.Concluida || item is null)
            return new(FaseDaBaixaComEnvio.NaoEnviada, Passos(1, convenioNome, parou: true),
                "A guia não foi enviada.",
                (envio.Estado == EstadoTransacaoTiss.Falhou ? envio.Erro : envio.Resultado) ?? "O pedido não chegou a sair.",
                "atencao", null, Acompanha: false, CamposEditaveis: true, PodeCancelarPedido: false, PodeRegistrarBaixa: false, OfereceBaixaManual: true);

        switch (item.Situacao)
        {
            case SituacaoGuiaTiss.Recebida when numeroDaBaixa is not null:
                var daOperadora = !string.IsNullOrWhiteSpace(item.NumeroGuiaOperadora);
                return new(FaseDaBaixaComEnvio.Baixada, Passos(4, convenioNome),
                    daOperadora
                        ? $"Guia nº {numeroDaBaixa} gerada por {convenioNome} · protocolo {protocolo}. Baixa registrada."
                        : $"{convenioNome} recebeu a guia (protocolo {protocolo}) e não informou o número dela; a baixa ficou com o nosso número {numeroDaBaixa}.",
                    daOperadora ? null : "Se o portal mostrar outro número para esta guia, estorne a baixa e refaça com o número de lá.",
                    "sucesso", numeroDaBaixa, Acompanha: false, CamposEditaveis: false, PodeCancelarPedido: false, PodeRegistrarBaixa: false, OfereceBaixaManual: false);
            case SituacaoGuiaTiss.Recebida when falhaDaBaixa is not null:
                return new(FaseDaBaixaComEnvio.RecebidaComBaixaRecusada, Passos(3, convenioNome, parou: true),
                    $"{convenioNome} recebeu a guia (protocolo {protocolo}), mas a baixa NÃO foi registrada.",
                    $"{falhaDaBaixa} A guia NÃO precisa ser enviada de novo: dê baixa com o número conferido no portal"
                    + (string.IsNullOrWhiteSpace(item.NumeroGuiaOperadora) ? "." : $" (a operadora devolveu \"{item.NumeroGuiaOperadora}\")."),
                    "atencao", null, Acompanha: false, CamposEditaveis: false, PodeCancelarPedido: false, PodeRegistrarBaixa: false, OfereceBaixaManual: true);
            case SituacaoGuiaTiss.Recebida:
                return new(FaseDaBaixaComEnvio.RecebidaSemBaixa, Passos(3, convenioNome),
                    $"{convenioNome} recebeu a guia · protocolo {protocolo}.", "Falta registrar a baixa no sistema.",
                    "info", null, Acompanha: false, CamposEditaveis: false, PodeCancelarPedido: false, PodeRegistrarBaixa: true, OfereceBaixaManual: false);
            case SituacaoGuiaTiss.Glosada:
                return new(FaseDaBaixaComEnvio.Glosada, Passos(3, convenioNome, parou: true),
                    $"{convenioNome} recusou esta guia (glosa).",
                    $"Motivo: {motivo ?? "a operadora não informou"}. Nada foi baixado. Corrija a guia e envie de novo.",
                    "erro", null, Acompanha: false, CamposEditaveis: true, PodeCancelarPedido: false, PodeRegistrarBaixa: false, OfereceBaixaManual: true);
            case SituacaoGuiaTiss.Pendente:
                return new(FaseDaBaixaComEnvio.SemRespostaSobreAGuia, Passos(3, convenioNome, parou: true),
                    $"Guia protocolada (protocolo {protocolo}), ainda sem resposta sobre ela.",
                    "A operadora recebeu o lote e não disse nada sobre esta guia. Consulte a situação em Guias TISS → Envio pelo web service; não envie de novo.",
                    "atencao", null, Acompanha: false, CamposEditaveis: false, PodeCancelarPedido: false, PodeRegistrarBaixa: false, OfereceBaixaManual: true);
            default:
                return new(FaseDaBaixaComEnvio.NaoEnviada, Passos(1, convenioNome, parou: true),
                    "A guia não foi enviada.", motivo ?? envio.Resultado ?? "O envio não se completou.",
                    "atencao", null, Acompanha: false, CamposEditaveis: true, PodeCancelarPedido: false, PodeRegistrarBaixa: false, OfereceBaixaManual: true);
        }
    }

    /// <summary>
    /// O formulário de uma guia que JÁ tem envio deve abrir no acompanhamento, e não oferecer
    /// enviar de novo? Sim enquanto o pedido anda, quando a operadora recebeu, quando ficou em
    /// dúvida ou sem resposta sobre a guia, e quando glosou (aí com os campos editáveis). Não
    /// quando nada chegou a sair: o formulário abre limpo, pronto para um envio novo.
    /// </summary>
    public static bool AbreNoAcompanhamento(SeloDeEnvioTiss? selo)
        => selo?.Estado is EstadoDoEnvioDaGuia.NaFila or EstadoDoEnvioDaGuia.Enviada or EstadoDoEnvioDaGuia.Recebida
            or EstadoDoEnvioDaGuia.Glosada or EstadoDoEnvioDaGuia.Falhou;

    /// <summary>
    /// Os quatro passos, em palavras de clínica. <paramref name="atual"/>: 1 = na fila, 2 = enviando,
    /// 3 = resposta recebida, 4 = tudo feito. <paramref name="parou"/> = o andamento parou no passo
    /// atual (ele não está "em curso"; é onde a coisa ficou).
    /// </summary>
    public static IReadOnlyList<PassoDoEnvio> Passos(int atual, string convenioNome, bool parou = false)
    {
        string[] rotulos = ["Guia conferida", "Na fila de envio", $"Enviando para {convenioNome}", "Resposta recebida"];
        return rotulos.Select((rotulo, i) => new PassoDoEnvio(rotulo,
            i < atual ? "feito" : i == atual ? (parou ? "parou" : "atual") : "pendente")).ToList();
    }
}
