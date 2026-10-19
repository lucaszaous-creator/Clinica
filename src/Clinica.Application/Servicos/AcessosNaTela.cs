using System.Globalization;
using Clinica.Domain;
using Clinica.Domain.Entities;
using Clinica.Domain.Regras;

namespace Clinica.Application.Servicos;

/// <summary>Uma linha da lista de usuários (o que era a <c>LinhaUsuario</c> do WPF).</summary>
public sealed record LinhaDeAcesso(
    int Id,
    string Nome,
    string Login,
    string Perfil,
    /// <summary>"padrão do perfil" ou "2 liberada(s) · 1 tirada(s)" — o que foi decidido À MÃO.</summary>
    string Ajustes,
    bool TemAjuste,
    string Profissional,
    string Situacao,
    string UltimoAcesso,
    bool Ativo,
    bool DeveTrocarSenha,
    /// <summary>Excluir só vale enquanto a pessoa nunca entrou (a regra do <c>AcessoService.ExcluirAsync</c>).</summary>
    bool PodeExcluir);

/// <summary>Os números do topo da tela: quem entra, quem não entra, quem foge do padrão e quem pode conceder.</summary>
public sealed record ResumoDeAcessos(int Ativos, int Inativos, int ComAjuste, int Gestores);

/// <summary>Uma caixinha do formulário: o bit, o rótulo e a CONSEQUÊNCIA em uma frase.</summary>
public sealed record PermissaoNaTela(Permissao Valor, string Rotulo, string Explicacao);

/// <summary>As caixinhas de um ASSUNTO, na ordem em que o assunto aparece no enum.</summary>
public sealed record GrupoDePermissoes(string Assunto, IReadOnlyList<PermissaoNaTela> Itens);

/// <summary>Um profissional no seletor de vínculo. <c>Id</c> nulo é "— nenhum —".</summary>
public sealed record OpcaoDeVinculo(int? Id, string Nome, string? EspecialidadeCodigo);

/// <summary>
/// O que a tela de ACESSOS do Gerente AFIRMA (out/2026, etapa 6 — saiu do WPF para o Blazor).
/// Era o <c>AcessosViewModel</c> + <c>UsuarioEdicaoViewModel</c>: o rótulo do perfil, a
/// PROCEDÊNCIA de cada permissão (padrão do perfil, liberada à mão, tirada à mão), o resumo
/// das exceções e o DELTA que vira extras/negadas na gravação. Nada aqui lê o banco nem grava:
/// a tela grava pelo MESMO <see cref="AcessoService"/>, que audita no mesmo SaveChanges.
///
/// A tela mostra o que a pessoa PODE FAZER, não "extras e negadas": quem administra pensa em
/// "a Ana lança caixa?", não em delta contra o perfil. O delta é calculado aqui, na gravação —
/// assim corrigir o padrão de um perfil continua alcançando todo mundo.
/// </summary>
public static class AcessosNaTela
{
    private const string Travessao = "—";

    /// <summary>Nunca a senha: só a regra de tamanho, para a dica do campo.</summary>
    public static string AjudaDaSenha(bool novo) => novo
        ? $"Mínimo de {HashSenha.TamanhoMinimoSenha} caracteres."
        : "Deixe em branco para manter a senha atual.";

    public const string AjudaDoLogin = "O login não muda depois de criado — é ele que aparece na auditoria.";

    public const string AjudaDoPerfil =
        "Médico: atendimento e prescrições. Enfermagem: evoluções BSV / BSV + acupuntura e infusões.";

    /// <summary>Explica a ausência do campo em vez de deixá-lo aceso e sem efeito.</summary>
    public static string AjudaDoCpf(bool temProfissional) => temProfissional
        ? "Necessário para assinar com certificado digital: o sistema compara este CPF com o que está dentro do certificado. Em branco, esta pessoa não assina."
        : "Vincule um profissional acima para poder cadastrar o CPF de assinatura.";

