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
        => MarcarAsync(pacienteId, profissionalId, modalidadeCodigo, DateOnly.FromDateTime(DateTime.Today));

    /// <summary>
    /// Com o DIA (o retorno sugerido): a data que chega no passado vira HOJE — pré-preencher
    /// uma data passada faria o formulário abrir recusando (a regra da tela WPF dos Retornos).
    /// </summary>
    public Task<bool> MarcarAsync(int pacienteId, int? profissionalId, string? modalidadeCodigo, DateOnly dia)
    {
        var hoje = DateOnly.FromDateTime(DateTime.Today);
        var data = (dia < hoje ? hoje : dia).ToDateTime(TimeOnly.MinValue);
        var despachante = System.Windows.Application.Current?.Dispatcher;
        if (despachante is null || despachante.CheckAccess()) return Task.FromResult(Marcar(pacienteId, profissionalId, modalidadeCodigo, data));
        return despachante.InvokeAsync(() => Marcar(pacienteId, profissionalId, modalidadeCodigo, data)).Task;
    }

    private bool Marcar(int pacienteId, int? profissionalId, string? modalidadeCodigo, DateTime data)
    {
        if (!NavegacaoSuite.Existe(ModuloRecepcao.ChaveMarcarHorario)) return false;
        ponte.Definir(new PedidoNovoAtendimento(true, data, profissionalId, null, pacienteId, ModalidadeCodigo: modalidadeCodigo));
        if (NavegacaoSuite.Ir(ModuloRecepcao.ChaveMarcarHorario)) return true;
        ponte.Consumir(); // não deixa o pedido pendurado
        return false;
    }
}
