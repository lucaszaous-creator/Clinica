using Clinica.Domain;

namespace Clinica.Application.Modelos;

/// <summary>Situação da consulta (renovável) de um paciente, exibida na aba de Consultas.</summary>
public sealed record StatusConsultaPaciente(
    int PacienteId,
    string PacienteNome,
    Convenio Convenio,
    bool UsaConsulta,            // convênio possui consulta renovável (ex.: Petrobras não usa)
    DateOnly? UltimaEmissao,
    DateOnly? Vencimento,
    int? DiasParaVencer,         // negativo = já venceu
    bool Vencida,
    bool PrecisaRenovar,         // vencida, sem consulta ainda, ou a vencer dentro da janela de alerta
    NivelUrgencia Urgencia)
{
    /// <summary>
    /// Código do convênio no catálogo. <see cref="Convenio"/> é a FAMÍLIA de REGRA — duas
    /// operadoras podem compartilhá-la —, então é só por ele que se chega ao nome que a
    /// clínica cadastrou. Resolver pela família faria toda operadora personalizada
    /// aparecer na tela como "Personalizado".
    /// </summary>
    public string? ConvenioCodigo { get; init; }

    /// <summary>
    /// Há consulta EMITIDA pedindo renovação (vencida ou a vencer dentro da janela).
    ///
    /// Diferente de <see cref="PrecisaRenovar"/>, que também é verdadeiro para quem nunca
    /// teve consulta nenhuma. A distinção existe por causa de onde este dado passou a ser
    /// lido: na aba de Consultas, "nunca emitiu" é uma linha numa lista que a secretária
    /// abriu para tratar consultas, e ali ela é informação. Na AGENDA, ela apareceria em
    /// todo paciente de convênio que a clínica ainda não tinha cadastrado — um selo que
    /// acende para a base inteira no primeiro dia, e alerta que dispara para todo mundo é
    /// alerta que ninguém lê. É a mesma escolha que o painel de pendências já fazia
    /// (<c>PendenciaService.ConsultasAVencerAsync</c> pula quem não emitiu nenhuma).
    /// </summary>
    public bool ARenovar => PrecisaRenovar && UltimaEmissao is not null;

    /// <summary>
    /// A frase que a agenda e o balcão mostram. Mora aqui, e não em cada tela, para o
    /// faturamento e a suíte não escreverem duas versões da mesma cobrança.
    /// Nula quando não há o que renovar.
    /// </summary>
    public string? AvisoRenovacao => !ARenovar ? null : Vencida
        ? $"Consulta vencida em {Vencimento:dd/MM/yyyy} — renove antes de faturar."
        : DiasParaVencer == 0
            ? $"A consulta vence hoje ({Vencimento:dd/MM/yyyy}) — dá para renovar agora."
            : $"A consulta vence em {DiasParaVencer} dia(s) ({Vencimento:dd/MM/yyyy}) — dá para renovar agora.";

    /// <summary>Selo curto do cartão da agenda, onde não cabe a frase inteira.</summary>
    public string? SeloRenovacao => !ARenovar ? null : Vencida
        ? "Consulta vencida"
        : "Consulta a renovar";
}
