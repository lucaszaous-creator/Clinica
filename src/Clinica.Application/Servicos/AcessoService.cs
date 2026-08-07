using Clinica.Application.Abstracoes;
using Clinica.Domain;
using Clinica.Domain.Entities;

namespace Clinica.Application.Servicos;

/// <summary>
/// O que aconteceu numa tentativa de entrar. Nunca diz "login não existe" ou "senha
/// errada" separadamente para a tela: distinguir os dois entrega a lista de logins
/// válidos a quem estiver tentando adivinhar.
/// </summary>
public sealed record ResultadoAutenticacao(
    bool Sucesso,
    UsuarioSistema? Usuario,
    string? Erro)
{
    public static ResultadoAutenticacao Ok(UsuarioSistema usuario) => new(true, usuario, null);
    public static ResultadoAutenticacao Falha(string erro) => new(false, null, erro);
}

/// <summary>
/// Usuários, senhas e perfis da suíte (a metade que faltava da feature 13 — a LGPD
/// saiu na parcela 2, no balcão).
///
/// Duas decisões que valem registrar:
///
/// 1. O usuário APONTA para o <see cref="Profissional"/> da parcela 1 em vez de
///    duplicá-lo. Quem atende já está cadastrado com registro no conselho, cor na
///    agenda e duração padrão; criar um segundo cadastro de pessoa faria a clínica
///    manter dois nomes para a mesma pessoa e escolher o errado na hora de assinar o
///    prontuário.
///
/// 2. Toda ação administrativa (criar, trocar senha, mudar permissão, desativar) grava
///    na trilha de auditoria no MESMO SaveChanges — a regra que já vale para baixa e
///    glosa. Permissão que muda sem deixar rastro é pior do que não ter permissão.
///
/// Desde a parcela 45 o app de FATURAMENTO também entra por aqui. Ele foi o último posto
/// sem login, e a razão de ganhar um não foi arquitetura: sem sessão, toda baixa, estorno e
/// glosa iam para a trilha de auditoria assinadas pelo usuário do WINDOWS — o mesmo nome
/// para as duas pessoas que dividem o balcão. A pergunta "quem fez isso?" não tinha
/// resposta justamente na tela onde ela mais importa.
/// </summary>
public sealed class AcessoService
{
    private readonly IClinicaRepositorio _repo;

    /// <summary>Erros seguidos antes de travar o login.</summary>
    public const int TentativasAteTravar = 5;

    /// <summary>Quanto tempo o login fica travado depois de estourar as tentativas.</summary>
    public static readonly TimeSpan DuracaoDoTravamento = TimeSpan.FromMinutes(5);

    /// <summary>Mensagem única para login inexistente E senha errada (ver <see cref="ResultadoAutenticacao"/>).</summary>
    private const string CredencialInvalida = "Usuário ou senha inválidos.";

    public AcessoService(IClinicaRepositorio repo) => _repo = repo;

    // ---------------------------------------------------------------- consulta

    public Task<IReadOnlyList<UsuarioSistema>> UsuariosAsync(CancellationToken ct = default)
        => _repo.UsuariosAsync(ct);

    public Task<UsuarioSistema?> ObterAsync(int usuarioId, CancellationToken ct = default)
        => _repo.ObterUsuarioAsync(usuarioId, ct);

    /// <summary>
    /// Existe alguém que consiga entrar? É o que decide se a abertura pede login ou
    /// oferece criar o primeiro acesso — base vazia não pode trancar ninguém do lado
    /// de fora.
    /// </summary>
    public async Task<bool> ExisteUsuarioAtivoAsync(CancellationToken ct = default)
        => (await _repo.UsuariosAsync(ct)).Any(u => u.Ativo);

    // ---------------------------------------------------------------- cadastro

