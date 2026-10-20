using Clinica.Domain;

namespace Clinica.Ui.Blazor.Hospedagem;

/// <summary>
/// O editor da prescrição de infusão — onde a folha é escrita, conferida contra as alergias e
/// assinada. Desde out/2026 (autorização da direção de 19/10/2026, só a camada de tela) ele é o
/// formulário Blazor <c>PrescricaoDeInfusaoForm</c>, aberto na própria página pela
/// <see cref="JanelasDaPrescricaoDeInfusaoNaPagina"/>; a assinatura continua chamando o MESMO
/// serviço congelado, na mesma ordem. A barreira de permissão é de quem chama.
/// </summary>
public interface IJanelasDaPrescricaoDeInfusao
{
    bool Disponivel { get; }

    /// <summary>Nova folha (ou a cópia da última, quando <paramref name="modelo"/> vem). Devolve se ASSINOU.</summary>
    Task<bool> NovaAsync(int pacienteId, string paciente, ModeloInfusao? modelo);

    /// <summary>Reabre o rascunho. Devolve se ASSINOU.</summary>
    Task<bool> EditarAsync(int pacienteId, string paciente, int prescricaoId);

    /// <summary>A prescrição ligada ao horário e à evolução da sessão (a folha do médico). Devolve se ASSINOU.</summary>
    Task<bool> PrescreverNaSessaoAsync(int pacienteId, string paciente, int? agendamentoId, int? evolucaoId);
}

public sealed class JanelasDaPrescricaoDeInfusaoIndisponiveis : IJanelasDaPrescricaoDeInfusao
{
    public static readonly JanelasDaPrescricaoDeInfusaoIndisponiveis Instancia = new();
    public const string Frase = "A prescrição de infusão é escrita no aplicativo do Consultório.";

    public bool Disponivel => false;
    public Task<bool> NovaAsync(int pacienteId, string paciente, ModeloInfusao? modelo) => Task.FromResult(false);
    public Task<bool> EditarAsync(int pacienteId, string paciente, int prescricaoId) => Task.FromResult(false);
    public Task<bool> PrescreverNaSessaoAsync(int pacienteId, string paciente, int? agendamentoId, int? evolucaoId) => Task.FromResult(false);
}
