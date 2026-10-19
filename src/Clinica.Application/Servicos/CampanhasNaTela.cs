using System.Globalization;
using Clinica.Application.Modelos;
using Clinica.Domain;
using Clinica.Domain.Entities;

namespace Clinica.Application.Servicos;

/// <summary>Uma linha da fila de contatos, já formatada para a tela.</summary>
public sealed record LinhaDeCampanha(
    int Id,
    string Paciente,
    string Telefone,
    string Campanha,
    string Referencia,
    string Situacao,
    StatusContato Status,
    string Nota,
    string Comentario,
    // Só o NPS registra nota — o botão some nos outros tipos.
    bool EhNps,
    // Sem telefone válido não há WhatsApp a abrir; a linha continua na tela.
    bool TemTelefone,
    // Já respondido ou dispensado não volta ao fluxo — as ações ficam desligadas.
    bool PodeAgir,
    // Dados crus, para montar a mensagem sem reparsear o que está na tela.
    string NomeCru,
    string TelefoneCru,
    TipoContato TipoCru,
    DateTime? QuandoCru,
    int DiasSemVir)
{
    /// <summary>Só há WhatsApp a abrir com telefone válido e contato ainda em aberto.</summary>
    public bool PodeEnviar => PodeAgir && TemTelefone;
}

/// <summary>A nota do NPS lida do que a pessoa digitou: a nota e o comentário, ou o erro.</summary>
public sealed record NotaDigitada(int? Nota, string? Comentario, string? Erro);

/// <summary>
/// O que a tela de CAMPANHAS do Gerente AFIRMA (out/2026, etapa 6 — a tela saiu do WPF para o
/// Blazor). Era o <c>CampanhasViewModel</c>: os filtros, a janela de tempo, a linha da fila, a
/// mensagem do WhatsApp, a leitura da nota e a frase da rodada. Nada aqui lê o banco — a tela
/// lê e grava com o MESMO <see cref="CampanhaService"/>.
///
/// GERAR e ENVIAR continuam passos separados de propósito: gerar é a parte automática (o
/// sistema descobre quem contatar, aplica a LGPD e não duplica ninguém); enviar é um clique por
/// paciente, porque o WhatsApp é o número da clínica e disparo em massa termina com ele
/// bloqueado.
/// </summary>
public static class CampanhasNaTela
{
    private static readonly CultureInfo PtBr = CultureInfo.GetCultureInfo("pt-BR");
    private const string Travessao = "—";

    public const string TipoTodos = "Todos";
    public const string TipoConfirmacao = "Confirmação";
    public const string TipoNps = "NPS";
    public const string TipoRecall = "Recall";

    public const string SituacaoAEnviar = "A enviar";
    public const string SituacaoTodos = "Todos";
    public const string SituacaoEnviados = "Enviados";
    public const string SituacaoRespondidos = "Respondidos";

    public const string JanelaSemanaQueVem = "Semana que vem";
    public const string JanelaUltimos30Dias = "Últimos 30 dias";
    public const string JanelaUltimos3Meses = "Últimos 3 meses";

    public static IReadOnlyList<string> Tipos { get; } = [TipoTodos, TipoConfirmacao, TipoNps, TipoRecall];
    public static IReadOnlyList<string> Situacoes { get; } = [SituacaoAEnviar, SituacaoTodos, SituacaoEnviados, SituacaoRespondidos];

    /// <summary>
    /// Janelas de tempo da fila. NÃO usa o período das outras telas da direção de propósito:
    /// lá o período termina hoje, porque indicador é sobre o que já aconteceu. Aqui metade do
    /// trabalho está no FUTURO — a confirmação da sessão de amanhã é o item mais importante da
    /// tela, e um período que acaba hoje a esconderia.
    /// </summary>
    public static IReadOnlyList<string> Janelas { get; } = [JanelaSemanaQueVem, JanelaUltimos30Dias, JanelaUltimos3Meses];

    /// <summary>Intervalo da janela escolhida. Sempre inclui alguns dias à frente.</summary>
    public static (DateOnly Inicio, DateOnly Fim) Intervalo(string janela, DateOnly hoje) => janela switch
    {
        JanelaUltimos30Dias => (hoje.AddDays(-30), hoje.AddDays(7)),
        JanelaUltimos3Meses => (hoje.AddDays(-90), hoje.AddDays(7)),
        // O padrão olha para frente: é a fila de trabalho, não o histórico.
        _ => (hoje.AddDays(-7), hoje.AddDays(7))
    };

    /// <summary>"11/10 a 25/10" — o intervalo que a fila está mostrando, no cabeçalho.</summary>
    public static string RotularIntervalo((DateOnly Inicio, DateOnly Fim) intervalo)
        => $"{intervalo.Inicio.ToString("dd/MM", PtBr)} a {intervalo.Fim.ToString("dd/MM", PtBr)}";

