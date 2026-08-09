using Clinica.Application;
using Clinica.Application.Servicos;

namespace Clinica.Infrastructure;

/// <summary>
/// A situação da cópia de segurança, como a tela e o aviso de abertura a leem.
/// </summary>
/// <param name="Pasta">Destino configurado. Null = a política não está ligada.</param>
/// <param name="Ultimo">Quando a última cópia foi gravada. Null = nunca houve.</param>
public sealed record SituacaoBackup(
    string? Pasta,
    DateTime? Ultimo,
    int IntervaloDias,
    int CopiasAGuardar,
    int CopiasNaPasta)
{
    /// <summary>A clínica escolheu um destino — sem isso nada roda sozinho.</summary>
    public bool Configurada => !string.IsNullOrWhiteSpace(Pasta);

    public int? DiasDesdeUltimo
        => Ultimo is { } u ? (int)(DateTime.Now.Date - u.Date).TotalDays : null;

    /// <summary>Passou do intervalo (ou nunca houve cópia).</summary>
    public bool Vencida
        => Ultimo is not { } u || (DateTime.Now.Date - u.Date).TotalDays >= IntervaloDias;

    /// <summary>
    /// A frase que a tela mostra. Os três estados são diferentes de propósito: "nunca
    /// houve cópia" é muito pior do que "a cópia está atrasada", e um texto só para os
    /// dois faria a clínica ler o pior caso como rotina.
    /// </summary>
    public string Descrever() => !Configurada
        ? "Cópia automática DESLIGADA — nenhuma pasta de destino foi escolhida. "
          + "Enquanto isso, o backup só acontece quando alguém clica."
        : Ultimo is not { } u
            ? $"Nenhuma cópia foi gravada ainda. O destino está configurado ({Pasta}) e a "
              + $"próxima abertura do Gerente vai gravar a primeira."
            : Vencida
                ? $"Última cópia em {u:dd/MM/yyyy HH:mm} — há {DiasDesdeUltimo} dia(s), "
                  + $"acima do intervalo de {IntervaloDias}. A próxima abertura grava uma nova."
                : $"Última cópia em {u:dd/MM/yyyy HH:mm} ({CopiasNaPasta} na pasta, "
                  + $"guardando as {CopiasAGuardar} mais recentes). Em dia.";
}

/// <summary>O que a execução automática fez.</summary>
public sealed record ResultadoBackupAutomatico(
    bool Executou,
    string? Caminho,
    ManifestoBackup? Manifesto,
    int CopiasApagadas,
    string? Erro)
{
    public bool Falhou => Erro is not null;
}

/// <summary>
/// A POLÍTICA de backup — o que faltava para a ferramenta virar garantia (parcela 52).
///
/// O que existia e por que não bastava
/// -----------------------------------
/// O <see cref="BackupService"/> (parcela 34) é bom: copia a base inteira, gera manifesto
/// conferível e recusa restaurar por cima. Mas era <b>um botão</b> em Configurações do
/// Gerente. A auditoria de fornecedor da cliente pediu, no ponto 6, <i>"política de
/// backup, redundância e recuperação"</i> — e a clínica tinha a ferramenta, não a
/// política. Backup que depende de alguém lembrar de clicar toda semana é backup que
/// existe no manual e não no disco.
///
/// As decisões
/// -----------
/// <b>Roda na ABERTURA do Gerente, não num agendador.</b> O sistema é desktop e não tem
/// serviço residente; inventar um daria uma peça a mais para quebrar em silêncio. A
/// abertura do Gerente é o gancho que o projeto já usa para a rodada de pendências, e a
/// direção abre o app com regularidade suficiente para um intervalo de dias.
///
/// <b>Várias cópias, nunca uma.</b> Guardar só a última é o erro clássico: a corrupção que
/// ninguém percebeu na sexta é copiada por cima da única cópia boa no sábado. O nome do
/// arquivo leva a data, e as mais antigas saem quando passam de
/// <c>CopiasAGuardar</c> — <b>estas sim se apagam</b>, e é o único lugar desta parcela em
/// que apagar é certo: cópia velha não é registro clínico, é redundância de uma base que
/// continua inteira.
///
/// <b>Falhar NÃO impede o app de abrir.</b> Uma pasta de rede indisponível não pode
/// trancar a clínica no começo do dia. A falha vira rastro no log e frase na tela — e
/// "não foi possível copiar" precisa APARECER, senão a clínica acredita estar coberta e
/// não está, que é pior do que não ter a política.
///
/// <b>O que este serviço NÃO resolve, e a clínica precisa saber</b> (está no
/// <c>docs/conformidade-lgpd.md</c>): cópia gravada na mesma máquina, ou num disco preso a
/// ela, não é redundância contra incêndio, furto nem ransomware. A pasta de destino deve
/// ser de rede ou de nuvem sincronizada. O código não tem como conferir isso — nenhum
/// caminho de arquivo diz onde ele fisicamente está —, então a orientação é da tela e do
/// documento, e não uma promessa do serviço.
/// </summary>
public sealed class PoliticaBackupService
{
    private readonly BackupService _backup;
    private readonly ParametrosService _parametros;

    /// <summary>Prefixo dos arquivos que a política gera — é como ela reconhece os seus.</summary>
    public const string Prefixo = "backup-clinica-";

