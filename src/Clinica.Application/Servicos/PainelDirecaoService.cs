using System.Globalization;
using Clinica.Application.Abstracoes;
using Clinica.Domain;
using Clinica.Domain.Entities;

namespace Clinica.Application.Servicos;

/// <summary>Quanto o assunto pesa: o que já é prejuízo, o que ainda dá para evitar, e o que é só notícia.</summary>
public enum GravidadeDirecao
{
    Info,
    Aviso,
    Perigo
}

/// <summary>
/// Assunto do alerta. É um enum e não um texto porque a TELA precisa saber para onde
/// mandar quem clica — e mapear destino a partir de uma frase seria adivinhação.
/// </summary>
public enum AssuntoDirecao
{
    ContasVencidas,
    PacientesDevendo,
    DepositoAtrasado,
    CaixaNaoConferido,
    GuiasSemReceita,

    /// <summary>
    /// Guia glosada que ainda conta receita no caixa (parcela 27) — dinheiro que a
    /// direção acha que vai entrar e que o convênio já recusou.
    /// </summary>
    ReceitaGlosada,

    /// <summary>
    /// O mês não vai alcançar a meta no ritmo atual (parcela 28). É o único alerta do
    /// painel que fala do FUTURO — os outros são coisas que já aconteceram.
    /// </summary>
    MetaEmRisco,

    /// <summary>
    /// Categoria que passou do teto de gasto do mês (parcela 31). Diferente da meta em
    /// risco: aqui o estouro JÁ aconteceu — não é previsão, é fato.
    /// </summary>
    OrcamentoEstourado,

    PendenciasVencidas,
    GlosasVencidas,
    GlosasAVencer
}

/// <summary>Uma coisa que a direção precisa resolver, já escrita como se fala.</summary>
public sealed record AlertaDirecao(
    AssuntoDirecao Assunto,
    string Titulo,
    string Detalhe,
    GravidadeDirecao Gravidade);

/// <summary>
/// O retrato da clínica hoje, na ordem em que a direção pergunta.
/// </summary>
public sealed record PainelDirecao(
    DateOnly Hoje,
    decimal EntradasMes,
    decimal SaidasMes,
    /// <summary>
    /// Entradas do MESMO TRECHO do mês anterior (dia 1 até o mesmo dia do mês).
    /// <c>null</c> = não há base de comparação, e aí não se desenha seta nenhuma.
    /// </summary>
    decimal? EntradasMesAnterior,
    ResumoContas Contas,
    ResumoRecebiveis Recebiveis,
    int DiasCaixaNaoConferido,
    decimal EspecieNaoConferida,
    int PacientesDevendo,
    decimal TotalDevidoPorPacientes,
    int GuiasSemReceita,

    /// <summary>Guias glosadas que ainda contam receita prevista, e quanto elas somam.</summary>
    int GuiasComReceitaGlosada,
    decimal ValorReceitaGlosada,

    /// <summary>
    /// As metas do mês com o realizado ao lado (parcela 28). Vazio = a direção não
    /// definiu alvo, e aí o painel volta a mostrar só a variação contra o mês anterior.
    /// </summary>
    IReadOnlyList<MetaApurada> Metas,

    int PendenciasVencidas,
    int PendenciasEmAberto,
    int GlosasVencidas,
    int GlosasAVencer,
    IReadOnlyList<AlertaDirecao> Alertas,
    /// <summary>
    /// Blocos cuja leitura FALHOU. Existe porque falha não pode aparecer como sucesso:
    /// um painel que mostra "nenhuma conta vencida" porque a consulta quebrou é pior do
    /// que um painel que não abre.
    /// </summary>
    IReadOnlyList<string> NaoVerificados)
{
    /// <summary>O que entrou menos o que saiu no mês, realizado. BRUTO, como o resto do sistema.</summary>
    public decimal SaldoMes => EntradasMes - SaidasMes;

    /// <summary>
    /// Variação das entradas contra o mesmo trecho do mês anterior, em fração
    /// (0,33 = +33%). <c>null</c> sem base — e sem base a tela não desenha seta.
    /// </summary>
    public double? VariacaoEntradas => EntradasMesAnterior is > 0m
        ? (double)((EntradasMes - EntradasMesAnterior.Value) / EntradasMesAnterior.Value)
        : null;

    /// <summary>Nada exige ação hoje.</summary>
    public bool SemAlerta => Alertas.Count == 0;

    /// <summary>Houve bloco que não pôde ser lido — a tela precisa dizer QUAL.</summary>
    public bool TemNaoVerificado => NaoVerificados.Count > 0;
}

