namespace Clinica.Domain.Regras;

/// <summary>Uma entrada do catálogo de convênios (dado de referência carregado do banco).</summary>
public sealed record EntradaConvenio(string Codigo, string Nome, Convenio Familia, bool Ativo,
    ConfiguracaoRegraGenerica? Config = null,
    FormatoNumeroGuia FormatoNumeroGuia = FormatoNumeroGuia.SemValidacao,
    string? RegistroAnsOperadora = null,
    bool GeraGuia = true);

/// <summary>
/// Cache em memória do catálogo de convênios, para servir NOME e FAMÍLIA de forma
/// síncrona a conversores/telas (que não podem consultar o banco de forma assíncrona).
///
/// - A FAMÍLIA (enum <see cref="Convenio"/>) continua definindo a REGRA de faturamento.
/// - O catálogo permite convênios adicionais (variantes) que reutilizam uma família existente,
///   com nome próprio e podendo ser ativados/desativados.
///
/// Populado no início do app e após salvar em Configurações. Enquanto vazio, tudo cai
/// nos padrões do código (os 4 convênios embutidos), então nada quebra.
/// </summary>
public static class CatalogoConvenios
{
    // Troca atômica de um dicionário imutável (leitura concorrente sem lock).
    private static volatile IReadOnlyDictionary<string, EntradaConvenio> _porCodigo =
        new Dictionary<string, EntradaConvenio>();

    /// <summary>Substitui o catálogo em cache (chamado após carregar/salvar).</summary>
    public static void Atualizar(IEnumerable<EntradaConvenio> entradas)
        => _porCodigo = entradas.ToDictionary(e => e.Codigo, StringComparer.OrdinalIgnoreCase);

    private static EntradaConvenio? Buscar(string? codigo)
        => codigo is not null && _porCodigo.TryGetValue(codigo, out var e) ? e : null;

    /// <summary>Nome exibido do convênio pelo código; cai no padrão da família se não estiver no catálogo.</summary>
    public static string Nome(string? codigo)
        => Buscar(codigo)?.Nome ?? ConvenioInfo.NomeExibicaoPadrao(Familia(codigo));

    /// <summary>Família (regra) do convênio pelo código; se desconhecido, tenta interpretar o código como o próprio enum.</summary>
    public static Convenio Familia(string? codigo)
    {
        if (Buscar(codigo) is { } e) return e.Familia;
        return Enum.TryParse<Convenio>(codigo, out var c) ? c : Convenio.UnimedPadrao;
    }

    /// <summary>
    /// Rótulo neutro da família genérica. NÃO é operadora nenhuma: é a REGRA que todas as
    /// operadoras cadastradas pela clínica compartilham.
    /// </summary>
    public const string RotuloFamiliaPersonalizada = "Convênio personalizado";

    /// <summary>
    /// Nome de um convênio embutido (pela família), refletindo rename salvo no catálogo.
    ///
    /// ⚠️ <see cref="Convenio.Personalizado"/> é a única família que NÃO identifica uma
    /// operadora, e por isso é a única que não passa pelo catálogo aqui. O motivo saiu de
    /// produção: <c>ListarAsync</c> garante uma linha por família, inclusive uma de código
    /// <c>"Personalizado"</c> — e essa linha é RENOMEÁVEL como qualquer outra. A clínica
    /// renomeou-a para o nome da primeira operadora que cadastrou (em vez de usar
    /// "Adicionar"), e a partir daí <c>Nome(Personalizado)</c> passou a devolver o nome
    /// DELA para toda operadora personalizada: oito pacientes Sul América apareceram
    /// faturando como "Porto Saúde" na consulta de guias.
    ///
    /// O rótulo neutro é pior de ler e MUITO melhor de confiar: ele não responde qual
    /// operadora é — o que é a verdade, porque a família não sabe —, em vez de responder a
    /// operadora errada com toda a cara de certa. Quem tem o código na mão usa
    /// <see cref="Nome(string?, Convenio)"/>, que é o caminho certo e é o que as telas fazem.
    /// </summary>
    public static string Nome(Convenio familia)
        => familia == Convenio.Personalizado
            ? RotuloFamiliaPersonalizada
            : Nome(familia.ToString());

