using System.Globalization;
using Clinica.Application.Formatacao;
using Clinica.Domain;
using Clinica.Domain.Entities;

namespace Clinica.Application.Servicos;

/// <summary>
/// O que a tela de ESTOQUE afirma (out/2026, migração do EstoqueViewModel do WPF para
/// Blazor). Cada decisão que o ViewModel tomava por conta própria — a frase do resumo, a
/// situação do lote, a sugestão de compra, o saldo depois de cada movimento, o custo
/// "parcial", quando o inventário pede lote e validade — mora aqui, pura e com teste:
/// projeto de tela não é alcançado pelo `dotnet test`, e regra que o teste não alcança
/// apodrece sem ninguém notar.
///
/// Número e dinheiro saem em pt-BR FIXO: a mesma tela roda no Windows da clínica e na
/// prévia em Linux, onde a cultura do processo é a invariante.
/// </summary>
public static class TelaDoEstoque
{
    private static readonly CultureInfo PtBr = CultureInfo.GetCultureInfo("pt-BR");

    /// <summary>"12,5" — quantidade com até três casas, sem zero à direita.</summary>
    public static string Quantidade(decimal q) => q.ToString("0.###", PtBr);

    /// <summary>"12,5 un".</summary>
    public static string QuantidadeComUnidade(decimal q, string unidade) => $"{Quantidade(q)} {unidade}";

    public static string Dinheiro(decimal valor) => valor.ToString("C", PtBr);

    // ==================== Lista de itens ====================

    /// <summary>Código · grupo · uso · local — o que identifica o item além do nome.</summary>
    public static string Cadastro(SaldoItemEstoque s)
        => string.Join(" · ", new[] { s.CodigoInterno, RotulosEnum.De(s.Grupo), RotulosEnum.De(s.Uso), s.LocalArmazenamento }
            .Where(x => !string.IsNullOrWhiteSpace(x)));

    /// <summary>Mínimo zero é "sem mínimo": "—", nunca "0 un" (que pareceria alerta configurado).</summary>
    public static string Minimo(SaldoItemEstoque s)
        => s.EstoqueMinimo > 0 ? QuantidadeComUnidade(s.EstoqueMinimo, s.Unidade) : "—";

    /// <summary>Custo médio sem entrada com custo é "—": o item não tem preço conhecido, não preço zero.</summary>
    public static string CustoMedio(SaldoItemEstoque s) => s.CustoMedio is { } c ? Dinheiro(c) : "—";

    /// <summary>A busca casa o nome, códigos, fabricante, apresentação, local, grupo e uso.</summary>
    public static bool CasaBusca(SaldoItemEstoque s, string? termo)
    {
        var t = termo?.Trim();
        return string.IsNullOrEmpty(t)
            || (s.TextoBusca + " " + Cadastro(s)).Contains(t, StringComparison.CurrentCultureIgnoreCase);
    }

    /// <summary>
    /// A frase do cabeçalho. A tela abre pelos ALERTAS: quem entra aqui quer saber o que
    /// está faltando e o que vai vencer.
    /// </summary>
    public static (string Texto, bool SemAlertas) Resumo(int itens, int abaixoDoMinimo, int lotesVencendo)
    {
        var sem = abaixoDoMinimo == 0 && lotesVencendo == 0;
        return (sem
            ? $"{itens} item(ns) no estoque — nada abaixo do mínimo nem vencendo."
            : $"{abaixoDoMinimo} item(ns) para repor · {lotesVencendo} lote(s) vencendo ou vencidos.", sem);
    }

    /// <summary>"VENCIDO" ou "vence em N dia(s)".</summary>
    public static string SituacaoDoLote(ValidadeProxima v, DateOnly hoje)
        => v.Vencido(hoje) ? "VENCIDO" : $"vence em {v.DiasRestantes(hoje)} dia(s)";

    /// <summary>
    /// Quanto comprar: até o MÁXIMO cadastrado, ou o DOBRO do mínimo quando não há máximo —
    /// repor só até o mínimo faz o item voltar ao alerta na semana seguinte. Nunca negativo.
    /// </summary>
    public static decimal SugestaoDeCompra(SaldoItemEstoque s)
        => Math.Max((s.EstoqueMaximo ?? s.EstoqueMinimo * 2) - s.Saldo, 0m);

