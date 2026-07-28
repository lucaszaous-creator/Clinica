using System.Collections.ObjectModel;
using System.Globalization;
using Clinica.Application.Servicos;
using Clinica.Desktop.Shell;
using Clinica.Domain.Entities;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Clinica.Financeiro.ViewModels;

/// <summary>Opção de categoria no combo (null = sem categoria).</summary>
public sealed record OpcaoCategoria(int? Id, string Nome);

/// <summary>
/// Formulário de um lançamento novo — a entrada manual do caixa (aluguel, material,
/// recebimento fora de guia). O que nasce de guia do convênio continua vindo da
/// Conciliação; aqui é o dinheiro que o faturamento não conhece.
///
/// O feedback é INLINE (<see cref="Mensagem"/>), não snackbar: é formulário, e a
/// mensagem precisa ficar na tela enquanto o usuário corrige.
/// </summary>
public sealed partial class LancamentoEdicaoViewModel : ObservableObject
{
    private readonly FinanceiroService _financeiro;

    /// <summary>
    /// Disparado uma única vez, depois de o lançamento ser gravado. A janela fecha por
    /// ele — um evento, e não uma propriedade, porque o fechamento é um acontecimento
    /// pontual e não um estado que alguém precise consultar depois.
    /// </summary>
    public event Action? Concluido;

    public ObservableCollection<OpcaoCategoria> Categorias { get; } = [];

    public IReadOnlyList<TipoLancamento> Tipos { get; } =
        [TipoLancamento.Entrada, TipoLancamento.Saida];

    /// <summary>Cancelado não entra aqui: cancelamento é ação sobre lançamento existente.</summary>
    public IReadOnlyList<StatusLancamento> Situacoes { get; } =
        [StatusLancamento.Realizado, StatusLancamento.Previsto];

    public IReadOnlyList<FormaPagamento> Formas { get; } = Enum.GetValues<FormaPagamento>();

    [ObservableProperty] private DateTime _data = DateTime.Today;
    [ObservableProperty] private TipoLancamento _tipo = TipoLancamento.Saida;
    [ObservableProperty] private string? _descricao;

    /// <summary>Texto do valor — validado na hora de salvar, para o campo aceitar digitação parcial.</summary>
    [ObservableProperty] private string? _valor;

    [ObservableProperty] private StatusLancamento _situacao = StatusLancamento.Realizado;
    [ObservableProperty] private FormaPagamento? _formaPagamento;
    [ObservableProperty] private OpcaoCategoria? _categoria;
    [ObservableProperty] private string? _observacoes;
    [ObservableProperty] private bool _ocupado;
    [ObservableProperty] private string? _mensagem;
    [ObservableProperty] private bool _mensagemEhErro;

    public LancamentoEdicaoViewModel(FinanceiroService financeiro)
    {
        _financeiro = financeiro;
        _ = CarregarCategoriasAsync();
    }

    /// <summary>
    /// Só as categorias do tipo escolhido fazem sentido — trocar Entrada/Saída recarrega
    /// a lista e descarta a seleção que deixou de valer.
    /// </summary>
    partial void OnTipoChanged(TipoLancamento value) => _ = CarregarCategoriasAsync();

    private async Task CarregarCategoriasAsync()
    {
        try
        {
            var todas = await _financeiro.CategoriasAsync(Tipo);
            var escolhida = Categoria?.Id;

            Categorias.Clear();
            Categorias.Add(new OpcaoCategoria(null, "(sem categoria)"));
            foreach (var c in todas)
                Categorias.Add(new OpcaoCategoria(c.Id, c.Nome));

            Categoria = Categorias.FirstOrDefault(o => o.Id == escolhida) ?? Categorias[0];
        }
        catch (Exception ex)
        {
            // Degradação deliberada: sem a lista, o lançamento ainda pode ser salvo sem
            // categoria. Mas fica registrado — senão o combo vazio vira mistério.
            Clinica.Application.Diagnostico.Registrar("Financeiro — categorias não puderam ser lidas", ex);
            Categorias.Clear();
            Categorias.Add(new OpcaoCategoria(null, "(sem categoria)"));
            Categoria = Categorias[0];
        }
    }

    [RelayCommand]
    private async Task SalvarAsync()
    {
        Mensagem = null;

        if (string.IsNullOrWhiteSpace(Descricao))
        {
            Erro("Informe a descrição do lançamento.");
            return;
        }
        if (!TentarLerValor(Valor, out var valor))
        {
            Erro("Informe um valor maior que zero (ex.: 1.250,00).");
            return;
        }

        try
        {
            Ocupado = true;
            await _financeiro.LancarAsync(
                data: DateOnly.FromDateTime(Data),
                tipo: Tipo,
                descricao: Descricao!,
                valor: valor,
                status: Situacao,
                formaPagamento: FormaPagamento,
                categoriaId: Categoria?.Id,
                observacoes: string.IsNullOrWhiteSpace(Observacoes) ? null : Observacoes,
                operador: SessaoUsuario.Atual.Operador);

            Concluido?.Invoke();
        }
        catch (Exception ex)
        {
            Erro($"Não foi possível salvar: {ex.Message}");
        }
        finally
        {
            Ocupado = false;
        }
    }

    /// <summary>
    /// Aceita o valor como a clínica digita: "1.250,00" (pt-BR) e também "1250.00".
    /// O sinal nunca vem daqui — quem decide entrada ou saída é o <see cref="Tipo"/>.
    /// </summary>
    internal static bool TentarLerValor(string? texto, out decimal valor)
    {
        valor = 0;
        if (string.IsNullOrWhiteSpace(texto)) return false;

        var limpo = texto.Trim().Replace("R$", string.Empty).Trim();

        if (!decimal.TryParse(limpo, NumberStyles.Currency, new CultureInfo("pt-BR"), out valor) &&
            !decimal.TryParse(limpo, NumberStyles.Currency, CultureInfo.InvariantCulture, out valor))
            return false;

        return valor > 0;
    }

    private void Erro(string mensagem)
    {
        Mensagem = mensagem;
        MensagemEhErro = true;
    }
}
