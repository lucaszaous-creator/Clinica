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
    public ObservableCollection<ConvenioEdicao> Catalogo { get; } = new();

    /// <summary>Catálogo de modalidades de atendimento (embutidas + variantes).</summary>
    public ObservableCollection<ModalidadeEdicao> Modalidades { get; } = new();

    /// <summary>Catálogo de especialidades de consulta (embutidas + adicionadas).</summary>
    public ObservableCollection<EspecialidadeEdicao> Especialidades { get; } = new();

    /// <summary>Convênio selecionado no catálogo (para editar nome, família e configuração de faturamento).</summary>
    [ObservableProperty] private ConvenioEdicao? _convenioSelecionado;
    [ObservableProperty] private bool _temConvenioSelecionado;

    partial void OnConvenioSelecionadoChanged(ConvenioEdicao? value)
        => TemConvenioSelecionado = value is not null;

    [ObservableProperty] private string? _mensagem;
    [ObservableProperty] private bool _mensagemEhErro;
    [ObservableProperty] private bool _salvando;

    /// <summary>Dias antes do vencimento em que a consulta entra em alerta.</summary>
    [ObservableProperty] private int _janelaAlertaConsultaDias = 5;

    /// <summary>Dias para recorrer de uma glosa (data-limite calculada no registro da glosa).</summary>
    [ObservableProperty] private int _prazoRecursoGlosaDias = 30;

    /// <summary>De quantos em quantos dias as pendências devem ser rodadas (fechamento de ciclo).</summary>
    [ObservableProperty] private int _intervaloRodadaPendenciasDias = 10;

    /// <summary>A rodada também cobra consultas a renovar? (começa aplicável só às guias).</summary>
    [ObservableProperty] private bool _rodadaAplicaConsultas;

    /// <summary>A rodada também cobra carteirinhas a vencer?</summary>
    [ObservableProperty] private bool _rodadaAplicaCarteirinhas;

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
    private readonly IDialogoService _dialogo;

    public ParametrosViewModel(IServiceScopeFactory scopeFactory, ISnackbarService snackbar, IDialogoService dialogo)
    {
        _scopeFactory = scopeFactory;
        _snackbar = snackbar;
        _dialogo = dialogo;
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
            Catalogo.Add(new ConvenioEdicao(c));

        var modalidades = scope.ServiceProvider.GetRequiredService<ModalidadeCatalogoService>();
        Modalidades.Clear();
        foreach (var m in await modalidades.ListarAsync())
            Modalidades.Add(new ModalidadeEdicao(m));

        var especialidades = scope.ServiceProvider.GetRequiredService<EspecialidadeCatalogoService>();
        Especialidades.Clear();
        foreach (var e in await especialidades.ListarAsync())
            Especialidades.Add(new EspecialidadeEdicao(e));

        JanelaAlertaConsultaDias = await parametros.ObterJanelaAlertaConsultaAsync();
        PrazoRecursoGlosaDias = await parametros.ObterPrazoRecursoGlosaAsync();
        IntervaloRodadaPendenciasDias = await parametros.ObterIntervaloRodadaPendenciasAsync();
        RodadaAplicaConsultas = await parametros.ObterRodadaAplicaConsultasAsync();
        RodadaAplicaCarteirinhas = await parametros.ObterRodadaAplicaCarteirinhasAsync();

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
        // Nome único para não esbarrar na validação de duplicados ao criar vários seguidos.
        var nome = "Novo convênio";
        for (var n = 2; Catalogo.Any(c => string.Equals(c.Nome, nome, StringComparison.OrdinalIgnoreCase)); n++)
            nome = $"Novo convênio {n}";

        var novo = new ConvenioEdicao(new ConvenioCadastro
        {
            Codigo = "CV" + Guid.NewGuid().ToString("N")[..8],
            Nome = nome,
            Familia = Convenio.Personalizado, // configurável pela clínica; troque para uma família embutida se preferir
            Ativo = true
        });
        Catalogo.Add(novo);
        ConvenioSelecionado = novo; // já abre o painel de configuração
    }

    /// <summary>Exclui a variante selecionada (embutidos e convênios com pacientes são recusados pelo serviço).</summary>
    [RelayCommand]
    private async Task RemoverConvenio()
    {
        if (ConvenioSelecionado is not { PodeExcluir: true } alvo) return;
        if (!_dialogo.ConfirmarPerigo("Excluir convênio",
                $"Excluir o convênio \"{alvo.Nome}\"?\n\nSe houver pacientes cadastrados nele, a exclusão será recusada — nesse caso, desative-o."))
            return;

        try
        {
            using var scope = _scopeFactory.CreateScope();
            var catalogo = scope.ServiceProvider.GetRequiredService<ConvenioCatalogoService>();
            var (ok, mensagem) = await catalogo.ExcluirAsync(alvo.Codigo);
            if (ok)
            {
                Catalogo.Remove(alvo);
                ConvenioSelecionado = null;
                _snackbar.Sucesso(mensagem);
            }
            else
            {
                _dialogo.Aviso("Não foi possível excluir", mensagem);
            }
        }
        catch (Exception ex)
        {
            _snackbar.Erro($"Erro ao excluir o convênio: {ex.Message}");
        }
    }

    // ---- Modalidades ----

    /// <summary>Adiciona uma nova modalidade (variante que reutiliza o comportamento de uma base existente).</summary>
    [RelayCommand]
    private void NovaModalidade()
    {
        var nome = NomeUnico("Nova modalidade", Modalidades.Select(m => m.Nome));
        Modalidades.Add(new ModalidadeEdicao(new ModalidadeCadastro
        {
            Codigo = "MD" + Guid.NewGuid().ToString("N")[..8],
            Nome = nome,
            Base = ModalidadeAtendimento.AcupunturaSimples,
            Ativo = true
        }));
    }

    /// <summary>Exclui a modalidade (embutidas e modalidades em uso são recusadas pelo serviço).</summary>
    [RelayCommand]
    private async Task RemoverModalidade(ModalidadeEdicao? alvo)
    {
        if (alvo is not { PodeExcluir: true }) return;
        if (!_dialogo.ConfirmarPerigo("Excluir modalidade",
                $"Excluir a modalidade \"{alvo.Nome}\"?\n\nSe houver registros usando-a, a exclusão será recusada — nesse caso, desative-a."))
            return;

        try
        {
            using var scope = _scopeFactory.CreateScope();
            var servico = scope.ServiceProvider.GetRequiredService<ModalidadeCatalogoService>();
            var (ok, mensagem) = await servico.ExcluirAsync(alvo.Codigo);
            if (ok)
            {
                Modalidades.Remove(alvo);
                _snackbar.Sucesso(mensagem);
            }
            else
            {
                _dialogo.Aviso("Não foi possível excluir", mensagem);
            }
        }
        catch (Exception ex)
        {
            _snackbar.Erro($"Erro ao excluir a modalidade: {ex.Message}");
        }
    }

    // ---- Especialidades ----

    /// <summary>Adiciona uma nova especialidade de consulta.</summary>
    [RelayCommand]
    private void NovaEspecialidade()
    {
        var nome = NomeUnico("Nova especialidade", Especialidades.Select(e => e.Nome));
        Especialidades.Add(new EspecialidadeEdicao(new EspecialidadeCadastro
        {
            Codigo = "ES" + Guid.NewGuid().ToString("N")[..8],
            Nome = nome,
            Ativo = true
        }));
    }

    /// <summary>Exclui a especialidade (embutidas e especialidades em uso são recusadas pelo serviço).</summary>
    [RelayCommand]
    private async Task RemoverEspecialidade(EspecialidadeEdicao? alvo)
    {
        if (alvo is not { PodeExcluir: true }) return;
        if (!_dialogo.ConfirmarPerigo("Excluir especialidade",
                $"Excluir a especialidade \"{alvo.Nome}\"?\n\nSe houver registros usando-a, a exclusão será recusada — nesse caso, desative-a."))
            return;

        try
        {
            using var scope = _scopeFactory.CreateScope();
            var servico = scope.ServiceProvider.GetRequiredService<EspecialidadeCatalogoService>();
            var (ok, mensagem) = await servico.ExcluirAsync(alvo.Codigo);
            if (ok)
            {
                Especialidades.Remove(alvo);
                _snackbar.Sucesso(mensagem);
            }
            else
            {
                _dialogo.Aviso("Não foi possível excluir", mensagem);
            }
        }
        catch (Exception ex)
        {
            _snackbar.Erro($"Erro ao excluir a especialidade: {ex.Message}");
        }
    }

    /// <summary>Gera um nome único ("Base", "Base 2", …) dado os nomes já usados na coleção.</summary>
    private static string NomeUnico(string baseNome, IEnumerable<string> existentes)
    {
        var usados = existentes.ToList();
        var nome = baseNome;
        for (var n = 2; usados.Any(x => string.Equals(x, nome, StringComparison.OrdinalIgnoreCase)); n++)
            nome = $"{baseNome} {n}";
        return nome;
    }

    /// <summary>Lista (em texto) os nomes repetidos numa coleção, ou null se não houver duplicados.</summary>
    private static string? NomesDuplicados(IEnumerable<string> nomes)
    {
        var dup = nomes
            .GroupBy(n => (n ?? string.Empty).Trim(), StringComparer.OrdinalIgnoreCase)
            .Where(g => !string.IsNullOrWhiteSpace(g.Key) && g.Count() > 1)
            .Select(g => g.Key)
            .ToList();
        return dup.Count > 0 ? string.Join(", ", dup) : null;
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

        if (IntervaloRodadaPendenciasDias < 1)
        {
            Mensagem = "O intervalo para rodar as pendências deve ser de pelo menos 1 dia.";
            MensagemEhErro = true;
            return;
        }

        if (NomesDuplicados(Catalogo.Select(c => c.Nome)) is { } dupConv)
        {
            Mensagem = $"Há convênios com o mesmo nome: {dupConv}. Dê nomes diferentes para não confundir os cadastros.";
            MensagemEhErro = true;
            return;
        }
        if (NomesDuplicados(Modalidades.Select(m => m.Nome)) is { } dupMod)
        {
            Mensagem = $"Há modalidades com o mesmo nome: {dupMod}. Dê nomes diferentes para não confundir os lançamentos.";
            MensagemEhErro = true;
            return;
        }
        if (NomesDuplicados(Especialidades.Select(e => e.Nome)) is { } dupEsp)
        {
            Mensagem = $"Há especialidades com o mesmo nome: {dupEsp}. Dê nomes diferentes para não confundir os lançamentos.";
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
            await parametros.SalvarIntervaloRodadaPendenciasAsync(IntervaloRodadaPendenciasDias);
            await parametros.SalvarRodadaAplicaAsync(RodadaAplicaConsultas, RodadaAplicaCarteirinhas);
            await parametros.SalvarPrestadorAsync(MontarPrestador());
            await catalogo.SalvarAsync(Catalogo.Select(c => c.ParaCadastro()).ToList());

            var modalidades = scope.ServiceProvider.GetRequiredService<ModalidadeCatalogoService>();
            var especialidades = scope.ServiceProvider.GetRequiredService<EspecialidadeCatalogoService>();
            await modalidades.SalvarAsync(Modalidades.Select(m => m.ParaCadastro()).ToList());
            await especialidades.SalvarAsync(Especialidades.Select(e => e.ParaCadastro()).ToList());

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
