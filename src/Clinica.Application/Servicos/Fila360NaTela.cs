using System.Globalization;
using Clinica.Application.Abstracoes;
using Clinica.Domain.Entities;

namespace Clinica.Application.Servicos;

/// <summary>A faixa do risco de o paciente não voltar (as cores da tela vêm sempre com o texto).</summary>
public enum FaixaDeRisco { Baixo, Medio, Alto }

/// <summary>Um sinal que pesou no risco, com os pontos que somou (negativo = o que diminui o risco).</summary>
public sealed record MotivoDeRisco(string Texto, int Pontos);

/// <summary>O risco de um paciente: a nota de 0 a 100, a faixa e os motivos, do que mais pesou para o que menos pesou.</summary>
public sealed record RiscoDoPaciente(int Pontos, FaixaDeRisco Faixa, IReadOnlyList<MotivoDeRisco> Motivos)
{
    public string Rotulo => Faixa switch { FaixaDeRisco.Alto => "Alto", FaixaDeRisco.Medio => "Médio", _ => "Baixo" };
    /// <summary>O tom do <c>Selo</c> da casa.</summary>
    public string Tom => Faixa switch { FaixaDeRisco.Alto => "perigo", FaixaDeRisco.Medio => "aviso", _ => "sucesso" };
    /// <summary>O motivo que mais pesou, para a linha da lista ("" sem motivo).</summary>
    public string Principal => Motivos.FirstOrDefault(m => m.Pontos > 0)?.Texto ?? "";
}

/// <summary>Os grupos da aba "Hoje", na ordem em que o trabalho é feito.</summary>
public enum GrupoDeHoje { Responder, Pegar, Mensagem, Marcar, Plano, Agenda, Balcao }

/// <summary>Um grupo da aba "Hoje": o título, a frase do que fazer e as linhas, do maior risco para o menor.</summary>
public sealed record GrupoDaFila(GrupoDeHoje Grupo, string Titulo, string Dica, IReadOnlyList<LinhaAcompanhamento> Linhas);

/// <summary>
/// A FILA 360 (nov/2026, pedido da direção): o sistema de retenção, dono do trabalho da gerente que
/// cuida de quem está em tratamento. Evolui o Acompanhamento de pacientes — mesma fila, mesmo serviço,
/// mesmos formulários — e acrescenta o que ele não dizia:
/// <list type="bullet">
/// <item>o RISCO de cada paciente, explicável: cada ponto com o seu motivo, sem caixa-preta;</item>
/// <item>a aba HOJE: só o que pede ação hoje, agrupado pela ação e do maior risco para o menor.</item>
/// </list>
/// Puro e com teste (<c>Fila360NaTelaTests</c>): é o que decide por onde a gerente começa o dia.
/// Só lê campos que a <see cref="LinhaAcompanhamento"/> já traz — nenhuma consulta a mais.
/// </summary>
public static class Fila360NaTela
{
    private static readonly CultureInfo PtBr = new("pt-BR");

    /// <summary>A partir de quantos pontos o risco é alto, e médio.</summary>
    public const int Alto = 60, Medio = 35;

    public static RiscoDoPaciente Risco(LinhaAcompanhamento l)
    {
        if (!l.Pendente) return new(0, FaixaDeRisco.Baixo, []);
        var m = new List<MotivoDeRisco>();
        if (l.Tipo == TipoAcompanhamento.Recall)
        {
            var dias = l.Dias switch { >= 120 => 30, >= 90 => 25, >= 60 => 20, >= 30 => 10, _ => 0 };
            if (dias > 0) m.Add(new($"Sem vir há {l.Dias.ToString(PtBr)} dias", dias));
            if (l.TratamentoLongo) m.Add(new($"Tratamento longo ({l.TotalSessoes.ToString(PtBr)} sessões)", 15));
        }
        else
        {
            var dias = l.Dias switch { >= 14 => 25, >= 7 => 15, _ => 5 };
            m.Add(new($"Indicado há {l.Dias.ToString(PtBr)} {(l.Dias == 1 ? "dia" : "dias")} sem a 1ª sessão", dias));
        }
        if (l.PacoteComSaldo) m.Add(new("Pacote com saldo parado", 15));
        if (l.Faltou) m.Add(new("Faltou à sessão", 15));
        if (l.Cancelou) m.Add(new("Cancelou a sessão", 10));
        if (l.Tentativas >= 2) m.Add(new($"{l.Tentativas.ToString(PtBr)} tentativas sem resposta", 10));
        else if (l.Tentativas == 1) m.Add(new("1 tentativa sem resposta", 5));
        if (l.Atrasado) m.Add(new("Contato atrasado", 10));
        if (l.Etapa == EtapaAcompanhamento.ProntoParaAgendar) m.Add(new("Disse que quer marcar", -15));
        if (l.AgendadoPara is not null) m.Add(new("Já tem sessão marcada", -30));

        var pontos = Math.Clamp(m.Sum(x => x.Pontos), 0, 100);
        var faixa = pontos >= Alto ? FaixaDeRisco.Alto : pontos >= Medio ? FaixaDeRisco.Medio : FaixaDeRisco.Baixo;
        // Do que mais pesou para o que menos pesou; os que diminuem o risco por último.
        return new(pontos, faixa, m.OrderByDescending(x => x.Pontos).ToList());
    }

