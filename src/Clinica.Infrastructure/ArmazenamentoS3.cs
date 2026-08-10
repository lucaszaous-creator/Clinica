using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using Clinica.Application.Abstracoes;
using Clinica.Application.Servicos;

namespace Clinica.Infrastructure;

/// <summary>
/// O armazenamento público sobre qualquer provedor S3-COMPATÍVEL (parcela 53).
///
/// Uma implementação, todos os provedores
/// --------------------------------------
/// Magalu, Cloudflare R2, AWS, MinIO — todos falam o mesmo protocolo, e o que muda é o
/// <c>ServiceURL</c>. Por isso não existe <c>ArmazenamentoMagalu</c> nem
/// <c>ArmazenamentoR2</c>: escolher fornecedor passou a ser preencher um campo em
/// Configurações, não publicar versão nova. Foi essa decisão que tirou a escolha do
/// provedor do caminho crítico da entrega.
///
/// Por que o SDK e não SigV4 à mão
/// -------------------------------
/// São só dois verbos, e assinar requisição S4 na unha é código conhecido. O que decide é o
/// <b>path-style addressing</b>: o SDK da AWS fala por padrão o estilo
/// <c>bucket.host/objeto</c>, e quase todo S3-compatível exige <c>host/bucket/objeto</c>.
/// Acertar isso, mais as diferenças de região e de assinatura entre provedores, é
/// exatamente o que <see cref="AmazonS3Config.ForcePathStyle"/> resolve numa linha. O custo
/// é ~1,5 MB de DLL num instalador que já passa de 100 MB.
///
/// Sem credencial, RECUSA — e diz o que falta
/// ------------------------------------------
/// Este é o caminho do meio, e é o que mais confunde: alguém cadastrou o domínio e não
/// configurou o provedor. Estourar <c>NullReferenceException</c> na cara de quem está
/// assinando uma receita seria pior; falhar em silêncio seria pior ainda, porque a clínica
/// acreditaria estar publicando e o farmacêutico encontraria um endereço morto.
///
/// Quem chama já trata a exceção: <c>PublicacaoDocumentoService.PublicarAsync</c> a
/// converte em aviso na tela, e o documento continua assinado e válido.
/// </summary>
public sealed class ArmazenamentoS3 : IArmazenamentoPublico
{
    private readonly ProvedorOpcoesArmazenamento _opcoes;

    public ArmazenamentoS3(ProvedorOpcoesArmazenamento opcoes) => _opcoes = opcoes;

    private const string SemConfiguracao =
        "O domínio de publicação está cadastrado, mas o armazenamento não foi configurado "
        + "nesta instalação. O documento continua assinado e válido — só o link não sobe. "
        + "Preencha endpoint, balde e credenciais em Configurações → Publicação, ou limpe o "
        + "domínio para desligar a publicação.";

    public async Task PublicarAsync(
        string caminho, byte[] conteudo, string tipoConteudo, CancellationToken ct = default)
    {
        var (cliente, opcoes) = await ClienteAsync(ct);
        using var _ = cliente;

        using var fluxo = new MemoryStream(conteudo, writable: false);

        await cliente.PutObjectAsync(
            new PutObjectRequest
            {
                BucketName = opcoes.Bucket,
                Key = caminho,
                InputStream = fluxo,
                ContentType = tipoConteudo,

                // O objeto precisa abrir para um farmacêutico ANÔNIMO — é o desenho inteiro
                // da funcionalidade, e por isso a barreira é o token de 128 bits e não uma
                // credencial. Provedor que não aceita ACL (o R2 é um) recusa aqui, e é
                // justamente por isso que o "Testar conexão" faz um PUT de verdade: a
                // incompatibilidade aparece em Configurações, e não na primeira receita.
                CannedACL = S3CannedACL.PublicRead,

                // Sem isto o navegador do celular BAIXA o arquivo em vez de mostrá-lo, e o
                // farmacêutico tem de procurar a receita na pasta de downloads com o cliente
                // no balcão. O nome é genérico de propósito: nome de paciente no arquivo
                // vaza dado de saúde para quem só olhou a tela por cima.
                Headers = { ContentDisposition = "inline; filename=\"documento.pdf\"" },
            },
            ct);
    }

    /// <summary>
    /// Tira do ar. <b>Não estoura quando o objeto já não existe</b> — o DELETE do S3 é
    /// idempotente por especificação, e a varredura de expiração roda em toda abertura do
    /// Gerente: falhar por um objeto já removido derrubaria a abertura do app.
    /// </summary>
    public async Task RemoverAsync(string caminho, CancellationToken ct = default)
    {
        var (cliente, opcoes) = await ClienteAsync(ct);
        using var _ = cliente;

        await cliente.DeleteObjectAsync(
            new DeleteObjectRequest { BucketName = opcoes.Bucket, Key = caminho }, ct);
    }

    private async Task<(IAmazonS3 Cliente, OpcoesArmazenamento Opcoes)> ClienteAsync(
        CancellationToken ct)
    {
        var opcoes = await _opcoes.ObterAsync(ct)
            ?? throw new InvalidOperationException(SemConfiguracao);

        var config = new AmazonS3Config
        {
            ServiceURL = opcoes.Endpoint,

            // A razão de existir do SDK aqui. Ver o resumo da classe.
            ForcePathStyle = true,

            AuthenticationRegion = opcoes.Regiao,

            // Curto de propósito: quem publica tem um paciente na frente. Falhar em 30 s
            // dizendo o motivo é melhor que travar a tela de assinatura por dois minutos —
            // a mesma escolha do HttpClient do SafeID.
            Timeout = TimeSpan.FromSeconds(30),
            MaxErrorRetry = 2,
        };

        return (
            new AmazonS3Client(new BasicAWSCredentials(opcoes.Chave, opcoes.Segredo), config),
            opcoes);
    }

}
