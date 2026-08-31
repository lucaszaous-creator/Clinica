namespace Clinica.Desktop.Shell.Modulos;

/// <summary>
/// O paciente que a PESQUISA GLOBAL mandou abrir — um pedido só, consumido uma vez.
///
/// Por que existe
/// --------------
/// A paleta do Ctrl+F passou a achar PACIENTE, e não só tela. Quem acha está no SHELL;
/// quem sabe abrir uma ficha está no MÓDULO — e nenhum dos dois pode referenciar o outro.
/// A navegação da suíte atravessa essa fronteira por CHAVE
/// (<see cref="NavegacaoSuite.Ir(string)"/>), e chave não carrega parâmetro: ela diz para
/// onde ir, não sobre quem.
///
/// Este é o mesmo padrão do <c>PreenchimentoNovoAtendimento</c> (parcela 70): um pedido
/// pendente, definido por quem navega e CONSUMIDO por quem chega — consumir LIMPA, senão
/// um pedido órfão abriria, amanhã, a ficha que alguém procurou hoje e desistiu de abrir.
///
/// ⚠️ Ele não substitui o <see cref="PacienteEmFoco"/> e não se confunde com ele: aquele
/// responde "quem está sendo atendido AGORA neste posto" e vive enquanto o atendimento
/// durar; este é um bilhete de uma viagem só, que deixa de existir na chegada.
/// </summary>
public sealed class FichaPedida
{
    private (int Id, string Nome)? _pedido;

    /// <summary>Marca o paciente a abrir na próxima tela de pacientes que montar.</summary>
    public void Definir(int pacienteId, string nome) => _pedido = (pacienteId, nome);

    /// <summary>Lê e LIMPA o pedido. <c>null</c> = ninguém pediu nada.</summary>
    public (int Id, string Nome)? Consumir()
    {
        var pedido = _pedido;
        _pedido = null;
        return pedido;
    }
}
