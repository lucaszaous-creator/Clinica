using Clinica.Application.Abstracoes;
using Clinica.Application.Modelos;
using Clinica.Domain;
using Clinica.Domain.Entities;

namespace Clinica.Application.Servicos;

/// <summary>
/// As campanhas da clínica (features 11 e a metade que faltava da 02): confirmação da
/// sessão, NPS pós-atendimento e recall de quem sumiu.
///
/// O serviço GERA e REGISTRA; quem envia é a tela, pelo WhatsApp do balcão. Não há
/// robô mandando mensagem sozinho de propósito: o número é o WhatsApp pessoal da
/// clínica, e disparo automático por ali é a receita certa para o número ser
/// bloqueado. O que a parcela automatiza é o TRABALHO — descobrir quem contatar,
/// escrever a mensagem e não deixar ninguém de fora — e não o clique.
///
/// A LINHA DA LGPD, que vale para as três campanhas:
/// confirmar a PRÓPRIA sessão marcada é transacional (o paciente pediu o horário; avisar
/// sobre ele não é marketing) e não exige consentimento. NPS e recall são comunicação
/// ativa da clínica e SÓ saem com <see cref="FinalidadeConsentimento.ComunicacaoEMarketing"/>
/// vigente. Quem não consentiu não é escondido: aparece contado no resultado da rodada,
/// para a clínica ir colher o consentimento no balcão.
/// </summary>
public sealed class CampanhaService
{
    private readonly IClinicaRepositorio _repo;

    /// <summary>Dias sem vir a partir dos quais o paciente entra no recall, por padrão.</summary>
    public const int DiasInatividadePadrao = 60;

    public CampanhaService(IClinicaRepositorio repo) => _repo = repo;

    // ==================== Confirmação de sessão (feature 02) ====================

    /// <summary>
    /// Gera os contatos de confirmação das sessões de um dia. Roda quantas vezes
    /// quiser: a origem (o próprio agendamento) impede a segunda rodada de duplicar.
    ///
    /// Só entram horários ainda EM PÉ — quem já foi atendido, cancelou ou faltou não
    /// tem o que confirmar.
    /// </summary>
    public async Task<ResultadoCampanha> GerarConfirmacoesAsync(
        DateOnly dia, CancellationToken ct = default)
    {
        var agendamentos = (await _repo.AgendamentosNoPeriodoAsync(
                dia.ToDateTime(TimeOnly.MinValue), dia.ToDateTime(TimeOnly.MaxValue), ct))
            .Where(a => a.Status == StatusAgendamento.Agendado)
            .ToList();

        var origens = agendamentos
            .Select(a => ContatoCampanha.OrigemDeAgendamento(a.Id))
            .ToList();
        var existentes = (await _repo.OrigensDeContatoAsync(
            TipoContato.ConfirmacaoSessao, origens, ct)).ToHashSet();

        int gerados = 0, jaExistiam = 0, semTelefone = 0;

        foreach (var agendamento in agendamentos)
        {
            var origem = ContatoCampanha.OrigemDeAgendamento(agendamento.Id);
            if (existentes.Contains(origem)) { jaExistiam++; continue; }

            // Sem telefone o contato ainda é criado: a recepção pode ligar, e a linha
            // fica na tela como pendência de cadastro em vez de desaparecer.
            if (!TemTelefone(agendamento.Paciente)) semTelefone++;

            await _repo.AdicionarContatoAsync(new ContatoCampanha
            {
                PacienteId = agendamento.PacienteId,
                Tipo = TipoContato.ConfirmacaoSessao,
                Origem = origem,
                AgendamentoId = agendamento.Id,
                Referencia = DateOnly.FromDateTime(agendamento.DataHora)
            }, ct);
            gerados++;
        }

        if (gerados > 0) await _repo.SalvarAsync(ct);

        return new ResultadoCampanha(TipoContato.ConfirmacaoSessao, gerados, jaExistiam, 0, semTelefone);
    }

    // ==================== NPS (feature 11) ====================

