using System.Text;
using Clinica.Domain.Entities;
using Clinica.Domain.Regras;

namespace Clinica.Application.Servicos;

/// <summary>
/// O que a tela de lotes TISS AFIRMA e decide ao exportar (out/2026, subiu do TissViewModel
/// do WPF quando a tela virou Blazor): o agrupamento por operadora, as frases das três
/// confirmações (operadoras, radar de glosas, dados incompletos), o nome dos arquivos, o
/// pacote .zip quando sai mais de um lote, a validação do XML gerado e as frases do retorno.
///
/// Mora aqui, e não no Razor, porque são DECISÕES com consequência: agrupar por outro
/// critério criaria dois lotes "da mesma" operadora; a frase errada no retorno diria que
/// tudo foi aceito com guias glosadas. Razor desenha; isto decide — e tem teste.
/// </summary>
public static class ExportacaoLoteTiss
{
    /// <summary>Quantos alertas do radar entram na pergunta (o resto fica para a próxima rodada).</summary>
    public const int AlertasNaPergunta = 8;

    /// <summary>Quantos problemas de validação por lote entram no aviso.</summary>
    public const int ProblemasPorLote = 6;

    /// <summary>As guias candidatas de UMA operadora — um lote, um XML.</summary>
    public sealed record GrupoDeOperadora(string ConvenioCodigo, string Nome, IReadOnlyList<CodigoFaturamento> Guias);

    /// <summary>
    /// UM LOTE POR OPERADORA (parcela 60): o XML tem um destino só. Agrupa pela MESMA
    /// resolução do serviço (<see cref="LoteTissService.ConvenioDaGuia"/>) e ordena pelo nome
    /// do catálogo — o que a confirmação lista e a ordem em que os lotes nascem.
    /// </summary>
    public static IReadOnlyList<GrupoDeOperadora> Agrupar(IEnumerable<CodigoFaturamento> candidatas)
        => candidatas
            .GroupBy(LoteTissService.ConvenioDaGuia)
            .Select(g => new GrupoDeOperadora(g.Key, CatalogoConvenios.Nome(g.Key), g.ToList()))
            .OrderBy(g => g.Nome, StringComparer.CurrentCulture)
            .ToList();

    /// <summary>A pergunta "Um lote por operadora" — só existe com duas ou mais.</summary>
    public static string ConfirmacaoDeOperadoras(IReadOnlyList<GrupoDeOperadora> grupos)
        => "As guias do período são de " + grupos.Count + " operadoras — sai um lote (e um XML) para cada uma:\n\n• " +
           string.Join("\n• ", grupos.Select(g => $"{g.Nome}: {g.Guias.Count} guia(s)")) +
           "\n\nGerar os lotes?";

    /// <summary>A pergunta do radar de glosas, com no máximo <see cref="AlertasNaPergunta"/> alertas.</summary>
    public static string ConfirmacaoDoRadar(IReadOnlyList<string> alertas)
        => "Risco de glosa detectado neste lote:\n\n• " +
           string.Join("\n\n• ", alertas.Take(AlertasNaPergunta)) +
           (alertas.Count > AlertasNaPergunta ? $"\n\n… e mais {alertas.Count - AlertasNaPergunta} alerta(s)." : string.Empty) +
           "\n\nExportar mesmo assim?";

    /// <summary>A pergunta dos dados do prestador incompletos para o TISS.</summary>
    public static string ConfirmacaoDePendencias(IReadOnlyList<string> pendencias)
        => "O lote será gerado com pendências que a operadora pode rejeitar:\n\n• " +
           string.Join("\n• ", pendencias) +
           "\n\nCorrija na tela Configurações (Clínica/prestador e Códigos TUSS) ou exporte mesmo assim.\n\nExportar mesmo assim?";

    /// <summary>Nome de operadora vira pedaço de nome de arquivo: sem caractere proibido, sem espaço.</summary>
    public static string NomeSeguroDeArquivo(string nome)
    {
        var invalidos = Path.GetInvalidFileNameChars().Concat(['\\', '/', ':', '*', '?', '"', '<', '>', '|']).ToHashSet();
        var limpo = new string(nome.Select(c => invalidos.Contains(c) || char.IsWhiteSpace(c) ? '-' : c).ToArray());
        return limpo.Trim('-');
    }

    /// <summary>
    /// O nome do XML de um lote. Lote único sai com o nome de sempre; com várias operadoras,
    /// cada um leva o nome dela — dois arquivos com o mesmo nome se sobrescreveriam.
    /// </summary>
    public static string NomeDoArquivo(int numeroLote, DateOnly fim, string? nomeOperadora)
        => nomeOperadora is null
            ? $"TISS-Lote-{numeroLote:000000}-{fim:yyyy-MM}.xml"
            : $"TISS-Lote-{numeroLote:000000}-{NomeSeguroDeArquivo(nomeOperadora)}-{fim:yyyy-MM}.xml";

    /// <summary>O nome da 2ª via de um lote já criado.</summary>
    public static string NomeDaSegundaVia(int numeroLote) => $"TISS-Lote-{numeroLote:000000}.xml";

    /// <summary>O XML como ele vai para o disco: UTF-8 sem BOM, como o WPF gravava.</summary>
    public static byte[] Bytes(string xml) => new UTF8Encoding(encoderShouldEmitUTF8Identifier: false).GetBytes(xml);

    /// <summary>A pasta onde o app guarda a cópia de cada lote e os .xsd da ANS (%APPDATA%\ClinicaFaturamento\tiss).</summary>
    public static string PastaTiss() => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ClinicaFaturamento", "tiss");

