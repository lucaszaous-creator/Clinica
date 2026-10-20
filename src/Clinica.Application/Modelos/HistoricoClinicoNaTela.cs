using Clinica.Domain.Entities;

namespace Clinica.Application.Modelos;

/// <summary>
/// O que a seção "Histórico" da tela do paciente afirma (out/2026: subido da
/// ProntuarioClinicoViewModel WPF na migração para Blazor): as linhas das sessões médicas, a
/// busca no texto (BuscaNoProntuario, a MESMA da Recepção) e a frase do resumo — que diz quando
/// a lista está filtrada.
/// </summary>
public static class HistoricoClinicoNaTela
{
    public sealed record Linha(
        int EvolucaoId, string Data, string Profissional, string Eva, string Queixa, string Conduta,
        string Evolucao, string? EvolucaoFormatada, string Orientacoes, string TextoParaCopia,
        int Anexos, int Correcoes)
    {
        public bool Retificada => Correcoes > 0;
        public bool TemAnexos => Anexos > 0;
        public string CorrecoesTexto => Correcoes switch { 0 => "Correções", 1 => "1 correção", _ => $"{Correcoes} correções" };
        public string AnexosTexto => Anexos switch { 0 => "Anexos", 1 => "1 anexo", _ => $"{Anexos} anexos" };
    }

    public static Linha LinhaDe(Evolucao e, int anexos, int correcoes, string paciente) => new(
        e.Id,
        e.Data.ToString("dd/MM/yyyy"),
        e.Profissional?.Nome ?? "—",
        e.TemParEva ? $"EVA {e.EvaAntes} → {e.EvaDepois}" : "EVA não medida",
        Texto(e.QueixaPrincipal),
        Texto(e.Conduta),
        Texto(e.TextoEvolucao),
        e.TextoEvolucaoFormatado,
        Texto(e.Orientacoes),
        SessaoDoProntuario.De(e, anexos, correcoes).TextoParaCopiar(paciente),
        anexos,
        correcoes);

    /// <summary>As linhas que casam com o termo (vazio = todas), na ordem do prontuário.</summary>
    public static IReadOnlyList<Linha> Filtrar(
        IReadOnlyList<Evolucao> sessoes, string? termo, IReadOnlyDictionary<int, int> anexos,
        IReadOnlyDictionary<int, int> correcoes, string paciente)
    {
        var t = termo?.Trim() ?? string.Empty;
        return sessoes
            .Where(e => t.Length == 0 || BuscaNoProntuario.Casa(e, t))
            .Select(e => LinhaDe(e, anexos.GetValueOrDefault(e.Id), correcoes.GetValueOrDefault(e.Id), paciente))
            .ToList();
    }

    public static string Resumo(int total, int visiveis, string? termo)
        => string.IsNullOrWhiteSpace(termo)
            ? $"{total} sessão(ões) no prontuário."
            : $"{visiveis} de {total} sessão(ões) contêm “{termo.Trim()}”.";

    private static string Texto(string? valor) => string.IsNullOrWhiteSpace(valor) ? "—" : valor;
}
