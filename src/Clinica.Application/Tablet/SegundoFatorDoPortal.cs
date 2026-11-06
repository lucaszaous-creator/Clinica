namespace Clinica.Application.Tablet;

/// <summary>Em que pé está o segundo fator do portal de uma pessoa.</summary>
public enum SituacaoDoSegundoFator { SemAutenticador, Ativo, Travado }

/// <summary>
/// O que a DIREÇÃO faz com o segundo fator do portal (nov/2026), pela tela de Acessos — no app e na
/// gestão web: zerar o autenticador de quem perdeu o celular (ou travou de tanto errar). A pessoa
/// entra de novo só com a senha e o portal volta a convidá-la a ativar.
/// </summary>
public interface ISegundoFatorDoPortal
{
    Task ZerarAsync(int usuarioId, string operador, CancellationToken ct = default);

    Task<IReadOnlyDictionary<int, SituacaoDoSegundoFator>> SituacoesAsync(CancellationToken ct = default);
}
