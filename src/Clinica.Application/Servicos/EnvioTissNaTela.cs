using Clinica.Application.Modelos;
using Clinica.Domain;
using Clinica.Domain.Entities;
using Clinica.Domain.Regras;

namespace Clinica.Application.Servicos;

/// <summary>O que a tela escreve sobre um ENVIO (o lote inteiro): o rótulo, a cor e o que fazer.</summary>
/// <param name="Rotulo">A situação em duas ou três palavras, para o selo.</param>
/// <param name="Tom">O tom do selo e do aviso: "", "info", "sucesso", "aviso", "perigo" ou "neutro".</param>
/// <param name="Incerto">Não se sabe se a operadora recebeu. A tela põe em destaque e NÃO oferece reenvio.</param>
/// <param name="Frase">A frase inteira, para a linha de baixo do lote (nula = o rótulo basta).</param>
public sealed record SituacaoDoEnvioNaTela(string Rotulo, string Tom, bool Incerto, string? Frase);

/// <summary>O selo de envio de uma linha do painel: curto, com a cor, e a frase inteira à parte.</summary>
/// <param name="Detalhe">O protocolo, o motivo da glosa ou da falha — nulo quando o rótulo já diz tudo.</param>
public sealed record SeloDaLinhaNaTela(string Rotulo, string Tom, string? Detalhe);

/// <summary>O que a tela diz de uma guia PRONTA que já passou por um envio que não deu certo.</summary>
/// <param name="PedeConferenciaNoPortal">
/// A guia só pode ser marcada depois de a pessoa declarar que conferiu no portal que ela NÃO está lá.
/// </param>
public sealed record RessalvaDaPronta(string Texto, bool PedeConferenciaNoPortal);

/// <summary>O que a tela escreve sobre uma consulta de elegibilidade em andamento ou terminada.</summary>
/// <param name="Tom">O tom do aviso: "info", "sucesso", "erro" ou "atencao".</param>
/// <param name="Terminou">A consulta acabou: a tela para de acompanhar.</param>
/// <param name="PodeDesistir">O pedido ainda está na fila e pode ser cancelado por quem pediu.</param>
public sealed record ElegibilidadeNaTela(string Texto, string Tom, bool Terminou, bool PodeDesistir);

/// <summary>
/// As decisões das TELAS DO ENVIO à operadora (nov/2026): o que cada selo diz, com que cor, quais
/// ações cabem em cada estado e o que se pergunta antes de enviar. Puras e com teste — as telas só
/// desenham. O que o sistema AFIRMA sobre uma guia continua vindo do <see cref="SeloDeEnvioTiss"/>
/// e do <see cref="FilaTissService"/>; aqui mora só a camada de leitura da tela.
///
/// A regra que atravessa tudo: <b>"pronta para envio" não é "enviada", "enviada" não é "recebida",
/// e "não se sabe se chegou" não é "falhou"</b>. Cada uma tem a sua frase e a sua cor, e nenhuma
/// delas oferece reenvio com um clique.
/// </summary>
public static class EnvioTissNaTela
{
    /// <summary>De quanto em quanto tempo a tela relê enquanto há pedido na fila ou em andamento.</summary>
    public static readonly TimeSpan RitmoDoAcompanhamento = TimeSpan.FromSeconds(5);

    /// <summary>
    /// Quanto se espera por uma consulta de elegibilidade antes de dizer que o serviço de envio não
    /// respondeu. O conector olha a fila a cada poucos segundos: um minuto parado é serviço fora do ar
    /// (ou não instalado), não fila cheia.
    /// </summary>
    public static readonly TimeSpan EsperaDaElegibilidade = TimeSpan.FromSeconds(60);

    /// <summary>
    /// O convênio tem web service previsto? Hoje, a família Petrobras — a mesma pergunta que o
    /// serviço de credenciais responde ao dar o endereço padrão. Um lugar só decide.
    /// </summary>
    public static bool TemWebService(Convenio familia)
        => CredenciaisTissService.EnderecoPadrao(familia, TipoTransacaoTiss.EnvioGuia) is not null;

    /// <summary>A mesma pergunta pelo código do catálogo (a ficha do paciente, a linha da pendência).</summary>
    public static bool TemWebService(string? convenioCodigo, Convenio familia)
        => TemWebService(CatalogoConvenios.Familia(convenioCodigo ?? familia.ToString()));

