using System.Text.RegularExpressions;
using Clinica.Ui.Blazor.Hospedagem;
using Xunit;

namespace Clinica.Tests;

/// <summary>
/// A base comum das telas Blazor (out/2026, docs/telas-blazor.md): os diálogos em HTML
/// seguem as MESMAS regras do shell — a pessoa não pode aprender duas — e toda tela herda
/// a TelaBase, que é onde moram as duas barreiras e o escopo por operação.
/// </summary>
public class TelasBlazorBaseTests
{
    [Fact]
    public async Task Cancelar_uma_pergunta_de_texto_devolve_null_e_nunca_texto_vazio()
    {
        // Checagem 39: a porta rotulada "desisti" não pode ser a que efetiva o ato.
        var d = new Dialogos();
        var resposta = d.PerguntarTextoAsync("Recibo", "Para quem?", obrigatorio: false);
        d.Responder(d.Atual!, confirmou: false);
        Assert.Null(await resposta);
    }

    [Fact]
    public async Task Pergunta_opcional_aceita_o_vazio_como_resposta()
    {
        var d = new Dialogos();
        var resposta = d.PerguntarTextoAsync("Recibo", "Para quem?", obrigatorio: false);
        d.Responder(d.Atual!, confirmou: true);
        Assert.Equal(string.Empty, await resposta);
    }

    [Fact]
    public async Task Pergunta_obrigatoria_recusa_o_vazio_e_continua_aberta()
    {
        var d = new Dialogos();
        var resposta = d.PerguntarTextoAsync("Cancelar", "Por quê?");
        var aberto = d.Atual!;
        d.Responder(aberto, confirmou: true);
        Assert.False(resposta.IsCompleted);
        Assert.Same(aberto, d.Atual);
        Assert.NotNull(aberto.Erro);

        aberto.Texto = "  pago em duplicidade  ";
        d.Responder(aberto, confirmou: true);
        Assert.Equal("pago em duplicidade", await resposta);
        Assert.Null(d.Atual);
    }

    [Fact]
    public async Task Um_dialogo_por_vez_o_segundo_espera_o_primeiro()
    {
        var d = new Dialogos();
        var primeiro = d.ConfirmarAsync("A", "a?");
        var segundo = d.ConfirmarAsync("B", "b?");
        Assert.Equal("A", d.Atual!.Titulo);
        d.Responder(d.Atual!, confirmou: true);
        Assert.True(await primeiro);
        Assert.Equal("B", d.Atual!.Titulo);
        d.Responder(d.Atual!, confirmou: false);
        Assert.False(await segundo);
    }

    [Fact]
    public void Notificacao_de_erro_fica_e_a_pilha_nao_passa_de_tres()
    {
        var n = new Notificacoes();
        for (var i = 0; i < 5; i++) n.Info($"aviso {i}");
        Assert.Equal(3, n.Ativas.Count);
        Assert.Equal("aviso 4", n.Ativas[^1].Texto);
    }

    /// <summary>
    /// Toda TELA herda a TelaBase. Uma tela que injetasse serviço da Application direto
    /// guardaria o DbContext da abertura pela vida inteira da aba (checagem 37 pelo outro
    /// lado), e uma que chamasse SessaoUsuario.Exigir solto num evento derrubaria o
    /// circuito ao negar. O piloto do Fluxo de caixa é a exceção declarada: ele nasceu antes
    /// da base e não grava nada.
    /// </summary>
    [Fact]
    public void Toda_tela_herda_a_TelaBase_e_nao_injeta_servico_da_Application()
    {
        var pasta = Path.Combine(Raiz(), "src", "Clinica.Ui.Blazor", "Telas");
        var telas = Directory.GetFiles(pasta, "*.razor");
        Assert.NotEmpty(telas);
        foreach (var arquivo in telas)
        {
            var nome = Path.GetFileNameWithoutExtension(arquivo);
            var texto = File.ReadAllText(arquivo);
            if (nome == "FluxoDeCaixa") continue;
            Assert.True(texto.Contains("@inherits TelaBase"), $"{nome}.razor não herda a TelaBase");
            Assert.False(Regex.IsMatch(texto, @"@inject\s+\w*Service\b"), $"{nome}.razor injeta serviço da Application — abra escopo por operação");
        }
    }

    private static string Raiz()
    {
        var d = new DirectoryInfo(AppContext.BaseDirectory);
        while (d is not null && !File.Exists(Path.Combine(d.FullName, "Clinica.sln"))) d = d.Parent;
        return d?.FullName ?? throw new InvalidOperationException("Clinica.sln não encontrado.");
    }
}
