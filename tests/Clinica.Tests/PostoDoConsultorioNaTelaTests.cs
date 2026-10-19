using Clinica.Application.Modelos;
using Clinica.Domain.Entities;
using Xunit;

namespace Clinica.Tests;

/// <summary>
/// DE QUEM É A LISTA do posto e para onde o Atender leva (<see cref="PostoDoConsultorioNaTela"/>,
/// out/2026 — a camada do perfil que o <c>PostoClinico</c> WPF punha por cima do domínio desceu
/// para cá, para o WPF e o Blazor responderem a MESMA coisa).
/// </summary>
public class PostoDoConsultorioNaTelaTests
{
    [Fact]
    public void Quem_consulta_ve_a_propria_agenda_e_sem_vinculo_ve_a_clinica()
    {
        var profissional = PerfisAcesso.Padrao(PerfilAcesso.Profissional);
        Assert.Equal(7, PostoDoConsultorioNaTela.ProfissionalDaLista(PerfilAcesso.Profissional, profissional, 7));
        Assert.Null(PostoDoConsultorioNaTela.ProfissionalDaLista(PerfilAcesso.Profissional, profissional, null));
        Assert.NotNull(PostoDoConsultorioNaTela.MotivoDaLista(profissional, null));
        Assert.Null(PostoDoConsultorioNaTela.MotivoDaLista(profissional, 7));
    }

    [Fact]
    public void O_perfil_de_enfermagem_ve_a_clinica_mesmo_com_um_bit_de_escrita_do_medico()
    {
        var comBitExtra = PerfisAcesso.Padrao(PerfilAcesso.Enfermagem) | Permissao.EditarProntuario;
        Assert.Null(PostoDoConsultorioNaTela.ProfissionalDaLista(PerfilAcesso.Enfermagem, comBitExtra, 7));
        Assert.True(PostoDoConsultorioNaTela.AtendimentoDeEnfermagem(PerfilAcesso.Enfermagem, comBitExtra));
    }

    [Fact]
    public void Quem_escreve_so_pela_enfermagem_cai_na_passagem_e_o_gerente_no_soap()
    {
        var soEnfermagem = Permissao.ChecarPrescricao | Permissao.RegistrarEvolucaoEnfermagem | Permissao.VerProntuario;
        Assert.True(PostoDoConsultorioNaTela.AtendimentoDeEnfermagem(PerfilAcesso.Recepcao, soEnfermagem));
        Assert.Null(PostoDoConsultorioNaTela.ProfissionalDaLista(PerfilAcesso.Recepcao, soEnfermagem, 7));
        Assert.False(PostoDoConsultorioNaTela.AtendimentoDeEnfermagem(PerfilAcesso.Gerente, PerfisAcesso.Todas));
        Assert.Equal(7, PostoDoConsultorioNaTela.ProfissionalDaLista(PerfilAcesso.Gerente, PerfisAcesso.Todas, 7));
    }
}
