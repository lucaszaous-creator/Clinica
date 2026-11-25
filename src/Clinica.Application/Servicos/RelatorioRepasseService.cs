using System.Globalization;
using System.Text;
using Clinica.Application.Abstracoes;
using Clinica.Application.Formatacao;
using Clinica.Application.Modelos;
using Clinica.Domain.Entities;

namespace Clinica.Application.Servicos;

/// <summary>
/// O relatório de repasse para os médicos (nov/2026): filtra os fechamentos gravados e
/// exporta — um PDF por médico (o que vai para cada um) e a planilha .xlsx (o financeiro).
/// Exportar é saída de dado de paciente: exige login e <see cref="PermissaoParaExportar"/>,
/// recusa ANTES de ler, e grava a trilha no mesmo SaveChanges.
///
/// ⚠️ CONTRATO FIXADO — a tela é escrita contra estas assinaturas.
///
/// Os totais contam só fechamentos VIVOS: o cancelado aparece (quando o filtro pede), marcado,
/// e fica fora de quantidade, total, pago e a pagar. O estorno é linha NEGATIVA e soma no total
/// — é o que de fato se paga.
/// </summary>
public sealed class RelatorioRepasseService(IClinicaRepositorio repo)
{
    public const Permissao PermissaoParaExportar = Permissao.VerFinanceiro;
    public const string AcaoDaTrilha = "RelatorioRepasseExportado";

    /// <summary>Teto do período: duas competências por ano, no máximo dois anos por leitura.</summary>
    public const int MaximoCompetencias = 24;

    public const string MensagemSemLinhas = "Nenhum fechamento no filtro — não há o que exportar.";
    public const string MensagemSemPermissao =
        "Seu acesso não permite exportar o repasse dos médicos (é preciso ver o financeiro).";

    private static readonly CultureInfo PtBr = CultureInfo.GetCultureInfo("pt-BR");
    private static readonly StringComparer Texto = StringComparer.Create(PtBr, CompareOptions.IgnoreCase);

    /// <summary>
    /// Lê o relatório (sem gravar nada). Recusa com frase: período invertido ou maior que 24
    /// competências. Consulta filtrada NO BANCO.
    /// </summary>
    public async Task<RelatorioRepasse> GerarAsync(FiltroRelatorioRepasse filtro, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(filtro);
        if (Recusa(filtro) is { } recusa) throw new InvalidOperationException(recusa);
        var f = Normalizar(filtro);

        // O banco devolve um SUPERCONJUNTO no trecho do paciente (LIKE com curinga onde pode
        // haver acento); a conferência exata, sem acento, é aqui, sobre o recorte já pequeno.
        var termo = TextoDeBusca(f.Paciente);
        var linhas = (await repo.RelatorioRepasseAsync(f, ct))
            .Where(l => termo.Length == 0 || TextoDeBusca(l.Paciente).Contains(termo, StringComparison.Ordinal))
            .OrderBy(l => l.Profissional, Texto).ThenBy(l => l.ProfissionalId)
            .ThenBy(l => l.Competencia)
            .ThenBy(l => l.FechamentoId)
            .ThenBy(l => l.Convenio, Texto)
            .ThenBy(l => l.ReferenciaPagamento ?? string.Empty, Texto)
            .ThenBy(l => l.Natureza)
            .ThenBy(l => l.Paciente, Texto)
            .ThenBy(l => l.DataAtendimento)
            .ToList();

        var prestador = await new ParametrosService(repo).ObterPrestadorAsync(ct);
        var clinica = new[] { prestador.NomeFantasia, prestador.RazaoSocial }
            .FirstOrDefault(v => !string.IsNullOrWhiteSpace(v))?.Trim() ?? "Clínica";

        var vivas = linhas.Where(l => !l.Cancelado).ToList();
        var porProfissional = linhas
            .GroupBy(l => l.ProfissionalId)
            .Select(g =>
            {
                var v = g.Where(l => !l.Cancelado).ToList();
                var total = v.Sum(l => l.Valor);
                var pago = v.Where(l => l.PagoEm is not null).Sum(l => l.Valor);
                return new TotalProfissionalRelatorioRepasse(
                    g.Key, g.First().Profissional, g.First().Registro,
                    v.Select(l => l.FechamentoId).Distinct().Count(),
                    v.Count(l => l.Natureza == NaturezaItemRepasse.Credito),
                    total, pago, total - pago);
            })
            .OrderBy(t => t.Profissional, Texto)
            .ToList();

        var resumo = vivas
            .GroupBy(l => (l.ProfissionalId, l.Convenio))
            .Select(g => Resumir(g.Key.ProfissionalId, g.First().Profissional, g.Key.Convenio, g.ToList()))
            .OrderBy(r => r.Profissional, Texto).ThenBy(r => r.Convenio, Texto)
            .ToList();

        var totalGeral = vivas.Sum(l => l.Valor);
        var pagoGeral = vivas.Where(l => l.PagoEm is not null).Sum(l => l.Valor);
        return new RelatorioRepasse(f, clinica, linhas, porProfissional, resumo,
            vivas.Count(l => l.Natureza == NaturezaItemRepasse.Credito), totalGeral, pagoGeral, totalGeral - pagoGeral);
    }

