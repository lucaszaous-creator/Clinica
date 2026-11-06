using Clinica.Application.Abstracoes;
using Clinica.Application.Modelos;
using Clinica.Application.Servicos;
using Clinica.Domain;
using Clinica.Domain.Entities;
using Xunit;

namespace Clinica.Tests;

/// <summary>
/// O que o Início e os Pedidos da enfermagem AFIRMAM depois da migração para Blazor
/// (out/2026): as frases e recortes que moravam nos ViewModels WPF e subiram para a
/// Application com as MESMAS palavras.
/// </summary>
public sealed class InicioEPedidosDaRecepcaoTelaTests
{
    // ==================== Início ====================

    [Fact]
    public void Contador_que_nao_pode_ser_lido_mostra_traco_e_diz_isso_nunca_zero()
    {
        var itens = InicioRecepcaoTela.ItensDoBalcao(guias: null, confirmarAmanha: null, recall: 3);

        Assert.Equal(["guias", "confirmar", "recall"], itens.Select(i => i.Chave));
        Assert.Equal("—", itens[0].Valor);
        Assert.Equal("não foi possível conferir", itens[0].Detalhe);
        Assert.True(itens[0].NaoVerificado);
        Assert.Equal("—", itens[1].Valor);
        Assert.Equal("não foi possível conferir", itens[1].Detalhe);
        Assert.Equal("3", itens[2].Valor);
        Assert.Equal("contatos combinados para hoje", itens[2].Detalhe);
        Assert.False(itens[2].NaoVerificado);
    }

    [Fact]
    public void Confirmar_amanha_sem_rodada_usa_a_frase_da_regra_e_nao_inventa_zero()
    {
        var semRodada = InicioDoBalcao.ConfirmarAmanha([]);
        var item = InicioRecepcaoTela.ItensDoBalcao(1, semRodada, 1)[1];

        Assert.Equal("—", item.Valor);
        Assert.Equal("rodada de amanhã ainda não gerada", item.Detalhe);
    }

    [Fact]
    public void Guias_do_balcao_contam_so_o_segundo_codigo_com_atraso_por_extenso()
    {
        PendenciaCodigo P(OrdemCodigo ordem, int atraso, string? descricao = null) => new(
            1, 7, "Maria Silva", Convenio.UnimedPadrao, TipoCodigo.Consulta, ordem, DateOnly.FromDateTime(DateTime.Today),
            FormaObtencao.App, atraso, NivelUrgencia.Verde, descricao, "(21) 99999-0000");

        var guias = InicioRecepcaoTela.GuiasDoSegundoCodigo(
        [
            P(OrdemCodigo.Primeiro, 0),
            P(OrdemCodigo.Segundo, 0, "Eletroacupuntura"),
            P(OrdemCodigo.Segundo, 3)
        ]);

        Assert.Equal(2, guias.Count);
        Assert.Equal("Eletroacupuntura", guias[0].Descricao);
        Assert.Equal("vence hoje", guias[0].Atraso);
        Assert.Equal("3 dia(s) em atraso", guias[1].Atraso);
        Assert.Contains("(", guias[1].Descricao); // sem descrição: "Tipo (Ordem)" pelos rótulos, nunca o enum cru
        Assert.True(guias[1].TemTelefone);
    }

    [Theory]
    [InlineData(SituacaoChegada.Confirmou, "sucesso")]
    [InlineData(SituacaoChegada.NaoConfirmou, "aviso")]
    [InlineData(SituacaoChegada.NaoVem, "perigo")]
    [InlineData(SituacaoChegada.NoLocal, "info")]
    [InlineData(SituacaoChegada.Marcado, "neutro")]
    public void A_pilula_ganha_o_tom_do_que_ela_pede(SituacaoChegada situacao, string tom)
        => Assert.Equal(tom, InicioRecepcaoTela.TomDaPilula(situacao));

    [Fact]
    public void Quem_respondeu_que_nao_vem_aparece_como_nao_vem_e_nao_como_confirmado()
    {
        var a = new Agendamento
        {
            Id = 5, PacienteId = 1, DataHora = DateTime.Today.AddHours(10), ModalidadePrevista = ModalidadeAtendimento.BsvApenas,
            Paciente = new Paciente { Id = 1, Nome = "Renata Oliveira" }
        };
        var resposta = new RespostaDeConfirmacao(StatusContato.Respondido, DateTime.Today, RodadaDeConfirmacao.RespostaNaoVaiPeloLink, CanalContato.Sms);

        var linha = Assert.Single(InicioDoBalcao.Proximos([a], new Dictionary<int, RespostaDeConfirmacao> { [5] = resposta }, DateTime.Now));

        Assert.Equal(SituacaoChegada.NaoVem, linha.Situacao);
        Assert.Equal(RodadaDeConfirmacao.SeloNaoVem, linha.Pilula);
        Assert.Equal("perigo", InicioRecepcaoTela.TomDaPilula(linha.Situacao));
    }

