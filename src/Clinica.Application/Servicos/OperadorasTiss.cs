using Clinica.Domain;
using Clinica.Domain.Entities;

namespace Clinica.Application.Servicos;

/// <summary>
/// O que vai em <c>codigoPrestadorNaOperadora</c> (cabeçalho e <c>dadosPrestador</c> das mensagens
/// do web service TISS). O XSD 4.01.00 aceita ali qualquer texto de 1 a 14 caracteres
/// (<c>st_texto14</c>); quem decide o que é o "código do prestador" é a operadora.
/// </summary>
public enum CodigoDoPrestadorTiss
{
    /// <summary>O CNPJ da clínica, só dígitos (Saúde Petrobras: decisão da direção, nov/2026).</summary>
    CnpjDaClinica,

    /// <summary>O login do web service (Amil: o login do portal é o código do credenciado).</summary>
    LoginDoWebService,

    /// <summary>Um código digitado no cadastro, na instalação (comando <c>codigo</c> do conector).</summary>
    Cadastrado
}

/// <summary>
/// A codificação em que a mensagem SOAP vai pela rede: a declaração <c>&lt;?xml … encoding=…?&gt;</c>,
/// os bytes do corpo e o <c>Content-Type</c> concordam sempre (<see cref="TissWebserviceCliente.Codificar"/>).
/// O HASH do epílogo não muda com ela: é calculado sobre o TEXTO das tags, em ISO-8859-1, como
/// manda o Componente de Comunicação do padrão (<see cref="TissElegibilidadeWs.CalcularHash"/>).
/// </summary>
public enum CodificacaoTissWs
{
    /// <summary>ISO-8859-1, a codificação do padrão TISS (Saúde Petrobras).</summary>
    Iso88591,

    /// <summary>
    /// UTF-8 (Amil, nov/2026): o APIM da Amil serve um WSDL SOAP 1.1 de WCF e respondeu HTTP 415 à
    /// mensagem em <c>text/xml; charset=ISO-8859-1</c> — o basicHttpBinding só aceita UTF-8.
    /// </summary>
    Utf8
}

/// <summary>Em que guia do padrão a operadora recebe a consulta e a sessão conferidas na baixa.</summary>
public enum ModeloDaGuiaTiss
{
    /// <summary>Guia de consulta (<c>guiaConsulta</c>): Saúde Petrobras, inclusive o BSV.</summary>
    Consulta,

    /// <summary>
    /// Guia SP/SADT (<c>guiaSP-SADT</c>), com senha de autorização e token: Amil. O Manual do
    /// Credenciado (jul/2026) manda cobrar a consulta "exclusivamente em guias de SP/SADT", e o
    /// faturamento do portal só tem SP/SADT, honorário individual e resumo de internação.
    /// </summary>
    SpSadt
}

/// <summary>
/// A janela em que a operadora aceita o envio do faturamento: do <paramref name="PrimeiroDiaUtil"/>º
/// ao <paramref name="UltimoDiaUtil"/>º dia útil do mês, das <paramref name="Abre"/> às
/// <paramref name="Fecha"/>. Dia útil aqui é segunda a sexta: o sistema não tem calendário de feriados.
/// </summary>
public sealed record JanelaDeEnvioTiss(int PrimeiroDiaUtil, int UltimoDiaUtil, TimeOnly Abre, TimeOnly Fecha);

