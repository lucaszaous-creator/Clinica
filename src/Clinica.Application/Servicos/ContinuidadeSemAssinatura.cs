using Clinica.Application.Abstracoes;
using Clinica.Domain.Entities;

namespace Clinica.Application.Servicos;

/// <summary>Liberação operacional temporária. Não produz assinatura nem comprova assinatura em papel.</summary>
public static class ContinuidadeSemAssinatura
{
    public const string Configuracao = "Clinica.ContinuidadeSemAssinatura";
    public static async Task<bool> HabilitadaAsync(IClinicaRepositorio repo, CancellationToken ct = default)
        => string.Equals(await repo.ObterConfiguracaoAsync(Configuracao, ct), "true", StringComparison.OrdinalIgnoreCase);
}

public sealed partial class PrescricaoInternaService
{
    public async Task<PrescricaoInterna> LiberarSemAssinaturaAsync(int id, int usuarioId,
        bool confirmouAlergia = false, CancellationToken ct = default)
    {
        if (!await ContinuidadeSemAssinatura.HabilitadaAsync(_repo, ct))
            throw new InvalidOperationException("A liberação sem assinatura não está habilitada.");
        var usuario = await _repo.ObterUsuarioAsync(usuarioId, ct)
            ?? throw new UnauthorizedAccessException();
        var p = await Exigir(id, ct);
        if (!usuario.Ativo || usuario.Perfil == PerfilAcesso.Enfermagem || !usuario.Pode(Permissao.Prescrever)
            || usuario.ProfissionalId is null || usuario.ProfissionalId != p.ProfissionalId)
            throw new UnauthorizedAccessException("Somente o médico responsável pode liberar esta prescrição.");
        if (p.Cancelada || p.DevolvidaEm is not null || p.AssinaturaDoPrescritor is not null
            || p.AssinaturaDaExecucao is { ArquivoId: null }
            || p.LiberadaSemAssinaturaEm is not null
            || !(p.Situacao == SituacaoPrescricao.Rascunho || p.AguardaValidacaoMedica))
            throw new InvalidOperationException("Esta prescrição não está disponível para liberação sem assinatura.");
        if (p.Itens.Count == 0 || p.Itens.All(i => i.Suspenso))
            throw new InvalidOperationException("Inclua ao menos um item ativo antes de liberar.");
        var profissional = await _repo.ObterProfissionalAsync(p.ProfissionalId.Value, ct);
        if (profissional is null || !profissional.Ativo || string.IsNullOrWhiteSpace(profissional.RegistroConselho))
            throw new InvalidOperationException("Confira o profissional responsável e seu registro no conselho.");
        var conferencia = await ConferirAsync(p, ct);
        if (conferencia.ExigeConfirmacao && !confirmouAlergia)
            throw new InvalidOperationException("Há alerta de alergia. Confira e confirme a revisão antes de liberar.");
        p.ModoSemAssinatura = true;
        p.LiberadaSemAssinaturaEm = DateTime.Now;
        p.ExigeAssinaturaEletronicaDaExecucao = false;
        p.Situacao = p.OrigemEnfermagem ? SituacaoPrescricao.Encerrada : SituacaoPrescricao.Liberada;
        p.AtualizadoEm = DateTime.Now;
        p.AtualizadoPor = usuario.Login;
        await _repo.RegistrarAuditoriaAsync(new EventoAuditoria {
            Operador = usuario.Login, PacienteId = p.PacienteId, Acao = "PrescricaoLiberadaSemAssinatura",
            Detalhe = $"{p.Numero} · usuário {usuario.Id} · profissional {p.ProfissionalId} · registro sem assinatura digital"
                + (conferencia.ExigeConfirmacao ? " · alerta de alergia confirmado" : "")
        }, ct);
        await _repo.SalvarAsync(ct);
        return p;
    }
}
