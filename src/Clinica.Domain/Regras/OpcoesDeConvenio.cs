using Clinica.Domain.Entities;

namespace Clinica.Domain.Regras;

/// <summary>
/// Um convênio como as telas o OFERECEM: o nome, o que a escolha significa e onde ele entra
/// na ordem.
/// </summary>
/// <param name="Codigo">A chave gravada na ficha.</param>
/// <param name="Nome">O nome exibido, como a clínica o cadastrou.</param>
/// <param name="Familia">
/// A FAMÍLIA de regra de faturamento. Viaja junto porque é o outro campo que a ficha grava
/// (<c>Paciente.Convenio</c>): sem ela a tela teria de voltar ao catálogo para escrever, e
/// uma segunda resolução é uma segunda chance de escrever a família errada.
/// </param>
/// <param name="Explicacao">
/// O que a escolha significa para a guia e para o dinheiro — dito na linha, porque é a única
/// diferença que a recepcionista precisa enxergar ali.
/// </param>
/// <param name="GeraGuia">Gera guia para faturar. Falso = o paciente paga a sessão.</param>
/// <param name="EhADefinir">
/// É a PERGUNTA, não uma resposta: a ficha veio do sistema anterior sem convênio. Vai para o
/// fim da lista e nunca é o padrão de ninguém.
/// </param>
public sealed record OpcaoDeConvenio(
    string Codigo, string Nome, Convenio Familia, string Explicacao, bool GeraGuia, bool EhADefinir);

/// <summary>
/// A escolha "convênio ou particular?", numa definição só (set/2026).
///
/// Por que isto existe
/// -------------------
/// A mesma escolha era feita em DOIS lugares com DUAS regras, e a do lugar mais importante
/// era a errada:
///
/// <list type="bullet">
/// <item>a JANELA de vínculo (<c>EscolhaDeConvenioWindow</c>, parcela 92) ordena os que geram
/// guia primeiro, explica o que cada opção significa, exclui o "a definir" como resposta e
/// não pré-seleciona nada — porque "um padrão que ninguém escolheu" é justamente o defeito
/// que ela existe para corrigir;</item>
/// <item>o CADASTRO do paciente — que é por onde TODO paciente novo recebe um convênio —
/// listava em ordem ALFABÉTICA, pré-selecionava o primeiro da lista e não explicava nada.</item>
/// </list>
///
/// Numa base que importou a carteira do sistema anterior, o primeiro em ordem alfabética é
/// <c>"A definir (importado sem convênio)"</c>: todo paciente cadastrado no balcão nascia com
/// um convênio que AFIRMA ter vindo de importação, que não gera guia e que acende alerta
/// vermelho na primeira vez que alguém tentar lançar a sessão dele (parcela 92). Um padrão
/// que depende da ordem alfabética não é uma decisão — é um acidente com aparência de
/// decisão, e foi metade do relato <i>"não consegui entender como fazer um atendimento
/// particular"</i>.
///
/// É a lição da parcela 64 ("o mesmo ato com duas regras: a metade sem regra é a que ninguém
/// confere") aplicada à porta principal.
/// </summary>
public static class OpcoesDeConvenio
{
    /// <summary>
    /// O que a escolha significa, em uma frase.
    ///
    /// ⚠️ Ela diz também ONDE o dinheiro entra. Sem isso a tela informa que "não gera guia"
    /// e cala sobre o que fazer em seguida — que é exatamente a dúvida de quem nunca lançou
    /// um particular.
    /// </summary>
    public static string Explicar(bool geraGuia, bool ehADefinir)
    {
        if (ehADefinir)
            return "Ninguém respondeu ainda. O balcão vê alerta vermelho e o atendimento "
                   + "NÃO pode ser lançado até alguém escolher — use só quando a resposta "
                   + "não está em mãos.";

        return geraGuia
            ? "Gera guia para o faturamento."
            : "Sem guia: o paciente paga a sessão. O valor é combinado no Finalizar, pela "
              + "tabela de preço do particular.";
    }

    /// <summary>
    /// As opções na ordem em que as telas as mostram: primeiro as OPERADORAS (que geram
    /// guia), depois o PARTICULAR, e o "a definir" por último.
    ///
    /// ⚠️ A ordem não é alfabética de propósito. São respostas de naturezas diferentes — as
    /// de cima são operadoras, a de baixo é "não tem convênio" —, e misturá-las pelo nome
    /// punha "Particular" entre a Petrobras e a Unimed, com o mesmo peso. Esta lista é
    /// justamente onde a recepcionista descobre que o particular existe (ele não existia até
    /// set/2026 — ver <see cref="ConvenioCadastro.Particular"/>).
    /// </summary>
    /// <param name="incluirADefinir">
    /// O "a definir" entra na lista. A JANELA de vínculo passa <c>false</c>: oferecê-lo como
    /// resposta devolveria uma tela de sucesso e um lançamento recusado em seguida. O
    /// CADASTRO passa <c>true</c>, por duas razões — a ficha importada precisa continuar
    /// mostrando o convênio que ela TEM (combo cujo <c>ItemsSource</c> não contém o
    /// <c>SelectedItem</c> devolve NULL pelo binding, e o Salvar apagaria a escolha), e
    /// "ainda não sei" é uma resposta legítima no balcão.
    /// </param>
    public static IReadOnlyList<OpcaoDeConvenio> Montar(
        IEnumerable<ConvenioCadastro> catalogo, bool incluirADefinir)
        => Ordenar(
            catalogo
                .Where(c => c.Ativo)
                .Select(c => Uma(c.Codigo, c.Nome, c.Familia, c.GeraGuia)),
            incluirADefinir);

    /// <summary>
    /// A mesma lista a partir do cache em memória (<see cref="CatalogoConvenios.Ativos"/>) —
    /// é o que as telas de cadastro têm à mão, sem ida ao banco.
    /// </summary>
    public static IReadOnlyList<OpcaoDeConvenio> Montar(
        IEnumerable<EntradaConvenio> catalogo, bool incluirADefinir)
        => Ordenar(
            catalogo
                .Where(c => c.Ativo)
                .Select(c => Uma(c.Codigo, c.Nome, c.Familia, c.GeraGuia)),
            incluirADefinir);

    private static OpcaoDeConvenio Uma(string codigo, string nome, Convenio familia, bool geraGuia)
    {
        var ehADefinir = string.Equals(
            codigo, ConvenioCadastro.CodigoADefinir, StringComparison.OrdinalIgnoreCase);

        return new OpcaoDeConvenio(
            codigo, nome, familia, Explicar(geraGuia, ehADefinir), geraGuia, ehADefinir);
    }

    private static IReadOnlyList<OpcaoDeConvenio> Ordenar(
        IEnumerable<OpcaoDeConvenio> opcoes, bool incluirADefinir)
        => opcoes
            .Where(o => incluirADefinir || !o.EhADefinir)
            .OrderBy(o => o.EhADefinir ? 2 : o.GeraGuia ? 0 : 1)
            .ThenBy(o => o.Nome, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
}
