using System.Globalization;
using System.Text.RegularExpressions;
using Clinica.Application.Abstracoes;
using Clinica.Domain;
using Clinica.Domain.Entities;
using Clinica.Domain.Regras;

namespace Clinica.Application.Servicos;

/// <summary>
/// Os campos da GUIA DE CONSULTA no padrão ANS, como o formulário de baixa os conferiu — os
/// mesmos da tela "Guia de Consulta" do portal da operadora. Os domínios vão pelo CÓDIGO do
/// padrão (<see cref="DominiosTiss"/>): "06", "33", "225133".
/// </summary>
public sealed record DadosGuiaConsultaAns
{
    public string RegistroAns { get; init; } = string.Empty;
    public string NumeroCarteira { get; init; } = string.Empty;
    public DateOnly? ValidadeCarteira { get; init; }
    public bool AtendimentoRn { get; init; }
    public string? NomeSocial { get; init; }
    public string NomeBeneficiario { get; init; } = string.Empty;
    public string CodigoPrestadorNaOperadora { get; init; } = string.Empty;
    public string? NomeContratado { get; init; }
    public string Cnes { get; init; } = string.Empty;
    public string NomeProfissional { get; init; } = string.Empty;
    public string ConselhoProfissional { get; init; } = string.Empty;
    public string NumeroConselho { get; init; } = string.Empty;
    public string UfConselho { get; init; } = string.Empty;
    public string Cbos { get; init; } = string.Empty;
    public string IndicacaoAcidente { get; init; } = string.Empty;
    public string? CoberturaEspecial { get; init; }
    public string RegimeAtendimento { get; init; } = string.Empty;
    public string? SaudeOcupacional { get; init; }
    public DateOnly DataAtendimento { get; init; }
    public string CodigoTabela { get; init; } = string.Empty;
    public string TipoConsulta { get; init; } = string.Empty;
    public string CodigoProcedimento { get; init; } = string.Empty;
    public decimal? ValorProcedimento { get; init; }
    public string? Observacao { get; init; }
}

/// <summary>
/// O que o formulário de baixa recebe ao abrir: se esta guia pede a guia ANS e, pedindo, os
/// campos já preenchidos com o que o sistema sabe — e a lista do que ele NÃO soube preencher.
/// </summary>
/// <param name="Exige">Falso = baixa comum, como sempre foi.</param>
/// <param name="Dados">Os campos propostos; nulo quando não exige.</param>
/// <param name="Avisos">De onde veio (ou por que faltou) o que merece conferência.</param>
public sealed record PropostaGuiaAns(bool Exige, DadosGuiaConsultaAns? Dados, IReadOnlyList<string> Avisos)
{
    public static readonly PropostaGuiaAns NaoExige = new(false, null, []);
}

/// <summary>
/// A regra da guia ANS na baixa (nov/2026, pedido da direção): TODA baixa de guia de consulta da
/// Saúde Petrobras passa pela conferência dos campos que a operadora pede. Aqui mora o que decide
/// — quem exige, o que é válido, como se grava —, puro e com teste; a tela só desenha.
/// </summary>
public static class GuiaAnsNaBaixa
{
    // Os padrões da guia de consulta da Saúde Petrobras, como a clínica a preenche no portal.
    public const string AcidentePadrao = "9";       // Não acidentes
    public const string RegimePadrao = "01";        // Ambulatorial
    public const string TabelaPadrao = "22";        // TUSS procedimentos
    public const string CnesDeQuemNaoTem = "9999999";
    public const string TipoConsultaPrimeira = "1";
    public const string TipoConsultaSeguimento = "2";

    /// <summary>
    /// A recusa das portas que dão baixa SEM formulário por guia (lote e rodada). Diz onde resolver.
    /// </summary>
    public const string RecusaSemGuia =
        "Guia de consulta da Saúde Petrobras: a baixa pede a conferência da guia ANS. "
        + "Use o botão \"Dar baixa\" desta guia no painel de pendências.";

