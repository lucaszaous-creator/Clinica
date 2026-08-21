namespace Clinica.Domain.Entities;

/// <summary>
/// Quem está usando o app NESTE processo. Registrada como singleton no host: as
/// ViewModels perguntam a ela quem assina a ação (o "operador" da auditoria, o
/// "EnviadoPor" da campanha) em vez de cada tela inventar um nome.
///
/// MORA NO DOMÍNIO desde a parcela 45, e não no shell da suíte, porque o app de
/// FATURAMENTO passou a ter login e precisa da mesma sessão. Ele não pode referenciar
/// `Clinica.Desktop.Shell`: os dois declaram tipos no namespace `Clinica.Desktop.Controls`
/// (o design system duplicado, débito assumido em docs/arquitetura-multi-exe.md), e a
/// referência tornaria `IDialogoService` e `ISnackbarService` ambíguos nos dois lados.
/// Duplicar a sessão seria pior: duas respostas para "quem assina esta ação" é
/// exatamente o buraco que ela existe para fechar.
///
/// Guarda uma FOTOGRAFIA do usuário no momento do login, não a entidade rastreada —
/// segurar a entidade do EF presa a um scope morto é fonte garantida de
/// ObjectDisposedException lá na frente, e a permissão de quem já está dentro não muda
/// no meio da sessão de qualquer forma (muda no próximo login, que é o comportamento
/// que a clínica espera).
/// </summary>
public sealed class SessaoUsuario
{
    /// <summary>
    /// A sessão deste processo, acessível sem passar pelo construtor.
    ///
    /// Existe porque METADE dos formulários da suíte é construída à mão pela tela que
    /// os abre (<c>new PacienteEdicaoViewModel(escopos, id)</c>) e nunca passa pelo DI:
    /// exigir injeção obrigaria a reescrever vinte construtores só para poder perguntar
    /// "quem está logado?". A instância é a MESMA registrada no host — quem entra
    /// aponta esta propriedade para si em <see cref="Entrar"/>.
    ///
    /// Antes do login (e nos testes) aponta para uma sessão vazia, que libera tudo:
    /// ver <see cref="Pode"/>.
    /// </summary>
    public static SessaoUsuario Atual { get; private set; } = new();

    public int UsuarioId { get; private set; }

    public string Nome { get; private set; } = string.Empty;

    public string Login { get; private set; } = string.Empty;

    public PerfilAcesso Perfil { get; private set; } = PerfilAcesso.Recepcao;

    public Permissao Permissoes { get; private set; } = Permissao.Nenhuma;

    /// <summary>Profissional vinculado, quando o usuário atende.</summary>
    public int? ProfissionalId { get; private set; }

    /// <summary>
    /// O registro no conselho (CRM, COREN) de quem está usando o app, copiado do
    /// <see cref="Profissional"/> vinculado ao login.
    ///
    /// ⚠️ Existe porque a folha de infusão gravava <c>Conselho: null</c> LITERAL desde a
    /// parcela 42: a coluna existia, o PDF tinha o ramo que a imprime e a exportação tinha
    /// a coluna — e o único chamador de produção passava nulo, então só os testes a
    /// preenchiam. Registro de enfermagem sem o número do conselho não é registro de
    /// enfermagem: ele é parte da assinatura profissional.
    ///
    /// Nulo quando o login não tem profissional vinculado — e nulo aqui quer dizer "não
    /// cadastrado", nunca vazio: a tela DIZ que falta, em vez de imprimir uma linha muda.
    /// </summary>
    public string? RegistroConselho { get; private set; }

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

    /// <summary>
    /// Tem a permissão pedida?
    ///
    /// SEM SESSÃO AUTENTICADA, LIBERA. A regra mora aqui, num lugar só, porque a
    /// alternativa é pior: uma sessão vazia negando tudo esconderia a suíte inteira em
    /// qualquer caminho que não passe pelo login (teste, tela aberta fora do shell) — e
    /// tela vazia parece defeito, não segurança. No app real o login é obrigatório,
    /// então este caso não acontece em produção.
    /// </summary>
    public bool Pode(Permissao permissao)
        => !Autenticado
           || permissao == Permissao.Nenhuma
           || (Permissoes & permissao) == permissao;

    /// <summary>
    /// Os acessos que VALEM para esta sessão, como conjunto (parcela 59).
    ///
    /// Existe para quem precisa FILTRAR uma lista pelo acesso — o catálogo de folhas da
    /// central de documentos — em vez de perguntar por um bit de cada vez. E existe aqui,
    /// e não como `Permissoes` cru, porque a regra do "sem sessão autenticada, libera"
    /// mora nesta classe: lida direto, a propriedade devolveria <c>Nenhuma</c> fora do
    /// login e a tela abriria VAZIA, que é o oposto do que <see cref="Pode"/> decide.
    /// Duas respostas para "o que esta pessoa alcança?" é o começo de uma divergir.
    /// </summary>
    public Permissao Efetivas => Autenticado ? Permissoes : PerfisAcesso.Todas;

    /// <summary>
    /// Bloqueia a ação quando falta permissão. As telas já tratam exceção e mostram a
    /// mensagem inline, então o texto aqui é o que o usuário vai ler.
    ///
    /// É a SEGUNDA barreira, de propósito: o botão desabilitado explica; esta impede.
    /// Só desabilitar seria enfeite — um atalho de teclado ou um comando disparado por
    /// outro caminho passaria direto.
    /// </summary>
    public void Exigir(Permissao permissao, string acao)
    {
        if (Pode(permissao)) return;

        throw new InvalidOperationException(
            $"Seu acesso não permite {acao}. Fale com a direção da clínica.");
    }

    /// <summary>
    /// Basta UM dos bits — para o ato que DUAS permissões alcançam.
    ///
    /// <see cref="Pode"/> com bits combinados exige TODOS (é um E, não um OU), então o
    /// ato coberto por mais de um bit precisa desta variante. O caso que a criou é a
    /// fila do dia (parcela 61): <see cref="Permissao.EditarAgenda"/> sempre a moveu
    /// pelo balcão, e <see cref="Permissao.MovimentarFila"/> é o corte estreito de quem
    /// atende — exigir só o bit novo tiraria a fila de quem a movia ontem.
    /// </summary>
    public bool PodeAlgum(Permissao permissoes)
        => !Autenticado
           || permissoes == Permissao.Nenhuma
           || (Permissoes & permissoes) != 0;

    /// <summary>A segunda barreira de <see cref="PodeAlgum"/> — mesma regra do <see cref="Exigir"/>.</summary>
    public void ExigirAlgum(Permissao permissoes, string acao)
    {
        if (PodeAlgum(permissoes)) return;

        throw new InvalidOperationException(
            $"Seu acesso não permite {acao}. Fale com a direção da clínica.");
    }

    /// <summary>Grava a fotografia do usuário que acabou de entrar.</summary>
    public void Entrar(UsuarioSistema usuario)
    {
        UsuarioId = usuario.Id;
        Nome = usuario.Nome;
        Login = usuario.Login;
        Perfil = usuario.Perfil;
        Permissoes = usuario.Efetivas;
        ProfissionalId = usuario.ProfissionalId;
        RegistroConselho = usuario.Profissional?.RegistroConselho;

        // A partir daqui é ESTA instância que responde por "quem está usando o app".
        Atual = this;
    }
}
