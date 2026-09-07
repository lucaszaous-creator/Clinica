using System.Collections.ObjectModel;
using Clinica.Application.Servicos;
using Clinica.Desktop.Controls;
using Clinica.Desktop.Shell;
using Clinica.Domain;
using Clinica.Domain.Entities;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;

namespace Clinica.Desktop.Shell.Componentes;

/// <summary>Uma linha da tabela do particular.</summary>
public sealed class LinhaPrecoParticular
{
    public required int PrecoId { get; init; }
    public required string Modalidade { get; init; }
    public required string Especialidade { get; init; }
    public required string Valor { get; init; }
    public required string Vigencia { get; init; }
    public required bool ValendoAgora { get; init; }

    public static LinhaPrecoParticular De(PrecoParticular p, DateOnly hoje) => new()
    {
        PrecoId = p.Id,
        Modalidade = p.ModalidadeNome,
        // "qualquer" escrito, e não célula vazia: vazio se lê como "não preenchido".
        Especialidade = string.IsNullOrWhiteSpace(p.EspecialidadeCodigo)
            ? "qualquer especialidade" : p.EspecialidadeNome,
        Valor = p.Valor.ToString("C"),
        Vigencia = p.Vigencia,
        ValendoAgora = p.VigenteEm(hoje)
    };
}

/// <summary>
/// Tabela de preço do PARTICULAR por especialidade atendida (set/2026).
///
/// O pedido da direção: <i>"um cadastro de preço por tipo de especialidade atendida quando o
/// paciente for particular"</i> — e, logo depois, <i>"o ideal seria a recepção também
/// cadastrar e editar preços"</i>. Por isso a tela mora no SHELL, como o Pacotes (parcela
/// 60): a Recepção a publica como item e o Gerente como aba da Tabela de preço, e os dois
/// apontam para a MESMA chave (<c>ChavesSuite.PrecosParticular</c>) — o Gerente Geral, que
/// carrega os dois, mostra uma linha só.
///
/// Quem lê é o balcão (o Finalizar do particular propõe este valor) e o Financeiro (a aba
/// Particulares da Conciliação). Sem linha aqui os dois pedem o valor digitado — o sistema
/// não inventa preço.
///
/// ⚠️ A permissão é <see cref="Permissao.VenderPacote"/> OU <see cref="Permissao.EditarFinanceiro"/>,
/// e não um bit novo: combinar o preço do particular é o mesmo corte que a parcela 60 fez
/// para vender o pacote — é do BALCÃO, com o paciente na frente, sem abrir o caixa e as
/// contas junto. E o enum de permissões tem UM bit sobrando antes de virar <c>long</c>
/// (coluna de produção); gastá-lo aqui, quando o bit existente já nomeia o ato, seria
/// pagar a migration mais cara do sistema por uma caixinha a mais em Acessos.
/// </summary>
public sealed partial class PrecosParticularViewModel : ObservableObject
{
    private readonly IServiceScopeFactory _escopos;
    private readonly ISnackbarService _snackbar;
    private readonly IDialogoService _dialogo;

    public ObservableCollection<LinhaPrecoParticular> Precos { get; } = [];
    private readonly List<LinhaPrecoParticular> _todas = [];

    /// <summary>Só as linhas vigentes hoje — o que o balcão está propondo agora.</summary>
    [ObservableProperty] private bool _soValendoHoje;

    partial void OnSoValendoHojeChanged(bool value) => Refiltrar();

    [ObservableProperty] private string _resumo = string.Empty;
    [ObservableProperty] private string _vazioDescricao = string.Empty;
    [ObservableProperty] private string? _mensagem;
    [ObservableProperty] private bool _mensagemEhErro;

    /// <summary>Metade visível da permissão; a que impede é o <c>ExigirAlgum</c> no comando.</summary>
    public bool PodeEditar => SessaoUsuario.Atual.PodeAlgum(
        Permissao.VenderPacote | Permissao.EditarFinanceiro);

    public PrecosParticularViewModel(
        IServiceScopeFactory escopos, ISnackbarService snackbar, IDialogoService dialogo)
    {
        _escopos = escopos;
        _snackbar = snackbar;
        _dialogo = dialogo;
        _ = CarregarAsync();
    }

    [RelayCommand]
    public async Task CarregarAsync()
    {
        try
        {
            Mensagem = null;
            MensagemEhErro = false;

            using var scope = _escopos.CreateScope();
            var precos = scope.ServiceProvider.GetRequiredService<PrecoParticularService>();
            var hoje = DateOnly.FromDateTime(DateTime.Today);

            // Entre o `Clear()` e o último `Add` não pode haver `await` (parcela 62).
            var linhas = (await precos.CatalogoAsync())
                .Select(p => LinhaPrecoParticular.De(p, hoje)).ToList();

            _todas.Clear();
            _todas.AddRange(linhas);
            Refiltrar();
        }
        catch (Exception ex)
        {
            Clinica.Application.Diagnostico.Registrar("Gerente — tabela do particular não pôde ser lida", ex);
            Erro($"Não foi possível ler a tabela do particular: {ex.Message}");
        }
    }

