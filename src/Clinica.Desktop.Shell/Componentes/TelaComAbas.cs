using Clinica.Desktop.Shell.Modulos;

namespace Clinica.Desktop.Shell.Componentes;

/// <summary>
/// Várias telas existentes como abas de um item de menu só (parcela 55).
///
/// Ele NÃO reimplementa tela nenhuma: recebe uma fábrica por aba e chama a que for escolhida.
/// Quem monta a fábrica é o <c>ShellViewModel</c>, o único que sabe qual módulo constrói cada
/// chave — é isso que permite compor telas de módulos diferentes sem que um conheça o outro.
///
/// ⚠️ Desde out/2026 isto não é mais um controle WPF: a régua de abas é desenhada pela moldura
/// Razor (<c>MolduraDoApp</c>), e o conteúdo de cada aba é um descritor de tela que o palco do
/// navegador põe à vista. O que ficou aqui é o ESTADO — quais abas, qual está escolhida, quais
/// já foram montadas — e as mesmas regras de antes.
/// </summary>
public sealed class TelaComAbas : IDisposable
{
    private readonly List<(string Rotulo, Func<object?> Criar)> _abas;
    private readonly object?[] _conteudos;

    /// <summary>
    /// <paramref name="abas"/> na ordem em que aparecem; <paramref name="inicial"/> é a aba já
    /// aberta — é por ela que <c>NavegacaoSuite.Ir("fechamento-caixa")</c> continua caindo na
    /// tela certa depois que ela virou aba de "Caixa".
    /// </summary>
    public TelaComAbas(IReadOnlyList<(string Rotulo, Func<object?> Criar)> abas, int inicial = 0)
    {
        _abas = abas.ToList();
        _conteudos = new object?[_abas.Count];
        if (_abas.Count == 0) return;

        // Fora da faixa cai na primeira: aba pedida que não existe neste executável (o módulo
        // dela não foi carregado) não pode deixar a tela em branco.
        Selecionada = inicial >= 0 && inicial < _abas.Count ? inicial : 0;
        Materializar(Selecionada);
    }

    /// <summary>Os rótulos, na ordem da régua.</summary>
    public IReadOnlyList<string> Rotulos => _abas.Select(a => a.Rotulo).ToList();

    /// <summary>A aba à vista.</summary>
    public int Selecionada { get; private set; }

    /// <summary>A aba escolhida mudou (a moldura redesenha a régua; o palco troca a tela).</summary>
    public event Action? Mudou;

    /// <summary>O conteúdo das abas já montadas.</summary>
    public IEnumerable<object> ConteudosAbertos => _conteudos.OfType<object>();

    /// <summary>O conteúdo da aba selecionada, se já montado.</summary>
    public object? ConteudoVisivel => _conteudos.Length == 0 ? null : _conteudos[Selecionada];

    /// <summary>
    /// Abre uma aba — pela régua, ou quando se navega de novo para o mesmo item composto. Aba
    /// JÁ montada volta com o que tinha e relê por trás, se souber; aba nova é montada.
    /// </summary>
    public void Selecionar(int indice)
    {
        if (indice < 0 || indice >= _abas.Count) return;
        if (indice != Selecionada)
        {
            var jaMontada = _conteudos[indice] is not null;
            Selecionada = indice;
            Materializar(indice);
            if (jaMontada && ConteudoVisivel is { } visivel) CicloDeVidaTela.AtualizarAoVoltar(visivel);
            Mudou?.Invoke();
        }
        if (ConteudoVisivel is IReceberNavegacao destino) destino.AoNavegar();
    }

    /// <summary>
    /// Cria o conteúdo da aba na PRIMEIRA vez que ela é aberta. Preguiçoso de propósito: o banco
    /// é remoto, e montar as três telas de "Caixa" ao abrir a primeira dispararia três
    /// consultas para mostrar uma. Depois de criada a aba é guardada.
    /// </summary>
    private void Materializar(int indice)
    {
        if (_conteudos[indice] is not null) return;
        try
        {
            _conteudos[indice] = _abas[indice].Criar();
        }
        catch (Exception ex)
        {
            // Degradação com rastro: uma aba que falhou não derruba as outras nem some em
            // silêncio — a moldura diz que ela não abriu (o conteúdo fica sem tela) e o log
            // guarda o porquê.
            Configuracao.LogSuite.Registrar($"TelaComAbas — falha ao montar a aba '{_abas[indice].Rotulo}'", ex);
            _conteudos[indice] = new AbaQueFalhou();
        }
    }

    public void Dispose()
    {
        foreach (var conteudo in ConteudosAbertos) CicloDeVidaTela.Descartar(conteudo);
    }

    /// <summary>A marca da aba que não montou: a moldura a mostra como falha, nunca em branco.</summary>
    private sealed class AbaQueFalhou;
}
