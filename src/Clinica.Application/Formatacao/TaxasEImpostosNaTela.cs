using System.Globalization;
using Clinica.Application.Servicos;
using Clinica.Domain.Entities;

namespace Clinica.Application.Formatacao;

/// <summary>
/// O que a tela "Taxas e impostos" AFIRMA (out/2026, migrado do <c>TaxasViewModel</c> do
/// WPF para a tela Blazor). Mora aqui porque são decisões, não desenho — e a mais cara
/// delas é a da alíquota única.
///
/// ⚠️ <b>A faixa da alíquota única aparece com a MESMA condição do serviço</b>
/// (<see cref="TributoService.ApurarAsync"/>): ele cai no fallback quando
/// <see cref="TributoService.VigentesEmAsync"/> do dia — sem convênio — vem vazio. E esse
/// conjunto EXCLUI a retenção de convênio (que nunca vaza para recebimento que não é dele).
/// O WPF contava todo tributo "valendo agora", retenção inclusive: com só a retenção da
/// Unimed vigente, a tela dizia "a alíquota única está desligada" e escondia o campo,
/// enquanto o particular pago no balcão continuava sofrendo a alíquota única. Campo
/// invisível que faz efeito é o defeito que a parcela 69 já corrigiu nesta tela — pela
/// metade. A conta aqui é a do serviço, inteira.
/// </summary>
public static class TaxasEImpostosNaTela
{
    private static readonly CultureInfo PtBr = CultureInfo.GetCultureInfo("pt-BR");

    private static string Data(DateOnly d) => d.ToString("dd/MM/yyyy", PtBr);

    /// <summary>
    /// Lê um PERCENTUAL digitado ("3,2", "3.2", "0", "15"). Diferente de
    /// <see cref="LeituraDeValores.TentarLerDecimal"/>, que é para DINHEIRO e recusa o zero:
    /// a própria janela da taxa diz que "taxa zero também deve ser informada" (o contrato
    /// sem desconto precisa existir para o cartão do paciente poder ser lançado), e o WPF a
    /// recusava com "Informe o percentual". Zero passa; vazio, texto e fora de 0–100, não.
    /// </summary>
    public static bool TentarLerPercentual(string? texto, out decimal percentual)
    {
        percentual = 0m;
        var limpo = texto?.Trim().TrimEnd('%').Trim();
        if (string.IsNullOrEmpty(limpo)) return false;
        return LeituraDeValores.TentarLerNumeroExato(limpo, out percentual)
               && percentual is >= 0m and <= 100m;
    }

    // ==================== Taxas da maquininha ====================

    /// <summary>
    /// "Stone · todas as bandeiras · Crédito parcelado 2–6x — 4,2%". O mesmo texto de
    /// <see cref="TaxaCartao.Descricao"/>, mas em pt-BR FIXO: aquele usa a cultura do
    /// processo, e na prévia em Linux sairia "4.2%".
    /// </summary>
    public static string DescreverTaxa(TaxaCartao t)
    {
        var bandeira = string.IsNullOrWhiteSpace(t.Bandeira) ? "todas as bandeiras" : t.Bandeira!;
        var faixa = t.Modalidade == ModalidadeCartao.CreditoParcelado && t.ParcelasDe is not null
            ? $" {t.ParcelasDe}–{t.ParcelasAte ?? t.ParcelasDe}x"
            : string.Empty;
        return $"{t.Adquirente} · {bandeira} · {TaxaCartao.Rotular(t.Modalidade)}{faixa} — "
               + $"{t.Percentual.ToString("0.##", PtBr)}%";
    }

    public static string Prazo(TaxaCartao t)
        => t.LiquidacaoMensal
            ? $"mensal · primeiro em {t.DiasParaReceber} dia(s)"
            : $"integral · em {t.DiasParaReceber} dia(s)";

    public static string Vigencia(DateOnly? de, DateOnly? ate) => (de, ate) switch
    {
        (null, null) => "sem prazo",
        ({ } d, null) => $"desde {Data(d)}",
        (null, { } a) => $"até {Data(a)}",
        var (d, a) => $"{Data(d!.Value)} a {Data(a!.Value)}"
    };

    /// <summary>
    /// A situação da taxa HOJE. O WPF dizia só "Ativa"/"Inativa" — e a taxa ativa com a
    /// vigência vencida aparecia como "Ativa" sem descontar nada. A pergunta real é "esta
    /// linha está valendo?", e a resposta tem três estados.
    /// </summary>
    public static (string Rotulo, string Tom) SituacaoTaxa(TaxaCartao t, DateOnly hoje)
        => !t.Ativa ? ("Inativa", "neutro")
            : t.VigenteEm(hoje) ? ("Valendo agora", "sucesso")
            : t.VigenteDe is { } de && de > hoje ? ("Começa depois", "info")
            : ("Vigência encerrada", "aviso");

    // ==================== Regime tributário ====================

    /// <summary>
    /// Quem recolhe. Retido pela operadora → nome dela (do catálogo de convênios, nunca o
    /// código cru — o defeito da parcela 54); código fora do catálogo aparece como está.
    /// </summary>
    public static string Origem(Tributo t, IReadOnlyDictionary<string, string> nomesDosConvenios)
    {
        if (!t.RetidoNaFonte) return "recolhido pela clínica";
        var codigo = t.ConvenioCodigo!.Trim();
        var nome = nomesDosConvenios.TryGetValue(codigo, out var n) ? n : codigo;
        return $"retido na fonte por {nome}";
    }

    /// <summary>Vigente HOJE — é o que de fato está sendo descontado agora.</summary>
    public static bool ValendoAgora(Tributo t, DateOnly hoje) => t.VigenteEm(hoje);

