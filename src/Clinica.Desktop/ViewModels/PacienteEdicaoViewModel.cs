using System.Collections.ObjectModel;
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

/// <summary>
/// Formulário de cadastro/edição do paciente, usado pela janela de cadastro
/// (<c>Alertas.PacienteEdicaoWindow</c>). Fica separado da lista de propósito: a tela
/// de Pacientes é uma listagem, e o cadastro é uma tarefa com começo, meio e fim.
/// </summary>
public partial class PacienteEdicaoViewModel : ObservableObject
{
    private readonly IServiceScopeFactory _scopeFactory;

    /// <summary>Convênios ATIVOS do catálogo (código + nome + família).</summary>
    public ObservableCollection<EntradaConvenio> Convenios { get; } = new();

    /// <summary>Modalidades ATIVAS do catálogo (código + nome + base).</summary>
    public ObservableCollection<EntradaModalidade> Modalidades { get; } = new();

    public Array Sexos => Enum.GetValues(typeof(Sexo));
    public Array Categorias => Enum.GetValues(typeof(Categoria));

    [ObservableProperty] private int? _editandoId;
    [ObservableProperty] private string _nome = string.Empty;
    [ObservableProperty] private string? _documento;
    [ObservableProperty] private string? _telefone;
    [ObservableProperty] private DateTime? _dataNascimento;
    [ObservableProperty] private string? _carteirinha;
    [ObservableProperty] private DateTime? _validadeCarteirinha;

    /// <summary>Código do convênio selecionado (do catálogo). A família é derivada dele.</summary>
    [ObservableProperty] private string? _convenioCodigo = Convenio.UnimedIntercambio.ToString();

    /// <summary>Família de regra do convênio selecionado (derivada do código).</summary>
    private Convenio _convenio = Convenio.UnimedIntercambio;

    [ObservableProperty] private bool _possuiApp;
    [ObservableProperty] private Sexo _sexo = Sexo.Feminino;
    [ObservableProperty] private Categoria _categoria = CategoriaConvenio.Base(Convenio.UnimedIntercambio, false);

    /// <summary>Código da modalidade preferida (do catálogo). A base é derivada dele.</summary>
    [ObservableProperty] private string? _modalidadePreferidaCodigo = ModalidadeAtendimento.AcupunturaComEletro.ToString();

    [ObservableProperty] private string? _observacoes;
    [ObservableProperty] private string? _mensagem;
    [ObservableProperty] private bool _mensagemEhErro;
    [ObservableProperty] private bool _ocupado;

    // Retrato em edição. A gravação só acontece no Salvar, junto com o resto do cadastro.
    [ObservableProperty] private byte[]? _fotoConteudo;
    [ObservableProperty] private byte[]? _fotoMiniatura;
    [ObservableProperty] private bool _temFoto;
    private bool _fotoAlterada;

    partial void OnFotoConteudoChanged(byte[]? value) => TemFoto = value is { Length: > 0 };

    // Controle da auto-sugestão de categoria (convênio + app) x override manual.
    private ParametrosSnapshot? _snapshot;
    private bool _categoriaManual;
    private bool _carregando;
    private bool _sugerindo;

    public string Titulo => EditandoId is null ? "Novo paciente" : "Editar paciente";

    /// <summary>Disparado quando o cadastro é gravado com sucesso (a janela fecha).</summary>
    public event Action? Salvou;

    public PacienteEdicaoViewModel(IServiceScopeFactory scopeFactory) => _scopeFactory = scopeFactory;

    partial void OnEditandoIdChanged(int? value) => OnPropertyChanged(nameof(Titulo));

    // Categoria acompanha convênio + app, a menos que o usuário a defina manualmente.
    partial void OnConvenioCodigoChanged(string? value)
    {
        _convenio = CatalogoConvenios.Familia(value);
        if (!_carregando) SugerirCategoria();
    }
    partial void OnPossuiAppChanged(bool value) { if (!_carregando) SugerirCategoria(); }
    partial void OnCategoriaChanged(Categoria value) { if (!_carregando && !_sugerindo) _categoriaManual = true; }

