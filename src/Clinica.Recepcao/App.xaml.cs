using System.Windows;
using Clinica.Application.Servicos;
using Clinica.Desktop.Shell;
using Clinica.Desktop.Shell.Configuracao;
using Clinica.Desktop.Shell.Modulos;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Clinica.Recepcao;

/// <summary>
/// Executável da Recepção: uma casca fina sobre o shell. Só escolhe quais módulos
/// carregar — a subida (log, conexão, banco, janela) é do <see cref="SuiteApp"/>.
/// </summary>
public partial class App : System.Windows.Application
{
    private IHost? _host;

    /// <summary>
    /// Módulos carregados por este executável. Instância única: o shell monta a sidebar
    /// a partir destes mesmos objetos que registraram os serviços no DI.
    /// </summary>
    private readonly IReadOnlyList<IModuloApp> _modulos =
    [
        new Modulo.ModuloRecepcao()
    ];

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        _host = await SuiteApp.IniciarAsync(this, "Recepção", "Recepção — Clínica SemDor", _modulos);

        if (_host is not null) await LembretesPorEmailAsync(_host);
    }

    /// <summary>
    /// Manda o lembrete da sessão por e-mail a quem tem endereço na ficha (set/2026).
    ///
    /// <b>Aqui pela razão do backup do Gerente</b>: o sistema é desktop, não tem serviço
    /// residente, e o balcão é a máquina que abre todo dia de manhã — antes de a primeira
    /// pessoa chegar. O Gerente roda o mesmo; a chave de idempotência é o contato, então as
    /// duas máquinas abertas no mesmo dia não mandam dois e-mails.
    ///
    /// <b>Depois da janela abrir, e sem esperar.</b> Vinte envios a um servidor lento levam
    /// tempo; prender a abertura a eles faria a recepcionista olhar uma tela cinza com o
    /// paciente na frente. Falha não derruba nada — cada envio que falha fica registrado no
    /// log e o contato continua pendente para o WhatsApp de um clique.
    ///
    /// Sem servidor configurado em Configurações isto não faz nada e não reclama: lembrete
    /// desligado é estado legítimo, e um aviso a cada abertura ensinaria a fechá-lo sem ler.
    /// </summary>
    private static async Task LembretesPorEmailAsync(IHost host)
    {
        try
        {
            using var escopo = host.Services.CreateScope();
            await escopo.ServiceProvider
                .GetRequiredService<LembreteEmailService>()
                .EnviarLembretesDaAberturaAsync();
        }
        catch (Exception ex)
        {
            LogSuite.Registrar("Recepção — lembretes por e-mail da abertura", ex);
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _host?.Dispose();
        base.OnExit(e);
    }
}
