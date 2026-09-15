using CommunityToolkit.Mvvm.ComponentModel;

namespace Clinica.Desktop.Shell.Componentes;

/// <summary>Uma sugestão escolhida pelo profissional para acrescentar ao texto em edição.</summary>
public sealed partial class SugestaoTextoClinico : ObservableObject
{
    private readonly Action<string> _acrescentar;
    public string Nome { get; }
    public string Texto { get; }
    [ObservableProperty] private bool _selecionada;

    public SugestaoTextoClinico(string nome, string texto, Action<string> acrescentar)
        => (Nome, Texto, _acrescentar) = (nome, texto, acrescentar);

    partial void OnSelecionadaChanged(bool value)
    {
        // Desmarcar não pode apagar um trecho que o profissional já tenha modificado.
        if (value) _acrescentar(Texto);
    }
}
