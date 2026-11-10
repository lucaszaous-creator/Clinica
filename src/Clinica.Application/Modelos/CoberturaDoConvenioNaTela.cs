using Clinica.Domain.Regras;

namespace Clinica.Application.Modelos;

/// <summary>O que a tela de configuração ESCREVE sobre a cobertura de um convênio (nov/2026).</summary>
public static class CoberturaDoConvenioNaTela
{
    /// <summary>"não marca Acupuntura (apenas) · consulta só de Geriatria, Ginecologia, Psiquiatria · uma a cada 7 dia(s) por especialidade".</summary>
    public static string Resumo(CoberturaDoConvenio c)
    {
        var partes = new List<string>();
        if (c.ModalidadesNaoAtendidas.Count > 0)
            partes.Add("não marca " + string.Join(", ", c.ModalidadesNaoAtendidas.Select(m => CatalogoModalidades.Nome(m, default))));
        if (c.Especialidades.Count > 0)
            partes.Add("consulta só de " + string.Join(", ", c.Especialidades.Select(CatalogoEspecialidades.Nome)));
        if (c.IntervaloDias > 0) partes.Add($"uma a cada {c.IntervaloDias} dia(s) por especialidade");
        return partes.Count == 0 ? "Sem restrição." : "Hoje: " + string.Join(" · ", partes) + ".";
    }
}