    [Fact]
    public void Frases_dos_cartoes()
    {
        Assert.Equal("62% da agenda ocupada · 1 encaixe", InicioRecepcaoTela.DetalheAgendados(62.4m, 1));
        Assert.Equal("ocupação sem horário de atendimento cadastrado · 2 encaixes", InicioRecepcaoTela.DetalheAgendados(null, 2));
        Assert.Null(InicioRecepcaoTela.FracaoOcupada(null));
        Assert.Equal(1.0, InicioRecepcaoTela.FracaoOcupada(130m));
        Assert.Equal("ninguém no local ainda", InicioRecepcaoTela.DetalheAAtender(0));
        Assert.Equal("1 já está no local", InicioRecepcaoTela.DetalheAAtender(1));
        Assert.Equal("3 já estão no local", InicioRecepcaoTela.DetalheAAtender(3));
        Assert.Equal("1 pessoa", InicioRecepcaoTela.ResumoProximos(1));
        Assert.Equal("4 pessoas", InicioRecepcaoTela.ResumoProximos(4));
    }

    [Fact]
    public void No_local_conta_quem_chegou_ou_foi_chamado_e_ainda_nao_entrou()
    {
        var agora = DateTime.Now;
        var dia = new[]
        {
            new Agendamento { ChegadaEm = agora },
            new Agendamento { ChegadaEm = agora, ChamadoEm = agora },
            new Agendamento { ChegadaEm = agora, InicioAtendimentoEm = agora },
            new Agendamento()
        };
        Assert.Equal(2, InicioRecepcaoTela.NoLocal(dia));
    }

    [Fact]
    public void Aniversariante_e_rodape()
    {
        var a = InicioRecepcaoTela.Aniversariante(new Aniversariante(1, "Laura Reis", null, new DateOnly(1981, 3, 9), 45, true));
        Assert.Equal("45 anos · 09/03 · vem hoje", a.Detalhe);
        Assert.False(a.TemTelefone);
        var semIdade = InicioRecepcaoTela.Aniversariante(new Aniversariante(2, "Otília", "21999990000", new DateOnly(1851, 3, 9), null, false));
        Assert.Equal("09/03", semIdade.Detalhe);

        Assert.Equal("Aniversariantes: não foi possível conferir", InicioRecepcaoTela.RodapeAniversarios(null));
        Assert.Equal("Nenhum aniversariante nesta semana", InicioRecepcaoTela.RodapeAniversarios(0));
        Assert.Equal("1 aniversariante nesta semana", InicioRecepcaoTela.RodapeAniversarios(1));
        Assert.Equal("4 aniversariantes nesta semana", InicioRecepcaoTela.RodapeAniversarios(4));
    }

    [Fact]
    public void Mensagens_do_whatsapp_chamam_pelo_primeiro_nome()
    {
        Assert.StartsWith("Olá, Maria! Precisamos do documento/guia", InicioRecepcaoTela.CobrancaDeGuia("Maria da Silva"));
        Assert.EndsWith(" — Clínica Viva", InicioRecepcaoTela.CobrancaDeGuia("Maria", "Clínica Viva"));
        Assert.Equal("Feliz aniversário, Laura! Toda a equipe da clínica deseja um ótimo dia.", InicioRecepcaoTela.Parabens("Laura Reis"));
    }

    // ==================== Pedidos da enfermagem ====================

    private static LinhaSolicitacaoEnfermagem L(int id, DateTime inicio, string situacao, bool ativa) => new(
        id, inicio, inicio.AddHours(2), inicio, "BSV", ModalidadeAtendimento.BsvApenas, 3, "Joana", 9,
        situacao, "", null, "", 4, ativa, ativa, false, Guid.NewGuid());

    [Fact]
    public void Recortes_da_caixa_e_contagens_sempre_sobre_a_caixa_inteira()
    {
        var hoje = DateTime.Today;
        var caixa = new[]
        {
            L(1, hoje.AddHours(14), "Novo", true),
            L(2, hoje.AddDays(1).AddHours(8), "Novo", true),
            L(3, hoje.AddHours(9), "Visto", true),
            L(4, hoje.AddDays(-3), "Encerrado", false),
            L(5, hoje.AddDays(-1), "Expirado", false)
        };

        var novos = PedidosDaEnfermagemTela.Recortar(caixa, FiltroPedidosEnfermagem.Novos, false, hoje);
        Assert.Equal([1, 2], novos.Linhas.Select(l => l.Id));
        Assert.Equal((2, 3, 2), (novos.Novos, novos.Abertos, novos.NoDia));
        Assert.Equal("2 avisos · todas as datas", novos.Resumo);

        var abertosHoje = PedidosDaEnfermagemTela.Recortar(caixa, FiltroPedidosEnfermagem.EmAberto, true, hoje);
        Assert.Equal([1, 3], abertosHoje.Linhas.Select(l => l.Id));
        Assert.Equal((2, 3, 2), (abertosHoje.Novos, abertosHoje.Abertos, abertosHoje.NoDia));
        Assert.Equal("2 avisos · dia consultado", abertosHoje.Resumo);

        var historico = PedidosDaEnfermagemTela.Recortar(caixa, FiltroPedidosEnfermagem.Historico, false, hoje);
        Assert.Equal([4, 5], historico.Linhas.Select(l => l.Id));
    }

