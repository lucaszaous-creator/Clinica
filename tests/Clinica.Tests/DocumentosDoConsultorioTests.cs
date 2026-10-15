using Clinica.Application.Modelos;
using Clinica.Application.Servicos;
using Clinica.Domain.Entities;
using Clinica.Domain.Prontuario;
using FluentAssertions;
using Xunit;

namespace Clinica.Tests;

/// <summary>
/// O histórico de documentos do Consultório (out/2026 — "os laudos precisam ser fáceis de
/// LOCALIZAR e REIMPRIMIR"). O que estes testes fixam é o que a tela promete: o laudo
/// aparece com o nome dele, a busca é a MESMA da central (número sem barra, código de
/// conferência, tipo, quem assinou), o combo oferece "Laudo médico" mesmo antes de haver
/// um, a régua de emitir o traz para quem prescreve, e a cor da situação segue a gravidade.
/// </summary>
public class DocumentosDoConsultorioTests
{
    private static DocumentoClinico Laudo(
        string numero = "2026/0014", string codigo = "LD77-QX12",
        DateTime? assinadoEm = null, DateTime? canceladoEm = null)
        => new()
        {
            Id = 14,
            Tipo = TipoDocumentoClinico.LaudoMedico,
            Numero = numero,
            CodigoVerificacao = codigo,
            PacienteId = 3,
            Paciente = new Paciente { Id = 3, Nome = "Maria das Dores" },
            Profissional = new Profissional { Id = 9, Nome = "Helena Duarte" },
            Data = new DateOnly(2026, 10, 9),
            CriadoEm = new DateTime(2026, 10, 9, 10, 0, 0),
            AssinaturaTipo = assinadoEm is null ? null : TipoAssinatura.IcpBrasil,
            AssinadoEm = assinadoEm,
            CanceladoEm = canceladoEm,
            MotivoCancelamento = canceladoEm is null ? null : "substituído pela nova versão"
        };

    [Fact]
    public void O_laudo_vira_linha_com_o_nome_e_a_chave_do_catalogo()
    {
        var folha = CentralDocumentosService.ComoFolha(Laudo());

        folha.FolhaRotulo.Should().Be("Laudo médico");
        folha.Chave.Should().Be("laudo-medico");
        folha.Numero.Should().Be("2026/0014");
        folha.Profissional.Should().Be("Helena Duarte");
        folha.PacienteId.Should().Be(3);
    }

    [Theory]
    [InlineData("2026/0014")]
    [InlineData("20260014")]      // o número sem a barra, como sai digitado
    [InlineData("0014")]
    [InlineData("ld77qx12")]      // o código de conferência sem hífen nem caixa
    [InlineData("laudo")]
    [InlineData("helena")]        // quem assinou
    public void O_laudo_e_achado_pela_busca_da_central(string termo)
        => BuscaNasFolhas.Casa(CentralDocumentosService.ComoFolha(Laudo()), termo, "Laudo médico")
            .Should().BeTrue();

    [Fact]
    public void O_tipo_Laudo_medico_recorta_e_outro_tipo_nao_acha_o_laudo()
    {
        var folha = CentralDocumentosService.ComoFolha(Laudo());

        BuscaNasFolhas.Casa(folha, null, "Laudo médico").Should().BeTrue();
        BuscaNasFolhas.Casa(folha, null, "Atestado").Should().BeFalse();
    }

    [Fact]
    public void O_combo_oferece_Laudo_medico_antes_de_haver_um()
    {
        // ⚠️ Pelo catálogo: "este paciente não tem laudo" não pode virar "o combo não sabe
        // filtrar laudo".
        var tipos = DocumentosDoConsultorio.Tipos(Permissao.VerProntuario | Permissao.VerFichaPaciente, []);

        tipos[0].Should().Be(BuscaNasFolhas.TodasAsFolhas);
        tipos.Should().Contain("Laudo médico").And.Contain("Atestado").And.Contain("Receituário");
        tipos.Should().NotContain("Recibo de pagamento", "o histórico é do paciente na clínica, não do caixa");
    }

