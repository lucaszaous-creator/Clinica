using System.Text.Json;
using Clinica.Domain.Entities;

namespace Clinica.Domain;

/// <summary>Conteúdo reutilizável, sem paciente, autoria de atendimento ou registros de execução.</summary>
public sealed record ItemModeloInfusao(string Descricao, string? DescricaoFormatada = null,
    string? Dose = null, string? Diluente = "SF 0,9%", string? Volume = null,
    ViaAdministracao Via = ViaAdministracao.Endovenosa, string? TempoInfusao = "1h",
    bool SeNecessario = false, string? Observacoes = null, string? ObservacoesFormatadas = null);

public sealed record ModeloInfusao(string? Indicacao, string? Observacoes,
    ItemModeloInfusao[] Itens, string? IndicacaoFormatada = null, string? ObservacoesFormatadas = null)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public string Guardar()
    {
        if (Itens is not { Length: > 0 and <= 50 } || Itens.Any(i => i is null || string.IsNullOrWhiteSpace(i.Descricao) || !Enum.IsDefined(i.Via)))
            throw new InvalidOperationException("Preencha ao menos um bloco de prescrição antes de salvar o modelo de infusão.");
        if (Indicacao?.Length>500 || Observacoes?.Length>2000 || Itens.Any(i => i.Descricao.Length > 20000 || i.Dose?.Length>60 || i.Observacoes?.Length>1000 || i.Diluente?.Length > 120 || i.Volume?.Length > 60 || i.TempoInfusao?.Length > 60))
            throw new InvalidOperationException("Confira o tamanho dos campos do modelo de infusão.");
        var json = JsonSerializer.Serialize(this, Json);
        if (json.Length > 500_000) throw new InvalidOperationException("Divida o conteúdo em modelos menores.");
        return json;
    }

    public static ModeloInfusao Ler(string? json)
    {
        if (string.IsNullOrWhiteSpace(json) || json.Length > 500_000)
            throw new InvalidOperationException("O modelo de infusão está incompleto. Abra-o novamente.");
        try
        {
            var modelo = JsonSerializer.Deserialize<ModeloInfusao>(json, Json)
                ?? throw new InvalidOperationException("Modelo de infusão inválido.");
            modelo.Guardar();
            return modelo;
        }
        catch (JsonException) { throw new InvalidOperationException("O modelo de infusão está inválido. Confira seu conteúdo."); }
    }

    public static ItemModeloInfusao De(ItemPrescricaoInterna i) => new(i.Descricao, i.DescricaoFormatada,
        i.Dose, i.Diluente, i.Volume, i.Via, i.TempoInfusao, i.SeNecessario, i.Observacoes, i.ObservacoesFormatadas);

    public static ItemPrescricaoInterna Para(ItemModeloInfusao i) => new()
    {
        Descricao = i.Descricao, DescricaoFormatada = i.DescricaoFormatada, Dose = i.Dose,
        Diluente = i.Diluente, Volume = i.Volume, Via = i.Via, TempoInfusao = i.TempoInfusao,
        SeNecessario = i.SeNecessario, Observacoes = i.Observacoes, ObservacoesFormatadas = i.ObservacoesFormatadas
    };
}