/// <summary>
/// O painel da direção (parcela 22).
///
/// O Gerente Geral carrega os três módulos e, por isso, abria no primeiro item do
/// primeiro módulo: o **painel da RECEPÇÃO**. Quem manda na clínica entrava no sistema
/// e via a fila de espera do balcão — informação correta, dona errada. O resto da
/// resposta estava espalhado por sete telas, e ninguém abre sete telas toda manhã: o
/// caixa que não foi conferido, o depósito que a adquirente não fez, a conta que venceu
/// e a guia que passou do prazo ficavam invisíveis exatamente porque cada um morava numa
/// tela que só se abre quando já se desconfia do problema.
///
/// Este serviço junta as seis leituras numa só. Ele **não calcula nada de novo** — cada
/// número vem do serviço que já é dono dele —, e isso é deliberado: um painel que
/// recalcula por conta própria vira a segunda verdade sobre o mesmo dinheiro, e quando as
/// duas divergem ninguém sabe qual está certa.
///
/// Duas decisões que mudam o resultado:
///
/// 1. **A comparação é com o MESMO TRECHO do mês anterior**, não com o mês anterior
///    inteiro. No dia 5, comparar cinco dias contra trinta faria o mês parecer uma
///    catástrofe todo começo de mês — e a direção aprenderia a ignorar a seta, que é o
///    pior destino de um indicador.
/// 2. **Cada bloco falha sozinho.** Um erro na leitura das contas não pode derrubar o
///    painel inteiro nem, muito pior, aparecer como "nada vencido": o bloco entra em
///    <see cref="PainelDirecao.NaoVerificados"/> e a tela mostra o terceiro estado.
/// </summary>
public sealed class PainelDirecaoService
{
    /// <summary>Horizonte das contas e dos recebíveis: o que vence nos próximos 30 dias.</summary>
    private const int HorizonteDias = 30;

    /// <summary>
    /// Janela da conferência de caixa. Trinta dias e não "desde sempre": a clínica que
    /// nunca conferiu veria um número na casa das centenas e concluiria, com razão, que
    /// o alerta não é sobre ela.
    /// </summary>
    private const int JanelaConferenciaDias = 30;

    private readonly IClinicaRepositorio _repo;
    private readonly FinanceiroService _financeiro;
    private readonly ContasService _contas;
    private readonly RecebiveisService _recebiveis;
    private readonly FechamentoCaixaService _fechamento;
    private readonly RodadaPendenciasService _rodada;
    private readonly PendenciaService _pendencias;
    private readonly InadimplenciaService _inadimplencia;
    private readonly ReceitaGlosadaService _receitaGlosada;
    private readonly MetaService _metas;
    private readonly OrcamentoService _orcamentos;

    public PainelDirecaoService(
        IClinicaRepositorio repo,
        FinanceiroService financeiro,
        ContasService contas,
        RecebiveisService recebiveis,
        FechamentoCaixaService fechamento,
        RodadaPendenciasService rodada,
        PendenciaService pendencias,
        InadimplenciaService inadimplencia,
        ReceitaGlosadaService receitaGlosada,
        MetaService metas,
        OrcamentoService orcamentos)
    {
        _receitaGlosada = receitaGlosada;
        _metas = metas;
        _orcamentos = orcamentos;
        _repo = repo;
        _financeiro = financeiro;
        _contas = contas;
        _recebiveis = recebiveis;
        _fechamento = fechamento;
        _rodada = rodada;
        _pendencias = pendencias;
        _inadimplencia = inadimplencia;
    }

