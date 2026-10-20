using Clinica.Application.Modelos;
using Clinica.Domain.Entities;
using FluentAssertions;
using Xunit;

namespace Clinica.Tests;

/// <summary>A seção Ficha do paciente (parte clínica) da tela do paciente (out/2026, migração para Blazor).</summary>
public class FichaClinicaNaTelaTests
{
    [Fact]
    public void Sem_prontuario_o_tratamento_diz_que_e_restrito()
        => FichaClinicaNaTela.EmTratamento(null, podeVerProntuario: false).Should().Be("Consulta restrita ao perfil clínico");

    [Fact]
    public void Cracha_que_falhou_nao_vira_paciente_novo()
        => FichaClinicaNaTela.EmTratamento(null, podeVerProntuario: true).Should().Be("não foi possível conferir");

    [Fact]
    public void Resumo_de_problemas_distingue_nada_ativo_de_nada_registrado()
    {
        FichaClinicaNaTela.ResumoProblemas(0, 0, incluindoEncerrados: false).Should().StartWith("Nenhum problema ATIVO");
        FichaClinicaNaTela.ResumoProblemas(0, 0, incluindoEncerrados: true).Should().Be("Nenhum problema registrado para este paciente.");
        FichaClinicaNaTela.ResumoProblemas(3, 1, false).Should().Be("3 linha(s), 1 com alerta de atendimento.");
    }

    [Fact]
    public void Problema_descartado_mostra_o_motivo()
    {
        var p = FichaClinicaNaTela.ProblemaDe(new ProblemaPaciente
        {
            Id = 1, PacienteId = 1, Natureza = NaturezaProblema.Alergia, Descricao = "Pressão caiu",
            Situacao = SituacaoProblema.Descartado, MotivoDescarte = "era intercorrência"
        });

        p.Natureza.Should().Be("Alergia");
        p.Situacao.Should().Be("Descartado — era intercorrência");
        p.Descartado.Should().BeTrue();
        p.Periodo.Should().Be("sem data informada");
    }

    [Fact]
    public void Anamnese_com_mais_de_um_ano_pede_para_reperguntar()
    {
        var a = new AnamnesePaciente { CriadaPor = "dra.helena", CriadaEm = new DateTime(2024, 10, 1) };

        FichaClinicaNaTela.Procedencia(a, new DateTime(2026, 10, 19)).Should().Contain("vale reperguntar");
    }

    [Fact]
    public void A_anamnese_tem_as_seis_secoes_na_ordem()
        => FichaClinicaNaTela.SecoesDaAnamnese.Select(s => s.Rotulo).Should().Equal(
            "Antecedentes pessoais", "Antecedentes familiares", "Hábitos de vida", "Interrogatório", "História obstétrica", "Observações");
}
