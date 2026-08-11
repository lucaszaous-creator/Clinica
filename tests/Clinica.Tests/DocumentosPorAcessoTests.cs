using Clinica.Application.Servicos;
using Clinica.Domain.Entities;
using FluentAssertions;
using Xunit;

namespace Clinica.Tests;

/// <summary>
/// QUEM ALCANÇA CADA FOLHA (parcela 59).
///
/// A direção viu a recepcionista abrindo os documentos e pediu a permissão granular.
/// <see cref="Permissao.VerDocumentos"/> é a PORTA da seção; o que decide o que aparece
/// dentro dela é o acesso declarado por cada folha do catálogo.
///
/// As duas metades são necessárias, e é isso que estes testes fixam: só a porta obrigaria
/// a direção a escolher entre a recepcionista lendo o relatório de evolução de todo mundo
/// e a recepcionista sem o recibo que ela emite dez vezes por dia — o bit sobrecarregado
/// que a parcela 49 corrigiu no domínio, reaparecendo numa tela.
/// </summary>
public class DocumentosPorAcessoTests
{
    private static Permissao Balcao => PerfisAcesso.Padrao(PerfilAcesso.Recepcao);
    private static Permissao QuemAtende => PerfisAcesso.Padrao(PerfilAcesso.Profissional);
    private static Permissao Caixa => PerfisAcesso.Padrao(PerfilAcesso.Financeiro);

    private static IReadOnlyList<string> Chaves(Permissao acessos)
        => CentralDocumentosService.CatalogoPara(acessos).Select(f => f.Chave).ToList();

    // ================================================================
    // A PORTA
    // ================================================================

    /// <summary>
    /// A seção fica atrás de um bit próprio — era isso o pedido literal da direção. Antes
    /// ela pedia `VerFichaPaciente`, que todo perfil de balcão tem.
    /// </summary>
    [Fact]
    public void A_central_de_documentos_tem_bit_proprio()
    {
        Balcao.HasFlag(Permissao.VerDocumentos).Should().BeTrue(
            "o balcão emite recibo, declaração e termo de consentimento todo dia");

        // O bit é do FATURAMENTO de ninguém: quem não tem porta para a central não o
        // recebe de brinde, senão a tela de Acessos mostra uma caixinha sem sentido.
        PerfisAcesso.Padrao(PerfilAcesso.Faturista)
            .HasFlag(Permissao.VerDocumentos).Should().BeFalse();
    }

    // ================================================================
    // O QUE CADA UM ALCANÇA
    // ================================================================

    /// <summary>
    /// O balcão NÃO alcança o que carrega dado de saúde — foi a reclamação da direção.
    /// Receituário, atestado, pedido de exame, relatório de evolução e anamnese pedem
    /// <c>VerProntuario</c>, que a recepção não tem desde a parcela 49.
    /// </summary>
    [Theory]
    [InlineData("receita")]
    [InlineData("atestado")]
    [InlineData("pedido-exame")]
    [InlineData("relatorio-evolucao")]
    [InlineData("anamnese")]
    public void Balcao_nao_alcanca_folha_de_dado_de_saude(string chave)
        => Chaves(Balcao).Should().NotContain(chave);

    /// <summary>
    /// ⚠️ A outra metade, e ela vale tanto quanto: fechar a porta inteira tiraria da
    /// recepção quatro papéis que ela entrega todo dia e que NÃO dizem o que o paciente
    /// tem. A declaração prova que a pessoa esteve aqui; o termo de consentimento é a
    /// peça da LGPD, montada do cadastro.
    ///
    /// Sem este teste, a próxima simplificação ("é tudo folha clínica, exige prontuário")
    /// passaria — e o balcão perderia a função na segunda de manhã.
    /// </summary>
    [Theory]
    [InlineData("comparecimento")]
    [InlineData("consentimento")]
    public void Balcao_continua_alcancando_o_que_nao_e_dado_de_saude(string chave)
        => Chaves(Balcao).Should().Contain(chave);

    /// <summary>
    /// Recibo e orçamento são papel de DINHEIRO: quem recebe é quem comprova. O balcão não
    /// tem `VerFinanceiro`, e o caixa não tem prontuário — cada um vê o seu lado.
    /// </summary>
    [Fact]
    public void Recibo_e_orcamento_sao_do_financeiro()
    {
        Chaves(Balcao).Should().NotContain("recibo").And.NotContain("orcamento");
        Chaves(Caixa).Should().Contain("recibo").And.Contain("orcamento");
        Chaves(Caixa).Should().NotContain("receita", "cobrar não exige saber o diagnóstico");
    }