    public static TipoContato? TipoFiltro(string tipo) => tipo switch
    {
        TipoConfirmacao => TipoContato.ConfirmacaoSessao,
        TipoNps => TipoContato.Nps,
        TipoRecall => TipoContato.Recall,
        _ => null
    };

    public static StatusContato? StatusFiltro(string situacao) => situacao switch
    {
        SituacaoAEnviar => StatusContato.Pendente,
        SituacaoEnviados => StatusContato.Enviado,
        SituacaoRespondidos => StatusContato.Respondido,
        _ => null
    };

    public static LinhaDeCampanha Linha(ContatoCampanha c, DateOnly hoje)
    {
        var nome = c.Paciente?.Nome ?? "(paciente removido)";
        var fone = c.Paciente?.Telefone;

        return new LinhaDeCampanha(
            Id: c.Id,
            Paciente: nome,
            Telefone: string.IsNullOrWhiteSpace(fone) ? "sem telefone" : Telefone.Formatar(fone),
            Campanha: CampanhaService.Rotular(c.Tipo),
            Referencia: c.Referencia.ToString("dd/MM/yyyy", PtBr),
            Situacao: CampanhaService.Rotular(c.Status),
            Status: c.Status,
            Nota: c.Nota is { } n && c.Classe is { } classe ? $"{n} · {CampanhaService.Rotular(classe)}" : Travessao,
            Comentario: c.Comentario ?? string.Empty,
            EhNps: c.Tipo == TipoContato.Nps,
            TemTelefone: Telefone.Normalizar(fone).Length is >= 10 and <= 13,
            PodeAgir: !c.Encerrado,
            NomeCru: nome,
            TelefoneCru: fone ?? string.Empty,
            TipoCru: c.Tipo,
            QuandoCru: c.Agendamento?.DataHora,
            DiasSemVir: hoje.DayNumber - c.Referencia.DayNumber);
    }

    /// <summary>A mensagem pronta de cada tipo — o TEXTO mora em <see cref="MensagensDeContato"/>.</summary>
    public static string Mensagem(LinhaDeCampanha linha, DateOnly hoje) => linha.TipoCru switch
    {
        TipoContato.ConfirmacaoSessao => MensagensDeContato.ConfirmacaoDeSessao(
            linha.NomeCru, linha.QuandoCru ?? hoje.ToDateTime(TimeOnly.MinValue), hoje),
        TipoContato.Nps => MensagensDeContato.PesquisaDeSatisfacao(linha.NomeCru),
        _ => MensagensDeContato.Recall(linha.NomeCru, linha.DiasSemVir)
    };

    /// <summary>
    /// O resultado da rodada: quantos foram gerados E quantos ficaram de fora, e por quê.
    /// Campanha que filtra em silêncio parece quebrada.
    /// </summary>
    public static string ResultadoDaRodada(ResultadoCampanha r)
    {
        var partes = new List<string> { $"{r.Gerados} gerado(s)" };
        if (r.JaExistiam > 0) partes.Add($"{r.JaExistiam} já existia(m)");
        if (r.SemConsentimento > 0) partes.Add($"{r.SemConsentimento} sem consentimento de comunicação (LGPD)");
        if (r.SemTelefone > 0) partes.Add($"{r.SemTelefone} sem telefone no cadastro");
        return $"{CampanhaService.Rotular(r.Tipo)}: {string.Join(" · ", partes)}.";
    }

    /// <summary>
    /// "9 adorei o atendimento" → nota 9 e o comentário. A nota é o primeiro termo; o resto,
    /// depois de um espaço, é o comentário. Fora de 0 a 10 é recusado com a frase da tela.
    /// </summary>
    public static NotaDigitada LerNota(string texto)
    {
        var partes = (texto ?? string.Empty).Trim().Split(' ', 2);
        if (!int.TryParse(partes[0], NumberStyles.Integer, PtBr, out var nota) || !ContatoCampanha.NotaValida(nota))
            return new NotaDigitada(null, null, "A nota precisa ser um número de 0 a 10.");
        var comentario = partes.Length > 1 && !string.IsNullOrWhiteSpace(partes[1]) ? partes[1].Trim() : null;
        return new NotaDigitada(nota, comentario, null);
    }

    /// <summary>
    /// O vazio diz qual recorte está vazio: "nada a enviar" com contatos enviados na janela é
    /// outra situação que "nenhum contato na janela".
    /// </summary>
    public static (string Titulo, string Descricao) Vazio(string tipo, string situacao)
        => tipo == TipoTodos && situacao == SituacaoTodos
            ? ("Nenhuma mensagem nesta janela",
               "Gere uma rodada acima para começar. Quem não consentiu aparece CONTADO no resultado da rodada, não sumindo.")
            : ("Nada nesta fila",
               $"Nenhum contato de {(tipo == TipoTodos ? "qualquer campanha" : tipo)} em \"{situacao}\" nesta janela. Troque o tipo, a situação ou a janela, ou gere uma rodada acima.");
}
