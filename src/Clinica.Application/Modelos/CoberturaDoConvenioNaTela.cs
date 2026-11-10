using Clinica.Domain.Regras;

namespace Clinica.Application.Modelos;

/// <summary>O que a tela de configuração ESCREVE sobre a cobertura de um convênio (nov/2026).</summary>
public static class CoberturaDoConvenioNaTela
{
    /// <summary>"Só consulta · Geriatria, Ginecologia e Psiquiatria · uma a cada 7 dia(s) por especialidade".</summary>
    public static string Resumo(CoberturaDoConvenio c)
    {
        var partes = new List<string>();
        if (c.SoConsulta) partes.Add("Só consulta");
        if (c.Especialidades.Count > 0)
            partes.Add(string.Join(", ", c.Especialidades.Select(CatalogoEspecialidades.Nome)));
        if (c.IntervaloDias > 0) partes.Add($"uma a cada {c.IntervaloDias} dia(s) por especialidade");
        return partes.Count == 0 ? "Sem restrição." : "Hoje: " + string.Join(" · ", partes) + ".";
    }
}
