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

    /// <summary>
    /// "Se necessário" — o cuidado só se executa quando a condição acontece ("se dor > 5",
    /// "se náusea").
    ///
    /// ⚠️ Booleano, e não adivinhado do texto de <see cref="Frequencia"/>, pela razão do
    /// item SOS da folha de infusão: cuidado condicional sem registro NÃO é trabalho
    /// atrasado — é a condição que não ocorreu. Contá-lo deixaria todo plano com um SOS
    /// eternamente "pendente", e o contador da sala, que existe para dizer o que falta
    /// fazer, passaria a apontar para nada. Ler a frequência com regex daria um palpite
    /// que erra nos dois sentidos sobre um campo que é texto livre por desenho.
    /// </summary>
    public bool SeNecessario { get; set; }

    /// <summary>
    /// As execuções deste cuidado — a etapa 4 da COFEN 358/2009 (parcela 76).
    ///
    /// ⚠️ São VÁRIAS, e é aqui que este registro se separa do item da folha de infusão: lá
    /// o item se administra UMA vez e "item já checado não se edita"; aqui o cuidado tem
    /// FREQUÊNCIA ("a cada 24h", "a cada turno") e é executado de novo a cada vez. Copiar a
    /// guarda de lá impediria a segunda troca de curativo do dia.
    /// </summary>
    public List<ChecagemCuidado> Checagens { get; set; } = new();

    /// <summary>Como o cuidado se lê na folha: o que fazer e de quanto em quanto tempo.</summary>
    public string Redacao => string.IsNullOrWhiteSpace(Frequencia)
        ? Descricao
        : $"{Descricao} — {Frequencia.Trim()}";
}

/// <summary>
/// A EXECUÇÃO DE UM CUIDADO DE ENFERMAGEM — a etapa 4 da COFEN 358/2009 (parcela 76).
///
/// O buraco que ela fecha
/// ----------------------
/// A Resolução COFEN 358/2009 divide o Processo de Enfermagem em CINCO etapas, e o sistema
/// cobria as três primeiras (histórico, diagnóstico, resultado esperado) e a quarta só como
/// TEXTO: a enfermeira escrevia "curativo a cada 24h" e <b>nada registrava que foi feito</b>.
/// Implementação sem registro não é implementação — é intenção; e cuidado que não se
/// registra é, para qualquer fiscalização, cuidado que não aconteceu.
///
/// Por que não é a checagem da folha de infusão
/// --------------------------------------------
/// A mecânica é a MESMA e de propósito (o ✓ com o horário, a "rodela" com justificativa, a
/// retificação em linha nova) — mas o objeto é outro: a folha de infusão é um documento com
/// ciclo de vida e itens de administração ÚNICA; o cuidado é uma linha do plano, com
/// frequência, executada muitas vezes. Enfiar um no outro obrigaria a folha a aceitar item
/// repetível, o que quebra a garantia de que a segunda via sai idêntica.
///
/// As regras, todas herdadas da parcela 42 porque já foram pagas caro lá
/// --------------------------------------------------------------------
/// A hora é <b>INFORMADA</b>, nunca <c>DateTime.Now</c> (a técnica executa às 14h e registra
/// às 14h20; o relógio vai em <see cref="RegistradoEm"/> AO LADO, e a diferença entre os dois
/// é o que uma auditoria de enfermagem procura). <b>Hora futura é recusada</b>, porque
/// pré-checagem é o hábito que faz um cuidado aparecer como feito num paciente que saiu antes
/// de recebê-lo. <b>Não realizado exige justificativa.</b> <b>Nada se apaga: RETIFICA-SE</b>,
/// com linha nova apontando a anterior. E <b>quem checa é quem fez LOGIN</b>, com o COREN
/// copiado no ato.
/// </summary>
public class ChecagemCuidado
{
    public int Id { get; set; }

    public int CuidadoEnfermagemId { get; set; }
    public CuidadoEnfermagem? Cuidado { get; set; }

    /// <summary>
    /// O DIA em que o cuidado foi (ou não) executado.
    ///
    /// ⚠️ Coluna própria, e não derivada de <see cref="RegistradoEm"/> como na folha de
    /// infusão: aquela é do dia, esta acompanha um plano que dura semanas — e a técnica que
    /// registra na segunda o curativo do sábado estaria movendo o fato para outro dia.
    /// </summary>
    public DateOnly Data { get; set; }

