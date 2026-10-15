using Clinica.Domain.Entities;

namespace Clinica.Application.Servicos;

/// <summary>
/// Quem assina, por padrão, o documento que se está emitindo — e o que a tela diz quando
/// não há resposta honesta para isso.
/// </summary>
/// <param name="Profissional">O assinante proposto; nulo quando ninguém pode ser presumido.</param>
/// <param name="Aviso">A frase para a tela quando o assinante ficou em branco.</param>
public sealed record EscolhaDoAssinante(Profissional? Profissional, string? Aviso);

/// <summary>
/// A regra do assinante padrão (out/2026 — a cliente entrou como "Lucas", login da
/// direção, e o atestado saiu proposto em nome de outra médica).
///
/// ⚠️ <b>O caminho de baixo era o defeito.</b> A janela propunha o profissional ligado ao
/// login e, sem ele, o PRIMEIRO DA LISTA — que é o primeiro em ORDEM ALFABÉTICA. Para
/// quem não atende (a direção, a recepção) ou para quem tem o vínculo apontando para um
/// cadastro desativado, isso punha no papel o nome de uma colega que não sabe que o
/// documento existe. O nome impresso é quem responde por ele, e um padrão que depende da
/// ordem alfabética não é uma decisão — é um acidente com aparência de decisão (a mesma
/// lição do convênio padrão do cadastro).
///
/// Agora: o vínculo do login, e só ele. Sem vínculo (ou com o vínculo fora da lista de
/// ativos) o assinante fica EM BRANCO e a tela DIZ por quê — quem assina passa a ser uma
/// escolha visível, nunca um palpite calado.
///
/// Mora aqui, e não na ViewModel do WPF, para o <c>dotnet test</c> alcançar.
/// </summary>
public static class AssinanteDoDocumento
{
    /// <summary>Frase quando o login não está ligado a nenhum profissional.</summary>
    public const string AvisoSemVinculo =
        "Escolha quem assina — seu usuário não está ligado a um profissional.";

    /// <summary>Frase quando o profissional ligado ao login não está ativo na Equipe.</summary>
    public const string AvisoVinculoInativo =
        "Escolha quem assina — o profissional ligado ao seu usuário não está "
        + "ativo na Equipe.";

    /// <summary>
    /// Documento que só vale assinado por profissional habilitado. Definição ÚNICA: a
    /// recusa do <see cref="DocumentoClinicoService"/> e o botão da tela leem esta mesma
    /// função — duas listas divergiriam na primeira correção, e a tela acenderia um
    /// "Emitir" que o serviço recusa.
    /// </summary>
    public static bool Exige(TipoDocumentoClinico tipo)
        => tipo is TipoDocumentoClinico.Receita
               or TipoDocumentoClinico.Atestado
               or TipoDocumentoClinico.PedidoExame;

    /// <summary>
    /// O assinante proposto a partir da lista de profissionais ATIVOS que a tela oferece e
    /// do vínculo do login (<c>SessaoUsuario.ProfissionalId</c>).
    /// </summary>
    public static EscolhaDoAssinante Resolver(
        IEnumerable<Profissional> ativos, int? profissionalDoLogin)
    {
        if (profissionalDoLogin is not { } id || id <= 0)
            return new EscolhaDoAssinante(null, AvisoSemVinculo);

        var ligado = ativos.FirstOrDefault(p => p.Id == id);
        return ligado is null
            ? new EscolhaDoAssinante(null, AvisoVinculoInativo)
            : new EscolhaDoAssinante(ligado, null);
    }

    /// <summary>
    /// Por que o "Emitir" está apagado, quando está por falta de assinante. Nulo quando o
    /// tipo não exige assinante ou quando alguém já foi escolhido.
    /// </summary>
    public static string? MotivoSemAssinante(
        TipoDocumentoClinico tipo, Profissional? escolhido, string? avisoDaAbertura)
    {
        if (escolhido is not null || !Exige(tipo)) return null;
        return avisoDaAbertura
               ?? $"{TipoDocumentoInfo.Rotular(tipo)} precisa de quem assina — escolha o "
                  + "profissional responsável.";
    }
}
