namespace Clinica.Domain.Entities;

/// <summary>
/// O que um usuário pode fazer na suíte. É um conjunto de bits de propósito: perfil
/// resolve 90% dos casos, mas clínica pequena sempre tem a exceção ("a recepcionista
/// também lança o caixa") — e sem permissão fina a exceção vira senha compartilhada,
/// que é o fim de qualquer trilha de auditoria.
///
/// Gravada como INTEIRO (não como texto): a lista de bits muda com o tempo e um valor
/// serializado por nome ("VerAgenda, EditarAgenda") quebraria ao renomear qualquer um.
/// </summary>
[Flags]
public enum Permissao
{
    Nenhuma = 0,

    /// <summary>Ver a agenda, a fila do dia e o painel da recepção.</summary>
    VerAgenda = 1 << 0,

    /// <summary>Marcar, remarcar, cancelar, dar check-in e concluir atendimento.</summary>
    EditarAgenda = 1 << 1,

    /// <summary>Abrir a ficha do paciente e o prontuário.</summary>
    VerProntuario = 1 << 2,

    /// <summary>Escrever evolução, anexar arquivo e registrar consentimento.</summary>
    EditarProntuario = 1 << 3,

    /// <summary>Ver caixa, conciliação e produção.</summary>
    VerFinanceiro = 1 << 4,

    /// <summary>Lançar, realizar e cancelar movimento de caixa.</summary>
    EditarFinanceiro = 1 << 5,

    /// <summary>Ler o faturamento (guias, pendências, lotes) — a visão do Gerente.</summary>
    VerFaturamento = 1 << 6,

    /// <summary>Ver os indicadores gerenciais.</summary>
    VerIndicadores = 1 << 7,

    /// <summary>Gerar e disparar campanhas: confirmação, NPS e recall.</summary>
    GerenciarCampanhas = 1 << 8,

    /// <summary>Cadastrar profissionais e salas.</summary>
    GerenciarEquipe = 1 << 9,

    /// <summary>Criar usuário, trocar senha e mexer em permissão.</summary>
    GerenciarUsuarios = 1 << 10,

    /// <summary>Ler a trilha de auditoria.</summary>
    VerAuditoria = 1 << 11
}

/// <summary>
/// Papel do usuário na clínica. Define o conjunto BASE de permissões; o ajuste fino
/// fica em <see cref="UsuarioSistema.PermissoesExtras"/> e
/// <see cref="UsuarioSistema.PermissoesNegadas"/>.
/// </summary>
public enum PerfilAcesso
{
    /// <summary>Balcão: agenda, fila, cadastro e prontuário.</summary>
    Recepcao,

    /// <summary>Quem atende: a própria agenda e o prontuário; nada de dinheiro.</summary>
    Profissional,

    /// <summary>Administrativo: caixa, conciliação e produção.</summary>
    Financeiro,

    /// <summary>Faturista: lê o faturamento inteiro (o app de faturamento continua sem login).</summary>
    Faturista,

    /// <summary>Direção: tudo, inclusive criar usuário.</summary>
    Gerente
}

/// <summary>Conjunto padrão de permissões de cada perfil, e o rótulo em português.</summary>
public static class PerfisAcesso
{
    /// <summary>
    /// Permissões que o perfil concede por padrão. NÃO é gravado na linha do usuário:
    /// é resolvido na leitura, para que corrigir o padrão de um perfil valha para todo
    /// mundo que o usa, em vez de exigir reeditar usuário por usuário.
    /// </summary>
    public static Permissao Padrao(PerfilAcesso perfil) => perfil switch
    {
        PerfilAcesso.Recepcao =>
            Permissao.VerAgenda | Permissao.EditarAgenda |
            Permissao.VerProntuario | Permissao.EditarProntuario |
            Permissao.GerenciarCampanhas,

        PerfilAcesso.Profissional =>
            Permissao.VerAgenda | Permissao.VerProntuario | Permissao.EditarProntuario,

        PerfilAcesso.Financeiro =>
            Permissao.VerAgenda | Permissao.VerFinanceiro | Permissao.EditarFinanceiro,

        PerfilAcesso.Faturista =>
            Permissao.VerAgenda | Permissao.VerFaturamento | Permissao.VerProntuario,

        PerfilAcesso.Gerente => Todas,

        _ => Permissao.Nenhuma
    };

    /// <summary>Todos os bits definidos — o que o Gerente recebe.</summary>
    public static Permissao Todas
    {
        get
        {
            var todas = Permissao.Nenhuma;
            foreach (var p in Enum.GetValues<Permissao>()) todas |= p;
            return todas;
        }
    }

    public static string Rotular(PerfilAcesso perfil) => perfil switch
    {
        PerfilAcesso.Recepcao => "Recepção",
        PerfilAcesso.Profissional => "Profissional",
        PerfilAcesso.Financeiro => "Financeiro",
        PerfilAcesso.Faturista => "Faturista",
        PerfilAcesso.Gerente => "Gerente Geral",
        _ => perfil.ToString()
    };

