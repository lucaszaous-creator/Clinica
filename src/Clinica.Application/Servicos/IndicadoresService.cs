using System.Globalization;
using Clinica.Application.Abstracoes;
using Clinica.Application.Modelos;
using Clinica.Domain.Entities;

namespace Clinica.Application.Servicos;

/// <summary>
/// Os indicadores gerenciais (feature 12): ocupação, no-show e produtividade por
/// profissional. É a tela que só o Gerente tem, e a que estava parada desde a parcela
/// 1 — sem <see cref="Profissional"/> não havia por quem agrupar.
///
/// Só LÊ. Nada aqui grava: o Gerente enxerga o que os outros módulos produziram, e por
/// ser leitura não disputa escrita com o balcão nem com o faturamento.
///
/// UMA HONESTIDADE IMPORTANTE SOBRE A OCUPAÇÃO: a clínica não cadastra jornada de
/// trabalho por profissional, então a base de cálculo é "dias em que ele TEVE agenda ×
/// jornada padrão". Ou seja, o indicador responde "nos dias em que abriu a agenda,
/// quanto dela ficou ocupado" — não "quanto da capacidade instalada foi usada".
/// Inventar dias úteis daria um número mais bonito e menos verdadeiro; quando a clínica
/// cadastrar jornada, é aqui que ela entra.
/// </summary>
public sealed class IndicadoresService
{
    private readonly IClinicaRepositorio _repo;
    private readonly ParametrosService _parametros;

    private static readonly CultureInfo PtBr = new("pt-BR");

    public IndicadoresService(IClinicaRepositorio repo, ParametrosService parametros)
    {
        _repo = repo;
        _parametros = parametros;
    }

    /// <summary>Painel completo do período: agenda, produtividade e série mensal.</summary>
    public async Task<PainelGerencial> GerarAsync(
        DateOnly inicio, DateOnly fim, CancellationToken ct = default)
    {
        if (fim < inicio) (inicio, fim) = (fim, inicio);

        var jornada = await _parametros.ObterJornadaDiariaAsync(ct);
        var agendamentos = await AgendamentosAsync(inicio, fim, ct);
        var evolucoes = await _repo.EvolucoesNoPeriodoAsync(inicio, fim, ct);
        var profissionais = await _repo.ProfissionaisAsync(ct);

        var agenda = ResumirAgenda(agendamentos, jornada);
        var produtividade = Produtividade(agendamentos, evolucoes, profissionais, jornada);
        var serie = Serie(agendamentos, jornada);

        return new PainelGerencial(inicio, fim, jornada, agenda, produtividade, serie);
    }

    /// <summary>Só a produtividade, para quem já tem o período na tela.</summary>
    public async Task<IReadOnlyList<ProdutividadeProfissional>> ProdutividadeAsync(
        DateOnly inicio, DateOnly fim, CancellationToken ct = default)
        => (await GerarAsync(inicio, fim, ct)).Produtividade;

    private async Task<IReadOnlyList<Agendamento>> AgendamentosAsync(
        DateOnly inicio, DateOnly fim, CancellationToken ct)
        => await _repo.AgendamentosNoPeriodoAsync(
            inicio.ToDateTime(TimeOnly.MinValue),
            fim.ToDateTime(TimeOnly.MaxValue),
            ct);

    // ---------------------------------------------------------------- agenda

    private static IndicadoresAgenda ResumirAgenda(
        IReadOnlyList<Agendamento> agendamentos, int jornada)
    {
        var ocupam = agendamentos.Where(a => a.OcupaAgenda).ToList();

        return new IndicadoresAgenda(
            Agendados: ocupam.Count,
            Atendidos: agendamentos.Count(a => a.Status == StatusAgendamento.Realizado),
            Faltas: agendamentos.Count(a => a.Status == StatusAgendamento.Faltou),
            Cancelados: agendamentos.Count(a => a.Status == StatusAgendamento.Cancelado),
            Encaixes: ocupam.Count(a => a.Encaixe),
            PacientesDistintos: ocupam.Select(a => a.PacienteId).Distinct().Count(),
            MinutosOcupados: ocupam.Sum(a => a.DuracaoEfetiva),
            MinutosDisponiveis: MinutosDisponiveis(agendamentos, jornada));
    }