    /// <summary>
    /// Esta guia pede a guia ANS na baixa? Hoje: consulta (comum ou de especialidade) pelo convênio,
    /// de paciente cujo convênio é da família Petrobras. Sessão particular não pede — não vai à
    /// operadora. Procedimento (BSV) também não: é guia SP/SADT, outro formulário, ainda não feito.
    /// ⚠️ O convênio é o ATUAL da ficha: a guia não guarda o convênio do dia do atendimento.
    /// </summary>
    public static bool Exige(CodigoFaturamento codigo)
    {
        var paciente = codigo.Atendimento?.Paciente;
        if (paciente is null || codigo.Atendimento!.Particular) return false;
        return Exige(codigo.Tipo, paciente.ConvenioCodigo, paciente.Convenio);
    }

    /// <summary>A mesma regra para quem só tem a linha da pendência (lote e rodada).</summary>
    public static bool Exige(TipoCodigo tipo, string? convenioCodigo, Convenio convenio)
        => TissExportService.EhConsulta(tipo)
           && CatalogoConvenios.Familia(convenioCodigo ?? convenio.ToString()) == Convenio.Petrobras;

    /// <summary>
    /// O que impede gravar a guia: campo → frase. Vazio = pode. São as exigências do XSD 4.01.00
    /// da guia de consulta (obrigatoriedade, tamanho e domínio); o que o padrão não exige, não
    /// bloqueia.
    /// </summary>
    public static IReadOnlyDictionary<string, string> Validar(DadosGuiaConsultaAns d)
    {
        var erros = new Dictionary<string, string>();

        if (d.RegistroAns.Length != 6 || !d.RegistroAns.All(char.IsAsciiDigit))
            erros[nameof(d.RegistroAns)] = "O registro ANS da operadora tem 6 dígitos. Confira no cadastro do convênio.";
        Texto(erros, nameof(d.NumeroCarteira), d.NumeroCarteira, 20, "Informe o número da carteira");
        Texto(erros, nameof(d.CodigoPrestadorNaOperadora), d.CodigoPrestadorNaOperadora, 14, "Informe o código do contratado na operadora");
        Texto(erros, nameof(d.Cnes), d.Cnes, 7, "Informe o CNES (9999999 para quem não tem)");
        Texto(erros, nameof(d.NomeProfissional), d.NomeProfissional, 70, "Informe o profissional executante");
        Dominio(erros, nameof(d.ConselhoProfissional), d.ConselhoProfissional, DominiosTiss.ConselhoProfissional, "Escolha o conselho profissional.");
        Texto(erros, nameof(d.NumeroConselho), d.NumeroConselho, 15, "Informe o número no conselho");
        Dominio(erros, nameof(d.UfConselho), d.UfConselho, DominiosTiss.Uf, "Escolha a UF do conselho.");
        Dominio(erros, nameof(d.Cbos), d.Cbos, DominiosTiss.Cbos, "Escolha o CBO do profissional.");
        Dominio(erros, nameof(d.IndicacaoAcidente), d.IndicacaoAcidente, DominiosTiss.IndicacaoAcidente, "Escolha a indicação de acidente.");
        Dominio(erros, nameof(d.RegimeAtendimento), d.RegimeAtendimento, DominiosTiss.RegimeAtendimento, "Escolha o regime de atendimento.");
        Dominio(erros, nameof(d.TipoConsulta), d.TipoConsulta, DominiosTiss.TipoConsulta, "Escolha o tipo de consulta.");
        Dominio(erros, nameof(d.CodigoTabela), d.CodigoTabela, DominiosTiss.Tabela, "Escolha a tabela do procedimento.");
        if (!string.IsNullOrEmpty(d.CoberturaEspecial) && !DominiosTiss.Contem(DominiosTiss.CoberturaEspecial, d.CoberturaEspecial))
            erros[nameof(d.CoberturaEspecial)] = "Cobertura especial fora da tabela do padrão.";
        if (!string.IsNullOrEmpty(d.SaudeOcupacional) && !DominiosTiss.Contem(DominiosTiss.SaudeOcupacional, d.SaudeOcupacional))
            erros[nameof(d.SaudeOcupacional)] = "Saúde ocupacional fora da tabela do padrão.";
        if (d.DataAtendimento == default)
            erros[nameof(d.DataAtendimento)] = "Informe a data do atendimento.";
        else if (d.DataAtendimento > DateOnly.FromDateTime(DateTime.Today))
            erros[nameof(d.DataAtendimento)] = "A data do atendimento não pode ser futura.";
        Texto(erros, nameof(d.CodigoProcedimento), d.CodigoProcedimento, 10, "Informe o código do procedimento");
        if (d.ValorProcedimento is not { } valor)
            erros[nameof(d.ValorProcedimento)] = "Informe o valor do procedimento.";
        else if (valor <= 0)
            erros[nameof(d.ValorProcedimento)] = "O valor do procedimento precisa ser maior que zero.";
        else if (valor > 999999.99m || decimal.Round(valor, 2) != valor)
            erros[nameof(d.ValorProcedimento)] = "O valor vai até 999.999,99, com dois decimais.";
        if (d.Observacao is { Length: > 500 })
            erros[nameof(d.Observacao)] = "A observação tem até 500 caracteres.";
        if (d.NomeSocial is { Length: > 70 })
            erros[nameof(d.NomeSocial)] = "O nome social tem até 70 caracteres.";

        return erros;
    }