    /// <summary>Os convênios do catálogo em que a tela de envio trabalha: ativos, que geram guia e têm web service.</summary>
    public static IReadOnlyList<(string Codigo, string Nome)> ConveniosDoEnvio(IEnumerable<ConvenioCadastro> catalogo)
        => catalogo.Where(c => c.Ativo && c.GeraGuia && TemWebService(c.Familia))
            .OrderBy(c => c.Nome, StringComparer.Create(new System.Globalization.CultureInfo("pt-BR"), ignoreCase: true))
            .Select(c => (c.Codigo, c.Nome)).ToList();

    // ==================== a guia (selo da linha) ====================

    /// <summary>O tom do selo de uma guia. Glosa e falha pedem gente (perigo); na fila e enviada são espera (info/aviso).</summary>
    public static string Tom(EstadoDoEnvioDaGuia estado) => estado switch
    {
        EstadoDoEnvioDaGuia.Recebida => "sucesso",
        EstadoDoEnvioDaGuia.Glosada or EstadoDoEnvioDaGuia.Falhou => "perigo",
        EstadoDoEnvioDaGuia.Enviada => "aviso",
        EstadoDoEnvioDaGuia.NaFila => "info",
        _ => "neutro"
    };

    /// <summary>
    /// O estado em uma ou duas palavras: é o que cabe, sem quebrar, no selo de uma coluna estreita
    /// (em 1366 a coluna tem ~140 px, e "Recebida pela operadora" virava selo de duas linhas).
    /// </summary>
    public static string Rotulo(EstadoDoEnvioDaGuia estado) => estado switch
    {
        EstadoDoEnvioDaGuia.NaFila => "Na fila",
        EstadoDoEnvioDaGuia.Enviada => "Enviada",
        EstadoDoEnvioDaGuia.Recebida => "Recebida",
        EstadoDoEnvioDaGuia.Glosada => "Glosada",
        EstadoDoEnvioDaGuia.Falhou => "Envio falhou",
        EstadoDoEnvioDaGuia.Cancelada => "Cancelada na operadora",
        _ => "Não enviada"
    };

    /// <summary>
    /// O selo de envio da LINHA do painel de pendências: o que o envio diz da guia e, quando ela
    /// ainda não foi a envio nenhum, se já está conferida ("pronta para envio") ou não. O selo é
    /// CURTO (a coluna é estreita) e a frase inteira — o protocolo, o motivo da glosa ou da falha —
    /// vai no detalhe, em texto corrido logo abaixo. Nulo = nada a mostrar (convênio sem web service).
    /// </summary>
    /// <param name="selo">O selo do último envio da guia; nulo quando a leitura não trouxe a guia.</param>
    /// <param name="pronta">A guia está conferida, sem baixa e fora de envio que a prenda.</param>
    public static SeloDaLinhaNaTela? SeloDaLinha(bool temWebService, SeloDeEnvioTiss? selo, bool pronta)
    {
        if (!temWebService) return null;
        if (selo is null || selo.Estado == EstadoDoEnvioDaGuia.NaoEnviada)
        {
            // "Pronta" vence o "não enviada: o envio foi cancelado" de um pedido antigo: a guia
            // continua conferida, e é isso que a pessoa precisa saber para montar o próximo envio.
            if (pronta) return new("Pronta para envio", "info", null);
            return new(Rotulo(EstadoDoEnvioDaGuia.NaoEnviada), "neutro", Detalhe(Rotulo(EstadoDoEnvioDaGuia.NaoEnviada), selo?.Texto));
        }
        var rotulo = Rotulo(selo.Estado);
        return new(rotulo, Tom(selo.Estado), Detalhe(rotulo, selo.Texto));
    }

    /// <summary>A frase inteira só vai para o detalhe quando diz mais que o rótulo.</summary>
    private static string? Detalhe(string rotulo, string? frase)
        => string.IsNullOrWhiteSpace(frase) || string.Equals(frase.Trim(), rotulo, StringComparison.Ordinal) ? null : frase.Trim();

