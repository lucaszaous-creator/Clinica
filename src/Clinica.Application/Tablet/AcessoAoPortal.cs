using Clinica.Domain.Entities;

namespace Clinica.Application.Tablet;

/// <summary>
/// Para onde o login do portal leva cada pessoa (nov/2026). Um login só para a equipe: quem
/// atende ou colhe assinatura vai ao POSTO (as telas do portal de hoje); quem tem o perfil
/// GERENTE vai à GESTÃO (o Gerente Geral no navegador). Quem tem os dois recebe os dois.
///
/// A regra mora aqui, num lugar só, com teste: a API decide a resposta do login e a gestão web
/// decide quem entra com a MESMA função — duas réguas seriam duas portas.
/// </summary>
public static class AcessoAoPortal
{
    public const string Posto = "posto";
    public const string Gestao = "gestao";

    /// <summary>
    /// A gestão web é do perfil Gerente. Não é um bit novo de permissão de propósito: o perfil
    /// já é o que a direção atribui na tela de Acessos, e o enum de permissões está no último
    /// bit seguro do <c>int</c>.
    /// </summary>
    public static bool PodeGestao(UsuarioSistema u)
        => u.Ativo && !u.DeveTrocarSenha && u.Perfil == PerfilAcesso.Gerente;

    /// <summary>Colher assinatura de paciente no tablet (a coleta dos termos).</summary>
    public static bool PodeColher(UsuarioSistema u) => u.Ativo && !u.DeveTrocarSenha
        && u.Pode(Permissao.ColherAssinaturaPaciente) && u.Pode(Permissao.VerAgenda);

    /// <summary>
    /// A régua do POSTO (as telas do portal em /profissional/): colher assinatura ou atender. A API
    /// decide os destinos com ela e a gestão web decide se mostra a porta "Meu dia" com ela —
    /// a mesma função, para a porta nunca levar a um posto que recusaria a pessoa.
    /// </summary>
    /// <param name="atendimentoHabilitado">A chave <c>Portal:AtendimentoHabilitado</c> da instalação.</param>
    public static bool PodePosto(UsuarioSistema u, bool atendimentoHabilitado)
        => PodeColher(u) || (atendimentoHabilitado && PoliticaAtendimentoTablet.PodeUsarPosto(u));

    /// <param name="podePosto">A régua do posto, que mora no serviço do portal (depende da configuração do atendimento).</param>
    public static IReadOnlyList<string> Destinos(UsuarioSistema u, bool podePosto)
    {
        var d = new List<string>(2);
        if (podePosto) d.Add(Posto);
        if (PodeGestao(u)) d.Add(Gestao);
        return d;
    }
}
