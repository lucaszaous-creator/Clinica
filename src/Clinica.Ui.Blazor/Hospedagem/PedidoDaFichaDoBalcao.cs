namespace Clinica.Ui.Blazor.Hospedagem;

/// <summary>
/// "Mostre a ficha do balcão DESTE paciente" — o pedido que a agenda do dia, a lista de
/// pacientes e o Início deixam para a tela <c>FichaDoBalcao</c> (out/2026: a ficha WPF que abria
/// numa janela por cima — <c>FichaBalcaoView</c> numa <c>ConsultaContextualWindow</c> — virou
/// tela Blazor no MESMO navegador da janela, como a tela do paciente).
///
/// O desenho é o do <see cref="PedidoDaTelaDoPaciente"/>: o palco guarda UMA instância da tela,
/// então ela compara a <see cref="Versao"/> na abertura e a cada volta e só troca de paciente
/// quando o pedido MUDOU. Ler NÃO limpa: voltar à ficha devolve a mesma pessoa.
/// </summary>
public sealed class PedidoDaFichaDoBalcao
{
    /// <summary>A chave da tela no menu (oculta — só existe com um paciente escolhido).</summary>
    public const string Chave = "ficha-balcao";

    private readonly object _trava = new();
    private Pedido? _atual;
    private int _versao;

    /// <summary>O paciente e a aba de dentro da ficha.</summary>
    public sealed record Pedido(int PacienteId, string Paciente, int Aba);

    /// <summary>Muda a cada <see cref="Definir"/> de um pedido diferente. Zero = nunca houve pedido.</summary>
    public int Versao
    {
        get { lock (_trava) return _versao; }
    }

    public void Definir(int pacienteId, string paciente, int aba = 0)
    {
        lock (_trava)
        {
            // O MESMO pedido não muda a versão: abrir de novo a ficha que já está aberta não pode
            // recarregar tudo nem jogar fora a aba em que a pessoa estava.
            var novo = new Pedido(pacienteId, paciente, aba);
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

    /// <summary>
    /// A porta ÚNICA para abrir a ficha do balcão: define o pedido e navega. Onde a ficha do balcão
    /// não existe neste executável (ou para este acesso), cai na ficha do HOST — a tela do paciente
    /// —, e falso só quando nenhuma das duas abriu: quem chama DIZ isso. A barreira
    /// (<c>VerFichaPaciente</c>) é de quem chama, como toda segunda barreira.
    /// </summary>
    public static async Task<bool> AbrirAsync(IServiceProvider servicos, IHostDaTela host, int pacienteId, string paciente)
    {
        if (servicos.GetService(typeof(PedidoDaFichaDoBalcao)) is PedidoDaFichaDoBalcao pedido)
        {
            pedido.Definir(pacienteId, paciente);
            if (await host.NavegarAsync(Chave)) return true;
        }
        return await host.AbrirFichaDoPacienteAsync(pacienteId);
    }
}
