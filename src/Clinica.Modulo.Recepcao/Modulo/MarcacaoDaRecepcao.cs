using Clinica.Desktop.Shell.Modulos;
using Clinica.Recepcao.ViewModels;
using Clinica.Ui.Blazor.Hospedagem;

namespace Clinica.Recepcao.Modulo;

/// <summary>
/// O "Marcar horário" das telas Blazor, do jeito da Recepção (out/2026): o mesmo gesto que o
/// Acompanhamento WPF fazia — deixa o pedido na ponte <see cref="PreenchimentoNovoAtendimento"/>
/// (paciente, profissional que atendia, modalidade do recall) e navega para a aba Marcar, que
/// o consome ao aparecer.
///
/// ⚠️ Antes de definir o pedido, pergunta se a aba EXISTE neste aplicativo (o Faturamento
/// publica o Acompanhamento e não publica a agenda): pedido definido e navegação recusada
/// deixariam um pedido ÓRFÃO, e a próxima abertura manual da tela nasceria pré-preenchida com
/// o clique de agora — o que a ponte existe para evitar.
/// </summary>
public sealed class MarcacaoDaRecepcao(PreenchimentoNovoAtendimento ponte) : IMarcacaoDeHorario
{
    public Task<bool> MarcarAsync(int pacienteId, int? profissionalId, string? modalidadeCodigo)
    {
        var despachante = System.Windows.Application.Current?.Dispatcher;
        if (despachante is null || despachante.CheckAccess()) return Task.FromResult(Marcar(pacienteId, profissionalId, modalidadeCodigo));
        return despachante.InvokeAsync(() => Marcar(pacienteId, profissionalId, modalidadeCodigo)).Task;
    }

    private bool Marcar(int pacienteId, int? profissionalId, string? modalidadeCodigo)
    {
        if (!NavegacaoSuite.Existe(ModuloRecepcao.ChaveMarcarHorario)) return false;
        ponte.Definir(new PedidoNovoAtendimento(true, DateTime.Today, profissionalId, null, pacienteId, ModalidadeCodigo: modalidadeCodigo));
        if (NavegacaoSuite.Ir(ModuloRecepcao.ChaveMarcarHorario)) return true;
        ponte.Consumir(); // não deixa o pedido pendurado
        return false;
    }
}
