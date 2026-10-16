using Microsoft.Extensions.DependencyInjection;

namespace Clinica.Ui.Blazor.Hospedagem;

/// <summary>
/// O que toda tela Blazor precisa no DI, num lugar só — o módulo WPF e a prévia chamam o
/// MESMO método, senão a prévia mostraria uma tela que funciona e o app uma que não abre.
/// </summary>
public static class RegistroTelas
{
    public static IServiceCollection AddTelasBlazor(this IServiceCollection servicos)
    {
        // Scoped = um por tela (escopo do BlazorWebView / circuito da prévia). Não
        // guardam DbContext nem serviço da Application: só o estado do que está na tela.
        servicos.AddScoped<Dialogos>();
        servicos.AddScoped<Notificacoes>();
        return servicos;
    }
}
