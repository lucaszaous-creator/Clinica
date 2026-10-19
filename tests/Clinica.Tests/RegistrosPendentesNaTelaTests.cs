using Clinica.Application.Modelos;
using Xunit;

namespace Clinica.Tests;

/// <summary>
/// "SESSÕES SEM EVOLUÇÃO" — a regra do <c>RegistrosPendentesViewModel</c> WPF em
/// <see cref="RegistrosPendentesNaTela"/>: a linha ("ontem" / "há N dias"), o combo de
/// profissional só no modo sem vínculo, o filtro sem acento e o resumo que DIZ que está filtrado.
/// </summary>
public class RegistrosPendentesNaTelaTests
{
    private static readonly DateOnly Hoje = new(2026, 10, 15);

    private static LinhaRegistroPendente L(int id, string paciente, string profissional, int diasAtras)
        => RegistrosPendentesNaTela.Linha(new RegistroPendente(id, Hoje.AddDays(-diasAtras).ToDateTime(new TimeOnly(14, 30)),
            100 + id, paciente, "Acupuntura", null) { Profissional = profissional }, Hoje);

    [Fact]
    public void A_linha_diz_quando_e_ha_quanto_tempo()
    {
        var ontem = L(1, "Ana", "Dra. Helena", 1);
        Assert.Equal("ontem", ontem.Atraso);
        Assert.Equal("14/10 às 14:30", ontem.Quando);
        Assert.Equal("há 6 dias", L(2, "Bia", "", 6).Atraso);
        Assert.Equal(Hoje.AddDays(-1), ontem.Data);
    }

    [Fact]
    public void O_combo_de_profissional_so_recorta_na_lista_da_clinica()
    {
        var linhas = new[] { L(1, "Ana", "Dr. Marcos", 1), L(2, "Bia", "Dra. Helena", 2), L(3, "Caio", "", 3), L(4, "Davi", "Dr. Marcos", 4) };
        Assert.Equal([RegistrosPendentesNaTela.TodosProfissionais, "Dr. Marcos", "Dra. Helena"],
            RegistrosPendentesNaTela.Profissionais(linhas, listaDaClinica: true));
        Assert.Equal([RegistrosPendentesNaTela.TodosProfissionais], RegistrosPendentesNaTela.Profissionais(linhas, listaDaClinica: false));
    }

    [Fact]
    public void A_escolha_que_saiu_da_lista_volta_a_todos()
    {
        string[] opcoes = [RegistrosPendentesNaTela.TodosProfissionais, "Dr. Marcos"];
        Assert.Equal("Dr. Marcos", RegistrosPendentesNaTela.ManterEscolha("Dr. Marcos", opcoes));
        Assert.Equal(RegistrosPendentesNaTela.TodosProfissionais, RegistrosPendentesNaTela.ManterEscolha("Dra. Helena", opcoes));
    }

    [Fact]
    public void O_filtro_casa_profissional_e_parte_do_nome_sem_acento()
    {
        var linhas = new[] { L(1, "José Antônio", "Dr. Marcos", 1), L(2, "Joana", "Dra. Helena", 2) };
        Assert.Single(RegistrosPendentesNaTela.Filtrar(linhas, null, "jose"));
        Assert.Single(RegistrosPendentesNaTela.Filtrar(linhas, "Dra. Helena", ""));
        Assert.Equal(2, RegistrosPendentesNaTela.Filtrar(linhas, RegistrosPendentesNaTela.TodosProfissionais, null).Count);
    }

    [Fact]
    public void O_resumo_e_o_vazio_dizem_que_esta_filtrado()
    {
        Assert.False(RegistrosPendentesNaTela.FiltroAtivo(RegistrosPendentesNaTela.TodosProfissionais, " "));
        Assert.True(RegistrosPendentesNaTela.FiltroAtivo("Dr. Marcos", null));
        Assert.Equal("3 de 20 sessão(ões) sem evolução no filtro. A mais antiga primeiro.", RegistrosPendentesNaTela.Resumo(20, 3, true));
        Assert.StartsWith("20 sessão(ões) desde 27/09/2026", RegistrosPendentesNaTela.Resumo(20, 20, false));
        Assert.StartsWith("Nenhuma sessão desde 27/09/2026", RegistrosPendentesNaTela.Resumo(0, 0, false));
        Assert.StartsWith("Nenhuma sessão bate com o filtro", RegistrosPendentesNaTela.VazioDescricao(true));
        Assert.StartsWith("Toda sessão atendida desde", RegistrosPendentesNaTela.VazioDescricao(false));
    }
}
