namespace Clinica.Domain.Entities;

/// <summary>
/// ETAPAS 2 e 3 do Processo de Enfermagem — o DIAGNÓSTICO e o resultado esperado dele
/// (COFEN 358/2009).
///
/// O que é um diagnóstico de enfermagem
/// ------------------------------------
/// Não é o diagnóstico médico. O médico diz <i>"lombalgia crônica"</i>; a enfermagem diz
/// <i>"dor aguda relacionada a espasmo muscular, evidenciada por relato de 7/10 e postura
/// antálgica"</i> — o que o paciente VIVE com aquilo, que é o que o cuidado trata. É por
/// isso que os dois convivem no mesmo prontuário sem se substituírem.
///
/// ⚠️ A REDAÇÃO em três partes é o que o torna um diagnóstico e não um rótulo: o problema,
/// o <b>relacionado a</b> (a causa provável, que é onde o cuidado age) e o <b>evidenciado
/// por</b> (o achado que o sustenta). Sem a terceira parte, ninguém consegue avaliar depois
/// se ele foi resolvido — e a etapa 5 vira opinião.
///
/// ⚠️ E ele é COPIADO do catálogo, nunca apontado. Mesma regra do protocolo do mapa
/// corporal e do preço por convênio: corrigir a redação de um diagnóstico no catálogo hoje
/// não pode reescrever o que a enfermeira registrou no mês passado — e aqui isso não é
/// desenho, é a Lei 13.787/2018.
/// </summary>
public class DiagnosticoEnfermagem
{
    public int Id { get; set; }

    public int EvolucaoEnfermagemId { get; set; }
    public EvolucaoEnfermagem? Evolucao { get; set; }

    /// <summary>
    /// O código do catálogo, quando veio de lá. Nulo = escrito à mão, e isso é legítimo:
    /// o catálogo é ATALHO, não a lista fechada do que a enfermagem pode diagnosticar.
    /// </summary>
    public string? Codigo { get; set; }

    /// <summary>O problema. Copiado do catálogo ou digitado.</summary>
    public string Titulo { get; set; } = string.Empty;

    /// <summary>O "relacionado a": a causa provável, que é onde o cuidado age.</summary>
    public string? RelacionadoA { get; set; }

    /// <summary>O "evidenciado por": o achado que sustenta o diagnóstico.</summary>
    public string? EvidenciadoPor { get; set; }

    /// <summary>
    /// ETAPA 3 — o PLANEJAMENTO: o que se espera alcançar, e em que prazo. É contra ele
    /// que a etapa 5 avalia; sem ele, "avaliação" é impressão.
    /// </summary>
    public string? ResultadoEsperado { get; set; }

    /// <summary>Ordem na folha, para a lista sair como a enfermeira a montou.</summary>
    public int Ordem { get; set; }

    /// <summary>A redação completa, como ela se lê no papel e no prontuário.</summary>
    public string Redacao
    {
        get
        {
            var partes = new List<string> { Titulo };
            if (!string.IsNullOrWhiteSpace(RelacionadoA))
                partes.Add($"relacionado a {RelacionadoA.Trim()}");
            if (!string.IsNullOrWhiteSpace(EvidenciadoPor))
                partes.Add($"evidenciado por {EvidenciadoPor.Trim()}");
            return string.Join(", ", partes);
        }
    }
}

/// <summary>
/// ETAPA 4 do Processo de Enfermagem — a PRESCRIÇÃO DE ENFERMAGEM (COFEN 358/2009).
///
/// ⚠️ NÃO CONFUNDIR com <see cref="PrescricaoInterna"/>, que é a folha de infusão: aquela é
/// prescrição MÉDICA e a enfermagem a EXECUTA; esta é o que a própria enfermagem prescreve
/// — os cuidados. "Elevar o membro puncionado", "trocar o curativo a cada 24h", "orientar
/// sinais de flebite". As duas convivem, e quem checa a de cima é a mesma pessoa que
/// escreve a de baixo.
///
/// A FREQUÊNCIA é parte do cuidado, não um detalhe: "verificar o acesso" sem dizer de
/// quanto em quanto tempo não é prescrição, é lembrete. É o mesmo motivo pelo qual o item
/// da folha de infusão carrega a via e o tempo de infusão.
/// </summary>
public class CuidadoEnfermagem
{
    public int Id { get; set; }

    public int EvolucaoEnfermagemId { get; set; }
    public EvolucaoEnfermagem? Evolucao { get; set; }

    /// <summary>Código do catálogo, quando veio de lá. Nulo = escrito à mão.</summary>
    public string? Codigo { get; set; }

    /// <summary>O cuidado. Copiado do catálogo ou digitado.</summary>
    public string Descricao { get; set; } = string.Empty;

    /// <summary>"a cada 24h", "a cada turno", "se dor &gt; 5", "contínuo".</summary>
    public string? Frequencia { get; set; }

    /// <summary>
    /// O diagnóstico que este cuidado atende, quando a enfermeira o vinculou.
    ///
    /// ⚠️ É o Id do <see cref="DiagnosticoEnfermagem"/> da MESMA evolução — e é opcional
    /// de propósito: exigir o vínculo faria a enfermeira parar de prescrever o cuidado que
    /// não se encaixa em nenhum diagnóstico escrito (a hidratação, a orientação de alta),
    /// e cuidado que não se registra é cuidado que não aconteceu.
    /// </summary>
    public int? DiagnosticoEnfermagemId { get; set; }

    public int Ordem { get; set; }

    /// <summary>Como o cuidado se lê na folha: o que fazer e de quanto em quanto tempo.</summary>
    public string Redacao => string.IsNullOrWhiteSpace(Frequencia)
        ? Descricao
        : $"{Descricao} — {Frequencia.Trim()}";
}
