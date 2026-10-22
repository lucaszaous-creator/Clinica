using Bunit;
using Clinica.Application.Abstracoes;
using Clinica.Application.Servicos;
using Clinica.Ui.Blazor.Moldura;
using Xunit;

namespace Clinica.Tests;

/// <summary>
/// O cartão do pedido que veio do PACIENTE (@SemDor, out/2026): o que a recepção vê e pode fazer em
/// cada passo — oferecer, esperar, ver a falha do envio e remarcar para o horário escolhido.
/// </summary>
public sealed class CartaoDoPedidoDoPacienteTests : TestContext
{
    private static readonly DateTime Sexta = new(2026, 10, 23, 15, 30, 0);

    private static CartaoChat Cartao(OfertaDoCartao oferta, string situacao = PedidosRecepcaoRegras.Aberto) =>
        new("pedido", "Paciente pediu para remarcar", "pelo WhatsApp, no lembrete",
            Linhas: [new LinhaDoCartao("09:00", "Maria da Silva", "qui, 22/10 às 09:00")], PedidoId: 7, Situacao: situacao, Oferta: oferta);

    private IRenderedComponent<CartaoDoChat> Desenhar(CartaoChat cartao, List<string> cliques) =>
        RenderComponent<CartaoDoChat>(p => p
            .Add(c => c.Cartao, cartao)
            .Add(c => c.PodeTratar, true)
            .Add(c => c.AoOferecer, (int id) => cliques.Add($"oferecer {id}"))
            .Add(c => c.AoRemarcar, (int id) => cliques.Add($"remarcar {id}")));

    private static string[] Botoes(IRenderedComponent<CartaoDoChat> c) => c.FindAll(".chat-card-botoes button").Select(b => b.TextContent.Trim()).ToArray();

    [Fact]
    public void Sem_oferta_o_botao_principal_e_oferecer_horarios()
    {
        var cliques = new List<string>();
        var c = Desenhar(Cartao(new OfertaDoCartao()), cliques);
        Assert.Equal(["Oferecer horários", "Não foi possível"], Botoes(c));
        Assert.Contains("O paciente espera os horários", c.Markup);
        c.FindAll(".chat-card-botoes button")[0].Click();
        Assert.Equal(["oferecer 7"], cliques);
    }

    [Fact]
    public void Oferta_mostra_os_horarios_e_se_chegou_ou_por_que_nao()
    {
        var enviando = Desenhar(Cartao(new OfertaDoCartao([Sexta])), []);
        Assert.Contains("Enviando ao paciente", enviando.Markup);
        Assert.Contains(PedidosRecepcaoRegras.Horario(Sexta), enviando.Markup);
        Assert.Equal(["Oferecer outros horários", "Não foi possível"], Botoes(enviando));

        var chegou = Desenhar(Cartao(new OfertaDoCartao([Sexta], Entregue: true), PedidosRecepcaoRegras.EmAndamento), []);
        Assert.Contains("Aguardando a escolha", chegou.Markup);

        var falhou = Desenhar(Cartao(new OfertaDoCartao([Sexta], Falha: "Janela de 24 h do WhatsApp fechada")), []);
        Assert.Contains("Não chegou ao paciente: Janela de 24 h do WhatsApp fechada", falhou.Find("[role=alert]").TextContent);
    }

    [Fact]
    public void Escolhido_oferece_remarcar_para_o_horario_dele_com_um_clique()
    {
        var cliques = new List<string>();
        var c = Desenhar(Cartao(new OfertaDoCartao([Sexta], true, null, Sexta), PedidosRecepcaoRegras.EmAndamento), cliques);
        Assert.Contains("Escolheu", c.Find(".chat-oferta-escolha").TextContent);
        Assert.Equal($"Remarcar para {PedidosRecepcaoRegras.Horario(Sexta)}", Botoes(c)[0]);
        c.FindAll(".chat-card-botoes button")[0].Click();
        Assert.Equal(["remarcar 7"], cliques);
    }

    [Fact]
    public void Encerrado_nao_mostra_oferta_nem_botoes()
    {
        var c = Desenhar(Cartao(new OfertaDoCartao([Sexta], true, null, Sexta), PedidosRecepcaoRegras.Concluido), []);
        Assert.Empty(c.FindAll(".chat-card-oferta"));
        Assert.Empty(c.FindAll(".chat-card-botoes"));
    }
}
