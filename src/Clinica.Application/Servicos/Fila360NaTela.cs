using System.Globalization;
using Clinica.Application.Abstracoes;
using Clinica.Crm.Contracts;
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

/// <summary>O que aconteceu num horário da jornada.</summary>
public enum TipoNaJornada { Realizada, Falta, Marcada }

/// <summary>Uma marca no desenho da jornada da Ficha 360.</summary>
public sealed record MarcaNaJornada(DateTime Quando, TipoNaJornada Tipo);

/// <summary>Uma conversa da página Conversas: o caso aberto, a conversa da ficha dele e se ele respondeu ao convite.</summary>
public sealed record ConversaDaFila(LinhaAcompanhamento Linha, AppConversationSummary Conversa, bool Respondeu);

/// <summary>Os números do Painel da Fila 360, sobre as duas filas juntas (Recall e Novos BSV).</summary>
public sealed record NumerosDaFila(int Abertos, int RiscoAlto, int RiscoMedio, int ParaHoje, int Voltaram, int Contatados, int VoltaramDosContatados)
{
    /// <summary>Dos contatados no período, quantos voltaram — nulo sem base (0% e "não medido" são coisas diferentes).</summary>
    public int? TaxaDeRetorno => Contatados == 0 ? null : (int)Math.Round(100.0 * VoltaramDosContatados / Contatados);
}

