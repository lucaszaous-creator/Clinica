using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Clinica.Domain.Entities;
using Clinica.Domain.Prontuario;

namespace Clinica.Application.Tablet;

public record ItemTablet(int Ordem, string Descricao, string? Detalhe, string? Codigo);
public record DocumentoTablet(int DocumentoId, int PacienteId, string Nome, DateOnly? Nascimento,
    string? Identificacao, string Numero, DateOnly Data, int? ModeloId, string? Titulo,
    string? Corpo, string? Observacoes, ItemTablet[] Itens);
public record PrepararTablet(int PacienteId, int[] Modelos, DateOnly Nascimento,
    string IdentidadeConferida);
public record EnviarRubrica(Guid Idempotencia, string ConteudoHash,
    Dictionary<int, string?> Respostas, string? AlergiasDetalhes, string TracoPng, bool Confirmo);
public record RespostasTablet(SortedDictionary<int, string?> Respostas, string? AlergiasDetalhes);

public static class ContratoTablet
{
    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    public static string Serializar<T>(T valor) => JsonSerializer.Serialize(valor, Json);
    public static string Hash(string texto) => Hash(Encoding.UTF8.GetBytes(texto));
    public static string Hash(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes));
    public static DocumentoTablet Fotografar(DocumentoClinico d) => new(d.Id, d.PacienteId,
        d.Paciente?.Nome ?? "", d.Paciente?.DataNascimento, d.Paciente?.Documento,
        d.Numero, d.Data, d.ModeloOrigemId, d.Titulo, d.Corpo, d.Observacoes,
        d.Itens.OrderBy(i => i.Ordem).Select(i => new ItemTablet(i.Ordem, i.Descricao, i.Detalhe, i.Codigo)).ToArray());

    public static RespostasTablet ValidarRespostas(DocumentoTablet documento, EnviarRubrica envio)
    {
        if (!envio.Confirmo || envio.Idempotencia == Guid.Empty)
            throw new InvalidOperationException("Confirme que deseja assinar este documento.");
        if (envio.Respostas is null || envio.Respostas.Count != documento.Itens.Length
            || documento.Itens.Any(i => !envio.Respostas.TryGetValue(i.Ordem, out var r) || r is not ("Sim" or "Não")))
            throw new InvalidOperationException("Responda todas as declarações, incluindo alergias, antes de assinar.");
        var alergia = documento.Itens.SingleOrDefault(i => i.Codigo == RespostaDeclaracao.CodigoAlergiasTablet)
            ?? throw new InvalidOperationException("Este documento precisa da pergunta obrigatória sobre alergias.");
        var detalhe = envio.AlergiasDetalhes?.Trim();
        if (detalhe?.Length > 500)
            throw new InvalidOperationException("Descreva as alergias em até 500 caracteres.");
        if (envio.Respostas[alergia.Ordem] == "Não" && !string.IsNullOrEmpty(detalhe))
            throw new InvalidOperationException("Confira a resposta sobre alergias e o relato preenchido.");
        return new(new SortedDictionary<int, string?>(envio.Respostas), string.IsNullOrEmpty(detalhe) ? null : detalhe);
    }
}
