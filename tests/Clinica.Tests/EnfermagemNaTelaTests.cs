using Clinica.Application.Modelos;
using Clinica.Application.Servicos;
using Clinica.Domain;
using Clinica.Domain.Entities;
using Clinica.Domain.Medidas;
using Xunit;

namespace Clinica.Tests;

/// <summary>
/// O que as telas Blazor da ENFERMAGEM afirmam (out/2026 — a seção "Atendimento de enfermagem"
/// e a tela Enfermagem saíram do WPF): as regras moram na Application, puras, e é aqui que elas
/// se provam — Razor não é lugar de regra.
/// </summary>
public sealed class EnfermagemNaTelaTests
{
    private static readonly DateOnly Hoje = new(2026, 10, 19);

    // ==================== o compositor ====================

    [Theory]
    [InlineData("14:20", 14, 20)]
    [InlineData(" 8:05 ", 8, 5)]
    public void A_hora_do_fato_e_lida_nos_dois_formatos(string texto, int h, int m)
        => Assert.Equal(new TimeOnly(h, m), PassagemDeEnfermagemNaTela.LerHora(texto));

    [Theory]
    [InlineData("")]
    [InlineData("25:99")]
    [InlineData("agora")]
    public void Hora_invalida_ou_vazia_nao_vira_hora(string texto)
        => Assert.Null(PassagemDeEnfermagemNaTela.LerHora(texto));

    [Fact]
    public void A_PA_num_campo_so_vira_o_par_e_a_temperatura_aceita_virgula_e_ponto()
    {
        var a = PassagemDeEnfermagemNaTela.LerSinaisVitais("120/80", "76", null, "36,5", "98", null);
        var b = PassagemDeEnfermagemNaTela.LerSinaisVitais("120/80", "76", null, "36.5", "98", null);
        Assert.NotNull(a);
        Assert.Equal(a, b);
        Assert.Equal(120, a!.PressaoSistolica);
        Assert.Equal(80, a.PressaoDiastolica);
        Assert.Equal(36.5m, a.Temperatura);
    }

    [Fact]
    public void Nada_aferido_e_nulo_e_nao_um_registro_de_zeros()
        => Assert.Null(PassagemDeEnfermagemNaTela.LerSinaisVitais("", " ", null, null, "", null));

    [Fact]
    public void Acesso_em_branco_e_nao_avaliado_nunca_ausencia_afirmada()
        => Assert.Null(PassagemDeEnfermagemNaTela.LerAcesso(" ", null, null));

    [Fact]
    public void Consulta_incompleta_AVISA_o_que_falta_e_anotacao_nao_cobra_etapa()
    {
        Assert.Null(PassagemDeEnfermagemNaTela.EtapasEmFalta(false, null, [], 0, null));

        var aviso = PassagemDeEnfermagemNaTela.EtapasEmFalta(true, "colhido", [("Dor aguda", null)], 0, null);
        Assert.NotNull(aviso);
        Assert.Contains("resultado esperado", aviso);
        Assert.Contains("prescrição de enfermagem", aviso);
        Assert.Contains("avaliação", aviso);
        Assert.DoesNotContain("histórico", aviso);

        Assert.Null(PassagemDeEnfermagemNaTela.EtapasEmFalta(true, "h", [("Dor", "Dor ≤ 4")], 2, "melhorou"));
    }

    [Fact]
    public void Compositor_em_branco_olha_TUDO_o_que_grava_e_nao_so_o_texto()
    {
        Assert.True(PassagemDeEnfermagemNaTela.CompositorEmBranco(null, null, null, null, null, null, null, [], []));
        // Só a PA: a passagem mais comum da sala — não está em branco.
        var sinais = PassagemDeEnfermagemNaTela.LerSinaisVitais("110/70", null, null, null, null, null);
        Assert.False(PassagemDeEnfermagemNaTela.CompositorEmBranco(null, null, sinais, null, null, null, null, [], []));
        Assert.False(PassagemDeEnfermagemNaTela.CompositorEmBranco(null, "dipirona", null, null, null, null, null, [], []));
        Assert.False(PassagemDeEnfermagemNaTela.CompositorEmBranco(null, null, null, null, null, null, null, [], ["Curativo"]));
    }

