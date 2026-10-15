using System.Globalization;
using Clinica.Domain;
using Clinica.Domain.Entities;
using Clinica.Domain.Regras;

namespace Clinica.Application.Modelos;

/// <summary>Em que ponto está quem ainda vai chegar — a pílula da linha do Início.</summary>
public enum SituacaoChegada
{
    /// <summary>Marcado, sem confirmação registrada: a linha não leva pílula.</summary>
    Marcado,
    Confirmou,
    NaoConfirmou,
    NoLocal
}

/// <summary>Uma linha de "Próximos a chegar" (out/2026, mockup aprovado "1 · Início").</summary>
public sealed record ProximoAChegar(
    int AgendamentoId,
    int PacienteId,
    DateTime Hora,
    string Paciente,
    string Contato,
    string Atendimento,
    ModalidadeAtendimento Familia,
    int? ProfissionalId,
    SituacaoChegada Situacao,
    string Pilula)
{
    /// <summary>
    /// O passo que a linha oferece. "No local" ABRE o horário (já chegou: o passo seguinte
    /// é de quem atende); o resto registra a CHEGADA. O termo do procedimento NÃO é colhido
    /// aqui (out/2026, decisão da clínica): quem colhe é a enfermagem.
    /// </summary>
    public string Acao => Situacao == SituacaoChegada.NoLocal ? "Abrir" : "Chegou";
}

/// <summary>Um contador de "O que resolver no balcão". Valor nulo = não foi possível conferir.</summary>
public sealed record ContadorDoBalcao(string Titulo, int? Valor, string Detalhe)
{
    public string ValorTexto => Valor?.ToString(CultureInfo.InvariantCulture) ?? "—";
}

/// <summary>
/// O que a tela Início AFIRMA (out/2026, mockup aprovado). Mora na Application pela regra
/// da casa: o projeto WPF não compila no <c>dotnet test</c>, e o que decide a frase e a
/// pílula tem de morar onde o teste alcança.
///
/// Regras:
/// - **"Próximos a chegar" é quem ainda não entrou na sala** (marcado, no local ou chamado),
///   na ordem da hora. Cancelado, falta, em atendimento e concluído ficam de fora — esta
///   lista é a fila do balcão, não a agenda do dia (que está a um clique, "Ver a agenda do
///   dia").
/// - **A pílula mostra UMA coisa, a mais urgente**: no local (está aqui agora) vence a
///   confirmação. Duas pílulas na mesma linha se leem como nenhuma.
/// - **"Não confirmou" só para quem FOI avisado** (a regra do kanban, parcela 87): sem
///   contato registrado não há pílula — acusar de não confirmar quem nunca foi avisado
///   seria a tela mentindo.
/// </summary>
public static class InicioDoBalcao
{
    private static readonly CultureInfo PtBr = CultureInfo.GetCultureInfo("pt-BR");

    public static string Saudacao(DateTime agora, string? nome)
    {
        var parte = agora.Hour switch { < 12 => "Bom dia", < 18 => "Boa tarde", _ => "Boa noite" };
        var primeiro = (nome ?? string.Empty).Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
        return string.IsNullOrEmpty(primeiro) ? parte : $"{parte}, {primeiro}";
    }

    /// <summary>"Quinta-feira, 15 de outubro · 28 horários hoje, 9 ainda por chegar".</summary>
    public static string LinhaDoDia(DateOnly dia, int horarios, int porChegar)
    {
        var data = dia.ToString("dddd, d 'de' MMMM", PtBr);
        data = char.ToUpper(data[0], PtBr) + data[1..];
        var conta = horarios == 0
            ? "nenhum horário hoje"
            : $"{horarios} {(horarios == 1 ? "horário" : "horários")} hoje, "
              + (porChegar == 0 ? "ninguém mais por chegar" : $"{porChegar} ainda por chegar");
        return $"{data} · {conta}";
    }

    /// <summary>Quem ainda está por chegar (marcado, sem check-in).</summary>
    public static int PorChegar(IEnumerable<Agendamento> doDia)
        => doDia.Count(a => a.Etapa == EtapaFila.Aguardando);

