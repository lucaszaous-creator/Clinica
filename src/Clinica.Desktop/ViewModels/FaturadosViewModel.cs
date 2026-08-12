using System.Windows.Input;
using System.Collections.ObjectModel;
using System.IO;
using System.Text;
using System.Windows;
using Clinica.Application.Servicos;
using Clinica.Desktop.Alertas;
using Clinica.Domain;
using Clinica.Domain.Entities;
using Clinica.Domain.Regras;
using Clinica.Infrastructure;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Win32;

namespace Clinica.Desktop.ViewModels;

/// <summary>Guias já baixadas no período, com a opção de ESTORNAR (reabrir a pendência).</summary>
public partial class FaturadosViewModel : ObservableObject, IAtalhosDeTela
{
    /// <summary>Metade VISÍVEL da permissão: desfazer a baixa apaga o trabalho de outra pessoa.</summary>
    public bool PodeEstornar => SessaoUsuario.Atual.Pode(Permissao.EstornarBaixa);

    /// <summary>Metade VISÍVEL da permissão: anotar glosa afirma que a operadora recusou, e isso chega ao caixa.</summary>
    public bool PodeGlosar => SessaoUsuario.Atual.Pode(Permissao.RegistrarGlosa);
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly Controls.IDialogoService _dialogo;

    public ObservableCollection<CodigoFaturamento> Baixados { get; } = new();

    /// <summary>Tudo o que o período devolveu; <see cref="Baixados"/> é o recorte do filtro.</summary>
    private readonly List<CodigoFaturamento> _todos = new();

    // ---- Filtro (em memória sobre o período carregado: refazer a consulta a cada
    // tecla seria pagar o banco remoto para responder o que a tela já sabe).
    public const string TodosConvenios = "Todos os convênios";

    /// <summary>
    /// As operadoras DO PERÍODO carregado, pelo nome que a coluna já mostra — oferecer
    /// as sem guia daria filtro que só leva a vazio. Nome único no repositório: a
    /// checagem 20 casa o ItemsSource pelo NOME num mapa global.
    /// </summary>
    public ObservableCollection<string> ConveniosDoPeriodo { get; } = new() { TodosConvenios };

    [ObservableProperty] private string _filtroPaciente = string.Empty;
    [ObservableProperty] private string _filtroGuia = string.Empty;
    [ObservableProperty] private string _filtroConvenio = TodosConvenios;

    /// <summary>O `Clear()` do combo devolve nulo pelo binding (lição da parcela 56) — remonta sob guarda.</summary>
    private bool _montandoConvenios;

    partial void OnFiltroPacienteChanged(string value) => Refiltrar();
    partial void OnFiltroGuiaChanged(string value) => Refiltrar();
    partial void OnFiltroConvenioChanged(string? value)
    {
        if (value is null)
        {
            FiltroConvenio = TodosConvenios;
            return;
        }
        if (!_montandoConvenios) Refiltrar();
    }

    public bool FiltroAtivo =>
        FiltroConvenio != TodosConvenios
        || !string.IsNullOrWhiteSpace(FiltroPaciente)
        || !string.IsNullOrWhiteSpace(FiltroGuia);

    [RelayCommand]
    private void LimparFiltro()
    {
        FiltroPaciente = string.Empty;
        FiltroGuia = string.Empty;
        FiltroConvenio = TodosConvenios;
    }

    /// <summary>O resumo diz que está filtrado: "12 de 90 no período" e "90 guias" respondem perguntas diferentes.</summary>
    [ObservableProperty] private string _resumo = string.Empty;

    [ObservableProperty] private DateTime _inicio;
    [ObservableProperty] private DateTime _fim;
    [ObservableProperty] private string? _mensagem;

    public FaturadosViewModel(IServiceScopeFactory scopeFactory, Controls.IDialogoService dialogo)
    {
        _scopeFactory = scopeFactory;
        _dialogo = dialogo;
        var hoje = DateTime.Today;
        _inicio = new DateTime(hoje.Year, hoje.Month, 1);
        _fim = _inicio.AddMonths(1).AddDays(-1);
    }

    public Task CarregarAsync() => Buscar();

