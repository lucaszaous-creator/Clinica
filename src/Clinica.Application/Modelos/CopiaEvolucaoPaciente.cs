using Clinica.Domain.Entities;
using Clinica.Application.Servicos;

namespace Clinica.Application.Modelos;

/// <summary>Somente texto clínico reutilizável; identidade, medidas e datas não são modelo.</summary>
public sealed record CopiaEvolucaoPaciente(DateOnly DataOrigem, string? QueixaPrincipal,
    string? HistoriaDoencaAtual, string? ExameFisico, string? HipoteseDiagnostica,
    string? CidSessao, string? Conduta, string? TextoEvolucao, string? Orientacoes,
    string? PlanoTerapeutico, string? Encaminhamento, string? TextoEvolucaoFormatado,
    IReadOnlyDictionary<int, string?> CamposPersonalizados)
{
    public static CopiaEvolucaoPaciente? Ultima(IEnumerable<Evolucao> evolucoes,
        int pacienteId, int evolucaoAtualId, DateOnly ate)
    {
        var e = evolucoes.Where(e => e.PacienteId == pacienteId && e.Id != evolucaoAtualId
                && e.CanceladaEm == null && e.Data <= ate)
            .OrderByDescending(e => e.Data).ThenByDescending(e => e.CriadoEm)
            .ThenByDescending(e => e.Id).FirstOrDefault();
        return e is null ? null : new(e.Data, e.QueixaPrincipal, e.HistoriaDoencaAtual,
            e.ExameFisico, e.HipoteseDiagnostica, e.CidSessao, e.Conduta,
            e.TextoEvolucao, e.Orientacoes, e.PlanoTerapeutico, e.Encaminhamento,
            e.TextoEvolucaoFormatado, e.CamposPersonalizados.ToDictionary(c => c.CampoId,
                c => (string?)CampoPersonalizadoService.Exibir(c)));
    }
}
