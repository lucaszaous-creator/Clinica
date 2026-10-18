using Clinica.Application.Modelos;
using Clinica.Application.Servicos;
using Clinica.Domain;
using Clinica.Domain.Entities;
using FluentAssertions;
using Xunit;

namespace Clinica.Tests;

/// <summary>
/// O que a tela Documentos emitidos do Gerente AFIRMA (Blazor desde out/2026, etapa 6) — antes no
/// <c>DocumentosEmitidosViewModel</c> do WPF. Os números são do <see cref="PainelDeDocumentos"/>
/// (testado à parte); aqui mora a ESCRITA: travessão, "balcão", "(sem motivo registrado)", o
/// resumo do período e a resposta da conferência pelo código.
/// </summary>
public class DocumentosEmitidosNaTelaTests
{
    private static readonly DateOnly Hoje = new(2026, 10, 18);
    private static readonly DateOnly Inicio = new(2026, 10, 1);

    private static FolhaEmitida Folha(int id, string rotulo, string? paciente = "Ana Lima", string? profissional = "Dra. Helena",
        bool cancelado = false, string? motivo = null, bool assinado = false, DateOnly? publicadoAte = null, int dia = 5,
        NaturezaFolha natureza = NaturezaFolha.Clinico)
        => new(id, natureza, $"2026/{id:0000}", $"COD{id}", rotulo, paciente, profissional,
            new DateOnly(2026, 10, dia), new DateTime(2026, 10, dia, 9, 0, 0), "recepcao", cancelado, motivo)
        {
            Assinado = assinado,
            PublicadoAte = publicadoAte
        };

    [Fact]
    public void Periodo_sem_emissao_diz_que_nao_houve_e_o_mais_emitido_e_travessao()
    {
        var p = DocumentosEmitidosNaTela.Montar([], Inicio, Hoje, Hoje);

        p.Vazio.Should().BeTrue();
        p.Resumo.Should().Be("Nenhum papel emitido de 01/10/2026 a 18/10/2026.");
        p.Emitidos.Should().Be("0");
        p.MaisEmitido.Should().Be("—", "\"— 0%\" seria um número onde não há pergunta a responder");
        p.TemCanceladas.Should().BeFalse();
        p.TituloCanceladas.Should().Be("Cancelados no período · 0");
        p.Linhas.Should().BeEmpty();
        p.PorQuemAssina.Should().BeEmpty();
    }

    [Fact]
    public void Os_numeros_sao_os_do_painel_da_application_e_a_cancelada_conta_como_emitida()
    {
        var folhas = new[]
        {
            Folha(1, "Receituário", assinado: true, publicadoAte: Hoje),
            Folha(2, "Receituário", publicadoAte: Hoje.AddDays(-1)),
            Folha(3, "Atestado", cancelado: true, motivo: "Saiu com a data errada")
        };

        var p = DocumentosEmitidosNaTela.Montar(folhas, Inicio, Hoje, Hoje);

        p.Resumo.Should().Be("3 folha(s) de 01/10/2026 a 18/10/2026.");
        p.Emitidos.Should().Be("3", "o número foi gasto — a numeração por ano não se reaproveita");
        p.Assinados.Should().Be("1");
        p.Cancelados.Should().Be("1");
        p.NoAr.Should().Be("1", "o dia do vencimento conta inteiro; o de ontem já saiu do ar");
        p.MaisEmitido.Should().Be("Receituário — 66%");
        p.TituloCanceladas.Should().Be("Cancelados no período · 1");
    }

    [Fact]
    public void Linha_sem_profissional_e_do_balcao_e_sem_paciente_e_travessao()
    {
        var l = DocumentosEmitidosNaTela.Linha(
            Folha(7, "Recibo", paciente: null, profissional: null, dia: 3, natureza: NaturezaFolha.Financeiro));

        l.QuemAssinou.Should().Be("balcão");
        l.Para.Should().Be("—");
        l.Data.Should().Be("03/10");
        l.DataCompleta.Should().Be("03/10/2026");
        l.Chave.Should().Be("Financeiro:7", "o id sozinho repete entre a clínica e o financeiro");
    }

