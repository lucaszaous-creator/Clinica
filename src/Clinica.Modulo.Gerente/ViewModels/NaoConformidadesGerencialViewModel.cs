using Clinica.Domain;
using System.Collections.ObjectModel;
using Clinica.Application.Modelos;
using Clinica.Application.Servicos;
using Clinica.Desktop.Controls;
using Clinica.Desktop.Shell;
using Clinica.Desktop.Shell.Componentes;
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
        Convenio = CatalogoConvenios.Nome(n.ConvenioCodigo, n.Convenio),
        Guia = $"{RotulosEnum.De(n.Tipo)} · {RotulosEnum.De(n.Ordem)}",
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

    /// <summary>Tudo o que o banco devolveu; <see cref="Itens"/> é o recorte do filtro.</summary>
    private readonly List<LinhaNaoConformidade> _todas = [];

    // ---- Filtro (em memória, sobre o que já foi lido). A pergunta da direção aqui é o
    // PADRÃO das justificativas — e padrão se acha estreitando: todas as NCs de um
    // convênio, todas as que citam "documento".
    public const string TodosConvenios = "Todos os convênios";

    /// <summary>As operadoras COM NC — oferecer as sem linha daria filtro que só leva a vazio.</summary>
    public ObservableCollection<string> ConveniosNc { get; } = [TodosConvenios];

    [ObservableProperty] private string _filtroConvenioNc = TodosConvenios;

    /// <summary>UM campo para paciente e justificativa: quem procura o padrão não sabe em qual dos dois o termo está.</summary>
    [ObservableProperty] private string _filtroBuscaNc = string.Empty;

    /// <summary>O `Clear()` do combo devolve nulo pelo binding (lição da parcela 56) — remonta sob guarda.</summary>
    private bool _montandoConvenios;

    partial void OnFiltroBuscaNcChanged(string value) => Refiltrar();
    partial void OnFiltroConvenioNcChanged(string value)
    {
        if (value is null)
        {
            FiltroConvenioNc = TodosConvenios;
            return;
        }
        if (!_montandoConvenios) Refiltrar();
    }

    public bool FiltroAtivo =>
        FiltroConvenioNc != TodosConvenios || !string.IsNullOrWhiteSpace(FiltroBuscaNc);

    [RelayCommand]
    private void LimparFiltro()
    {
        FiltroConvenioNc = TodosConvenios;
        FiltroBuscaNc = string.Empty;
    }

    /// <summary>O estado vazio muda de frase quando há filtro — "não há NC" e "nenhuma bate com o filtro" são respostas diferentes.</summary>
    [ObservableProperty] private string _vazioDescricao = "Nenhuma guia em não conformidade.";

    [ObservableProperty] private string _resumo = string.Empty;
    [ObservableProperty] private string? _mensagem;
    [ObservableProperty] private bool _mensagemEhErro;

    public bool PodeAgir => SessaoUsuario.Atual.Pode(Permissao.MarcarNaoConformidade);

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

            // Entre o `Clear()` e o último `Add` não pode haver `await` (parcela 62):
            // reabrir uma NC recarrega no fim, e uma recarga que comece com outra no ar
            // deixaria a lista com linhas repetidas ou faltando.
            var linhas = (await rodada.NaoConformidadesAsync())
                .Select(LinhaNaoConformidade.De).ToList();

            _todas.Clear();
            _todas.AddRange(linhas);

            // As operadoras com NC, preservando a escolha quando ela ainda existe —
            // atualizar a lista não pode desfazer o filtro de quem está trabalhando nela.
            var escolhido = FiltroConvenioNc;
            _montandoConvenios = true;
            try
            {
                ConveniosNc.Clear();
                ConveniosNc.Add(TodosConvenios);
                foreach (var nome in _todas.Select(l => l.Convenio).Distinct().OrderBy(n => n))
                    ConveniosNc.Add(nome);
                FiltroConvenioNc = ConveniosNc.Contains(escolhido) ? escolhido : TodosConvenios;
            }
            finally
            {
                _montandoConvenios = false;
            }

            Refiltrar();
        }
        catch (Exception ex)
        {
            Clinica.Application.Diagnostico.Registrar(
                "Gerente — não conformidades não puderam ser lidas", ex);
            Erro($"Não foi possível ler as não conformidades: {ex.Message}");
        }
    }

    /// <summary>
    /// Aplica o filtro sobre o que já foi lido — em memória, sem ida ao banco (o padrão
    /// da tela de Consultas da Recepção). A busca casa em paciente OU justificativa.
    /// </summary>
    private void Refiltrar()
    {
        Itens.Clear();
        foreach (var l in _todas.Where(l =>
                     (FiltroConvenioNc == TodosConvenios || l.Convenio == FiltroConvenioNc)
                     && (Busca.Casa(l.Paciente, FiltroBuscaNc)
                         || Busca.Casa(l.Justificativa, FiltroBuscaNc))))
            Itens.Add(l);

        OnPropertyChanged(nameof(FiltroAtivo));

        // O resumo DIZ que está filtrado: "4 guias" e "4 de 17 no filtro" respondem
        // perguntas diferentes.
        Resumo = _todas.Count == 0
            ? "Nenhuma guia em não conformidade."
            : FiltroAtivo
                ? $"{Itens.Count} de {_todas.Count} guia(s) no filtro."
                : $"{Itens.Count} guia(s) que a clínica prestou e decidiu não faturar.";

        VazioDescricao = FiltroAtivo
            ? "Nenhuma não conformidade bate com o filtro — limpe-o para ver todas."
            : "Nenhuma guia em não conformidade.";
    }

    /// <summary>Devolve a guia às pendências ativas — a direção decidiu brigar por ela.</summary>
    [RelayCommand]
    private async Task ReabrirAsync(LinhaNaoConformidade? linha)
    {
        if (linha is null) return;

        try
        {
            SessaoUsuario.Atual.Exigir(Permissao.MarcarNaoConformidade, "reabrir a não conformidade");

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
