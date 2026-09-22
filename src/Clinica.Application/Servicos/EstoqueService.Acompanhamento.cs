using Clinica.Domain;
using Clinica.Domain.Entities;

namespace Clinica.Application.Servicos;

public sealed record MateriaisDoAtendimento(int AgendamentoId, int AtendimentoId, DateTime Data,
    string Paciente, string Situacao, string? Pendencia);

public sealed partial class EstoqueService
{
    public async Task<IReadOnlyList<MateriaisDoAtendimento>> MateriaisDosAtendimentosAsync(
        DateOnly inicio, DateOnly fim, int usuarioId, CancellationToken ct = default)
    {
        var usuario = await _repo.ObterUsuarioAsync(usuarioId, ct);
        if (usuario is null || !usuario.Ativo || usuario.DeveTrocarSenha || !usuario.Pode(Permissao.EditarFinanceiro))
            throw new UnauthorizedAccessException("Seu acesso não permite gerenciar os materiais dos atendimentos.");
        if (fim < inicio || fim.DayNumber - inicio.DayNumber > 92)
            throw new InvalidOperationException("Escolha um período de até 93 dias.");
        var politica = await new PoliticaMateriaisService(_repo).ObterAsync(ct);
        if (politica.Modo == ModoMateriais.Desativado) return [];
        var horarios = (await _repo.AgendamentosNoPeriodoAsync(inicio.ToDateTime(TimeOnly.MinValue), fim.ToDateTime(TimeOnly.MaxValue), ct))
            .Where(a => a.Status == StatusAgendamento.Realizado && a.FimAtendimentoEm is not null && a.AtendimentoId is not null).ToList();
        var registros = (await _repo.ConferenciasDosAtendimentosAsync(horarios.Select(a => a.AtendimentoId!.Value).Distinct().ToArray(), ct))
            .ToDictionary(c => c.AtendimentoId);
        return horarios.Where(a => politica.Abrange(a) || registros.ContainsKey(a.AtendimentoId!.Value))
            .OrderByDescending(a => a.DataHora).Select(a =>
            {
                var r = registros.GetValueOrDefault(a.AtendimentoId!.Value);
                return new MateriaisDoAtendimento(a.Id, a.AtendimentoId.Value, a.DataHora, a.Paciente?.Nome ?? "Paciente",
                    r is null ? "Não informado" : r.SemConsumo ? "Sem consumo declarado" : r.BaixadoEm is null ? "Baixa pendente" : "Baixa registrada",
                    r?.MotivoPendencia);
            }).ToList();
    }
}
