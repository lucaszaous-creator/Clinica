using System.Windows.Input;
using System.Collections.ObjectModel;
using System.IO;
using Clinica.Application.Modelos;
using Clinica.Application.Servicos;
using Clinica.Domain.Entities;
using Clinica.Domain.Regras;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Win32;

namespace Clinica.Desktop.ViewModels;

/// <summary>Uma glosa na tabela, com o prazo de recurso calculado (semáforo).</summary>
public sealed record GlosaLinha(
    CodigoFaturamento Codigo,
    string MotivoAns,
    DateOnly? PrazoRecurso,
    int? DiasParaFimPrazo,
    NivelUrgencia Urgencia)
{
    public string PrazoRotulo => PrazoRecurso is null
        ? string.Empty
        : DiasParaFimPrazo < 0
            ? $"{PrazoRecurso:dd/MM/yyyy} (vencido)"
            : $"{PrazoRecurso:dd/MM/yyyy} ({DiasParaFimPrazo} d)";
}

/// <summary>Um motivo que se repete nas glosas da clínica, com quantas vezes apareceu.</summary>
public sealed record MotivoRecorrente(string Codigo, string Descricao, int Ocorrencias, string ComoEvitar)
{
    public string Rotulo => $"{Ocorrencias}× · {Codigo} — {Descricao}";
}

/// <summary>
/// Controle de glosas: acompanha guias recusadas com o prazo de recurso no semáforo,
/// reapresenta, marca recuperadas e gera o XML de recurso de glosa (TISS).
/// </summary>
public partial class GlosasViewModel : ObservableObject, IAtalhosDeTela
{
    /// <summary>Metade VISÍVEL da permissão de mexer em glosa (reapresentar, recuperar, recurso).</summary>
    public bool PodeRegistrarGlosa => SessaoUsuario.Atual.Pode(Permissao.RegistrarGlosa);
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly Controls.IDialogoService _dialogo;

    public ObservableCollection<GlosaLinha> Glosas { get; } = new();

    /// <summary>Tudo o que o banco devolveu; <see cref="Glosas"/> é o recorte do filtro.</summary>
    private readonly List<GlosaLinha> _todas = new();

    /// <summary>
    /// Os motivos que mais glosam esta clínica, do maior para o menor. A cliente dizia
    /// nunca saber por que as guias caem; ver o próprio padrão é o primeiro passo para
    /// atacar a causa em vez de recorrer caso a caso.
    /// </summary>
    public ObservableCollection<MotivoRecorrente> MotivosRecorrentes { get; } = new();

    /// <summary>Quando true, mostra só as glosas ainda não recuperadas.</summary>
    [ObservableProperty] private bool _somenteEmAberto = true;

    // ---- Filtro (em memória: a carga traz TODAS as glosas, e é isso que dá o total
    // honesto — a caixinha "em aberto", ligada por padrão, escondia o resto sem dizer
    // que a lista era parcial).
    [ObservableProperty] private string _filtroPaciente = string.Empty;
    [ObservableProperty] private string _filtroGuia = string.Empty;

    /// <summary>Só as glosas cujo prazo de recurso JÁ passou — as que viraram decisão, não fila.</summary>
    [ObservableProperty] private bool _soPrazoVencido;

    partial void OnSomenteEmAbertoChanged(bool value) => Refiltrar();
    partial void OnFiltroPacienteChanged(string value) => Refiltrar();
    partial void OnFiltroGuiaChanged(string value) => Refiltrar();
    partial void OnSoPrazoVencidoChanged(bool value) => Refiltrar();

    /// <summary>Só os filtros NOVOS: o "em aberto" é o recorte padrão da tela, com caixinha própria.</summary>
    public bool FiltroAtivo =>
        SoPrazoVencido
        || !string.IsNullOrWhiteSpace(FiltroPaciente)
        || !string.IsNullOrWhiteSpace(FiltroGuia);

    [RelayCommand]
    private void LimparFiltro()
    {
        FiltroPaciente = string.Empty;
        FiltroGuia = string.Empty;
        SoPrazoVencido = false;
    }

