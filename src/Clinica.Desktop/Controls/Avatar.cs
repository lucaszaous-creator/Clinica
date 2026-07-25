using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Clinica.Desktop.Servicos;

namespace Clinica.Desktop.Controls;

/// <summary>
/// Retrato circular do paciente. Recebe os bytes da foto (miniatura ou foto cheia) e,
/// quando não há foto, cai nas iniciais do nome — a lista nunca fica com buracos.
/// Template em Styles/Componentes/Midia.xaml.
/// </summary>
public class Avatar : Control
{
    static Avatar()
    {
        DefaultStyleKeyProperty.OverrideMetadata(typeof(Avatar),
            new FrameworkPropertyMetadata(typeof(Avatar)));
    }

    public static readonly DependencyProperty NomeProperty =
        DependencyProperty.Register(nameof(Nome), typeof(string), typeof(Avatar),
            new PropertyMetadata(string.Empty, (d, _) => ((Avatar)d).AtualizarIniciais()));

    /// <summary>Nome do paciente — origem das iniciais quando não há foto.</summary>
    public string? Nome
    {
        get => (string?)GetValue(NomeProperty);
        set => SetValue(NomeProperty, value);
    }

    public static readonly DependencyProperty FotoProperty =
        DependencyProperty.Register(nameof(Foto), typeof(byte[]), typeof(Avatar),
            new PropertyMetadata(null, (d, _) => ((Avatar)d).AtualizarImagem()));

    /// <summary>Bytes da foto (JPEG). Null ou vazio = mostra as iniciais.</summary>
    public byte[]? Foto
    {
        get => (byte[]?)GetValue(FotoProperty);
        set => SetValue(FotoProperty, value);
    }

    public static readonly DependencyProperty TamanhoProperty =
        DependencyProperty.Register(nameof(Tamanho), typeof(double), typeof(Avatar),
            new PropertyMetadata(40d, (d, _) => ((Avatar)d).AtualizarTamanhoTexto()));

    /// <summary>Diâmetro do avatar em pixels independentes de dispositivo.</summary>
    public double Tamanho
    {
        get => (double)GetValue(TamanhoProperty);
        set => SetValue(TamanhoProperty, value);
    }

    private static readonly DependencyPropertyKey IniciaisKey =
        DependencyProperty.RegisterReadOnly(nameof(Iniciais), typeof(string), typeof(Avatar),
            new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty IniciaisProperty = IniciaisKey.DependencyProperty;

    /// <summary>Até duas letras do nome (primeiro + último sobrenome).</summary>
    public string Iniciais => (string)GetValue(IniciaisProperty);

    private static readonly DependencyPropertyKey ImagemKey =
        DependencyProperty.RegisterReadOnly(nameof(Imagem), typeof(ImageSource), typeof(Avatar),
            new PropertyMetadata(null));

    public static readonly DependencyProperty ImagemProperty = ImagemKey.DependencyProperty;

    /// <summary>Foto decodificada para o template. Null quando não há retrato.</summary>
    public ImageSource? Imagem => (ImageSource?)GetValue(ImagemProperty);

    private static readonly DependencyPropertyKey TemFotoKey =
        DependencyProperty.RegisterReadOnly(nameof(TemFoto), typeof(bool), typeof(Avatar),
            new PropertyMetadata(false));

    public static readonly DependencyProperty TemFotoProperty = TemFotoKey.DependencyProperty;

    /// <summary>Gatilho do template: true mostra a foto, false mostra as iniciais.</summary>
    public bool TemFoto => (bool)GetValue(TemFotoProperty);

    private static readonly DependencyPropertyKey TamanhoTextoKey =
        DependencyProperty.RegisterReadOnly(nameof(TamanhoTexto), typeof(double), typeof(Avatar),
            new PropertyMetadata(16d));

    public static readonly DependencyProperty TamanhoTextoProperty = TamanhoTextoKey.DependencyProperty;

    /// <summary>Tamanho das iniciais, proporcional ao diâmetro.</summary>
    public double TamanhoTexto => (double)GetValue(TamanhoTextoProperty);

    private void AtualizarImagem()
    {
        var imagem = Retrato.Carregar(Foto);
        SetValue(ImagemKey, imagem);
        SetValue(TemFotoKey, imagem is not null);
    }

    private void AtualizarTamanhoTexto()
        => SetValue(TamanhoTextoKey, Math.Max(10d, Math.Round(Tamanho * 0.36)));

    private void AtualizarIniciais() => SetValue(IniciaisKey, Calcular(Nome));

    /// <summary>"Maria da Silva" → "MS"; nome de uma palavra só → primeira letra.</summary>
    public static string Calcular(string? nome)
    {
        if (string.IsNullOrWhiteSpace(nome)) return "?";

        var partes = nome
            .Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Where(p => p.Length > 1 || char.IsLetter(p[0]))
            .ToArray();
        if (partes.Length == 0) return "?";
        if (partes.Length == 1) return partes[0][..1].ToUpperInvariant();

        return (partes[0][..1] + partes[^1][..1]).ToUpperInvariant();
    }
}
