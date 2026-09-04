using System.Reflection;
using Clinica.Application.Modelos;
using Clinica.Domain.Entities;
using FluentAssertions;
using Xunit;

namespace Clinica.Tests;

/// <summary>
/// A SESSÃO DO PRONTUÁRIO ABERTA POR INTEIRO (set/2026 — o pedido do cliente:
/// <i>"ao abrir o prontuário não conseguimos abrir o prontuário daquela sessão"</i>).
///
/// A asserção que carrega a suíte é a PRIMEIRA, e ela é por REFLEXÃO de propósito: uma
/// lista de campos escrita à mão é uma lista que a próxima parcela não alcança. Foi
/// exatamente assim que o <c>ModeloEvolucao</c> ficou com quatro campos enquanto a sessão
/// passava a ter doze (parcela 76), e o painel da sessão anterior com quatro enquanto ela
/// já tinha nove (parcela 77) — as duas vezes sem quebrar build, teste ou rede nenhuma.
/// </summary>
public class SessaoDoProntuarioTests
{
    /// <summary>
    /// O que NÃO é conteúdo da sessão, e por quê. Campo novo que não estiver aqui é
    /// cobrado pelo primeiro teste — que é o ponto: a decisão passa a ser explícita.
    /// </summary>
    private static readonly Dictionary<string, string> ForaDosBlocos = new()
    {
        [nameof(Evolucao.CriadoPor)] = "sai na PROCEDÊNCIA, não como bloco de conteúdo",
        [nameof(Evolucao.ChaveImportacao)] = "procedência da importação; não é texto clínico",
        [nameof(Evolucao.MotivoCancelamento)] = "sai no AVISO de cancelamento",
        [nameof(Evolucao.CanceladaPor)] = "sai no AVISO de cancelamento",
    };

    private static Evolucao Cheia() => new()
    {
        Id = 42,
        PacienteId = 7,
        Data = new DateOnly(2026, 8, 20),
        EvaAntes = 8,
        EvaDepois = 3,
        RetornoSugeridoEm = new DateOnly(2026, 8, 27),
        CriadoEm = new DateTime(2026, 8, 20, 14, 30, 0),
        CriadoPor = "dra.ana",
        Profissional = new Profissional { Id = 1, Nome = "Dra. Ana" }
    };

    /// <summary>
    /// ⚠️ TODO campo de texto da sessão precisa aparecer na janela que a abre.
    ///
    /// Este é o décimo lugar da lista dos oito do CLAUDE.md, e ele falha no commit em que
    /// alguém acrescentar um campo à <see cref="Evolucao"/> sem pô-lo em
    /// <see cref="SessaoDoProntuario"/> — meses antes de alguém abrir a tela e não achar o
    /// que escreveu.
    /// </summary>
    [Fact]
    public void Todo_campo_escrito_aparece_em_algum_bloco()
    {
        var evolucao = Cheia();

        // Preenche TODO campo de texto com um valor único e reconhecível.
        var textuais = typeof(Evolucao).GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.PropertyType == typeof(string) && p.CanWrite)
            .ToList();

        textuais.Should().NotBeEmpty("a reflexão precisa achar os campos de verdade");

        foreach (var p in textuais)
            p.SetValue(evolucao, $"VALOR-{p.Name}");

        var sessao = SessaoDoProntuario.De(evolucao, anexos: 0, correcoes: 0);
        var tudo = string.Join("\n", sessao.Blocos.Select(b => $"{b.Rotulo}: {b.Texto}"));

