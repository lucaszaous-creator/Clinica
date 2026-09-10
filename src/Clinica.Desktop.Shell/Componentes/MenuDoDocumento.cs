using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;

namespace Clinica.Desktop.Shell.Componentes;

/// <summary>
/// Os comandos da tela, na ordem em que o menu os mostra. Cada tela tem os seus (os nomes
/// dos <c>[RelayCommand]</c> mudam de uma para outra), e é só isto que o menu precisa saber
/// delas.
/// </summary>
public sealed record ComandosDoDocumento(
    ICommand Imprimir, ICommand Assinar, ICommand Enviar,
    ICommand RenovarLink, ICommand TirarDoAr, ICommand Cancelar);

/// <summary>
/// O "⋯" da linha de um documento: as ações que não são a principal daquele papel
/// (set/2026).
///
/// Por que ele é um componente
/// ---------------------------
/// Eram seis botões por linha, e o mockup aprovado (01) trocou isso por "uma ação + ⋯" —
/// primeiro no Consultório. Com a central e a ficha ganhando os mesmos seis atos, o menu
/// passaria a existir em TRÊS telas; os RÓTULOS dele são metade do que a parcela entrega
/// ("Imprimir a 2ª via", "Assinar com o e-CPF…", "Tirar do ar"), e três cópias deles
/// divergiriam na primeira correção — a tela que ficasse para trás chamaria o mesmo ato por
/// outro nome, que é exatamente o que faz a pessoa procurar a diferença que não existe.
///
/// ⚠️ Montado em CÓDIGO, e não como <c>ContextMenu</c> em XAML: o menu declarado vive num
/// <c>Popup</c>, fora da árvore visual da tela, e os comandos precisariam de
/// <c>PlacementTarget.Tag</c> para chegar ao ViewModel — binding que erra o caminho falha em
/// RUNTIME, calado, que é a categoria que nenhuma rede local pega.
///
/// ⚠️ Item que a linha não pode exercer NÃO ENTRA, em vez de entrar apagado: menu de seis
/// itens com quatro cinzentos é menu que se fecha sem ler. As duas barreiras continuam de
/// pé — o ato exige a permissão de novo, e a resolução é a MESMA (<c>DocumentoNaTela</c>).
/// </summary>
public static class MenuDoDocumento
{
    /// <summary>
    /// Abre o menu ancorado no botão. <paramref name="parametro"/> é a LINHA da tela — é ela
    /// que os comandos recebem, porque é ela que a tela conhece.
    /// </summary>
    public static void Abrir(
        object? remetente, DocumentoNaTela doc, object parametro, ComandosDoDocumento comandos)
    {
        if (remetente is not FrameworkElement botao) return;

        var menu = new ContextMenu
        {
            PlacementTarget = botao,
            Placement = PlacementMode.Bottom
        };

        void Acrescentar(string rotulo, ICommand comando, bool visivel)
        {
            if (!visivel) return;
            menu.Items.Add(new MenuItem
            {
                Header = rotulo,
                Command = comando,
                CommandParameter = parametro
            });
        }

        // A 2ª via está SEMPRE aqui, inclusive quando ela é o botão da linha: quem abriu o
        // menu procurando por ela não deveria ter de fechá-lo para achá-la ao lado. É também
        // o que impede o menu de nascer vazio — "⋯" que abre e fecha sem dizer nada é o
        // botão que não faz nada da parcela 41.
        Acrescentar("Imprimir a 2ª via", comandos.Imprimir, true);

        Acrescentar("Assinar com o e-CPF…", comandos.Assinar, doc.OferecerAssinar);

        // É o ARQUIVO que vale: sem esta porta, o paciente sai com o papel e o PDF assinado
        // fica no computador da clínica.
        Acrescentar("Enviar ao paciente…", comandos.Enviar, doc.OferecerEnviar);

        // Renovar põe o link vencido de volta no ar com o MESMO endereço — o QR já impresso
        // volta a funcionar.
        Acrescentar("Renovar o link", comandos.RenovarLink, doc.OferecerRenovarLink);
        Acrescentar("Tirar do ar", comandos.TirarDoAr, doc.OferecerTirarDoAr);

        Acrescentar("Cancelar o documento…", comandos.Cancelar, doc.OferecerCancelar);

        menu.IsOpen = true;
    }
}
