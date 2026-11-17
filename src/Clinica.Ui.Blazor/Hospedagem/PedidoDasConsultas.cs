namespace Clinica.Ui.Blazor.Hospedagem;

/// <summary>
/// "Abra as Consultas de convênio já nesta situação" — o clique no contador "Consultas a
/// renovar" do Início da Recepção (nov/2026). O mesmo desenho do <see cref="PedidoDaAgendaDoDia"/>:
/// um pedido só, e ler LIMPA — pedido órfão filtraria a lista na próxima vez que alguém
/// entrasse na tela pelo menu, e filtro que ninguém escolheu esconde paciente em silêncio.
/// Quem consome é a <c>ConsultasDeConvenio</c>, na abertura e na volta à aba.
/// </summary>
public sealed class PedidoDasConsultas
{
    private readonly object _trava = new();
    private string? _situacao;

    public void Definir(string situacao)
    {
        lock (_trava) _situacao = situacao;
    }

    public string? Consumir()
    {
        lock (_trava)
        {
            var s = _situacao;
            _situacao = null;
            return s;
        }
    }
}