    /// <summary>
    /// As pendências que têm selo de envio (as de convênio com web service), agrupadas pelo código
    /// do convênio: é o que a tela precisa para ler os selos e as prontas numa ida por convênio —
    /// e não uma por linha.
    /// </summary>
    public static IReadOnlyDictionary<string, IReadOnlyList<int>> GuiasComSeloPorConvenio(IEnumerable<PendenciaCodigo> pendencias)
        => pendencias.Where(p => TemWebService(p.ConvenioCodigo, p.Convenio))
            .GroupBy(p => p.ConvenioCodigo ?? p.Convenio.ToString(), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => (IReadOnlyList<int>)g.Select(p => p.CodigoId).Distinct().ToList(), StringComparer.OrdinalIgnoreCase);

    /// <summary>O selo de cada linha, pronto para desenhar. Linha de convênio sem web service não entra.</summary>
    public static IReadOnlyDictionary<int, SeloDaLinhaNaTela> SelosDasLinhas(
        IEnumerable<int> guiasComSelo, IReadOnlyDictionary<int, SeloDeEnvioTiss> selos, IReadOnlySet<int> prontas)
    {
        var porLinha = new Dictionary<int, SeloDaLinhaNaTela>();
        foreach (var id in guiasComSelo)
            if (SeloDaLinha(true, selos.GetValueOrDefault(id), prontas.Contains(id)) is { } selo)
                porLinha[id] = selo;
        return porLinha;
    }

    // ==================== o envio (o lote) ====================

    /// <summary>
    /// A mensagem saiu e não há resposta em que confiar: NÃO SE SABE se a operadora recebeu.
    /// É o estado em que reenviar pode apresentar a mesma guia duas vezes. A fila o marca do mesmo
    /// jeito para "a operadora respondeu que não" e para "ninguém respondeu" — e a tela trata os
    /// dois com a mesma cautela, porque quem decide é quem conferir no portal.
    /// </summary>
    public static bool NaoSeSabeSeChegou(TransacaoTiss envio) => FilaTiss.NaoSeSabeSeChegou(envio);

    /// <summary>
    /// A ressalva de uma guia que está na lista das PRONTAS mas já foi a um envio (nula = nunca foi,
    /// ou o pedido foi cancelado antes de sair). A fila devolve à lista a guia glosada e a do envio
    /// que falhou — pedir de novo é ato de gente —, e é aqui que a tela impede que esse ato seja
    /// um clique distraído:
    /// <list type="bullet">
    /// <item><b>falhou e não se sabe se chegou</b> (ou não dá para saber, porque o envio saiu da
    /// lista dos recentes): a guia fica TRAVADA até a pessoa declarar que conferiu no portal;</item>
    /// <item><b>falhou antes de sair</b>: nada chegou à operadora; só o aviso;</item>
    /// <item><b>glosada</b>: o motivo fica na linha, para ser corrigido antes do novo envio.</item>
    /// </list>
    /// </summary>
    /// <param name="ultimo">O selo do último envio da guia.</param>
    /// <param name="envioDoSelo">O envio daquele selo, quando está entre os lidos; nulo = não se sabe.</param>
    public static RessalvaDaPronta? DaPronta(SeloDeEnvioTiss? ultimo, TransacaoTiss? envioDoSelo)
        => ultimo?.Estado switch
        {
            EstadoDoEnvioDaGuia.Glosada => new($"No último envio: {ultimo.Texto}. Corrija a guia antes de enviar de novo.", false),
            EstadoDoEnvioDaGuia.Falhou when envioDoSelo is not null && !NaoSeSabeSeChegou(envioDoSelo)
                => new($"O último envio desta guia não saiu ({ultimo.Texto}). Nada chegou à operadora.", false),
            EstadoDoEnvioDaGuia.Falhou => new(
                "Não se sabe se o último envio desta guia chegou à operadora. Confira no portal ANTES de enviar de novo: "
                + "se ela estiver lá, não envie — dê baixa pelo painel de pendências, com o número do portal.", true),
            _ => null
        };

    /// <summary>A pergunta que LIBERA a guia travada para um novo envio: quem responde declara o que conferiu.</summary>
    public static string PerguntaDaLiberacao(string pacienteNome)
        => $"Você conferiu no portal da operadora que a guia de {pacienteNome} NÃO está lá?\n\n"
           + "Se ela estiver lá, enviar de novo apresenta a mesma guia duas vezes (glosa por duplicidade). "
           + "Nesse caso cancele aqui e dê baixa pelo painel de pendências, com o número do portal.";