    [Fact]
    public void Quem_nao_le_prontuario_nao_ve_o_laudo_no_combo()
    {
        // O laudo carrega diagnóstico (art. 5º, II): oferecê-lo no filtro anunciaria que ele
        // existe para quem só tem o cadastro.
        var tipos = DocumentosDoConsultorio.Tipos(Permissao.VerFichaPaciente, []);

        tipos.Should().NotContain("Laudo médico");
        tipos.Should().Contain("Declaração de comparecimento");
    }

    [Fact]
    public void A_regua_de_emitir_traz_o_laudo_para_quem_prescreve()
    {
        var folhas = DocumentosDoConsultorio.ParaEmitir(
            Permissao.Prescrever | Permissao.VerProntuario | Permissao.EditarProntuario);

        folhas.Select(f => f.Rotulo).Should().Contain("Laudo médico").And.Contain("Atestado");
        folhas.Should().NotContain(f => TipoDocumentoInfo.AssinadoPeloPaciente(f.TipoClinico!.Value),
            "termo se COLHE com o traço do paciente, não se emite por janela");
    }

    [Fact]
    public void Sem_o_bit_de_prescrever_o_laudo_nao_entra_na_regua()
        => DocumentosDoConsultorio.ParaEmitir(Permissao.VerProntuario | Permissao.EditarPaciente)
            .Select(f => f.Rotulo)
            .Should().NotContain("Laudo médico").And.Contain("Declaração de comparecimento");

    [Fact]
    public void Situacao_cancelado_vence_assinado_e_cada_uma_tem_sua_cor()
    {
        DocumentosDoConsultorio.Situacao(Laudo()).Estado.Should().Be(EstadoDoDocumento.Emitido);

        var assinado = DocumentosDoConsultorio.Situacao(Laudo(assinadoEm: new DateTime(2026, 10, 9, 11, 0, 0)));
        assinado.Estado.Should().Be(EstadoDoDocumento.Assinado);
        assinado.Texto.Should().Contain("e-CPF").And.Contain("09/10/2026");

        // Um laudo selado e depois substituído por nova versão NÃO vale — verde mandaria
        // reimprimir o papel que foi trocado.
        var cancelado = DocumentosDoConsultorio.Situacao(Laudo(
            assinadoEm: new DateTime(2026, 10, 9, 11, 0, 0),
            canceladoEm: new DateTime(2026, 10, 12, 9, 0, 0)));
        cancelado.Estado.Should().Be(EstadoDoDocumento.Cancelado);
        cancelado.Texto.Should().Contain("12/10/2026");
    }

    [Fact]
    public void O_resumo_conta_o_que_foi_lido_e_os_cancelados()
    {
        DocumentosDoConsultorio.Resumo([]).Should().BeEmpty();
        DocumentosDoConsultorio.Resumo([CentralDocumentosService.ComoFolha(Laudo())])
            .Should().Be("1 documento deste paciente");

        var lidas = new[]
        {
            CentralDocumentosService.ComoFolha(Laudo()),
            CentralDocumentosService.ComoFolha(Laudo("2026/0015", canceladoEm: new DateTime(2026, 10, 12))),
            CentralDocumentosService.ComoFolha(Laudo("2026/0016", canceladoEm: new DateTime(2026, 10, 13)))
        };
        DocumentosDoConsultorio.Resumo(lidas).Should().Be("3 documentos deste paciente · 2 cancelados");
    }

    [Fact]
    public void O_vazio_separa_nao_ha_documento_de_nenhum_bate_com_a_busca()
    {
        var semNada = DocumentosDoConsultorio.Vazio(haDocumentos: false);
        var filtrado = DocumentosDoConsultorio.Vazio(haDocumentos: true);

        semNada.Titulo.Should().Be("Nenhum documento emitido para este paciente");
        filtrado.Titulo.Should().Be("Nenhum documento bate com a busca");
        filtrado.Descricao.Should().Contain("Limpar busca");
    }

    [Fact]
    public void Na_linha_do_tempo_do_paciente_o_laudo_aparece_como_Laudo_medico()
    {
        var linhas = LinhaDoTempoClinica.Montar(
            Permissao.VerProntuario | Permissao.VerFichaPaciente, documentos: [Laudo()]);

        var item = linhas[NaturezaRegistroClinico.DocumentoClinico].Should().ContainSingle().Subject;
        item.Titulo.Should().Be("Laudo médico 2026/0014");
    }
}