    /// <summary>A hora do FATO, informada por quem executou. No papel é o ✓ com o horário.</summary>
    public TimeOnly HoraRealizacao { get; set; }

    public SituacaoChecagem Situacao { get; set; }

    /// <summary>
    /// Por que NÃO foi feito. Obrigatória no <see cref="SituacaoChecagem.NaoRealizado"/> —
    /// a "rodela" do papel. Opcional no realizado.
    /// </summary>
    public string? Justificativa { get; set; }

    /// <summary>
    /// O que se observou ao executar ("ferida com secreção serosa", "tolerou bem a
    /// deambulação"). É a etapa 5 da COFEN — a AVALIAÇÃO — no lugar onde ela de fato
    /// acontece: junto do cuidado, e não numa tela separada que ninguém abre.
    /// </summary>
    public string? Observacao { get; set; }

    /// <summary>O login que executou. É o vínculo forte; o resto é cópia para a impressão.</summary>
    public int? ExecutanteUsuarioId { get; set; }
    public UsuarioSistema? ExecutanteUsuario { get; set; }

    /// <summary>Nome copiado no ato — a folha continua legível depois de o usuário sair.</summary>
    public string ExecutanteNome { get; set; } = string.Empty;

    /// <summary>COREN copiado no ato. Obrigatório: é parte da assinatura profissional.</summary>
    public string? ExecutanteConselho { get; set; }

    /// <summary>O relógio do sistema no momento do registro, AO LADO da hora informada.</summary>
    public DateTime RegistradoEm { get; set; } = DateTime.Now;

    /// <summary>A checagem que esta corrige. A apontada deixa de valer e FICA na base.</summary>
    public int? RetificaChecagemId { get; set; }
    public ChecagemCuidado? RetificaChecagem { get; set; }

    /// <summary>Por que a anterior estava errada. Obrigatório ao retificar.</summary>
    public string? MotivoRetificacao { get; set; }

    // ---- Leituras derivadas ----

    public bool EhRetificacao => RetificaChecagemId is not null;

    public bool Realizado => Situacao == SituacaoChecagem.Realizado;

    /// <summary>
    /// Quanto tempo depois da execução a checagem foi digitada. Informativo, não acusação —
    /// a técnica está com o paciente, não com o teclado. Só vira leitura de auditoria quando
    /// é grande.
    /// </summary>
    public TimeSpan AtrasoDoRegistro
    {
        get
        {
            var diferenca = RegistradoEm - Data.ToDateTime(HoraRealizacao);
            return diferenca < TimeSpan.Zero ? TimeSpan.Zero : diferenca;
        }
    }

    /// <summary>"✓ 14:30 — Joana Técnica (COREN-SP 999999)" ou "○ 14:30 (não realizado)".</summary>
    public string Linha
    {
        get
        {
            var marca = Realizado ? "\u2713" : "\u25CB";
            var quem = string.IsNullOrWhiteSpace(ExecutanteConselho)
                ? ExecutanteNome
                : $"{ExecutanteNome} ({ExecutanteConselho})";
            var cauda = Realizado ? string.Empty : " \u2014 n\u00E3o realizado";
            return $"{marca} {HoraRealizacao:HH\\:mm} \u2014 {quem}{cauda}";
        }
    }

    /// <summary>
    /// As checagens VIGENTES de uma lista: as que ainda não foram retificadas por outra.
    ///
    /// ⚠️ Estática e pública pela razão de <see cref="EvolucaoEnfermagem.Vigentes"/>: a
    /// regra tem mais de um leitor (o quadro do dia, a impressão e a contagem de pendências),
    /// e duas definições de "a checagem que vale" divergem na primeira correção.
    /// </summary>
    public static IEnumerable<ChecagemCuidado> Vigentes(IEnumerable<ChecagemCuidado> todas)
    {
        var lista = todas as IReadOnlyCollection<ChecagemCuidado> ?? todas.ToList();

        var substituidas = lista
            .Where(c => c.RetificaChecagemId is not null)
            .Select(c => c.RetificaChecagemId!.Value)
            .ToHashSet();

        return lista.Where(c => !substituidas.Contains(c.Id));
    }
}
