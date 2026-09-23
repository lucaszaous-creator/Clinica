using System.Collections.ObjectModel;
using Clinica.Desktop.Controls;
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
    private readonly Func<PedidoConsumoProcedimento, Task<ConferenciaConsumoProcedimento>>? _salvar;
    public string Contexto { get; init; } = string.Empty;
    [ObservableProperty] private bool _podeEditar = true;
    [ObservableProperty, NotifyCanExecuteChangedFor(nameof(ConfirmarCommand))] private bool _podeConfirmar = true;
    [ObservableProperty] private string _situacao = "Não informado. O atendimento e as guias já estão concluídos.";
    [ObservableProperty] private string _rotuloConfirmar = "Registrar materiais";
    public PedidoConsumoProcedimento? Pedido { get; private set; }
    public event Action? Confirmado;

    public MateriaisProcedimentoViewModel(IEnumerable<MaterialProcedimentoLinha> itens,
        Func<PedidoConsumoProcedimento, Task<ConferenciaConsumoProcedimento>>? salvar = null,
        ConferenciaConsumoProcedimento? registro = null)
    {
        _salvar = salvar;
        _todos = itens.ToList();
        if (registro is not null)
        {
            SemConsumo = registro.SemConsumo;
            Pedido = new(EstoqueService.MateriaisRegistrados(registro), registro.SemConsumo);
            MostrarRegistro(registro);
        }
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
    private void MostrarRegistro(ConferenciaConsumoProcedimento r)
    {
        PodeEditar = false;
        PodeConfirmar = r.BaixadoEm is null;
        RotuloConfirmar = "Tentar baixa novamente";
        Situacao = r.MotivoPendencia is { } motivo ? "Consumo registrado; baixa pendente. " + motivo
            : r.SemConsumo ? "Sem consumo declarado." : "Materiais registrados e baixados do estoque.";
    }

    [RelayCommand(CanExecute = nameof(PodeConfirmar))]
    private async Task ConfirmarAsync()
    {
        Erro = null;
        try
        {
            var materiais = new List<MaterialConsumidoProcedimento>();
            foreach (var item in _todos.Where(i => !string.IsNullOrWhiteSpace(i.Quantidade)))
            {
                if (!Valores.TentarLerNumeroExato(item.Quantidade, out var quantidade) || quantidade <= 0 || decimal.Round(quantidade, 3) != quantidade)
                    throw new InvalidOperationException($"{item.Nome}: informe quantidade positiva com até três casas decimais, ou deixe em branco.");
                materiais.Add(new(item.ItemId, quantidade, item.Lote));
            }
            if (materiais.Count == 0 && !SemConsumo || materiais.Count > 0 && SemConsumo)
                throw new InvalidOperationException("Informe os materiais usados ou marque que não houve consumo.");
            if (PodeEditar) Pedido = new(materiais, SemConsumo);
            if (_salvar is null) Confirmado?.Invoke();
            else MostrarRegistro(await _salvar(Pedido!));
        }
        catch (Exception ex) { Erro = ex.Message; }
    }
}

public static class ConferenciaMateriaisProcedimento
{
    /// <summary>Registro opcional posterior, aberto apenas por ação explícita.</summary>
    public static async Task AbrirAsync(
        IServiceScopeFactory escopos, int agendamentoId)
    {
        using var escopo = escopos.CreateScope();
        var repo = escopo.ServiceProvider.GetRequiredService<IClinicaRepositorio>();
        var horario = await repo.ObterAgendamentoAsync(agendamentoId)
            ?? throw new InvalidOperationException("Horário não encontrado.");
        var estoque = escopo.ServiceProvider.GetRequiredService<EstoqueService>();
        await new PoliticaMateriaisService(repo).ExigirRegistroAsync(agendamentoId, SessaoUsuario.Atual.UsuarioId);
        var registro = await estoque.ConferenciaDoProcedimentoAsync(horario.AtendimentoId!.Value);
        var saldos = await estoque.SaldosAsync(somenteAtivos: true);
        var linhas = saldos.Where(s => s.Uso != UsoEstoque.Rotina).OrderBy(s => s.Nome)
            .Select(s => new MaterialProcedimentoLinha { ItemId = s.ItemId,
                Nome = string.IsNullOrWhiteSpace(s.CodigoInterno) ? s.Nome : $"{s.CodigoInterno} · {s.Nome}",
                Saldo = $"Disponível: {s.SaldoRotulo}" }).ToList();
        // Baixas antigas são apresentadas para conferência; o serviço impede repeti-las.
        if (horario.AtendimentoId is { } atendimentoId)
        {
            var registrados = registro is not null ? EstoqueService.MateriaisRegistrados(registro) : [];
            if (registrados.Count == 0 && registro?.SemConsumo != true)
                registrados = (await repo.MovimentosDoAtendimentoAsync(atendimentoId))
                    .Where(m => m.Tipo == TipoMovimentoEstoque.Saida)
                    .Select(m => new MaterialConsumidoProcedimento(m.ItemEstoqueId, m.Quantidade, m.Lote)).ToArray();
            var anteriores = registrados.GroupBy(m => (ItemEstoqueId: m.ItemId, m.Lote));
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
        var vm = new MateriaisProcedimentoViewModel(linhas, async pedido =>
        {
            using var gravacao = escopos.CreateScope();
            return await gravacao.ServiceProvider.GetRequiredService<EstoqueService>()
                .RegistrarMateriaisAsync(agendamentoId, SessaoUsuario.Atual.UsuarioId, pedido);
        }, registro) { Contexto = $"{horario.Paciente?.Nome} · {horario.DataHora:dd/MM/yyyy HH:mm}" };
        var janela = new MateriaisProcedimentoWindow(vm) { Owner = JanelaDona.Atual() };
        janela.ShowDialog();
    }
}
