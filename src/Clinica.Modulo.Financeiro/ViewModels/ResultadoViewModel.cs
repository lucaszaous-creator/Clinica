using System.Collections.ObjectModel;
using Clinica.Application.Servicos;
using Clinica.Desktop.Controls;
using Clinica.Desktop.Shell;
using Clinica.Desktop.Shell.Componentes;
using Clinica.Domain.Entities;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;

namespace Clinica.Financeiro.ViewModels;

/// <summary>Uma linha do resultado do mês (uma categoria).</summary>
public sealed class LinhaResultadoMes
{
    public required string Categoria { get; init; }
    public required string Valor { get; init; }
    public required double Fracao { get; init; }
}

/// <summary>Um teto de gasto com o que já foi consumido dele.</summary>
public sealed class LinhaOrcamento
{
    public required int CategoriaId { get; init; }
    public required string Categoria { get; init; }
    public required string Teto { get; init; }
    public required string Gasto { get; init; }
    public required string Saldo { get; init; }
    public required string Situacao { get; init; }
    public required double Fracao { get; init; }
    public required bool Estourou { get; init; }
    public required bool EmAtencao { get; init; }
}

/// <summary>
/// Resultado do mês e teto de gasto — as duas leituras que faltavam entre o extrato e o
/// indicador (parcela 31).
///
/// O Financeiro responde "o que entrou e saiu" (Caixa), "como isso se distribui no tempo"
/// (Fluxo de caixa) e "quanto de imposto" (Apuração). Nenhuma responde as duas perguntas
/// que a direção faz todo mês: **sobrou quanto?** e **o gasto ficou dentro do que a gente
/// combinou?**
///
/// As duas moram na mesma tela em sub-abas porque são o mesmo assunto visto dos dois
/// lados: o resultado diz o que aconteceu, o orçamento diz o que deveria ter acontecido.
/// </summary>
public sealed partial class ResultadoViewModel : ObservableObject
{
    private readonly IServiceScopeFactory _escopos;
    private readonly ISnackbarService _snackbar;
    private readonly IDialogoService _dialogo;

    public ObservableCollection<LinhaResultadoMes> Entradas { get; } = [];
    public ObservableCollection<LinhaResultadoMes> Saidas { get; } = [];
    public ObservableCollection<LinhaOrcamento> Orcamentos { get; } = [];

    [ObservableProperty] private DateTime _mes = new(DateTime.Today.Year, DateTime.Today.Month, 1);
    [ObservableProperty] private bool _carregando;

    /// <summary>
    /// A leitura FALHOU — o terceiro estado. Sem ele, tela vazia por erro fica idêntica
    /// a tela vazia por não haver nada.
    /// </summary>
    [ObservableProperty] private bool _naoVerificado;

    // ---- Resultado ----
    [ObservableProperty] private string _receita = "—";
    [ObservableProperty] private string _deducoes = "—";
    [ObservableProperty] private string _receitaLiquida = "—";
    [ObservableProperty] private string _despesas = "—";
    [ObservableProperty] private string _resultado = "—";
    [ObservableProperty] private string _margem = "—";
    [ObservableProperty] private bool _prejuizo;
    [ObservableProperty] private string _resumo = string.Empty;

    /// <summary>Leitura do resultado falhou — terceiro estado, nunca zero disfarçado.</summary>
    [ObservableProperty] private bool _resultadoNaoVerificado;

    // ---- Orçamento ----
    [ObservableProperty] private string _resumoOrcamento = string.Empty;
    [ObservableProperty] private bool _orcamentoNaoVerificado;

    [ObservableProperty] private string? _mensagem;
    [ObservableProperty] private bool _mensagemEhErro;

    public bool PodeEditar => SessaoUsuario.Atual.Pode(Permissao.EditarFinanceiro);

    public ResultadoViewModel(
        IServiceScopeFactory escopos, ISnackbarService snackbar, IDialogoService dialogo)
    {
        _escopos = escopos;
        _snackbar = snackbar;
        _dialogo = dialogo;
        _ = CarregarAsync();
    }

