using Clinica.Application.Modelos;
using Clinica.Application.Servicos;
using Clinica.Domain;
using Clinica.Domain.Entities;
using Xunit;

namespace Clinica.Tests;

/// <summary>
/// Horários de atendimento por dia (out/2026): cada dia com as SUAS faixas. O que estes
/// testes prendem é a pergunta que a clínica não conseguia responder com a "jornada" de um
/// horário só — "a Dra. atende segunda de manhã e quinta à tarde, com almoço".
/// </summary>
public class HorariosDeAtendimentoTests
{
    private static FaixaDeAtendimento F(string de, string ate) => new(TimeOnly.Parse(de), TimeOnly.Parse(ate));

    private static Profissional ComSemana()
    {
        var p = new Profissional { Nome = "Dra. Helena" };
        p.DefinirHorarios(new Dictionary<DayOfWeek, IReadOnlyList<FaixaDeAtendimento>>
        {
            [DayOfWeek.Monday] = [F("08:00", "12:00"), F("14:00", "18:00")],
            [DayOfWeek.Thursday] = [F("14:00", "19:00")]
        });
        return p;
    }

    [Fact]
    public void Texto_vai_e_volta_sem_perder_faixa()
    {
        var p = ComSemana();
        Assert.Equal("1=08:00-12:00,14:00-18:00;4=14:00-19:00", p.HorariosAtendimento);
        var lido = HorariosDeAtendimento.Ler(p.HorariosAtendimento);
        Assert.Equal(2, lido[DayOfWeek.Monday].Count);
        Assert.Equal(F("14:00", "19:00"), lido[DayOfWeek.Thursday][0]);
    }

    [Fact]
    public void Dia_sem_faixa_nao_atende_e_almoco_nao_e_expediente()
    {
        var p = ComSemana();
        Assert.False(p.AtendeEm(DayOfWeek.Tuesday));
        Assert.True(p.AtendeEm(DayOfWeek.Monday));
        var seg = new DateTime(2026, 10, 19);
        Assert.True(p.DentroDoExpediente(seg.AddHours(9), seg.AddHours(10)));
        Assert.False(p.DentroDoExpediente(seg.AddHours(12), seg.AddHours(13)));   // almoço
        Assert.False(p.DentroDoExpediente(seg.AddHours(11.5), seg.AddHours(12.5))); // atravessa a faixa
        Assert.Equal(480, p.MinutosEm(DayOfWeek.Monday));
        Assert.Equal(0, p.MinutosEm(DayOfWeek.Tuesday));
    }

    [Fact]
    public void Descricao_junta_dias_iguais_e_le_como_a_clinica_fala()
    {
        var p = new Profissional { Nome = "X" };
        p.DefinirHorarios(new Dictionary<DayOfWeek, IReadOnlyList<FaixaDeAtendimento>>
        {
            [DayOfWeek.Monday] = [F("08:00", "12:00")],
            [DayOfWeek.Wednesday] = [F("08:00", "12:00")],
            [DayOfWeek.Friday] = [F("14:00", "18:00")]
        });
        Assert.Equal("seg · qua 08:00–12:00 · sex 14:00–18:00", p.DescricaoJornada);
    }

    [Fact]
    public void Gravar_mantem_o_legado_para_o_binario_anterior()
    {
        var p = ComSemana();
        Assert.Equal(Profissional.BitDe(DayOfWeek.Monday) | Profissional.BitDe(DayOfWeek.Thursday), p.DiasDeAtendimento);
        Assert.Equal(new TimeOnly(8, 0), p.AtendeDas);
        Assert.Equal(new TimeOnly(19, 0), p.AtendeAte);
        p.DefinirHorarios(null);
        Assert.False(p.JornadaDeclarada);
        Assert.Null(p.HorariosAtendimento);
    }

    [Fact]
    public void Cadastro_antigo_continua_valendo_pelos_tres_campos()
    {
        var p = new Profissional
        {
            Nome = "Antigo", AtendeDas = new TimeOnly(8, 0), AtendeAte = new TimeOnly(12, 0),
            DiasDeAtendimento = Profissional.BitDe(DayOfWeek.Tuesday)
        };
        Assert.Equal([F("08:00", "12:00")], p.FaixasEm(DayOfWeek.Tuesday));
        Assert.Empty(p.FaixasEm(DayOfWeek.Monday)!);
        Assert.True(p.HorarioCompleto);
        Assert.Null(new Profissional { Nome = "Sem nada" }.FaixasEm(DayOfWeek.Monday)); // qualquer hora
    }

    [Fact]
    public void Faixas_sobrepostas_e_fim_antes_do_inicio_sao_recusadas()
    {
        Assert.NotNull(HorariosDeAtendimento.Validar(new Dictionary<DayOfWeek, IReadOnlyList<FaixaDeAtendimento>>
            { [DayOfWeek.Monday] = [F("08:00", "12:00"), F("11:00", "14:00")] }));
        Assert.Null(HorariosDeAtendimento.Validar(new Dictionary<DayOfWeek, IReadOnlyList<FaixaDeAtendimento>>
            { [DayOfWeek.Monday] = [F("08:00", "12:00"), F("12:00", "14:00")] }));
    }

    [Fact]
    public void Pilulas_e_vagas_seguem_as_faixas_e_pulam_o_almoco()
    {
        var p = ComSemana();
        p.Id = 1;
        var segunda = new DateOnly(2026, 10, 19);
        var pilulas = PilulasDeHorario.Montar(p, segunda, [], 60, segunda.ToDateTime(TimeOnly.MinValue));
        Assert.Equal(8, pilulas.Count);
        Assert.DoesNotContain(pilulas, x => x.Hora == new TimeOnly(12, 0));
        Assert.Contains(pilulas, x => x.Hora == new TimeOnly(14, 0));

        var vagas = BuscaDeVagas.Calcular(segunda.ToDateTime(TimeOnly.MinValue), 60, p, [], [], quantidade: 20, diasMaximos: 7);
        Assert.All(vagas, v => Assert.True(p.DentroDoExpediente(v.Inicio, v.Fim)));
    }

    [Fact]
    public void Texto_ilegivel_nao_derruba_a_agenda()
    {
        var lido = HorariosDeAtendimento.Ler("1=08:00-12:00,xx;9=08:00-09:00;lixo;4=10:00-09:00");
        Assert.Single(lido);
        Assert.Single(lido[DayOfWeek.Monday]);
    }
}
