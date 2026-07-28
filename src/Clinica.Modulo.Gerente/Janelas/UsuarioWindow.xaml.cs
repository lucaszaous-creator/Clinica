using System.Windows;
using Clinica.Gerente.ViewModels;

namespace Clinica.Gerente.Janelas;

/// <summary>
/// Janela do cadastro de usuário. Quem fecha é o ViewModel (evento <c>Concluido</c>),
/// como nos demais cadastros da suíte.
///
/// A senha é o único campo que NÃO usa binding: o <see cref="System.Windows.Controls.PasswordBox"/>
/// não expõe a senha como DependencyProperty de propósito — deixá-la ir e voltar pelo
/// binding a espalharia pela árvore visual. Aqui ela é copiada para o ViewModel a cada
/// digitação e some quando a janela fecha.
/// </summary>
public partial class UsuarioWindow : Window
{
    private readonly UsuarioEdicaoViewModel _vm;

    public UsuarioWindow(UsuarioEdicaoViewModel vm)
    {
        InitializeComponent();
        _vm = vm;
        DataContext = vm;

        TxtAjudaSenha.Text = vm.EhNovo
            ? "Mínimo de 6 caracteres."
            : "Deixe em branco para manter a senha atual.";

        void AoConcluir()
        {
            vm.Concluido -= AoConcluir;
            DialogResult = true;
        }

        vm.Concluido += AoConcluir;
        Closed += (_, _) => vm.Concluido -= AoConcluir;
    }

    private void SenhaDigitada(object remetente, RoutedEventArgs e)
        => _vm.Senha = TxtSenha.Password;
}
