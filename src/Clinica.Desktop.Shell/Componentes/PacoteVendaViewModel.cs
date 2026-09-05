using Clinica.Domain;
using Clinica.Domain.Entities;
using System.Collections.ObjectModel;
using Clinica.Application.Servicos;
using Clinica.Desktop.Shell;
using Clinica.Desktop.Shell.Componentes;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;

namespace Clinica.Desktop.Shell.Componentes;

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
    private readonly IServiceScopeFactory _escopos;

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

    public PacoteVendaViewModel(IServiceScopeFactory escopos)
    {
        _escopos = escopos;
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
            // Monta e só ENTÃO publica: entre o Clear e o último Add não pode haver await.
            using var escopo = _escopos.CreateScope();
            var catalogo = await escopo.ServiceProvider
                .GetRequiredService<PacoteService>().CatalogoAsync(somenteAtivos: true);

            Opcoes.Clear();
            foreach (var p in catalogo)
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

        // A porta (o "Vender…" da tela de trás) já exige; a JANELA não exigia — e é na
        // janela que se grava (a lição da parcela 54: a segunda barreira vale mais onde
        // se escreve). Os bits são os de `PacotesViewModel.PodeVender`.
        try
        {
            SessaoUsuario.Atual.ExigirAlgum(
                Permissao.VenderPacote | Permissao.EditarFinanceiro, "vender pacote");
        }
        catch (Exception ex)
        {
            Erro(ex.Message);
            return;
        }

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

            using (var escopo = _escopos.CreateScope())
                await escopo.ServiceProvider.GetRequiredService<PacoteService>()
                    .VenderAsync(
                        paciente.Id, pacote.Id, DateOnly.FromDateTime(DataCompra), valor,
                        Observacoes, SessaoUsuario.Atual.Operador);

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
