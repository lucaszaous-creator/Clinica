using Clinica.Application.Modelos;
using Clinica.Domain;
using Clinica.Domain.Entities;
using Xunit;

namespace Clinica.Tests;

/// <summary>
/// O que as telas "Consultar guias" e "Guias faturadas" AFIRMAM (out/2026, migração para
/// Blazor): o resumo que diz o recorte, a situação de cada guia e o filtro dos faturados.
/// </summary>
public class GuiasConsultaEFaturadasTests
{
    private static CodigoFaturamento Guia(string paciente, string? convenioCodigo = "Amil", Convenio familia = Convenio.Amil,
        DateOnly? baixa = null, string? numero = null, StatusCodigo status = StatusCodigo.Aberto, StatusGlosa glosa = StatusGlosa.SemGlosa)
        => new()
        {
            Id = 1,
            Tipo = TipoCodigo.ConsultaEspecialidade,
            Ordem = OrdemCodigo.Segundo,
            DataPrevistaFaturamento = new DateOnly(2026, 10, 9),
            DataBaixa = baixa,
            NumeroGuiaReal = numero,
            Status = status,
            Glosa = glosa,
            Atendimento = new Atendimento
            {
                Data = new DateOnly(2026, 10, 8),
                Paciente = new Paciente { Nome = paciente, Convenio = familia, ConvenioCodigo = convenioCodigo }
            }
        };

    [Fact]
    public void Resumo_sem_filtro_so_conta()
        => Assert.Equal("3 guia(s) encontrada(s)", ConsultaDeGuiasNaTela.Resumo(new FiltroConsultaGuias(), 3));

    [Fact]
    public void Resumo_diz_todo_recorte_marcado_e_nao_so_modalidade()
    {
        var f = new FiltroConsultaGuias
        {
            Status = FiltroStatusGuia.Baixado,
            Modalidade = ModalidadeAtendimento.Consulta,
            NumeroGuia = " 123 ",
            Inicio = new DateOnly(2026, 10, 1)
        };
        var r = ConsultaDeGuiasNaTela.Resumo(f, 2);
        Assert.StartsWith("2 guia(s) encontrada(s) — ", r);
        Assert.Contains("baixadas", r);
        Assert.Contains("Consulta", r);
        Assert.Contains("guia “123”", r);
        Assert.Contains("desde 01/10/2026", r);
        Assert.True(ConsultaDeGuiasNaTela.FiltroAtivo(f));
        Assert.False(ConsultaDeGuiasNaTela.FiltroAtivo(new FiltroConsultaGuias()));
    }

    [Fact]
    public void Bater_no_teto_da_consulta_e_dito()
        => Assert.Contains("mostrando as 500 mais recentes",
            ConsultaDeGuiasNaTela.Resumo(new FiltroConsultaGuias(), ConsultaDeGuiasNaTela.Limite));

    [Fact]
    public void Periodo_invertido_e_reconhecido()
    {
        Assert.True(ConsultaDeGuiasNaTela.PeriodoInvertido(new DateOnly(2026, 10, 10), new DateOnly(2026, 10, 1)));
        Assert.False(ConsultaDeGuiasNaTela.PeriodoInvertido(new DateOnly(2026, 10, 1), null));
    }

    [Fact]
    public void Situacao_da_nao_conformidade_nao_se_diz_pendente()
    {
        // O StatusCodigoConverter do WPF a escrevia "Aberto — faturar em …".
        var (texto, _) = ConsultaDeGuiasNaTela.Situacao(Guia("Ana", status: StatusCodigo.NaoConformidade));
        Assert.DoesNotContain("faturar", texto);
        Assert.Contains("Não conformidade", texto);
    }

    [Fact]
    public void Situacao_baixada_e_aberta_em_pt_br()
    {
        Assert.Equal(("Baixada em 12/10/2026", "sucesso"),
            ConsultaDeGuiasNaTela.Situacao(Guia("Ana", baixa: new DateOnly(2026, 10, 12), numero: "9")));
        Assert.Equal(("A faturar em 09/10/2026", "aviso"), ConsultaDeGuiasNaTela.Situacao(Guia("Ana")));
        Assert.Equal("Não aplicável", ConsultaDeGuiasNaTela.Situacao(Guia("Ana", status: StatusCodigo.NaoAplicavel)).Texto);
    }

