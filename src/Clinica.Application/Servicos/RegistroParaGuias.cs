using Clinica.Domain.Entities;
using Clinica.Domain.Regras;

namespace Clinica.Application.Servicos;

/// <summary>
/// O registro do profissional como a GUIA de convênio o pede (padrão ANS/TISS): conselho, número,
/// UF e CBO, cada um pelo CÓDIGO do padrão. Qualquer parte pode faltar — nulo é "não há".
/// </summary>
public sealed record RegistroParaGuias(string? Conselho, string? Numero, string? Uf, string? Cbos)
{
    public static readonly RegistroParaGuias Vazio = new(null, null, null, null);

    /// <summary>Nenhuma das quatro partes está preenchida.</summary>
    public bool EmBranco => Conselho is null && Numero is null && Uf is null && Cbos is null;

    /// <summary>As quatro estão: a guia sai sem ninguém digitar o executante.</summary>
    public bool Completo => Conselho is not null && Numero is not null && Uf is not null && Cbos is not null;
}

/// <summary>
/// As regras do "Registro para guias de convênio" do cadastro do profissional (nov/2026, decisão
/// da direção: conselho, número, UF e CBO são dados do PROFISSIONAL, não de cada guia). Mora
/// aqui, puro e com teste, o que a tela e o serviço precisam decidir: o que é válido, o que se
/// lê do registro em texto livre para PROPOR, e de onde sai cada campo do executante na guia.
/// O texto livre (<see cref="Profissional.RegistroConselho"/>) continua valendo nos documentos
/// clínicos — nada aqui o altera.
/// </summary>
public static class RegistroDoProfissionalParaGuias
{
    /// <summary>Onde isto se cadastra, escrito do mesmo jeito em todo aviso que manda a pessoa para lá.</summary>
    public const string OndeCadastrar =
        "Profissionais e salas → editar o profissional → \"Registro para guias de convênio (padrão ANS)\"";

    public const int TamanhoDoNumero = 15;

    /// <summary>As UFs em ordem de SIGLA — é pela sigla que a pessoa procura na lista.</summary>
    public static readonly IReadOnlyList<ValorTiss> UfsPorSigla =
        DominiosTiss.Uf.OrderBy(v => v.Termo, StringComparer.Ordinal).ToList();

    /// <summary>
    /// Os CBOs em ordem de NOME. Os termos do padrão que começam com "- " (como a ANS os publica)
    /// entram pela palavra, não pelo traço — senão quinze ocupações ficariam antes do "A".
    /// </summary>
    public static readonly IReadOnlyList<ValorTiss> CbosPorNome =
        DominiosTiss.Cbos.OrderBy(v => NomeDoCbo(v), StringComparer.Create(new System.Globalization.CultureInfo("pt-BR"), ignoreCase: true)).ToList();

    /// <summary>O nome do CBO sem o traço inicial e com a primeira letra maiúscula: "Arteterapeuta".</summary>
    public static string NomeDoCbo(ValorTiss cbo)
    {
        var nome = cbo.Termo.TrimStart('-', ' ');
        return nome.Length == 0 ? cbo.Codigo : char.ToUpperInvariant(nome[0]) + nome[1..];
    }

    /// <summary>Como o CBO aparece na lista: o nome primeiro (é por ele que se acha), o código depois.</summary>
    public static string RotuloDoCbo(ValorTiss cbo) => $"{NomeDoCbo(cbo)} ({cbo.Codigo})";

    /// <summary>O que está no cadastro, com o que é vazio virando nulo.</summary>
    public static RegistroParaGuias Do(Profissional? p) => p is null
        ? RegistroParaGuias.Vazio
        : new RegistroParaGuias(Limpo(p.ConselhoProfissional), LimparNumero(p.NumeroConselho), Limpo(p.UfConselho), Limpo(p.Cbos));

