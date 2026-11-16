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

/// <summary>Uma operadora com web service TISS que o sistema conhece sem ninguém digitar nada.</summary>
/// <param name="Familia">A família do convênio no catálogo (o convênio cadastrado aponta para ela).</param>
/// <param name="Nome">Como a operadora aparece nas frases.</param>
/// <param name="RegistroAns">O registro ANS da operadora (dado público), o destino das mensagens.</param>
/// <param name="CodigoDoPrestador">O que vai em <c>codigoPrestadorNaOperadora</c> quando o convênio não cadastrou outra regra.</param>
/// <param name="EnvioDeGuiaNasTelas">
/// As telas oferecem o envio de guias (selos, aba de envio, chave no formulário)? Só quando a guia
/// dessa operadora passa pela conferência ANS na baixa (<see cref="GuiaAnsNaBaixa.Exige(CodigoFaturamento)"/>):
/// sem ela não há guia pronta para enviar, e mostrar o envio seria prometer o que não acontece.
/// </param>
/// <param name="Enderecos">Os endereços de produção, um por tipo de transação. O cadastrado no convênio vence.</param>
public sealed record OperadoraTiss(
    Convenio Familia, string Nome, string RegistroAns, CodigoDoPrestadorTiss CodigoDoPrestador,
    bool EnvioDeGuiaNasTelas, IReadOnlyDictionary<TipoTransacaoTiss, Uri> Enderecos)
{
    public Uri? Endereco(TipoTransacaoTiss tipo) => Enderecos.TryGetValue(tipo, out var uri) ? uri : null;
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

    /// <summary>
    /// Amil, da tabela "Endereços web service Amil 4.01.00" do Portal do Credenciado (APIM, o
    /// endereço novo; nov/2026). O manual fala de um "link único" pedido ao Núcleo TISS: quando a
    /// clínica o tiver, ele é cadastrado por cima destes (comando <c>endereco … todos</c>).
    /// O envio de guias tem endereço, mas não aparece nas telas: a guia da Amil ainda não passa
    /// pela conferência ANS na baixa.
    /// </summary>
    public static readonly OperadoraTiss Amil = new(
        Convenio.Amil, "Amil", "326305", CodigoDoPrestadorTiss.LoginDoWebService, EnvioDeGuiaNasTelas: false,
        Enderecos(
            "https://api.servicos.grupoamil.com.br/",
            (TipoTransacaoTiss.Elegibilidade, "api-tiss-verifica-elegibilidade/v4.01.00"),
            (TipoTransacaoTiss.EnvioGuia, "api-tiss-lote-guias/v4.01.00"),
            (TipoTransacaoTiss.StatusProtocolo, "api-tiss-solicitacao-status-protocolo/v4.01.00"),
            (TipoTransacaoTiss.CancelamentoGuia, "api-tiss-cancela-guia/v4.01.00")));

    /// <summary>Todas as operadoras com web service conhecidas.</summary>
    public static IReadOnlyList<OperadoraTiss> Todas { get; } = [SaudePetrobras, Amil];

    /// <summary>A operadora da família, ou nulo quando o sistema não conhece web service dela.</summary>
    public static OperadoraTiss? Da(Convenio familia) => Todas.FirstOrDefault(o => o.Familia == familia);

    /// <summary>O endereço de produção que o sistema conhece para a família e o tipo (nulo = nenhum).</summary>
    public static Uri? EnderecoPadrao(Convenio familia, TipoTransacaoTiss tipo) => Da(familia)?.Endereco(tipo);

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
