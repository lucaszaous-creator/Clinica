using Clinica.Domain;
using QuestPDF.Fluent;
using QuestPDF.Infrastructure;

namespace Clinica.Application.Servicos;

internal static class TextoFormatadoPdf
{
    public static void Trechos(TextDescriptor texto,string? conteudo,string? formato,float tamanho=10)
    {
        foreach(var trecho in TextoFormatado.Ler(conteudo,formato)) {
            var span=texto.Span(trecho.Texto).FontSize(tamanho);
            if(trecho.Negrito)span.Bold();
            if(trecho.Italico)span.Italic();
        }
    }
    public static void TextoClinico(this IContainer container,string? conteudo,string? formato)
        => container.Text(t=>Trechos(t,conteudo,formato));
}
