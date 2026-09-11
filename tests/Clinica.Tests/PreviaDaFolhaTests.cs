using Clinica.Application.Modelos;
using Clinica.Application.Servicos;
using FluentAssertions;

namespace Clinica.Tests;

/// <summary>
/// O PASSO 3 da emissão de documentos (mockup 5). O que a tela AFIRMA mora na Application
/// justamente para caber aqui — dentro da ViewModel nenhuma destas asserções existiria.
/// </summary>
public class PreviaDaFolhaTests
{
    private static FolhaCatalogo Folha(string chave)
        => CentralDocumentosService.Folha(chave)
           ?? throw new InvalidOperationException($"folha '{chave}' saiu do catálogo");

    private static PreviaDaFolha Montar(
        string chave, string? nome = "Lucas de Souza Santos",
        string? documento = "178.747.197-78", string pendencia = "", bool pode = true)
        => PreviaDaFolha.Montar(
            Folha(chave), "Emitir", pode, pendencia, nome, documento, "de 01/08/2026 a 11/09/2026");

    [Fact]
    public void O_destinatario_junta_nome_e_documento()
        => Montar("comparecimento").ParaQuem.Should().Be("Lucas de Souza Santos · 178.747.197-78");

    [Fact]
    public void Ficha_sem_documento_nao_produz_separador_solto()
    {
        // A importação do Smart Clinic deixou fichas sem documento às centenas; uma frase
        // montada por concatenação sairia "Lucas de Souza Santos · " — e o separador solto
        // se lê como dado que não carregou.
        var previa = Montar("comparecimento", documento: "");

        previa.ParaQuem.Should().Be("Lucas de Souza Santos");
        previa.ParaQuem.Should().NotEndWith("·");
    }

    [Fact]
    public void Sem_paciente_a_frase_DIZ_que_ninguem_foi_escolhido()
    {
        // Linha em branco no lugar do destinatário se lê como leitura que falhou; aqui a
        // ausência é a resposta certa, e ela precisa ser escrita.
        var previa = Montar("comparecimento", nome: null, documento: null,
            pendencia: "Escolha o paciente no passo 1.", pode: false);

        previa.ParaQuem.Should().NotBeNullOrWhiteSpace();
        previa.ParaQuem.Should().Contain("Ninguém escolhido");
        previa.PodeEmitir.Should().BeFalse();
        previa.TemPendencia.Should().BeTrue();
    }

    [Fact]
    public void A_folha_do_PERIODO_nao_fala_de_paciente_nenhum()
    {
        // Ela não passa pelo passo 1, e exigir paciente a tornaria INALCANÇÁVEL — a regra 3
        // do faturamento, que a parcela 88 já invocou para deixar esta tela de fora da
        // busca obrigatória.
        var previa = PreviaDaFolha.Montar(
            Folha(CentralDocumentosService.ChaveFechamentoPeriodo),
            "Gerar PDF", true, "", pacienteNome: null, pacienteDocumento: null,
            periodo: "de 01/08/2026 a 11/09/2026");

        previa.ParaQuem.Should().Be("de 01/08/2026 a 11/09/2026");
        previa.ParaQuem.Should().NotContain("Ninguém");
        previa.PodeEmitir.Should().BeTrue();
    }

    [Fact]
    public void O_recibo_diz_que_o_clique_NAVEGA_em_vez_de_emitir()
    {
        // "Ir para o Caixa" no botão e nada explicando: a leitura natural de uma tela que
        // troca sem sair papel é que o papel saiu.
        var previa = Montar("recibo");

        previa.OQueAcontece.Should().Contain("Caixa");
        previa.OQueAcontece.Should().Contain("dois recibos");
    }

    [Fact]
    public void O_termo_diz_que_ele_so_fica_cumprido_com_a_conferencia()
    {
        var previa = Montar("termo-procedimento");

        previa.OQueAcontece.Should().Contain("assinatura");
        previa.OQueAcontece.Should().Contain("confere");
    }

    [Fact]
    public void A_montada_do_prontuario_avisa_que_nao_se_digita_nada()
    {
        var previa = Montar("relatorio-evolucao");

        previa.OQueAcontece.Should().Contain("MONTADA");
        previa.OQueAcontece.Should().Contain("não se digita");
    }

    [Fact]
    public void O_orcamento_se_distingue_do_recibo_na_propria_frase()
    {
        // São as duas folhas do dinheiro, e confundi-las é emitir comprovante de um
        // pagamento que não houve.
        var previa = Montar("orcamento");

        previa.OQueAcontece.Should().Contain("PROPÕE");
        previa.OQueAcontece.Should().NotBe(Montar("recibo").OQueAcontece);
    }

    [Fact]
    public void TODA_folha_do_catalogo_tem_uma_frase_de_o_que_o_clique_faz()
    {
        // Asserção contra a lista COMPLETA, e não contra as folhas que eu lembrei: é o que
        // faz a folha NOVA cobrar a própria cobertura, como o `Os_sete_documentos_geram_PDF`
        // cobrou quando nasceu o oitavo tipo.
        foreach (var folha in CentralDocumentosService.Catalogo)
        {
            var previa = PreviaDaFolha.Montar(
                folha, "Emitir", true, "", "Maria", "123", "de 01/08 a 11/09");

            previa.OQueAcontece.Should().NotBeNullOrWhiteSpace(
                $"a folha '{folha.Chave}' precisa dizer o que o clique faz");
            previa.Descricao.Should().NotBeNullOrWhiteSpace(
                $"a folha '{folha.Chave}' precisa dizer o que ela é");
            previa.ParaQuem.Should().NotBeNullOrWhiteSpace(
                $"a folha '{folha.Chave}' precisa dizer para quem sai");
        }
    }

    [Fact]
    public void As_garantias_NAO_prometem_numero_nem_assinatura_digital()
    {
        // O mockup desenhava "2026/0189" no passo 3. O número é atribuído na EMISSÃO, e
        // adivinhá-lo seria escrever um número que a próxima emissão torna falso; assinatura
        // digital quem tem é o Consultório, com e-CPF.
        var previa = Montar("comparecimento");

        previa.Garantias.Should().Be(PreviaDaFolha.GarantiasDaFolha);
        previa.Garantias.Should().Contain("sai na emissão");
        previa.Garantias.Should().Contain("não se apaga");
        previa.Garantias.Should().NotContain("ICP");
        previa.Garantias.Should().NotContain("assinatura digital");
    }

    [Fact]
    public void Sem_pendencia_nao_ha_o_que_pintar_de_aviso()
    {
        Montar("comparecimento").TemPendencia.Should().BeFalse();
        Montar("comparecimento", pendencia: "Escolha o paciente no passo 1.")
            .TemPendencia.Should().BeTrue();
    }
}
