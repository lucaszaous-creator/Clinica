using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

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
        // Os formulários abertos por CÓDIGO (out/2026 — a área do SafeID/assinatura em Blazor) e
        // as portas que os abrem: a assinatura com o A1, a coleta do termo, a sala de infusão, a
        // enfermagem e o editor da prescrição de infusão. Scoped = um por navegador, como a camada.
        servicos.AddScoped<JanelasNaTela>();
        servicos.AddScoped<IAssinaturaNaTela, AssinaturaNaTela>();
        servicos.AddScoped<ColetaDeTermoNaPagina>();
        servicos.AddScoped<IJanelasDaSalaDeInfusao, JanelasDaSalaDeInfusaoNaPagina>();
        servicos.AddScoped<IJanelasDaEnfermagem, JanelasDaEnfermagemNaPagina>();
        servicos.AddScoped<IJanelasDaPrescricaoDeInfusao, JanelasDaPrescricaoDeInfusaoNaPagina>();
        // Singleton: a ponte entre duas telas (a Visão geral pede, a Agenda do dia consome).
        servicos.TryAddSingleton<PedidoDaAgendaDoDia>();
        // Singleton: o paciente e a seção que a tela do paciente deve mostrar (quem pede é a
        // navegação do posto; quem lê é a tela, na abertura e a cada volta).
        servicos.TryAddSingleton<PedidoDaTelaDoPaciente>();
        return servicos;
    }
}
