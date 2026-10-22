using System.Globalization;
using Clinica.Application.Abstracoes;
using Clinica.Application.Modelos;
using Clinica.Domain.Entities;

namespace Clinica.Application.Servicos;

/// <summary>O que a pessoa perguntou ao Sistema SemDor.</summary>
public enum IntencaoDoSistema
{
    Ajuda,
    Agenda,
    Confirmaram,
    Remarcar,
    NaoConfirmaram,
    Proximo,
    Chegaram,
    Faltas,
    Pedidos,
    NovoPedido,
    Paciente,
    Saudacao,
    NaoEntendi,
}

/// <summary>
/// A pergunta interpretada: a intenção, o dia, o profissional citado (quando há) e o termo
/// procurado (o nome do paciente).
/// </summary>
public sealed record PerguntaAoSistema(IntencaoDoSistema Intencao, DateOnly Dia, int? ProfissionalId = null,
    string? Termo = null, bool DiaCitado = false);

/// <summary>Um horário como o sistema o descreve: o que a agenda tem e a resposta ao lembrete.</summary>
public sealed record HorarioDoSistema(int AgendamentoId, DateTime DataHora, int PacienteId, string Paciente,
    string Procedimento, string Profissional, StatusAgendamento Status, EtapaFila Etapa, SituacaoConfirmacao? Confirmacao);

/// <summary>
/// As regras do Sistema SemDor (out/2026 — chat da equipe integrado ao CRM). Ele responde, na
/// conversa de cada pessoa, à agenda do dia, a quem confirmou, a quem pediu para remarcar, aos
/// pedidos à recepção — com os MESMOS dados e as MESMAS palavras da agenda do balcão e do CRM
/// ("Confirmou", "Não vem — remarcar", "Não confirmou"; <see cref="RodadaDeConfirmacao"/>).
///
/// Por que regras, e não um modelo de linguagem: a direção retirou a IA do CRM, a agenda é dado
/// de paciente (nada vai para fora da clínica — LGPD, art. 33) e quem pergunta "quem confirmou
/// amanhã?" precisa da MESMA resposta toda vez, sem inventar. O que não se entende vira a lista
/// do que dá para perguntar, com botões — nunca um palpite.
/// </summary>
public static class SistemaEquipeRegras
{
    private static readonly CultureInfo PtBr = CultureInfo.GetCultureInfo("pt-BR");

    /// <summary>Quantas linhas um cartão mostra; o resto vira o rodapé com o total.</summary>
    public const int LinhasNoCartao = 40;

    /// <summary>As perguntas que viram botão na ajuda e no fim das respostas.</summary>
    public static IReadOnlyList<string> SugestoesIniciais { get; } =
        ["Minha agenda de hoje", "Agenda de amanhã", "Quem não confirmou", "Quem pediu para remarcar", "Meus pedidos"];

    // ================================================================ interpretar

    /// <summary>
    /// Lê a pergunta. Sem acento e sem caixa; o dia sai de "hoje", "amanhã", "ontem", "depois de
    /// amanhã", do dia da semana ("sexta" é a próxima sexta, ou hoje se hoje é sexta) ou da data
    /// ("24/10", "dia 24"). O profissional é achado pelo nome entre os da clínica.
    /// </summary>
    public static PerguntaAoSistema Interpretar(string texto, DateOnly hoje, IReadOnlyList<(int Id, string Nome)> profissionais)
    {
        var t = " " + Limpar(texto) + " ";
        var (dia, citado) = Dia(t, hoje);
        var profissional = Profissional(t, profissionais);

        if (t.Trim().Length == 0 || Tem(t, "ajuda", "help", "comandos", "o que voce faz", "o que da para", "como funciona", "menu"))
            return new(IntencaoDoSistema.Ajuda, dia);
        if (Tem(t, "pedir", "peca", "pede ", "solicitar", "solicito", "avisar a recepcao", "pedido novo", "novo pedido"))
            return new(IntencaoDoSistema.NovoPedido, dia, profissional);
        if (Tem(t, "pedido"))
            return new(IntencaoDoSistema.Pedidos, dia);
        if (Tem(t, "remarc", "nao vem", "nao vai vir", "desmarc"))
            return new(IntencaoDoSistema.Remarcar, dia, profissional, DiaCitado: citado);
        if (Tem(t, "nao confirm", "sem resposta", "nao respond", "pendente", "falta confirmar"))
            return new(IntencaoDoSistema.NaoConfirmaram, dia, profissional, DiaCitado: citado);
        if (Tem(t, "confirm"))
            return new(IntencaoDoSistema.Confirmaram, dia, profissional, DiaCitado: citado);
        if (Tem(t, "proximo", "proxima", "quem e o seguinte", "quem vem agora"))
            return new(IntencaoDoSistema.Proximo, hoje, profissional);
        if (Tem(t, "chegou", "chegaram", "sala de espera", "esperando", "aguardando"))
            return new(IntencaoDoSistema.Chegaram, hoje, profissional);
        if (Tem(t, "falt"))
            return new(IntencaoDoSistema.Faltas, dia, profissional, DiaCitado: citado);
        if (Termo(texto ?? "", "paciente", "ficha", "procurar", "buscar") is { } termo)
            return new(IntencaoDoSistema.Paciente, dia, Termo: termo);
        if (Tem(t, "agenda", "horario", "meu dia", "pacientes", "atendimentos", "consultas", "sessoes") || citado || profissional is not null)
            return new(IntencaoDoSistema.Agenda, dia, profissional, DiaCitado: citado);
        if (Tem(t, "oi ", "ola", "bom dia", "boa tarde", "boa noite", "obrigad", "valeu"))
            return new(IntencaoDoSistema.Saudacao, dia);
        return new(IntencaoDoSistema.NaoEntendi, dia);
    }