    [Fact]
    public void Glosa_tem_rotulo_para_todo_estado_e_some_sem_glosa()
    {
        Assert.Null(ConsultaDeGuiasNaTela.Glosa(StatusGlosa.SemGlosa));
        foreach (var g in Enum.GetValues<StatusGlosa>().Where(g => g != StatusGlosa.SemGlosa))
            Assert.False(string.IsNullOrWhiteSpace(ConsultaDeGuiasNaTela.Glosa(g)?.Texto));
    }

    [Fact]
    public void Descrever_traz_o_numero_real_primeiro()
    {
        var d = ConsultaDeGuiasNaTela.Descrever(Guia("Bruno Lima", numero: "777"));
        Assert.StartsWith("Guia 777 · ", d);
        Assert.Contains("prevista para 09/10/2026", d);
        Assert.EndsWith("Bruno Lima", d);
    }

    [Fact]
    public void Filtro_dos_faturados_casa_paciente_sem_acento_e_guia_por_trecho()
    {
        var jose = Guia("José Antônio", numero: "123456", baixa: new DateOnly(2026, 10, 2));
        var outro = Guia("Maria", numero: "999", baixa: new DateOnly(2026, 10, 2));
        var f = FiltroFaturados.Nenhum with { Paciente = "jose antonio" };
        Assert.True(f.Ativo);
        Assert.True(f.Casa(jose));
        Assert.False(f.Casa(outro));
        Assert.True((FiltroFaturados.Nenhum with { Guia = "345" }).Casa(jose));
        Assert.False(FiltroFaturados.Nenhum.Ativo);
    }

    [Fact]
    public void Convenios_sao_os_do_periodo_pelo_nome_da_operadora_e_a_escolha_sobrevive_so_se_existir()
    {
        var lista = new[] { Guia("A"), Guia("B"), Guia("C", "Petrobras", Convenio.Petrobras) };
        var convenios = GuiasFaturadasNaTela.Convenios(lista);
        Assert.Equal(GuiasFaturadasNaTela.TodosConvenios, convenios[0]);
        Assert.Equal(3, convenios.Count);

        var escolhido = FiltroFaturados.Nenhum with { Convenio = convenios[1] };
        Assert.Equal(convenios[1], GuiasFaturadasNaTela.Preservar(escolhido, convenios).Convenio);
        Assert.Equal(GuiasFaturadasNaTela.TodosConvenios,
            GuiasFaturadasNaTela.Preservar(FiltroFaturados.Nenhum with { Convenio = "Sumiu" }, convenios).Convenio);
    }

    [Fact]
    public void Resumo_dos_faturados_diz_que_esta_filtrado()
    {
        Assert.Equal("90 guia(s) no período", GuiasFaturadasNaTela.Resumo(FiltroFaturados.Nenhum, 90, 90));
        Assert.Equal("12 de 90 guia(s) no filtro", GuiasFaturadasNaTela.Resumo(FiltroFaturados.Nenhum with { Guia = "1" }, 12, 90));
    }

    [Fact]
    public void Csv_sai_com_as_palavras_da_tela_e_nao_com_o_enum()
    {
        var linha = GuiasFaturadasNaTela.LinhaCsv(Guia("Ana", baixa: new DateOnly(2026, 10, 3), numero: "55", glosa: StatusGlosa.Glosada));
        Assert.Equal(GuiasFaturadasNaTela.CabecalhoCsv.Count, linha.Count);
        Assert.DoesNotContain("ConsultaEspecialidade", linha);
        Assert.DoesNotContain("Segundo", linha);
        Assert.Contains("03/10/2026", linha);
        Assert.Contains("Glosada", linha);
        Assert.Equal("Sem glosa", GuiasFaturadasNaTela.LinhaCsv(Guia("Ana"))[^1]);
    }

    [Fact]
    public void Abre_no_mes_corrente()
        => Assert.Equal((new DateOnly(2026, 2, 1), new DateOnly(2026, 2, 28)), GuiasFaturadasNaTela.MesDe(new DateOnly(2026, 2, 17)));
}
