using System.Globalization;
using Clinica.Application.Servicos;
using Clinica.Domain.Entities;

namespace Clinica.Application.Modelos;

/// <summary>Um item da pauta: o alerta do painel, já com a porta que o resolve.</summary>
public sealed record ItemDaPauta(
    AssuntoDirecao Assunto,
    string Titulo,
    string Detalhe,
    GravidadeDirecao Gravidade,
    string Modulo,
    string ChaveDestino,
    string Acao);

/// <summary>
/// A régua do mês: o recebido, a meta e a projeção numa linha só. Sem meta definida, a régua
/// não inventa uma — mostra o recebido e a comparação com o mesmo trecho do mês anterior.
/// </summary>
public sealed record ReguaDoMes(
    decimal Recebido,
    decimal? Meta,
    decimal? Projecao,
    int Dia,
    int DiasNoMes,
    double? VariacaoMesAnterior)
{
    /// <summary>Quanto do mês já passou, de 0 a 1.</summary>
    public double FracaoDoMes => DiasNoMes <= 0 ? 0 : Math.Clamp((double)Dia / DiasNoMes, 0, 1);

    /// <summary>O recebido como fração da escala (a meta, ou a projeção quando ela passa da meta).</summary>
    public double FracaoRecebida => Escala is { } e && e > 0 ? Math.Clamp((double)(Recebido / e), 0, 1) : 0;

    public double? FracaoProjecao => Projecao is { } p && Escala is { } e && e > 0 ? Math.Clamp((double)(p / e), 0, 1) : null;

    /// <summary>Onde a meta cai na escala (1 quando a projeção não a ultrapassa).</summary>
    public double? FracaoMeta => Meta is { } m && Escala is { } e && e > 0 ? Math.Clamp((double)(m / e), 0, 1) : null;

    private decimal? Escala => Meta is { } m ? Math.Max(m, Projecao ?? 0m) : null;

    public bool TemMeta => Meta is > 0m;

    /// <summary>A frase do selo, ou nula quando não há o que dizer.</summary>
    public string? Selo
    {
        get
        {
            if (Meta is not { } meta || meta <= 0m) return null;
            if (Recebido >= meta) return "Meta do mês batida";
            if (Projecao is not { } p) return null; // antes do 5º dia não se projeta
            var diferenca = meta - p;
            return diferenca > 0m
                ? $"Projeção {MesaDaDirecaoNaTela.Dinheiro(diferenca)} abaixo da meta"
                : $"Projeção {MesaDaDirecaoNaTela.Dinheiro(-diferenca)} acima da meta";
        }
    }

    /// <summary>O selo é um alerta (âmbar) quando a projeção não alcança a meta.</summary>
    public bool SeloEmRisco => TemMeta && Recebido < Meta && Projecao is { } p && p < Meta;
}

/// <summary>A ocupação de um profissional hoje, para a lista "Hoje na agenda".</summary>
public sealed record OcupacaoNaMesa(string Profissional, double? Ocupacao, int Atendidos, int Marcados);

/// <summary>Um convênio na lista "De onde vem o dinheiro".</summary>
public sealed record ConvenioNaMesa(string Convenio, decimal Bruto, decimal Liquido)
{
    /// <summary>O que sobra depois da retenção, em fração do bruto. Nula sem bruto.</summary>
    public double? FracaoLiquida => Bruto > 0m ? (double)(Liquido / Bruto) : null;
}

/// <summary>
/// O que a MESA DA DIREÇÃO afirma (out/2026, mockup aprovado "mesa da direção v2"). Puro: a
/// tela só desenha. Ela substitui o painel WPF da direção e lê o MESMO
/// <see cref="PainelDirecaoService"/> — nenhum número é recalculado aqui.
/// </summary>
public static class MesaDaDirecaoNaTela
{
    private static readonly CultureInfo PtBr = new("pt-BR");

    public static string Dinheiro(decimal valor) =>
        valor.ToString("C0", PtBr);

    /// <summary>"Bom dia" / "Boa tarde" / "Boa noite" pela hora.</summary>
    public static string Saudacao(int hora) => hora switch
    {
        < 12 => "Bom dia",
        < 18 => "Boa tarde",
        _ => "Boa noite"
    };

    /// <summary>
    /// A régua do mês. A meta é a de FATURAMENTO da clínica inteira (sem profissional e sem
    /// recorte): é a que fala do mesmo número que a régua desenha.
    /// </summary>
    public static ReguaDoMes Regua(PainelDirecao p)
    {
        var meta = p.Metas.FirstOrDefault(m => m.Indicador == IndicadorMeta.Faturamento
                                               && m.ProfissionalId is null
                                               && string.IsNullOrEmpty(m.Segmento));
        var dias = DateTime.DaysInMonth(p.Hoje.Year, p.Hoje.Month);
        return new ReguaDoMes(
            Recebido: meta?.Realizado ?? p.EntradasMes,
            Meta: meta?.Alvo,
            Projecao: meta?.Projecao,
            Dia: p.Hoje.Day,
            DiasNoMes: dias,
            VariacaoMesAnterior: p.VariacaoEntradas);
    }

    /// <summary>
    /// A pauta: os alertas do painel, o que custa mais esperar primeiro (perigo, aviso, notícia),
    /// e dentro de cada gravidade a ordem em que o serviço os escreveu. Cada item leva a tela
    /// que resolve — alerta sem porta ensina a ignorá-lo.
    /// </summary>
    public static IReadOnlyList<ItemDaPauta> Pauta(PainelDirecao p) =>
        p.Alertas
            .Select((a, i) => (a, i))
            .OrderByDescending(x => x.a.Gravidade)
            .ThenBy(x => x.i)
            .Select(x =>
            {
                var (chave, acao, modulo) = Destino(x.a.Assunto);
                return new ItemDaPauta(x.a.Assunto, x.a.Titulo, x.a.Detalhe, x.a.Gravidade, modulo, chave, acao);
            })
            .ToList();

