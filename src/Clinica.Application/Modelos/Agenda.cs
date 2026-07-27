namespace Clinica.Application.Modelos;

/// <summary>Que recurso da agenda está em choque.</summary>
public enum RecursoAgenda
{
    /// <summary>Mesmo profissional em dois lugares ao mesmo tempo.</summary>
    Profissional,

    /// <summary>Mesma sala ocupada por dois atendimentos.</summary>
    Sala,

    /// <summary>O próprio paciente já tem horário nesse intervalo.</summary>
    Paciente
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
