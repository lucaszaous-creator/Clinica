using Clinica.Application.Modelos;
using Clinica.Application.Servicos;
using Clinica.Infrastructure;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Clinica.Tests;

/// <summary>
/// O visto e o lido do sino (out/2026 — o número do sino nunca sumia, porque contava as
/// pendências). O número é o que este usuário ainda não VIU; o destaque, o que não LEU.
/// </summary>
public sealed class LeituraDoSinoTests : IDisposable
{
    private readonly SqliteConnection _conn = new("DataSource=:memory:");
    private readonly ClinicaDbContext _db;
    private readonly ClinicaRepositorio _repo;

    public LeituraDoSinoTests()
    {
        _conn.Open();
        _db = new ClinicaDbContext(new DbContextOptionsBuilder<ClinicaDbContext>().UseSqlite(_conn).Options);
        _db.Database.EnsureCreated();
        _repo = new ClinicaRepositorio(_db);
    }

    public void Dispose()
    {
        _db.Dispose();
        _conn.Dispose();
    }

    private static readonly string NaoVem = LeituraDoSino.ChaveResposta(10, TipoAvisoConfirmacao.NaoVem);
    private static readonly string SemResposta = LeituraDoSino.ChaveResposta(11, TipoAvisoConfirmacao.SemResposta);
    private static readonly string Pedido = LeituraDoSino.ChavePedido(7);

    [Fact]
    public void Abrir_o_sino_ve_tudo_e_o_numero_some_mas_o_destaque_fica()
    {
        var m = MarcasDoSino.Vazias;
        m.NaoVistas([NaoVem, SemResposta, Pedido]).Should().Be(3);

        m = m.Ver([NaoVem, SemResposta, Pedido]);

        m.NaoVistas([NaoVem, SemResposta, Pedido]).Should().Be(0);
        m.Lido(NaoVem).Should().BeFalse("ver não é ler: o destaque continua até marcar como lida");
    }

    [Fact]
    public void So_o_que_e_novo_volta_a_contar()
    {
        var m = MarcasDoSino.Vazias.Ver([NaoVem, Pedido]);
        m.NaoVistas([NaoVem, Pedido, SemResposta]).Should().Be(1);
    }

    [Fact]
    public void O_sem_resposta_que_vira_nao_vem_e_noticia_nova()
    {
        var m = MarcasDoSino.Vazias.Ver([LeituraDoSino.ChaveResposta(10, TipoAvisoConfirmacao.SemResposta)]);
        m.NaoVistas([LeituraDoSino.ChaveResposta(10, TipoAvisoConfirmacao.NaoVem)]).Should().Be(1);
    }

    [Fact]
    public void Marcar_todas_e_clicar_leem_e_ler_implica_ver()
    {
        var m = MarcasDoSino.Vazias.Ler(Pedido);
        m.Lido(Pedido).Should().BeTrue();
        m.Visto(Pedido).Should().BeTrue();
        m.Lido(NaoVem).Should().BeFalse();

        m = m.LerTodas([NaoVem, SemResposta]);
        new[] { NaoVem, SemResposta, Pedido }.Should().OnlyContain(c => m.Lido(c) && m.Visto(c));
    }

    [Fact]
    public void O_conjunto_tem_teto_e_sai_o_mais_antigo()
    {
        var m = MarcasDoSino.Vazias.Ver(Enumerable.Range(1, MarcasDoSino.Teto + 20).Select(LeituraDoSino.ChavePedido));
        m.Vistas.Should().HaveCount(MarcasDoSino.Teto);
        m.Visto(LeituraDoSino.ChavePedido(1)).Should().BeFalse();
        m.Visto(LeituraDoSino.ChavePedido(MarcasDoSino.Teto + 20)).Should().BeTrue();
    }

    [Fact]
    public async Task Primeiro_uso_nao_tem_marcas_e_o_gravado_volta_por_usuario()
    {
        var servico = new LeituraDoSinoService(_repo);
        (await servico.ObterAsync(5)).Should().BeNull("primeiro uso: quem chama semeia o que já estava lá");

        await servico.VerAsync(5, [NaoVem, Pedido]);
        await servico.LerAsync(5, [Pedido]);

        var lidas = await new LeituraDoSinoService(_repo).ObterAsync(5);
        lidas!.Visto(NaoVem).Should().BeTrue();
        lidas.Lido(NaoVem).Should().BeFalse();
        lidas.Lido(Pedido).Should().BeTrue();
        (await servico.ObterAsync(6)).Should().BeNull("cada usuário tem as próprias marcas");
    }

    [Fact]
    public async Task Gravar_soma_ao_que_a_outra_maquina_marcou()
    {
        var aqui = new LeituraDoSinoService(_repo);
        var outra = new LeituraDoSinoService(_repo);
        await aqui.VerAsync(5, [NaoVem]);
        await outra.VerAsync(5, [Pedido]);

        var m = await aqui.ObterAsync(5);
        m!.Visto(NaoVem).Should().BeTrue();
        m.Visto(Pedido).Should().BeTrue();
    }

    [Fact]
    public async Task Gravado_estragado_recomeca_vazio_sem_derrubar_o_sino()
    {
        await _repo.SalvarConfiguracaoAsync(LeituraDoSinoService.Chave(5), "{não é json");
        await _repo.SalvarAsync();
        (await new LeituraDoSinoService(_repo).ObterAsync(5)).Should().Be(MarcasDoSino.Vazias);
    }
}
