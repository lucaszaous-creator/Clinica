using System.Globalization;
using Clinica.Application.Servicos;
using Clinica.Domain;
using Clinica.Domain.Entities;
using Clinica.Domain.Regras;

namespace Clinica.Application.Modelos;

/// <summary>
/// O que as abas ADMINISTRATIVAS da ficha (Blazor, out/2026 — migradas do
/// <c>FichaPacienteViewModel</c> WPF: agenda e pendências, convênio, relacionamento, privacidade
/// e termos) AFIRMAM. Pura e com teste, pela regra da casa: o que a tela diz mora onde o
/// <c>dotnet test</c> alcança. As frases são as da ficha WPF, para as duas portas dizerem o
/// mesmo enquanto as duas existirem.
/// </summary>
public static class FichaAdministrativaNaTela
{
    private static readonly CultureInfo PtBr = CultureInfo.GetCultureInfo("pt-BR");

    /// <summary>Quantos próximos horários a ficha mostra.</summary>
    public const int TetoDeProximosHorarios = 5;

    // ==================== Agenda e pendências ====================

    /// <summary>Um alerta de elegibilidade; a DÍVIDA é a única linha que se resolve com dinheiro.</summary>
    public sealed record Alerta(string Descricao, bool EhVermelho, bool PodeReceber);

    public static Alerta AlertaDe(AlertaElegibilidade a) => new(
        a.Descricao,
        a.Urgencia == NivelUrgencia.Vermelho,
        a.Motivo == ImpedimentoElegibilidade.PacienteEmDebito);

    /// <summary>Um dos próximos horários: "qua 16/09 · 14:00–14:30" e "Acupuntura · Dra. Ana · Sala 2".</summary>
    public sealed record ProximoHorario(int AgendamentoId, DateOnly Dia, string Rotulo, string Contexto, bool Encaixe);

    public static ProximoHorario ProximoDe(Agendamento a)
    {
        // Nome do CATÁLOGO, nunca o enum (o defeito da parcela 41); só o que existe entra.
        var modalidade = CatalogoModalidades.NomeComEspecialidade(a.ModalidadeCodigo, a.ModalidadePrevista, a.EspecialidadeConsultaCodigo);
        var partes = new[] { modalidade, a.Profissional?.Rotulo, a.Sala?.Nome }.Where(p => !string.IsNullOrWhiteSpace(p));
        return new ProximoHorario(
            a.Id,
            DateOnly.FromDateTime(a.DataHora),
            $"{a.DataHora.ToString("ddd", PtBr).TrimEnd('.')} {a.DataHora:dd/MM} · {a.DataHora:HH:mm}–{a.FimPrevisto:HH:mm}",
            string.Join(" · ", partes),
            a.Encaixe);
    }

    // ==================== Convênio ====================

    /// <summary>A linha da consulta renovável do convênio — o que a ficha diz da validade.</summary>
    public static string ValidadeDaConsulta(StatusConsultaPaciente? consulta) =>
        consulta is null ? "Validade não disponível."
        : !consulta.UsaConsulta ? "Este convênio não usa consulta renovável."
        : consulta.Vencimento is null ? "Nenhuma validade de consulta registrada."
        : $"Validade da consulta do convênio: {consulta.Vencimento.Value.ToString("dd/MM/yyyy", PtBr)}.";

    /// <summary>"Senha 123 · emitida 01/09/2026 · válida até 30/11/2026".</summary>
    public static string DescreverAutorizacao(AutorizacaoSessoes a)
        => $"{a.Numero ?? "(sem número)"} · emitida {a.DataEmissao.ToString("dd/MM/yyyy", PtBr)} · válida até {a.DataValidade.ToString("dd/MM/yyyy", PtBr)}"
           + (a.Encerrada ? " · encerrada" : string.Empty);

    // ==================== Relacionamento ====================

    /// <summary>Origem em branco é "ninguém perguntou" — e a ficha diz isso com todas as letras.</summary>
    public static string Origem(Paciente p) => p.Origem switch
    {
        null => "Não perguntado",
        OrigemPaciente.Indicacao when !string.IsNullOrWhiteSpace(p.IndicadoPor) => $"Indicação de {p.IndicadoPor}",
        var o => RotulosEnum.De(o)
    };

    /// <summary>Um contato de campanha no histórico de CRM da ficha.</summary>
    public sealed record Contato(string Tipo, string Data, string Situacao, string Detalhe);

    public static Contato ContatoDe(ContatoCampanha c) => new(
        c.Tipo switch
        {
            TipoContato.ConfirmacaoSessao => "Confirmação de sessão",
            TipoContato.Nps => "Pesquisa de satisfação (NPS)",
            TipoContato.Recall => "Recall",
            var t => RotulosEnum.De(t)
        },
        c.Referencia.ToString("dd/MM/yyyy", PtBr),
        // Rótulo, nunca o enum cru ("Respondido" sai igual; "NaoEnviado" não).
        RotulosEnum.De(c.Status),
        // A nota do NPS é o que a direção quer ver de relance; nos outros, quem disparou.
        c.Nota is { } nota ? $"nota {nota}/10"
        : c.EnviadoEm is { } enviado ? $"enviado em {enviado.ToString("dd/MM/yyyy", PtBr)} por {c.EnviadoPor ?? "?"}"
        : "ainda não enviado");