    /// <summary>
    /// O resumo diz que a lista é PARCIAL — a tela vivia com o "em aberto" ligado por
    /// padrão e nada dizia que havia mais glosas atrás da caixinha.
    /// </summary>
    [ObservableProperty] private string _resumo = string.Empty;

    [ObservableProperty] private string? _mensagem;

    public GlosasViewModel(IServiceScopeFactory scopeFactory, Controls.IDialogoService dialogo)
    {
        _scopeFactory = scopeFactory;
        _dialogo = dialogo;
    }

    public Task CarregarAsync() => Buscar();

    /// <summary>
    /// Descarte de resposta fora de ordem (parcela 50): duas cargas rápidas deixariam a
    /// lista de uma sob os controles da outra — num banco remoto a resposta mais velha
    /// pode chegar por último.
    /// </summary>
    private int _geracaoCarga;

    [RelayCommand]
    private async Task Buscar()
    {
        var geracao = ++_geracaoCarga;

        using var scope = _scopeFactory.CreateScope();
        var glosas = scope.ServiceProvider.GetRequiredService<GlosaService>();
        var hoje = DateOnly.FromDateTime(DateTime.Today);

        // SEMPRE tudo, e o recorte fica em memória: é o que dá o "N de M no total" —
        // sem o M, a caixinha "em aberto" mostrava lista parcial sem dizer que era.
        var lista = await glosas.ListarAsync(somenteEmAberto: false);

        // Chegou tarde: outra carga mais nova já foi pedida.
        if (geracao != _geracaoCarga) return;

        _todas.Clear();
        foreach (var g in lista)
        {
            var dias = g.DiasParaFimRecurso(hoje);
            _todas.Add(new GlosaLinha(
                g,
                MotivosGlosa.Descricao(g.MotivoGlosaCodigo),
                g.DataLimiteRecurso,
                dias,
                dias is null ? NivelUrgencia.Verde
                    : dias <= 3 ? NivelUrgencia.Vermelho
                    : dias <= 7 ? NivelUrgencia.Amarelo
                    : NivelUrgencia.Verde));
        }

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
        Glosas.Clear();
        foreach (var g in _todas.Where(g =>
                     (!SomenteEmAberto || g.Codigo.GlosaEmAberto)
                     // null < 0 é falso: glosa sem prazo calculado não é "vencida".
                     && (!SoPrazoVencido || g.DiasParaFimPrazo < 0)
                     && Casa(g.Codigo.Atendimento?.Paciente?.Nome, FiltroPaciente)
                     && Casa(g.Codigo.NumeroGuiaReal, FiltroGuia)))
            Glosas.Add(g);

        OnPropertyChanged(nameof(FiltroAtivo));

        // O resumo diz que está filtrado — e diz o TOTAL mesmo sem filtro digitado,
        // porque o "em aberto" ligado por padrão também é um recorte.
        Resumo = FiltroAtivo
            ? $"{Glosas.Count} de {_todas.Count} glosa(s) no filtro"
            : SomenteEmAberto
                ? $"{Glosas.Count} glosa(s) em aberto de {_todas.Count} no total"
                : $"{Glosas.Count} glosa(s)";

        MontarMotivosRecorrentes();
    }

    /// <summary>Ranking dos motivos da própria clínica — o padrão que dá para atacar.</summary>
    private void MontarMotivosRecorrentes()
    {
        MotivosRecorrentes.Clear();
        // Sobre o recorte "em aberto", nunca sobre o texto digitado: o padrão é da
        // CLÍNICA, e não pode mudar porque alguém procurou a Maria.
        var ranking = _todas
            .Where(g => !SomenteEmAberto || g.Codigo.GlosaEmAberto)
            .Where(g => !string.IsNullOrWhiteSpace(g.Codigo.MotivoGlosaCodigo))
            .GroupBy(g => g.Codigo.MotivoGlosaCodigo!)
            .OrderByDescending(g => g.Count())
            .ThenBy(g => g.Key)
            .Take(3);

        foreach (var grupo in ranking)
        {
            var motivo = MotivosGlosa.Obter(grupo.Key);
            MotivosRecorrentes.Add(new MotivoRecorrente(
                grupo.Key,
                motivo?.Descricao ?? grupo.Key,
                grupo.Count(),
                motivo?.ComoEvitar ?? string.Empty));
        }
    }

