using Clinica.Application.Modelos;
using Clinica.Domain.Entities;
using Xunit;

namespace Clinica.Tests;

/// <summary>
/// O que a tela Pacientes do balcão (Blazor, out/2026) AFIRMA — os chips com contagem, o
/// rodapé da listagem paginada e a regra do "carregar mais". Saiu do PacientesViewModel WPF,
/// onde o <c>dotnet test</c> não alcançava.
/// </summary>
public sealed class PacientesDoBalcaoNaTelaTests
{
    private static readonly DateOnly Hoje = new(2026, 10, 14);
    private static readonly RecortesDePacientes Recortes = new(2284, 9, 312, 2021, 4);

    [Fact]
    public void O_chip_diz_o_recorte_e_quantos_cabem_nele()
    {
        Assert.Equal("Cadastro incompleto · 312", PacientesDoBalcaoNaTela.Chip(FiltroListaPacientes.CadastroIncompleto, Recortes));
        Assert.Equal("Sem convênio definido · 2.021", PacientesDoBalcaoNaTela.Chip(FiltroListaPacientes.SemConvenioDefinido, Recortes));
        Assert.Equal("Aniversário nesta semana · 4", PacientesDoBalcaoNaTela.Chip(FiltroListaPacientes.AniversarioNaSemana, Recortes));
    }

    [Fact]
    public void Sem_contagem_o_chip_continua_valendo_sem_numero()
    {
        // Falha na contagem não pode virar "· 0", que afirmaria que o recorte está vazio.
        Assert.Equal("Cadastro incompleto", PacientesDoBalcaoNaTela.Chip(FiltroListaPacientes.CadastroIncompleto, null));
        Assert.Null(PacientesDoBalcaoNaTela.Contagem(FiltroListaPacientes.Todos, null));
    }

    [Fact]
    public void O_subtitulo_diz_o_tamanho_do_cadastro_so_quando_sabe()
    {
        Assert.StartsWith("2.284 fichas cadastradas", PacientesDoBalcaoNaTela.Subtitulo(Recortes));
        Assert.StartsWith("1 ficha cadastrada", PacientesDoBalcaoNaTela.Subtitulo(Recortes with { Total = 1 }));
        Assert.StartsWith("Fichas cadastradas", PacientesDoBalcaoNaTela.Subtitulo(null));
    }

    [Fact]
    public void O_balcao_nao_oferece_em_tratamento_que_e_dado_de_saude()
        => Assert.DoesNotContain(FiltroListaPacientes.EmTratamento, PacientesDoBalcaoNaTela.RecortesDoBalcao);

    [Theory]
    [InlineData(50, 50, 2284, true)]   // página cheia e ainda falta
    [InlineData(37, 37, null, false)]  // página incompleta é o fim
    [InlineData(100, 50, 100, false)]  // carregou o total contado
    [InlineData(50, 50, null, true)]   // sem contagem, página cheia ainda pode ter mais
    [InlineData(80, 30, 2284, false)]  // a página veio incompleta: outra máquina mexeu no meio
    public void Carregar_mais_so_quando_a_pagina_veio_cheia(int carregados, int ultima, int? total, bool esperado)
        => Assert.Equal(esperado, PacientesDoBalcaoNaTela.TemMais(carregados, ultima, total));

    [Fact]
    public void O_rodape_diz_quantos_de_quantos_e_o_recorte()
    {
        Assert.Equal("50 de 2.284 pacientes · todos, em ordem de nome",
            PacientesDoBalcaoNaTela.Rodape(50, FiltroListaPacientes.Todos, 2284, temMais: true));
        Assert.Equal("12 pacientes · cadastro incompleto",
            PacientesDoBalcaoNaTela.Rodape(12, FiltroListaPacientes.CadastroIncompleto, 12, temMais: false));
        Assert.Equal("1 paciente · aniversário nesta semana",
            PacientesDoBalcaoNaTela.Rodape(1, FiltroListaPacientes.AniversarioNaSemana, null, temMais: false));
    }

    [Fact]
    public void A_linha_usa_a_mesma_identificacao_da_lista_e_diz_sem_convenio_curto()
    {
        var p = new Paciente
        {
            Nome = "Maria Souza", Documento = "12345678900", Telefone = "21987654321",
            DataNascimento = new DateOnly(1980, 1, 1), ConvenioCodigo = ConvenioCadastro.CodigoADefinir
        };
        var l = PacientesDoBalcaoNaTela.Linha(p, Hoje);
        Assert.Equal(ListaDePacientes.Identificacao(p, Hoje), l.Identificacao);
        Assert.Equal("sem convênio", l.Convenio);
        Assert.True(l.ConvenioADefinir);
        Assert.NotEqual("21987654321", l.Telefone); // formatado, não cru
    }

    [Fact]
    public void Telefone_em_branco_vira_travessao()
        => Assert.Equal("—", PacientesDoBalcaoNaTela.Linha(new Paciente { Nome = "X" }, Hoje).Telefone);

    [Fact]
    public void O_aniversario_diz_ate_quando_vai_a_semana()
        => Assert.Contains("20/10", PacientesDoBalcaoNaTela.Explicacao(FiltroListaPacientes.AniversarioNaSemana, Hoje));
}
