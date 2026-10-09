using System.Windows;

namespace Clinica.Financeiro.Web;

/// <summary>Janela autenticada da interface local. O bootstrap continua no executável.</summary>
public sealed class FinanceiroWebWindow : Window
{
    public FinanceiroWebView TelaWeb { get; }

    public FinanceiroWebWindow(IServiceProvider services, string? dadosTreinamento = null)
    {
        Title = "Financeiro — Clínica SemDor" + (Clinica.Desktop.Shell.Configuracao.EdicaoDeTeste.Ativa ? " — teste PR 245" : "");
        var area = SystemParameters.WorkArea;
        Width = Math.Min(1440, area.Width);
        Height = Math.Min(900, area.Height);
        MinWidth = Math.Min(680, area.Width);
        MinHeight = Math.Min(460, area.Height);
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        UseLayoutRounding = true;
        SnapsToDevicePixels = true;
        Resources.MergedDictionaries.Add(new ResourceDictionary
        { Source = new Uri("/Clinica.Modulo.Financeiro;component/Styles/Financeiro.xaml", UriKind.Relative) });
        TelaWeb = new FinanceiroWebView(services, dadosTreinamento);
        Content = TelaWeb;
        Closing += (_, e) =>
        {
            if (TelaWeb.PodeFechar) return;
            e.Cancel = true;
            TelaWeb.AvisarOperacaoEmAndamento();
        };
        Closed += (_, _) => TelaWeb.Dispose();
    }
}
