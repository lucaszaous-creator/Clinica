using Clinica.Application.Modelos;
using Xunit;

namespace Clinica.Tests;

/// <summary>
/// A BUSCA DA AGENDA do posto (Meu dia e Minha semana) — a regra do <c>FiltroDaAgenda</c> WPF,
/// agora em <see cref="FiltroDaAgendaNaTela"/>: cada palavra no nome sem acento, as contagens
/// das pílulas ANTES do clique, o resumo que diz que está filtrado e o vazio que pede "limpar".
/// </summary>
public class FiltroDaAgendaNaTelaTests
{
    private static readonly IReadOnlyList<ItemDaAgendaDoPosto> Itens =
    [
        new("José Antônio Silva", "Acupuntura", "pendente"),
        new("Maria Souza", "Acupuntura", "atendido"),
        new("João Pedro", "BSV", "cancelado"),
    ];

    [Fact]
    public void Cada_palavra_casa_sem_acento_e_sem_caixa()
    {
        var f = new FiltroAgendaDoPosto(Busca: "jose silva");
        Assert.True(FiltroDaAgendaNaTela.Aceitar(f, "José Antônio Silva", "Acupuntura", "pendente"));
        Assert.False(FiltroDaAgendaNaTela.Aceitar(f, "Maria Souza", "Acupuntura", "pendente"));
    }

    [Fact]
    public void As_contagens_das_pilulas_sao_sobre_busca_e_modalidade_nunca_sobre_a_propria_pilula()
    {
        var r = FiltroDaAgendaNaTela.Recortar(Itens, new FiltroAgendaDoPosto(Modalidade: "Acupuntura", Situacao: "atendido"));
        Assert.Equal(2, r.Situacoes.Single(s => s.Chave == "todos").Contagem);
        Assert.Equal(1, r.Situacoes.Single(s => s.Chave == "pendente").Contagem);
        Assert.True(r.Situacoes.Single(s => s.Chave == "atendido").Marcada);
        Assert.Equal(1, r.Visiveis);
        Assert.Equal("1 de 3 horário(s) com o filtro", r.Resumo);
    }

    [Fact]
    public void Sem_filtro_o_resumo_conta_o_periodo_e_sem_horario_nao_diz_nada()
    {
        Assert.Equal("3 horário(s) no período", FiltroDaAgendaNaTela.Recortar(Itens, new()).Resumo);
        Assert.Equal(string.Empty, FiltroDaAgendaNaTela.Recortar([], new()).Resumo);
    }

    [Fact]
    public void Nenhum_casa_com_o_filtro_e_o_vazio_de_filtro_nao_o_de_dia_sem_ninguem()
    {
        var r = FiltroDaAgendaNaTela.Recortar(Itens, new FiltroAgendaDoPosto(Busca: "ninguém"));
        Assert.True(r.SemResultado);
        Assert.False(FiltroDaAgendaNaTela.Recortar([], new FiltroAgendaDoPosto(Busca: "x")).SemResultado);
    }

    [Fact]
    public void A_modalidade_escolhida_que_sumiu_do_periodo_continua_no_combo()
    {
        var r = FiltroDaAgendaNaTela.Recortar(Itens, new FiltroAgendaDoPosto(Modalidade: "Consulta"));
        Assert.Equal(FiltroDaAgendaNaTela.TodasAsModalidades, r.Modalidades[0]);
        Assert.Contains("Consulta", r.Modalidades);
        Assert.Equal(["Todas as modalidades", "Acupuntura", "BSV", "Consulta"], r.Modalidades);
    }

    [Fact]
    public void Clicar_na_pilula_marcada_a_mantem_marcada()
    {
        var f = new FiltroAgendaDoPosto(Situacao: "pendente");
        Assert.Same(f, FiltroDaAgendaNaTela.EscolherSituacao(f, "pendente"));
        Assert.Equal("atendido", FiltroDaAgendaNaTela.EscolherSituacao(f, "atendido").Situacao);
    }

    [Fact]
    public void As_oito_pilulas_na_ordem_da_agenda()
        => Assert.Equal(["todos", "pendente", "no-local", "em-atendimento", "atendido", "cancelado", "faltou", "substituido"],
            FiltroDaAgendaNaTela.Recortar(Itens, new()).Situacoes.Select(s => s.Chave));
}