    /// <summary>
    /// Para onde cada assunto leva. As chaves são as do menu da suíte (<c>ChavesSuite</c> e as
    /// dos módulos); <c>MesaDaDirecaoTests</c> fixa cada uma.
    /// </summary>
    public static (string Chave, string Acao, string Modulo) Destino(AssuntoDirecao assunto) => assunto switch
    {
        AssuntoDirecao.EstoqueMinimo or AssuntoDirecao.EstoqueValidade => ("estoque", "Conferir estoque", "Financeiro"),
        AssuntoDirecao.PagamentosPendentes => ("conciliacao", "Conferir cobranças", "Financeiro"),
        AssuntoDirecao.BancoNaoConciliado => ("extrato-banco", "Conferir extrato", "Financeiro"),
        AssuntoDirecao.ContasVencidas => ("contas", "Ver contas", "Financeiro"),
        AssuntoDirecao.PacientesDevendo => ("inadimplencia", "Ver quem deve", "Financeiro"),
        AssuntoDirecao.DepositoAtrasado => ("recebiveis", "Ver recebíveis", "Financeiro"),
        AssuntoDirecao.CaixaNaoConferido => ("fechamento-caixa", "Fechar caixa", "Financeiro"),
        AssuntoDirecao.GuiasSemReceita => ("conciliacao", "Ver conciliação", "Financeiro"),
        AssuntoDirecao.ReceitaGlosada => ("conciliacao", "Ver glosadas", "Financeiro"),
        AssuntoDirecao.MetaEmRisco => ("metas", "Ver metas", "Desempenho"),
        AssuntoDirecao.OrcamentoEstourado => ("resultado", "Ver tetos", "Dinheiro"),
        AssuntoDirecao.ProntuarioEmAberto => ("consultorio-meu-dia", "Ver sessões", "Consultório"),
        AssuntoDirecao.InfusaoAguardando or AssuntoDirecao.InfusaoSemAssinatura
            => ("consultorio-sala-infusao", "Abrir a sala", "Enfermagem"),
        AssuntoDirecao.PacientesNaoVem => ("agenda-confirmacoes", "Ver quem não vem", "Recepção"),
        AssuntoDirecao.LembretesSemResposta => ("agenda-confirmacoes", "Ver confirmações", "Recepção"),
        AssuntoDirecao.PendenciasVencidas => ("faturamento-gerencial", "Ver pendências", "Faturamento"),
        AssuntoDirecao.GlosasVencidas or AssuntoDirecao.GlosasAVencer => ("faturamento-gerencial", "Ver glosas", "Faturamento"),
        _ => ("faturamento-gerencial", "Ver faturamento", "Faturamento")
    };

    /// <summary>A frase da variação contra o mesmo trecho do mês anterior. Nula sem base.</summary>
    public static string? FraseVariacao(double? variacao) => variacao switch
    {
        null => null,
        > 0.005 => $"{Math.Round(variacao.Value * 100):0}% acima do mesmo trecho do mês passado",
        < -0.005 => $"{Math.Round(-variacao.Value * 100):0}% abaixo do mesmo trecho do mês passado",
        _ => "igual ao mesmo trecho do mês passado"
    };

    /// <summary>
    /// A nota sob o resultado por COMPETÊNCIA na régua (etapa "Dinheiro", out/2026): quanto
    /// dele ainda está a receber e, quando houver, quanta produção do mês ainda não tem valor
    /// lançado — sem ela, o número pareceria completo. Leitura que falhou tem frase própria.
    /// </summary>
    public static string NotaDaCompetencia(ResultadoPorCompetencia? c)
    {
        if (c is null) return "não pôde ser lido agora";
        var nota = c.ReceitaAReceber > 0m
            ? $"inclui {Dinheiro(c.ReceitaAReceber)} a receber"
            : "nada do mês a receber";
        return c.ProducaoSemValor switch
        {
            null => $"{nota} · produção sem valor não conferida",
            0 => nota,
            1 => $"{nota} · 1 atendimento sem valor lançado",
            var n => $"{nota} · {n} atendimentos sem valor lançado"
        };
    }

    /// <summary>
    /// A ocupação de hoje por profissional, do mais cheio para o mais vazio. Quem não tem
    /// jornada declarada não tem ocupação (nula), e vai para o fim — sem inventar 0%.
    /// </summary>
    public static IReadOnlyList<OcupacaoNaMesa> AgendaDeHoje(IEnumerable<ProdutividadeProfissional> hoje) =>
        hoje.Where(p => p.ProfissionalId is not null && p.Agendados + p.Atendidos > 0)
            .Select(p => new OcupacaoNaMesa(p.Nome,
                p.MinutosDisponiveis > 0 ? Math.Clamp((double)p.MinutosOcupados / p.MinutosDisponiveis, 0, 1) : null,
                p.Atendidos, p.Agendados + p.Atendidos))
            .OrderByDescending(o => o.Ocupacao ?? -1)
            .ThenBy(o => o.Profissional, StringComparer.Create(PtBr, false))
            .ToList();

    /// <summary>Os convênios que mais trouxeram no mês, até <paramref name="maximo"/>.</summary>
    public static IReadOnlyList<ConvenioNaMesa> Convenios(IEnumerable<RentabilidadeConvenio> lista, int maximo = 5) =>
        lista.Where(r => r.Bruto > 0m)
            .OrderByDescending(r => r.Bruto)
            .Take(maximo)
            .Select(r => new ConvenioNaMesa(r.Convenio, r.Bruto, r.Liquido))
            .ToList();
}
