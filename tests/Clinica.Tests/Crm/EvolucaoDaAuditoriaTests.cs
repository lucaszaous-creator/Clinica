using Clinica.Application.Abstracoes;
using Clinica.Application.Servicos;
using Clinica.Crm.Contracts;
using Xunit;

namespace Clinica.Tests;

/// <summary>
/// A Evolução da auditoria do atendimento (27/10): a direção quer ver se a devolutiva faz efeito. O gráfico (escala, buraco
/// sem dado, semana corrente parcial, rótulos, cor por pessoa) e a frase de tendência (as 4 semanas completas mais recentes
/// contra as 4 anteriores, sem a corrente) moram em <see cref="EvolucaoNaTela"/>, puros.
/// </summary>
[Collection(ColecaoDiagnostico.Nome)] // a falha da evolução vai ao Diagnostico: em fila com os testes que o leem
public sealed class EvolucaoDaAuditoriaTests
{
    static readonly DateOnly Segunda = new(2026, 10, 26);

    static DateOnly[] Semanas(int n) => Enumerable.Range(0, n).Select(i => Segunda.AddDays(-7 * (n - 1 - i))).ToArray();

    /// <summary>Uma secretária com uma nota por semana (nulo = sem avaliação) e a 1ª resposta fixa, salvo indicação.</summary>
    static AppQualityTrendAgent Pessoa(string id, string nome, decimal?[] notas, double?[]? respostas = null, int avaliacoes = 1)
    {
        var semanas = Semanas(notas.Length);
        return new AppQualityTrendAgent(id, nome, notas.Select((n, i) => new AppQualityWeek(semanas[i], 10, respostas?[i] ?? 8.0, 8, 9, n,
            n is null ? 0 : avaliacoes)).ToArray());
    }

    static AppQualityTrend Evolucao(params AppQualityTrendAgent[] pessoas)
        => new(Semanas(pessoas[0].Weeks.Length), pessoas);

    // ------------------------------------------------------------------ o gráfico

    [Fact]
    public void Semana_sem_dado_e_buraco_na_linha_nunca_zero()
    {
        var t = Evolucao(Pessoa("ana", "Ana", [70, 72, null, 80, 82, 84, 86, 88]));
        var g = EvolucaoNaTela.GraficoDaNota(t, EvolucaoNaTela.Cores(t.Agents));
        var linha = Assert.Single(g.Linhas);

        Assert.Null(linha.Pontos[2].Y);
        Assert.Null(linha.Pontos[2].Valor);
        // Dois trechos sólidos (antes e depois do buraco) e o tracejado que chega à semana corrente.
        Assert.Equal([false, false, true], linha.Trechos.Select(tr => tr.Parcial));
        Assert.Equal(2, linha.Trechos[0].Pontos.Split(' ').Length);
        Assert.Equal(4, linha.Trechos[1].Pontos.Split(' ').Length);
        Assert.DoesNotContain(linha.Trechos, tr => tr.Pontos.Contains($",{EvolucaoNaTela.N(GraficoDaEvolucao.Base)}"));
    }

    [Fact]
    public void Escala_da_nota_e_fixa_de_0_a_100_e_a_x_vai_da_primeira_a_ultima_semana()
    {
        var t = Evolucao(Pessoa("ana", "Ana", [100, 0, 50, 50]));
        var g = EvolucaoNaTela.GraficoDaNota(t, EvolucaoNaTela.Cores(t.Agents));
        var p = g.Linhas[0].Pontos;
        Assert.Equal(GraficoDaEvolucao.Topo, p[0].Y);
        Assert.Equal(GraficoDaEvolucao.Base, p[1].Y);
        Assert.Equal((GraficoDaEvolucao.Topo + GraficoDaEvolucao.Base) / 2, p[2].Y);
        Assert.Equal(GraficoDaEvolucao.Esq, p[0].X);
        Assert.Equal(GraficoDaEvolucao.Largura - GraficoDaEvolucao.Dir, p[^1].X);
        Assert.Equal(["0", "50", "100"], g.Marcas.Select(m => m.Texto));
        Assert.Null(g.Referencia);
    }

    [Fact]
    public void Escala_da_resposta_tem_teto_redondo_que_inclui_o_prazo_e_a_linha_de_referencia()
    {
        var t = Evolucao(Pessoa("ana", "Ana", [80, 80, 80, 80], [6, 7, 8, 9]));
        var g = EvolucaoNaTela.GraficoDaResposta(t, EvolucaoNaTela.Cores(t.Agents));
        Assert.Equal(["0", "7,5", "15"], g.Marcas.Select(m => m.Texto));
        Assert.Equal("prazo 15 min", g.Referencia!.Texto);
        Assert.Equal(GraficoDaEvolucao.Topo, g.Referencia.Y);

        Assert.Equal(15, EvolucaoNaTela.TetoRedondo(12));
        Assert.Equal(30, EvolucaoNaTela.TetoRedondo(22));
        Assert.Equal(90, EvolucaoNaTela.TetoRedondo(61));
    }

