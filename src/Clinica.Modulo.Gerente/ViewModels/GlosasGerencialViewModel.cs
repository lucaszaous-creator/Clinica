using System.Collections.ObjectModel;
using Clinica.Application.Servicos;
using Clinica.Desktop.Controls;
using Clinica.Desktop.Shell;
using Clinica.Desktop.Shell.Componentes;
using Clinica.Domain;
using Clinica.Domain.Entities;
using Clinica.Domain.Regras;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;

namespace Clinica.Gerente.ViewModels;

/// <summary>Uma guia glosada na visão da direção.</summary>
public sealed class LinhaGlosa
{
    public required int CodigoId { get; init; }
    public required string Paciente { get; init; }
    public required string Convenio { get; init; }
    public required string Guia { get; init; }
    public required string DataGlosa { get; init; }
    public required string Motivo { get; init; }
    public required string Situacao { get; init; }
    public required string Prazo { get; init; }

    /// <summary>Prazo de recurso vencido — passou a hora de recorrer, e o dinheiro se perdeu.</summary>
    public required bool PrazoVencido { get; init; }

    /// <summary>Só glosa em aberto aceita reapresentação.</summary>
    public required bool PodeReapresentar { get; init; }

    /// <summary>Só reapresentada pode ser marcada como recuperada.</summary>
    public required bool PodeRecuperar { get; init; }

    /// <summary>
    /// Ainda em aberto (glosada ou reapresentada) — o recorte da checkbox. Deriva do
    /// MESMO critério do repositório, para o filtro em memória não discordar do que o
    /// banco devolvia quando era ele quem filtrava.
    /// </summary>
    public required bool EmAberto { get; init; }

    public static LinhaGlosa De(CodigoFaturamento c, DateOnly hoje)
    {
        var limite = c.DataLimiteRecurso;
        return new LinhaGlosa
        {
            CodigoId = c.Id,
            Paciente = c.Atendimento?.Paciente?.Nome ?? "(paciente removido)",
            Convenio = c.Atendimento?.Paciente is { } p
                ? p.ConvenioNome
                : "—",
            Guia = string.IsNullOrWhiteSpace(c.NumeroGuiaReal)
                ? $"{RotulosEnum.De(c.Tipo)} · {RotulosEnum.De(c.Ordem)}"
                : c.NumeroGuiaReal!,
            DataGlosa = c.DataGlosa?.ToString("dd/MM/yyyy") ?? "—",
            Motivo = string.IsNullOrWhiteSpace(c.MotivoGlosa)
                ? (string.IsNullOrWhiteSpace(c.MotivoGlosaCodigo) ? "sem motivo registrado" : c.MotivoGlosaCodigo!)
                : c.MotivoGlosa!,
            Situacao = RotulosEnum.De(c.Glosa),
            Prazo = limite is null
                ? "sem prazo registrado"
                : limite < hoje
                    ? $"venceu em {limite:dd/MM/yyyy}"
                    : $"até {limite:dd/MM/yyyy}",
            // Vencido só conta para o que AINDA dá para recorrer: glosa já recuperada
            // com prazo passado não é problema, é caso encerrado.
            PrazoVencido = limite is { } l && l < hoje && c.Glosa == StatusGlosa.Glosada,
            PodeReapresentar = c.Glosa == StatusGlosa.Glosada,
            PodeRecuperar = c.Glosa == StatusGlosa.Reapresentada,
            EmAberto = c.Glosa is StatusGlosa.Glosada or StatusGlosa.Reapresentada
        };
    }
}

/// <summary>
/// Glosas na visão da DIREÇÃO — a segunda aba do "Faturamento (TISS)".
///
/// Glosa é faturamento recusado: dinheiro que a clínica já trabalhou e não recebeu. O
/// prazo de recurso é o que decide se ainda dá para brigar, e por isso ele vem escrito em
/// cada linha e destacado quando venceu — uma glosa que passou do prazo não é pendência,
/// é prejuízo consumado, e confundir as duas faz a direção perseguir o caso errado.
///
/// Trabalha sobre o <see cref="GlosaService"/> compartilhado, que já aplica o prazo
/// configurado e grava a auditoria. `Clinica.Desktop` não é tocado.
/// </summary>
public sealed partial class GlosasGerencialViewModel : ObservableObject
{
    private readonly IServiceScopeFactory _escopos;
    private readonly ISnackbarService _snackbar;
    private readonly IDialogoService _dialogo;