    /// <summary>Abre o guia com o que cada motivo de glosa significa e como evitá-lo.</summary>
    [RelayCommand]
    private void AbrirGuia(string? codigo)
    {
        var janela = new Alertas.GuiaGlosasWindow(codigo)
        {
            Owner = System.Windows.Application.Current.MainWindow
        };
        janela.ShowDialog();
    }

    [RelayCommand]
    private async Task Reapresentar(GlosaLinha? linha)
    {
        if (linha is null) return;
        try
        {
            SessaoUsuario.Atual.Exigir(Permissao.RegistrarGlosa, "reapresentar a guia glosada");

            using (var scope = _scopeFactory.CreateScope())
            {
                var glosas = scope.ServiceProvider.GetRequiredService<GlosaService>();
                await glosas.ReapresentarAsync(linha.Codigo.Id, DateOnly.FromDateTime(DateTime.Today), SessaoUsuario.Atual.Operador);
            }
            await Buscar();
        }
        catch (Exception ex)
        {
            Mensagem = $"Não foi possível reapresentar: {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task Recuperar(GlosaLinha? linha)
    {
        if (linha is null) return;

        SessaoUsuario.Atual.Exigir(Permissao.RegistrarGlosa, "marcar a glosa como recuperada");

        if (!_dialogo.Confirmar("Confirmar",
            "Marcar esta glosa como recuperada (aceita pelo convênio)?")) return;

        try
        {
            using (var scope = _scopeFactory.CreateScope())
            {
                var glosas = scope.ServiceProvider.GetRequiredService<GlosaService>();
                await glosas.MarcarRecuperadaAsync(linha.Codigo.Id, SessaoUsuario.Atual.Operador);
            }
            await Buscar();
        }
        catch (Exception ex)
        {
            Mensagem = $"Não foi possível marcar como recuperada: {ex.Message}";
        }
    }

    /// <summary>Gera o XML de recurso de glosa (TISS) com todas as glosas em aberto da lista.</summary>
    [RelayCommand]
    private async Task GerarRecursoXml()
    {
        var emAberto = Glosas.Where(l => l.Codigo.GlosaEmAberto).Select(l => l.Codigo).ToList();
        if (emAberto.Count == 0)
        {
            Mensagem = "Nenhuma glosa em aberto para recorrer.";
            return;
        }

        try
        {
            string xml;
            using (var scope = _scopeFactory.CreateScope())
            {
                var tiss = scope.ServiceProvider.GetRequiredService<TissExportService>();
                var parametros = scope.ServiceProvider.GetRequiredService<ParametrosService>();
                var dados = await parametros.ObterPrestadorAsync();
                var numero = DateTime.Now.ToString("yyyyMMddHHmm"); // identificador do recurso
                xml = tiss.GerarRecursoGlosaXml(emAberto, dados, numero);
            }

            var dialog = new SaveFileDialog
            {
                FileName = $"TISS-Recurso-Glosa-{DateTime.Today:yyyy-MM-dd}.xml",
                Filter = "Arquivo TISS (*.xml)|*.xml",
                DefaultExt = ".xml"
            };
            if (dialog.ShowDialog() != true) return;

            await File.WriteAllTextAsync(dialog.FileName, xml);
            Mensagem = $"Recurso de glosa gerado com {emAberto.Count} guia(s): {dialog.FileName}";
        }
        catch (Exception ex)
        {
            Mensagem = $"Não foi possível gerar o recurso: {ex.Message}";
        }
    }

    // Atalhos globais do shell (IAtalhosDeTela)
    public ICommand? AtalhoAtualizar => BuscarCommand;
    public ICommand? AtalhoImprimir => GerarRecursoXmlCommand;
}
