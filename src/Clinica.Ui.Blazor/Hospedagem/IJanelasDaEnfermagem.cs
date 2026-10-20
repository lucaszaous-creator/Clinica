namespace Clinica.Ui.Blazor.Hospedagem;

/// <summary>
/// O que a ENFERMAGEM em Blazor (out/2026 — a seção "Atendimento de enfermagem" da tela do paciente
/// e a tela Enfermagem) abre. Desde out/2026 (autorização da direção de 19/10/2026, só a camada de
/// tela) são formulários Blazor na própria página (<see cref="JanelasDaEnfermagemNaPagina"/>):
/// <list type="bullet">
/// <item><b>colher o termo</b> do procedimento do dia — a coleta da assinatura do paciente
/// (<see cref="ColetaDeTermoNaPagina"/>, o link do WhatsApp); aqui
/// ela recebe o modelo, o documento e o horário que a tela JÁ SABE, em vez de perguntar qual
/// termo (o <c>IDocumentosDaRecepcao.ColherTermoAsync</c> pergunta, e seria um clique a mais
/// sobre a pendência que a tela acabou de nomear);</item>
/// <item><b>registrar a infusão realizada</b> com orientação externa para o paciente aberto e
/// abrir a folha criada (a folha de execução é a área da 2ª assinatura).</item>
/// </list>
/// A folha de HOJE abre pela ponte da sala que já existe (<see cref="IJanelasDaSalaDeInfusao.AbrirFolhaAsync"/>).
/// As barreiras de permissão são de quem chama (a tela), como toda segunda barreira. Onde não
/// há WPF (a prévia, os testes) vale <see cref="JanelasDaEnfermagemIndisponiveis"/>, e a tela DIZ.
/// </summary>
public interface IJanelasDaEnfermagem
{
    bool Disponivel { get; }

    /// <summary>
    /// Abre a coleta do termo pendente. Volta quando a janela fecha (abrir já EMITE o termo
    /// numerado, então quem chama relê de qualquer forma). Falso = não abriu ou desistiu.
    /// </summary>
    Task<bool> ColherTermoAsync(int pacienteId, string paciente, int? modeloId, int? documentoId, int? agendamentoId);

    /// <summary>
    /// Registra a infusão realizada com orientação externa para ESTE paciente (o horário vai
    /// junto, quando há) e abre a folha criada. Falso = desistiu.
    /// </summary>
    Task<bool> RegistrarInfusaoExternaAsync(int pacienteId, string paciente, int? agendamentoId);
}

public sealed class JanelasDaEnfermagemIndisponiveis : IJanelasDaEnfermagem
{
    public static readonly JanelasDaEnfermagemIndisponiveis Instancia = new();

    public const string Frase = "Esta janela abre no aplicativo instalado (Consultório ou Recepção).";

    public bool Disponivel => false;

    public Task<bool> ColherTermoAsync(int pacienteId, string paciente, int? modeloId, int? documentoId, int? agendamentoId)
        => Task.FromResult(false);

    public Task<bool> RegistrarInfusaoExternaAsync(int pacienteId, string paciente, int? agendamentoId)
        => Task.FromResult(false);
}