    [Fact]
    public void Semana_corrente_e_parcial_tracejada_e_ponto_isolado_vira_marcador()
    {
        var t = Evolucao(Pessoa("ana", "Ana", [70, null, 75, null, 80, 82]));
        var linha = EvolucaoNaTela.GraficoDaNota(t, EvolucaoNaTela.Cores(t.Agents)).Linhas[0];

        Assert.True(linha.Pontos[^1].Parcial);
        Assert.Equal(1, linha.Trechos.Count(tr => tr.Parcial));
        // 70 e 75 não têm vizinho: sem marcador, sumiriam. A ponta (a corrente) também leva o seu.
        Assert.Equal([0, 2, 5], linha.Marcadores.Select(p => p.Indice));
        Assert.True(linha.Marcadores[^1].Parcial);

        var semCorrente = Evolucao(Pessoa("ana", "Ana", [70, 72, 74, null]));
        var l2 = EvolucaoNaTela.GraficoDaNota(semCorrente, EvolucaoNaTela.Cores(semCorrente.Agents)).Linhas[0];
        Assert.DoesNotContain(l2.Trechos, tr => tr.Parcial);
        Assert.Equal([2], l2.Marcadores.Select(p => p.Indice));
    }

    [Fact]
    public void Colunas_da_semana_tem_rotulo_dd_MM_a_dica_com_todas_e_a_corrente_marcada()
    {
        var t = Evolucao(Pessoa("ana", "Ana Lima", [80, 82, 84, 86], avaliacoes: 2), Pessoa("bia", "Bia Reis", [null, 70, 72, 74]));
        var g = EvolucaoNaTela.GraficoDaNota(t, EvolucaoNaTela.Cores(t.Agents));

        Assert.Equal(["05/10", "12/10", "19/10", "26/10"], g.Semanas.Select(s => s.Rotulo));
        Assert.True(g.Semanas[^1].Parcial);
        Assert.Contains("parcial", g.Semanas[^1].Dica);
        Assert.Contains("Ana Lima: 80 (2 avaliações)", g.Semanas[0].Dica);
        Assert.Contains("Bia Reis: sem avaliação", g.Semanas[0].Dica);
    }

    [Fact]
    public void Com_doze_semanas_um_rotulo_sim_um_nao_e_a_corrente_sempre_aparece()
    {
        Assert.All(Enumerable.Range(0, 8), i => Assert.True(EvolucaoNaTela.MostraRotulo(i, 8)));
        Assert.True(EvolucaoNaTela.MostraRotulo(11, 12));
        Assert.False(EvolucaoNaTela.MostraRotulo(10, 12));
        Assert.True(EvolucaoNaTela.MostraRotulo(9, 12));
    }

    [Fact]
    public void A_cor_segue_a_pessoa_pelo_nome_e_da_quinta_em_diante_e_cinza()
    {
        var t = Evolucao(Pessoa("z", "Zélia", [80]), Pessoa("a", "Ana", [80]), Pessoa("c", "Carla", [80]), Pessoa("b", "Bruna", [80]), Pessoa("d", "Dora", [80]));
        var cores = EvolucaoNaTela.Cores(t.Agents);
        Assert.Equal((1, 2, 3, 4, 0), (cores["a"], cores["b"], cores["c"], cores["d"], cores["z"]));

        // Recortar a janela ou tirar alguém não repinta os outros.
        var semZelia = EvolucaoNaTela.Cores(t.Agents.Where(a => a.AgentId != "z"));
        Assert.Equal(cores["c"], semZelia["c"]);
    }

    [Fact]
    public void Nome_na_ponta_so_sem_rotulos_colados_e_ate_quatro_pessoas()
    {
        var separadas = Evolucao(Pessoa("a", "Ana", [90, 90]), Pessoa("b", "Bia", [40, 40]));
        Assert.True(EvolucaoNaTela.GraficoDaNota(separadas, EvolucaoNaTela.Cores(separadas.Agents)).RotulosDiretos);

        var coladas = Evolucao(Pessoa("a", "Ana", [80, 80]), Pessoa("b", "Bia", [82, 82]));
        var g = EvolucaoNaTela.GraficoDaNota(coladas, EvolucaoNaTela.Cores(coladas.Agents));
        Assert.False(g.RotulosDiretos); // a legenda e a tabela dizem; empurrar um rótulo o descolaria da linha
    }

    [Fact]
    public void Sem_nenhum_dado_o_grafico_diz_que_nao_tem()
    {
        var t = Evolucao(Pessoa("a", "Ana", [null, null, null]));
        Assert.False(EvolucaoNaTela.GraficoDaNota(t, EvolucaoNaTela.Cores(t.Agents)).TemDado);
    }

    [Fact]
    public void Recorte_fica_com_as_ultimas_semanas()
    {
        var t = Evolucao(Pessoa("a", "Ana", [1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12]));
        var r = EvolucaoNaTela.Recorte(t, 8);
        Assert.Equal(8, r.Weeks.Length);
        Assert.Equal(t.Weeks[4..], r.Weeks);
        Assert.Equal(5m, r.Agents[0].Weeks[0].AverageScore);
    }

    // ------------------------------------------------------------------ a tendência