    private static string Limpar(string texto)
    {
        var t = ChatEquipeRegras.TextoDeBusca(texto ?? "");
        var sb = new System.Text.StringBuilder(t.Length);
        foreach (var c in t) sb.Append(char.IsLetterOrDigit(c) || c == '/' ? c : ' ');
        return string.Join(' ', sb.ToString().Split(' ', StringSplitOptions.RemoveEmptyEntries));
    }

    private static bool Tem(string t, params string[] chaves) => chaves.Any(c => t.Contains(c, StringComparison.Ordinal));

    /// <summary>
    /// O nome procurado depois de "paciente", "ficha"…, COMO FOI ESCRITO (com acento: a busca de
    /// paciente compara o nome gravado, e "José" sem o acento não acharia o José). As palavras do
    /// dia e as ligações soltas ("de hoje") não são do nome.
    /// </summary>
    private static readonly char[] SeparadoresDoTermo = [' ', '\t', '\n', '\r', ',', '?', '!', ':'];

    private static string? Termo(string original, params string[] prefixos)
    {
        var palavras = original.Split(SeparadoresDoTermo, StringSplitOptions.RemoveEmptyEntries).ToList();
        var i = palavras.FindIndex(w => prefixos.Contains(ChatEquipeRegras.TextoDeBusca(w)));
        if (i < 0) return null;
        var nome = palavras.Skip(i + 1).TakeWhile(w => ChatEquipeRegras.TextoDeBusca(w) is not ("hoje" or "amanha" or "ontem"))
            .ToList();
        while (nome.Count > 0 && ChatEquipeRegras.TextoDeBusca(nome[^1]) is "de" or "da" or "do") nome.RemoveAt(nome.Count - 1);
        while (nome.Count > 0 && ChatEquipeRegras.TextoDeBusca(nome[0]) is "o" or "a" or "do" or "da" or "de") nome.RemoveAt(0);
        var termo = string.Join(' ', nome).Trim('.', ' ');
        return termo.Length >= 2 ? termo : null;
    }

    private static readonly (string Nome, DayOfWeek Dia)[] Semana =
    [
        ("segunda", DayOfWeek.Monday), ("terca", DayOfWeek.Tuesday), ("quarta", DayOfWeek.Wednesday),
        ("quinta", DayOfWeek.Thursday), ("sexta", DayOfWeek.Friday), ("sabado", DayOfWeek.Saturday), ("domingo", DayOfWeek.Sunday),
    ];

