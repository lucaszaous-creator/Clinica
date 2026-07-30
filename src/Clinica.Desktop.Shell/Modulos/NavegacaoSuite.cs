namespace Clinica.Desktop.Shell.Modulos;

/// <summary>
/// Pedir ao shell para abrir outra seção, de dentro de uma tela (parcela 22).
///
/// Nasceu do painel da direção, que lista o que exige ação hoje: "3 contas vencidas",
/// "2 vendas sem depósito". Um painel que aponta o problema e não leva até ele obriga a
/// pessoa a decorar em qual das quatro seções da sidebar mora o assunto — e a leva a
/// resolver o que está à mão em vez do que é urgente.
///
/// É estático pelo mesmo motivo de <c>SessaoUsuario.Atual</c>: quem constrói a
/// <c>ShellViewModel</c> é o <c>SuiteApp</c>, à mão, e ela não está no DI — uma tela de
/// módulo não tem como recebê-la. Há uma janela de shell por processo, então não há
/// ambiguidade sobre quem responde.
/// </summary>
public static class NavegacaoSuite
{
    /// <summary>(chave, apenasConferir) → o destino existe e, se não era consulta, foi aberto.</summary>
    private static Func<string, bool, bool>? _navegar;

    /// <summary>Ligado pela <c>ShellViewModel</c> na construção. Só o shell chama.</summary>
    internal static void Ligar(Func<string, bool, bool> navegar) => _navegar = navegar;

    /// <summary>
    /// O destino existe e está visível para quem está usando o app — o módulo pode não
    /// estar carregado neste executável, ou a permissão pode faltar.
    ///
    /// Existe para a tela DESABILITAR o botão em vez de descobrir na hora do clique:
    /// botão que não faz nada é pior do que botão ausente.
    /// </summary>
    public static bool Existe(string chave) => _navegar?.Invoke(chave, true) ?? false;

    /// <summary>Abre a seção. <c>false</c> = o destino não existe aqui, e nada aconteceu.</summary>
    public static bool Ir(string chave) => _navegar?.Invoke(chave, false) ?? false;
}
