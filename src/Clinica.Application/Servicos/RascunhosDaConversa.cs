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

    /// <summary>"Pronto, Maria! Marquei a sua acupuntura para terça-feira, 27/10 às 14:00 com Dra. Helena."</summary>
    public static string Marcacao(string? nome, HorarioDoPaciente novo, DateOnly hoje)
    {
        var quem = ConversasNaTela.PrimeiroNomeOu(nome, "");
        return $"Pronto{(quem.Length == 0 ? "" : ", " + quem)}! Marquei a sua {ModeloDoWhatsApp.NaFrase(novo.Modalidade)} para "
               + $"{MensagensDeContato.QuandoPorExtenso(novo.Quando, hoje)}{Com(novo)}.";
    }

    /// <summary>
    /// "Maria, cancelei o seu horário de amanhã às 14:00 com Dra. Helena. Se quiser marcar outro dia, é só
    /// responder por aqui." — o convite é para ELA escrever; não promete nada que o sistema faça sozinho.
    /// </summary>
    public static string Cancelamento(string? nome, HorarioDoPaciente cancelado, DateOnly hoje)
    {
        var quem = ConversasNaTela.PrimeiroNomeOu(nome, "");
        return $"{(quem.Length == 0 ? "Olá! C" : quem + ", c")}ancelei o seu horário de {MensagensDeContato.QuandoPorExtenso(cancelado.Quando, hoje)}"
               + $"{Com(cancelado)}. Se quiser marcar outro dia, é só responder por aqui.";
    }

    /// <summary>A confirmação passageira depois de marcar pela conversa: diz se a mensagem ficou no campo, ou por que não.</summary>
    public static string DepoisDeMarcar(bool preparou, bool janelaFechada)
        => preparou ? "Horário marcado na agenda. A mensagem ficou pronta no campo: revise e envie."
            : janelaFechada ? "Horário marcado na agenda. A janela de 24 h está fechada: para avisar, escolha um modelo."
            : "Horário marcado na agenda.";

    /// <summary>A confirmação passageira depois de cancelar pela conversa (nada vai ao paciente sem o Enviar).</summary>
    public static string DepoisDeCancelar(bool preparou)
        => preparou ? "Horário cancelado e liberado na agenda. A mensagem ficou pronta no campo: revise e envie."
            : "Horário cancelado e liberado na agenda. Nenhuma mensagem foi ao paciente.";

    private static string Com(HorarioDoPaciente h) => string.IsNullOrWhiteSpace(h.Profissional) ? "" : $" com {h.Profissional.Trim()}";
}
