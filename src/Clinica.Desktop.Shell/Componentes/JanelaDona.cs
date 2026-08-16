using System.Linq;
using System.Windows;

namespace Clinica.Desktop.Shell.Componentes;

/// <summary>
/// Quem é o DONO da próxima janela modal.
///
/// A resposta é a janela ATIVA, nunca a principal — e essa é a lição da parcela 58,
/// escrita ali por causa da lista de espera da agenda: com um modal já aberto, a próxima
/// janela nasce ATRÁS dele, e quem clicou conclui que o botão não fez nada (o defeito da
/// parcela 41, agora sem nem um botão apagado para explicá-lo). É o caso normal de quem
/// atende: a janela do horário abre a do fechamento, a do fechamento abre a do termo.
///
/// Existe como PONTO ÚNICO porque a suíte tem cinquenta lugares que abrem modal, e a
/// regra estava escrita como método privado numa ViewModel só — <b>sem chamador</b>,
/// enquanto as outras quarenta e nove repetiam <c>MainWindow</c> à mão. Duas definições
/// de "quem é o dono" divergem na primeira correção, e aqui a que fica para trás produz
/// uma janela invisível: nada falha, nada aparece no log, e a pessoa clica de novo.
/// </summary>
public static class JanelaDona
{
    /// <summary>
    /// A janela ativa do aplicativo, com a principal como caminho de baixo (a abertura,
    /// em que nenhuma janela ganhou o foco ainda).
    /// </summary>
    public static Window? Atual()
        => System.Windows.Application.Current?.Windows.OfType<Window>()
               .FirstOrDefault(w => w.IsActive)
           ?? System.Windows.Application.Current?.MainWindow;
}
