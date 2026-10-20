namespace Clinica.Application.Modelos;

/// <summary>
/// O que ESTE usuário já viu e já leu no sino (out/2026 — pedido da clínica: o número do sino
/// nunca sumia, porque contava as PENDÊNCIAS — "21 para ligar" — e não as novidades).
///
/// Duas perguntas diferentes, e é por isso que são dois conjuntos:
/// <list type="bullet">
/// <item><b>Visto</b> responde "aconteceu algo desde a última olhada?" — é o NÚMERO do sino.
///   Abrir o sino vê tudo o que está nele, e o número some.</item>
/// <item><b>Lido</b> responde "isto eu já tratei?" — é o DESTAQUE (negrito e ponto) dentro do
///   sino. Sai com "Marcar todas como lidas" ou clicando no aviso.</item>
/// </list>
///
/// ⚠️ Por CHAVE, nunca por "lido até tal hora": a resposta do SMS chega pelo relógio que lê o
/// balde a cada 5 minutos, com o carimbo de quando o paciente respondeu — um corte por hora
/// a esconderia de quem abriu o sino nesse meio-tempo. A chave é a do aviso
/// (<see cref="ChaveResposta"/>, <see cref="ChavePedido"/>): um aviso novo é uma chave que
/// o conjunto não tem.
///
/// Mora na Application (e o shell só aplica) porque é o que decide o NÚMERO que a pessoa vê —
/// o que a tela afirma precisa morar onde o <c>dotnet test</c> alcança.
/// </summary>
public sealed record MarcasDoSino(IReadOnlyList<string> Vistas, IReadOnlyList<string> Lidas)
{
    /// <summary>
    /// Teto de cada conjunto. Os avisos ativos de um dia são dezenas; guardar sem teto faria a
    /// linha crescer para sempre. Sai o mais ANTIGO: ele já deixou de ser aviso há muito.
    /// </summary>
    public const int Teto = 500;

    public static MarcasDoSino Vazias { get; } = new([], []);

    public bool Visto(string chave) => Vistas.Contains(chave);
    public bool Lido(string chave) => Lidas.Contains(chave);

    /// <summary>Abrir o sino: tudo o que está nele passa a VISTO (o número some).</summary>
    public MarcasDoSino Ver(IEnumerable<string> chaves) => this with { Vistas = Somar(Vistas, chaves) };

    /// <summary>"Marcar todas como lidas": lido implica visto.</summary>
    public MarcasDoSino LerTodas(IEnumerable<string> chaves)
    {
        var lista = chaves.ToList();
        return new MarcasDoSino(Somar(Vistas, lista), Somar(Lidas, lista));
    }

    /// <summary>Clicar num aviso: só ele vira lido (e visto).</summary>
    public MarcasDoSino Ler(string chave) => LerTodas([chave]);

    /// <summary>Quantos destes ainda não foram vistos — o número do sino.</summary>
    public int NaoVistas(IEnumerable<string> chaves) => chaves.Distinct().Count(c => !Visto(c));

    private static IReadOnlyList<string> Somar(IReadOnlyList<string> atuais, IEnumerable<string> novas)
    {
        var lista = atuais.ToList();
        var conjunto = new HashSet<string>(lista);
        foreach (var c in novas)
            if (!string.IsNullOrWhiteSpace(c) && conjunto.Add(c)) lista.Add(c);
        return lista.Count > Teto ? lista.Skip(lista.Count - Teto).ToList() : lista;
    }
}

/// <summary>As chaves dos avisos do sino — uma definição só para o shell e para os testes.</summary>
public static class LeituraDoSino
{
    /// <summary>Uma resposta de paciente: o horário e o tipo (o "sem resposta" que vira "não vem" é notícia nova).</summary>
    public static string ChaveResposta(int agendamentoId, TipoAvisoConfirmacao tipo) => $"R:{agendamentoId}:{(int)tipo}";

    /// <summary>Um pedido da enfermagem.</summary>
    public static string ChavePedido(int pedidoId) => $"P:{pedidoId}";
}
