using Clinica.Application.Modelos;
using Clinica.Domain.Entities;
using Xunit;

namespace Clinica.Tests;

/// <summary>
/// O que a MINHA SEMANA afirma (out/2026 — a regra do <c>MinhaSemanaViewModel</c> WPF em
/// <see cref="MinhaSemanaNaTela"/>): o cabeçalho de cada dia (o vazio continua tendo coluna), o
/// período, o resumo com o dia mais cheio e a dívida "nesta semana", e o filtro contando a
/// sessão longa UMA vez.
/// </summary>
public class MinhaSemanaNaTelaTests
{
    private static readonly DateOnly Segunda = new(2026, 10, 12);

    private static SessaoDoDia S(int id, DateOnly dia, int hora, int paciente, StatusAgendamento status = StatusAgendamento.Agendado)
        => new(id, dia.ToDateTime(new TimeOnly(hora, 0)), paciente, $"Paciente {paciente}", "Acupuntura", null,
            status, EtapaFila.Aguardando, false, null, null);

    private static SemanaDoProfissional Semana(params SessaoDoDia[] s)
        => new(Segunda, 1, "Dra. Helena", Enumerable.Range(0, 7).Select(i => Segunda.AddDays(i))
            .Select(d => new DiaDoProfissional(d, 1, "Dra. Helena", s.Where(x => DateOnly.FromDateTime(x.DataHora) == d).ToList()))
            .ToList());

    [Fact]
    public void Sete_cabecalhos_o_vazio_com_travessao_e_hoje_marcado()
    {
        var c = MinhaSemanaNaTela.Cabecalhos(Semana(S(1, Segunda, 8, 1), S(2, Segunda, 9, 2)), Segunda.AddDays(2));
        Assert.Equal(7, c.Count);
        Assert.Equal(("Segunda", "12/10", "2 sessão(ões)"), (c[0].Titulo, c[0].Subtitulo, c[0].Resumo));
        Assert.Equal("—", c[1].Resumo);
        Assert.True(c[2].EhHoje);
        Assert.Equal("Domingo", c[6].Titulo);
    }

    [Fact]
    public void O_periodo_vai_de_segunda_a_domingo()
        => Assert.Equal("12/10 a 18/10/2026", MinhaSemanaNaTela.Periodo(Semana()));

    [Fact]
    public void O_resumo_conta_pessoas_e_o_dia_mais_cheio()
    {
        var semana = Semana(S(1, Segunda, 8, 1), S(2, Segunda.AddDays(1), 8, 1), S(3, Segunda.AddDays(1), 9, 2));
        Assert.Equal("3 sessão(ões) · 2 paciente(s) · dia mais cheio: terça (2)", MinhaSemanaNaTela.Resumo(semana));
        Assert.Equal("Nenhum horário marcado nesta semana.", MinhaSemanaNaTela.Resumo(Semana()));
    }

    [Fact]
    public void Os_itens_do_filtro_contam_cada_horario_uma_vez()
    {
        var semana = Semana(S(1, Segunda, 8, 1), S(2, Segunda, 9, 2));
        Assert.Equal(2, MinhaSemanaNaTela.ItensDoFiltro(semana).Count);
    }
}
