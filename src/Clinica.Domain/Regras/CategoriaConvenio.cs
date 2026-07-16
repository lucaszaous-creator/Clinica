namespace Clinica.Domain.Regras;

/// <summary>
/// Categoria (semáforo) de base do paciente, derivada apenas do convênio + app —
/// independentemente da modalidade de um atendimento específico.
///
/// Reflete a capacidade do plano de gerar o 2º código:
/// - VERDE   = faz acu + eletro e o 2º código será obtido (Intercâmbio sempre; Padrão só com app).
/// - AMARELA = não haverá 2º código (Padrão sem app; Amil).
/// - VERMELHA= Petrobras (código de prioridade vermelho).
///
/// Usado no cadastro do paciente para que a categoria mude conforme o plano + app,
/// mantendo a coerência com o motor de regras (RegraUnimedPadrao/Intercambio/Amil/Petrobras).
/// </summary>
public static class CategoriaConvenio
{
    public static Categoria Base(Convenio convenio, bool possuiApp) => convenio switch
    {
        Convenio.Petrobras => Categoria.Vermelha,
        Convenio.Amil => Categoria.Amarela,
        Convenio.UnimedIntercambio => Categoria.Verde,
        Convenio.UnimedPadrao => possuiApp ? Categoria.Verde : Categoria.Amarela,
        _ => Categoria.Amarela
    };
}
