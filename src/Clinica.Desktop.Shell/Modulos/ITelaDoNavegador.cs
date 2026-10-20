namespace Clinica.Desktop.Shell.Modulos;

/// <summary>
/// Uma tela que o shell mostra no navegador da janela (out/2026). Desde que a moldura virou
/// Blazor, o que <see cref="IModuloApp.CriarTela"/> devolve não entra mais numa árvore WPF:
/// é um DESCRITOR — o componente Razor que o palco da janela deve pôr à vista. A
/// <c>TelaBlazor</c> dos módulos o implementa; a tela de Treinamento, que é do shell, usa
/// <see cref="TelaDoNavegador"/>.
/// </summary>
public interface ITelaDoNavegador
{
    /// <summary>O componente Razor da tela (ex.: <c>typeof(Telas.Caixa)</c>).</summary>
    Type Componente { get; }
}

/// <summary>O descritor mais simples: só o componente.</summary>
public sealed record TelaDoNavegador(Type Componente) : ITelaDoNavegador;