    /// <summary>A situação do lote, para o selo e a linha de baixo.</summary>
    public static SituacaoDoEnvioNaTela DoEnvio(TransacaoTiss envio)
    {
        if (NaoSeSabeSeChegou(envio))
            return new("Não se sabe se chegou", "perigo", true,
                "Não se sabe se a operadora recebeu este envio. Confira no portal da operadora ANTES de enviar essas guias de novo: "
                + "se elas estiverem lá, dê baixa em cada uma pelo painel de pendências, com o número do portal.");

        var guias = envio.Guias;
        var glosadas = guias.Count(g => g.Situacao == SituacaoGuiaTiss.Glosada);
        var semResposta = guias.Count(g => g.Situacao == SituacaoGuiaTiss.Pendente);
        var protocolo = string.IsNullOrWhiteSpace(envio.Protocolo) ? null : envio.Protocolo;
        return envio.Estado switch
        {
            EstadoTransacaoTiss.Pendente when envio.Tentativas > 0
                => new("Na fila", "info", false, envio.Resultado ?? "Na fila: o sistema vai tentar de novo."),
            EstadoTransacaoTiss.Pendente => new("Na fila", "info", false, "Aguardando o serviço de envio. Ainda não foi à operadora."),
            EstadoTransacaoTiss.EmEnvio => new("Enviando", "info", false, "O serviço de envio está falando com a operadora."),
            EstadoTransacaoTiss.Cancelada => new("Não enviado", "neutro", false, envio.Resultado ?? "O envio foi cancelado antes de ir à operadora."),
            // A mensagem saiu e a operadora RESPONDEU que não (sem a marca da dúvida): não é "não enviado".
            EstadoTransacaoTiss.Falhou when envio.EnvioIniciadoEm is not null => new("Recusado pela operadora", "perigo", false,
                $"{(envio.Erro ?? envio.Resultado ?? "A operadora não informou o motivo.").TrimEnd()} Nada ficou na operadora: corrija e envie de novo."),
            EstadoTransacaoTiss.Falhou => new("Não enviado", "perigo", false, envio.Erro ?? envio.Resultado ?? "O envio não se completou."),
            _ when glosadas > 0 => new("Recebido com glosa", "aviso", false,
                $"Protocolo {protocolo ?? "não informado"}. {glosadas} guia(s) com glosa: veja o motivo na linha."),
            _ when semResposta > 0 => new("Protocolado", "aviso", false,
                $"Protocolo {protocolo ?? "não informado"}. {semResposta} guia(s) ainda sem resposta: consulte a situação na operadora."),
            _ => new("Recebido", "sucesso", false, $"Protocolo {protocolo ?? "não informado"}.")
        };
    }

    /// <summary>Há pedido na fila ou em andamento: a tela acompanha sozinha.</summary>
    public static bool Acompanha(IEnumerable<TransacaoTiss> transacoes)
        => transacoes.Any(t => t.Estado is EstadoTransacaoTiss.Pendente or EstadoTransacaoTiss.EmEnvio);

    /// <summary>O pedido ainda não saiu: quem pediu pode desistir.</summary>
    public static bool PodeCancelarPedido(TransacaoTiss t) => t.Estado == EstadoTransacaoTiss.Pendente;

    /// <summary>Há protocolo a consultar: o envio terminou e a operadora o protocolou.</summary>
    public static bool PodeConsultarSituacao(TransacaoTiss envio)
        => envio is { Tipo: TipoTransacaoTiss.EnvioGuia, Estado: EstadoTransacaoTiss.Concluida } && !string.IsNullOrWhiteSpace(envio.Protocolo);

    /// <summary>Quantas guias do envio a operadora recebeu (as que a baixa das recebidas alcança).</summary>
    public static int Recebidas(TransacaoTiss envio)
        => envio.Estado == EstadoTransacaoTiss.Concluida ? envio.Guias.Count(g => g.Situacao == SituacaoGuiaTiss.Recebida) : 0;