    /// <summary>
    /// A aba HOJE: o que pede ação hoje (sem dono, ou com o próximo contato vencido ou para hoje),
    /// agrupado pela ação, cada grupo do maior risco para o menor. Grupo vazio não aparece.
    /// "Responder" é de quem o paciente respondeu ao convite e espera a clínica (<paramref name="responderam"/>,
    /// os casos que a conversa do WhatsApp aponta) — vem antes de tudo.
    /// </summary>
    public static IReadOnlyList<GrupoDaFila> Hoje(IEnumerable<LinhaAcompanhamento> linhas, DateOnly hoje,
        IReadOnlySet<int>? responderam = null)
    {
        var doDia = linhas.Where(l => l.Pendente && (l.ResponsavelId is null || l.ProximoContato <= hoje || l.Atrasado
            || responderam?.Contains(l.Id) == true)).ToList();
        return doDia
            .GroupBy(l => responderam?.Contains(l.Id) == true ? GrupoDeHoje.Responder : GrupoDe(AcompanhamentoNaTela.Passo(l)))
            .OrderBy(g => g.Key)
            .Select(g => new GrupoDaFila(g.Key, Titulo(g.Key), Dica(g.Key),
                g.OrderByDescending(l => Risco(l).Pontos).ThenByDescending(l => l.Atrasado)
                 .ThenBy(l => l.Paciente, StringComparer.CurrentCultureIgnoreCase).ToList()))
            .ToList();
    }

    public static GrupoDeHoje GrupoDe(PassoAcompanhamento passo) => passo switch
    {
        PassoAcompanhamento.Assumir => GrupoDeHoje.Pegar,
        PassoAcompanhamento.MarcarHorario => GrupoDeHoje.Marcar,
        PassoAcompanhamento.AcompanharPlano => GrupoDeHoje.Plano,
        PassoAcompanhamento.ConferirAgenda => GrupoDeHoje.Agenda,
        PassoAcompanhamento.AtualizarFicha or PassoAcompanhamento.ColherAutorizacao => GrupoDeHoje.Balcao,
        _ => GrupoDeHoje.Mensagem
    };

    public static string Titulo(GrupoDeHoje g) => g switch
    {
        GrupoDeHoje.Responder => "Responderam · responder",
        GrupoDeHoje.Pegar => "Ninguém pegou ainda",
        GrupoDeHoje.Mensagem => "Mandar mensagem",
        GrupoDeHoje.Marcar => "Querem marcar",
        GrupoDeHoje.Plano => "Acompanhar o plano",
        GrupoDeHoje.Agenda => "Conferir na agenda",
        _ => "Pedir autorização no balcão"
    };

    public static string Dica(GrupoDeHoje g) => g switch
    {
        GrupoDeHoje.Responder => "O paciente escreveu depois do convite: a resposta dele espera a clínica.",
        GrupoDeHoje.Pegar => "Pegue para você: a mensagem e o resultado ficam no seu nome.",
        GrupoDeHoje.Mensagem => "A mensagem sai pelo WhatsApp da clínica, assinada por você; a resposta volta para cá.",
        GrupoDeHoje.Marcar => "Escolha o horário com o paciente.",
        GrupoDeHoje.Plano => "Confira se a autorização do plano saiu.",
        GrupoDeHoje.Agenda => "Veja na agenda se a sessão passada aconteceu.",
        _ => "Sem telefone ou sem autorização de mensagens: a recepção resolve na próxima visita, na ficha do paciente."
    };

    /// <summary>A frase do topo da aba Hoje.</summary>
    public static string Resumo(IReadOnlyList<GrupoDaFila> grupos)
    {
        var total = grupos.Sum(g => g.Linhas.Count);
        if (total == 0) return "Nada pede ação hoje.";
        var alto = grupos.Sum(g => g.Linhas.Count(l => Risco(l).Faixa == FaixaDeRisco.Alto));
        var pacientes = total == 1 ? "1 paciente pede ação hoje" : $"{total.ToString(PtBr)} pacientes pedem ação hoje";
        return alto == 0 ? pacientes + "." : $"{pacientes} · {alto.ToString(PtBr)} com risco alto.";
    }

    /// <summary>
    /// Os casos cujo paciente escreveu DEPOIS do último contato registrado — a MESMA regra do "Respondeu"
    /// da conversa (<see cref="RetencaoNaTela.Respondeu"/>), sobre a última recebida de cada ficha.
    /// </summary>
    public static IReadOnlySet<int> QueResponderam(IEnumerable<LinhaAcompanhamento> linhas,
        IReadOnlyDictionary<int, DateTimeOffset> ultimaRecebidaPorPaciente, DateTimeOffset agora)
        => linhas.Where(l => RetencaoNaTela.Respondeu(l, ultimaRecebidaPorPaciente.TryGetValue(l.PacienteId, out var u) ? u : null, agora))
            .Select(l => l.Id).ToHashSet();

    /// <summary>Quantos casos abertos estão com risco alto (o atalho do topo).</summary>
    public static int RiscoAlto(IEnumerable<LinhaAcompanhamento> linhas) => linhas.Count(l => Risco(l).Faixa == FaixaDeRisco.Alto);
}
