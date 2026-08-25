using System.Windows;
using Clinica.Application.Assinatura;

namespace Clinica.Desktop.Shell.Componentes;

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
    /// <param name="escopos">
    /// Habilita a busca de certificado em NUVEM (SafeID). Null a desliga — e desligar é
    /// melhor que mostrar um botão que não teria como funcionar.
    /// </param>
    /// <param name="cpfDeQuemAssina">
    /// Vai como <c>login_hint</c> para a página do PSC já abrir no CPF certo, poupando a
    /// médica de digitá-lo a cada documento.
    /// </param>
    /// <param name="assinaturasDoAto">
    /// Quantos documentos este ato vai selar com o MESMO certificado. ⚠️ Em nuvem isso
    /// escolhe o ESCOPO da autorização: o padrão do SafeID é "um hash só; o token morre no
    /// uso", e um ato de duas selagens teria a segunda recusada SEMPRE. Quem sela dois
    /// documentos precisa dizer que são dois.
    /// </param>
    public static CertificadoAssinatura? Perguntar(
        string assunto, Window? dono,
        Microsoft.Extensions.DependencyInjection.IServiceScopeFactory? escopos = null,
        string? cpfDeQuemAssina = null,
        int assinaturasDoAto = 1)
    {
        var janela = new EscolherCertificadoWindow(
            new EscolherCertificadoViewModel(
                assunto, escopos, cpfDeQuemAssina, assinaturasDoAto))
        {
            Owner = dono
        };

        return janela.ShowDialog() == true ? janela._vm.Escolhido : null;
    }
}
