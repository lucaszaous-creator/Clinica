using System.Text.Json;
using Clinica.Application.Abstracoes;
using Clinica.Application.Modelos;
using Clinica.Application.Sms;
using Clinica.Domain;
using Clinica.Domain.Entities;

namespace Clinica.Application.Servicos;

/// <summary>O que uma rodada de SMS produziu — contado, nunca em silêncio.</summary>
public sealed record ResultadoLembretesSms(
    bool Desligado, int Enviados, int SemCelular, int JaTratados, int Falhas,
    int SemLink, int RespostasRecebidas)
{
    public static ResultadoLembretesSms DesligadoAgora => new(true, 0, 0, 0, 0, 0, 0);

    public string Descricao
    {
        get
        {
            if (Desligado)
                return "SMS desligado — escolha o provedor e preencha o token em Relacionamento → Canal de SMS.";
            var partes = new List<string>();
            if (Enviados > 0) partes.Add($"{Enviados} SMS enviado(s)");
            if (RespostasRecebidas > 0) partes.Add($"{RespostasRecebidas} resposta(s) recebida(s)");
            if (SemCelular > 0) partes.Add($"{SemCelular} sem celular na ficha");
            if (SemLink > 0) partes.Add($"{SemLink} sem link (publicação não configurada)");
            if (Falhas > 0) partes.Add($"{Falhas} FALHA(S) — tentam de novo na próxima rodada");
            return partes.Count == 0 ? "Nenhum lembrete a enviar agora." : string.Join(" · ", partes) + ".";
        }
    }
}

/// <summary>Quem a campanha de SMS alcança, antes de mandar — a prévia que a tela mostra.</summary>
public sealed record PreviaCampanhaSms(
    IReadOnlyList<DestinatarioSms> Destinatarios, int SemConsentimento, int SemCelular, int Segmentos)
{
    public int CustoEmSegmentos => Destinatarios.Count * Segmentos;
}

public sealed record DestinatarioSms(int PacienteId, string Nome, string Celular);

/// <summary>O que o envio da campanha produziu.</summary>
public sealed record ResultadoCampanhaSms(int Enviados, int Falhas, int JaEnviados, int Recusados);

/// <summary>
/// O CANAL DE SMS da clínica (out/2026 — pedido da cliente: confirmação 24h antes, com link
/// para o paciente confirmar ou desmarcar, e o canal pronto para campanhas). O mapa está em
/// <c>docs/canal-sms.md</c>.
///
/// <b>É a MESMA rodada de confirmação</b> da <see cref="CampanhaService"/> — o mesmo contato,
/// a mesma chave de idempotência (o agendamento) — com outro canal, como o e-mail. Por isso
/// quem já foi avisado pelo WhatsApp do balcão não recebe SMS, e quem recebeu SMS aparece
/// na tela de Confirmações como "SMS enviado".
///
/// <b>Cada SMS é cobrado, e a idempotência é de BANCO</b>: a linha de
/// <see cref="MensagemSms"/> é gravada ANTES do envio com chave única. Duas máquinas abertas
/// ao mesmo tempo (a Recepção e o Gerente) esbarram no índice e só uma manda.
///
/// <b>A resposta volta pelo balde</b>, como a assinatura do termo (parcela 81): o desktop
/// publica um pedido minimizado (primeiro nome, dia, hora, clínica) em <c>c/</c>, o Worker
/// serve a página com "Confirmo" e "Não vou", grava a resposta write-once ao lado, e
/// <see cref="SincronizarRespostasAsync"/> a traz para o contato. O Worker nunca toca o banco.
///
/// <b>"Não vou" NÃO cancela o horário sozinho.</b> Ele vira "avisou que não vai" na rodada
/// e nas listas do dia; quem libera o horário é a recepção, que liga para remarcar — a
/// decisão de liberar a vaga é de quem pode oferecê-la a outra pessoa na mesma ligação.
/// </summary>
public sealed class LembreteSmsService
{
    public const string OperadorAutomatico = "sistema (SMS automático)";