    /// <summary>
    /// O número como a operadora o recebe: sem espaço, ponto, traço nem barra, em maiúsculas
    /// ("52.1234-5" → "5212345", "12345-f" → "12345F"). Nulo quando não sobra nada.
    /// </summary>
    public static string? LimparNumero(string? numero)
    {
        if (string.IsNullOrWhiteSpace(numero)) return null;
        var limpo = new string(numero.Where(c => c is not (' ' or '.' or '-' or '/')).ToArray()).ToUpperInvariant();
        return limpo.Length == 0 ? null : limpo;
    }

    /// <summary>
    /// O que impede GRAVAR o registro, campo → frase (os nomes são os das propriedades do
    /// <see cref="Profissional"/>). Vazio = pode. Campo em branco nunca é erro: os quatro são
    /// opcionais e podem ser preenchidos aos poucos. Só se recusa o que a operadora recusaria —
    /// código fora da tabela do padrão e número que não cabe ou tem símbolo.
    /// </summary>
    public static IReadOnlyDictionary<string, string> Criticar(string? conselho, string? numero, string? uf, string? cbos)
    {
        var erros = new Dictionary<string, string>();
        if (Limpo(conselho) is { } c && !DominiosTiss.Contem(DominiosTiss.ConselhoProfissional, c))
            erros[nameof(Profissional.ConselhoProfissional)] = "Conselho fora da tabela do padrão ANS. Escolha na lista.";
        if (Limpo(uf) is { } u && !DominiosTiss.Contem(DominiosTiss.Uf, u))
            erros[nameof(Profissional.UfConselho)] = "UF fora da tabela do padrão ANS. Escolha na lista.";
        if (Limpo(cbos) is { } o && !DominiosTiss.Contem(DominiosTiss.Cbos, o))
            erros[nameof(Profissional.Cbos)] = "CBO fora da tabela do padrão ANS. Escolha na lista.";
        if (LimparNumero(numero) is { } n)
        {
            if (!n.All(char.IsLetterOrDigit))
                erros[nameof(Profissional.NumeroConselho)] = "O número no conselho leva só letras e números.";
            else if (n.Length > TamanhoDoNumero)
                erros[nameof(Profissional.NumeroConselho)] = $"O número no conselho tem até {TamanhoDoNumero} caracteres.";
        }
        return erros;
    }

    /// <summary>
    /// A PROPOSTA a partir do registro em texto livre: só existe quando conselho, número e UF
    /// estão os TRÊS em branco (com um deles preenchido a pessoa já está cadastrando, e propor por
    /// cima seria discutir com ela) e o texto deixa ler alguma coisa. O CBO nunca vem do texto —
    /// ele não está escrito lá. Devolve nulo quando não há o que propor. Nada é gravado aqui.
    /// </summary>
    public static RegistroParaGuias? PropostaDoTextoLivre(string? registroLivre, string? conselho, string? numero, string? uf)
    {
        if (Limpo(conselho) is not null || LimparNumero(numero) is not null || Limpo(uf) is not null) return null;
        var (c, n, u) = GuiaAnsNaBaixa.LerRegistroConselho(registroLivre);
        return c is null && n is null && u is null ? null : new RegistroParaGuias(c, n, u, null);
    }

    /// <summary>A frase que oferece a proposta: diz o que foi lido, de onde, e que nada foi gravado.</summary>
    public static string FraseDaProposta(RegistroParaGuias proposta, string? registroLivre)
    {
        var partes = new List<string>();
        if (proposta.Conselho is { } c) partes.Add($"conselho {DominiosTiss.Termo(DominiosTiss.ConselhoProfissional, c)}");
        if (proposta.Numero is { } n) partes.Add($"número {n}");
        if (proposta.Uf is { } u) partes.Add($"UF {DominiosTiss.Termo(DominiosTiss.Uf, u)}");
        return $"Li de \"{registroLivre?.Trim()}\": {Juntar(partes)}. Use se estiver certo e confira antes de salvar — o CBO é sempre escolhido à mão.";
    }

