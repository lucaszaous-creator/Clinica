using Clinica.Ui.Blazor.Hospedagem;

namespace PreviaBlazor;

// AS PONTES DA AULA (out/2026). No app, algumas telas abrem OUTRAS pelo hospedeiro — "Atender"
// leva à tela do paciente, "Marcar atendimento" à aba de marcação. Na prévia essas pontes
// respondiam "indisponível", e o vídeo da aula mostraria um aviso no lugar do caminho que a
// pessoa vai percorrer. Dentro da página da aula elas navegam pela moldura, como no app; fora
// dela (/tela, testes) continuam indisponíveis, como sempre foram.

/// <summary>Abrir o paciente no posto do Consultório: a tela do paciente, na seção pedida.</summary>
public sealed class PostoDaAula(AulaAtual aula, PedidoDaTelaDoPaciente pedido) : IPostoDoConsultorio
{
    public bool Disponivel => aula.Moldura is not null;

    public Task<bool> AbrirNoPostoAsync(PacienteNoPosto paciente, DestinoNoPosto destino)
    {
        var (chave, secao) = destino switch
        {
            DestinoNoPosto.Historico => ("consultorio-prontuario", "Histórico"),
            DestinoNoPosto.Paciente => ("consultorio-paciente", "Ficha do paciente"),
            _ => ("consultorio-atendimento", "Atendimento"),
        };
        if (aula.Moldura is null || !aula.Moldura.Conhece(chave)) return Task.FromResult(false);
        pedido.Definir(paciente, secao);
        return Task.FromResult(aula.Navegar(chave));
    }

    public IDisposable? EscutarAgenda(Func<Task> reler) => null;
}

/// <summary>"Marcar atendimento" da agenda: a aba de marcação.</summary>
public sealed class JanelasDaAgendaDaAula(AulaAtual aula) : IJanelasDaAgenda
{
    public bool Disponivel => aula.Moldura is not null;
    public Task<bool> MarcarNovoHorarioAsync(DateOnly dia, int? profissionalId) => Task.FromResult(aula.Navegar("marcar-horario"));
}

/// <summary>Marcar um horário para um paciente (retornos, acompanhamento): a aba de marcação.</summary>
public sealed class MarcacaoDaAula(AulaAtual aula) : IMarcacaoDeHorario
{
    public Task<bool> MarcarAsync(int pacienteId, int? profissionalId, string? modalidadeCodigo) => Task.FromResult(aula.Navegar("marcar-horario"));
}
