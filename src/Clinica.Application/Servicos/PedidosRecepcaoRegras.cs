using System.Globalization;
using Clinica.Application.Abstracoes;
using Clinica.Domain.Entities;

namespace Clinica.Application.Servicos;

/// <summary>
/// As regras do pedido à recepção (out/2026): os tipos, quem pede, quem trata, as situações e as
/// frases do cartão. O pedido é um RECADO com dono e desfecho — nunca mexe na agenda sozinho; quem
/// remarca, cancela ou encaixa é a recepção, pela agenda, com as regras dela.
/// </summary>
public static class PedidosRecepcaoRegras
{
    public const string Aberto = "Aberto";
    public const string EmAndamento = "EmAndamento";
    public const string Concluido = "Concluido";
    public const string NaoFeito = "NaoFeito";

    public const int DetalheMaximo = 2000;

    /// <summary>Os tipos, na ordem do formulário, com o rótulo que a tela mostra.</summary>
    public static IReadOnlyList<(string Chave, string Rotulo)> Tipos { get; } =
    [
        ("remarcar", "Remarcar"),
        ("cancelar", "Cancelar"),
        ("encaixar", "Encaixar"),
        ("avisar", "Avisar o paciente"),
        ("outro", "Outro"),
    ];

    public static bool TipoValido(string? tipo) => Tipos.Any(t => t.Chave == tipo);

    public static string RotuloDoTipo(string tipo) => Tipos.FirstOrDefault(t => t.Chave == tipo).Rotulo ?? tipo;

    /// <summary>O tipo pede um horário marcado? Remarcar e cancelar falam de um horário que existe.</summary>
    public static bool PedeHorario(string tipo) => tipo is "remarcar" or "cancelar";

    public static string RotuloDaSituacao(string situacao) => situacao switch
    {
        Aberto => "Aberto",
        EmAndamento => "Em andamento",
        Concluido => "Concluído",
        NaoFeito => "Não foi possível",
        _ => situacao,
    };

    public static string TomDaSituacao(string situacao) => situacao switch
    {
        Aberto => TomDoCartao.Atencao,
        EmAndamento => TomDoCartao.Info,
        Concluido => TomDoCartao.Ok,
        NaoFeito => TomDoCartao.Alerta,
        _ => TomDoCartao.Neutro,
    };

    public static bool Encerrado(string situacao) => situacao is Concluido or NaoFeito;

    /// <summary>
    /// Quem trata pedidos: quem marca e remarca (<see cref="Permissao.EditarAgenda"/>) — a
    /// recepção, a direção e quem a direção liberar. O pedido é tratado no # Recepção dos apps (o chat
    /// da equipe saiu do CRM em out/2026).
    /// </summary>
    public static bool PodeTratar(UsuarioSistema u) => u.Ativo && u.Pode(Permissao.EditarAgenda);

    /// <summary>Quem pede: qualquer pessoa ativa da equipe (o médico, a enfermagem, a direção).</summary>
    public static bool PodePedir(UsuarioSistema u) => u.Ativo;

    /// <summary>
    /// A transição pedida vale? Assumir só o que está aberto (ou passar adiante o que está em
    /// andamento); encerrar só o que não terminou. Encerrado não volta: um novo pedido é outro recado.
    /// </summary>
    public static string? RecusaDaTransicao(string situacaoAtual, string acao) => (situacaoAtual, acao) switch
    {
        (Concluido or NaoFeito, _) => "Este pedido já foi encerrado.",
        (_, "assumir") => null,
        (_, "concluir" or "nao-feito") => null,
        _ => "Ação desconhecida.",
    };

    private static readonly CultureInfo PtBr = CultureInfo.GetCultureInfo("pt-BR");

    /// <summary>"qui, 24/10 às 14:00" — o horário como a recepção fala dele.</summary>
    public static string Horario(DateTime h)
    {
        var semana = h.DayOfWeek switch
        {
            DayOfWeek.Monday => "seg", DayOfWeek.Tuesday => "ter", DayOfWeek.Wednesday => "qua",
            DayOfWeek.Thursday => "qui", DayOfWeek.Friday => "sex", DayOfWeek.Saturday => "sáb", _ => "dom"
        };
        return $"{semana}, {h.ToString("dd/MM", PtBr)} às {h.ToString("HH:mm", PtBr)}";
    }

