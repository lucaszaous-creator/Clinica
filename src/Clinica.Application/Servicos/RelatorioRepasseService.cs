using Clinica.Application.Abstracoes;
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
/// </summary>
public sealed class RelatorioRepasseService(IClinicaRepositorio repo)
{
    public const Permissao PermissaoParaExportar = Permissao.VerFinanceiro;
    public const string AcaoDaTrilha = "RelatorioRepasseExportado";

    /// <summary>
    /// Lê o relatório (sem gravar nada). Recusa com frase: período invertido ou maior que 24
    /// competências. Consulta filtrada NO BANCO.
    /// </summary>
    public Task<RelatorioRepasse> GerarAsync(FiltroRelatorioRepasse filtro, CancellationToken ct = default)
        => throw new NotImplementedException();

    /// <summary>
    /// Um PDF por médico presente no filtro (com o filtro de médico, só o dele). Cada PDF é o
    /// relatório daquele médico no período: cabeçalho da clínica, médico e registro, período,
    /// resumo por competência × convênio, detalhe item a item agrupado por competência,
    /// convênio e referência do pagamento, totais (pago / a pagar) e rodapé com a emissão.
    /// Grava UMA linha de trilha para a exportação inteira.
    /// </summary>
    public Task<IReadOnlyList<ArquivoDoRelatorioRepasse>> ExportarPdfsAsync(
        FiltroRelatorioRepasse filtro, SessaoUsuario? sessao, CancellationToken ct = default)
        => throw new NotImplementedException();

    /// <summary>A planilha .xlsx de todas as linhas do filtro (autofiltro, datas e números tipados).</summary>
    public Task<ArquivoDoRelatorioRepasse> ExportarPlanilhaAsync(
        FiltroRelatorioRepasse filtro, SessaoUsuario? sessao, CancellationToken ct = default)
        => throw new NotImplementedException();
}
