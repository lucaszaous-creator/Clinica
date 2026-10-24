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
        // Os formulários do paciente que um fluxo espera (cadastro, convênio "a definir").
        servicos.AddScoped<FormulariosDoPaciente>();
        // A conversa do WhatsApp AO LADO da tela (fase 2, 24/10): o painel é do navegador, como a camada.
        servicos.AddScoped<ConversaAoLado>();
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
        // O catálogo das videoaulas é do shell (que sabe quais telas o exe publica); sem ele —
        // prévia e testes — a tela de Treinamento abre com o estado vazio.
        servicos.TryAddSingleton<ITreinamentoDoApp, TreinamentoSemAulas>();
        // Singleton: o paciente que a ficha do balcão deve mostrar (quem pede é a agenda, a lista
        // de pacientes e o Início; quem lê é a tela, na abertura e a cada volta).
        servicos.TryAddSingleton<PedidoDaFichaDoBalcao>();
        // Singleton: o paciente em que a central de Documentos deve abrir (quem pede é uma tela
        // de fora antes de navegar; quem lê é a central, na abertura e a cada volta).
        servicos.TryAddSingleton<PedidoDeDocumento>();
        // Singleton: a conversa que a aba WhatsApp deve abrir (quem pede é o "Escrever no WhatsApp"
        // de outra tela antes de navegar; quem lê é a aba, na abertura e a cada volta).
        servicos.TryAddSingleton<PedidoDeConversaNoCrm>();
        // Singleton: o Acompanhamento deve abrir na fila de retornos (quem pede é o Início).
        servicos.TryAddSingleton<PedidoDoAcompanhamento>();
        return servicos;
    }
}
