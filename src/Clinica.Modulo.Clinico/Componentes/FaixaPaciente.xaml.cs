using System.Windows;
using System.Windows.Controls;

namespace Clinica.Clinico.Componentes;

/// <summary>
/// A faixa de identificação do paciente no topo do conteúdo das telas clínicas.
///
/// Por que existe
/// --------------
/// As telas mostravam o nome do paciente como um <c>H2</c> solto sobre o fundo da página.
/// Funciona para dizer QUEM é, e não para dizer que dali para baixo tudo pertence a essa
/// pessoa — que é a diferença entre uma tela de consulta e um prontuário. Num app onde o
/// profissional passa vinte minutos entre quatro abas sobre a MESMA pessoa, a identidade
/// precisa ser âncora visual: avatar, nome grande, contexto embaixo, ações da tela à
/// direita.
///
/// <see cref="Acoes"/> é um <c>object</c> (e não um painel fixo) porque cada tela tem as
/// suas: exportar a curva, emitir documento, registrar medida. Colocá-las no cabeçalho da
/// PÁGINA, como estavam, deixava botão aceso sobre uma tela sem paciente nenhum.
/// </summary>
public partial class FaixaPaciente : UserControl
{
    public FaixaPaciente() => InitializeComponent();

    public static readonly DependencyProperty NomeProperty =
        DependencyProperty.Register(nameof(Nome), typeof(string), typeof(FaixaPaciente),
            new PropertyMetadata(string.Empty));

    public string? Nome
    {
        get => (string?)GetValue(NomeProperty);
        set => SetValue(NomeProperty, value);
    }

    public static readonly DependencyProperty ContextoProperty =
        DependencyProperty.Register(nameof(Contexto), typeof(string), typeof(FaixaPaciente),
            new PropertyMetadata(string.Empty));

    /// <summary>
    /// A linha de baixo: de onde a sessão veio, quantas já houve, o que a curva diz. Cada
    /// tela escreve a sua — é o contexto que ELA usa, e um texto genérico ("paciente
    /// selecionado") não valeria o espaço.
    /// </summary>
    public string? Contexto
    {
        get => (string?)GetValue(ContextoProperty);
        set => SetValue(ContextoProperty, value);
    }

    public static readonly DependencyProperty AcoesProperty =
        DependencyProperty.Register(nameof(Acoes), typeof(object), typeof(FaixaPaciente),
            new PropertyMetadata(null));

    /// <summary>Botões da tela, à direita da identificação.</summary>
    public object? Acoes
    {
        get => GetValue(AcoesProperty);
        set => SetValue(AcoesProperty, value);
    }
}
