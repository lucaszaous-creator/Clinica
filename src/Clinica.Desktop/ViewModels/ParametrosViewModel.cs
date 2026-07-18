using System.Windows.Input;
using System.Collections.ObjectModel;
using Clinica.Application.Modelos;
using Clinica.Application.Servicos;
using Clinica.Domain;
using Clinica.Domain.Entities;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;

namespace Clinica.Desktop.ViewModels;

/// <summary>
/// Configurações GLOBAIS do sistema (salvas no banco, valem para todas as máquinas):
/// regras por convênio, janela de alerta de consultas, dados da clínica/prestador
/// e códigos TUSS por procedimento.
/// </summary>
public partial class ParametrosViewModel : ObservableObject, IAtalhosDeTela
{
    private readonly IServiceScopeFactory _scopeFactory;

    public ObservableCollection<ParametroConvenio> Itens { get; } = new();

    [ObservableProperty] private string? _mensagem;

    /// <summary>Dias antes do vencimento em que a consulta entra em alerta.</summary>
    [ObservableProperty] private int _janelaAlertaConsultaDias = 5;

    // Dados da clínica/prestador (capa de faturamento + lote TISS)
    [ObservableProperty] private string? _razaoSocial;
    [ObservableProperty] private string? _nomeFantasia;
    [ObservableProperty] private string? _cnpj;
    [ObservableProperty] private string? _cnes;
    [ObservableProperty] private string? _endereco;
    [ObservableProperty] private string? _telefone;
    [ObservableProperty] private string? _email;
    [ObservableProperty] private string? _codigoNaOperadora;
    [ObservableProperty] private string? _registroAnsOperadora;

    // Códigos TUSS por procedimento
    [ObservableProperty] private string? _tussAcupuntura;
    [ObservableProperty] private string? _tussEletro;
    [ObservableProperty] private string? _tussBsv;
    [ObservableProperty] private string? _tussConsulta;
    [ObservableProperty] private string? _tussEspecialidade;

    public ParametrosViewModel(IServiceScopeFactory scopeFactory) => _scopeFactory = scopeFactory;

    public async Task CarregarAsync()
    {
        using var scope = _scopeFactory.CreateScope();
        var parametros = scope.ServiceProvider.GetRequiredService<ParametrosService>();

        var snap = await parametros.ObterAsync();
        Itens.Clear();
        foreach (var p in snap.Todos.OrderBy(p => p.Convenio))
            Itens.Add(p);

        JanelaAlertaConsultaDias = await parametros.ObterJanelaAlertaConsultaAsync();

        var d = await parametros.ObterPrestadorAsync();
        RazaoSocial = d.RazaoSocial;
        NomeFantasia = d.NomeFantasia;
        Cnpj = d.Cnpj;
        Cnes = d.Cnes;
        Endereco = d.Endereco;
        Telefone = d.Telefone;
        Email = d.Email;
        CodigoNaOperadora = d.CodigoNaOperadora;
        RegistroAnsOperadora = d.RegistroAnsOperadora;
        TussAcupuntura = d.CodigoTuss(TipoCodigo.Acupuntura);
        TussEletro = d.CodigoTuss(TipoCodigo.Eletroacupuntura);
        TussBsv = d.CodigoTuss(TipoCodigo.Bsv);
        TussConsulta = d.CodigoTuss(TipoCodigo.Consulta);
        TussEspecialidade = d.CodigoTuss(TipoCodigo.ConsultaEspecialidade);
    }

    private DadosPrestador MontarPrestador() => new()
    {
        RazaoSocial = RazaoSocial,
        NomeFantasia = NomeFantasia,
        Cnpj = Cnpj,
        Cnes = Cnes,
        Endereco = Endereco,
        Telefone = Telefone,
        Email = Email,
        CodigoNaOperadora = CodigoNaOperadora,
        RegistroAnsOperadora = RegistroAnsOperadora,
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
    private async Task Salvar()
    {
        using var scope = _scopeFactory.CreateScope();
        var parametros = scope.ServiceProvider.GetRequiredService<ParametrosService>();

        await parametros.SalvarAsync(Itens.ToList());
        await parametros.SalvarJanelaAlertaConsultaAsync(JanelaAlertaConsultaDias);
        await parametros.SalvarPrestadorAsync(MontarPrestador());

        Mensagem = "Configurações salvas. Valem imediatamente em todas as máquinas.";
    }

    // Atalhos globais do shell (IAtalhosDeTela)
    public ICommand? AtalhoSalvar => SalvarCommand;
}
