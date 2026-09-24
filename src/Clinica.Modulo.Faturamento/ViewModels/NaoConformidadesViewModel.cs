using Clinica.Domain.Entities;
using System.Collections.ObjectModel;
using System.Windows.Input;
using Clinica.Application.Modelos;
using Clinica.Application.Servicos;
using Clinica.Desktop.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;

namespace Clinica.Desktop.ViewModels;

/// <summary>
/// Aba NC: lista todas as não conformidades (guias que, ao rodar as pendências, não puderam ser
/// baixadas e foram justificadas). Permite ler a justificativa e reabrir (voltar a ser pendência)
/// quando aparece uma solução.
/// </summary>
public partial class NaoConformidadesViewModel : ObservableObject, IAtalhosDeTela
{
    /// <summary>Metade VISÍVEL da permissão: reabrir devolve a guia à cobrança do painel.</summary>
    public bool PodeMarcarNaoConformidade => SessaoUsuario.Atual.Pode(Permissao.MarcarNaoConformidade);
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IDialogoService _dialogo;

    public ObservableCollection<NaoConformidadeItem> Itens { get; } = new();

    /// <summary>Tudo o que o banco devolveu; <see cref="Itens"/> é o recorte do filtro.</summary>
    private readonly List<NaoConformidadeItem> _todas = new();

    // ---- Filtro (em memória). Um campo só, que casa no PACIENTE e na JUSTIFICATIVA:
    // quem procura tanto lembra o nome quanto lembra o motivo ("portal fora do ar"),
    // e dois campos obrigariam a saber de antemão onde a palavra está.
    [ObservableProperty] private string _filtroTexto = string.Empty;

    partial void OnFiltroTextoChanged(string value) => Refiltrar();

    public bool FiltroAtivo => !string.IsNullOrWhiteSpace(FiltroTexto);

    [RelayCommand]
    private void LimparFiltro() => FiltroTexto = string.Empty;

    /// <summary>O resumo diz que está filtrado: "3 de 12 no filtro" e "12 NCs" respondem perguntas diferentes.</summary>
    [ObservableProperty] private string _resumo = string.Empty;

    /// <summary>O estado vazio muda de frase quando há filtro — "nada justificado" e "nenhuma bate com o filtro" são respostas diferentes.</summary>
    [ObservableProperty] private string _vazioDescricao =
        "Nada justificado como não conformidade até agora.";

    [ObservableProperty] private bool _temItens;

    public NaoConformidadesViewModel(IServiceScopeFactory scopeFactory, IDialogoService dialogo)
    {
        _scopeFactory = scopeFactory;
        _dialogo = dialogo;
    }

    public async Task CarregarAsync()
    {
        using var scope = _scopeFactory.CreateScope();
        var rodada = scope.ServiceProvider.GetRequiredService<RodadaPendenciasService>();

        _todas.Clear();
        foreach (var n in await rodada.NaoConformidadesAsync())
            _todas.Add(n);

        Refiltrar();
    }

    /// <summary>
    /// Mesma comparação da consulta de guias (repositório): minúsculas + Contains. Este
    /// app não referencia o Shell, então o <c>Busca.Casa</c> da suíte não existe aqui.
    /// </summary>
    private static bool Casa(string? texto, string? termo)
        => string.IsNullOrWhiteSpace(termo)
           || (texto is not null && texto.ToLower().Contains(termo.Trim().ToLower()));

    /// <summary>
    /// Aplica o filtro sobre o que já foi lido — em memória, sem ida ao banco.
    /// </summary>
    private void Refiltrar()
    {
        Itens.Clear();
        foreach (var n in _todas.Where(n =>
                     Casa(n.PacienteNome, FiltroTexto) || Casa(n.Justificativa, FiltroTexto)))
            Itens.Add(n);

        TemItens = Itens.Count > 0;
        OnPropertyChanged(nameof(FiltroAtivo));

        Resumo = FiltroAtivo
            ? $"{Itens.Count} de {_todas.Count} não conformidade(s) no filtro"
            : $"{_todas.Count} não conformidade(s)";

        VazioDescricao = FiltroAtivo
            ? "Nenhuma não conformidade bate com o filtro — limpe-o para ver todas."
            : "Nada justificado como não conformidade até agora.";
    }

    /// <summary>Mostra a justificativa completa da não conformidade (opção de leitura).</summary>
    [RelayCommand]
    private void VerJustificativa(NaoConformidadeItem? item)
    {
        if (item is null) return;
        _dialogo.Aviso($"Não conformidade — {item.PacienteNome}",
            string.IsNullOrWhiteSpace(item.Justificativa) ? "(sem justificativa registrada)" : item.Justificativa);
    }

    /// <summary>Reabre a não conformidade: a guia volta a ser pendência ativa e pode ser baixada.</summary>
    [RelayCommand]
    private async Task Reabrir(NaoConformidadeItem? item)
    {
        if (item is null) return;

        SessaoUsuario.Atual.Exigir(Permissao.MarcarNaoConformidade, "reabrir a não conformidade");

        if (!_dialogo.Confirmar("Reabrir não conformidade",
                $"Reabrir a guia de {item.PacienteNome}? Ela volta a ser pendência ativa (aba Pendências).")) return;

        try
        {
            using var scope = _scopeFactory.CreateScope();
            var rodada = scope.ServiceProvider.GetRequiredService<RodadaPendenciasService>();
            await rodada.ReabrirNaoConformidadeAsync(item.CodigoId, SessaoUsuario.Atual.Operador);
        }
        catch (Exception ex)
        {
            _dialogo.Aviso("Reabrir não conformidade", ex.Message);
        }

        await CarregarAsync();
    }

    [RelayCommand]
    private Task Atualizar() => CarregarAsync();

    // Atalhos globais do shell (IAtalhosDeTela)
    public ICommand? AtalhoAtualizar => AtualizarCommand;
}