    /// <summary>Quem atende alcança as clínicas — é o outro lado da decisão.</summary>
    [Theory]
    [InlineData("receita")]
    [InlineData("atestado")]
    [InlineData("relatorio-evolucao")]
    public void Quem_atende_alcanca_a_folha_clinica(string chave)
        => Chaves(QuemAtende).Should().Contain(chave);

    /// <summary>
    /// O fechamento do período é conferência de gestão, não de uma pessoa: fica com quem
    /// tem os relatórios gerenciais.
    /// </summary>
    [Fact]
    public void Fechamento_do_periodo_e_da_direcao()
    {
        Chaves(Balcao).Should().NotContain(CentralDocumentosService.ChaveFechamentoPeriodo);
        Chaves(PerfisAcesso.Todas).Should()
            .Contain(CentralDocumentosService.ChaveFechamentoPeriodo);
    }

    // ================================================================
    // AS REGRAS DO CATÁLOGO
    // ================================================================

    /// <summary>
    /// Toda folha declara os dois acessos. Uma sem declaração cairia em
    /// <c>Permissao.Nenhuma</c>, que <c>Pode</c> LIBERA — o papel novo nasceria aberto
    /// para todo mundo, e ninguém notaria até vazar.
    /// </summary>
    [Fact]
    public void Toda_folha_declara_o_acesso_que_exige()
    {
        foreach (var f in CentralDocumentosService.Catalogo)
        {
            f.PermissaoVer.Should().NotBe(Permissao.Nenhuma, $"{f.Chave} precisa dizer quem vê");
            f.PermissaoEmitir.Should().NotBe(Permissao.Nenhuma, $"{f.Chave} precisa dizer quem emite");
        }
    }

    /// <summary>
    /// Emitir nunca é mais fácil que ver: quem não pode ler o relatório de evolução também
    /// não o imprime. Um catálogo em que o par se invertesse produziria um cartão apagado
    /// com um botão aceso — ou o contrário, que é pior.
    /// </summary>
    [Fact]
    public void Quem_emite_tambem_ve()
    {
        foreach (var f in CentralDocumentosService.Catalogo)
        {
            var soEmitir = PerfisAcesso.Individuais
                .Where(p => f.PermissaoEmitir.HasFlag(p))
                .Aggregate(Permissao.Nenhuma, (a, p) => a | p);

            CentralDocumentosService.CatalogoPara(soEmitir | f.PermissaoVer)
                .Should().Contain(x => x.Chave == f.Chave);
        }
    }

    /// <summary>
    /// O tipo de documento clínico resolve para a folha dele. Tipo que entre no enum sem
    /// entrar no catálogo cai no acesso mais RESTRITIVO — papel cujo acesso ninguém
    /// declarou nasce fechado, e não aberto.
    /// </summary>
    [Fact]
    public void Tipo_clinico_resolve_para_a_folha_dele()
    {
        CentralDocumentosService.AcessoParaVer(TipoDocumentoClinico.Receita)
            .Should().Be(Permissao.VerProntuario);
        CentralDocumentosService.AcessoParaEmitir(TipoDocumentoClinico.Receita)
            .Should().Be(Permissao.Prescrever);

        CentralDocumentosService.AcessoParaVer(TipoDocumentoClinico.Comparecimento)
            .Should().Be(Permissao.VerFichaPaciente);

        foreach (var tipo in Enum.GetValues<TipoDocumentoClinico>())
        {
            CentralDocumentosService.AcessoParaVer(tipo)
                .Should().NotBe(Permissao.Nenhuma, $"{tipo} não pode nascer aberto");
            CentralDocumentosService.AcessoParaEmitir(tipo)
                .Should().NotBe(Permissao.Nenhuma, $"{tipo} não pode nascer aberto");
        }
    }

    /// <summary>
    /// ⚠️ A regra que a sessão sem login carrega: <c>Pode</c> LIBERA quando ninguém entrou
    /// (tela aberta fora do shell, teste), porque tela vazia parece defeito e não
    /// segurança. <c>Efetivas</c> tem de dar a MESMA resposta — se ela devolvesse
    /// `Permissoes` cru, a central abriria sem um único cartão fora do login e alguém
    /// concluiria que a tela quebrou.
    /// </summary>
    [Fact]
    public void Sem_login_a_central_nao_abre_vazia()
    {
        var sessao = new SessaoUsuario();

        sessao.Autenticado.Should().BeFalse();
        sessao.Pode(Permissao.VerProntuario).Should().BeTrue("sem sessão autenticada, libera");
        CentralDocumentosService.CatalogoPara(sessao.Efetivas)
            .Should().HaveCount(CentralDocumentosService.Catalogo.Count);
    }
}
