namespace Clinica.Ui.Blazor.Hospedagem;

/// <summary>
/// "Mostre ESTE paciente, nesta seção" — o pedido que a navegação do posto deixa para a tela
/// do paciente (out/2026, P0 da migração da tela do paciente para Blazor).
///
/// O palco guarda UMA instância por tipo de tela: a tela do paciente não é recriada a cada
/// paciente, como a ViewModel WPF era. Por isso ela precisa de um jeito de saber que o pedido
/// MUDOU desde a última vez que olhou — e é para isso a <see cref="Versao"/>: a tela compara a
/// versão na abertura e a cada volta, e só troca de paciente (ou de seção) quando ela mudou.
///
/// ⚠️ Diferente do <see cref="PedidoDaAgendaDoDia"/>, ler NÃO limpa: a tela do paciente é o
/// prontuário aberto, e voltar a ela tem de devolver a MESMA pessoa — um pedido consumido
/// deixaria a volta sem paciente. Quem muda o pedido é só quem abre outro paciente.
/// </summary>
public sealed class PedidoDaTelaDoPaciente
{
    private readonly object _trava = new();
    private Pedido? _atual;
    private int _versao;

    /// <summary>O paciente (com o horário de onde veio), a seção e a aba de dentro dela.</summary>
    public sealed record Pedido(PacienteNoPosto Paciente, string Secao, int SubAba);

    /// <summary>Muda a cada <see cref="Definir"/>. Zero = nunca houve pedido.</summary>
    public int Versao
    {
        get { lock (_trava) return _versao; }
    }

    public Pedido? Atual
    {
        get { lock (_trava) return _atual; }
    }

    public void Definir(PacienteNoPosto paciente, string secao, int subAba = 0)
    {
        lock (_trava)
        {
            // O MESMO pedido não muda a versão: navegar de novo para o paciente que já está
            // aberto (o shell recria o marcador a cada navegação) não pode remontar as seções —
            // a folha em edição perderia o que está sendo escrito.
            var novo = new Pedido(paciente, secao, subAba);
            if (novo == _atual) return;
            _atual = novo;
            _versao++;
        }
    }

    /// <summary>O pedido e a versão lidos juntos (sem uma escrita no meio).</summary>
    public (Pedido? Pedido, int Versao) Ler()
    {
        lock (_trava) return (_atual, _versao);
    }
}