    public PoliticaBackupService(BackupService backup, ParametrosService parametros)
    {
        _backup = backup;
        _parametros = parametros;
    }

    public async Task<SituacaoBackup> SituacaoAsync(CancellationToken ct = default)
    {
        var pasta = await _parametros.ObterPastaBackupAsync(ct);

        return new SituacaoBackup(
            pasta,
            await _parametros.ObterUltimoBackupAsync(ct),
            await _parametros.ObterIntervaloBackupAsync(ct),
            await _parametros.ObterCopiasBackupAsync(ct),
            ContarCopias(pasta));
    }

    /// <summary>
    /// Grava a cópia se estiver vencida. Chamado na abertura do Gerente.
    ///
    /// Devolve <c>Executou = false</c> sem erro quando não era hora (ou quando a política
    /// não está configurada): não é falha, é o caso normal na maioria das aberturas.
    /// </summary>
    public async Task<ResultadoBackupAutomatico> ExecutarSeVencidoAsync(
        CancellationToken ct = default)
    {
        try
        {
            var situacao = await SituacaoAsync(ct);

            if (!situacao.Configurada || !situacao.Vencida)
                return new ResultadoBackupAutomatico(false, null, null, 0, null);

            return await ExecutarAsync(situacao.Pasta!, situacao.CopiasAGuardar, ct);
        }
        catch (Exception ex)
        {
            Diagnostico.Registrar("Backup automático — não pôde ser avaliado", ex);
            return new ResultadoBackupAutomatico(false, null, null, 0, ex.Message);
        }
    }

    /// <summary>
    /// Grava a cópia agora, na pasta indicada, e faz a rotação. Também é o que o botão
    /// "Copiar agora" chama — um caminho só, para o automático e o manual nunca
    /// divergirem no que produzem.
    /// </summary>
    public async Task<ResultadoBackupAutomatico> ExecutarAsync(
        string pasta, int copiasAGuardar, CancellationToken ct = default)
    {
        string? caminho = null;

        try
        {
            Directory.CreateDirectory(pasta);

            // O nome leva data E hora: duas cópias no mesmo dia (uma antes de uma
            // migration, digamos) não podem se sobrescrever em silêncio.
            caminho = Path.Combine(
                pasta, $"{Prefixo}{DateTime.Now:yyyy-MM-dd-HHmm}.json");

            ManifestoBackup manifesto;
            await using (var arquivo = File.Create(caminho))
                manifesto = await _backup.GerarAsync(arquivo, ct);

            // Base vazia gera arquivo válido e inútil. Melhor apagar a cópia e gritar do
            // que deixar na pasta um arquivo com cara de backup bom.
            if (manifesto.Vazio)
            {
                TentarApagar(caminho);
                return new ResultadoBackupAutomatico(false, null, null, 0,
                    "A base não tem uma linha sequer — a cópia foi descartada em vez de "
                    + "ficar na pasta com aparência de backup bom.");
            }

            await _parametros.SalvarUltimoBackupAsync(DateTime.Now, ct);

            return new ResultadoBackupAutomatico(
                true, caminho, manifesto, Rotacionar(pasta, copiasAGuardar), null);
        }
        catch (Exception ex)
        {
            // Arquivo pela metade tem a pior propriedade possível: parece backup.
            if (caminho is not null) TentarApagar(caminho);

            Diagnostico.Registrar($"Backup automático — falha ao gravar em {pasta}", ex);
            return new ResultadoBackupAutomatico(false, null, null, 0, ex.Message);
        }
    }

    /// <summary>
    /// Apaga as cópias que passam do limite, da mais antiga para a mais nova. Devolve
    /// quantas saíram.
    ///
    /// Só mexe em arquivo com o <see cref="Prefixo"/>: a pasta pode ser compartilhada, e
    /// uma rotação que varre tudo o que encontra é a rotação que apaga o que não é dela.
    /// </summary>
    private static int Rotacionar(string pasta, int copiasAGuardar)
    {
        try
        {
            var arquivos = new DirectoryInfo(pasta)
                .GetFiles($"{Prefixo}*.json")
                .OrderByDescending(f => f.LastWriteTime)
                .Skip(Math.Max(1, copiasAGuardar))
                .ToList();

            var apagados = 0;
            foreach (var f in arquivos)
                if (TentarApagar(f.FullName)) apagados++;

            return apagados;
        }
        catch (Exception ex)
        {
            // A cópia NOVA já está gravada — falhar a rotação não pode desfazer isso.
            Diagnostico.Registrar("Backup automático — rotação das cópias antigas falhou", ex);
            return 0;
        }
    }

    private static int ContarCopias(string? pasta)
    {
        if (string.IsNullOrWhiteSpace(pasta)) return 0;

        try
        {
            return Directory.Exists(pasta)
                ? Directory.GetFiles(pasta, $"{Prefixo}*.json").Length
                : 0;
        }
        catch (Exception ex)
        {
            Diagnostico.Registrar($"Backup automático — pasta {pasta} não pôde ser lida", ex);
            return 0;
        }
    }

    private static bool TentarApagar(string caminho)
    {
        try
        {
            if (!File.Exists(caminho)) return false;
            File.Delete(caminho);
            return true;
        }
        catch (Exception ex)
        {
            Diagnostico.Registrar($"Backup automático — {caminho} não pôde ser apagado", ex);
            return false;
        }
    }
}
