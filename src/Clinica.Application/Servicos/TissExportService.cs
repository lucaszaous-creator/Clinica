using System.Security.Cryptography;
using System.Text;
using System.Xml.Linq;
using Clinica.Application.Modelos;
using Clinica.Domain;
using Clinica.Domain.Entities;

namespace Clinica.Application.Servicos;

/// <summary>
/// Gera as mensagens TISS (XML, padrão ANS 4.01.00): lote de guias e recurso de glosa.
/// É uma base estruturada com os dados disponíveis + configuração do prestador — pode exigir
/// ajustes conforme a operadora (códigos TUSS, registro ANS, regras específicas).
/// </summary>
public sealed class TissExportService
{
    public const string VersaoPadrao = "4.01.00";

    private static readonly XNamespace Ans = "http://www.ans.gov.br/padroes/tiss/schemas";

    /// <summary>
    /// Pré-validação do lote: lista os dados obrigatórios do prestador que estão
    /// faltando para as guias do lote (registro ANS, código na operadora, CNPJ e os
    /// códigos TUSS dos tipos de procedimento realmente usados). Vazio = pronto.
    /// Operadoras rejeitam lotes com esses campos em branco — melhor avisar antes.
    /// </summary>
    public IReadOnlyList<string> ValidarPrestador(DadosPrestador prestador, IEnumerable<TipoCodigo> tiposUsados)
    {
        var pendencias = new List<string>();

        if (string.IsNullOrWhiteSpace(prestador.RegistroAnsOperadora))
            pendencias.Add("Registro ANS da operadora não informado.");
        if (string.IsNullOrWhiteSpace(prestador.CodigoNaOperadora))
            pendencias.Add("Código do prestador na operadora não informado.");
        if (string.IsNullOrWhiteSpace(prestador.Cnpj))
            pendencias.Add("CNPJ do prestador não informado.");

        foreach (var tipo in tiposUsados.Distinct().OrderBy(t => t))
            if (string.IsNullOrWhiteSpace(prestador.CodigoTuss(tipo)))
                pendencias.Add($"Código TUSS de {tipo} não configurado (há guias desse tipo no lote).");

        return pendencias;
    }

    /// <summary>Lote de guias (ENVIO_LOTE_GUIAS). Só entram guias já baixadas.</summary>
    public string GerarLoteXml(IReadOnlyList<CodigoFaturamento> codigos, DadosPrestador prestador, string numeroLote)
    {
        var guias = codigos
            .Where(c => c.Baixado)
            .GroupBy(c => c.AtendimentoId)
            .Select((g, i) => CriarGuia(g, prestador, i + 1))
            .ToList();

        var mensagem = new XElement(Ans + "mensagemTISS",
            new XAttribute(XNamespace.Xmlns + "ans", Ans.NamespaceName),
            Cabecalho("ENVIO_LOTE_GUIAS", numeroLote, prestador),
            new XElement(Ans + "prestadorParaOperadora",
                new XElement(Ans + "loteGuias",
                    new XElement(Ans + "numeroLote", numeroLote),
                    new XElement(Ans + "guiasTISS", guias))));

        return Finalizar(mensagem);
    }

    /// <summary>
    /// Recurso de glosa (RECURSO_GLOSA): uma guia de recurso por guia glosada, com o
    /// motivo da tabela ANS e a justificativa do prestador.
    /// </summary>
    public string GerarRecursoGlosaXml(IReadOnlyList<CodigoFaturamento> glosadas, DadosPrestador prestador,
        string numeroGuiaRecurso, string? justificativaGeral = null)
    {
        var itens = glosadas
            .Where(c => c.GlosaEmAberto)
            .Select(c => new XElement(Ans + "opcaoRecurso",
                new XElement(Ans + "guiaRecurso",
                    new XElement(Ans + "numeroGuiaOrigem", c.NumeroGuiaReal ?? c.Id.ToString()),
                    ElementoOpcional("codigoGlosaGuia", c.MotivoGlosaCodigo),
                    new XElement(Ans + "justificativaGuia",
                        string.IsNullOrWhiteSpace(c.MotivoGlosa) ? justificativaGeral ?? string.Empty : c.MotivoGlosa))))
            .ToList();

        var mensagem = new XElement(Ans + "mensagemTISS",
            new XAttribute(XNamespace.Xmlns + "ans", Ans.NamespaceName),
            Cabecalho("RECURSO_GLOSA", numeroGuiaRecurso, prestador),
            new XElement(Ans + "prestadorParaOperadora",
                new XElement(Ans + "recursoGlosa",
                    new XElement(Ans + "numeroGuiaRecursoGlosa", numeroGuiaRecurso),
                    new XElement(Ans + "nomePrestador", prestador.RazaoSocial ?? prestador.NomeFantasia ?? string.Empty),
                    new XElement(Ans + "recursosGuia", itens))));

        return Finalizar(mensagem);
    }

    // ---------- Blocos comuns ----------

    private static XElement Cabecalho(string tipoTransacao, string sequencial, DadosPrestador prestador)
    {
        var agora = DateTime.Now;
        return new XElement(Ans + "cabecalho",
            new XElement(Ans + "identificacaoTransacao",
                new XElement(Ans + "tipoTransacao", tipoTransacao),
                new XElement(Ans + "sequencialTransacao", sequencial),
                new XElement(Ans + "dataRegistroTransacao", agora.ToString("yyyy-MM-dd")),
                new XElement(Ans + "horaRegistroTransacao", agora.ToString("HH:mm:ss"))),
            new XElement(Ans + "origem",
                new XElement(Ans + "identificacaoPrestador",
                    new XElement(Ans + "codigoPrestadorNaOperadora", prestador.CodigoNaOperadora ?? string.Empty))),
            new XElement(Ans + "destino",
                new XElement(Ans + "registroANS", prestador.RegistroAnsOperadora ?? string.Empty)),
            new XElement(Ans + "Padrao", VersaoPadrao));
    }