    /// <summary>O dia da pergunta e se ela citou um dia (sem citar, é hoje).</summary>
    public static (DateOnly Dia, bool Citado) Dia(string t, DateOnly hoje)
    {
        if (t.Contains(" depois de amanha ", StringComparison.Ordinal)) return (hoje.AddDays(2), true);
        if (t.Contains(" amanha ", StringComparison.Ordinal)) return (hoje.AddDays(1), true);
        if (t.Contains(" ontem ", StringComparison.Ordinal)) return (hoje.AddDays(-1), true);
        if (t.Contains(" hoje ", StringComparison.Ordinal)) return (hoje, true);

        foreach (var palavra in t.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            var partes = palavra.Split('/');
            if (partes.Length is 2 or 3 && int.TryParse(partes[0], out var d) && int.TryParse(partes[1], out var m)
                && m is >= 1 and <= 12 && d >= 1 && d <= DateTime.DaysInMonth(hoje.Year, m))
            {
                var ano = partes.Length == 3 && int.TryParse(partes[2], out var a) ? (a < 100 ? 2000 + a : a) : hoje.Year;
                if (ano is >= 2000 and <= 2100 && d <= DateTime.DaysInMonth(ano, m))
                {
                    var data = new DateOnly(ano, m, d);
                    // "05/01" escrito em dezembro é o janeiro que vem, não o que passou.
                    if (partes.Length == 2 && data < hoje.AddDays(-60)) data = data.AddYears(1);
                    return (data, true);
                }
            }
        }

        var dia = System.Text.RegularExpressions.Regex.Match(t, @" dia (\d{1,2}) ");
        if (dia.Success && int.TryParse(dia.Groups[1].Value, out var n) && n >= 1)
        {
            var mes = new DateOnly(hoje.Year, hoje.Month, 1);
            if (n < hoje.Day) mes = mes.AddMonths(1);
            if (n <= DateTime.DaysInMonth(mes.Year, mes.Month)) return (new DateOnly(mes.Year, mes.Month, n), true);
        }

        foreach (var (nome, semana) in Semana)
            if (t.Contains(" " + nome, StringComparison.Ordinal))
            {
                var faltam = ((int)semana - (int)hoje.DayOfWeek + 7) % 7;
                return (hoje.AddDays(faltam), true);
            }
        return (hoje, false);
    }

    /// <summary>
    /// O profissional citado: um nome da clínica cujo primeiro nome (ou o nome inteiro) aparece na
    /// pergunta. Dois com o mesmo primeiro nome e só ele escrito: não escolhe — devolve nulo, e a
    /// resposta é a da pessoa que perguntou (o sistema não decide por palpite).
    /// </summary>
    public static int? Profissional(string t, IReadOnlyList<(int Id, string Nome)> profissionais)
    {
        var achados = new List<(int Id, int Peso)>();
        foreach (var (id, nome) in profissionais)
        {
            var palavras = Limpar(nome).Split(' ', StringSplitOptions.RemoveEmptyEntries)
                .Where(w => w.Length > 2 && w is not ("dra" or "dr" or "dos" or "das")).ToList();
            if (palavras.Count == 0) continue;
            var inteiro = string.Join(' ', palavras);
            if (t.Contains(" " + inteiro + " ", StringComparison.Ordinal)) achados.Add((id, 3));
            else if (palavras.Count > 1 && t.Contains($" {palavras[0]} {palavras[^1]} ", StringComparison.Ordinal)) achados.Add((id, 2));
            else if (t.Contains(" " + palavras[0] + " ", StringComparison.Ordinal)) achados.Add((id, 1));
        }
        if (achados.Count == 0) return null;
        var maior = achados.Max(a => a.Peso);
        var melhores = achados.Where(a => a.Peso == maior).Select(a => a.Id).Distinct().ToList();
        return melhores.Count == 1 ? melhores[0] : null;
    }

    // ================================================================ a situação de cada horário

    /// <summary>
    /// O selo do horário: primeiro o que aconteceu no balcão (chegou, em atendimento, atendido,
    /// faltou), depois a resposta ao lembrete — com as palavras da agenda do balcão e do CRM.
    /// </summary>
    public static (string Selo, string Tom) Situacao(StatusAgendamento status, EtapaFila etapa, SituacaoConfirmacao? confirmacao)
        => (status, etapa, confirmacao) switch
        {
            (StatusAgendamento.Faltou, _, _) => ("Faltou", TomDoCartao.Alerta),
            (StatusAgendamento.Cancelado, _, _) => ("Cancelado", TomDoCartao.Neutro),
            (StatusAgendamento.Realizado, _, _) or (_, EtapaFila.Finalizado, _) => ("Atendido", TomDoCartao.Ok),
            (_, EtapaFila.EmAtendimento, _) => ("Em atendimento", TomDoCartao.Info),
            (_, EtapaFila.Chamado, _) => ("Chamado", TomDoCartao.Info),
            (_, EtapaFila.Chegou, _) => ("Chegou", TomDoCartao.Info),
            (_, _, SituacaoConfirmacao.Confirmou) => ("Confirmou", TomDoCartao.Ok),
            (_, _, SituacaoConfirmacao.PediuRemarcar) => ("Não vem — remarcar", TomDoCartao.Alerta),
            (_, _, SituacaoConfirmacao.PediuCancelar) => ("Pediu para cancelar", TomDoCartao.Alerta),
            (_, _, SituacaoConfirmacao.AvisadoSemResposta) => ("Não confirmou", TomDoCartao.Atencao),
            _ => ("Sem lembrete", TomDoCartao.Neutro),
        };

