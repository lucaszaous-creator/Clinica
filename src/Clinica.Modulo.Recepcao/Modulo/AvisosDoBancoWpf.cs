namespace Clinica.Recepcao.Modulo;

/// <summary>
/// O aviso instantâneo do banco (<c>OuvinteDeMudancas</c>, o <c>pg_notify</c> da Infrastructure)
/// entregue às telas Blazor da Recepção — a Grade da agenda relê quando outra máquina grava.
/// Complemento do relógio da tela, nunca substituto (ver <see cref="Clinica.Ui.Blazor.Hospedagem.IAvisosDoBanco"/>).
/// </summary>
public sealed class AvisosDoBancoWpf : Clinica.Ui.Blazor.Hospedagem.IAvisosDoBanco
{
    public event Action<string>? Mudou
    {
        add => Clinica.Infrastructure.OuvinteDeMudancas.Mudou += value;
        remove => Clinica.Infrastructure.OuvinteDeMudancas.Mudou -= value;
    }
}
