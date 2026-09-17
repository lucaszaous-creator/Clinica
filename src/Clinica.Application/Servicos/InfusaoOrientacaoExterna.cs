using Clinica.Domain.Entities;

namespace Clinica.Application.Servicos;

public sealed record RegistroInfusaoExterna(int PacienteId, int MedicoId, int? AgendamentoId,
    DateOnly Data, TimeOnly Hora, string Texto, string Orientacao,
    string? Volume = null, string? Diluente = "SF 0,9%", string? Tempo = "1h",
    ViaAdministracao Via = ViaAdministracao.Endovenosa, bool ConfirmouAlergia = false);

public sealed partial class PrescricaoInternaService
{
    public async Task<IReadOnlyList<Profissional>> MedicosParaValidacaoAsync(CancellationToken ct = default)
        => (await _repo.UsuariosAsync(ct))
            .Where(u => u.Ativo && u.Profissional?.Ativo == true
                && u.Perfil is PerfilAcesso.Profissional or PerfilAcesso.Gerente
                && u.Pode(Permissao.Prescrever))
            .Select(u => u.Profissional!).DistinctBy(p => p.Id).OrderBy(p => p.Nome).ToList();

    /// <summary>Documenta execução já realizada. Não cria uma prescrição médica assinada nem conclui a sessão.</summary>
    public async Task<PrescricaoInterna> RegistrarExecucaoExternaAsync(RegistroInfusaoExterna dados,
        int usuarioId, CancellationToken ct = default)
    {
        var usuario = await _repo.ObterUsuarioAsync(usuarioId, ct);
        if (usuario is null || !usuario.Ativo || usuario.Profissional?.Ativo != true
            || !usuario.Pode(Permissao.ChecarPrescricao | Permissao.RegistrarEvolucaoEnfermagem))
            throw new UnauthorizedAccessException("Seu acesso não permite registrar execução de enfermagem.");
        var autor = new IdentificacaoExecutante(usuario.Id, usuario.Profissional.Nome, usuario.Profissional.RegistroConselho);
        autor.Exigir("registrar a infusão realizada");
        var medico = (await MedicosParaValidacaoAsync(ct)).FirstOrDefault(p => p.Id == dados.MedicoId);
        if (medico?.Ativo != true || medico.Id == usuario.ProfissionalId)
            throw new InvalidOperationException("Escolha o médico responsável pela orientação, diferente do executante.");
        if (await _repo.ObterPacienteAsync(dados.PacienteId, ct) is null)
            throw new InvalidOperationException("Paciente não encontrado.");
        if (string.IsNullOrWhiteSpace(dados.Texto) || dados.Texto.Length > 20000
            || string.IsNullOrWhiteSpace(dados.Orientacao) || dados.Orientacao.Length > 2000)
            throw new InvalidOperationException("Descreva a infusão realizada e a orientação médica recebida fora do sistema.");
        if (dados.Volume?.Length > 60 || dados.Diluente?.Length > 120 || dados.Tempo?.Length > 60 || !Enum.IsDefined(dados.Via))
            throw new InvalidOperationException("Confira volume, diluente, tempo e via de administração.");
        if (dados.Data.ToDateTime(dados.Hora) > DateTime.Now.AddMinutes(5))
            throw new InvalidOperationException("A execução registrada não pode estar no futuro.");
        if (dados.AgendamentoId is { } id)
        {
            var sessao = await _repo.ObterAgendamentoAsync(id, ct);
            if (sessao is null || sessao.PacienteId != dados.PacienteId || sessao.ProfissionalId != dados.MedicoId
                || sessao.Status is not (StatusAgendamento.Agendado or StatusAgendamento.Realizado))
                throw new InvalidOperationException("A sessão deve pertencer ao paciente e ao médico responsável informados.");
        }
        var conferencia = await _conferencia.ConferirAsync(dados.PacienteId, [dados.Texto], ct);
        if (conferencia.ExigeConfirmacao && !dados.ConfirmouAlergia)
            throw new InvalidOperationException("Há alerta de alergia. Confira o prontuário e registre a revisão antes de continuar.");
        var agora = DateTime.Now;
        var numero = await _repo.ProximoNumeroPrescricaoInternaAsync(agora.Year, ct);
        var p = new PrescricaoInterna {
            Numero = $"PRE {agora.Year}/{numero:0000}", CodigoVerificacao = GerarCodigo(),
            PacienteId = dados.PacienteId, ProfissionalId = dados.MedicoId, AgendamentoId = dados.AgendamentoId,
            Data = dados.Data, Hora = dados.Hora, OrigemEnfermagem = true,
            OrientacaoExterna = dados.Orientacao.Trim(), RegistradaPorUsuarioId = usuario.Id,
            Situacao = SituacaoPrescricao.Encerrada, ExigeAssinaturaEletronicaDaExecucao = true,
            CriadoPor = usuario.Login, CriadoEm = agora, EncerradaEm = agora,
            Itens = [new() { Ordem = 1, Descricao = dados.Texto.Trim(), Volume = Limpar(dados.Volume),
                Diluente = Limpar(dados.Diluente), TempoInfusao = Limpar(dados.Tempo), Via = dados.Via,
                Checagens = [new() { Situacao = SituacaoChecagem.Realizado, HoraRealizacao = dados.Hora,
                    ExecutanteUsuarioId = usuario.Id, ExecutanteNome = autor.Nome, ExecutanteConselho = autor.Conselho,
                    RegistradoEm = agora }] }]
        };
        await _repo.AdicionarPrescricaoInternaAsync(p, ct);
        await _repo.RegistrarAuditoriaAsync(new EventoAuditoria { PacienteId = p.PacienteId,
            Operador = usuario.Login, Acao = "InfusaoExternaRegistrada",
            Detalhe = $"{p.Numero}: execução registrada; responsável {dados.MedicoId}; aguarda assinaturas. A sessão e as guias não foram alteradas."
        }, ct);
        await _repo.SalvarAsync(ct);
        return p;
    }
}
