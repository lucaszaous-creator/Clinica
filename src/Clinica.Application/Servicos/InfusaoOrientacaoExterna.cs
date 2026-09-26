using Clinica.Domain.Entities;

namespace Clinica.Application.Servicos;

public sealed record RegistroInfusaoExterna(int PacienteId, int MedicoId, int? AgendamentoId,
    DateOnly Data, TimeOnly Hora, string Texto, string Orientacao,
    string? Volume = null, string? Diluente = "SF 0,9%", string? Tempo = "1h",
    ViaAdministracao Via = ViaAdministracao.Endovenosa, bool ConfirmouAlergia = false,
    DateOnly? DataPrescricao = null, TimeOnly? HoraPrescricao = null,
    int? RetificaPrescricaoId = null);

public sealed partial class PrescricaoInternaService
{
    /// <summary>Devolve a avaliação ao executante, mantendo imutável a folha já assinada.</summary>
    public async Task DevolverInfusaoExternaAsync(int prescricaoId, int usuarioId, string motivo,
        CancellationToken ct = default)
    {
        var p = await Exigir(prescricaoId, ct);
        var usuario = await _repo.ObterUsuarioAsync(usuarioId, ct);
        if (usuario is null || !usuario.Ativo || !usuario.Pode(Permissao.Prescrever)
            || usuario.ProfissionalId != p.ProfissionalId)
            throw new UnauthorizedAccessException("Somente o médico responsável pode devolver esta infusão.");
        if (!p.AguardaValidacaoMedica || p.AssinaturaDaExecucao?.ArquivoId is null
            || p.AssinaturaDaExecucao.ArquivoRegistroId is null)
            throw new InvalidOperationException("A devolução exige a execução assinada e uma pendência médica aberta.");
        if (motivo?.Trim().Length is not (>= 5 and <= 500))
            throw new InvalidOperationException("Descreva o motivo da devolução (5 a 500 caracteres).");
        p.DevolvidaEm = DateTime.Now;
        p.DevolvidaPorUsuarioId = usuarioId;
        p.MotivoDevolucao = motivo.Trim();
        p.AtualizadoEm = p.DevolvidaEm;
        p.AtualizadoPor = usuario.Login;
        await _repo.RegistrarAuditoriaAsync(new EventoAuditoria { PacienteId = p.PacienteId,
            Operador = usuario.Login, Acao = "InfusaoExternaDevolvida",
            Detalhe = $"{p.Numero}: devolvida à enfermagem. Motivo: {p.MotivoDevolucao}"
        }, ct);
        await _repo.SalvarAsync(ct);
    }

