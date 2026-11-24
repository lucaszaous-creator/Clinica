using Clinica.Application.Modelos;
using Clinica.Domain.Entities;
using Xunit;

namespace Clinica.Tests;

/// <summary>
/// Quem vê e atende a agenda de quem no Consultório (24/11/2026, decisão do dono): o Gerente vê e atende a agenda de
/// qualquer profissional; os outros perfis ficam com a própria — e a escolha que viesse na requisição é ignorada.
/// </summary>
public sealed class AgendaDoConsultorioTests
{
    [Theory]
    [InlineData(null, 1)]       // padrão: a própria agenda
    [InlineData("eu", 1)]
    [InlineData("todos", null)] // a clínica inteira
    [InlineData("TODOS", null)]
    [InlineData("7", 7)]        // outro profissional
    [InlineData("abc", 1)]      // escolha estranha volta à própria
    [InlineData("-3", 1)]
    public void O_Gerente_escolhe_a_agenda(string? escolha, int? esperado)
        => Assert.Equal(esperado, AgendaDoConsultorio.Filtro(PerfilAcesso.Gerente, 1, escolha));

    [Fact]
    public void O_Gerente_sem_vinculo_ve_a_clinica_inteira_por_padrao()
    {
        Assert.Null(AgendaDoConsultorio.Filtro(PerfilAcesso.Gerente, null, null));
        Assert.Equal(7, AgendaDoConsultorio.Filtro(PerfilAcesso.Gerente, null, "7"));
    }

    [Theory]
    [InlineData(PerfilAcesso.Profissional)]
    [InlineData(PerfilAcesso.Psicologia)]
    [InlineData(PerfilAcesso.Recepcao)]
    [InlineData(PerfilAcesso.Enfermagem)]
    public void Quem_nao_e_Gerente_fica_com_a_propria_mesmo_pedindo_outra(PerfilAcesso perfil)
    {
        Assert.Equal(1, AgendaDoConsultorio.Filtro(perfil, 1, "todos"));
        Assert.Equal(1, AgendaDoConsultorio.Filtro(perfil, 1, "7"));
        Assert.False(AgendaDoConsultorio.VeQualquerAgenda(perfil));
    }

    [Fact]
    public void Gerente_atende_horario_de_outro_e_Profissional_nao()
    {
        Assert.True(AgendaDoConsultorio.PodeAtenderHorarioDe(PerfilAcesso.Gerente, 1, 7));
        Assert.True(AgendaDoConsultorio.PodeAtenderHorarioDe(PerfilAcesso.Profissional, 1, 1));
        Assert.False(AgendaDoConsultorio.PodeAtenderHorarioDe(PerfilAcesso.Profissional, 1, 7));
        Assert.False(AgendaDoConsultorio.PodeAtenderHorarioDe(PerfilAcesso.Profissional, null, null));
    }

    [Fact]
    public void A_tela_diz_de_quem_e_a_agenda_e_quem_registra()
    {
        Assert.Equal("Agenda de Dr. Fictício · registrado por Jade Fictícia",
            AgendaDoConsultorio.Rotulo(1, 7, "Dr. Fictício", "Jade Fictícia"));
        Assert.Null(AgendaDoConsultorio.Rotulo(1, 1, "Ela mesma", "Jade Fictícia")); // a própria: nada a avisar
    }
}
