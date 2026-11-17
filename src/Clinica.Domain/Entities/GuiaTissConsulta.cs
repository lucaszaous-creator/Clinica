namespace Clinica.Domain.Entities;

/// <summary>
/// A GUIA DE CONSULTA no padrão TISS/ANS, como foi conferida na BAIXA (nov/2026) — os mesmos
/// campos que a recepção preenche na tela "Guia de Consulta" do portal da operadora, com a
/// numeração da guia ANS no comentário de cada um.
///
/// Uma por guia (<see cref="CodigoFaturamento"/>), e é uma FOTOGRAFIA: guarda o que foi
/// conferido naquele dia, não um ponteiro para o cadastro. Se a carteira do paciente ou o
/// conselho do profissional mudarem depois, a guia já baixada continua dizendo o que foi
/// faturado. Os domínios (conselho, UF, CBO, regime…) são guardados pelo CÓDIGO do padrão
/// (<see cref="Regras.DominiosTiss"/>), que é o que vai no XML.
///
/// Não é prontuário: não leva procedimento além do código TUSS da consulta, nem CID, nem
/// evolução. É dado de faturamento, o mesmo que já vai à operadora pelo portal.
/// </summary>
public class GuiaTissConsulta
{
    /// <summary>Chave e vínculo: a guia do sistema a que estes dados pertencem.</summary>
    public int CodigoId { get; set; }
    public CodigoFaturamento? Codigo { get; set; }

    // ---------- Cabeçalho ----------

    /// <summary>1 - Registro ANS da operadora (6 dígitos).</summary>
    public string RegistroAns { get; set; } = string.Empty;

    // ---------- Dados do beneficiário ----------

    /// <summary>4 - Número da carteira.</summary>
    public string NumeroCarteira { get; set; } = string.Empty;

    /// <summary>5 - Validade da carteira. Só conferência: não existe na guia de consulta do XML 4.01.00.</summary>
    public DateOnly? ValidadeCarteira { get; set; }

    /// <summary>6 - Atendimento a recém-nascido.</summary>
    public bool AtendimentoRn { get; set; }

    /// <summary>26 - Nome social. Só conferência: não vai no XML.</summary>
    public string? NomeSocial { get; set; }

    /// <summary>7 - Nome do beneficiário. Só conferência: o TISS 4 não transporta o nome.</summary>
    public string NomeBeneficiario { get; set; } = string.Empty;

    // ---------- Dados do contratado ----------

    /// <summary>9 - Código do contratado na operadora (na Saúde Petrobras, o CNPJ só com dígitos).</summary>
    public string CodigoPrestadorNaOperadora { get; set; } = string.Empty;

    /// <summary>10 - Nome do contratado. Só conferência: não vai no XML.</summary>
    public string? NomeContratado { get; set; }

    /// <summary>11 - Código CNES (9999999 para quem não tem).</summary>
    public string Cnes { get; set; } = string.Empty;

    // ---------- Dados do profissional executante ----------

    /// <summary>12 - Nome do profissional executante.</summary>
    public string NomeProfissional { get; set; } = string.Empty;

    /// <summary>13 - Conselho profissional (código: "06" = CRM).</summary>
    public string ConselhoProfissional { get; set; } = string.Empty;

    /// <summary>14 - Número no conselho.</summary>
    public string NumeroConselho { get; set; } = string.Empty;

    /// <summary>15 - UF do conselho (código IBGE: "33" = RJ).</summary>
    public string UfConselho { get; set; } = string.Empty;

    /// <summary>16 - Código CBO (lista fechada do padrão).</summary>
    public string Cbos { get; set; } = string.Empty;

    // ---------- Dados do atendimento / procedimento realizado ----------

    /// <summary>17 - Indicação de acidente ("9" = não acidentes).</summary>
    public string IndicacaoAcidente { get; set; } = string.Empty;

    /// <summary>27 - Indicador de cobertura especial; nulo = não se aplica.</summary>
    public string? CoberturaEspecial { get; set; }

    /// <summary>28 - Regime de atendimento ("01" = ambulatorial).</summary>
    public string RegimeAtendimento { get; set; } = string.Empty;

    /// <summary>29 - Saúde ocupacional; nulo = não se aplica.</summary>
    public string? SaudeOcupacional { get; set; }