    public async Task<PainelDirecao> MontarAsync(DateOnly hoje, CancellationToken ct = default)
    {
        var naoVerificados = new List<string>();
        var alertas = new List<AlertaDirecao>();

        var inicioMes = new DateOnly(hoje.Year, hoje.Month, 1);

        // ---- Dinheiro do mês ----
        decimal entradas = 0m, saidas = 0m;
        decimal? entradasAnterior = null;
        try
        {
            var resumo = await _financeiro.ResumoAsync(inicioMes, hoje, ct);
            entradas = resumo.EntradasRealizadas;
            saidas = resumo.SaidasRealizadas;
            entradasAnterior = await EntradasDoTrechoAnteriorAsync(hoje, ct);
        }
        catch (Exception ex)
        {
            Diagnostico.Registrar("Painel da direção — caixa do mês não pôde ser lido", ex);
            naoVerificados.Add("Dinheiro do mês");
        }

        // ---- Contas A PAGAR vencidas ----
        //
        // Só o que a clínica DEVE. `ResumoContas.TotalVencido` soma os dois lados, e num
        // alerta isso daria um número sem significado: "R$ 8.000 vencidos" misturando o
        // aluguel que a clínica não pagou com a sessão que o paciente não pagou são dois
        // problemas opostos — um se resolve pagando, o outro cobrando.
        var contas = new ResumoContas(0m, 0m, 0m, 0m, 0);
        try
        {
            contas = await _contas.ResumoAsync(hoje, hoje.AddDays(HorizonteDias), ct);
            var aPagar = await _contas.VencidasAsync(hoje, TipoLancamento.Saida, ct);

            if (aPagar.Count > 0)
                alertas.Add(new AlertaDirecao(
                    AssuntoDirecao.ContasVencidas,
                    $"{aPagar.Count} conta(s) a pagar vencida(s)",
                    $"{Moeda(contas.APagarVencido)} já passou do vencimento e continua em aberto.",
                    GravidadeDirecao.Perigo));
        }
        catch (Exception ex)
        {
            Diagnostico.Registrar("Painel da direção — contas em aberto não puderam ser lidas", ex);
            naoVerificados.Add("Contas a pagar");
        }

        // ---- Paciente devendo ----
        var pacientesDevendo = 0;
        var totalDevido = 0m;
        try
        {
            var resumo = await _inadimplencia.ResumoAsync(hoje, ct);
            pacientesDevendo = resumo.Pacientes;
            totalDevido = resumo.Total;

            if (pacientesDevendo > 0)
            {
                // Dívida velha muda o assunto: até 90 dias é lembrete, acima disso é
                // decisão (acordo, ou parar de contar com o dinheiro). Marcar as duas com
                // a mesma cor faria a clínica tratar as duas do mesmo jeito.
                var velha = resumo.Faixas
                    .FirstOrDefault(f => f.Faixa == FaixasInadimplencia.Acima90);
                var temVelha = velha is { Pacientes: > 0 };

                alertas.Add(new AlertaDirecao(
                    AssuntoDirecao.PacientesDevendo,
                    $"{pacientesDevendo} paciente(s) devendo",
                    $"{Moeda(totalDevido)} em contas de paciente vencidas"
                    + (temVelha
                        ? $", das quais {Moeda(velha!.Valor)} com mais de 90 dias — aí já não é lembrete, é decisão."
                        : "."),
                    temVelha ? GravidadeDirecao.Perigo : GravidadeDirecao.Aviso));
            }
        }
        catch (Exception ex)
        {
            Diagnostico.Registrar("Painel da direção — inadimplência não pôde ser lida", ex);
            naoVerificados.Add("Inadimplência");
        }

        // ---- Depósito de cartão atrasado ----
        var recebiveis = new ResumoRecebiveis(0m, 0m, 0, null);
        try
        {
            recebiveis = await _recebiveis.ResumoAsync(hoje, hoje.AddDays(HorizonteDias), ct);
            if (recebiveis.TemAtraso)
                alertas.Add(new AlertaDirecao(
                    AssuntoDirecao.DepositoAtrasado,
                    $"{recebiveis.QuantidadeAtrasada} venda(s) sem depósito",
                    $"{Moeda(recebiveis.LiquidoAtrasado)} líquidos deveriam ter caído e não caíram. " +
                    "Dinheiro de cartão que não entrou não aparece em nenhuma outra tela.",
                    GravidadeDirecao.Perigo));
        }
        catch (Exception ex)
        {
            Diagnostico.Registrar("Painel da direção — recebíveis de cartão não puderam ser lidos", ex);
            naoVerificados.Add("Recebíveis de cartão");
        }

        // ---- Gaveta não conferida ----
        var diasCaixa = 0;
        var especieNaoConferida = 0m;
        try
        {
            var pendentes = await _fechamento.NaoConferidosAsync(
                hoje.AddDays(-JanelaConferenciaDias), hoje, ct);
            diasCaixa = pendentes.Count;
            especieNaoConferida = pendentes.Sum(d => d.Esperado);

            if (diasCaixa > 0)
                alertas.Add(new AlertaDirecao(
                    AssuntoDirecao.CaixaNaoConferido,
                    $"{diasCaixa} dia(s) sem conferir a gaveta",
                    $"{Moeda(especieNaoConferida)} em espécie passaram pelo caixa sem conferência " +
                    "nos últimos 30 dias.",
                    // Aviso e não perigo: a divergência ainda pode não existir. O que se
                    // perde com o tempo é a chance de descobrir de onde ela veio.
                    GravidadeDirecao.Aviso));
        }
        catch (Exception ex)
        {
            Diagnostico.Registrar("Painel da direção — fechamentos de caixa não puderam ser lidos", ex);
            naoVerificados.Add("Fechamento de caixa");
        }

        // ---- Guia baixada que não virou receita ----
        var guiasSemReceita = 0;
        try
        {
            // O período é o do MÊS, igual ao resto do painel: guia baixada há seis meses
            // e nunca lançada é problema de conciliação histórica, não da manhã de hoje.
            guiasSemReceita = (await _financeiro.GuiasSemLancamentoAsync(inicioMes, hoje, ct)).Count;
            if (guiasSemReceita > 0)
                alertas.Add(new AlertaDirecao(
                    AssuntoDirecao.GuiasSemReceita,
                    $"{guiasSemReceita} guia(s) sem receita no caixa",
                    "Foram efetivadas no convênio neste mês e o dinheiro delas não foi lançado.",
                    GravidadeDirecao.Aviso));
        }
        catch (Exception ex)
        {
            Diagnostico.Registrar("Painel da direção — conciliação de guias não pôde ser lida", ex);
            naoVerificados.Add("Guias sem receita");
        }

        // ---- Receita que a glosa derrubou e ninguém tirou do caixa (parcela 27) ----
        var guiasGlosadas = 0;
        var valorGlosado = 0m;
        try
        {
            (guiasGlosadas, valorGlosado) = await _receitaGlosada.ResumoPrevistoAsync(
                inicioMes, hoje, ct);

            if (guiasGlosadas > 0)
                alertas.Add(new AlertaDirecao(
                    AssuntoDirecao.ReceitaGlosada,
                    $"{Moeda(valorGlosado)} de receita glosada ainda no caixa",
                    $"{guiasGlosadas} guia(s) que o convênio recusou continuam contadas como "
                    + "receita prevista. Enquanto estiverem, o fluxo de caixa promete um "
                    + "dinheiro que foi negado.",
                    // Perigo, e não aviso: os outros alertas são coisas a fazer; este é um
                    // número ERRADO em outra tela. Direção que decide olhando previsão
                    // inflada decide errado sem saber que está decidindo errado.
                    GravidadeDirecao.Perigo));
        }
        catch (Exception ex)
        {
            Diagnostico.Registrar("Painel da direção — receita glosada não pôde ser lida", ex);
            naoVerificados.Add("Receita glosada");
        }

        // ---- Metas do mês (parcela 28) ----
        IReadOnlyList<MetaApurada> metas = [];
        try
        {
            metas = await _metas.ApurarMesAsync(hoje, ct);

            // Um alerta por meta em risco, e não um alerta somado: "2 metas em risco" não
            // diz qual, e a direção precisa saber se o problema é faturamento ou agenda —
            // são conversas diferentes, com pessoas diferentes.
            foreach (var meta in metas.Where(m => m.EmRisco))
                alertas.Add(new AlertaDirecao(
                    AssuntoDirecao.MetaEmRisco,
                    $"{meta.Rotulo}: no ritmo atual o mês fecha abaixo da meta",
                    meta.DesvioProjetado is { } desvio
                        ? $"Projeção de {desvio:0.#}% contra o alvo. Ainda dá para agir — "
                          + "no dia 31 já não dá."
                        : "A projeção do mês não alcança o alvo definido.",
                    // Aviso, não perigo: a meta ainda pode ser alcançada, e é justamente
                    // por isso que o alerta existe. Perigo aqui competiria com o dinheiro
                    // que JÁ está errado, e o painel perderia a hierarquia.
                    GravidadeDirecao.Aviso));
        }
        catch (Exception ex)
        {
            Diagnostico.Registrar("Painel da direção — metas não puderam ser apuradas", ex);
            naoVerificados.Add("Metas do mês");
        }

        // ---- Teto de gasto estourado (parcela 31) ----
        try
        {
            var tetos = await _orcamentos.ApurarMesAsync(hoje.Year, hoje.Month, ct);

            // Um alerta por categoria, e não um somado: "3 categorias estouraram" não diz
            // se o problema é material, aluguel ou repasse — e são três decisões
            // diferentes, com três interlocutores diferentes.
            foreach (var teto in tetos.Where(t => t.Estourou))
                alertas.Add(new AlertaDirecao(
                    AssuntoDirecao.OrcamentoEstourado,
                    $"{teto.Categoria} passou do teto do mês",
                    $"Teto de {Moeda(teto.Teto)}, gasto de {Moeda(teto.Gasto)} — "
                    + $"{Moeda(Math.Abs(teto.Saldo))} acima.",
                    GravidadeDirecao.Aviso));

            // O que ainda não estourou mas vai, pelo que já foi contratado. É o único
            // aviso que ainda dá para atender: depois de estourar, resta explicar.
            foreach (var teto in tetos.Where(t => t.VaiEstourar))
                alertas.Add(new AlertaDirecao(
                    AssuntoDirecao.OrcamentoEstourado,
                    $"{teto.Categoria} vai passar do teto",
                    teto.Projetado is { } projetado
                        ? $"O que já foi contratado leva a {Moeda(projetado)} contra um teto "
                          + $"de {Moeda(teto.Teto)}."
                        : "O que já foi contratado passa do teto do mês.",
                    GravidadeDirecao.Info));
        }
        catch (Exception ex)
        {
            Diagnostico.Registrar("Painel da direção — tetos de gasto não puderam ser lidos", ex);
            naoVerificados.Add("Teto de gasto");
        }

        // ---- Pendências de faturamento com prazo vencido ----
        int pendenciasVencidas = 0, pendenciasAbertas = 0;
        try
        {
            // Pelo serviço da rodada, que já resolve o prazo configurado e a carência de
            // 1ª execução. Recontar aqui daria um número diferente do que o faturamento
            // mostra, sobre a mesma guia.
            var status = await _rodada.ObterStatusAsync(hoje, ct);
            pendenciasVencidas = status.GuiasParaDecisao;
            pendenciasAbertas = status.GuiasPendentes;

            if (pendenciasVencidas > 0)
                alertas.Add(new AlertaDirecao(
                    AssuntoDirecao.PendenciasVencidas,
                    $"{pendenciasVencidas} guia(s) com prazo de decisão vencido",
                    $"Passaram de {status.PrazoDias} dias pendentes sem baixa nem não conformidade.",
                    GravidadeDirecao.Perigo));
        }
        catch (Exception ex)
        {
            Diagnostico.Registrar("Painel da direção — rodada de pendências não pôde ser lida", ex);
            naoVerificados.Add("Pendências de faturamento");
        }

        // ---- Glosa e o prazo de recurso ----
        int glosasVencidas = 0, glosasAVencer = 0;
        try
        {
            var glosas = await _pendencias.GlosasARecorrerAsync(hoje, ct);
            // DiasParaFimPrazo negativo = a data-limite de recurso já passou.
            glosasVencidas = glosas.Count(g => g.DiasParaFimPrazo < 0);
            glosasAVencer = glosas.Count - glosasVencidas;

            // Glosa fora do prazo e glosa dentro do prazo são alertas DIFERENTES: uma é
            // prejuízo consumado, a outra é dinheiro que ainda dá para buscar. Somar as
            // duas num número só faria perseguir o caso errado.
            if (glosasVencidas > 0)
                alertas.Add(new AlertaDirecao(
                    AssuntoDirecao.GlosasVencidas,
                    $"{glosasVencidas} glosa(s) com prazo de recurso vencido",
                    "Não há mais recurso a apresentar: é prejuízo consumado, e serve para " +
                    "entender por que aconteceu.",
                    GravidadeDirecao.Perigo));

            if (glosasAVencer > 0)
                alertas.Add(new AlertaDirecao(
                    AssuntoDirecao.GlosasAVencer,
                    $"{glosasAVencer} glosa(s) dentro do prazo de recurso",
                    "Ainda dá para recorrer. Passado o prazo, o valor não volta.",
                    GravidadeDirecao.Aviso));
        }
        catch (Exception ex)
        {
            Diagnostico.Registrar("Painel da direção — glosas não puderam ser lidas", ex);
            naoVerificados.Add("Glosas");
        }

        return new PainelDirecao(
            hoje,
            entradas, saidas, entradasAnterior,
            contas, recebiveis,
            diasCaixa, especieNaoConferida,
            pacientesDevendo, totalDevido,
            guiasSemReceita,
            guiasGlosadas, valorGlosado,
            metas,
            pendenciasVencidas, pendenciasAbertas,
            glosasVencidas, glosasAVencer,
            // Perigo primeiro. O painel é lido de cima para baixo, e a ordem é a única
            // forma de dizer o que vem antes sem escrever "prioridade 1" em cada linha.
            alertas.OrderByDescending(a => a.Gravidade).ThenBy(a => a.Assunto).ToList(),
            naoVerificados);
    }