    /// <summary>
    /// Gera as pesquisas de satisfação dos atendimentos de um dia. Um contato por
    /// atendimento realizado, e só para quem consentiu receber comunicação.
    /// </summary>
    public async Task<ResultadoCampanha> GerarNpsAsync(
        DateOnly dia, CancellationToken ct = default)
    {
        var realizados = (await _repo.AgendamentosNoPeriodoAsync(
                dia.ToDateTime(TimeOnly.MinValue), dia.ToDateTime(TimeOnly.MaxValue), ct))
            .Where(a => a.Status == StatusAgendamento.Realizado && a.AtendimentoId is not null)
            .ToList();

        var origens = realizados
            .Select(a => ContatoCampanha.OrigemDeAtendimento(a.AtendimentoId!.Value))
            .ToList();
        var existentes = (await _repo.OrigensDeContatoAsync(TipoContato.Nps, origens, ct)).ToHashSet();

        var comConsentimento = (await _repo.PacientesComConsentimentoVigenteAsync(
            FinalidadeConsentimento.ComunicacaoEMarketing,
            realizados.Select(a => a.PacienteId).Distinct().ToList(),
            ct)).ToHashSet();

        int gerados = 0, jaExistiam = 0, semConsentimento = 0, semTelefone = 0;

        foreach (var agendamento in realizados)
        {
            var origem = ContatoCampanha.OrigemDeAtendimento(agendamento.AtendimentoId!.Value);
            if (existentes.Contains(origem)) { jaExistiam++; continue; }
            if (!comConsentimento.Contains(agendamento.PacienteId)) { semConsentimento++; continue; }
            if (!TemTelefone(agendamento.Paciente)) { semTelefone++; continue; }

            await _repo.AdicionarContatoAsync(new ContatoCampanha
            {
                PacienteId = agendamento.PacienteId,
                Tipo = TipoContato.Nps,
                Origem = origem,
                AgendamentoId = agendamento.Id,
                AtendimentoId = agendamento.AtendimentoId,
                Referencia = dia
            }, ct);
            gerados++;
        }

        if (gerados > 0) await _repo.SalvarAsync(ct);

        return new ResultadoCampanha(TipoContato.Nps, gerados, jaExistiam, semConsentimento, semTelefone);
    }

    /// <summary>Registra a nota que o paciente respondeu (0 a 10) e o comentário.</summary>
    public async Task<ContatoCampanha> RegistrarNotaAsync(
        int contatoId, int nota, string? comentario = null, CancellationToken ct = default)
    {
        if (!ContatoCampanha.NotaValida(nota))
            throw new ArgumentOutOfRangeException(nameof(nota),
                $"A nota do NPS vai de {ContatoCampanha.NotaMinima} a {ContatoCampanha.NotaMaxima}.");

        var contato = await _repo.ObterContatoAsync(contatoId, ct)
            ?? throw new InvalidOperationException("Contato não encontrado.");

        if (contato.Tipo != TipoContato.Nps)
            throw new InvalidOperationException("Só a pesquisa de NPS registra nota.");

        contato.Nota = nota;
        contato.Comentario = string.IsNullOrWhiteSpace(comentario) ? contato.Comentario : comentario.Trim();
        contato.Status = StatusContato.Respondido;
        contato.RespondidoEm = DateTime.Now;
        // Responder implica ter sido enviado — se a nota veio pelo telefone, o envio
        // pode nunca ter sido marcado, e sem isto a taxa de resposta passaria de 100%.
        contato.EnviadoEm ??= contato.RespondidoEm;

        await _repo.SalvarAsync(ct);
        return contato;
    }