    partial void OnMesChanged(DateTime value) => _ = CarregarAsync();

    [RelayCommand]
    public async Task CarregarAsync()
    {
        Carregando = true;
            NaoVerificado = false;
        Mensagem = null;
        MensagemEhErro = false;

        // Cada bloco carrega sozinho: o orçamento quebrar não pode apagar o resultado,
        // que é a resposta principal da tela.
        await CarregarResultadoAsync();
        await CarregarOrcamentoAsync();

        Carregando = false;
    }

    private async Task CarregarResultadoAsync()
    {
        try
        {
            ResultadoNaoVerificado = false;

            using var scope = _escopos.CreateScope();
            var servico = scope.ServiceProvider.GetRequiredService<ResultadoMensalService>();
            var r = await servico.DoMesAsync(Mes.Year, Mes.Month);

            Receita = r.Receita.ToString("C");
            Deducoes = r.Deducoes.ToString("C");
            ReceitaLiquida = r.ReceitaLiquida.ToString("C");
            Despesas = r.Despesas.ToString("C");
            Resultado = r.Resultado.ToString("C");
            // Margem sobre zero não é 0%: é conta que não existe.
            Margem = r.Margem is { } m ? $"{m:0.#}%" : "—";
            Prejuizo = r.Prejuizo;

            Entradas.Clear();
            foreach (var l in r.Entradas)
                Entradas.Add(new LinhaResultadoMes
                {
                    Categoria = l.Categoria, Valor = l.Valor.ToString("C"), Fracao = l.Fracao
                });

            Saidas.Clear();
            foreach (var l in r.Saidas)
                Saidas.Add(new LinhaResultadoMes
                {
                    Categoria = l.Categoria, Valor = l.Valor.ToString("C"), Fracao = l.Fracao
                });

            Resumo = r.Vazio
                ? "Nenhum movimento realizado neste mês."
                : r.Prejuizo
                    ? $"O mês fechou {Math.Abs(r.Resultado):C} no vermelho."
                    : $"Sobraram {r.Resultado:C}"
                      + (r.Margem is { } margem ? $" — {margem:0.#}% do faturado." : ".");
        }
        catch (Exception ex)
        {
            NaoVerificado = true;
            Clinica.Application.Diagnostico.Registrar(
                "Financeiro — resultado do mês não pôde ser lido", ex);
            ResultadoNaoVerificado = true;
            Entradas.Clear();
            Saidas.Clear();
            Receita = Deducoes = ReceitaLiquida = Despesas = Resultado = Margem = "—";
            Resumo = $"Não foi possível apurar o mês: {ex.Message}";
        }
    }

    private async Task CarregarOrcamentoAsync()
    {
        try
        {
            OrcamentoNaoVerificado = false;

            using var scope = _escopos.CreateScope();
            var servico = scope.ServiceProvider.GetRequiredService<OrcamentoService>();
            var apurados = await servico.ApurarMesAsync(Mes.Year, Mes.Month);

            Orcamentos.Clear();
            foreach (var o in apurados)
                Orcamentos.Add(new LinhaOrcamento
                {
                    CategoriaId = o.CategoriaId,
                    Categoria = o.Categoria,
                    Teto = o.Teto.ToString("C"),
                    Gasto = o.Gasto.ToString("C"),
                    Saldo = o.Saldo.ToString("C"),
                    Fracao = Math.Min((o.PercentualUsado ?? 0) / 100.0, 1.0),
                    Estourou = o.Estourou,
                    EmAtencao = o.EmAtencao,
                    Situacao = Descrever(o)
                });

            var estourados = apurados.Count(o => o.Estourou);
            ResumoOrcamento = apurados.Count == 0
                ? "Nenhum teto definido para este mês. Sem teto, o gasto só é julgado "
                  + "contra o mês anterior — e gastar 10% menos que um mês ruim aparece "
                  + "como economia."
                : estourados == 0
                    ? $"{apurados.Count} categoria(s) com teto — nenhuma estourou."
                    : $"{estourados} de {apurados.Count} categoria(s) passaram do teto.";
        }
        catch (Exception ex)
        {
            Clinica.Application.Diagnostico.Registrar(
                "Financeiro — orçamento do mês não pôde ser lido", ex);
            Orcamentos.Clear();
            OrcamentoNaoVerificado = true;
            ResumoOrcamento = $"Não foi possível ler os tetos: {ex.Message}";
        }
    }