    /// <summary>
    /// Nome exibido a partir do par que TODA entidade carrega: o código (que identifica a
    /// operadora) e a família (que identifica a REGRA). Este é o jeito certo, e existe
    /// porque o jeito errado é fácil demais de escrever.
    ///
    /// Resolver só pela FAMÍLIA — <c>Nome(p.Convenio)</c> — devolve "Personalizado" para
    /// toda operadora que a clínica cadastrou, porque `Convenio.Personalizado` é a regra
    /// compartilhada por todas elas. A clínica cadastra "Sul América" em Configurações e a
    /// tela escreve "Personalizado": o nome existe no banco e a tela não o alcança.
    ///
    /// O código vence quando existe; a família é o caminho de baixo, para o embutido cujo
    /// código é o próprio nome do enum.
    ///
    /// ⚠️ Aqui o caminho de baixo passa por <see cref="Nome(string?)"/> e NÃO pelo rótulo
    /// neutro de <see cref="Nome(Convenio)"/>, e a assimetria é deliberada: sem código, a
    /// linha de catálogo da família é o melhor palpite disponível — o paciente foi mesmo
    /// cadastrado nela. Lá em cima não há palpite a dar, porque a pergunta é sobre a
    /// família e não sobre um paciente.
    /// </summary>
    public static string Nome(string? codigo, Convenio familia)
        => Nome(string.IsNullOrWhiteSpace(codigo) ? familia.ToString() : codigo);

    /// <summary>Config da regra genérica de um convênio personalizado (nulo se não for personalizado).</summary>
    public static ConfiguracaoRegraGenerica? Config(string? codigo) => Buscar(codigo)?.Config;

    /// <summary>
    /// Registro ANS da operadora deste convênio (parcela 60) — o destino do lote TISS
    /// dela. Nulo quando a clínica não preencheu: o lote cai no registro global de
    /// Configurações, que era o único que existia.
    /// </summary>
    public static string? RegistroAns(string? codigo)
        => Buscar(codigo)?.RegistroAnsOperadora is { Length: > 0 } r ? r : null;

    /// <summary>Validade da consulta pelo código: config do personalizado, ou o padrão da família.</summary>
    public static int? ValidadeConsultaDias(string? codigo)
    {
        var e = Buscar(codigo);
        if (e?.Familia == Convenio.Personalizado)
            return e.Config?.ValidadeConsultaDias;
        return ConvenioInfo.ValidadeConsultaDias(Familia(codigo));
    }

    /// <summary>
    /// Forma do número da guia deste convênio (parcela 45), para a baixa criticar o que
    /// foi digitado.
    ///
    /// A resolução tem três degraus, e o terceiro é o que separa "não configurado" de
    /// "não sei quem é":
    ///
    /// 1. Está no catálogo → vale o que a clínica marcou, inclusive "sem validação".
    ///    A escolha dela ganha de qualquer padrão nosso.
    /// 2. Não está, mas o código É uma família embutida → padrão da família. É o caso do
    ///    banco anterior ao catálogo e o dos testes: devolver "aceita tudo" faria a regra
    ///    sumir sem uma linha de erro, que é a forma mais silenciosa de uma validação
    ///    deixar de existir.
    /// 3. Código desconhecido (variante excluída do catálogo, por exemplo) → SEM
    ///    validação. Aqui não se sabe de que operadora é a guia, e chutar o formato
    ///    recusaria baixa legítima por causa de um dado que falta do NOSSO lado.
    ///    <see cref="Familia"/> devolve <c>UnimedPadrao</c> nesse caso, e usá-la aqui
    ///    exigiria número só de dígitos de um convênio que talvez nem seja Unimed.
    /// </summary>
    public static FormatoNumeroGuia FormatoDoNumeroDaGuia(string? codigo)
    {
        if (Buscar(codigo) is { } entrada) return entrada.FormatoNumeroGuia;

        return Enum.TryParse<Convenio>(codigo, out var familia)
            ? RegraNumeroGuia.PadraoDaFamilia(familia)
            : FormatoNumeroGuia.SemValidacao;
    }

    /// <summary>
    /// Este convênio gera guia para faturar? Falso = PARTICULAR (parcela 60).
    ///
    /// ⚠️ Código desconhecido devolve <b>true</b>, e a assimetria é deliberada: um convênio
    /// que não está no catálogo (variante excluída, cache ainda vazio na abertura) é
    /// presumido faturável. O erro nesse sentido é uma guia a mais para conferir; no
    /// sentido contrário, seria o sistema <b>parar de gerar guia</b> para um convênio de
    /// verdade — em silêncio, e só descoberto quando a clínica fosse faturar o mês.
    /// </summary>
    public static bool GeraGuia(string? codigo) => Buscar(codigo)?.GeraGuia ?? true;

    /// <summary>Convênios ativos (código + nome), ordenados por nome — para os combos de cadastro.</summary>
    public static IReadOnlyList<EntradaConvenio> Ativos
        => _porCodigo.Values.Where(e => e.Ativo).OrderBy(e => e.Nome).ToList();
}
