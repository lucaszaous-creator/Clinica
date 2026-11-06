namespace Clinica.Application.Tablet;

/// <summary>Em que pé está o segundo fator do portal de uma pessoa (a coluna da tela de Acessos).</summary>
public enum SituacaoDoSegundoFator { SemAutenticador, Liberado, Ativo, Travado }

/// <summary>
/// O que a DIREÇÃO faz com o segundo fator do portal (nov/2026), pela tela de Acessos — no app e na
/// gestão web. Liberar o primeiro acesso gera um código de uso único; para quem já tinha
/// autenticador (celular perdido), liberar também o ZERA, e o próximo login cadastra de novo.
/// </summary>
public interface ISegundoFatorDoPortal
{
    /// <summary>Validade do código de liberação.</summary>
    static TimeSpan Validade => TimeSpan.FromHours(72);

    Task<string> LiberarPrimeiroAcessoAsync(int usuarioId, string operador, CancellationToken ct = default);

    Task<IReadOnlyDictionary<int, SituacaoDoSegundoFator>> SituacoesAsync(CancellationToken ct = default);
}
