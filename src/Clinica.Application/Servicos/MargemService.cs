using Clinica.Application.Abstracoes;
using Clinica.Domain.Entities;
using Clinica.Domain.Regras;

namespace Clinica.Application.Servicos;

/// <summary>Uma sessão realizada, com o que ela trouxe e o que ela custou.</summary>
public sealed record SessaoParaMargem(
    int AtendimentoId,
    string Profissional,
    string Modalidade,
    string Pagador,
    decimal Receita,
    decimal Deducoes,
    decimal Insumo,
    bool InsumoIncompleto,
    decimal Repasse,
    bool TemReceita)
{
    /// <summary>O que sobra da sessão: receita menos taxa e imposto, insumo e repasse.</summary>
    public decimal Margem => Receita - Deducoes - Insumo - Repasse;
}

/// <summary>Uma linha da tabela de margem (por modalidade, profissional ou pagador).</summary>
public sealed record LinhaMargem(
    string Nome,
    int Sessoes,
    int SessoesComReceita,
    decimal Receita,
    decimal Deducoes,
    decimal Insumo,
    decimal Repasse)
{
    public decimal Margem => Receita - Deducoes - Insumo - Repasse;

    /// <summary>A margem média das sessões que TÊM receita. Nula sem nenhuma — nunca zero.</summary>
    public decimal? MargemPorSessao => SessoesComReceita > 0 ? Math.Round(Margem / SessoesComReceita, 2) : null;

    /// <summary>O que sobra de cada real que entrou. Nula sem receita.</summary>
    public double? FracaoDaReceita => Receita > 0m ? (double)(Margem / Receita) : null;
}

/// <summary>A margem do período, nos três recortes, e o que ficou de fora dela.</summary>
public sealed record MargemDoPeriodo(
    DateOnly Inicio,
    DateOnly Fim,
    LinhaMargem Total,
    IReadOnlyList<LinhaMargem> PorModalidade,
    IReadOnlyList<LinhaMargem> PorProfissional,
    IReadOnlyList<LinhaMargem> PorPagador,
    int SessoesSemReceita,
    int SessoesComInsumoIncompleto);

/// <summary>
/// A margem por sessão (out/2026, etapa "Produção e margem" do Gerente): quanto sobra de cada
/// sessão depois da taxa, do imposto, do insumo e do repasse — por modalidade, profissional e
/// pagador. Não recalcula nada que outro serviço já calcula: a receita é a do financeiro (só o
/// REALIZADO, como o repasse), o insumo é o do <see cref="EstoqueService"/> e o repasse é o do
/// <see cref="RepasseService"/>.
///
/// Sessão sem receita lançada (guia que o convênio ainda não pagou, particular a receber) fica
/// FORA da margem e é contada à parte: entrar com receita zero e custo cheio daria uma margem
/// negativa que não aconteceu.
/// </summary>
public sealed class MargemService(IClinicaRepositorio repo, EstoqueService estoque, RepasseService repasse)
{
    public async Task<MargemDoPeriodo> CalcularAsync(DateOnly inicio, DateOnly fim, CancellationToken ct = default)
    {
        if (fim < inicio) (inicio, fim) = (fim, inicio);

        var agendamentos = (await repo.AgendamentosComAtendimentoAsync(inicio, fim, ct))
            .Where(a => a.AtendimentoId is not null)
            .GroupBy(a => a.AtendimentoId!.Value)
            .Select(g => g.OrderBy(a => a.DataHora).First())
            .ToList();
        var ids = agendamentos.Select(a => a.AtendimentoId!.Value).ToList();

        var lancamentos = ids.Count == 0 ? [] : await repo.LancamentosDosAtendimentosAsync(ids, ct);
        var receita = lancamentos
            .Where(l => l.Tipo == TipoLancamento.Entrada && l.Status == StatusLancamento.Realizado && l.AtendimentoId is not null)
            .GroupBy(l => l.AtendimentoId!.Value)
            .ToDictionary(g => g.Key, g => (Valor: g.Sum(l => l.Valor), Deducoes: g.Sum(l => (l.ValorTaxa ?? 0m) + (l.ValorImposto ?? 0m))));

        var insumo = (await estoque.CustosDeSessaoAsync(inicio, fim, ct)).ToDictionary(c => c.AtendimentoId);
        var repassePorProfissional = (await repasse.CalcularAsync(inicio, fim, ct)).ToDictionary(r => r.ProfissionalId);

        var sessoes = new List<SessaoParaMargem>();
        foreach (var porProfissional in agendamentos.GroupBy(a => a.ProfissionalId))
        {
            var lista = porProfissional.ToList();
            var valorRepasse = porProfissional.Key is { } pid && repassePorProfissional.TryGetValue(pid, out var r) ? r.Valor : 0m;
            var rateio = MargemNaTela.RatearRepasse(valorRepasse,
                lista.Select(a => receita.TryGetValue(a.AtendimentoId!.Value, out var v) ? v.Valor : 0m).ToList());

            for (var i = 0; i < lista.Count; i++)
            {
                var a = lista[i];
                var id = a.AtendimentoId!.Value;
                var temReceita = receita.TryGetValue(id, out var rec);
                insumo.TryGetValue(id, out var custo);
                sessoes.Add(new SessaoParaMargem(
                    id,
                    a.Profissional?.Rotulo ?? "Sem profissional",
                    CatalogoModalidades.Nome(a.ModalidadeCodigo ?? a.ModalidadePrevista.ToString()),
                    a.Paciente?.ConvenioNome ?? "Sem convênio",
                    temReceita ? rec.Valor : 0m,
                    temReceita ? rec.Deducoes : 0m,
                    custo?.Custo ?? 0m,
                    custo is { Completo: false },
                    rateio[i],
                    temReceita));
            }
        }

        return MargemNaTela.Montar(inicio, fim, sessoes);
    }
}

