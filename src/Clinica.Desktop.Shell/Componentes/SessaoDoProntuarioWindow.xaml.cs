using System.Windows;

namespace Clinica.Desktop.Shell.Componentes;

/// <summary>
/// A sessão do prontuário aberta por inteiro, e o papel dela (set/2026).
///
/// Mora no shell porque são QUATRO portas em módulos que não se conhecem — ver
/// <see cref="SessaoDoProntuarioViewModel"/>. Somente LEITURA: escrever continua sendo a
/// tela de Atendimento (Consultório) e a janela de evolução (Recepção), cada uma com
/// <c>EditarProntuario</c>.
/// </summary>
public partial class SessaoDoProntuarioWindow : Window
{
    public SessaoDoProntuarioWindow(SessaoDoProntuarioViewModel vm)
    {
        InitializeComponent();
        DataContext = vm;

        // A janela devolve INTENÇÃO (ver anexos, ver correções) e quem age é a tela dona
        // — a janela de anexos mora no módulo Clínico e o shell não a alcança, e copiá-la
        // seria a segunda definição que este componente existe para não criar. É o padrão
        // da janela do horário (parcela 58).
        vm.Fechar += () =>
        {
            DialogResult = true;
            Close();
        };
    }
}
