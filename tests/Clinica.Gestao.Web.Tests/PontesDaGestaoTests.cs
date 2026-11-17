using Clinica.Domain.Entities;
using Clinica.Gestao.Web.Hospedagem;
using Clinica.Infrastructure;
using Clinica.Ui.Blazor.Hospedagem;
using Xunit;

namespace Clinica.Gestao.Web.Tests;

/// <summary>
/// As pontes das telas no portal (nov/2026): o que no app o módulo WPF responde e que o portal
/// deixava no "indisponível" — a marcação com o contexto, o dia na agenda, os atos do documento e
/// o aviso do banco com a sessão de quem assinou.
/// </summary>
public sealed class PontesDaGestaoTests
{
    private static SessaoUsuario Sessao(PerfilAcesso perfil)
        => SessaoUsuario.Fotografar(new UsuarioSistema { Id = 7, Nome = "Teste", Login = "teste", Perfil = perfil });

    private sealed record Conexao(NavegacaoDaGestao Navegacao, MolduraDaGestao? Moldura, PedidosDaGestao Pedidos, MarcacaoComPedido Marcacao);

    private static Conexao Abrir(PerfilAcesso? perfil = PerfilAcesso.Gerente)
    {
        var navegacao = new NavegacaoDaGestao();
        if (perfil is { } p) navegacao.Moldura = new MolduraDaGestao(CatalogoDaGestao.Embutido(), Sessao(p), () => { });
        var pedidos = new PedidosDaGestao();
        return new Conexao(navegacao, navegacao.Moldura, pedidos, new MarcacaoComPedido(navegacao, pedidos));
    }

    [Fact]
    public async Task Marcar_pela_agenda_do_dia_leva_o_dia_e_o_profissional()
    {
        var c = Abrir();
        var dia = new DateOnly(2026, 11, 12);
        Assert.True(await new JanelasDaAgendaDaGestao(c.Marcacao).MarcarNovoHorarioAsync(dia, 42));
        Assert.Equal(ChavesDaGestao.Marcar, c.Moldura!.ChaveAtual);

        var janelas = new JanelasDoAtendimentoDaGestao(c.Pedidos);
        // A aba Lançar não engole o pedido da aba Marcar.
        Assert.False(janelas.HaPedido(marcar: false));
        Assert.Null(janelas.ConsumirPedido(marcar: false));
        Assert.True(janelas.HaPedido(marcar: true));
        var pedido = janelas.ConsumirPedido(marcar: true);
        Assert.Equal(dia.ToDateTime(TimeOnly.MinValue), pedido!.DataHora);
        Assert.Equal(42, pedido.ProfissionalId);
        // Consumir LIMPA: o clique de ontem não preenche a abertura de amanhã.
        Assert.Null(janelas.ConsumirPedido(marcar: true));
    }

    [Fact]
    public async Task Marcar_para_um_paciente_leva_paciente_modalidade_e_nunca_dia_passado()
    {
        var c = Abrir();
        IMarcacaoDeHorario marcacao = new MarcacaoDaGestao(c.Marcacao);
        Assert.True(await marcacao.MarcarAsync(9, 3, "BsvApenas", new DateOnly(2020, 1, 1)));
        var pedido = c.Pedidos.ConsumirMarcacao()!;
        Assert.Equal(9, pedido.PacienteId);
        Assert.Equal(3, pedido.ProfissionalId);
        Assert.Equal("BsvApenas", pedido.ModalidadeCodigo);
        Assert.Equal(DateTime.Today, pedido.DataHora);
    }

    [Fact]
    public async Task O_vao_da_grade_leva_sala_duracao_e_modalidade()
    {
        var c = Abrir();
        IAgendaDaVisaoGeral agenda = new AgendaDaVisaoGeralDaGestao(c.Navegacao, c.Marcacao, c.Pedidos, new PedidoDaAgendaDoDia());
        Assert.True(agenda.MarcacaoDisponivel);
        var quando = new DateTime(2026, 11, 12, 14, 30, 0);
        Assert.True(await agenda.MarcarAsync(quando, 3, 5, 40, "Consulta"));
        Assert.Equal(new PedidoDoAtendimento(quando, 3, 5, null, 40, "Consulta"), c.Pedidos.ConsumirMarcacao());
    }

