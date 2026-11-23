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

    // ---- só na guia SP/SADT (Amil, nov/2026) ----
    /// <summary>Em que guia a consulta vai à operadora (o catálogo decide; a pessoa não escolhe).</summary>
    public ModeloDaGuiaTiss Modelo { get; init; } = ModeloDaGuiaTiss.Consulta;
    public string? Senha { get; init; }
    public DateOnly? DataAutorizacao { get; init; }
    public DateOnly? ValidadeSenha { get; init; }
    public string? CodigoValidacao { get; init; }
    public string? AusenciaCodigoValidacao { get; init; }
    public string? TipoAtendimento { get; init; }
    public string? CaraterAtendimento { get; init; }
    public string? DescricaoProcedimento { get; init; }

    /// <summary>Como o beneficiário confirmou: <see cref="GuiaAnsNaBaixa.ConfirmacaoToken"/> e as outras.</summary>
    public string? FormaConfirmacao { get; init; }

    /// <summary>"Não confirmou": quem confere declara a guia assinada e o documento com foto anexados no portal.</summary>
    public bool AnexosNoPortal { get; init; }

    public TimeOnly? HoraInicial { get; init; }
    public TimeOnly? HoraFinal { get; init; }

    /// <summary>A operadora exige a equipe com o profissional executante (catálogo).</summary>
    public bool ExigeEquipe { get; init; }
    public string? EquipeCpf { get; init; }
    public string? GrauParticipacao { get; init; }

    // ---- a autorização pedida pelo sistema (nov/2026) ----
    /// <summary>A autorização do atendimento pedida pelo sistema: a senha, a data e a validade vêm dela, travadas.</summary>
    public int? AutorizacaoId { get; init; }
    /// <summary>"autorizado pela Amil em DD/MM": a operadora que autorizou (nulo = autorização feita fora do sistema).</summary>
    public string? AutorizadaPor { get; init; }
    /// <summary>O token (ou o motivo de não haver) foi colhido na RECEPÇÃO, com o paciente presente: vem travado.</summary>
    public bool ConfirmacaoDaRecepcao { get; init; }
}

/// <summary>
/// O que o formulário de baixa recebe ao abrir: se esta guia pede a guia ANS e, pedindo, os
/// campos já preenchidos com o que o sistema sabe — e a lista do que ele NÃO soube preencher.
/// </summary>
/// <param name="Exige">Falso = baixa comum, como sempre foi.</param>
/// <param name="Dados">Os campos propostos; nulo quando não exige.</param>
/// <param name="Avisos">De onde veio (ou por que faltou) o que merece conferência.</param>
/// <param name="AlertaDePrazo">
/// O prazo de cobrança GRAVE (vermelho): a guia já venceu, ou vence antes da próxima janela de envio
/// da operadora (<see cref="PrazosTiss.AvisoDaGuia"/>). Nulo = nada grave.
/// </param>
public sealed record PropostaGuiaAns(bool Exige, DadosGuiaConsultaAns? Dados, IReadOnlyList<string> Avisos, string? AlertaDePrazo = null)
{
    public static readonly PropostaGuiaAns NaoExige = new(false, null, []);
}

