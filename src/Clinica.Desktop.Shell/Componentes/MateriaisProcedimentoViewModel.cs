using System.Collections.ObjectModel;
using Clinica.Application.Abstracoes;
using Clinica.Application.Servicos;
using Clinica.Domain.Entities;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;

namespace Clinica.Desktop.Shell.Componentes;

public sealed partial class MaterialProcedimentoLinha : ObservableObject
{
    public int ItemId { get; init; }
    public string Nome { get; init; } = string.Empty;
    public string Saldo { get; init; } = string.Empty;
    [ObservableProperty] private string? _quantidade;
    [ObservableProperty] private string? _lote;
}

public sealed partial class MateriaisProcedimentoViewModel : ObservableObject
{
    private readonly List<MaterialProcedimentoLinha> _todos;
    public ObservableCollection<MaterialProcedimentoLinha> Itens { get; } = [];
    [ObservableProperty] private string? _busca;
    [ObservableProperty] private bool _semConsumo;
    [ObservableProperty] private string? _erro;
    public PedidoConsumoProcedimento? Pedido { get; private set; }
    public event Action? Confirmado;

    public MateriaisProcedimentoViewModel(IEnumerable<MaterialProcedimentoLinha> itens)
    {
        _todos = itens.ToList();
        Filtrar();
    }
    partial void OnBuscaChanged(string? value) => Filtrar();
    private void Filtrar()
    {
        var termo = Busca?.Trim();
        Itens.Clear();
        foreach (var item in _todos.Where(i => string.IsNullOrEmpty(termo) || i.Nome.Contains(termo, StringComparison.CurrentCultureIgnoreCase)))
            Itens.Add(item);
    }
    [RelayCommand]
    private void OutroLote(MaterialProcedimentoLinha? item)
    {
        if (item is null) return;
        _todos.Add(new MaterialProcedimentoLinha { ItemId = item.ItemId, Nome = item.Nome, Saldo = item.Saldo });
        Filtrar();
    }
    [RelayCommand]
    private void Confirmar()
    {
        Erro = null;
        try
        {
            var materiais = new List<MaterialConsumidoProcedimento>();
            foreach (var item in _todos.Where(i => !string.IsNullOrWhiteSpace(i.Quantidade)))
            {
                if (!decimal.TryParse(item.Quantidade, out var quantidade) || quantidade <= 0 || decimal.Round(quantidade, 3) != quantidade)
                    throw new InvalidOperationException($"{item.Nome}: informe quantidade positiva com até três casas decimais, ou deixe em branco.");
                materiais.Add(new(item.ItemId, quantidade, item.Lote));
            }
            if (materiais.Count == 0 && !SemConsumo || materiais.Count > 0 && SemConsumo)
                throw new InvalidOperationException("Informe os materiais usados ou marque que não houve consumo.");
            Pedido = new(materiais, SemConsumo);
            Confirmado?.Invoke();
        }
        catch (Exception ex) { Erro = ex.Message; }
    }
}

public static class ConferenciaMateriaisProcedimento
{
    /// <summary>A pergunta só prepara dados. Quem conclui grava sessão e estoque atomicamente.</summary>
    public static async Task<(bool Prosseguir, PedidoConsumoProcedimento? Pedido)> PerguntarAsync(
        IServiceScopeFactory escopos, int agendamentoId)
    {
        using var escopo = escopos.CreateScope();
        var repo = escopo.ServiceProvider.GetRequiredService<IClinicaRepositorio>();
        var horario = await repo.ObterAgendamentoAsync(agendamentoId)
            ?? throw new InvalidOperationException("Horário não encontrado.");
        var estoque = escopo.ServiceProvider.GetRequiredService<EstoqueService>();
        if (horario.AtendimentoId is { } id && await estoque.ConferenciaDoProcedimentoAsync(id) is not null)
            return (true, null);
        var saldos = await estoque.SaldosAsync(somenteAtivos: true);
        var linhas = saldos.Where(s => s.Uso != UsoEstoque.Rotina).OrderBy(s => s.Nome)
            .Select(s => new MaterialProcedimentoLinha { ItemId = s.ItemId,
                Nome = string.IsNullOrWhiteSpace(s.CodigoInterno) ? s.Nome : $"{s.CodigoInterno} · {s.Nome}",
                Saldo = $"Disponível: {s.SaldoRotulo}" }).ToList();
        // Baixas antigas são apresentadas para conferência; o serviço impede repeti-las.
        if (horario.AtendimentoId is { } atendimentoId)
        {
            var anteriores = (await repo.MovimentosDoAtendimentoAsync(atendimentoId))
                .Where(m => m.Tipo == TipoMovimentoEstoque.Saida)
                .GroupBy(m => (m.ItemEstoqueId, m.Lote));
            foreach (var grupo in anteriores)
            {
                var linha = linhas.FirstOrDefault(l => l.ItemId == grupo.Key.ItemEstoqueId && l.Quantidade is null);
                if (linha is null)
                {
                    var item = await estoque.ObterItemAsync(grupo.Key.ItemEstoqueId);
                    linha = new MaterialProcedimentoLinha { ItemId = grupo.Key.ItemEstoqueId, Nome = item?.Nome ?? $"Item #{grupo.Key.ItemEstoqueId}", Saldo = "Já registrado nesta sessão" };
                    linhas.Add(linha);
                }
                linha.Quantidade = grupo.Sum(m => m.Quantidade).ToString("0.###");
                linha.Lote = grupo.Key.Lote;
            }
        }
        var vm = new MateriaisProcedimentoViewModel(linhas);
        var janela = new MateriaisProcedimentoWindow(vm) { Owner = JanelaDona.Atual() };
        return janela.ShowDialog() == true ? (true, vm.Pedido) : (false, null);
    }
}
