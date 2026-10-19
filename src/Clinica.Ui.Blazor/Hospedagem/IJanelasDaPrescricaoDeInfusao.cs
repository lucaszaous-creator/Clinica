using Clinica.Domain;

namespace Clinica.Ui.Blazor.Hospedagem;

/// <summary>
/// O editor da prescrição de infusão (a <c>PrescricaoInternaWindow</c> do módulo Clínico), que
/// continua WPF: é onde a folha é escrita, conferida contra as alergias e assinada (área
/// congelada, só chamada). A tela Blazor pede ao hospedeiro; a barreira é de quem chama.
/// </summary>
public interface IJanelasDaPrescricaoDeInfusao
{
    bool Disponivel { get; }

    /// <summary>Nova folha (ou a cópia da última, quando <paramref name="modelo"/> vem). Devolve se ASSINOU.</summary>
    Task<bool> NovaAsync(int pacienteId, string paciente, ModeloInfusao? modelo);

    /// <summary>Reabre o rascunho. Devolve se ASSINOU.</summary>
    Task<bool> EditarAsync(int pacienteId, string paciente, int prescricaoId);
}

public sealed class JanelasDaPrescricaoDeInfusaoIndisponiveis : IJanelasDaPrescricaoDeInfusao
{
    public static readonly JanelasDaPrescricaoDeInfusaoIndisponiveis Instancia = new();
    public const string Frase = "A prescrição de infusão é escrita no aplicativo do Consultório.";

    public bool Disponivel => false;
    public Task<bool> NovaAsync(int pacienteId, string paciente, ModeloInfusao? modelo) => Task.FromResult(false);
    public Task<bool> EditarAsync(int pacienteId, string paciente, int prescricaoId) => Task.FromResult(false);
}