    private void Refiltrar()
    {
        Precos.Clear();
        foreach (var p in _todas.Where(p => !SoValendoHoje || p.ValendoAgora))
            Precos.Add(p);

        var vigentes = _todas.Count(p => p.ValendoAgora);
        Resumo = _todas.Count == 0
            ? "Nenhum preço cadastrado — o Finalizar e a Conciliação continuam pedindo o valor digitado."
            : SoValendoHoje
                ? $"{Precos.Count} de {_todas.Count} preço(s) valendo hoje."
                : $"{Precos.Count} preço(s) cadastrado(s) · {vigentes} valendo hoje.";

        VazioDescricao = SoValendoHoje
            ? "Nenhum preço vale hoje — desmarque o filtro para ver a tabela inteira."
            : "Nenhum preço cadastrado. Sem tabela, o Finalizar do particular e a Conciliação pedem o valor digitado — o sistema não inventa um valor.";
    }

    [RelayCommand]
    private async Task NovoPrecoAsync() => await AbrirAsync(0);

    [RelayCommand]
    private async Task EditarAsync(LinhaPrecoParticular? linha)
    {
        if (linha is null) return;
        await AbrirAsync(linha.PrecoId);
    }

    private async Task AbrirAsync(int precoId)
    {
        SessaoUsuario.Atual.ExigirAlgum(
            Permissao.VenderPacote | Permissao.EditarFinanceiro, "cadastrar preço do particular");

        var vm = new PrecoParticularEdicaoViewModel(_escopos, precoId);
        var janela = new PrecoParticularWindow(vm)
        {
            Owner = JanelaDona.Atual()
        };

        if (janela.ShowDialog() != true) return;

        _snackbar.Sucesso(precoId == 0
            ? "Preço cadastrado — o Finalizar do particular já vai propor esse valor."
            : "Preço atualizado. Vale para as sessões novas; o que já foi lançado não muda.");
        await CarregarAsync();
    }

    /// <summary>Excluir é para o cadastrado errado; preço que já propôs valor se ENCERRA pela vigência.</summary>
    [RelayCommand]
    private async Task ExcluirAsync(LinhaPrecoParticular? linha)
    {
        if (linha is null) return;

        try
        {
            SessaoUsuario.Atual.ExigirAlgum(
                Permissao.VenderPacote | Permissao.EditarFinanceiro, "excluir preço do particular");

            if (!_dialogo.ConfirmarPerigo("Excluir preço",
                    $"Apagar o preço de {linha.Modalidade} ({linha.Especialidade})? Se ele já "
                    + "propôs valor a alguma sessão, prefira ENCERRAR pela vigência: os lançamentos "
                    + "guardam o valor copiado, mas apagar a linha apaga a explicação de por que "
                    + "aquela sessão valeu aquilo.")) return;

            using var scope = _escopos.CreateScope();
            await scope.ServiceProvider.GetRequiredService<PrecoParticularService>()
                .ExcluirAsync(linha.PrecoId);

            _snackbar.Info("Preço excluído.");
            await CarregarAsync();
        }
        catch (Exception ex)
        {
            Clinica.Application.Diagnostico.Registrar("Gerente — preço do particular não pôde ser excluído", ex);
            Erro(ex.Message);
        }
    }

    private void Erro(string mensagem)
    {
        Mensagem = mensagem;
        MensagemEhErro = true;
    }
}

/// <summary>Uma opção de modalidade ou especialidade no combo (código + nome do catálogo).</summary>
public sealed record OpcaoCatalogo(string? Codigo, string Nome);

/// <summary>
/// O preço de uma sessão particular, na janela — cadastro e correção.
///
/// A modalidade é obrigatória (é o procedimento); a especialidade é o que diferencia a
/// consulta de psiquiatria da de geriatria, e em branco vale para qualquer uma. Preço com
/// especialidade VENCE o genérico da modalidade; reajuste entra como linha nova.
/// </summary>
public sealed partial class PrecoParticularEdicaoViewModel : ObservableObject
{
    private readonly IServiceScopeFactory _escopos;
    private readonly int _precoId;

    public ObservableCollection<OpcaoCatalogo> Modalidades { get; } = [];
    public ObservableCollection<OpcaoCatalogo> Especialidades { get; } = [];

    public string Titulo => _precoId == 0 ? "Preço do particular" : "Editar preço do particular";