    // ================================================================ lista

    /// <summary>"Médico · Acupuntura" quando o perfil Profissional tem especialidade; senão o rótulo do perfil.</summary>
    public static string RotuloDoPerfil(UsuarioSistema u)
        => u.Perfil == PerfilAcesso.Profissional && u.Profissional?.EspecialidadeCodigo is { } especialidade
            ? "Médico · " + CatalogoEspecialidades.Nome(especialidade)
            : PerfisAcesso.Rotular(u.Perfil);

    public static IReadOnlyList<LinhaDeAcesso> Linhas(IEnumerable<UsuarioSistema> usuarios)
        => usuarios.Select(Linha).ToList();

    public static LinhaDeAcesso Linha(UsuarioSistema u)
    {
        var padrao = PerfisAcesso.Padrao(u.Perfil);
        var efetivas = u.Efetivas;
        var liberadas = ContarBits(efetivas & ~padrao);
        var tiradas = ContarBits(padrao & ~efetivas);
        return new LinhaDeAcesso(
            u.Id, u.Nome, u.Login, RotuloDoPerfil(u),
            Ajustes(liberadas, tiradas), liberadas + tiradas > 0,
            u.Profissional?.Nome ?? Travessao,
            u.Ativo ? "Ativo" : "Inativo",
            u.UltimoAcessoEm is { } quando
                ? quando.ToString("dd/MM/yyyy HH:mm", CultureInfo.InvariantCulture)
                : "nunca entrou",
            u.Ativo, u.DeveTrocarSenha,
            PodeExcluir: u.UltimoAcessoEm is null);
    }

    /// <summary>A frase curta da lista: o que foge do padrão do perfil, para que lado.</summary>
    public static string Ajustes(int liberadas, int tiradas) => (liberadas, tiradas) switch
    {
        (0, 0) => "padrão do perfil",
        (_, 0) => $"{liberadas} liberada(s) à mão",
        (0, _) => $"{tiradas} tirada(s) à mão",
        _ => $"{liberadas} liberada(s) · {tiradas} tirada(s) à mão"
    };

    public static ResumoDeAcessos Resumo(IReadOnlyCollection<UsuarioSistema> usuarios) => new(
        Ativos: usuarios.Count(u => u.Ativo),
        Inativos: usuarios.Count(u => !u.Ativo),
        ComAjuste: usuarios.Count(u => u.Efetivas != PerfisAcesso.Padrao(u.Perfil)),
        // Quem pode CONCEDER permissão — inclusive a si mesmo. É o número que a direção audita.
        Gestores: usuarios.Count(u => u.Ativo && u.Pode(Permissao.GerenciarUsuarios)));

    public static string PerguntaDeExclusao(string nome)
        => $"Excluir {nome}? Só é possível enquanto ele nunca entrou no sistema — "
           + "se já entrou, desative-o em vez de excluir.";

    public const string MotivoSemExcluir =
        "Já entrou no sistema: não pode ser excluído — desative-o em Editar, para a auditoria continuar dizendo quem fez o quê.";

    // ================================================================ formulário

    /// <summary>
    /// As permissões AGRUPADAS por assunto (parcela 49), na ordem em que cada assunto aparece
    /// no enum — a mesma ordem do agrupamento do WPF. Vinte e quatro caixinhas numa lista
    /// corrida não são uma decisão; é lendo por bloco que se percebe o bit solto.
    /// </summary>
    public static IReadOnlyList<GrupoDePermissoes> Grupos()
        => PerfisAcesso.Individuais
            .Select(p => (Assunto: PerfisAcesso.Assunto(p),
                Item: new PermissaoNaTela(p, PerfisAcesso.Rotular(p), PerfisAcesso.Explicar(p))))
            .GroupBy(x => x.Assunto)
            .Select(g => new GrupoDePermissoes(g.Key, g.Select(x => x.Item).ToList()))
            .ToList();

