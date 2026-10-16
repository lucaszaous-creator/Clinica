using Bunit;
using Clinica.Ui.Blazor.Componentes;
using Clinica.Ui.Blazor.Hospedagem;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Clinica.Tests;

/// <summary>
/// O palco (out/2026): UM navegador por janela, e as telas trocando DENTRO dele. É ele que
/// tirou o flash preto de criar um BlazorWebView a cada tela. O que se prende aqui é o que a
/// pessoa percebe: a tela aberta continua montada (volta como foi deixada), só a atual fica
/// à vista, voltar a ela RELÊ, e o hospedeiro só é liberado depois de haver tela desenhada.
/// </summary>
public sealed class PalcoDeTelasTests : TestContext
{
    public PalcoDeTelasTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddTelasBlazor();
    }

    private sealed class TelaA : ComponentBase
    {
        [CascadingParameter] public SinalDeVolta? Sinal { get; set; }
        public static int Voltas;
        protected override void OnInitialized() { if (Sinal is not null) Sinal.Voltou += () => Voltas++; }
        protected override void BuildRenderTree(RenderTreeBuilder b) => b.AddMarkupContent(0, "<p class=\"tela-a\">A</p>");
    }

    private sealed class TelaB : ComponentBase
    {
        protected override void BuildRenderTree(RenderTreeBuilder b) => b.AddMarkupContent(0, "<p class=\"tela-b\">B</p>");
    }

    [Fact]
    public void Trocar_de_tela_mantem_a_anterior_montada_e_oculta_e_voltar_rele()
    {
        TelaA.Voltas = 0;
        var palco = new Palco();
        var montou = 0;
        palco.Montou += () => montou++;

        var r = RenderComponent<PalcoDeTelas>(p => p.Add(x => x.Palco, palco));
        Assert.Equal(0, montou); // sem tela, nada a liberar

        palco.Ativar(typeof(TelaA));
        r.WaitForAssertion(() => Assert.Single(r.FindAll(".tela-a")));
        Assert.Equal(1, montou);

        palco.Ativar(typeof(TelaB));
        r.WaitForAssertion(() => Assert.Single(r.FindAll(".tela-b")));
        var conteineres = r.FindAll(".palco-tela");
        Assert.Equal(2, conteineres.Count);           // A continua montada…
        Assert.True(conteineres[0].HasAttribute("hidden"));   // …mas oculta
        Assert.False(conteineres[1].HasAttribute("hidden"));
        Assert.Equal(0, TelaA.Voltas);

        palco.Ativar(typeof(TelaA));                  // voltar à A: mesma instância, relê
        r.WaitForAssertion(() => Assert.False(r.FindAll(".palco-tela")[0].HasAttribute("hidden")));
        Assert.Equal(2, r.FindAll(".palco-tela").Count);
        Assert.Equal(1, TelaA.Voltas);
        Assert.Equal(1, montou); // liberar acontece UMA vez por navegador
    }

    [Fact]
    public void Tela_WPF_a_vista_desativa_e_a_volta_a_mesma_tela_rele()
    {
        TelaA.Voltas = 0;
        var palco = new Palco();
        var r = RenderComponent<PalcoDeTelas>(p => p.Add(x => x.Palco, palco));
        palco.Ativar(typeof(TelaA));
        palco.Ativar(typeof(TelaA));   // a mesma tela já à vista não relê
        Assert.Equal(0, TelaA.Voltas);

        palco.Desativar();             // o shell foi para uma tela WPF
        r.WaitForAssertion(() => Assert.True(r.Find(".palco-tela").HasAttribute("hidden")));
        palco.Ativar(typeof(TelaA));
        Assert.Equal(1, TelaA.Voltas);
    }

    [Fact]
    public void Dialogos_e_notificacoes_existem_uma_vez_por_navegador()
    {
        var palco = new Palco();
        palco.Ativar(typeof(TelaA));
        palco.Ativar(typeof(TelaB));
        var r = RenderComponent<PalcoDeTelas>(p => p.Add(x => x.Palco, palco));
        Assert.Equal(1, r.FindComponents<CamadaDeDialogos>().Count);
        Assert.Equal(1, r.FindComponents<PilhaDeNotificacoes>().Count);
    }
}
