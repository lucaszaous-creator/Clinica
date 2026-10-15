using Clinica.Domain.Entities;

namespace Clinica.Application.Servicos;

/// <summary>O que a tela "Plano de contas" afirma, fora do Razor (out/2026).</summary>
public static class PlanoDeContasTela
{
    /// <summary>"8 categoria(s) · 3 de entrada · 5 de saída".</summary>
    public static string Resumo(IReadOnlyList<CategoriaFinanceira> categorias)
    {
        var entradas = categorias.Count(c => c.Tipo == TipoLancamento.Entrada);
        return $"{categorias.Count} categoria(s) · {entradas} de entrada · {categorias.Count - entradas} de saída";
    }
}