    [RelayCommand]
    private async Task Buscar()
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ClinicaDbContext>();
        var lista = await db.Codigos
            .Include(c => c.Atendimento!).ThenInclude(a => a.Paciente!)
            .Where(c => c.DataBaixa != null
                        && c.DataBaixa >= DateOnly.FromDateTime(Inicio)
                        && c.DataBaixa <= DateOnly.FromDateTime(Fim))
            .OrderByDescending(c => c.DataBaixa)
            .ToListAsync();

        _todos.Clear();
        foreach (var c in lista) _todos.Add(c);

        // As operadoras do período, preservando a escolha quando ela ainda existe —
        // atualizar o período não pode desfazer o filtro de quem está trabalhando nele.
        var escolhido = FiltroConvenio;
        _montandoConvenios = true;
        try
        {
            ConveniosDoPeriodo.Clear();
            ConveniosDoPeriodo.Add(TodosConvenios);
            foreach (var nome in _todos.Select(NomeConvenio).Distinct().OrderBy(n => n))
                ConveniosDoPeriodo.Add(nome);
            FiltroConvenio = ConveniosDoPeriodo.Contains(escolhido) ? escolhido : TodosConvenios;
        }
        finally
        {
            _montandoConvenios = false;
        }

        Refiltrar();
    }

    /// <summary>O nome como a COLUNA já mostra (conversor <c>EnumDescricao</c> → <see cref="ConvenioInfo"/>) — o combo tem de casar com o que o olho lê na linha.</summary>
    private static string NomeConvenio(CodigoFaturamento c)
        => ConvenioInfo.NomeExibicao(c.Atendimento?.Paciente?.Convenio ?? default);

    /// <summary>
    /// Mesma comparação da consulta de guias (repositório): minúsculas + Contains. Este
    /// app não referencia o Shell, então o <c>Busca.Casa</c> da suíte não existe aqui.
    /// </summary>
    private static bool Casa(string? texto, string? termo)
        => string.IsNullOrWhiteSpace(termo)
           || (texto is not null && texto.ToLower().Contains(termo.Trim().ToLower()));

    /// <summary>
    /// Aplica o filtro sobre o período já lido — em memória, sem ida ao banco.
    /// </summary>
    private void Refiltrar()
    {
        Baixados.Clear();
        foreach (var c in _todos.Where(c =>
                     (FiltroConvenio == TodosConvenios || NomeConvenio(c) == FiltroConvenio)
                     && Casa(c.Atendimento?.Paciente?.Nome, FiltroPaciente)
                     && Casa(c.NumeroGuiaReal, FiltroGuia)))
            Baixados.Add(c);

        OnPropertyChanged(nameof(FiltroAtivo));

        Resumo = FiltroAtivo
            ? $"{Baixados.Count} de {_todos.Count} guia(s) no filtro"
            : $"{_todos.Count} guia(s) no período";
    }

    [RelayCommand]
    private async Task Estornar(CodigoFaturamento? codigo)
    {
        if (codigo is null) return;

        // Antes da confirmação, nunca depois: perguntar "confirmar estorno?" e só então
        // recusar por permissão é fazer a pessoa decidir sobre algo que ela não pode fazer.
        SessaoUsuario.Atual.Exigir(Permissao.EstornarBaixa, "estornar a baixa da guia");

        if (!_dialogo.Confirmar("Confirmar estorno",
            $"Estornar a baixa desta guia de {codigo.Atendimento?.Paciente?.Nome}?\n\n" +
            "A pendência voltará a aparecer no painel para ser faturada novamente.")) return;

        using var scope = _scopeFactory.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<FaturamentoService>();
        await service.EstornarBaixaAsync(codigo.Id, "estorno pela tela de Faturados", SessaoUsuario.Atual.Operador);

        await Buscar();
    }

    [RelayCommand]
    private async Task Glosar(CodigoFaturamento? codigo)
    {
        if (codigo is null) return;

        SessaoUsuario.Atual.Exigir(Permissao.RegistrarGlosa, "registrar glosa");

        var descricao = $"{codigo.Atendimento?.Paciente?.Nome} — {RotulosEnum.De(codigo.Tipo)} (guia {codigo.NumeroGuiaReal})";
        int prazo;
        using (var scopePrazo = _scopeFactory.CreateScope())
            prazo = await scopePrazo.ServiceProvider.GetRequiredService<ParametrosService>().ObterPrazoRecursoGlosaAsync();

        var dialog = new GlosaWindow(descricao, prazo) { Owner = System.Windows.Application.Current.MainWindow };
        if (dialog.ShowDialog() != true) return;

        using var scope = _scopeFactory.CreateScope();
        var glosas = scope.ServiceProvider.GetRequiredService<GlosaService>();
        await glosas.RegistrarAsync(codigo.Id, dialog.DataGlosa, dialog.Motivo, dialog.MotivoCodigo, SessaoUsuario.Atual.Operador);

        await Buscar();
    }

    /// <summary>Guia TISS impressa (PDF, leiaute ANS) do atendimento desta guia.</summary>
    [RelayCommand]
    private async Task GuiaPdf(CodigoFaturamento? codigo)
    {
        if (codigo is null) return;
        try
        {
            byte[] pdf;
            using (var scope = _scopeFactory.CreateScope())
            {
                var guia = scope.ServiceProvider.GetRequiredService<GuiaTissPdfService>();
                var prestador = await scope.ServiceProvider.GetRequiredService<ParametrosService>().ObterPrestadorAsync();
                pdf = await guia.GerarPdfAsync(codigo.AtendimentoId, prestador);
            }

            var dialog = new SaveFileDialog
            {
                FileName = $"Guia-TISS-{codigo.Atendimento?.Numero ?? codigo.AtendimentoId.ToString()}-{codigo.Atendimento?.Data:yyyy-MM-dd}.pdf",
                Filter = "PDF (*.pdf)|*.pdf",
                DefaultExt = ".pdf"
            };
            if (dialog.ShowDialog() != true) return;

            await File.WriteAllBytesAsync(dialog.FileName, pdf);
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(dialog.FileName) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            Mensagem = $"Não foi possível gerar a guia: {ex.Message}";
        }
    }

    /// <summary>Reimpressão da capa de faturamento do atendimento desta guia (estado atual).</summary>
    [RelayCommand]
    private async Task Capa(CodigoFaturamento? codigo)
    {
        if (codigo is null) return;
        await Configuracao.CapaImpressao.GerarESalvarAsync(
            _scopeFactory, codigo.AtendimentoId, codigo.Atendimento?.Numero, codigo.Atendimento?.Data ?? default);
    }

    /// <summary>Exporta as guias do período em CSV (Excel pt-BR: ';' e BOM UTF-8) para conferência com o convênio.</summary>
    [RelayCommand]
    private async Task ExportarCsv()
    {
        if (Baixados.Count == 0)
        {
            Mensagem = "Nada a exportar — busque um período com guias baixadas.";
            return;
        }

        var dialog = new SaveFileDialog
        {
            FileName = $"Faturados-{Inicio:yyyy-MM-dd}-a-{Fim:yyyy-MM-dd}.csv",
            Filter = "CSV (*.csv)|*.csv",
            DefaultExt = ".csv"
        };
        if (dialog.ShowDialog() != true) return;

        try
        {
            var sb = new StringBuilder();
            sb.AppendLine("Paciente;Convênio;Código;Ordem;Nº guia;Baixado em;Glosa");
            foreach (var c in Baixados)
                sb.AppendLine(string.Join(';',
                    c.Atendimento?.Paciente?.Nome,
                    ConvenioInfo.NomeExibicao(c.Atendimento?.Paciente?.Convenio ?? default),
                    c.Tipo,
                    c.Ordem,
                    c.NumeroGuiaReal,
                    c.DataBaixa?.ToString("dd/MM/yyyy"),
                    c.Glosa));

            // BOM garante acentos corretos ao abrir direto no Excel.
            await File.WriteAllTextAsync(dialog.FileName, sb.ToString(), new UTF8Encoding(true));
            Mensagem = $"Exportado: {Baixados.Count} guia(s) em {dialog.FileName}";
        }
        catch (Exception ex)
        {
            Mensagem = $"Não foi possível exportar: {ex.Message}";
        }
    }

    // Atalhos globais do shell (IAtalhosDeTela)
    public ICommand? AtalhoAtualizar => BuscarCommand;
    public ICommand? AtalhoImprimir => ExportarCsvCommand;
}
