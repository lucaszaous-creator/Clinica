using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Clinica.Desktop.Shell.Componentes;

/// <summary>Uma declaração como o PACIENTE a lê, com a resposta que foi marcada.</summary>
public sealed partial class DeclaracaoNaTelaDoPaciente : ObservableObject
{
    public required int Ordem { get; init; }

    public required string Descricao { get; init; }

    /// <summary>
    /// "Sim", "Não" ou um traço enquanto ninguém respondeu.
    ///
    /// O traço é deliberado: em branco, a linha pareceria uma declaração que o sistema
    /// esqueceu de mostrar, e o paciente assinaria um documento com um item pela metade sem
    /// perceber. O traço diz "isto ainda vai ser respondido".
    /// </summary>
    [ObservableProperty] private string _resposta = "—";
}

/// <summary>
/// O que a tela do paciente mostra (parcela 66, 5ª rodada).
///
/// É um espelho VIVO da janela da recepcionista: ela marca "Não" no jejum e a linha muda na
/// frente do paciente, no mesmo instante. Sem isso, ele assinaria um texto e a clínica
/// gravaria outro — e a evidência que o termo produz (o SHA-256 do que ele tinha na frente)
/// passaria a afirmar algo que não é verdade.
/// </summary>
public sealed partial class PainelDoPacienteViewModel : ObservableObject
{
    [ObservableProperty] private string _titulo = string.Empty;

    [ObservableProperty] private string _pacienteNome = string.Empty;

    [ObservableProperty] private string _corpo = string.Empty;

    public ObservableCollection<DeclaracaoNaTelaDoPaciente> Declaracoes { get; } = [];
}
