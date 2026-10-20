using Clinica.Application.Abstracoes;
using Clinica.Application.Email;
using Clinica.Application.Modelos;
using Clinica.Domain;
using Clinica.Domain.Entities;

namespace Clinica.Application.Servicos;

/// <summary>
/// O que uma rodada de lembretes produziu — contado, nunca em silêncio (a regra das
/// campanhas: quem fica de fora aparece no resultado, senão a rodada parece quebrada).
/// </summary>
/// <param name="Dia">O dia das sessões lembradas.</param>
/// <param name="Desligado">Não há servidor configurado — nada foi gerado nem enviado.</param>
/// <param name="Enviados">E-mails que saíram e ficaram registrados no contato.</param>
/// <param name="SemEmail">Sessões cujo paciente não tem e-mail válido na ficha.</param>
/// <param name="JaTratados">Contatos que já não estavam pendentes (avisados, respondidos, dispensados).</param>
/// <param name="ForaDaAgenda">Horário que já passou ou saiu de "Agendado" — não há o que confirmar.</param>
/// <param name="Falhas">O servidor recusou ou não respondeu; o contato continua pendente.</param>
public sealed record ResultadoLembretesEmail(
    DateOnly Dia, bool Desligado, int Enviados, int SemEmail, int JaTratados, int ForaDaAgenda, int Falhas,
    int PorSms = 0)
{
    public static ResultadoLembretesEmail DesligadoEm(DateOnly dia) => new(dia, true, 0, 0, 0, 0, 0);

    /// <summary>A frase da tela. Um resultado por pergunta: desligado, nada a fazer, ou os números.</summary>
    public string Descricao
    {
        get
        {
            if (Desligado)
                return "Lembrete por e-mail desligado — configure o servidor de saída em "
                     + "Gerente → Configurações → Lembretes por e-mail.";

            var partes = new List<string>();
            if (Enviados > 0) partes.Add($"{Enviados} e-mail(s) enviado(s)");
            if (SemEmail > 0) partes.Add($"{SemEmail} sem e-mail no cadastro");
            if (JaTratados > 0) partes.Add($"{JaTratados} já avisado(s)");
            if (PorSms > 0) partes.Add($"{PorSms} vão por SMS");
            if (Falhas > 0) partes.Add($"{Falhas} FALHA(S) de envio — continuam a enviar");

            return partes.Count == 0
                ? $"Nenhuma sessão a lembrar em {Dia:dd/MM}."
                : $"Sessões de {Dia:dd/MM}: " + string.Join(" · ", partes) + ".";
        }
    }

    public bool TeveFalha => Falhas > 0;
}

/// <summary>
/// O lembrete AUTOMÁTICO da sessão, por e-mail (set/2026 — o item 6 da agenda).
///
/// Por que e-mail, e não WhatsApp automático
/// -----------------------------------------
/// O WhatsApp da clínica é um número de telefone: disparo em massa por ali termina com o
/// número bloqueado (a regra da parcela 5), e a API oficial é contrato mensal com provedor.
/// E-mail é o canal que a clínica já tem, sai de um servidor que ela já paga, e nenhum
/// paciente estranha um lembrete de consulta na caixa de entrada.
///
/// O que ele É: a rodada de confirmação da <see cref="CampanhaService"/> — a MESMA chave de
/// idempotência (o agendamento), o MESMO contato, o MESMO registro de envio. Só o canal muda.
/// Por isso quem já foi avisado pelo WhatsApp não recebe e-mail, e quem recebeu e-mail
/// aparece na janela "Confirmar sessões" do balcão como "avisado por e-mail".
///
/// O que ele NÃO É: marketing. Confirmar a própria sessão é transacional (o paciente pediu
/// o horário) e não exige consentimento de comunicação — a decisão da parcela 5.
///
/// Quando roda (out/2026 — decisão da direção: "às 06:00 do dia da consulta"): NO DIA da
/// sessão, a partir de <see cref="HoraDoLembreteDoDia"/>, pelo relógio do SERVIDOR
/// (<c>RelogioDoEmailNoServidor</c>, na ponte do CRM) e, como caminho de baixo, na abertura
/// da Recepção e do Gerente. A véspera deixou de ser lembrada: o pedido de confirmação é do
/// dia, igual ao WhatsApp do CRM.
///
/// Os DOIS canais saem (WhatsApp pelo CRM e e-mail daqui): o contato já "avisado" pelo
/// WhatsApp também recebe o e-mail do dia. O que impede o SEGUNDO e-mail é a trilha
/// (<see cref="AcaoLembreteDoDia"/> com a chave do horário), não o status do contato — o
/// status é um só para os dois canais.
///
/// Sem servidor configurado, NÃO GERA contato nenhum: a rodada do balcão continua sendo
/// gerada por quem clica em "Gerar rodada", exatamente como antes. Ligar o e-mail é o que
/// passa a gerar na abertura.
/// </summary>
public sealed class LembreteEmailService
{
    /// <summary>Quem assina o envio automático na trilha do contato — não é ninguém do balcão.</summary>
    public const string OperadorAutomatico = "sistema (e-mail automático)";

