using System.Collections.ObjectModel;
using Clinica.Application.Servicos;
using Clinica.Desktop.Controls;
using Clinica.Desktop.Shell;
using Clinica.Domain.Entities;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Clinica.Financeiro.ViewModels;

/// <summary>Um item do estoque na tela, com saldo e alerta já resolvidos.</summary>
public sealed class LinhaEstoque
{
    public required int Id { get; init; }
    public required string Nome { get; init; }
    public required string Saldo { get; init; }
    public required string Minimo { get; init; }
    public required string Custo { get; init; }
    public required bool AbaixoDoMinimo { get; init; }
    public required bool Ativo { get; init; }
}

/// <summary>Um lote vencido ou a vencer.</summary>
public sealed class LinhaValidade
{
    public required string Item { get; init; }
    public required string Validade { get; init; }
    public required string Quantidade { get; init; }
    public required string Situacao { get; init; }
    public required bool Vencido { get; init; }
}

/// <summary>
/// Estoque de insumos (feature 10): saldo, alerta de reposição e de validade.
///
/// A tela abre pelos ALERTAS, não pela lista: quem entra aqui quer saber o que está
/// faltando e o que vai vencer — a lista completa é consulta, não é o motivo da visita.
/// </summary>
public sealed partial class EstoqueViewModel : ObservableObject
{
    private readonly EstoqueService _estoque;
    private readonly ISnackbarService _snackbar;
    private readonly IDialogoService _dialogo;

    public ObservableCollection<LinhaEstoque> Itens { get; } = [];
    public ObservableCollection<LinhaValidade> Validades { get; } = [];

    // ---- Cadastro rápido (item novo é linha curta: nome, unidade, mínimo) ----
    [ObservableProperty] private string? _novoNome;
    [ObservableProperty] private string? _novaUnidade = "un";
    [ObservableProperty] private string? _novoMinimo;

    [ObservableProperty] private bool _carregando;
    [ObservableProperty] private string _mensagem = string.Empty;
    [ObservableProperty] private bool _mensagemEhErro;
    [ObservableProperty] private string _resumo = "—";
    [ObservableProperty] private bool _semAlertas;

    /// <summary>
    /// Habilita os botões de escrita da tela. É a metade VISÍVEL da permissão: o
    /// botão apagado explica por que não dá; a guarda no comando é que impede.
    /// Só desabilitar seria enfeite — um atalho de teclado passaria direto.
    /// </summary>
    public bool PodeEditarFinanceiro => SessaoUsuario.Atual.Pode(Permissao.EditarFinanceiro);

    public EstoqueViewModel(EstoqueService estoque, ISnackbarService snackbar, IDialogoService dialogo)
    {
        _estoque = estoque;
        _snackbar = snackbar;
        _dialogo = dialogo;
        _ = CarregarAsync();
    }

    [RelayCommand]
    private async Task CarregarAsync()
    {
        try
        {
            Carregando = true;
            Mensagem = string.Empty;
            MensagemEhErro = false;

            var saldos = await _estoque.SaldosAsync();

            Itens.Clear();
            foreach (var s in saldos)
                Itens.Add(new LinhaEstoque
                {
                    Id = s.ItemId,
                    Nome = s.Nome,
                    Saldo = s.SaldoRotulo,
                    Minimo = s.EstoqueMinimo > 0 ? $"{s.EstoqueMinimo:0.##} {s.Unidade}" : "—",
                    Custo = s.CustoMedio is { } custo ? custo.ToString("C") : "—",
                    AbaixoDoMinimo = s.AbaixoDoMinimo,
                    Ativo = s.Ativo
                });

            var hoje = DateOnly.FromDateTime(DateTime.Today);
            var validades = await _estoque.ValidadesAsync(hoje);

            Validades.Clear();
            foreach (var v in validades)
                Validades.Add(new LinhaValidade
                {
                    Item = v.Nome,
                    Validade = v.Validade.ToString("dd/MM/yyyy"),
                    Quantidade = $"{v.Quantidade:0.##}",
                    Situacao = v.Vencido(hoje)
                        ? "VENCIDO"
                        : $"vence em {v.DiasRestantes(hoje)} dia(s)",
                    Vencido = v.Vencido(hoje)
                });

            var faltando = saldos.Count(s => s.AbaixoDoMinimo);
            SemAlertas = faltando == 0 && validades.Count == 0;
            Resumo = SemAlertas
                ? $"{saldos.Count} item(ns) no estoque — nada abaixo do mínimo nem vencendo."
                : $"{faltando} item(ns) para repor · {validades.Count} lote(s) vencendo ou vencidos.";
        }
        catch (Exception ex)
        {
            Clinica.Application.Diagnostico.Registrar("Financeiro — estoque não pôde ser carregado", ex);
            Erro($"Não foi possível carregar o estoque: {ex.Message}");
        }
        finally
        {
            Carregando = false;
        }
    }

