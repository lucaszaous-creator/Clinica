using Clinica.Application.Modelos;
using Clinica.Domain.Entities;

namespace Clinica.Ui.Blazor.Hospedagem;

/// <summary>
/// O que a folha do médico (a seção Atendimento da tela do paciente, Blazor, out/2026) pede ao
/// aplicativo do Consultório e que continua WPF — o mesmo desenho das outras pontes:
///
/// - (o documento escrito, a prescrição de infusão e a coleta do termo saíram daqui em out/2026:
///   viraram formulários Blazor — <c>DocumentoForm</c>,
///   <see cref="IJanelasDaPrescricaoDeInfusao.PrescreverNaSessaoAsync"/> e
///   <see cref="ColetaDeTermoNaPagina"/>; a escolha do convênio é o formulário de
///   <see cref="FormulariosDoPaciente"/>);
/// - <b>a pendência da conclusão automática</b>, que mora na Infrastructure (que esta
///   biblioteca não enxerga).
///
/// O papel ESCRITO (receita, atestado, laudo…) com a herança da sessão deixou de ser janela WPF
/// (out/2026): é o <c>DocumentoForm</c> Blazor, renderizado na própria seção.
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


    /// <summary>O motivo pelo qual a conclusão automática ainda não concluiu este horário (nulo = nenhum).</summary>
    Task<string?> PendenciaDeConclusaoAutomaticaAsync(int agendamentoId);
}

/// <summary>Onde não há janelas WPF (a prévia, os testes): tudo responde "não disponível", e a tela diz.</summary>
public sealed class JanelasDoAtendimentoClinicoIndisponiveis : IJanelasDoAtendimentoClinico
{
    public static readonly JanelasDoAtendimentoClinicoIndisponiveis Instancia = new();

    public const string Frase = "Esta janela é do aplicativo do Consultório — abra o atendimento por lá.";

    public bool Disponivel => false;
    public Task<string?> PendenciaDeConclusaoAutomaticaAsync(int agendamentoId) => Task.FromResult<string?>(null);
}
