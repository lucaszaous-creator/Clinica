using Clinica.Application.Servicos;

namespace Clinica.Application.Modelos;

/// <summary>Uma linha da barra "por quem assina".</summary>
/// <param name="Nome">Quem assinou, ou "balcão (sem assinatura)".</param>
/// <param name="Quantidade">Quantos papéis.</param>
/// <param name="Fracao">
/// O tamanho da barra, de 0 a 1, NORMALIZADO pela maior linha — o DataTemplate não enxerga
/// os irmãos da série (a regra dos gráficos do projeto). Fração sobre o TOTAL faria a
/// maior barra de uma clínica com cinco profissionais nunca passar de um quinto da largura,
/// e o que se compara aqui é um profissional com o outro.
/// </param>
public sealed record QuemAssina(string Nome, int Quantidade, double Fracao)
{
    /// <summary>
    /// Os dois nomes que o <c>ItemBarraRotulada</c> do design system espera. Ele já
    /// resolve o desenho da linha (nome à esquerda, número à direita, barra embaixo, com o
    /// número ESCRITO ao lado para quem não distingue cores) — desenhar outra barra aqui
    /// seria a segunda definição que diverge na primeira correção.
    /// </summary>
    public string Rotulo => Nome;

    public string ValorRotulo => Quantidade.ToString();
}

/// <summary>
/// O que a DIREÇÃO pergunta sobre os papéis que saíram (set/2026, tela 4 do mockup
/// aprovado "documentos nas quatro telas").
///
/// A direção não emite receita. Até aqui a única tela de documentos da suíte era a do
/// BALCÃO — com os cartões de emitir, o seletor de paciente e a régua de folhas —, e quem
/// só queria saber quantos papéis saíram, quem assinou e quantos foram cancelados
/// atravessava uma tela inteira de operação para chegar a uma lista.
///
/// ⚠️ Mora na Application porque decide o que a tela AFIRMA — "Receituário, 31%" e
/// "6 cancelados" são números que a direção usa —, e regra assim mora onde o
/// <c>dotnet test</c> alcança (a regra da <c>GradeSemana</c> e do
/// <c>ResumoSessaoAnterior</c>).
/// </summary>
/// <param name="Emitidos">
/// Quantos papéis saíram no período, CANCELADOS INCLUSIVE. O número foi gasto: a
/// numeração por ano não se reaproveita, e esconder o cancelado daqui faria a contagem
/// não bater com a sequência dos números.
/// </param>
/// <param name="Assinados">Quantos foram selados com e-CPF.</param>
/// <param name="Cancelados">Quantos daqueles foram cancelados, sempre com motivo escrito.</param>
/// <param name="NoAr">Quantos têm link público válido HOJE.</param>
/// <param name="MaisEmitido">
/// A folha mais emitida e a fatia dela, por extenso. Vazio quando não houve emissão —
/// escrever "— 0%" daria um número onde não há pergunta a responder.
/// </param>
/// <param name="PorQuemAssina">Quem assinou o quê, da maior para a menor.</param>
/// <param name="Canceladas">As canceladas do período, da mais recente para a mais antiga.</param>
public sealed record PainelDeDocumentos(
    int Emitidos,
    int Assinados,
    int Cancelados,
    int NoAr,
    string MaisEmitido,
    IReadOnlyList<QuemAssina> PorQuemAssina,
    IReadOnlyList<FolhaEmitida> Canceladas)
{
    /// <summary>O rótulo de quem não assina papel nenhum — o balcão.</summary>
    public const string SemAssinatura = "balcão (sem assinatura)";

    /// <summary>Não houve emissão no período. A tela troca de frase em vez de mostrar zeros.</summary>
    public bool Vazio => Emitidos == 0;

    /// <summary>
    /// Monta o painel a partir das folhas do período.
    /// </summary>
    /// <param name="folhas">O que saiu, já filtrado pelo acesso de quem está lendo.</param>
    /// <param name="hoje">
    /// Para decidir o que está no ar. Por parâmetro para o número ser testável — e o dia do
    /// vencimento conta INTEIRO: quem publicou com prazo até 09/10 espera que a receita
    /// abra no dia 09, não que ela caia na virada.
    /// </param>
    public static PainelDeDocumentos Montar(IReadOnlyList<FolhaEmitida> folhas, DateOnly hoje)
    {
        var canceladas = folhas.Where(f => f.Cancelado).ToList();

        var porFolha = folhas
            .GroupBy(f => f.FolhaRotulo)
            .Select(g => (Rotulo: g.Key, Quantos: g.Count()))
            // Desempate pelo NOME: sem ele, duas folhas com a mesma contagem trocariam de
            // lugar entre duas leituras do mesmo período, e a direção veria o "mais
            // emitido" mudar sem nada ter mudado.
            .OrderByDescending(x => x.Quantos).ThenBy(x => x.Rotulo)
            .ToList();

        var maisEmitido = porFolha.Count == 0 || folhas.Count == 0
            ? string.Empty
            : $"{porFolha[0].Rotulo} — {porFolha[0].Quantos * 100 / folhas.Count}%";

        var grupos = folhas
            .GroupBy(f => string.IsNullOrWhiteSpace(f.Profissional) ? SemAssinatura : f.Profissional!)
            .Select(g => (Nome: g.Key, Quantos: g.Count()))
            .OrderByDescending(x => x.Quantos).ThenBy(x => x.Nome)
            .ToList();

        var maior = grupos.Count == 0 ? 0 : grupos[0].Quantos;

        return new PainelDeDocumentos(
            folhas.Count,
            folhas.Count(f => f.Assinado),
            canceladas.Count,
            folhas.Count(f => f.PublicadoAte is { } ate && ate >= hoje),
            maisEmitido,
            grupos.Select(g => new QuemAssina(
                g.Nome, g.Quantos, maior == 0 ? 0 : (double)g.Quantos / maior)).ToList(),
            canceladas);
    }
}
