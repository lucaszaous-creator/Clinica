using Clinica.Domain;
using FluentAssertions;
using Xunit;

namespace Clinica.Tests;

/// <summary>
/// O ATALHO DA CID-10 (parcela 63).
///
/// O campo <c>Cid</c> é texto livre desde que existe, em <c>DocumentoClinico</c> e em
/// <c>ProblemaPaciente</c>: a profissional digitava "M54.5" de cabeça, e um dígito trocado
/// saía impresso no atestado que vai para o RH do paciente. "M54.4" também existe — não há
/// nada no código que denuncie o erro.
///
/// ⚠️ O que este catálogo NÃO é: a CID-10 inteira (são ~14 mil códigos) nem uma validação.
/// O campo continua aceitando qualquer texto. Recusar código fora da lista seria a regra
/// apertada demais que o projeto já rejeitou no formato do número da guia: travaria o
/// atendimento sem que o profissional tivesse como contornar.
/// </summary>
public class CatalogoCidTests
{
    // ==================== A conferência que pega o dígito trocado ====================

    /// <summary>
    /// A razão de existir do catálogo: dois códigos vizinhos, ambos plausíveis, com
    /// significados diferentes. Com a descrição ao lado do campo, o erro deixa de ser
    /// invisível.
    /// </summary>
    [Fact]
    public void Descrever_distingue_codigos_vizinhos()
    {
        CatalogoCid.Descrever("M54.5").Should().Be("Dor lombar baixa");
        CatalogoCid.Descrever("M54.4").Should().Be("Lumbago com ciática");
    }

    /// <summary>
    /// Código fora da lista devolve NULO, e nulo não é erro — é "não está no atalho desta
    /// clínica". A tela mostra a descrição quando há e não diz nada quando não há; tratar
    /// a ausência como recusa transformaria um atalho de cinquenta códigos numa trava
    /// sobre uma classificação de quatorze mil.
    /// </summary>
    [Fact]
    public void Codigo_fora_da_lista_nao_e_erro_e_devolve_nulo()
    {
        CatalogoCid.Descrever("A00.9").Should().BeNull();
        CatalogoCid.Descrever("qualquer coisa").Should().BeNull();
        CatalogoCid.Descrever(null).Should().BeNull();
        CatalogoCid.Descrever("  ").Should().BeNull();
    }

    /// <summary>
    /// "M545" e "M54.5" são o mesmo código. A mesma pessoa digita dos dois jeitos, e fazer
    /// a conferência depender do ponto obrigaria a acertar a pontuação de uma classificação
    /// para encontrar o item que existe justamente para não ter de decorá-la.
    /// </summary>
    [Theory]
    [InlineData("M54.5")]
    [InlineData("M545")]
    [InlineData("m54.5")]
    [InlineData(" M54.5 ")]
    public void O_ponto_e_a_caixa_nao_atrapalham(string digitado)
        => CatalogoCid.Descrever(digitado).Should().Be("Dor lombar baixa");

    // ==================== A busca ====================

    /// <summary>
    /// As duas formas de procurar, porque são duas pessoas diferentes: quem lembra o
    /// código digita o código, quem não lembra digita a palavra.
    /// </summary>
    [Fact]
    public void Busca_por_codigo_e_por_palavra()
    {
        CatalogoCid.Buscar("M54").Should().OnlyContain(c => c.Codigo.StartsWith("M54"));
        CatalogoCid.Buscar("M54").Should().HaveCountGreaterThan(1,
            "o prefixo traz a família inteira — é assim que se escolhe entre M54.4 e M54.5");

        CatalogoCid.Buscar("lombar").Should().Contain(c => c.Codigo == "M54.5");
        CatalogoCid.Buscar("ansiedade").Should().Contain(c => c.Codigo == "F41.1");
    }

    /// <summary>
    /// A busca ignora acento: "depressao" acha "Episódio depressivo". Quem digita rápido
    /// não acentua, e uma busca que exige o acento devolve vazio para um termo certo.
    /// </summary>
    [Fact]
    public void Busca_ignora_acento()
        => CatalogoCid.Buscar("episodio").Should().Contain(c => c.Codigo == "F32.9");

    /// <summary>
    /// Sem termo, devolve TUDO — é o que faz a janela abrir mostrando a lista em vez de
    /// uma caixa de busca vazia. Tela que abre vazia é tela inacabada (parcela 37).
    /// </summary>
    [Fact]
    public void Sem_termo_devolve_a_lista_inteira()
    {
        CatalogoCid.Buscar(null).Should().HaveCount(CatalogoCid.Todos.Count);
        CatalogoCid.Buscar("   ").Should().HaveCount(CatalogoCid.Todos.Count);
    }

    [Fact]
    public void Termo_sem_resultado_devolve_vazio_e_nao_a_lista_toda()
        => CatalogoCid.Buscar("zzzzz").Should().BeEmpty(
            "devolver tudo faria a tela parecer que o filtro não pegou — o mesmo defeito "
            + "do filtro de folha desconhecida da central de documentos");

    // ==================== A integridade do catálogo ====================

    /// <summary>
    /// Código repetido faria <c>Descrever</c> escolher um dos dois sem dizer qual — e a
    /// descrição errada ao lado do campo é pior do que descrição nenhuma, porque a pessoa
    /// confia nela para conferir.
    /// </summary>
    [Fact]
    public void Nenhum_codigo_se_repete()
        => CatalogoCid.Todos.Select(c => c.Codigo).Should().OnlyHaveUniqueItems();

    /// <summary>
    /// Toda linha tem código, descrição e grupo. O grupo é o que agrupa a janela; uma
    /// linha sem ele cairia num agrupamento em branco.
    /// </summary>
    [Fact]
    public void Toda_linha_esta_completa()
        => CatalogoCid.Todos.Should().OnlyContain(c =>
            !string.IsNullOrWhiteSpace(c.Codigo)
            && !string.IsNullOrWhiteSpace(c.Descricao)
            && !string.IsNullOrWhiteSpace(c.Grupo));

    /// <summary>
    /// Todo código do catálogo se acha por ele mesmo — a garantia de que a normalização
    /// da busca e a da conferência não divergiram.
    /// </summary>
    [Fact]
    public void Todo_codigo_do_catalogo_e_encontrado_por_ele_mesmo()
    {
        foreach (var c in CatalogoCid.Todos)
        {
            CatalogoCid.Descrever(c.Codigo).Should().Be(c.Descricao);
            CatalogoCid.Buscar(c.Codigo).Should().Contain(x => x.Codigo == c.Codigo);
        }
    }

    /// <summary>
    /// As cinco especialidades da casa estão cobertas. Um catálogo que só servisse à
    /// acupuntura faria o psiquiatra e o endocrinologista continuarem digitando de cabeça
    /// — e um atalho que serve a metade da equipe é lido como quebrado pela outra metade.
    /// </summary>
    [Fact]
    public void As_especialidades_da_casa_estao_cobertas()
        => CatalogoCid.Todos.Select(c => c.Grupo).Distinct().Should().Contain(
        [
            CatalogoCid.GrupoColuna,
            CatalogoCid.GrupoNeuro,
            CatalogoCid.GrupoMental,
            CatalogoCid.GrupoEndocrino,
            CatalogoCid.GrupoGeriatria
        ]);
}
