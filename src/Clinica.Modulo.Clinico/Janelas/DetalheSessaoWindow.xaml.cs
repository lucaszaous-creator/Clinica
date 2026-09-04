using System.Windows;
using Clinica.Clinico.ViewModels;

namespace Clinica.Clinico.Janelas;

/// <summary>
/// Os campos separados da sessão — queixa, história, exame, hipótese, CID, conduta,
/// orientações, plano, motivo do retorno e encaminhamento.
///
/// A tela de atendimento virou UMA folha de texto livre (mockup 01), e estes campos
/// continuam existindo por duas razões que não são de leiaute: eles são colunas do
/// prontuário, que não se apagam (Lei 13.787/2018), e são o que o relatório do convênio
/// imprime separado por assunto. O que mudou é que eles deixaram de ocupar a tela de quem
/// só quer escrever a sessão.
///
/// ⚠️ Ela edita o MESMO <see cref="AtendimentoViewModel"/> da tela de trás e NÃO grava
/// nada: quem grava é o "Salvar sessão" de lá. Duas cópias do mesmo registro dariam duas
/// verdades sobre a mesma sessão, e um segundo botão de gravar faria a pessoa supor que
/// fechar sem clicar perde o que digitou.
/// </summary>
public partial class DetalheSessaoWindow : Window
{
    public DetalheSessaoWindow(AtendimentoViewModel vm)
    {
        InitializeComponent();
        DataContext = vm;
    }
}