    public static string Rotular(Permissao permissao) => permissao switch
    {
        Permissao.VerAgenda => "Ver agenda e fila",
        Permissao.EditarAgenda => "Marcar e remarcar",
        Permissao.VerProntuario => "Ver prontuário",
        Permissao.EditarProntuario => "Escrever no prontuário",
        Permissao.VerFinanceiro => "Ver financeiro",
        Permissao.EditarFinanceiro => "Lançar no caixa",
        Permissao.VerFaturamento => "Ver faturamento",
        Permissao.VerIndicadores => "Ver indicadores",
        Permissao.GerenciarCampanhas => "Gerenciar campanhas",
        Permissao.GerenciarEquipe => "Cadastrar equipe",
        Permissao.GerenciarUsuarios => "Gerenciar usuários",
        Permissao.VerAuditoria => "Ver auditoria",
        _ => permissao.ToString()
    };

    /// <summary>Bits individuais (sem <see cref="Permissao.Nenhuma"/>), para montar a tela.</summary>
    public static IReadOnlyList<Permissao> Individuais { get; } =
        Enum.GetValues<Permissao>().Where(p => p != Permissao.Nenhuma).ToList();
}

/// <summary>
/// Quem entra na suíte. É o outro lado do <see cref="Profissional"/>, que a parcela 1
/// criou: o profissional ocupa a agenda e assina o atendimento; o usuário é o login.
/// Ligar os dois (<see cref="ProfissionalId"/>) é o que permite a um profissional ver
/// a própria agenda sem duplicar cadastro — e é por isso que a permissão fina só
/// pôde vir depois da fundação.
///
/// A senha NUNCA é guardada: só o hash PBKDF2 e o sal, um por usuário
/// (<see cref="Clinica.Domain.HashSenha"/>).
///
/// O app de FATURAMENTO continua sem login: ele está congelado, e é o único posto onde
/// já existe uma pessoa só operando a máquina.
/// </summary>
public class UsuarioSistema
{
    public int Id { get; set; }

    public string Nome { get; set; } = string.Empty;

    /// <summary>Identificador de entrada, sempre em minúsculas e sem espaço.</summary>
    public string Login { get; set; } = string.Empty;

    /// <summary>Hash PBKDF2 da senha, em Base64. Nunca a senha.</summary>
    public string SenhaHash { get; set; } = string.Empty;

    /// <summary>Sal do hash, em Base64. Um por usuário — duas senhas iguais não colidem.</summary>
    public string SenhaSalt { get; set; } = string.Empty;

    public PerfilAcesso Perfil { get; set; } = PerfilAcesso.Recepcao;

    /// <summary>Permissões concedidas ALÉM do perfil (a exceção da clínica pequena).</summary>
    public Permissao PermissoesExtras { get; set; } = Permissao.Nenhuma;

    /// <summary>
    /// Permissões retiradas do perfil. Vence as extras de propósito: tirar acesso é a
    /// decisão que não pode ser anulada por engano de configuração.
    /// </summary>
    public Permissao PermissoesNegadas { get; set; } = Permissao.Nenhuma;

    /// <summary>Profissional correspondente, quando o usuário atende. Null para o balcão.</summary>
    public int? ProfissionalId { get; set; }
    public Profissional? Profissional { get; set; }

    /// <summary>Desativado não entra, mas continua existindo na auditoria.</summary>
    public bool Ativo { get; set; } = true;

    /// <summary>Senha provisória: o usuário troca no próximo acesso.</summary>
    public bool DeveTrocarSenha { get; set; }

    public DateTime CriadoEm { get; set; } = DateTime.Now;

    public DateTime? UltimoAcessoEm { get; set; }

    /// <summary>Tentativas erradas seguidas. Zera a cada acesso bem-sucedido.</summary>
    public int TentativasFalhas { get; set; }

    /// <summary>Até quando o login está travado por excesso de tentativas.</summary>
    public DateTime? BloqueadoAte { get; set; }

    public string? Observacoes { get; set; }

    /// <summary>
    /// Permissões que valem de fato: o padrão do perfil, mais as extras, menos as
    /// negadas. Resolver na leitura (em vez de gravar o resultado) é o que faz um
    /// ajuste no perfil alcançar quem já estava cadastrado.
    /// </summary>
    public Permissao Efetivas
        => (PerfisAcesso.Padrao(Perfil) | PermissoesExtras) & ~PermissoesNegadas;

    /// <summary>Tem a permissão pedida? <see cref="Permissao.Nenhuma"/> é sempre sim (tela livre).</summary>
    public bool Pode(Permissao permissao)
        => permissao == Permissao.Nenhuma || (Efetivas & permissao) == permissao;

    /// <summary>Está travado por tentativas erradas neste instante?</summary>
    public bool Travado(DateTime agora) => BloqueadoAte is { } ate && agora < ate;

    /// <summary>Login normalizado: minúsculas, sem espaço nas pontas.</summary>
    public static string NormalizarLogin(string? login)
        => (login ?? string.Empty).Trim().ToLowerInvariant();
}
