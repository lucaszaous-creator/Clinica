using Clinica.Application.Modelos;
using Clinica.Application.Servicos;
using Clinica.Domain;
using Clinica.Domain.Entities;
using Xunit;

namespace Clinica.Tests;

/// <summary>
/// O recorte e as frases do painel de pendências do faturamento (out/2026) — o que o
/// DashboardViewModel WPF decidia e a tela Blazor passou a pedir à Application.
/// </summary>
public sealed class PainelPendenciasFaturamentoTests
{
    private static PendenciaCodigo P(int id, OrdemCodigo ordem, NivelUrgencia urgencia,
        Convenio convenio = Convenio.UnimedIntercambio, TipoCodigo tipo = TipoCodigo.Acupuntura,
        int atraso = 0, string? telefone = "(21) 97777-1234", FormaObtencao forma = FormaObtencao.Sistema)
        => new(id, id, $"Paciente {id} Silva", convenio, tipo, ordem, new DateOnly(2026, 9, 12), forma,
            atraso, urgencia, null, telefone) { ConvenioCodigo = convenio.ToString() };

    private static readonly List<PendenciaCodigo> Pilha =
    [
        P(1, OrdemCodigo.Primeiro, NivelUrgencia.Vermelho, atraso: 3),
        P(2, OrdemCodigo.Segundo, NivelUrgencia.Vermelho, atraso: 1),
        P(3, OrdemCodigo.Segundo, NivelUrgencia.Amarelo),
        P(4, OrdemCodigo.Primeiro, NivelUrgencia.Verde, convenio: Convenio.Amil),
    ];

    [Fact]
    public void Chips_de_uma_dupla_sao_exclusivos_e_desmarcar_os_dois_e_todas()
    {
        var f = FiltroPendencias.Nenhum.AlternarPrimeiros();
        Assert.True(f.SoPrimeiros);
        f = f.AlternarSegundos();
        Assert.True(f.SoSegundos);
        Assert.False(f.SoPrimeiros);
        f = f.AlternarSegundos();
        Assert.False(f.SoSegundos || f.SoPrimeiros);
        Assert.False(f.Ativo);

        f = f.AlternarAtrasadas().AlternarNoPrazo();
        Assert.True(f.SoNoPrazo);
        Assert.False(f.SoAtrasadas);
        // A outra dupla não é tocada.
        var g = FiltroPendencias.Nenhum.AlternarPrimeiros().AlternarAtrasadas();
        Assert.True(g.SoPrimeiros && g.SoAtrasadas);
    }

    [Fact]
    public void Aplicar_recorta_pelos_chips_e_pelos_combos()
    {
        var f = FiltroPendencias.Nenhum.AlternarSegundos().AlternarAtrasadas();
        Assert.Equal([2], f.Aplicar(Pilha).Select(p => p.CodigoId));
        Assert.Equal([4], (FiltroPendencias.Nenhum with { Convenio = Convenio.Amil }).Aplicar(Pilha).Select(p => p.CodigoId));
        Assert.Equal(4, FiltroPendencias.Nenhum.Aplicar(Pilha).Count());
    }

    [Fact]
    public void Contagens_sao_sobre_o_total_e_a_fracao_some_sem_guia()
    {
        var c = ContagensPendencias.De(Pilha);
        Assert.Equal((4, 2, 2, 2, 1), (c.Total, c.Primeiros, c.Segundos, c.Atrasadas, c.NoPrazo));
        Assert.Equal(0.5, c.FracaoUrgentes);
        Assert.Null(ContagensPendencias.De([]).FracaoUrgentes);
    }

    [Fact]
    public void O_resumo_diz_que_esta_filtrado_e_o_vazio_distingue_filtro_de_nada()
    {
        Assert.Equal("4 pendências", PainelPendenciasFaturamento.ResumoDoRecorte(4, 4, FiltroPendencias.Nenhum));
        Assert.Equal("1 pendência", PainelPendenciasFaturamento.ResumoDoRecorte(1, 1, FiltroPendencias.Nenhum));
        var f = FiltroPendencias.Nenhum.AlternarSegundos();
        Assert.Equal("2 de 4 pendência(s) — 2º código", PainelPendenciasFaturamento.ResumoDoRecorte(4, 2, f));

        Assert.Equal("Nenhuma pendência bate com o filtro", PainelPendenciasFaturamento.Vazio(4, f).Titulo);
        Assert.Contains("4 pendência(s) fora", PainelPendenciasFaturamento.Vazio(4, f).Descricao);
        // Filtro ligado sobre um painel VAZIO não é "o filtro escondeu": não há nada mesmo.
        Assert.Equal("Nenhuma pendência", PainelPendenciasFaturamento.Vazio(0, f).Titulo);
        Assert.Equal("Nenhuma pendência", PainelPendenciasFaturamento.Vazio(4, FiltroPendencias.Nenhum).Titulo);
    }

    [Fact]
    public void O_banner_so_existe_quando_a_rodada_exige_decisao()
    {
        var vencida = new RodadaPendenciasStatus(10, 3, 8, false, 0, false, 0);
        Assert.Equal("3 guia(s) estão pendentes há mais de 10 dias sem resolução. Rode agora: cada uma exige baixa ou não conformidade.",
            PainelPendenciasFaturamento.BannerDaRodada(vencida));
        Assert.Null(PainelPendenciasFaturamento.BannerDaRodada(vencida with { GuiasParaDecisao = 0 }));
    }