    private Categoria CategoriaBase(bool app)
    {
        // Personalizado: categoria vem da config do próprio convênio (por código).
        if (_convenio == Convenio.Personalizado && CatalogoConvenios.Config(ConvenioCodigo) is { } cfg)
            return app ? cfg.CategoriaComApp : cfg.CategoriaSemApp;
        return _snapshot?.CategoriaBase(_convenio, app) ?? CategoriaConvenio.Base(_convenio, app);
    }

    /// <summary>Reaplica a categoria de base (convênio + app), descartando um override anterior.</summary>
    private void SugerirCategoria()
    {
        _sugerindo = true;
        _categoriaManual = false;
        Categoria = CategoriaBase(PossuiApp);
        _sugerindo = false;
    }

    /// <summary>Prepara o formulário: catálogos e, quando há id, os dados do paciente.</summary>
    public async Task CarregarAsync(int? pacienteId)
    {
        using (var scope = _scopeFactory.CreateScope())
        {
            _snapshot = await scope.ServiceProvider.GetRequiredService<ParametrosService>().ObterAsync();
        }

        Convenios.Clear();
        foreach (var c in CatalogoConvenios.Ativos)
            Convenios.Add(c);
        Modalidades.Clear();
        foreach (var m in CatalogoModalidades.Ativas)
            Modalidades.Add(m);

        if (pacienteId is not int id)
        {
            SugerirCategoria();
            return;
        }

        using var escopo = _scopeFactory.CreateScope();
        var service = escopo.ServiceProvider.GetRequiredService<PacienteService>();
        var db = escopo.ServiceProvider.GetRequiredService<ClinicaDbContext>();
        var p = await db.Pacientes.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
        if (p is null)
        {
            Mensagem = "Paciente não encontrado.";
            MensagemEhErro = true;
            return;
        }

        _carregando = true;
        EditandoId = p.Id;
        Nome = p.Nome;
        Documento = Cpf.Formatar(p.Documento);
        Telefone = p.Telefone;
        DataNascimento = p.DataNascimento?.ToDateTime(TimeOnly.MinValue);
        Carteirinha = p.Carteirinha;
        ValidadeCarteirinha = p.ValidadeCarteirinha?.ToDateTime(TimeOnly.MinValue);
        ConvenioCodigo = p.ConvenioCodigo ?? p.Convenio.ToString();
        _convenio = p.Convenio;
        PossuiApp = p.PossuiApp;
        Sexo = p.Sexo;
        ModalidadePreferidaCodigo = p.ModalidadePreferidaCodigo ?? p.ModalidadePreferida.ToString();
        Observacoes = p.Observacoes;
        Categoria = p.Categoria;
        // Preserva um override manual (categoria diferente da base do convênio + app).
        _categoriaManual = p.Categoria != CategoriaBase(p.PossuiApp);
        _carregando = false;

        // Miniatura entra na hora (é pequena e já veio na linha); a foto cheia em seguida.
        _fotoAlterada = false;
        FotoMiniatura = p.FotoMiniatura;
        FotoConteudo = p.FotoMiniatura;
        try
        {
            var cheia = await service.ObterFotoAsync(id);
            if (!_fotoAlterada && cheia is not null) FotoConteudo = cheia;
        }
        catch
        {
            // Sem a foto cheia o formulário segue com a miniatura.
        }
    }