    /// <summary>18 - Data do atendimento.</summary>
    public DateOnly DataAtendimento { get; set; }

    /// <summary>19 - Tabela do procedimento ("22" = TUSS procedimentos).</summary>
    public string CodigoTabela { get; set; } = string.Empty;

    /// <summary>20 - Tipo de consulta ("1" = primeira, "2" = seguimento…).</summary>
    public string TipoConsulta { get; set; } = string.Empty;

    /// <summary>21 - Código do procedimento.</summary>
    public string CodigoProcedimento { get; set; } = string.Empty;

    /// <summary>
    /// 22 - Valor do procedimento, como consta na guia enviada à operadora. É o valor FATURADO
    /// na guia — não é recebimento, e o sistema continua sem controlar recebíveis.
    /// </summary>
    public decimal ValorProcedimento { get; set; }

    /// <summary>23 - Observação / justificativa.</summary>
    public string? Observacao { get; set; }

    // ---------- Guia SP/SADT (nov/2026, Amil) ----------
    // A Amil não recebe guia de consulta: a consulta e a sessão vão em SP/SADT, com a senha de
    // autorização e o token do beneficiário. Os campos abaixo só existem nessa guia; nulos = guia
    // de consulta, como todas as que já estavam gravadas (a migration é aditiva).

    /// <summary>Modelo da guia: nulo ou "consulta" = guia de consulta; "sp-sadt" = guia SP/SADT.</summary>
    public string? ModeloGuia { get; set; }

    /// <summary>Senha de autorização da operadora (<c>dadosAutorizacao/senha</c>).</summary>
    public string? Senha { get; set; }

    /// <summary>Data da autorização (<c>dadosAutorizacao/dataAutorizacao</c>, obrigatória no bloco).</summary>
    public DateOnly? DataAutorizacao { get; set; }

    /// <summary>Validade da senha (<c>dadosAutorizacao/dataValidadeSenha</c>).</summary>
    public DateOnly? ValidadeSenha { get; set; }

    /// <summary>O token do beneficiário (<c>dadosAutorizacao/codValidacao</c>; na Amil, 6 dígitos).</summary>
    public string? CodigoValidacao { get; set; }

    /// <summary>Por que não há token (<c>dm_ausenciaCodValidacao</c>, "01" a "07").</summary>
    public string? AusenciaCodigoValidacao { get; set; }

    /// <summary>Tipo de atendimento (<c>dm_tipoAtendimento</c>: "04" consulta, "03" outras terapias).</summary>
    public string? TipoAtendimento { get; set; }

    /// <summary>Caráter do atendimento ("1" eletivo, "2" urgência).</summary>
    public string? CaraterAtendimento { get; set; }

    /// <summary>Descrição do procedimento (<c>descricaoProcedimento</c>, obrigatória na SP/SADT).</summary>
    public string? DescricaoProcedimento { get; set; }

    /// <summary>
    /// Como o beneficiário confirmou o atendimento (nov/2026, decisão do dono: a Amil é faturada só pelo
    /// sistema, com o TOKEN na guia): "token" ou "nao" (<c>GuiaAnsNaBaixa.Confirmacao*</c>). Nulo nas
    /// guias anteriores a este campo.
    /// </summary>
    public string? FormaConfirmacao { get; set; }

    /// <summary>Sem confirmação: quem conferiu declarou que a guia assinada e o documento com foto foram anexados no portal.</summary>
    public bool? AnexosNoPortal { get; set; }

    /// <summary>Hora inicial e final do procedimento (campos 37/38), do consultório ou da agenda.</summary>
    public TimeOnly? HoraInicial { get; set; }
    public TimeOnly? HoraFinal { get; set; }

    /// <summary>
    /// Equipe (campos 49–50): o CPF do profissional executante (11 dígitos) — é por ele que a Amil
    /// identifica o profissional (cadastrado em "Meus Dados" no portal; não existe código de profissional).
    /// </summary>
    public string? EquipeCpf { get; set; }

    /// <summary>Grau de participação (<c>dm_grauPart</c>; "12" = clínico).</summary>
    public string? GrauParticipacao { get; set; }

    // ---------- Trilha ----------

    /// <summary>Quem conferiu a guia na baixa (o operador que fez login).</summary>
    public string ConferidaPor { get; set; } = string.Empty;

    public DateTime ConferidaEm { get; set; }
}
