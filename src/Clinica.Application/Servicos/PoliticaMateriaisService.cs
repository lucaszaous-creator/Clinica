using System.Text.Json;
using Clinica.Application.Abstracoes;
using Clinica.Domain;
using Clinica.Domain.Entities;

namespace Clinica.Application.Servicos;

public enum ModoMateriais { Desativado, Gestao, Equipe }
public sealed record PoliticaMateriais(ModoMateriais Modo = ModoMateriais.Desativado, DateTime? AtivadaEm = null)
{
    public bool Abrange(Agendamento horario) => Modo != ModoMateriais.Desativado
        && AtivadaEm is { } inicio && horario.FimAtendimentoEm >= inicio;
}

/// <summary>Adesão explícita da gestão. Ausência de configuração preserva a rotina clínica.</summary>
public sealed class PoliticaMateriaisService(IClinicaRepositorio repo)
{
    public const string Chave = "PoliticaMateriaisProcedimentoV1";
    public static DateTime Agora => TimeZoneInfo.ConvertTimeBySystemTimeZoneId(DateTimeOffset.UtcNow, "America/Sao_Paulo").DateTime;
    public async Task<PoliticaMateriais> ObterAsync(CancellationToken ct = default)
    {
        var json = await repo.ObterConfiguracaoAsync(Chave, ct);
        return json is null ? new() : JsonSerializer.Deserialize<PoliticaMateriais>(json)
            ?? throw new InvalidOperationException("Confira a ativação de materiais no estoque.");
    }

    public Task SalvarAsync(ModoMateriais modo, int usuarioId, CancellationToken ct = default)
        => repo.ExecutarGestaoAtomicaAsync(async () =>
        {
            var u = await repo.ObterUsuarioAsync(usuarioId, ct);
            if (u is null || !u.Ativo || u.DeveTrocarSenha || u.Perfil != PerfilAcesso.Gerente || !u.Pode(Permissao.EditarFinanceiro))
                throw new UnauthorizedAccessException("Somente a gerência pode ativar os materiais dos atendimentos.");
            if (!Enum.IsDefined(modo)) throw new InvalidOperationException("Escolha um modo válido.");
            var antes = await ObterAsync(ct);
            var depois = new PoliticaMateriais(modo, modo == ModoMateriais.Desativado ? null
                : antes.Modo == ModoMateriais.Desativado ? Agora : antes.AtivadaEm);
            await repo.SalvarConfiguracaoAsync(Chave, JsonSerializer.Serialize(depois), ct);
            await repo.RegistrarAuditoriaAsync(new EventoAuditoria { Operador = u.Login, Acao = "PoliticaMateriaisAlterada",
                Detalhe = $"Antes: {JsonSerializer.Serialize(antes)}; depois: {JsonSerializer.Serialize(depois)}." }, ct);
            await repo.SalvarAsync(ct);
            return true;
        }, ct);

    public async Task<Agendamento> ExigirRegistroAsync(int agendamentoId, int usuarioId, CancellationToken ct = default)
    {
        var u = await repo.ObterUsuarioAsync(usuarioId, ct);
        var a = await repo.ObterAgendamentoAsync(agendamentoId, ct)
            ?? throw new InvalidOperationException("Atendimento não encontrado.");
        var regra = await ObterAsync(ct);
        var gestao = u?.Pode(Permissao.EditarFinanceiro) == true;
        var profissional = u is not null && u.Perfil == PerfilAcesso.Profissional
            && u.Profissional?.Ativo == true && u.ProfissionalId == a.ProfissionalId
            && u.Pode(Permissao.VerProntuario | Permissao.EditarProntuario);
        if (u is null || !u.Ativo || u.DeveTrocarSenha || u.Travado(DateTime.Now)
            || !(gestao || regra.Modo == ModoMateriais.Equipe && profissional))
            throw new UnauthorizedAccessException("Seu acesso não permite registrar materiais neste atendimento.");
        // Um registro anterior continua consultável, sem criar pendências em atendimentos antigos.
        var registrado = a.AtendimentoId is { } id && await repo.ConferenciaConsumoAsync(id, ct) is not null;
        if (regra.Modo == ModoMateriais.Desativado || !regra.Abrange(a) && !registrado)
            throw new InvalidOperationException("O registro de materiais não está habilitado para este atendimento.");
        if (a.FimAtendimentoEm is null || a.Status != StatusAgendamento.Realizado || a.AtendimentoId is null)
            throw new InvalidOperationException("Registre os materiais depois de concluir o atendimento.");
        return a;
    }
}
