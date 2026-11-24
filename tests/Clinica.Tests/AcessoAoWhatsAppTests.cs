using Clinica.Domain.Entities;
using Xunit;

namespace Clinica.Tests;

/// <summary>
/// A regra única do WhatsApp da clínica — menu, tela, conversa ao lado, sino e a entrada no CRM pela
/// ponte. Desde 24/11/2026 (decisão 4 do "CRM com cara de WhatsApp"): SÓ o bit EditarAgenda, em
/// qualquer perfil — o diretor que entra como Médico passa a ver quando a direção lhe dá o bit.
/// </summary>
public sealed class AcessoAoWhatsAppTests
{
    private static UsuarioSistema U(PerfilAcesso perfil, Permissao extras = Permissao.Nenhuma, Permissao negadas = Permissao.Nenhuma, bool ativo = true)
        => new() { Login = "x", Nome = "X", Perfil = perfil, PermissoesExtras = extras, PermissoesNegadas = negadas, Ativo = ativo };

    [Theory]
    [InlineData(PerfilAcesso.Recepcao)]
    [InlineData(PerfilAcesso.Gerente)]
    [InlineData(PerfilAcesso.Faturista)]
    [InlineData(PerfilAcesso.Financeiro)]
    [InlineData(PerfilAcesso.Profissional)]
    [InlineData(PerfilAcesso.Psicologia)]
    [InlineData(PerfilAcesso.Enfermagem)]
    public void Com_o_bit_de_agendar_qualquer_perfil_atende(PerfilAcesso perfil)
        => Assert.True(AcessoAoWhatsApp.Pode(U(perfil, extras: Permissao.EditarAgenda)));

    // O padrão dos perfis não muda quem vê: só Recepção e Gerente nascem com o bit.
    [Theory]
    [InlineData(PerfilAcesso.Recepcao, true)]
    [InlineData(PerfilAcesso.Gerente, true)]
    [InlineData(PerfilAcesso.Faturista, false)]
    [InlineData(PerfilAcesso.Financeiro, false)]
    [InlineData(PerfilAcesso.Profissional, false)]
    [InlineData(PerfilAcesso.Psicologia, false)]
    [InlineData(PerfilAcesso.Enfermagem, false)]
    public void Sem_mexer_em_Acessos_so_recepcao_e_gerente_veem(PerfilAcesso perfil, bool pode)
        => Assert.Equal(pode, AcessoAoWhatsApp.Pode(U(perfil)));

    [Fact]
    public void O_bit_negado_tira_e_desativado_nao_entra()
    {
        Assert.False(AcessoAoWhatsApp.Pode(U(PerfilAcesso.Recepcao, negadas: Permissao.EditarAgenda)));
        Assert.False(AcessoAoWhatsApp.Pode(U(PerfilAcesso.Gerente, negadas: Permissao.EditarAgenda)));
        Assert.False(AcessoAoWhatsApp.Pode(U(PerfilAcesso.Recepcao, ativo: false)));
        Assert.True(AcessoAoWhatsApp.Pode(U(PerfilAcesso.Recepcao)));
    }

    [Fact]
    public void Sessao_sem_login_libera_como_o_resto_da_suite()
        => Assert.True(AcessoAoWhatsApp.Pode(new SessaoUsuario())); // não toca a SessaoUsuario.Atual: outras classes rodam em paralelo

    [Fact]
    public void Com_login_vale_o_bit_efetivo()
    {
        Assert.True(AcessoAoWhatsApp.Pode(PerfisAcesso.Padrao(PerfilAcesso.Profissional) | Permissao.EditarAgenda));
        Assert.False(AcessoAoWhatsApp.Pode(PerfisAcesso.Padrao(PerfilAcesso.Profissional)));
        Assert.False(AcessoAoWhatsApp.Pode(PerfisAcesso.Padrao(PerfilAcesso.Recepcao) & ~Permissao.EditarAgenda));
    }
}
