using Clinica.Domain.Entities;

namespace Clinica.Desktop.Shell;

/// <summary>
/// Quem está usando o app NESTE processo. Registrada como singleton no host: as
/// ViewModels perguntam a ela quem assina a ação (o "operador" da auditoria, o
/// "EnviadoPor" da campanha) em vez de cada tela inventar um nome.
///
/// Guarda uma FOTOGRAFIA do usuário no momento do login, não a entidade rastreada —
/// segurar a entidade do EF presa a um scope morto é fonte garantida de
/// ObjectDisposedException lá na frente, e a permissão de quem já está dentro não muda
/// no meio da sessão de qualquer forma (muda no próximo login, que é o comportamento
/// que a clínica espera).
/// </summary>
public sealed class SessaoUsuario
{
    public int UsuarioId { get; private set; }

    public string Nome { get; private set; } = string.Empty;

    public string Login { get; private set; } = string.Empty;

    public PerfilAcesso Perfil { get; private set; } = PerfilAcesso.Recepcao;

    public Permissao Permissoes { get; private set; } = Permissao.Nenhuma;

    /// <summary>Profissional vinculado, quando o usuário atende.</summary>
    public int? ProfissionalId { get; private set; }

    /// <summary>Há alguém autenticado neste processo?</summary>
    public bool Autenticado => UsuarioId > 0;

    /// <summary>
    /// Nome curto para gravar em auditoria e em campanha. Sem sessão, devolve o
    /// usuário do Windows — é o que o faturamento sempre fez, e continua sendo melhor
    /// do que gravar "?".
    /// </summary>
    public string Operador => Autenticado ? Login : Environment.UserName;

    /// <summary>Rótulo do cabeçalho: "Ana Souza · Recepção".</summary>
    public string Rotulo => Autenticado
        ? $"{Nome} · {PerfisAcesso.Rotular(Perfil)}"
        : Environment.UserName;

    public bool Pode(Permissao permissao)
        => permissao == Permissao.Nenhuma || (Permissoes & permissao) == permissao;

    /// <summary>Grava a fotografia do usuário que acabou de entrar.</summary>
    public void Entrar(UsuarioSistema usuario)
    {
        UsuarioId = usuario.Id;
        Nome = usuario.Nome;
        Login = usuario.Login;
        Perfil = usuario.Perfil;
        Permissoes = usuario.Efetivas;
        ProfissionalId = usuario.ProfissionalId;
    }
}
