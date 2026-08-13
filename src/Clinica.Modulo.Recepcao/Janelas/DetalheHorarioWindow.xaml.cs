using System.Windows;
using Clinica.Recepcao.ViewModels;

namespace Clinica.Recepcao.Janelas;

/// <summary>O que a recepção escolheu fazer com o horário aberto.</summary>
public enum AcaoHorario
{
    Nenhuma,
    Remarcar,
    Confirmar,
    Falta,
    Cancelar,
    QuemChamar,
    Comprovante,
    CancelarSerie
}

/// <summary>
/// O horário aberto: tudo o que ele é, e tudo o que dá para fazer com ele.
///
/// Ela NÃO executa nada — devolve a <see cref="Acao"/> e quem age é a
/// <c>AgendaViewModel</c>. É de propósito: as sete ações já têm um dono, com a guarda de
/// permissão, o recarregamento da grade e o tratamento de erro no mesmo lugar. Repetir a
/// chamada aqui daria duas cópias que divergem na primeira correção — e a segunda seria a
/// que ninguém lembra de ajustar.
/// </summary>
public partial class DetalheHorarioWindow : Window
{
    public AcaoHorario Acao { get; private set; } = AcaoHorario.Nenhuma;

    public DetalheHorarioWindow(CartaoAgenda cartao)
    {
        InitializeComponent();
        DataContext = cartao;
    }

    private void Escolher(AcaoHorario acao)
    {
        Acao = acao;
        DialogResult = true;
    }

    private void AoRemarcar(object sender, RoutedEventArgs e) => Escolher(AcaoHorario.Remarcar);
    private void AoConfirmar(object sender, RoutedEventArgs e) => Escolher(AcaoHorario.Confirmar);
    private void AoMarcarFalta(object sender, RoutedEventArgs e) => Escolher(AcaoHorario.Falta);
    private void AoCancelar(object sender, RoutedEventArgs e) => Escolher(AcaoHorario.Cancelar);
    private void AoQuemChamar(object sender, RoutedEventArgs e) => Escolher(AcaoHorario.QuemChamar);
    private void AoComprovante(object sender, RoutedEventArgs e) => Escolher(AcaoHorario.Comprovante);
    private void AoCancelarSerie(object sender, RoutedEventArgs e) => Escolher(AcaoHorario.CancelarSerie);

    private void AoFechar(object sender, RoutedEventArgs e) => Close();
}
