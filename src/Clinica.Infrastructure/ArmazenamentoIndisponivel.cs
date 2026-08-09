using Clinica.Application.Abstracoes;

namespace Clinica.Infrastructure;

/// <summary>
/// O armazenamento público quando a clínica ainda NÃO contratou um (parcela 53).
///
/// Por que existe em vez de simplesmente não registrar nada
/// -------------------------------------------------------
/// O grafo do DI tem de resolver: <c>PublicacaoDocumentoService</c> é registrado sempre, e
/// um teste do projeto (<c>MontagemDoSistemaTests</c>) constrói TODOS os serviços
/// registrados justamente para o app não morrer na abertura por uma dependência que
/// ninguém percebeu que faltava.
///
/// Na prática ele quase nunca é chamado: sem domínio cadastrado em Configurações, a
/// publicação está desligada e o serviço nem chega aqui — o QR volta a apontar para o
/// validador do ITI e o sistema funciona como sempre funcionou.
///
/// O caso em que ele fala é o do meio: alguém cadastrou o domínio e NÃO configurou o
/// provedor. Aí a mensagem precisa dizer o que falta, em vez de estourar um
/// <c>NullReferenceException</c> na cara de quem está assinando uma receita com o paciente
/// esperando. Falha silenciosa aqui seria pior ainda: a clínica acreditaria estar
/// publicando, e o farmacêutico encontraria um endereço morto.
/// </summary>
public sealed class ArmazenamentoIndisponivel : IArmazenamentoPublico
{
    private const string Explicacao =
        "O domínio de publicação está cadastrado, mas nenhum provedor de armazenamento foi "
        + "configurado nesta instalação. O documento continua assinado e válido — só o link "
        + "não sobe. Configure o armazenamento ou limpe o domínio em Configurações.";

    public Task PublicarAsync(
        string caminho, byte[] conteudo, string tipoConteudo, CancellationToken ct = default)
        => throw new InvalidOperationException(Explicacao);

    /// <summary>
    /// Remover não estoura: não há nada publicado para tirar do ar, e derrubar a expiração
    /// por causa disso faria a varredura do Gerente falhar toda abertura.
    /// </summary>
    public Task RemoverAsync(string caminho, CancellationToken ct = default)
        => Task.CompletedTask;
}
