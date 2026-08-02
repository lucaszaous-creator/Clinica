namespace Clinica.Domain.Avaliacoes;

/// <summary>
/// Onde os instrumentos são resolvidos — por código e por especialidade.
///
/// É o mesmo desenho do <c>RegistroRegras</c> dos convênios, e pelo mesmo motivo: quem
/// aplica a avaliação sabe QUAL escala quer (ou de qual especialidade é), não como ela
/// pontua. A lista mora em código porque a definição de uma escala publicada não é
/// configuração da clínica.
///
/// Acrescentar um instrumento é: uma classe nova aqui do lado, uma linha nesta lista e o
/// fluxograma coberto em <c>InstrumentosAvaliacaoTests</c> — a mesma convenção que o
/// <c>CLAUDE.md</c> já fixa para convênio novo.
/// </summary>
public static class RegistroInstrumentos
{
    private static readonly IReadOnlyList<IInstrumentoAvaliacao> _todos =
    [
        new InstrumentoOswestry(),
        new InstrumentoPhq9(),
        new InstrumentoGad7(),
        new InstrumentoKatz(),
        new InstrumentoFindrisc()
    ];

    /// <summary>Todos os instrumentos, na ordem em que a tela os oferece.</summary>
    public static IReadOnlyList<IInstrumentoAvaliacao> Todos => _todos;

    /// <summary>O instrumento de um código, ou null se ele não existe (mais) no catálogo.</summary>
    /// <remarks>
    /// Devolver null em vez de lançar é deliberado: uma avaliação gravada guarda o código
    /// do instrumento, e uma versão futura pode ter retirado a escala da lista. O
    /// histórico continua legível porque nome, enunciado e faixa foram COPIADOS na
    /// aplicação — o registro só é consultado para APLICAR uma nova.
    /// </remarks>
    public static IInstrumentoAvaliacao? Obter(string? codigo)
        => string.IsNullOrWhiteSpace(codigo)
            ? null
            : _todos.FirstOrDefault(i => string.Equals(i.Codigo, codigo, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// Instrumentos oferecidos a uma especialidade (código do catálogo). Instrumento que
    /// não declara especialidade nenhuma vale para todas.
    ///
    /// Especialidade nula ou desconhecida devolve TODOS — o profissional sem especialidade
    /// cadastrada precisa poder trabalhar, e uma lista vazia aqui seria lida como "este
    /// sistema não tem avaliação nenhuma".
    /// </summary>
    public static IReadOnlyList<IInstrumentoAvaliacao> DaEspecialidade(string? especialidadeCodigo)
    {
        if (string.IsNullOrWhiteSpace(especialidadeCodigo)) return _todos;

        var doTipo = _todos
            .Where(i => i.Especialidades.Count == 0
                        || i.Especialidades.Any(e =>
                            string.Equals(e, especialidadeCodigo, StringComparison.OrdinalIgnoreCase)))
            .ToList();

        return doTipo.Count > 0 ? doTipo : _todos;
    }
}
