using System.Text.Json;
using System.Text.RegularExpressions;
using Clinica.Application.Servicos;
using Clinica.Domain.Entities;
using FluentAssertions;
using Xunit;

namespace Clinica.Tests;

public class ResumoAlergiasAtendimentoTests
{
    [Fact]
    public void Oitenta_e_cinco_registros_equivalentes_geram_um_nome_e_preservam_todas_as_observacoes()
    {
        var registros = Enumerable.Range(0, 85).Select(i => new ProblemaPaciente
        {
            Id = i + 1, PacienteId = 90, Natureza = NaturezaProblema.Alergia,
            Descricao = (i % 3) switch
            {
                0 => "Plasil e bromoprida",
                1 => "  PLASIL   E BROMOPRIDA  ",
                _ => "Plasil\te bromoprida"
            },
            Cid = i % 2 == 0 ? "T88.7" : " t88.7 ",
            Observacoes = $"Observação clínica exclusiva {i:000}.",
            Situacao = i % 2 == 0 ? SituacaoProblema.Ativo : SituacaoProblema.Resolvido,
            EvolucaoId = 500 + i, ProfissionalId = 12,
            CriadoEm = new DateTime(2026, 10, 1).AddMinutes(i), CriadoPor = $"Profissional {i:000}"
        }).ToArray();
        var origensAntes = JsonSerializer.Serialize(registros);

        var resumo = ProblemaPacienteService.ResumirAlergias(registros);
        var detalhes = ProblemaPacienteService.DetalharAlergias(registros);

        resumo.Should().Be("ALERGIA — Plasil e bromoprida");
        Ocorrencias(resumo!, "Plasil e bromoprida").Should().Be(1);
        resumo.Should().NotContain("T88.7").And.NotContain("Observação clínica");
        detalhes.Should().NotBeNull();
        Ocorrencias(detalhes!, "Plasil e bromoprida").Should().Be(1);
        Ocorrencias(detalhes!, "T88.7").Should().Be(1);
        for (var i = 0; i < 85; i++)
            Ocorrencias(detalhes!, $"Observação clínica exclusiva {i:000}.").Should().Be(1);
        JsonSerializer.Serialize(registros).Should().Be(origensAntes,
            "resumir é apresentação: os registros e suas procedências devem permanecer intactos");
    }

    [Fact]
    public void Agrupa_multiplas_alergias_sem_aproximar_nomes_e_sem_repetir_detalhes_equivalentes()
    {
        var registros = new[]
        {
            Alergia("Dipirona", "Urticária", "T88.7"),
            Alergia(" DIPIRONA ", "  URTICÁRIA  ", " t88.7 "),
            Alergia("Dipirona", "Edema observado", "T78.4"),
            Alergia("Bromoprida", "Náusea registrada", null),
            Alergia("Bromoprida", "Náusea registrada", null),
            Alergia("Plasil e bromoprida", "Relato anterior", null)
        };

        var resumo = ProblemaPacienteService.ResumirAlergias(registros)!;
        var detalhes = ProblemaPacienteService.DetalharAlergias(registros)!;

        Ocorrencias(resumo, "ALERGIA — ").Should().Be(1);
        Ocorrencias(resumo, "Dipirona").Should().Be(1);
        Ocorrencias(resumo, "Plasil e bromoprida").Should().Be(1);
        // Os dois nomes com bromoprida permanecem distintos: não se infere equivalência clínica.
        Ocorrencias(resumo, "bromoprida").Should().Be(2);
        foreach (var detalhe in new[] { "Urticária", "Edema observado", "Náusea registrada", "Relato anterior", "T88.7", "T78.4" })
        {
            resumo.Should().NotContain(detalhe);
            Ocorrencias(detalhes, detalhe).Should().Be(1);
        }
        Ocorrencias(detalhes, "Dipirona").Should().Be(1);
        Ocorrencias(detalhes, "Plasil e bromoprida").Should().Be(1);
    }

    [Fact]
    public void Inclui_alergia_resolvida_mas_exclui_descartada_diagnostico_e_medicacao_continua()
    {
        var resolvida = Alergia("Alergia resolvida", "Observação preservada", null);
        resolvida.Situacao = SituacaoProblema.Resolvido;
        var descartada = Alergia("Alergia descartada", "Nota descartada", null);
        descartada.Situacao = SituacaoProblema.Descartado;
        var medicacao = Alergia("Medicação contínua", "Nota da medicação", null);
        medicacao.Natureza = NaturezaProblema.MedicacaoContinua;
        var diagnostico = Alergia("Diagnóstico ativo", "Nota do diagnóstico", null);
        diagnostico.Natureza = NaturezaProblema.Diagnostico;

        var resumo = ProblemaPacienteService.ResumirAlergias([resolvida, descartada, medicacao, diagnostico]);
        var detalhes = ProblemaPacienteService.DetalharAlergias([resolvida, descartada, medicacao, diagnostico]);

        resumo.Should().Be("ALERGIA — Alergia resolvida");
        detalhes.Should().Contain("Alergia resolvida").And.Contain("Observação preservada")
            .And.NotContain("Alergia descartada").And.NotContain("Nota descartada")
            .And.NotContain("Medicação contínua").And.NotContain("Nota da medicação")
            .And.NotContain("Diagnóstico ativo").And.NotContain("Nota do diagnóstico");
    }

    [Fact]
    public void Sem_alergias_alertaveis_nao_cria_entrada_vazia()
    {
        var descartada = Alergia("Descartada", null, null);
        descartada.Situacao = SituacaoProblema.Descartado;
        var medicacao = Alergia("Medicamento de uso contínuo", null, null);
        medicacao.Natureza = NaturezaProblema.MedicacaoContinua;

        ProblemaPacienteService.ResumirAlergias([]).Should().BeNull();
        ProblemaPacienteService.ResumirAlergias([descartada, medicacao]).Should().BeNull();
        ProblemaPacienteService.DetalharAlergias([]).Should().BeNull();
        ProblemaPacienteService.DetalharAlergias([descartada, medicacao]).Should().BeNull();
    }

    private static ProblemaPaciente Alergia(string descricao, string? observacoes, string? cid)
        => new() { PacienteId = 90, Natureza = NaturezaProblema.Alergia,
            Descricao = descricao, Observacoes = observacoes, Cid = cid };

    private static int Ocorrencias(string texto, string trecho)
        => Regex.Matches(texto, Regex.Escape(trecho), RegexOptions.IgnoreCase | RegexOptions.CultureInvariant).Count;
}