    /// <summary>A frase que toda campanha leva no fim — quem não quer receber precisa saber como sair.</summary>
    public const string RodapeCampanha = " Nao quer receber? Avise a clinica.";

    private readonly IClinicaRepositorio _repo;
    private readonly CampanhaService _campanhas;
    private readonly ParametrosService _parametros;
    private readonly IGatewaySms _gateway;
    private readonly IArmazenamentoPublico? _armazenamento;
    private readonly Func<DateTime> _agora;

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    public LembreteSmsService(
        IClinicaRepositorio repo, CampanhaService campanhas, ParametrosService parametros,
        IGatewaySms gateway, IArmazenamentoPublico armazenamento)
        : this(repo, campanhas, parametros, gateway, armazenamento, () => DateTime.Now) { }

    /// <summary>Relógio injetado: "24h antes" e "já passou" são regra, e regra se testa.</summary>
    public LembreteSmsService(
        IClinicaRepositorio repo, CampanhaService campanhas, ParametrosService parametros,
        IGatewaySms gateway, IArmazenamentoPublico? armazenamento, Func<DateTime> agora)
    {
        _repo = repo;
        _campanhas = campanhas;
        _parametros = parametros;
        _gateway = gateway;
        _armazenamento = armazenamento;
        _agora = agora;
    }

    /// <summary>
    /// UMA rodada: traz as respostas que chegaram e manda o lembrete das sessões cuja hora
    /// chegou. Chamada pelo relógio da Recepção e do Gerente (e pelo serviço da web, quando
    /// ligado). Idempotente — rodar a cada cinco minutos não manda nada duas vezes.
    /// </summary>
    public async Task<ResultadoLembretesSms> RodarAsync(CancellationToken ct = default)
    {
        var opcoes = await _parametros.ObterOpcoesSmsAsync(ct);
        if (opcoes is null) return ResultadoLembretesSms.DesligadoAgora;

        var respostas = await SincronizarRespostasAsync(ct);

        var agora = _agora();
        var hoje = DateOnly.FromDateTime(agora);
        var (inicio, fim) = ConfirmacaoPorLink.JanelaDeBusca(agora, opcoes.AntecedenciaHoras);

        var candidatos = (await _repo.AgendamentosNoPeriodoAsync(inicio, fim, ct))
            .Where(a => a.Status == StatusAgendamento.Agendado
                        && ConfirmacaoPorLink.HoraDeEnviar(a.DataHora, agora, opcoes.AntecedenciaHoras, opcoes.AdiantarFimDeSemana))
            .ToList();
        if (candidatos.Count == 0) return new ResultadoLembretesSms(false, 0, 0, 0, 0, 0, respostas);

        // A rodada de confirmação de cada dia envolvido (idempotente — a mesma do balcão).
        var dias = candidatos.Select(a => DateOnly.FromDateTime(a.DataHora)).Distinct().OrderBy(d => d).ToList();
        foreach (var d in dias) await _campanhas.GerarConfirmacoesAsync(d, ct);

        var contatos = (await _campanhas.ContatosAsync(TipoContato.ConfirmacaoSessao, null, dias[0], dias[^1], ct))
            .Where(c => c.AgendamentoId is not null)
            .GroupBy(c => c.AgendamentoId!.Value)
            .ToDictionary(g => g.Key, g => g.OrderBy(c => c.Id).First());

        var baseUrl = await _parametros.ObterUrlPublicacaoAsync(ct);
        var prestador = await _parametros.ObterPrestadorAsync(ct);
        var nomeClinica = EmailDaClinicaService.NomeDaClinica(prestador.NomeFantasia, prestador.RazaoSocial);

        int enviados = 0, semCelular = 0, jaTratados = 0, falhas = 0, semLink = 0;

        foreach (var a in candidatos)
        {
            if (!contatos.TryGetValue(a.Id, out var c) || c.Status != StatusContato.Pendente)
            {
                jaTratados++;
                continue;
            }

            var celular = TextoSms.CelularE164(a.Paciente?.Telefone);
            if (celular is null) { semCelular++; continue; }

            var chave = MensagemSms.ChaveDeConfirmacao(c.Id);
            var reserva = await ReservarAsync(chave, () => new MensagemSms
            {
                PacienteId = a.PacienteId,
                ContatoCampanhaId = c.Id,
                Finalidade = FinalidadeSms.ConfirmacaoSessao,
                ChaveIdempotencia = chave,
                Telefone = celular,
                EnviadaPor = OperadorAutomatico,
                CriadaEm = agora
            }, ct);
            if (reserva is null) { jaTratados++; continue; }

            // O link: publicar o pedido ANTES do SMS — mandar um link que ainda não existe é
            // mandar o paciente para "não encontrado". Falhou a publicação, o SMS sai sem
            // link (pede para ligar), em vez de não sair.
            string? token = null;
            string? link = null;
            if (baseUrl is not null && _armazenamento is not null)
            {
                token = ConfirmacaoPorLink.GerarToken();
                try
                {
                    await PublicarPedidoAsync(token, a, nomeClinica, ct);
                    link = ConfirmacaoPorLink.Url(baseUrl, token);
                }
                catch (Exception ex)
                {
                    Diagnostico.Registrar("SMS — publicação do link de confirmação falhou", ex);
                    token = null;
                }
            }
            if (link is null) semLink++;

            var texto = MensagensDeContato.ConfirmacaoPorSms(a.Paciente?.Nome ?? string.Empty, a.DataHora, hoje, nomeClinica, link);
            if (await EnviarReservadaAsync(opcoes, reserva, celular, texto, ct))
            {
                // Reler RASTREADO: a lista veio sem rastreio, e o contato pode ter sido
                // avisado pelo WhatsApp do balcão no meio da rodada — aí não se sobrescreve
                // o canal, só se registra o SMS que de fato saiu.
                var vivo = await _repo.ObterContatoAsync(c.Id, ct);
                if (vivo is not null && vivo.Status == StatusContato.Pendente)
                {
                    vivo.Canal = CanalContato.Sms;
                    vivo.Status = StatusContato.Enviado;
                    vivo.EnviadoEm = _agora();
                    vivo.EnviadoPor = OperadorAutomatico;
                    vivo.TokenResposta = token;
                    await _repo.SalvarAsync(ct);
                }
                enviados++;
            }
            else
            {
                falhas++;
                if (token is not null) await RemoverPedidoAsync(token, ct);
            }
        }

        return new ResultadoLembretesSms(false, enviados, semCelular, jaTratados, falhas, semLink, respostas);
    }

