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

/// <summary>Opção do filtro de convênio da lista de pacientes. Código null = todos.</summary>
public sealed record OpcaoConvenio(string? Codigo, string Nome);

/// <summary>Cadastro, busca (nome/CPF), edição, exclusão de pacientes e acesso à ficha.</summary>
public partial class PacientesViewModel : ObservableObject, IAtalhosDeTela
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly Controls.IDialogoService _dialogo;

    public ObservableCollection<Paciente> Pacientes { get; } = new();

    /// <summary>Opções do filtro de convênio (a primeira é sempre "Todos os convênios").</summary>
    public ObservableCollection<OpcaoConvenio> FiltrosConvenio { get; } = new();

    /// <summary>Convênios ATIVOS do catálogo (código + nome + família).</summary>
    public ObservableCollection<EntradaConvenio> Convenios { get; } = new();
    public Array Sexos => Enum.GetValues(typeof(Sexo));
    public Array Categorias => Enum.GetValues(typeof(Categoria));

    /// <summary>Modalidades ATIVAS do catálogo (código + nome + base).</summary>
    public ObservableCollection<EntradaModalidade> Modalidades { get; } = new();

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

    // Pesquisa instantânea (padrão CampoPesquisa do design system).
    partial void OnBuscaChanged(string? value) => _ = Buscar();

    /// <summary>Filtro de convênio aplicado sobre o resultado da busca.</summary>
    [ObservableProperty] private OpcaoConvenio? _filtroConvenio;
    partial void OnFiltroConvenioChanged(OpcaoConvenio? value) { if (!_carregando) _ = Buscar(); }

    // Formulário
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
        Modalidades.Clear();
        foreach (var m in CatalogoModalidades.Ativas)
            Modalidades.Add(m);

        _carregando = true;
        FiltrosConvenio.Clear();
        FiltrosConvenio.Add(new OpcaoConvenio(null, "Todos os convênios"));
        foreach (var op in CatalogoConvenios.Ativos)
            FiltrosConvenio.Add(new OpcaoConvenio(op.Codigo, op.Nome));
        FiltroConvenio = FiltrosConvenio[0];
        _carregando = false;

        await Buscar();
    }

    [RelayCommand]
    private async Task Buscar()
    {
        using var scope = _scopeFactory.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<PacienteService>();
        var encontrados = await service.BuscarAsync(Busca);

        // Filtro de convênio: comparado pelo código do catálogo, com fallback na
        // família para cadastros antigos gravados antes dos convênios dinâmicos.
        if (FiltroConvenio?.Codigo is { } codigo)
            encontrados = encontrados
                .Where(p => (p.ConvenioCodigo ?? p.Convenio.ToString()) == codigo)
                .ToList();

        Pacientes.Clear();
        foreach (var p in encontrados)
            Pacientes.Add(p);
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
                Mensagem = "Paciente atualizado.";
                MensagemEhErro = false;
            }
            else
            {
                var p = new Paciente();
                Aplicar(p);
                await service.SalvarNovoAsync(p, _categoriaManual);
                await GravarFotoAsync(service, p.Id);
                Mensagem = "Paciente salvo.";
                MensagemEhErro = false;
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

        // Limpar() zera a mensagem junto com o formulário — a confirmação é reposta
        // depois, senão o usuário salva e não vê retorno nenhum.
        var confirmacao = Mensagem;
        Limpar();
        Mensagem = confirmacao;
        MensagemEhErro = false;
        await Buscar();
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

    /// <summary>Grava (ou apaga) o retrato depois que o cadastro já tem Id. Sem foto tocada, não faz nada.</summary>
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

    /// <summary>Traz a foto cheia do paciente em edição (a lista só carrega a miniatura).</summary>
    private async Task CarregarFotoAsync(int pacienteId)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var service = scope.ServiceProvider.GetRequiredService<PacienteService>();
            var foto = await service.ObterFotoAsync(pacienteId);

            // Se o usuário já capturou outra foto (ou trocou de paciente) enquanto
            // a consulta corria, o que veio do banco é descartado.
            if (_fotoAlterada || EditandoId != pacienteId) return;
            FotoConteudo = foto;
        }
        catch
        {
            // Sem a foto cheia a ficha ainda funciona: a miniatura já está na tela.
        }
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
        Mensagem = null;

        // Miniatura entra na hora (já veio com a lista) e a foto cheia chega em seguida.
        _fotoAlterada = false;
        FotoMiniatura = p.FotoMiniatura;
        FotoConteudo = p.FotoMiniatura;
        _ = CarregarFotoAsync(p.Id);
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
        DataNascimento = null;
        Carteirinha = null;
        ValidadeCarteirinha = null;
        PossuiApp = false;
        ConvenioCodigo = Convenio.UnimedIntercambio.ToString();
        _convenio = Convenio.UnimedIntercambio;
        Sexo = Sexo.Feminino;
        ModalidadePreferidaCodigo = ModalidadeAtendimento.AcupunturaComEletro.ToString();
        Observacoes = null;
        Mensagem = null;
        FotoConteudo = null;
        FotoMiniatura = null;
        _fotoAlterada = false;
        _carregando = false;
        SugerirCategoria();
    }

    /// <summary>Carrega a tela já com um paciente em edição (usado pelo botão Editar da ficha).</summary>
    public async Task CarregarEEditarAsync(int pacienteId)
    {
        await CarregarAsync();
        var p = Pacientes.FirstOrDefault(x => x.Id == pacienteId);
        if (p is not null) Editar(p);
    }

    // Atalhos globais do shell (IAtalhosDeTela)
    public ICommand? AtalhoSalvar => SalvarCommand;
    public ICommand? AtalhoAtualizar => BuscarCommand;
}
