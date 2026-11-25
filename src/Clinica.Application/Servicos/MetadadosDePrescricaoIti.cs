using Clinica.Application.Modelos;
using Clinica.Domain.Entities;
using PdfSharp.Pdf;
using PdfSharp.Pdf.IO;

namespace Clinica.Application.Servicos;

/// <summary>
/// Os metadados de PRESCRIÇÃO do ITI (nov/2026, autorização de 24/11, ponto 2), gravados no PDF ANTES de assinar.
///
/// Fonte: o Guia de Orientações aos Desenvolvedores do VALIDAR (validar.iti.gov.br/guia-desenvolvedor.html).
/// - Cap. I, item 3: a prescrição "deve[m] ser incluídos como metadados" o OID do documento, o do registro
///   profissional e o da UF.
/// - Cap. V: 2.16.76.1.12.1.1 = "Prescrição de medicamento"; 2.16.76.1.4.2.2.1/.2/.3 = CFM — número de registro,
///   UF de registro e especialidade.
///
/// ⚠️ O guia NÃO diz em que lugar do PDF nem em que formato de valor. A decisão (Jade/dono, 25/11/2026) é a leitura
/// literal: uma entrada do dicionário Info por OID (a chave é o próprio OID) e, como valor, o rótulo do guia para o
/// tipo do documento e o dado do prescritor para os do CFM. Não mexe na assinatura: o Info entra antes, coberto pelo
/// hash como qualquer outro byte. Se o validador não reconhecer, muda-se SÓ aqui.
/// </summary>
public static class MetadadosDePrescricaoIti
{
    public const string OidPrescricao = "2.16.76.1.12.1.1";
    public const string RotuloPrescricao = "Prescrição de medicamento";
    public const string OidCfmNumero = "2.16.76.1.4.2.2.1";
    public const string OidCfmUf = "2.16.76.1.4.2.2.2";
    public const string OidCfmEspecialidade = "2.16.76.1.4.2.2.3";

    /// <summary>As entradas para este documento — vazio quando ele não é receita. Os do CFM só para médico (CRM).</summary>
    public static IReadOnlyDictionary<string, string> Entradas(DocumentoClinico documento)
    {
        var saida = new Dictionary<string, string>(StringComparer.Ordinal);
        if (!TipoDocumentoInfo.EhReceita(documento.Tipo)) return saida;
        saida[OidPrescricao] = RotuloPrescricao;
        var r = RegistroDoPrescritor.De(documento.Profissional);
        if (r.EhMedico)
        {
            if (r.Numero is { } n) saida[OidCfmNumero] = n;
            if (r.Uf is { } uf) saida[OidCfmUf] = uf;
            if (r.Especialidade is { } e) saida[OidCfmEspecialidade] = e;
        }
        return saida;
    }

    /// <summary>Grava as entradas no Info do PDF. Sem entradas, devolve os MESMOS bytes.</summary>
    public static byte[] Aplicar(byte[] pdf, DocumentoClinico documento)
    {
        var entradas = Entradas(documento);
        if (entradas.Count == 0) return pdf;
        using var entrada = new MemoryStream(pdf, writable: false);
        using var doc = PdfReader.Open(entrada, PdfDocumentOpenMode.Modify);
        foreach (var (oid, valor) in entradas)
            doc.Info.Elements.SetString("/" + oid, valor);
        using var saida = new MemoryStream();
        doc.Save(saida, false);
        return saida.ToArray();
    }
}