    /// <summary>
    /// Valida o XML gerado: sempre a validação estrutural; e contra o XSD oficial da ANS
    /// quando houver um .xsd em <paramref name="pastaSchemas"/>. Falha de leitura do XSD não
    /// derruba a exportação — vai para o log, e a estrutural continua valendo.
    /// </summary>
    public static IReadOnlyList<string> ValidarXml(string xml, string? pastaSchemas)
    {
        var problemas = new List<string>(TissValidador.Validar(xml));
        if (pastaSchemas is null) return problemas;
        try
        {
            if (Directory.Exists(pastaSchemas))
            {
                var xsd = Directory.GetFiles(pastaSchemas, "*.xsd").OrderBy(f => f, StringComparer.Ordinal).FirstOrDefault();
                if (xsd is not null)
                    problemas.AddRange(TissValidador.ValidarComXsd(xml, xsd));
            }
        }
        catch (Exception ex)
        {
            Diagnostico.Registrar("Validação XSD do lote TISS", ex);
        }
        return problemas;
    }

    /// <summary>
    /// Guarda uma cópia do lote (histórico auditável do que foi enviado à operadora). Cópia é
    /// conveniência: falhar aqui não desfaz a exportação — vai para o log e devolve nulo.
    /// </summary>
    public static string? ArquivarCopia(string xml, int numeroLote, string pasta, DateTime agora)
    {
        try
        {
            Directory.CreateDirectory(pasta);
            var arquivo = Path.Combine(pasta, $"TISS-Lote-{numeroLote:000000}-{agora:yyyy-MM-dd-HHmm}.xml");
            File.WriteAllBytes(arquivo, Bytes(xml));
            return arquivo;
        }
        catch (Exception ex)
        {
            Diagnostico.Registrar("Arquivar cópia do lote TISS", ex);
            return null;
        }
    }

    /// <summary>O pedaço do aviso de validação de UM lote (vazio = nada a dizer).</summary>
    public static string? ProblemasDoLote(int numeroLote, string nomeOperadora, IReadOnlyList<string> problemas)
        => problemas.Count == 0
            ? null
            : $"Lote nº {numeroLote} ({nomeOperadora}):\n• " + string.Join("\n• ", problemas.Take(ProblemasPorLote));

    /// <summary>O aviso "Validação do XML TISS", depois de os lotes estarem gerados.</summary>
    public static string AvisoDeValidacao(IReadOnlyList<string> avisos)
        => "Os lotes foram gerados, mas a validação encontrou problemas que a operadora pode recusar:\n\n" +
           string.Join("\n\n", avisos) +
           "\n\nCorrija os cadastros/Configurações e use \"XML\" na linha do lote para regenerar o arquivo antes de enviar.";

    /// <summary>A frase depois de gerar, com ONDE foi gravado (o arquivo ou a pasta), como no WPF.</summary>
    public static string MensagemDeGerados(IReadOnlyList<string> gerados, string destino)
        => gerados.Count switch
        {
            0 => "Nenhum lote gerado — outra máquina exportou as guias no meio-tempo.",
            1 => $"Lote {gerados[0]}: {destino}",
            _ => $"{gerados.Count} lotes gerados em {destino}: " + string.Join(" · ", gerados)
        };

    /// <summary>
    /// O texto do demonstrativo XML que a operadora mandou. A maioria declara UTF-8, mas há
    /// operadora que manda ISO-8859-1/windows-1252 — ler esse como UTF-8 estragaria os acentos
    /// do motivo da glosa. Quem decide é a declaração do próprio arquivo; sem ela, UTF-8 (com
    /// o BOM respeitado, como o File.ReadAllText do WPF fazia).
    /// </summary>
    public static string LerDemonstrativo(byte[] conteudo)
    {
        var inicio = Encoding.ASCII.GetString(conteudo, 0, Math.Min(conteudo.Length, 200)).ToLowerInvariant();
        if (inicio.Contains("encoding=\"iso-8859-1\"") || inicio.Contains("encoding='iso-8859-1'")
            || inicio.Contains("encoding=\"windows-1252\"") || inicio.Contains("encoding='windows-1252'"))
            return Encoding.Latin1.GetString(conteudo);
        using var leitor = new StreamReader(new MemoryStream(conteudo), new UTF8Encoding(false), detectEncodingFromByteOrderMarks: true);
        return leitor.ReadToEnd();
    }

    /// <summary>As ressalvas da importação do demonstrativo (nulo = importação limpa).</summary>
    public static string? RessalvasDaImportacao(ResultadoImportacaoRetorno resultado)
    {
        if (resultado.GuiasForaDoLote.Count == 0 && resultado.GuiasSemRetorno.Count == 0) return null;
        return ((resultado.GuiasForaDoLote.Count > 0
                   ? $"Guias no XML que NÃO são deste lote: {string.Join(", ", resultado.GuiasForaDoLote.Take(10))}. Confira se o arquivo é do lote certo.\n\n"
                   : string.Empty) +
               (resultado.GuiasSemRetorno.Count > 0
                   ? $"Guias do lote sem retorno no XML: {string.Join(", ", resultado.GuiasSemRetorno.Take(10))}. Elas ficam como aceitas — ajuste manualmente se necessário."
                   : string.Empty)).TrimEnd();
    }

    /// <summary>A confirmação do retorno registrado.</summary>
    public static string MensagemDoRetorno(int numeroLote, int glosadas)
        => glosadas == 0
            ? $"Retorno do lote nº {numeroLote} registrado — todas as guias aceitas."
            : $"Retorno do lote nº {numeroLote} registrado — {glosadas} guia(s) glosada(s) foram para o Controle de glosas, com prazo de recurso correndo.";
}
