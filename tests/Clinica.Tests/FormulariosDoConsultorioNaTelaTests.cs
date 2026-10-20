using Clinica.Application.Formatacao;
using Clinica.Application.Modelos;
using Clinica.Application.Servicos;
using Clinica.Domain.Entities;
using Xunit;

namespace Clinica.Tests;

/// <summary>
/// O que os formulários Blazor que vieram das janelas WPF do Consultório e do balcão AFIRMAM
/// (out/2026): o quick-view do prontuário (<see cref="ResumoProntuarioNaTela"/>), os resultados do
/// pedido de exame (<see cref="PedidosDeExameNaTela.ResultadosDoPedido"/>), a cobrança no balcão
/// (<see cref="CobrancaDoPacienteNaTela"/>) e o aviso do envio do atestado sem certificado. As
/// regras moravam nos ViewModels WPF, onde o teste não alcançava.
/// </summary>
public class FormulariosDoConsultorioNaTelaTests
{
    // ==================== Quick-view do prontuário ====================

    private static Evolucao Ev(int id, DateOnly data, string? texto = null, string? queixa = null,
        int? antes = null, int? depois = null, string? conduta = null, string? plano = null,
        string? profissional = null, string? criadoPor = null) => new()
    {
        Id = id, Data = data, TextoEvolucao = texto, QueixaPrincipal = queixa, EvaAntes = antes, EvaDepois = depois,
        Conduta = conduta, PlanoTerapeutico = plano, CriadoPor = criadoPor,
        Profissional = profissional is null ? null : new Profissional { Nome = profissional }
    };

    [Fact]
    public void As_sessoes_vem_da_mais_recente_e_o_id_desempata_o_mesmo_dia()
    {
        var d = new DateOnly(2026, 10, 1);
        var lista = ResumoProntuarioNaTela.Evolucoes([Ev(1, d, "a"), Ev(3, d.AddDays(-5), "c"), Ev(2, d, "b")]);
        Assert.Equal([2, 1, 3], lista.Select(e => e.EvolucaoId));
        Assert.Equal("01/10/2026", lista[0].Data);
    }

    [Fact]
    public void O_autor_e_quem_atendeu_e_cai_para_quem_digitou_e_depois_para_a_frase()
    {
        var d = new DateOnly(2026, 10, 1);
        var lista = ResumoProntuarioNaTela.Evolucoes([
            Ev(3, d, "x", profissional: "Dra. Ana", criadoPor: "recepcao"),
            Ev(2, d, "x", criadoPor: "recepcao"),
            Ev(1, d, "x")]);
        Assert.Equal(["Dra. Ana", "recepcao", "sem autor registrado"], lista.Select(e => e.Autor));
    }

    [Fact]
    public void Sem_texto_a_entrada_compoe_queixa_eva_e_conduta_e_nunca_fica_em_branco()
    {
        var d = new DateOnly(2026, 10, 1);
        Assert.Equal("Lombalgia · EVA 8 → 3 · Agulhamento", ResumoProntuarioNaTela.ComporTexto(Ev(1, d, queixa: " Lombalgia ", antes: 8, depois: 3, conduta: "Agulhamento")));
        Assert.Equal("EVA 6", ResumoProntuarioNaTela.ComporTexto(Ev(1, d, antes: 6)));
        Assert.Equal("(sessão sem texto)", ResumoProntuarioNaTela.ComporTexto(Ev(1, d)));
        Assert.Equal("Escrito", ResumoProntuarioNaTela.ComporTexto(Ev(1, d, texto: "  Escrito  ", queixa: "ignorada")));
    }

    [Fact]
    public void O_plano_e_o_ultimo_escrito_nao_o_ultimo_da_lista()
    {
        var d = new DateOnly(2026, 10, 1);
        Assert.Equal("Plano B", ResumoProntuarioNaTela.PlanoTerapeutico([Ev(1, d.AddDays(-9), plano: "Plano A"), Ev(2, d.AddDays(-2), plano: "Plano B"), Ev(3, d, plano: " ")]));
        Assert.Null(ResumoProntuarioNaTela.PlanoTerapeutico([Ev(1, d)]));
    }

    [Fact]
    public void A_anamnese_so_traz_o_que_foi_escrito_na_ordem_da_folha()
    {
        Assert.Empty(ResumoProntuarioNaTela.Anamnese(null));
        var blocos = ResumoProntuarioNaTela.Anamnese(new AnamnesePaciente
        {
            Observacoes = "Obs", AntecedentesPessoais = " HAS ", HabitosDeVida = "  "
        });
        Assert.Equal(["Antecedentes pessoais", "Observações"], blocos.Select(b => b.Rotulo));
        Assert.Equal("HAS", blocos[0].Texto);
    }

    [Fact]
    public void Aberto_de_uma_anamnese_o_modal_cai_na_aba_dela()
    {
        Assert.Equal(ResumoProntuarioNaTela.AbaEvolucoes, ResumoProntuarioNaTela.AbaInicial(null));
        Assert.Equal(ResumoProntuarioNaTela.AbaAnamnese, ResumoProntuarioNaTela.AbaInicial(42));
    }

