using Clinica.Application.Modelos;
using FluentAssertions;
using Xunit;

namespace Clinica.Tests;

/// <summary>
/// O QUE A TELA DE PACIENTES AFIRMA (parcela 88, 3ª rodada).
///
/// Estas frases decidem o que a pessoa faz a seguir, e a pior delas manda cadastrar de novo
/// um paciente que já tem ficha — partindo o histórico em dois (parcela 57). Enquanto elas
/// moravam na ViewModel, nenhum teste as alcançava: projeto WPF não compila aqui.
/// </summary>
public class ResumoDaCarteiraTests
{
    private const int Teto = 200;
    private const int Destaque = 45;

    private static ResumoDaCarteira Montar(
        int lidos, int mostrados, string? termo = null, bool somenteSumidos = false,
        int teto = Teto)
        => ResumoDaCarteira.Montar(lidos, mostrados, termo, somenteSumidos, teto, Destaque);

    [Fact]
    public void Sem_busca_o_resumo_diz_que_a_carteira_e_da_CLINICA()
    {
        Montar(lidos: 8, mostrados: 8).Resumo
            .Should().Contain("8 paciente(s) atendidos na clínica")
            .And.NotContain("sua carteira", "não existe \"meu paciente\"");
    }

    /// <summary>
    /// ⚠️ O TETO É DITO. Numa clínica de verdade a lista bate no limite todo dia — e uma
    /// lista cortada que se anuncia como a carteira inteira faz quem não achou alguém
    /// concluir que ele não está cadastrado. Corte silencioso é o que este projeto recusa.
    /// </summary>
    [Fact]
    public void O_teto_atingido_e_DITO_e_a_saida_e_a_busca()
    {
        var batendo = Montar(lidos: Teto, mostrados: Teto).Resumo;

        batendo.Should().Contain($"os {Teto} mais recentes")
            .And.Contain("teto desta tela")
            .And.Contain("Busque pelo nome ou CPF", Exactly.Once());

        Montar(lidos: Teto - 1, mostrados: Teto - 1).Resumo
            .Should().NotContain("teto", "abaixo do limite não há corte para anunciar");
    }

    [Fact]
    public void Com_busca_o_resumo_diz_que_esta_filtrado()
        => Montar(lidos: 2, mostrados: 2, termo: "Joana").Resumo
            .Should().Contain("Joana").And.Contain("2 paciente(s)");

    // ================= os TRÊS vazios, que são a razão de este arquivo existir =========

    [Fact]
    public void Sem_busca_e_sem_ninguem_o_vazio_explica_como_alguem_ENTRA_na_lista()
    {
        var vazio = Montar(lidos: 0, mostrados: 0);

        vazio.TituloVazio.Should().Be("Nenhum paciente em tratamento");
        vazio.DescricaoVazio.Should().Contain("presença confirmada na recepção");
    }

    /// <summary>
    /// ⚠️ O vazio mais caro do sistema. Responder "entra aqui quem já foi atendido" a quem
    /// digitou um nome faz concluir que o paciente EXISTE e só não foi atendido — quando o
    /// que o sistema está dizendo é que ele NÃO ESTÁ CADASTRADO. É essa leitura errada que
    /// leva a cadastrar quem já tem ficha.
    /// </summary>
    [Fact]
    public void Busca_sem_resultado_manda_conferir_a_GRAFIA_antes_de_cadastrar_de_novo()
    {
        var vazio = Montar(lidos: 0, mostrados: 0, termo: "Zulmira");

        vazio.TituloVazio.Should().Contain("nome ou CPF");
        vazio.DescricaoVazio.Should().Contain("Zulmira")
            .And.Contain("nem entre quem ainda não veio",
                "a busca alcança o cadastro inteiro — inclusive a primeira consulta")
            .And.Contain("Confira a grafia");
        vazio.DescricaoVazio.Should().NotContain("presença confirmada",
            "esse texto explica como se ENTRA na lista, e não é a pergunta de quem buscou");
    }

    /// <summary>
    /// ⚠️ QUEM ESCONDEU RESPONDE POR SI. A busca trouxe gente e o "só quem sumiu" a
    /// escondeu: dizer "não achei ninguém no cadastro" mentiria sobre a causa e mandaria
    /// cadastrar de novo alguém que a tela ACABOU de achar.
    /// </summary>
    [Fact]
    public void O_recorte_de_sumidos_responde_ANTES_da_busca_quando_foi_ele_que_escondeu()
    {
        var vazio = Montar(lidos: 3, mostrados: 0, termo: "Joana", somenteSumidos: true);

        vazio.TituloVazio.Should().Be("Ninguém está sem vir há mais tempo");
        vazio.DescricaoVazio.Should().Contain("Desmarque")
            .And.NotContain("Confira a grafia",
                "não é a busca que falhou — ela trouxe 3 pessoas");
    }

    /// <summary>
    /// ⚠️ E a frase do recorte NÃO AFIRMA que todos vieram há pouco: quem a busca alcança
    /// pode nunca ter vindo (a primeira consulta), e essa pessoa também não é "sumida".
    /// Afirmar "vieram nos últimos 45 dias" sobre ela seria inventar uma sessão.
    /// </summary>
    [Fact]
    public void A_frase_do_recorte_cobre_quem_NUNCA_veio()
        => Montar(lidos: 1, mostrados: 0, termo: "Pedro", somenteSumidos: true)
            .DescricaoVazio.Should().Contain("ainda não têm sessão registrada");

    [Fact]
    public void Termo_em_branco_nao_conta_como_busca()
        => Montar(lidos: 0, mostrados: 0, termo: "   ").TituloVazio
            .Should().Be("Nenhum paciente em tratamento");
}
