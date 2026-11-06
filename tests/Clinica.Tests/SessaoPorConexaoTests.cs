using Clinica.Domain.Entities;

namespace Clinica.Tests;

/// <summary>
/// A sessão por CONEXÃO (nov/2026), que a gestão no navegador usa: no servidor, várias pessoas
/// dividem o processo, e `SessaoUsuario.Atual` tem de responder quem está naquela conexão —
/// nunca quem entrou por último. E, sem login, NEGAR (no desktop, sem login, libera).
///
/// Os testes não ligam o modo servidor do processo (ele é global e sem volta, e quebraria os
/// testes que rodam em paralelo): exercitam o fluxo, que vence a sessão do processo nos dois
/// modos, e a sessão fechada, que é a que o modo servidor põe no processo.
/// </summary>
public class SessaoPorConexaoTests
{
    private static UsuarioSistema Usuario(int id, string login, PerfilAcesso perfil) =>
        new() { Id = id, Nome = login, Login = login, Perfil = perfil };

    [Fact]
    public async Task Duas_conexoes_ao_mesmo_tempo_veem_cada_uma_a_sua_sessao()
    {
        var gerente = SessaoUsuario.Fotografar(Usuario(1, "ana", PerfilAcesso.Gerente));
        var recepcao = SessaoUsuario.Fotografar(Usuario(2, "bia", PerfilAcesso.Recepcao));
        using var entrar = new Barrier(2);

        async Task<(string antes, string depois)> Conexao(SessaoUsuario sessao)
        {
            await Task.Yield();
            using var _ = SessaoUsuario.UsarNesteFluxo(sessao);
            var antes = SessaoUsuario.Atual.Operador;
            entrar.SignalAndWait(TimeSpan.FromSeconds(5)); // as duas definidas ao mesmo tempo
            await Task.Delay(20);                          // troca de thread no meio
            return (antes, SessaoUsuario.Atual.Operador);
        }

        var r = await Task.WhenAll(Task.Run(() => Conexao(gerente)), Task.Run(() => Conexao(recepcao)));

        Assert.Equal(("ana", "ana"), r[0]);
        Assert.Equal(("bia", "bia"), r[1]);
    }

    [Fact]
    public void A_permissao_e_a_da_conexao_e_nao_a_de_quem_entrou_por_ultimo()
    {
        var gerente = SessaoUsuario.Fotografar(Usuario(1, "ana", PerfilAcesso.Gerente));
        var recepcao = SessaoUsuario.Fotografar(Usuario(2, "bia", PerfilAcesso.Recepcao));

        using (SessaoUsuario.UsarNesteFluxo(recepcao))
        {
            Assert.False(SessaoUsuario.Atual.Pode(Permissao.GerenciarUsuarios));
            using (SessaoUsuario.UsarNesteFluxo(gerente))
                Assert.True(SessaoUsuario.Atual.Pode(Permissao.GerenciarUsuarios));
            // Ao sair do bloco interno, volta a ser a recepção — não fica a do gerente.
            Assert.Equal("bia", SessaoUsuario.Atual.Operador);
            Assert.False(SessaoUsuario.Atual.Pode(Permissao.GerenciarUsuarios));
        }
    }

    [Fact]
    public void Sem_login_no_servidor_nega_tudo_e_nao_assina_com_o_usuario_da_maquina()
    {
        var ninguem = SessaoUsuario.SemLoginNoServidor;

        Assert.True(ninguem.Fechada);
        Assert.False(ninguem.Autenticado);
        Assert.False(ninguem.Pode(Permissao.VerProntuario));
        Assert.False(ninguem.PodeAlgum(Permissao.VerProntuario | Permissao.VerAgenda));
        Assert.Equal(Permissao.Nenhuma, ninguem.Efetivas);
        Assert.NotEqual(Environment.UserName, ninguem.Operador);
        Assert.Throws<InvalidOperationException>(() => ninguem.Exigir(Permissao.VerAgenda, "ver a agenda"));
    }

    [Fact]
    public void A_sessao_vazia_do_desktop_continua_liberando_como_sempre()
    {
        // A regra antiga não muda fora do servidor: teste e prévia sem login veem tudo.
        var vazia = new SessaoUsuario();
        Assert.False(vazia.Fechada);
        Assert.True(vazia.Pode(Permissao.GerenciarUsuarios));
    }

    [Fact]
    public void Fotografar_nao_vira_a_sessao_do_processo()
    {
        var antes = SessaoUsuario.Atual;
        var foto = SessaoUsuario.Fotografar(Usuario(77, "foto", PerfilAcesso.Recepcao));

        Assert.Same(antes, SessaoUsuario.Atual);
        Assert.Equal(77, foto.UsuarioId);
        Assert.True(foto.Autenticado);
    }
}