    /// <summary>
    /// Um PDF por médico presente no filtro (com o filtro de médico, só o dele). Cada PDF é o
    /// relatório daquele médico no período: cabeçalho da clínica, médico e registro, período,
    /// resumo por competência × convênio, detalhe item a item agrupado por competência,
    /// convênio e referência do pagamento, totais (pago / a pagar) e rodapé com a emissão.
    /// Grava UMA linha de trilha para a exportação inteira.
    /// </summary>
    public async Task<IReadOnlyList<ArquivoDoRelatorioRepasse>> ExportarPdfsAsync(
        FiltroRelatorioRepasse filtro, SessaoUsuario? sessao, CancellationToken ct = default)
    {
        var operador = Barreira(sessao);
        var relatorio = await GerarAsync(filtro, ct);
        if (relatorio.Linhas.Count == 0) throw new InvalidOperationException(MensagemSemLinhas);

        var emitidoEm = DateTime.Now;
        var arquivos = relatorio.PorProfissional
            .Select(t => new ArquivoDoRelatorioRepasse(
                RelatorioRepassePdf.Gerar(relatorio, t, emitidoEm, sessao!.Nome is { Length: > 0 } nome ? nome : operador),
                NomeDoPdf(t.Profissional, relatorio.Filtro)))
            .ToList();

        await GravarTrilhaAsync(operador, relatorio, $"{arquivos.Count} PDF(s)", ct);
        return arquivos;
    }

    /// <summary>A planilha .xlsx de todas as linhas do filtro (autofiltro, datas e números tipados).</summary>
    public async Task<ArquivoDoRelatorioRepasse> ExportarPlanilhaAsync(
        FiltroRelatorioRepasse filtro, SessaoUsuario? sessao, CancellationToken ct = default)
    {
        var operador = Barreira(sessao);
        var relatorio = await GerarAsync(filtro, ct);
        if (relatorio.Linhas.Count == 0) throw new InvalidOperationException(MensagemSemLinhas);

        var conteudo = PlanilhaXlsx.Gerar("Repasse", Colunas, relatorio.Linhas.Select(Celulas));
        await GravarTrilhaAsync(operador, relatorio, "planilha", ct);
        return new ArquivoDoRelatorioRepasse(conteudo, NomeDaPlanilha(relatorio.Filtro));
    }

    // ==================== Regras (públicas para a tela e os testes) ====================

    /// <summary>Recusa o período impossível antes de ir ao banco; nulo = válido.</summary>
    public static string? Recusa(FiltroRelatorioRepasse f)
    {
        var meses = Meses(f.CompetenciaDe, f.CompetenciaAte);
        return meses < 1 ? "A competência final está antes da inicial."
            : meses > MaximoCompetencias ? $"Escolha até {MaximoCompetencias} competências por relatório."
            : null;
    }

