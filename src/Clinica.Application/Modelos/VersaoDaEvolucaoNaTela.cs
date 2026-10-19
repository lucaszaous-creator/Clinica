using Clinica.Domain.Entities;

namespace Clinica.Application.Modelos;

/// <summary>
/// Uma versão da sessão no histórico de CORREÇÕES (out/2026: subido da LinhaVersaoEvolucao do
/// shell WPF, para o Modal Blazor e a janela WPF lerem a mesma coisa). A versão atual primeiro,
/// as substituídas da mais nova para a mais antiga — nenhuma se apaga (Lei 13.787/2018).
/// </summary>
public sealed record VersaoDaEvolucaoNaTela(
    string Titulo,
    string Quando,
    string Motivo,
    string Queixa,
    string Anamnese,
    string Conduta,
    string Evolucao,
    string? EvolucaoFormatada,
    string Orientacoes,
    string Plano,
    string Eva,
    bool Vigente)
{
    public static VersaoDaEvolucaoNaTela De(VersaoEvolucao v) => new(
        $"Versão {v.Versao}",
        $"substituída em {v.SubstituidaEm:dd/MM/yyyy HH:mm}"
            + (string.IsNullOrWhiteSpace(v.SubstituidaPor) ? string.Empty : $" por {v.SubstituidaPor}"),
        string.IsNullOrWhiteSpace(v.Motivo) ? "Correção sem motivo informado." : v.Motivo,
        Texto(v.QueixaPrincipal),
        MontarAnamnese(v.HistoriaDoencaAtual, v.ExameFisico, v.HipoteseDiagnostica, v.CidSessao),
        Texto(v.Conduta),
        Texto(v.TextoEvolucao),
        v.TextoEvolucaoFormatado,
        Texto(v.Orientacoes),
        Texto(v.PlanoTerapeutico),
        v.EvaAntes is null && v.EvaDepois is null
            ? "EVA não medida"
            : $"EVA {v.EvaAntes?.ToString() ?? "—"} → {v.EvaDepois?.ToString() ?? "—"}",
        false);

    public static VersaoDaEvolucaoNaTela Atual(Evolucao e) => new(
        "Versão atual",
        e.AtualizadoEm is { } q
            ? $"corrigida pela última vez em {q:dd/MM/yyyy HH:mm}"
            : $"registrada em {e.CriadoEm:dd/MM/yyyy HH:mm}",
        "É o que está valendo no prontuário.",
        Texto(e.QueixaPrincipal),
        MontarAnamnese(e.HistoriaDoencaAtual, e.ExameFisico, e.HipoteseDiagnostica, e.CidSessao),
        Texto(e.Conduta),
        Texto(e.TextoEvolucao),
        e.TextoEvolucaoFormatado,
        Texto(e.Orientacoes),
        Texto(e.PlanoTerapeutico),
        e.TemParEva ? $"EVA {e.EvaAntes} → {e.EvaDepois}" : "EVA não medida",
        true);

    /// <summary>A lista do Modal: a atual primeiro, as anteriores da mais nova para a mais antiga.</summary>
    public static IReadOnlyList<VersaoDaEvolucaoNaTela> Lista(Evolucao? atual, IEnumerable<VersaoEvolucao> anteriores)
    {
        var lista = new List<VersaoDaEvolucaoNaTela>();
        if (atual is not null) lista.Add(Atual(atual));
        lista.AddRange(anteriores.OrderByDescending(v => v.Versao).Select(De));
        return lista;
    }

    public static string Resumo(int correcoes)
        => correcoes == 0
            ? "Esta sessão nunca foi corrigida — o que está escrito é o registro original."
            : $"{correcoes} correção(ões). Nenhuma versão é apagada: a Lei 13.787/2018 exige que a "
              + "retificação do prontuário seja rastreável.";

    private static string MontarAnamnese(string? hda, string? exame, string? hipotese, string? cid)
    {
        var partes = new List<string>();
        if (!string.IsNullOrWhiteSpace(hda)) partes.Add($"HDA: {hda.Trim()}");
        if (!string.IsNullOrWhiteSpace(exame)) partes.Add($"Exame físico: {exame.Trim()}");
        if (!string.IsNullOrWhiteSpace(hipotese))
            partes.Add($"Hipótese: {hipotese.Trim()}" + (string.IsNullOrWhiteSpace(cid) ? string.Empty : $" ({cid.Trim()})"));
        return string.Join("\n", partes);
    }

    private static string Texto(string? valor) => string.IsNullOrWhiteSpace(valor) ? "—" : valor;
}
