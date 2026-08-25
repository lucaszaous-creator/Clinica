namespace Clinica.Application.Abstracoes;

/// <summary>
/// Onde o arquivo assinado fica acessível por URL (parcela 53).
///
/// Por que é uma abstração
/// -----------------------
/// A escolha do provedor é comercial e vai mudar — Magalu, AWS `sa-east-1`, ou qualquer
/// outro S3-compatível. Toda a lógica de publicação (token, prazo, renovação, o que pode e
/// o que não pode virar link) mora na camada de aplicação e no domínio; o provedor conhece
/// só dois verbos. Trocar de fornecedor é escrever outra implementação e nada mais.
///
/// <b>Isto NÃO é um serviço de validação.</b> Ele entrega bytes. Quem responde se o
/// documento é válido continua sendo o validador do ITI — é o que dispensa a farmácia de
/// confiar na clínica.
///
/// Sobre segurança
/// ---------------
/// O caminho é a única barreira: quem tem a URL tem o arquivo. Por isso o token é de 128
/// bits (<see cref="Clinica.Domain.PublicacaoDocumento.TamanhoToken"/>) e a implementação
/// tem de garantir, no provedor, que o balde <b>não lista conteúdo</b>. Balde com listagem
/// aberta transforma "inadivinhável" em "está tudo aqui".
/// </summary>
public interface IArmazenamentoPublico
{
    /// <summary>
    /// Publica (ou substitui) o objeto no caminho indicado. O caminho vem do domínio —
    /// ver <see cref="Clinica.Domain.PublicacaoDocumento.CaminhoDoObjeto"/>.
    /// </summary>
    /// <param name="metadados">
    /// Pares gravados JUNTO do objeto no provedor (metadados S3), invisíveis a quem baixa
    /// o arquivo. Hoje carregam o código de conferência do documento, que é o que permite
    /// a uma camada de borda (Worker no domínio da clínica) responder o contrato de QR
    /// Code do validador do ITI — `_secretCode` conferido contra o código impresso na
    /// folha — sem banco nenhum na borda. Chaves e valores em ASCII: metadado S3 não
    /// aceita acento.
    /// </param>
    Task PublicarAsync(
        string caminho, byte[] conteudo, string tipoConteudo,
        IReadOnlyDictionary<string, string>? metadados = null, CancellationToken ct = default);

    /// <summary>
    /// Tira o objeto do ar. Usado quando o prazo vence e quando o documento é CANCELADO —
    /// receita cancelada que continua baixável é a pior espécie de documento no ar.
    ///
    /// Apagar aqui não fere a regra do "não se apaga": o registro e os bytes assinados
    /// continuam no banco pelos 20 anos. O que sai é a PUBLICAÇÃO.
    /// </summary>
    Task RemoverAsync(string caminho, CancellationToken ct = default);

    /// <summary>
    /// Lê um objeto de volta, ou <c>null</c> quando não existe (parcela 81 — a coleta do
    /// termo pelo celular: o desktop publica o pedido e fica LENDO o balde à espera da
    /// resposta que o Worker grava). A leitura é pela API autenticada, nunca pela URL
    /// pública — o desktop tem credencial, e é ela que prova que quem lê é a clínica.
    /// </summary>
    Task<byte[]?> LerAsync(string caminho, CancellationToken ct = default);
}
