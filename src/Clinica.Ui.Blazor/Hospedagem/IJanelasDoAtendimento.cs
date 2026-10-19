using Clinica.Application.Servicos;

namespace Clinica.Ui.Blazor.Hospedagem;

/// <summary>
/// O pedido que outra tela deixou para o Lançar/Marcar (a ponte <c>PreenchimentoNovoAtendimento</c>
/// da Recepção, que esta biblioteca não enxerga): o vão clicado na grade, o "Marcar horário" dos
/// Retornos a marcar, o Acompanhamento. Hora 00:00 = "só o dia".
/// </summary>
public sealed record PedidoDoAtendimento(
    DateTime? DataHora, int? ProfissionalId, int? SalaId,
    int? PacienteId = null, int? DuracaoMinutos = null, string? ModalidadeCodigo = null);

/// <summary>
/// O que a tela "Lançar e marcar atendimento" (Blazor, out/2026) pede à Recepção WPF e que
/// esta biblioteca não enxerga — o mesmo desenho do <see cref="IJanelasDaAgenda"/>:
///
/// - <b>A ponte do pedido</b>: quem navega para cá DEFINE, a tela CONSOME — e consumir limpa
///   (pedido órfão pré-preencheria a abertura de amanhã com o clique de ontem). Só o pedido do
///   MODO desta aba: a aba Lançar não engole o pedido da aba Marcar.
/// - <b>As janelas modais que continuam WPF</b>: escolher o convênio da ficha "a definir"
///   (<c>VinculoDeConvenio</c>, parcela 92), o fechamento da sessão (pacote, insumo, caixa —
///   <c>FechamentoSessaoWindow</c>), a cobrança da dívida (<c>CobrancaDoPacienteWindow</c>) e o
///   cadastro do paciente
///   (<c>CadastroPacienteWindow</c>). Reescrevê-las seria a segunda definição de cada uma.
///   A VENDA DE PACOTE saiu daqui em out/2026: é Blazor (<c>PacoteVendaForm</c>), e a tela a
///   abre direto, com o paciente já escolhido pelo parâmetro.
///
/// A barreira de permissão é da TELA (as duas barreiras moram lá); as janelas conferem de
/// novo ao salvar, como sempre conferiram. Onde a Recepção não existe (a prévia, o bUnit) vale
/// <see cref="JanelasDoAtendimentoIndisponiveis"/>, e a tela DIZ que a ação é do aplicativo da
/// Recepção — botão que não faz nada em silêncio é o defeito da parcela 41.
/// </summary>
public interface IJanelasDoAtendimento
{
    /// <summary>Falso onde as janelas não existem: a tela avisa em vez de abrir nada.</summary>
    bool Disponivel { get; }

    /// <summary>Há pedido do modo desta aba? Lê SEM consumir (a volta à aba decide se aplica).</summary>
    bool HaPedido(bool marcar);

    /// <summary>Consome o pedido do modo desta aba (nulo = não havia, ou era da outra aba).</summary>
    PedidoDoAtendimento? ConsumirPedido(bool marcar);

    /// <summary>Pergunta o convênio da ficha "a definir". Verdadeiro = a ficha tem convênio agora.</summary>
    Task<bool> EscolherConvenioAsync(int pacienteId);


    /// <summary>A cobrança da dívida no balcão. Verdadeiro = algo foi recebido (o alerta muda).</summary>
    Task<bool> ReceberDividaAsync(int pacienteId);

    /// <summary>O cadastro de um paciente novo. Devolve o id da ficha criada (nulo = desistiu).</summary>
    Task<int?> CadastrarPacienteAsync();
}

/// <summary>Onde não há janelas WPF (a prévia, os testes): tudo responde "não disponível", e a tela diz.</summary>
public sealed class JanelasDoAtendimentoIndisponiveis : IJanelasDoAtendimento
{
    public static readonly JanelasDoAtendimentoIndisponiveis Instancia = new();

    public const string Frase = "Esta janela é do aplicativo da Recepção — abra o Lançar/Marcar por lá.";

    public bool Disponivel => false;
    public bool HaPedido(bool marcar) => false;
    public PedidoDoAtendimento? ConsumirPedido(bool marcar) => null;
    public Task<bool> EscolherConvenioAsync(int pacienteId) => Task.FromResult(false);
    public Task<bool> ReceberDividaAsync(int pacienteId) => Task.FromResult(false);
    public Task<int?> CadastrarPacienteAsync() => Task.FromResult<int?>(null);
}
