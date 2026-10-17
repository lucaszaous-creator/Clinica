using Clinica.Application.Abstracoes;
using Clinica.Application.Formatacao;
using Clinica.Domain;
using Xunit;

namespace Clinica.Tests;

/// <summary>
/// O DESENHO da Visão geral (out/2026): a matriz profissional × dia, os quatro números, a linha
/// discreta de quem está sem horário, a linha do tempo do dia e o calor do mês. Puro — os números
/// chegam prontos do serviço; aqui se prende o que a tela AFIRMA.
/// </summary>
public sealed class VisaoGeralDaAgendaDesenhoTests
{
    private static readonly DateOnly Segunda = new(2026, 10, 12);

    private static MedidaOcupacao M(int atend = 0, int ocup = 0, int disp = 0, int sem = 0, int canc = 0, int faltas = 0, int bloq = 0, int fora = 0)
        => new(atend, canc, faltas, ocup, disp, bloq, sem, fora);

    private static readonly FaixaDeAtendimento Manha = new(new TimeOnly(8, 0), new TimeOnly(12, 0));
    private static readonly FaixaDeAtendimento Tarde = new(new TimeOnly(14, 0), new TimeOnly(18, 0));

    /// <summary>Ana atende seg (manhã e tarde) e qua; não atende ter; qui fechada. Bruno sem horário.</summary>
    private static OcupacaoAgenda Semana(IReadOnlyList<HorarioOcupacao>? horarios = null)
    {
        var dias = Enumerable.Range(0, 7).Select(i => Segunda.AddDays(i)).ToList();
        var ana = new ProfissionalOcupacao(1, "Dra. Ana", "seg 08:00–12:00 e 14:00–18:00", M(atend: 3, ocup: 180, disp: 960))
        {
            Dias = dias.Select(d => d.DayOfWeek switch
            {
                DayOfWeek.Monday => new DiaDoProfissionalOcupacao(d, M(atend: 2, ocup: 120, disp: 480), [Manha, Tarde], null),
                DayOfWeek.Wednesday => new DiaDoProfissionalOcupacao(d, M(atend: 1, ocup: 60, disp: 240), [Manha], null),
                DayOfWeek.Thursday => new DiaDoProfissionalOcupacao(d, M(), [Manha], "Agenda fechada · Congresso"),
                _ => new DiaDoProfissionalOcupacao(d, M(), [], null)
            }).ToList()
        };
        var bruno = new ProfissionalOcupacao(2, "Bruno", "Horário de atendimento não cadastrado", M(atend: 1, sem: 1))
        {
            Dias = dias.Select(d => new DiaDoProfissionalOcupacao(d, M(atend: d == Segunda ? 1 : 0, sem: 1), null, null)).ToList()
        };
        return new OcupacaoAgenda(Segunda, Segunda.AddDays(6), Segunda.ToDateTime(new TimeOnly(9, 0)),
            M(atend: 4, ocup: 180, disp: 960, sem: 1, canc: 1, faltas: 2, bloq: 60),
            dias.Select(d => new DiaOcupacao(d, M(atend: d == Segunda ? 3 : d == Segunda.AddDays(2) ? 1 : 0, sem: 1))).ToList(),
            [ana, bruno], horarios ?? []);
    }

    [Fact]
    public void Matriz_diz_o_estado_de_cada_dia_por_forma_e_texto_e_tira_o_domingo_vazio()
    {
        var m = VisaoGeralDaAgendaDesenho.Matriz(Semana(), Segunda.AddDays(2));
        Assert.Equal(6, m.Dias.Count); // domingo: ninguém atende nem tem nada
        Assert.True(m.Dias[2].EhHoje);
        var ana = m.Linhas[0];
        Assert.Equal(EstadoCelulaSemana.Atende, ana.Celulas[0].Estado);
        Assert.Equal(25m, ana.Celulas[0].Percentual);
        Assert.Equal("25%", ana.Celulas[0].Rotulo);
        Assert.True(ana.Celulas[0].TemBarra);
        Assert.Equal(EstadoCelulaSemana.NaoAtende, ana.Celulas[1].Estado);
        Assert.Equal("não atende", ana.Celulas[1].Rotulo);
        Assert.Equal(EstadoCelulaSemana.Fechado, ana.Celulas[3].Estado);
        Assert.Contains("Congresso", ana.Celulas[3].Dica);
        var bruno = m.Linhas[1];
        Assert.True(bruno.SemHorario);
        Assert.Equal("horário não cadastrado", bruno.Horario);
        Assert.All(bruno.Celulas, c => Assert.Equal(EstadoCelulaSemana.SemHorario, c.Estado));
        Assert.Equal(1, bruno.Celulas[0].Atendimentos);
        // O total do dia mede só quem tem horário: Bruno sem horário não apaga a ocupação da segunda.
        Assert.Equal(3, m.Totais[0].Atendimentos);
        Assert.Equal(25m, m.Totais[0].Percentual);
    }