    /// <summary>
    /// Grava os campos conferidos na guia do sistema: cria a guia ANS ou SUBSTITUI a que existia
    /// (baixa refeita depois de um estorno). Quem chama já validou.
    /// </summary>
    public static void Aplicar(CodigoFaturamento codigo, DadosGuiaConsultaAns d, string? operador, DateTime agora)
    {
        var guia = codigo.GuiaAns ??= new GuiaTissConsulta { CodigoId = codigo.Id };
        guia.RegistroAns = d.RegistroAns.Trim();
        guia.NumeroCarteira = d.NumeroCarteira.Trim();
        guia.ValidadeCarteira = d.ValidadeCarteira;
        guia.AtendimentoRn = d.AtendimentoRn;
        guia.NomeSocial = Limpo(d.NomeSocial);
        guia.NomeBeneficiario = Cortar(d.NomeBeneficiario.Trim(), 200);
        guia.CodigoPrestadorNaOperadora = d.CodigoPrestadorNaOperadora.Trim();
        guia.NomeContratado = Limpo(d.NomeContratado) is { } nome ? Cortar(nome, 200) : null;
        guia.Cnes = d.Cnes.Trim();
        guia.NomeProfissional = d.NomeProfissional.Trim();
        guia.ConselhoProfissional = d.ConselhoProfissional;
        guia.NumeroConselho = d.NumeroConselho.Trim();
        guia.UfConselho = d.UfConselho;
        guia.Cbos = d.Cbos;
        guia.IndicacaoAcidente = d.IndicacaoAcidente;
        guia.CoberturaEspecial = Limpo(d.CoberturaEspecial);
        guia.RegimeAtendimento = d.RegimeAtendimento;
        guia.SaudeOcupacional = Limpo(d.SaudeOcupacional);
        guia.DataAtendimento = d.DataAtendimento;
        guia.CodigoTabela = d.CodigoTabela;
        guia.TipoConsulta = d.TipoConsulta;
        guia.CodigoProcedimento = d.CodigoProcedimento.Trim();
        guia.ValorProcedimento = d.ValorProcedimento!.Value;
        guia.Observacao = Limpo(d.Observacao);
        guia.ConferidaPor = string.IsNullOrWhiteSpace(operador) ? "?" : Cortar(operador.Trim(), 80);
        guia.ConferidaEm = agora;
    }