    [RelayCommand]
    private async Task Salvar()
    {
        if (string.IsNullOrWhiteSpace(Nome))
        {
            Mensagem = "Informe o nome do paciente.";
            MensagemEhErro = true;
            return;
        }
        if (!string.IsNullOrWhiteSpace(Documento) && !Cpf.Valido(Documento))
        {
            Mensagem = "CPF inválido. Verifique os dígitos.";
            MensagemEhErro = true;
            return;
        }

        if (Ocupado) return;
        Ocupado = true;
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
                    MensagemEhErro = true;
                    return;
                }
            }

            if (EditandoId is int id)
            {
                var p = await db.Pacientes.FirstOrDefaultAsync(x => x.Id == id);
                if (p is null) { Mensagem = "Paciente não encontrado."; MensagemEhErro = true; return; }
                Aplicar(p);
                await service.AtualizarAsync(p, _categoriaManual);
                await GravarFotoAsync(service, p.Id);
            }
            else
            {
                var p = new Paciente();
                Aplicar(p);
                await service.SalvarNovoAsync(p, _categoriaManual);
                await GravarFotoAsync(service, p.Id);
            }
        }
        catch (Exception ex)
        {
            Mensagem = ex.Message;
            MensagemEhErro = true;
            return;
        }
        finally
        {
            Ocupado = false;
        }

        Salvou?.Invoke();
    }

    private void Aplicar(Paciente p)
    {
        p.Nome = Nome.Trim();
        // CPF só com dígitos (busca e comparação de duplicidade ficam estáveis);
        // telefone gravado já formatado para exibição.
        p.Documento = string.IsNullOrWhiteSpace(Documento) ? null : Cpf.Normalizar(Documento);
        p.Telefone = string.IsNullOrWhiteSpace(Telefone) ? null : Domain.Telefone.Formatar(Telefone);
        p.DataNascimento = DataNascimento is { } nasc ? DateOnly.FromDateTime(nasc) : null;
        p.Carteirinha = string.IsNullOrWhiteSpace(Carteirinha) ? null : Carteirinha.Trim();
        p.ValidadeCarteirinha = ValidadeCarteirinha is { } val ? DateOnly.FromDateTime(val) : null;
        p.ConvenioCodigo = ConvenioCodigo;
        p.Convenio = _convenio; // família derivada do código selecionado
        p.PossuiApp = PossuiApp;
        p.Sexo = Sexo;
        p.Categoria = Categoria;
        p.ModalidadePreferidaCodigo = ModalidadePreferidaCodigo;
        p.ModalidadePreferida = CatalogoModalidades.Base(ModalidadePreferidaCodigo); // base derivada do código
        p.Observacoes = string.IsNullOrWhiteSpace(Observacoes) ? null : Observacoes.Trim();
    }

    /// <summary>Grava (ou apaga) o retrato depois que o cadastro já tem Id.</summary>
    private async Task GravarFotoAsync(PacienteService service, int pacienteId)
    {
        if (!_fotoAlterada) return;

        if (FotoConteudo is { Length: > 0 } conteudo && FotoMiniatura is { Length: > 0 } miniatura)
            await service.DefinirFotoAsync(pacienteId, conteudo, miniatura);
        else
            await service.RemoverFotoAsync(pacienteId);

        _fotoAlterada = false;
    }

    /// <summary>Abre a webcam da recepção (ou a escolha de arquivo) para o retrato do paciente.</summary>
    [RelayCommand]
    private void CapturarFoto()
    {
        var janela = new Alertas.CapturaFotoWindow(Nome)
        {
            Owner = System.Windows.Application.Current.MainWindow
        };
        if (janela.ShowDialog() != true) return;

        FotoConteudo = janela.Conteudo;
        FotoMiniatura = janela.Miniatura;
        _fotoAlterada = true;
        Mensagem = "Foto definida — clique em Salvar para gravar no cadastro.";
        MensagemEhErro = false;
    }

    [RelayCommand]
    private void RemoverFoto()
    {
        if (FotoConteudo is null && FotoMiniatura is null) return;

        FotoConteudo = null;
        FotoMiniatura = null;
        _fotoAlterada = true;
        Mensagem = "Foto removida — clique em Salvar para confirmar.";
        MensagemEhErro = false;
    }
}
