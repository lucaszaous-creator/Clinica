using System.Windows;
using Clinica.Desktop.Shell.Configuracao;

namespace Clinica.Desktop.Shell;

/// <summary>
/// Primeiro acesso de um app da suíte: a clínica informa e testa a conexão do banco,
/// que fica criptografada por usuário do Windows (DPAPI) numa pasta comum a todos os
/// apps da suíte.
///
/// Antes desta tela, Recepção, Financeiro e Gerente só subiam se o app de Faturamento
/// estivesse instalado na mesma máquina — eles liam a configuração dele. Com um app por
/// posto de trabalho isso é inviável: o balcão não tem faturamento.
///
/// Salvar só é liberado depois de um teste bem-sucedido: uma conexão que não abre,
/// gravada, transforma o próximo erro em mistério na abertura do app.
/// </summary>
public partial class SetupWindow : Window
{
    private string? _conexaoValidada;

    /// <param name="nomeApp">Qual app está pedindo ("Recepção") — a clínica precisa saber.</param>
    public SetupWindow(string nomeApp)
    {
        InitializeComponent();
        TxtTitulo.Text = $"Configurar o banco de dados — {nomeApp}";
    }

    /// <summary>Editar a string invalida o teste anterior: o que foi validado não é mais o que está na tela.</summary>
    private void AoDigitar(object remetente, RoutedEventArgs e)
    {
        if (_conexaoValidada is null) return;
        _conexaoValidada = null;
        BtnSalvar.IsEnabled = false;
        Esconder();
    }

    private async void Testar(object remetente, RoutedEventArgs e)
    {
        var entrada = TxtConexao.Text;
        if (string.IsNullOrWhiteSpace(entrada))
        {
            Erro("Informe a connection string ou a URL da Neon.");
            return;
        }

        BtnTestar.IsEnabled = false;
        Info("Testando conexão…");

        string conexao;
        try
        {
            conexao = ConexaoStore.Normalizar(entrada);
        }
        catch (Exception ex)
        {
            LogSuite.Registrar("Setup — connection string não pôde ser interpretada", ex);
            BtnTestar.IsEnabled = true;
            Erro($"Não foi possível interpretar o que você colou: {ex.Message}");
            return;
        }

        var (ok, mensagem) = await ConexaoStore.TestarAsync(conexao);
        BtnTestar.IsEnabled = true;

        if (ok)
        {
            _conexaoValidada = conexao;
            BtnSalvar.IsEnabled = true;
            Sucesso("Conexão bem-sucedida! Clique em \"Salvar e continuar\".");
        }
        else
        {
            _conexaoValidada = null;
            BtnSalvar.IsEnabled = false;
            Erro($"Não foi possível conectar: {mensagem}");
        }
    }

    private void Salvar(object remetente, RoutedEventArgs e)
    {
        if (_conexaoValidada is null)
        {
            Erro("Teste a conexão antes de salvar.");
            return;
        }

        try
        {
            ConexaoStore.Salvar(_conexaoValidada);
        }
        catch (Exception ex)
        {
            // Sem disco ou sem permissão: falhar aqui em silêncio faria o app pedir a
            // conexão de novo na próxima abertura, sem ninguém entender por quê.
            LogSuite.Registrar("Setup — conexão não pôde ser gravada", ex);
            Erro($"A conexão funciona, mas não foi possível salvá-la neste computador: {ex.Message}");
            return;
        }

        DialogResult = true;
        Close();
    }

    private void Esconder()
    {
        BoxInfo.Visibility = Visibility.Collapsed;
        BoxSucesso.Visibility = Visibility.Collapsed;
        BoxErro.Visibility = Visibility.Collapsed;
    }

    private void Info(string mensagem)
    {
        Esconder();
        TxtInfo.Text = mensagem;
        BoxInfo.Visibility = Visibility.Visible;
    }

    private void Sucesso(string mensagem)
    {
        Esconder();
        TxtSucesso.Text = mensagem;
        BoxSucesso.Visibility = Visibility.Visible;
    }

    private void Erro(string mensagem)
    {
        Esconder();
        TxtErro.Text = mensagem;
        BoxErro.Visibility = Visibility.Visible;
    }
}
