using Clinica.Application.Modelos;

namespace Clinica.Application.Servicos;

/// <summary>
/// AS MENSAGENS DO QUE A CLÍNICA FEZ pela conversa (25/10, R5): marcar e cancelar o horário deixam o texto
/// pronto NO CAMPO, e a secretária revisa e envia — nada sai sozinho (regra 3 do CRM: a mensagem diz o que
/// a clínica FEZ, e só depois de feito). Nenhuma delas promete lembrete: o lembrete automático depende de
/// uma chave do CRM que pode estar desligada.
/// </summary>
public static class RascunhosDaConversa
{
    /// <summary>
    /// O horário que a marcação criou: o que está na lista DEPOIS e não estava ANTES (pelo número do
    /// agendamento — remarcar muda a hora, marcar cria outro número). Nulo quando nada novo apareceu (a
    /// pessoa desistiu, ou marcou para além dos <see cref="WhatsAppDaRecepcaoService.DiasDeHorarios"/> dias).
    /// </summary>
    public static HorarioDoPaciente? HorarioNovo(IReadOnlyList<HorarioDoPaciente> antes, IReadOnlyList<HorarioDoPaciente> depois)
        => depois.FirstOrDefault(n => antes.All(a => a.AgendamentoId != n.AgendamentoId));

    /// <summary>
    /// "Pronto, Maria! Marquei a sua acupuntura para terça-feira, 27/10 às 14:00 com a Dra. Helena. Qualquer coisa é só
    /// chamar." — o texto do protótipo aprovado pelo dono (26/11/2026, Agenda dentro da conversa).
    /// </summary>
    public static string Marcacao(string? nome, HorarioDoPaciente novo, DateOnly hoje)
    {
        var quem = ConversasNaTela.PrimeiroNomeOu(nome, "");
        return $"Pronto{(quem.Length == 0 ? "" : ", " + quem)}! Marquei a sua {ModeloDoWhatsApp.NaFrase(novo.Modalidade)} para "
               + $"{MensagensDeContato.QuandoPorExtenso(novo.Quando, hoje)}{Com(novo)}. {QualquerCoisa}";
    }

    /// <summary>
    /// "Pronto, Maria! A sua acupuntura passou de hoje às 14:00 para sexta-feira, 27/11 às 10:00 com a Dra. Helena. Qualquer
    /// coisa é só chamar." — a remarcação pela Agenda da conversa, que sabe o horário de ANTES.
    /// </summary>
    public static string Remarcacao(string? nome, DateTime antes, HorarioDoPaciente novo, DateOnly hoje)
    {
        var quem = ConversasNaTela.PrimeiroNomeOu(nome, "");
        return $"Pronto{(quem.Length == 0 ? "" : ", " + quem)}! A sua {ModeloDoWhatsApp.NaFrase(novo.Modalidade)} passou de "
               + $"{MensagensDeContato.QuandoPorExtenso(antes, hoje)} para {MensagensDeContato.QuandoPorExtenso(novo.Quando, hoje)}{Com(novo)}. {QualquerCoisa}";
    }

    /// <summary>O fecho das mensagens de marcar e remarcar (o do protótipo aprovado).</summary>
    public const string QualquerCoisa = "Qualquer coisa é só chamar.";

    /// <summary>
    /// "Maria, desmarquei o seu horário de amanhã às 14:00 com a Dra. Helena. Quando quiser remarcar, é só me chamar por
    /// aqui." — o convite é para ELA escrever; não promete nada que o sistema faça sozinho (texto do protótipo aprovado).
    /// </summary>
    public static string Cancelamento(string? nome, HorarioDoPaciente cancelado, DateOnly hoje)
    {
        var quem = ConversasNaTela.PrimeiroNomeOu(nome, "");
        return $"{(quem.Length == 0 ? "Olá! D" : quem + ", d")}esmarquei o seu horário de {MensagensDeContato.QuandoPorExtenso(cancelado.Quando, hoje)}"
               + $"{Com(cancelado)}. Quando quiser remarcar, é só me chamar por aqui.";
    }

    // ------------------------------------------------ a RESERVA SEM FICHA (nov/2026, aprovada pelo dono a partir do protótipo)

