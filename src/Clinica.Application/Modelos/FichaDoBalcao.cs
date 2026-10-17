using System.Globalization;
using Clinica.Domain;
using Clinica.Domain.Entities;
using Clinica.Domain.Regras;

namespace Clinica.Application.Modelos;

/// <summary>Um acontecimento na "Linha do tempo" da ficha do balcão.</summary>
/// <param name="Tipo">"sessao", "contato" ou "documento" — a cor do ponto.</param>
public sealed record ItemLinhaDoTempoBalcao(DateTime Quando, bool TemHora, string Titulo, string Detalhe, string Tipo)
{
    public string QuandoTexto => TemHora ? Quando.ToString("dd/MM HH:mm") : Quando.ToString("dd/MM");
}

/// <summary>Uma sessão que aconteceu, na aba "Agenda e sessões".</summary>
public sealed record SessaoDaFicha(DateOnly Data, string Atendimento, string Guias);

/// <summary>
/// O que a FICHA DO BALCÃO afirma (out/2026, mockup aprovado "4 · Ficha"). Pura, pela
/// regra da casa: o que a tela afirma mora onde o <c>dotnet test</c> alcança.
///
/// - **Sessão é o que ACONTECEU** (<c>RealizadoEm</c>, não estornado): desde a parcela 70
///   o atendimento nasce na marcação, e contar linhas de Atendimento somaria a semana que
///   vem — a ficha diria "24 sessões" a quem teve 18.
/// - **A linha do tempo é ADMINISTRATIVA**: sessão, contato e papel emitido. A evolução
///   clínica não entra — é dado de saúde, e quem a lê abre o prontuário.
/// </summary>
public static class FichaDoBalcao
{
    private static readonly CultureInfo PtBr = CultureInfo.GetCultureInfo("pt-BR");

    private static IEnumerable<Atendimento> Realizadas(IEnumerable<Atendimento> atendimentos)
        => atendimentos.Where(a => a.RealizadoEm is not null && !a.Estornado);

    /// <summary>"2023 · 46 sessões" — desde quando se trata aqui, e quanto.</summary>
    public static string PacienteDesde(IEnumerable<Atendimento> atendimentos)
    {
        var feitas = Realizadas(atendimentos).ToList();
        if (feitas.Count == 0) return "nenhuma sessão realizada ainda";
        var desde = feitas.Min(a => a.Data).Year;
        return $"{desde} · {feitas.Count} {(feitas.Count == 1 ? "sessão" : "sessões")}";
    }

    /// <summary>O bloquinho de data do próximo horário: ("HOJE", "15") ou ("QUI", "22").</summary>
    public static (string Sigla, string Numero) DiaDoHorario(DateOnly dia, DateOnly hoje)
    {
        var sigla = dia == hoje ? "HOJE" : dia.DayOfWeek switch
        {
            DayOfWeek.Monday => "SEG", DayOfWeek.Tuesday => "TER", DayOfWeek.Wednesday => "QUA",
            DayOfWeek.Thursday => "QUI", DayOfWeek.Friday => "SEX", DayOfWeek.Saturday => "SÁB", _ => "DOM"
        };
        return (sigla, dia.Day.ToString(CultureInfo.InvariantCulture));
    }

    /// <summary>As sessões realizadas, da mais recente, com a situação das guias.</summary>
    public static IReadOnlyList<SessaoDaFicha> Sessoes(IEnumerable<Atendimento> atendimentos)
        => Realizadas(atendimentos)
            .OrderByDescending(a => a.Data).ThenByDescending(a => a.Id)
            .Select(a => new SessaoDaFicha(a.Data,
                CatalogoModalidades.NomeComEspecialidade(a.ModalidadeCodigo, a.Modalidade, a.EspecialidadeConsultaCodigo),
                Guias(a.Codigos)))
            .ToList();

