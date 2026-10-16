using Clinica.Application.Modelos;
using Clinica.Domain;
using Clinica.Domain.Entities;
using Clinica.Domain.Regras;
using Xunit;

namespace Clinica.Tests;

/// <summary>
/// O que as telas de Glosas e recursos e de Não conformidades do faturamento AFIRMAM
/// (migração para Blazor, out/2026) — as regras que moravam no GlosasViewModel e no
/// NaoConformidadesViewModel do WPF, onde o `dotnet test` não alcançava.
/// </summary>
public class GlosasNaTelaTests
{
    private static readonly DateOnly Hoje = new(2026, 10, 16);

    private static CodigoFaturamento Glosa(int id, StatusGlosa status, int diasParaPrazo, string paciente = "Maria José",
        string? guia = "900100", string? motivo = "1201")
        => new()
        {
            Id = id,
            Tipo = TipoCodigo.Acupuntura,
            Glosa = status,
            DataGlosa = Hoje.AddDays(diasParaPrazo - 30),
            DataLimiteRecurso = Hoje.AddDays(diasParaPrazo),
            MotivoGlosaCodigo = motivo,
            NumeroGuiaReal = guia,
            Atendimento = new Atendimento { PacienteId = id, Paciente = new Paciente { Id = id, Nome = paciente } }
        };

    [Theory]
    [InlineData(-1, NivelUrgencia.Vermelho)]
    [InlineData(3, NivelUrgencia.Vermelho)]
    [InlineData(4, NivelUrgencia.Amarelo)]
    [InlineData(7, NivelUrgencia.Amarelo)]
    [InlineData(8, NivelUrgencia.Verde)]
    public void O_semaforo_do_prazo_de_recurso_e_o_do_WPF(int dias, NivelUrgencia esperado)
        => Assert.Equal(esperado, GlosasNaTela.Urgencia(dias));

    [Fact]
    public void Glosa_sem_prazo_e_verde_e_nao_conta_como_vencida()
    {
        var linha = GlosasNaTela.Linha(Glosa(1, StatusGlosa.Recuperada, -10), Hoje);
        Assert.Null(linha.DiasParaFimPrazo);
        Assert.Equal(NivelUrgencia.Verde, linha.Urgencia);
        Assert.False(linha.PrazoVencido);
        Assert.Equal(string.Empty, linha.PrazoRotulo);
        Assert.False(new FiltroGlosas(SomenteEmAberto: false, SoPrazoVencido: true).Casa(linha));
    }

    [Fact]
    public void O_rotulo_do_prazo_diz_vencido_ou_os_dias()
    {
        Assert.EndsWith("(vencido)", GlosasNaTela.Linha(Glosa(1, StatusGlosa.Glosada, -2), Hoje).PrazoRotulo);
        Assert.EndsWith("(5 d)", GlosasNaTela.Linha(Glosa(2, StatusGlosa.Glosada, 5), Hoje).PrazoRotulo);
    }

    [Fact]
    public void Reapresentar_so_na_glosada_e_recuperar_so_em_aberto()
    {
        var glosada = GlosasNaTela.Linha(Glosa(1, StatusGlosa.Glosada, 10), Hoje);
        var reapresentada = GlosasNaTela.Linha(Glosa(2, StatusGlosa.Reapresentada, 10), Hoje);
        var recuperada = GlosasNaTela.Linha(Glosa(3, StatusGlosa.Recuperada, 10), Hoje);

        Assert.True(glosada.PodeReapresentar);
        Assert.False(reapresentada.PodeReapresentar);
        Assert.False(recuperada.PodeReapresentar);
        Assert.True(glosada.PodeRecuperar);
        Assert.True(reapresentada.PodeRecuperar);
        Assert.False(recuperada.PodeRecuperar);
        Assert.Equal("Reapresentada", reapresentada.Situacao);
    }