    /// <summary>
    /// Entradas realizadas do dia 1 ao MESMO DIA do mês anterior.
    ///
    /// O recorte é o que torna a comparação honesta: no dia 5, comparar cinco dias deste
    /// mês contra o mês anterior fechado apontaria queda de 80% todo começo de mês.
    /// Quando o dia não existe no mês anterior (31 de março contra fevereiro), vale o
    /// último dia dele — o mês inteiro, que é o máximo comparável.
    ///
    /// Devolve <c>null</c> quando o mês anterior não tem NENHUM lançamento: zero e
    /// "não medido" são coisas diferentes, e uma clínica que começou a usar o sistema
    /// neste mês veria "+∞%" no lugar de "—".
    /// </summary>
    private async Task<decimal?> EntradasDoTrechoAnteriorAsync(DateOnly hoje, CancellationToken ct)
    {
        var inicioAnterior = new DateOnly(hoje.Year, hoje.Month, 1).AddMonths(-1);
        var ultimoDiaAnterior = DateTime.DaysInMonth(inicioAnterior.Year, inicioAnterior.Month);
        var fimAnterior = new DateOnly(
            inicioAnterior.Year, inicioAnterior.Month, Math.Min(hoje.Day, ultimoDiaAnterior));

        var valores = await _repo.ValoresDeLancamentoNoPeriodoAsync(inicioAnterior, fimAnterior, ct);
        if (valores.Count == 0) return null;

        return valores
            .Where(l => l.Tipo == TipoLancamento.Entrada && l.Status == StatusLancamento.Realizado)
            .Sum(l => l.Valor);
    }

    /// <summary>
    /// pt-BR FIXO, não a cultura da máquina: o texto do alerta é montado aqui e lido na
    /// tela, e dois postos com regionalização diferente escreveriam "R$ 1.200,00" e
    /// "$1,200.00" para o mesmo dinheiro.
    /// </summary>
    private static readonly CultureInfo Brasil = new("pt-BR");

    private static string Moeda(decimal valor) => valor.ToString("C2", Brasil);
}
