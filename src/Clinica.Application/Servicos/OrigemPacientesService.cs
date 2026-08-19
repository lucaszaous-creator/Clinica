using Clinica.Application.Abstracoes;
using Clinica.Domain;

namespace Clinica.Application.Servicos;

/// <summary>Uma origem, com o tamanho dela na base e no período.</summary>
/// <param name="Origem">Nula = "não perguntado" — a base cadastrada antes de o campo existir,
/// ou o balcão que pulou a pergunta. É uma linha de primeira classe, nunca escondida: quando
/// ela é a maior de todas, a leitura certa não é "ninguém veio por indicação", é "o balcão
/// parou de perguntar" — e esse é o primeiro problema a resolver antes de decidir anúncio.</param>
public sealed record LinhaOrigemPacientes(
    OrigemPaciente? Origem,
    string Rotulo,
    int TotalNaBase,
    int EstreiasNoPeriodo);

/// <summary>Quem indicou, e quantos pacientes trouxe.</summary>
public sealed record IndicadorFrequente(string Nome, int Indicados);

/// <summary>O relatório inteiro, de uma vez.</summary>
public sealed record ResumoOrigemPacientes(
    DateOnly Inicio,
    DateOnly Fim,
    int TotalPacientes,
    int SemResposta,
    IReadOnlyList<LinhaOrigemPacientes> Linhas,
    IReadOnlyList<IndicadorFrequente> QuemMaisIndica);

/// <summary>
/// De onde vêm os pacientes (parcela 69).
///
/// O cadastro pergunta "como conheceu a clínica?" a todo paciente novo desde que o campo
/// existe — e a resposta era lida em exatamente UM lugar: a própria ficha, uma pessoa por
/// vez ("Indicação de Maria"). Nenhuma tela agregava. A recepcionista perguntava e digitava
/// havia dezenas de parcelas, e a direção não tinha como responder "quantos vieram por
/// indicação neste ano?" nem "vale manter o anúncio?" — que é a única razão de a pergunta
/// ser feita. O comentário do próprio rótulo em <see cref="RotulosEnum"/> já dizia "é um
/// dos poucos campos que a direção lê agrupado num relatório": o rótulo foi preparado para
/// um relatório que nunca existiu. É o defeito recorrente do projeto — dado gravado sem
/// leitor — na variante que custa decisão de dinheiro em vez de guia.
///
/// As regras:
/// - **"Estreou no período" = o PRIMEIRO atendimento caiu no período.** `Paciente` não tem
///   data de cadastro, então a estreia é o melhor fato disponível — e é o mais honesto:
///   cadastro sem atendimento é intenção, atendimento é a clínica trabalhando. Quem já
///   vinha antes e continuou vindo NÃO conta como estreia; quem foi cadastrado e nunca
///   veio conta na base e em estreia nenhuma. A tela escreve essa definição.
/// - **Toda origem aparece, MESMO zerada** (a regra do aging, parcela 23): sem a linha
///   "Redes sociais — 0", a direção lê "não medimos" onde o fato é "ninguém veio por aí".
/// - **A fração é sobre a BASE, e sem base é nula** — 0% de zero pacientes viraria um
///   relatório inteiro de alvos batidos numa clínica recém-instalada.
/// - **Quem mais indica agrupa por nome NORMALIZADO** (espaços e maiúsculas fora): "maria
///   silva" e "Maria Silva " são a mesma pessoa digitada por duas recepcionistas, e
///   separá-las esconderia justamente a maior indicadora da clínica.
/// </summary>
public sealed class OrigemPacientesService
{
    /// <summary>
    /// Quantos indicadores o ranking mostra. É um TOP, não uma lista truncada: a pergunta
    /// da direção é "quem são os maiores", e a tela diz que mostra os maiores.
    /// </summary>
    public const int MaximoIndicadores = 10;

    private readonly IClinicaRepositorio _repo;

    public OrigemPacientesService(IClinicaRepositorio repo) => _repo = repo;

    public async Task<ResumoOrigemPacientes> ResumoAsync(
        DateOnly inicio, DateOnly fim, CancellationToken ct = default)
    {
        if (fim < inicio)
            throw new InvalidOperationException("O fim do período é anterior ao início.");

        var pacientes = await _repo.OrigensDosPacientesAsync(ct);
        var primeiras = await _repo.PrimeiroAtendimentoPorPacienteAsync(ct);

        int Estreias(IEnumerable<(int PacienteId, OrigemPaciente? Origem, string? IndicadoPor)> grupo)
            => grupo.Count(p =>
                primeiras.TryGetValue(p.PacienteId, out var primeira)
                && primeira >= inicio && primeira <= fim);

        var linhas = new List<LinhaOrigemPacientes>();
        foreach (var origem in Enum.GetValues<OrigemPaciente>())
        {
            var doGrupo = pacientes.Where(p => p.Origem == origem).ToList();
            linhas.Add(new LinhaOrigemPacientes(
                origem, RotulosEnum.De(origem), doGrupo.Count, Estreias(doGrupo)));
        }

        var semResposta = pacientes.Where(p => p.Origem is null).ToList();
        linhas.Add(new LinhaOrigemPacientes(
            null, "Não perguntado", semResposta.Count, Estreias(semResposta)));

        // Do maior para o menor — inclusive o "não perguntado", DE PROPÓSITO: quando ele
        // encabeça a lista, esse é o achado do relatório, e escondê-lo no rodapé faria a
        // direção decidir anúncio sobre uma amostra que o balcão parou de colher.
        var ordenadas = linhas
            .OrderByDescending(l => l.TotalNaBase)
            .ThenBy(l => l.Rotulo, StringComparer.CurrentCulture)
            .ToList();

        var quemIndica = pacientes
            .Where(p => !string.IsNullOrWhiteSpace(p.IndicadoPor))
            .GroupBy(p => p.IndicadoPor!.Trim(), StringComparer.CurrentCultureIgnoreCase)
            .Select(g => new IndicadorFrequente(g.Key, g.Count()))
            .OrderByDescending(i => i.Indicados)
            .ThenBy(i => i.Nome, StringComparer.CurrentCulture)
            .Take(MaximoIndicadores)
            .ToList();

        return new ResumoOrigemPacientes(
            inicio, fim, pacientes.Count, semResposta.Count, ordenadas, quemIndica);
    }
}
