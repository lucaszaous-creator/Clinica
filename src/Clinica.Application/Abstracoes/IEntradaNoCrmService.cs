namespace Clinica.Application.Abstracoes;

/// <summary>
/// O bilhete que leva quem já entrou no app para dentro do CRM (out/2026,
/// <see cref="Servicos.EntradaNoCrm"/>). O app EMITE; a ponte CONFERE.
/// </summary>
public interface IEntradaNoCrmService
{
    /// <summary>Grava o hash de um bilhete novo para o usuário e devolve o bilhete (só ele vai à URL).</summary>
    Task<string> EmitirAsync(int usuarioId, CancellationToken ct = default);

    /// <summary>
    /// Confere e gasta o bilhete: devolve o usuário quando ele vale, ou <c>null</c> (formato
    /// errado, desconhecido, vencido ou já usado). Duas conferências do mesmo bilhete ao mesmo
    /// tempo: só uma ganha.
    /// </summary>
    Task<int?> ConferirAsync(string? bilhete, CancellationToken ct = default);
}
