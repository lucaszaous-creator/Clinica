using System.Windows;
using Clinica.Application.Assinatura;
using Clinica.Clinico.ViewModels;

namespace Clinica.Clinico.Janelas;

/// <summary>
/// Escolha do certificado ICP-Brasil na hora de assinar.
///
/// Fecha com <c>DialogResult = true</c> só quando o ViewModel aceita a escolha — recusa
/// (nada selecionado, certificado vencido) deixa a janela aberta com a mensagem inline,
/// que é a regra de feedback de formulário do projeto.
/// </summary>
public partial class EscolherCertificadoWindow : Window
{
    private readonly EscolherCertificadoViewModel _vm;

    public EscolherCertificadoWindow(EscolherCertificadoViewModel vm)
    {
        InitializeComponent();
        _vm = vm;
        DataContext = vm;

        vm.Fechar = () => DialogResult = true;
        Closed += (_, _) => vm.Fechar = null;
    }

    /// <summary>
    /// Abre e devolve o certificado escolhido, ou null se o usuário desistiu.
    ///
    /// Estático porque as três telas que assinam fazem exatamente a mesma coisa aqui, e
    /// repetir o `ShowDialog` em cada uma é como se perde a comparação com `Confirmou`.
    /// </summary>
    public static CertificadoAssinatura? Perguntar(string assunto, Window? dono)
    {
        var janela = new EscolherCertificadoWindow(new EscolherCertificadoViewModel(assunto))
        {
            Owner = dono
        };

        return janela.ShowDialog() == true ? janela._vm.Escolhido : null;
    }
}
