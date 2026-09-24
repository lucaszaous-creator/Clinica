using System.Windows.Input;

namespace Clinica.Desktop.ViewModels;

/// <summary>
/// Contrato opcional dos ViewModels de tela para os atalhos globais do shell
/// (Ctrl+S salvar, Ctrl+P imprimir/exportar, F5 atualizar). O MainViewModel
/// roteia o atalho para o comando da tela ativa; nulo = atalho sem efeito ali.
/// </summary>
public interface IAtalhosDeTela
{
    ICommand? AtalhoSalvar => null;
    ICommand? AtalhoImprimir => null;
    ICommand? AtalhoAtualizar => null;
}
