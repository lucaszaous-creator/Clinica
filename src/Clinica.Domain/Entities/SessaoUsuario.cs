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
    public static SessaoUsuario Atual => _doFluxo.Value?.Invoke() ?? _doProcesso;

    // ── Modo SERVIDOR (nov/2026, a gestão no navegador) ──────────────────────────────
    // No desktop há UM usuário por processo, e `Atual` é a sessão do processo. Num servidor
    // web, o mesmo processo atende várias pessoas ao mesmo tempo: a sessão passa a ser a do
    // FLUXO (AsyncLocal), que o hospedeiro define a cada atividade da conexão
    // (`UsarNesteFluxo`). E a regra "sem sessão, libera" se INVERTE: `AtivarModoServidor`
    // troca a sessão do processo por `Ninguem`, que nega tudo, e é ela que responde fora de
    // um fluxo com sessão. Uma tela aberta sem login no servidor não
    // é teste nem prévia — é uma porta aberta na internet.
    private static SessaoUsuario _doProcesso = new();
    private static readonly AsyncLocal<Func<SessaoUsuario>?> _doFluxo = new();
    private static volatile bool _modoServidor;

    /// <summary>A sessão de quem não entrou, no servidor: nega toda permissão.</summary>
    private static readonly SessaoUsuario Ninguem = new() { _fechada = true };

    private bool _fechada;

    /// <summary>
    /// Liga o modo servidor no processo: a partir daqui `Atual` é a sessão do fluxo e,
    /// sem fluxo, NEGA. Sem volta — um processo é desktop ou servidor, nunca os dois.
    /// </summary>
    public static void AtivarModoServidor()
    {
        _modoServidor = true;
        _doProcesso = Ninguem;
    }

    /// <summary>A sessão que nega tudo — a que vale, no servidor, fora de uma conexão com login.</summary>
    public static SessaoUsuario SemLoginNoServidor => Ninguem;

    /// <summary>Esta é a sessão fechada do servidor (ninguém entrou)?</summary>
    public bool Fechada => _fechada;

    /// <summary>O processo está em modo servidor?</summary>
    public static bool ModoServidor => _modoServidor;

    /// <summary>
    /// A fotografia de um usuário SEM torná-la a sessão do processo — para o servidor, que
    /// guarda uma por conexão. No desktop o caminho continua sendo <see cref="Entrar"/>.
    /// </summary>
    public static SessaoUsuario Fotografar(UsuarioSistema usuario)
    {
        var s = new SessaoUsuario();
        s.Copiar(usuario);
        return s;
    }

    /// <summary>
    /// Faz <paramref name="sessao"/> responder por `Atual` neste fluxo assíncrono (e no que
    /// ele disparar) até o descarte, que devolve a anterior. O fluxo vence a sessão do
    /// processo; é o que o servidor usa a cada atividade da conexão.
    /// </summary>
    public static IDisposable UsarNesteFluxo(SessaoUsuario sessao)
    {
        ArgumentNullException.ThrowIfNull(sessao);
        return UsarNesteFluxo(() => sessao);
    }

    /// <summary>
    /// A variante que LÊ a sessão na hora: o servidor define a sessão da conexão no meio da
    /// primeira atividade dela (o login é conferido ali), e um valor fixo deixaria o resto da
    /// mesma atividade — a primeira tela — com a sessão que nega tudo. Encerrar a sessão também
    /// vale na hora, sem esperar a próxima atividade.
    /// </summary>
    public static IDisposable UsarNesteFluxo(Func<SessaoUsuario> fonte)
    {
        ArgumentNullException.ThrowIfNull(fonte);
        var anterior = _doFluxo.Value;
        _doFluxo.Value = fonte;
        return new Restaurar(anterior);
    }

    private sealed class Restaurar(Func<SessaoUsuario>? anterior) : IDisposable
    {
        private bool _feito;
        public void Dispose()
        {
            if (_feito) return;
            _feito = true;
            _doFluxo.Value = anterior;
        }
    }

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
    public string Operador => Autenticado ? Login : _fechada ? "sem-sessao" : Environment.UserName;

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
        => (!Autenticado && !_fechada)
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
    public Permissao Efetivas => Autenticado ? Permissoes : _fechada ? Permissao.Nenhuma : PerfisAcesso.Todas;

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
        => (!Autenticado && !_fechada)
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
        // No servidor não existe "o usuário do processo": entrar aqui faria a próxima pessoa
        // herdar a sessão de quem entrou antes. O servidor usa Fotografar + UsarNesteFluxo.
        if (_modoServidor)
            throw new InvalidOperationException("No modo servidor, use Fotografar e UsarNesteFluxo.");
        Copiar(usuario);

        // A partir daqui é ESTA instância que responde por "quem está usando o app".
        _doProcesso = this;
    }

    private void Copiar(UsuarioSistema usuario)
    {
        UsuarioId = usuario.Id;
        Nome = usuario.Nome;
        Login = usuario.Login;
        Perfil = usuario.Perfil;
        Permissoes = usuario.Efetivas;
        ProfissionalId = usuario.ProfissionalId;
        RegistroConselho = usuario.Profissional?.RegistroConselho;
    }
}
