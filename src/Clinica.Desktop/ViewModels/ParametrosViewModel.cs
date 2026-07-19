using System.Windows.Input;
using System.Collections.ObjectModel;
using Clinica.Application.Modelos;
using Clinica.Application.Servicos;
using Clinica.Domain;
using Clinica.Domain.Entities;
using Clinica.Desktop.Controls;
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

    /// <summary>Catálogo de convênios (embutidos + variantes) — nome, família e ativo.</summary>
    public ObservableCollection<ConvenioCadastro> Catalogo { get; } = new();

    /// <summary>Convênio selecionado no catálogo (para editar a configuração de faturamento).</summary>
    [ObservableProperty] private ConvenioCadastro? _convenioSelecionado;
    [ObservableProperty] private bool _temConvenioSelecionado;

    partial void OnConvenioSelecionadoChanged(ConvenioCadastro? value)
        => TemConvenioSelecionado = value is not null;

    [ObservableProperty] private string? _mensagem;
    [ObservableProperty] private bool _mensagemEhErro;
    [ObservableProperty] private bool _salvando;

    /// <summary>Dias antes do vencimento em que a consulta entra em alerta.</summary>
    [ObservableProperty] private int _janelaAlertaConsultaDias = 5;

    /// <summary>Dias para recorrer de uma glosa (data-limite calculada no registro da glosa).</summary>
    [ObservableProperty] private int _prazoRecursoGlosaDias = 30;

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

    private readonly ISnackbarService _snackbar;

    public ParametrosViewModel(IServiceScopeFactory scopeFactory, ISnackbarService snackbar)
    {
        _scopeFactory = scopeFactory;
        _snackbar = snackbar;
    }

    public async Task CarregarAsync()
    {
        using var scope = _scopeFactory.CreateScope();
        var parametros = scope.ServiceProvider.GetRequiredService<ParametrosService>();

        var snap = await parametros.ObterAsync();
        Itens.Clear();
        foreach (var p in snap.Todos.OrderBy(p => p.Convenio))
            Itens.Add(p);

        var catalogo = scope.ServiceProvider.GetRequiredService<ConvenioCatalogoService>();
        Catalogo.Clear();
        foreach (var c in await catalogo.ListarAsync())
            Catalogo.Add(c);

        JanelaAlertaConsultaDias = await parametros.ObterJanelaAlertaConsultaAsync();
        PrazoRecursoGlosaDias = await parametros.ObterPrazoRecursoGlosaAsync();

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

    /// <summary>Adiciona uma nova variante de convênio (reutiliza a regra de uma família existente).</summary>
    [RelayCommand]
    private void NovoConvenio()
    {
        var novo = new ConvenioCadastro
        {
            Codigo = "CV" + Guid.NewGuid().ToString("N")[..8],
            Nome = "Novo convênio",
            Familia = Convenio.Personalizado, // configurável pela clínica; troque para uma família embutida se preferir
            Ativo = true
        };
        Catalogo.Add(novo);
        ConvenioSelecionado = novo; // já abre o painel de configuração
    }

    [RelayCommand]
    private async Task Salvar()
    {
        if (Salvando) return;

        if (JanelaAlertaConsultaDias < 0)
        {
            Mensagem = "A antecedência do alerta de consultas não pode ser negativa.";
            MensagemEhErro = true;
            return;
        }

        Salvando = true;
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var parametros = scope.ServiceProvider.GetRequiredService<ParametrosService>();
            var catalogo = scope.ServiceProvider.GetRequiredService<ConvenioCatalogoService>();

            await parametros.SalvarAsync(Itens.ToList());
            await parametros.SalvarJanelaAlertaConsultaAsync(JanelaAlertaConsultaDias);
            await parametros.SalvarPrazoRecursoGlosaAsync(PrazoRecursoGlosaDias);
            await parametros.SalvarPrestadorAsync(MontarPrestador());
            await catalogo.SalvarAsync(Catalogo.ToList());

            Mensagem = "Configurações salvas. Valem imediatamente em todas as máquinas.";
            MensagemEhErro = false;
            _snackbar.Sucesso("Configurações salvas.");
        }
        catch (Exception ex)
        {
            Mensagem = $"Não foi possível salvar: {ex.Message}";
            MensagemEhErro = true;
            _snackbar.Erro("Erro ao salvar as configurações. Nada foi perdido na tela.");
        }
        finally
        {
            Salvando = false;
        }
    }

    // Atalhos globais do shell (IAtalhosDeTela)
    public ICommand? AtalhoSalvar => SalvarCommand;
}
