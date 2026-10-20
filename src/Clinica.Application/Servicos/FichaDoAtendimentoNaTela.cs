using Clinica.Domain.Entities;
using Microsoft.Extensions.DependencyInjection;

namespace Clinica.Application.Servicos;

/// <summary>
/// A FICHA DO ATENDIMENTO — o relatório de evolução recortado num dia, que o médico e a
/// enfermagem entregam ao paciente (parcela 78). Subiu do shell WPF (<c>FichaDoAtendimento</c>)
/// para a Application em out/2026, na migração da tela do paciente para Blazor: a tela Blazor
/// e o WPF emitem pelo MESMO caminho — documento numerado, PDF e a trilha de exportação.
///
/// A segunda barreira (<c>VerProntuario</c>) e o "salve antes" são de quem chama; aqui se emite.
/// </summary>
public static class FichaDoAtendimentoNaTela
{
    /// <summary>O que a tela diz quando há texto digitado e ainda não gravado.</summary>
    public const string SalveAntes =
        "Salve a sessão antes de imprimir a ficha — o papel sai do que está gravado no prontuário, "
        + "e o que você digitou ainda não está.";

    /// <summary>
    /// Emite o documento do dia, gera o PDF e registra a trilha (dado de saúde saindo para um
    /// arquivo). Devolve o número do documento e os bytes.
    /// </summary>
    public static async Task<(string Numero, byte[] Pdf)> EmitirAsync(
        IServiceProvider servicos, int pacienteId, int? profissionalId, DateOnly dia, string operador)
    {
        var documento = await servicos.GetRequiredService<DocumentoClinicoService>()
            .EmitirRelatorioEvolucaoAsync(pacienteId, profissionalId, inicio: dia, fim: dia, operador: operador);
        var pdf = await servicos.GetRequiredService<DocumentosClinicosPdfService>().GerarAsync(
            documento.Id, await servicos.GetRequiredService<ParametrosService>().ObterPrestadorAsync());
        await servicos.GetRequiredService<AcessoProntuarioService>()
            .RegistrarAsync(pacienteId, operador, OrigemAcessoProntuario.ExportacaoClinica);
        return (documento.Numero, pdf);
    }

    public static string NomeDoArquivo(string numero) => $"Ficha-do-atendimento-{numero.Replace('/', '-')}.pdf";

    /// <summary>A frase do resultado: emitida, ou emitida e gravada sem o leitor abrir.</summary>
    public static string Frase(string numero, string? erroAoAbrir)
        => erroAoAbrir is null
            ? $"Ficha {numero} emitida — ela fica na lista de documentos do paciente."
            : $"Ficha {numero} emitida e gravada, mas o leitor de PDF não abriu: {erroAoAbrir}";
}