        foreach (var p in textuais)
        {
            if (ForaDosBlocos.ContainsKey(p.Name)) continue;

            tudo.Should().Contain($"VALOR-{p.Name}",
                $"o campo {p.Name} é escrito na sessão e precisa aparecer para quem a abre — "
                + "se ele não é conteúdo clínico, declare-o em ForaDosBlocos com a razão");
        }
    }

    /// <summary>
    /// Bloco vazio SOME. Dez rótulos com "—" empurram para fora da vista os dois que a
    /// sessão de acupuntura mais comum da casa tem (EVA e conduta).
    /// </summary>
    [Fact]
    public void Campo_em_branco_nao_vira_bloco()
    {
        var e = Cheia();
        e.RetornoSugeridoEm = null;          // `Cheia()` traz retorno; aqui só a conduta importa
        e.Conduta = "Acupuntura em B60, R1, TA11";
        e.QueixaPrincipal = "   ";

        var sessao = SessaoDoProntuario.De(e, anexos: 0, correcoes: 0);

        sessao.Blocos.Should().ContainSingle()
            .Which.Rotulo.Should().Be("Conduta");
    }

    /// <summary>
    /// A hipótese e o CID são UM bloco, com a MESMA regra do
    /// <see cref="ResumoSessaoAnterior"/>: duas leituras do mesmo par divergiriam, e a que
    /// ficasse para trás mostraria um CID sem o que ele significa.
    /// </summary>
    [Theory]
    [InlineData("Lombalgia", "M54.5", "Lombalgia (M54.5)")]
    [InlineData("Lombalgia", null, "Lombalgia")]
    [InlineData(null, "M54.5", "CID M54.5")]
    public void A_hipotese_e_o_CID_saem_no_mesmo_bloco(string? hipotese, string? cid, string esperado)
    {
        var e = Cheia();
        e.HipoteseDiagnostica = hipotese;
        e.CidSessao = cid;

        SessaoDoProntuario.De(e, 0, 0).Blocos
            .Should().ContainSingle(b => b.Rotulo == "Hipótese diagnóstica")
            .Which.Texto.Should().Be(esperado);
    }

    [Fact]
    public void Sem_hipotese_e_sem_CID_o_bloco_nao_existe()
    {
        SessaoDoProntuario.De(Cheia(), 0, 0).Blocos
            .Should().NotContain(b => b.Rotulo == "Hipótese diagnóstica");
    }

    /// <summary>
    /// O retorno é a resposta para "por que este paciente está aqui hoje", e ele só existe
    /// com DATA: a nota sozinha não diz quando.
    /// </summary>
    [Fact]
    public void O_retorno_sai_com_a_data_e_a_nota()
    {
        var e = Cheia();
        e.RetornoSugeridoNota = "reavaliar a EVA";

        SessaoDoProntuario.De(e, 0, 0).Blocos
            .Should().ContainSingle(b => b.Rotulo == "Retorno sugerido")
            .Which.Texto.Should().Be("Voltar em 27/08/2026 — reavaliar a EVA");
    }

    [Fact]
    public void Sem_data_de_retorno_nao_ha_bloco_de_retorno()
    {
        var e = Cheia();
        e.RetornoSugeridoEm = null;
        e.RetornoSugeridoNota = "reavaliar a EVA";

        SessaoDoProntuario.De(e, 0, 0).Blocos
            .Should().NotContain(b => b.Rotulo == "Retorno sugerido");
    }

    /// <summary>
    /// ⚠️ A sessão CANCELADA aparece MARCADA, nunca sumindo (parcela 52) — e o MOTIVO vai
    /// junto: linha cancelada sem o porquê deixa o próximo leitor sem saber se ela era
    /// falsa ou se foi engano.
    /// </summary>
    [Fact]
    public void A_sessao_cancelada_diz_quem_cancelou_e_por_que()
    {
        var e = Cheia();
        e.CanceladaEm = new DateTime(2026, 8, 21, 9, 0, 0);
        e.CanceladaPor = "dra.ana";
        e.MotivoCancelamento = "lançada no paciente errado";

        var sessao = SessaoDoProntuario.De(e, 0, 0);

        sessao.Cancelada.Should().BeTrue();
        sessao.AvisoCancelamento.Should()
            .Contain("dra.ana")
            .And.Contain("lançada no paciente errado")
            .And.Contain("não se apaga");
    }

    [Fact]
    public void A_sessao_viva_nao_tem_aviso_de_cancelamento()
    {
        SessaoDoProntuario.De(Cheia(), 0, 0).Cancelada.Should().BeFalse();
    }

    /// <summary>
    /// "EVA não medida" e "EVA 0 → 0" são coisas diferentes, e meia medida também: sem o
    /// PAR não dá para afirmar variação nenhuma (a regra do gráfico, parcela 2).
    /// </summary>
    [Theory]
    [InlineData(8, 3, "EVA 8 → 3")]
    [InlineData(8, null, "EVA 8 (só antes)")]
    [InlineData(null, null, "EVA não medida")]
    public void A_EVA_distingue_o_par_da_meia_medida(int? antes, int? depois, string esperado)
    {
        var e = Cheia();
        e.EvaAntes = antes;
        e.EvaDepois = depois;

        SessaoDoProntuario.De(e, 0, 0).Eva.Should().Be(esperado);
    }

    /// <summary>
    /// A procedência é a rastreabilidade do art. 3º da Lei 13.787/2018, e a lista não tinha
    /// onde mostrá-la. A data de ALTERAÇÃO sai ao lado da de criação: as duas juntas são o
    /// que diz que o registro foi mexido depois de escrito.
    /// </summary>
    [Fact]
    public void A_procedencia_diz_quem_escreveu_e_quando_foi_alterada()
    {
        var e = Cheia();
        e.AtualizadoEm = new DateTime(2026, 8, 22, 10, 15, 0);

        var p = SessaoDoProntuario.De(e, 0, 0).Procedencia;

        p.Should().Contain("dra.ana").And.Contain("20/08/2026")
            .And.Contain("última alteração").And.Contain("22/08/2026");
    }

    [Fact]
    public void Sem_alteracao_a_procedencia_nao_inventa_uma()
    {
        SessaoDoProntuario.De(Cheia(), 0, 0).Procedencia
            .Should().NotContain("última alteração");
    }

    /// <summary>
    /// Sem autor gravado, a frase DIZ isso — em branco não se distingue de "não carregou"
    /// (a regra do nulo da parcela 58).
    /// </summary>
    [Fact]
    public void Sem_autor_a_procedencia_admite_a_falta()
    {
        var e = Cheia();
        e.CriadoPor = null;

        SessaoDoProntuario.De(e, 0, 0).Procedencia
            .Should().Contain("autor não registrado");
    }

    /// <summary>
    /// O nome do PROFISSIONAL vence o login de quem digitou: numa sessão escrita pelo
    /// balcão os dois são pessoas diferentes, e quem assina o registro é quem atendeu.
    /// </summary>
    [Fact]
    public void O_profissional_vence_o_login_de_quem_digitou()
    {
        SessaoDoProntuario.De(Cheia(), 0, 0).Profissional.Should().Be("Dra. Ana");
    }

    [Fact]
    public void Sem_profissional_cai_no_login_e_depois_admite_a_falta()
    {
        var e = Cheia();
        e.Profissional = null;
        SessaoDoProntuario.De(e, 0, 0).Profissional.Should().Be("dra.ana");

        e.CriadoPor = null;
        SessaoDoProntuario.De(e, 0, 0).Profissional.Should().Be("sem autor registrado");
    }

    /// <summary>
    /// Os contadores da linha: o botão só aparece quando há o que abrir, e o texto diz
    /// QUANTOS — "Anexos" e "3 anexos" respondem perguntas diferentes.
    /// </summary>
    [Fact]
    public void As_contagens_dizem_quantos_e_escondem_o_botao_vazio()
    {
        var vazia = SessaoDoProntuario.De(Cheia(), anexos: 0, correcoes: 0);
        vazia.TemAnexos.Should().BeFalse();
        vazia.Retificada.Should().BeFalse();
        vazia.AnexosTexto.Should().Be("Anexos");

        var cheia = SessaoDoProntuario.De(Cheia(), anexos: 3, correcoes: 1);
        cheia.TemAnexos.Should().BeTrue();
        cheia.Retificada.Should().BeTrue();
        cheia.AnexosTexto.Should().Be("3 anexos");
        cheia.CorrecoesTexto.Should().Be("1 correção");
    }

    /// <summary>
    /// A ordem é a do S-O-A-P, a MESMA da tela onde a sessão foi escrita (parcela 77): ler
    /// o registro na forma com que ele foi escrito é o que faz o prontuário parecer um
    /// sistema só.
    /// </summary>
    [Fact]
    public void Os_blocos_saem_na_ordem_do_SOAP()
    {
        var e = Cheia();
        e.QueixaPrincipal = "dor lombar";
        e.ExameFisico = "dor à palpação";
        e.HipoteseDiagnostica = "lombalgia";
        e.Conduta = "acupuntura";

        SessaoDoProntuario.De(e, 0, 0).Blocos.Select(b => b.Rotulo)
            .Should().ContainInOrder(
                "Queixa principal", "Exame físico", "Hipótese diagnóstica", "Conduta");
    }
}