    /// <summary>
    /// Lê o registro profissional escrito em texto livre no cadastro da equipe ("CRM-RJ 123456",
    /// "CRM/RJ 52.12345-0", "CREFITO 2 12345-F") e devolve o que conseguiu reconhecer, nos códigos
    /// do padrão. É PROPOSTA para a pessoa conferir: cada parte não reconhecida volta nula.
    /// </summary>
    public static (string? Conselho, string? Numero, string? Uf) LerRegistroConselho(string? texto)
    {
        if (string.IsNullOrWhiteSpace(texto)) return (null, null, null);

        var limpo = texto.Trim().ToUpperInvariant();
        string? conselho = null, uf = null;
        foreach (var palavra in Regex.Split(limpo, @"[^A-ZÀ-Ú]+").Where(p => p.Length > 0))
        {
            conselho ??= DominiosTiss.ConselhoPelaSigla(palavra);
            // "EX" é sigla de UF no padrão, mas num registro profissional não aparece: fica de fora.
            if (palavra.Length == 2 && palavra != "EX") uf ??= DominiosTiss.UfPelaSigla(palavra);
        }

        // O número é o que sobra depois das letras: dígitos, com ponto, traço ou letra final.
        var numero = Regex.Match(limpo, @"\d[\d.\-/]*\d(?:-?[A-Z])?|\d").Value;
        numero = Regex.Replace(numero, @"[.\-/]", string.Empty);
        return (conselho, numero is { Length: > 0 and <= 15 } ? numero : null, uf);
    }

    /// <summary>O valor como o XML do padrão o quer: ponto decimal e duas casas ("139.00").</summary>
    public static string ValorNoXml(decimal valor) => valor.ToString("0.00", CultureInfo.InvariantCulture);

    private static void Texto(Dictionary<string, string> erros, string campo, string? valor, int maximo, string frase)
    {
        if (string.IsNullOrWhiteSpace(valor)) erros[campo] = frase + ".";
        else if (valor.Trim().Length > maximo) erros[campo] = $"{frase} com até {maximo} caracteres.";
    }

    private static void Dominio(Dictionary<string, string> erros, string campo, string? valor,
        IReadOnlyList<ValorTiss> dominio, string frase)
    {
        if (!DominiosTiss.Contem(dominio, valor)) erros[campo] = frase;
    }

    private static string? Limpo(string? texto) => string.IsNullOrWhiteSpace(texto) ? null : texto.Trim();

    private static string Cortar(string texto, int maximo) => texto.Length <= maximo ? texto : texto[..maximo];
}

