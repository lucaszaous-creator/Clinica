using System.Globalization;
using Clinica.Application.Formatacao;
using Clinica.Application.Servicos;
using Clinica.Domain.Entities;

namespace Clinica.Application.Modelos;

/// <summary>
/// O filtro da aba "A lançar" da Conciliação (parcela 56), com a regra que o
/// <c>ConciliacaoViewModel</c> do WPF aplicava — subiu para cá na migração para Blazor
/// (out/2026), onde o `dotnet test` alcança.
///
/// Filtra pela OPERADORA (o nome resolvido), e não pela família do enum: quem concilia tem
/// o demonstrativo de UMA operadora na mão, e filtrar por família juntaria "Sul América" e
/// "Unimed Costa do Sol", que respondem ao mesmo <c>Convenio.Personalizado</c>.
/// </summary>
public sealed record FiltroConciliacao(
    string Convenio = ConciliacaoNaTela.TodosConvenios,
    string Paciente = "",
    string Guia = "",
    bool SomenteSemValor = false)
{
    public static readonly FiltroConciliacao Nenhum = new();

    /// <summary>Algum filtro está valendo — acende o "Limpar" e muda o texto do vazio.</summary>
    public bool Ativo =>
        Convenio != ConciliacaoNaTela.TodosConvenios
        || !string.IsNullOrWhiteSpace(Paciente)
        || !string.IsNullOrWhiteSpace(Guia)
        || SomenteSemValor;

    /// <summary>
    /// A linha passa. O número da guia casa por TRECHO, o paciente sem acento e sem caixa,
    /// e "só as sem valor" deixa a fila de quem precisa DIGITAR (a tabela não propôs).
    /// </summary>
    public bool Casa(string convenioNome, string paciente, string numeroGuia, bool temValorProposto)
        => (Convenio == ConciliacaoNaTela.TodosConvenios || convenioNome == Convenio)
           && Busca.Casa(paciente, Paciente)
           && Busca.Casa(numeroGuia, Guia)
           && (!SomenteSemValor || !temValorProposto);
}

/// <summary>
/// O que a tela de CONCILIAÇÃO afirma — guias sem receita, receitas glosadas e sessões
/// particulares sem dinheiro (parcelas 27, 56 e set/2026). Subiu do <c>ConciliacaoViewModel</c>
/// para cá na migração para Blazor (out/2026).
///
/// As frases não são enfeite: "nenhuma guia esperando receita" e "nenhuma guia bate com o
/// filtro" pedem ações OPOSTAS, e um filtro esquecido que respondesse a primeira faria a
/// clínica dar o mês por conciliado com dezenas de guias pendentes.
/// </summary>
public static class ConciliacaoNaTela
{
    private static readonly CultureInfo PtBr = CultureInfo.GetCultureInfo("pt-BR");

    /// <summary>Rótulo da opção "não filtrar por convênio". Fica sempre no topo da lista.</summary>
    public const string TodosConvenios = "Todos os convênios";

    /// <summary>
    /// As operadoras presentes NO MÊS carregado — não o catálogo inteiro: convênio sem guia
    /// no mês só levaria a resultado vazio, que se lê como "não achei".
    /// </summary>
    public static IReadOnlyList<string> ConveniosDoMes(IEnumerable<string> nomes)
        => [TodosConvenios, .. nomes.Distinct().OrderBy(n => n, StringComparer.Create(PtBr, false))];

    /// <summary>
    /// A escolha anterior sobrevive à troca de mês quando a operadora ainda está lá — quem
    /// confere o demonstrativo da mesma operadora mês a mês não perde o filtro.
    /// </summary>
    public static string PreservarConvenio(string? escolhido, IReadOnlyList<string> opcoes)
        => escolhido is not null && opcoes.Contains(escolhido) ? escolhido : TodosConvenios;

    /// <summary>
    /// O resumo DIZ que a lista está filtrada: "12 guia(s)" e "12 de 53 guia(s)" respondem
    /// perguntas diferentes, e quem volta à tela depois do café não lembra o filtro.
    /// </summary>
    public static string ResumoDasGuias(int total, int visiveis, int semTabela, bool filtroAtivo)
    {
        if (total == 0) return "Nenhuma guia pendente de lançamento neste mês.";
        if (visiveis == 0) return $"Nenhuma das {total} guia(s) do mês bate com o filtro atual.";

        var quantas = filtroAtivo ? $"{visiveis} de {total} guia(s)" : $"{visiveis} guia(s)";
        return semTabela == 0
            ? $"{quantas} efetivada(s) sem receita lançada — valor proposto pela tabela."
            : semTabela == visiveis
                ? $"{quantas} efetivada(s) sem receita lançada. Sem tabela de preço cadastrada: informe o valor."
                : $"{quantas} efetivada(s) sem receita lançada · {semTabela} sem preço na tabela.";
    }

