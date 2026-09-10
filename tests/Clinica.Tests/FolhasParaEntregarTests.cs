using Clinica.Application.Modelos;
using Clinica.Domain.Entities;
using FluentAssertions;
using Xunit;

namespace Clinica.Tests;

/// <summary>
/// A coluna ENTREGAR AGORA do atendimento (set/2026, tela 1 do mockup aprovado
/// "documentos nas quatro telas").
///
/// Ela existe aqui porque cada cartão faz uma PROMESSA — "CID M54.5 da sua hipótese" — que
/// a janela de emissão tem de cumprir. Enquanto a frase vivesse na ViewModel do WPF,
/// nenhum teste a alcançaria; é a regra da <c>GradeSemana</c> e do
/// <c>ResumoSessaoAnterior</c>.
/// </summary>
public class FolhasParaEntregarTests
{
    private const Permissao TudoDoConsultorio =
        Permissao.VerProntuario | Permissao.EditarProntuario | Permissao.Prescrever
        | Permissao.VerFichaPaciente | Permissao.EditarPaciente;

    private static FolhaParaEntregar Cartao(
        HerancaDaSessao heranca, string chave, Permissao acessos = TudoDoConsultorio)
        => FolhasParaEntregar.Montar(heranca, acessos).Single(c => c.Chave == chave);

    /// <summary>
    /// A ordem é a de quem ATENDE, e as cinco folhas são as que se entregam ao fim da
    /// sessão — o termo e a anamnese ficam de fora de propósito.
    /// </summary>
    [Fact]
    public void A_coluna_tem_as_cinco_folhas_na_ordem_de_quem_atende()
        => FolhasParaEntregar.Montar(new HerancaDaSessao(1), TudoDoConsultorio)
            .Select(c => c.Chave)
            .Should().Equal("receita", "atestado", "pedido-exame", "comparecimento", "relatorio-evolucao");

    /// <summary>
    /// ⚠️ O DESVIO DECLARADO do mockup: o desenho mostra a receita dizendo "já com a
    /// Passiflora que você escreveu", e isso não se entrega sem LER o texto livre da
    /// conduta à procura de nome de medicamento. A receita abre em branco, e o cartão diz
    /// isso — a frase é CINZA (não veio da sessão), que é a outra metade da honestidade.
    /// </summary>
    [Fact]
    public void A_receita_abre_em_branco_e_diz_isso()
    {
        var receita = Cartao(
            new HerancaDaSessao(1, Cid: "M54.5", Hipotese: "Lombalgia mecânica"), "receita");

        receita.VemDaSessao.Should().BeFalse();
        receita.Heranca.Should().Contain("em branco");
    }

    [Fact]
    public void O_atestado_herda_o_CID_da_hipotese()
    {
        var comCid = Cartao(new HerancaDaSessao(1, Cid: "M54.5"), "atestado");
        comCid.VemDaSessao.Should().BeTrue();
        comCid.Heranca.Should().Be("CID M54.5 da sua hipótese");

        // Sem CID escrito, o cartão NÃO inventa: ele diz o que o papel vai pedir.
        var semCid = Cartao(new HerancaDaSessao(1), "atestado");
        semCid.VemDaSessao.Should().BeFalse();
    }

    /// <summary>
    /// A hipótese vira a INDICAÇÃO CLÍNICA do pedido — que é a coluna que o laboratório lê
    /// e é literalmente o que ela é.
    /// </summary>
    [Fact]
    public void O_pedido_de_exame_herda_a_hipotese_como_indicacao()
    {
        var cartao = Cartao(new HerancaDaSessao(1, Hipotese: "Lombalgia mecânica"), "pedido-exame");

        cartao.VemDaSessao.Should().BeTrue();
        cartao.Heranca.Should().Be("indicação clínica: Lombalgia mecânica");
    }

    /// <summary>
    /// Hipótese comprida é cortada por PALAVRA e com reticências: cortar no meio de uma
    /// palavra faz o cartão parecer defeito, e sem o sinal ninguém sabe que há mais.
    /// </summary>
    [Fact]
    public void A_hipotese_comprida_cabe_no_cartao()
    {
        var cartao = Cartao(
            new HerancaDaSessao(1, Hipotese: "Lombalgia mecânica crônica com irradiação para o membro inferior direito"),
            "pedido-exame");

        cartao.Heranca.Should().EndWith("…");
        cartao.Heranca.Length.Should().BeLessThan(60);
        cartao.Heranca.Should().NotContain("irradiaç…");
    }