/// <summary>Uma operadora com web service TISS que o sistema conhece sem ninguém digitar nada.</summary>
/// <param name="Familia">A família do convênio no catálogo (o convênio cadastrado aponta para ela).</param>
/// <param name="Nome">Como a operadora aparece nas frases.</param>
/// <param name="RegistroAns">O registro ANS da operadora (dado público), o destino das mensagens.</param>
/// <param name="CodigoDoPrestador">O que vai em <c>codigoPrestadorNaOperadora</c> quando o convênio não cadastrou outra regra.</param>
/// <param name="EnvioDeGuiaNasTelas">
/// As telas oferecem o envio de guias (selos, aba de envio, chave no formulário) E a baixa pede a
/// conferência da guia ANS (<see cref="GuiaAnsNaBaixa.Exige(CodigoFaturamento)"/>)? As duas coisas
/// andam juntas: sem a conferência não há guia pronta para enviar, e conferir sem poder enviar seria
/// trabalho a mais na baixa sem nada sair.
/// </param>
/// <param name="Enderecos">Os endereços de produção, um por tipo de transação. O cadastrado no convênio vence.</param>
/// <param name="Codificacao">Em que codificação a mensagem vai pela rede (ver <see cref="CodificacaoTissWs"/>).</param>
/// <param name="ModeloDaGuia">A guia em que a consulta conferida vai à operadora (ver <see cref="ModeloDaGuiaTiss"/>).</param>
/// <param name="PrazoDeCobrancaDias">Dias, a partir do atendimento, em que a operadora ainda aceita a guia (nulo = o sistema não sabe).</param>
/// <param name="JanelaDeEnvio">Quando a operadora aceita o envio do faturamento (nulo = a qualquer hora).</param>
/// <param name="Versoes">
/// A versão do padrão TISS em que cada transação é escrita (nov/2026). O que não está aqui sai em
/// <see cref="OperadorasTiss.VersaoPadrao"/> (4.01.00). A Amil só publica o lote em 4.03.00.
/// </param>
/// <param name="EnderecoDemonstrativo">
/// O demonstrativo de retorno: só o endereço, porque o sistema ainda não tem a transação dele.
/// </param>
/// <param name="EnderecoHomologacao">
/// O ponto ÚNICO de homologação da operadora (nulo = ela não tem): com o convênio em homologação
/// (<c>comando.sh ambiente</c>), TODOS os serviços vão para ele.
/// </param>
/// <param name="EntregaConfirmadaNoPortal">
/// A operadora só considera o faturamento ENTREGUE depois da confirmação no portal dela: "enviado"
/// pelo web service não é "entregue", e a tela diz isso depois do envio.
/// </param>
public sealed record OperadoraTiss(
    Convenio Familia, string Nome, string RegistroAns, CodigoDoPrestadorTiss CodigoDoPrestador,
    bool EnvioDeGuiaNasTelas, IReadOnlyDictionary<TipoTransacaoTiss, Uri> Enderecos,
    CodificacaoTissWs Codificacao = CodificacaoTissWs.Iso88591,
    ModeloDaGuiaTiss ModeloDaGuia = ModeloDaGuiaTiss.Consulta,
    int? PrazoDeCobrancaDias = null,
    JanelaDeEnvioTiss? JanelaDeEnvio = null,
    IReadOnlyDictionary<TipoTransacaoTiss, string>? Versoes = null,
    Uri? EnderecoDemonstrativo = null,
    Uri? EnderecoHomologacao = null,
    bool EntregaConfirmadaNoPortal = false)
{
    public Uri? Endereco(TipoTransacaoTiss tipo) => Enderecos.TryGetValue(tipo, out var uri) ? uri : null;

    /// <summary>A versão do padrão em que esta transação é escrita para esta operadora.</summary>
    public string VersaoTiss(TipoTransacaoTiss tipo)
        => Versoes is not null && Versoes.TryGetValue(tipo, out var v) ? v : OperadorasTiss.VersaoPadrao;
}

