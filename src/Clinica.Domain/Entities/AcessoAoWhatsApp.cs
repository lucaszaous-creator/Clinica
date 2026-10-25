namespace Clinica.Domain.Entities;

/// <summary>
/// QUEM ATENDE O WHATSAPP DA CLÍNICA (25/10/2026, decisão da direção: "só quem terá acesso são as
/// secretárias e o gerente"). Uma regra só, para as quatro portas: o item "WhatsApp" do menu, a
/// tela da aba, o botão da conversa ao lado (Agenda do dia, Ficha, Acompanhamento) e a entrada no
/// CRM pela ponte (<c>ClinicTeamBridge.PodeEntrar</c>), que é a porta que decide de verdade — o
/// CRM web e a API dos apps só aceitam quem a ponte deixa entrar.
///
/// Por que PERFIL e bit, e não só o bit <see cref="Permissao.EditarAgenda"/> (a regra de antes):
/// o bit de marcar horário pode ser concedido a qualquer pessoa em Acessos — a faturista que
/// também agenda, por exemplo (parcela 58) —, e com ele vinha junto a conversa com os pacientes
/// pelo número oficial. O perfil diz QUEM a pessoa é na clínica; o bit continua sendo o ajuste
/// fino: a direção ainda tira o WhatsApp de uma recepcionista específica negando o bit.
///
/// Não é um bit novo de propósito: o enum é gravado como inteiro e o último bit livre antes do
/// sinal é o 1 &lt;&lt; 31 — gastá-lo com uma regra que o perfil já responde não se paga.
/// </summary>
public static class AcessoAoWhatsApp
{
    /// <summary>O bit que, além do perfil, a pessoa precisa ter (o de quem marca e fala com o paciente).</summary>
    public const Permissao Requer = Permissao.EditarAgenda;

    /// <summary>A frase de quem não alcança o WhatsApp da clínica.</summary>
    public const string SemAcesso = "O WhatsApp da clínica é da recepção e da direção. Se você precisa dele, fale com a direção.";

    /// <summary>Os perfis que atendem o WhatsApp: a recepção (as secretárias) e a direção.</summary>
    public static bool PerfilAtende(PerfilAcesso perfil) => perfil is PerfilAcesso.Recepcao or PerfilAcesso.Gerente;

    /// <summary>O perfil atende e o bit vale.</summary>
    public static bool Pode(PerfilAcesso perfil, Permissao efetivas) => PerfilAtende(perfil) && (efetivas & Requer) == Requer;

    /// <summary>Para a sessão do app. Sem login (teste, tela fora do shell) libera, como <see cref="SessaoUsuario.Pode"/>.</summary>
    public static bool Pode(SessaoUsuario sessao) => !sessao.Autenticado || Pode(sessao.Perfil, sessao.Efetivas);

    /// <summary>Para a ponte do CRM: usuário ativo, do perfil certo, com o bit.</summary>
    public static bool Pode(UsuarioSistema usuario) => usuario.Ativo && Pode(usuario.Perfil, usuario.Efetivas);
}