    public ObservableCollection<LinhaGlosa> Glosas { get; } = [];

    /// <summary>Tudo o que o banco devolveu; <see cref="Glosas"/> é o recorte do filtro.</summary>
    private readonly List<LinhaGlosa> _todas = [];

    // ---- Filtro (em memória, sobre o que já foi lido). A carga traz SEMPRE tudo e a
    // checkbox recorta aqui: é o que faz o total honesto existir — "9 glosa(s)" com o
    // "só em aberto" ligado escondia as encerradas do número, e a direção lia o recorte
    // como se fosse o todo.
    [ObservableProperty]
    private bool _somenteEmAberto = true;

    /// <summary>Só o que ainda dava para recorrer e passou da hora — o prejuízo consumado.</summary>
    [ObservableProperty] private bool _soPrazoVencido;

    [ObservableProperty] private string _filtroPacienteGlosa = string.Empty;

    partial void OnSoPrazoVencidoChanged(bool value) => Refiltrar();
    partial void OnFiltroPacienteGlosaChanged(string value) => Refiltrar();

    public bool FiltroAtivo =>
        SomenteEmAberto || SoPrazoVencido || !string.IsNullOrWhiteSpace(FiltroPacienteGlosa);

    [RelayCommand]
    private void LimparFiltro()
    {
        SomenteEmAberto = false;
        SoPrazoVencido = false;
        FiltroPacienteGlosa = string.Empty;
    }

    /// <summary>
    /// O estado vazio muda de frase quando há filtro: "nenhuma glosa registrada" e
    /// "nenhuma bate com o filtro" são respostas diferentes (a lição da lista de espera,
    /// parcela 25).
    /// </summary>
    [ObservableProperty] private string _vazioDescricao = "Nenhuma glosa registrada.";

    [ObservableProperty] private string _resumo = string.Empty;
    [ObservableProperty] private string? _mensagem;
    [ObservableProperty] private bool _mensagemEhErro;

    public bool PodeAgir => SessaoUsuario.Atual.Pode(Permissao.RegistrarGlosa);

    public GlosasGerencialViewModel(
        IServiceScopeFactory escopos, ISnackbarService snackbar, IDialogoService dialogo)
    {
        _escopos = escopos;
        _snackbar = snackbar;
        _dialogo = dialogo;
        _ = CarregarAsync();
    }

    // A checkbox só refiltra: a lista completa já está na memória, e recarregar do banco
    // a cada clique pagaria o banco remoto para responder o que a tela já sabe.
    partial void OnSomenteEmAbertoChanged(bool value) => Refiltrar();

    /// <summary>
    /// Número da carga mais recente pedida — descarte de resposta fora de ordem (parcela 50).
    /// Dois "Atualizar" seguidos disparam duas leituras; a resposta velha chegando por
    /// último sobrescreveria a mais nova.
    /// </summary>
    private int _geracaoCarga;

    [RelayCommand]
    public async Task CarregarAsync()
    {
        var geracao = ++_geracaoCarga;

        try
        {
            Mensagem = null;
            MensagemEhErro = false;

            using var scope = _escopos.CreateScope();
            var glosas = scope.ServiceProvider.GetRequiredService<GlosaService>();

            var hoje = DateOnly.FromDateTime(DateTime.Today);

            // Sempre TUDO (somenteEmAberto: false): a checkbox recorta em memória, e o
            // total completo é o que permite ao resumo dizer "N de M" em vez de
            // apresentar o recorte como se fosse o todo.
            var lista = await glosas.ListarAsync(somenteEmAberto: false);

            // Chegou tarde: outra carga mais nova já foi pedida.
            if (geracao != _geracaoCarga) return;

            _todas.Clear();
            foreach (var c in lista) _todas.Add(LinhaGlosa.De(c, hoje));

            Refiltrar();
        }
        catch (Exception ex)
        {
            // Chegou tarde: outra carga mais nova já foi pedida.
            if (geracao != _geracaoCarga) return;

            Clinica.Application.Diagnostico.Registrar("Gerente — glosas não puderam ser lidas", ex);
            Erro($"Não foi possível ler as glosas: {ex.Message}");
        }
    }

