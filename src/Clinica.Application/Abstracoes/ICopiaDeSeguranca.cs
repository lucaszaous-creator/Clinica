namespace Clinica.Application.Abstracoes;

/// <summary>
/// A cópia de segurança da base, como a tela de Configurações a usa (out/2026, migração
/// para Blazor). O motor continua no Infrastructure (<c>BackupService</c> e
/// <c>PoliticaBackupService</c>, que leem o modelo do EF); esta é a PORTA que a tela
/// alcança — a camada de tela só enxerga a Application, e um segundo caminho de "como se
/// copia a base" divergiria na primeira correção.
///
/// ⚠️ O que já existia não muda: a cópia automática da abertura do Gerente continua chamando
/// o <c>PoliticaBackupService</c> direto, e o "Copiar agora" passa pelo MESMO
/// <c>ExecutarAsync</c> dele — o manual e o automático não podem divergir no que produzem
/// nem em como rotacionam.
/// </summary>
public interface ICopiaDeSeguranca
{
    /// <summary>A política gravada e a frase pronta sobre ela (a tela não reconta).</summary>
    Task<SituacaoDaCopia> SituacaoAsync(CancellationToken ct = default);

    /// <summary>Grava uma cópia AGORA na pasta, com rotação — o caminho do automático.</summary>
    Task<ResultadoDaCopiaAgora> CopiarAgoraAsync(string pasta, int copiasAGuardar, CancellationToken ct = default);

    /// <summary>Gera o backup completo no fluxo indicado (o "Salvar cópia em…").</summary>
    Task<ResumoDoBackup> GerarAsync(Stream destino, CancellationToken ct = default);

    /// <summary>
    /// Lê o arquivo SEM restaurar nada. Arquivo que não é backup deste sistema lança
    /// <see cref="InvalidOperationException"/> com a frase para a tela — escolher o arquivo
    /// errado é engano de quem escolheu, não defeito do sistema.
    /// </summary>
    Task<ResumoDoBackup> ConferirAsync(Stream origem, CancellationToken ct = default);

    /// <summary>
    /// A pasta existe e é alcançável DESTA máquina agora? A janela de pasta do Windows só
    /// devolve pasta que existe; o hospedeiro de teste e a prévia devolvem um caminho
    /// fictício, e gravá-lo como destino faria a cópia automática falhar toda abertura.
    /// </summary>
    bool PastaAcessivel(string pasta);
}

/// <summary>A política de cópia como está gravada.</summary>
/// <param name="Pasta">Destino escolhido; null = a cópia automática está desligada.</param>
/// <param name="Descricao">A frase da situação (nunca houve / atrasada / em dia), do serviço dono.</param>
public sealed record SituacaoDaCopia(
    string? Pasta, int IntervaloDias, int CopiasAGuardar, bool Configurada, string Descricao);

/// <summary>O que um arquivo de backup contém, sem abri-lo inteiro.</summary>
/// <param name="Vazio">Sem nenhuma linha DA CLÍNICA (os catálogos semeados não contam).</param>
public sealed record ResumoDoBackup(
    DateTime GeradoEm, int TotalTabelas, int TotalLinhas, bool Vazio,
    int Pacientes, int Evolucoes, int Lancamentos);

/// <summary>O que o "Copiar agora" fez. <see cref="Erro"/> preenchido = falhou, e a frase diz por quê.</summary>
public sealed record ResultadoDaCopiaAgora(
    string? Caminho, ResumoDoBackup? Resumo, int CopiasApagadas, string? Erro)
{
    public bool Falhou => Erro is not null;
}
