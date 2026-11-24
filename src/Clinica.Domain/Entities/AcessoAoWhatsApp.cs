namespace Clinica.Domain.Entities;

/// <summary>
/// QUEM ATENDE O WHATSAPP DA CLÍNICA. Uma regra só, para todas as portas: o item "WhatsApp" do menu,
/// a tela da aba, o botão da conversa ao lado (Agenda do dia, Ficha, Acompanhamento), o aviso do
/// @SemDor no sino e a entrada no CRM pela ponte (<c>ClinicTeamBridge.PodeEntrar</c>), que é a porta
/// que decide de verdade — o CRM web e a API dos apps só aceitam quem a ponte deixa entrar.
///
/// SÓ O BIT <see cref="Permissao.EditarAgenda"/>, EM QUALQUER PERFIL (24/11/2026, decisão 4 do "CRM
/// com cara de WhatsApp", aprovada pelo dono). De 25/10 a 24/11 valia perfil Recepção ou Gerente E o
/// bit; com isso o diretor que entra com o perfil Médico ficava sem a aba mesmo com o bit dado em
/// Acessos. Quem recebe o WhatsApp agora é decidido caso a caso em Acessos, por pessoa: o bit de
/// marcar e remarcar é o mesmo de quem fala com o paciente. Os perfis-padrão não mudam nada — só
/// Recepção e Gerente nascem com o bit; Médico, Faturista, Financeiro, Enfermagem e Psicologia só
/// ganham a aba quando a direção dá o bit à pessoa.
/// </summary>
public static class AcessoAoWhatsApp
{
    /// <summary>O bit de quem atende o WhatsApp (o de quem marca e fala com o paciente).</summary>
    public const Permissao Requer = Permissao.EditarAgenda;

    /// <summary>A frase de quem não alcança o WhatsApp da clínica.</summary>
    public const string SemAcesso = "Seu acesso não inclui o WhatsApp da clínica. Se você precisa dele, peça à direção em Acessos.";

    /// <summary>O bit vale (o perfil não decide mais: decisão 4, 24/11/2026).</summary>
    public static bool Pode(Permissao efetivas) => (efetivas & Requer) == Requer;

    /// <summary>
    /// Para a sessão do app. Sem login (teste, tela fora do shell) libera, como <see cref="SessaoUsuario.Pode"/> —
    /// MENOS a sessão fechada do servidor (a gestão web sem login), que nega, como lá (achado da auditoria, nov/2026).
    /// </summary>
    public static bool Pode(SessaoUsuario sessao) => (!sessao.Autenticado && !sessao.Fechada) || Pode(sessao.Efetivas);

    /// <summary>Para a ponte do CRM: usuário ativo, com o bit.</summary>
    public static bool Pode(UsuarioSistema usuario) => usuario.Ativo && Pode(usuario.Efetivas);
}
