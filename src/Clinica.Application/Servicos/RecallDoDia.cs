namespace Clinica.Application.Servicos;

/// <summary>
/// O DIA em que a fila do recall foi renovada neste app (item 8 da revisão do WhatsApp, 25/10). A renovação é o
/// <c>IAcompanhamentoPacienteService.GerarRecallAsync</c> com os dias do Gerente, e ela não duplica caso — então
/// renovar duas vezes não estraga nada, só custa uma leitura de todos os atendimentos. Esta marca faz a aba WhatsApp
/// (que relê a Retenção a cada três minutos) e a tela Acompanhamento renovarem no máximo UMA vez por dia, dividindo a
/// mesma resposta. Singleton: é do processo, como a sessão do app; outro posto renova o dele, sem dano.
/// </summary>
public sealed class RecallDoDia
{
    private readonly object _trava = new();
    private DateOnly? _dia;

    /// <summary>Verdadeiro uma vez por dia: quem recebe renova. Os outros (no mesmo dia) não precisam.</summary>
    public bool Reservar(DateOnly hoje)
    {
        lock (_trava)
        {
            if (_dia == hoje) return false;
            _dia = hoje;
            return true;
        }
    }

    /// <summary>A renovação falhou: o dia volta a estar livre e a próxima leitura tenta de novo.</summary>
    public void Liberar(DateOnly hoje)
    {
        lock (_trava)
        {
            if (_dia == hoje) _dia = null;
        }
    }
}
