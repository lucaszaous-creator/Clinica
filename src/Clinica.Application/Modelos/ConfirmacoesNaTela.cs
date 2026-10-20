using Clinica.Domain;
using Clinica.Domain.Entities;
using Clinica.Domain.Regras;

namespace Clinica.Application.Modelos;

/// <summary>
/// UMA sessão da rodada de confirmação, como a tela de Confirmações (Blazor, out/2026) a
/// mostra. É o que o <c>LinhaConfirmacao</c> do ViewModel WPF montava — subiu para cá pela
/// regra da casa: o que a tela AFIRMA ("confirmou pelo SMS", "avisou que não vai — ligue")
/// precisa morar onde o <c>dotnet test</c> alcança.
/// </summary>
public sealed record SessaoDaRodada(
    int ContatoId,
    int? AgendamentoId,
    int PacienteId,
    string Paciente,
    string NomeCru,
    DateTime Quando,
    string? Telefone,
    string? Email,
    string Profissional,
    string Atendimento,
    SituacaoConfirmacao Estado,
    CanalContato Canal,
    bool Enviado,
    string Aviso,
    string Detalhe,
    string? Comentario)
{
    public bool TemTelefone => !string.IsNullOrWhiteSpace(Telefone);

    public bool NaoAvisado => Estado == SituacaoConfirmacao.NaoAvisado;

    /// <summary>Quem já respondeu (ou foi dispensado) não precisa dos botões: a resposta fica escrita.</summary>
    public bool Respondeu => Estado is SituacaoConfirmacao.Confirmou or SituacaoConfirmacao.PediuRemarcar or SituacaoConfirmacao.Dispensado;

    /// <summary>Quem já recebeu a mensagem é LEMBRADO; quem não recebeu é AVISADO.</summary>
    public string RotuloEnviar => NaoAvisado ? "Avisar" : "Lembrar de novo";

    /// <summary>O aviso saiu pelo SMS automático.</summary>
    public bool PeloSms => Enviado && Canal == CanalContato.Sms;

    /// <summary>A resposta chegou pelo LINK do SMS (o paciente clicou), não pela recepção.</summary>
    public bool RespondeuPeloLink =>
        string.Equals(Comentario?.Trim(), RodadaDeConfirmacao.RespostaConfirmouPeloLink, StringComparison.OrdinalIgnoreCase)
        || string.Equals(Comentario?.Trim(), RodadaDeConfirmacao.RespostaNaoVaiPeloLink, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// O que a recepcionista precisa FAZER com esta linha, por extenso — só quando há algo.
    /// "Não vou" pelo link NÃO cancela o horário (a regra do <c>LembreteSmsService</c>):
    /// quem libera a vaga é a recepção, que liga e remarca. Por isso a frase manda ligar.
    /// </summary>
    public string? Destaque => Estado == SituacaoConfirmacao.PediuRemarcar
        ? (RespondeuPeloLink
            ? "Avisou pelo SMS que não vai — ligue, remarque e libere o horário."
            : "Pediu para remarcar — ligue e remarque o horário.")
        : null;

    /// <summary>
    /// O selo da resposta, por extenso — "Confirmou pelo SMS" e "Confirmou" (no balcão) são
    /// fatos diferentes para quem confere; o dispensado não é resposta, é decisão da recepção.
    /// </summary>
    public string RespostaTexto => Estado switch
    {
        SituacaoConfirmacao.Confirmou => RespondeuPeloLink ? "Confirmou pelo SMS" : "Confirmou",
        SituacaoConfirmacao.PediuRemarcar => RespondeuPeloLink ? "Avisou pelo SMS que não vai" : "Pediu para remarcar",
        SituacaoConfirmacao.Dispensado => "Dispensado",
        _ => string.Empty
    };

    /// <summary>"Ana Souza" → "AS" — o avatar da linha.</summary>
    public string Iniciais
    {
        get
        {
            var partes = NomeCru.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            return partes.Length switch
            {
                0 => "?",
                1 => partes[0][..1].ToUpperInvariant(),
                _ => (partes[0][..1] + partes[^1][..1]).ToUpperInvariant()
            };
        }
    }
}

/// <summary>O placar dos quatro cartões — sempre da rodada INTEIRA, nunca do recorte.</summary>
public sealed record PlacarDaRodada(
    int Confirmaram, int SemResposta, int NaoAvisados, int PediramRemarcar,
    int Total, int Percentual, int PorSms, int RespostasPeloLink);

/// <summary>
/// As regras da tela de Confirmações (out/2026) que não cabiam no
/// <see cref="RodadaDeConfirmacao"/> (que é do backend do SMS e de várias telas): a linha,
/// o placar, o resumo do rodapé e o recorte do cartão escolhido.
/// </summary>
public static class ConfirmacoesNaTela
{
    /// <summary>Monta as linhas da rodada, na ordem do horário.</summary>
    public static IReadOnlyList<SessaoDaRodada> Montar(IEnumerable<ContatoCampanha> contatos, DateOnly dia)
        => contatos
            .OrderBy(c => c.Agendamento?.DataHora ?? dia.ToDateTime(TimeOnly.MinValue))
            .ThenBy(c => c.Id)
            .Select(c => Linha(c, dia))
            .ToList();

    public static SessaoDaRodada Linha(ContatoCampanha c, DateOnly dia)
    {
        var quando = c.Agendamento?.DataHora ?? dia.ToDateTime(TimeOnly.MinValue);
        var enviado = c.EnviadoEm is not null;
        return new SessaoDaRodada(
            c.Id,
            c.AgendamentoId,
            c.PacienteId,
            c.Paciente?.Nome ?? "(paciente removido)",
            c.Paciente?.Nome ?? string.Empty,
            quando,
            c.Paciente?.Telefone,
            c.Paciente?.Email,
            c.Agendamento?.Profissional?.Rotulo ?? "sem profissional",
            c.Agendamento is { } a
                ? CatalogoModalidades.NomeComEspecialidade(a.ModalidadeCodigo, a.ModalidadePrevista, a.EspecialidadeConsultaCodigo)
                : string.Empty,
            RodadaDeConfirmacao.Classificar(c.Status, c.EnviadoEm, c.Comentario),
            c.Canal,
            enviado,
            enviado ? RodadaDeConfirmacao.Canal(c.Canal) : "Não avisado",
            Detalhe(c),
            c.Comentario);
    }

    /// <summary>
    /// A frase sob o aviso: QUANDO saiu (e por quem), quando respondeu, ou — no dispensado — o
    /// MOTIVO (o serviço o exige). O QUE respondeu é o selo da coluna ao lado
    /// (<see cref="SessaoDaRodada.RespostaTexto"/>): repetir aqui seria dizer a mesma coisa duas vezes.
    /// </summary>
    public static string Detalhe(ContatoCampanha c) => c.Status switch
    {
        StatusContato.Respondido => c.RespondidoEm is { } r ? $"respondeu em {r.ToString("dd/MM HH:mm", PtBr)}" : "respondeu",
        StatusContato.Dispensado => string.IsNullOrWhiteSpace(c.Comentario) ? "dispensado" : $"dispensado — {c.Comentario.Trim()}",
        _ => c.EnviadoEm is { } quando
            ? quando.ToString("dd/MM HH:mm", PtBr)
              + (string.IsNullOrWhiteSpace(c.EnviadoPor) ? string.Empty : $" · por {c.EnviadoPor}")
            : "ainda não avisado"
    };

    public static PlacarDaRodada Placar(IReadOnlyCollection<SessaoDaRodada> linhas)
    {
        var (_, _, pct) = RodadaDeConfirmacao.Progresso(linhas.Select(l => l.Estado));
        return new PlacarDaRodada(
            linhas.Count(l => l.Estado == SituacaoConfirmacao.Confirmou),
            linhas.Count(l => l.Estado == SituacaoConfirmacao.AvisadoSemResposta),
            linhas.Count(l => l.Estado == SituacaoConfirmacao.NaoAvisado),
            linhas.Count(l => l.Estado == SituacaoConfirmacao.PediuRemarcar),
            linhas.Count,
            pct,
            linhas.Count(l => l.PeloSms),
            linhas.Count(l => l.RespondeuPeloLink));
    }

    /// <summary>
    /// O resumo do rodapé: quantas sessões, quantas o SMS já avisou e quantas estão sem
    /// telefone — o número que diz quem ainda precisa de um clique no WhatsApp.
    /// </summary>
    public static string Resumo(IReadOnlyCollection<SessaoDaRodada> linhas)
    {
        if (linhas.Count == 0) return "Nenhuma sessão gerada para este dia. Clique em \"Gerar rodada\".";
        var semTelefone = linhas.Count(l => !l.TemTelefone);
        var porSms = linhas.Count(l => l.PeloSms);
        return $"{linhas.Count} sessão(ões)"
               + (porSms > 0 ? $" · {porSms} avisada(s) por SMS" : string.Empty)
               + (semTelefone > 0 ? $" · {semTelefone} SEM TELEFONE no cadastro." : ".");
    }

    /// <summary>O recorte da tabela — o rodapé DIZ o que está à vista.</summary>
    public static IReadOnlyList<SessaoDaRodada> Recortar(IReadOnlyList<SessaoDaRodada> linhas, SituacaoConfirmacao? filtro)
        => filtro is null ? linhas : linhas.Where(l => l.Estado == filtro).ToList();

    public static string RecorteTexto(SituacaoConfirmacao? filtro, int quantos) => filtro switch
    {
        SituacaoConfirmacao.Confirmou => $"Mostrando: confirmaram ({quantos})",
        SituacaoConfirmacao.AvisadoSemResposta => $"Mostrando: avisados sem resposta ({quantos})",
        SituacaoConfirmacao.NaoAvisado => $"Mostrando: ainda não avisados ({quantos})",
        SituacaoConfirmacao.PediuRemarcar => $"Mostrando: pediram para remarcar ou não vêm ({quantos})",
        _ => $"Mostrando: todas as sessões ({quantos})"
    };

    /// <summary>
    /// O próximo a avisar pelo botão "Avisar os que faltam": o primeiro ainda não avisado
    /// que TEM telefone. Nulo quando não há — e a frase de <see cref="SemProximo"/> diz por quê.
    /// </summary>
    public static SessaoDaRodada? Proximo(IEnumerable<SessaoDaRodada> linhas)
        => linhas.FirstOrDefault(l => l.NaoAvisado && l.TemTelefone);

    public static string SemProximo(int naoAvisados) => naoAvisados > 0
        ? "Quem falta avisar está sem telefone no cadastro — ligue ou complete a ficha."
        : "Todas as sessões deste dia já foram avisadas.";

    /// <summary>A frase do "Gerar rodada": "nada a fazer" e "não rodou" precisam ser distinguíveis.</summary>
    public static string ResultadoDaGeracao(int gerados, int jaExistiam) => gerados > 0
        ? $"{gerados} sessão(ões) para avisar." + (jaExistiam > 0 ? $" {jaExistiam} já estavam na rodada." : string.Empty)
        : jaExistiam > 0
            ? $"Nada novo — as {jaExistiam} sessão(ões) do dia já estavam na rodada."
            : "Nenhuma sessão marcada para este dia.";

    private static readonly System.Globalization.CultureInfo PtBr = System.Globalization.CultureInfo.GetCultureInfo("pt-BR");

    private static bool Igual(string? a, string b) => string.Equals(a?.Trim(), b, StringComparison.OrdinalIgnoreCase);
}