    /// <summary>Corrige horários de um registro externo antes da primeira assinatura, mantendo auditoria.</summary>
    public async Task CorrigirHorariosInfusaoExternaAsync(int prescricaoId, int usuarioId,
        DateOnly dataPrescricao, TimeOnly horaPrescricao, DateOnly dataExecucao, TimeOnly horaExecucao,
        string motivo, CancellationToken ct = default)
    {
        var p = await Exigir(prescricaoId, ct);
        if (!p.OrigemEnfermagem || p.RegistradaPorUsuarioId != usuarioId || p.Cancelada
            || p.AssinadaEm is not null || p.Assinaturas.Count > 0)
            throw new InvalidOperationException("Os horários só podem ser corrigidos pela enfermagem antes da primeira assinatura.");
        if (motivo?.Trim().Length is not (>= 5 and <= 500))
            throw new InvalidOperationException("Informe o motivo da correção (5 a 500 caracteres).");
        var agora = DateTime.Now;
        if (dataPrescricao.ToDateTime(horaPrescricao) > agora.AddMinutes(5)
            || dataExecucao.ToDateTime(horaExecucao) > agora.AddMinutes(5))
            throw new InvalidOperationException("Os horários informados não podem estar no futuro.");
        var checagem = p.Itens.Single().ChecagemVigente
            ?? throw new InvalidOperationException("Não há execução para corrigir.");
        var anterior = $"prescrição {p.Data:dd/MM/yyyy} {p.Hora:HH\\:mm}; execução "
            + $"{(checagem.DataRealizacao ?? DateOnly.FromDateTime(checagem.RegistradoEm)):dd/MM/yyyy} {checagem.HoraRealizacao:HH\\:mm}";
        p.Data = dataPrescricao; p.Hora = horaPrescricao;
        checagem.DataRealizacao = dataExecucao; checagem.HoraRealizacao = horaExecucao;
        p.AtualizadoEm = agora;
        var usuario = await _repo.ObterUsuarioAsync(usuarioId, ct);
        p.AtualizadoPor = usuario?.Login;
        await _repo.RegistrarAuditoriaAsync(new EventoAuditoria { PacienteId = p.PacienteId,
            Operador = usuario?.Login ?? "?", Acao = "InfusaoExternaHorariosCorrigidos",
            Detalhe = $"{p.Numero}: {anterior} → prescrição {dataPrescricao:dd/MM/yyyy} {horaPrescricao:HH\\:mm}; execução {dataExecucao:dd/MM/yyyy} {horaExecucao:HH\\:mm}. Motivo: {motivo.Trim()}"
        }, ct);
        await _repo.SalvarAsync(ct);
    }

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
        PrescricaoInterna? anterior = null;
        if (dados.RetificaPrescricaoId is { } anteriorId)
        {
            anterior = await Exigir(anteriorId, ct);
            if (!anterior.OrigemEnfermagem || anterior.DevolvidaEm is null
                || anterior.Retificacao is not null || anterior.PacienteId != dados.PacienteId
                || anterior.ProfissionalId != dados.MedicoId || anterior.RegistradaPorUsuarioId != usuarioId)
                throw new InvalidOperationException("A correção deve partir de uma infusão devolvida a este executante e ainda sem nova versão.");
        }
        if (string.IsNullOrWhiteSpace(dados.Texto) || dados.Texto.Length > 20000
            || string.IsNullOrWhiteSpace(dados.Orientacao) || dados.Orientacao.Length > 2000)
            throw new InvalidOperationException("Descreva a infusão realizada e a orientação médica recebida fora do sistema.");
        if (dados.Volume?.Length > 60 || dados.Diluente?.Length > 120 || dados.Tempo?.Length > 60 || !Enum.IsDefined(dados.Via))
            throw new InvalidOperationException("Confira volume, diluente, tempo e via de administração.");
        if (dados.Data.ToDateTime(dados.Hora) > DateTime.Now.AddMinutes(5))
            throw new InvalidOperationException("A execução registrada não pode estar no futuro.");
        if (dados.DataPrescricao.HasValue != dados.HoraPrescricao.HasValue)
            throw new InvalidOperationException("Informe data e hora da prescrição juntas.");
        if (dados.DataPrescricao is { } dataPrescricao && dados.HoraPrescricao is { } horaPrescricao
            && dataPrescricao.ToDateTime(horaPrescricao) > DateTime.Now.AddMinutes(5))
            throw new InvalidOperationException("A data e a hora da prescrição não podem estar no futuro.");
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
            Data = dados.DataPrescricao ?? dados.Data, Hora = dados.HoraPrescricao ?? dados.Hora, OrigemEnfermagem = true,
            OrientacaoExterna = dados.Orientacao.Trim(), RegistradaPorUsuarioId = usuario.Id,
            RetificaPrescricaoId = anterior?.Id,
            Situacao = SituacaoPrescricao.Encerrada, ExigeAssinaturaEletronicaDaExecucao = true,
            CriadoPor = usuario.Login, CriadoEm = agora, EncerradaEm = agora,
            Itens = [new() { Ordem = 1, Descricao = dados.Texto.Trim(), Volume = Limpar(dados.Volume),
                Diluente = Limpar(dados.Diluente), TempoInfusao = Limpar(dados.Tempo), Via = dados.Via,
                Checagens = [new() { Situacao = SituacaoChecagem.Realizado, DataRealizacao = dados.Data, HoraRealizacao = dados.Hora,
                    ExecutanteUsuarioId = usuario.Id, ExecutanteNome = autor.Nome, ExecutanteConselho = autor.Conselho,
                    RegistradoEm = agora }] }]
        };
        await _repo.AdicionarPrescricaoInternaAsync(p, ct);
        if (anterior is not null) anterior.Retificacao = p;
        await _repo.RegistrarAuditoriaAsync(new EventoAuditoria { PacienteId = p.PacienteId,
            Operador = usuario.Login, Acao = "InfusaoExternaRegistrada",
            Detalhe = $"{p.Numero}: execução registrada; responsável {dados.MedicoId}; aguarda assinaturas."
                + (anterior is null ? "" : $" Retifica a folha devolvida {anterior.Numero}.")
                + " A sessão e as guias não foram alteradas."
        }, ct);
        await _repo.SalvarAsync(ct);
        return p;
    }
}