    private readonly IClinicaRepositorio _repo;
    private readonly CampanhaService _campanhas;
    private readonly ParametrosService _parametros;
    private readonly IEnviadorDeEmail _enviador;
    private readonly Func<DateTime> _agora;

    public LembreteEmailService(
        IClinicaRepositorio repo, CampanhaService campanhas, ParametrosService parametros,
        IEnviadorDeEmail enviador)
        : this(repo, campanhas, parametros, enviador, () => DateTime.Now) { }

    /// <summary>O relógio vem de fora para "hoje", "amanhã" e "já passou" serem testáveis.</summary>
    public LembreteEmailService(
        IClinicaRepositorio repo, CampanhaService campanhas, ParametrosService parametros,
        IEnviadorDeEmail enviador, Func<DateTime> agora)
    {
        _repo = repo;
        _campanhas = campanhas;
        _parametros = parametros;
        _enviador = enviador;
        _agora = agora;
    }

    /// <summary>A partir de que hora do DIA da sessão o pedido de confirmação sai (o mesmo padrão do WhatsApp do CRM).</summary>
    public const int HoraDoLembreteDoDia = 6;

    /// <summary>Trilha do e-mail do dia: é ela, e não o status do contato, que impede o segundo e-mail.</summary>
    public const string AcaoLembreteDoDia = "EmailLembreteDoDia";

    /// <summary>Trilha do e-mail "sua sessão foi agendada" — um por horário.</summary>
    public const string AcaoAgendamentoConfirmado = "EmailAgendamentoConfirmado";

    /// <summary>Quanto tempo depois de marcado o horário ainda recebe o e-mail de agendamento.</summary>
    public static readonly TimeSpan JanelaDoAvisoDeAgendamento = TimeSpan.FromHours(2);

    internal static string ChaveDoHorario(int agendamentoId) => "AGD:" + agendamentoId;

    /// <summary>
    /// O lembrete da ABERTURA do app: o mesmo do servidor (<see cref="EnviarLembretesDoDiaAsync"/>).
    /// Fica como caminho de baixo — com o servidor desligado, a primeira máquina que abrir
    /// depois das 06:00 manda; com os dois ligados, a trilha impede o segundo e-mail.
    /// </summary>
    public async Task<IReadOnlyList<ResultadoLembretesEmail>> EnviarLembretesDaAberturaAsync(CancellationToken ct = default)
        => [await EnviarLembretesDoDiaAsync(ct)];

    /// <summary>
    /// O e-mail do DIA da sessão: antes das <see cref="HoraDoLembreteDoDia"/> não faz nada;
    /// depois, manda a quem tem sessão hoje e ainda não respondeu nem recebeu este e-mail.
    /// </summary>
    public async Task<ResultadoLembretesEmail> EnviarLembretesDoDiaAsync(CancellationToken ct = default)
    {
        var agora = _agora();
        var hoje = DateOnly.FromDateTime(agora);
        if (agora.Hour < HoraDoLembreteDoDia) return new ResultadoLembretesEmail(hoje, false, 0, 0, 0, 0, 0);
        return await EnviarCoreAsync(hoje, doDia: true, ct);
    }

    /// <summary>
    /// Gera a rodada de confirmação do dia (idempotente) e manda o e-mail a quem ainda está
    /// pendente e tem endereço válido. Sequencial, um paciente por vez: mesmo
    /// <c>DbContext</c> (parcela 74), e um servidor SMTP não gosta de vinte conexões de uma vez.
    /// </summary>
    public Task<ResultadoLembretesEmail> EnviarConfirmacoesAsync(DateOnly dia, CancellationToken ct = default)
        => EnviarCoreAsync(dia, doDia: false, ct);