    [Fact]
    public async Task Abrir_o_dia_vai_para_a_agenda_do_dia_e_ver_na_agenda_para_a_grade()
    {
        var c = Abrir();
        var doDia = new PedidoDaAgendaDoDia();
        var agenda = new AgendaDaVisaoGeralDaGestao(c.Navegacao, c.Marcacao, c.Pedidos, doDia);
        var dia = new DateOnly(2026, 11, 20);
        Assert.True(await agenda.AbrirDiaAsync(dia, 3));
        Assert.Equal(ChavesDaGestao.AgendaDoDia, c.Moldura!.ChaveAtual);
        Assert.Equal((dia, (int?)3), doDia.Consumir());

        Assert.True(await new BalcaoDaGestao(c.Navegacao, c.Pedidos).AbrirDiaNaAgendaAsync(dia));
        Assert.Equal(ChavesDaGestao.Grade, c.Moldura.ChaveAtual);
        Assert.Equal(dia, agenda.ConsumirPedidoDeDia()!.Dia);
        Assert.Null(agenda.ConsumirPedidoDeDia());
    }

    [Fact]
    public async Task Sem_moldura_nada_navega_e_nenhum_pedido_fica_pendurado()
    {
        var c = Abrir(perfil: null);
        var doDia = new PedidoDaAgendaDoDia();
        var agenda = new AgendaDaVisaoGeralDaGestao(c.Navegacao, c.Marcacao, c.Pedidos, doDia);
        Assert.False(agenda.MarcacaoDisponivel);
        Assert.False(await agenda.MarcarAsync(DateTime.Today, 1));
        Assert.False(await agenda.AbrirDiaAsync(DateOnly.FromDateTime(DateTime.Today), 1));
        Assert.False(await new BalcaoDaGestao(c.Navegacao, c.Pedidos).AbrirDiaNaAgendaAsync(DateOnly.FromDateTime(DateTime.Today)));
        Assert.False(c.Pedidos.HaMarcacao);
        Assert.Null(c.Pedidos.ConsumirDia());
        Assert.Null(doDia.Consumir());
    }

    [Fact]
    public async Task Documento_no_portal_nao_oferece_o_que_e_da_area_congelada_e_recusar_nao_cancela()
    {
        using var _ = SessaoUsuario.UsarNesteFluxo(Sessao(PerfilAcesso.Gerente));
        var c = Abrir();
        // Sem serviços: se a recusa caísse no cancelamento (ou em qualquer ato), isto estouraria.
        var ponte = new DocumentosDaGestao(null!, null!, null!, null!, c.Navegacao);
        var doc = new DocumentoEmitido(1, 9, "REC-1/2026", TipoDocumentoClinico.Receita, "Receituário",
            Cancelado: false, Assinado: true, LinkNoAr: true, JaTeveLink: true);

        var atos = ponte.Atos(doc);
        Assert.False(atos.Assinar);
        Assert.False(atos.RenovarLink);
        Assert.False(atos.TirarDoAr);
        Assert.True(atos.Cancelar);
        Assert.True(atos.Enviar);

        foreach (var ato in new[] { AtoDoDocumento.Assinar, AtoDoDocumento.RenovarLink, AtoDoDocumento.TirarDoAr })
        {
            var r = await ponte.ExecutarAsync(ato, doc);
            Assert.True(r.EhErro);
            Assert.False(r.Mudou);
            Assert.Equal(DocumentosDaGestao.SoNoAplicativo, r.Frase);
        }
    }

    [Fact]
    public async Task O_aviso_do_banco_roda_com_a_sessao_de_quem_assinou()
    {
        using var avisos = new AvisosDoBancoDaGestao();
        var sessao = Sessao(PerfilAcesso.Recepcao);
        SessaoUsuario? vista = null;
        var chamadas = 0;
        void AoMudar(string tabela) { vista = SessaoUsuario.Atual; chamadas++; }

        using (SessaoUsuario.UsarNesteFluxo(sessao)) avisos.Mudou += AoMudar;

        // O aviso chega de uma thread SEM o fluxo da conexão (a do Npgsql).
        await Avisar();
        Assert.Same(sessao, vista);
        Assert.Equal(1, chamadas);

        avisos.Mudou -= AoMudar;
        await Avisar();
        Assert.Equal(1, chamadas);

        // Descartar a conexão solta o que ficou assinado.
        avisos.Mudou += AoMudar;
        avisos.Dispose();
        await Avisar();
        Assert.Equal(1, chamadas);

        static Task Avisar()
        {
            using (ExecutionContext.SuppressFlow())
                return Task.Run(() => OuvinteDeMudancas.Avisar("Agendamentos"));
        }
    }
}
