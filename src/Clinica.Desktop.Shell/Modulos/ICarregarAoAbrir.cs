namespace Clinica.Desktop.Shell.Modulos;

/// <summary>
/// A ViewModel que precisa BUSCAR alguma coisa quando a tela abre.
///
/// Por que isto existe
/// -------------------
/// O shell monta a tela em <see cref="IModuloApp.CriarTela"/> e só resolve o
/// <c>DataContext</c> — ele não chama método nenhum. A maioria das ViewModels da suíte
/// resolve isso disparando a busca no PRÓPRIO construtor (<c>_ = BuscarAsync(...)</c>), e
/// funciona; mas as duas telas que vieram do app de FATURAMENTO na parcela 46 (Novo
/// atendimento e Consultas) não faziam assim: lá quem chamava o <c>CarregarAsync</c> era a
/// navegação do <c>MainViewModel</c>, que não existe aqui. Elas chegaram à suíte abrindo
/// com o catálogo de modalidades VAZIO e a lista de consultas em branco — e tela vazia se
/// lê como sistema quebrado, não como "ninguém chamou o carregar".
///
/// Podia-se copiar o <c>_ = ...</c> para cada construtor. O contrato faz o mesmo no
/// **ponto único** por onde toda tela da suíte passa: quem implementa isto é carregado ao
/// abrir, em qualquer módulo, sem depender de alguém lembrar da linha na próxima porta.
///
/// A carga é disparada e não esperada — abrir a tela não pode ficar bloqueado num banco
/// remoto —, e a ViewModel continua responsável por tratar o próprio erro: falha aqui vira
/// o terceiro estado da tela ("não verificado"), nunca lista vazia.
/// </summary>
public interface ICarregarAoAbrir
{
    Task CarregarAsync();
}
