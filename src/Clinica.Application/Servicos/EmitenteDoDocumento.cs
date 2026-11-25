using Clinica.Application.Modelos;
using Clinica.Domain.Entities;

namespace Clinica.Application.Servicos;

/// <summary>Com o que o emitente pode assinar agora: um meio e o estado dele, em uma frase para a tela.</summary>
/// <param name="Meio">"A1" ou "SafeID".</param>
/// <param name="Disponivel">Dá para usar já.</param>
/// <param name="Estado">"válido até 10/03/2027", "não cadastrado", "vencido em …", "habilitado", "não habilitado na clínica".</param>
public sealed record MeioDeAssinatura(string Meio, bool Disponivel, string Estado);

/// <summary>Quem assina este documento, e se é quem está logado.</summary>
/// <param name="LogadoAssina">O login É o emitente: "Emitir e assinar" vale.</param>
/// <param name="Frase">O que a tela diz sobre quem assina (nunca vazio quando há emitente).</param>
public sealed record AssinaturaDoEmitente(bool LogadoAssina, string Frase);

/// <summary>
/// O EMITENTE da janela de emitir documento (nov/2026, pedido do dono, no iPhone: "não está identificando o profissional
/// logado na conta e tão pouco dá as opções de quem assina"). O padrão continua sendo o de <see cref="AssinanteDoDocumento"/>
/// (o profissional ligado ao login, nunca "o primeiro da lista"); aqui mora o resto da pergunta:
/// <list type="bullet">
/// <item>como o emitente aparece no TOPO da janela ("Emitindo como Dra. … · CRM 52-…/RJ");</item>
/// <item>QUEM pode trocar o emitente: a direção (perfil Gerente) e quem não tem profissional ligado ao login (a recepção,
/// que emite a declaração em nome de alguém). O profissional comum emite como ele mesmo — a troca não aparece;</item>
/// <item>QUEM assina: só o emitente. Escolhido outro, a janela diz que só ele assina e oferece emitir sem assinar
/// (o documento fica em "Para assinar" dele);</item>
/// <item>COM O QUÊ: A1 (cadastrado em "Meu certificado") e SafeID (habilitado na clínica), cada um com o estado.</item>
/// </list>
/// O motor de assinatura não é tocado (docs/safeid-congelado.md): isto só decide o que a tela mostra e qual botão acende.
/// </summary>
public static class EmitenteDoDocumento
{
    /// <summary>A direção escolhe por quem emite; sem profissional ligado ao login, a escolha é obrigatória.</summary>
    public static bool PodeEscolherOutro(PerfilAcesso perfil, int? profissionalDoLogin)
        => perfil == PerfilAcesso.Gerente || profissionalDoLogin is not > 0;

    /// <summary>"CRM 52-0115854-6/RJ" — com o que o cadastro tiver; sem nada, a frase que pede o cadastro.</summary>
    public static string Registro(Profissional p)
    {
        var r = RegistroDoPrescritor.De(p);
        if (r.Numero is null) return string.IsNullOrWhiteSpace(p.RegistroConselho) ? "registro no conselho não cadastrado" : p.RegistroConselho.Trim();
        return $"{(r.Conselho is null ? "" : r.Conselho + " ")}{r.Numero}{(r.Uf is null ? "" : "/" + r.Uf)}";
    }

    /// <summary>A linha do topo da janela: "Dra. Jade Bittencourt · CRM 52-0115854-6/RJ".</summary>
    public static string Rotulo(Profissional p) => $"{p.Nome} · {Registro(p)}";

    /// <summary>
    /// O que falta no cadastro do emitente para o papel sair completo — o receituário imprime CRM e UF em campos próprios.
    /// Nulo quando nada falta. Aviso, não recusa: o documento sai, com o campo em branco.
    /// </summary>
    public static string? FaltaNoCadastro(TipoDocumentoClinico tipo, Profissional p)
    {
        if (tipo != TipoDocumentoClinico.ReceituarioControleEspecial) return null;
        var r = RegistroDoPrescritor.De(p);
        var faltas = new List<string>();
        if (r.Conselho is null) faltas.Add("o conselho (CRM)");
        if (r.Numero is null) faltas.Add("o número");
        if (r.Uf is null) faltas.Add("a UF");
        return faltas.Count == 0 ? null
            : $"Complete {string.Join(" e ", faltas)} de {p.Nome} em Equipe: o receituário sai com o campo em branco.";
    }

    /// <summary>Só o emitente assina. Emitente nulo: quem assina ainda não foi escolhido.</summary>
    public static AssinaturaDoEmitente Assinante(Profissional? emitente, int? profissionalDoLogin)
    {
        if (emitente is null) return new(false, "Escolha o emitente: é ele quem assina.");
        return emitente.Id == profissionalDoLogin
            ? new(true, $"Assina: {emitente.Nome} (você).")
            : new(false, $"Só {emitente.Nome} assina. Emita sem assinar: o documento fica em \"Para assinar\" de {emitente.Nome}.");
    }

    /// <summary>Os meios de assinatura do emitente, na ordem em que a tela os mostra (A1, SafeID).</summary>
    public static IReadOnlyList<MeioDeAssinatura> Meios(DateTime? validadeDoA1, bool safeIdHabilitado, DateTime agora)
        =>
        [
            validadeDoA1 is not { } v ? new("A1", false, "não cadastrado — cadastre em Meu certificado")
                : v < agora ? new("A1", false, $"vencido em {v:dd/MM/yyyy} — cadastre o novo em Meu certificado")
                : new("A1", true, $"válido até {v:dd/MM/yyyy}"),
            safeIdHabilitado ? new("SafeID", true, "habilitado — autoriza pelo celular")
                : new("SafeID", false, "não habilitado na clínica"),
        ];
}