/// <summary>
/// O catálogo das operadoras com web service TISS (nov/2026). É DADO: a operadora nova entra aqui
/// como uma linha, e nenhum outro arquivo decide "esta família tem web service". Quem pergunta:
/// o endereço padrão (<see cref="CredenciaisTissService.EnderecoAsync"/>), as telas
/// (<see cref="EnvioTissNaTela"/>) e o conector (registro ANS e código do prestador).
///
/// O que o convênio cadastra na instalação sempre vence o que está aqui: endereço
/// (<c>endereco</c>), registro ANS (cadastro do convênio) e código do prestador (<c>codigo</c>).
/// </summary>
public static class OperadorasTiss
{
    /// <summary>
    /// Saúde Petrobras (conectividade CRC), do "Manual de Utilização Webservice TISS 4.01.00" da
    /// operadora. O manual não traz endereço de homologação: toda chamada é em produção.
    /// </summary>
    public static readonly OperadoraTiss SaudePetrobras = new(
        Convenio.Petrobras, "Saúde Petrobras", "422631", CodigoDoPrestadorTiss.CnpjDaClinica, EnvioDeGuiaNasTelas: true,
        Enderecos(
            "https://webservices.crc.com.br/tiss/ws/40100/",
            (TipoTransacaoTiss.Elegibilidade, "tissVerificaElegibilidade"),
            (TipoTransacaoTiss.EnvioGuia, "tissLoteGuias"),
            (TipoTransacaoTiss.StatusProtocolo, "tissSolicitacaoStatusProtocolo"),
            (TipoTransacaoTiss.CancelamentoGuia, "tissCancelaGuia")));

    /// <summary>A versão do padrão que o sistema escreve quando a operadora não pede outra.</summary>
    public const string VersaoPadrao = "4.01.00";

    /// <summary>A 4.03.00 (ANS, vigente desde 01/12/2025): a que a Amil exige no lote de guias.</summary>
    public const string Versao403 = "4.03.00";

    /// <summary>As versões que o sistema sabe escrever (os XSD que estão nos testes).</summary>
    public static readonly IReadOnlyList<string> VersoesConhecidas = [VersaoPadrao, Versao403];

    /// <summary>
    /// Amil, da tabela "Endereços web service Amil 4.01.00" do Portal do Credenciado (APIM, o
    /// endereço novo; nov/2026). O manual fala de um "link único" pedido ao Núcleo TISS: quando a
    /// clínica o tiver, ele é cadastrado por cima destes (comando <c>endereco … todos</c>).
    ///
    /// O ENVIO DE GUIAS está pronto e DESLIGADO (nov/2026): a guia vai em SP/SADT, com senha e token
    /// (<see cref="GuiaAnsNaBaixa"/>), pelo mesmo lote da Petrobras. <see cref="OperadoraTiss.EnvioDeGuiaNasTelas"/>
    /// falso mantém a Amil fora das telas de envio E da conferência na baixa — ligar é trocar para
    /// verdadeiro quando o Núcleo TISS responder (docs/tiss-conector.md, "Amil: o que falta saber"),
    /// e depois ligar o envio do convênio no servidor (<c>comando.sh envio Amil ligar</c>).
    /// Prazos do Manual de Faturamento Eletrônico e do portal: cobrança em até 90 dias do
    /// atendimento; envio do 1º ao 5º dia útil, das 7h às 19h.
    ///
    /// VERSÃO (nov/2026, resposta do Núcleo TISS): a Amil pede TISS 4.03.00. O lote só existe em
    /// <c>api-tiss-lote-guias/v4.03.00</c> (em /v4.01.00 o WSDL dá 404), e o status do protocolo e o
    /// demonstrativo vão junto. A elegibilidade continua em 4.01.00: funcionou em produção assim, e o
    /// cancelamento também fica onde estava (o WSDL 4.01.00 dele abre).
    /// </summary>
    public static readonly OperadoraTiss Amil = new(
        Convenio.Amil, "Amil", "326305", CodigoDoPrestadorTiss.LoginDoWebService, EnvioDeGuiaNasTelas: false,
        Enderecos(
            "https://api.servicos.grupoamil.com.br/",
            (TipoTransacaoTiss.Elegibilidade, "api-tiss-verifica-elegibilidade/v4.01.00"),
            (TipoTransacaoTiss.EnvioGuia, "api-tiss-lote-guias/v4.03.00"),
            (TipoTransacaoTiss.StatusProtocolo, "api-tiss-solicitacao-status-protocolo/v4.03.00"),
            (TipoTransacaoTiss.CancelamentoGuia, "api-tiss-cancela-guia/v4.01.00")),
        // O APIM respondeu HTTP 415 à primeira elegibilidade real em ISO-8859-1 (nov/2026).
        Codificacao: CodificacaoTissWs.Utf8,
        ModeloDaGuia: ModeloDaGuiaTiss.SpSadt,
        PrazoDeCobrancaDias: 90,
        JanelaDeEnvio: new JanelaDeEnvioTiss(1, 5, new TimeOnly(7, 0), new TimeOnly(19, 0)),
        Versoes: new Dictionary<TipoTransacaoTiss, string>
        {
            [TipoTransacaoTiss.EnvioGuia] = Versao403,
            [TipoTransacaoTiss.StatusProtocolo] = Versao403
        },
        EnderecoDemonstrativo: new Uri("https://api.servicos.grupoamil.com.br/api-tiss-solicitacao-demonstrativo-retorno/v4.03.00"),
        // Resposta do Núcleo TISS (nov/2026): um endereço só para todos os serviços (o ?wsdl dá 404,
        // então o serviço é decidido pelo corpo da mensagem). Aceita carteira válida de produção.
        EnderecoHomologacao: new Uri("https://api-dev.servicos.grupoamil.com.br/api-tiss-servico-test"),
        // "A confirmação de entrega do faturamento e do recurso continua no portal."
        EntregaConfirmadaNoPortal: true);

