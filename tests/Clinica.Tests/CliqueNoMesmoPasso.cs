using AngleSharp.Dom;
using Bunit;

namespace Clinica.Tests;

/// <summary>
/// Achar e agir NO MESMO passo do renderizador (25/10). Uma tela que publica algo depois de
/// abrir — o retrato da ficha no avatar do WhatsApp chega depois da lista — pode redesenhar entre
/// o <c>Find</c> e o <c>Click</c>; o bUnit então recusa o clique ("no event handler with ID").
/// O SQLite responde tão rápido que a corrida não aparece; no Postgres, aparece. O caminho que o
/// bUnit recomenda é achar dentro do <c>InvokeAsync</c>.
/// </summary>
internal static class CliqueNoMesmoPasso
{
    public static void Clicar(this IRenderedFragment tela, Func<IElement> achar)
        => tela.InvokeAsync(() => achar().Click()).GetAwaiter().GetResult();

    public static void Digitar(this IRenderedFragment tela, Func<IElement> achar, string texto)
        => tela.InvokeAsync(() => achar().Input(texto)).GetAwaiter().GetResult();
}
