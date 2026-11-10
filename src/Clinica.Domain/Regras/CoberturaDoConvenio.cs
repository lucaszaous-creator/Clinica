namespace Clinica.Domain.Regras;

/// <summary>
/// O QUE A CLÍNICA ATENDE POR UM CONVÊNIO (nov/2026 — a direção, sobre a Petrobras: "vamos
/// parar com a acupuntura; agora é só consulta de especialidade, uma por semana de cada").
///
/// Não é regra do CONVÊNIO (o que ele cobre no contrato) nem de faturamento: é decisão da
/// CLÍNICA sobre o que ela aceita marcar por aquele plano. Por isso mora numa configuração
/// (<c>ParametrosService.CoberturaDosConveniosAsync</c>) e não numa <c>Regra&lt;Convenio&gt;</c>
/// — a clínica muda de ideia sem release, e a regra de guias de quem já foi atendido não muda.
///
/// ⚠️ Diferente da trava de consulta, aqui NÃO há pedido de liberação: a direção foi
/// explícita ("não permite, nem libera"). A saída de quem quer o atendimento é marcá-lo
/// como PARTICULAR, que esta regra nunca alcança.
/// </summary>
/// <param name="ModalidadesNaoAtendidas">
/// Os códigos das modalidades que a clínica NÃO marca pelo plano (ex.: as de acupuntura). É
/// lista do que SAI, e não do que fica: a direção tirou a acupuntura da Petrobras e o BSV
/// continuou — "só consulta" teria levado o BSV junto. A variante do catálogo é alcançada
/// pelo código dela OU pelo da modalidade de que deriva.
/// </param>
/// <param name="Especialidades">Os códigos das especialidades aceitas; vazio = qualquer uma.</param>
/// <param name="IntervaloDias">Mínimo de dias entre duas consultas da MESMA especialidade; 0 = sem intervalo.</param>
public sealed record CoberturaDoConvenio(
    IReadOnlyList<string> ModalidadesNaoAtendidas, IReadOnlyList<string> Especialidades, int IntervaloDias)
{
    /// <summary>Restringe alguma coisa? Cobertura que não restringe nada não se grava.</summary>
    public bool Restringe => ModalidadesNaoAtendidas.Count > 0 || Especialidades.Count > 0 || IntervaloDias > 0;

    /// <summary>A modalidade (pelo código do catálogo ou pela base de que deriva) saiu do plano?</summary>
    public bool NaoAtende(string? modalidadeCodigo, ModalidadeAtendimento modalidadeBase)
        => ModalidadesNaoAtendidas.Contains(modalidadeBase.ToString(), StringComparer.OrdinalIgnoreCase)
           || (modalidadeCodigo is not null && ModalidadesNaoAtendidas.Contains(modalidadeCodigo, StringComparer.OrdinalIgnoreCase));

    public bool AceitaEspecialidade(string? codigo)
        => Especialidades.Count == 0
           || (codigo is not null && Especialidades.Contains(codigo, StringComparer.OrdinalIgnoreCase));
}

/// <summary>Uma consulta pelo convênio já na agenda do paciente, com a especialidade dela.</summary>
public sealed record ConsultaDaCobertura(int AgendamentoId, DateTime DataHora, string? EspecialidadeCodigo);

/// <summary>A marcação que a cobertura do convênio recusa — a frase já diz o porquê e a saída.</summary>
public sealed class CoberturaDoConvenioException(string mensagem) : InvalidOperationException(mensagem);

public static class CoberturaDoConvenioRegra
{
    /// <summary>
    /// A frase da recusa, ou nulo quando a marcação cabe na cobertura. Pura: recebe o que a
    /// agenda leu. <paramref name="soIntervalo"/> é a remarcação de um horário que JÁ existia
    /// com a mesma modalidade e especialidade ("os que já estão marcados ficam como estão"):
    /// só o intervalo é conferido, porque o dia mudou.
    /// </summary>
    public static string? Recusa(
        CoberturaDoConvenio? cobertura, string plano, ModalidadeAtendimento modalidade, string? modalidadeCodigo,
        string? especialidadeCodigo, string especialidadeNome, DateOnly dia,
        IEnumerable<ConsultaDaCobertura> consultasPeloPlano, bool soIntervalo = false)
    {
        if (cobertura is null || !cobertura.Restringe) return null;
        var ehConsulta = modalidade == ModalidadeAtendimento.Consulta;

        if (!soIntervalo)
        {
            if (cobertura.NaoAtende(modalidadeCodigo, modalidade))
                return $"A clínica não atende {CatalogoModalidades.Nome(modalidadeCodigo, modalidade)} pelo {plano}. "
                       + "Se o paciente quiser este atendimento, marque como PARTICULAR.";

            if (ehConsulta && !cobertura.AceitaEspecialidade(especialidadeCodigo))
                return $"Pelo {plano} a clínica atende só consulta de {Lista(cobertura.Especialidades)}. "
                       + $"Consulta de {especialidadeNome} é PARTICULAR.";
        }

        if (!ehConsulta || cobertura.IntervaloDias <= 0) return null;

        // A mais próxima da MESMA especialidade, para trás ou para a frente: marcar "antes" de
        // uma consulta que já existe fura o intervalo do mesmo jeito.
        var conflito = consultasPeloPlano
            .Where(c => string.Equals(c.EspecialidadeCodigo, especialidadeCodigo, StringComparison.OrdinalIgnoreCase))
            .Select(c => DateOnly.FromDateTime(c.DataHora))
            .Where(d => Math.Abs(d.DayNumber - dia.DayNumber) < cobertura.IntervaloDias)
            .OrderBy(d => Math.Abs(d.DayNumber - dia.DayNumber))
            .Cast<DateOnly?>()
            .FirstOrDefault();
        if (conflito is not { } outra) return null;

        return $"Pelo {plano} é uma consulta de {especialidadeNome} a cada {cobertura.IntervaloDias} dia(s), e o paciente "
               + $"já tem uma em {outra:dd/MM/yyyy}. Marque a partir de {outra.AddDays(cobertura.IntervaloDias):dd/MM/yyyy}"
               + " — ou como PARTICULAR. Consulta de outra especialidade pode ser na mesma semana.";
    }

    private static string Lista(IReadOnlyList<string> codigos)
    {
        var nomes = codigos.Select(CatalogoEspecialidades.Nome).ToList();
        return nomes.Count switch
        {
            0 => string.Empty,
            1 => nomes[0],
            _ => string.Join(", ", nomes.Take(nomes.Count - 1)) + " e " + nomes[^1]
        };
    }
}
