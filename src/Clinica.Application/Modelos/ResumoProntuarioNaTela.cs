using System.Globalization;
using Clinica.Domain.Entities;

namespace Clinica.Application.Modelos;

/// <summary>
/// O que o quick-view do prontuário AFIRMA (out/2026: subido do <c>ResumoProntuarioViewModel</c>
/// WPF quando o modal virou Blazor, <c>ResumoProntuarioForm</c>). Ler as últimas sessões, a
/// anamnese e os anexos sem sair da lista de Prontuários. Puro, com teste.
///
/// O "Assinar evolução" do mockup NÃO existe aqui de propósito: evolução não é assinável no
/// domínio. Quem tem assinatura de verdade é a ANAMNESE emitida, e o Assinar dela mora na
/// linha da lista.
/// </summary>
public static class ResumoProntuarioNaTela
{
    private static readonly CultureInfo PtBr = CultureInfo.GetCultureInfo("pt-BR");

    public const string AbaEvolucoes = "Evolucoes";
    public const string AbaAnamnese = "Anamnese";
    public const string AbaAnexos = "Anexos";

    /// <summary>A frase da leitura que falhou: o caminho que sobra é a tela do paciente.</summary>
    public const string FalhaDeLeitura = "Não foi possível carregar o prontuário — tente pelo botão \"Abrir prontuário completo\".";

    /// <summary>
    /// Uma sessão no modal: data, autor e o texto composto. O id é o da <c>Evolucao</c> (os ids
    /// são POR TABELA), e é ele que abre a sessão inteira.
    /// </summary>
    public sealed record EvolucaoResumida(int EvolucaoId, string Data, string Autor, string Texto);

    /// <summary>Um bloco da anamnese — só existe se foi escrito.</summary>
    public sealed record BlocoAnamnese(string Rotulo, string Texto);

    /// <summary>Aberto de uma linha de ANAMNESE, o modal já cai na aba dela.</summary>
    public static string AbaInicial(int? documentoAnamneseId) => documentoAnamneseId is null ? AbaEvolucoes : AbaAnamnese;

    /// <summary>As sessões da mais recente para a mais antiga.</summary>
    public static IReadOnlyList<EvolucaoResumida> Evolucoes(IEnumerable<Evolucao> evolucoes)
        => evolucoes.OrderByDescending(e => e.Data).ThenByDescending(e => e.Id)
            .Select(e => new EvolucaoResumida(
                e.Id,
                e.Data.ToString("dd/MM/yyyy", PtBr),
                e.Profissional?.Nome ?? e.CriadoPor ?? "sem autor registrado",
                ComporTexto(e)))
            .ToList();

    /// <summary>O plano é o ÚLTIMO escrito — a decisão vigente, não um histórico.</summary>
    public static string? PlanoTerapeutico(IEnumerable<Evolucao> evolucoes)
        => evolucoes.OrderByDescending(e => e.Data).ThenByDescending(e => e.Id)
            .Select(e => e.PlanoTerapeutico)
            .FirstOrDefault(p => !string.IsNullOrWhiteSpace(p));

    /// <summary>"F32 · M54.5"; nulo quando não há diagnóstico (a tela escreve "—").</summary>
    public static string? Diagnosticos(IReadOnlyCollection<string> ultimos)
        => ultimos.Count == 0 ? null : string.Join(" · ", ultimos);

    /// <summary>Os blocos da anamnese que foram escritos, na ordem da folha.</summary>
    public static IReadOnlyList<BlocoAnamnese> Anamnese(AnamnesePaciente? anamnese)
    {
        if (anamnese is null) return [];
        var blocos = new List<BlocoAnamnese>();
        void Adicionar(string rotulo, string? texto)
        {
            if (!string.IsNullOrWhiteSpace(texto)) blocos.Add(new BlocoAnamnese(rotulo, texto.Trim()));
        }
        Adicionar("Antecedentes pessoais", anamnese.AntecedentesPessoais);
        Adicionar("Antecedentes familiares", anamnese.AntecedentesFamiliares);
        Adicionar("Hábitos de vida", anamnese.HabitosDeVida);
        Adicionar("História obstétrica", anamnese.HistoriaObstetrica);
        Adicionar("Revisão de sistemas", anamnese.RevisaoDeSistemas);
        Adicionar("Observações", anamnese.Observacoes);
        return blocos;
    }

    /// <summary>
    /// O texto da entrada: a evolução escrita, e o que a sessão registrou quando não há texto
    /// (queixa, EVA e conduta) — entrada em branco não explica nada. Os outros campos da sessão
    /// se leem pelo "Abrir sessão".
    /// </summary>
    public static string ComporTexto(Evolucao e)
    {
        if (!string.IsNullOrWhiteSpace(e.TextoEvolucao)) return e.TextoEvolucao.Trim();

        var partes = new List<string>();
        if (!string.IsNullOrWhiteSpace(e.QueixaPrincipal)) partes.Add(e.QueixaPrincipal.Trim());
        if (e.TemParEva) partes.Add($"EVA {e.EvaAntes} → {e.EvaDepois}");
        else if (e.EvaAntes is { } eva) partes.Add($"EVA {eva}");
        if (!string.IsNullOrWhiteSpace(e.Conduta)) partes.Add(e.Conduta.Trim());
        return partes.Count > 0 ? string.Join(" · ", partes) : "(sessão sem texto)";
    }

    /// <summary>O nome do arquivo da 2ª via da anamnese (o mesmo saneamento das outras folhas da tela).</summary>
    public static string NomeDaSegundaVia(string paciente)
        => AtendimentoDoBalcaoNaTela.NomeDoArquivo($"Anamnese-{paciente}.pdf");
}
