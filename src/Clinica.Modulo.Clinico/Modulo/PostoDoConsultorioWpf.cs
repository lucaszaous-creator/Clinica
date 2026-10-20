using Clinica.Desktop.Shell.Modulos;
using Clinica.Ui.Blazor.Hospedagem;

namespace Clinica.Clinico.Modulo;

/// <summary>
/// A ponte das telas Blazor do posto (Meu dia, Minha semana, Sessões sem evolução, Meus
/// números — out/2026) até o PRONTUÁRIO, que continua WPF. Faz EXATAMENTE o que os ViewModels
/// WPF faziam ao abrir o paciente: <c>PacienteEmFoco.Definir</c> com o paciente, o horário, o
/// atendimento e o DIA do horário, e <c>NavegacaoSuite.Ir</c> para a seção certa — a do "Atender"
/// bifurcada por <see cref="PostoClinico.ChaveDoAtendimento"/> (quem consulta cai no S-O-A-P,
/// quem executa na passagem de enfermagem).
///
/// A barreira de permissão e o carimbo de entrada são da tela que chama; aqui só se fixa o foco
/// e se navega.
///
/// ⚠️ Tudo roda no despachante do WPF: o foco é lido pelas telas WPF e a navegação mexe no
/// shell, e a tela Blazor pode chamar de outra thread (o mesmo cuidado de
/// <c>JanelasDaAgendaWpf</c> da Recepção).
/// </summary>
public sealed class PostoDoConsultorioWpf(PacienteEmFoco foco) : IPostoDoConsultorio
{
    public bool Disponivel => true;

    public PacienteNoPosto? PacienteEmFoco => foco.PacienteId is { } id
        ? new PacienteNoPosto(id, foco.Nome, foco.AgendamentoId, null, foco.DataDoHorario)
        : null;

    public Task<bool> AbrirNoPostoAsync(PacienteNoPosto paciente, DestinoNoPosto destino) => NaInterface(() =>
    {
        foco.Definir(paciente.PacienteId, paciente.Paciente, paciente.AgendamentoId,
                     paciente.AtendimentoId, paciente.DataDoHorario);
        var chave = destino switch
        {
            DestinoNoPosto.Atendimento => PostoClinico.ChaveDoAtendimento(),
            DestinoNoPosto.EvolucaoMedica => ModuloClinico.ChaveAtendimento,
            DestinoNoPosto.Historico => ModuloClinico.ChaveProntuario,
            _ => ModuloClinico.ChavePaciente
        };
        return Task.FromResult(NavegacaoSuite.Ir(chave));
    });

    public IDisposable? EscutarAgenda(Func<Task> reler)
    {
        // A EscutaDoBanco agrupa os avisos num DispatcherTimer: ela nasce na thread da
        // interface, senão o relógio dela ficaria preso a um despachante que nunca roda.
        var despachante = System.Windows.Application.Current?.Dispatcher;
        EscutaDoBanco Criar()
        {
            var escuta = new EscutaDoBanco(reler, "Agendamentos");
            escuta.Ligar();
            return escuta;
        }
        var criada = despachante is null || despachante.CheckAccess() ? Criar() : despachante.Invoke(Criar);
        return new Assinatura(criada, despachante);
    }

    private sealed class Assinatura(EscutaDoBanco escuta, System.Windows.Threading.Dispatcher? despachante) : IDisposable
    {
        public void Dispose()
        {
            if (despachante is null || despachante.CheckAccess()) escuta.Desligar();
            else despachante.Invoke(escuta.Desligar);
        }
    }

    private static Task<T> NaInterface<T>(Func<Task<T>> acao)
    {
        var despachante = System.Windows.Application.Current?.Dispatcher;
        if (despachante is null || despachante.CheckAccess()) return acao();
        return despachante.InvokeAsync(acao).Task.Unwrap();
    }
}
