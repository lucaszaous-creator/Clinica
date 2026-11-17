using Clinica.Application.Formatacao;
using Clinica.Domain;
using Clinica.Domain.Entities;
using FluentAssertions;
using Xunit;

namespace Clinica.Tests;

/// <summary>
/// O que o DETALHE DO ATENDIMENTO afirma (nov/2026) — a situação de cada guia, as regras da
/// busca e as duas fronteiras de leitura (prontuário de fora, valor só para o financeiro).
/// A leitura do banco é executada em <c>CircuitoParticularTests</c>.
/// </summary>
public class DetalheDoAtendimentoNaTelaTests
{
    private static readonly DateOnly Hoje = new(2026, 11, 13);

    private static CodigoFaturamento Guia(Action<CodigoFaturamento>? mexer = null)
    {
        var c = new CodigoFaturamento
        {
            Tipo = TipoCodigo.Acupuntura, Ordem = OrdemCodigo.Primeiro,
            DataPrevistaFaturamento = Hoje.AddDays(-3), Status = StatusCodigo.Aberto
        };
        mexer?.Invoke(c);
        return c;
    }

    [Fact]
    public void Guia_aberta_diz_desde_quando_espera_e_a_que_libera_depois_diz_quando()
    {
        DetalheDoAtendimentoNaTela.Guia(Guia(), Hoje).Situacao.Should().Be("aguardando baixa desde 10/11/2026");

        var segunda = DetalheDoAtendimentoNaTela.Guia(Guia(c =>
        {
            c.Ordem = OrdemCodigo.Segundo;
            c.DataPrevistaFaturamento = Hoje.AddDays(1);
        }), Hoje);
        segunda.Titulo.Should().StartWith("2ª guia");
        segunda.Situacao.Should().Be("libera em 14/11/2026");
        segunda.Numero.Should().Be("sem número");
    }

    [Fact]
    public void Guia_baixada_mostra_numero_quem_baixou_e_o_lote()
    {
        var g = DetalheDoAtendimentoNaTela.Guia(Guia(c =>
        {
            c.DarBaixa(Hoje, "20261100417", "Ana", null);
            c.Lote = new LoteTiss { Numero = 12, DataGeracao = Hoje };
        }), Hoje);

        g.Numero.Should().Be("20261100417");
        g.Situacao.Should().Be("baixada em 13/11/2026");
        g.Tom.Should().Be("sucesso");
        g.Detalhes.Should().Contain("Baixa por Ana").And.Contain(d => d.StartsWith("Lote TISS nº 12"));
    }

    [Fact]
    public void Glosa_ganha_da_baixa_e_traz_motivo_e_prazo_de_recurso()
    {
        var g = DetalheDoAtendimentoNaTela.Guia(Guia(c =>
        {
            c.DarBaixa(Hoje.AddDays(-20), "555", "Ana", null);
            c.RegistrarGlosa(Hoje.AddDays(-2), "Guia sem assinatura");
        }), Hoje);

        g.Situacao.Should().Be("glosada");
        g.Tom.Should().Be("perigo");
        g.Detalhes.Should().Contain(d => d.Contains("Guia sem assinatura")).And.Contain(d => d.StartsWith("Recurso até"));
    }

    [Fact]
    public void Guia_do_particular_e_nao_conformidade_tem_frase_propria()
    {
        DetalheDoAtendimentoNaTela.Guia(Guia(c => c.Status = StatusCodigo.NaoAplicavel), Hoje)
            .Situacao.Should().Be("não vai ao convênio");

        var nc = DetalheDoAtendimentoNaTela.Guia(Guia(c => c.MarcarNaoConformidade("Paciente sem o app")), Hoje);
        nc.Situacao.Should().Be("não conformidade");
        nc.Detalhes.Should().Contain("Paciente sem o app");
    }

    [Fact]
    public void Numero_busca_em_qualquer_data_e_nome_fica_no_periodo()
    {
        DetalheDoAtendimentoNaTela.EhBuscaPorNumero("2026-000123").Should().BeTrue();
        DetalheDoAtendimentoNaTela.EhBuscaPorNumero("#417").Should().BeTrue();
        DetalheDoAtendimentoNaTela.EhBuscaPorNumero("12").Should().BeFalse("com dois dígitos viria a clínica inteira");
        DetalheDoAtendimentoNaTela.EhBuscaPorNumero("maria 123").Should().BeFalse("com letra é nome: fica no período");
        DetalheDoAtendimentoNaTela.EhBuscaPorNumero(null).Should().BeFalse();

        DetalheDoAtendimentoNaTela.TermoDeNumero(" #417 ").Should().Be("417");
        DetalheDoAtendimentoNaTela.Normalizar("  JOÃO Conceição ").Should().Be("joao conceicao");
    }

    [Fact]
    public void O_resumo_da_busca_diz_que_vale_para_qualquer_data_e_quando_bateu_no_teto()
    {
        DetalheDoAtendimentoNaTela.ResumoDaBuscaPorNumero("417", 0, 50).Should().Contain("em nenhuma data");
        DetalheDoAtendimentoNaTela.ResumoDaBuscaPorNumero("417", 3, 50).Should().StartWith("3 atendimento(s)").And.Contain("qualquer data");
        DetalheDoAtendimentoNaTela.ResumoDaBuscaPorNumero("417", 50, 50).Should().Contain("mais dígitos");
    }

    [Fact]
    public void Atendimento_sem_horario_nao_afirma_nada_sobre_o_prontuario_e_o_estornado_diz_por_que()
    {
        var a = new Atendimento
        {
            Id = 9, PacienteId = 1, Data = Hoje, Modalidade = ModalidadeAtendimento.AcupunturaSimples,
            Paciente = new Paciente { Id = 1, Nome = "Fulana de Tal", Convenio = Convenio.UnimedPadrao },
            EstornadoEm = new DateTime(2026, 11, 13, 10, 0, 0), EstornadoPor = "Ana", MotivoEstorno = "lançado em dobro"
        };
        var d = DetalheDoAtendimentoNaTela.Montar(new DadosDoDetalheDoAtendimento(a, null, [], null, [], false), true, Hoje);

        d.Numero.Should().Be("#9");
        d.Estorno.Should().Contain("ESTORNADO").And.Contain("Ana").And.Contain("lançado em dobro");
        d.OQueFoiFeito.Should().Contain(c => c.Rotulo == "Quem atendeu" && c.Valor.Contains("sem horário"));
        d.OQueFoiFeito.Should().Contain(c => c.Rotulo == "Evolução do profissional" && c.Valor.StartsWith("não se aplica"));
        d.ResumoDaCobranca.Should().StartWith("Atendimento estornado");
    }
}
