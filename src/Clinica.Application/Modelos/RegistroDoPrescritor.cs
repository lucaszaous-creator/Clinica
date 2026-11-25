using System.Text.RegularExpressions;
using Clinica.Domain.Entities;
using Clinica.Domain.Regras;

namespace Clinica.Application.Modelos;

/// <summary>
/// O registro do prescritor como o PAPEL e os metadados o pedem: sigla do conselho ("CRM"), número e UF por SIGLA
/// ("RJ"). O cadastro guarda os campos TISS em CÓDIGO ("06" = CRM, "33" = RJ) — imprimi-los crus daria "06:" e
/// "UF: 33" no receituário. Sem os campos TISS, lê o texto livre de <see cref="Profissional.RegistroConselho"/>
/// ("CRM-RJ 123456", "CRM/RJ 52-0115854-6", "CRM 123456" — ou só o número, "52-0115854-6", com o conselho desconhecido).
/// </summary>
public sealed record RegistroDoPrescritor(string? Conselho, string? Numero, string? Uf, string? Especialidade)
{
    public bool EhMedico => string.Equals(Conselho, "CRM", StringComparison.OrdinalIgnoreCase);

    public static RegistroDoPrescritor De(Profissional? p)
    {
        if (p is null) return new(null, null, null, null);
        string? conselho = null, numero = null, uf = null;

        if (!string.IsNullOrWhiteSpace(p.ConselhoProfissional))
            conselho = DominiosTiss.Termo(DominiosTiss.ConselhoProfissional, p.ConselhoProfissional.Trim());
        if (!string.IsNullOrWhiteSpace(p.NumeroConselho)) numero = p.NumeroConselho.Trim();
        if (!string.IsNullOrWhiteSpace(p.UfConselho))
        {
            var u = p.UfConselho.Trim();
            uf = ReceituarioControleEspecialRegras.UfValida(u) ? u.ToUpperInvariant() : DominiosTiss.Termo(DominiosTiss.Uf, u);
            if (!ReceituarioControleEspecialRegras.UfValida(uf)) uf = null;
        }

        if ((conselho is null || numero is null || uf is null) && !string.IsNullOrWhiteSpace(p.RegistroConselho))
        {
            var m = Regex.Match(p.RegistroConselho.Trim(),
                @"^(?:(?<c>[A-Za-z]{2,6})(?:\s*[-/ ]\s*(?<uf>[A-Za-z]{2})(?![A-Za-z]))?\s*[-/:nº°. ]*\s*)?(?<n>\d[\d.\-/]*)\s*(?:[-/ ]\s*(?<uf2>[A-Za-z]{2}))?\s*$");
            if (m.Success)
            {
                if (m.Groups["c"].Success) conselho ??= m.Groups["c"].Value.ToUpperInvariant(); // só o número: conselho desconhecido
                numero ??= m.Groups["n"].Value;
                var lida = m.Groups["uf"].Success ? m.Groups["uf"].Value : m.Groups["uf2"].Success ? m.Groups["uf2"].Value : null;
                if (uf is null && ReceituarioControleEspecialRegras.UfValida(lida)) uf = lida!.ToUpperInvariant();
            }
        }

        var especialidade = string.IsNullOrWhiteSpace(p.EspecialidadeCodigo) ? null : CatalogoEspecialidades.Nome(p.EspecialidadeCodigo);
        return new(conselho, numero, uf, string.IsNullOrWhiteSpace(especialidade) ? null : especialidade);
    }
}
