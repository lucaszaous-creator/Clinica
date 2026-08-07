using System.Collections.ObjectModel;
using Clinica.Application.Servicos;
using Clinica.Domain;
using Clinica.Domain.Entities;
using Clinica.Domain.Regras;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;

namespace Clinica.Desktop.ViewModels;

/// <summary>
/// Cadastro da autorização de sessões (a "senha" do convênio), usado pela janela
/// <c>Alertas.AutorizacaoWindow</c>. É o registro que permite avisar a recepção antes
/// de a cota estourar e virar glosa 2006.
/// </summary>
public partial class AutorizacaoEdicaoViewModel : ObservableObject
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly int _pacienteId;

    public ObservableCollection<EntradaConvenio> Convenios { get; } = new();

    [ObservableProperty] private int? _editandoId;
    [ObservableProperty] private string? _numero;
    [ObservableProperty] private string? _convenioCodigo;
    [ObservableProperty] private DateTime _dataEmissao = DateTime.Today;
    [ObservableProperty] private DateTime _dataValidade = DateTime.Today.AddDays(90);
    [ObservableProperty] private int _quantidadeAutorizada = 10;
    [ObservableProperty] private int _quantidadeUtilizadaManual;
    [ObservableProperty] private string? _observacoes;
    [ObservableProperty] private bool _encerrada;
    [ObservableProperty] private string? _mensagem;
    [ObservableProperty] private bool _ocupado;

    public string Titulo => EditandoId is null ? "Nova autorização" : "Editar autorização";

    partial void OnEditandoIdChanged(int? value) => OnPropertyChanged(nameof(Titulo));

    /// <summary>Disparado quando a autorização é gravada (a janela fecha).</summary>
    public event Action? Salvou;

    public AutorizacaoEdicaoViewModel(IServiceScopeFactory scopeFactory, int pacienteId)
    {
        _scopeFactory = scopeFactory;
        _pacienteId = pacienteId;
    }

    public async Task CarregarAsync(int? autorizacaoId, string? convenioPadrao)
    {
        Convenios.Clear();
        foreach (var c in CatalogoConvenios.Ativos)
            Convenios.Add(c);
        ConvenioCodigo = convenioPadrao ?? Convenios.FirstOrDefault()?.Codigo;

        if (autorizacaoId is not int id) return;

        using var scope = _scopeFactory.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<AutorizacaoService>();
        var autorizacao = (await service.DoPacienteAsync(_pacienteId)).FirstOrDefault(a => a.Id == id);
        if (autorizacao is null)
        {
            Mensagem = "Autorização não encontrada.";
            return;
        }

        EditandoId = autorizacao.Id;
        Numero = autorizacao.Numero;
        ConvenioCodigo = autorizacao.ConvenioCodigo ?? autorizacao.Convenio.ToString();
        DataEmissao = autorizacao.DataEmissao.ToDateTime(TimeOnly.MinValue);
        DataValidade = autorizacao.DataValidade.ToDateTime(TimeOnly.MinValue);
        QuantidadeAutorizada = autorizacao.QuantidadeAutorizada;
        QuantidadeUtilizadaManual = autorizacao.QuantidadeUtilizadaManual;
        Observacoes = autorizacao.Observacoes;
        Encerrada = autorizacao.Encerrada;
    }

    [RelayCommand]
    private async Task Salvar()
    {
        if (Ocupado) return;
        Ocupado = true;
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var service = scope.ServiceProvider.GetRequiredService<AutorizacaoService>();

            await service.SalvarAsync(new AutorizacaoSessoes
            {
                Id = EditandoId ?? 0,
                PacienteId = _pacienteId,
                Numero = string.IsNullOrWhiteSpace(Numero) ? null : Numero.Trim(),
                ConvenioCodigo = ConvenioCodigo,
                Convenio = CatalogoConvenios.Familia(ConvenioCodigo),
                DataEmissao = DateOnly.FromDateTime(DataEmissao),
                DataValidade = DateOnly.FromDateTime(DataValidade),
                QuantidadeAutorizada = QuantidadeAutorizada,
                QuantidadeUtilizadaManual = QuantidadeUtilizadaManual,
                Observacoes = string.IsNullOrWhiteSpace(Observacoes) ? null : Observacoes.Trim(),
                Encerrada = Encerrada
            }, SessaoUsuario.Atual.Operador);
        }
        catch (Exception ex)
        {
            Mensagem = ex.Message;
            return;
        }
        finally
        {
            Ocupado = false;
        }

        Salvou?.Invoke();
    }
}