/// <summary>
/// Monta a PROPOSTA da guia ANS para o formulário de baixa: lê o que o sistema sabe (ficha,
/// atendimento, horário, equipe, dados do prestador, tabela de preços, guias anteriores) e
/// devolve os campos preenchidos. Só leitura — quem grava é o <see cref="FaturamentoService"/>.
/// </summary>
public sealed class GuiaAnsNaBaixaService(IClinicaRepositorio repo, ParametrosService parametros, PrecoConvenioService precos)
{
    public async Task<PropostaGuiaAns> PrepararAsync(int codigoId, CancellationToken ct = default)
    {
        var codigo = await repo.ObterCodigoAsync(codigoId, ct);
        if (codigo is null || !GuiaAnsNaBaixa.Exige(codigo)) return PropostaGuiaAns.NaoExige;

        var atendimento = codigo.Atendimento!;
        var paciente = atendimento.Paciente!;
        var convenioCodigo = paciente.ConvenioCodigo ?? paciente.Convenio.ToString();
        var avisos = new List<string>();

        // Leituras em FILA: é o mesmo DbContext, e ele não aceita duas operações ao mesmo tempo.
        var prestador = await parametros.ObterPrestadorAsync(ct);
        var horario = await repo.AgendamentoDoAtendimentoAsync(atendimento.Id, ct);
        var profissional = horario?.ProfissionalId is { } id ? await repo.ObterProfissionalAsync(id, ct) : null;
        var preco = await precos.ResolverAsync(convenioCodigo, codigo.Tipo, atendimento.Data, codigo.Especialidade, ct);

        // Baixa refeita (depois de um estorno): parte do que já tinha sido conferido.
        if (codigo.GuiaAns is { } anterior)
        {
            avisos.Add($"Esta guia já tinha sido conferida por {anterior.ConferidaPor} em {anterior.ConferidaEm:dd/MM/yyyy}. Confira de novo antes de confirmar.");
            return new PropostaGuiaAns(true, DaGuia(anterior), avisos);
        }

        // ---- profissional executante ----
        var nomeProfissional = profissional?.Nome?.Trim() ?? string.Empty;
        string? conselho = null, numeroConselho = null, uf = null, cbos = null;
        if (profissional is null)
            avisos.Add("O sistema não achou o profissional deste atendimento (horário sem profissional): informe o executante.");
        else
        {
            // 1º a última guia conferida deste profissional — é o que alguém já conferiu contra o portal.
            var ultima = await repo.UltimaGuiaAnsDoProfissionalAsync(nomeProfissional, ct);
            if (ultima is not null)
            {
                (conselho, numeroConselho, uf, cbos) = (ultima.ConselhoProfissional, ultima.NumeroConselho, ultima.UfConselho, ultima.Cbos);
            }
            else
            {
                // 2º o registro em texto livre do cadastro da equipe; o CBO não existe no cadastro.
                (conselho, numeroConselho, uf) = GuiaAnsNaBaixa.LerRegistroConselho(profissional.RegistroConselho);
                avisos.Add("Primeira guia deste profissional: confira conselho, número e UF e escolha o CBO. Nas próximas, eles já vêm preenchidos.");
            }
        }

        // ---- tipo de consulta: proposta pelo histórico, decisão de quem confere ----
        var inicioDoDia = atendimento.Data.ToDateTime(TimeOnly.MinValue);
        var anteriores = await repo.ConsultasPeloConvenioDoPacienteAsync(paciente.Id, inicioDoDia.AddYears(-5), inicioDoDia, ct);
        var consultaAnterior = anteriores.Where(c => c.DataHora < inicioDoDia).OrderByDescending(c => c.DataHora).FirstOrDefault();
        if (consultaAnterior is not null)
            avisos.Add($"Tipo de consulta proposto como seguimento: há consulta pelo convênio em {consultaAnterior.DataHora:dd/MM/yyyy}.");

        // ---- contratado ----
        var cnpj = new string((prestador.Cnpj ?? string.Empty).Where(char.IsAsciiDigit).ToArray());
        if (cnpj.Length != 14)
            avisos.Add("O CNPJ da clínica não está em Configurações do faturamento: informe o código do contratado na operadora.");
        var registroAns = CatalogoConvenios.RegistroAns(convenioCodigo) ?? prestador.RegistroAnsOperadora ?? string.Empty;
        if (registroAns.Length == 0)
            avisos.Add("O registro ANS da operadora não está no cadastro do convênio.");

        // ---- procedimento e valor ----
        var tuss = prestador.CodigoTuss(codigo.Tipo);
        if (tuss.Length == 0)
            avisos.Add("O código do procedimento desta consulta não está em Configurações do faturamento: informe-o.");
        if (preco is null)
            avisos.Add("Não há preço cadastrado para esta consulta neste convênio: informe o valor da guia.");
        if (string.IsNullOrWhiteSpace(paciente.Carteirinha))
            avisos.Add("A ficha do paciente está sem o número da carteira.");

        return new PropostaGuiaAns(true, new DadosGuiaConsultaAns
        {
            RegistroAns = registroAns,
            NumeroCarteira = paciente.Carteirinha?.Trim() ?? string.Empty,
            ValidadeCarteira = paciente.ValidadeCarteirinha,
            AtendimentoRn = false,
            NomeBeneficiario = paciente.Nome,
            // Na Saúde Petrobras o código do contratado na operadora é o CNPJ, só com dígitos.
            CodigoPrestadorNaOperadora = cnpj.Length == 14 ? cnpj : string.Empty,
            NomeContratado = prestador.RazaoSocial ?? prestador.NomeFantasia,
            Cnes = string.IsNullOrWhiteSpace(prestador.Cnes) ? GuiaAnsNaBaixa.CnesDeQuemNaoTem : prestador.Cnes.Trim(),
            NomeProfissional = nomeProfissional.Length > 70 ? nomeProfissional[..70] : nomeProfissional,
            ConselhoProfissional = conselho ?? string.Empty,
            NumeroConselho = numeroConselho ?? string.Empty,
            UfConselho = uf ?? string.Empty,
            Cbos = cbos ?? string.Empty,
            IndicacaoAcidente = GuiaAnsNaBaixa.AcidentePadrao,
            RegimeAtendimento = GuiaAnsNaBaixa.RegimePadrao,
            DataAtendimento = atendimento.Data,
            CodigoTabela = GuiaAnsNaBaixa.TabelaPadrao,
            TipoConsulta = consultaAnterior is null ? GuiaAnsNaBaixa.TipoConsultaPrimeira : GuiaAnsNaBaixa.TipoConsultaSeguimento,
            CodigoProcedimento = tuss,
            ValorProcedimento = preco?.Valor
        }, avisos);
    }

