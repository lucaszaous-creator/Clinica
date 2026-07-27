using Velopack;

namespace Clinica.Gerente;

/// <summary>
/// Ponto de entrada. O Velopack precisa rodar seus hooks (instalação/atualização)
/// antes de qualquer UI subir — por isso o Main é customizado, como nos demais apps.
/// </summary>
public static class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        VelopackApp.Build().Run();

        var app = new App();
        app.InitializeComponent();
        app.Run();
    }
}