/// <summary>
/// A regra da guia ANS na baixa (nov/2026, pedido da direção): TODA baixa de guia de consulta da
/// Saúde Petrobras passa pela conferência dos campos que a operadora pede. Aqui mora o que decide
/// — quem exige, o que é válido, como se grava —, puro e com teste; a tela só desenha.
/// </summary>
public static partial class GuiaAnsNaBaixa
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
        "Guia de convênio com envio pelo web service: a baixa pede a conferência da guia ANS. "
        + "Use o botão \"Dar baixa\" desta guia no painel de pendências.";

    /// <summary>O token da Amil: "código único de 6 dígitos" (Manual do Credenciado, jul/2026).</summary>
    public const int DigitosDoToken = 6;

    // Como o beneficiário confirmou o atendimento (nov/2026, decisão do dono): a Amil é faturada SÓ pelo
    // sistema, com o TOKEN de 6 dígitos do app Amil Clientes na guia (codValidacao) — a recepção não
    // confirma no portal, porque a confirmação lá cria sozinha a guia no faturamento do portal (duplicaria).
    // O QR Code não gera código para digitar (o paciente o lê com a câmera do app): não tem lugar aqui.
    public const string ConfirmacaoToken = "token";
    public const string ConfirmacaoNenhuma = "nao";

    public static readonly IReadOnlyList<ValorTiss> FormasDeConfirmacao =
    [
        new(ConfirmacaoToken, "Token (6 dígitos)"), new(ConfirmacaoNenhuma, "Não confirmou"),
    ];

    /// <summary>
    /// Os motivos de NÃO ter o token que fazem sentido num consultório, com o código do XSD 4.03.00
    /// (<c>dm_ausenciaCodValidacao</c>). O "outros" do portal não tem código no padrão: não é inventado.
    /// </summary>
    public static readonly IReadOnlyList<ValorTiss> MotivosSemToken =
    [
        new("07", "Paciente não tem celular"),
        new("03", "Instabilidade de sistema (regularizado depois do atendimento)"),
        new("04", "Paciente não quis informar o token"),
    ];

    /// <summary>A forma de confirmação da guia: a escolhida, ou (guias antigas) deduzida do token ou do motivo.</summary>
    public static string? FormaDe(DadosGuiaConsultaAns d)
        => !string.IsNullOrWhiteSpace(d.FormaConfirmacao) ? d.FormaConfirmacao
            : !string.IsNullOrWhiteSpace(d.CodigoValidacao) ? ConfirmacaoToken
            : !string.IsNullOrWhiteSpace(d.AusenciaCodigoValidacao) ? ConfirmacaoNenhuma
            : null;

    /// <summary>
    /// Esta guia pede a guia ANS na baixa? Hoje: TODA guia pelo convênio de paciente da família
    /// Petrobras — consulta, consulta de especialidade e também o BSV, que a clínica lança no
    /// portal da operadora na MESMA tela "Guia de Consulta", mudando só o procedimento (decisão da
    /// direção, 11/11/2026). Sessão particular não pede — não vai à operadora.
    /// ⚠️ O convênio é o ATUAL da ficha: a guia não guarda o convênio do dia do atendimento.
    /// </summary>
    public static bool Exige(CodigoFaturamento codigo)
    {
        var paciente = codigo.Atendimento?.Paciente;
        if (paciente is null || codigo.Atendimento!.Particular) return false;
        return Exige(codigo.Tipo, paciente.ConvenioCodigo, paciente.Convenio);
    }

    /// <summary>
    /// A mesma regra para quem só tem a linha da pendência (lote e rodada). O tipo da guia fica na
    /// assinatura porque é ele que decidirá quando outro convênio pedir a guia só em parte.
    /// Quem decide é o catálogo de operadoras (nov/2026): pede a conferência a operadora com envio de
    /// guias nas telas — hoje só a Saúde Petrobras. A Amil está pronta e desligada (OperadorasTiss.Amil).
    /// </summary>
    public static bool Exige(TipoCodigo tipo, string? convenioCodigo, Convenio convenio)
        => Exige(OperadorasTiss.DoConvenio(convenioCodigo, convenio));

    /// <summary>A operadora pede a guia ANS na baixa? Só com o envio de guias ligado nas telas.</summary>
    public static bool Exige(OperadoraTiss? operadora) => operadora is { EnvioDeGuiaNasTelas: true };

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
        // Na SP/SADT o tipo de consulta só existe quando o atendimento é consulta (glosa 954 da Amil).
        if (d.Modelo == ModeloDaGuiaTiss.Consulta || d.TipoAtendimento == DominiosTiss.TipoAtendimentoConsulta)
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
        if (d.Modelo == ModeloDaGuiaTiss.SpSadt)
            ValidarSpSadt(erros, d);

        return erros;
    }

    /// <summary>
    /// O que a guia SP/SADT pede além da de consulta — XSD 4.03.00 (<c>ctm_sp-sadtGuia</c>) e as
    /// GLOSAS DE BLOQUEIO da prévia da Amil ("Lista de glosas – Prévia – Bloqueio e Alerta", jan/2023)
    /// que dá para pegar antes do envio. Cada frase diz o que corrigir e qual glosa evita.
    /// </summary>
    private static void ValidarSpSadt(Dictionary<string, string> erros, DadosGuiaConsultaAns d)
    {
        // 1403/894 "Senha/Token não informado" e 1403/862 "Senha inexistente": bloqueio.
        if (string.IsNullOrWhiteSpace(d.Senha))
            erros[nameof(d.Senha)] = "Informe a senha de autorização. Sem ela a guia é bloqueada (glosa 1403).";
        else if (d.Senha.Trim().Length > 20)
            erros[nameof(d.Senha)] = "A senha de autorização tem até 20 caracteres.";

        // A data da autorização é obrigatória no bloco de autorização do XSD; posterior ao
        // atendimento é bloqueio na Amil (1814/643).
        if (d.DataAutorizacao is not { } autorizada)
            erros[nameof(d.DataAutorizacao)] = "Informe a data da autorização (está na senha, no portal).";
        else if (d.DataAtendimento != default && autorizada > d.DataAtendimento)
            erros[nameof(d.DataAutorizacao)] = "A autorização não pode ser posterior ao atendimento: a guia é bloqueada (glosa 1814).";

        // A CONFIRMAÇÃO do atendimento (3096/893: sem ela, bloqueio). O token vai em codValidacao; "não
        // confirmou" vai em ausenciaCodValidacao, com os anexos no portal (991: bloqueio).
        var token = d.CodigoValidacao?.Trim();
        switch (FormaDe(d))
        {
            case ConfirmacaoToken:
                if (string.IsNullOrEmpty(token))
                    erros[nameof(d.CodigoValidacao)] = $"Informe o token do beneficiário ({DigitosDoToken} dígitos). Sem confirmação a guia é bloqueada (glosa 3096).";
                else if (token.Length != DigitosDoToken || !token.All(char.IsAsciiDigit))
                    erros[nameof(d.CodigoValidacao)] = $"O token tem {DigitosDoToken} dígitos. Token inválido bloqueia a guia (glosa 1406).";
                break;
            case ConfirmacaoNenhuma:
                if (!DominiosTiss.Contem(MotivosSemToken, d.AusenciaCodigoValidacao))
                    erros[nameof(d.AusenciaCodigoValidacao)] = "Escolha o motivo de o paciente não ter confirmado.";
                if (!d.AnexosNoPortal)
                    erros[nameof(d.AnexosNoPortal)] = "Sem confirmação, a Amil bloqueia a guia se a guia assinada e o documento com foto não estiverem anexados no portal no dia (glosa 991). Anexe e marque.";
                break;
            default:
                erros[nameof(d.FormaConfirmacao)] = "Informe o token que o paciente mostrou no app Amil Clientes, ou que ele não confirmou. Sem confirmação a guia é bloqueada (glosa 3096).";
                break;
        }

        // A EQUIPE: o profissional que executou, pelo CPF — é por ele que a Amil o identifica (1203/992: bloqueio).
        if (d.ExigeEquipe)
        {
            if (!Cpf.Valido(Cpf.Normalizar(d.EquipeCpf)))
                erros[nameof(d.EquipeCpf)] = $"Cadastre o CPF de {(string.IsNullOrWhiteSpace(d.NomeProfissional) ? "o profissional" : d.NomeProfissional.Trim())} "
                    + "(cadastro da equipe): a operadora identifica o profissional executante pelo CPF, e sem ele a guia é bloqueada (glosas 1203 e 992).";
            if (!string.IsNullOrEmpty(d.GrauParticipacao) && !DominiosTiss.Contem(DominiosTiss.GrauParticipacao, d.GrauParticipacao))
                erros[nameof(d.GrauParticipacao)] = "Escolha o grau de participação na lista do padrão.";
        }
        if (d.HoraInicial is { } inicio && d.HoraFinal is { } fim && fim < inicio)
            erros[nameof(d.HoraFinal)] = "A hora final não pode ser antes da inicial.";

        Dominio(erros, nameof(d.TipoAtendimento), d.TipoAtendimento, DominiosTiss.TipoAtendimento, "Escolha o tipo de atendimento.");
        // 5031/955: caráter divergente da autorização é bloqueio; aqui só o domínio — a autorização está no portal.
        Dominio(erros, nameof(d.CaraterAtendimento), d.CaraterAtendimento, DominiosTiss.CaraterAtendimento, "Escolha o caráter do atendimento.");
        Texto(erros, nameof(d.DescricaoProcedimento), d.DescricaoProcedimento, 150, "Informe a descrição do procedimento");
        // O nome do contratado solicitante é obrigatório na SP/SADT (st_texto70).
        Texto(erros, nameof(d.NomeContratado), d.NomeContratado, 70, "Informe o nome do contratado (razão social da clínica)");
    }

    /// <summary>
    /// O que NÃO impede gravar, mas a operadora pode glosar depois (glosas de ALERTA da Amil) ou
    /// pede algo fora do XML. Frases curtas, para ler antes de confirmar.
    /// </summary>
    public static IReadOnlyList<string> Alertas(DadosGuiaConsultaAns d)
    {
        var alertas = new List<string>();
        if (d.Modelo != ModeloDaGuiaTiss.SpSadt) return alertas;
        if (d.ValidadeSenha is { } validade && d.DataAtendimento != default && validade < d.DataAtendimento)
            alertas.Add($"A senha venceu em {validade:dd/MM/yyyy}, antes do atendimento: a operadora pode glosar (glosa 1405).");
        if (d.DataAutorizacao is { } autorizada && d.DataAtendimento != default && autorizada < d.DataAtendimento.AddDays(-60) && d.ValidadeSenha is null)
            alertas.Add("A senha eletiva vale 60 dias: confira a validade dela no portal.");
        return alertas;
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
        // SP/SADT: os campos dela; na guia de consulta, todos nulos (inclusive ao trocar de modelo).
        var sp = d.Modelo == ModeloDaGuiaTiss.SpSadt;
        guia.ModeloGuia = sp ? ModeloGuiaSpSadt : null;
        guia.Senha = sp ? Limpo(d.Senha) : null;
        guia.DataAutorizacao = sp ? d.DataAutorizacao : null;
        guia.ValidadeSenha = sp ? d.ValidadeSenha : null;
        guia.TipoAtendimento = sp ? d.TipoAtendimento : null;
        guia.CaraterAtendimento = sp ? d.CaraterAtendimento : null;
        guia.DescricaoProcedimento = sp ? Limpo(d.DescricaoProcedimento) : null;
        var forma = sp ? FormaDe(d) : null;
        guia.FormaConfirmacao = forma;
        // Token: só o token. Não confirmou: só o motivo (e a declaração dos anexos).
        guia.CodigoValidacao = forma == ConfirmacaoToken ? Limpo(d.CodigoValidacao) : null;
        guia.AusenciaCodigoValidacao = forma == ConfirmacaoNenhuma ? Limpo(d.AusenciaCodigoValidacao) : null;
        guia.AnexosNoPortal = forma == ConfirmacaoNenhuma ? d.AnexosNoPortal : null;
        guia.HoraInicial = sp ? d.HoraInicial : null;
        guia.HoraFinal = sp ? d.HoraFinal : null;
        guia.EquipeCpf = sp && Cpf.Valido(Cpf.Normalizar(d.EquipeCpf)) ? Cpf.Normalizar(d.EquipeCpf) : null;
        guia.GrauParticipacao = sp && guia.EquipeCpf is not null ? Limpo(d.GrauParticipacao) : null;
        // Na guia de consulta o tipo de consulta é obrigatório; na SP/SADT só existe na consulta.
        if (sp && d.TipoAtendimento != DominiosTiss.TipoAtendimentoConsulta) guia.TipoConsulta = string.Empty;
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

    /// <summary>O que vai em <see cref="GuiaTissConsulta.ModeloGuia"/> na guia SP/SADT.</summary>
    public const string ModeloGuiaSpSadt = "sp-sadt";

    /// <summary>O modelo de uma guia gravada (as antigas, sem a coluna preenchida, são de consulta).</summary>
    public static ModeloDaGuiaTiss ModeloDe(GuiaTissConsulta g)
        => g.ModeloGuia == ModeloGuiaSpSadt ? ModeloDaGuiaTiss.SpSadt : ModeloDaGuiaTiss.Consulta;

    /// <summary>A descrição proposta do procedimento na SP/SADT (a pessoa confere com a autorização).</summary>
    public static string DescricaoProposta(TipoCodigo tipo) => tipo switch
    {
        TipoCodigo.Consulta or TipoCodigo.ConsultaEspecialidade => "Consulta em consultório",
        TipoCodigo.Acupuntura => "Sessão de acupuntura",
        _ => tipo.ToString()
    };

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
/// atendimento, horário, cadastro da equipe, dados do prestador, tabela de preços, guias anteriores) e
/// devolve os campos preenchidos. Só leitura — quem grava é o <see cref="FaturamentoService"/>.
/// </summary>
/// <param name="relogio">A hora da clínica (padrão: <see cref="HoraDaClinica.Agora"/>). Os avisos de prazo e da
/// janela do mês dependem do dia; o teste fixa o relógio para não depender da data em que roda.</param>
public sealed class GuiaAnsNaBaixaService(IClinicaRepositorio repo, ParametrosService parametros, PrecoConvenioService precos,
    CredenciaisTissService credenciais, EnvioTissLigadoNaPrevia? previa = null, Func<DateTime>? relogio = null)
{
    public async Task<PropostaGuiaAns> PrepararAsync(int codigoId, CancellationToken ct = default)
    {
        var codigo = await repo.ObterCodigoAsync(codigoId, ct);
        var familia = codigo?.Atendimento?.Paciente is { } p ? CatalogoConvenios.Familia(p.ConvenioCodigo ?? p.Convenio.ToString()) : (Convenio?)null;
        var naPrevia = familia is { } f && codigo!.Atendimento!.Particular == false && previa?.Familias.Contains(f) == true;
        if (codigo is null || !(GuiaAnsNaBaixa.Exige(codigo) || naPrevia)) return PropostaGuiaAns.NaoExige;

        var atendimento = codigo.Atendimento!;
        var paciente = atendimento.Paciente!;
        var convenioCodigo = paciente.ConvenioCodigo ?? paciente.Convenio.ToString();
        var operadora = OperadorasTiss.DoConvenio(paciente.ConvenioCodigo, paciente.Convenio);
        var modelo = operadora?.ModeloDaGuia ?? ModeloDaGuiaTiss.Consulta;
        var agora = (relogio ?? HoraDaClinica.Agora)();
        var prazo = PrazosTiss.AvisoDaGuia(operadora, atendimento.Data, agora);
        var alertaDePrazo = prazo is { Grave: true } ? prazo.Value.Texto : null;
        // O grave vai à parte (vermelho); o comum e a janela do mês, junto dos outros avisos.
        var avisos = new List<string>(new[] { prazo is { Grave: false } ? prazo.Value.Texto : null, PrazosTiss.JanelaDeEnvio(operadora, agora) }.OfType<string>());

        // Leituras em FILA: é o mesmo DbContext, e ele não aceita duas operações ao mesmo tempo.
        var prestador = await parametros.ObterPrestadorAsync(ct);
        var horario = await repo.AgendamentoDoAtendimentoAsync(atendimento.Id, ct);
        var profissional = horario?.ProfissionalId is { } id ? await repo.ObterProfissionalAsync(id, ct) : null;
        var preco = await precos.ResolverAsync(convenioCodigo, codigo.Tipo, atendimento.Data, codigo.Especialidade, ct);

        // ---- consulta a menos do prazo da anterior (nov/2026): o que já entrou ----
        // A trava barra a MARCAÇÃO; o que passou antes dela (chave desligada, liberação da gerência,
        // a linha do convênio com "Não renova") a operadora ainda glosa. A mesma conta da trava,
        // sem a chave e sem a liberação: aqui é aviso para quem vai mandar a guia, não recusa.
        if (horario is { ModalidadePrevista: ModalidadeAtendimento.Consulta, Particular: false }
            && (horario.EspecialidadeConsultaCodigo ?? horario.EspecialidadeConsulta?.ToString()) is { Length: > 0 } especialidade
            && await ConsultaNoPrazoDoConvenio.ProcurarAsync(repo, await parametros.ObterAsync(ct), paciente, horario.DataHora,
                especialidade, horario.ProfissionalId, horario.Id, soAnteriores: true, ct) is { } noPrazo)
            avisos.Insert(0, ConsultaNoPrazoDoConvenio.AvisoDeGlosa(paciente.ConvenioNome, noPrazo));

        // Baixa refeita (depois de um estorno): parte do que já tinha sido conferido.
        if (codigo.GuiaAns is { } anterior)
        {
            avisos.Add($"Esta guia já tinha sido conferida por {anterior.ConferidaPor} em {anterior.ConferidaEm:dd/MM/yyyy}. Confira de novo antes de confirmar.");
            return new PropostaGuiaAns(true, DaGuia(anterior) with { ExigeEquipe = operadora?.EquipeComProfissional == true }, avisos, alertaDePrazo);
        }

        // ---- profissional executante ----
        var nomeProfissional = profissional?.Nome?.Trim() ?? string.Empty;
        string? conselho = null, numeroConselho = null, uf = null, cbos = null;
        if (profissional is null)
            avisos.Add("O sistema não achou o profissional deste atendimento (horário sem profissional): informe o executante.");
        else
        {
            // As fontes, campo a campo e nesta ordem (RegistroDoProfissionalParaGuias.ParaAGuia):
            // 1º o CADASTRO do profissional; 2º a última guia conferida dele; 3º o texto livre.
            // A última guia só é lida quando o cadastro não deu os quatro — é uma consulta a menos
            // em toda baixa de quem já está cadastrado, que passa a ser o caso comum.
            var ultima = RegistroDoProfissionalParaGuias.Do(profissional).Completo
                ? null
                : await repo.UltimaGuiaAnsDoProfissionalAsync(nomeProfissional, ct);
            var (proposto, avisoDoExecutante) = RegistroDoProfissionalParaGuias.ParaAGuia(profissional, ultima);
            (conselho, numeroConselho, uf, cbos) = (proposto.Conselho, proposto.Numero, proposto.Uf, proposto.Cbos);
            if (avisoDoExecutante is not null) avisos.Add(avisoDoExecutante);
        }

        // ---- tipo de consulta: proposta pelo histórico, decisão de quem confere ----
        var inicioDoDia = atendimento.Data.ToDateTime(TimeOnly.MinValue);
        var anteriores = await repo.ConsultasPeloConvenioDoPacienteAsync(paciente.Id, inicioDoDia.AddYears(-5), inicioDoDia, ct);
        var consultaAnterior = anteriores.Where(c => c.DataHora < inicioDoDia).OrderByDescending(c => c.DataHora).FirstOrDefault();
        if (consultaAnterior is not null)
            avisos.Add($"Tipo de consulta proposto como seguimento: há consulta pelo convênio em {consultaAnterior.DataHora:dd/MM/yyyy}.");

        // ---- contratado ----
        // O código na operadora segue a MESMA regra do conector (comando "codigo" ou o catálogo):
        // CNPJ na Saúde Petrobras, o login do web service na Amil.
        var cnpj = new string((prestador.Cnpj ?? string.Empty).Where(char.IsAsciiDigit).ToArray());
        var (regra, cadastrado) = await credenciais.CodigoDoPrestadorAsync(convenioCodigo, ct);
        var codigoNaOperadora = regra switch
        {
            CodigoDoPrestadorTiss.Cadastrado => cadastrado ?? string.Empty,
            CodigoDoPrestadorTiss.LoginDoWebService => (await credenciais.ParaATelaAsync(convenioCodigo, ct)).Login?.Trim() ?? string.Empty,
            _ => cnpj.Length == 14 ? cnpj : string.Empty
        };
        if (codigoNaOperadora.Length is 0 or > 14)
        {
            codigoNaOperadora = codigoNaOperadora.Length > 14 ? string.Empty : codigoNaOperadora;
            avisos.Add(regra == CodigoDoPrestadorTiss.CnpjDaClinica
                ? "O CNPJ da clínica não está em Configurações do faturamento: informe o código do contratado na operadora."
                : "O código do contratado na operadora não está cadastrado no servidor de envio: informe-o.");
        }
        // O registro ANS: o do cadastro do convênio, o da operadora no catálogo e só então o da clínica.
        var registroAns = CatalogoConvenios.RegistroAns(convenioCodigo) ?? operadora?.RegistroAns ?? prestador.RegistroAnsOperadora ?? string.Empty;
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
        var consulta = TissExportService.EhConsulta(codigo.Tipo);

        // ---- equipe (Amil): o profissional executante, pelo CPF do cadastro da equipe ----
        var exigeEquipe = modelo == ModeloDaGuiaTiss.SpSadt && operadora?.EquipeComProfissional == true;
        string? cpfDoProfissional = null;
        if (exigeEquipe && profissional is not null)
        {
            cpfDoProfissional = Cpf.Valido(Cpf.Normalizar(profissional.Cpf)) ? Cpf.Normalizar(profissional.Cpf) : null;
            if (cpfDoProfissional is null)
                avisos.Add($"Cadastre o CPF de {nomeProfissional} (cadastro da equipe): a {operadora!.Nome} identifica o profissional pelo CPF, e a guia não sai sem ele.");
        }

        // ---- horas (37/38): as do consultório (início e fim do atendimento); sem elas, as da agenda ----
        TimeOnly? horaInicial = null, horaFinal = null;
        if (modelo == ModeloDaGuiaTiss.SpSadt && horario is not null)
        {
            if (horario.InicioAtendimentoEm is { } inicio)
            {
                horaInicial = TimeOnly.FromDateTime(inicio);
                horaFinal = horario.FimAtendimentoEm is { } fim ? TimeOnly.FromDateTime(fim) : null;
                avisos.Add("Horas da guia: o início e o fim do atendimento registrados no consultório.");
            }
            else
            {
                horaInicial = TimeOnly.FromDateTime(horario.DataHora);
                horaFinal = TimeOnly.FromDateTime(horario.FimPrevisto);
                avisos.Add("Horas da guia: o horário marcado na agenda (o consultório não registrou início e fim). Confira.");
            }
        }
        // ---- a autorização pedida pelo sistema (nov/2026): senha, data, validade e o token da recepção ----
        AutorizacaoTiss? autorizacao = null;
        if (modelo == ModeloDaGuiaTiss.SpSadt && horario is not null)
            autorizacao = (await repo.AutorizacoesDoAgendamentoAsync(horario.Id, ct))
                .Where(a => a.Situacao == SituacaoAutorizacaoTiss.Autorizada && !string.IsNullOrWhiteSpace(a.Senha))
                .OrderBy(a => a.Id).LastOrDefault();
        if (modelo == ModeloDaGuiaTiss.SpSadt)
        {
            if (autorizacao is not null)
            {
                avisos.Add($"Autorizado pela {operadora!.Nome}"
                           + (autorizacao.DataAutorizacao is { } d ? $" em {d:dd/MM}" : "") + ": a senha, a data e a validade vêm do pedido do sistema.");
                if (!autorizacao.ConfirmacaoResolvida)
                    avisos.Add("A recepção não registrou o token do paciente: digite-o (a guia sem confirmação é bloqueada).");
            }
            else
                avisos.Add($"{operadora!.Nome}: autorização feita fora do sistema — informe a senha, a data e a validade, e o token que o beneficiário confirmou.");
        }

        return new PropostaGuiaAns(true, new DadosGuiaConsultaAns
        {
            RegistroAns = registroAns,
            NumeroCarteira = paciente.Carteirinha?.Trim() ?? string.Empty,
            ValidadeCarteira = paciente.ValidadeCarteirinha,
            AtendimentoRn = false,
            NomeBeneficiario = paciente.Nome,
            CodigoPrestadorNaOperadora = codigoNaOperadora,
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
            ValorProcedimento = preco?.Valor,
            Modelo = modelo,
            // SP/SADT: consulta é "04" (com o tipo de consulta); acupuntura e sessões, "03" outras terapias.
            TipoAtendimento = modelo == ModeloDaGuiaTiss.SpSadt
                ? (consulta ? DominiosTiss.TipoAtendimentoConsulta : "03") : null,
            CaraterAtendimento = modelo == ModeloDaGuiaTiss.SpSadt ? "1" : null,
            DescricaoProcedimento = modelo == ModeloDaGuiaTiss.SpSadt ? GuiaAnsNaBaixa.DescricaoProposta(codigo.Tipo) : null,
            FormaConfirmacao = modelo == ModeloDaGuiaTiss.SpSadt ? GuiaAnsNaBaixa.ConfirmacaoToken : null,
            HoraInicial = horaInicial,
            HoraFinal = horaFinal,
            ExigeEquipe = exigeEquipe,
            EquipeCpf = exigeEquipe ? cpfDoProfissional : null,
            GrauParticipacao = exigeEquipe ? DominiosTiss.GrauClinico : null,
            Senha = autorizacao?.Senha,
            DataAutorizacao = autorizacao?.DataAutorizacao,
            ValidadeSenha = autorizacao?.ValidadeSenha,
            CodigoValidacao = autorizacao?.Token,
            AusenciaCodigoValidacao = string.IsNullOrWhiteSpace(autorizacao?.Token) ? autorizacao?.AusenciaToken : null,
            AutorizacaoId = autorizacao?.Id,
            AutorizadaPor = autorizacao is null ? null : operadora!.Nome,
            ConfirmacaoDaRecepcao = autorizacao?.ConfirmacaoResolvida == true
        }, avisos, alertaDePrazo);
    }

    private static DadosGuiaConsultaAns DaGuia(GuiaTissConsulta g) => GuiaAnsNaBaixa.DaGuia(g);
}

/// <summary>
/// As operadoras cuja conferência na baixa aparece MESMO com o envio dela desligado no catálogo. Só a
/// prévia das telas (para fotografar a baixa da Amil antes de ela ser ligada) e os testes o registram;
/// o app nunca: sem registro, vale só <see cref="OperadoraTiss.EnvioDeGuiaNasTelas"/>.
/// </summary>
public sealed record EnvioTissLigadoNaPrevia(IReadOnlySet<Convenio> Familias);

/// <summary>A leitura da guia ANS já gravada, de volta para os campos do formulário.</summary>
public static partial class GuiaAnsNaBaixa
{
    /// <summary>
    /// Os campos conferidos de uma guia ANS já gravada. É o que a baixa de uma guia ENVIADA usa:
    /// a guia foi conferida antes do envio, e a baixa a reapresenta como está, sem ninguém redigitar.
    /// </summary>
    public static DadosGuiaConsultaAns DaGuia(GuiaTissConsulta g) => new()
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
        Observacao = g.Observacao,
        Modelo = ModeloDe(g),
        Senha = g.Senha,
        DataAutorizacao = g.DataAutorizacao,
        ValidadeSenha = g.ValidadeSenha,
        CodigoValidacao = g.CodigoValidacao,
        AusenciaCodigoValidacao = g.AusenciaCodigoValidacao,
        TipoAtendimento = g.TipoAtendimento,
        CaraterAtendimento = g.CaraterAtendimento,
        DescricaoProcedimento = g.DescricaoProcedimento,
        FormaConfirmacao = g.FormaConfirmacao,
        AnexosNoPortal = g.AnexosNoPortal == true,
        HoraInicial = g.HoraInicial,
        HoraFinal = g.HoraFinal,
        // A guia gravada com a equipe foi conferida com ela: relida, continua exigindo.
        ExigeEquipe = g.EquipeCpf is not null,
        EquipeCpf = g.EquipeCpf,
        GrauParticipacao = g.GrauParticipacao
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

    // ---- SP/SADT ----
    public ModeloDaGuiaTiss Modelo { get; private init; }
    public bool EhSpSadt => Modelo == ModeloDaGuiaTiss.SpSadt;
    public string? Senha { get; set; }
    public DateOnly? DataAutorizacao { get; set; }
    public DateOnly? ValidadeSenha { get; set; }
    public string? CodigoValidacao { get; set; }
    public string? AusenciaCodigoValidacao { get; set; }
    public string? TipoAtendimento { get; set; }
    public string? CaraterAtendimento { get; set; }
    public string? DescricaoProcedimento { get; set; }
    public string? FormaConfirmacao { get; set; }
    public bool AnexosNoPortal { get; set; }
    public TimeOnly? HoraInicial { get; set; }
    public TimeOnly? HoraFinal { get; set; }
    public bool ExigeEquipe { get; private init; }
    public string? EquipeCpf { get; set; }
    public string? GrauParticipacao { get; set; }
    public int? AutorizacaoId { get; private init; }
    public string? AutorizadaPor { get; private init; }
    public bool ConfirmacaoDaRecepcao { get; private init; }

    /// <summary>A senha, a data e a validade vieram da autorização pedida pelo sistema: o formulário as trava.</summary>
    public bool SenhaTravada => AutorizacaoId is not null;

    /// <summary>O token (ou o motivo) veio da recepção: travado, salvo a correção por exceção (vai à trilha).</summary>
    public bool ConfirmacaoTravada => ConfirmacaoDaRecepcao && !CorrigindoConfirmacao;
    public bool CorrigindoConfirmacao { get; set; }

    /// <summary>"autorizado pela Amil em 17/11".</summary>
    public string? RotuloDaAutorizacao => AutorizadaPor is null ? null
        : $"autorizado pela {AutorizadaPor}" + (DataAutorizacao is { } d ? $" em {d:dd/MM}" : "");

    /// <summary>A forma de confirmação que a tela mostra escolhida (a gravada ou a deduzida).</summary>
    public string? FormaEscolhida => GuiaAnsNaBaixa.FormaDe(ParaDados());

    /// <summary>O tipo de consulta só aparece na SP/SADT quando o atendimento é consulta.</summary>
    public bool PedeTipoConsulta => !EhSpSadt || TipoAtendimento == DominiosTiss.TipoAtendimentoConsulta;

    /// <summary>Os alertas que não impedem gravar (glosas de alerta), com o que está nos campos agora.</summary>
    public IReadOnlyList<string> Alertas() => GuiaAnsNaBaixa.Alertas(ParaDados());

    public static GuiaAnsEmEdicao De(DadosGuiaConsultaAns d) => new()
    {
        Modelo = d.Modelo,
        Senha = d.Senha,
        DataAutorizacao = d.DataAutorizacao,
        ValidadeSenha = d.ValidadeSenha,
        CodigoValidacao = d.CodigoValidacao,
        AusenciaCodigoValidacao = d.AusenciaCodigoValidacao,
        TipoAtendimento = d.TipoAtendimento,
        CaraterAtendimento = d.CaraterAtendimento,
        DescricaoProcedimento = d.DescricaoProcedimento,
        FormaConfirmacao = GuiaAnsNaBaixa.FormaDe(d),
        AnexosNoPortal = d.AnexosNoPortal,
        HoraInicial = d.HoraInicial,
        HoraFinal = d.HoraFinal,
        ExigeEquipe = d.ExigeEquipe,
        EquipeCpf = d.EquipeCpf,
        GrauParticipacao = d.GrauParticipacao,
        AutorizacaoId = d.AutorizacaoId,
        AutorizadaPor = d.AutorizadaPor,
        ConfirmacaoDaRecepcao = d.ConfirmacaoDaRecepcao,
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
        Observacao = Observacao,
        Modelo = Modelo,
        Senha = Senha?.Trim(),
        DataAutorizacao = DataAutorizacao,
        ValidadeSenha = ValidadeSenha,
        CodigoValidacao = CodigoValidacao?.Trim(),
        AusenciaCodigoValidacao = string.IsNullOrWhiteSpace(AusenciaCodigoValidacao) ? null : AusenciaCodigoValidacao,
        TipoAtendimento = TipoAtendimento,
        CaraterAtendimento = CaraterAtendimento,
        DescricaoProcedimento = DescricaoProcedimento?.Trim(),
        FormaConfirmacao = FormaConfirmacao,
        AnexosNoPortal = AnexosNoPortal,
        HoraInicial = HoraInicial,
        HoraFinal = HoraFinal,
        ExigeEquipe = ExigeEquipe,
        EquipeCpf = EquipeCpf?.Trim(),
        GrauParticipacao = GrauParticipacao,
        AutorizacaoId = AutorizacaoId,
        AutorizadaPor = AutorizadaPor,
        ConfirmacaoDaRecepcao = ConfirmacaoDaRecepcao
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