    // ==================== Privacidade ====================

    /// <summary>Uma finalidade LGPD e o que vale para ela hoje.</summary>
    public sealed record Consentimento(FinalidadeConsentimento Finalidade, string Rotulo, string Situacao, bool Vigente, int? RegistroId);

    public static IReadOnlyList<Consentimento> Consentimentos(IReadOnlyDictionary<FinalidadeConsentimento, ConsentimentoLgpd> situacao)
        => ConsentimentoService.Finalidades.Select(f =>
        {
            situacao.TryGetValue(f, out var atual);
            return new Consentimento(f, ConsentimentoService.Rotular(f), DescreverConsentimento(atual), atual?.Vigente == true, atual?.Id);
        }).ToList();

    /// <summary>"Concedido em 12/03 · termo 2026/0007" — a procedência é o que responde "onde está a prova?".</summary>
    public static string DescreverConsentimento(ConsentimentoLgpd? registro)
    {
        if (registro is null) return "Nunca perguntado";
        var origem = string.IsNullOrWhiteSpace(registro.VersaoTermo) ? string.Empty : $" · termo {registro.VersaoTermo}";
        return registro switch
        {
            { RevogadoEm: { } revogado } => $"Revogado em {revogado.ToString("dd/MM/yyyy", PtBr)}{origem}",
            { Concedido: true } r => $"Concedido em {r.RegistradoEm.ToString("dd/MM/yyyy", PtBr)}{origem}",
            var r => $"Recusado em {r.RegistradoEm.ToString("dd/MM/yyyy", PtBr)}{origem}"
        };
    }

    // ==================== Termos do dia ====================

    /// <summary>
    /// Um termo que o procedimento de HOJE exige. As três respostas são diferentes de propósito:
    /// "falta assinar" é tarefa, "recusou" é decisão tomada, e "assinado com declaração negada"
    /// é o caso GRAVE — fundi-los faria "resolvido" cobrir um problema clínico.
    /// </summary>
    public sealed record Termo(
        int ModeloId, int? DocumentoId, string Nome, string Procedimento, bool Pendente, bool AguardaConferencia,
        string Situacao, bool EhVermelho, int? ProfissionalId, int? AgendamentoId);

    public static Termo TermoDe(SituacaoTermo s) => new(
        s.ModeloId, s.DocumentoId, s.NomeDoTermo,
        CatalogoModalidades.Nome(s.Modalidade.ToString()),
        s.Pendente, s.AssinaturaRemotaAguardaConferencia,
        SituacaoDoTermo(s),
        // Vermelho: ninguém assinou, ou negou uma declaração. O assinado no celular fica de fora.
        (s.Pendente && !s.AssinaturaRemotaAguardaConferencia) || s.DeclaracoesNegadas.Count > 0,
        s.ProfissionalId, s.AgendamentoId);

    public static string SituacaoDoTermo(SituacaoTermo s)
    {
        if (s.Recusado)
            return string.IsNullOrWhiteSpace(s.MotivoRecusa) ? "Paciente recusou assinar" : $"Paciente recusou: {s.MotivoRecusa}";
        if (s.DeclaracoesNegadas.Count > 0)
            return $"Assinado — respondeu NÃO em: {string.Join("; ", s.DeclaracoesNegadas)}";
        if (s.Assinado) return "Assinado hoje";
        // "Falta o paciente assinar" sobre quem JÁ ASSINOU no celular mandaria reenviar o link.
        return s.AssinaturaRemotaAguardaConferencia ? "Assinado no celular — falta conferir e concluir" : "Falta o paciente assinar";
    }

    /// <summary>O resumo da seção de termos — os dois estados contados SEPARADO e direto, nunca por subtração.</summary>
    public static string ResumoTermos(IReadOnlyList<Termo> termos, bool naoVerificado)
    {
        if (naoVerificado)
            return "Não foi possível conferir os termos deste paciente agora. Recarregue a ficha antes de liberar o procedimento.";
        if (termos.Count == 0)
            return "Nenhum procedimento marcado para hoje exige termo assinado. Dá para colher agora mesmo assim — "
                   + "o termo vale a partir da assinatura, e o paciente está aqui.";

        var aConferir = termos.Count(t => t.AguardaConferencia);
        var faltam = termos.Count(t => t.Pendente && !t.AguardaConferencia);
        if (faltam == 0 && aConferir > 0)
            return aConferir == 1
                ? "1 termo já foi assinado no celular — falta conferir e concluir."
                : $"{aConferir} termos já foram assinados no celular — falta conferir e concluir.";

        var conferir = aConferir switch
        {
            0 => string.Empty,
            1 => " E 1 já assinado no celular, à espera de conferência.",
            _ => $" E {aConferir} já assinados no celular, à espera de conferência."
        };
        return faltam switch
        {
            0 => "Os termos de hoje já foram resolvidos.",
            1 => "Falta 1 termo assinado para o procedimento de hoje." + conferir,
            _ => $"Faltam {faltam} termos assinados para os procedimentos de hoje." + conferir
        };
    }
}