    private static DadosGuiaConsultaAns DaGuia(GuiaTissConsulta g) => new()
    {
        RegistroAns = g.RegistroAns,
        NumeroCarteira = g.NumeroCarteira,
        ValidadeCarteira = g.ValidadeCarteira,
        AtendimentoRn = g.AtendimentoRn,
        NomeSocial = g.NomeSocial,
        NomeBeneficiario = g.NomeBeneficiario,
        CodigoPrestadorNaOperadora = g.CodigoPrestadorNaOperadora,
        NomeContratado = g.NomeContratado,
        Cnes = g.Cnes,
        NomeProfissional = g.NomeProfissional,
        ConselhoProfissional = g.ConselhoProfissional,
        NumeroConselho = g.NumeroConselho,
        UfConselho = g.UfConselho,
        Cbos = g.Cbos,
        IndicacaoAcidente = g.IndicacaoAcidente,
        CoberturaEspecial = g.CoberturaEspecial,
        RegimeAtendimento = g.RegimeAtendimento,
        SaudeOcupacional = g.SaudeOcupacional,
        DataAtendimento = g.DataAtendimento,
        CodigoTabela = g.CodigoTabela,
        TipoConsulta = g.TipoConsulta,
        CodigoProcedimento = g.CodigoProcedimento,
        ValorProcedimento = g.ValorProcedimento,
        Observacao = g.Observacao
    };
}

/// <summary>
/// A guia ANS ENQUANTO a pessoa a confere no formulário: os mesmos campos, mutáveis, com o valor
/// ainda em texto (como foi digitado). A tela só liga os controles a este objeto; o que é válido
/// e como o valor se lê ficam aqui, com teste — Razor não é lugar de regra.
/// </summary>
public sealed class GuiaAnsEmEdicao
{
    private static readonly CultureInfo PtBr = CultureInfo.GetCultureInfo("pt-BR");

    public string RegistroAns { get; set; } = string.Empty;
    public string NumeroCarteira { get; set; } = string.Empty;
    public DateOnly? ValidadeCarteira { get; private init; }
    public bool AtendimentoRn { get; set; }
    public string? NomeSocial { get; set; }
    public string NomeBeneficiario { get; private init; } = string.Empty;
    public string CodigoPrestadorNaOperadora { get; set; } = string.Empty;
    public string? NomeContratado { get; private init; }
    public string Cnes { get; set; } = string.Empty;
    public string NomeProfissional { get; set; } = string.Empty;
    public string ConselhoProfissional { get; set; } = string.Empty;
    public string NumeroConselho { get; set; } = string.Empty;
    public string UfConselho { get; set; } = string.Empty;
    public string Cbos { get; set; } = string.Empty;
    public string IndicacaoAcidente { get; set; } = string.Empty;
    public string? CoberturaEspecial { get; set; }
    public string RegimeAtendimento { get; set; } = string.Empty;
    public string? SaudeOcupacional { get; set; }
    public DateOnly DataAtendimento { get; set; }
    public string CodigoTabela { get; set; } = string.Empty;
    public string TipoConsulta { get; set; } = string.Empty;
    public string CodigoProcedimento { get; set; } = string.Empty;

    /// <summary>O valor como está no campo: "139,00".</summary>
    public string ValorTexto { get; set; } = string.Empty;

    public string? Observacao { get; set; }

