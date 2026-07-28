using System.Collections.ObjectModel;
using Clinica.Application.Servicos;
using Clinica.Desktop.Shell.Componentes;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;

namespace Clinica.Financeiro.ViewModels;

/// <summary>Um pacote do catálogo, como aparece no combo da venda.</summary>
public sealed class OpcaoPacote
{
    public required int Id { get; init; }
    public required string Rotulo { get; init; }
    public required decimal Valor { get; init; }
}

/// <summary>
/// Venda de um pacote a um paciente.
///
/// O paciente é escolhido por BUSCA, não por uma lista de todos: a base cresce e um
/// combo com mil nomes é inutilizável no balcão. A busca é a mesma do resto da suíte —
/// não se reescreve seletor de paciente.
/// </summary>
public sealed partial class PacoteVendaViewModel : ObservableObject
{
    private readonly PacoteService _pacotes;

    public ObservableCollection<OpcaoPacote> Opcoes { get; } = [];

    /// <summary>Busca de paciente do design system (limite no SQL, teclas agrupadas).</summary>
    public SeletorPacienteViewModel Seletor { get; }

    [ObservableProperty] private OpcaoPacote? _pacoteSelecionado;
    [ObservableProperty] private DateTime _dataCompra = DateTime.Today;
    [ObservableProperty] private string? _valorCobrado;
    [ObservableProperty] private string? _observacoes;

    [ObservableProperty] private string _mensagem = string.Empty;
    [ObservableProperty] private bool _mensagemEhErro;
    [ObservableProperty] private bool _salvando;

    public event Action? Concluido;

    public PacoteVendaViewModel(PacoteService pacotes, IServiceScopeFactory escopos)
    {
        _pacotes = pacotes;
        Seletor = new SeletorPacienteViewModel(escopos);
        _ = CarregarAsync();
    }

    partial void OnPacoteSelecionadoChanged(OpcaoPacote? value)
    {
        // O valor vem preenchido com o de tabela, mas continua editável: desconto no
        // balcão é regra, não exceção.
        if (value is not null) ValorCobrado = value.Valor.ToString("0.00");
    }

    private async Task CarregarAsync()
    {
        try
        {
            Opcoes.Clear();
            foreach (var p in await _pacotes.CatalogoAsync(somenteAtivos: true))
                Opcoes.Add(new OpcaoPacote
                {
                    Id = p.Id,
                    Rotulo = p.SessoesIncluidas is { } n
                        ? $"{p.Nome} — {n} sessões — {p.Valor:C}"
                        : $"{p.Nome} — {p.Valor:C}",
                    Valor = p.Valor
                });
        }
        catch (Exception ex)
        {
            Clinica.Application.Diagnostico.Registrar("Financeiro — catálogo não pôde ser lido", ex);
            Erro(ex.Message);
        }
    }

    [RelayCommand]
    private async Task SalvarAsync()
    {
        Mensagem = string.Empty;
        MensagemEhErro = false;

        if (Seletor.Selecionado is not { } paciente)
        {
            Erro("Escolha o paciente que está comprando.");
            return;
        }

        if (PacoteSelecionado is not { } pacote)
        {
            Erro("Escolha o pacote.");
            return;
        }

        try
        {
            Salvando = true;

            decimal? valor = null;
            if (!string.IsNullOrWhiteSpace(ValorCobrado))
            {
                if (!decimal.TryParse(ValorCobrado, out var lido))
                    throw new InvalidOperationException("Não entendi o valor: use algo como 250,00.");
                valor = lido;
            }

            await _pacotes.VenderAsync(
                paciente.Id, pacote.Id, DateOnly.FromDateTime(DataCompra), valor,
                Observacoes, Environment.UserName);

            Concluido?.Invoke();
        }
        catch (Exception ex)
        {
            Clinica.Application.Diagnostico.Registrar("Financeiro — pacote não pôde ser vendido", ex);
            Erro(ex.Message);
        }
        finally
        {
            Salvando = false;
        }
    }

    private void Erro(string texto)
    {
        Mensagem = texto;
        MensagemEhErro = true;
    }
}
