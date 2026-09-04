using Clinica.Application.Modelos;
using FluentAssertions;
using Xunit;

namespace Clinica.Tests;

/// <summary>
/// O QUE A BUSCA DE PACIENTE FAZ ANTES DE ALGUÉM DIGITAR (set/2026 — pedido da direção:
/// <i>"isso nos ajuda e minimiza o tempo de resposta entre sistema servidor"</i>).
///
/// A asserção que carrega a suíte é a primeira: <b>a tela ociosa não vai ao banco</b>.
/// Doze telas abriam chamando a busca com o termo VAZIO — e com o termo vazio a consulta
/// não filtra nada, cai no <c>OrderBy(Nome).Take(50)</c>. Era uma ida ao banco REMOTO, na
/// abertura, para trazer o começo do alfabeto de 2.238 fichas: ACELINO, ADAISE, ADAO. Não
/// é o paciente de ninguém, e é consulta paga.
///
/// ⚠️ Estes testes existem porque o <c>SeletorPacienteViewModel</c> é WPF e não compila
/// aqui. A decisão foi para a Application justamente por isso — sem ela, a regra que
/// decide se a tela consulta o banco viveria de inspeção visual.
/// </summary>
public class BuscaDePacienteTests
{
    private static ModoDaBusca Modo(
        bool semBuscaInicial = false, bool pediramLista = false, string? termo = null,
        bool temSugestao = false, bool sugestaoLigada = true)
        => BuscaDePaciente.Modo(semBuscaInicial, pediramLista, termo, temSugestao, sugestaoLigada);

    /// <summary>⚠️ O teste do pedido: abrir a tela não custa uma consulta.</summary>
    [Fact]
    public void A_tela_ociosa_NAO_vai_ao_banco()
    {
        var modo = Modo(semBuscaInicial: true);

        modo.Should().Be(ModoDaBusca.Ocioso);
        BuscaDePaciente.Consulta(modo).Should().BeFalse(
            "abrir a tela não pode custar uma ida ao banco remoto para trazer o alfabeto");
    }

    /// <summary>
    /// ⚠️ E o oposto: numa tela de LISTAGEM a lista É a resposta. Abrir vazio ali seria
    /// trocar um defeito pelo contrário — a razão de a correção ser OPT-IN.
    /// </summary>
    [Fact]
    public void Sem_o_opt_in_a_tela_continua_listando_como_sempre()
    {
        var modo = Modo(semBuscaInicial: false);

        modo.Should().Be(ModoDaBusca.Todos);
        BuscaDePaciente.Consulta(modo).Should().BeTrue();
    }

    [Fact]
    public void Digitar_tira_a_tela_do_ocioso_e_consulta()
    {
        var modo = Modo(semBuscaInicial: true, termo: "silva");

        modo.Should().Be(ModoDaBusca.PorTermo);
        BuscaDePaciente.Consulta(modo).Should().BeTrue();
    }

    /// <summary>Espaço em branco não é busca: ele não pode disparar consulta nenhuma.</summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Termo_em_branco_nao_tira_do_ocioso(string? termo)
    {
        Modo(semBuscaInicial: true, termo: termo).Should().Be(ModoDaBusca.Ocioso);
    }

    /// <summary>
    /// Clicar num chip é PEDIR: a partir daí a tela tem uma lista, e apagar o campo devolve
    /// ao modo pedido — não ao ocioso. O chip aceso sobre uma tela em branco seria a
    /// pílula mentindo sobre o que está na tela.
    /// </summary>
    [Fact]
    public void Depois_de_pedir_uma_lista_a_tela_nao_volta_ao_ocioso()
    {
        Modo(semBuscaInicial: true, pediramLista: true, temSugestao: true)
            .Should().Be(ModoDaBusca.Sugestao);

        Modo(semBuscaInicial: true, pediramLista: true, sugestaoLigada: false)
            .Should().Be(ModoDaBusca.Todos);
    }

    /// <summary>
    /// ⚠️ O TERMO VENCE OS MODOS. Com algo digitado nenhuma pílula acende, que é a
    /// verdade: nem "com horário hoje" nem "todos os pacientes" está no ar. Amarradas às
    /// flags de configuração, elas ficavam acesas sobre resultados de busca — a tela
    /// dizendo "com horário hoje" em cima de quatro resultados de "pinheiro".
    /// </summary>
    [Fact]
    public void Com_termo_digitado_nenhum_modo_de_lista_esta_no_ar()
    {
        Modo(termo: "pinheiro", temSugestao: true, sugestaoLigada: true)
            .Should().Be(ModoDaBusca.PorTermo);

        Modo(semBuscaInicial: true, pediramLista: true, termo: "pinheiro", temSugestao: true)
            .Should().Be(ModoDaBusca.PorTermo);
    }

    /// <summary>
    /// ⚠️ Sem sugestão FORNECIDA a tela cai em "todos", nunca em "sugestão": senão a
    /// pílula "Com horário hoje" acenderia sobre o alfabeto, e a lista viria do
    /// <c>OrderBy(Nome)</c> sob um rótulo que promete a agenda do dia.
    /// </summary>
    [Fact]
    public void Sem_sugestao_fornecida_a_tela_nunca_diz_que_esta_mostrando_uma()
    {
        Modo(temSugestao: false, sugestaoLigada: true).Should().Be(ModoDaBusca.Todos);
        Modo(semBuscaInicial: true, pediramLista: true, temSugestao: false)
            .Should().Be(ModoDaBusca.Todos);
    }

    /// <summary>A tela do Novo atendimento: sugestão fornecida e ligada, sem opt-in.</summary>
    [Fact]
    public void O_novo_atendimento_abre_na_agenda_do_dia()
    {
        var modo = Modo(temSugestao: true, sugestaoLigada: true);

        modo.Should().Be(ModoDaBusca.Sugestao);
        BuscaDePaciente.Consulta(modo).Should().BeTrue(
            "a agenda do dia é UMA consulta, e é a resposta certa desta tela");
    }

    /// <summary>
    /// Desligar a sugestão devolve a listagem — e ela não é o defeito de volta: o defeito
    /// era ela CHEGAR sem ninguém pedir, com cara de resposta.
    /// </summary>
    [Fact]
    public void Desligar_a_sugestao_devolve_a_listagem_PEDIDA()
    {
        Modo(temSugestao: true, sugestaoLigada: false).Should().Be(ModoDaBusca.Todos);
    }

    /// <summary>
    /// Só o ocioso deixa de consultar. Se algum dia outro modo passar a não consultar, a
    /// tela ficaria vazia sem dizer por quê — e vazio sem explicação se lê como quebrado.
    /// </summary>
    [Theory]
    [InlineData(ModoDaBusca.Sugestao)]
    [InlineData(ModoDaBusca.Todos)]
    [InlineData(ModoDaBusca.PorTermo)]
    public void Todo_modo_que_nao_e_ocioso_consulta(ModoDaBusca modo)
    {
        BuscaDePaciente.Consulta(modo).Should().BeTrue();
    }
}
