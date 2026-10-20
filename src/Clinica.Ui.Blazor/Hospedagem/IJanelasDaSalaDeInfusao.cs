using Clinica.Application.Modelos;

namespace Clinica.Ui.Blazor.Hospedagem;

/// <summary>
/// O que a Sala de infusão (Blazor, out/2026) abre: a FOLHA DE EXECUÇÃO (onde se checa item a item
/// — e onde mora a 2ª assinatura), a evolução de enfermagem da folha e o registro da infusão com
/// orientação externa. Desde out/2026 (autorização da direção de 19/10/2026, só a camada de tela)
/// são formulários Blazor na própria página (<see cref="JanelasDaSalaDeInfusaoNaPagina"/>); a
/// interface ficou para as telas não mudarem o que pedem. A barreira de permissão é da tela que
/// chama. <see cref="JanelasDaSalaDeInfusaoIndisponiveis"/> segue para quem monta uma tela sem a
/// hospedagem (o caminho de baixo do <c>GetService</c>).
/// </summary>
public interface IJanelasDaSalaDeInfusao
{
    bool Disponivel { get; }

    /// <summary>Abre a folha de execução e volta quando ela fecha.</summary>
    Task AbrirFolhaAsync(int prescricaoId);

    /// <summary>A evolução de enfermagem amarrada à folha.</summary>
    Task AnotarAsync(LinhaSalaInfusao linha);

    /// <summary>
    /// Registra a infusão com orientação externa para o paciente que a tela já perguntou
    /// (<c>EscolherPacienteForm</c>) e abre a folha criada. Falso = a pessoa desistiu.
    /// </summary>
    Task<bool> RegistrarInfusaoExternaAsync(int pacienteId, string paciente);
}

public sealed class JanelasDaSalaDeInfusaoIndisponiveis : IJanelasDaSalaDeInfusao
{
    public static readonly JanelasDaSalaDeInfusaoIndisponiveis Instancia = new();
    public const string Frase = "A folha de execução abre no aplicativo.";

    public bool Disponivel => false;
    public Task AbrirFolhaAsync(int prescricaoId) => Task.CompletedTask;
    public Task AnotarAsync(LinhaSalaInfusao linha) => Task.CompletedTask;
    public Task<bool> RegistrarInfusaoExternaAsync(int pacienteId, string paciente) => Task.FromResult(false);
}
