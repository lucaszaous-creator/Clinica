namespace Clinica.Domain.Entities;

/// <summary>Em que pé está o problema. É o eixo da lista: ativo é o que se trata hoje.</summary>
public enum SituacaoProblema
{
    /// <summary>Em curso — entra na lista que abre com o paciente.</summary>
    Ativo,

    /// <summary>Encerrado (curou, foi corrigido, o episódio passou).</summary>
    Resolvido,

    /// <summary>
    /// Registrado por engano. Não some da base: quem escreveu precisa poder desdizer sem
    /// apagar, porque a linha esteve no prontuário e conduta pode ter sido tomada com ela.
    /// </summary>
    Descartado
}

/// <summary>
/// Natureza da linha. Alergia fica separada de diagnóstico porque a leitura é outra: uma
/// descreve o que o paciente TEM, a outra o que não se pode fazer com ele.
/// </summary>
public enum NaturezaProblema
{
    /// <summary>Diagnóstico ou queixa em acompanhamento.</summary>
    Diagnostico,

    /// <summary>Alergia ou intolerância — vira alerta de ponto de atendimento.</summary>
    Alergia,

    /// <summary>Cirurgia, internação, fratura — o que já aconteceu e importa saber.</summary>
    Antecedente,

    /// <summary>Medicação de uso contínuo.</summary>
    MedicacaoContinua
}

/// <summary>
/// Uma linha da LISTA DE PROBLEMAS do paciente (parcela 37).
///
/// Por que existe
/// --------------
/// Até aqui o CID morava só dentro de <see cref="DocumentoClinico"/>: era um campo por
/// papel emitido, não o diagnóstico da pessoa. Na prática isso significa que o profissional
/// redigitava "M54.5" a cada atestado, que ninguém conseguia responder "o que este paciente
/// tem?" sem ler o prontuário inteiro, e que a alergia — a informação que muda conduta em
/// dez segundos — ficava enterrada em texto livre no meio de uma evolução de dois anos
/// atrás. Prontuário de verdade tem lista de problemas ativa, e é ela que alimenta os
/// documentos, não o contrário.
///
/// As regras
/// ---------
/// - <b>A lista é do PACIENTE, não da sessão.</b> Uma hérnia não pertence à consulta em que
///   foi descoberta; ela acompanha a pessoa. A evolução de origem fica guardada como
///   procedência (<see cref="EvolucaoId"/>), e é opcional.
/// - <b>Não se apaga: muda-se a situação.</b> Resolvido e Descartado continuam na base,
///   como a NC do faturamento e o documento clínico cancelado. Linha que some leva junto a
///   prova de que a conduta da época era razoável.
/// - <b>Alergia é sempre alerta</b>, em qualquer situação que não seja Descartado — inclusive
///   "resolvida", porque alergia que alguém deu por superada e voltou é exatamente o caso
///   em que o aviso salva. Ver <see cref="EhAlertaDeAtendimento"/>.
/// - <b>O CID é opcional.</b> Exigi-lo faria o fisioterapeuta e o acupunturista pararem de
///   usar a lista, e uma lista de problemas pela metade é pior que nenhuma: ela seria lida
///   como completa.
/// </summary>
public class ProblemaPaciente
{
    public int Id { get; set; }

    public int PacienteId { get; set; }
    public Paciente? Paciente { get; set; }

    /// <summary>Quem registrou. Null quando a clínica ainda não cadastrou a equipe.</summary>
    public int? ProfissionalId { get; set; }
    public Profissional? Profissional { get; set; }

    /// <summary>Sessão em que o problema foi levantado, quando houve uma. Procedência.</summary>
    public int? EvolucaoId { get; set; }
    public Evolucao? Evolucao { get; set; }

    public NaturezaProblema Natureza { get; set; } = NaturezaProblema.Diagnostico;

    /// <summary>Como o profissional escreve ("Lombalgia crônica", "Dipirona").</summary>
    public string Descricao { get; set; } = string.Empty;

    /// <summary>Código CID-10, quando há. Opcional de propósito.</summary>
    public string? Cid { get; set; }

    public SituacaoProblema Situacao { get; set; } = SituacaoProblema.Ativo;

    /// <summary>Desde quando. Null quando o paciente não sabe precisar — que é o comum.</summary>
    public DateOnly? Inicio { get; set; }

    /// <summary>Quando encerrou. Só faz sentido com <see cref="Situacao"/> resolvida.</summary>
    public DateOnly? Fim { get; set; }

    /// <summary>
    /// Observação clínica da linha — a conduta da alergia ("faz broncoespasmo"), o estágio
    /// do diagnóstico, a dose da medicação contínua.
    /// </summary>
    public string? Observacoes { get; set; }

    /// <summary>Motivo do descarte. Obrigatório ao descartar: desdizer sem dizer por quê
    /// deixa o próximo leitor sem saber se a linha era falsa ou se o paciente melhorou.</summary>
    public string? MotivoDescarte { get; set; }

    public DateTime CriadoEm { get; set; } = DateTime.Now;

    public string? CriadoPor { get; set; }

    public DateTime? AtualizadoEm { get; set; }

    public string? AtualizadoPor { get; set; }

    /// <summary>Entra na lista que abre junto com o paciente.</summary>
    public bool EstaAtivo => Situacao == SituacaoProblema.Ativo;

    /// <summary>
    /// A linha tem de aparecer em destaque na hora de atender.
    ///
    /// Alergia resolvida CONTINUA alertando (só o descarte a cala), porque "resolvida" numa
    /// alergia é quase sempre "não reagiu da última vez" — e o dia em que reagir é o dia em
    /// que o aviso teria valido. Medicação contínua também alerta: interação é o segundo
    /// motivo de dano evitável no consultório.
    /// </summary>
    public bool EhAlertaDeAtendimento
        => Situacao != SituacaoProblema.Descartado
           && Natureza is NaturezaProblema.Alergia or NaturezaProblema.MedicacaoContinua;

    /// <summary>Descrição com o CID ao lado, como a lista e o documento a escrevem.</summary>
    public string Rotulo
        => string.IsNullOrWhiteSpace(Cid) ? Descricao : $"{Descricao} ({Cid})";
}