    /// <summary>O horário ainda pode virar pedido à recepção: marcado, futuro e sem chegada.</summary>
    public static bool PodePedir(HorarioDoSistema h, DateTime agora)
        => h.Status == StatusAgendamento.Agendado && h.Etapa == EtapaFila.Aguardando && h.DataHora > agora;

    /// <summary>Os horários que contam numa agenda: cancelado e substituído saem (como na grade do balcão).</summary>
    public static bool ContaNaAgenda(StatusAgendamento s) => s is not (StatusAgendamento.Cancelado or StatusAgendamento.Substituido);

    // ================================================================ montar as respostas

    /// <summary>"Hoje · qui, 22/10" — o mesmo título do dia da rodada de confirmação.</summary>
    public static string TituloDoDia(DateOnly dia, DateOnly hoje) => RodadaDeConfirmacao.TituloDoDia(dia, hoje);

    /// <summary>
    /// O cartão da agenda: o resumo do dia (horários, confirmaram, remarcar, não confirmaram,
    /// chegaram/atendidos) e uma linha por horário. <paramref name="deQuem"/> é "Sua agenda",
    /// "Agenda da Dra. Helena" ou "Agenda da clínica" — com a clínica inteira, a linha diz o
    /// profissional.
    /// </summary>
    public static (string Texto, CartaoChat Cartao) Agenda(string deQuem, DateOnly dia, DateOnly hoje,
        IReadOnlyList<HorarioDoSistema> horarios, DateTime agora, bool mostrarProfissional, IntencaoDoSistema filtro = IntencaoDoSistema.Agenda)
    {
        var doDia = horarios.Where(h => ContaNaAgenda(h.Status)).OrderBy(h => h.DataHora).ThenBy(h => h.Paciente, StringComparer.CurrentCultureIgnoreCase).ToList();
        var situacoes = doDia.Select(h => (H: h, S: Situacao(h.Status, h.Etapa, h.Confirmacao))).ToList();

        var confirmaram = situacoes.Count(x => x.S.Selo == "Confirmou");
        var remarcar = situacoes.Count(x => x.S.Selo is "Não vem — remarcar" or "Pediu para cancelar");
        var naoConfirmaram = situacoes.Count(x => x.S.Selo == "Não confirmou");
        var noBalcao = situacoes.Count(x => x.S.Selo is "Chegou" or "Chamado" or "Em atendimento");
        var atendidos = situacoes.Count(x => x.S.Selo == "Atendido");
        var faltas = situacoes.Count(x => x.S.Selo == "Faltou");

        var mostrados = filtro switch
        {
            IntencaoDoSistema.Confirmaram => situacoes.Where(x => x.S.Selo == "Confirmou").ToList(),
            IntencaoDoSistema.Remarcar => situacoes.Where(x => x.S.Selo is "Não vem — remarcar" or "Pediu para cancelar").ToList(),
            IntencaoDoSistema.NaoConfirmaram => situacoes.Where(x => x.S.Selo is "Não confirmou" or "Sem lembrete" && x.H.DataHora > agora).ToList(),
            IntencaoDoSistema.Chegaram => situacoes.Where(x => x.S.Selo is "Chegou" or "Chamado" or "Em atendimento").ToList(),
            IntencaoDoSistema.Faltas => situacoes.Where(x => x.S.Selo == "Faltou").ToList(),
            _ => situacoes,
        };

        var titulo = filtro switch
        {
            IntencaoDoSistema.Confirmaram => "Quem confirmou",
            IntencaoDoSistema.Remarcar => "Quem pediu para remarcar ou cancelar",
            IntencaoDoSistema.NaoConfirmaram => "Quem ainda não confirmou",
            IntencaoDoSistema.Chegaram => "Na clínica agora",
            IntencaoDoSistema.Faltas => "Faltas",
            _ => deQuem,
        };
        var subtitulo = filtro == IntencaoDoSistema.Agenda ? TituloDoDia(dia, hoje) : $"{deQuem} · {TituloDoDia(dia, hoje)}";

        var resumo = new List<ResumoDoCartao> { new(doDia.Count == 1 ? "horário" : "horários", doDia.Count) };
        if (dia >= hoje)
        {
            resumo.Add(new("confirmaram", confirmaram, TomDoCartao.Ok));
            resumo.Add(new("remarcar", remarcar, remarcar > 0 ? TomDoCartao.Alerta : TomDoCartao.Neutro));
            resumo.Add(new("não confirmaram", naoConfirmaram, naoConfirmaram > 0 ? TomDoCartao.Atencao : TomDoCartao.Neutro));
        }
        if (dia == hoje) resumo.Add(new("na clínica", noBalcao, TomDoCartao.Info));
        if (dia <= hoje) resumo.Add(new("atendidos", atendidos, TomDoCartao.Ok));
        if (dia <= hoje && faltas > 0) resumo.Add(new("faltas", faltas, TomDoCartao.Alerta));

        var linhas = mostrados.Take(LinhasNoCartao).Select(x => new LinhaDoCartao(
            x.H.DataHora.ToString("HH:mm", PtBr), x.H.Paciente,
            mostrarProfissional && x.H.Profissional.Length > 0 ? $"{x.H.Procedimento} · {x.H.Profissional}" : x.H.Procedimento,
            x.S.Selo, x.S.Tom, x.H.PacienteId, x.H.AgendamentoId, PodePedir(x.H, agora))).ToList();
        var rodape = mostrados.Count > LinhasNoCartao
            ? $"Mostrando {LinhasNoCartao} de {mostrados.Count}. A lista inteira está na Agenda."
            : mostrados.Count == 0 ? Vazio(filtro, doDia.Count) : null;

        var sugestoes = new List<string>();
        if (filtro != IntencaoDoSistema.Agenda) sugestoes.Add(dia == hoje ? "Agenda de hoje" : $"Agenda de {dia:dd/MM}");
        if (dia == hoje) sugestoes.Add("Agenda de amanhã");
        if (filtro != IntencaoDoSistema.NaoConfirmaram && naoConfirmaram > 0) sugestoes.Add(dia == hoje ? "Quem não confirmou" : $"Quem não confirmou {dia:dd/MM}");
        if (filtro != IntencaoDoSistema.Remarcar && remarcar > 0) sugestoes.Add(dia == hoje ? "Quem pediu para remarcar" : $"Quem pediu para remarcar {dia:dd/MM}");
        if (dia == hoje && filtro != IntencaoDoSistema.Proximo) sugestoes.Add("Próximo paciente");

        var cartao = new CartaoChat(filtro == IntencaoDoSistema.Agenda ? "agenda" : "lista", titulo, subtitulo, resumo, linhas,
            sugestoes.Distinct().Take(4).ToList(), rodape);
        return (TextoDoCartao(cartao), cartao);
    }

