namespace Clinica.Ui.Blazor.Hospedagem;

/// <summary>
/// "Abra a Agenda do dia neste dia, com este profissional" — o clique na célula da matriz da
/// Visão geral (out/2026). Um pedido só; ler LIMPA (pedido órfão abriria o dia de ontem na
/// próxima vez que alguém entrasse na aba Dia). Quem consome é a <c>AgendaDoDia</c>, na
/// abertura e na volta à aba.
/// </summary>
public sealed class PedidoDaAgendaDoDia
{
    private readonly object _trava = new();
    private (DateOnly Dia, int? ProfissionalId)? _pedido;

    public void Definir(DateOnly dia, int? profissionalId)
    {
        lock (_trava) _pedido = (dia, profissionalId);
    }

    public (DateOnly Dia, int? ProfissionalId)? Consumir()
    {
        lock (_trava)
        {
            var p = _pedido;
            _pedido = null;
            return p;
        }
    }
}
