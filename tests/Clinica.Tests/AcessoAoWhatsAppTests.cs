using Clinica.Domain.Entities;
using Xunit;

namespace Clinica.Tests;

/// <summary>
/// "Só quem terá acesso são as secretárias e o gerente" (25/10/2026): a regra única do
/// WhatsApp da clínica — menu, tela, conversa ao lado e a entrada no CRM pela ponte.
/// </summary>
public sealed class AcessoAoWhatsAppTests
{
    private static UsuarioSistema U(PerfilAcesso perfil, Permissao extras = Permissao.Nenhuma, Permissao negadas = Permissao.Nenhuma, bool ativo = true)
        => new() { Login = "x", Nome = "X", Perfil = perfil, PermissoesExtras = extras, PermissoesNegadas = negadas, Ativo = ativo };

    [Theory]
    [InlineData(PerfilAcesso.Recepcao, true)]
    [InlineData(PerfilAcesso.Gerente, true)]
    [InlineData(PerfilAcesso.Faturista, false)]
    [InlineData(PerfilAcesso.Financeiro, false)]
    [InlineData(PerfilAcesso.Profissional, false)]
    [InlineData(PerfilAcesso.Psicologia, false)]
    [InlineData(PerfilAcesso.Enfermagem, false)]
    public void So_recepcao_e_direcao_atendem_mesmo_com_o_bit_de_agendar(PerfilAcesso perfil, bool pode)
        => Assert.Equal(pode, AcessoAoWhatsApp.Pode(U(perfil, extras: Permissao.EditarAgenda)));

    [Fact]
    public void O_bit_continua_sendo_o_ajuste_fino_e_desativado_nao_entra()
    {
        Assert.False(AcessoAoWhatsApp.Pode(U(PerfilAcesso.Recepcao, negadas: Permissao.EditarAgenda)));
        Assert.False(AcessoAoWhatsApp.Pode(U(PerfilAcesso.Recepcao, ativo: false)));
        Assert.True(AcessoAoWhatsApp.Pode(U(PerfilAcesso.Recepcao)));
    }

    [Fact]
    public void Sessao_sem_login_libera_como_o_resto_da_suite()
        => Assert.True(AcessoAoWhatsApp.Pode(new SessaoUsuario())); // não toca a SessaoUsuario.Atual: outras classes rodam em paralelo

    [Fact]
    public void Com_login_vale_o_perfil_e_o_bit_efetivo()
    {
        Assert.False(AcessoAoWhatsApp.Pode(PerfilAcesso.Faturista, PerfisAcesso.Padrao(PerfilAcesso.Faturista) | Permissao.EditarAgenda));
        Assert.True(AcessoAoWhatsApp.Pode(PerfilAcesso.Gerente, PerfisAcesso.Padrao(PerfilAcesso.Gerente)));
        Assert.False(AcessoAoWhatsApp.Pode(PerfilAcesso.Recepcao, PerfisAcesso.Padrao(PerfilAcesso.Recepcao) & ~Permissao.EditarAgenda));
    }
}
