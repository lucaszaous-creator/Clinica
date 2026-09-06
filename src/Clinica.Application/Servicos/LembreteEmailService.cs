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
    DateOnly Dia, bool Desligado, int Enviados, int SemEmail, int JaTratados, int ForaDaAgenda, int Falhas)
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
/// Quando roda: na ABERTURA da Recepção e do Gerente (as máquinas que abrem todo dia), para
/// os dias de <see cref="DiasDaAbertura"/>. Não há agendador residente — é a razão do
/// backup e da expiração de links, e vale aqui.
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

    /// <summary>
    /// Os dias que a abertura do app lembra: hoje (o app pode não ter sido aberto ontem),
    /// amanhã (a véspera, que é o que evita o buraco na agenda) e, quando amanhã cai no fim
    /// de semana, os dias até a segunda-feira — senão a sessão de segunda só seria lembrada
    /// na própria segunda de manhã, porque no domingo ninguém abre o sistema.
    /// </summary>
    public static IReadOnlyList<DateOnly> DiasDaAbertura(DateOnly hoje)
    {
        var dias = new List<DateOnly> { hoje, hoje.AddDays(1) };
        var d = hoje.AddDays(1);
        while (d.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday)
        {
            d = d.AddDays(1);
            dias.Add(d);
        }
        return dias;
    }

    /// <summary>A rodada da abertura: <see cref="EnviarConfirmacoesAsync"/> para cada dia de <see cref="DiasDaAbertura"/>.</summary>
    public async Task<IReadOnlyList<ResultadoLembretesEmail>> EnviarLembretesDaAberturaAsync(CancellationToken ct = default)
    {
        var hoje = DateOnly.FromDateTime(_agora());
        var resultados = new List<ResultadoLembretesEmail>();
        foreach (var dia in DiasDaAbertura(hoje))
        {
            var r = await EnviarConfirmacoesAsync(dia, ct);
            resultados.Add(r);
            // Desligado num dia é desligado em todos — não vale ir ao banco de novo.
            if (r.Desligado) break;
        }
        return resultados;
    }

    /// <summary>
    /// Gera a rodada de confirmação do dia (idempotente) e manda o e-mail a quem ainda está
    /// pendente e tem endereço válido. Sequencial, um paciente por vez: mesmo
    /// <c>DbContext</c> (parcela 74), e um servidor SMTP não gosta de vinte conexões de uma vez.
    /// </summary>
    public async Task<ResultadoLembretesEmail> EnviarConfirmacoesAsync(DateOnly dia, CancellationToken ct = default)
    {
        var opcoes = await _parametros.ObterOpcoesEmailAsync(ct);
        if (opcoes is null) return ResultadoLembretesEmail.DesligadoEm(dia);

        await _campanhas.GerarConfirmacoesAsync(dia, ct);
        var contatos = await _campanhas.ContatosAsync(TipoContato.ConfirmacaoSessao, null, dia, dia, ct);

        var prestador = await _parametros.ObterPrestadorAsync(ct);
        var nomeClinica = NomeDaClinica(prestador.NomeFantasia, prestador.RazaoSocial, opcoes.NomeRemetente);

        var agora = _agora();
        var hoje = DateOnly.FromDateTime(agora);
        int enviados = 0, semEmail = 0, jaTratados = 0, foraDaAgenda = 0, falhas = 0;

        foreach (var c in contatos)
        {
            if (c.Status != StatusContato.Pendente) { jaTratados++; continue; }

            var agendamento = c.Agendamento;
            if (agendamento is null || agendamento.Status != StatusAgendamento.Agendado || agendamento.DataHora < agora)
            {
                foraDaAgenda++;
                continue;
            }

            var email = EnderecoDeEmail.SeValido(c.Paciente?.Email);
            if (email is null) { semEmail++; continue; }

            // Reler RASTREADO antes de mandar: a Recepção e o Gerente podem abrir no mesmo
            // minuto, e o contato que a outra máquina acabou de avisar não recebe o segundo.
            var vivo = await _repo.ObterContatoAsync(c.Id, ct);
            if (vivo is null || vivo.Status != StatusContato.Pendente) { jaTratados++; continue; }

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

            await _campanhas.RegistrarEnvioAsync(c.Id, OperadorAutomatico, CanalContato.Email, ct);
            enviados++;
        }

        return new ResultadoLembretesEmail(dia, false, enviados, semEmail, jaTratados, foraDaAgenda, falhas);
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
