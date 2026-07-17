using System.Windows.Input;
using System.IO;
using System.Windows;
using Clinica.Application.Modelos;
using Clinica.Application.Servicos;
using Clinica.Desktop.Configuracao;
using Clinica.Domain;
using Clinica.Infrastructure;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Win32;

namespace Clinica.Desktop.ViewModels;

/// <summary>Configuração do prestador e exportação do lote de guias no formato TISS (XML).</summary>
public partial class TissViewModel : ObservableObject, IAtalhosDeTela
{
    private readonly IServiceScopeFactory _scopeFactory;

    // Config do prestador
    [ObservableProperty] private string? _codigoNaOperadora;
    [ObservableProperty] private string? _cnpj;
    [ObservableProperty] private string? _razaoSocial;
    [ObservableProperty] private string? _registroAnsOperadora;

    // Dados da clínica exibidos na capa de faturamento
    [ObservableProperty] private string? _nomeFantasia;
    [ObservableProperty] private string? _cnes;
    [ObservableProperty] private string? _endereco;
    [ObservableProperty] private string? _telefone;
    [ObservableProperty] private string? _email;
    [ObservableProperty] private string? _tussAcupuntura;
    [ObservableProperty] private string? _tussEletro;
    [ObservableProperty] private string? _tussBsv;
    [ObservableProperty] private string? _tussConsulta;
    [ObservableProperty] private string? _tussEspecialidade;

    // Exportação
    [ObservableProperty] private DateTime _inicio;
    [ObservableProperty] private DateTime _fim;
    [ObservableProperty] private string? _mensagem;

    public TissViewModel(IServiceScopeFactory scopeFactory)
    {
        _scopeFactory = scopeFactory;
        var hoje = DateTime.Today;
        _inicio = new DateTime(hoje.Year, hoje.Month, 1);
        _fim = _inicio.AddMonths(1).AddDays(-1);
    }

    public Task CarregarAsync()
    {
        var d = PrestadorStore.Carregar();
        CodigoNaOperadora = d.CodigoNaOperadora;
        Cnpj = d.Cnpj;
        RazaoSocial = d.RazaoSocial;
        RegistroAnsOperadora = d.RegistroAnsOperadora;
        NomeFantasia = d.NomeFantasia;
        Cnes = d.Cnes;
        Endereco = d.Endereco;
        Telefone = d.Telefone;
        Email = d.Email;
        TussAcupuntura = d.CodigoTuss(TipoCodigo.Acupuntura);
        TussEletro = d.CodigoTuss(TipoCodigo.Eletroacupuntura);
        TussBsv = d.CodigoTuss(TipoCodigo.Bsv);
        TussConsulta = d.CodigoTuss(TipoCodigo.Consulta);
        TussEspecialidade = d.CodigoTuss(TipoCodigo.ConsultaEspecialidade);
        return Task.CompletedTask;
    }

    private DadosPrestador MontarDados() => new()
    {
        CodigoNaOperadora = CodigoNaOperadora,
        Cnpj = Cnpj,
        RazaoSocial = RazaoSocial,
        RegistroAnsOperadora = RegistroAnsOperadora,
        NomeFantasia = NomeFantasia,
        Cnes = Cnes,
        Endereco = Endereco,
        Telefone = Telefone,
        Email = Email,
        CodigosTuss = new()
        {
            [TipoCodigo.Acupuntura] = TussAcupuntura ?? string.Empty,
            [TipoCodigo.Eletroacupuntura] = TussEletro ?? string.Empty,
            [TipoCodigo.Bsv] = TussBsv ?? string.Empty,
            [TipoCodigo.Consulta] = TussConsulta ?? string.Empty,
            [TipoCodigo.ConsultaEspecialidade] = TussEspecialidade ?? string.Empty
        }
    };

    [RelayCommand]
    private void SalvarConfig()
    {
        PrestadorStore.Salvar(MontarDados());
        Mensagem = "Configuração do prestador salva.";
    }

    [RelayCommand]
    private async Task Exportar()
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ClinicaDbContext>();
        var tiss = scope.ServiceProvider.GetRequiredService<TissExportService>();

        var inicio = DateOnly.FromDateTime(Inicio);
        var fim = DateOnly.FromDateTime(Fim);
        var codigos = await db.Codigos
            .Include(c => c.Atendimento!).ThenInclude(a => a.Paciente!)
            .Where(c => c.DataBaixa != null && c.Atendimento!.Data >= inicio && c.Atendimento!.Data <= fim)
            .ToListAsync();

        if (codigos.Count == 0)
        {
            Mensagem = "Nenhuma guia faturada no período.";
            return;
        }

        var numeroLote = $"LOTE-{DateTime.Now:yyyyMMddHHmm}";
        var xml = tiss.GerarLoteXml(codigos, MontarDados(), numeroLote);

        var dialog = new SaveFileDialog
        {
            FileName = $"{numeroLote}.xml",
            Filter = "Arquivo TISS (*.xml)|*.xml",
            DefaultExt = ".xml"
        };
        if (dialog.ShowDialog() == true)
        {
            await File.WriteAllTextAsync(dialog.FileName, xml);
            Mensagem = $"Lote gerado com {codigos.Count} guia(s): {dialog.FileName}";
        }
    }

    // Atalhos globais do shell (IAtalhosDeTela)
    public ICommand? AtalhoSalvar => SalvarConfigCommand;
    public ICommand? AtalhoImprimir => ExportarCommand;
}