    [Fact]
    public void O_motivo_mostra_o_catalogo_o_codigo_cru_ou_o_complemento()
    {
        var catalogado = Glosa(1, StatusGlosa.Glosada, 5, motivo: "1201");
        catalogado.MotivoGlosa = "Venceu em agosto";
        var l1 = GlosasNaTela.Linha(catalogado, Hoje);
        Assert.True(l1.MotivoCatalogado);
        Assert.Equal(MotivosGlosa.Descricao("1201"), l1.MotivoPrincipal);
        Assert.Equal("Venceu em agosto", l1.MotivoComplemento);

        var foraDoCatalogo = GlosasNaTela.Linha(Glosa(2, StatusGlosa.Glosada, 5, motivo: "1801"), Hoje);
        Assert.False(foraDoCatalogo.MotivoCatalogado);
        Assert.Equal("Código ANS 1801", foraDoCatalogo.MotivoPrincipal);

        var semCodigo = Glosa(3, StatusGlosa.Glosada, 5, motivo: null);
        semCodigo.MotivoGlosa = "Duplicidade de cobrança";
        var l3 = GlosasNaTela.Linha(semCodigo, Hoje);
        Assert.Equal("Duplicidade de cobrança", l3.MotivoPrincipal);
        Assert.Null(l3.MotivoComplemento); // não repete o mesmo texto embaixo

        Assert.Equal("Motivo não informado", GlosasNaTela.Linha(Glosa(4, StatusGlosa.Glosada, 5, motivo: null), Hoje).MotivoPrincipal);
    }

    [Fact]
    public void Motivo_recorrente_fora_do_catalogo_nao_repete_o_codigo_nem_oferece_o_guia()
    {
        var todas = GlosasNaTela.Montar([Glosa(1, StatusGlosa.Glosada, 5, motivo: "1801")], Hoje);
        var m = Assert.Single(GlosasNaTela.MotivosRecorrentes(todas, somenteEmAberto: true));
        Assert.False(m.Catalogado);
        Assert.Equal("1× · 1801 — código fora do catálogo", m.Rotulo);
    }

    [Fact]
    public void O_resumo_diz_que_a_lista_e_parcial()
    {
        var todas = GlosasNaTela.Montar([Glosa(1, StatusGlosa.Glosada, 5), Glosa(2, StatusGlosa.Recuperada, 5)], Hoje);
        var padrao = new FiltroGlosas();
        var mostradas = GlosasNaTela.Filtrar(todas, padrao);

        Assert.Single(mostradas);
        Assert.False(padrao.Ativo); // o "em aberto" é o recorte padrão, não acende o Limpar
        Assert.Equal("1 glosa(s) em aberto de 2 no total", GlosasNaTela.Resumo(mostradas.Count, todas.Count, padrao));
        Assert.Equal("2 glosa(s)", GlosasNaTela.Resumo(2, 2, padrao with { SomenteEmAberto = false }));
        Assert.Equal("0 de 2 glosa(s) no filtro", GlosasNaTela.Resumo(0, 2, padrao with { Guia = "777" }));
    }

    [Fact]
    public void O_filtro_de_paciente_ignora_acento_e_o_de_guia_casa_por_trecho()
    {
        var todas = GlosasNaTela.Montar([Glosa(1, StatusGlosa.Glosada, 5, "José Antônio", "123456")], Hoje);
        Assert.Single(GlosasNaTela.Filtrar(todas, new FiltroGlosas(Paciente: "jose antonio")));
        Assert.Single(GlosasNaTela.Filtrar(todas, new FiltroGlosas(Guia: "345")));
        Assert.Empty(GlosasNaTela.Filtrar(todas, new FiltroGlosas(Guia: "999")));
    }

    [Fact]
    public void O_vazio_do_filtro_e_o_vazio_do_em_aberto_tem_frases_diferentes()
    {
        Assert.Equal("Nenhuma glosa bate com o filtro", GlosasNaTela.Vazio(3, new FiltroGlosas(Paciente: "x")).Titulo);
        Assert.Equal("Nenhuma glosa em aberto", GlosasNaTela.Vazio(3, new FiltroGlosas()).Titulo);
        Assert.Equal("Nenhuma glosa registrada", GlosasNaTela.Vazio(0, new FiltroGlosas()).Titulo);
    }