    /// <summary>Todas as operadoras com web service conhecidas.</summary>
    public static IReadOnlyList<OperadoraTiss> Todas { get; } = [SaudePetrobras, Amil];

    /// <summary>A operadora da família, ou nulo quando o sistema não conhece web service dela.</summary>
    public static OperadoraTiss? Da(Convenio familia) => Todas.FirstOrDefault(o => o.Familia == familia);

    /// <summary>O endereço de produção que o sistema conhece para a família e o tipo (nulo = nenhum).</summary>
    public static Uri? EnderecoPadrao(Convenio familia, TipoTransacaoTiss tipo) => Da(familia)?.Endereco(tipo);

    /// <summary>A operadora do convênio (código do cadastro ou família), ou nulo fora do catálogo.</summary>
    public static OperadoraTiss? DoConvenio(string? convenioCodigo, Convenio familia)
        => Da(Clinica.Domain.Regras.CatalogoConvenios.Familia(convenioCodigo ?? familia.ToString()));

    /// <summary>A codificação da mensagem para a operadora (fora do catálogo: a do padrão, ISO-8859-1).</summary>
    public static CodificacaoTissWs CodificacaoDe(OperadoraTiss? operadora) => operadora?.Codificacao ?? CodificacaoTissWs.Iso88591;

    /// <summary>A família tem algum web service (a credencial faz sentido)?</summary>
    public static bool TemWebService(Convenio familia) => Da(familia) is not null;

    /// <summary>A família tem a verificação de elegibilidade?</summary>
    public static bool TemElegibilidade(Convenio familia) => EnderecoPadrao(familia, TipoTransacaoTiss.Elegibilidade) is not null;

    /// <summary>As telas oferecem o envio de guias desta família (ver <see cref="OperadoraTiss.EnvioDeGuiaNasTelas"/>)?</summary>
    public static bool TemEnvioDeGuia(Convenio familia)
        => Da(familia) is { EnvioDeGuiaNasTelas: true } o && o.Endereco(TipoTransacaoTiss.EnvioGuia) is not null;

    private static IReadOnlyDictionary<TipoTransacaoTiss, Uri> Enderecos(string baseHttps, params (TipoTransacaoTiss Tipo, string Caminho)[] servicos)
        => servicos.ToDictionary(s => s.Tipo, s => new Uri(baseHttps + s.Caminho));
}
