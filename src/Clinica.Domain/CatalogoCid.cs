namespace Clinica.Domain;

/// <summary>Um código da CID-10 com a descrição oficial dele.</summary>
/// <param name="Codigo">"M54.5".</param>
/// <param name="Descricao">"Dor lombar baixa".</param>
/// <param name="Grupo">Para agrupar a lista na tela ("Coluna e dor", "Saúde mental"…).</param>
public sealed record ItemCid(string Codigo, string Descricao, string Grupo)
{
    /// <summary>"M54.5 — Dor lombar baixa", como sai impresso e como se lê na lista.</summary>
    public string Rotulo => $"{Codigo} — {Descricao}";
}

/// <summary>
/// ATALHO DA CID-10 (parcela 63).
///
/// O campo <c>Cid</c> existe em <c>DocumentoClinico</c> e em <c>ProblemaPaciente</c> desde
/// que os dois existem, e sempre foi <b>texto livre</b>: a profissional digitava "M54.5"
/// de cabeça. Um dígito trocado sai impresso no atestado, vai para o RH do paciente e
/// entra na lista de problemas dele — e "M54.4" também existe, então não há nada que
/// denuncie o erro.
///
/// ⚠️ <b>Isto NÃO é a CID-10 inteira, e a tela diz isso.</b> A classificação tem cerca de
/// 14 mil códigos; aqui estão os que esta clínica usa, pelas cinco especialidades da casa.
/// O campo <b>continua aceitando qualquer texto</b> — a lista é atalho e conferência, não
/// validação. Recusar código fora da lista seria a regra apertada demais que o projeto já
/// rejeitou no formato do número da guia: ela travaria o atendimento e o profissional não
/// teria como contornar.
///
/// Mora em CÓDIGO e não em tabela pelo mesmo desenho das escalas clínicas
/// (<c>Domain/Avaliacoes/</c>) e do <c>CatalogoMedidas</c>: a CID-10 é classificação
/// PUBLICADA, não configuração da clínica. Deixar editar a descrição de um código
/// produziria um "M54.5" que continua se chamando M54.5 sem ser.
/// </summary>
public static class CatalogoCid
{
    public const string GrupoColuna = "Coluna, articulações e dor";
    public const string GrupoNeuro = "Neurologia";
    public const string GrupoMental = "Saúde mental";
    public const string GrupoEndocrino = "Endocrinologia e metabolismo";
    public const string GrupoGeriatria = "Geriatria";
    public const string GrupoGeral = "Geral";

    /// <summary>
    /// Os códigos que esta clínica usa. Acrescentar um aqui é uma linha — e é o caminho
    /// certo quando a clínica passar a atender algo que não está na lista.
    /// </summary>
    public static readonly IReadOnlyList<ItemCid> Todos =
    [
        // ---- Coluna, articulações e dor (acupuntura, clínica da dor, fisioterapia) ----
        new("M54.5", "Dor lombar baixa", GrupoColuna),
        new("M54.4", "Lumbago com ciática", GrupoColuna),
        new("M54.3", "Ciática", GrupoColuna),
        new("M54.2", "Cervicalgia", GrupoColuna),
        new("M54.6", "Dor na coluna torácica", GrupoColuna),
        new("M54.1", "Radiculopatia", GrupoColuna),
        new("M53.1", "Síndrome cervicobraquial", GrupoColuna),
        new("M51.1", "Transtornos de disco lombar com radiculopatia", GrupoColuna),
        new("M47.9", "Espondilose não especificada", GrupoColuna),
        new("M25.5", "Dor articular", GrupoColuna),
        new("M75.0", "Capsulite adesiva do ombro", GrupoColuna),
        new("M77.1", "Epicondilite lateral", GrupoColuna),
        new("M17.9", "Gonartrose não especificada", GrupoColuna),
        new("M16.9", "Coxartrose não especificada", GrupoColuna),
        new("M79.7", "Fibromialgia", GrupoColuna),
        new("M62.8", "Outras afecções musculares especificadas", GrupoColuna),
        new("M81.9", "Osteoporose não especificada, sem fratura patológica", GrupoColuna),
        new("R52.2", "Outra dor crônica", GrupoColuna),

        // ---- Neurologia e neurocirurgia ----
        new("G43.9", "Enxaqueca não especificada", GrupoNeuro),
        new("G44.2", "Cefaleia tensional", GrupoNeuro),
        new("G54.0", "Transtornos do plexo braquial", GrupoNeuro),
        new("G56.0", "Síndrome do túnel do carpo", GrupoNeuro),
        new("G62.9", "Polineuropatia não especificada", GrupoNeuro),
        new("G47.0", "Distúrbios do início e da manutenção do sono", GrupoNeuro),

        // ---- Saúde mental (psiquiatria) ----
        new("F32.9", "Episódio depressivo não especificado", GrupoMental),
        new("F33.9", "Transtorno depressivo recorrente não especificado", GrupoMental),
        new("F41.1", "Ansiedade generalizada", GrupoMental),
        new("F41.0", "Transtorno de pânico", GrupoMental),
        new("F40.1", "Fobias sociais", GrupoMental),
        new("F43.1", "Estado de estresse pós-traumático", GrupoMental),
        new("F43.2", "Transtornos de adaptação", GrupoMental),
        new("F51.0", "Insônia não orgânica", GrupoMental),
        new("F45.4", "Transtorno doloroso persistente somatoforme", GrupoMental),
        new("F31.9", "Transtorno afetivo bipolar não especificado", GrupoMental),

        // ---- Endocrinologia e metabolismo ----
        new("E11.9", "Diabetes mellitus tipo 2 sem complicações", GrupoEndocrino),
        new("E10.9", "Diabetes mellitus tipo 1 sem complicações", GrupoEndocrino),
        new("E66.9", "Obesidade não especificada", GrupoEndocrino),
        new("E03.9", "Hipotireoidismo não especificado", GrupoEndocrino),
        new("E05.9", "Tireotoxicose não especificada", GrupoEndocrino),
        new("E04.9", "Bócio não tóxico não especificado", GrupoEndocrino),
        new("E78.5", "Hiperlipidemia não especificada", GrupoEndocrino),

        // ---- Geriatria ----
        new("G30.9", "Doença de Alzheimer não especificada", GrupoGeriatria),
        new("F03", "Demência não especificada", GrupoGeriatria),
        new("R26.8", "Outras anormalidades da marcha e da mobilidade", GrupoGeriatria),
        new("R54", "Senilidade", GrupoGeriatria),

        // ---- Geral ----
        new("I10", "Hipertensão essencial (primária)", GrupoGeral),
        new("R53", "Mal-estar e fadiga", GrupoGeral),
        new("K58.9", "Síndrome do intestino irritável sem diarreia", GrupoGeral),
        new("N39.0", "Infecção do trato urinário de localização não especificada", GrupoGeral),
        new("Z00.0", "Exame médico geral", GrupoGeral)
    ];