    /// <summary>"09/2026" quando é um mês só; senão "09/2026 a 11/2026".</summary>
    public static string Periodo(FiltroRelatorioRepasse f)
        => Meses(f.CompetenciaDe, f.CompetenciaAte) == 1
            ? Mes(f.CompetenciaDe)
            : $"{Mes(f.CompetenciaDe)} a {Mes(f.CompetenciaAte)}";

    /// <summary>O trecho digitado, sem acento, minúsculo e aparado — a mesma limpeza que o banco faz na coluna.</summary>
    public static string TextoDeBusca(string? texto)
    {
        if (string.IsNullOrWhiteSpace(texto)) return string.Empty;
        var sb = new StringBuilder();
        foreach (var ch in texto.Trim().Normalize(NormalizationForm.FormD))
            if (CharUnicodeInfo.GetUnicodeCategory(ch) != UnicodeCategory.NonSpacingMark) sb.Append(ch);
        return sb.ToString().Normalize(NormalizationForm.FormC).ToLowerInvariant();
    }

    /// <summary>Relatorio_Repasse_{Nome}_{MM-aaaa}[_a_{MM-aaaa}].pdf, sem acento nem espaço.</summary>
    public static string NomeDoPdf(string profissional, FiltroRelatorioRepasse f)
    {
        var nome = DemonstrativoRepassePdf.SemAcentosNemEspacos(profissional);
        return $"Relatorio_Repasse_{(nome.Length == 0 ? "Profissional" : nome)}_{SufixoDoArquivo(f)}.pdf";
    }

    /// <summary>Relatorio_Repasse_{MM-aaaa}[_a_{MM-aaaa}].xlsx</summary>
    public static string NomeDaPlanilha(FiltroRelatorioRepasse f) => $"Relatorio_Repasse_{SufixoDoArquivo(f)}.xlsx";

    /// <summary>As colunas da planilha, na ordem pedida.</summary>
    public static IReadOnlyList<string> Colunas { get; } =
    [
        "Médico", "Registro", "Competência", "Situação", "Conferido por", "Conferido em", "Pago em",
        "Convênio", "Referência do pagamento", "Paciente", "Data do atendimento", "Tipo", "Guia",
        "Recebido em", "Valor recebido", "Regra", "Repasse"
    ];

    /// <summary>A linha da planilha com o TIPO de cada célula: data como data, valor como número.</summary>
    public static IReadOnlyList<CelulaXlsx> Celulas(LinhaRelatorioRepasse l) =>
    [
        CelulaXlsx.Texto(l.Profissional),
        CelulaXlsx.Texto(l.Registro),
        CelulaXlsx.Data(l.Competencia),
        CelulaXlsx.Texto(l.Situacao),
        CelulaXlsx.Texto(l.ConferidoPor),
        CelulaXlsx.Data(DateOnly.FromDateTime(l.ConferidoEm)),
        l.PagoEm is { } pago ? CelulaXlsx.Data(pago) : CelulaXlsx.Texto(null),
        CelulaXlsx.Texto(l.Convenio),
        CelulaXlsx.Texto(l.ReferenciaPagamento),
        CelulaXlsx.Texto(l.Paciente),
        CelulaXlsx.Data(l.DataAtendimento),
        CelulaXlsx.Texto(l.Natureza == NaturezaItemRepasse.Estorno ? $"{l.Descricao} (estorno)" : l.Descricao),
        CelulaXlsx.Texto(l.NumeroGuia),
        CelulaXlsx.Data(l.DataRecebimento),
        CelulaXlsx.Numero((double)l.ValorRecebido),
        CelulaXlsx.Texto(l.Regra),
        CelulaXlsx.Numero((double)l.Valor)
    ];

    /// <summary>Resumo de um grupo (vivo): créditos contados, unitário só quando único e sem estorno.</summary>
    public static ResumoRelatorioRepasse Resumir(int profissionalId, string profissional, string convenio,
        IReadOnlyList<LinhaRelatorioRepasse> linhas)
    {
        var creditos = linhas.Where(l => l.Natureza == NaturezaItemRepasse.Credito).ToList();
        var valores = creditos.Select(l => l.Valor).Distinct().ToList();
        var temEstorno = linhas.Any(l => l.Natureza == NaturezaItemRepasse.Estorno);
        return new ResumoRelatorioRepasse(profissionalId, profissional, convenio, creditos.Count,
            valores.Count == 1 && !temEstorno ? valores[0] : null, linhas.Sum(l => l.Valor));
    }

