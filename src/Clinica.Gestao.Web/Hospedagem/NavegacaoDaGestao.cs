using Clinica.Ui.Blazor.Hospedagem;

namespace Clinica.Gestao.Web.Hospedagem;

/// <summary>
/// A moldura DESTA conexão, para quem precisa navegar sem conhecê-la (o hospedeiro das telas e as
/// pontes). Scoped = circuito: a navegação de uma pessoa nunca troca a tela de outra.
/// </summary>
public sealed class NavegacaoDaGestao
{
    public MolduraDaGestao? Moldura { get; set; }

    public bool Conhece(string chave) => Moldura?.Conhece(chave) ?? false;

    public bool Navegar(string chave)
    {
        if (Moldura is null || !Moldura.Conhece(chave)) return false;
        Moldura.Navegar(chave);
        return true;
    }
}

/// <summary>"Atender" e "Ver ficha": a tela do paciente, na seção pedida (como o posto do Consultório no app).</summary>
public sealed class PostoDaGestao(NavegacaoDaGestao navegacao, PedidoDaTelaDoPaciente pedido) : IPostoDoConsultorio
{
    public bool Disponivel => navegacao.Moldura is not null;

    public Task<bool> AbrirNoPostoAsync(PacienteNoPosto paciente, DestinoNoPosto destino)
    {
        var (chave, secao) = destino switch
        {
            DestinoNoPosto.Historico => ("consultorio-prontuario", "Histórico"),
            DestinoNoPosto.Paciente => ("consultorio-paciente", "Ficha do paciente"),
            _ => ("consultorio-atendimento", "Atendimento"),
        };
        if (!navegacao.Conhece(chave)) return Task.FromResult(false);
        pedido.Definir(paciente, secao);
        return Task.FromResult(navegacao.Navegar(chave));
    }

    public IDisposable? EscutarAgenda(Func<Task> reler) => null;
}

/// <summary>"Marcar atendimento" da agenda: a aba de marcação.</summary>
public sealed class JanelasDaAgendaDaGestao(NavegacaoDaGestao navegacao) : IJanelasDaAgenda
{
    public bool Disponivel => navegacao.Conhece("marcar-horario");
    public Task<bool> MarcarNovoHorarioAsync(DateOnly dia, int? profissionalId) => Task.FromResult(navegacao.Navegar("marcar-horario"));
}

/// <summary>Marcar um horário para um paciente (retornos, acompanhamento): a aba de marcação.</summary>
public sealed class MarcacaoDaGestao(NavegacaoDaGestao navegacao) : IMarcacaoDeHorario
{
    public Task<bool> MarcarAsync(int pacienteId, int? profissionalId, string? modalidadeCodigo)
        => Task.FromResult(navegacao.Navegar("marcar-horario"));
}
