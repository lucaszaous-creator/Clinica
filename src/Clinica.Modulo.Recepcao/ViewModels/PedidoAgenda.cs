namespace Clinica.Recepcao.ViewModels;

/// <summary>
/// A ponte por onde outra tela pede à Agenda que abra num DIA (set/2026) — a ficha do
/// paciente, ao clicar num dos próximos horários dele.
///
/// Mesmo desenho de <see cref="PreenchimentoNovoAtendimento"/>: singleton de UM pedido,
/// que consumir LIMPA. A Agenda é montada uma vez e guardada pelo shell, então o pedido
/// não pode viver no construtor dela — é lido quando a tela APARECE (o <c>Loaded</c> da
/// View, que já liga o relógio). Pedido órfão abriria a agenda de amanhã no dia de um
/// clique de ontem, por isso ele é descartado ao ser lido.
/// </summary>
public sealed class PedidoAgenda
{
    private DateOnly? _dia;

    public void AbrirEm(DateOnly dia) => _dia = dia;

    public DateOnly? Consumir()
    {
        var dia = _dia;
        _dia = null;
        return dia;
    }
}
