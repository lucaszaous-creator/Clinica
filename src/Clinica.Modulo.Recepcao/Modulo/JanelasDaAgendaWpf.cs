using Clinica.Application.Servicos;
using Clinica.Desktop.Controls;
using Clinica.Desktop.Shell;
using Clinica.Desktop.Shell.Componentes;
using Clinica.Desktop.Shell.Modulos;
using Clinica.Domain.Entities;
using Clinica.Recepcao.ViewModels;
using Clinica.Ui.Blazor.Hospedagem;
using Microsoft.Extensions.DependencyInjection;

namespace Clinica.Recepcao.Modulo;

/// <summary>
/// As janelas WPF que a Agenda do dia (Blazor, out/2026) abre — cada método faz EXATAMENTE o
/// que o <c>FilaViewModel</c> fazia para abrir a sua: os mesmos ViewModels, as mesmas janelas,
/// o dono pela <see cref="JanelaDona"/> (com um modal aberto, a próxima nasceria ATRÁS dele) e
/// um escopo por operação. Nada foi reescrito: a janela de fechamento carrega pacote, insumo e
/// caixa, e uma segunda definição divergiria na primeira correção.
///
/// A barreira de permissão é da tela que chama (as duas barreiras moram lá); as janelas
/// conferem de novo ao salvar, como sempre conferiram.
///
/// ⚠️ Tudo roda no despachante do WPF: a janela modal precisa da thread da interface, e a tela
/// Blazor pode chamar de outra (é o mesmo cuidado de <see cref="MarcacaoDaRecepcao"/>).
/// </summary>
public sealed class JanelasDaAgendaWpf(
    IServiceScopeFactory escopos,
    ISnackbarService snackbar,
    IDialogoService dialogo,
    PreenchimentoNovoAtendimento ponte) : IJanelasDaAgenda
{
    public bool Disponivel => true;

    public Task<bool> AbrirFichaAsync(int pacienteId, string paciente) => NaInterface(() =>
    {
        // A MESMA ficha do balcão da tela Pacientes: duas fichas para a mesma pessoa no mesmo
        // app fariam procurar a diferença que não existe.
        ConsultaContextualWindow? janela = null;
        var ficha = new Views.FichaBalcaoView
        {
            DataContext = new FichaBalcaoViewModel(escopos, snackbar, dialogo, pacienteId, () => janela?.Close())
        };
        janela = new ConsultaContextualWindow($"Ficha — {paciente}", ficha, "Voltar à agenda do dia");
        janela.ShowDialog();
        return Task.FromResult(true);
    });

    public Task<bool> MarcarNovoHorarioAsync(DateOnly dia, int? profissionalId) => NaInterface(() =>
    {
        // A MESMA ponte da grade (PreenchimentoNovoAtendimento → aba Marcar), com o dia da
        // lista e o profissional do filtro. Pergunta ANTES se a aba existe: pedido definido e
        // navegação recusada deixariam um pedido ÓRFÃO pré-preenchendo a próxima abertura.
        if (!NavegacaoSuite.Existe(ModuloRecepcao.ChaveMarcarHorario)) return Task.FromResult(false);
        ponte.Definir(new PedidoNovoAtendimento(
            MarcarParaDepois: true, DataHora: dia.ToDateTime(TimeOnly.MinValue),
            ProfissionalId: profissionalId, SalaId: null));
        if (NavegacaoSuite.Ir(ModuloRecepcao.ChaveMarcarHorario)) return Task.FromResult(true);
        ponte.Consumir();
        return Task.FromResult(false);
    });

    private static Task<T> NaInterface<T>(Func<Task<T>> acao)
    {
        var despachante = System.Windows.Application.Current?.Dispatcher;
        if (despachante is null || despachante.CheckAccess()) return acao();
        return despachante.InvokeAsync(acao).Task.Unwrap();
    }
}
