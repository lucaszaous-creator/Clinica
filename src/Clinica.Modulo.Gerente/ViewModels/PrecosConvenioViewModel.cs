using System.Collections.ObjectModel;
using Clinica.Application.Servicos;
using Clinica.Desktop.Controls;
using Clinica.Desktop.Shell;
using Clinica.Domain;
using Clinica.Domain.Entities;
using Clinica.Domain.Regras;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;

namespace Clinica.Gerente.ViewModels;

/// <summary>Uma linha da tabela de preço.</summary>
public sealed class LinhaPreco
{
    public required int PrecoId { get; init; }
    public required string Convenio { get; init; }
    public required string Descricao { get; init; }
    public required string Valor { get; init; }
    public required string Vigencia { get; init; }
    public required string Situacao { get; init; }

    /// <summary>Vigente HOJE — é o valor que a conciliação está propondo agora.</summary>
    public required bool ValendoAgora { get; init; }

    public static LinhaPreco De(PrecoConvenio p, DateOnly hoje) => new()
    {
        PrecoId = p.Id,
        // O NOME da operadora na tela, o código no cadastro: o nome é o que a direção
        // reconhece, o código é a identidade que a conciliação casa.
        Convenio = CatalogoConvenios.Nome(p.ConvenioCodigo),
        Descricao = p.Especialidade is { } e ? $"{p.Tipo} ({e})" : p.Tipo.ToString(),
        Valor = p.Valor.ToString("C"),
        Vigencia = p.Vigencia,
        Situacao = p.Ativo ? "Ativo" : "Inativo",
        ValendoAgora = p.VigenteEm(hoje)
    };
}

/// <summary>
/// Tabela de preço por convênio, cadastrada na DIREÇÃO (parcela 20).
///
/// É o pedido literal da clínica — "cadastrado no Gerente e refletido nos outros módulos" —
/// e também é onde faz sentido: **quem negocia tabela com a operadora é a direção; quem
/// concilia guia é o balcão**. O cadastro é do primeiro, o uso é do segundo, sobre o mesmo
/// banco. Não há sincronização nem cópia: o Financeiro lê a mesma tabela que esta tela
/// escreve, e o valor aparece na conciliação no próximo carregamento.
///
/// O que isso resolve: o valor da guia era digitado à mão em cada conciliação. Um R$ 45 no
/// lugar de R$ 145 não é recusado por ninguém, e a diferença só apareceria numa conferência
/// que a clínica não faz. Com a tabela, a conciliação deixa de ser digitação e passa a ser
/// CONFERÊNCIA contra o demonstrativo.
///
/// A **vigência** é o que faz o cadastro valer a pena: reajuste entra como linha nova, e a
/// guia de março continua valendo o preço de março.
/// </summary>
public sealed partial class PrecosConvenioViewModel : ObservableObject
{
    private readonly IServiceScopeFactory _escopos;
    private readonly ISnackbarService _snackbar;
    private readonly IDialogoService _dialogo;

    public ObservableCollection<LinhaPreco> Precos { get; } = [];

    /// <summary>Convênios do catálogo, para o combo não pedir o código de cabeça.</summary>
    public ObservableCollection<ConvenioCadastro> Convenios { get; } = [];

    public IReadOnlyList<TipoCodigo> Tipos { get; } = Enum.GetValues<TipoCodigo>();

    /// <summary>Null = vale para qualquer especialidade, que é o caso comum.</summary>
    public IReadOnlyList<Especialidade?> Especialidades { get; } =
        [null, .. Enum.GetValues<Especialidade>().Cast<Especialidade?>()];

    // ---- Formulário ----
    [ObservableProperty] private int _editandoId;
    [ObservableProperty] private ConvenioCadastro? _convenio;
    [ObservableProperty] private TipoCodigo _tipo = TipoCodigo.Acupuntura;
    [ObservableProperty] private Especialidade? _especialidade;
    [ObservableProperty] private string? _valor;
    [ObservableProperty] private DateTime? _vigenteDe;
    [ObservableProperty] private DateTime? _vigenteAte;
    [ObservableProperty] private bool _ativo = true;

    [ObservableProperty] private string _resumo = string.Empty;
    [ObservableProperty] private string? _mensagem;
    [ObservableProperty] private bool _mensagemEhErro;

    public string TituloFormulario => EditandoId == 0 ? "Novo preço" : "Editar preço";

    /// <summary>Metade visível da permissão; a que impede é o <c>Exigir</c> no comando.</summary>
    public bool PodeEditar => SessaoUsuario.Atual.Pode(Permissao.EditarFinanceiro);

    public PrecosConvenioViewModel(
        IServiceScopeFactory escopos, ISnackbarService snackbar, IDialogoService dialogo)
    {
        _escopos = escopos;
        _snackbar = snackbar;
        _dialogo = dialogo;
        _ = CarregarAsync();
    }

    partial void OnEditandoIdChanged(int value) => OnPropertyChanged(nameof(TituloFormulario));