    /// <summary>
    /// Traz as respostas que o Worker gravou para os contatos que esperam. Devolve quantas
    /// chegaram. NUNCA lança — balde fora do ar numa batida do relógio não pode virar erro
    /// na tela de ninguém; vira log, e a próxima batida tenta de novo.
    /// </summary>
    public async Task<int> SincronizarRespostasAsync(CancellationToken ct = default)
    {
        if (_armazenamento is null) return 0;
        try
        {
            var agora = _agora();
            var aguardando = await _repo.ContatosAguardandoRespostaPorLinkAsync(
                DateOnly.FromDateTime(agora).AddDays(-2), ct);
            var recebidas = 0;

            foreach (var c in aguardando)
            {
                var token = c.TokenResposta!;
                byte[]? bytes;
                try { bytes = await _armazenamento.LerAsync(ConfirmacaoPorLink.CaminhoResposta(token), ct); }
                catch (Exception ex)
                {
                    Diagnostico.Registrar($"SMS — leitura da resposta do contato {c.Id} falhou", ex);
                    continue;
                }

                if (bytes is null)
                {
                    // A sessão passou sem resposta: o pedido sai do ar (o link não serve mais).
                    if (c.Agendamento is { } ag && ag.DataHora < agora)
                        await RemoverPedidoAsync(token, ct);
                    continue;
                }

                var resposta = LerResposta(bytes);
                if (resposta is null) continue;

                c.Status = StatusContato.Respondido;
                c.RespondidoEm = resposta.Value.Em ?? agora;
                c.Comentario = resposta.Value.Resposta == RespostaPeloLink.Confirmou
                    ? RodadaDeConfirmacao.RespostaConfirmouPeloLink
                    : RodadaDeConfirmacao.RespostaNaoVaiPeloLink;
                await _repo.RegistrarAuditoriaAsync(new EventoAuditoria
                {
                    Operador = OperadorAutomatico,
                    Acao = "ConfirmacaoRespondidaPorLink",
                    PacienteId = c.PacienteId,
                    Detalhe = $"Contato {c.Id}: {c.Comentario}"
                }, ct);
                await _repo.SalvarAsync(ct);
                await RemoverPedidoAsync(token, ct);
                recebidas++;
            }
            return recebidas;
        }
        catch (Exception ex)
        {
            Diagnostico.Registrar("SMS — sincronização das respostas falhou", ex);
            return 0;
        }
    }