    [RelayCommand]
    private async Task AdicionarItemAsync()
    {
        SessaoUsuario.Atual.Exigir(Permissao.EditarFinanceiro, "mexer no estoque");

        try
        {
            decimal minimo = 0m;
            if (!string.IsNullOrWhiteSpace(NovoMinimo) && !decimal.TryParse(NovoMinimo, out minimo))
                throw new InvalidOperationException("O mínimo tem de ser um número.");

            await _estoque.SalvarItemAsync(new ItemEstoque
            {
                Nome = NovoNome ?? string.Empty,
                Unidade = string.IsNullOrWhiteSpace(NovaUnidade) ? "un" : NovaUnidade!,
                EstoqueMinimo = minimo,
                Ativo = true
            }, SessaoUsuario.Atual.Operador);

            NovoNome = NovoMinimo = null;
            NovaUnidade = "un";
            _snackbar.Sucesso("Item cadastrado.");
            await CarregarAsync();
        }
        catch (Exception ex)
        {
            Clinica.Application.Diagnostico.Registrar("Financeiro — item de estoque não pôde ser salvo", ex);
            Erro(ex.Message);
        }
    }

    [RelayCommand]
    private async Task MovimentarAsync(LinhaEstoque? linha)
    {
        if (linha is null) return;

        SessaoUsuario.Atual.Exigir(Permissao.EditarFinanceiro, "mexer no estoque");

        var vm = new MovimentoEstoqueViewModel(_estoque, linha.Id, linha.Nome);
        var janela = new Janelas.MovimentoEstoqueWindow(vm)
        {
            Owner = System.Windows.Application.Current?.MainWindow
        };

        if (janela.ShowDialog() != true) return;
        _snackbar.Sucesso("Movimento registrado.");
        await CarregarAsync();
    }

    [RelayCommand]
    private async Task ExcluirItemAsync(LinhaEstoque? linha)
    {
        if (linha is null) return;

        SessaoUsuario.Atual.Exigir(Permissao.EditarFinanceiro, "mexer no estoque");
        if (!_dialogo.ConfirmarPerigo("Excluir item",
                $"Apagar \"{linha.Nome}\" e TODO o histórico de movimentos dele? "
                + "Item que já se movimentou normalmente deve ser inativado, não apagado.")) return;

        try
        {
            await _estoque.ExcluirItemAsync(linha.Id);
            _snackbar.Info("Item excluído.");
            await CarregarAsync();
        }
        catch (Exception ex)
        {
            Clinica.Application.Diagnostico.Registrar("Financeiro — item de estoque não pôde ser excluído", ex);
            Erro(ex.Message);
        }
    }

    private void Erro(string texto)
    {
        Mensagem = texto;
        MensagemEhErro = true;
    }
}

/// <summary>Entrada, baixa ou perda de um item — o formulário curto da janela.</summary>
public sealed partial class MovimentoEstoqueViewModel : ObservableObject
{
    private readonly EstoqueService _estoque;
    private readonly int _itemId;

    public string Item { get; }

    public IReadOnlyList<TipoMovimentoEstoque> Tipos { get; } = Enum.GetValues<TipoMovimentoEstoque>();

    [ObservableProperty] private TipoMovimentoEstoque _tipo = TipoMovimentoEstoque.Entrada;
    [ObservableProperty] private string? _quantidade;
    [ObservableProperty] private string? _custoUnitario;
    [ObservableProperty] private DateTime _data = DateTime.Today;
    [ObservableProperty] private DateTime? _validade;
    [ObservableProperty] private string? _lote;
    [ObservableProperty] private string? _observacao;

    [ObservableProperty] private string _mensagem = string.Empty;
    [ObservableProperty] private bool _mensagemEhErro;
    [ObservableProperty] private bool _salvando;

    /// <summary>Custo e validade só fazem sentido na entrada — é ela que traz o lote.</summary>
    public bool EhEntrada => Tipo == TipoMovimentoEstoque.Entrada;

    public event Action? Concluido;

    public MovimentoEstoqueViewModel(EstoqueService estoque, int itemId, string item)
    {
        _estoque = estoque;
        _itemId = itemId;
        Item = item;
    }

    partial void OnTipoChanged(TipoMovimentoEstoque value) => OnPropertyChanged(nameof(EhEntrada));

    [RelayCommand]
    private async Task SalvarAsync()
    {
        Mensagem = string.Empty;
        MensagemEhErro = false;

        try
        {
            SessaoUsuario.Atual.Exigir(Permissao.EditarFinanceiro, "movimentar o estoque");

            Salvando = true;

            if (!decimal.TryParse(Quantidade, out var quantidade))
                throw new InvalidOperationException("Informe a quantidade (um número).");

            decimal? custo = null;
            if (EhEntrada && !string.IsNullOrWhiteSpace(CustoUnitario))
            {
                if (!decimal.TryParse(CustoUnitario, out var lido))
                    throw new InvalidOperationException("Não entendi o custo: use algo como 12,50.");
                custo = lido;
            }

            if (Tipo == TipoMovimentoEstoque.Perda && string.IsNullOrWhiteSpace(Observacao))
                throw new InvalidOperationException(
                    "Perda sem motivo escrito vira estoque que não bate — diga o que houve.");

            await _estoque.MovimentarAsync(new MovimentoEstoque
            {
                ItemEstoqueId = _itemId,
                Tipo = Tipo,
                Quantidade = quantidade,
                CustoUnitario = custo,
                Data = DateOnly.FromDateTime(Data),
                Validade = EhEntrada && Validade is { } v ? DateOnly.FromDateTime(v) : null,
                Lote = Lote,
                Observacao = Observacao
            }, SessaoUsuario.Atual.Operador);

            Concluido?.Invoke();
        }
        catch (Exception ex)
        {
            Clinica.Application.Diagnostico.Registrar("Financeiro — movimento de estoque não pôde ser salvo", ex);
            Mensagem = ex.Message;
            MensagemEhErro = true;
        }
        finally
        {
            Salvando = false;
        }
    }
}
