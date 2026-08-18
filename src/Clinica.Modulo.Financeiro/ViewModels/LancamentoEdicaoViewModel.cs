using System.Collections.ObjectModel;
using Clinica.Application.Servicos;
using Clinica.Desktop.Shell;
using Clinica.Domain.Entities;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;

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
    private readonly IServiceScopeFactory _escopos;

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

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(EhCartao))]
    private FormaPagamento? _formaPagamento;

    [ObservableProperty] private OpcaoCategoria? _categoria;
    [ObservableProperty] private string? _observacoes;
    [ObservableProperty] private bool _ocupado;
    [ObservableProperty] private string? _mensagem;
    [ObservableProperty] private bool _mensagemEhErro;

    // ---- Taxa da maquininha e imposto (parcela 9) ----
    [ObservableProperty] private string? _adquirente;
    [ObservableProperty] private string? _bandeira;
    [ObservableProperty] private string? _parcelas = "1";
    [ObservableProperty] private bool _reterImposto;

    /// <summary>Resumo do que sera descontado, recalculado a cada tecla relevante.</summary>
    [ObservableProperty] private string _resumoDeducoes = string.Empty;

    [ObservableProperty] private bool _temDeducoes;

    /// <summary>Adquirente e parcelas so aparecem quando a forma e cartao.</summary>
    public bool EhCartao => FormaPagamento is Clinica.Domain.Entities.FormaPagamento.CartaoDebito
        or Clinica.Domain.Entities.FormaPagamento.CartaoCredito;

    public LancamentoEdicaoViewModel(IServiceScopeFactory escopos)
    {
        _escopos = escopos;
        _ = CarregarCategoriasAsync();
    }

    /// <summary>
    /// Só as categorias do tipo escolhido fazem sentido — trocar Entrada/Saída recarrega
    /// a lista e descarta a seleção que deixou de valer.
    /// </summary>
    partial void OnTipoChanged(TipoLancamento value)
    {
        _ = CarregarCategoriasAsync();
        _ = RecalcularDeducoesAsync();
    }

    // Cada um destes muda o que a maquininha desconta — a previa acompanha, para o valor
    // liquido aparecer ANTES de gravar e nao no extrato do mes que vem.
    partial void OnValorChanged(string? value) => _ = RecalcularDeducoesAsync();
    partial void OnFormaPagamentoChanged(Clinica.Domain.Entities.FormaPagamento? value)
        => _ = RecalcularDeducoesAsync();
    partial void OnAdquirenteChanged(string? value) => _ = RecalcularDeducoesAsync();
    partial void OnBandeiraChanged(string? value) => _ = RecalcularDeducoesAsync();
    partial void OnParcelasChanged(string? value) => _ = RecalcularDeducoesAsync();
    partial void OnReterImpostoChanged(bool value) => _ = RecalcularDeducoesAsync();
    partial void OnDataChanged(DateTime value) => _ = RecalcularDeducoesAsync();

    /// <summary>Deducoes calculadas da ultima vez — o que vai junto no LancarAsync.</summary>
    private DeducoesRecebimento? _deducoes;

    /// <summary>
    /// Descarte de resposta fora de ordem (a regra da parcela 50): o valor é recalculado
    /// a cada TECLA, e num banco remoto a resposta de "125" pode chegar DEPOIS da de
    /// "1250" — e o que ela escreveria em <see cref="_deducoes"/> não é só exibido, é
    /// GRAVADO no lançamento pelo Confirmar. Quem começou primeiro perde.
    /// </summary>
    private int _geracaoDeducoes;

    private int LerParcelas()
        => int.TryParse(Parcelas, out var n) && n >= 1 ? n : 1;

    /// <summary>
    /// Previa do desconto. So faz sentido em ENTRADA: taxa de maquininha e imposto
    /// retido incidem sobre o que a clinica RECEBE — numa saida seriam invencao.
    ///
    /// Falha aqui nao trava o formulario: o lancamento continua podendo ser gravado com
    /// o bruto, que e a verdade disponivel. Mas fica registrado.
    /// </summary>
    private async Task RecalcularDeducoesAsync()
    {
        var geracao = ++_geracaoDeducoes;

        _deducoes = null;
        ResumoDeducoes = string.Empty;
        TemDeducoes = false;

        if (Tipo != TipoLancamento.Entrada) return;
        if (!TentarLerValor(Valor, out var bruto)) return;

        try
        {
            using var escopo = _escopos.CreateScope();
            var d = await escopo.ServiceProvider.GetRequiredService<TaxaService>().CalcularAsync(
                bruto, DateOnly.FromDateTime(Data), FormaPagamento,
                Adquirente, Bandeira, LerParcelas(), ReterImposto);

            // Chegou tarde: outra tecla já pediu um recálculo mais novo.
            if (geracao != _geracaoDeducoes) return;

            if (d.Total <= 0m) return;

            _deducoes = d;
            TemDeducoes = true;

            var partes = new List<string>();
            if (d.ValorTaxa is { } taxa)
                partes.Add($"taxa {d.TaxaPercentual:0.##}% = {taxa:C}");
            if (d.ValorImposto is { } imposto)
                partes.Add($"imposto {d.AliquotaImposto:0.##}% = {imposto:C}");
            if (d.PrevisaoRecebimento is { } previsao)
                partes.Add($"cai em {previsao:dd/MM/yyyy}");

            ResumoDeducoes = $"Liquido {bruto - d.Total:C} — " + string.Join(" · ", partes);
        }
        catch (Exception ex)
        {
            Clinica.Application.Diagnostico.Registrar(
                "Financeiro — previa de taxa nao pode ser calculada", ex);
        }
    }

    private async Task CarregarCategoriasAsync()
    {
        try
        {
            using var escopo = _escopos.CreateScope();
            var todas = await escopo.ServiceProvider
                .GetRequiredService<FinanceiroService>().CategoriasAsync(Tipo);
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
            using var escopo = _escopos.CreateScope();
            await escopo.ServiceProvider.GetRequiredService<FinanceiroService>().LancarAsync(
                data: DateOnly.FromDateTime(Data),
                tipo: Tipo,
                descricao: Descricao!,
                valor: valor,
                status: Situacao,
                formaPagamento: FormaPagamento,
                categoriaId: Categoria?.Id,
                observacoes: string.IsNullOrWhiteSpace(Observacoes) ? null : Observacoes,
                operador: SessaoUsuario.Atual.Operador,
                deducoes: _deducoes,
                adquirente: EhCartao ? Adquirente : null,
                bandeira: EhCartao ? Bandeira : null,
                modalidadeCartao: TaxaService.ModalidadeDe(FormaPagamento, LerParcelas()),
                parcelas: EhCartao ? LerParcelas() : null);

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
    /// A regra mora no shell (<see cref="Clinica.Desktop.Controls.Valores"/>) porque o
    /// fechamento da sessão, na Recepção, digita dinheiro pelo mesmo teclado.
    /// O sinal nunca vem daqui — quem decide entrada ou saída é o <see cref="Tipo"/>.
    /// </summary>
    internal static bool TentarLerValor(string? texto, out decimal valor)
        => Clinica.Desktop.Controls.Valores.TentarLerDecimal(texto, out valor);

    private void Erro(string mensagem)
    {
        Mensagem = mensagem;
        MensagemEhErro = true;
    }
}