    /// <summary>
    /// Lê o que o Worker gravou: <c>{"resposta":"confirma"|"nao-vai","respondidoEmUnixMs":…}</c>.
    /// Resposta que não é uma das duas é IGNORADA, nunca adivinhada — "confirmou" lido de um
    /// arquivo estragado daria por confirmado quem não respondeu.
    /// </summary>
    public static (RespostaPeloLink Resposta, DateTime? Em)? LerResposta(byte[] bytes)
    {
        try
        {
            using var doc = JsonDocument.Parse(bytes);
            var raiz = doc.RootElement;
            if (!raiz.TryGetProperty("resposta", out var r) || r.ValueKind != JsonValueKind.String) return null;
            RespostaPeloLink? qual = r.GetString() switch
            {
                "confirma" => RespostaPeloLink.Confirmou,
                "nao-vai" => RespostaPeloLink.NaoVai,
                _ => null
            };
            if (qual is null) return null;
            DateTime? em = raiz.TryGetProperty("respondidoEmUnixMs", out var ms) && ms.TryGetInt64(out var v)
                ? DateTimeOffset.FromUnixTimeMilliseconds(v).LocalDateTime
                : null;
            return (qual.Value, em);
        }
        catch (JsonException) { return null; }
    }

    /// <summary>
    /// O pedido MINIMIZADO que a página do Worker lê: primeiro nome, o dia e a hora por
    /// extenso, a clínica e quando o link morre (a hora da sessão). Sobrenome, telefone,
    /// modalidade e profissional NÃO entram — há teste fixando isso.
    /// </summary>
    public static byte[] MontarPedido(Agendamento a, string? nomeClinica, DateOnly hoje)
        => JsonSerializer.SerializeToUtf8Bytes(new
        {
            versao = 1,
            paciente = MensagensDeContato.PrimeiroNome(a.Paciente?.Nome ?? string.Empty),
            quando = MensagensDeContato.QuandoPorExtenso(a.DataHora, hoje),
            clinica = nomeClinica ?? string.Empty,
            expiraEmUnixMs = new DateTimeOffset(a.DataHora).ToUnixTimeMilliseconds()
        }, Json);

    private async Task PublicarPedidoAsync(string token, Agendamento a, string? nomeClinica, CancellationToken ct)
        => await _armazenamento!.PublicarAsync(
            ConfirmacaoPorLink.CaminhoPedido(token),
            MontarPedido(a, nomeClinica, DateOnly.FromDateTime(_agora())),
            "application/json; charset=utf-8", ct: ct);

    private async Task RemoverPedidoAsync(string token, CancellationToken ct)
    {
        if (_armazenamento is null) return;
        try
        {
            await _armazenamento.RemoverAsync(ConfirmacaoPorLink.CaminhoPedido(token), ct);
            await _armazenamento.RemoverAsync(ConfirmacaoPorLink.CaminhoResposta(token), ct);
        }
        catch (Exception ex)
        {
            Diagnostico.Registrar("SMS — remoção do link do balde falhou", ex);
        }
    }

    // ==================== Reserva e envio ====================