    /// <summary>Resultado do NPS no período (pela data de referência do contato).</summary>
    public async Task<ResumoNps> NpsAsync(
        DateOnly inicio, DateOnly fim, CancellationToken ct = default)
    {
        var contatos = await _repo.ContatosAsync(TipoContato.Nps, null, inicio, fim, ct);

        var respondidos = contatos.Where(c => c.Status == StatusContato.Respondido && c.Nota is not null).ToList();

        return new ResumoNps(
            Gerados: contatos.Count,
            Enviados: contatos.Count(c => c.EnviadoEm is not null),
            Respondidos: respondidos.Count,
            Promotores: respondidos.Count(c => c.Classe == ClasseNps.Promotor),
            Neutros: respondidos.Count(c => c.Classe == ClasseNps.Neutro),
            Detratores: respondidos.Count(c => c.Classe == ClasseNps.Detrator));
    }

    // ==================== Recall (feature 11) ====================

    /// <summary>
    /// Quem parou de vir: sem atendimento há <paramref name="diasInatividade"/> dias e
    /// sem horário futuro marcado.
    ///
    /// Paciente que NUNCA veio não entra: ele não sumiu, ele nunca chegou — e chamá-lo
    /// de volta seria uma mensagem sem sentido. Quem já tem horário marcado também não:
    /// o recall existe para trazer de volta, não para lembrar quem já voltou.
    /// </summary>
    public async Task<IReadOnlyList<CandidatoRecall>> CandidatosRecallAsync(
        DateOnly referencia,
        int diasInatividade = DiasInatividadePadrao,
        CancellationToken ct = default)
    {
        var inatividade = await _repo.InatividadeAsync(referencia, ct);

        var candidatos = inatividade
            .Where(i => i.UltimoAtendimento is not null
                        && i.ProximoAgendamento is null
                        && referencia.DayNumber - i.UltimoAtendimento!.Value.DayNumber >= diasInatividade)
            .ToList();

        if (candidatos.Count == 0) return [];

        var comConsentimento = (await _repo.PacientesComConsentimentoVigenteAsync(
            FinalidadeConsentimento.ComunicacaoEMarketing,
            candidatos.Select(c => c.PacienteId).ToList(),
            ct)).ToHashSet();

        return candidatos
            .Select(c => new CandidatoRecall(
                c.PacienteId,
                c.Nome,
                c.Telefone,
                c.UltimoAtendimento,
                referencia.DayNumber - c.UltimoAtendimento!.Value.DayNumber,
                comConsentimento.Contains(c.PacienteId),
                TemTelefone(c.Telefone)))
            .OrderByDescending(c => c.DiasSemVir)
            .ThenBy(c => c.Nome)
            .ToList();
    }

    /// <summary>
    /// Gera a rodada de recall do mês. Quem continua sumido volta a entrar no mês
    /// seguinte (a origem é por paciente e mês), mas nunca duas vezes na mesma rodada.
    /// </summary>
    public async Task<ResultadoCampanha> GerarRecallAsync(
        DateOnly referencia,
        int diasInatividade = DiasInatividadePadrao,
        CancellationToken ct = default)
    {
        var candidatos = await CandidatosRecallAsync(referencia, diasInatividade, ct);

        var origens = candidatos
            .Select(c => ContatoCampanha.OrigemDeRecall(c.PacienteId, referencia))
            .ToList();
        var existentes = (await _repo.OrigensDeContatoAsync(TipoContato.Recall, origens, ct)).ToHashSet();

        int gerados = 0, jaExistiam = 0, semConsentimento = 0, semTelefone = 0;

        foreach (var candidato in candidatos)
        {
            var origem = ContatoCampanha.OrigemDeRecall(candidato.PacienteId, referencia);
            if (existentes.Contains(origem)) { jaExistiam++; continue; }
            if (!candidato.TemConsentimento) { semConsentimento++; continue; }
            if (!candidato.TemTelefone) { semTelefone++; continue; }

            await _repo.AdicionarContatoAsync(new ContatoCampanha
            {
                PacienteId = candidato.PacienteId,
                Tipo = TipoContato.Recall,
                Origem = origem,
                Referencia = referencia,
                Comentario = $"Sem vir há {candidato.DiasSemVir} dias."
            }, ct);
            gerados++;
        }

        if (gerados > 0) await _repo.SalvarAsync(ct);

        return new ResultadoCampanha(TipoContato.Recall, gerados, jaExistiam, semConsentimento, semTelefone);
    }

