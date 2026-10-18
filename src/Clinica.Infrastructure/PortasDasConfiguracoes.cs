using System.Text.Json;
using Clinica.Application.Abstracoes;

namespace Clinica.Infrastructure;

/// <summary>
/// A porta da cópia de segurança para a tela de Configurações em Blazor (out/2026). Não
/// copia nada por conta própria: delega ao <see cref="PoliticaBackupService"/> e ao
/// <see cref="BackupService"/>, os mesmos que a tela WPF chamava — só traduz o resultado
/// para os tipos da Application, que é tudo o que a camada de tela enxerga.
/// </summary>
public sealed class CopiaDeSegurancaDaClinica(PoliticaBackupService politica, BackupService backup) : ICopiaDeSeguranca
{
    public async Task<SituacaoDaCopia> SituacaoAsync(CancellationToken ct = default)
    {
        var s = await politica.SituacaoAsync(ct);
        return new SituacaoDaCopia(s.Pasta, s.IntervaloDias, s.CopiasAGuardar, s.Configurada, s.Descrever());
    }

    public async Task<ResultadoDaCopiaAgora> CopiarAgoraAsync(string pasta, int copiasAGuardar, CancellationToken ct = default)
    {
        var r = await politica.ExecutarAsync(pasta, copiasAGuardar, ct);
        return new ResultadoDaCopiaAgora(r.Caminho, r.Manifesto is { } m ? Resumo(m) : null, r.CopiasApagadas, r.Erro);
    }

    public async Task<ResumoDoBackup> GerarAsync(Stream destino, CancellationToken ct = default)
        => Resumo(await backup.GerarAsync(destino, ct));

    public async Task<ResumoDoBackup> ConferirAsync(Stream origem, CancellationToken ct = default)
    {
        try
        {
            return Resumo(await backup.ConferirAsync(origem, ct));
        }
        catch (JsonException)
        {
            // A tela WPF mostrava a frase crua do leitor de JSON ("'O' is an invalid start of
            // value…"), que não diz a ninguém que o arquivo escolhido é outro.
            throw new InvalidOperationException(
                "O arquivo escolhido não é um backup deste sistema — ele não pôde ser lido. "
                + "Confira se é o .json gerado pelo \"Salvar cópia em…\" ou pela cópia automática.");
        }
    }

    public bool PastaAcessivel(string pasta)
    {
        try { return !string.IsNullOrWhiteSpace(pasta) && Directory.Exists(pasta); }
        catch (Exception) { return false; }
    }

    private static ResumoDoBackup Resumo(ManifestoBackup m) => new(
        m.GeradoEm, m.TotalTabelas, m.TotalLinhas, m.Vazio,
        m.Tabela("Pacientes")?.Linhas ?? 0,
        m.Tabela("Evolucoes")?.Linhas ?? 0,
        m.Tabela("Lancamentos")?.Linhas ?? 0);
}

/// <summary>
/// A porta das sessões com conclusão automática pendente (out/2026). Delega ao
/// <see cref="ConclusaoAutomaticaService"/>, com o MESMO relógio de São Paulo que o WPF usava.
/// </summary>
public sealed class PendenciasDeConclusaoDaClinica(ConclusaoAutomaticaService conclusao) : IPendenciasDeConclusao
{
    public async Task<IReadOnlyList<SessaoPendenteDeConclusao>> PendenciasAsync(CancellationToken ct = default)
        => (await conclusao.PendenciasAsync(ConclusaoAutomaticaService.Agora, ct: ct))
            .Select(p => new SessaoPendenteDeConclusao(p.AgendamentoId, p.Paciente, p.Profissional, p.Data, p.Motivo))
            .ToList();
}