    private static string Vazio(IntencaoDoSistema filtro, int total) => filtro switch
    {
        _ when total == 0 => "Nenhum horário neste dia.",
        IntencaoDoSistema.Confirmaram => "Ninguém confirmou ainda.",
        IntencaoDoSistema.Remarcar => "Ninguém pediu para remarcar ou cancelar.",
        IntencaoDoSistema.NaoConfirmaram => "Todos os próximos horários já responderam.",
        IntencaoDoSistema.Chegaram => "Ninguém na clínica agora.",
        IntencaoDoSistema.Faltas => "Nenhuma falta.",
        _ => "Nada a mostrar.",
    };

    /// <summary>O próximo paciente: o primeiro horário de hoje que ainda não terminou.</summary>
    public static (string Texto, CartaoChat Cartao) Proximo(string deQuem, IReadOnlyList<HorarioDoSistema> hoje, DateTime agora)
    {
        var proximo = hoje.Where(h => h.Status == StatusAgendamento.Agendado && h.Etapa != EtapaFila.Finalizado
                                      && (h.Etapa != EtapaFila.Aguardando || h.DataHora >= agora.AddMinutes(-30)))
            .OrderBy(h => h.Etapa == EtapaFila.EmAtendimento ? 1 : 0).ThenBy(h => h.DataHora).FirstOrDefault();
        if (proximo is null)
        {
            var vazio = new CartaoChat("lista", "Próximo paciente", deQuem, Rodape: "Não há mais ninguém marcado para hoje.",
                Sugestoes: ["Agenda de amanhã"]);
            return (TextoDoCartao(vazio), vazio);
        }
        var (selo, tom) = Situacao(proximo.Status, proximo.Etapa, proximo.Confirmacao);
        var restantes = hoje.Count(h => h.Status == StatusAgendamento.Agendado && h.Etapa == EtapaFila.Aguardando && h.DataHora > proximo.DataHora);
        var cartao = new CartaoChat("lista", "Próximo paciente", deQuem,
            [new("depois dele hoje", restantes)],
            [new(proximo.DataHora.ToString("HH:mm", PtBr), proximo.Paciente, proximo.Procedimento, selo, tom,
                proximo.PacienteId, proximo.AgendamentoId, PodePedir(proximo, agora))],
            ["Agenda de hoje", "Quem já chegou"]);
        return (TextoDoCartao(cartao), cartao);
    }

