using Clinica.Application.Modelos;
using Clinica.Domain;
using Clinica.Domain.Entities;
using Xunit;

namespace Clinica.Tests;

/// <summary>
/// As regras do rascunho automático da evolução (out/2026, "como no Excel"): quando guardar
/// sem pesar para quem digita, e o conteúdo que vai e volta sem perder campo.
/// </summary>
public class RascunhoAutomaticoTests
{
    private static readonly DateTime T0 = new(2026, 10, 22, 14, 0, 0);

    [Fact]
    public void Nao_guarda_o_que_ja_esta_guardado()
        => Assert.False(RascunhoAutomatico.DeveGuardar("a", "a", "a", T0, T0.AddSeconds(-1)));

    [Fact]
    public void Digitando_sem_parar_espera_a_pausa_ate_o_teto()
    {
        // Mudou desde a conferência anterior (está digitando) e guardou há pouco: espera.
        Assert.False(RascunhoAutomatico.DeveGuardar("abc", "a", "ab", T0, T0.AddSeconds(-5)));
        // Passou o teto: guarda mesmo digitando.
        Assert.True(RascunhoAutomatico.DeveGuardar("abc", "a", "ab", T0, T0 - RascunhoAutomatico.TetoSemGuardar));
    }

    [Fact]
    public void Parou_de_digitar_guarda_na_hora()
        => Assert.True(RascunhoAutomatico.DeveGuardar("abc", "a", "abc", T0, T0.AddSeconds(-1)));

    [Fact]
    public void A_primeira_mudanca_sem_guarda_anterior_guarda()
        => Assert.True(RascunhoAutomatico.DeveGuardar("abc", "", "ab", T0, null));

    [Fact]
    public void O_rodape_so_promete_prazo_com_a_conclusao_automatica_ligada()
    {
        Assert.Contains("24 h", RascunhoAutomatico.Rodape(T0, 24));
        Assert.DoesNotContain(" h após", RascunhoAutomatico.Rodape(T0, null));
        Assert.Contains("14:00", RascunhoAutomatico.Rodape(T0, null));
    }

    private static RascunhoDaSessao Escrito()
    {
        var r = new RascunhoDaSessao
        {
            Data = new DateOnly(2026, 10, 22), EvaAntes = 8, EvaDepois = 3,
            QueixaPrincipal = "Dor lombar", HistoriaDoencaAtual = "Há 3 meses", ExameFisico = "Lasègue negativo",
            HipoteseDiagnostica = "Lombalgia", CidSessao = "M54.5", Conduta = "Acupuntura",
            TextoEvolucao = "Paciente refere melhora.", Orientacoes = "Alongar", PlanoTerapeutico = "10 sessões",
            RetornoSugeridoEm = new DateOnly(2026, 10, 29), RetornoSugeridoNota = "reavaliar", Encaminhamento = "Fisio"
        };
        r.TextoEvolucaoFormatado = TextoFormatado.Guardar([new TrechoTexto("Paciente", Negrito: true), new TrechoTexto(" refere melhora.")]);
        r.Campos.Add(new CampoDaSessaoNaTela { Id = 7, Rotulo = "Aparelho", Tipo = TipoCampoPersonalizado.Texto, Opcoes = [], Resposta = "A" });
        return r;
    }

    [Fact]
    public void O_conteudo_vai_e_volta_sem_perder_campo_nenhum()
    {
        var original = Escrito();
        var json = ConteudoDoRascunho.De(original).Serializar();

        var volta = new RascunhoDaSessao();
        volta.Campos.Add(new CampoDaSessaoNaTela { Id = 7, Rotulo = "Aparelho", Tipo = TipoCampoPersonalizado.Texto, Opcoes = [] });
        ConteudoDoRascunho.Ler(json)!.Aplicar(volta);

        // O mesmo conteúdo serializado dos dois lados: todo campo do rascunho atravessou.
        Assert.Equal(json, ConteudoDoRascunho.De(volta).Serializar());
        Assert.Equal("A", volta.Campos.Single().Resposta);
        Assert.Equal(original.TextoEvolucaoFormatado, volta.TextoEvolucaoFormatado);
    }

    [Fact]
    public void Json_ilegivel_nao_derruba_a_folha()
    {
        Assert.Null(ConteudoDoRascunho.Ler("{não é json"));
        Assert.Null(ConteudoDoRascunho.Ler(""));
    }

    [Fact]
    public void A_evolucao_gravada_pelo_prazo_tem_o_que_o_medico_escreveu()
    {
        var c = ConteudoDoRascunho.De(Escrito());
        var definicoes = new List<CampoPersonalizadoProntuario>
            { new() { Id = 7, Rotulo = "Aparelho", Tipo = TipoCampoPersonalizado.Texto, Ativo = true } };
        var e = c.ParaEvolucao(0, pacienteId: 3, profissionalId: 5, agendamentoId: 9, definicoes);
        Assert.Equal((3, 5, 9), (e.PacienteId, e.ProfissionalId!.Value, e.AgendamentoId!.Value));
        Assert.Equal("Paciente refere melhora.", e.TextoEvolucao);
        Assert.Equal((8, 3), (e.EvaAntes!.Value, e.EvaDepois!.Value));
        Assert.Equal("M54.5", e.CidSessao);
        Assert.Single(e.CamposPersonalizados);
    }
}
