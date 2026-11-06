using Clinica.Domain.Entities;
using Clinica.Infrastructure.Tablet;
using Microsoft.AspNetCore.Components.Server.Circuits;

namespace Clinica.Gestao.Web.Hospedagem;

/// <summary>
/// Quem está NESTA conexão (Scoped = circuito do Blazor Server). A fotografia é feita uma vez,
/// na abertura; o servidor a põe como `SessaoUsuario.Atual` em cada atividade da conexão
/// (<see cref="AtividadeComSessao"/>) e a reconfere no banco a cada minuto.
/// </summary>
public sealed class SessaoDaConexao
{
    public static readonly TimeSpan IntervaloDeConferencia = TimeSpan.FromMinutes(1);

    public string? SessaoId { get; private set; }

    /// <summary>A sessão que responde por `SessaoUsuario.Atual` nesta conexão; antes do login, a que nega tudo.</summary>
    public SessaoUsuario Sessao { get; private set; } = SessaoUsuario.SemLoginNoServidor;

    public DateTimeOffset UltimaConferencia { get; private set; }

    public bool Encerrada { get; private set; }

    /// <summary>A sessão do portal deixou de valer (saiu, venceu, senha trocada, perfil retirado).</summary>
    public event Action? Encerrou;

    public void Iniciar(SessaoDoPortalNaGestao.Valida valida, DateTimeOffset agora)
    {
        SessaoId = valida.SessaoId;
        Sessao = SessaoUsuario.Fotografar(valida.Usuario);
        UltimaConferencia = agora;
    }

    public void Conferida(DateTimeOffset agora) => UltimaConferencia = agora;

    public void Encerrar()
    {
        if (Encerrada) return;
        Encerrada = true;
        Sessao = SessaoUsuario.SemLoginNoServidor;
        Encerrou?.Invoke();
    }
}

/// <summary>
/// Cada atividade da conexão (um clique, uma renderização, um retorno de JS) roda com a sessão
/// DESTA conexão em `SessaoUsuario.Atual` — é o que impede duas pessoas no mesmo servidor de
/// verem a permissão uma da outra. E, passado o intervalo, reconfere a sessão do portal.
/// </summary>
public sealed class AtividadeComSessao(SessaoDaConexao conexao, IServiceScopeFactory escopos, TimeProvider relogio)
    : CircuitHandler
{
    public override Func<CircuitInboundActivityContext, Task> CreateInboundActivityHandler(
        Func<CircuitInboundActivityContext, Task> next)
        => async contexto =>
        {
            await ReconferirSePrecisoAsync();
            using (SessaoUsuario.UsarNesteFluxo(() => conexao.Sessao))
                await next(contexto);
        };

    private async Task ReconferirSePrecisoAsync()
    {
        if (conexao.SessaoId is not { } id || conexao.Encerrada) return;
        var agora = relogio.GetUtcNow();
        if (agora - conexao.UltimaConferencia < SessaoDaConexao.IntervaloDeConferencia) return;
        using var escopo = escopos.CreateScope();
        var valida = await escopo.ServiceProvider.GetRequiredService<SessaoDoPortalNaGestao>().ReconferirAsync(id, default);
        if (valida is null) conexao.Encerrar();
        else conexao.Conferida(agora);
    }
}
