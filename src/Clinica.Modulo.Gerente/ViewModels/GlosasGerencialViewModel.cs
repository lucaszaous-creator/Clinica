using System.Collections.ObjectModel;
using Clinica.Application.Servicos;
using Clinica.Desktop.Controls;
using Clinica.Desktop.Shell;
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
            PodeRecuperar = c.Glosa == StatusGlosa.Reapresentada
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

    [ObservableProperty]
    private bool _somenteEmAberto = true;

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

    partial void OnSomenteEmAbertoChanged(bool value) => _ = CarregarAsync();

    /// <summary>
    /// Número da carga mais recente pedida — descarte de resposta fora de ordem (parcela 50).
    /// Alternar "somente em aberto" dispara outra leitura; a resposta velha chegando por
    /// último devolveria à tela glosas encerradas com o filtro dizendo o contrário.
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
            var lista = await glosas.ListarAsync(SomenteEmAberto);

            // Chegou tarde: outra carga mais nova já foi pedida.
            if (geracao != _geracaoCarga) return;

            Glosas.Clear();
            foreach (var c in lista) Glosas.Add(LinhaGlosa.De(c, hoje));

            var vencidas = Glosas.Count(g => g.PrazoVencido);
            Resumo = Glosas.Count == 0
                ? "Nenhuma glosa no filtro atual."
                : vencidas == 0
                    ? $"{Glosas.Count} glosa(s), nenhuma com prazo vencido."
                    : $"{Glosas.Count} glosa(s) · {vencidas} com prazo de recurso VENCIDO.";
        }
        catch (Exception ex)
        {
            // Chegou tarde: outra carga mais nova já foi pedida.
            if (geracao != _geracaoCarga) return;

            Clinica.Application.Diagnostico.Registrar("Gerente — glosas não puderam ser lidas", ex);
            Erro($"Não foi possível ler as glosas: {ex.Message}");
        }
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