    [Fact]
    public void A_mensagem_da_guia_diz_a_ordem_e_lembra_o_QR_quando_o_plano_usa_app()
    {
        var segunda = PainelPendenciasFaturamento.MensagemDaGuia(P(9, OrdemCodigo.Segundo, NivelUrgencia.Amarelo, forma: FormaObtencao.App));
        Assert.StartsWith("Olá, Paciente!", segunda);
        Assert.Contains("da 2ª guia", segunda);
        Assert.Contains("12/09", segunda);
        Assert.Contains("QR Code", segunda);
        Assert.DoesNotContain("QR Code", PainelPendenciasFaturamento.MensagemDaGuia(P(9, OrdemCodigo.Primeiro, NivelUrgencia.Amarelo)));
    }

    [Fact]
    public void O_link_do_WhatsApp_assina_com_a_clinica_e_recusa_telefone_que_nao_serve()
    {
        var link = PainelPendenciasFaturamento.LinkWhatsApp("(21) 97777-1234", "Olá", "Clínica SemDor");
        Assert.StartsWith("https://wa.me/5521977771234?text=", link);
        Assert.Contains(Uri.EscapeDataString("Olá — Clínica SemDor"), link);
        Assert.Null(PainelPendenciasFaturamento.LinkWhatsApp("123", "Olá", null));
        Assert.Null(PainelPendenciasFaturamento.LinkWhatsApp(null, "Olá", null));
        Assert.EndsWith(Uri.EscapeDataString("Olá"), PainelPendenciasFaturamento.LinkWhatsApp("21977771234", "Olá", " "));
    }

    [Fact]
    public void Mensagens_de_consulta_e_carteirinha_dizem_o_prazo()
    {
        var consulta = new PendenciaConsulta(1, "Ana Lima", Convenio.Amil, new DateOnly(2026, 9, 20), 0, NivelUrgencia.Amarelo);
        Assert.Contains("está vencendo", PainelPendenciasFaturamento.MensagemDaConsulta(consulta));
        Assert.Contains("vence em 4 dia(s)", PainelPendenciasFaturamento.MensagemDaConsulta(consulta with { DiasParaVencer = 4 }));

        var cart = new PendenciaCarteirinha(1, "Ana Lima", Convenio.Amil, "X", new DateOnly(2026, 9, 1), -5, NivelUrgencia.Vermelho);
        Assert.Contains("está vencida desde 01/09/2026", PainelPendenciasFaturamento.MensagemDaCarteirinha(cart));
        Assert.Contains("vence em 3 dia(s) (01/09/2026)", PainelPendenciasFaturamento.MensagemDaCarteirinha(cart with { DiasParaVencer = 3 }));
    }

    [Fact]
    public void O_lote_recusa_numero_fora_do_formato_antes_de_baixar_e_ignora_linha_em_branco()
    {
        var linha = new LinhaDaRodada
        {
            CodigoId = 1, Descricao = "Ana — Acupuntura (1º código)", Situacao = "Unimed",
            Formato = FormatoNumeroGuia.SomenteNumeros
        };
        var linhas = new[] { linha };
        Assert.Null(PainelPendenciasFaturamento.ImpedimentoDoLote(linhas)); // em branco: ignorada

        linha.NumeroGuia = "12O45"; // o "O" no lugar do zero
        var impedimento = PainelPendenciasFaturamento.ImpedimentoDoLote(linhas);
        Assert.NotNull(impedimento);
        Assert.Contains("1 guia(s) com o número fora do formato", impedimento);
        Assert.Contains("nenhuma baixa foi feita", impedimento);
        Assert.Contains("• Ana — Acupuntura (1º código):", impedimento);

        linha.NumeroGuia = "12045";
        Assert.Null(PainelPendenciasFaturamento.ImpedimentoDoLote(linhas));
    }

    [Fact]
    public void O_aviso_do_lote_conta_o_que_saiu_e_o_que_nao()
    {
        Assert.Null(PainelPendenciasFaturamento.AvisoDoLote(3, []));
        Assert.Equal("Nenhuma linha tinha número de guia — nada foi baixado.", PainelPendenciasFaturamento.AvisoDoLote(0, []));
        Assert.StartsWith("2 guia(s) baixada(s); 1 NÃO:", PainelPendenciasFaturamento.AvisoDoLote(2, ["X: recusada"]));
    }

    [Fact]
    public void Rotulo_da_observacao_anuncia_se_ja_ha_nota()
    {
        Assert.Equal("Anotar", PainelPendenciasFaturamento.RotuloDaObservacao(false));
        Assert.Equal("Ver/editar", PainelPendenciasFaturamento.RotuloDaObservacao(true));
    }

    [Fact]
    public void Como_obter_tem_frase_para_toda_forma()
    {
        foreach (var f in Enum.GetValues<FormaObtencao>())
            Assert.DoesNotContain("Nao", PainelPendenciasFaturamento.ComoObter(f));
        Assert.Equal("pelo app do paciente", PainelPendenciasFaturamento.ComoObter(FormaObtencao.App));
    }
}