    /// <summary>As linhas do CSV da lista de compras (o papel que vai ao fornecedor).</summary>
    public static IReadOnlyList<IReadOnlyList<string>> ListaDeCompras(IEnumerable<SaldoItemEstoque> faltando)
        => faltando.Select(s => (IReadOnlyList<string>)
        [
            s.Nome, s.Unidade, Quantidade(s.Saldo), Quantidade(s.EstoqueMinimo),
            Quantidade(SugestaoDeCompra(s)), CustoMedio(s)
        ]).ToList();

    public static readonly IReadOnlyList<string> CabecalhoListaDeCompras =
        ["Item", "Unidade", "Saldo", "Mínimo", "Sugestão de compra", "Custo médio"];

    // ==================== Inventário ====================

    /// <summary>O acerto pede o LOTE quando o item exige lote ou validade, ou é medicamento.</summary>
    public static bool InventarioPedeLote(ItemEstoque item)
        => item.ExigirLote || item.ExigirValidade || item.Grupo == GrupoEstoque.Medicamento;

    /// <summary>E a VALIDADE quando exige validade ou é medicamento.</summary>
    public static bool InventarioPedeValidade(ItemEstoque item)
        => item.ExigirValidade || item.Grupo == GrupoEstoque.Medicamento;

    /// <summary>A confirmação do acerto, ou a frase de "bateu" quando nada foi registrado.</summary>
    public static string ResultadoDoInventario(MovimentoEstoque? movimento)
        => movimento is null
            ? "A contagem bateu com o sistema — nada a ajustar."
            : movimento.AjusteParaCima == true
                ? $"Ajuste para cima: {Quantidade(movimento.Quantidade)} a mais no saldo."
                : $"Ajuste para baixo: {Quantidade(movimento.Quantidade)} a menos no saldo.";

    // ==================== Extrato ====================

    /// <summary>
    /// O extrato do item. Recebe os movimentos do mais RECENTE para o mais antigo (como o
    /// repositório devolve), acumula na ordem CRONOLÓGICA e devolve do mais recente — a
    /// primeira linha TEM de bater com o saldo da lista, porque as duas usam
    /// <see cref="MovimentoEstoque.Delta"/>.
    /// </summary>
    public static ExtratoDoItem Extrato(IReadOnlyList<MovimentoEstoque> maisRecentePrimeiro)
    {
        var cronologica = maisRecentePrimeiro.Reverse().ToList();
        var saldo = 0m;
        var saldos = new decimal[cronologica.Count];
        for (var i = 0; i < cronologica.Count; i++)
        {
            saldo += cronologica[i].Delta;
            saldos[i] = saldo;
        }

        var linhas = new List<LinhaDoExtrato>(cronologica.Count);
        for (var i = cronologica.Count - 1; i >= 0; i--)
        {
            var m = cronologica[i];
            linhas.Add(new LinhaDoExtrato(
                m.Data,
                RotulosEnum.De(m.Tipo),
                m.Delta >= 0 ? $"+{Quantidade(m.Delta)}" : $"−{Quantidade(-m.Delta)}",
                m.Delta >= 0,
                Quantidade(saldos[i]),
                Detalhe(m),
                string.IsNullOrWhiteSpace(m.CriadoPor) ? "—" : m.CriadoPor!,
                m.Tipo == TipoMovimentoEstoque.Perda));
        }

        var resumo = maisRecentePrimeiro.Count == 0
            ? "Nenhum movimento registrado."
            : $"{maisRecentePrimeiro.Count} movimento(s) · saldo atual {Quantidade(saldo)}";
        return new ExtratoDoItem(linhas, saldo, resumo);
    }