/// <summary>As visões da Carteira (os botões de cima).</summary>
public enum VisaoDaCarteira { Todos, RiscoAlto, NovosBsv, SemAutorizacao, Encerrados }

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

    /// <summary>
    /// Quantas linhas cada grupo da Fila de hoje mostra antes do "Mostrar mais" (nov/2026, a clínica: "lista
    /// infinita"). As de maior risco ficam à vista; o resto abre num clique.
    /// </summary>
    public const int LinhasPorGrupo = 5;

    /// <summary>As linhas à vista de um grupo: todas quando aberto, senão as <see cref="LinhasPorGrupo"/> primeiras (as de maior risco).</summary>
    public static IReadOnlyList<LinhaAcompanhamento> Visiveis(GrupoDaFila grupo, bool expandido)
        => expandido || grupo.Linhas.Count <= LinhasPorGrupo ? grupo.Linhas : grupo.Linhas.Take(LinhasPorGrupo).ToList();

    /// <summary>"Mostrar mais 12" — quantas ficaram escondidas (0: o grupo cabe inteiro, sem botão).</summary>
    public static int Escondidas(GrupoDaFila grupo) => Math.Max(0, grupo.Linhas.Count - LinhasPorGrupo);

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

    // ---------------------------------------------------------------- a jornada da Ficha 360

    /// <summary>Quantos meses a jornada desenha.</summary>
    public const int MesesDaJornada = 6;

    /// <summary>
    /// A jornada do paciente nos últimos meses: as sessões realizadas, as faltas e o que está marcado. Cancelado
    /// e substituído não entram (não são presença nem ausência); horário passado ainda "Agendado" também não —
    /// é o "conferir comparecimento", que a fila já mostra.
    /// </summary>
    public static IReadOnlyList<MarcaNaJornada> Jornada(IEnumerable<SessaoDaJornada> horarios, DateTime agora)
        => horarios.Select(h => h.Status switch
            {
                StatusAgendamento.Realizado => new MarcaNaJornada(h.Quando, TipoNaJornada.Realizada),
                StatusAgendamento.Faltou => new MarcaNaJornada(h.Quando, TipoNaJornada.Falta),
                StatusAgendamento.Agendado when h.Quando >= agora => new MarcaNaJornada(h.Quando, TipoNaJornada.Marcada),
                _ => null
            })
            .OfType<MarcaNaJornada>().OrderBy(m => m.Quando).ToList();

    /// <summary>A frase da jornada, para quem não vê o desenho (leitor de tela) e para o title.</summary>
    public static string ResumoDaJornada(IReadOnlyList<MarcaNaJornada> marcas)
    {
        if (marcas.Count == 0) return "Nenhum horário nos últimos meses.";
        int Conta(TipoNaJornada t) => marcas.Count(m => m.Tipo == t);
        var partes = new List<string>();
        var r = Conta(TipoNaJornada.Realizada);
        var f = Conta(TipoNaJornada.Falta);
        var m = Conta(TipoNaJornada.Marcada);
        if (r > 0) partes.Add(r == 1 ? "1 sessão realizada" : $"{r.ToString(PtBr)} sessões realizadas");
        if (f > 0) partes.Add(f == 1 ? "1 falta" : $"{f.ToString(PtBr)} faltas");
        if (m > 0) partes.Add(m == 1 ? "1 horário marcado" : $"{m.ToString(PtBr)} horários marcados");
        return string.Join(" · ", partes) + ".";
    }

    // ---------------------------------------------------------------- o Painel

    /// <summary>
    /// Quantos voltaram em cada um dos últimos <paramref name="meses"/> meses (o mês corrente por último), pela
    /// data em que o caso viu a sessão realizada (<see cref="LinhaAcompanhamento.VoltouEm"/>). Mês sem ninguém
    /// aparece com zero: buraco no gráfico é dado, não falha.
    /// </summary>
    public static IReadOnlyList<(DateOnly Mes, int Voltaram)> VoltaramPorMes(IEnumerable<LinhaAcompanhamento> linhas, DateOnly hoje, int meses = 6)
    {
        var inicio = new DateOnly(hoje.Year, hoje.Month, 1).AddMonths(-(meses - 1));
        var voltas = linhas.Where(l => l.VoltouEm is { } v && v >= inicio && v <= hoje).Select(l => l.VoltouEm!.Value).ToList();
        return Enumerable.Range(0, meses).Select(i => inicio.AddMonths(i))
            .Select(mes => (mes, voltas.Count(v => v.Year == mes.Year && v.Month == mes.Month))).ToList();
    }

    // ---------------------------------------------------------------- as conversas e o progresso do dia

    /// <summary>
    /// A página Conversas: a conversa da FICHA de cada caso aberto (a mais recente, se houver mais de uma), quem
    /// respondeu primeiro e depois a conversa mais recente. Conversa sem ficha conferida não entra — o telefone
    /// não é identidade (regra 4 da tela): quem confere a ficha é a recepção, na conversa. Um paciente com dois
    /// casos aparece uma vez, pelo caso de maior risco.
    /// </summary>
    public static IReadOnlyList<ConversaDaFila> ConversasDaFila(IEnumerable<LinhaAcompanhamento> linhas,
        IEnumerable<AppConversationSummary> conversas, DateTimeOffset agora)
    {
        var lista = conversas.ToList();
        var porPaciente = lista.Where(c => c.PatientId is not null).GroupBy(c => c.PatientId!.Value)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(c => c.UpdatedAt).First());
        var ultimas = ConversasNaTela.UltimaRecebidaPorPaciente(lista);
        return linhas.Where(l => l.Pendente && porPaciente.ContainsKey(l.PacienteId))
            .GroupBy(l => l.PacienteId).Select(g => g.OrderByDescending(l => Risco(l).Pontos).First())
            .Select(l => new ConversaDaFila(l, porPaciente[l.PacienteId],
                RetencaoNaTela.Respondeu(l, ultimas.TryGetValue(l.PacienteId, out var u) ? u : null, agora)))
            .OrderByDescending(x => x.Respondeu).ThenByDescending(x => x.Conversa.UpdatedAt).ToList();
    }

    /// <summary>Quantos casos tiveram contato registrado hoje — o "feitos" do progresso da Fila de hoje.</summary>
    public static int FeitosHoje(IEnumerable<LinhaAcompanhamento> linhas, DateOnly hoje)
        => linhas.Count(l => l.UltimoContato is { } u && DateOnly.FromDateTime(u) == hoje);

    /// <summary>"3 de 12 feitos" — o que já foi feito hoje sobre o que foi feito mais o que ainda pede ação.</summary>
    public static (int Feitos, int Total) ProgressoDeHoje(IEnumerable<LinhaAcompanhamento> linhas, IReadOnlyList<GrupoDaFila> grupos, DateOnly hoje)
    {
        var naFila = grupos.SelectMany(g => g.Linhas).Select(l => l.Id).ToHashSet();
        var feitos = linhas.Count(l => !naFila.Contains(l.Id) && l.UltimoContato is { } u && DateOnly.FromDateTime(u) == hoje);
        return (feitos, feitos + naFila.Count);
    }

    // ---------------------------------------------------------------- Painel e Carteira (as duas filas juntas)

    /// <summary>Os números do topo do Painel, sobre as duas filas e o escopo pedido (minha fila ou a equipe).</summary>
    public static NumerosDaFila Numeros(IEnumerable<LinhaAcompanhamento> linhas, IReadOnlyList<GrupoDaFila> hoje, bool soMinhaFila, int usuarioId, DateOnly dia)
    {
        var lista = linhas.Where(l => !soMinhaFila || AcompanhamentoNaTela.DaMinhaFila(l, usuarioId)).ToList();
        var abertos = lista.Where(l => l.Pendente).ToList();
        var funis = new[] { TipoAcompanhamento.Recall, TipoAcompanhamento.NovoBsv }
            .Select(t => AcompanhamentoNaTela.Funil(lista, t, false, usuarioId, dia)).ToList();
        return new(abertos.Count, abertos.Count(l => Risco(l).Faixa == FaixaDeRisco.Alto), abertos.Count(l => Risco(l).Faixa == FaixaDeRisco.Medio),
            hoje.Sum(g => g.Linhas.Count), funis.Sum(f => f.Voltaram), funis.Sum(f => f.ContatadosNoPeriodo), funis.Sum(f => f.VoltaramDosContatados));
    }

    /// <summary>A equipe e os motivos de encerramento, sobre as duas filas (a mesma conta da visão por responsável).</summary>
    public static (IReadOnlyList<CargaDoResponsavel> Responsaveis, IReadOnlyList<EncerramentosPorMotivo> Encerramentos) Equipe(
        IEnumerable<LinhaAcompanhamento> linhas, DateOnly hoje)
    {
        var partes = new[] { TipoAcompanhamento.Recall, TipoAcompanhamento.NovoBsv }
            .Select(t => ConversaDoAcompanhamento.VisaoDaEquipe(linhas, t, hoje)).ToList();
        var responsaveis = partes.SelectMany(p => p.Responsaveis).GroupBy(r => r.Responsavel)
            .Select(g => new CargaDoResponsavel(g.Key, g.Sum(x => x.Pendentes), g.Sum(x => x.Atrasados), g.Sum(x => x.Agendados), g.Sum(x => x.Voltaram)))
            .OrderByDescending(c => c.Pendentes).ThenBy(c => c.Responsavel, StringComparer.CurrentCultureIgnoreCase).ToList();
        var encerramentos = partes.SelectMany(p => p.Encerramentos).GroupBy(e => e.Motivo)
            .Select(g => new EncerramentosPorMotivo(g.Key, g.Sum(x => x.Quantidade)))
            .OrderByDescending(e => e.Quantidade).ThenBy(e => e.Motivo, StringComparer.CurrentCultureIgnoreCase).ToList();
        return (responsaveis, encerramentos);
    }

    /// <summary>
    /// A Carteira: as duas filas pela regra de sempre (<see cref="AcompanhamentoNaTela.Filtrar"/> — busca, "Mais filtros",
    /// minha fila), recortadas pela visão. "Todos" e as outras visões são os casos ABERTOS; "Encerrados", os fechados com motivo.
    /// Do maior risco para o menor; empate, pela ordem de trabalho de sempre.
    /// </summary>
    public static IReadOnlyList<LinhaAcompanhamento> Carteira(IEnumerable<LinhaAcompanhamento> linhas, FiltroAcompanhamento filtro,
        VisaoDaCarteira visao, int usuarioId, DateOnly hoje)
    {
        var faixa = visao == VisaoDaCarteira.Encerrados ? FaixaAcompanhamento.Encerrado : (FaixaAcompanhamento?)null;
        var tipos = visao == VisaoDaCarteira.NovosBsv ? new[] { TipoAcompanhamento.NovoBsv } : new[] { TipoAcompanhamento.Recall, TipoAcompanhamento.NovoBsv };
        var lista = linhas.ToList();
        var ordem = 0;
        return tipos.SelectMany(t => AcompanhamentoNaTela.Filtrar(lista, filtro with { Tipo = t, Faixa = faixa }, usuarioId, hoje))
            .Where(l => visao switch
            {
                VisaoDaCarteira.RiscoAlto => Risco(l).Faixa == FaixaDeRisco.Alto,
                VisaoDaCarteira.SemAutorizacao => string.IsNullOrWhiteSpace(l.Telefone) || !l.Consentimento,
                _ => true
            })
            .Select(l => (Linha: l, Ordem: ordem++))
            .OrderByDescending(x => Risco(x.Linha).Pontos).ThenBy(x => x.Ordem).Select(x => x.Linha).ToList();
    }

    /// <summary>Quantos casos abertos estão com risco alto (o atalho do topo).</summary>
    public static int RiscoAlto(IEnumerable<LinhaAcompanhamento> linhas) => linhas.Count(l => Risco(l).Faixa == FaixaDeRisco.Alto);
}