    /// <summary>
    /// Busca por CÓDIGO ou por PALAVRA da descrição — as duas formas de procurar, porque
    /// quem lembra o código digita o código e quem não lembra digita "lombar".
    ///
    /// O código casa por PREFIXO (digitar "M54" traz os seis da família) e a descrição por
    /// trecho; sem termo, devolve tudo, que é o que faz a janela abrir mostrando a lista
    /// em vez de uma caixa vazia (a lição da parcela 37: tela que abre vazia é tela
    /// inacabada).
    /// </summary>
    public static IReadOnlyList<ItemCid> Buscar(string? termo)
    {
        if (string.IsNullOrWhiteSpace(termo)) return Todos;

        var t = Normalizar(termo);

        return Todos
            .Where(c => Normalizar(c.Codigo).StartsWith(t, StringComparison.Ordinal)
                        || Normalizar(c.Descricao).Contains(t, StringComparison.Ordinal))
            .ToList();
    }

    /// <summary>
    /// O que este código quer dizer, ou nulo quando ele não está no atalho.
    ///
    /// É a metade que pega o erro de digitação: com a descrição ao lado do campo, "M54.4"
    /// digitado no lugar de "M54.5" deixa de ser um código plausível e passa a dizer
    /// "Lumbago com ciática" para um paciente que tem lombalgia simples.
    ///
    /// Nulo NÃO é erro — é "não está na lista da clínica", e a tela precisa dizer isso
    /// sem parecer recusa: a CID-10 tem 14 mil códigos e este atalho tem cinquenta.
    /// </summary>
    public static string? Descrever(string? codigo)
    {
        if (string.IsNullOrWhiteSpace(codigo)) return null;

        var alvo = Normalizar(codigo);
        return Todos.FirstOrDefault(c => Normalizar(c.Codigo) == alvo)?.Descricao;
    }

    /// <summary>
    /// Sem acento, em maiúsculas e sem o ponto do código.
    ///
    /// O ponto sai porque a mesma pessoa digita "M545" e "M54.5" — e as duas se referem ao
    /// mesmo código. Fazer a busca depender dele obrigaria a acertar a pontuação de uma
    /// classificação para encontrar o item que existe justamente para não ter de decorá-la.
    /// </summary>
    private static string Normalizar(string texto)
    {
        var span = texto.Trim().ToUpperInvariant().Replace(".", string.Empty);

        var sb = new System.Text.StringBuilder(span.Length);
        foreach (var c in span.Normalize(System.Text.NormalizationForm.FormD))
            if (System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c)
                != System.Globalization.UnicodeCategory.NonSpacingMark)
                sb.Append(c);

        return sb.ToString();
    }
}
