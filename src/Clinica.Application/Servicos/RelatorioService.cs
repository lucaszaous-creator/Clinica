using Clinica.Application.Abstracoes;
using Clinica.Application.Modelos;
using Clinica.Domain;
using Clinica.Domain.Entities;
using Clinica.Domain.Regras;

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
                return new FaturamentoPorConvenio(g.Key, r.TotalCodigos, r.Baixados, r.Pendentes, r.TaxaBaixa,
                    r.Glosadas, r.TaxaGlosa, r.TempoMedioBaixaDias);
            })
            .OrderBy(c => c.Convenio)
            .ToList();

        var envelhecimento = await EnvelhecimentoAsync(referencia, ct);

        // Consultas avulsas por especialidade — responde "quantas consultas de cada especialidade fizemos".
        // Agrupa pelo código (abrange especialidades criadas pela clínica) e resolve o nome pelo catálogo.
        var consultasEspecialidades = codigos
            .Where(c => c.Tipo == TipoCodigo.Consulta && c.Status != StatusCodigo.NaoAplicavel)
            .GroupBy(c => c.EspecialidadeCodigo ?? c.Especialidade?.ToString())
            .Select(g => new ConsultasPorEspecialidade(
                g.Key is { } cod ? CatalogoEspecialidades.Nome(cod) : "Sem especialidade",
                g.Count(),
                g.Count(c => c.Baixado)))
            .OrderByDescending(c => c.Quantidade)
            .ThenBy(c => c.Especialidade)
            .ToList();

        return new RelatorioFaturamento(inicio, fim, resumo, porConvenio, envelhecimento, consultasEspecialidades);
    }

    private static ResumoFaturamento Resumir(IEnumerable<CodigoFaturamento> codigos)
    {
        var lista = codigos as IReadOnlyCollection<CodigoFaturamento> ?? codigos.ToList();
        var total = lista.Count;
        var baixados = lista.Count(c => c.Baixado);
        var pendentes = total - baixados;
        var taxa = total == 0 ? 0 : Math.Round(baixados * 100.0 / total, 1);

        // Taxa de glosa: % das guias baixadas que sofreram glosa (mesmo que depois recuperada).
        var glosadas = lista.Count(c => c.Glosa != StatusGlosa.SemGlosa);
        var taxaGlosa = baixados == 0 ? 0 : Math.Round(glosadas * 100.0 / baixados, 1);

        // Tempo médio atendimento → baixa (dias corridos), só das guias com baixa e atendimento carregado.
        var tempos = lista
            .Where(c => c.DataBaixa is not null && c.Atendimento is not null)
            .Select(c => (double)(c.DataBaixa!.Value.DayNumber - c.Atendimento!.Data.DayNumber))
            .ToList();
        double? tempoMedio = tempos.Count == 0 ? null : Math.Round(tempos.Average(), 1);

        return new ResumoFaturamento(total, baixados, pendentes, taxa, glosadas, taxaGlosa, tempoMedio);
    }

    /// <summary>
    /// Comparativo mensal: taxa de baixa dos últimos <paramref name="meses"/> meses
    /// (terminando no mês de <paramref name="referencia"/>), do mais antigo ao mais recente.
    /// </summary>
    public async Task<IReadOnlyList<ResumoMensal>> ComparativoMensalAsync(
        DateOnly referencia, int meses = 6, CancellationToken ct = default)
    {
        var lista = new List<ResumoMensal>();
        var mesRef = new DateOnly(referencia.Year, referencia.Month, 1);

        for (var i = meses - 1; i >= 0; i--)
        {
            var inicio = mesRef.AddMonths(-i);
            var fim = inicio.AddMonths(1).AddDays(-1);
            var r = Resumir(await _repo.CodigosNoPeriodoAsync(inicio, fim, ct));
            lista.Add(new ResumoMensal(inicio.Year, inicio.Month,
                inicio.ToDateTime(TimeOnly.MinValue).ToString("MMM/yyyy", new System.Globalization.CultureInfo("pt-BR")),
                r.TotalCodigos, r.Baixados, r.Pendentes, r.TaxaBaixa));
        }

        return lista;
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
