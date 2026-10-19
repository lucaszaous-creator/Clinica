using Clinica.Application.Modelos;

namespace Clinica.Ui.Blazor.Hospedagem;

/// <summary>
/// As janelas que a Sala de infusão (Blazor, out/2026) abre e que continuam WPF: a FOLHA DE
/// EXECUÇÃO (onde se checa item a item — e onde mora a 2ª assinatura, área congelada), a
/// evolução de enfermagem da folha e o registro da infusão com orientação externa. A barreira
/// de permissão é da tela que chama; a prévia responde com
/// <see cref="JanelasDaSalaDeInfusaoIndisponiveis"/>, e a tela DIZ que a ação é do aplicativo.
/// </summary>
public interface IJanelasDaSalaDeInfusao
{
    bool Disponivel { get; }

    /// <summary>Abre a folha de execução e volta quando ela fecha.</summary>
    Task AbrirFolhaAsync(int prescricaoId);

    /// <summary>A evolução de enfermagem amarrada à folha.</summary>
    Task AnotarAsync(LinhaSalaInfusao linha);

    /// <summary>
    /// Pergunta o paciente, registra a infusão com orientação externa e abre a folha criada.
    /// Falso = a pessoa desistiu em algum passo.
    /// </summary>
    Task<bool> RegistrarInfusaoExternaAsync();
}

public sealed class JanelasDaSalaDeInfusaoIndisponiveis : IJanelasDaSalaDeInfusao
{
    public static readonly JanelasDaSalaDeInfusaoIndisponiveis Instancia = new();
    public const string Frase = "A folha de execução abre no aplicativo.";

    public bool Disponivel => false;
    public Task AbrirFolhaAsync(int prescricaoId) => Task.CompletedTask;
    public Task AnotarAsync(LinhaSalaInfusao linha) => Task.CompletedTask;
    public Task<bool> RegistrarInfusaoExternaAsync() => Task.FromResult(false);
}