    /// <summary>
    /// Aplica o filtro sobre o que já foi lido — em memória, sem ida ao banco (o padrão
    /// da tela de Consultas da Recepção).
    /// </summary>
    private void Refiltrar()
    {
        Glosas.Clear();
        foreach (var g in _todas.Where(g =>
                     (!SomenteEmAberto || g.EmAberto)
                     && (!SoPrazoVencido || g.PrazoVencido)
                     && Busca.Casa(g.Paciente, FiltroPacienteGlosa)))
            Glosas.Add(g);

        OnPropertyChanged(nameof(FiltroAtivo));

        // O resumo DIZ que está filtrado: "9 glosas" e "9 de 23 no filtro" respondem
        // perguntas diferentes, e a checkbox nasce ligada — sem o "de M" a direção leria
        // o recorte como o tamanho do problema.
        var vencidas = Glosas.Count(g => g.PrazoVencido);
        Resumo = _todas.Count == 0
            ? "Nenhuma glosa registrada."
            : FiltroAtivo
                ? $"{Glosas.Count} de {_todas.Count} glosa(s) no filtro"
                  + (vencidas > 0 ? $" · {vencidas} com prazo de recurso VENCIDO." : ".")
                : vencidas == 0
                    ? $"{Glosas.Count} glosa(s), nenhuma com prazo vencido."
                    : $"{Glosas.Count} glosa(s) · {vencidas} com prazo de recurso VENCIDO.";

        VazioDescricao = FiltroAtivo
            ? "Nenhuma glosa bate com o filtro — limpe-o para ver todas."
            : "Nenhuma glosa registrada.";
    }

    /// <summary>Reapresenta a guia ao convênio — o recurso propriamente dito.</summary>
    [RelayCommand]
    private async Task ReapresentarAsync(LinhaGlosa? linha)
        => await AgirAsync(linha, async (glosas, l) =>
        {
            if (l.PrazoVencido && !_dialogo.ConfirmarPerigo("Prazo vencido",
                    $"O prazo de recurso da guia de {l.Paciente} já venceu ({l.Prazo}). "
                    + "A operadora provavelmente vai recusar. Reapresentar mesmo assim?"))
                return null;

            await glosas.ReapresentarAsync(
                l.CodigoId, DateOnly.FromDateTime(DateTime.Today), SessaoUsuario.Atual.Operador);
            return $"Guia de {l.Paciente} reapresentada.";
        });

    /// <summary>O convênio aceitou o recurso: a glosa virou faturamento de volta.</summary>
    [RelayCommand]
    private async Task RecuperarAsync(LinhaGlosa? linha)
        => await AgirAsync(linha, async (glosas, l) =>
        {
            await glosas.MarcarRecuperadaAsync(l.CodigoId, SessaoUsuario.Atual.Operador);
            return $"Glosa de {l.Paciente} recuperada.";
        });

    /// <summary>
    /// Envelope das duas ações. Devolver null significa "o usuário desistiu no diálogo" —
    /// diferente de erro, e por isso sem mensagem.
    /// </summary>
    private async Task AgirAsync(
        LinhaGlosa? linha, Func<GlosaService, LinhaGlosa, Task<string?>> acao)
    {
        if (linha is null) return;

        try
        {
            SessaoUsuario.Atual.Exigir(Permissao.RegistrarGlosa, "mexer na glosa");

            using var scope = _escopos.CreateScope();
            var glosas = scope.ServiceProvider.GetRequiredService<GlosaService>();

            var ok = await acao(glosas, linha);
            if (ok is null) return;

            _snackbar.Sucesso(ok);
            await CarregarAsync();
        }
        catch (Exception ex)
        {
            Clinica.Application.Diagnostico.Registrar("Gerente — ação de glosa falhou", ex);
            Erro(ex.Message);
        }
    }

    private void Erro(string mensagem)
    {
        Mensagem = mensagem;
        MensagemEhErro = true;
    }
}
