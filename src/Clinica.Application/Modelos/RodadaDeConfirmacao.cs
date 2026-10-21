using System.Globalization;
using Clinica.Domain.Entities;
using Clinica.Domain;

namespace Clinica.Application.Modelos;

/// <summary>Em que ponto está a confirmação de UMA sessão — o placar da tela.</summary>
public enum SituacaoConfirmacao
{
    NaoAvisado,
    AvisadoSemResposta,
    Confirmou,
    PediuRemarcar,
    Dispensado,

    /// <summary>
    /// O paciente pediu pelo WhatsApp para CANCELAR (out/2026). O horário segue marcado até a
    /// recepção aceitar o cancelamento na própria agenda. No fim do enum: valor novo no meio
    /// mudaria o número dos que já existem.
    /// </summary>
    PediuCancelar
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

    /// <summary>
    /// O que se grava quando o paciente responde pelo LINK do SMS que NÃO vai (out/2026).
    /// Classifica como <see cref="SituacaoConfirmacao.PediuRemarcar"/>: para o balcão os
    /// dois pedem o MESMO gesto — ligar, remarcar e liberar o horário —, e uma segunda
    /// situação com a mesma ação faria a recepcionista procurar a diferença que não há.
    /// </summary>
    public const string RespostaNaoVaiPeloLink = "avisou pelo SMS que não vai";

    /// <summary>O que se grava quando o paciente confirma pelo link do SMS.</summary>
    public const string RespostaConfirmouPeloLink = "confirmou pelo SMS";

    /// <summary>
    /// O que a ponte do CRM grava quando o paciente toca em "Cancelar" no WhatsApp e a clínica
    /// não cancela sozinha (<c>CRM_CANCEL_ENABLED=false</c>, out/2026). A recepção vê o pedido
    /// na agenda e aceita ali mesmo ("Aceitar cancelamento").
    /// </summary>
    public const string RespostaCancelar = "pediu para cancelar";

    /// <summary>
    /// O que se grava quando a recepção liga para quem pediu para cancelar (ou avisou que não
    /// vem) e o paciente MANTÉM o horário (out/2026 — a clínica quer tentar reverter antes de
    /// liberar a vaga). Classifica como <see cref="SituacaoConfirmacao.Confirmou"/>, e não
    /// é notícia do sino: quem confirmou foi a recepção, não o paciente.
    /// </summary>
    public const string RespostaManteveHorario = "manteve o horário após contato da recepção";

    /// <summary>
    /// O que a ponte do CRM grava quando o paciente confirma pelo WhatsApp. Era texto solto
    /// dentro do <c>ClinicAgendaBridge</c>; subiu para cá (out/2026) porque o sino passou a
    /// distinguir a confirmação do PACIENTE da que a recepção marcou
    /// (<see cref="AvisosDeConfirmacao.RespondeuOPaciente"/>) — e texto repetido à mão nos
    /// dois lados é o que um ajuste de redação separa sem quebrar nada.
    /// </summary>
    public const string RespostaConfirmouPeloWhatsApp =
        "Paciente confirmou intenção de comparecer pelo WhatsApp. Não representa presença ou atendimento realizado.";

    public static SituacaoConfirmacao Classificar(StatusContato status, DateTime? enviadoEm, string? comentario) => status switch
    {
        StatusContato.Dispensado => SituacaoConfirmacao.Dispensado,
        StatusContato.Respondido when string.Equals(comentario?.Trim(), RespostaRemarcar, StringComparison.OrdinalIgnoreCase)
                                   || string.Equals(comentario?.Trim(), RespostaNaoVaiPeloLink, StringComparison.OrdinalIgnoreCase)
            => SituacaoConfirmacao.PediuRemarcar,
        StatusContato.Respondido when string.Equals(comentario?.Trim(), RespostaCancelar, StringComparison.OrdinalIgnoreCase)
            => SituacaoConfirmacao.PediuCancelar,
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

    /// <summary>A pílula "não vem" das listas do dia — quem respondeu que não vai.</summary>
    public const string SeloNaoVem = "Não vem — remarcar";

    /// <summary>A pílula de quem pediu para cancelar — a recepção aceita na própria agenda.</summary>
    public const string SeloPediuCancelar = "Pediu para cancelar";

    /// <summary>A pílula de quem confirmou.</summary>
    public const string SeloConfirmou = "Confirmou";

    /// <summary>A pílula de quem foi avisado e não respondeu.</summary>
    public const string SeloSemResposta = "Não confirmou";

    /// <summary>
    /// O selo de UMA linha da agenda (lista do dia, grade, Início, quadro do médico): o que a
    /// rodada AFIRMOU, numa definição só (out/2026). Antes, cada tela trocava o status
    /// <c>Respondido</c> por "Confirmou" — e quem respondeu pelo SMS que NÃO vai aparecia
    /// como confirmado. Sem contato não há selo: acusar de "não confirmou" quem nunca foi
    /// avisado seria o selo mentindo (parcela 87).
    /// </summary>
    public static string SeloDaLista(RespostaDeConfirmacao? r) => r?.Situacao switch
    {
        SituacaoConfirmacao.Confirmou => SeloConfirmou,
        SituacaoConfirmacao.PediuRemarcar => SeloNaoVem,
        SituacaoConfirmacao.PediuCancelar => SeloPediuCancelar,
        SituacaoConfirmacao.AvisadoSemResposta => SeloSemResposta,
        _ => string.Empty
    };

    /// <summary>A frase do canal no "aviso" de cada linha.</summary>
    public static string Canal(CanalContato canal) => canal switch
    {
        CanalContato.Email => "E-mail enviado",
        CanalContato.Telefone => "Ligação feita",
        CanalContato.Presencial => "Avisado no balcão",
        CanalContato.Sms => "SMS enviado",
        _ => "WhatsApp enviado"
    };
}

/// <summary>
/// O contato de confirmação de UM horário, com o que é preciso para dizer em que ponto ele
/// está (<see cref="RodadaDeConfirmacao.Classificar"/>) e por qual canal saiu.
/// </summary>
public sealed record RespostaDeConfirmacao(
    StatusContato Status, DateTime? EnviadoEm, string? Comentario, CanalContato Canal)
{
    public SituacaoConfirmacao Situacao => RodadaDeConfirmacao.Classificar(Status, EnviadoEm, Comentario);
}

/// <summary>O mínimo de uma ficha para falar com a pessoa — a projeção da campanha de SMS.</summary>
public sealed record PacienteParaContato(int Id, string Nome, string? Telefone, string? ConvenioCodigo, Convenio ConvenioFamilia = default);
