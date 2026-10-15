using System.Globalization;
using Clinica.Domain.Entities;

namespace Clinica.Application.Modelos;

/// <summary>Em que ponto está a confirmação de UMA sessão — o placar da tela.</summary>
public enum SituacaoConfirmacao
{
    NaoAvisado,
    AvisadoSemResposta,
    Confirmou,
    PediuRemarcar,
    Dispensado
}

/// <summary>
/// As regras da rodada de confirmação que a tela AFIRMA (out/2026, mockup aprovado): em
/// que ponto está cada sessão, o placar dos quatro cartões e o título do dia. Moram aqui, e
/// não no ViewModel, pela regra da casa — o que a tela afirma precisa morar onde o
/// <c>dotnet test</c> alcança.
/// </summary>
public static class RodadaDeConfirmacao
{
    /// <summary>
    /// O que se grava como resposta quando o paciente pede para remarcar. É texto no
    /// <see cref="ContatoCampanha.Comentario"/>, e não status novo: valor novo de enum é o
    /// que o binário anterior não lê (parcela 67), e os cinco apps dividem o banco.
    /// </summary>
    public const string RespostaRemarcar = "pediu para remarcar";

    public static SituacaoConfirmacao Classificar(StatusContato status, DateTime? enviadoEm, string? comentario) => status switch
    {
        StatusContato.Dispensado => SituacaoConfirmacao.Dispensado,
        StatusContato.Respondido when string.Equals(comentario?.Trim(), RespostaRemarcar, StringComparison.OrdinalIgnoreCase)
            => SituacaoConfirmacao.PediuRemarcar,
        StatusContato.Respondido => SituacaoConfirmacao.Confirmou,
        _ when enviadoEm is not null => SituacaoConfirmacao.AvisadoSemResposta,
        _ => SituacaoConfirmacao.NaoAvisado
    };

    /// <summary>
    /// Fração avisada: quem já recebeu a mensagem (respondeu ou não). Dispensado não conta
    /// na base — saiu da rodada por decisão de alguém, e contá-lo como "não avisado" faria
    /// a barra nunca fechar.
    /// </summary>
    public static (int Base, int Avisados, int Percentual) Progresso(IEnumerable<SituacaoConfirmacao> situacoes)
    {
        var lista = situacoes.Where(s => s != SituacaoConfirmacao.Dispensado).ToList();
        var avisados = lista.Count(s => s != SituacaoConfirmacao.NaoAvisado);
        var percentual = lista.Count == 0 ? 0 : (int)Math.Round(100.0 * avisados / lista.Count, MidpointRounding.AwayFromZero);
        return (lista.Count, avisados, percentual);
    }

    private static readonly CultureInfo PtBr = CultureInfo.GetCultureInfo("pt-BR");

    /// <summary>"Amanhã · sex, 16/10" — o dia da rodada como a recepcionista pensa nele.</summary>
    public static string TituloDoDia(DateOnly dia, DateOnly hoje)
    {
        // Abreviação escrita à mão: o "ddd" do pt-BR do .NET devolve "sex." com ponto.
        var semana = dia.DayOfWeek switch
        {
            DayOfWeek.Monday => "seg", DayOfWeek.Tuesday => "ter", DayOfWeek.Wednesday => "qua",
            DayOfWeek.Thursday => "qui", DayOfWeek.Friday => "sex", DayOfWeek.Saturday => "sáb", _ => "dom"
        };
        var rotulo = $"{semana}, {dia.ToString("dd/MM", PtBr)}";
        var relativo = (dia.DayNumber - hoje.DayNumber) switch
        {
            0 => "Hoje",
            1 => "Amanhã",
            -1 => "Ontem",
            _ => null
        };
        return relativo is null ? rotulo : $"{relativo} · {rotulo}";
    }

    /// <summary>A frase do canal no "aviso" de cada linha.</summary>
    public static string Canal(CanalContato canal) => canal switch
    {
        CanalContato.Email => "E-mail enviado",
        CanalContato.Telefone => "Ligação feita",
        CanalContato.Presencial => "Avisado no balcão",
        _ => "WhatsApp enviado"
    };
}