    private async Task<ResultadoLembretesEmail> EnviarCoreAsync(DateOnly dia, bool doDia, CancellationToken ct)
    {
        var opcoes = await _parametros.ObterOpcoesEmailAsync(ct);
        if (opcoes is null) return ResultadoLembretesEmail.DesligadoEm(dia);

        await _campanhas.GerarConfirmacoesAsync(dia, ct);
        var contatos = await _campanhas.ContatosAsync(TipoContato.ConfirmacaoSessao, null, dia, dia, ct);

        var prestador = await _parametros.ObterPrestadorAsync(ct);
        var nomeClinica = NomeDaClinica(prestador.NomeFantasia, prestador.RazaoSocial, opcoes.NomeRemetente);

        var agora = _agora();
        var hoje = DateOnly.FromDateTime(agora);
        int enviados = 0, semEmail = 0, jaTratados = 0, foraDaAgenda = 0, falhas = 0, porSms = 0;

        // Com o SMS ligado (out/2026), o e-mail vira o caminho de BAIXO: quem tem celular
        // recebe o SMS na hora certa (24h antes), e mandar também o e-mail agora — que sai
        // na ABERTURA, às vezes três dias antes — faria o contato sair de "pendente" e o
        // SMS, que é o que a clínica paga para ter, nunca sair. Configurável na tela do SMS.
        var camposSms = await _parametros.ObterCamposSmsAsync(ct);
        var smsCobre = camposSms.EmailSoParaQuemNaoTemCelular
                       && Clinica.Application.Sms.OpcoesSms.De(camposSms) is not null;

        // No modo do dia, o que já recebeu ESTE e-mail (a trilha) — o status não serve: o
        // WhatsApp das 06:00 também tira o contato de "pendente".
        var jaMandados = doDia ? await ChavesNaTrilhaAsync(AcaoLembreteDoDia, dia.AddDays(-1), ct) : [];

        foreach (var c in contatos)
        {
            if (doDia
                ? c.Status is not (StatusContato.Pendente or StatusContato.Enviado) || jaMandados.Contains(ChaveDoHorario(c.AgendamentoId ?? 0))
                : c.Status != StatusContato.Pendente) { jaTratados++; continue; }

            var agendamento = c.Agendamento;
            if (agendamento is null || agendamento.Status != StatusAgendamento.Agendado || agendamento.DataHora < agora)
            {
                foraDaAgenda++;
                continue;
            }

            if (smsCobre && TextoSms.CelularE164(c.Paciente?.Telefone) is not null) { porSms++; continue; }

            var email = EnderecoDeEmail.SeValido(c.Paciente?.Email);
            if (email is null) { semEmail++; continue; }

            // Reler RASTREADO antes de mandar: a Recepção e o Gerente podem abrir no mesmo
            // minuto, e o contato que a outra máquina acabou de avisar não recebe o segundo.
            var vivo = await _repo.ObterContatoAsync(c.Id, ct);
            if (vivo is null || (doDia ? vivo.Status is not (StatusContato.Pendente or StatusContato.Enviado) : vivo.Status != StatusContato.Pendente))
            { jaTratados++; continue; }

            var nome = c.Paciente?.Nome ?? string.Empty;
            try
            {
                await _enviador.EnviarAsync(
                    opcoes, email,
                    MensagensDeContato.AssuntoConfirmacao(agendamento.DataHora, hoje, nomeClinica),
                    MensagensDeContato.ConfirmacaoDeSessao(nome, agendamento.DataHora, hoje, nomeClinica),
                    ct);
            }
            catch (Exception ex)
            {
                // O contato FICA pendente: o balcão ainda pode avisar pelo WhatsApp, e a
                // próxima abertura tenta de novo. Sem rastro seria falha exibida como nada.
                falhas++;
                Diagnostico.Registrar($"Lembrete por e-mail — envio para {email} falhou", ex);
                continue;
            }

            if (doDia)
                await _repo.RegistrarAuditoriaAsync(new EventoAuditoria
                {
                    Acao = AcaoLembreteDoDia, Operador = OperadorAutomatico, PacienteId = c.PacienteId,
                    Detalhe = ChaveDoHorario(agendamento.Id)
                }, ct);
            // A trilha e o contato no MESMO SaveChanges: RegistrarEnvioAsync salva os dois.
            if (vivo.Status == StatusContato.Pendente)
                await _campanhas.RegistrarEnvioAsync(c.Id, OperadorAutomatico, CanalContato.Email, ct);
            else
                await _repo.SalvarAsync(ct);
            enviados++;
        }

        return new ResultadoLembretesEmail(dia, false, enviados, semEmail, jaTratados, foraDaAgenda, falhas, porSms);
    }