    /// <summary>
    /// Cria um usuário. O primeiro da base nasce Gerente por construção (é quem vai
    /// cadastrar os outros) — quem chama decide, mas a tela de primeiro acesso não
    /// oferece outra opção.
    /// </summary>
    public async Task<UsuarioSistema> CriarAsync(
        string nome,
        string login,
        string senha,
        PerfilAcesso perfil,
        int? profissionalId = null,
        string? operador = null,
        bool deveTrocarSenha = false,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(nome))
            throw new ArgumentException("Informe o nome do usuário.", nameof(nome));

        var normalizado = UsuarioSistema.NormalizarLogin(login);
        if (normalizado.Length < 3)
            throw new ArgumentException("O login precisa de pelo menos 3 caracteres.", nameof(login));
        if (normalizado.Any(char.IsWhiteSpace))
            throw new ArgumentException("O login não pode ter espaços.", nameof(login));

        if (HashSenha.Criticar(senha) is { } critica)
            throw new ArgumentException(critica, nameof(senha));

        if (await _repo.ObterUsuarioPorLoginAsync(normalizado, ct) is not null)
            throw new InvalidOperationException($"Já existe um usuário com o login \"{normalizado}\".");

        await CriticarVinculoAsync(profissionalId, usuarioId: 0, ct);

        var (hash, sal) = HashSenha.Gerar(senha);

        var usuario = new UsuarioSistema
        {
            Nome = nome.Trim(),
            Login = normalizado,
            SenhaHash = hash,
            SenhaSalt = sal,
            Perfil = perfil,
            ProfissionalId = profissionalId,
            DeveTrocarSenha = deveTrocarSenha
        };

