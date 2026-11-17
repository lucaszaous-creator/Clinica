using Clinica.Infrastructure;

namespace Clinica.Gestao.Web.Hospedagem;

/// <summary>
/// OS CATÁLOGOS EM MEMÓRIA DO PORTAL (nov/2026): convênios, modalidades e especialidades.
///
/// As telas leem <c>CatalogoModalidades.Ativas</c> e companhia de forma SÍNCRONA; nos apps quem
/// enche esse cache é a abertura (<see cref="AberturaDoBanco.PrepararAsync"/>). O portal não passa
/// por ela (não migra o banco), e ficou sem carga nenhuma: no Lançar/Marcar, escolher quem atende
/// respondia "Nenhuma modalidade ativa que este profissional atenda" — a lista vazia era o cache
/// vazio, não o cadastro da clínica. O mesmo valia para o nome dos convênios personalizados.
///
/// E, diferente do app (que abre todo dia), este processo vive semanas: o que a clínica muda em
/// Configurações num dos apps precisa chegar aqui sem reinício — por isso a releitura periódica.
/// </summary>
public sealed class CatalogosDaGestao(IServiceProvider servicos, ILogger<CatalogosDaGestao> log) : BackgroundService
{
    /// <summary>De quanto em quanto o portal relê o que a clínica mudou nos apps.</summary>
    public static readonly TimeSpan Intervalo = TimeSpan.FromMinutes(2);

    /// <summary>Enquanto a leitura falha (banco fora do ar na subida), tenta de novo logo.</summary>
    public static readonly TimeSpan IntervaloNaFalha = TimeSpan.FromSeconds(5);

    /// <summary>Relê os três catálogos. Falso quando o banco não respondeu — o cache fica como estava.</summary>
    public async Task<bool> RecarregarAsync(CancellationToken ct = default)
    {
        try
        {
            await AberturaDoBanco.RecarregarCatalogosAsync(servicos, ct);
            return true;
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
        {
            // Só o TIPO: a mensagem de uma exceção de banco pode carregar dado (regra do Program.cs).
            log.LogWarning("Catálogos da gestão não puderam ser lidos ({Tipo}); nova tentativa em instantes.", ex.GetType().Name);
            return false;
        }
    }

    /// <summary>A primeira carga acontece ANTES de o portal aceitar a primeira conexão.</summary>
    public override async Task StartAsync(CancellationToken cancellationToken)
    {
        _carregado = await RecarregarAsync(cancellationToken);
        await base.StartAsync(cancellationToken);
    }

    private bool _carregado;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                await Task.Delay(_carregado ? Intervalo : IntervaloNaFalha, stoppingToken);
                // Uma releitura que falha depois de uma boa não desfaz o que já está carregado.
                _carregado = await RecarregarAsync(stoppingToken) || _carregado;
            }
        }
        catch (OperationCanceledException) { }
    }
}