    // ==================== Fila de contatos (comum aos três) ====================

    /// <summary>Contatos do período, filtrando por tipo e situação.</summary>
    public Task<IReadOnlyList<ContatoCampanha>> ContatosAsync(
        TipoContato? tipo, StatusContato? status, DateOnly inicio, DateOnly fim,
        CancellationToken ct = default)
        => _repo.ContatosAsync(tipo, status, inicio, fim, ct);

    /// <summary>Marca que a mensagem foi disparada. Só sai de Pendente — reenvio não reabre o fluxo.</summary>
    public async Task<ContatoCampanha> RegistrarEnvioAsync(
        int contatoId, string? operador = null, CanalContato canal = CanalContato.WhatsApp,
        CancellationToken ct = default)
    {
        var contato = await _repo.ObterContatoAsync(contatoId, ct)
            ?? throw new InvalidOperationException("Contato não encontrado.");

        contato.Canal = canal;
        contato.EnviadoEm = DateTime.Now;
        contato.EnviadoPor = operador;
        if (contato.Status == StatusContato.Pendente) contato.Status = StatusContato.Enviado;

        await _repo.SalvarAsync(ct);
        return contato;
    }

    /// <summary>Registra que o paciente respondeu (fora do NPS: confirmou, remarcou, agradeceu).</summary>
    public async Task<ContatoCampanha> RegistrarRespostaAsync(
        int contatoId, string? comentario = null, CancellationToken ct = default)
    {
        var contato = await _repo.ObterContatoAsync(contatoId, ct)
            ?? throw new InvalidOperationException("Contato não encontrado.");

        contato.Status = StatusContato.Respondido;
        contato.RespondidoEm = DateTime.Now;
        contato.EnviadoEm ??= contato.RespondidoEm;
        if (!string.IsNullOrWhiteSpace(comentario)) contato.Comentario = comentario.Trim();

        await _repo.SalvarAsync(ct);
        return contato;
    }

    /// <summary>
    /// Tira o contato da campanha sem enviar. Exige motivo: "não mandamos" sem
    /// explicação é indistinguível de esquecimento quando alguém for conferir depois.
    /// </summary>
    public async Task<ContatoCampanha> DispensarAsync(
        int contatoId, string motivo, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(motivo))
            throw new ArgumentException("Informe o motivo de dispensar o contato.", nameof(motivo));

        var contato = await _repo.ObterContatoAsync(contatoId, ct)
            ?? throw new InvalidOperationException("Contato não encontrado.");

        contato.Status = StatusContato.Dispensado;
        contato.Comentario = motivo.Trim();
        contato.RespondidoEm ??= DateTime.Now;

        await _repo.SalvarAsync(ct);
        return contato;
    }

    // ==================== Rótulos ====================

    public static string Rotular(TipoContato tipo) => tipo switch
    {
        TipoContato.ConfirmacaoSessao => "Confirmação de sessão",
        TipoContato.Nps => "Pesquisa de satisfação (NPS)",
        TipoContato.Recall => "Recall",
        _ => tipo.ToString()
    };

    public static string Rotular(StatusContato status) => status switch
    {
        StatusContato.Pendente => "A enviar",
        StatusContato.Enviado => "Enviado",
        StatusContato.Respondido => "Respondido",
        StatusContato.Dispensado => "Dispensado",
        _ => status.ToString()
    };

    public static string Rotular(ClasseNps classe) => classe switch
    {
        ClasseNps.Detrator => "Detrator",
        ClasseNps.Neutro => "Neutro",
        ClasseNps.Promotor => "Promotor",
        _ => classe.ToString()
    };

    private static bool TemTelefone(Paciente? paciente) => TemTelefone(paciente?.Telefone);

    private static bool TemTelefone(string? telefone)
        => Telefone.Normalizar(telefone).Length is >= 10 and <= 13;
}
