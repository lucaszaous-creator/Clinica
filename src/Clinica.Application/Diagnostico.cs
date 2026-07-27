namespace Clinica.Application;

/// <summary>
/// Ponte de log para as camadas que não enxergam a UI (Application/Infrastructure).
/// O app liga o <see cref="Sink"/> ao arquivo de log na abertura; sem ninguém ligado
/// (testes, ferramentas de linha de comando) não faz nada.
///
/// Existe porque há degradações deliberadas fora da camada de tela — configuração
/// corrompida que volta ao padrão, por exemplo — e elas também precisam deixar rastro.
/// </summary>
public static class Diagnostico
{
    /// <summary>Destino das ocorrências. Definido uma vez, na abertura do app.</summary>
    public static Action<string, Exception>? Sink { get; set; }

    /// <summary>Registra uma ocorrência. Nunca lança.</summary>
    public static void Registrar(string contexto, Exception ex)
    {
        try
        {
            Sink?.Invoke(contexto, ex);
        }
        catch
        {
            // Log que falha não pode atrapalhar quem chamou.
        }
    }
}
