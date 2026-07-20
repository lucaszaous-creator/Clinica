namespace Clinica.Domain;

/// <summary>Nome de exibição (pt-BR) de cada especialidade de consulta.</summary>
public static class EspecialidadeInfo
{
    public static string NomeExibicao(Especialidade especialidade) => especialidade switch
    {
        Especialidade.ClinicaDaDor => "Clínica da Dor",
        _ => especialidade.ToString()
    };
}
