using System.Windows;

namespace Clinica.Desktop.Shell.Componentes;

/// <summary>
/// O catálogo de enfermagem aberto em janela, para a enfermeira MAXIMIZAR e ler tudo
/// (parcela 88, 4ª rodada — pedido da clínica).
///
/// ⚠️ PONTO ÚNICO de abertura, e uma janela só para os DOIS catálogos (diagnósticos e
/// cuidados): o que muda entre eles é o <see cref="CatalogoDeEnfermagem"/> que chega, e
/// duas janelas seriam duas definições de "escolher do catálogo" — a segunda correção já
/// sairia divergente.
/// </summary>
public partial class CatalogoEnfermagemWindow : Window
{
    private CatalogoEnfermagemWindow(CatalogoDeEnfermagem catalogo)
    {
        InitializeComponent();
        DataContext = catalogo;
    }

    /// <summary>
    /// Abre o catálogo sobre a janela ATIVA.
    ///
    /// ⚠️ O dono é <see cref="JanelaDona.Atual"/>, nunca a <c>MainWindow</c>: o compositor
    /// pode estar dentro de uma janela modal (a evolução da sala de infusão), e ali a
    /// janela nasceria ATRÁS dela — quem clicou concluiria que o botão não fez nada (a
    /// lição da parcela 58).
    /// </summary>
    public static void Abrir(CatalogoDeEnfermagem catalogo)
    {
        ArgumentNullException.ThrowIfNull(catalogo);

        // O plano mudou desde a última abertura — escolher um diagnóstico traz os cuidados
        // dele junto —, então a lista é remontada AQUI. Sem isso ela ofereceria
        // "Acrescentar" para o que já está no plano, e o clique voltaria calado.
        catalogo.Recarregar();

        new CatalogoEnfermagemWindow(catalogo) { Owner = JanelaDona.Atual() }.ShowDialog();
    }

    private void Fechar(object sender, RoutedEventArgs e) => Close();
}
