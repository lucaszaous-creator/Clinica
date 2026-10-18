using Clinica.Application.Servicos;
using Clinica.Domain;
using FluentAssertions;
using Xunit;

namespace Clinica.Tests;

/// <summary>
/// O que a tela "De onde vêm os pacientes" AFIRMA (Blazor desde out/2026, etapa 6) — antes no
/// <c>OrigensViewModel</c> do WPF. O período é o das outras telas da direção, com "Este ano".
/// </summary>
public class OrigensNaTelaTests
{
    private static readonly DateOnly Inicio = new(2026, 1, 1);
    private static readonly DateOnly Fim = new(2026, 10, 18);

    [Fact]
    public void O_periodo_padrao_e_este_ano_e_a_conta_e_a_das_telas_da_direcao()
    {
        OrigensNaTela.PeriodoPadrao.Should().Be("Este ano");
        ResumoFaturamentoNaTela.Intervalo(OrigensNaTela.PeriodoPadrao, Fim).Should().Be((Inicio, Fim));
    }

    [Fact]
    public void A_fracao_e_sobre_a_base_e_o_nao_perguntado_vem_marcado()
    {
        var resumo = new ResumoOrigemPacientes(Inicio, Fim, 8, 4,
        [
            new LinhaOrigemPacientes(null, "Não perguntado", 4, 1),
            new LinhaOrigemPacientes(OrigemPaciente.Indicacao, "Indicação", 3, 2),
            new LinhaOrigemPacientes(OrigemPaciente.RedesSociais, "Redes sociais", 1, 0)
        ],
        [new IndicadorFrequente("Maria Silva", 2), new IndicadorFrequente("João", 1)]);

        var p = OrigensNaTela.Montar(resumo);

        p.Resumo.Should().Be("8 paciente(s) na base · 4 sem a pergunta respondida · estreias de 01/01/2026 a 18/10/2026");
        p.Linhas.Select(l => l.Fracao).Should().Equal("50%", "38%", "12%");
        p.Linhas[0].EhSemResposta.Should().BeTrue();
        p.Linhas.Skip(1).Should().OnlyContain(l => !l.EhSemResposta);
        p.Linhas[1].Estreias.Should().Be(2);
        p.Indicadores.Select(i => i.Quantos).Should().Equal("2 pacientes", "1 paciente");
    }

    [Fact]
    public void Sem_base_a_fracao_e_travessao_e_o_resumo_diz_que_nao_ha_paciente()
    {
        var resumo = new ResumoOrigemPacientes(Inicio, Fim, 0, 0,
            [new LinhaOrigemPacientes(OrigemPaciente.Indicacao, "Indicação", 0, 0)], []);

        var p = OrigensNaTela.Montar(resumo);

        p.Resumo.Should().Be("Nenhum paciente cadastrado ainda.");
        p.Linhas.Single().Fracao.Should().Be("—", "0% de zero pacientes seria alvo batido falso");
        p.Indicadores.Should().BeEmpty();
    }
}
