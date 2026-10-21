using Clinica.Application.Modelos;
using Clinica.Application.Servicos;
using Clinica.Domain;
using Clinica.Domain.Entities;
using Xunit;

namespace Clinica.Tests;

/// <summary>
/// O que a tela "Lançar e marcar atendimento" (Blazor, out/2026) AFIRMA. As regras saíram do
/// NovoAtendimentoViewModel WPF — onde o <c>dotnet test</c> não alcançava — para
/// <see cref="AtendimentoDoBalcaoNaTela"/>, com as MESMAS frases.
/// </summary>
public sealed class AtendimentoDoBalcaoNaTelaTests
{
    private static readonly DateOnly Hoje = new(2026, 10, 14);

    private static CodigoFaturamento Codigo(OrdemCodigo ordem, DateOnly prevista, TipoCodigo tipo = TipoCodigo.Acupuntura,
        StatusCodigo status = StatusCodigo.Aberto, DateOnly? baixa = null) => new()
    {
        Tipo = tipo, Ordem = ordem, DataPrevistaFaturamento = prevista, Status = status,
        FormaObtencao = FormaObtencao.Sistema, DataBaixa = baixa
    };

    private static PreviaLancamento Previa(params CodigoFaturamento[] codigos) => new(codigos, Categoria.Verde, []);

    [Fact]
    public void O_botao_promete_o_numero_de_guias_e_o_horario_do_dia()
    {
        Assert.Equal("Lançar e gerar 2 guias", AtendimentoDoBalcaoNaTela.RotuloDoBotao(false, false, 2, "qua", null));
        Assert.Equal("Lançar no horário das 09h00 e gerar 1 guia", AtendimentoDoBalcaoNaTela.RotuloDoBotao(false, false, 1, "qua", "09h00"));
        Assert.Equal("Lançar atendimento", AtendimentoDoBalcaoNaTela.RotuloDoBotao(false, true, 0, "qua", null));
        Assert.Equal("Lançar e gerar as guias", AtendimentoDoBalcaoNaTela.RotuloDoBotao(false, false, null, "qua", null));
        // Marcar com a chave DESLIGADA não promete guia: ela nasce no Finalizar do profissional.
        Assert.Equal("Marcar qua às 14:30", AtendimentoDoBalcaoNaTela.RotuloDoBotao(true, false, 2, "qua às 14:30", null));
        Assert.Equal("Marcar qua às 14:30 e gerar 2 guias", AtendimentoDoBalcaoNaTela.RotuloDoBotao(true, true, 2, "qua às 14:30", null));
    }

    [Fact]
    public void A_previa_so_aparece_onde_a_guia_nasce_neste_clique()
    {
        Assert.True(AtendimentoDoBalcaoNaTela.MostraPrevia(marcar: false, chaveLigada: false));
        Assert.False(AtendimentoDoBalcaoNaTela.MostraPrevia(marcar: true, chaveLigada: false));
        Assert.True(AtendimentoDoBalcaoNaTela.MostraPrevia(marcar: true, chaveLigada: true));
        // A exigência do convênio escolhido (parcela 92) segue o mesmo momento.
        Assert.False(AtendimentoDoBalcaoNaTela.NasceAtendimento(marcar: true, chaveLigada: false));
    }

    [Fact]
    public void A_consequencia_da_modalidade_diz_quantas_guias_e_quando_a_segunda_libera()
    {
        var dupla = Previa(Codigo(OrdemCodigo.Primeiro, Hoje), Codigo(OrdemCodigo.Segundo, Hoje.AddDays(1)));
        var c = AtendimentoDoBalcaoNaTela.Consequencia(dupla);
        Assert.Equal("2 guias", c.QuantasGuias);
        Assert.Equal("a 2ª libera 15/10", c.Quando);
        Assert.True(c.TemSegundoCodigo);

        var simples = AtendimentoDoBalcaoNaTela.Consequencia(Previa(Codigo(OrdemCodigo.Primeiro, Hoje)));
        Assert.Equal(("1 guia", "tudo hoje", false), (simples.QuantasGuias, simples.Quando, simples.TemSegundoCodigo));
        Assert.Equal(ConsequenciaDaModalidade.SemPrevia, AtendimentoDoBalcaoNaTela.Consequencia(null));
    }