    public static GuiaAnsEmEdicao De(DadosGuiaConsultaAns d) => new()
    {
        RegistroAns = d.RegistroAns,
        NumeroCarteira = d.NumeroCarteira,
        ValidadeCarteira = d.ValidadeCarteira,
        AtendimentoRn = d.AtendimentoRn,
        NomeSocial = d.NomeSocial,
        NomeBeneficiario = d.NomeBeneficiario,
        CodigoPrestadorNaOperadora = d.CodigoPrestadorNaOperadora,
        NomeContratado = d.NomeContratado,
        Cnes = d.Cnes,
        NomeProfissional = d.NomeProfissional,
        ConselhoProfissional = d.ConselhoProfissional,
        NumeroConselho = d.NumeroConselho,
        UfConselho = d.UfConselho,
        Cbos = d.Cbos,
        IndicacaoAcidente = d.IndicacaoAcidente,
        CoberturaEspecial = d.CoberturaEspecial,
        RegimeAtendimento = d.RegimeAtendimento,
        SaudeOcupacional = d.SaudeOcupacional,
        DataAtendimento = d.DataAtendimento,
        CodigoTabela = d.CodigoTabela,
        TipoConsulta = d.TipoConsulta,
        CodigoProcedimento = d.CodigoProcedimento,
        ValorTexto = d.ValorProcedimento is { } v ? v.ToString("N2", PtBr) : string.Empty,
        Observacao = d.Observacao
    };

    /// <summary>Os campos como estão agora; o valor ilegível vai nulo (e a validação o acusa).</summary>
    public DadosGuiaConsultaAns ParaDados() => new()
    {
        RegistroAns = RegistroAns.Trim(),
        NumeroCarteira = NumeroCarteira.Trim(),
        ValidadeCarteira = ValidadeCarteira,
        AtendimentoRn = AtendimentoRn,
        NomeSocial = NomeSocial,
        NomeBeneficiario = NomeBeneficiario,
        CodigoPrestadorNaOperadora = CodigoPrestadorNaOperadora.Trim(),
        NomeContratado = NomeContratado,
        Cnes = Cnes.Trim(),
        NomeProfissional = NomeProfissional.Trim(),
        ConselhoProfissional = ConselhoProfissional,
        NumeroConselho = NumeroConselho.Trim(),
        UfConselho = UfConselho,
        Cbos = Cbos,
        IndicacaoAcidente = IndicacaoAcidente,
        CoberturaEspecial = CoberturaEspecial,
        RegimeAtendimento = RegimeAtendimento,
        SaudeOcupacional = SaudeOcupacional,
        DataAtendimento = DataAtendimento,
        CodigoTabela = CodigoTabela,
        TipoConsulta = TipoConsulta,
        CodigoProcedimento = CodigoProcedimento.Trim(),
        ValorProcedimento = LerValor(ValorTexto),
        Observacao = Observacao
    };

    /// <summary>O que falta ou está errado AGORA, campo → frase. Vazio = a guia pode ser gravada.</summary>
    public IReadOnlyDictionary<string, string> Erros()
    {
        var erros = new Dictionary<string, string>(GuiaAnsNaBaixa.Validar(ParaDados()));
        if (!string.IsNullOrWhiteSpace(ValorTexto) && LerValor(ValorTexto) is null)
            erros[nameof(DadosGuiaConsultaAns.ValorProcedimento)] = "Não entendi este valor. Digite como 139,00.";
        return erros;
    }

    /// <summary>
    /// Lê o valor como a clínica digita: "139,00", "1.250,00", "R$ 139" — e também "139.00", que
    /// é como o portal da operadora o mostra. ⚠️ Em pt-BR o ponto é milhar: lido pela cultura,
    /// "139.00" viraria 13.900. Por isso ponto seguido de UMA ou DUAS casas, sem vírgula, é decimal.
    /// </summary>
    public static decimal? LerValor(string? texto)
    {
        if (string.IsNullOrWhiteSpace(texto)) return null;
        var limpo = texto.Replace("R$", string.Empty).Trim();
        if (limpo.Length == 0 || limpo.StartsWith('-')) return null;

        if (!limpo.Contains(',') && Regex.IsMatch(limpo, @"^\d+\.\d{1,2}$"))
            return decimal.Parse(limpo, CultureInfo.InvariantCulture);
        return decimal.TryParse(limpo, NumberStyles.AllowDecimalPoint | NumberStyles.AllowThousands, PtBr, out var valor)
            ? valor
            : null;
    }
}
