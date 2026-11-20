using Clinica.Application.Modelos;

namespace Clinica.Application.Servicos;

/// <summary>
/// O PDF do demonstrativo de repasse médico — o papel que vai ao profissional, no leiaute
/// que a clínica já usava (cabeçalho, resumo por convênio, valores por consulta, anexo por
/// convênio e pagamento, assinatura de quem conferiu).
/// ⚠️ CONTRATO FIXADO — a tela chama exatamente estas assinaturas.
/// </summary>
public static class DemonstrativoRepassePdf
{
    public static byte[] Gerar(DemonstrativoRepasse demonstrativo)
        => throw new NotImplementedException();

    /// <summary>Nome de arquivo sugerido: Demonstrativo_Repasse_{Nome}_{MM-aaaa}.pdf</summary>
    public static string NomeDoArquivo(DemonstrativoRepasse demonstrativo)
        => throw new NotImplementedException();

    /// <summary>O mesmo conteúdo em CSV (pt-BR, ';'), uma linha por item.</summary>
    public static string Csv(DemonstrativoRepasse demonstrativo)
        => throw new NotImplementedException();
}