    [Fact]
    public void A_previa_esconde_o_nao_aplicavel_e_marca_o_segundo_codigo()
    {
        var previa = Previa(
            Codigo(OrdemCodigo.Segundo, Hoje.AddDays(1), TipoCodigo.Eletroacupuntura),
            Codigo(OrdemCodigo.Primeiro, Hoje),
            Codigo(OrdemCodigo.Primeiro, Hoje, TipoCodigo.Consulta, StatusCodigo.NaoAplicavel));
        var linhas = AtendimentoDoBalcaoNaTela.LinhasDaPrevia(previa);
        Assert.Equal(2, linhas.Count);
        Assert.Equal("1º", linhas[0].Ordem);
        Assert.True(linhas[1].EhSegundo);
        Assert.Contains("se esquece", linhas[1].Nota);
        Assert.Equal("2 guias · a 2ª libera 15/10", AtendimentoDoBalcaoNaTela.ResumoCurtoPrevia(previa));
        Assert.Equal("1 guia · hoje", AtendimentoDoBalcaoNaTela.ResumoCurtoPrevia(Previa(Codigo(OrdemCodigo.Primeiro, Hoje))));
    }

    [Fact]
    public void O_primeiro_codigo_so_muda_quando_deixa_de_servir()
    {
        Assert.Equal(TipoCodigo.Eletroacupuntura,
            AtendimentoDoBalcaoNaTela.PrimeiroCodigoApos(ModalidadeAtendimento.AcupunturaComEletro, TipoCodigo.Eletroacupuntura));
        Assert.Equal(TipoCodigo.Bsv,
            AtendimentoDoBalcaoNaTela.PrimeiroCodigoApos(ModalidadeAtendimento.BsvComAcupuntura, TipoCodigo.Eletroacupuntura));
        Assert.Null(AtendimentoDoBalcaoNaTela.PrimeiroCodigoApos(ModalidadeAtendimento.Consulta, TipoCodigo.Bsv));
    }

    [Fact]
    public void O_horario_do_dia_tem_tres_estados_e_frase_por_modo()
    {
        var h = new HorarioEmAberto(7, new DateTime(2026, 10, 14, 9, 0, 0), "Consulta", "Consulta", null, "Dra. Ana", false, false, true);
        var lancar = AtendimentoDoBalcaoNaTela.AvisoHorarioDoDia(h, 1, false, marcar: false, Hoje)!;
        Assert.Contains("09h00 · Consulta · Dra. Ana · importado do sistema anterior (e mais 1)", lancar);
        Assert.Contains("Nenhum encaixe é criado", lancar);
        Assert.Contains("SEGUNDO", AtendimentoDoBalcaoNaTela.AvisoHorarioDoDia(h, 0, false, marcar: true, Hoje));
        // Leitura que falhou não vira silêncio — senão a tela cairia no encaixe, a duplicata.
        Assert.Contains("Não foi possível conferir", AtendimentoDoBalcaoNaTela.AvisoHorarioDoDia(null, 0, true, false, Hoje));
        Assert.Null(AtendimentoDoBalcaoNaTela.AvisoHorarioDoDia(null, 0, false, false, Hoje));
    }

    [Fact]
    public void A_capa_do_dia_avisa_e_a_pergunta_e_informada()
    {
        var capas = new[] { new CapaDoDia(1, "2026/0007", "Acupuntura", "lançado por Ana às 09:10", 2, 1) };
        Assert.Null(AtendimentoDoBalcaoNaTela.AvisoJaLancado([], Hoje));
        Assert.Contains("nº 2026/0007 (Acupuntura", AtendimentoDoBalcaoNaTela.AvisoJaLancado(capas, Hoje));
        var pergunta = AtendimentoDoBalcaoNaTela.PerguntaDeRepetido("Maria", capas, Hoje, marcar: true);
        Assert.Contains("Maria já tem atendimento em 14/10/2026", pergunta);
        Assert.EndsWith("Marcar mesmo assim?", pergunta);
    }

    [Fact]
    public void A_cota_vermelha_ambar_ou_nada()
    {
        var aut = new AutorizacaoSessoes { Numero = "123", QuantidadeAutorizada = 10, DataValidade = Hoje.AddDays(30) };
        Assert.True(AtendimentoDoBalcaoNaTela.AutorizacaoCritica(new SaldoAutorizacao(aut, 10, 0, false)));
        Assert.True(AtendimentoDoBalcaoNaTela.AutorizacaoNaUltima(new SaldoAutorizacao(aut, 9, 1, false)));
        Assert.StartsWith("Senha 123: venceu", AtendimentoDoBalcaoNaTela.SaldoAutorizacao(new SaldoAutorizacao(aut, 1, 9, true)));
        Assert.Null(AtendimentoDoBalcaoNaTela.SaldoAutorizacao(null));
    }