    [Fact]
    public void Diagnosticos_vazios_viram_nulo_para_a_tela_escrever_o_traco()
    {
        Assert.Null(ResumoProntuarioNaTela.Diagnosticos([]));
        Assert.Equal("M54.5 · F32", ResumoProntuarioNaTela.Diagnosticos(["M54.5", "F32"]));
    }

    [Fact]
    public void O_nome_da_segunda_via_sai_saneado()
        => Assert.Equal("Anamnese-Maria_Silva.pdf", ResumoProntuarioNaTela.NomeDaSegundaVia("Maria/Silva"));

    // ==================== Resultados do pedido ====================

    [Fact]
    public void Os_resultados_sao_so_os_deste_pedido_e_vigentes()
    {
        var lista = PedidosDeExameNaTela.ResultadosDoPedido([
            new ResultadoExame { Id = 1, PedidoDocumentoId = 10, Data = new DateOnly(2026, 10, 2), Nome = "Glicada", Valor = "6,1", Unidade = "%", Laboratorio = "Lab A", Referencia = " " },
            new ResultadoExame { Id = 2, PedidoDocumentoId = 11, Nome = "Outro pedido", Valor = "1" },
            new ResultadoExame { Id = 3, PedidoDocumentoId = 10, Nome = "Cancelado", Valor = "2", CanceladoEm = DateTime.Today },
            new ResultadoExame { Id = 4, PedidoDocumentoId = null, Nome = "Avulso", Valor = "3" },
            new ResultadoExame { Id = 5, PedidoDocumentoId = 10, Nome = "Só o laudo", ArquivoNome = "laudo.pdf" },
        ], 10);
        Assert.Equal([1, 5], lista.Select(r => r.Id));
        Assert.Equal("6,1 %", lista[0].Valor);
        Assert.Equal("02/10/2026", lista[0].Data);
        Assert.Null(lista[0].Referencia);
        Assert.False(lista[0].TemArquivo);
        // O registro que é só o laudo diz o que ele é, e oferece abrir.
        Assert.Equal("laudo em arquivo — laudo.pdf", lista[1].Valor);
        Assert.True(lista[1].TemArquivo);
    }

    [Fact]
    public void O_cabecalho_do_pedido_diz_o_numero_e_a_data()
        => Assert.Equal("Pedido 2026/0001 · 01/10/2026", PedidosDeExameNaTela.PedidoTexto(
            new PedidoDeExameLinha(1, "2026/0001", 7, "Maria", new DateOnly(2026, 10, 1), "Hemograma", 1, 0, false, null)));

    // ==================== Cobrança no balcão ====================

    [Fact]
    public void As_contas_vem_da_mais_antiga_com_o_atraso_por_extenso_e_em_reais()
    {
        var devedor = new PacienteInadimplente(7, "Maria", null, 360m, 2, 30, new DateOnly(2026, 9, 1),
        [
            new ContaEmAtraso(2, "Parcela 2/3", new DateOnly(2026, 9, 20), 1, 180m),
            new ContaEmAtraso(1, "Parcela 1/3", new DateOnly(2026, 9, 1), 30, 180m),
        ]);
        var contas = CobrancaDoPacienteNaTela.Contas(devedor);
        Assert.Equal([1, 2], contas.Select(c => c.LancamentoId));
        Assert.Equal("vencida há 30 dias", contas[0].Atraso);
        Assert.Equal("vencida há 1 dia", contas[1].Atraso);
        Assert.Equal("01/09/2026", contas[0].Vencimento);
        Assert.Equal("R$ 180,00", contas[0].ValorTexto);
        Assert.Equal("2 conta(s) vencida(s), somando R$ 360,00 · a mais antiga de 01/09/2026.", CobrancaDoPacienteNaTela.Resumo(devedor));
    }

    [Fact]
    public void Sem_devedor_nao_ha_conta_e_a_frase_diz_que_o_alerta_some()
    {
        Assert.Empty(CobrancaDoPacienteNaTela.Contas(null));
        Assert.Contains("Nenhuma conta vencida", CobrancaDoPacienteNaTela.Resumo(null));
    }

    // ==================== Envio ao paciente ====================

    [Fact]
    public void So_o_atestado_sem_certificado_leva_o_aviso_do_art_13()
    {
        Assert.Contains("atestado em arquivo", EnvioAoPacienteService.AvisoSemAssinatura(TipoDocumentoClinico.Atestado, false, "Atestado"));
        Assert.Null(EnvioAoPacienteService.AvisoSemAssinatura(TipoDocumentoClinico.Atestado, true, "Atestado"));
        Assert.Null(EnvioAoPacienteService.AvisoSemAssinatura(TipoDocumentoClinico.Receita, false, "Receituário"));
    }
}