    [ObservableProperty] private OpcaoCatalogo? _modalidade;
    [ObservableProperty] private OpcaoCatalogo? _especialidade;
    [ObservableProperty] private string? _valor;
    [ObservableProperty] private DateTime? _vigenteDe;
    [ObservableProperty] private DateTime? _vigenteAte;
    [ObservableProperty] private bool _ativo = true;

    [ObservableProperty] private string? _mensagem;
    [ObservableProperty] private bool _mensagemEhErro;
    [ObservableProperty] private bool _salvando;

    public event Action? Concluido;

    public PrecoParticularEdicaoViewModel(IServiceScopeFactory escopos, int precoId)
    {
        _escopos = escopos;
        _precoId = precoId;
        _ = CarregarAsync();
    }

    private async Task CarregarAsync()
    {
        try
        {
            using var scope = _escopos.CreateScope();

            // Os catálogos da clínica (embutidos garantidos + variantes), só os ativos.
            // Monta e só então publica: entre o Clear e o último Add não pode haver await.
            var modalidades = (await scope.ServiceProvider
                    .GetRequiredService<ModalidadeCatalogoService>().ListarAsync())
                .Where(m => m.Ativo)
                .Select(m => new OpcaoCatalogo(m.Codigo, m.Nome))
                .ToList();
            var especialidades = (await scope.ServiceProvider
                    .GetRequiredService<EspecialidadeCatalogoService>().ListarAsync())
                .Where(e => e.Ativo)
                .Select(e => new OpcaoCatalogo(e.Codigo, e.Nome))
                .ToList();

            PrecoParticular? p = null;
            if (_precoId != 0)
                p = (await scope.ServiceProvider.GetRequiredService<PrecoParticularService>()
                    .CatalogoAsync()).FirstOrDefault(x => x.Id == _precoId);

            Modalidades.Clear();
            foreach (var m in modalidades) Modalidades.Add(m);

            Especialidades.Clear();
            Especialidades.Add(new OpcaoCatalogo(null, "(qualquer especialidade)"));
            foreach (var e in especialidades) Especialidades.Add(e);

            Especialidade = Especialidades[0];

            if (p is null) return;

            Modalidade = Modalidades.FirstOrDefault(m =>
                string.Equals(m.Codigo, p.ModalidadeCodigo, StringComparison.OrdinalIgnoreCase));
            Especialidade = Especialidades.FirstOrDefault(e =>
                string.Equals(e.Codigo, p.EspecialidadeCodigo, StringComparison.OrdinalIgnoreCase))
                ?? Especialidades[0];
            Valor = p.Valor.ToString("0.##");
            VigenteDe = p.VigenteDe?.ToDateTime(TimeOnly.MinValue);
            VigenteAte = p.VigenteAte?.ToDateTime(TimeOnly.MinValue);
            Ativo = p.Ativo;
        }
        catch (Exception ex)
        {
            Clinica.Application.Diagnostico.Registrar("Gerente — preço do particular não pôde ser aberto", ex);
            Erro(ex.Message);
        }
    }

    [RelayCommand]
    private async Task SalvarAsync()
    {
        Mensagem = null;
        MensagemEhErro = false;

        if (Modalidade is null)
        {
            Erro("Escolha a modalidade (acupuntura, consulta, BSV…).");
            return;
        }
        if (!Valores.TentarLerDecimal(Valor, out var valor) || valor <= 0m)
        {
            Erro("Informe quanto a clínica cobra por esta sessão (ex.: 180,00).");
            return;
        }

        try
        {
            Salvando = true;
            SessaoUsuario.Atual.ExigirAlgum(
                Permissao.VenderPacote | Permissao.EditarFinanceiro, "cadastrar preço do particular");

            using var scope = _escopos.CreateScope();
            await scope.ServiceProvider.GetRequiredService<PrecoParticularService>()
                .SalvarAsync(new PrecoParticular
                {
                    Id = _precoId,
                    ModalidadeCodigo = Modalidade.Codigo!,
                    EspecialidadeCodigo = Especialidade?.Codigo,
                    Valor = valor,
                    VigenteDe = VigenteDe is { } de ? DateOnly.FromDateTime(de) : null,
                    VigenteAte = VigenteAte is { } ate ? DateOnly.FromDateTime(ate) : null,
                    Ativo = Ativo
                }, SessaoUsuario.Atual.Operador);

            Concluido?.Invoke();
        }
        catch (Exception ex)
        {
            Clinica.Application.Diagnostico.Registrar("Gerente — preço do particular não pôde ser salvo", ex);
            Erro(ex.Message);
        }
        finally
        {
            Salvando = false;
        }
    }

    private void Erro(string mensagem)
    {
        Mensagem = mensagem;
        MensagemEhErro = true;
    }
}
