using Clinica.Application.Servicos;
using Clinica.Domain;
using Clinica.Domain.Entities;

namespace Clinica.Application.Modelos;

/// <summary>Uma folha na fila da sala.</summary>
public sealed class LinhaSalaInfusao
{
    public required int PrescricaoId { get; init; }

    /// <summary>O paciente, para abrir a evolução de enfermagem (parcela 71).</summary>
    public required int PacienteId { get; init; }

    public required string Paciente { get; init; }
    public required string Numero { get; init; }
    public required string Hora { get; init; }
    public required string Prescritor { get; init; }
    public required string Progresso { get; init; }
    public required string Itens { get; init; }
    public required bool TemPendencia { get; init; }
    public required bool Encerrada { get; init; }
    public required bool Devolvida { get; init; }

    /// <summary>
    /// A folha encerrou e ainda deve a assinatura eletrônica da enfermagem.
    ///
    /// É o campo que faltava: a pendência existia no domínio
    /// (<c>PrescricaoInterna.AguardaAssinaturaDaExecucao</c>) e só era lida DENTRO da
    /// janela de uma folha já aberta — nenhuma lista, nenhum selo, nenhum contador. Para
    /// descobrir qual folha ficou sem assinar, a técnica teria de abrir uma por uma.
    /// </summary>
    public required bool AguardaAssinatura { get; init; }

    /// <summary>A assinatura está registrada, mas o documento principal está indisponível.</summary>
    public required bool RegistroPendente { get; init; }

    /// <summary>
    /// O dia da folha, escrito só quando NÃO é hoje. A fila mostra hoje; a folha de outro
    /// dia só entra quando deve assinatura, e sem a data ela se leria como sendo de hoje.
    /// </summary>
    public required string Dia { get; init; }

    public IReadOnlyList<EtapaInfusao> Etapas { get; init; } = [];
    public bool DeOutroDia => Dia.Length > 0;

    public static LinhaSalaInfusao De(PrescricaoInterna p, DateOnly hoje) => new()
    {
        PrescricaoId = p.Id,
        Etapas = EtapasInfusao.Da(p),
        PacienteId = p.PacienteId,
        Paciente = p.Paciente?.Nome ?? "—",
        Numero = p.Numero,
        Dia = p.Data == hoje ? string.Empty : p.Data.ToString("dd/MM"),
        Devolvida = p.DevolvidaEm is not null,
        AguardaAssinatura = p.Situacao == SituacaoPrescricao.Encerrada
            && p.DevolvidaEm is null
            && (p.ExigeAssinaturaEletronicaDaExecucao || p.OrigemEnfermagem && p.AssinadaEm is null)
            && p.AssinaturaDaExecucao?.ArquivoId is null,
        RegistroPendente = p.Situacao == SituacaoPrescricao.Encerrada
            && p.AssinaturaDaExecucao is { ArquivoId: null },
        Hora = p.Hora.ToString("HH\\:mm"),
        Prescritor = p.Profissional?.Rotulo ?? "—",
        Progresso = p.DevolvidaEm is not null ? $"devolvida pelo médico · {p.MotivoDevolucao}"
            : p.AguardaValidacaoMedica
            ? (p.AguardaAssinaturaDaExecucao ? "execução registrada · falta assinatura da enfermagem" : "execução assinada · aguarda validação médica")
            : p.Situacao == SituacaoPrescricao.Encerrada
            ? $"encerrada · {p.Realizados} realizados, {p.NaoRealizados} não realizados"
            : $"{p.Realizados} de {p.Itens.Count} realizados · {p.Pendentes} aguardando",
        Itens = string.Join(" · ", p.Itens
            .OrderBy(i => i.Ordem)
            .Take(3)
            .Select(i => i.Descricao)) + (p.Itens.Count > 3 ? " · …" : string.Empty),
        TemPendencia = p.Pendentes > 0,
        Encerrada = p.Situacao == SituacaoPrescricao.Encerrada
    };
}

/// <summary>
/// O que a tela da Sala de infusão AFIRMA (out/2026 — migrado do SalaInfusaoViewModel WPF para
/// morar onde o teste alcança): quais folhas entram na fila e as duas frases de resumo.
/// </summary>
public static class SalaDeInfusaoNaTela
{
    /// <summary>
    /// A fila: as folhas do dia mais as que AGUARDAM A 2ª ASSINATURA (de qualquer dia — encerrar
    /// era o que as tirava daqui), sem repetir. A folha registrada pela ENFERMAGEM só aparece a
    /// quem pode checar, e enquanto não foi validada só a quem a registrou.
    /// </summary>
    public static IReadOnlyList<LinhaSalaInfusao> Montar(
        IEnumerable<PrescricaoInterna> doDia, IEnumerable<PrescricaoInterna> aguardandoAssinatura,
        bool podeChecar, int usuarioId, DateOnly hoje)
    {
        var linhas = new List<LinhaSalaInfusao>();
        var vistas = new HashSet<int>();
        foreach (var folha in doDia.Concat(aguardandoAssinatura))
        {
            if (!podeChecar && folha.OrigemEnfermagem) continue;
            if (folha.OrigemEnfermagem && folha.AssinadaEm is null && folha.RegistradaPorUsuarioId != usuarioId) continue;
            if (vistas.Add(folha.Id)) linhas.Add(LinhaSalaInfusao.De(folha, hoje));
        }
        return linhas;
    }

    /// <summary>
    /// O resumo da fila. O contador da ASSINATURA é separado do de itens aguardando: um se resolve
    /// administrando, o outro com o certificado — somados não diriam o que fazer.
    /// </summary>
    public static string Resumo(IReadOnlyList<LinhaSalaInfusao> folhas)
    {
        if (folhas.Count == 0) return "Nenhuma prescrição de infusão para hoje.";
        var pendentes = folhas.Count(f => f.TemPendencia);
        var semAssinar = folhas.Count(f => f.AguardaAssinatura);
        var semRegistro = folhas.Count(f => f.RegistroPendente);
        var devolvidas = folhas.Count(f => f.Devolvida);
        var recado = (semAssinar > 0 ? $" · {semAssinar} aguardando a assinatura da enfermagem" : string.Empty)
            + (semRegistro > 0 ? $" · {semRegistro} com documento assinado a conferir" : string.Empty)
            + (devolvidas > 0 ? $" · {devolvidas} devolvida(s) à enfermagem" : string.Empty);
        return $"{folhas.Count} {(folhas.Count == 1 ? "folha" : "folhas")} · {pendentes} com item aguardando{recado}.";
    }

    public static string ResumoValidacoes(int quantidade) => quantidade == 1
        ? "1 infusão aguardando sua avaliação e assinatura"
        : $"{quantidade} infusões aguardando sua avaliação e assinatura";

    /// <summary>O tom do selo de cada etapa: concluída verde, a que precisa de revisão âmbar, a atual azul.</summary>
    public static string Tom(EtapaInfusao e) => e.Concluida ? "sucesso" : e.Atencao ? "aviso" : e.Atual ? "info" : string.Empty;
}
