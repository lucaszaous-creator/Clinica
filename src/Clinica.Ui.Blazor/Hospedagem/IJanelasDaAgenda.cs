using Clinica.Application.Servicos;

namespace Clinica.Ui.Blazor.Hospedagem;

/// <summary>
/// O que a Agenda do dia (e a Grade e a Visão geral) ainda pede ao WPF (out/2026): escolher o
/// convênio da ficha "a definir" (<c>VinculoDeConvenio</c>, a janela do shell) e a marcação de
/// um horário novo na aba Marcar. O resto virou Blazor: o horário é o
/// <c>HorarioDaAgendaForm</c>, editar é o <c>AgendamentoForm</c>, fechar a sessão é o
/// <c>FechamentoSessaoForm</c>, e a ficha do balcão é a tela <c>FichaDoBalcao</c>, aberta por
/// <see cref="PedidoDaFichaDoBalcao.AbrirAsync"/>.
///
/// A tela PEDE a quem a hospeda, como pede o disco ao
/// <see cref="IHostDaTela"/> e a marcação ao <see cref="IMarcacaoDeHorario"/>: a Recepção
/// registra a implementação WPF (<c>JanelasDaAgendaWpf</c>), que faz EXATAMENTE o que o
/// <c>FilaViewModel</c> fazia para abrir cada uma; onde ela não existe (a prévia, os testes)
/// vale <see cref="JanelasDaAgendaIndisponiveis"/>, e a tela DIZ que a ação é do aplicativo da
/// Recepção — botão que não faz nada em silêncio é o defeito da parcela 41.
///
/// A barreira de permissão é de QUEM CHAMA (a tela), como toda segunda barreira; a janela
/// confere de novo ao salvar, como sempre conferiu. Depois de a janela fechar, a tela relê.
/// </summary>
public interface IJanelasDaAgenda
{
    /// <summary>Falso onde as janelas não existem: a tela avisa em vez de abrir nada.</summary>
    bool Disponivel { get; }



    /// <summary>
    /// A ficha com convênio "a definir" PERGUNTA o convênio antes de concluir (parcela 92).
    /// Verdadeiro = a ficha tem convênio (escolhido agora); falso = desistiu.
    /// </summary>
    Task<bool> EscolherConvenioAsync(int pacienteId);


    /// <summary>
    /// "Marcar atendimento" neste dia, com o profissional do filtro já escolhido — a aba
    /// Marcar do Atendimento. Falso = a marcação não está disponível para este acesso.
    /// </summary>
    Task<bool> MarcarNovoHorarioAsync(DateOnly dia, int? profissionalId);
}

/// <summary>Onde não há janelas WPF (a prévia, os testes): tudo responde "não disponível", e a tela diz.</summary>
public sealed class JanelasDaAgendaIndisponiveis : IJanelasDaAgenda
{
    public static readonly JanelasDaAgendaIndisponiveis Instancia = new();

    public bool Disponivel => false;
    public Task<bool> EscolherConvenioAsync(int pacienteId) => Task.FromResult(false);
    public Task<bool> MarcarNovoHorarioAsync(DateOnly dia, int? profissionalId) => Task.FromResult(false);
}