    /// <summary>"guias 1 e 2 baixadas" · "1 de 2 guias baixadas" · "sem guia (particular)".</summary>
    public static string Guias(IEnumerable<CodigoFaturamento> codigos)
    {
        var faturaveis = codigos.Where(c => c.Status != StatusCodigo.NaoAplicavel).ToList();
        if (faturaveis.Count == 0) return "sem guia a faturar";
        var baixadas = faturaveis.Count(c => c.DataBaixa is not null);
        if (baixadas == faturaveis.Count)
            return faturaveis.Count == 1 ? "guia baixada" : faturaveis.Count == 2 ? "guias 1 e 2 baixadas" : $"{baixadas} guias baixadas";
        return baixadas == 0
            ? (faturaveis.Count == 1 ? "guia a baixar" : $"{faturaveis.Count} guias a baixar")
            : $"{baixadas} de {faturaveis.Count} guias baixadas";
    }

    /// <summary>
    /// A linha do tempo: sessões, contatos e documentos, do mais recente, cortada em
    /// <paramref name="limite"/>. Sessão vem sem hora (o atendimento guarda só o dia).
    /// </summary>
    public static IReadOnlyList<ItemLinhaDoTempoBalcao> LinhaDoTempo(
        IEnumerable<Atendimento> atendimentos,
        IEnumerable<ContatoCampanha> contatos,
        IEnumerable<ItemLinhaDoTempoBalcao> documentos,
        int limite = 8)
    {
        var itens = new List<ItemLinhaDoTempoBalcao>();
        foreach (var a in Realizadas(atendimentos))
            itens.Add(new ItemLinhaDoTempoBalcao(a.Data.ToDateTime(TimeOnly.MinValue), false,
                $"Sessão de {CatalogoModalidades.NomeComEspecialidade(a.ModalidadeCodigo, a.Modalidade, a.EspecialidadeConsultaCodigo).ToLower(PtBr)}",
                Guias(a.Codigos), "sessao"));

        foreach (var c in contatos.Where(c => c.EnviadoEm is not null || c.RespondidoEm is not null))
        {
            var (titulo, quando) = c.Tipo switch
            {
                TipoContato.ConfirmacaoSessao when RodadaDeConfirmacao.Classificar(c.Status, c.EnviadoEm, c.Comentario) == SituacaoConfirmacao.PediuRemarcar
                    => ($"Avisou que não vem à sessão de {c.Referencia:dd/MM}", c.RespondidoEm ?? c.EnviadoEm!.Value),
                TipoContato.ConfirmacaoSessao when c.Status == StatusContato.Respondido
                    => ($"Confirmou a sessão de {c.Referencia:dd/MM}", c.RespondidoEm ?? c.EnviadoEm!.Value),
                TipoContato.ConfirmacaoSessao => ($"Avisado da sessão de {c.Referencia:dd/MM}", c.EnviadoEm ?? c.RespondidoEm!.Value),
                TipoContato.Nps when c.Nota is { } nota => ($"Respondeu a pesquisa: nota {nota}", c.RespondidoEm ?? c.EnviadoEm!.Value),
                TipoContato.Nps => ("Pesquisa de satisfação enviada", c.EnviadoEm ?? c.RespondidoEm!.Value),
                _ => ("Contato de recall", c.EnviadoEm ?? c.RespondidoEm!.Value)
            };
            var canal = c.Canal switch
            {
                CanalContato.Email => "E-mail",
                CanalContato.Telefone => "Ligação",
                CanalContato.Presencial => "No balcão",
                CanalContato.Sms => "SMS",
                _ => "WhatsApp"
            };
            itens.Add(new ItemLinhaDoTempoBalcao(quando, true, titulo,
                string.IsNullOrWhiteSpace(c.EnviadoPor) ? canal : $"{canal} · {c.EnviadoPor}", "contato"));
        }

        itens.AddRange(documentos);
        return itens.OrderByDescending(i => i.Quando).Take(limite).ToList();
    }
}