    public static string TituloDoVazio(bool filtroAtivo) => filtroAtivo
        ? "Nenhuma guia bate com o filtro"
        : "Nenhuma guia baixada esperando receita";

    public static string DescricaoDoVazio(bool filtroAtivo, int total) => filtroAtivo
        ? $"O mês tem {total} guia(s) pendente(s) — elas estão fora do filtro atual."
        : "A guia sai desta lista quando passa a TER receita, não quando alguém a marca.";

    /// <summary>
    /// O valor proposto como ele volta para o CAMPO de digitação ("145,5"); vazio quando a
    /// tabela não propôs — o sistema não inventa valor de mercado.
    /// </summary>
    public static string ValorDoCampo(PrecoProposto proposto)
        => proposto.Houve ? proposto.Valor.ToString("0.##", PtBr) : string.Empty;

    /// <summary>DE ONDE veio o valor; nulo quando não houve proposta.</summary>
    public static string? Procedencia(PrecoProposto proposto)
        => proposto.Houve ? proposto.Procedencia : null;

    /// <summary>A guia glosada aparece MARCADA, nunca sumindo — com o motivo escrito.</summary>
    public static string AvisoDeGlosa(GuiaSemLancamento g)
        => g.GlosaEmAberto
            ? $"Glosada em {g.DataGlosa?.ToString("dd/MM/yyyy", PtBr)}"
              + (string.IsNullOrWhiteSpace(g.MotivoGlosa) ? "" : $" — {g.MotivoGlosa}")
            : string.Empty;

    /// <summary>A pergunta antes de lançar receita de guia que o convênio recusou.</summary>
    public static string PerguntaDeGuiaGlosada(GuiaSemLancamento g)
        => $"{AvisoDeGlosa(g)}.\n\nO convênio recusou esta guia e ainda não a "
           + "aceitou de volta. Lançar a receita agora conta um dinheiro que foi "
           + "negado — se o recurso não for aceito, ele vira receita fantasma no "
           + "fluxo de caixa.\n\nLançar mesmo assim?";

    /// <summary>
    /// A retenção na fonte, calculada ANTES de gravar. Sem retenção cadastrada não se inventa
    /// desconto — mas a linha DIZ que não achou, senão líquido = bruto passaria por retenção zero.
    /// </summary>
    public static string Retencao(decimal valor, DeducoesRecebimento d)
        => d.ValorImposto is { } retido and > 0m
            ? $"retém {retido.ToString("C", PtBr)} ({d.DetalheImposto}) · líquido {(valor - retido).ToString("C", PtBr)}"
            : "sem retenção cadastrada para este convênio";

    public static string SucessoDaGuia(decimal valor, string paciente, DeducoesRecebimento d)
        => d.ValorImposto is { } retido and > 0m
            ? $"Receita de {valor.ToString("C", PtBr)} lançada para {paciente} — {retido.ToString("C", PtBr)} retidos na fonte."
            : $"Receita de {valor.ToString("C", PtBr)} lançada para {paciente}.";

    /// <summary>O valor digitado, ou a frase que diz por que não serve.</summary>
    public static string? LerValor(string? texto, out decimal valor)
        => LeituraDeValores.TentarLerDecimal(texto, out valor) && valor > 0
            ? null
            : "Informe um valor válido, maior que zero.";

    // ===================== Glosadas (o caminho de volta, parcela 27) =====================

    public static string MotivoDaGlosa(ReceitaGlosada r)
        => string.IsNullOrWhiteSpace(r.MotivoGlosa)
            ? (r.MotivoGlosaCodigo ?? "sem motivo registrado")
            : r.MotivoGlosa;

    public static string SituacaoDaReceita(ReceitaGlosada r)
        => r.AindaPrevisto ? "receita prevista" : "receita já realizada";

    /// <summary>Prazo de recurso, quando ainda corre — é o que decide a pressa.</summary>
    public static string PrazoDoRecurso(int? dias) => dias switch
    {
        null => "—",
        < 0 => $"recurso vencido há {-dias.Value} dia(s)",
        0 => "recurso vence hoje",
        _ => $"{dias} dia(s) para recorrer"
    };

    /// <summary>O que fazer quando o dinheiro já entrou — dito na própria linha.</summary>
    public static string? OrientacaoDaReceita(ReceitaGlosada r)
        => r.JaRealizado
            ? "O dinheiro desta guia já entrou. Se a operadora estornou, lance a devolução como saída, com a data do estorno."
            : null;