    /// <summary>
    /// Reserva a chave: grava a linha nova, ou retoma uma que FALHOU e ainda pode tentar.
    /// Devolve nulo quando outra máquina reservou, ou quando o SMS já saiu.
    /// </summary>
    private async Task<MensagemSms?> ReservarAsync(string chave, Func<MensagemSms> nova, CancellationToken ct)
    {
        var existente = await _repo.MensagemSmsPorChaveAsync(chave, ct);
        if (existente is null)
        {
            var m = nova();
            return await _repo.ReservarMensagemSmsAsync(m, ct) ? m : null;
        }
        if (!existente.PodeRetentar) return null;
        existente.Situacao = SituacaoSms.Enviando;
        existente.Erro = null;
        await _repo.SalvarAsync(ct);
        return existente;
    }

    private async Task<bool> EnviarReservadaAsync(
        OpcoesSms opcoes, MensagemSms m, string celular, string texto, CancellationToken ct)
    {
        m.Texto = TextoSms.ParaGsm(texto);
        m.Segmentos = TextoSms.Segmentos(m.Texto);
        m.Tentativas++;
        try
        {
            m.IdNoProvedor = await _gateway.EnviarAsync(opcoes, celular, m.Texto, ct);
            m.Situacao = SituacaoSms.Enviada;
            m.EnviadaEm = _agora();
            await _repo.SalvarAsync(ct);
            return true;
        }
        catch (Exception ex)
        {
            m.Situacao = SituacaoSms.Falhou;
            m.Erro = ex.Message.Length <= 400 ? ex.Message : ex.Message[..400];
            Diagnostico.Registrar($"SMS — envio para {celular} falhou", ex);
            await _repo.SalvarAsync(ct);
            return false;
        }
    }

    // ==================== Teste e campanha ====================

    /// <summary>
    /// UM SMS de teste para o número digitado, com a configuração GRAVADA — o mesmo caminho
    /// do lembrete. Lança com a frase do provedor quando ele recusa: "token inválido" é a
    /// única pista de que a chave foi colada errada. Registra a linha (é cobrado também).
    /// </summary>
    public async Task EnviarTesteAsync(string telefone, string operador, CancellationToken ct = default)
    {
        var celular = TextoSms.CelularE164(telefone)
            ?? throw new InvalidOperationException("Informe um CELULAR com DDD (11 dígitos) para o teste.");
        var opcoes = await _parametros.ObterOpcoesSmsAsync(ct)
            ?? throw new InvalidOperationException(
                "O SMS está desligado: escolha o provedor, preencha o que falta, salve, e teste de novo.");

        var prestador = await _parametros.ObterPrestadorAsync(ct);
        var clinica = EmailDaClinicaService.NomeDaClinica(prestador.NomeFantasia, prestador.RazaoSocial) ?? "a clinica";
        var chave = $"TESTE:{Guid.NewGuid():N}";
        var m = new MensagemSms
        {
            Finalidade = FinalidadeSms.Teste, ChaveIdempotencia = chave, Telefone = celular,
            EnviadaPor = operador, CriadaEm = _agora()
        };
        await _repo.ReservarMensagemSmsAsync(m, ct);
        if (!await EnviarReservadaAsync(opcoes, m, celular,
                $"Teste do SMS de {clinica}. Se chegou, os lembretes vao sair por este provedor.", ct))
            throw new InvalidOperationException(m.Erro ?? "O provedor não aceitou o SMS de teste.");
    }

