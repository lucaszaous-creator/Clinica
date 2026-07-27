using System;
using System.Globalization;
using System.Threading;
using System.Windows;
using System.Windows.Markup;
using Velopack;

namespace Clinica.Desktop;

/// <summary>
/// Ponto de entrada do aplicativo. O Velopack precisa executar seus hooks de
/// instalação/atualização ANTES de qualquer UI — por isso o Main é explícito aqui.
/// </summary>
public static class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        // Processa hooks do Velopack (primeira instalação, update, desinstalação).
        // Em execução normal, retorna e o app segue.
        VelopackApp.Build().Run();

        FixarCulturaBrasileira();

        var app = new App();
        app.InitializeComponent();
        app.Run();
    }

    /// <summary>
    /// Trava o app em pt-BR: data DD/MM/AAAA, decimal com vírgula e nomes de dia/mês
    /// em português, independentemente da configuração regional da máquina da clínica.
    ///
    /// O <c>OverrideMetadata</c> do Language é o pulo do gato: o WPF ignora a cultura da
    /// thread na camada visual e assume en-US em todo FrameworkElement — sem isso, o
    /// DatePicker e qualquer StringFormat de data saem no formato americano MM/DD.
    /// </summary>
    private static void FixarCulturaBrasileira()
    {
        var cultura = new CultureInfo("pt-BR");

        CultureInfo.DefaultThreadCurrentCulture = cultura;
        CultureInfo.DefaultThreadCurrentUICulture = cultura;
        Thread.CurrentThread.CurrentCulture = cultura;
        Thread.CurrentThread.CurrentUICulture = cultura;

        FrameworkElement.LanguageProperty.OverrideMetadata(
            typeof(FrameworkElement),
            new FrameworkPropertyMetadata(XmlLanguage.GetLanguage(cultura.IetfLanguageTag)));
    }
}