    /// <summary>"Dra. Helena, Dr. Marcos" — quem está atendendo agora, sem repetir.</summary>
    public static string QuemAtende(IEnumerable<Agendamento> doDia)
    {
        var nomes = doDia.Where(a => a.Etapa == EtapaFila.EmAtendimento)
            .Select(a => a.Profissional?.Rotulo)
            .Where(n => !string.IsNullOrWhiteSpace(n))
            .Distinct()
            .ToList();
        return nomes.Count == 0 ? "ninguém na sala agora" : string.Join(", ", nomes);
    }

    /// <summary>"2 faltas · 1 cancelamento" — as duas sempre ditas, mesmo zeradas.</summary>
    public static string FaltasECancelamentos(int faltas, int cancelados)
        => $"{faltas} {(faltas == 1 ? "falta" : "faltas")} · {cancelados} {(cancelados == 1 ? "cancelamento" : "cancelamentos")}";

    public static IReadOnlyList<ProximoAChegar> Proximos(
        IEnumerable<Agendamento> doDia,
        IReadOnlyDictionary<int, StatusContato> confirmacoes,
        DateTime agora)
    {
        var lista = new List<ProximoAChegar>();
        foreach (var a in doDia
                     .Where(a => a.Etapa is EtapaFila.Aguardando or EtapaFila.Chegou or EtapaFila.Chamado)
                     .OrderBy(a => a.DataHora).ThenBy(a => a.Id))
        {
            var (situacao, pilula) = Situacao(a, confirmacoes, agora);
            var paciente = a.Paciente;
            var telefone = string.IsNullOrWhiteSpace(paciente?.Telefone) ? null : Telefone.Formatar(paciente!.Telefone);
            var contato = string.Join(" · ", new[] { paciente?.ConvenioNome, telefone }
                .Where(s => !string.IsNullOrWhiteSpace(s)));
            var atendimento = CatalogoModalidades.NomeComEspecialidade(
                a.ModalidadeCodigo, a.ModalidadePrevista, a.EspecialidadeConsultaCodigo);
            if (a.Profissional?.Rotulo is { Length: > 0 } quem) atendimento += $" · {quem}";

            lista.Add(new ProximoAChegar(a.Id, a.PacienteId, a.DataHora,
                paciente?.Nome ?? "(paciente removido)", contato, atendimento,
                a.ModalidadePrevista, a.ProfissionalId, situacao, pilula));
        }
        return lista;
    }

    private static (SituacaoChegada, string) Situacao(
        Agendamento a, IReadOnlyDictionary<int, StatusContato> confirmacoes, DateTime agora)
    {
        if (a.Etapa is EtapaFila.Chegou or EtapaFila.Chamado)
        {
            var espera = a.EsperaMinutos(agora);
            return (SituacaoChegada.NoLocal, espera is { } m ? $"No local · espera {m} min" : "No local");
        }
        return confirmacoes.TryGetValue(a.Id, out var status) switch
        {
            true when status == StatusContato.Respondido => (SituacaoChegada.Confirmou, "Confirmou"),
            true when status == StatusContato.Enviado => (SituacaoChegada.NaoConfirmou, "Não confirmou"),
            _ => (SituacaoChegada.Marcado, string.Empty)
        };
    }

    /// <summary>
    /// O contador "Confirmar amanhã": quanto da rodada de amanhã ainda pede ação (não
    /// avisados + avisados sem resposta). Sem rodada gerada, o valor é NULO e a frase diz
    /// isso — zero afirmaria "todos confirmaram" sobre uma rodada que não existe.
    /// </summary>
    public static ContadorDoBalcao ConfirmarAmanha(IReadOnlyList<SituacaoConfirmacao> rodada)
    {
        const string titulo = "Confirmar amanhã";
        var vivos = rodada.Where(s => s != SituacaoConfirmacao.Dispensado).ToList();
        if (vivos.Count == 0) return new(titulo, null, "rodada de amanhã ainda não gerada");

        var naoAvisados = vivos.Count(s => s == SituacaoConfirmacao.NaoAvisado);
        var semResposta = vivos.Count(s => s == SituacaoConfirmacao.AvisadoSemResposta);
        var avisados = vivos.Count - naoAvisados;
        var detalhe = naoAvisados > 0
            ? $"{naoAvisados} a avisar · {semResposta} sem resposta"
            : $"{semResposta} sem resposta de {avisados} avisados";
        return new(titulo, naoAvisados + semResposta, detalhe);
    }
}
