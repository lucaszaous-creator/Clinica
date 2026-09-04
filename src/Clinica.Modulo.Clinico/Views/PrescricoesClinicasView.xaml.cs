using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using Clinica.Clinico.ViewModels;

namespace Clinica.Clinico.Views;

/// <summary>Prescrições do consultório: emitir e reimprimir documento clínico.</summary>
public partial class PrescricoesClinicasView : UserControl
{
    public PrescricoesClinicasView() => InitializeComponent();

    /// <summary>
    /// O "⋯" da linha: as ações que não são a principal daquele documento.
    ///
    /// Eram seis botões por linha (mockup 01, a tela que a direção mandou profissionalizar).
    /// Fica visível o que a linha está pedindo — assinar, quando falta assinatura; 2ª via,
    /// no resto —, e as outras continuam TODAS aqui: nada foi tirado.
    ///
    /// ⚠️ Montado em CÓDIGO, e não como <c>ContextMenu</c> em XAML: o menu declarado vive
    /// num <c>Popup</c>, fora da árvore visual desta tela, e os comandos precisariam de
    /// <c>PlacementTarget.Tag</c> para chegar ao ViewModel — binding que erra o caminho
    /// falha em RUNTIME, calado, que é a categoria que nenhuma rede local pega.
    ///
    /// ⚠️ Item que a linha não pode exercer NÃO ENTRA no menu, em vez de entrar apagado:
    /// menu de seis itens com quatro cinzentos é menu que se fecha sem ler. As duas
    /// barreiras continuam de pé — o comando do ViewModel exige a permissão.
    /// </summary>
    private void AoAbrirMenuDoDocumento(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement botao) return;
        if (botao.DataContext is not LinhaDocumentoClinico linha) return;
        if (DataContext is not PrescricoesClinicasViewModel vm) return;

        var menu = new ContextMenu
        {
            PlacementTarget = botao,
            Placement = PlacementMode.Bottom
        };

        void Acrescentar(string rotulo, System.Windows.Input.ICommand comando, bool visivel)
        {
            if (!visivel) return;
            menu.Items.Add(new MenuItem
            {
                Header = rotulo,
                Command = comando,
                CommandParameter = linha
            });
        }

        // A 2ª via está sempre aqui, inclusive quando ela é o botão da linha: quem abriu o
        // menu procurando por ela não deveria ter de fechá-lo para achá-la ao lado.
        Acrescentar("Imprimir a 2ª via", vm.ImprimirCommand, true);
        Acrescentar("Assinar com o e-CPF…", vm.AssinarCommand, linha.PodeAssinar);

        // É o ARQUIVO que vale: sem esta porta, o paciente sai com o papel e o PDF
        // assinado fica na clínica.
        Acrescentar("Enviar ao paciente…", vm.EnviarCommand, linha.PodeEnviar);

        // Renovar põe o link vencido de volta no ar com o MESMO endereço — o QR já
        // impresso volta a funcionar.
        Acrescentar("Renovar o link", vm.RenovarLinkCommand, linha.PodeRenovarLink);
        Acrescentar("Tirar do ar", vm.TirarDoArCommand, linha.PodeTirarDoAr);
        Acrescentar("Cancelar o documento…", vm.CancelarCommand, linha.PodeCancelar);

        if (menu.Items.Count == 0) return;

        menu.IsOpen = true;
    }
}
