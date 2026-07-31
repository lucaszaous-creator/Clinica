using Clinica.Infrastructure;
using FluentAssertions;
using Xunit;

namespace Clinica.Tests;

/// <summary>
/// O que o balcão lê quando a gravação é recusada.
///
/// Até aqui o texto cru do Postgres chegava à tela — <c>23505: duplicate key value
/// violates unique constraint "IX_Metas_..."</c> —, e para quem está com o paciente na
/// frente isso não é informação, é susto.
///
/// O que estes testes protegem é a **ORDEM das regras**, que é o que erra sozinho: um
/// `Contains` genérico colocado cedo demais engole os específicos que vêm depois. O
/// resultado seria uma mensagem plausível e errada — pior que o texto cru, porque
/// ninguém desconfia dela.
/// </summary>
public class MensagensDeErroTests
{
    [Theory]
    [InlineData("IX_Usuarios_Login", "login")]
    [InlineData("IX_Salas_Nome", "sala")]
    [InlineData("IX_Metas_Ano_Mes_Indicador_ProfissionalId", "meta")]
    [InlineData("IX_Orcamentos_Ano_Mes_CategoriaFinanceiraId", "teto de gasto")]
    [InlineData("IX_Lancamentos_OrigemRecorrencia", "recorrência")]
    [InlineData("IX_Contatos_Tipo_Origem", "mensagem")]
    public void Cada_indice_conhecido_explica_o_proprio_caso(string indice, string trecho)
        => MensagensDeErro.Duplicidade(indice)
            .Should().ContainEquivalentOf(trecho);

    /// <summary>
    /// `IX_Lotes_Numero` e `IX_DocumentosClinicos_Numero` contêm os dois a palavra
    /// "Numero" — e é exatamente aí que a regra genérica engoliria a específica.
    /// </summary>
    [Fact]
    public void Regra_generica_de_numero_nao_engole_a_do_lote()
    {
        MensagensDeErro.Duplicidade("IX_Lotes_Numero")
            .Should().ContainEquivalentOf("lote");

        MensagensDeErro.Duplicidade("IX_DocumentosClinicos_Numero")
            .Should().ContainEquivalentOf("documento")
            .And.NotContainEquivalentOf("lote");
    }

    /// <summary>
    /// Índice desconhecido não some nem mente: diz que é DUPLICIDADE — que é a única
    /// coisa que se sabe com certeza — e carrega o nome do índice para quem der suporte.
    /// </summary>
    [Fact]
    public void Indice_desconhecido_ainda_diz_que_e_duplicidade()
    {
        var m = MensagensDeErro.Duplicidade("IX_TabelaQueAindaNaoExiste_Coluna");

        m.Should().ContainEquivalentOf("já existe");
        m.Should().Contain("IX_TabelaQueAindaNaoExiste_Coluna",
            "o nome do índice é o que permite a alguém descobrir o caso novo");
    }

    [Fact]
    public void Sem_nome_de_indice_a_mensagem_continua_util()
        => MensagensDeErro.Duplicidade(null)
            .Should().ContainEquivalentOf("já existe");

    /// <summary>
    /// A mensagem de conexão não manda "tentar de novo" e para por aí: o
    /// `EnableRetryOnFailure` já tentou três vezes antes de a exceção chegar à tela, e
    /// pedir a quarta mandaria a pessoa repetir o que já falhou. Ela diz o que resolve
    /// (olhar a internet) e o que NÃO aconteceu (nada foi gravado) — sem a segunda parte
    /// a secretária relança o pagamento e a clínica fica com dois.
    /// </summary>
    [Fact]
    public void Mensagem_de_conexao_diz_o_que_fazer_e_que_nada_foi_gravado()
    {
        MensagensDeErro.SemConexao.Should().ContainEquivalentOf("internet");
        MensagensDeErro.SemConexao.Should().ContainEquivalentOf("nada foi gravado");
    }
}
