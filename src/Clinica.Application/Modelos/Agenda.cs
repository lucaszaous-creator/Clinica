namespace Clinica.Application.Modelos;

/// <summary>Que recurso da agenda está em choque.</summary>
public enum RecursoAgenda
{
    /// <summary>Mesmo profissional em dois lugares ao mesmo tempo.</summary>
    Profissional,

    /// <summary>Mesma sala ocupada por dois atendimentos.</summary>
    Sala,

    /// <summary>O próprio paciente já tem horário nesse intervalo.</summary>
    Paciente,

    /// <summary>
    /// A agenda está fechada nesse intervalo: férias, feriado, folga, sala em manutenção.
    ///
    /// Diferente dos outros três, aqui não há agendamento do outro lado — o
    /// <see cref="ConflitoAgenda.AgendamentoId"/> carrega o id do BLOQUEIO. Quem lê o
    /// choque usa a descrição, que já vem escrita; o id serve para achar o registro.
    /// </summary>
    Bloqueio
}

/// <summary>
/// Um choque de horário detectado ao marcar. A agenda não decide sozinha: ela devolve
/// o choque para a recepção, que pode escolher outro horário ou assumir o encaixe.
/// </summary>
public sealed record ConflitoAgenda(
    RecursoAgenda Recurso,
    int AgendamentoId,
    string Descricao,
    DateTime Inicio,
    DateTime Fim);

/// <summary>Quanto um profissional tem na agenda de um dia.</summary>
public sealed record OcupacaoProfissional(
    int? ProfissionalId,
    string Nome,
    int Agendados,
    int Atendidos,
    int Faltas,
    int Cancelados,
    int MinutosOcupados)
{
    /// <summary>Horários que ainda ocupam a agenda (marcados + atendidos).</summary>
    public int Total => Agendados + Atendidos;
}

/// <summary>
/// O dia visto do balcão: o que o painel da recepção precisa mostrar de uma vez só.
/// </summary>
public sealed record ResumoDiaRecepcao(
    DateOnly Dia,
    int Agendados,
    int Aguardando,
    int NaRecepcao,
    int EmAtendimento,
    int Atendidos,
    int Faltas,
    int Cancelados,
    int Encaixes,
    int EsperaMediaMinutos,
    int NaListaDeEspera,
    IReadOnlyList<OcupacaoProfissional> Ocupacao)
{
    /// <summary>Taxa de falta do dia, em % dos horários que chegaram ao fim (atendidos + faltas).</summary>
    public int TaxaFaltaPercentual
    {
        get
        {
            var fechados = Atendidos + Faltas;
            return fechados == 0 ? 0 : (int)Math.Round(Faltas * 100.0 / fechados);
        }
    }
}

/// <summary>Uma data que a série não conseguiu marcar, e por quê.</summary>
public sealed record SessaoRecusada(DateTime Quando, string Motivo);

/// <summary>
/// O resultado de marcar em série: o que entrou na agenda e o que ficou de fora.
///
/// As duas listas vêm juntas de propósito. Recusar as dez sessões porque a de 25/12 caiu
/// em feriado devolveria a recepção ao trabalho manual; marcar nove e dizer qual não deu
/// é exatamente o que ela faria à mão — e agora sabe quais faltam sem conferir uma a uma.
/// </summary>
public sealed record SerieAgendada(
    string SerieId,
    IReadOnlyList<Clinica.Domain.Entities.Agendamento> Marcados,
    IReadOnlyList<SessaoRecusada> Recusados)
{
    public bool TudoMarcado => Recusados.Count == 0;
}