    // ==================== a lista de passagens ====================

    [Fact]
    public void Lista_marca_cancelada_e_corrigida_e_acende_DESTA_SESSAO_so_no_horario_aberto()
    {
        var original = Passagem(1, new TimeOnly(9, 0), agendamento: 50);
        var correcao = Passagem(2, new TimeOnly(9, 30), agendamento: 50);
        correcao.RetificaEvolucaoId = 1;
        correcao.MotivoRetificacao = "hora errada";
        var cancelada = Passagem(3, new TimeOnly(8, 0), agendamento: null);
        cancelada.CanceladaEm = Hoje.ToDateTime(new TimeOnly(10, 0));
        cancelada.MotivoCancelamento = "paciente errado";

        var linhas = PassagemDeEnfermagemNaTela.Linhas([original, correcao, cancelada], mostrarData: true, agendamentoAberto: 50);

        Assert.Equal([2, 1, 3], linhas.Select(l => l.Id));           // da mais recente para a mais antiga
        Assert.True(linhas.Single(l => l.Id == 1).Substituida);      // a corrigida aparece MARCADA
        Assert.False(linhas.Single(l => l.Id == 1).Vigente);
        Assert.Contains("CANCELADA", linhas.Single(l => l.Id == 3).Marca);
        Assert.True(linhas.Single(l => l.Id == 2).DestaSessao);
        Assert.False(linhas.Single(l => l.Id == 3).DestaSessao);      // nulo dos dois lados NÃO casa
        Assert.False(PassagemDeEnfermagemNaTela.Linhas([cancelada], true, null).Single().DestaSessao);
    }

    [Fact]
    public void So_a_passagem_vigente_e_avulsa_pode_ser_vinculada_a_sessao()
    {
        var avulsa = PassagemDeEnfermagemNaTela.Linha(Passagem(1, new TimeOnly(9, 0), null), false, true, null);
        var ligada = PassagemDeEnfermagemNaTela.Linha(Passagem(2, new TimeOnly(9, 0), 7), false, true, null);
        Assert.True(avulsa.PodeVincular);
        Assert.False(ligada.PodeVincular);
    }

    // ==================== o plano de cuidados ====================

    [Fact]
    public void Se_necessario_NAO_conta_como_pendente_e_ganha_selo_proprio()
    {
        var plano = new PlanoDeCuidadosDoDia(Hoje, 1, Hoje, "Joana",
        [
            new CuidadoDoDia(10, "Aferir sinais vitais — a cada 15 min", false, []),
            new CuidadoDoDia(11, "Compressa fria — se dor", true, [])
        ]);

        var linhas = PlanoDeCuidadosNaTela.Linhas(plano);

        Assert.True(linhas[0].Pendente);
        Assert.Equal("aguardando", linhas[0].Selo);
        Assert.False(linhas[1].Pendente);
        Assert.Equal("se necessário", linhas[1].Selo);
        Assert.Contains("1 aguardando registro hoje", PlanoDeCuidadosNaTela.Resumo(plano));
        Assert.Empty(PlanoDeCuidadosNaTela.Linhas(null));
    }

    // ==================== a tela Enfermagem e a seção ====================

    [Fact]
    public void Horario_de_hoje_prefere_quem_esta_na_sala_e_ignora_cancelado_e_substituido()
    {
        var manha = Horario(1, 8, StatusAgendamento.Agendado);
        var tarde = Horario(2, 14, StatusAgendamento.Agendado);
        tarde.InicioAtendimentoEm = Hoje.ToDateTime(new TimeOnly(14, 2));
        var cancelado = Horario(3, 7, StatusAgendamento.Cancelado);
        var outro = Horario(4, 9, StatusAgendamento.Agendado, pacienteId: 99);

        Assert.Equal(2, EnfermagemNaTela.HorarioDeHoje([manha, tarde, cancelado, outro], 5)!.Id);

        tarde.FimAtendimentoEm = Hoje.ToDateTime(new TimeOnly(14, 40)); // encerrada: volta à primeira do dia
        Assert.Equal(1, EnfermagemNaTela.HorarioDeHoje([manha, tarde, cancelado], 5)!.Id);
        Assert.Null(EnfermagemNaTela.HorarioDeHoje([cancelado, outro], 5));
    }

