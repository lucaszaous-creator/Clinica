using Clinica.Domain.Entities;

namespace Clinica.Application.Modelos;

/// <summary>
/// DE QUEM É A LISTA que as telas do posto do Consultório mostram — o dia, a semana, a dívida
/// de prontuário e os números — e para onde o <b>Atender</b> leva (out/2026: as telas viraram
/// Blazor e a regra que o adaptador <c>PostoClinico</c> do módulo WPF aplicava desceu para cá).
///
/// ⚠️ A decisão de fundo continua no DOMÍNIO (<see cref="PerfisAcesso"/>: "escreve SÓ por Y" é
/// enfermagem, sem agenda própria). O que mora aqui é a camada que o <c>PostoClinico</c> punha
/// por cima — o perfil <see cref="PerfilAcesso.Enfermagem"/> vale como enfermagem mesmo com um
/// bit extra de escrita —, para o WPF (que ainda abre o atendimento) e o Blazor (que desenha as
/// listas) responderem a MESMA coisa. Duas cópias dessa camada divergiriam na primeira correção,
/// e a que ficasse para trás devolveria a lista VAZIA à enfermeira cadastrada certo.
/// </summary>
public static class PostoDoConsultorioNaTela
{
    /// <summary>O perfil é o de enfermagem — o atalho que o posto aplica antes dos bits.</summary>
    public static bool PerfilDeEnfermagem(PerfilAcesso perfil) => perfil == PerfilAcesso.Enfermagem;

    /// <summary>
    /// O profissional cuja agenda as telas do posto mostram. <c>null</c> = <b>a clínica
    /// inteira</b> (sem vínculo, ou enfermagem — que não tem agenda própria).
    /// </summary>
    public static int? ProfissionalDaLista(PerfilAcesso perfil, Permissao efetivas, int? profissionalId)
        => PerfilDeEnfermagem(perfil) ? null : PerfisAcesso.ProfissionalDaListaDoPosto(efetivas, profissionalId);

    /// <summary>POR QUE a lista é a da clínica — <c>null</c> quando ela não é (a frase do domínio).</summary>
    public static string? MotivoDaLista(Permissao efetivas, int? profissionalId)
        => PerfisAcesso.MotivoDaListaDoPosto(efetivas, profissionalId);

    /// <summary>
    /// O "Atender" leva à seção de escrita da ENFERMAGEM (a passagem, COFEN) e não ao S-O-A-P do
    /// médico — quem escreve pela enfermagem cairia num formulário que não pode gravar.
    /// </summary>
    public static bool AtendimentoDeEnfermagem(PerfilAcesso perfil, Permissao efetivas)
        => PerfilDeEnfermagem(perfil) || PerfisAcesso.EscreveComoEnfermagem(efetivas);

    // ---- os mesmos três, lidos da sessão de quem entrou ----

    public static int? ProfissionalDaLista(SessaoUsuario s) => ProfissionalDaLista(s.Perfil, s.Efetivas, s.ProfissionalId);

    public static string? MotivoDaLista(SessaoUsuario s) => MotivoDaLista(s.Efetivas, s.ProfissionalId);

    public static bool AtendimentoDeEnfermagem(SessaoUsuario s) => AtendimentoDeEnfermagem(s.Perfil, s.Efetivas);
}
