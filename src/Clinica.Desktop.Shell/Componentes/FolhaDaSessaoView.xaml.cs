using System.Windows;
using System.Windows.Controls;

namespace Clinica.Desktop.Shell.Componentes;

/// <summary>
/// A folha da sessão, com os dois pontos em que as portas diferem.
///
/// ⚠️ As duas são propriedades de DEPENDÊNCIA e não campos do ViewModel: o que muda entre
/// a tela de Atendimento e a janela do Prontuário é LEIAUTE (que botões existem, que frase
/// vai acima da folha), e pôr isso no ViewModel obrigaria a base a conhecer as portas —
/// exatamente o acoplamento que a extração existe para desfazer.
/// </summary>
public partial class FolhaDaSessaoView : UserControl
{
    public FolhaDaSessaoView() => InitializeComponent();

    /// <summary>
    /// O que só ESTA porta faz, na ponta direita da tira: no Consultório, "Colher termo…"
    /// e "Emitir documento". O conteúdo herda o DataContext da folha, então os bindings
    /// dele resolvem contra o ViewModel da porta.
    /// </summary>
    public static readonly DependencyProperty FerramentasProperty =
        DependencyProperty.Register(nameof(Ferramentas), typeof(object), typeof(FolhaDaSessaoView),
            new PropertyMetadata(null));

    public object? Ferramentas
    {
        get => GetValue(FerramentasProperty);
        set => SetValue(FerramentasProperty, value);
    }

    /// <summary>
    /// A linha acima da folha. Vazia, ela SOME: vão em branco no topo de uma tela de
    /// escrita é o que a composição centrada ensinou a não deixar (parcela 57).
    /// </summary>
    public static readonly DependencyProperty ContextoProperty =
        DependencyProperty.Register(nameof(Contexto), typeof(string), typeof(FolhaDaSessaoView),
            new PropertyMetadata(string.Empty));

    public string? Contexto
    {
        get => (string?)GetValue(ContextoProperty);
        set => SetValue(ContextoProperty, value);
    }
}