    /// <summary>
    /// De onde veio a decisão — "isto é o padrão da função ou alguém mexeu?", a pergunta da
    /// direção ao auditar. Os textos são os do WPF.
    /// </summary>
    public static string Procedencia(bool marcada, bool doPerfil)
        => marcada && doPerfil ? "padrão do perfil"
            : marcada ? "LIBERADA para esta pessoa"
            : doPerfil ? "TIRADA desta pessoa"
            : "não concedida";

    /// <summary>Difere do padrão do perfil — a tela destaca, porque é o que foi decidido à mão.</summary>
    public static bool EhExcecao(bool marcada, bool doPerfil) => marcada != doPerfil;

    public static bool Tem(Permissao conjunto, Permissao bit) => (conjunto & bit) == bit;

    /// <summary>
    /// Bits que NÃO valem para esta pessoa, marcados ou não: a psicologia (pelo perfil ou pela
    /// especialidade do profissional vinculado) não prescreve, não checa nem evolui como
    /// enfermagem — o <see cref="UsuarioSistema.Efetivas"/> os corta na leitura. O WPF deixava
    /// marcar e a caixinha não tinha efeito nenhum; a tela agora diz por quê.
    /// </summary>
    public static Permissao Ignoradas(PerfilAcesso perfil, string? especialidadeDoVinculado)
        => perfil == PerfilAcesso.Psicologia
           || string.Equals(especialidadeDoVinculado, "Psicologia", StringComparison.OrdinalIgnoreCase)
            ? Permissao.Prescrever | Permissao.ChecarPrescricao | Permissao.RegistrarEvolucaoEnfermagem
            : Permissao.Nenhuma;

    public const string MotivoIgnorada = "Não vale para Psicologia: o sistema ignora esta concessão.";

    /// <summary>
    /// Diz, em uma linha, quanto esta pessoa foge do padrão da função dela — e para que lado.
    /// "3 exceções" sem dizer se foram concessões ou cortes obrigaria a direção a percorrer os
    /// blocos para descobrir. Os textos são os do WPF.
    /// </summary>
    public static string ResumoDasExcecoes(PerfilAcesso perfil, Permissao marcadas, Permissao ignoradas = Permissao.Nenhuma)
    {
        var (extras, negadas) = Delta(perfil, marcadas, ignoradas);
        var liberadas = ContarBits(extras);
        var tiradas = ContarBits(negadas);
        var rotulo = PerfisAcesso.Rotular(perfil);
        return (liberadas, tiradas) switch
        {
            (0, 0) => $"Exatamente o padrão de {rotulo} — nada foi mexido à mão.",
            (_, 0) => $"{liberadas} permissão(ões) LIBERADA(S) além do padrão de {rotulo}.",
            (0, _) => $"{tiradas} permissão(ões) TIRADA(S) do padrão de {rotulo}.",
            _ => $"{liberadas} liberada(s) e {tiradas} tirada(s) em relação ao padrão de {rotulo}."
        };
    }

    /// <summary>
    /// O que vai para o banco: marcada fora do padrão vira EXTRA; desmarcada dentro do padrão
    /// vira NEGADA. Só bits individuais conhecidos entram (nada de bit perdido no inteiro).
    /// </summary>
    /// <remarks>
    /// Os bits <paramref name="ignoradas"/> (ver <see cref="Ignoradas"/>) contam como o padrão: o
    /// sistema os corta na leitura, então gravá-los como exceção seria registrar uma decisão que
    /// não tem efeito — e a caixinha deles fica apagada na tela.
    /// </remarks>
    public static (Permissao Extras, Permissao Negadas) Delta(PerfilAcesso perfil, Permissao marcadas,
        Permissao ignoradas = Permissao.Nenhuma)
    {
        var padrao = PerfisAcesso.Padrao(perfil);
        marcadas = (marcadas & ~ignoradas) | (padrao & ignoradas);
        var extras = Permissao.Nenhuma;
        var negadas = Permissao.Nenhuma;
        foreach (var p in PerfisAcesso.Individuais)
        {
            var marcada = Tem(marcadas, p);
            var noPadrao = Tem(padrao, p);
            if (marcada && !noPadrao) extras |= p;
            if (!marcada && noPadrao) negadas |= p;
        }
        return (extras, negadas);
    }