    /// <summary>
    /// "Pronto, Fernanda! Reservei acupuntura para sexta-feira, 27/11 às 14:00 com a Dra. Helena. No dia, chegue 10 minutos
    /// antes para fazermos o seu cadastro. Qualquer coisa é só chamar." No convênio, antes do fecho: ", e traga a carteirinha
    /// da Unimed". Nada de CPF nem de documento pelo WhatsApp (LGPD, decisão do dono): o cadastro é no balcão.
    /// </summary>
    public static string Reserva(string? nome, string atendimento, DateTime inicio, string? profissional, string? convenio, DateOnly hoje)
    {
        var quem = ConversasNaTela.PrimeiroNomeOu(nome, "");
        var carteirinha = string.IsNullOrWhiteSpace(convenio) ? "" : $", e traga a carteirinha da {convenio.Trim()}";
        return $"Pronto{(quem.Length == 0 ? "" : ", " + quem)}! Reservei {NaFraseDoRotulo(atendimento)} para "
               + $"{MensagensDeContato.QuandoPorExtenso(inicio, hoje)}{ComQuem(profissional)}. "
               + $"No dia, chegue 10 minutos antes para fazermos o seu cadastro{carteirinha}. {QualquerCoisa}";
    }

    /// <summary>"Pronto, Fernanda! A sua reserva de acupuntura passou de … para … com a Dra. Helena. Qualquer coisa é só chamar."</summary>
    public static string RemarcacaoDaReserva(string? nome, string atendimento, DateTime antes, DateTime novo, string? profissional, DateOnly hoje)
    {
        var quem = ConversasNaTela.PrimeiroNomeOu(nome, "");
        return $"Pronto{(quem.Length == 0 ? "" : ", " + quem)}! A sua reserva de {NaFraseDoRotulo(atendimento)} passou de "
               + $"{MensagensDeContato.QuandoPorExtenso(antes, hoje)} para {MensagensDeContato.QuandoPorExtenso(novo, hoje)}{ComQuem(profissional)}. {QualquerCoisa}";
    }

    /// <summary>"Fernanda, cancelei a sua reserva de sexta-feira, 27/11 às 14:00. Quando quiser outro horário, é só me chamar por aqui."</summary>
    public static string CancelamentoDaReserva(string? nome, DateTime inicio, DateOnly hoje)
    {
        var quem = ConversasNaTela.PrimeiroNomeOu(nome, "");
        return $"{(quem.Length == 0 ? "Olá! C" : quem + ", c")}ancelei a sua reserva de {MensagensDeContato.QuandoPorExtenso(inicio, hoje)}. "
               + "Quando quiser outro horário, é só me chamar por aqui.";
    }

    /// <summary>"Acupuntura (apenas)" é nome de catálogo: na mensagem vai "acupuntura".</summary>
    private static string NaFraseDoRotulo(string atendimento)
        => System.Text.RegularExpressions.Regex.Replace(atendimento ?? "", @"\s*\([^)]*\)", "").Trim().ToLowerInvariant() is { Length: > 0 } t ? t : "o atendimento";

    /// <summary>A confirmação passageira depois de marcar pela conversa: diz se a mensagem ficou no campo, ou por que não.</summary>
    public static string DepoisDeMarcar(bool preparou, bool janelaFechada)
        => preparou ? "Horário marcado na agenda. A mensagem ficou pronta no campo: revise e envie."
            : janelaFechada ? "Horário marcado na agenda. A janela de 24 h está fechada: para avisar, escolha um modelo."
            : "Horário marcado na agenda.";

    /// <summary>A confirmação passageira depois de cancelar pela conversa (nada vai ao paciente sem o Enviar).</summary>
    public static string DepoisDeCancelar(bool preparou)
        => preparou ? "Horário cancelado e liberado na agenda. A mensagem ficou pronta no campo: revise e envie."
            : "Horário cancelado e liberado na agenda. Nenhuma mensagem foi ao paciente.";

    private static string Com(HorarioDoPaciente h) => ComQuem(h.Profissional);

    /// <summary>" com a Dra. Helena", " com o Dr. Marcos", " com Fulano" (sem título, sem artigo); vazio sem profissional.</summary>
    public static string ComQuem(string? profissional)
    {
        var p = profissional?.Trim();
        if (string.IsNullOrEmpty(p)) return "";
        return p.StartsWith("Dra.", StringComparison.OrdinalIgnoreCase) ? $" com a {p}"
            : p.StartsWith("Dr.", StringComparison.OrdinalIgnoreCase) ? $" com o {p}"
            : $" com {p}";
    }
}
