using System.Globalization;
using Clinica.Application.Modelos;
using Clinica.Domain;
using Clinica.Domain.Entities;
using Clinica.Domain.Regras;

namespace Clinica.Application.Servicos;

/// <summary>
/// O recorte do painel de pendências do faturamento (out/2026): os quatro combos e as duas
/// duplas de chips. Saiu do <c>DashboardViewModel</c> WPF quando o painel virou Blazor —
/// o que decide a lista e o que a tela AFIRMA sobre ela mora onde o <c>dotnet test</c> alcança.
///
/// Cada dupla de chips é EXCLUSIVA (uma guia não é 1º e 2º ao mesmo tempo): marcar um
/// desmarca o irmão, e desmarcar os dois é "todas". Os métodos <c>Alternar*</c> devolvem o
/// recorte novo — a tela não escreve a regra da exclusividade.
/// </summary>
public sealed record FiltroPendencias
{
    public Convenio? Convenio { get; init; }
    public TipoCodigo? Tipo { get; init; }
    public ModalidadeAtendimento? Modalidade { get; init; }
    public Especialidade? Especialidade { get; init; }
    public bool SoPrimeiros { get; init; }
    public bool SoSegundos { get; init; }
    public bool SoAtrasadas { get; init; }
    public bool SoNoPrazo { get; init; }

    public static FiltroPendencias Nenhum { get; } = new();

    public FiltroPendencias AlternarPrimeiros() => this with { SoPrimeiros = !SoPrimeiros, SoSegundos = !SoPrimeiros ? false : SoSegundos };
    public FiltroPendencias AlternarSegundos() => this with { SoSegundos = !SoSegundos, SoPrimeiros = !SoSegundos ? false : SoPrimeiros };
    public FiltroPendencias AlternarAtrasadas() => this with { SoAtrasadas = !SoAtrasadas, SoNoPrazo = !SoAtrasadas ? false : SoNoPrazo };
    public FiltroPendencias AlternarNoPrazo() => this with { SoNoPrazo = !SoNoPrazo, SoAtrasadas = !SoNoPrazo ? false : SoAtrasadas };

    public IEnumerable<PendenciaCodigo> Aplicar(IEnumerable<PendenciaCodigo> todos)
    {
        var f = todos;
        if (Convenio is { } cv) f = f.Where(p => p.Convenio == cv);
        if (Tipo is { } tipo) f = f.Where(p => p.Tipo == tipo);
        if (Modalidade is { } mod) f = f.Where(p => p.Modalidade == mod);
        if (Especialidade is { } esp) f = f.Where(p => p.Especialidade == esp);
        if (SoPrimeiros) f = f.Where(p => p.Ordem == OrdemCodigo.Primeiro);
        if (SoSegundos) f = f.Where(p => p.Ordem == OrdemCodigo.Segundo);
        if (SoAtrasadas) f = f.Where(p => p.Urgencia == NivelUrgencia.Vermelho);
        if (SoNoPrazo) f = f.Where(p => p.Urgencia == NivelUrgencia.Amarelo);
        return f;
    }

    /// <summary>O recorte por extenso, na ordem: chips, depois combos.</summary>
    public IReadOnlyList<string> Partes()
    {
        var partes = new List<string>();
        if (SoPrimeiros) partes.Add("1º código");
        if (SoSegundos) partes.Add("2º código");
        if (SoAtrasadas) partes.Add("atrasadas");
        if (SoNoPrazo) partes.Add("no prazo");
        if (Tipo is { } tipo) partes.Add(RotulosEnum.De(tipo));
        if (Modalidade is { } mod) partes.Add(ModalidadeInfo.NomeExibicao(mod));
        if (Especialidade is { } esp) partes.Add(EspecialidadeInfo.NomeExibicao(esp));
        if (Convenio is { } cv) partes.Add(CatalogoConvenios.Nome(cv));
        return partes;
    }

    public bool Ativo => Partes().Count > 0;
}

/// <summary>Contagens do painel — SEMPRE sobre o total, nunca sobre o recorte (a distribuição antes do clique).</summary>
public sealed record ContagensPendencias(int Total, int Primeiros, int Segundos, int Atrasadas, int NoPrazo)
{
    /// <summary>
    /// A fatia da pilha que está no vermelho, para a barra do cartão "Urgentes". O denominador é
    /// o total de GUIAS (o mesmo conjunto de onde sai o urgente), nunca o badge — que soma
    /// consultas e glosas. Nula sem guia em aberto: ali não há proporção, e a barra some.
    /// </summary>
    public double? FracaoUrgentes => Total == 0 ? null : Math.Clamp((double)Atrasadas / Total, 0, 1);

