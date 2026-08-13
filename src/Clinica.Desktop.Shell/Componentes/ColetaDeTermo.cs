using System.Linq;
using System.Windows;
using Clinica.Application.Servicos;
using Clinica.Desktop.Controls;
using Microsoft.Extensions.DependencyInjection;

namespace Clinica.Desktop.Shell.Componentes;

/// <summary>
/// O PONTO ÚNICO por onde toda coleta de termo passa (parcela 66, 3ª rodada).
///
/// São QUATRO portas — a ficha do paciente, a fila do balcão, o atendimento do Consultório
/// e a central de documentos —, e cada uma tinha começado a montar a janela por conta
/// própria: escopo, ViewModel, dono da janela, recarga. Quatro montagens divergem na
/// primeira correção, e o que elas colhem é a prova de que o paciente consentiu.
///
/// Aqui mora o que é igual nas quatro: escolher o modelo quando ele não vem decidido,
/// resolver os serviços do escopo, abrir a janela com o dono certo e dizer se concluiu.
/// </summary>
public static class ColetaDeTermo
{
    /// <summary>
    /// Abre a coleta. Quando <paramref name="modeloId"/> é nulo, pergunta ANTES qual termo
    /// — é o caminho da porta AVULSA, em que não há procedimento marcado dizendo qual é.
    /// </summary>
    /// <returns>
    /// <c>true</c> quando o paciente assinou ou recusou. <c>false</c> quando ninguém
    /// escolheu termo nenhum, ou a janela foi fechada sem resolver.
    ///
    /// ⚠️ Quem chama deve RECARREGAR de qualquer forma, e não só no <c>true</c>: abrir a
    /// janela já EMITE o termo numerado, então mesmo o "fechei sem assinar" mudou o que a
    /// tela de trás mostra.
    /// </returns>
    public static bool Abrir(
        IServiceScopeFactory escopos,
        int pacienteId,
        string pacienteNome,
        int? modeloId = null,
        int? documentoId = null,
        int? profissionalId = null)
    {
        ArgumentNullException.ThrowIfNull(escopos);

        using var scope = escopos.CreateScope();
        var servicos = scope.ServiceProvider;

        if (modeloId is null)
        {
            var escolha = new EscolherTermoViewModel(
                servicos.GetRequiredService<TermoProcedimentoService>(), pacienteNome);

            var seletor = new EscolherTermoWindow(escolha) { Owner = Dono() };
            if (seletor.ShowDialog() != true || escolha.Escolhido is not { } modelo)
                return false;

            modeloId = modelo.Id;
        }

        var vm = new AssinaturaPacienteViewModel(
            servicos.GetRequiredService<DocumentoClinicoService>(),
            servicos.GetRequiredService<AssinaturaDoPacienteService>(),
            servicos.GetRequiredService<IDialogoService>(),
            pacienteId,
            modeloId.Value,
            pacienteNome,
            documentoId,
            profissionalId,
            servicos.GetRequiredService<AcessoProntuarioService>(),
            servicos.GetRequiredService<ParametrosService>());

        new AssinaturaPacienteWindow(vm) { Owner = Dono() }.ShowDialog();

        return vm.Concluido;
    }

    /// <summary>
    /// A janela ATIVA, não a principal: com um modal já aberto, a próxima nasceria ATRÁS
    /// dele e quem clicou concluiria que o botão não fez nada (a lição da parcela 58).
    /// </summary>
    private static Window? Dono()
        => System.Windows.Application.Current?.Windows.OfType<Window>()
               .FirstOrDefault(w => w.IsActive)
           ?? System.Windows.Application.Current?.MainWindow;
}
