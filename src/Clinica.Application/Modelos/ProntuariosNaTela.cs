namespace Clinica.Application.Modelos;

/// <summary>
/// O que a tela de Prontuários do Consultório AFIRMA (out/2026, migrado do
/// <c>ProntuariosViewModel</c> WPF para morar onde o teste alcança). A situação de cada linha
/// continua decidida em <see cref="LinhaProntuario"/>; aqui ficam o recorte, a frase do resumo
/// e o rótulo do chip "Assinar pendentes".
/// </summary>
public static class ProntuariosNaTela
{
    /// <summary>A janela das ESCRITAS (evoluções e anamneses); o resumo DIZ o recorte.</summary>
    public const int JanelaDias = 90;

    /// <summary>Remonta DA MEMÓRIA — filtrar não vai ao banco.</summary>
    public static IReadOnlyList<LinhaProntuario> Filtrar(
        IReadOnlyList<LinhaProntuario> todas, string? termo, bool soAssinaturasPendentes)
    {
        IEnumerable<LinhaProntuario> visiveis = todas;
        if (soAssinaturasPendentes)
            visiveis = visiveis.Where(l => l.Situacao == SituacaoLinhaProntuario.AAssinar);
        if (!string.IsNullOrWhiteSpace(termo))
            visiveis = visiveis.Where(l => l.Paciente.Contains(termo.Trim(), StringComparison.CurrentCultureIgnoreCase));
        return visiveis.ToList();
    }

    public static bool FiltroAtivo(string? termo, bool soAssinaturasPendentes)
        => soAssinaturasPendentes || !string.IsNullOrWhiteSpace(termo);

    /// <summary>
    /// O resumo diz o RECORTE — filtro esquecido respondendo "tudo em dia" faria a lista mentir
    /// (a lição do painel de pendências).
    /// </summary>
    public static string Resumo(int total, int visiveis, bool filtroAtivo, DateOnly pendentesDesde)
    {
        var recorte = filtroAtivo ? $"{visiveis} de {total} registro(s)" : $"{total} registro(s)";
        return $"{recorte} · escritas dos últimos {JanelaDias} dias · sessões sem evolução desde {pendentesDesde:dd/MM/yyyy}";
    }

    /// <summary>"Assinar pendentes (3)" — a contagem sai da lista INTEIRA, não do recorte.</summary>
    public static string RotuloPendentes(IReadOnlyList<LinhaProntuario> todas)
    {
        var aAssinar = todas.Count(l => l.Situacao == SituacaoLinhaProntuario.AAssinar);
        return aAssinar > 0 ? $"Assinar pendentes ({aAssinar})" : "Assinar pendentes";
    }

    /// <summary>O tom do selo, pelo que a linha pede: ação é aviso, cancelada é erro.</summary>
    public static string Tom(LinhaProntuario linha)
        => linha.TomAviso ? "aviso" : linha.TomErro ? "erro" : linha.TomSucesso ? "sucesso" : linha.TomInfo ? "info" : string.Empty;
}