    /// <summary>
    /// A pergunta antes de enviar. Diz QUANTAS guias, para QUEM e que é cobrança de verdade — e
    /// que desfazer depois não é um clique.
    /// </summary>
    public static string PerguntaDoEnvio(int guias, string convenioNome)
        => $"Enviar {guias} guia(s) à {convenioNome} pelo web service?\n\n"
           + "É a COBRANÇA de verdade: a operadora recebe essas guias como apresentadas pela clínica. "
           + "Depois de enviado, não há como desfazer por aqui — só cancelando a guia na operadora.";

    /// <summary>O que impede marcar mais guias (nulo = pode): o lote leva até 100.</summary>
    public static string? ImpedimentoDaMarcacao(int marcadas)
        => marcadas > TransacaoTissLimites.GuiasPorLote
            ? $"Um envio leva até {TransacaoTissLimites.GuiasPorLote} guias e há {marcadas} marcadas: desmarque algumas e envie em dois lotes."
            : null;

    /// <summary>
    /// O aviso da tela quando o envio está desligado para o convênio (nulo = está ligado). Diz
    /// onde ligar — e que a tela, sozinha, não basta.
    /// </summary>
    public static string? AvisoDeEnvioDesligado(bool ligado, string convenioNome)
        => ligado ? null
            : $"O envio pelo web service está desligado para {convenioNome}. As guias conferidas ficam guardadas, e a baixa continua "
              + "sendo feita com o número copiado do portal. Para ligar: Configurações do faturamento → Convênios → "
              + "\"Web service\" na linha do convênio.";

    /// <summary>A linha de uma guia num envio: quem e o quê, para a pessoa reconhecer sem abrir nada.</summary>
    public static string DescricaoDaGuia(CodigoFaturamento? codigo, string numeroGuiaPrestador)
        => codigo?.Atendimento?.Paciente is { } p
            ? $"{p.Nome} — {RotulosEnum.De(codigo.Tipo)} · {codigo.Atendimento.Data:dd/MM/yyyy}"
            : $"Guia {numeroGuiaPrestador}";

    // ==================== a credencial ====================

    /// <summary>O que impede gravar a credencial, campo → frase (vazio = pode). A regra final é do serviço.</summary>
    /// <param name="haSenhaGravada">Já existe senha: o campo em branco quer dizer "não trocar".</param>
    public static IReadOnlyDictionary<string, string> CriticarCredencial(string? login, string? senha, bool haSenhaGravada)
    {
        var erros = new Dictionary<string, string>();
        var usuario = (login ?? string.Empty).Trim();
        if (usuario.Length == 0) erros["Login"] = "Informe o login do web service.";
        else if (usuario.Length > 20) erros["Login"] = "O login tem até 20 caracteres.";
        if (string.IsNullOrEmpty(senha) && !haSenhaGravada) erros["Senha"] = "Informe a senha do web service.";
        return erros;
    }

    /// <summary>
    /// O login mudou e a senha não foi digitada: o serviço grava login e senha JUNTOS (a senha
    /// gravada não é lida de volta), então trocar só o login pede a senha de novo.
    /// </summary>
    public static bool TrocaDeLoginPedeSenha(string? loginGravado, string? loginDigitado, string? senha)
        => !string.Equals((loginGravado ?? string.Empty).Trim(), (loginDigitado ?? string.Empty).Trim(), StringComparison.Ordinal)
           && string.IsNullOrEmpty(senha);

    /// <summary>A frase sobre a senha: a tela nunca a mostra, só diz se existe.</summary>
    public static string SituacaoDaSenha(bool temSenha)
        => temSenha ? "Há uma senha gravada. Ela não é mostrada; para trocar, digite a nova." : "Nenhuma senha gravada.";

    /// <summary>O aviso fixo da chave de envio: ligar aqui não basta.</summary>
    public const string AvisoDasDuasChaves =
        "Ligar aqui não envia nada sozinho: o envio também depende da chave do servidor e do serviço de envio instalado "
        + "(docs/tiss-conector.md). Com esta chave ligada, a baixa da guia ganha \"Conferir e deixar pronta para envio\" "
        + "e a tela Guias TISS ganha o envio à operadora.";

    // ==================== a elegibilidade ====================

    /// <summary>
    /// Como o serviço de envio escreve, na frase do resultado, o nome que a operadora devolveu. A
    /// tela o separa da frase por este rótulo para mostrá-lo ao lado do nome da FICHA — conferência,
    /// nunca cópia para a ficha.
    /// </summary>
    public const string RotuloDoNomeNaOperadora = "Nome na operadora:";