    /// <summary>
    /// Quem a campanha alcança: dos pacientes escolhidos, SÓ quem tem consentimento de
    /// comunicação VIGENTE e celular — os outros aparecem CONTADOS (a regra da parcela 5:
    /// quem fica de fora não some, senão a campanha parece maior do que é).
    /// </summary>
    public async Task<PreviaCampanhaSms> PreverCampanhaAsync(
        IReadOnlyCollection<int> pacienteIds, string texto, CancellationToken ct = default)
    {
        var ids = pacienteIds.Distinct().ToList();
        var consentidos = (await _repo.PacientesComConsentimentoVigenteAsync(
            FinalidadeConsentimento.ComunicacaoEMarketing, ids, ct)).ToHashSet();

        var fichas = (await _repo.PacientesParaContatoAsync(consentidos, ct)).ToDictionary(p => p.Id);

        var destinatarios = new List<DestinatarioSms>();
        int semConsentimento = 0, semCelular = 0;
        foreach (var id in ids)
        {
            if (!consentidos.Contains(id)) { semConsentimento++; continue; }
            var celular = fichas.TryGetValue(id, out var p) ? TextoSms.CelularE164(p.Telefone) : null;
            if (p is null || celular is null) { semCelular++; continue; }
            destinatarios.Add(new DestinatarioSms(id, p.Nome, celular));
        }
        return new PreviaCampanhaSms(destinatarios, semConsentimento, semCelular,
            TextoSms.Segmentos(TextoDaCampanha(texto)));
    }

    /// <summary>O texto que de fato sai na campanha: o da tela + o rodapé de saída.</summary>
    public static string TextoDaCampanha(string texto) => TextoSms.ParaGsm((texto ?? string.Empty).Trim() + RodapeCampanha);

    /// <summary>
    /// Manda a campanha. Recusa texto vazio ou maior que <see cref="TextoSms.SegmentosMaximos"/>
    /// segmentos; reconfere o consentimento NA HORA (a prévia pode ter sido aberta antes de
    /// alguém revogar no balcão). <paramref name="lote"/> é a chave do envio: clicar duas
    /// vezes no mesmo lote não manda duas vezes.
    /// </summary>
    public async Task<ResultadoCampanhaSms> EnviarCampanhaAsync(
        IReadOnlyCollection<int> pacienteIds, string texto, string lote, string operador, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(texto))
            throw new InvalidOperationException("Escreva a mensagem da campanha.");
        var final = TextoDaCampanha(texto);
        if (TextoSms.Segmentos(final) > TextoSms.SegmentosMaximos)
            throw new InvalidOperationException(
                $"A mensagem passa de {TextoSms.SegmentosMaximos} SMS por pessoa — encurte o texto.");
        var opcoes = await _parametros.ObterOpcoesSmsAsync(ct)
            ?? throw new InvalidOperationException("O SMS está desligado — configure o provedor antes.");

        var previa = await PreverCampanhaAsync(pacienteIds, texto, ct);
        int enviados = 0, falhas = 0, jaEnviados = 0;
        foreach (var d in previa.Destinatarios)
        {
            var chave = MensagemSms.ChaveDeCampanha(lote, d.PacienteId);
            var reserva = await ReservarAsync(chave, () => new MensagemSms
            {
                PacienteId = d.PacienteId, Finalidade = FinalidadeSms.Campanha, ChaveIdempotencia = chave,
                Telefone = d.Celular, EnviadaPor = operador, CriadaEm = _agora()
            }, ct);
            if (reserva is null) { jaEnviados++; continue; }
            if (await EnviarReservadaAsync(opcoes, reserva, d.Celular, final, ct)) enviados++; else falhas++;
        }

        await _repo.RegistrarAuditoriaAsync(new EventoAuditoria
        {
            Operador = operador,
            Acao = "CampanhaSmsEnviada",
            Detalhe = $"Lote {lote}: {enviados} enviado(s), {falhas} falha(s), "
                    + $"{previa.SemConsentimento} sem consentimento, {previa.SemCelular} sem celular"
        }, ct);
        await _repo.SalvarAsync(ct);

        return new ResultadoCampanhaSms(enviados, falhas, jaEnviados, previa.SemConsentimento + previa.SemCelular);
    }

    /// <summary>Os SMS do período, para a tela do canal (custo e entrega).</summary>
    public Task<IReadOnlyList<MensagemSms>> HistoricoAsync(DateOnly inicio, DateOnly fim, CancellationToken ct = default)
        => _repo.MensagensSmsAsync(inicio.ToDateTime(TimeOnly.MinValue), fim.ToDateTime(TimeOnly.MaxValue), 1000, ct);
}
