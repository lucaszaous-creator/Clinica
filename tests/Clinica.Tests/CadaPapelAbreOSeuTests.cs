using Clinica.Application.Modelos;
using Clinica.Application.Servicos;
using Clinica.Domain.Entities;
using FluentAssertions;
using Xunit;

namespace Clinica.Tests;

/// <summary>
/// CADA PAPEL ABRE O SEU FORMULÁRIO (out/2026 — a clínica: "durante o atendimento, quando
/// clico para emitir receita, comparecimento e afins, tem de ir ao caminho correto e não ao
/// laudo").
///
/// As três portas — os cartões de "Novo documento" (Consultório e central), a escolha da
/// ficha do paciente e os botões EMITIR do atendimento — entregam uma FOLHA do catálogo, e
/// é o <see cref="FolhaCatalogo.TipoClinico"/> dela que a janela abre. Estes testes prendem
/// a tradução chave → tipo e o ícone de cada cartão: um tipo trocado aqui não quebra build
/// nenhum, só abre o papel errado.
/// </summary>
public class CadaPapelAbreOSeuTests
{
    public static IEnumerable<object[]> Chaves() =>
    [
        ["receita", TipoDocumentoClinico.Receita],
        ["atestado", TipoDocumentoClinico.Atestado],
        ["laudo-medico", TipoDocumentoClinico.LaudoMedico],
        ["comparecimento", TipoDocumentoClinico.Comparecimento],
        ["pedido-exame", TipoDocumentoClinico.PedidoExame],
        ["relatorio-evolucao", TipoDocumentoClinico.RelatorioEvolucao]
    ];

    [Theory]
    [MemberData(nameof(Chaves))]
    public void A_chave_do_botao_do_atendimento_abre_o_papel_dela(string chave, TipoDocumentoClinico tipo)
    {
        // Os botões EMITIR do atendimento passam a CHAVE ("receita", "atestado"…).
        CentralDocumentosService.Folha(chave)!.TipoClinico.Should().Be(tipo);
        FolhasParaEntregar.Ordem.Should().Contain(chave);
    }

    [Fact]
    public void Os_cartoes_cobrem_cada_tipo_uma_vez_so_e_nenhum_e_o_laudo_por_engano()
    {
        var cartoes = DocumentosDoConsultorio.Cartoes(PerfisAcesso.Todas);

        cartoes.Select(c => c.Folha.TipoClinico).Should().OnlyHaveUniqueItems();
        cartoes.Should().Contain(c => c.Folha.TipoClinico == TipoDocumentoClinico.Receita);
        cartoes.Should().Contain(c => c.Folha.TipoClinico == TipoDocumentoClinico.Comparecimento);

        // O cartão diz o nome do papel que ele abre — rótulo e tipo andam juntos.
        foreach (var c in cartoes)
            c.Rotulo.Should().Be(CentralDocumentosService.Catalogo
                .Single(f => f.TipoClinico == c.Folha.TipoClinico).Rotulo);
    }

    [Fact]
    public void Os_cartoes_seguem_o_acesso_de_quem_emite()
    {
        // Quem só cadastra (o balcão sem Prescrever) vê o comparecimento e não a receita.
        var balcao = DocumentosDoConsultorio.Cartoes(Permissao.EditarPaciente);

        balcao.Should().Contain(c => c.Folha.TipoClinico == TipoDocumentoClinico.Comparecimento);
        balcao.Should().NotContain(c => c.Folha.TipoClinico == TipoDocumentoClinico.Receita);
        balcao.Should().NotContain(c => c.Folha.TipoClinico == TipoDocumentoClinico.LaudoMedico);
    }

    [Fact]
    public void Cada_folha_do_catalogo_tem_um_icone_e_os_papeis_clinicos_tem_icones_diferentes()
    {
        foreach (var folha in CentralDocumentosService.Catalogo)
            DocumentosDoConsultorio.Icone(folha).Should().NotBeNullOrWhiteSpace();

        DocumentosDoConsultorio.Cartoes(PerfisAcesso.Todas)
            .Select(c => c.Icone).Should().OnlyHaveUniqueItems(
                "o ícone é a primeira coisa que distingue um papel do outro na régua");
    }
}
