using Clinica.Domain.Entities;
using FluentAssertions;
using Xunit;

namespace Clinica.Tests;

/// <summary>
/// As permissões granulares do faturamento (parcela 45) — o pedido da cliente: "permitir
/// usuários no módulo faturamento com permissões granulares, assim a gerente consegue
/// auditar o que está sendo feito e liberar ou não permissões".
///
/// O que estes testes protegem não é o enum: é a promessa de compatibilidade. A atualização
/// que INTRODUZ login não pode, de quebra, tirar da faturista uma capacidade que ela usava
/// ontem — isso vira chamado de suporte na segunda de manhã, e o pedido era o contrário.
/// </summary>
public class PermissoesFaturamentoTests
{
    /// <summary>
    /// O faturista continua fazendo tudo o que o app de faturamento deixava fazer antes de
    /// ter login. Se alguém tirar um destes bits do padrão, é aqui que aparece — e a
    /// pergunta passa a ser "a direção decidiu isso?", que é a pergunta certa.
    /// </summary>
    [Theory]
    [InlineData(Permissao.VerFaturamento)]
    [InlineData(Permissao.BaixarGuia)]
    [InlineData(Permissao.EstornarBaixa)]
    [InlineData(Permissao.RegistrarGlosa)]
    [InlineData(Permissao.GerenciarLotesTiss)]
    [InlineData(Permissao.LancarAtendimento)]
    [InlineData(Permissao.MarcarNaoConformidade)]
    [InlineData(Permissao.VerAgenda)]
    [InlineData(Permissao.EditarAgenda)]
    [InlineData(Permissao.VerProntuario)]
    [InlineData(Permissao.EditarProntuario)]
    public void Faturista_faz_o_que_ja_fazia_antes_do_login(Permissao permissao)
        => PerfisAcesso.Padrao(PerfilAcesso.Faturista).HasFlag(permissao).Should().BeTrue();

    /// <summary>
    /// Configurar o faturamento (catálogo de convênios, formato do número da guia, prazos)
    /// muda a regra para TODO MUNDO, não o registro de uma guia. Fica com a direção.
    /// </summary>
    [Fact]
    public void Faturista_nao_configura_o_faturamento()
        => PerfisAcesso.Padrao(PerfilAcesso.Faturista)
            .HasFlag(Permissao.ConfigurarFaturamento).Should().BeFalse();

    [Fact]
    public void Gerente_tem_todas_as_permissoes_novas()
    {
        var gerente = PerfisAcesso.Padrao(PerfilAcesso.Gerente);
        foreach (var p in PerfisAcesso.Individuais)
            gerente.HasFlag(p).Should().BeTrue($"o Gerente Geral recebe {p}");
    }

    /// <summary>
    /// Quem não é do faturamento não ganhou nenhum dos bits novos de tabela. Sem isto, um
    /// bit acrescentado ao padrão errado daria à recepção o poder de estornar baixa sem
    /// ninguém notar.
    /// </summary>
    [Theory]
    [InlineData(PerfilAcesso.Recepcao)]
    [InlineData(PerfilAcesso.Profissional)]
    [InlineData(PerfilAcesso.Enfermagem)]
    [InlineData(PerfilAcesso.Financeiro)]
    public void Perfis_de_fora_do_faturamento_nao_escrevem_no_faturamento(PerfilAcesso perfil)
    {
        var padrao = PerfisAcesso.Padrao(perfil);
        padrao.HasFlag(Permissao.BaixarGuia).Should().BeFalse();
        padrao.HasFlag(Permissao.EstornarBaixa).Should().BeFalse();
        padrao.HasFlag(Permissao.RegistrarGlosa).Should().BeFalse();
        padrao.HasFlag(Permissao.GerenciarLotesTiss).Should().BeFalse();
        padrao.HasFlag(Permissao.MarcarNaoConformidade).Should().BeFalse();
        padrao.HasFlag(Permissao.ConfigurarFaturamento).Should().BeFalse();
    }

