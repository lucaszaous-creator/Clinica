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
    private readonly Controls.IDialogoService _dialogo;

    public ObservableCollection<Paciente> Pacientes { get; } = new();

    /// <summary>Convênios ATIVOS do catálogo (código + nome + família).</summary>
    public ObservableCollection<EntradaConvenio> Convenios { get; } = new();
    public Array Sexos => Enum.GetValues(typeof(Sexo));
    public Array Categorias => Enum.GetValues(typeof(Categoria));
    public Array Modalidades => Enum.GetValues(typeof(ModalidadeAtendimento));

    /// <summary>Categoria-base efetiva do catálogo (fallback para o padrão do código até carregar).</summary>
    private ParametrosSnapshot? _snapshot;
    private Categoria CategoriaBase(bool app)
    {
        // Personalizado: categoria vem da config do próprio convênio (por código).
        if (_convenio == Convenio.Personalizado && CatalogoConvenios.Config(ConvenioCodigo) is { } cfg)
            return app ? cfg.CategoriaComApp : cfg.CategoriaSemApp;
        return _snapshot?.CategoriaBase(_convenio, app) ?? CategoriaConvenio.Base(_convenio, app);
    }

    [ObservableProperty] private string? _busca;

    // Formulário
    [ObservableProperty] private int? _editandoId;
    [ObservableProperty] private string _nome = string.Empty;
    [ObservableProperty] private string? _documento;
    [ObservableProperty] private string? _telefone;
    /// <summary>Código do convênio selecionado (do catálogo). A família é derivada dele.</summary>
    [ObservableProperty] private string? _convenioCodigo = Convenio.UnimedIntercambio.ToString();
    /// <summary>Família de regra do convênio selecionado (derivada do código).</summary>
    private Convenio _convenio = Convenio.UnimedIntercambio;
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

    public PacientesViewModel(IServiceScopeFactory scopeFactory, Controls.IDialogoService dialogo)
    {
        _scopeFactory = scopeFactory;
        _dialogo = dialogo;
    }

    partial void OnEditandoIdChanged(int? value) => OnPropertyChanged(nameof(TituloFormulario));

    // Categoria acompanha convênio + app, a menos que o usuário a defina manualmente.
    partial void OnConvenioCodigoChanged(string? value)
    {
        _convenio = CatalogoConvenios.Familia(value);
        if (!_carregando) SugerirCategoria();
    }
    partial void OnPossuiAppChanged(bool value) { if (!_carregando) SugerirCategoria(); }
    partial void OnCategoriaChanged(Categoria value) { if (!_carregando && !_sugerindo) _categoriaManual = true; }

    /// <summary>Reaplica a categoria de base (convênio + app), descartando um override anterior.</summary>
    private void SugerirCategoria()
    {
        _sugerindo = true;
        _categoriaManual = false;
        Categoria = CategoriaBase(PossuiApp);
        _sugerindo = false;
    }

    public async Task CarregarAsync()
    {
        using (var scope = _scopeFactory.CreateScope())
        {
            _snapshot = await scope.ServiceProvider.GetRequiredService<ParametrosService>().ObterAsync();
        }
        Convenios.Clear();
        foreach (var op in CatalogoConvenios.Ativos)
            Convenios.Add(op);
        await Buscar();
    }

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

            // Impede dois cadastros com o mesmo CPF (compara normalizado, pois dados
            // antigos podem ter sido gravados com máscara).
            if (!string.IsNullOrWhiteSpace(Documento))
            {
                var cpfNovo = Cpf.Normalizar(Documento);
                var duplicado = (await db.Pacientes.AsNoTracking()
                        .Where(x => x.Documento != null && x.Id != (EditandoId ?? 0))
                        .Select(x => new { x.Nome, x.Documento })
                        .ToListAsync())
                    .FirstOrDefault(x => Cpf.Normalizar(x.Documento) == cpfNovo);
                if (duplicado is not null)
                {
                    Mensagem = $"Já existe um paciente com este CPF: {duplicado.Nome}.";
                    return;
                }
            }

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
        // CPF só com dígitos (busca e comparação de duplicidade ficam estáveis);
        // telefone gravado já formatado para exibição.
        p.Documento = string.IsNullOrWhiteSpace(Documento) ? null : Cpf.Normalizar(Documento);
        p.Telefone = string.IsNullOrWhiteSpace(Telefone) ? null : Domain.Telefone.Formatar(Telefone);
        p.ConvenioCodigo = ConvenioCodigo;
        p.Convenio = _convenio; // família derivada do código selecionado
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
        ConvenioCodigo = p.ConvenioCodigo ?? p.Convenio.ToString();
        _convenio = p.Convenio;
        PossuiApp = p.PossuiApp;
        Sexo = p.Sexo;
        ModalidadePreferida = p.ModalidadePreferida;
        Categoria = p.Categoria;
        // Preserva um override manual (categoria diferente da base do convênio + app).
        _categoriaManual = p.Categoria != CategoriaBase(p.PossuiApp);
        _carregando = false;
        Mensagem = null;
    }

    [RelayCommand]
    private void Novo() => Limpar();

    [RelayCommand]
    private async Task Excluir(Paciente? p)
    {
        if (p is null) return;
        if (!_dialogo.ConfirmarPerigo("Confirmar exclusão",
            $"Excluir o paciente \"{p.Nome}\"?\nTodos os atendimentos e códigos dele também serão removidos.")) return;

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
        ConvenioCodigo = Convenio.UnimedIntercambio.ToString();
        _convenio = Convenio.UnimedIntercambio;
        Sexo = Sexo.Feminino;
        ModalidadePreferida = ModalidadeAtendimento.AcupunturaComEletro;
        _carregando = false;
        SugerirCategoria();
    }

    // Atalhos globais do shell (IAtalhosDeTela)
    public ICommand? AtalhoSalvar => SalvarCommand;
    public ICommand? AtalhoAtualizar => BuscarCommand;
}