        await _repo.AdicionarUsuarioAsync(usuario, ct);
        await AuditarAsync("UsuarioCriado",
            $"{usuario.Nome} ({usuario.Login}) — perfil {PerfisAcesso.Rotular(perfil)}", operador, ct);
        await _repo.SalvarAsync(ct);
        return usuario;
    }

    /// <summary>Altera dados e permissões. A senha tem caminho próprio (<see cref="DefinirSenhaAsync"/>).</summary>
    public async Task<UsuarioSistema> AtualizarAsync(
        int usuarioId,
        string nome,
        PerfilAcesso perfil,
        int? profissionalId,
        Permissao extras,
        Permissao negadas,
        bool ativo,
        string? operador = null,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(nome))
            throw new ArgumentException("Informe o nome do usuário.", nameof(nome));

        var usuario = await _repo.ObterUsuarioAsync(usuarioId, ct)
            ?? throw new InvalidOperationException("Usuário não encontrado.");

        // Desativar (ou tirar a permissão de) o ÚLTIMO gerente deixa a clínica sem
        // ninguém capaz de criar usuário — e o conserto seria no banco, na mão.
        var perdeGestao = !ativo
            || !new UsuarioSistema { Perfil = perfil, PermissoesExtras = extras, PermissoesNegadas = negadas }
                .Pode(Permissao.GerenciarUsuarios);
        if (perdeGestao && await EhUltimoGestorAsync(usuarioId, ct))
            throw new InvalidOperationException(
                "Este é o último usuário que pode gerenciar acessos. " +
                "Dê a permissão a outra pessoa antes de tirar a dele.");

        await CriticarVinculoAsync(profissionalId, usuarioId, ct);

        usuario.Nome = nome.Trim();
        usuario.Perfil = perfil;
        usuario.ProfissionalId = profissionalId;
        usuario.PermissoesExtras = extras;
        usuario.PermissoesNegadas = negadas;
        usuario.Ativo = ativo;

        await AuditarAsync("UsuarioAlterado",
            $"{usuario.Login} — perfil {PerfisAcesso.Rotular(perfil)}, {(ativo ? "ativo" : "inativo")}",
            operador, ct);
        await _repo.SalvarAsync(ct);
        return usuario;
    }

    /// <summary>Define (ou redefine) a senha. Zera o travamento e as tentativas.</summary>
    public async Task DefinirSenhaAsync(
        int usuarioId, string senha, bool deveTrocar = false,
        string? operador = null, CancellationToken ct = default)
    {
        if (HashSenha.Criticar(senha) is { } critica)
            throw new ArgumentException(critica, nameof(senha));

        var usuario = await _repo.ObterUsuarioAsync(usuarioId, ct)
            ?? throw new InvalidOperationException("Usuário não encontrado.");

        var (hash, sal) = HashSenha.Gerar(senha);
        usuario.SenhaHash = hash;
        usuario.SenhaSalt = sal;
        usuario.DeveTrocarSenha = deveTrocar;
        usuario.TentativasFalhas = 0;
        usuario.BloqueadoAte = null;

        await AuditarAsync("SenhaDefinida", usuario.Login, operador, ct);
        await _repo.SalvarAsync(ct);
    }

    /// <summary>
    /// Troca de senha pelo próprio usuário: exige a senha atual. Sem isso, uma estação
    /// deixada aberta viraria a senha de outra pessoa.
    /// </summary>
    public async Task TrocarSenhaAsync(
        int usuarioId, string senhaAtual, string senhaNova, CancellationToken ct = default)
    {
        var usuario = await _repo.ObterUsuarioAsync(usuarioId, ct)
            ?? throw new InvalidOperationException("Usuário não encontrado.");

        if (!HashSenha.Confere(senhaAtual, usuario.SenhaHash, usuario.SenhaSalt))
            throw new InvalidOperationException("A senha atual está incorreta.");

        if (HashSenha.Criticar(senhaNova) is { } critica)
            throw new ArgumentException(critica, nameof(senhaNova));

        var (hash, sal) = HashSenha.Gerar(senhaNova);
        usuario.SenhaHash = hash;
        usuario.SenhaSalt = sal;
        usuario.DeveTrocarSenha = false;
        usuario.TentativasFalhas = 0;
        usuario.BloqueadoAte = null;

        await AuditarAsync("SenhaTrocada", usuario.Login, usuario.Login, ct);
        await _repo.SalvarAsync(ct);
    }

    /// <summary>
    /// Exclui um usuário. Só vale enquanto ele nunca entrou — depois disso o caminho é
    /// DESATIVAR, para a auditoria continuar sabendo quem era o "operador" gravado nas
    /// ações antigas. Mesma regra do cadastro de profissionais.
    /// </summary>
    public async Task ExcluirAsync(int usuarioId, string? operador = null, CancellationToken ct = default)
    {
        var usuario = await _repo.ObterUsuarioAsync(usuarioId, ct)
            ?? throw new InvalidOperationException("Usuário não encontrado.");

        if (usuario.UltimoAcessoEm is not null)
            throw new InvalidOperationException(
                "Este usuário já entrou no sistema e não pode ser excluído — desative-o, " +
                "para a auditoria continuar dizendo quem fez o quê.");

        if (await EhUltimoGestorAsync(usuarioId, ct))
            throw new InvalidOperationException(
                "Este é o último usuário que pode gerenciar acessos.");

        await _repo.RemoverUsuarioAsync(usuarioId, ct);
        await AuditarAsync("UsuarioExcluido", usuario.Login, operador, ct);
        await _repo.SalvarAsync(ct);
    }

    // ----------------------------------------------------------- autenticação

    /// <summary>
    /// Confere login e senha. Erra 5 vezes seguidas e o login trava por 5 minutos —
    /// o suficiente para inviabilizar tentativa por força bruta sem transformar um dia
    /// ruim de digitação em chamado de suporte.
    /// </summary>
    public async Task<ResultadoAutenticacao> AutenticarAsync(
        string login, string senha, DateTime? agora = null, CancellationToken ct = default)
    {
        var instante = agora ?? DateTime.Now;
        var normalizado = UsuarioSistema.NormalizarLogin(login);

        var usuario = await _repo.ObterUsuarioPorLoginAsync(normalizado, ct);
        if (usuario is null)
            return ResultadoAutenticacao.Falha(CredencialInvalida);

        if (!usuario.Ativo)
            return ResultadoAutenticacao.Falha("Este usuário está desativado. Procure a direção da clínica.");

        if (usuario.Travado(instante))
        {
            var faltam = (int)Math.Ceiling((usuario.BloqueadoAte!.Value - instante).TotalMinutes);
            return ResultadoAutenticacao.Falha(
                $"Muitas tentativas erradas. Tente de novo em {Math.Max(faltam, 1)} min.");
        }

        if (!HashSenha.Confere(senha, usuario.SenhaHash, usuario.SenhaSalt))
        {
            usuario.TentativasFalhas++;
            if (usuario.TentativasFalhas >= TentativasAteTravar)
            {
                usuario.BloqueadoAte = instante.Add(DuracaoDoTravamento);
                usuario.TentativasFalhas = 0;
                await AuditarAsync("LoginTravado",
                    $"{usuario.Login} — {TentativasAteTravar} tentativas erradas", usuario.Login, ct);
            }
            await _repo.SalvarAsync(ct);
            return ResultadoAutenticacao.Falha(CredencialInvalida);
        }

        usuario.TentativasFalhas = 0;
        usuario.BloqueadoAte = null;
        usuario.UltimoAcessoEm = instante;
        await _repo.SalvarAsync(ct);

        return ResultadoAutenticacao.Ok(usuario);
    }

    /// <summary>
    /// Entra pelo CPF que veio de DENTRO de um certificado ICP-Brasil (parcela 44).
    ///
    /// O que este método é, e o que ele NÃO é
    /// --------------------------------------
    /// Ele não autentica ninguém: quem autenticou foi o PSC, quando a médica confirmou no
    /// celular com o PIN dela. O que se faz aqui é a segunda pergunta, que é outra —
    /// <b>esta pessoa tem acesso a ESTE sistema?</b> Autenticar prova quem alguém é; quem
    /// concede acesso é a direção, em Acessos.
    ///
    /// Por isso CPF sem usuário correspondente <b>não cria usuário</b>. Criar seria deixar
    /// qualquer titular de e-CPF entrar numa clínica onde ninguém o cadastrou — e o
    /// certificado, que é a prova mais forte de identidade do sistema, viraria a porta mais
    /// larga dele.
    ///
    /// Entra AO LADO de <see cref="AutenticarAsync"/>, nunca no lugar: no balcão duas
    /// pessoas dividem a mesma máquina e não vão ter e-CPF cada uma.
    /// </summary>
    /// <param name="cpfDoCertificado">
    /// Só dígitos, lido do OID 2.16.76.1.3.1 por <c>CertificadoIcpBrasil.CpfDoTitular</c> —
    /// nunca de um campo digitado, que é o que separa isto de "informe seu CPF para entrar".
    /// </param>
    public async Task<ResultadoAutenticacao> AutenticarPorCertificadoAsync(
        string? cpfDoCertificado, DateTime? agora = null, CancellationToken ct = default)
    {
        var cpf = Cpf.Normalizar(cpfDoCertificado);

        if (cpf.Length == 0 || !Cpf.Valido(cpf))
            return ResultadoAutenticacao.Falha(
                "O certificado não traz um CPF válido, então não há como saber de quem ele é.");

        var usuarios = await _repo.UsuariosAsync(ct);

        var candidatos = usuarios
            .Where(u => Cpf.Normalizar(u.Profissional?.Cpf) == cpf)
            .ToList();

        if (candidatos.Count == 0)
            return ResultadoAutenticacao.Falha(
                $"Nenhum usuário deste sistema está vinculado ao CPF {Cpf.Formatar(cpf)}. "
                + "A direção cadastra o acesso em Acessos e vincula o profissional; o "
                + "certificado sozinho não dá entrada.");

        var ativos = candidatos.Where(u => u.Ativo).ToList();

        if (ativos.Count == 0)
            return ResultadoAutenticacao.Falha(
                "Este usuário está desativado. Procure a direção da clínica.");

        // Dois usuários ativos para o mesmo CPF é erro de cadastro, e escolher um em
        // silêncio daria acesso com o perfil errado — que é pior que não entrar.
        if (ativos.Count > 1)
            return ResultadoAutenticacao.Falha(
                $"Há mais de um usuário ativo vinculado ao CPF {Cpf.Formatar(cpf)}. "
                + "A direção precisa corrigir isso em Acessos antes da entrada por certificado.");

        // `UsuariosAsync` devolve entidades SEM rastreamento — servem para achar, não para
        // gravar. Sem esta releitura o `UltimoAcessoEm` abaixo seria descartado no
        // SaveChanges, e o sistema mostraria "nunca acessou" para quem entra todo dia.
        var usuario = await _repo.ObterUsuarioAsync(ativos[0].Id, ct);

        if (usuario is null)
            return ResultadoAutenticacao.Falha(CredencialInvalida);

        // Não zera TentativasFalhas nem BloqueadoAte: o travamento é do caminho da SENHA, e
        // limpá-lo aqui daria ao certificado o poder de destravar uma conta sob ataque de
        // força bruta — que é justamente quando o travamento tem de valer.
        usuario.UltimoAcessoEm = agora ?? DateTime.Now;

        await AuditarAsync(
            "LoginPorCertificado",
            $"{usuario.Login} — CPF {Cpf.Formatar(cpf)} lido do certificado",
            usuario.Login, ct);

        await _repo.SalvarAsync(ct);

        return ResultadoAutenticacao.Ok(usuario);
    }

    /// <summary>
    /// Um profissional tem UM acesso.
    ///
    /// Não é preciosismo de modelagem: <c>SessaoUsuario.ProfissionalId</c> é o que faz o
    /// Consultório saber de quem é "meu dia", e a entrada por certificado casa o CPF do
    /// e-CPF com o profissional. Dois usuários ativos apontando para a mesma pessoa tornam
    /// ambígua a resposta a "quem entrou?" — e a entrada por certificado teria de escolher
    /// um perfil em silêncio, que é pior do que não entrar.
    ///
    /// A recusa mora na ESCRITA, e não num índice único do banco, pela mesma razão do CPF
    /// repetido: migration com índice único falharia no <c>MigrateAsync</c> da abertura se a
    /// base da clínica já tivesse duplicata, e quem não abriria seria o faturamento, que
    /// roda em produção.
    /// </summary>
    private async Task CriticarVinculoAsync(int? profissionalId, int usuarioId, CancellationToken ct)
    {
        if (profissionalId is not { } alvo) return;

        var jaVinculado = (await _repo.UsuariosAsync(ct))
            .FirstOrDefault(u => u.Id != usuarioId && u.Ativo && u.ProfissionalId == alvo);

        if (jaVinculado is not null)
            throw new InvalidOperationException(
                $"O profissional já está vinculado ao usuário \"{jaVinculado.Login}\". "
                + "Cada profissional tem um acesso — desative o outro antes, ou vincule "
                + "este usuário a outro profissional.");
    }

    // ---------------------------------------------------------------- interno

    /// <summary>É o último usuário ativo capaz de gerenciar acessos?</summary>
    private async Task<bool> EhUltimoGestorAsync(int usuarioId, CancellationToken ct)
    {
        var usuarios = await _repo.UsuariosAsync(ct);
        return !usuarios.Any(u =>
            u.Id != usuarioId && u.Ativo && u.Pode(Permissao.GerenciarUsuarios));
    }

    private Task AuditarAsync(string acao, string detalhe, string? operador, CancellationToken ct)
        => _repo.RegistrarAuditoriaAsync(new EventoAuditoria
        {
            Acao = acao,
            Detalhe = detalhe,
            Operador = string.IsNullOrWhiteSpace(operador) ? "?" : operador!
        }, ct);
}
