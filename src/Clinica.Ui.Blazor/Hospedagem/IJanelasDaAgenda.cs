using Clinica.Application.Servicos;

namespace Clinica.Ui.Blazor.Hospedagem;

/// <summary>
/// As JANELAS que a Agenda do dia abre e que ainda são WPF (out/2026 — a tela virou Blazor,
/// as janelas não): editar o horário (<c>AgendamentoWindow</c>), fechar a sessão
/// (<c>FechamentoSessaoWindow</c>), a ficha do balcão (<c>FichaBalcaoView</c>), a janela do
/// horário (remarcar, reabrir, comprovante, WhatsApp) e a marcação de um horário novo. (A
/// escolha do convênio da ficha "a definir" saiu daqui em out/2026: é o formulário Blazor de
/// <see cref="FormulariosDoPaciente"/>.)
///
/// Reescrevê-las agora seria a segunda definição de cada uma — a janela de fechamento sozinha
/// carrega pacote, insumo e caixa. A tela PEDE a quem a hospeda, como pede o disco ao
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

    /// <summary>A ficha do BALCÃO (a mesma da tela Pacientes), por cima da agenda. Falso = indisponível.</summary>
    Task<bool> AbrirFichaAsync(int pacienteId, string paciente);


    /// <summary>
    /// "Marcar atendimento" neste dia, com o profissional do filtro já escolhido — a aba
    /// Marcar do Atendimento. Falso = a marcação não está disponível para este acesso.
    /// </summary>
    Task<bool> MarcarNovoHorarioAsync(DateOnly dia, int? profissionalId);
}

/// <summary>O que a janela do horário devolveu: achou o horário no dia, e a frase que deixou.</summary>
public sealed record DesfechoDaJanelaDoHorario(bool Achou, string? Mensagem = null, bool MensagemEhErro = false)
{
    public static readonly DesfechoDaJanelaDoHorario Indisponivel = new(false);
}

/// <summary>Onde não há janelas WPF (a prévia, os testes): tudo responde "não disponível", e a tela diz.</summary>
public sealed class JanelasDaAgendaIndisponiveis : IJanelasDaAgenda
{
    public static readonly JanelasDaAgendaIndisponiveis Instancia = new();

    public bool Disponivel => false;
    public Task<bool> AbrirFichaAsync(int pacienteId, string paciente) => Task.FromResult(false);
    public Task<bool> MarcarNovoHorarioAsync(DateOnly dia, int? profissionalId) => Task.FromResult(false);
}
