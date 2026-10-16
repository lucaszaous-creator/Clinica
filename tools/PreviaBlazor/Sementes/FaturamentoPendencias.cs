// Dados FICTÍCIOS da prévia — nenhuma pessoa real.
using Clinica.Application.Servicos;
using Clinica.Infrastructure;

namespace PreviaBlazor.Sementes;

/// <summary>
/// O painel de PENDÊNCIAS precisa mostrar a rodada VENCIDA (o banner e o "Rodar agora"): sem
/// isto a carência da primeira execução ancora em hoje e nenhuma guia da prévia vence, então a
/// prévia, as fotos e o teste que clica cada botão nunca veriam o banner. Ancora o início da
/// rodada há 90 dias — as pendências antigas da semente do faturamento passam do prazo.
/// </summary>
public sealed class SementeFaturamentoPendencias : ISemente
{
    public int Ordem => 36;

    public void Semear(ClinicaDbContext db)
    {
        var repo = new ClinicaRepositorio(db);
        new ParametrosService(repo)
            .SalvarInicioRodadaPrazoAsync(DateOnly.FromDateTime(DateTime.Today).AddDays(-90))
            .GetAwaiter().GetResult();
        db.ChangeTracker.Clear();
    }
}
