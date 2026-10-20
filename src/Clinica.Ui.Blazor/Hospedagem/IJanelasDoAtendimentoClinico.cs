using Clinica.Application.Modelos;
using Clinica.Domain.Entities;

namespace Clinica.Ui.Blazor.Hospedagem;

/// <summary>
/// O que a folha do médico (a seção Atendimento da tela do paciente, Blazor, out/2026) pede ao
/// aplicativo do Consultório e que continua WPF — o mesmo desenho das outras pontes:
///
/// - <b>A janela do papel ESCRITO com a herança da sessão</b> (<c>DocumentoWindow</c> com
///   <see cref="HerancaDaSessao"/>): ela resolve quem assina, os modelos, a regra do CID e a
///   conferência de alergia — reescrevê-la seria a segunda definição;
/// - <b>a prescrição de infusão</b> ligada ao horário e à evolução
///   (<c>PrescricaoInternaWindow</c>, área de assinatura — só chamada);
/// - <b>a coleta do termo</b> (<c>ColetaDeTermo</c> → <c>AssinaturaPacienteWindow</c>, área
///   congelada);
/// - <b>a pendência da conclusão automática</b>, que mora na Infrastructure (que esta
///   biblioteca não enxerga).
///
/// A barreira de permissão é da TELA (as duas barreiras moram lá); as janelas conferem de
/// novo, como sempre conferiram. Onde o Consultório não existe (a prévia, o bUnit) vale
/// <see cref="JanelasDoAtendimentoClinicoIndisponiveis"/>, e a tela DIZ que a ação é do
/// aplicativo — botão que não faz nada em silêncio é o defeito da parcela 41.
/// </summary>
public interface IJanelasDoAtendimentoClinico
{
    /// <summary>Falso onde as janelas não existem: a tela avisa em vez de abrir nada.</summary>
    bool Disponivel { get; }

    /// <summary>A janela do papel escrito, já com o que a sessão sabe. Verdadeiro = concluiu.</summary>
    Task<bool> EmitirAsync(int pacienteId, TipoDocumentoClinico tipo, HerancaDaSessao heranca);

    /// <summary>A prescrição de infusão ligada ao horário e à evolução da sessão. Verdadeiro = ASSINOU.</summary>
    Task<bool> PrescreverInfusaoAsync(int pacienteId, string paciente, int? agendamentoId, int? evolucaoId);

    /// <summary>
    /// A coleta do termo com o paciente na sala. Modelo nulo = a janela pergunta qual termo é
    /// (a coleta avulsa). Verdadeiro = concluiu; a tela relê de qualquer forma (abrir já emite).
    /// </summary>
    Task<bool> ColherTermoAsync(int pacienteId, string paciente, int? modeloId, int? documentoId,
        int? profissionalId, int? agendamentoId);

    /// <summary>O motivo pelo qual a conclusão automática ainda não concluiu este horário (nulo = nenhum).</summary>
    Task<string?> PendenciaDeConclusaoAutomaticaAsync(int agendamentoId);

    /// <summary>
    /// "Enviar…" do que saiu hoje (out/2026): a MESMA janela de envio das outras portas
    /// (<c>AcoesDoDocumento.EnviarAsync</c>) — e-mail primeiro, WhatsApp pelo CRM depois.
    /// </summary>
    Task<ResultadoDoAto> EnviarAoPacienteAsync(int documentoId);
}

/// <summary>Onde não há janelas WPF (a prévia, os testes): tudo responde "não disponível", e a tela diz.</summary>
public sealed class JanelasDoAtendimentoClinicoIndisponiveis : IJanelasDoAtendimentoClinico
{
    public static readonly JanelasDoAtendimentoClinicoIndisponiveis Instancia = new();

    public const string Frase = "Esta janela é do aplicativo do Consultório — abra o atendimento por lá.";

    public bool Disponivel => false;
    public Task<bool> EmitirAsync(int pacienteId, TipoDocumentoClinico tipo, HerancaDaSessao heranca) => Task.FromResult(false);
    public Task<bool> PrescreverInfusaoAsync(int pacienteId, string paciente, int? agendamentoId, int? evolucaoId) => Task.FromResult(false);
    public Task<bool> ColherTermoAsync(int pacienteId, string paciente, int? modeloId, int? documentoId,
        int? profissionalId, int? agendamentoId) => Task.FromResult(false);
    public Task<string?> PendenciaDeConclusaoAutomaticaAsync(int agendamentoId) => Task.FromResult<string?>(null);
    public Task<ResultadoDoAto> EnviarAoPacienteAsync(int documentoId) => Task.FromResult(new ResultadoDoAto(Frase: Frase, EhErro: true));
}