    /// <summary>O nome que a operadora devolveu, tirado da frase do resultado (nulo = ela não devolveu).</summary>
    public static string? NomeNaOperadora(string? resultado)
    {
        if (string.IsNullOrWhiteSpace(resultado)) return null;
        var i = resultado.IndexOf(RotuloDoNomeNaOperadora, StringComparison.Ordinal);
        if (i < 0) return null;
        var nome = resultado[(i + RotuloDoNomeNaOperadora.Length)..].Trim().TrimEnd('.').Trim();
        return nome.Length == 0 ? null : nome;
    }

    /// <summary>
    /// A linha de conferência do nome: o da operadora ao lado do da ficha, e se batem. A comparação
    /// ignora maiúsculas, acentos e espaços a mais — a operadora costuma devolver tudo em maiúsculas
    /// e sem acento, e isso não é divergência. Nulo = a operadora não devolveu nome.
    /// </summary>
    public static (string Texto, bool Confere)? ConferenciaDoNome(string? resultado, string? nomeDaFicha)
    {
        if (NomeNaOperadora(resultado) is not { } daOperadora) return null;
        var ficha = (nomeDaFicha ?? string.Empty).Trim();
        if (ficha.Length == 0) return ($"Na operadora: {daOperadora}.", false);
        var confere = string.Equals(SemAcentos(daOperadora), SemAcentos(ficha), StringComparison.OrdinalIgnoreCase);
        return confere
            ? ($"Na operadora: {daOperadora} · na ficha: {ficha}. Os nomes conferem.", true)
            : ($"Na operadora: {daOperadora} · na ficha: {ficha}. Os nomes são DIFERENTES: confira se é a carteira certa. A ficha não foi alterada.", false);
    }

    private static string SemAcentos(string texto)
    {
        var decomposto = texto.Normalize(System.Text.NormalizationForm.FormD);
        var letras = decomposto.Where(c => System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c) != System.Globalization.UnicodeCategory.NonSpacingMark);
        return string.Join(' ', new string(letras.ToArray()).Split(' ', StringSplitOptions.RemoveEmptyEntries));
    }

    /// <summary>
    /// A leitura de uma consulta de elegibilidade. TRÊS desfechos, e nenhum com a cara do outro:
    /// elegível, não elegível (a operadora negou) e não foi possível consultar (ninguém avaliou).
    /// Enquanto espera: "consultando"; passado o prazo com o pedido ainda na fila, "o serviço de
    /// envio não respondeu" — e a pessoa pode cancelar o pedido.
    /// </summary>
    public static ElegibilidadeNaTela DaElegibilidade(TransacaoTiss consulta, DateTime agora)
    {
        var resultado = (consulta.Resultado ?? string.Empty).Trim();
        switch (consulta.Estado)
        {
            case EstadoTransacaoTiss.Concluida:
                // A frase vem do padrão (TissElegibilidadeWs.Resumo): "Elegível." ou "Não elegível…".
                return resultado.StartsWith("Elegível", StringComparison.Ordinal)
                    ? new(resultado, "sucesso", true, false)
                    : new(resultado.Length == 0 ? "Não elegível (a operadora não informou o motivo)." : resultado, "erro", true, false);
            case EstadoTransacaoTiss.Falhou:
                return new("Não foi possível consultar: " + Motivo(consulta.Erro ?? resultado, "a consulta não se completou."), "atencao", true, false);
            case EstadoTransacaoTiss.Cancelada:
                return new("Não foi possível consultar: " + Motivo(resultado, "o pedido foi cancelado."), "atencao", true, false);
            case EstadoTransacaoTiss.EmEnvio:
                return new("Consultando na operadora…", "info", false, false);
            default:
                return agora - consulta.PedidoEm >= EsperaDaElegibilidade
                    ? new("O serviço de envio não respondeu. O pedido continua na fila; você pode cancelá-lo e conferir no portal da operadora.",
                        "atencao", false, true)
                    : new("Consultando na operadora…", "info", false, true);
        }
    }

    private static string Motivo(string? texto, string padrao)
    {
        var limpo = (texto ?? string.Empty).Trim();
        return limpo.Length == 0 ? padrao : limpo;
    }
}