    public static string ResumoDasGlosadas(IReadOnlyList<ReceitaGlosada> receitas)
    {
        if (receitas.Count == 0) return "Nenhuma guia glosada com receita lançada neste mês.";
        var previstas = receitas.Where(r => r.AindaPrevisto).ToList();
        return previstas.Count == 0
            ? $"{receitas.Count} guia(s) glosada(s) — todas com o dinheiro já recebido."
            : $"{previstas.Count} de {receitas.Count} guia(s) glosada(s) ainda contam "
              + $"{previstas.Sum(r => r.Valor).ToString("C", PtBr)} de receita que o convênio recusou.";
    }

    public const string AvisoReceitaJaEntrou =
        "Esta guia foi glosada DEPOIS de o valor cair na conta. Cancelar a "
        + "entrada faria o caixa parar de bater com o extrato e levaria junto a "
        + "conferência do dia.\n\nSe a operadora estornou o valor, lance a "
        + "devolução como saída no Caixa — ela é outro fato, com a data do estorno.";

    public static string PerguntaDeDerrubar(ReceitaGlosada r)
        => $"A guia de {r.Paciente} ({r.Valor.ToString("C", PtBr)}) foi glosada em {r.DataGlosa.ToString("dd/MM/yyyy", PtBr)}. "
           + "Por que a receita está caindo? O lançamento NÃO é apagado — fica cancelado "
           + "com este motivo, e a guia volta para a aba \"A lançar\" caso o recurso seja "
           + "aceito.";

    /// <summary>
    /// A guia volta ao mês do ATENDIMENTO, não ao carregado: sem dizer o mês, quem cancela
    /// em setembro procura a guia de junho em setembro — e conclui que ela sumiu.
    /// </summary>
    public static string SucessoDeDerrubar(ReceitaGlosada r, DateOnly mesCarregado)
    {
        var voltouPara = r.DataAtendimento is { } a && (a.Year != mesCarregado.Year || a.Month != mesCarregado.Month)
            ? $" de {a.ToString("MMMM/yyyy", PtBr)} (mês do atendimento)"
            : string.Empty;
        return $"Receita de {r.Paciente} cancelada — a guia voltou para a conciliação{voltouPara}.";
    }

    // ===================== Particulares (set/2026) =====================

    /// <summary>Tudo menos "Convênio" — particular não é pago pela operadora.</summary>
    public static readonly IReadOnlyList<FormaPagamento> FormasDoParticular =
        Enum.GetValues<FormaPagamento>().Where(f => f != FormaPagamento.Convenio).ToList();

    /// <summary>A forma que a linha já traz marcada.</summary>
    public const FormaPagamento FormaPadrao = FormaPagamento.Pix;

    /// <summary>O "a receber" nasce vencendo em 30 dias.</summary>
    public static DateOnly VencimentoPadrao(DateOnly hoje) => hoje.AddDays(30);

    public static string ResumoDosParticulares(int total, int semPreco)
        => total == 0
            ? "Nenhuma sessão particular do mês sem dinheiro registrado."
            : semPreco == 0
                ? $"{total} sessão(ões) particular(es) sem dinheiro registrado — valor proposto pela tabela."
                : $"{total} sessão(ões) particular(es) sem dinheiro registrado · {semPreco} sem preço na tabela: informe o valor.";

    /// <summary>
    /// As três conferências antes de registrar o dinheiro da sessão: valor, e — recebido — a
    /// forma; a receber — um vencimento que não seja anterior à própria sessão.
    /// </summary>
    public static string? ConferirSessao(string? valorTexto, bool aReceber, FormaPagamento? forma,
        DateOnly? vencimento, DateOnly dataSessao, out decimal valor)
    {
        if (LerValor(valorTexto, out valor) is { } erro) return erro;
        if (!aReceber && forma is null) return "Diga como a sessão foi paga — ou marque \"a receber\".";
        if (aReceber && vencimento is null) return "Informe o vencimento da conta a receber.";
        if (aReceber && vencimento < dataSessao) return "O vencimento não pode ser anterior à sessão.";
        return null;
    }

    public static string SucessoDaSessao(string paciente, decimal valor, DateOnly? vencimento)
        => vencimento is { } v
            ? $"Sessão de {paciente} registrada como a receber ({valor.ToString("C", PtBr)}, vence {v.ToString("dd/MM/yyyy", PtBr)})."
            : $"Receita de {valor.ToString("C", PtBr)} lançada para {paciente}.";
}
