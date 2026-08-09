using System.Security.Cryptography;
using Clinica.Domain.Entities;

namespace Clinica.Domain;

/// <summary>
/// A REGRA DE PUBLICAÇÃO — o que pode virar link, e o que nunca pode (parcela 53).
///
/// O que o link resolve
/// --------------------
/// A receita assinada é conferida no validador do ITI pelo ENVIO DO ARQUIVO. Isso obriga o
/// paciente a mandar o PDF para a farmácia (WhatsApp, e-mail), e é onde o balcão trava. Com
/// o documento publicado, o farmacêutico escaneia o QR da tela do celular, baixa o arquivo e
/// confere no ITI — sem ninguém enviar nada.
///
/// <b>Nós não afirmamos que o documento é válido</b>, e essa distinção é o coração do
/// desenho: só entregamos os bytes. Quem responde "íntegro, assinado por quem diz, com
/// registro ativo" continua sendo o ITI. Uma farmácia não precisa confiar na clínica para o
/// esquema funcionar — precisaria, se fôssemos nós a atestar a validade.
///
/// O que NUNCA se publica
/// ----------------------
/// <b>Prontuário, evolução, avaliação, medida e a folha de infusão.</b> Publicar é criar um
/// endereço na internet para dado de saúde; só se justifica no documento que o paciente
/// LEVA PARA FORA, e para o único fim de ser conferido por quem o recebe. O prontuário não
/// sai da clínica — a exportação dele existe, é autenticada e passa pela trilha de acesso.
///
/// A folha de infusão fica de fora por um motivo a mais: ela é de USO INTERNO. Não há
/// terceiro para conferi-la, e o link só abriria uma porta sem ninguém do outro lado.
/// </summary>
public static class PublicacaoDocumento
{
    /// <summary>
    /// Alfabeto do token: base32 sem os caracteres que se confundem à mão (I, L, O, U, 0, 1).
    /// O token não é digitado — vai no QR —, mas alguém vai lê-lo em log e em suporte.
    /// </summary>
    private const string Alfabeto = "ABCDEFGHJKMNPQRSTVWXYZ23456789";

    /// <summary>
    /// Tamanho do token em caracteres. 26 caracteres neste alfabeto passam de 2^127
    /// combinações — a URL é a única barreira de acesso, e ela precisa ser inadivinhável
    /// por força bruta, não "difícil de chutar".
    /// </summary>
    public const int TamanhoToken = 26;

    /// <summary>
    /// Por quantos dias o documento fica publicado, a contar da assinatura.
    ///
    /// A receita simples vale ~30 dias na prática da farmácia; 60 dá folga para o paciente
    /// que demora a comprar. Passado o prazo, o objeto sai do ar e a clínica republica com
    /// um clique — o token NÃO muda, então o QR do PDF que o paciente já tem volta a
    /// funcionar. Fosse token novo, cada renovação mataria os arquivos já assinados.
    ///
    /// Deixar publicado para sempre seria construir um acervo público de dado de saúde, que
    /// é exatamente o que a decisão de publicar tinha de evitar.
    /// </summary>
    public const int DiasPublicado = 60;

    /// <summary>
    /// Os tipos que o paciente leva para fora e que alguém de fora vai conferir.
    ///
    /// A lista é FECHADA e positiva de propósito: tipo novo no enum nasce NÃO publicável, e
    /// quem quiser publicá-lo tem de vir aqui e justificar. O contrário — publicar tudo
    /// menos o que estiver na lista de exceções — faria o próximo tipo clínico nascer
    /// exposto por esquecimento.
    /// </summary>
    public static bool PodePublicar(TipoDocumentoClinico tipo) => tipo
        is TipoDocumentoClinico.Receita
        or TipoDocumentoClinico.Atestado
        or TipoDocumentoClinico.PedidoExame
        or TipoDocumentoClinico.Comparecimento;

    /// <summary>
    /// Sorteia um token novo. <see cref="RandomNumberGenerator"/>, nunca
    /// <see cref="Random"/>: token previsível é o mesmo que documento público.
    /// </summary>
    public static string GerarToken()
    {
        var token = new char[TamanhoToken];
        for (var i = 0; i < token.Length; i++)
            token[i] = Alfabeto[RandomNumberGenerator.GetInt32(Alfabeto.Length)];
        return new string(token);
    }

    /// <summary>
    /// O caminho do objeto no armazenamento. Um nível de pasta pelo prefixo do token só
    /// para o balde não virar um diretório de milhares de arquivos irmãos.
    /// </summary>
    public static string CaminhoDoObjeto(string token) => $"r/{token[..2]}/{token}.pdf";

    /// <summary>
    /// A URL que vai no QR — e que fica SELADA dentro da assinatura, para sempre.
    ///
    /// É por isso que o domínio é da clínica e não do provedor: trocar de armazenamento um
    /// dia é repontar o DNS. Se o QR apontasse para o endereço do balde, mudar de
    /// fornecedor mataria o QR de toda receita já assinada, sem conserto possível — mexer
    /// no PDF quebra a assinatura que ele carrega.
    /// </summary>
    public static string Url(string baseUrl, string token)
        => $"{baseUrl.TrimEnd('/')}/{CaminhoDoObjeto(token)}";
}