    /// <summary>
    /// Consulta pura vira guia de consulta; qualquer procedimento (acupuntura, eletro, BSV)
    /// vira guia SP/SADT, como as operadoras esperam no 4.01.
    /// </summary>
    private static XElement CriarGuia(IGrouping<int, CodigoFaturamento> grupo, DadosPrestador prestador, int numeroGuia)
    {
        var somenteConsulta = grupo.All(c => c.Tipo is TipoCodigo.Consulta or TipoCodigo.ConsultaEspecialidade);
        return somenteConsulta
            ? CriarGuiaConsulta(grupo, prestador, numeroGuia)
            : CriarGuiaSpSadt(grupo, prestador, numeroGuia);
    }

    private static XElement CriarGuiaConsulta(IGrouping<int, CodigoFaturamento> grupo, DadosPrestador prestador, int numeroGuia)
    {
        var primeiro = grupo.First();

        return new XElement(Ans + "guiaConsulta",
            new XElement(Ans + "cabecalhoConsulta",
                new XElement(Ans + "registroANS", prestador.RegistroAnsOperadora ?? string.Empty),
                new XElement(Ans + "numeroGuiaPrestador", primeiro.NumeroGuiaReal ?? numeroGuia.ToString())),
            DadosBeneficiario(primeiro),
            ContratadoExecutante(prestador),
            new XElement(Ans + "dadosAtendimento",
                new XElement(Ans + "dataAtendimento", primeiro.Atendimento?.Data.ToString("yyyy-MM-dd") ?? string.Empty),
                new XElement(Ans + "tipoConsulta", "1"), // 1 = primeira consulta / consulta
                new XElement(Ans + "procedimento",
                    new XElement(Ans + "codigoTabela", "22"),
                    new XElement(Ans + "codigoProcedimento", prestador.CodigoTuss(primeiro.Tipo)))));
    }

    private static XElement CriarGuiaSpSadt(IGrouping<int, CodigoFaturamento> grupo, DadosPrestador prestador, int numeroGuia)
    {
        var primeiro = grupo.First();
        var data = primeiro.Atendimento?.Data.ToString("yyyy-MM-dd") ?? string.Empty;

        var procedimentos = grupo.Select((c, i) => new XElement(Ans + "procedimentoExecutado",
            new XElement(Ans + "sequencialItem", i + 1),
            new XElement(Ans + "dataExecucao", data),
            new XElement(Ans + "procedimento",
                new XElement(Ans + "codigoTabela", "22"),
                new XElement(Ans + "codigoProcedimento", prestador.CodigoTuss(c.Tipo)),
                new XElement(Ans + "descricaoProcedimento", c.Tipo.ToString())),
            new XElement(Ans + "quantidadeExecutada", "1")));

        return new XElement(Ans + "guiaSP-SADT",
            new XElement(Ans + "cabecalhoGuia",
                new XElement(Ans + "registroANS", prestador.RegistroAnsOperadora ?? string.Empty),
                new XElement(Ans + "numeroGuiaPrestador", primeiro.NumeroGuiaReal ?? numeroGuia.ToString())),
            DadosBeneficiario(primeiro),
            new XElement(Ans + "dadosSolicitante", ContratadoExecutante(prestador)),
            new XElement(Ans + "dadosExecutante", ContratadoExecutante(prestador)),
            new XElement(Ans + "dadosAtendimento",
                new XElement(Ans + "dataAtendimento", data)),
            new XElement(Ans + "procedimentosExecutados", procedimentos));
    }

    private static XElement DadosBeneficiario(CodigoFaturamento codigo)
    {
        var paciente = codigo.Atendimento?.Paciente;
        // Carteirinha do convênio quando cadastrada; CPF como último recurso.
        // O TISS 4 não transporta mais o nome do beneficiário (privacidade/LGPD).
        var carteira = !string.IsNullOrWhiteSpace(paciente?.Carteirinha)
            ? paciente!.Carteirinha!
            : paciente?.Documento ?? string.Empty;
        return new XElement(Ans + "dadosBeneficiario",
            new XElement(Ans + "numeroCarteira", carteira),
            new XElement(Ans + "atendimentoRN", "N"));
    }

    private static XElement ContratadoExecutante(DadosPrestador prestador)
        => new(Ans + "contratadoExecutante",
            new XElement(Ans + "codigoPrestadorNaOperadora", prestador.CodigoNaOperadora ?? string.Empty),
            ElementoOpcional("cnpjContratado", prestador.Cnpj),
            ElementoOpcional("CNES", prestador.Cnes));

    private static XElement? ElementoOpcional(string nome, string? valor)
        => string.IsNullOrWhiteSpace(valor) ? null : new XElement(Ans + nome, valor);

    /// <summary>Anexa o epílogo (hash MD5 dos valores da mensagem, exigido pelo padrão) e serializa.</summary>
    private static string Finalizar(XElement mensagem)
    {
        mensagem.Add(new XElement(Ans + "epilogo",
            new XElement(Ans + "hash", CalcularHash(mensagem))));

        var doc = new XDocument(new XDeclaration("1.0", "UTF-8", null), mensagem);
        return doc.ToString();
    }

    /// <summary>MD5 (hex minúsculo) da concatenação dos valores-texto da mensagem, na ordem do documento.</summary>
    private static string CalcularHash(XElement mensagem)
    {
        var sb = new StringBuilder();
        foreach (var texto in mensagem.DescendantNodes().OfType<XText>())
            sb.Append(texto.Value);

        var bytes = MD5.HashData(Encoding.UTF8.GetBytes(sb.ToString()));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}
