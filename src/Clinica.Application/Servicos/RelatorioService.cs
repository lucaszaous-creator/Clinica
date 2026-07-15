using Clinica.Application.Abstracoes;
using Clinica.Application.Modelos;
using Clinica.Domain;
using Clinica.Domain.Entities;

namespace Clinica.Application.Servicos;

/// <summary>
/// Gera o relatório de faturamento: taxa de baixa (a métrica que comprova que a clínica parou de
/// perder faturas), quebra por convênio e envelhecimento das pendências em aberto.
/// </summary>
public sealed class RelatorioService
{
    private readonly IClinicaRepositorio _repo;

    public RelatorioService(IClinicaRepositorio repo) => _repo = repo;

    public async Task<RelatorioFaturamento> GerarAsync(DateOnly inicio, DateOnly fim, DateOnly referencia, CancellationToken ct = default)
    {
        var codigos = await _repo.CodigosNoPeriodoAsync(inicio, fim, ct);

        var resumo = Resumir(codigos);

        var porConvenio = codigos
            .GroupBy(c => c.Atendimento?.Paciente?.Convenio ?? default)
            .Select(g =>
            {
                var r = Resumir(g);
                return new FaturamentoPorConvenio(g.Key, r.TotalCodigos, r.Baixados, r.Pendentes, r.TaxaBaixa);
            })
            .OrderBy(c => c.Convenio)
            .ToList();

        var envelhecimento = await EnvelhecimentoAsync(referencia, ct);

        return new RelatorioFaturamento(inicio, fim, resumo, porConvenio, envelhecimento);
    }

    private static ResumoFaturamento Resumir(IEnumerable<CodigoFaturamento> codigos)
    {
        var lista = codigos as IReadOnlyCollection<CodigoFaturamento> ?? codigos.ToList();
        var total = lista.Count;
        var baixados = lista.Count(c => c.Baixado);
        var pendentes = total - baixados;
        var taxa = total == 0 ? 0 : Math.Round(baixados * 100.0 / total, 1);
        return new ResumoFaturamento(total, baixados, pendentes, taxa);
    }

    /// <summary>Agrupa as pendências em aberto por faixa de atraso.</summary>
    private async Task<IReadOnlyList<FaixaEnvelhecimento>> EnvelhecimentoAsync(DateOnly referencia, CancellationToken ct)
    {
        var abertos = await _repo.CodigosEmAbertoAsync(ct);
        var pendentes = abertos.Where(c => c.EstaPendente(referencia)).ToList();

        int Faixa(CodigoFaturamento c, int min, int max)
        {
            var atraso = referencia.DayNumber - c.DataPrevistaFaturamento.DayNumber;
            return atraso >= min && atraso <= max ? 1 : 0;
        }

        return new List<FaixaEnvelhecimento>
        {
            new("0–7 dias",   pendentes.Sum(c => Faixa(c, 0, 7))),
            new("8–30 dias",  pendentes.Sum(c => Faixa(c, 8, 30))),
            new("+30 dias",   pendentes.Sum(c => Faixa(c, 31, int.MaxValue)))
        };
    }
}