    private static string Descrever(OrcamentoApurado o)
    {
        if (o.Estourou)
            return $"estourou em {Math.Abs(o.Saldo):C}";

        if (o.VaiEstourar && o.Projetado is { } projetado)
            // O comprometido é o que muda a decisão: o teto costuma estourar no que já
            // foi contratado, não no que já foi pago.
            return $"o já contratado leva a {projetado:C} — passa do teto";

        return o.PercentualUsado is { } p
            ? $"{p:0.#}% do teto usado"
            : "sem teto para comparar";
    }

    /// <summary>Define o teto de uma categoria no mês.</summary>
    [RelayCommand]
    private async Task DefinirTetoAsync()
    {
        SessaoUsuario.Atual.Exigir(Permissao.EditarFinanceiro, "definir teto de gasto");

        var vm = new OrcamentoEdicaoViewModel(_escopos, Mes.Year, Mes.Month);
        var janela = new Janelas.OrcamentoWindow(vm)
        {
            Owner = System.Windows.Application.Current?.MainWindow
        };

        if (janela.ShowDialog() != true) return;

        _snackbar.Sucesso("Teto definido — o painel da direção já avisa quando estourar.");
        await CarregarAsync();
    }

    /// <summary>
    /// Apagar o teto é diferente de zerá-lo: sem linha, a categoria volta a não ter régua;
    /// zero declara que não se gasta nada nela.
    /// </summary>
    [RelayCommand]
    private async Task ExcluirTetoAsync(LinhaOrcamento? linha)
    {
        if (linha is null) return;

        try
        {
            SessaoUsuario.Atual.Exigir(Permissao.EditarFinanceiro, "excluir teto de gasto");

            if (!_dialogo.ConfirmarPerigo(
                    "Excluir teto",
                    $"Tirar o teto de {linha.Categoria} neste mês?\n\n"
                    + "A categoria volta a não ter régua: o gasto passa a ser julgado só "
                    + "contra o mês anterior. Se a intenção é declarar que não se gasta "
                    + "nada nela, edite o valor para zero em vez de apagar.")) return;

            using var scope = _escopos.CreateScope();
            var servico = scope.ServiceProvider.GetRequiredService<OrcamentoService>();
            var tetos = await servico.DoMesAsync(Mes.Year, Mes.Month);
            var alvo = tetos.FirstOrDefault(t => t.CategoriaFinanceiraId == linha.CategoriaId);
            if (alvo is null) return;

            await servico.ExcluirAsync(alvo.Id, SessaoUsuario.Atual.Operador);
            _snackbar.Info("Teto excluído.");
            await CarregarAsync();
        }
        catch (Exception ex)
        {
            Clinica.Application.Diagnostico.Registrar("Financeiro — teto não pôde ser excluído", ex);
            Erro(ex.Message);
        }
    }

    /// <summary>
    /// O resultado em CSV, para o contador. Separador ponto e vírgula e BOM UTF-8, como o
    /// resto da suíte — com vírgula o arquivo abre com tudo numa coluna só.
    /// </summary>
    [RelayCommand]
    private async Task ExportarAsync()
    {
        if (Entradas.Count == 0 && Saidas.Count == 0)
        {
            _snackbar.Info("Não há movimento para exportar neste mês.");
            return;
        }

        try
        {
            var linhas = new List<IReadOnlyList<string>>();
            foreach (var e in Entradas) linhas.Add(["Entrada", e.Categoria, e.Valor]);
            foreach (var s in Saidas) linhas.Add(["Saída", s.Categoria, s.Valor]);
            linhas.Add(["Total", "Receita bruta", Receita]);
            linhas.Add(["Total", "Deduções (taxa e imposto)", Deducoes]);
            linhas.Add(["Total", "Receita líquida", ReceitaLiquida]);
            linhas.Add(["Total", "Despesas", Despesas]);
            linhas.Add(["Total", "Resultado", Resultado]);
            linhas.Add(["Total", "Margem", Margem]);

            var csv = ExportacaoCsv.Montar(["Tipo", "Categoria", "Valor"], linhas);

            var erro = await ImpressaoPdf.SalvarEAbrirAsync(
                csv,
                ImpressaoPdf.NomeSeguro($"resultado-{Mes:yyyy-MM}.csv"),
                "CSV (*.csv)|*.csv", ".csv");

            if (erro is not null) Erro(erro);
        }
        catch (Exception ex)
        {
            Clinica.Application.Diagnostico.Registrar(
                "Financeiro — resultado não pôde ser exportado", ex);
            Erro(ex.Message);
        }
    }