    [Fact]
    public void Os_motivos_recorrentes_sao_da_clinica_e_nao_do_texto_digitado()
    {
        var todas = GlosasNaTela.Montar(
        [
            Glosa(1, StatusGlosa.Glosada, 5, "Ana", motivo: "1201"),
            Glosa(2, StatusGlosa.Glosada, 5, "Bia", motivo: "1201"),
            Glosa(3, StatusGlosa.Glosada, 5, "Caio", motivo: "1302"),
            Glosa(4, StatusGlosa.Recuperada, 5, "Davi", motivo: "1302"),
            Glosa(5, StatusGlosa.Recuperada, 5, "Eva", motivo: "1302")
        ], Hoje);

        var emAberto = GlosasNaTela.MotivosRecorrentes(todas, somenteEmAberto: true);
        Assert.Equal("1201", emAberto[0].Codigo);
        Assert.Equal(2, emAberto[0].Ocorrencias);

        var tudo = GlosasNaTela.MotivosRecorrentes(todas, somenteEmAberto: false);
        Assert.Equal("1302", tudo[0].Codigo);
        Assert.Equal(3, tudo[0].Ocorrencias);
        Assert.StartsWith("3× · 1302 — ", tudo[0].Rotulo);
    }

    [Fact]
    public void O_recurso_leva_so_as_glosas_em_aberto_da_lista_na_tela()
    {
        var todas = GlosasNaTela.Montar(
            [Glosa(1, StatusGlosa.Glosada, 5), Glosa(2, StatusGlosa.Reapresentada, 5), Glosa(3, StatusGlosa.Recuperada, 5)], Hoje);
        var recurso = GlosasNaTela.ParaRecurso(todas);
        Assert.Equal([1, 2], recurso.Select(c => c.Id).ToArray());
    }

    [Fact]
    public void A_contagem_e_sobre_todas_as_glosas_em_aberto()
    {
        var todas = GlosasNaTela.Montar(
        [
            Glosa(1, StatusGlosa.Glosada, -3),
            Glosa(2, StatusGlosa.Glosada, 0),
            Glosa(3, StatusGlosa.Reapresentada, 7),
            Glosa(4, StatusGlosa.Glosada, 20),
            Glosa(5, StatusGlosa.Recuperada, -3)
        ], Hoje);
        var c = GlosasNaTela.Contagem(todas);
        Assert.Equal(4, c.EmAberto);
        Assert.Equal(1, c.Vencidas);
        Assert.Equal(2, c.VencendoEmSete);
    }

    [Fact]
    public void A_busca_do_guia_de_motivos_acha_por_codigo_e_por_palavra_sem_acento()
    {
        Assert.Equal(MotivosGlosa.Todos.Count, GlosasNaTela.FiltrarMotivos(null).Count);
        Assert.Contains(GlosasNaTela.FiltrarMotivos("1201"), m => m.Codigo == "1201");
        Assert.Contains(GlosasNaTela.FiltrarMotivos("autorizacao"), m => m.Codigo == "1302");
        Assert.Empty(GlosasNaTela.FiltrarMotivos("zzzz-nada"));
    }

    // ---- Não conformidades ----

    private static NaoConformidadeItem Nc(int id, string paciente, string justificativa)
        => new(id, paciente, Convenio.UnimedPadrao, TipoCodigo.Acupuntura, OrdemCodigo.Segundo, Hoje, justificativa, null);

    [Fact]
    public void O_filtro_de_NC_casa_no_paciente_e_na_justificativa()
    {
        var todas = new[] { Nc(1, "Joana Prado", "Portal fora do ar"), Nc(2, "Luís Melo", "Paciente não retornou") };
        Assert.Single(NaoConformidadesNaTela.Filtrar(todas, "joana"));
        Assert.Single(NaoConformidadesNaTela.Filtrar(todas, "portal"));
        Assert.Equal(2, NaoConformidadesNaTela.Filtrar(todas, "").Count);
    }

    [Fact]
    public void O_resumo_e_o_vazio_de_NC_dizem_que_esta_filtrado()
    {
        Assert.Equal("12 não conformidade(s)", NaoConformidadesNaTela.Resumo(12, 12, ""));
        Assert.Equal("3 de 12 não conformidade(s) no filtro", NaoConformidadesNaTela.Resumo(3, 12, "x"));
        Assert.StartsWith("Nada justificado", NaoConformidadesNaTela.VazioDescricao(null));
        Assert.StartsWith("Nenhuma não conformidade bate", NaoConformidadesNaTela.VazioDescricao("x"));
        Assert.Equal("(sem justificativa registrada)", NaoConformidadesNaTela.Justificativa(Nc(1, "A", " ")));
    }
}
