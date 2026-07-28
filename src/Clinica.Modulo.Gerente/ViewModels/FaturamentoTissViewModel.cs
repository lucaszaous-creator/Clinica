using Clinica.Desktop.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Extensions.DependencyInjection;

namespace Clinica.Gerente.ViewModels;

/// <summary>
/// O item "Faturamento (TISS)" da sidebar, que a proposta mostra como UM lugar. Por
/// dentro são assuntos diferentes, e por isso abas: a visão consolidada (quanto foi
/// faturado, quanto glosou) e a fila de guias pendentes de baixa.
///
/// Abas e não itens de menu separados porque a proposta comercial tem UM item aqui, e
/// quebrar em quatro entradas encheria a sidebar da direção com o vocabulário do
/// faturamento — que é justamente o que a seção temática veio arrumar.
///
/// Este ViewModel não faz trabalho nenhum: só segura os dois que fazem. Cada aba mantém
/// o seu estado enquanto a tela está aberta, então trocar de aba não recarrega o que já
/// foi buscado.
/// </summary>
public sealed partial class FaturamentoTissViewModel : ObservableObject
{
    /// <summary>Visão consolidada: faturado, taxa de baixa, glosa, envelhecimento.</summary>
    public FaturamentoGerencialViewModel Visao { get; }

    /// <summary>Fila de guias pendentes, com o semáforo e a baixa.</summary>
    public PendenciasGerencialViewModel Pendencias { get; }

    public FaturamentoTissViewModel(
        IServiceScopeFactory escopos, ISnackbarService snackbar, IDialogoService dialogo)
    {
        Visao = new FaturamentoGerencialViewModel(escopos);
        Pendencias = new PendenciasGerencialViewModel(escopos, snackbar, dialogo);
    }
}
