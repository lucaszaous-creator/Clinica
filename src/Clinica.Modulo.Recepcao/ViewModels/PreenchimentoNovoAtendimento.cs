namespace Clinica.Recepcao.ViewModels;

/// <summary>
/// O que a agenda quer ver preenchido ao abrir o Novo atendimento (parcela 70).
/// <see cref="DataHora"/> com hora 00:00 significa "só o dia" — a grade do vão livre
/// manda a hora clicada; o botão "Novo horário" só sabe o dia exibido.
/// </summary>
public sealed record PedidoNovoAtendimento(
    bool MarcarParaDepois, DateTime? DataHora, int? ProfissionalId, int? SalaId);

/// <summary>
/// A ponte de um clique entre a AGENDA e o NOVO ATENDIMENTO — a criação de horário foi
/// unificada lá (decisão da direção, parcela 70: "unificar tudo em um lugar só"), e o
/// gesto da parcela 58 (clicar no vão das 14h30 do Dr. Fulano e marcar ALI, sem
/// redigitar hora nem profissional) não podia se perder na mudança.
///
/// É um singleton de UM pedido, consumido na abertura da tela: quem navega DEFINE, quem
/// abre CONSOME — e consumir LIMPA, para uma navegação que falhou no meio não deixar um
/// pedido órfão esperando a próxima abertura manual da tela, que abriria pré-preenchida
/// com um clique de ontem.
/// </summary>
public sealed class PreenchimentoNovoAtendimento
{
    private PedidoNovoAtendimento? _pedido;

    public void Definir(PedidoNovoAtendimento pedido) => _pedido = pedido;

    public PedidoNovoAtendimento? Consumir()
    {
        var pedido = _pedido;
        _pedido = null;
        return pedido;
    }
}