    [Fact]
    public void Indicadores_medem_quem_tem_horario_e_dizem_de_quantos()
    {
        var k = VisaoGeralDaAgendaDesenho.Indicadores(Semana());
        Assert.Equal("4", k.Atendimentos);
        Assert.Equal("18,8%", k.Ocupacao);
        Assert.Equal("de 1 profissional com horário (de 2)", k.OcupacaoNota);
        Assert.Equal("13h", k.HorasLivres);
        Assert.Contains("1h fechado", k.HorasLivresNota);
        Assert.Equal("3", k.FaltasECancelamentos);
        Assert.Equal("2 faltas · 1 cancelamento", k.FaltasNota);
    }

    [Fact]
    public void Linha_discreta_nomeia_quem_esta_sem_horario_e_some_quando_todos_tem()
    {
        var d = Semana([new HorarioOcupacao(9, Segunda.ToDateTime(new TimeOnly(9, 0)), Segunda.ToDateTime(new TimeOnly(9, 30)), null,
            "Sem profissional", "Paciente", "Acupuntura", "Agendado", true)]);
        Assert.Equal("Sem horário cadastrado: Bruno", VisaoGeralDaAgendaDesenho.LinhaSemHorario(d));
        Assert.Equal(2, VisaoGeralDaAgendaDesenho.PrimeiroSemHorario(d));
        Assert.Equal("1 atendimento sem profissional", VisaoGeralDaAgendaDesenho.LinhaDeAtencao(d));
        var soAna = d with { Profissionais = [d.Profissionais[0]], Horarios = [] };
        Assert.Null(VisaoGeralDaAgendaDesenho.LinhaSemHorario(soAna));
        Assert.Null(VisaoGeralDaAgendaDesenho.LinhaDeAtencao(soAna));
    }

    [Fact]
    public void Linha_do_tempo_poe_faixas_e_atendimentos_na_mesma_regua()
    {
        var h = new HorarioOcupacao(1, Segunda.ToDateTime(new TimeOnly(9, 0)), Segunda.ToDateTime(new TimeOnly(10, 0)), 1,
            "Dra. Ana", "Paciente A", "Acupuntura", "Agendado", true);
        var t = VisaoGeralDaAgendaDesenho.LinhaDoTempo(Semana([h]), Segunda, Segunda.ToDateTime(new TimeOnly(10, 0)));
        // Régua 07h–20h (13 h): a abertura padrão da grade.
        Assert.Equal("07h", t.Marcas[0].Rotulo);
        Assert.Equal("20h", t.Marcas[^1].Rotulo);
        var ana = t.Linhas[0];
        Assert.Equal(2, ana.Faixas.Count);
        Assert.Equal(1.0 / 13, ana.Faixas[0].Esquerda, 3);
        var bloco = Assert.Single(ana.Atendimentos);
        Assert.Equal(2.0 / 13, bloco.Esquerda, 3);
        Assert.Equal(1.0 / 13, bloco.Largura, 3);
        Assert.Equal(1, ana.Total);
        Assert.Equal("horário não cadastrado", t.Linhas[1].Estado);
        Assert.Equal(3.0 / 13, t.Agora!.Value, 3);
        // Terça: Ana não atende — o estado é texto, não só cor.
        var terca = VisaoGeralDaAgendaDesenho.LinhaDoTempo(Semana(), Segunda.AddDays(1), Segunda.ToDateTime(new TimeOnly(10, 0)));
        Assert.Equal("não atende neste dia", terca.Linhas[0].Estado);
        Assert.Null(terca.Agora);
    }

    [Fact]
    public void Calor_segue_a_ocupacao_e_sem_horario_o_numero_de_atendimentos()
    {
        Assert.Equal(0, VisaoGeralDaAgendaDesenho.Calor(0m, 0, 10));
        Assert.Equal(1, VisaoGeralDaAgendaDesenho.Calor(0m, 1, 10));
        Assert.Equal(3, VisaoGeralDaAgendaDesenho.Calor(60m, 2, 10));
        Assert.Equal(4, VisaoGeralDaAgendaDesenho.Calor(90m, 2, 10));
        Assert.Equal(4, VisaoGeralDaAgendaDesenho.Calor(null, 10, 10));
        Assert.Equal(2, VisaoGeralDaAgendaDesenho.Calor(null, 3, 10));
        Assert.Equal(0, VisaoGeralDaAgendaDesenho.Calor(null, 0, 10));
        var d = Semana();
        Assert.Equal(25m, VisaoGeralDaAgendaDesenho.PercentualDoDia(d, Segunda));
        Assert.Equal("25% ocupado", VisaoGeralDaAgendaDesenho.TaxaDoDia(25m, 2));
        Assert.Equal("sem horário para medir", VisaoGeralDaAgendaDesenho.TaxaDoDia(null, 2));
        Assert.StartsWith("Cor pela ocupação", VisaoGeralDaAgendaDesenho.LegendaDoCalor(d with
        {
            Dias = [new DiaOcupacao(Segunda, M(atend: 1, ocup: 60, disp: 240))]
        }));
    }
}
