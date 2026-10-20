namespace Clinica.Ui.Blazor.Hospedagem;

/// <summary>
/// O que o Início e os Pedidos da enfermagem (Blazor, out/2026) pedem à Recepção WPF e que
/// esta biblioteca não enxerga — o mesmo desenho do <see cref="IMarcacaoDeHorario"/>.
///
/// - <b>Abrir um DIA na agenda</b>: a ponte <c>PedidoAgenda</c> + a navegação, que só o módulo
///   conhece.
/// - <b>Destino existe?</b>: o Início esconde o contador cuja tela não existe neste executável
///   (era o <c>NavegacaoSuite.Existe</c> do WPF).
/// - <b>O sino e o sinal dos pedidos</b>: <c>NotificacoesEnfermagem</c> do shell — a caixa
///   relê quando outra tela deste app mexe num aviso, e abre o aviso que o sino pediu.
///
/// Onde a Recepção não existe (a prévia, os testes, outro executável) vale
/// <see cref="BalcaoIndisponivel"/>, e a tela DIZ que não deu: botão que não faz nada em
/// silêncio é o defeito da parcela 37.
/// </summary>
public interface IBalcaoDaRecepcao
{
    /// <summary>Leva a Agenda para o dia. Falso = a agenda não está disponível neste aplicativo.</summary>
    Task<bool> AbrirDiaNaAgendaAsync(DateOnly dia);

    /// <summary>A tela da chave existe neste executável (e para este acesso)?</summary>
    bool DestinoExiste(string chave);

    /// <summary>Outra tela deste app mudou um aviso da enfermagem (marcou visto, encerrou).</summary>
    event Action? PedidosMudaram;

    /// <summary>Avise as outras telas (o sino, a faixa da Agenda) que um aviso mudou.</summary>
    void SinalizarMudancaDosPedidos();

    /// <summary>O aviso que o sino pediu para abrir; ler LIMPA (pedido órfão abriria o aviso de ontem).</summary>
    int? ConsumirAberturaDePedido();
}

/// <summary>A Recepção de onde não há Recepção: responde "não disponível", e a tela diz.</summary>
public sealed class BalcaoIndisponivel : IBalcaoDaRecepcao
{
    public static readonly BalcaoIndisponivel Instancia = new();

    public Task<bool> AbrirDiaNaAgendaAsync(DateOnly dia) => Task.FromResult(false);

    /// <summary>Não sabe: mostra o contador, e o clique diz se o destino não abriu.</summary>
    public bool DestinoExiste(string chave) => true;

    // Sem shell não há outra tela para avisar; o evento existe e nunca dispara.
    public event Action? PedidosMudaram { add { } remove { } }

    public void SinalizarMudancaDosPedidos() { }

    public int? ConsumirAberturaDePedido() => null;
}
