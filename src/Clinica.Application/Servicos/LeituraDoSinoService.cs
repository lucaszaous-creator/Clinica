using System.Text.Json;
using Clinica.Application.Abstracoes;
using Clinica.Application.Modelos;

namespace Clinica.Application.Servicos;

/// <summary>
/// Guarda o que cada usuário já viu e leu no sino (<see cref="MarcasDoSino"/>) — no BANCO,
/// por usuário (out/2026). Na máquina não serviria: a recepção divide o computador e a mesma
/// pessoa entra em mais de um; reabrir o app ou trocar de posto não pode trazer o número de
/// volta.
///
/// Cada gravação RELÊ o que está no banco e soma a ele: a mesma pessoa com o app aberto em
/// duas máquinas não apaga o que a outra marcou. Sem linha gravada, <see cref="ObterAsync"/>
/// devolve <c>null</c> — é o primeiro uso, e quem chama decide semear (marcar como visto e
/// lido o que já estava lá), para não estrear com o dia inteiro piscando.
/// </summary>
public sealed class LeituraDoSinoService(IClinicaRepositorio repo)
{
    private static readonly JsonSerializerOptions Json = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    private sealed record Gravado(List<string>? Vistas, List<string>? Lidas);

    /// <summary>A chave da configuração deste usuário (cabe nos 60 caracteres da coluna).</summary>
    public static string Chave(int usuarioId) => $"SinoLeitura:{usuarioId}";

    /// <summary>As marcas gravadas, ou <c>null</c> no primeiro uso. JSON estragado vale como vazio (e fica no log).</summary>
    public async Task<MarcasDoSino?> ObterAsync(int usuarioId, CancellationToken ct = default)
    {
        var json = await repo.ObterConfiguracaoAsync(Chave(usuarioId), ct);
        if (string.IsNullOrWhiteSpace(json)) return null;
        try
        {
            var g = JsonSerializer.Deserialize<Gravado>(json, Json);
            return new MarcasDoSino(g?.Vistas ?? [], g?.Lidas ?? []);
        }
        catch (JsonException ex)
        {
            Diagnostico.Registrar($"Sino — leitura do usuário {usuarioId} ilegível; recomeça vazia", ex);
            return MarcasDoSino.Vazias;
        }
    }

    /// <summary>Abrir o sino: as chaves passam a VISTAS.</summary>
    public Task<MarcasDoSino> VerAsync(int usuarioId, IEnumerable<string> chaves, CancellationToken ct = default)
        => AplicarAsync(usuarioId, m => m.Ver(chaves), ct);

    /// <summary>Marcar como lidas (todas as do sino, ou a clicada) — e vistas junto.</summary>
    public Task<MarcasDoSino> LerAsync(int usuarioId, IEnumerable<string> chaves, CancellationToken ct = default)
        => AplicarAsync(usuarioId, m => m.LerTodas(chaves), ct);

    private async Task<MarcasDoSino> AplicarAsync(int usuarioId, Func<MarcasDoSino, MarcasDoSino> mudar, CancellationToken ct)
    {
        var atual = await ObterAsync(usuarioId, ct) ?? MarcasDoSino.Vazias;
        var nova = mudar(atual);
        await repo.SalvarConfiguracaoAsync(Chave(usuarioId),
            JsonSerializer.Serialize(new Gravado(nova.Vistas.ToList(), nova.Lidas.ToList()), Json), ct);
        await repo.SalvarAsync(ct);
        return nova;
    }
}
