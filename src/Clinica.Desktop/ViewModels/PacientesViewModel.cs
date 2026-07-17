using System.Windows.Input;
using System.Collections.ObjectModel;
using System.Windows;
using Clinica.Application.Servicos;
using Clinica.Domain;
using Clinica.Domain.Entities;
using Clinica.Domain.Regras;
using Clinica.Infrastructure;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Clinica.Desktop.ViewModels;

/// <summary>Cadastro, busca (nome/CPF), edição, exclusão de pacientes e acesso à ficha.</summary>
public partial class PacientesViewModel : ObservableObject, IAtalhosDeTela
{
    private readonly IServiceScopeFactory _scopeFactory;

    public ObservableCollection<Paciente> Pacientes { get; } = new();

    public Array Convenios => Enum.GetValues(typeof(Convenio));
    public Array Sexos => Enum.GetValues(typeof(Sexo));
    public Array Categorias => Enum.GetValues(typeof(Categoria));
    public Array Modalidades => Enum.GetValues(typeof(ModalidadeAtendimento));

    [ObservableProperty] private string? _busca;

    // Formulário
    [ObservableProperty] private int? _editandoId;
    [ObservableProperty] private string _nome = string.Empty;
    [ObservableProperty] private string? _documento;
    [ObservableProperty] private string? _telefone;
    [ObservableProperty] private Convenio _convenio = Convenio.UnimedIntercambio;
    [ObservableProperty] private bool _possuiApp;
    [ObservableProperty] private Sexo _sexo = Sexo.Feminino;
    [ObservableProperty] private Categoria _categoria = CategoriaConvenio.Base(Convenio.UnimedIntercambio, false);
    [ObservableProperty] private ModalidadeAtendimento _modalidadePreferida = ModalidadeAtendimento.AcupunturaComEletro;
    [ObservableProperty] private string? _mensagem;

    // Controle da auto-sugestão de categoria (plano + app) x override manual.
    private bool _categoriaManual;
    private bool _carregando;
    private bool _sugerindo;

    public string TituloFormulario => EditandoId is null ? "Novo paciente" : "Editar paciente";

    /// <summary>Pede ao shell para abrir a ficha de um paciente.</summary>
    public event Action<int>? FichaSolicitada;

    public PacientesViewModel(IServiceScopeFactory scopeFactory) => _scopeFactory = scopeFactory;

    partial void OnEditandoIdChanged(int? value) => OnPropertyChanged(nameof(TituloFormulario));

    // Categoria acompanha convênio + app, a menos que o usuário a defina manualmente.
    partial void OnConvenioChanged(Convenio value) { if (!_carregando) SugerirCategoria(); }
    partial void OnPossuiAppChanged(bool value) { if (!_carregando) SugerirCategoria(); }
    partial void OnCategoriaChanged(Categoria value) { if (!_carregando && !_sugerindo) _categoriaManual = true; }

    /// <summary>Reaplica a categoria de base (convênio + app), descartando um override anterior.</summary>
    private void SugerirCategoria()
    {
        _sugerindo = true;
        _categoriaManual = false;
        Categoria = CategoriaConvenio.Base(Convenio, PossuiApp);
        _sugerindo = false;
    }

    public Task CarregarAsync() => Buscar();

    [RelayCommand]
    private async Task Buscar()
    {
        using var scope = _scopeFactory.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<PacienteService>();
        Pacientes.Clear();
        foreach (var p in await service.BuscarAsync(Busca))
            Pacientes.Add(p);
    }

    [RelayCommand]
    private async Task Salvar()
    {
        if (string.IsNullOrWhiteSpace(Nome))
        {
            Mensagem = "Informe o nome do paciente.";
            return;
        }
        if (!string.IsNullOrWhiteSpace(Documento) && !Cpf.Valido(Documento))
        {
            Mensagem = "CPF inválido. Verifique os dígitos.";
            return;
        }

        try
        {
            using var scope = _scopeFactory.CreateScope();
            var service = scope.ServiceProvider.GetRequiredService<PacienteService>();
            var db = scope.ServiceProvider.GetRequiredService<ClinicaDbContext>();

            if (EditandoId is int id)
            {
                var p = await db.Pacientes.FirstOrDefaultAsync(x => x.Id == id);
                if (p is null) { Mensagem = "Paciente não encontrado."; return; }
                Aplicar(p);
                await service.AtualizarAsync(p, _categoriaManual);
                Mensagem = "Paciente atualizado.";
            }
            else
            {
                var p = new Paciente();
                Aplicar(p);
                await service.SalvarNovoAsync(p, _categoriaManual);
                Mensagem = "Paciente salvo.";
            }
        }
        catch (Exception ex)
        {
            Mensagem = ex.Message;
            return;
        }

        Limpar();
        await Buscar();
    }

    private void Aplicar(Paciente p)
    {
        p.Nome = Nome.Trim();
        p.Documento = Documento;
        p.Telefone = Telefone;
        p.Convenio = Convenio;
        p.PossuiApp = PossuiApp;
        p.Sexo = Sexo;
        p.Categoria = Categoria;
        p.ModalidadePreferida = ModalidadePreferida;
    }

    [RelayCommand]
    private void Editar(Paciente? p)
    {
        if (p is null) return;
        _carregando = true;
        EditandoId = p.Id;
        Nome = p.Nome;
        Documento = Cpf.Formatar(p.Documento);
        Telefone = p.Telefone;
        Convenio = p.Convenio;
        PossuiApp = p.PossuiApp;
        Sexo = p.Sexo;
        ModalidadePreferida = p.ModalidadePreferida;
        Categoria = p.Categoria;
        // Preserva um override manual (categoria diferente da base do convênio + app).
        _categoriaManual = p.Categoria != CategoriaConvenio.Base(p.Convenio, p.PossuiApp);
        _carregando = false;
        Mensagem = null;
    }

    [RelayCommand]
    private void Novo() => Limpar();

    [RelayCommand]
    private async Task Excluir(Paciente? p)
    {
        if (p is null) return;
        var confirma = MessageBox.Show(
            $"Excluir o paciente \"{p.Nome}\"?\nTodos os atendimentos e códigos dele também serão removidos.",
            "Confirmar exclusão", MessageBoxButton.YesNo, MessageBoxImage.Warning);
        if (confirma != MessageBoxResult.Yes) return;

        using var scope = _scopeFactory.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<PacienteService>();
        await service.RemoverAsync(p.Id);
        if (EditandoId == p.Id) Limpar();
        await Buscar();
    }

    [RelayCommand]
    private void AbrirFicha(Paciente? p)
    {
        if (p is not null) FichaSolicitada?.Invoke(p.Id);
    }

    private void Limpar()
    {
        _carregando = true;
        EditandoId = null;
        Nome = string.Empty;
        Documento = null;
        Telefone = null;
        PossuiApp = false;
        Convenio = Convenio.UnimedIntercambio;
        Sexo = Sexo.Feminino;
        ModalidadePreferida = ModalidadeAtendimento.AcupunturaComEletro;
        _carregando = false;
        SugerirCategoria();
    }

    // Atalhos globais do shell (IAtalhosDeTela)
    public ICommand? AtalhoSalvar => SalvarCommand;
    public ICommand? AtalhoAtualizar => BuscarCommand;
}
