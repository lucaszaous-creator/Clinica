using System;
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

        var app = new App();
        app.InitializeComponent();
        app.Run();
    }
}
