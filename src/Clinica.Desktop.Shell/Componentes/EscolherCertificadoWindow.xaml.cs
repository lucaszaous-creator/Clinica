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
    private string? _arquivoA1;

    public EscolherCertificadoWindow(EscolherCertificadoViewModel vm)
    {
        InitializeComponent();
        _vm = vm;
        DataContext = vm;

        vm.Fechar = () => DialogResult = true;
        Closing += (_, e) => { if (vm.Autorizando) e.Cancel = true; };
        Closed += (_, _) => { vm.Fechar = null; SenhaA1.Clear(); vm.LiberarNaoEscolhidos(); };
    }

    private void EscolherArquivoA1(object sender, RoutedEventArgs e)
    {
        var dialogo = new Microsoft.Win32.OpenFileDialog { Filter = "Certificado A1 (*.pfx;*.p12)|*.pfx;*.p12", CheckFileExists = true };
        if (dialogo.ShowDialog(this) != true) return;
        _vm.Selecionado = null;
        _arquivoA1 = dialogo.FileName;
        NomeArquivoA1.Text = System.IO.Path.GetFileName(_arquivoA1);
        SenhaA1.Clear(); SenhaA1.Focus();
    }

    private async void CarregarArquivoA1(object sender, RoutedEventArgs e)
    {
        if (_vm.Autorizando) return;
        _vm.Selecionado = null;
        byte[]? arquivo = null;
        try
        {
            if (_arquivoA1 is null) throw new InvalidOperationException("Escolha o arquivo do certificado A1.");
            using var stream = System.IO.File.OpenRead(_arquivoA1);
            if (stream.Length is <= 0 or > CertificadoA1.LimiteBytes)
                throw new InvalidOperationException("Escolha um arquivo A1 de até 128 KB.");
            arquivo = new byte[(int)stream.Length];
            stream.ReadExactly(arquivo);
            var senha = SenhaA1.Password; SenhaA1.Clear();
            await _vm.CarregarA1Async(arquivo, senha);
        }
        catch (Exception ex)
        {
            _vm.Mensagem = ex is InvalidOperationException ? ex.Message : "Não foi possível ler o arquivo A1.";
            _vm.MensagemEhErro = true;
        }
        finally { SenhaA1.Clear(); if (arquivo is not null) System.Security.Cryptography.CryptographicOperations.ZeroMemory(arquivo); }
    }


    /// <summary>Abre o importador A1 e transfere somente o certificado confirmado ao chamador.</summary>
    public static CertificadoAssinatura? Perguntar(string assunto, Window? dono,
        Microsoft.Extensions.DependencyInjection.IServiceScopeFactory? escopos = null)
    {
        var janela = new EscolherCertificadoWindow(new EscolherCertificadoViewModel(assunto, escopos)) { Owner = dono };
        return janela.ShowDialog() == true ? janela._vm.Escolhido : null;
    }
}