    /// <summary>A linha que resume o pedido: "Remarcar · Maria Silva · qui, 24/10 às 14:00".</summary>
    public static string Resumo(string tipo, string paciente, DateTime? horario)
        => string.Join(" · ", new[] { RotuloDoTipo(tipo), paciente, horario is { } h ? Horario(h) : null }
            .Where(x => !string.IsNullOrWhiteSpace(x)));

    /// <summary>
    /// O cartão do pedido — o mesmo no # Recepção e na conversa de quem pediu; a situação muda
    /// nos dois quando alguém assume ou encerra.
    /// </summary>
    public static CartaoChat Cartao(PedidoRecepcao p)
    {
        var linhas = new List<LinhaDoCartao>
        {
            new(p.HorarioEm is { } h ? h.ToString("HH:mm", PtBr) : "", p.PacienteNome.Length > 0 ? p.PacienteNome : "Sem paciente",
                p.HorarioEm is { } hh ? Horario(hh) : null, RotuloDaSituacao(p.Situacao), TomDaSituacao(p.Situacao), p.PacienteId),
        };
        if (p.Detalhe.Length > 0) linhas.Add(new("", "Pedido", p.Detalhe));
        if (p.ResponsavelNome.Length > 0 && !Encerrado(p.Situacao))
            linhas.Add(new("", "Com " + p.ResponsavelNome, p.AssumidoEm is { } a ? "desde " + a.ToString("HH:mm", PtBr) : null));
        if (Encerrado(p.Situacao))
            linhas.Add(new("", (p.Situacao == Concluido ? "Feito por " : "Respondido por ") + p.ResponsavelNome,
                p.Desfecho.Length > 0 ? p.Desfecho : null));
        if (DoPaciente(p))
            return new CartaoChat("pedido", "Paciente pediu para " + RotuloDoTipo(p.Tipo).ToLowerInvariant(), "pelo WhatsApp, no lembrete",
                Linhas: linhas, PedidoId: p.Id, Situacao: p.Situacao, Oferta: Oferta(p));
        return new CartaoChat("pedido", "Pedido à recepção · " + RotuloDoTipo(p.Tipo), $"de {p.CriadoPorNome}",
            Linhas: linhas, PedidoId: p.Id, Situacao: p.Situacao);
    }

    /// <summary>O pedido veio do paciente (o "Remarcar" do lembrete pelo WhatsApp), e não de alguém da equipe.</summary>
    public static bool DoPaciente(PedidoRecepcao p) => p.Origem == RemarcacaoPeloPacienteRegras.OrigemPaciente;

    /// <summary>A conversa com o paciente, para o cartão: o que foi oferecido, se chegou e o que ele escolheu.</summary>
    public static OfertaDoCartao Oferta(PedidoRecepcao p) => new(
        RemarcacaoPeloPacienteRegras.Ler(p.HorariosOferecidos),
        p.OfertaTratadaEm is not null && p.OfertaFalha.Length == 0,
        p.OfertaFalha.Length > 0 ? p.OfertaFalha : null,
        p.HorarioEscolhido);

    /// <summary>O texto legível do cartão (busca, prévia, quem não desenha o cartão).</summary>
    public static string Texto(PedidoRecepcao p)
    {
        var t = (DoPaciente(p) ? "Pedido do paciente pelo WhatsApp: " : "Pedido à recepção: ") + Resumo(p.Tipo, p.PacienteNome, p.HorarioEm);
        if (DoPaciente(p) && p.HorarioEscolhido is { } escolhido) t += $" — escolheu {Horario(escolhido)}";
        if (p.Detalhe.Length > 0) t += $" — {p.Detalhe}";
        return t + $" [{RotuloDaSituacao(p.Situacao)}]";
    }

    /// <summary>
    /// A mensagem do sistema a quem pediu quando o pedido termina: o que foi feito (ou por que
    /// não), por quem — sem precisar perguntar.
    /// </summary>
    public static string Desfecho(PedidoRecepcao p)
    {
        var o = Resumo(p.Tipo, p.PacienteNome, p.HorarioEm);
        return p.Situacao == Concluido
            ? $"{p.ResponsavelNome} concluiu o seu pedido ({o})." + (p.Desfecho.Length > 0 ? $" {p.Desfecho}" : "")
            : $"{p.ResponsavelNome} não conseguiu atender o seu pedido ({o})." + (p.Desfecho.Length > 0 ? $" Motivo: {p.Desfecho}" : "");
    }
}
