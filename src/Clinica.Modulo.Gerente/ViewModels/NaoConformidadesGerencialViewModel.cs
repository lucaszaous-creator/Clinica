using System.Collections.ObjectModel;
using Clinica.Application.Modelos;
using Clinica.Application.Servicos;
using Clinica.Desktop.Controls;
using Clinica.Desktop.Shell;
using Clinica.Domain.Entities;
using Clinica.Domain.Regras;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;

namespace Clinica.Gerente.ViewModels;

/// <summary>Uma guia em não conformidade, na visão da direção.</summary>
public sealed class LinhaNaoConformidade
{
    public required int CodigoId { get; init; }
    public required string Paciente { get; init; }
    public required string Convenio { get; init; }
    public required string Guia { get; init; }
    public required string Prevista { get; init; }
    public required string Justificativa { get; init; }
    public required string Quando { get; init; }

    public static LinhaNaoConformidade De(NaoConformidadeItem n) => new()
    {
        CodigoId = n.CodigoId,
        Paciente = n.PacienteNome,
        Convenio = CatalogoConvenios.Nome(n.Convenio),
        Guia = $"{n.Tipo} · {n.Ordem}",
        Prevista = n.DataPrevista.ToString("dd/MM/yyyy"),
        Justificativa = string.IsNullOrWhiteSpace(n.Justificativa)
            ? "(sem justificativa registrada)"
            : n.Justificativa,
        Quando = n.Em is { } em ? em.ToString("dd/MM/yyyy") : "—"
    };
}

/// <summary>
/// Não conformidades na visão da DIREÇÃO — a guia que alguém decidiu que não vai ser
/// faturada, e por quê.
///
/// É a aba que mais interessa a quem dirige: cada linha aqui é uma sessão que a clínica
/// prestou e não vai receber. A JUSTIFICATIVA vem escrita por extenso, e não resumida,
/// porque o padrão delas é o que a direção precisa enxergar — dez NCs por "paciente não
/// trouxe o documento" é um problema de processo no balcão, não dez acidentes.
///
/// Reabrir devolve a guia às pendências ativas. O serviço compartilhado
/// (<see cref="RodadaPendenciasService"/>) também reabre sozinho quando o paciente volta;
/// aqui é a reabertura manual, para quando a direção decide brigar pela guia.
/// </summary>
public sealed partial class NaoConformidadesGerencialViewModel : ObservableObject
{
    private readonly IServiceScopeFactory _escopos;
    private readonly ISnackbarService _snackbar;
    private readonly IDialogoService _dialogo;

    public ObservableCollection<LinhaNaoConformidade> Itens { get; } = [];

    [ObservableProperty] private string _resumo = string.Empty;
    [ObservableProperty] private string? _mensagem;
    [ObservableProperty] private bool _mensagemEhErro;

    public bool PodeAgir => SessaoUsuario.Atual.Pode(Permissao.VerFaturamento);

    public NaoConformidadesGerencialViewModel(
        IServiceScopeFactory escopos, ISnackbarService snackbar, IDialogoService dialogo)
    {
        _escopos = escopos;
        _snackbar = snackbar;
        _dialogo = dialogo;
        _ = CarregarAsync();
    }

    [RelayCommand]
    public async Task CarregarAsync()
    {
        try
        {
            Mensagem = null;
            MensagemEhErro = false;

            using var scope = _escopos.CreateScope();
            var rodada = scope.ServiceProvider.GetRequiredService<RodadaPendenciasService>();

            Itens.Clear();
            foreach (var n in await rodada.NaoConformidadesAsync())
                Itens.Add(LinhaNaoConformidade.De(n));

            Resumo = Itens.Count == 0
                ? "Nenhuma guia em não conformidade."
                : $"{Itens.Count} guia(s) que a clínica prestou e decidiu não faturar.";
        }
        catch (Exception ex)
        {
            Clinica.Application.Diagnostico.Registrar(
                "Gerente — não conformidades não puderam ser lidas", ex);
            Erro($"Não foi possível ler as não conformidades: {ex.Message}");
        }
    }

    /// <summary>Devolve a guia às pendências ativas — a direção decidiu brigar por ela.</summary>
    [RelayCommand]
    private async Task ReabrirAsync(LinhaNaoConformidade? linha)
    {
        if (linha is null) return;

        try
        {
            SessaoUsuario.Atual.Exigir(Permissao.VerFaturamento, "reabrir a não conformidade");

            if (!_dialogo.ConfirmarPerigo("Reabrir guia",
                    $"A guia de {linha.Paciente} volta para as pendências ativas e passa a "
                    + "cobrar decisão de novo. A justificativa atual fica no histórico.")) return;

            using var scope = _escopos.CreateScope();
            var rodada = scope.ServiceProvider.GetRequiredService<RodadaPendenciasService>();
            await rodada.ReabrirNaoConformidadeAsync(linha.CodigoId, SessaoUsuario.Atual.Operador);

            _snackbar.Sucesso($"Guia de {linha.Paciente} reaberta.");
            await CarregarAsync();
        }
        catch (Exception ex)
        {
            Clinica.Application.Diagnostico.Registrar(
                "Gerente — não conformidade não pôde ser reaberta", ex);
            Erro(ex.Message);
        }
    }

    private void Erro(string mensagem)
    {
        Mensagem = mensagem;
        MensagemEhErro = true;
    }
}