    [Fact]
    public void Cancelada_aparece_marcada_e_nunca_some_da_tabela()
    {
        var p = DocumentosEmitidosNaTela.Montar(
            [Folha(1, "Atestado", cancelado: true, motivo: "Errado"), Folha(2, "Atestado")], Inicio, Hoje, Hoje);

        p.Linhas.Should().HaveCount(2);
        p.Linhas.Single(l => l.Numero == "2026/0001").Cancelado.Should().BeTrue();
        p.Canceladas.Should().ContainSingle().Which.Motivo.Should().Be("Errado");
    }

    [Fact]
    public void Cancelada_sem_motivo_diz_isso_e_o_contexto_pula_o_que_nao_existe()
    {
        var com = DocumentosEmitidosNaTela.Cancelada(Folha(1, "Atestado", cancelado: true, motivo: "  ", dia: 9));
        com.Motivo.Should().Be("(sem motivo registrado)");
        com.Contexto.Should().Be("Dra. Helena · 09/10/2026");

        var semQuem = DocumentosEmitidosNaTela.Cancelada(Folha(2, "Recibo", profissional: null, cancelado: true, motivo: "x", dia: 9));
        semQuem.Contexto.Should().Be("09/10/2026", "sem quem assinou, o contexto não começa com \" · \"");
    }

    [Fact]
    public void Por_quem_assina_vem_do_painel_com_o_balcao_nomeado()
    {
        var p = DocumentosEmitidosNaTela.Montar(
            [Folha(1, "Receituário"), Folha(2, "Receituário"), Folha(3, "Recibo", profissional: null)], Inicio, Hoje, Hoje);

        p.PorQuemAssina.Select(q => (q.Rotulo, q.ValorRotulo)).Should().Equal(
            ("Dra. Helena", "2"), (PainelDeDocumentos.SemAssinatura, "1"));
        p.PorQuemAssina[0].Fracao.Should().Be(1);
    }

    // ==================== Conferência pelo código ====================

    private static DocumentoClinico Doc(bool cancelado = false, string? motivo = null) => new()
    {
        Numero = "2026/0012", Tipo = TipoDocumentoClinico.Receita, Data = new DateOnly(2026, 10, 3),
        Paciente = new Paciente { Nome = "Maria Silva" },
        CanceladoEm = cancelado ? new DateTime(2026, 10, 4, 10, 0, 0) : null,
        MotivoCancelamento = motivo
    };

    [Fact]
    public void Conferencia_de_papel_valido_diz_folha_numero_paciente_data_e_valido()
        => ConferenciaPeloCodigo.Achado(Doc()).Should().Be(
            $"{CentralDocumentosService.RotularClinico(TipoDocumentoClinico.Receita)} 2026/0012 · Maria Silva · emitido em 03/10/2026 · válido");

    [Fact]
    public void Conferencia_de_cancelado_diz_quando_e_o_motivo_e_sem_motivo_nao_fica_em_branco()
    {
        ConferenciaPeloCodigo.Achado(Doc(true, "Dose errada")).Should().EndWith("CANCELADO em 04/10/2026 — Dose errada");
        ConferenciaPeloCodigo.Achado(Doc(true, null)).Should().EndWith("CANCELADO em 04/10/2026 — (sem motivo registrado)");
    }

    [Fact]
    public void Conferencia_sem_paciente_e_codigo_inexistente_sao_respostas_escritas()
    {
        var d = Doc();
        d.Paciente = null;
        ConferenciaPeloCodigo.Achado(d).Should().Contain("(paciente removido)");
        ConferenciaPeloCodigo.NaoEncontrado("ABC").Should().Be(
            "Nenhum documento com o código \"ABC\". Confira a digitação; se estiver certa, o papel não saiu deste sistema.");
    }
}