    /// <summary>
    /// A SAÍDA só entra quando a sessão foi encerrada. Enquanto o paciente está na sala,
    /// escrever "→ agora" numa declaração seria afirmar um fato que ainda não aconteceu.
    /// </summary>
    [Fact]
    public void O_comparecimento_herda_a_chegada_e_so_diz_a_saida_quando_ela_existe()
    {
        var naSala = Cartao(new HerancaDaSessao(1, Chegada: new TimeOnly(14, 0)), "comparecimento");
        naSala.VemDaSessao.Should().BeTrue();
        naSala.Heranca.Should().Be("está aqui desde 14:00");

        var encerrada = Cartao(
            new HerancaDaSessao(1, Chegada: new TimeOnly(14, 0), Saida: new TimeOnly(14, 40)),
            "comparecimento");
        encerrada.Heranca.Should().Be("esteve aqui 14:00 → 14:40");

        // Sem carimbo de chegada não há o que herdar, e o cartão DIZ isso em vez de
        // oferecer um horário inventado.
        var semHorario = Cartao(new HerancaDaSessao(1), "comparecimento");
        semHorario.VemDaSessao.Should().BeFalse();
    }

    /// <summary>
    /// O relatório é MONTADO do prontuário: sem sessão nenhuma ele sairia com o cabeçalho,
    /// o rodapé e nada no meio. O botão fica apagado, com a razão escrita — botão apagado
    /// sem frase vira "o sistema travou" (parcela 41).
    /// </summary>
    [Fact]
    public void O_relatorio_nao_se_emite_sem_sessao_para_montar()
    {
        var semBase = Cartao(new HerancaDaSessao(1), "relatorio-evolucao");
        semBase.PodeEmitir.Should().BeFalse();
        semBase.Pendencia.Should().NotBeEmpty();

        var comBase = Cartao(new HerancaDaSessao(1, SessoesRegistradas: 18), "relatorio-evolucao");
        comBase.PodeEmitir.Should().BeTrue();
        comBase.Pendencia.Should().BeEmpty();
        comBase.Heranca.Should().Contain("18");
    }

    /// <summary>
    /// Cartão que a pessoa não alcança SOME (a regra da parcela 59): cartão apagado
    /// dizendo "sem permissão" ANUNCIA que existe um relatório de evolução daquele
    /// paciente, que é o que não se quer contar a quem não pode lê-lo.
    /// </summary>
    [Fact]
    public void So_aparecem_as_folhas_que_o_acesso_alcanca()
    {
        // O balcão: cadastro, sem prontuário.
        var doBalcao = FolhasParaEntregar.Montar(
            new HerancaDaSessao(1, SessoesRegistradas: 3),
            Permissao.VerFichaPaciente | Permissao.EditarPaciente);

        doBalcao.Select(c => c.Chave).Should().Equal("comparecimento");
    }

    /// <summary>
    /// Quem VÊ e não pode EMITIR recebe o cartão apagado COM a frase — as duas barreiras:
    /// o botão explica, o <c>Exigir</c> do comando impede.
    /// </summary>
    [Fact]
    public void Quem_so_le_o_prontuario_ve_a_receita_apagada_e_sabe_por_que()
    {
        var receita = Cartao(new HerancaDaSessao(1), "receita", Permissao.VerProntuario);

        receita.PodeEmitir.Should().BeFalse();
        receita.Pendencia.Should().Contain("não emitir");
    }

    /// <summary>
    /// A procedência que a janela escreve no cabeçalho. VAZIA sem horário: inventar "da
    /// sessão de hoje" para uma receita emitida fora de consulta seria afirmar um vínculo
    /// que o banco não tem.
    /// </summary>
    [Fact]
    public void A_procedencia_distingue_hoje_de_outro_dia_e_cala_no_avulso()
    {
        var hoje = new DateOnly(2026, 9, 10);

        new HerancaDaSessao(1, DataHoraDaSessao: new DateTime(2026, 9, 10, 14, 0, 0))
            .Procedencia(hoje).Should().Be("da sessão de hoje, 14h00");

        new HerancaDaSessao(1, DataHoraDaSessao: new DateTime(2026, 7, 28, 9, 30, 0))
            .Procedencia(hoje).Should().Be("da sessão de 28/07, 09h30");

        new HerancaDaSessao(1).Procedencia(hoje).Should().BeEmpty();
    }
}