    /// <summary>O que dá para perguntar — a resposta da ajuda e de tudo o que o sistema não entendeu.</summary>
    public static (string Texto, CartaoChat Cartao) Ajuda(bool naoEntendeu, bool trataPedidos)
    {
        var linhas = new List<LinhaDoCartao>
        {
            new("", "Minha agenda de hoje", "também: amanhã, sexta, 24/10, \"agenda da Dra. Helena\""),
            new("", "Quem confirmou · Quem não confirmou", "a resposta de cada paciente ao lembrete"),
            new("", "Quem pediu para remarcar", "\"Não vem — remarcar\" e \"Pediu para cancelar\""),
            new("", "Próximo paciente · Quem já chegou", "o balcão em tempo real"),
            new("", "Paciente Maria Silva", "os próximos horários de um paciente"),
            new("", trataPedidos ? "Pedidos" : "Meus pedidos", trataPedidos ? "os pedidos da equipe em aberto" : "o que você pediu à recepção e o desfecho"),
        };
        var cartao = new CartaoChat("ajuda",
            naoEntendeu ? "Não entendi a pergunta" : "O que posso responder",
            "Respostas do sistema da clínica, com os dados da agenda — nada sai da clínica.",
            Linhas: linhas, Sugestoes: SugestoesIniciais,
            Rodape: "Para pedir algo à recepção (remarcar, encaixar, avisar o paciente), use \"Pedir à recepção\" ou o botão na linha do horário.");
        return (TextoDoCartao(cartao), cartao);
    }

    /// <summary>
    /// O texto que acompanha o cartão: legível sozinho (a busca, a prévia e quem não desenha o
    /// cartão leem ESTE texto), com o resumo e as linhas.
    /// </summary>
    public static string TextoDoCartao(CartaoChat c)
    {
        var sb = new System.Text.StringBuilder();
        sb.Append(c.Titulo);
        if (!string.IsNullOrWhiteSpace(c.Subtitulo)) sb.Append(" · ").Append(c.Subtitulo);
        if (c.Resumo is { Count: > 0 } r)
            sb.Append('\n').Append(string.Join(" · ", r.Select(x => $"{x.Valor} {x.Rotulo}")));
        foreach (var l in c.Linhas ?? [])
        {
            sb.Append('\n');
            if (l.Hora.Length > 0) sb.Append(l.Hora).Append(' ');
            sb.Append(l.Titulo);
            if (!string.IsNullOrWhiteSpace(l.Detalhe)) sb.Append(" — ").Append(l.Detalhe);
            if (!string.IsNullOrWhiteSpace(l.Selo)) sb.Append(" [").Append(l.Selo).Append(']');
        }
        if (!string.IsNullOrWhiteSpace(c.Rodape)) sb.Append('\n').Append(c.Rodape);
        return sb.ToString();
    }

    /// <summary>A resposta curta (sem cartão) para o cumprimento.</summary>
    public static string Saudacao(string nome, DateTime agora)
    {
        var periodo = agora.Hour < 12 ? "Bom dia" : agora.Hour < 18 ? "Boa tarde" : "Boa noite";
        var primeiro = nome.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault(w => !w.EndsWith('.')) ?? nome;
        return $"{periodo}, {primeiro}! Pergunte a agenda, quem confirmou ou os seus pedidos à recepção.";
    }
}
