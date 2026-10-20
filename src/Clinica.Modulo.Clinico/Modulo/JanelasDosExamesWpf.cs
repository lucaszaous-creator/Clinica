using Clinica.Application.Modelos;
using Clinica.Desktop.Shell.Componentes;
using Clinica.Desktop.Shell.Modulos;
using Clinica.Domain.Entities;
using Clinica.Ui.Blazor.Hospedagem;
using Microsoft.Extensions.DependencyInjection;

namespace Clinica.Clinico.Modulo;

/// <summary>
/// A ponte da tela Blazor de Exames (out/2026) até o que continua fora dela: a navegação do posto
/// até a seção de exames do paciente. Escolher o paciente, emitir o pedido, registrar o laudo e ver
/// os resultados viraram formulários Blazor da tela. A
/// barreira de permissão é da tela que chama; aqui só se abre. Tudo roda no despachante do WPF (a
/// janela modal precisa de dono e a tela Blazor pode chamar de outra thread).
/// </summary>
public sealed class JanelasDosExamesWpf(PacienteEmFoco foco) : IJanelasDosExames
{
    public Task<bool> AbrirExamesDoPacienteAsync(PedidoDeExameLinha pedido) => NaInterface(() =>
    {
        foco.Definir(pedido.PacienteId, pedido.Paciente);
        return NavegacaoSuite.Ir(ModuloClinico.ChaveExamesDoPaciente);
    });

    private static Task<T> NaInterface<T>(Func<T> acao)
    {
        var despachante = System.Windows.Application.Current?.Dispatcher;
        if (despachante is null || despachante.CheckAccess()) return Task.FromResult(acao());
        return despachante.InvokeAsync(acao).Task;
    }
}