    [Fact]
    public void Contexto_clinico_pula_o_que_nao_existe_e_escreve_o_peso_em_pt_BR()
    {
        Assert.Empty(EnfermagemNaTela.PartesClinicas([], [], []));

        var partes = EnfermagemNaTela.PartesClinicas(
            [new ProblemaPaciente { Natureza = NaturezaProblema.MedicacaoContinua, Descricao = "Gabapentina 300 mg" }],
            [new EvolucaoEnfermagem { Data = Hoje, Hora = new TimeOnly(8, 10), PressaoSistolica = 120, PressaoDiastolica = 80 }],
            [new MedidaClinica { TipoCodigo = CatalogoMedidas.Peso, Valor = 72.5m, Data = Hoje }]);

        Assert.Equal(3, partes.Count);
        Assert.StartsWith("em uso: ", partes[0]);
        Assert.Contains("Gabapentina", partes[0]);
        Assert.Contains("às 08:10 de 19/10", partes[1]);
        Assert.Equal("peso 72,5 kg (19/10/2026)", partes[2]);
    }

    [Fact]
    public void A_ultima_afericao_ignora_o_registro_cancelado()
    {
        var cancelada = new EvolucaoEnfermagem { Data = Hoje, Hora = new TimeOnly(9, 0), PressaoSistolica = 200, PressaoDiastolica = 120,
            CanceladaEm = Hoje.ToDateTime(new TimeOnly(9, 5)) };
        Assert.Null(EnfermagemNaTela.UltimaAfericao([cancelada]));
    }

    [Fact]
    public void Quem_nao_escreve_pelo_lado_Y_le_em_modo_leitura_e_a_tela_diz_por_que()
    {
        Assert.Null(EnfermagemNaTela.AvisoDeLeitura(true));
        Assert.Contains("Registrar evolução de enfermagem", EnfermagemNaTela.AvisoDeLeitura(false));
    }

    [Fact]
    public void A_linha_de_contexto_junta_so_o_que_existe()
    {
        Assert.Equal("62 anos · infusão 2026/0007 · 2 item(ns) aguardando",
            EnfermagemNaTela.JuntarContexto("62 anos", [null, " ", EnfermagemNaTela.ContextoDaFolha("2026/0007", 2)]));
        Assert.Equal("Folha 2026/0007 · 3 item(ns)", EnfermagemNaTela.FolhaDaSecao("2026/0007", 3));
    }

    [Fact]
    public void Termo_ja_assinado_no_celular_diz_a_conferir_e_nao_pendente()
    {
        var pendente = new SituacaoTermo(1, ModalidadeAtendimento.BsvApenas, "TCLE do BSV", 1, 9, false, false, null, []);
        Assert.StartsWith("termo pendente:", EnfermagemNaTela.ContextoDoTermo(pendente));
        Assert.StartsWith("termo assinado no celular, a conferir:",
            EnfermagemNaTela.ContextoDoTermo(pendente with { AssinaturaRemotaAguardaConferencia = true }));
    }

    [Fact]
    public void A_listagem_diz_a_pagina_em_que_esta()
        => Assert.Equal("Todos os pacientes — 100 na página 2.", EnfermagemNaTela.ResumoDaBusca(null, 100, 2));

    private static EvolucaoEnfermagem Passagem(int id, TimeOnly hora, int? agendamento) => new()
    {
        Id = id, PacienteId = 5, Data = Hoje, Hora = hora, Texto = $"passagem {id}", AgendamentoId = agendamento,
        AutorNome = "Joana", AutorConselho = "COREN-RJ 1", RegistradoEm = Hoje.ToDateTime(hora)
    };

    private static Agendamento Horario(int id, int hora, StatusAgendamento status, int pacienteId = 5) => new()
    {
        Id = id, PacienteId = pacienteId, DataHora = Hoje.ToDateTime(new TimeOnly(hora, 0)), Status = status,
        ModalidadePrevista = ModalidadeAtendimento.BsvApenas
    };
}