    /// <summary>
    /// A alíquota única está ARMADA hoje? Exatamente quando o serviço cairia nela: nenhum
    /// tributo GERAL (sem convênio) vigente no dia.
    /// </summary>
    public static bool AliquotaUnicaValendo(IEnumerable<Tributo> catalogo, DateOnly hoje)
        => !catalogo.Any(t => !t.RetidoNaFonte && t.VigenteEm(hoje));

    /// <summary>"3,65%" ou "—" quando nada sai de cada recebimento.</summary>
    public static string CargaHoje(decimal carga)
        => carga > 0m ? $"{carga.ToString("0.####", PtBr)}%" : "—";

    /// <summary>
    /// A frase que explica QUAL mecanismo está valendo. Sem ela, quem cadastra o primeiro
    /// tributo não entende por que o campo da alíquota única parou de ter efeito — e quem só
    /// cadastrou retenção de convênio não entende por que ele CONTINUA tendo.
    /// </summary>
    public static string OrigemDaCarga(IReadOnlyCollection<Tributo> catalogo, decimal aliquotaUnica, DateOnly hoje)
    {
        var gerais = catalogo.Count(t => !t.RetidoNaFonte && t.VigenteEm(hoje));
        var retencoes = catalogo.Count(t => t.RetidoNaFonte && t.VigenteEm(hoje));
        var sobreRetencao = retencoes > 0
            ? $" {retencoes} retenção(ões) de convênio vigente(s) valem só para os recebimentos daquela operadora."
            : string.Empty;

        if (gerais > 0)
            return $"{gerais} tributo(s) vigente(s) hoje. A alíquota única está desligada." + sobreRetencao;

        if (catalogo.Count > 0)
            return (aliquotaUnica > 0m
                       ? $"Há {catalogo.Count} tributo(s) cadastrado(s), mas NENHUM tributo geral vigente hoje — "
                         + "vale a alíquota única abaixo. Confira as vigências."
                       : $"Há {catalogo.Count} tributo(s) cadastrado(s), mas nenhum tributo geral vigente hoje "
                         + "e nenhuma alíquota única: não se retém imposto. Confira as vigências.")
                   + sobreRetencao;

        return aliquotaUnica > 0m
            ? "Nenhum tributo cadastrado — vale a alíquota única abaixo."
            : "Nenhum tributo e nenhuma alíquota: não se retém imposto.";
    }

    /// <summary>A alíquota como volta para o campo: vazio é "não configurado", nunca "0".</summary>
    public static string? AliquotaParaCampo(decimal aliquota)
        => aliquota > 0m ? aliquota.ToString("0.##", PtBr) : null;

    // ==================== Simulador ====================

    /// <summary>
    /// A frase do simulador, a partir do MESMO <see cref="TaxaService.CalcularAsync"/> da
    /// venda real. <paramref name="semTaxa"/> diz que é cartão e não há taxa cadastrada:
    /// sem taxa não se inventa desconto, mas a tela DIZ que não achou — senão o líquido igual
    /// ao bruto passaria por taxa zero.
    /// </summary>
    public static string Simulacao(decimal bruto, DeducoesRecebimento d, bool ehCartao, out bool semTaxa)
    {
        string Din(decimal v) => v.ToString("C", PtBr);
        semTaxa = false;
        var partes = new List<string> { $"Bruto {Din(bruto)}" };

        if (d.ValorTaxa is { } taxa)
            partes.Add($"taxa {Din(taxa)} ({d.TaxaPercentual?.ToString("0.##", PtBr)}%)");
        else if (ehCartao)
        {
            partes.Add("sem taxa cadastrada para esta combinação");
            semTaxa = true;
        }

        if (d.ValorImposto is { } imposto)
            partes.Add($"imposto {Din(imposto)} ({d.DetalheImposto ?? $"{d.AliquotaImposto?.ToString("0.##", PtBr)}%"})");

        partes.Add($"→ líquido {Din(bruto - d.Total)}");

        if (d.PrevisaoRecebimento is { } previsao)
            partes.Add($"cai em {Data(previsao)}");

        return string.Join(" · ", partes);
    }

    // ==================== Apuração do mês ====================

    public static string ResumoApuracao(ApuracaoMensal r)
    {
        string Din(decimal v) => v.ToString("C", PtBr);
        return r.Vazia
            ? "Nenhum tributo vigente incidiu sobre os recebimentos deste mês."
            : $"Receita recebida {Din(r.ReceitaBruta)} · a recolher {Din(r.ARecolher)}"
              + (r.Retido > 0m ? $" · retido na fonte {Din(r.Retido)}" : string.Empty);
    }

    /// <summary>
    /// A divergência aparece em vez de ser escondida: significa que a regra mudou DEPOIS de
    /// o dinheiro entrar, e é a clínica que decide qual número vai para a guia. Null quando bate.
    /// </summary>
    public static string? Divergencia(ApuracaoMensal r)
    {
        if (!r.Diverge) return null;
        string Din(decimal v) => v.ToString("C", PtBr);
        return $"O apurado agora ({Din(r.TotalApurado)}) não bate com o que foi gravado nos "
               + $"lançamentos ({Din(r.TotalGravado)}) — diferença de {Din(r.Divergencia)}. "
               + "Isso acontece quando uma alíquota foi alterada depois de o dinheiro "
               + "entrar. Confira qual das duas regras vale para este mês.";
    }

    public static string AliquotaEfetiva(decimal aliquota) => $"{aliquota.ToString("0.####", PtBr)}%";
}