    /// <summary>
    /// A direção tira o bit de uma pessoa sem mexer no perfil dela — é literalmente o
    /// "liberar ou não" do pedido. NEGADA vence EXTRA: tirar acesso é a decisão que não
    /// pode ser anulada por engano de configuração.
    /// </summary>
    [Fact]
    public void Direcao_tira_o_estorno_de_uma_faturista_especifica()
    {
        var usuario = new UsuarioSistema
        {
            Perfil = PerfilAcesso.Faturista,
            PermissoesNegadas = Permissao.EstornarBaixa
        };

        usuario.Pode(Permissao.BaixarGuia).Should().BeTrue();
        usuario.Pode(Permissao.EstornarBaixa).Should().BeFalse();
    }

    [Fact]
    public void Direcao_libera_a_configuracao_para_uma_faturista_especifica()
    {
        var usuario = new UsuarioSistema
        {
            Perfil = PerfilAcesso.Faturista,
            PermissoesExtras = Permissao.ConfigurarFaturamento
        };

        usuario.Pode(Permissao.ConfigurarFaturamento).Should().BeTrue();
    }

    /// <summary>
    /// Bits novos não podem COLIDIR com os antigos: <see cref="Permissao"/> é gravada como
    /// inteiro, e dois valores com o mesmo bit fariam uma permissão conceder a outra em
    /// silêncio, em bases que já estão em produção.
    /// </summary>
    [Fact]
    public void Cada_permissao_ocupa_um_bit_proprio()
    {
        var vistos = new HashSet<int>();
        foreach (var p in PerfisAcesso.Individuais)
        {
            var valor = (int)p;
            System.Numerics.BitOperations.PopCount((uint)valor).Should().Be(1,
                $"{p} tem de ser um bit único, não uma combinação");
            vistos.Add(valor).Should().BeTrue($"{p} repete um bit já usado");
        }
    }

    /// <summary>
    /// Todo bit tem rótulo em português. Sem isto a tela de Acessos ofereceria à direção
    /// "GerenciarLotesTiss" — o identificador do programador escapando para quem decide
    /// (o defeito da parcela 41), e aqui na tela onde se dá poder a alguém.
    /// </summary>
    [Fact]
    public void Toda_permissao_tem_rotulo_em_portugues()
    {
        foreach (var p in PerfisAcesso.Individuais)
            PerfisAcesso.Rotular(p).Should().NotBe(p.ToString(),
                $"{p} precisa de rótulo em PerfisAcesso.Rotular");
    }

    /// <summary>
    /// Sem sessão autenticada, LIBERA. A regra é da parcela 5 e continua valendo: uma sessão
    /// vazia negando tudo esconderia as telas em qualquer caminho que não passe pelo login
    /// (teste, janela aberta fora do shell), e tela vazia parece defeito, não segurança.
    /// </summary>
    [Fact]
    public void Sem_sessao_autenticada_libera()
    {
        var sessao = new SessaoUsuario();
        sessao.Autenticado.Should().BeFalse();
        sessao.Pode(Permissao.EstornarBaixa).Should().BeTrue();
    }

    /// <summary>
    /// Com sessão, a permissão vale — e <c>Exigir</c> é a barreira que IMPEDE, com uma
    /// frase que a tela mostra ao usuário.
    /// </summary>
    [Fact]
    public void Com_sessao_exigir_bloqueia_e_explica()
    {
        var sessao = new SessaoUsuario();
        sessao.Entrar(new UsuarioSistema
        {
            Id = 7,
            Nome = "Ana",
            Login = "ana",
            Perfil = PerfilAcesso.Faturista,
            PermissoesNegadas = Permissao.EstornarBaixa
        });

        sessao.Pode(Permissao.BaixarGuia).Should().BeTrue();

        var acao = () => sessao.Exigir(Permissao.EstornarBaixa, "estornar a baixa da guia");
        acao.Should().Throw<InvalidOperationException>()
            .WithMessage("*estornar a baixa da guia*");

        // O operador gravado na auditoria passa a ser o LOGIN, não o usuário do Windows —
        // que é o que faltava para a direção conseguir auditar quem fez o quê.
        sessao.Operador.Should().Be("ana");
    }
}