    [Fact]
    public void O_resultado_diz_o_que_libera_hoje_e_o_que_espera()
    {
        var codigos = new[] { Codigo(OrdemCodigo.Primeiro, Hoje), Codigo(OrdemCodigo.Segundo, Hoje.AddDays(1)) };
        var linhas = AtendimentoDoBalcaoNaTela.CodigosGerados(codigos, Hoje);
        Assert.Equal("Liberada — o faturamento já pode dar baixa", linhas[0].Situacao);
        Assert.Equal("Libera em 15/10/2026", linhas[1].Situacao);
        Assert.True(linhas[1].TemImpedimento);
        Assert.Equal("0 de 2 guia(s) já baixada(s) pelo faturamento — a próxima libera em 15/10/2026 e entra no painel de pendências do faturamento.",
            AtendimentoDoBalcaoNaTela.ResumoBaixas(codigos, Hoje));
        Assert.Null(AtendimentoDoBalcaoNaTela.ResumoBaixas([Codigo(OrdemCodigo.Primeiro, Hoje, status: StatusCodigo.NaoAplicavel)], Hoje));
    }

    [Fact]
    public void Os_alertas_que_a_tela_ja_diz_nao_se_repetem()
    {
        Assert.True(AtendimentoDoBalcaoNaTela.JaDitoPelaTela(ImpedimentoElegibilidade.CarteirinhaVencida));
        Assert.False(AtendimentoDoBalcaoNaTela.JaDitoPelaTela(ImpedimentoElegibilidade.PacienteEmDebito));
    }

    [Fact]
    public void O_nome_do_arquivo_nao_leva_caractere_que_o_windows_recusa()
        => Assert.Equal("comprovante-Ana_Silva-2026.pdf", AtendimentoDoBalcaoNaTela.NomeDoArquivo("comprovante-Ana/Silva-2026.pdf"));

    // ==================== marcar → "Marcar outro" (out/2026) ====================

    [Fact]
    public void No_marcar_o_aviso_e_a_pergunta_falam_de_marcar_e_nao_de_lancar()
    {
        var capas = new[] { new CapaDoDia(1, "2026/0007", "Acupuntura", "lançado por Ana às 09:10", 2, 1) };

        var semChave = AtendimentoDoBalcaoNaTela.AvisoJaLancado(capas, Hoje, marcar: true, criaAtendimento: false)!;
        Assert.DoesNotContain("Lançar", semChave);
        Assert.Contains("SEGUNDO horário", semChave);
        Assert.DoesNotContain("guias.", semChave);

        var comChave = AtendimentoDoBalcaoNaTela.AvisoJaLancado(capas, Hoje, marcar: true, criaAtendimento: true)!;
        Assert.Contains("Marcar de novo cria OUTRO atendimento", comChave);

        // O lançar continua com a frase de sempre.
        Assert.Equal(AtendimentoDoBalcaoNaTela.AvisoJaLancado(capas, Hoje),
                     AtendimentoDoBalcaoNaTela.AvisoJaLancado(capas, Hoje, marcar: false, criaAtendimento: true));

        var pergunta = AtendimentoDoBalcaoNaTela.PerguntaDeRepetido("Maria", capas, Hoje, marcar: true, criaAtendimento: false);
        Assert.Contains("SEGUNDO horário", pergunta);
        Assert.DoesNotContain("jogo de guias", pergunta);
        Assert.EndsWith("Marcar mesmo assim?", pergunta);
    }

    [Fact]
    public void Depois_de_gravar_o_resumo_fala_no_passado()
    {
        Assert.Equal("O que vai ser marcado", AtendimentoDoBalcaoNaTela.TituloResumo(true, false));
        Assert.Equal("O que foi marcado", AtendimentoDoBalcaoNaTela.TituloResumo(true, true));
        Assert.Equal("O que foi lançado", AtendimentoDoBalcaoNaTela.TituloResumo(false, true));
    }

    [Fact]
    public void Sem_hora_o_marcar_espera_e_diz_onde_escolher()
    {
        Assert.False(AtendimentoDoBalcaoNaTela.HoraValida(""));
        Assert.False(AtendimentoDoBalcaoNaTela.HoraValida("15:"));
        Assert.True(AtendimentoDoBalcaoNaTela.HoraValida("09:30"));
        Assert.Contains("Outro horário", AtendimentoDoBalcaoNaTela.RecusaSemHora(marcar: true));
        Assert.Contains("HH:mm", AtendimentoDoBalcaoNaTela.RecusaSemHora(marcar: false));
    }

    [Fact]
    public void No_marcar_a_guia_sem_segundo_codigo_nao_diz_tudo_hoje()
    {
        var previa = Previa(Codigo(OrdemCodigo.Primeiro, Hoje));
        Assert.Equal("tudo hoje", AtendimentoDoBalcaoNaTela.Consequencia(previa).Quando);
        Assert.Equal("tudo no dia da sessão", AtendimentoDoBalcaoNaTela.Consequencia(previa, marcar: true).Quando);
    }
}