    /// <summary>
    /// O executante da GUIA, campo a campo, na ordem das fontes (nov/2026):
    /// 1º o CADASTRO do profissional; 2º a ÚLTIMA GUIA conferida dele; 3º o registro em TEXTO
    /// LIVRE (que nunca dá o CBO). A mistura é por campo: cadastro com CBO e sem número fica com
    /// o CBO do cadastro e busca só o número nas outras fontes — fonte mais fraca nunca passa por
    /// cima de fonte mais forte.
    /// </summary>
    /// <returns>O que se propõe e o AVISO (nulo quando o cadastro deu os quatro).</returns>
    public static (RegistroParaGuias Proposto, string? Aviso) ParaAGuia(
        Profissional profissional, GuiaTissConsulta? ultimaGuia)
    {
        var cadastro = Do(profissional);
        if (cadastro.Completo) return (cadastro, null);

        var (conselhoLido, numeroLido, ufLida) = GuiaAnsNaBaixa.LerRegistroConselho(profissional.RegistroConselho);
        var daGuia = ultimaGuia is null
            ? RegistroParaGuias.Vazio
            : new RegistroParaGuias(Limpo(ultimaGuia.ConselhoProfissional), Limpo(ultimaGuia.NumeroConselho),
                Limpo(ultimaGuia.UfConselho), Limpo(ultimaGuia.Cbos));

        var proposto = new RegistroParaGuias(
            cadastro.Conselho ?? daGuia.Conselho ?? conselhoLido,
            cadastro.Numero ?? daGuia.Numero ?? numeroLido,
            cadastro.Uf ?? daGuia.Uf ?? ufLida,
            cadastro.Cbos ?? daGuia.Cbos);

        // O que o CADASTRO não tem, pelo nome que a pessoa lê na tela, e de onde veio o substituto.
        var faltas = new List<(string Nome, bool Achou, bool DaGuia)>
        {
            ("conselho", proposto.Conselho is not null, daGuia.Conselho is not null),
            ("número", proposto.Numero is not null, daGuia.Numero is not null),
            ("UF", proposto.Uf is not null, daGuia.Uf is not null),
            ("CBO", proposto.Cbos is not null, daGuia.Cbos is not null),
        };
        var noCadastro = new[] { cadastro.Conselho, cadastro.Numero, cadastro.Uf, cadastro.Cbos };
        var semCadastro = faltas.Where((_, i) => noCadastro[i] is null).ToList();

        var daUltimaGuia = semCadastro.Where(f => f.Achou && f.DaGuia).Select(f => f.Nome).ToList();
        var doTexto = semCadastro.Where(f => f.Achou && !f.DaGuia).Select(f => f.Nome).ToList();
        var emBranco = semCadastro.Where(f => !f.Achou).Select(f => f.Nome).ToList();

        var frases = new List<string> { $"O cadastro de {profissional.Nome.Trim()} está sem {Juntar(semCadastro.Select(f => f.Nome).ToList())} para guias" };
        if (daUltimaGuia.Count > 0) frases.Add($"{Juntar(daUltimaGuia)} {Veio(daUltimaGuia.Count)} da última guia conferida dele");
        if (doTexto.Count > 0) frases.Add($"{Juntar(doTexto)} {FoiLido(doTexto.Count)} do registro em texto (\"{profissional.RegistroConselho?.Trim()}\")");
        if (emBranco.Count > 0) frases.Add($"informe {Juntar(emBranco)}");
        return (proposto, $"{string.Join("; ", frases)}. Confira com o portal. Para não repetir a cada guia, cadastre em {OndeCadastrar}.");
    }

    private static string Veio(int quantos) => quantos == 1 ? "veio" : "vieram";
    private static string FoiLido(int quantos) => quantos == 1 ? "foi lido" : "foram lidos";

    /// <summary>"conselho", "conselho e UF", "conselho, número e UF".</summary>
    private static string Juntar(IReadOnlyList<string> itens) => itens.Count switch
    {
        0 => string.Empty,
        1 => itens[0],
        _ => $"{string.Join(", ", itens.Take(itens.Count - 1))} e {itens[^1]}"
    };

    private static string? Limpo(string? texto) => string.IsNullOrWhiteSpace(texto) ? null : texto.Trim();
}