    public static ContagensPendencias De(IReadOnlyCollection<PendenciaCodigo> todos) => new(
        todos.Count,
        todos.Count(p => p.Ordem == OrdemCodigo.Primeiro),
        todos.Count(p => p.Ordem == OrdemCodigo.Segundo),
        todos.Count(p => p.Urgencia == NivelUrgencia.Vermelho),
        todos.Count(p => p.Urgencia == NivelUrgencia.Amarelo));
}

/// <summary>
/// As frases do painel de pendências e a baixa em lote (out/2026). Cada uma era decidida no
/// <c>DashboardViewModel</c> WPF; aqui elas ficam testadas e servem à tela Blazor.
/// </summary>
public static class PainelPendenciasFaturamento
{
    private static readonly CultureInfo PtBr = CultureInfo.GetCultureInfo("pt-BR");

    /// <summary>
    /// A lista filtrada DIZ que está filtrada (parcela 45): "12 pendências" e "12 de 30 —
    /// 2º código · acupuntura" respondem perguntas diferentes.
    /// </summary>
    public static string ResumoDoRecorte(int total, int filtradas, FiltroPendencias filtro)
    {
        var partes = filtro.Partes();
        return partes.Count == 0
            ? (total == 1 ? "1 pendência" : $"{total} pendências")
            : $"{filtradas} de {total} pendência(s) — {string.Join(" · ", partes)}";
    }

    /// <summary>
    /// "Nenhuma pendência" e "nenhuma bate com o filtro" são respostas DIFERENTES: um filtro
    /// esquecido respondendo "tudo em dia" faria a clínica dar o dia por resolvido com o painel
    /// inteiro pendente (a lição da lista de espera, parcela 25).
    /// </summary>
    public static (string Titulo, string Descricao) Vazio(int total, FiltroPendencias filtro)
        => filtro.Ativo && total > 0
            ? ("Nenhuma pendência bate com o filtro", $"Há {total} pendência(s) fora deste recorte. Ajuste os filtros ou use Limpar.")
            : ("Nenhuma pendência", "Tudo em dia por aqui. Use Atualizar (F5) para recarregar.");

    /// <summary>O banner da rodada vencida (nulo = a rodada não exige decisão).</summary>
    public static string? BannerDaRodada(RodadaPendenciasStatus status) => status.ExigeDecisao
        ? $"{status.GuiasParaDecisao} guia(s) estão pendentes há mais de {status.PrazoDias} dias " +
          "sem resolução. Rode agora: cada uma exige baixa ou não conformidade."
        : null;

    /// <summary>
    /// A rodada NÃO pôde ser verificada — estado próprio, nunca "não venceu": a promessa do
    /// produto é não deixar guia esquecida, e silêncio por falha tem de aparecer como falha.
    /// </summary>
    public const string RodadaNaoVerificada =
        "Não foi possível verificar a rodada de pendências agora — pode haver guia vencida " +
        "sem aparecer aqui. Atualize o painel; se continuar, avise o suporte.";

    /// <summary>A frase de quem não pode decidir nada na rodada (nem baixa nem NC).</summary>
    public const string RodadaSemPermissao =
        "O seu acesso não permite dar baixa nem registrar não conformidade. Avise a direção da " +
        "clínica: as guias continuam pendentes até alguém com essa permissão rodar as pendências.";

    /// <summary>"Anotar" sem nota, "Ver/editar" com — percebe-se de relance quais guias já têm explicação.</summary>
    public static string RotuloDaObservacao(bool temObservacao) => temObservacao ? "Ver/editar" : "Anotar";

    /// <summary>
    /// Como o código se obtém, em palavras da clínica. O rótulo genérico do enum sairia "Nao aplica"
    /// sem acento e "Sistema" sem dizer de quem — e é a frase que diz à secretária o que fazer.
    /// </summary>
    public static string ComoObter(FormaObtencao forma) => forma switch
    {
        FormaObtencao.NaoAplica => "faturado no ato",
        FormaObtencao.App => "pelo app do paciente",
        FormaObtencao.Sistema => "no sistema do convênio",
        FormaObtencao.Ligacao => "ligando para o paciente",
        _ => RotulosEnum.De(forma)
    };

    // ==================== WhatsApp ====================

