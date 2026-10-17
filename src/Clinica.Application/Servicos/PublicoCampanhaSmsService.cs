using Clinica.Application.Abstracoes;
using Clinica.Application.Modelos;

namespace Clinica.Application.Servicos;

/// <summary>
/// Lê o que o recorte do público da campanha de SMS precisa (out/2026): as fichas (projeção
/// de nome, telefone e convênio — nunca a ficha inteira) e os atendimentos do período pela
/// MESMA base da tendência. SEQUENCIAL no mesmo repositório (parcela 74). A decisão de quem
/// entra é de <see cref="PublicoCampanhaSms.Montar"/>, pura.
///
/// Não precisa de registro no DI: a tela o monta com <c>ActivatorUtilities</c> sobre o
/// escopo da operação — os dois serviços que ele usa já estão lá.
/// </summary>
public sealed class PublicoCampanhaSmsService
{
    private readonly IClinicaRepositorio _repo;
    private readonly TendenciaAtendimentosService _tendencia;

    public PublicoCampanhaSmsService(IClinicaRepositorio repo, TendenciaAtendimentosService tendencia)
    {
        _repo = repo;
        _tendencia = tendencia;
    }

    public async Task<PublicoSms> MontarAsync(FiltroPublicoSms filtro, CancellationToken ct = default)
    {
        var (inicio, fim) = filtro.Fim < filtro.Inicio ? (filtro.Fim, filtro.Inicio) : (filtro.Inicio, filtro.Fim);
        var fichas = await _repo.PacientesParaContatoAsync(null, ct);
        var fatos = await _tendencia.MontarFatosAsync(MetricaTendencia.Sessoes, inicio, fim, ct);
        return PublicoCampanhaSms.Montar(fichas, fatos, filtro with { Inicio = inicio, Fim = fim });
    }
}