    /// <summary>
    /// O que a linha do extrato diz além do número: o MOTIVO da perda, o lote e a
    /// validade, a sessão que consumiu, o setor da rotina, fornecedor, documento, a
    /// conversão de embalagem, a compra vinculada e a direção do acerto.
    /// </summary>
    public static string Detalhe(MovimentoEstoque m)
    {
        var partes = new List<string>();
        if (!string.IsNullOrWhiteSpace(m.Observacao)) partes.Add(m.Observacao!);
        if (!string.IsNullOrWhiteSpace(m.Lote)) partes.Add($"lote {m.Lote}");
        if (m.Validade is { } v) partes.Add($"validade {v.ToString("dd/MM/yyyy", PtBr)}");
        if (m.AtendimentoId is not null) partes.Add($"consumo do atendimento #{m.AtendimentoId}");
        if (m.DestinoConsumo == DestinoConsumoEstoque.Rotina) partes.Add($"rotina · setor {m.SetorDestino}");
        if (!string.IsNullOrWhiteSpace(m.Fornecedor)) partes.Add($"fornecedor {m.Fornecedor}");
        if (!string.IsNullOrWhiteSpace(m.DocumentoEntrada)) partes.Add($"documento {m.DocumentoEntrada}");
        if (m.FatorConversao is > 1 or < 1)
            partes.Add($"{Quantidade(m.QuantidadeInformada ?? 0m)} {m.UnidadeInformada} × {Quantidade(m.FatorConversao.Value)} unidades");
        if (m.LancamentoFinanceiroId is { } conta) partes.Add($"compra vinculada à conta #{conta}");
        if (m.Tipo == TipoMovimentoEstoque.Ajuste)
            partes.Add(m.AjusteParaCima == true ? "contagem achou a mais" : "contagem achou a menos");
        return partes.Count == 0 ? "—" : string.Join(" · ", partes);
    }

    // ==================== Custo por sessão ====================

    /// <summary>
    /// Os três números e a frase do custo por sessão. "(parcial)" quando alguma sessão tem
    /// custo incompleto; média "—" quando não há base — período sem baixa por sessão não
    /// tem média zero, tem média NENHUMA.
    /// </summary>
    public static ResumoDoCusto ResumoDeCusto(ResumoCustoSessoes r)
    {
        var sem = r.Sessoes == 0;
        var total = r.SessoesComCustoIncompleto == 0 ? Dinheiro(r.Total) : $"{Dinheiro(r.Total)} (parcial)";
        var medio = r.MedioPorSessao is { } m ? Dinheiro(m) : "—";
        var frase = sem
            ? "Nenhuma sessão baixou insumo neste período. A baixa por sessão sai do fechamento do atendimento, na Recepção."
            : r.SessoesComCustoIncompleto > 0
                ? $"{r.SessoesComCustoIncompleto} sessão(ões) com custo incompleto. O total soma apenas os custos conhecidos; média e comparação aguardam custos completos."
                : $"{r.Sessoes} sessão(ões) com insumo · mais cara: {Dinheiro(r.MaisCara!.Custo)} em {r.MaisCara.Data.ToString("dd/MM/yyyy", PtBr)}.";
        return new ResumoDoCusto(r.Sessoes.ToString(PtBr), medio, total, frase, sem);
    }

    public static string PacienteDaSessao(CustoDeSessao s)
        => string.IsNullOrWhiteSpace(s.Paciente) ? "(paciente não informado)" : s.Paciente!;

    public static string CustoDaSessao(CustoDeSessao s)
        => s.Completo ? Dinheiro(s.Custo) : $"{Dinheiro(s.Custo)} (parcial)";

    public static string ItensDaSessao(CustoDeSessao s) => s.Itens.Count == 0 ? "—" : string.Join(" · ", s.Itens);

    // ==================== Materiais dos atendimentos ====================

    /// <summary>A frase da aba de materiais: desativado, ou desde quando e quantas baixas pendentes.</summary>
    public static string ResumoMateriais(PoliticaMateriais politica, IReadOnlyList<MateriaisDoAtendimento> lista)
        => politica.Modo == ModoMateriais.Desativado
            ? "Desativado. A rotina do atendimento continua como antes."
            : $"Ativo desde {politica.AtivadaEm?.ToString("dd/MM/yyyy HH:mm", PtBr)}. {lista.Count} atendimento(s) no período; "
              + $"{lista.Count(a => a.Situacao == "Baixa pendente")} baixa(s) pendente(s).";