    /// <summary>A mensagem para obter a 1ª/2ª guia quando a ligação não deu certo.</summary>
    public static string MensagemDaGuia(PendenciaCodigo p)
    {
        var ordinal = p.Ordem == OrdemCodigo.Segundo ? "2ª" : "1ª";
        var texto = $"Olá, {PrimeiroNome(p.PacienteNome)}! Tentamos falar com você por telefone e não conseguimos. " +
                    $"Precisamos de um retorno rápido para concluir a autorização da {ordinal} guia do seu convênio, " +
                    $"referente ao atendimento de {p.DataPrevista.ToString("dd/MM", PtBr)}. " +
                    "Quando puder, é só responder por aqui, por favor.";
        if (p.FormaObtencao == FormaObtencao.App)
            texto += " Se o seu plano gera o código pelo aplicativo, pode nos enviar o QR Code por aqui mesmo.";
        return texto;
    }

    /// <summary>A mensagem para agendar a renovação da consulta a vencer.</summary>
    public static string MensagemDaConsulta(PendenciaConsulta c)
    {
        var prazo = c.DiasParaVencer <= 0 ? "está vencendo" : $"vence em {c.DiasParaVencer} dia(s)";
        return $"Olá, {PrimeiroNome(c.PacienteNome)}! Aqui é da clínica. A validade da sua consulta {prazo} " +
               $"(até {c.DataVencimento.ToString("dd/MM", PtBr)}). " +
               "Para não interromper o seu tratamento, vamos agendar o seu retorno? Responda por aqui, por favor.";
    }

    /// <summary>
    /// O endereço do wa.me com a mensagem assinada pela clínica, ou null quando o telefone do
    /// cadastro não serve (a MESMA regra de telefone da cobrança: 10 a 13 dígitos, DDI 55).
    /// </summary>
    public static string? LinkWhatsApp(string? telefone, string corpo, string? nomeClinica)
        => QuemMeDeveTela.LinkWhatsApp(telefone,
            corpo + (string.IsNullOrWhiteSpace(nomeClinica) ? string.Empty : $" — {nomeClinica}"));

    /// <summary>A frase de quem não tem telefone utilizável — e onde corrigir.</summary>
    public static string SemTelefone(string pacienteNome)
        => $"{pacienteNome}: telefone ausente ou inválido no cadastro (edite em Pacientes).";

    private static string PrimeiroNome(string nome)
        => nome.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? nome;

    // ==================== Baixa em lote ====================

    /// <summary>
    /// Critica os números do lote ANTES de processar (nulo = pode). O serviço recusa cada guia
    /// de qualquer jeito, mas aqui a recusa chegaria NO MEIO do lote, com as anteriores já
    /// baixadas. Linha em branco é ignorada: "ainda não tenho o número" é resposta legítima.
    /// </summary>
    public static string? ImpedimentoDoLote(IReadOnlyList<LinhaDaRodada> linhas)
    {
        var recusadas = linhas.Where(l => l.CriticaDoNumero is not null).ToList();
        return recusadas.Count == 0
            ? null
            : $"{recusadas.Count} guia(s) com o número fora do formato do convênio. "
              + "Corrija antes de confirmar — nenhuma baixa foi feita.\n\n"
              + string.Join("\n", recusadas.Select(l => $"• {l.Descricao}: {l.CriticaDoNumero}"));
    }

    /// <summary>
    /// Baixa linha a linha as que têm número. Falha POR LINHA: uma exceção no meio (o outro posto
    /// baixou a mesma guia; o formato recusou) não aborta as seguintes nem passa em silêncio.
    /// </summary>
    public static async Task<(int Feitas, IReadOnlyList<string> Falhas)> BaixarLoteAsync(
        IReadOnlyList<LinhaDaRodada> linhas, DateOnly dataBaixa, FaturamentoService faturamento,
        CancellationToken ct = default)
    {
        var feitas = 0;
        var falhas = new List<string>();
        foreach (var l in linhas.Where(l => !string.IsNullOrWhiteSpace(l.NumeroGuia)))
        {
            try
            {
                SessaoUsuario.Atual.Exigir(Permissao.BaixarGuia, "dar baixa nas guias");
                await faturamento.DarBaixaAsync(l.CodigoId, dataBaixa, l.NumeroGuia!.Trim(),
                    SessaoUsuario.Atual.Operador, "baixa em lote", ct);
                feitas++;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                Diagnostico.Registrar($"Baixa em lote — guia do código {l.CodigoId}", ex);
                falhas.Add($"{l.Descricao}: {ex.Message}");
            }
        }
        return (feitas, falhas);
    }

    /// <summary>O desfecho do lote (nulo = tudo baixado e nada a dizer além da confirmação).</summary>
    public static string? AvisoDoLote(int feitas, IReadOnlyList<string> falhas)
        => falhas.Count > 0
            ? $"{feitas} guia(s) baixada(s); {falhas.Count} NÃO:\n\n" + string.Join("\n", falhas)
            : feitas == 0 ? "Nenhuma linha tinha número de guia — nada foi baixado." : null;
}