    /// <summary>
    /// "— nenhum —" e os profissionais ATIVOS, com a especialidade. O vinculado ATUAL entra
    /// mesmo inativo, marcado "(inativo)": no WPF ele sumia da lista, a combo caía em
    /// "— nenhum —" e o Salvar DESFAZIA o vínculo de quem só abriu para trocar a senha.
    /// </summary>
    public static IReadOnlyList<OpcaoDeVinculo> Vinculos(IEnumerable<Profissional> profissionais, int? vinculadoAtual)
    {
        var opcoes = new List<OpcaoDeVinculo> { new(null, "— nenhum —", null) };
        foreach (var p in profissionais)
        {
            if (!p.Ativo && p.Id != vinculadoAtual) continue;
            var nome = p.Nome
                + (p.EspecialidadeCodigo is { } codigo ? " · " + CatalogoEspecialidades.Nome(codigo) : "")
                + (p.Ativo ? "" : " (inativo)");
            opcoes.Add(new OpcaoDeVinculo(p.Id, nome, p.EspecialidadeCodigo));
        }
        return opcoes;
    }

    /// <summary>O CPF mudou de fato? Comparar normalizado evita gravação e auditoria à toa.</summary>
    public static bool CpfMudou(string? gravado, string? digitado)
        => Cpf.Normalizar(gravado) != Cpf.Normalizar(string.IsNullOrWhiteSpace(digitado) ? null : digitado.Trim());

    /// <summary>
    /// A crítica da senha digitada no campo MASCARADO: a do domínio (a mesma do cadastro e da
    /// troca) e a repetição — sem ver o texto, a repetição é o que pega o erro de digitação.
    /// Devolve só a explicação, nunca a senha.
    /// </summary>
    public static string? CriticarSenha(string? senha, string? repeticao)
    {
        if (HashSenha.Criticar(senha) is { } critica) return critica;
        return string.Equals(senha, repeticao, StringComparison.Ordinal)
            ? null
            : "As duas senhas não são iguais. Digite de novo.";
    }

    /// <summary>
    /// O que falta para gravar, antes de chamar o serviço (que critica de novo). Na edição a
    /// senha em branco significa "não mexer" — obrigar a redigitar faria a direção trocar a
    /// senha de alguém sem querer, em toda edição.
    /// </summary>
    public static string? CriticarCadastro(bool novo, string? nome, string? login, string? senha, string? repeticao)
    {
        if (string.IsNullOrWhiteSpace(nome)) return "Informe o nome do usuário.";
        if (novo && string.IsNullOrWhiteSpace(login)) return "Informe o login.";
        if (!novo && string.IsNullOrEmpty(senha) && string.IsNullOrEmpty(repeticao)) return null;
        return CriticarSenha(senha, repeticao);
    }

    /// <summary>
    /// A recusa do serviço como FRASE: <see cref="ArgumentException"/> acrescenta
    /// "(Parameter 'login')" ao texto, que não diz nada a quem está no balcão. Nunca carrega o
    /// valor digitado — as recusas do <see cref="AcessoService"/> falam da regra, não da senha.
    /// </summary>
    public static string Frase(Exception ex)
        => ex is ArgumentException { ParamName: { } nome } a
            ? a.Message.Replace($" (Parameter '{nome}')", string.Empty, StringComparison.Ordinal)
            : ex.Message;

    private static int ContarBits(Permissao p) => System.Numerics.BitOperations.PopCount((uint)(int)p);
}