    [Fact]
    public void Vazio_diz_se_e_o_filtro_do_dia_ou_a_caixa()
    {
        var vazioNovos = PedidosDaEnfermagemTela.Recortar([], FiltroPedidosEnfermagem.Novos, false, DateTime.Today);
        Assert.Equal("Nenhum aviso novo", vazioNovos.TituloVazio);
        Assert.Equal("Os pedidos enviados pela enfermagem aparecerão aqui.", vazioNovos.TextoVazio);
        Assert.Equal("0 avisos · todas as datas", vazioNovos.Resumo);

        var vazioDia = PedidosDaEnfermagemTela.Recortar([], FiltroPedidosEnfermagem.Historico, true, DateTime.Today);
        Assert.Equal("Nenhum aviso neste filtro", vazioDia.TituloVazio);
        Assert.Equal("Desmarque o filtro do dia para consultar as outras datas.", vazioDia.TextoVazio);
    }

    [Fact]
    public void Linha_e_detalhe_escrevem_as_frases_da_tela()
    {
        var inicio = new DateTime(2026, 10, 20, 14, 0, 0);
        var p = L(7, inicio, "Visto", true) with { VistaEm = new DateTime(2026, 10, 16, 9, 5, 0), VistaPor = "Ana Lima" };
        var linha = new LinhaPedidoDaEnfermagem(p);

        Assert.Equal("20/10/2026", linha.Data);
        Assert.Equal("14:00–16:00", linha.Faixa);
        Assert.Equal("Visto pela recepção", linha.Status);
        Assert.Equal("info", linha.TomDoStatus);
        Assert.Equal("Até 3 atendimentos adicionais · BSV", linha.Titulo);
        Assert.Equal("Sem observação adicional.", linha.Observacao);
        Assert.False(linha.Novo);

        Assert.Equal("Pedido #7 · Joana", PedidosDaEnfermagemTela.DetalheFaixa(p));
        Assert.Equal("20/10/2026 · 14:00 às 16:00", PedidosDaEnfermagemTela.DetalheTitulo(p));
        Assert.Equal("Válido até 20/10/2026 às 14:00 · Visto por Ana Lima em 16/10/2026 às 09:05", PedidosDaEnfermagemTela.DetalheMeta(p));

        var periodo = new ResumoPeriodoEnfermagem(inicio, inicio.AddHours(2), 6, 2, 1, 3, "ref", DateTime.Now, [],
            [new BloqueioPlanejamento(inicio, inicio.AddHours(1), "Dra. Helena")], 0, false);
        var detalhe = new DetalheSolicitacaoEnfermagem(p, periodo, true,
            [new EventoPlanejamento(new DateTime(2026, 10, 16, 8, 0, 0), "Joana", "Pedido enviado", "")]);
        Assert.Equal("6 agendados · 2 BSV · 1 BSV + acupuntura · 3 outras modalidades", PedidosDaEnfermagemTela.ResumoPeriodo(periodo));
        Assert.StartsWith("Havia 4 agendados; agora há 6.", PedidosDaEnfermagemTela.TextoAgendaAlterada(detalhe));
        Assert.Equal(["14:00–15:00 · Dra. Helena"], PedidosDaEnfermagemTela.Bloqueios(periodo));
        var h = Assert.Single(PedidosDaEnfermagemTela.Historico(detalhe));
        Assert.Equal("16/10/2026 às 08:00 · Joana", h.Quando);
        Assert.False(h.TemObservacao);
    }

    [Theory]
    [InlineData("Novo", "Novo")]
    [InlineData("Visto", "Visto pela recepção")]
    [InlineData("Encerrado", "Aviso encerrado")]
    [InlineData("Retirado", "Retirado pela enfermagem")]
    [InlineData("Expirado", "Expirado")]
    [InlineData("Outro", "Outro")]
    public void Rotulo_da_situacao(string situacao, string rotulo)
        => Assert.Equal(rotulo, PedidosDaEnfermagemTela.RotuloSituacao(situacao));

    [Fact]
    public void A_caixa_e_da_recepcao_e_da_direcao_logadas_com_agenda_e_ficha()
    {
        const Permissao tres = Permissao.VerAgenda | Permissao.EditarAgenda | Permissao.VerFichaPaciente;
        Assert.True(PedidosDaEnfermagemTela.Permitido(true, PerfilAcesso.Recepcao, tres));
        Assert.True(PedidosDaEnfermagemTela.Permitido(true, PerfilAcesso.Gerente, tres | Permissao.EditarFinanceiro));
        Assert.False(PedidosDaEnfermagemTela.Permitido(false, PerfilAcesso.Gerente, tres));
        Assert.False(PedidosDaEnfermagemTela.Permitido(true, PerfilAcesso.Enfermagem, tres));
        Assert.False(PedidosDaEnfermagemTela.Permitido(true, PerfilAcesso.Recepcao, Permissao.VerAgenda | Permissao.VerFichaPaciente));
    }
}