    [Fact]
    public void Tendencia_compara_as_4_completas_com_as_4_anteriores_e_ignora_a_corrente()
    {
        // 4 anteriores ≈ 70, 4 recentes ≈ 82, a corrente (parcial) com 20 não entra.
        var a = Pessoa("a", "Ana", [99, 70, 70, 70, 70, 82, 82, 82, 82, 20], avaliacoes: 2);
        var t = EvolucaoNaTela.Tendencia(a, 1);
        Assert.Equal("Nota subiu 12 pontos nas últimas 4 semanas (de 70 para 82).", t.Nota);
        Assert.Equal(("sucesso", "subiu"), (t.TomDaNota, t.RotuloDaNota));

        var caiu = EvolucaoNaTela.Tendencia(Pessoa("b", "Bia", [80, 80, 80, 80, 60, 60, 60, 60, 99], avaliacoes: 2));
        Assert.Equal("Nota caiu 20 pontos nas últimas 4 semanas (de 80 para 60).", caiu.Nota);
        Assert.Equal("perigo", caiu.TomDaNota);
    }

    [Fact]
    public void Tendencia_estavel_e_sem_avaliacoes_suficientes()
    {
        var estavel = EvolucaoNaTela.Tendencia(Pessoa("a", "Ana", [80, 78, 80, 78, 80, 82, 80, 82, 0], avaliacoes: 2));
        Assert.Equal("Nota estável (81 nas últimas 4 semanas).", estavel.Nota);
        Assert.Equal("neutro", estavel.TomDaNota);

        var pouca = EvolucaoNaTela.Tendencia(Pessoa("b", "Bia", [null, null, null, 70, null, null, null, 90, 90]));
        Assert.Equal("Nota: sem avaliações suficientes para comparar.", pouca.Nota);

        var curta = EvolucaoNaTela.Tendencia(Pessoa("c", "Cris", [80, 80]));
        Assert.Equal("Nota: sem avaliações suficientes para comparar.", curta.Nota);
    }

    [Fact]
    public void Tendencia_da_primeira_resposta()
    {
        var piorou = EvolucaoNaTela.Tendencia(Pessoa("a", "Ana", [80, 80, 80, 80, 80, 80, 80, 80, 80],
            [6, 6, 6, 6, 12, 12, 12, 12, 30]));
        Assert.Equal("1ª resposta piorou: de 6 min para 12 min.", piorou.Resposta);

        var melhorou = EvolucaoNaTela.Tendencia(Pessoa("b", "Bia", [80, 80, 80, 80, 80, 80, 80, 80, 80],
            [20, 20, 20, 20, 9, 9, 9, 9, 1]));
        Assert.Equal("1ª resposta melhorou: de 20 min para 9 min.", melhorou.Resposta);

        var estavel = EvolucaoNaTela.Tendencia(Pessoa("c", "Cris", [80, 80, 80, 80, 80, 80, 80, 80, 80],
            [7, 7, 7, 7, 7.5, 7.5, 7.5, 7.5, 7]));
        Assert.Equal("1ª resposta estável (cerca de 8 min).", estavel.Resposta);
    }

    [Fact]
    public void Tabela_tem_a_corrente_marcada_e_traco_onde_nao_ha_dado()
    {
        var t = Evolucao(Pessoa("a", "Ana", [80, null, 84], avaliacoes: 2));
        var cores = EvolucaoNaTela.Cores(t.Agents);
        Assert.Equal(["12/10", "19/10", "26/10 (parcial)"], EvolucaoNaTela.CabecalhoDaTabela(t));
        Assert.Equal(["80 (2)", "—", "84 (2)"], EvolucaoNaTela.TabelaDaNota(t, cores)[0].Valores);
        Assert.Equal(["8 min", "8 min", "8 min"], EvolucaoNaTela.TabelaDaResposta(t, cores)[0].Valores);
    }

    // ------------------------------------------------------------------ a leitura

    [Fact]
    public async Task A_falha_da_evolucao_nao_derruba_o_painel_e_o_403_vira_sem_permissao()
    {
        var crm = new PreviaBlazor.CrmDeMentira(null);
        var servico = new AuditoriaDoAtendimentoService(crm);

        var painel = await servico.LerAsync(7);
        Assert.NotNull(painel.Evolucao?.Dados);
        Assert.Equal(EvolucaoNaTela.SemanasLidas, painel.Evolucao!.Dados!.Weeks.Length);

        crm.FalhaDaEvolucao = new CrmIndisponivel("caiu");
        var semEvolucao = await servico.LerAsync(7);
        Assert.NotEmpty(semEvolucao.Painel.Agents);
        Assert.Null(semEvolucao.Evolucao!.Dados);
        Assert.Contains("Sem ligação", semEvolucao.Evolucao.Erro);
        Assert.False(semEvolucao.Evolucao.SemPermissao);

        crm.FalhaDaEvolucao = new RecusaDoCrm("Esta função está disponível para supervisores e administradores.", 403);
        var proibida = await servico.LerEvolucaoAsync();
        Assert.True(proibida.SemPermissao);
        Assert.Contains("administrador ou supervisor", proibida.Erro);
    }
}