    // ==================== Peças internas ====================

    /// <summary>As duas barreiras do serviço, ANTES de qualquer leitura. Devolve o operador.</summary>
    private static string Barreira(SessaoUsuario? sessao)
    {
        if (sessao is not { Autenticado: true } || !sessao.Pode(PermissaoParaExportar))
            throw new UnauthorizedAccessException(MensagemSemPermissao);
        return string.IsNullOrWhiteSpace(sessao.Operador) ? "?" : sessao.Operador;
    }

    private async Task GravarTrilhaAsync(string operador, RelatorioRepasse r, string arquivos, CancellationToken ct)
    {
        await repo.RegistrarAuditoriaAsync(new EventoAuditoria
        {
            Operador = operador,
            Acao = AcaoDaTrilha,
            Detalhe = DetalheDaTrilha(r, arquivos)
        }, ct);
        await repo.SalvarAsync(ct);
    }

    /// <summary>
    /// "Competências 09/2026 a 11/2026 · médico 3 · convênio Amil · situação a pagar · com filtro de
    /// paciente · 2 médico(s), 14 linha(s) · 2 PDF(s)". O trecho do paciente NÃO vai à trilha (nome
    /// de paciente numa linha de auditoria é dado pessoal sem necessidade).
    /// </summary>
    public static string DetalheDaTrilha(RelatorioRepasse r, string arquivos)
    {
        var f = r.Filtro;
        var partes = new List<string> { $"Competências {Periodo(f)}" };
        if (f.ProfissionalId is { } id)
            partes.Add($"médico {r.PorProfissional.FirstOrDefault(p => p.ProfissionalId == id)?.Profissional ?? id.ToString(CultureInfo.InvariantCulture)}");
        if (!string.IsNullOrWhiteSpace(f.ConvenioCodigo)) partes.Add($"convênio {f.ConvenioCodigo.Trim()}");
        partes.Add($"situação {Situacao(f.Situacao)}");
        if (!string.IsNullOrWhiteSpace(f.Paciente)) partes.Add("com filtro de paciente");
        partes.Add($"{r.PorProfissional.Count} médico(s), {r.Linhas.Count} linha(s)");
        partes.Add(arquivos);
        return string.Join(" · ", partes);
    }

    public static string Situacao(SituacaoNoRelatorioRepasse s) => s switch
    {
        SituacaoNoRelatorioRepasse.SoPagos => "só pagos",
        SituacaoNoRelatorioRepasse.SoAPagar => "a pagar",
        SituacaoNoRelatorioRepasse.IncluirCancelados => "inclusive cancelados",
        _ => "conferidos e pagos"
    };

    private static FiltroRelatorioRepasse Normalizar(FiltroRelatorioRepasse f) => f with
    {
        CompetenciaDe = new DateOnly(f.CompetenciaDe.Year, f.CompetenciaDe.Month, 1),
        CompetenciaAte = new DateOnly(f.CompetenciaAte.Year, f.CompetenciaAte.Month, 1),
        ConvenioCodigo = string.IsNullOrWhiteSpace(f.ConvenioCodigo) ? null : f.ConvenioCodigo.Trim(),
        Paciente = string.IsNullOrWhiteSpace(f.Paciente) ? null : f.Paciente.Trim()
    };

    private static int Meses(DateOnly de, DateOnly ate) => (ate.Year * 12 + ate.Month) - (de.Year * 12 + de.Month) + 1;

    private static string Mes(DateOnly d) => d.ToString("MM/yyyy", PtBr);

    private static string SufixoDoArquivo(FiltroRelatorioRepasse f)
        => Meses(f.CompetenciaDe, f.CompetenciaAte) == 1
            ? f.CompetenciaDe.ToString("MM-yyyy", PtBr)
            : $"{f.CompetenciaDe.ToString("MM-yyyy", PtBr)}_a_{f.CompetenciaAte.ToString("MM-yyyy", PtBr)}";
}