    [RelayCommand]
    private void MesAnterior() => Mes = Mes.AddMonths(-1);

    [RelayCommand]
    private void ProximoMes() => Mes = Mes.AddMonths(1);

    private void Erro(string mensagem)
    {
        Mensagem = mensagem;
        MensagemEhErro = true;
    }
}

/// <summary>
/// Definir o teto de uma categoria, na janela.
///
/// Só categorias de SAÍDA aparecem: alvo de receita é meta, e a direção já define metas
/// na tela própria do Gerente.
/// </summary>
public sealed partial class OrcamentoEdicaoViewModel : ObservableObject
{
    private readonly IServiceScopeFactory _escopos;
    private readonly int _ano;
    private readonly int _mes;

    public sealed record OpcaoCategoria(int Id, string Nome);

    public ObservableCollection<OpcaoCategoria> Categorias { get; } = [];

    [ObservableProperty] private OpcaoCategoria? _categoria;
    [ObservableProperty] private string _teto = string.Empty;
    [ObservableProperty] private string? _mensagem;
    [ObservableProperty] private bool _mensagemEhErro;
    [ObservableProperty] private bool _salvando;

    public string Titulo { get; }

    public event Action? Concluido;

    public OrcamentoEdicaoViewModel(IServiceScopeFactory escopos, int ano, int mes)
    {
        _escopos = escopos;
        _ano = ano;
        _mes = mes;
        Titulo = $"Teto de gasto — {mes:00}/{ano}";
        _ = CarregarCategoriasAsync();
    }

    private async Task CarregarCategoriasAsync()
    {
        try
        {
            using var scope = _escopos.CreateScope();
            var financeiro = scope.ServiceProvider.GetRequiredService<FinanceiroService>();

            Categorias.Clear();
            foreach (var c in await financeiro.CategoriasAsync(TipoLancamento.Saida))
                Categorias.Add(new OpcaoCategoria(c.Id, c.Nome));

            Categoria = Categorias.FirstOrDefault();
        }
        catch (Exception ex)
        {
            Clinica.Application.Diagnostico.Registrar(
                "Financeiro — categorias não puderam ser lidas para o teto", ex);
            Mensagem = ex.Message;
            MensagemEhErro = true;
        }
    }

    [RelayCommand]
    private async Task SalvarAsync()
    {
        if (Categoria is null)
        {
            Erro("Escolha a categoria.");
            return;
        }

        if (!Valores.TentarLerDecimal(Teto, out var teto))
        {
            Erro("Informe o teto do mês.");
            return;
        }

        try
        {
            Salvando = true;
            using var scope = _escopos.CreateScope();
            var servico = scope.ServiceProvider.GetRequiredService<OrcamentoService>();
            await servico.DefinirAsync(
                _ano, _mes, Categoria.Id, teto, SessaoUsuario.Atual.Operador);

            Concluido?.Invoke();
        }
        catch (Exception ex)
        {
            Clinica.Application.Diagnostico.Registrar("Financeiro — teto não pôde ser salvo", ex);
            Erro(ex.Message);
        }
        finally
        {
            Salvando = false;
        }
    }

    private void Erro(string mensagem)
    {
        Mensagem = mensagem;
        MensagemEhErro = true;
    }
}
