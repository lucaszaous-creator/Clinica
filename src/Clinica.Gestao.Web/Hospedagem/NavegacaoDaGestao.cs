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
public sealed class PostoDaGestao(NavegacaoDaGestao navegacao, PedidoDaTelaDoPaciente pedido, IAvisosDoBanco avisos) : IPostoDoConsultorio
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

    /// <summary>
    /// A agenda mudou noutra máquina: relê (agrupando a rajada — um horário remarcado dispara
    /// mais de um aviso). Complemento do relógio da tela, nunca substituto.
    /// </summary>
    public IDisposable? EscutarAgenda(Func<Task> reler) => new EscutaDaAgenda(avisos, reler);

    private sealed class EscutaDaAgenda : IDisposable
    {
        private static readonly TimeSpan Pausa = TimeSpan.FromMilliseconds(400);
        private readonly IAvisosDoBanco _avisos;
        private readonly Func<Task> _reler;
        private int _geracao;
        private volatile bool _desligada;

        public EscutaDaAgenda(IAvisosDoBanco avisos, Func<Task> reler)
        {
            _avisos = avisos;
            _reler = reler;
            _avisos.Mudou += AoMudar;
        }

        private void AoMudar(string tabela)
        {
            if (_desligada || (tabela != "*" && tabela != "Agendamentos")) return;
            _ = RelerDepoisAsync(Interlocked.Increment(ref _geracao));
        }

        private async Task RelerDepoisAsync(int geracao)
        {
            try
            {
                await Task.Delay(Pausa);
                if (_desligada || geracao != Volatile.Read(ref _geracao)) return;
                await _reler();
            }
            catch (Exception ex)
            {
                Clinica.Application.Diagnostico.Registrar("Gestão web — releitura da agenda por aviso do banco", ex);
            }
        }

        public void Dispose()
        {
            _desligada = true;
            _avisos.Mudou -= AoMudar;
        }
    }
}