    [RelayCommand]
    public async Task CarregarAsync()
    {
        try
        {
            Mensagem = null;
            MensagemEhErro = false;

            using var scope = _escopos.CreateScope();
            var precos = scope.ServiceProvider.GetRequiredService<PrecoConvenioService>();
            var catalogo = scope.ServiceProvider.GetRequiredService<ConvenioCatalogoService>();

            var hoje = DateOnly.FromDateTime(DateTime.Today);

            Convenios.Clear();
            foreach (var c in await catalogo.ListarAsync())
                if (c.Ativo) Convenios.Add(c);

            Precos.Clear();
            foreach (var p in await precos.CatalogoAsync())
                Precos.Add(LinhaPreco.De(p, hoje));

            var vigentes = Precos.Count(p => p.ValendoAgora);
            Resumo = Precos.Count == 0
                ? "Nenhum preço cadastrado — a conciliação continua pedindo o valor digitado."
                : $"{Precos.Count} preço(s) cadastrado(s) · {vigentes} valendo hoje.";
        }
        catch (Exception ex)
        {
            Clinica.Application.Diagnostico.Registrar("Gerente — tabela de preço não pôde ser lida", ex);
            Erro($"Não foi possível ler a tabela: {ex.Message}");
        }
    }

    [RelayCommand]
    private async Task SalvarAsync()
    {
        Mensagem = null;
        MensagemEhErro = false;

        if (Convenio is null)
        {
            Erro("Escolha o convênio.");
            return;
        }
        if (!Valores.TentarLerDecimal(Valor, out var valor) || valor <= 0m)
        {
            Erro("Informe quanto a operadora paga por esta guia (ex.: 145,00).");
            return;
        }

        try
        {
            SessaoUsuario.Atual.Exigir(Permissao.EditarFinanceiro, "cadastrar preço de convênio");

            using var scope = _escopos.CreateScope();
            var precos = scope.ServiceProvider.GetRequiredService<PrecoConvenioService>();

            await precos.SalvarAsync(new PrecoConvenio
            {
                Id = EditandoId,
                ConvenioCodigo = Convenio.Codigo,
                Tipo = Tipo,
                Especialidade = Especialidade,
                Valor = valor,
                VigenteDe = VigenteDe is { } de ? DateOnly.FromDateTime(de) : null,
                VigenteAte = VigenteAte is { } ate ? DateOnly.FromDateTime(ate) : null,
                Ativo = Ativo
            }, SessaoUsuario.Atual.Operador);

            _snackbar.Sucesso(EditandoId == 0
                ? "Preço cadastrado — a conciliação já vai propor esse valor."
                : "Preço atualizado. Vale para as guias novas; o que já foi lançado não muda.");
            Limpar();
            await CarregarAsync();
        }
        catch (Exception ex)
        {
            Clinica.Application.Diagnostico.Registrar("Gerente — preço não pôde ser salvo", ex);
            Erro(ex.Message);
        }
    }

    [RelayCommand]
    private async Task EditarAsync(LinhaPreco? linha)
    {
        if (linha is null) return;

        try
        {
            using var scope = _escopos.CreateScope();
            var precos = scope.ServiceProvider.GetRequiredService<PrecoConvenioService>();
            var p = (await precos.CatalogoAsync()).FirstOrDefault(x => x.Id == linha.PrecoId);
            if (p is null) return;

            EditandoId = p.Id;
            Convenio = Convenios.FirstOrDefault(c =>
                string.Equals(c.Codigo, p.ConvenioCodigo, StringComparison.OrdinalIgnoreCase));
            Tipo = p.Tipo;
            Especialidade = p.Especialidade;
            Valor = p.Valor.ToString("0.##");
            VigenteDe = p.VigenteDe?.ToDateTime(TimeOnly.MinValue);
            VigenteAte = p.VigenteAte?.ToDateTime(TimeOnly.MinValue);
            Ativo = p.Ativo;
        }
        catch (Exception ex)
        {
            Clinica.Application.Diagnostico.Registrar("Gerente — preço não pôde ser aberto", ex);
            Erro(ex.Message);
        }
    }

    /// <summary>
    /// Excluir só serve para o que foi cadastrado errado. Preço que já propôs valor a alguma
    /// guia deve ser ENCERRADO pela vigência: o lançamento guardou o valor copiado, mas
    /// apagar a linha apaga a explicação de por que aquela guia valeu aquilo.
    /// </summary>
    [RelayCommand]
    private async Task ExcluirAsync(LinhaPreco? linha)
    {
        if (linha is null) return;

        try
        {
            SessaoUsuario.Atual.Exigir(Permissao.EditarFinanceiro, "excluir preço de convênio");

            if (!_dialogo.ConfirmarPerigo("Excluir preço",
                    $"Apagar o preço de {linha.Convenio} ({linha.Descricao})? Se ele já propôs "
                    + "valor a alguma guia, prefira ENCERRAR pela vigência: os lançamentos guardam "
                    + "o valor copiado, mas apagar a linha apaga a explicação de por que aquela "
                    + "guia valeu aquilo.")) return;

            using var scope = _escopos.CreateScope();
            var precos = scope.ServiceProvider.GetRequiredService<PrecoConvenioService>();
            await precos.ExcluirAsync(linha.PrecoId);

            _snackbar.Info("Preço excluído.");
            if (EditandoId == linha.PrecoId) Limpar();
            await CarregarAsync();
        }
        catch (Exception ex)
        {
            Clinica.Application.Diagnostico.Registrar("Gerente — preço não pôde ser excluído", ex);
            Erro(ex.Message);
        }
    }

    [RelayCommand]
    private void Limpar()
    {
        EditandoId = 0;
        Convenio = null;
        Tipo = TipoCodigo.Acupuntura;
        Especialidade = null;
        Valor = null;
        VigenteDe = VigenteAte = null;
        Ativo = true;
    }

    private void Erro(string mensagem)
    {
        Mensagem = mensagem;
        MensagemEhErro = true;
    }
}