    /// <summary>
    /// O e-mail "sua sessão foi agendada": para o horário marcado há no máximo
    /// <see cref="JanelaDoAvisoDeAgendamento"/> (pela hora da marcação), ainda em "Agendado" e
    /// começando em mais de 1 hora. A janela é o que impede ligar o recurso de mandar e-mail
    /// para a agenda inteira; a trilha impede o segundo e-mail do mesmo horário (remarcar não
    /// repete). Devolve quantos saíram; falha fica no log e o próximo ciclo tenta de novo.
    /// </summary>
    public async Task<int> EnviarConfirmacoesDeAgendamentoAsync(CancellationToken ct = default)
    {
        var opcoes = await _parametros.ObterOpcoesEmailAsync(ct);
        if (opcoes is null) return 0;
        var agora = _agora();
        var hoje = DateOnly.FromDateTime(agora);
        var marcados = await _repo.AgendamentosMarcadosDesdeAsync(agora - JanelaDoAvisoDeAgendamento, ct);
        if (marcados.Count == 0) return 0;

        var jaMandados = await ChavesNaTrilhaAsync(AcaoAgendamentoConfirmado, hoje.AddDays(-1), ct);
        var prestador = await _parametros.ObterPrestadorAsync(ct);
        var nomeClinica = NomeDaClinica(prestador.NomeFantasia, prestador.RazaoSocial, opcoes.NomeRemetente);
        var enviados = 0;
        foreach (var ag in marcados)
        {
            if (ag.Status != StatusAgendamento.Agendado || ag.DataHora <= agora.AddHours(1)) continue;
            if (jaMandados.Contains(ChaveDoHorario(ag.Id))) continue;
            var email = EnderecoDeEmail.SeValido(ag.Paciente?.Email);
            if (email is null) continue;
            var nome = ag.Paciente?.Nome ?? string.Empty;
            try
            {
                await _enviador.EnviarAsync(
                    opcoes, email,
                    MensagensDeContato.AssuntoAgendamento(ag.DataHora, hoje, nomeClinica),
                    MensagensDeContato.AgendamentoConfirmado(nome, ag.DataHora, hoje, nomeClinica),
                    ct);
            }
            catch (Exception ex)
            {
                Diagnostico.Registrar($"E-mail de agendamento — envio para {email} falhou", ex);
                continue;
            }
            await _repo.RegistrarAuditoriaAsync(new EventoAuditoria
            {
                Acao = AcaoAgendamentoConfirmado, Operador = OperadorAutomatico, PacienteId = ag.PacienteId,
                Detalhe = ChaveDoHorario(ag.Id)
            }, ct);
            await _repo.SalvarAsync(ct);
            jaMandados.Add(ChaveDoHorario(ag.Id));
            enviados++;
        }
        return enviados;
    }

    /// <summary>As chaves de horário (<c>AGD:id</c>) que a trilha já registrou para a ação, desde o dia.</summary>
    private async Task<HashSet<string>> ChavesNaTrilhaAsync(string acao, DateOnly desde, CancellationToken ct)
    {
        var eventos = await _repo.ConsultarAuditoriaAsync(new FiltroAuditoria { Acao = acao, Inicio = desde, Limite = 5000 }, ct);
        return eventos.Where(e => e.Acao == acao && e.Detalhe is not null).Select(e => e.Detalhe!).ToHashSet();
    }

    /// <summary>
    /// Manda UM e-mail de teste para o endereço informado, com a configuração GRAVADA — é o
    /// mesmo caminho que o lembrete usa. Lança dizendo o que falta quando não há servidor,
    /// e deixa a exceção do servidor subir com a frase dele: "autenticação recusada" é a
    /// única pista de que a senha de aplicativo está errada.
    /// </summary>
    public async Task EnviarTesteAsync(string destino, CancellationToken ct = default)
    {
        var email = EnderecoDeEmail.SeValido(destino)
            ?? throw new InvalidOperationException("Informe um e-mail de destino válido para o teste.");

        var opcoes = await _parametros.ObterOpcoesEmailAsync(ct)
            ?? throw new InvalidOperationException(
                "Lembrete por e-mail desligado: preencha o servidor de saída e o remetente, salve, e teste de novo.");

        var prestador = await _parametros.ObterPrestadorAsync(ct);
        var clinica = NomeDaClinica(prestador.NomeFantasia, prestador.RazaoSocial, opcoes.NomeRemetente) ?? "a clínica";

        await _enviador.EnviarAsync(
            opcoes, email,
            $"Teste do lembrete por e-mail · {clinica}",
            $"Este é um e-mail de teste do sistema de {clinica}. Se você o recebeu, os lembretes de sessão "
            + "vão sair por este servidor. Nenhum paciente recebeu nada.",
            ct);
    }

    /// <summary>
    /// O nome que assina a mensagem: o nome fantasia do prestador, a razão social, e por
    /// último o nome do remetente cadastrado junto do servidor — que é o que o paciente já
    /// vê no "De:", então assinar com ele nunca contradiz o cabeçalho.
    /// </summary>
    public static string? NomeDaClinica(string? nomeFantasia, string? razaoSocial, string? nomeRemetente = null)
        => EnderecoDeEmail.Normalizar(nomeFantasia)
           ?? EnderecoDeEmail.Normalizar(razaoSocial)
           ?? EnderecoDeEmail.Normalizar(nomeRemetente);
}