    /// <summary>
    /// Base da ocupação: cada par (profissional, dia) com agenda vale uma jornada. Os
    /// horários sem profissional informado contam como UMA agenda da clínica no dia —
    /// senão eles somariam ocupação sem somar disponibilidade, e a taxa passaria de 100%.
    /// </summary>
    private static int MinutosDisponiveis(IReadOnlyList<Agendamento> agendamentos, int jornada)
        => agendamentos
            .Where(a => a.OcupaAgenda)
            .Select(a => (a.ProfissionalId, Dia: DateOnly.FromDateTime(a.DataHora)))
            .Distinct()
            .Count() * jornada;

    // -------------------------------------------------------- produtividade

    private static IReadOnlyList<ProdutividadeProfissional> Produtividade(
        IReadOnlyList<Agendamento> agendamentos,
        IReadOnlyList<Evolucao> evolucoes,
        IReadOnlyList<Profissional> profissionais,
        int jornada)
    {
        var nomes = profissionais.ToDictionary(p => p.Id, p => p.Rotulo);

        var porProfissional = evolucoes
            .Where(e => e.ProfissionalId is not null)
            .GroupBy(e => e.ProfissionalId!.Value)
            .ToDictionary(g => g.Key, g => g.ToList());

        var linhas = agendamentos
            .GroupBy(a => a.ProfissionalId)
            .Select(g =>
            {
                var ocupam = g.Where(a => a.OcupaAgenda).ToList();
                var dias = ocupam.Select(a => DateOnly.FromDateTime(a.DataHora)).Distinct().Count();

                var doProfissional = g.Key is { } id && porProfissional.TryGetValue(id, out var lista)
                    ? lista
                    : new List<Evolucao>();

                var pares = doProfissional
                    .Where(e => e.TemParEva)
                    .Select(e => (double)e.VariacaoEva!.Value)
                    .ToList();

                return new ProdutividadeProfissional(
                    ProfissionalId: g.Key,
                    Nome: g.Key is { } pid && nomes.TryGetValue(pid, out var nome)
                        ? nome
                        : "Sem profissional informado",
                    Agendados: ocupam.Count,
                    Atendidos: g.Count(a => a.Status == StatusAgendamento.Realizado),
                    Faltas: g.Count(a => a.Status == StatusAgendamento.Faltou),
                    Cancelados: g.Count(a => a.Status == StatusAgendamento.Cancelado),
                    Encaixes: ocupam.Count(a => a.Encaixe),
                    PacientesDistintos: ocupam.Select(a => a.PacienteId).Distinct().Count(),
                    MinutosOcupados: ocupam.Sum(a => a.DuracaoEfetiva),
                    DiasComAgenda: dias,
                    MinutosDisponiveis: dias * jornada,
                    Evolucoes: doProfissional.Count,
                    // A EVA só entra em PAR (regra da parcela 2): média de meia medida
                    // oscila por falta de dado, não por dor.
                    MelhoraMediaEva: pares.Count == 0 ? null : Math.Round(pares.Average(), 1));
            })
            .OrderByDescending(l => l.Atendidos)
            .ThenBy(l => l.Nome)
            .ToList();

        return linhas;
    }

    // ------------------------------------------------------------- série mensal

    private static IReadOnlyList<MesGerencial> Serie(
        IReadOnlyList<Agendamento> agendamentos, int jornada)
        => agendamentos
            .GroupBy(a => new { a.DataHora.Year, a.DataHora.Month })
            .OrderBy(g => g.Key.Year).ThenBy(g => g.Key.Month)
            .Select(g =>
            {
                var ocupam = g.Where(a => a.OcupaAgenda).ToList();
                return new MesGerencial(
                    g.Key.Year,
                    g.Key.Month,
                    new DateTime(g.Key.Year, g.Key.Month, 1).ToString("MMM/yyyy", PtBr),
                    Atendidos: g.Count(a => a.Status == StatusAgendamento.Realizado),
                    Faltas: g.Count(a => a.Status == StatusAgendamento.Faltou),
                    Cancelados: g.Count(a => a.Status == StatusAgendamento.Cancelado),
                    MinutosOcupados: ocupam.Sum(a => a.DuracaoEfetiva),
                    MinutosDisponiveis: MinutosDisponiveis(g.ToList(), jornada));
            })
            .ToList();
}
