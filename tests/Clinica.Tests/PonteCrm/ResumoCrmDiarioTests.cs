using Clinica.Application.Servicos;
using Clinica.Crm.Bridge;
using Clinica.Crm.Contracts;
using Clinica.Domain.Entities;
using Clinica.Infrastructure;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Clinica.Tests;

/// <summary>
/// O dia do CRM para a direção (caminho A, out/2026): a ponte grava UMA linha por dia, só
/// troca por um cálculo mais novo, recusa número impossível; e a Mesa lê sem inventar zero.
/// </summary>
public sealed class ResumoCrmDiarioTests : IDisposable
{
    static readonly TimeZoneInfo Zona = TimeZoneInfo.FindSystemTimeZoneById("America/Sao_Paulo");
    readonly SqliteConnection _conn = new("Data Source=:memory:");
    readonly ClinicaDbContext _db;

    public ResumoCrmDiarioTests()
    {
        _conn.Open();
        _db = new(new DbContextOptionsBuilder<ClinicaDbContext>().UseSqlite(_conn).Options);
        _db.Database.EnsureCreated();
    }

    public void Dispose() { _db.Dispose(); _conn.Dispose(); }

    static DateOnly HojeNaClinica => DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, Zona).DateTime);

    static CrmDailySummary Dia(DateOnly dia, DateTimeOffset gerado, int conversas = 10, int? mediana = 120, int respostas = 8)
        => new(dia, gerado, conversas, conversas - 2, 2, 40, 35, mediana, respostas, 5, 6, 1, 12, 9, 6, 2, 1);

    [Fact]
    public async Task Grava_o_dia_e_troca_so_por_um_calculo_mais_novo()
    {
        var ontem = HojeNaClinica.AddDays(-1);
        var bridge = new ClinicSummaryBridge(_db);
        var cedo = DateTimeOffset.UtcNow.AddHours(-2);

        Assert.Equal("recorded", (await bridge.Daily(Dia(ontem, cedo, conversas: 10), default)).Status);
        Assert.Equal("recorded", (await bridge.Daily(Dia(ontem, cedo.AddHours(1), conversas: 14), default)).Status);
        // Chegou fora de ordem: o cálculo da manhã não troca o do fim do dia.
        Assert.Equal("stale", (await bridge.Daily(Dia(ontem, cedo.AddMinutes(30), conversas: 12), default)).Status);

        var linha = Assert.Single(await _db.ResumosCrmDiarios.AsNoTracking().ToListAsync());
        Assert.Equal(14, linha.ConversasIniciadas);
        Assert.Equal(ontem, linha.Dia);
        Assert.Equal(6, linha.SmsSegmentos);
    }

    [Theory]
    [InlineData("negativo")]
    [InlineData("mediana-sem-base")]
    [InlineData("respondidas-demais")]
    [InlineData("dia-futuro")]
    [InlineData("calculado-antes-do-dia")]
    public async Task Recusa_o_que_nao_pode_ser_um_dia_do_CRM(string caso)
    {
        var hoje = HojeNaClinica;
        var agora = DateTimeOffset.UtcNow;
        var bom = Dia(hoje.AddDays(-1), agora);
        var ruim = caso switch
        {
            "negativo" => bom with { MessagesIn = -1 },
            "mediana-sem-base" => bom with { RepliesMeasured = 0 },
            "respondidas-demais" => bom with { ConfirmationsAnswered = bom.ConfirmationsAsked + 1 },
            "dia-futuro" => bom with { Day = hoje.AddDays(1) },
            _ => bom with { Day = hoje, GeneratedAt = agora.AddDays(-3) }
        };

        await Assert.ThrowsAsync<InvalidOperationException>(() => new ClinicSummaryBridge(_db).Daily(ruim, default));
        Assert.Empty(await _db.ResumosCrmDiarios.ToListAsync());
    }

    [Fact]
    public async Task A_mesa_le_a_semana_fechada_e_o_dia_de_hoje_a_parte()
    {
        var hoje = new DateOnly(2026, 10, 18);
        var agora = new DateTime(2026, 10, 18, 10, 0, 0);
        ResumoCrmDiario R(int diasAtras, int conversas, int? mediana, int respostas, int pedidas = 10, int respondidas = 5) => new()
        {
            Dia = hoje.AddDays(-diasAtras), GeradoEm = hoje.AddDays(-diasAtras).ToDateTime(new TimeOnly(23, 30)),
            ConversasIniciadas = conversas, RespostaMedianaSegundos = mediana, RespostasMedidas = respostas,
            SmsEnviados = 2, SmsSegmentos = 3, ConfirmacoesPedidas = pedidas, ConfirmacoesRespondidas = respondidas
        };
        _db.ResumosCrmDiarios.AddRange(
            R(1, 10, 60, 30), R(2, 6, 300, 10), R(3, 4, null, 0),
            R(8, 99, 999, 99), // fora da semana
            new ResumoCrmDiario { Dia = hoje, GeradoEm = agora.AddMinutes(-20), ConversasIniciadas = 3 });
        await _db.SaveChangesAsync();

        var mesa = await new CrmNaDirecaoService(new ClinicaRepositorio(_db)).MesaAsync(hoje, agora);

        Assert.Equal(3, mesa.DiasNaSemana);
        Assert.Equal(20, mesa.Conversas);
        Assert.Equal(3, mesa.Hoje!.ConversasIniciadas);
        // Pesada pelas respostas: (60*30 + 300*10) / 40 = 120 s; o dia sem resposta não puxa para zero.
        Assert.Equal(120, mesa.RespostaTipicaSegundos);
        Assert.Equal("2 min", CrmNaTela.Duracao(mesa.RespostaTipicaSegundos));
        Assert.Equal("15 de 30 (50%)", CrmNaTela.Confirmacoes(mesa));
        Assert.Equal("6 (9 segmentos)", CrmNaTela.Sms(mesa));
        Assert.Null(mesa.Atraso);
    }

    [Fact]
    public void Sem_resumo_a_mesa_diz_o_que_liga_o_envio_e_resumo_velho_avisa()
    {
        var hoje = new DateOnly(2026, 10, 18);
        var vazio = CrmNaTela.Montar([], hoje, new DateTime(2026, 10, 18, 9, 0, 0));
        Assert.True(vazio.SemDados);
        Assert.Null(vazio.RespostaTipicaSegundos);
        Assert.Null(vazio.FracaoConfirmada);
        Assert.Equal("nenhuma pedida", CrmNaTela.Confirmacoes(vazio));
        Assert.Equal("—", CrmNaTela.Duracao(null));

        var velho = CrmNaTela.Montar(
            [new ResumoCrmDiario { Dia = hoje.AddDays(-2), GeradoEm = new DateTime(2026, 10, 16, 23, 30, 0), ConversasIniciadas = 4 }],
            hoje, new DateTime(2026, 10, 18, 9, 0, 0));
        Assert.Contains("16/10 às 23:30", velho.Atraso);
        Assert.Equal("1 h 5 min", CrmNaTela.Duracao(3900));
        Assert.Equal("45 s", CrmNaTela.Duracao(45));
    }
}
