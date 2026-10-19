using Clinica.Ui.Blazor.Hospedagem;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Clinica.Desktop.Blazor;

/// <summary>
/// O que todo módulo com tela Blazor registra — num lugar só, e IDEMPOTENTE: o Gerente
/// carrega o Financeiro E o Faturamento, e os dois chamam isto. `TryAdd` faz a segunda
/// chamada não registrar um segundo hospedeiro (que venceria o primeiro em silêncio).
/// </summary>
public static class HospedagemBlazor
{
    public static IServiceCollection AddHospedagemBlazor(this IServiceCollection servicos)
    {
        servicos.AddWpfBlazorWebView();
        servicos.TryAddSingleton<IHostDaTela, HostDaTelaWpf>();
        servicos.TryAddSingleton<IJanelasDaFicha, JanelasDaFichaWpf>();
        if (servicos.All(d => d.ServiceType != typeof(Dialogos))) servicos.AddTelasBlazor();
        return servicos;
    }

    /// <summary>O lugar de uma tela Blazor no shell (o navegador é o da janela).</summary>
    public static TelaBlazor Tela(Type componente, IServiceProvider servicos) => new(componente, servicos);
}