    /// <summary>
    /// As linhas do registro de materiais de UMA sessão: os itens ativos de uso clínico
    /// (rotina fica de fora), por nome, e o que já foi registrado — pela conferência ou
    /// pelas baixas antigas — preenchido para conferência. Registrado de item que saiu da
    /// lista (inativo, de rotina) ganha linha própria com o nome que <paramref name="nomes"/>
    /// traz, para não sumir do que se confere.
    /// </summary>
    public static List<LinhaMaterialDaSessao> LinhasDosMateriais(
        IReadOnlyList<SaldoItemEstoque> saldosAtivos,
        IReadOnlyList<MaterialConsumidoProcedimento> registrados,
        IReadOnlyDictionary<int, string> nomes)
    {
        var linhas = saldosAtivos.Where(s => s.Uso != UsoEstoque.Rotina).OrderBy(s => s.Nome)
            .Select(s => new LinhaMaterialDaSessao
            {
                ItemId = s.ItemId,
                Nome = string.IsNullOrWhiteSpace(s.CodigoInterno) ? s.Nome : $"{s.CodigoInterno} · {s.Nome}",
                Saldo = $"Disponível: {QuantidadeComUnidade(s.Saldo, s.Unidade)}"
            }).ToList();

        foreach (var grupo in registrados.GroupBy(m => (m.ItemId, m.Lote)))
        {
            var linha = linhas.FirstOrDefault(l => l.ItemId == grupo.Key.ItemId && l.Quantidade is null);
            if (linha is null)
            {
                linha = new LinhaMaterialDaSessao
                {
                    ItemId = grupo.Key.ItemId,
                    Nome = nomes.TryGetValue(grupo.Key.ItemId, out var n) ? n : $"Item #{grupo.Key.ItemId}",
                    Saldo = "Já registrado nesta sessão"
                };
                linhas.Add(linha);
            }
            linha.Quantidade = Quantidade(grupo.Sum(m => m.Quantidade));
            linha.Lote = grupo.Key.Lote;
        }
        return linhas;
    }

    /// <summary>
    /// O pedido a gravar. Quantidade em branco = não usou; preenchida tem de ser positiva e
    /// com até três casas. Ou há material, ou a declaração explícita de que não houve
    /// consumo — nunca os dois, nunca nenhum.
    /// </summary>
    public static PedidoConsumoProcedimento PedidoDosMateriais(IEnumerable<LinhaMaterialDaSessao> linhas, bool semConsumo)
    {
        var materiais = new List<MaterialConsumidoProcedimento>();
        foreach (var l in linhas.Where(i => !string.IsNullOrWhiteSpace(i.Quantidade)))
        {
            if (!LeituraDeValores.TentarLerQuantidade(l.Quantidade, out var q) || q <= 0 || decimal.Round(q, 3) != q)
                throw new InvalidOperationException($"{l.Nome}: informe quantidade positiva com até três casas decimais, ou deixe em branco.");
            materiais.Add(new MaterialConsumidoProcedimento(l.ItemId, q, string.IsNullOrWhiteSpace(l.Lote) ? null : l.Lote.Trim()));
        }
        if (materiais.Count == 0 && !semConsumo || materiais.Count > 0 && semConsumo)
            throw new InvalidOperationException("Informe os materiais usados ou marque que não houve consumo.");
        return new PedidoConsumoProcedimento(materiais, semConsumo);
    }

    /// <summary>A situação de um registro já gravado: pendente com motivo, sem consumo ou baixado.</summary>
    public static string SituacaoDoRegistro(ConferenciaConsumoProcedimento r)
        => r.MotivoPendencia is { } motivo ? "Consumo registrado; baixa pendente. " + motivo
            : r.SemConsumo ? "Sem consumo declarado." : "Materiais registrados e baixados do estoque.";
}

/// <summary>Uma linha do extrato, já formatada.</summary>
public sealed record LinhaDoExtrato(
    DateOnly Data, string Tipo, string Movimento, bool Acrescimo, string SaldoApos,
    string Detalhe, string Quem, bool EhPerda);

/// <summary>O extrato inteiro: as linhas (mais recente primeiro), o saldo final e a frase.</summary>
public sealed record ExtratoDoItem(IReadOnlyList<LinhaDoExtrato> Linhas, decimal SaldoFinal, string Resumo);

/// <summary>Os números do custo por sessão, prontos para a tela.</summary>
public sealed record ResumoDoCusto(string Sessoes, string Medio, string Total, string Frase, bool SemCusto);

/// <summary>Uma linha do registro de materiais da sessão (o que se edita na tela).</summary>
public sealed class LinhaMaterialDaSessao
{
    public int ItemId { get; init; }
    public string Nome { get; init; } = string.Empty;
    public string Saldo { get; init; } = string.Empty;
    public string? Quantidade { get; set; }
    public string? Lote { get; set; }
}