/// <summary>A conta da margem, pura: o que a tela mostra sai daqui.</summary>
public static class MargemNaTela
{
    private static readonly System.Globalization.CultureInfo PtBr = new("pt-BR");

    /// <summary>"38%" — ou "—" quando não houve receita (0% diria que entrou e não sobrou nada).</summary>
    public static string Percentual(double? fracao) => fracao is { } f ? f.ToString("P0", PtBr) : "—";

    /// <summary>A frase de cima da tabela: quantas sessões entraram na conta e de que período.</summary>
    public static string ResumoDoPeriodo(MargemDoPeriodo m)
    {
        var t = m.Total;
        if (t.Sessoes == 0) return "Nenhuma sessão realizada no período.";
        var com = t.SessoesComReceita == 1 ? "1 sessão com receita" : $"{t.SessoesComReceita} sessões com receita";
        return t.SessoesComReceita == t.Sessoes
            ? $"{com} entre {m.Inicio:dd/MM} e {m.Fim:dd/MM}."
            : $"{com} de {t.Sessoes} realizadas entre {m.Inicio:dd/MM} e {m.Fim:dd/MM}.";
    }

    /// <summary>
    /// O que ficou FORA da margem, dito por extenso. Nulo quando nada ficou: frase que diz
    /// "zero sessões sem receita" em todo mês ensina a não ler o rodapé.
    /// </summary>
    public static string? ForaDaMargem(MargemDoPeriodo m)
    {
        var partes = new List<string>();
        if (m.SessoesSemReceita > 0)
            partes.Add(m.SessoesSemReceita == 1
                ? "1 sessão realizada ainda não tem receita lançada (guia a receber ou particular em aberto) e ficou fora da conta"
                : $"{m.SessoesSemReceita} sessões realizadas ainda não têm receita lançada (guia a receber ou particular em aberto) e ficaram fora da conta");
        if (m.SessoesComInsumoIncompleto > 0)
            partes.Add(m.SessoesComInsumoIncompleto == 1
                ? "1 sessão usou insumo sem custo cadastrado — a margem dela está um pouco acima da real"
                : $"{m.SessoesComInsumoIncompleto} sessões usaram insumo sem custo cadastrado — a margem delas está um pouco acima da real");
        return partes.Count == 0 ? null : string.Join(". ", partes) + ".";
    }

    /// <summary>
    /// Divide o repasse do profissional entre as sessões dele: proporcional à receita de cada
    /// uma (é como o repasse por percentual nasce) e, sem receita nenhuma, em partes iguais
    /// (o repasse por sessão). O resto dos centavos vai para a última, para a soma bater.
    /// </summary>
    public static IReadOnlyList<decimal> RatearRepasse(decimal total, IReadOnlyList<decimal> receitas)
    {
        if (receitas.Count == 0) return [];
        var soma = receitas.Sum();
        var partes = receitas
            .Select(r => soma > 0m ? Math.Round(total * r / soma, 2) : Math.Round(total / receitas.Count, 2))
            .ToArray();
        partes[^1] += total - partes.Sum();
        return partes;
    }

    public static MargemDoPeriodo Montar(DateOnly inicio, DateOnly fim, IReadOnlyList<SessaoParaMargem> sessoes)
    {
        // Só as sessões com receita entram na margem; as outras são contadas à parte.
        var comReceita = sessoes.Where(s => s.TemReceita).ToList();

        LinhaMargem Linha(string nome, IEnumerable<SessaoParaMargem> grupo)
        {
            var g = grupo.ToList();
            var r = g.Where(s => s.TemReceita).ToList();
            return new LinhaMargem(nome, g.Count, r.Count,
                r.Sum(s => s.Receita), r.Sum(s => s.Deducoes), r.Sum(s => s.Insumo), r.Sum(s => s.Repasse));
        }

        IReadOnlyList<LinhaMargem> Por(Func<SessaoParaMargem, string> chave) => sessoes
            .GroupBy(chave)
            .Select(g => Linha(g.Key, g))
            .OrderByDescending(l => l.Margem)
            .ThenBy(l => l.Nome, StringComparer.Create(new System.Globalization.CultureInfo("pt-BR"), false))
            .ToList();

        return new MargemDoPeriodo(inicio, fim,
            Linha("Clínica", sessoes),
            Por(s => s.Modalidade), Por(s => s.Profissional), Por(s => s.Pagador),
            sessoes.Count - comReceita.Count,
            comReceita.Count(s => s.InsumoIncompleto));
    }
}
