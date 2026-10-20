using Clinica.Desktop.Shell.Componentes;
using Clinica.Ui.Blazor.Hospedagem;

namespace Clinica.Desktop.Blazor;

/// <summary>
/// O WhatsApp deste computador para o formulário Blazor de envio — EXATAMENTE o
/// <see cref="EntregaAoPaciente.Entregar(byte[], string, string?, string, string)"/> que a janela WPF
/// chamava: a mesma pasta estável, a mesma área de transferência, a mesma conversa. Mora aqui porque
/// o formulário roda nos quatro aplicativos que hospedam tela Blazor.
///
/// ⚠️ Roda no despachante do WPF: a área de transferência do Windows exige a thread da interface
/// (STA), e o formulário pode chamar de outra.
/// </summary>
public sealed class EntregaAoPacienteWpf : IEntregaAoPaciente
{
    public Task<ResultadoDoAto> EntregarNoWhatsAppAsync(byte[] pdf, string nomeArquivo, string? telefone, string paciente, string legenda)
    {
        ResultadoDoAto Entregar()
        {
            var r = EntregaAoPaciente.Entregar(pdf, nomeArquivo, telefone, paciente, legenda);
            return new ResultadoDoAto(Frase: r.Frase, EhErro: r.EhErro);
        }

        var d = System.Windows.Application.Current?.Dispatcher;
        if (d is null || d.CheckAccess()) return Task.FromResult(Entregar());
        return d.InvokeAsync(Entregar).Task;
    }
}
