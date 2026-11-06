using Velopack;

namespace Clinica.Financeiro;

/// <summary>
/// Ponto de entrada. O Velopack precisa rodar seus hooks (instalação/atualização)
/// antes de qualquer UI subir — por isso o Main é customizado, como no faturamento.
/// </summary>
public static class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        // Sem a instalação automática do Velopack na partida: quem instala é o
        // AtualizadorSuite, que limita as tentativas. Ligada, ela reaplicaria a cada
        // abertura um pacote que já falhou — a tela de atualização que não sai (26/10/2026).
        VelopackApp.Build().SetAutoApplyOnStartup(false).Run();

        var app = new App();
        app.InitializeComponent();
        app.Run();
    }
}
