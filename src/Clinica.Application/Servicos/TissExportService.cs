using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Xml.Linq;
using Clinica.Application.Modelos;
using Clinica.Domain;
using Clinica.Domain.Entities;
using Clinica.Domain.Regras;

namespace Clinica.Application.Servicos;

/// <summary>
/// Gera as mensagens TISS (XML, padrão ANS 4.01.00): lote de guias e recurso de glosa.
/// O que sai daqui VALIDA contra o esquema oficial da ANS (nov/2026) — os .xsd vão embutidos na
/// DLL (<c>Recursos/Tiss</c>), a exportação valida todo lote com eles e
/// <c>TissConformidadeXsdTests</c> prova os dois tipos de guia e o recurso. Regra particular de
/// operadora (código TUSS, tabela própria) continua sendo configuração.
/// </summary>
public sealed class TissExportService
{
    public const string VersaoPadrao = "4.01.00";

    /// <summary>O teto do esquema: <c>maxOccurs="100"</c> em cada tipo de guia do lote.</summary>
    public const int GuiasPorLote = 100;

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

    /// <summary>
    /// Lote de guias (ENVIO_LOTE_GUIAS). Só entram guias já baixadas.
    ///
    /// ⚠️ O esquema da ANS aceita UM tipo de guia por lote (consulta OU SP/SADT) e no máximo
    /// <see cref="GuiasPorLote"/> guias — quem separa é <see cref="ExportacaoLoteTiss.Agrupar"/>,
    /// antes de o lote nascer. Aqui o XML sai com o que o lote tem; lote antigo, misturado,
    /// regenera misturado e a validação diz.
    /// </summary>
    public string GerarLoteXml(IReadOnlyList<CodigoFaturamento> codigos, DadosPrestador prestador, string numeroLote,
        ContextoTiss? contexto = null)
    {
        contexto ??= ContextoTiss.Vazio;
        var guias = codigos
            .Where(c => c.Baixado)
            .GroupBy(c => c.AtendimentoId)
            .SelectMany(g => SepararGuias(g))
            // As de consulta primeiro: dentro de <guiasTISS> os tipos não se intercalam.
            .OrderBy(g => g.All(c => EhConsulta(c.Tipo)) ? 0 : 1)
            .Select((g, i) => CriarGuia(g, prestador, i + 1, contexto))
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

    /// <summary>O lote a que um recurso de glosa responde: o esquema exige o número dele e o protocolo da operadora.</summary>
    public sealed record LoteDoRecurso(string NumeroLote, string? Protocolo, string NomeOperadora);

    /// <summary>
    /// Recurso de glosa (RECURSO_GLOSA) das guias glosadas de UM lote: o padrão amarra o
    /// recurso ao lote e ao protocolo que a operadora devolveu. Um <c>recursoGuia</c> por guia,
    /// com o motivo da tabela ANS e a justificativa do prestador.
    /// </summary>
    public string GerarRecursoGlosaXml(IReadOnlyList<CodigoFaturamento> glosadas, DadosPrestador prestador,
        string numeroGuiaRecurso, LoteDoRecurso lote, ContextoTiss? contexto = null, DateOnly? dataRecurso = null,
        string? justificativaGeral = null)
    {
        contexto ??= ContextoTiss.Vazio;
        var emAberto = glosadas.Where(c => c.GlosaEmAberto).ToList();
        var guias = emAberto
            .GroupBy(c => c.NumeroGuiaReal ?? c.Id.ToString(CultureInfo.InvariantCulture))
            .Select(g => new XElement(Ans + "recursoGuia",
                new XElement(Ans + "numeroGuiaOrigem", g.Key),
                new XElement(Ans + "opcaoRecursoGuia",
                    g.Select(c => new XElement(Ans + "recursoGuiaCompleta",
                        new XElement(Ans + "codGlosaGuia", c.MotivoGlosaCodigo ?? string.Empty),
                        new XElement(Ans + "justificativaGuia", Justificativa(c, justificativaGeral)))))))
            .ToList();

        var mensagem = new XElement(Ans + "mensagemTISS",
            new XAttribute(XNamespace.Xmlns + "ans", Ans.NamespaceName),
            Cabecalho("RECURSO_GLOSA", numeroGuiaRecurso, prestador),
            new XElement(Ans + "prestadorParaOperadora",
                new XElement(Ans + "recursoGlosa",
                    new XElement(Ans + "guiaRecursoGlosa",
                        new XElement(Ans + "registroANS", prestador.RegistroAnsOperadora ?? string.Empty),
                        new XElement(Ans + "numeroGuiaRecGlosaPrestador", numeroGuiaRecurso),
                        new XElement(Ans + "nomeOperadora", Cortar(lote.NomeOperadora, 70)),
                        new XElement(Ans + "objetoRecurso", "2"), // 2 = recurso de guia
                        new XElement(Ans + "dadosContratado", Contratado(prestador)),
                        new XElement(Ans + "numeroLote", lote.NumeroLote),
                        new XElement(Ans + "numeroProtocolo", SoDigitos(lote.Protocolo)),
                        new XElement(Ans + "opcaoRecurso", guias),
                        new XElement(Ans + "valorTotalRecursado", Dinheiro(emAberto.Sum(contexto.ValorDe))),
                        new XElement(Ans + "dataRecurso", (dataRecurso ?? DateOnly.FromDateTime(DateTime.Today)).ToString("yyyy-MM-dd"))))));

        return Finalizar(mensagem);
    }

    private static string Justificativa(CodigoFaturamento c, string? geral)
        => Cortar(!string.IsNullOrWhiteSpace(c.MotivoGlosa) ? c.MotivoGlosa!
            : !string.IsNullOrWhiteSpace(geral) ? geral! : "Solicitamos a revisão da glosa.", 500);

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

    /// <summary>Consulta (de qualquer tipo) — o que sai em guia de consulta, e não SP/SADT.</summary>
    public static bool EhConsulta(TipoCodigo tipo) => tipo is TipoCodigo.Consulta or TipoCodigo.ConsultaEspecialidade;

    /// <summary>
    /// As GUIAS de um atendimento (out/2026): as consultas numa, os procedimentos noutra.
    /// Até a coleta de preventivo, um atendimento nunca misturava os dois — a modalidade
    /// Consulta só gerava consulta —, e agrupar por atendimento bastava. A consulta com
    /// preventivo são DUAS guias (consulta + SP/SADT do preventivo); agrupadas, a consulta
    /// sairia dentro de uma SP/SADT. Atendimento sem mistura continua com uma guia só, igual.
    /// O PDF da guia usa a mesma separação, para o papel e o XML dizerem o mesmo.
    /// </summary>
    public static List<IReadOnlyList<CodigoFaturamento>> SepararGuias(IEnumerable<CodigoFaturamento> codigos)
    {
        var lista = codigos.ToList();
        var consultas = lista.Where(c => EhConsulta(c.Tipo)).ToList();
        var procedimentos = lista.Where(c => !EhConsulta(c.Tipo)).ToList();
        var guias = new List<IReadOnlyList<CodigoFaturamento>>();
        if (consultas.Count > 0) guias.Add(consultas);
        if (procedimentos.Count > 0) guias.Add(procedimentos);
        return guias;
    }

    /// <summary>
    /// Consulta pura vira guia de consulta; qualquer procedimento (acupuntura, eletro, BSV,
    /// preventivo) vira guia SP/SADT, como as operadoras esperam no 4.01.
    /// </summary>
    private static XElement CriarGuia(IReadOnlyList<CodigoFaturamento> grupo, DadosPrestador prestador, int numeroGuia,
        ContextoTiss contexto)
    {
        var somenteConsulta = grupo.All(c => EhConsulta(c.Tipo));
        return somenteConsulta
            ? CriarGuiaConsulta(grupo, prestador, numeroGuia, contexto)
            : CriarGuiaSpSadt(grupo, prestador, numeroGuia, contexto);
    }

    // ⚠️ A ORDEM dos elementos é a do esquema da ANS (xs:sequence): trocar duas linhas de
    // lugar reprova o lote. `TissConformidadeXsdTests` valida os dois tipos de guia contra o
    // esquema oficial — é a rede desta página.

    private static XElement CriarGuiaConsulta(IReadOnlyList<CodigoFaturamento> grupo, DadosPrestador prestador, int numeroGuia,
        ContextoTiss contexto)
    {
        var primeiro = grupo.First();

        return new XElement(Ans + "guiaConsulta",
            new XElement(Ans + "cabecalhoConsulta",
                new XElement(Ans + "registroANS", prestador.RegistroAnsOperadora ?? string.Empty),
                new XElement(Ans + "numeroGuiaPrestador", primeiro.NumeroGuiaReal ?? numeroGuia.ToString(CultureInfo.InvariantCulture))),
            DadosBeneficiario(primeiro),
            new XElement(Ans + "contratadoExecutante", Contratado(prestador), Cnes(prestador)),
            Profissional("profissionalExecutante", contexto.ProfissionalDe(primeiro)),
            new XElement(Ans + "indicacaoAcidente", NaoAcidente),
            new XElement(Ans + "dadosAtendimento",
                new XElement(Ans + "regimeAtendimento", Ambulatorial),
                new XElement(Ans + "dataAtendimento", primeiro.Atendimento?.Data.ToString("yyyy-MM-dd") ?? string.Empty),
                new XElement(Ans + "tipoConsulta", "1"), // 1 = primeira consulta / consulta
                new XElement(Ans + "procedimento",
                    new XElement(Ans + "codigoTabela", "22"),
                    new XElement(Ans + "codigoProcedimento", prestador.CodigoTuss(primeiro.Tipo)),
                    new XElement(Ans + "valorProcedimento", Dinheiro(contexto.ValorDe(primeiro))))));
    }

    private static XElement CriarGuiaSpSadt(IReadOnlyList<CodigoFaturamento> grupo, DadosPrestador prestador, int numeroGuia,
        ContextoTiss contexto)
    {
        var primeiro = grupo.First();
        var data = primeiro.Atendimento?.Data.ToString("yyyy-MM-dd") ?? string.Empty;
        var profissional = contexto.ProfissionalDe(primeiro);

        var procedimentos = grupo.Select((c, i) => new XElement(Ans + "procedimentoExecutado",
            new XElement(Ans + "sequencialItem", i + 1),
            new XElement(Ans + "dataExecucao", data),
            new XElement(Ans + "procedimento",
                new XElement(Ans + "codigoTabela", "22"),
                new XElement(Ans + "codigoProcedimento", prestador.CodigoTuss(c.Tipo)),
                new XElement(Ans + "descricaoProcedimento", Descricao(c.Tipo))),
            new XElement(Ans + "quantidadeExecutada", "1"),
            new XElement(Ans + "reducaoAcrescimo", "1.00"), // 1.00 = sem redução nem acréscimo
            new XElement(Ans + "valorUnitario", Dinheiro(contexto.ValorDe(c))),
            new XElement(Ans + "valorTotal", Dinheiro(contexto.ValorDe(c)))));
        var total = Dinheiro(grupo.Sum(contexto.ValorDe));

        return new XElement(Ans + "guiaSP-SADT",
            new XElement(Ans + "cabecalhoGuia",
                new XElement(Ans + "registroANS", prestador.RegistroAnsOperadora ?? string.Empty),
                new XElement(Ans + "numeroGuiaPrestador", primeiro.NumeroGuiaReal ?? numeroGuia.ToString(CultureInfo.InvariantCulture))),
            DadosBeneficiario(primeiro),
            // A sessão é pedida e feita na própria clínica: solicitante e executante são o mesmo.
            new XElement(Ans + "dadosSolicitante",
                new XElement(Ans + "contratadoSolicitante", Contratado(prestador)),
                new XElement(Ans + "nomeContratadoSolicitante", Cortar(prestador.RazaoSocial ?? prestador.NomeFantasia ?? string.Empty, 70)),
                Profissional("profissionalSolicitante", profissional)),
            new XElement(Ans + "dadosSolicitacao",
                new XElement(Ans + "dataSolicitacao", data),
                new XElement(Ans + "caraterAtendimento", "1")), // 1 = eletiva
            new XElement(Ans + "dadosExecutante",
                new XElement(Ans + "contratadoExecutante", Contratado(prestador)),
                Cnes(prestador)),
            new XElement(Ans + "dadosAtendimento",
                new XElement(Ans + "tipoAtendimento", TipoAtendimento(grupo)),
                new XElement(Ans + "indicacaoAcidente", NaoAcidente),
                new XElement(Ans + "regimeAtendimento", Ambulatorial)),
            new XElement(Ans + "procedimentosExecutados", procedimentos),
            new XElement(Ans + "valorTotal",
                new XElement(Ans + "valorProcedimentos", total),
                new XElement(Ans + "valorTotalGeral", total)));
    }

    private const string NaoAcidente = "9";   // dm_indicadorAcidente: 9 = não acidente
    private const string Ambulatorial = "01"; // dm_regimeAtendimento: 01 = ambulatorial

    /// <summary>dm_tipoAtendimento: 03 = outras terapias (acupuntura, eletro, BSV); 23 = exame (a coleta do preventivo).</summary>
    private static string TipoAtendimento(IReadOnlyList<CodigoFaturamento> grupo)
        => grupo.All(c => c.Tipo == TipoCodigo.Preventivo) ? "23" : "03";

    /// <summary>O nome do procedimento na guia — o mesmo do PDF, e não o nome do enum.</summary>
    public static string Descricao(TipoCodigo tipo) => tipo switch
    {
        TipoCodigo.Consulta => "Consulta",
        TipoCodigo.Acupuntura => "Acupuntura",
        TipoCodigo.Eletroacupuntura => "Eletroacupuntura",
        TipoCodigo.Bsv => "Bloqueio Simpático Venoso",
        TipoCodigo.ConsultaEspecialidade => "Consulta de especialidade",
        TipoCodigo.Preventivo => "Coleta de preventivo",
        _ => tipo.ToString()
    };

    private static XElement DadosBeneficiario(CodigoFaturamento codigo)
    {
        var paciente = codigo.Atendimento?.Paciente;
        // Carteirinha do convênio quando cadastrada; CPF como último recurso.
        // O TISS 4 não transporta mais o nome do beneficiário (privacidade/LGPD).
        var carteira = !string.IsNullOrWhiteSpace(paciente?.Carteirinha)
            ? paciente!.Carteirinha!
            : paciente?.Documento ?? string.Empty;
        return new XElement(Ans + "dadosBeneficiario",
            new XElement(Ans + "numeroCarteira", carteira.Trim()),
            new XElement(Ans + "atendimentoRN", "N"));
    }

    /// <summary>
    /// A identificação do contratado é UMA só (xs:choice): o código na operadora, e o CNPJ só
    /// quando a clínica não tem código — os dois juntos reprovam a guia.
    /// </summary>
    private static XElement Contratado(DadosPrestador prestador)
        => !string.IsNullOrWhiteSpace(prestador.CodigoNaOperadora) || string.IsNullOrWhiteSpace(prestador.Cnpj)
            ? new XElement(Ans + "codigoPrestadorNaOperadora", prestador.CodigoNaOperadora?.Trim() ?? string.Empty)
            : new XElement(Ans + "cnpjContratado", SoDigitos(prestador.Cnpj));

    /// <summary>O CNES é obrigatório; quem não tem preenche com 9999999, como o padrão manda.</summary>
    private static XElement Cnes(DadosPrestador prestador)
        => new(Ans + "CNES", string.IsNullOrWhiteSpace(prestador.Cnes) ? TabelasTiss.CnesNaoInformado : prestador.Cnes.Trim());

    /// <summary>
    /// Conselho, número, UF e CBO de quem atendeu. O que a clínica não cadastrou sai em branco
    /// (o CBO, como "não informado") — a exportação avisou antes, e a validação aponta o campo.
    /// </summary>
    private static XElement Profissional(string elemento, ProfissionalTiss? p)
        => new(Ans + elemento,
            ElementoOpcional("nomeProfissional", p?.Nome is { } nome ? Cortar(nome, 70) : null),
            new XElement(Ans + "conselhoProfissional", p?.Conselho ?? string.Empty),
            new XElement(Ans + "numeroConselhoProfissional", p?.Numero ?? string.Empty),
            new XElement(Ans + "UF", p?.Uf ?? string.Empty),
            new XElement(Ans + "CBOS", p?.Cbos ?? TabelasTiss.CbosNaoInformado));

    private static string Dinheiro(decimal valor) => valor.ToString("0.00", CultureInfo.InvariantCulture);

    private static string SoDigitos(string? texto) => new((texto ?? string.Empty).Where(char.IsDigit).ToArray());

    private static string Cortar(string texto, int tamanho)
    {
        texto = texto.Trim();
        return texto.Length <= tamanho ? texto : texto[..tamanho];
    }

    private static XElement? ElementoOpcional(string nome, string? valor)
        => string.IsNullOrWhiteSpace(valor) ? null : new XElement(Ans + nome, valor);

    /// <summary>
    /// A codificação do arquivo TISS: ISO-8859-1, a mesma dos esquemas da ANS e a que os
    /// demonstrativos das operadoras trazem. O hash do epílogo é calculado sobre ESTES bytes —
    /// arquivo e hash em codificações diferentes deixam de conferir no primeiro acento.
    /// </summary>
    public static readonly Encoding Codificacao = Encoding.Latin1;

    /// <summary>O XML como vai para o disco: ISO-8859-1, com a declaração que diz isso.</summary>
    public static byte[] Bytes(string xml) => Codificacao.GetBytes(xml);

    /// <summary>Anexa o epílogo (hash MD5 dos valores da mensagem, exigido pelo padrão) e serializa.</summary>
    private static string Finalizar(XElement mensagem)
    {
        mensagem.Add(new XElement(Ans + "epilogo",
            new XElement(Ans + "hash", CalcularHash(mensagem))));

        // `XDocument.ToString()` NÃO escreve a declaração: ela vai à mão, e é ela que diz a
        // codificação a quem lê o arquivo.
        return "<?xml version=\"1.0\" encoding=\"ISO-8859-1\"?>" + Environment.NewLine + mensagem;
    }

    /// <summary>
    /// MD5 (hex minúsculo) da concatenação dos valores-texto da mensagem, na ordem do documento.
    /// É a MESMA conta do <see cref="TissValidador"/> — as duas usam este método.
    /// </summary>
    public static string CalcularHash(XElement mensagem)
    {
        var epilogo = mensagem.Element(Ans + "epilogo");
        var sb = new StringBuilder();
        foreach (var texto in mensagem.DescendantNodes().OfType<XText>())
            if (epilogo is null || !texto.Ancestors().Contains(epilogo))
                sb.Append(texto.Value);

        var bytes = MD5.HashData(Codificacao.GetBytes(sb.ToString()));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}
