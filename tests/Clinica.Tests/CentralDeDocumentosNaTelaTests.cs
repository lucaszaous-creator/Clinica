using Clinica.Application.Modelos;
using Clinica.Application.Servicos;
using Clinica.Domain.Entities;
using FluentAssertions;

namespace Clinica.Tests;

/// <summary>
/// O que a central de Documentos da Recepção (Blazor, out/2026) AFIRMA — os cartões do passo 2
/// e as linhas de "O que já saiu". Era decidido dentro do DocumentosViewModel WPF, onde o
/// dotnet test não alcança.
/// </summary>
public class CentralDeDocumentosNaTelaTests
{
    private static readonly DateOnly Hoje = new(2026, 10, 16);

    private static CartaoDaCentral Cartao(string chave, Permissao acessos, bool paciente = true, bool caixa = true)
        => CentralDeDocumentosNaTela.Cartoes(acessos, paciente, caixa).Single(c => c.Folha.Chave == chave);

    [Fact]
    public void Sem_paciente_a_folha_de_pessoa_pede_o_paciente_e_a_do_periodo_segue()
    {
        var todas = PerfisAcesso.Todas;
        Cartao("comparecimento", todas, paciente: false).Should().Match<CartaoDaCentral>(c =>
            !c.PodeGerar && c.Pendencia == "Escolha o paciente para liberar." && c.ExigePaciente);
        // O passo 1 é PASSO, não portão: o fechamento do período não é de ninguém.
        var fechamento = Cartao(CentralDocumentosService.ChaveFechamentoPeriodo, todas, paciente: false);
        fechamento.PodeGerar.Should().BeTrue();
        fechamento.ExigePaciente.Should().BeFalse();
        fechamento.AcaoRotulo.Should().Be("Gerar PDF");
    }

    [Fact]
    public void O_recibo_navega_ao_Caixa_e_diz_quando_ele_nao_esta_neste_aplicativo()
    {
        var com = Cartao("recibo", PerfisAcesso.Todas, paciente: false, caixa: true);
        com.PodeGerar.Should().BeTrue();
        com.AcaoRotulo.Should().Be("Ir para o Caixa");

        var sem = Cartao("recibo", PerfisAcesso.Todas, caixa: false);
        sem.PodeGerar.Should().BeFalse();
        sem.Pendencia.Should().Contain("não está aberto neste aplicativo");
    }

    [Fact]
    public void O_termo_diz_que_o_clique_colhe_a_assinatura()
        => Cartao("termo-procedimento", PerfisAcesso.Todas).AcaoRotulo.Should().Be("Colher assinatura…");

    [Fact]
    public void Quem_ve_e_nao_emite_le_o_porque_no_cartao()
    {
        // A recepção alcança o termo do procedimento por um segundo bit (só ver) — sem o de emitir,
        // o cartão diz o que falta em vez de ficar aceso.
        var folha = CentralDocumentosService.Folha("termo-procedimento")!;
        var soVer = folha.AcessosQueVeem & ~folha.PermissaoEmitir;
        if (soVer == Permissao.Nenhuma) return;
        var c = CentralDeDocumentosNaTela.Cartao(folha, soVer, true, true);
        c.PodeGerar.Should().BeFalse();
        c.Pendencia.Should().StartWith("Seu acesso permite ver, não colher");
    }

    [Fact]
    public void Folha_que_o_acesso_nao_alcanca_some_dos_cartoes()
    {
        var cartoes = CentralDeDocumentosNaTela.Cartoes(Permissao.VerFichaPaciente, true, true);
        cartoes.Should().NotContain(c => c.Folha.Chave == "receita");
        cartoes.Select(c => c.Folha.Chave).Should().BeEquivalentTo(
            CentralDocumentosService.CatalogoPara(Permissao.VerFichaPaciente).Select(f => f.Chave));
    }

    [Fact]
    public void Os_grupos_seguem_a_ordem_do_catalogo()
        => CentralDeDocumentosNaTela.Cartoes(PerfisAcesso.Todas, true, true)
            .Select(c => c.Grupo).Distinct().Should().Equal("Do atendimento", "Do dinheiro", "Da gestão");

    private static FolhaEmitida Emitida(bool cancelado = false, DateOnly? publicadoAte = null, bool jaTeveLink = false,
        string chave = "receita")
        => new(7, NaturezaFolha.Clinico, "2026/0007", "ABC123", "Receituário", "Lúcia Andrade", "Dra. Helena",
                Hoje.AddDays(-1), DateTime.Now, "ana", cancelado, cancelado ? "Emitida errada" : null)
            { Chave = chave, PublicadoAte = publicadoAte, JaTeveLink = jaTeveLink };

    [Fact]
    public void A_linha_escreve_o_codigo_e_quem_emitiu()
    {
        var l = CentralDeDocumentosNaTela.Linha(Emitida(), Hoje, PerfisAcesso.Todas);
        l.Detalhe.Should().Be("Dra. Helena · por ana · conferência ABC123");
        l.Situacao.Should().Be("código ABC123");
        l.Data.Should().Be("15/10/2026");
        l.Link.Should().BeEmpty();
        l.PodeMexer.Should().BeTrue();
    }

    [Fact]
    public void Cancelada_aparece_marcada_com_o_motivo()
        => CentralDeDocumentosNaTela.Linha(Emitida(cancelado: true), Hoje, PerfisAcesso.Todas)
            .Situacao.Should().Be("CANCELADA — Emitida errada");

    [Fact]
    public void O_link_conta_o_dia_do_vencimento_inteiro()
    {
        var noUltimoDia = CentralDeDocumentosNaTela.Linha(Emitida(publicadoAte: Hoje, jaTeveLink: true), Hoje, PerfisAcesso.Todas);
        noUltimoDia.LinkNoAr.Should().BeTrue();
        noUltimoDia.Link.Should().Be("link no ar até 16/10/2026");

        CentralDeDocumentosNaTela.Linha(Emitida(publicadoAte: Hoje.AddDays(-1), jaTeveLink: true), Hoje, PerfisAcesso.Todas)
            .Link.Should().Be("link fora do ar");
    }

    [Fact]
    public void Folha_fora_do_catalogo_nao_vira_botao_aceso()
        => CentralDeDocumentosNaTela.Linha(Emitida(chave: "inexistente"), Hoje, PerfisAcesso.Todas)
            .PodeMexer.Should().BeFalse();

    [Fact]
    public void Sem_o_bit_de_emitir_a_linha_nao_se_mexe()
        => CentralDeDocumentosNaTela.Linha(Emitida(), Hoje, Permissao.VerProntuario)
            .PodeMexer.Should().BeFalse();

    [Fact]
    public void O_contexto_de_hoje_so_existe_com_horario()
    {
        CentralDeDocumentosNaTela.ContextoDeHoje(new TimeOnly(14, 5)).Should().Be("tem horário hoje às 14:05");
        CentralDeDocumentosNaTela.ContextoDeHoje(null).Should().BeEmpty();
    }
}
