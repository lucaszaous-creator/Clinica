using System.Collections.ObjectModel;
using Clinica.Application.Servicos;
using Clinica.Desktop.Controls;
using Clinica.Desktop.Shell;
using Clinica.Domain.Entities;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;

namespace Clinica.Financeiro.ViewModels;

/// <summary>Uma taxa do catálogo, como aparece na lista.</summary>
public sealed class LinhaTaxa
{
    public required int TaxaId { get; init; }
    public required string Descricao { get; init; }
    public required string Prazo { get; init; }
    public required string Vigencia { get; init; }
    public required bool Ativa { get; init; }
    public required string Situacao { get; init; }

    public static LinhaTaxa De(TaxaCartao t) => new()
    {
        TaxaId = t.Id,
        Descricao = t.Descricao,
        Prazo = t.DiasParaReceber == 0 ? "na hora" : $"em {t.DiasParaReceber} dia(s)",
        Vigencia = (t.VigenteDe, t.VigenteAte) switch
        {
            (null, null) => "sem prazo",
            ({ } de, null) => $"desde {de:dd/MM/yyyy}",
            (null, { } ate) => $"até {ate:dd/MM/yyyy}",
            var (de, ate) => $"{de:dd/MM/yyyy} a {ate:dd/MM/yyyy}"
        },
        Ativa = t.Ativa,
        Situacao = t.Ativa ? "Ativa" : "Inativa"
    };
}

/// <summary>
/// Taxas da maquininha e imposto retido.
///
/// Existe porque o caixa só conhecia o BRUTO: a clínica passava R$ 150 no cartão, a tela
/// dizia R$ 150, e o extrato da adquirente trazia R$ 145,20 trinta dias depois. A
/// diferença aparecia no fim do mês como um caixa que não bate, sem ninguém saber de onde
/// veio.
///
/// A vigência é o que faz o cadastro valer a pena: a adquirente renegocia, e o que vale
/// no recebimento de março é o percentual de março. Mudar aqui não reescreve o mês já
/// conciliado.
/// </summary>
public sealed partial class TaxasViewModel : ObservableObject
{
    private readonly IServiceScopeFactory _escopos;
    private readonly ISnackbarService _snackbar;
    private readonly IDialogoService _dialogo;

    public ObservableCollection<LinhaTaxa> Taxas { get; } = [];

    public IReadOnlyList<ModalidadeCartao> Modalidades { get; } = Enum.GetValues<ModalidadeCartao>();

    // ---- Formulário ----
    [ObservableProperty] private int _editandoId;
    [ObservableProperty] private string? _adquirente;
    [ObservableProperty] private string? _bandeira;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(EhParcelado))]
    private ModalidadeCartao _modalidade = ModalidadeCartao.CreditoAVista;

    [ObservableProperty] private string? _percentual;
    [ObservableProperty] private string? _diasParaReceber = "30";
    [ObservableProperty] private string? _parcelasDe;
    [ObservableProperty] private string? _parcelasAte;
    [ObservableProperty] private DateTime? _vigenteDe;
    [ObservableProperty] private DateTime? _vigenteAte;
    [ObservableProperty] private bool _ativa = true;

    /// <summary>Faixa de parcelas só faz sentido no crédito parcelado.</summary>
    public bool EhParcelado => Modalidade == ModalidadeCartao.CreditoParcelado;

    public string TituloFormulario => EditandoId == 0 ? "Nova taxa" : "Editar taxa";

    // ---- Imposto ----
    [ObservableProperty] private string? _aliquotaImposto;

    [ObservableProperty] private string? _mensagem;
    [ObservableProperty] private bool _mensagemEhErro;

    /// <summary>Metade visível da permissão; a que impede é o <c>Exigir</c> no comando.</summary>
    public bool PodeEditar => SessaoUsuario.Atual.Pode(Permissao.EditarFinanceiro);

    public TaxasViewModel(
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
            var taxas = scope.ServiceProvider.GetRequiredService<TaxaService>();
            var parametros = scope.ServiceProvider.GetRequiredService<ParametrosService>();

            Taxas.Clear();
            foreach (var t in await taxas.CatalogoAsync())
                Taxas.Add(LinhaTaxa.De(t));

            var aliquota = await parametros.ObterAliquotaImpostoAsync();
            // Zero é "não configurado" e o campo fica VAZIO, não "0" — um zero digitado e
            // um zero padrão parecem iguais na tela e não são a mesma decisão.
            AliquotaImposto = aliquota > 0m ? aliquota.ToString("0.##") : null;
        }
        catch (Exception ex)
        {
            Clinica.Application.Diagnostico.Registrar("Financeiro — taxas não puderam ser lidas", ex);
            Erro(ex.Message);
        }
    }

    [RelayCommand]
    private async Task SalvarAsync()
    {
        Mensagem = null;
        MensagemEhErro = false;

        if (string.IsNullOrWhiteSpace(Adquirente))
        {
            Erro("Diga de qual adquirente é a taxa (Stone, Cielo, Rede…).");
            return;
        }
        if (!Valores.TentarLerDecimal(Percentual, out var percentual))
        {
            Erro("Informe o percentual da taxa (ex.: 3,2).");
            return;
        }
        if (!int.TryParse(DiasParaReceber, out var dias) || dias < 0)
        {
            Erro("Informe em quantos dias o dinheiro cai (0 para na hora).");
            return;
        }

        int? de = null, ate = null;
        if (EhParcelado)
        {
            if (!string.IsNullOrWhiteSpace(ParcelasDe) && int.TryParse(ParcelasDe, out var d)) de = d;
            if (!string.IsNullOrWhiteSpace(ParcelasAte) && int.TryParse(ParcelasAte, out var a)) ate = a;
        }

        try
        {
            SessaoUsuario.Atual.Exigir(Permissao.EditarFinanceiro, "cadastrar taxa de cartão");

            using var scope = _escopos.CreateScope();
            var taxas = scope.ServiceProvider.GetRequiredService<TaxaService>();

            await taxas.SalvarAsync(new TaxaCartao
            {
                Id = EditandoId,
                Adquirente = Adquirente!,
                Bandeira = Bandeira,
                Modalidade = Modalidade,
                Percentual = percentual,
                DiasParaReceber = dias,
                ParcelasDe = de,
                ParcelasAte = ate,
                VigenteDe = VigenteDe is { } vd ? DateOnly.FromDateTime(vd) : null,
                VigenteAte = VigenteAte is { } va ? DateOnly.FromDateTime(va) : null,
                Ativa = Ativa
            }, SessaoUsuario.Atual.Operador);

            _snackbar.Sucesso(EditandoId == 0 ? "Taxa cadastrada." : "Taxa atualizada.");
            Limpar();
            await CarregarAsync();
        }
        catch (Exception ex)
        {
            Clinica.Application.Diagnostico.Registrar("Financeiro — taxa não pôde ser salva", ex);
            Erro(ex.Message);
        }
    }

    [RelayCommand]
    private async Task EditarAsync(LinhaTaxa? linha)
    {
        if (linha is null) return;

        try
        {
            using var scope = _escopos.CreateScope();
            var taxas = scope.ServiceProvider.GetRequiredService<TaxaService>();
            var t = (await taxas.CatalogoAsync()).FirstOrDefault(x => x.Id == linha.TaxaId);
            if (t is null) return;

            EditandoId = t.Id;
            Adquirente = t.Adquirente;
            Bandeira = t.Bandeira;
            Modalidade = t.Modalidade;
            Percentual = t.Percentual.ToString("0.##");
            DiasParaReceber = t.DiasParaReceber.ToString();
            ParcelasDe = t.ParcelasDe?.ToString();
            ParcelasAte = t.ParcelasAte?.ToString();
            VigenteDe = t.VigenteDe?.ToDateTime(TimeOnly.MinValue);
            VigenteAte = t.VigenteAte?.ToDateTime(TimeOnly.MinValue);
            Ativa = t.Ativa;
        }
        catch (Exception ex)
        {
            Clinica.Application.Diagnostico.Registrar("Financeiro — taxa não pôde ser aberta", ex);
            Erro(ex.Message);
        }
    }

    /// <summary>
    /// Excluir só serve para o que foi cadastrado errado. A taxa que já descontou algum
    /// recebimento deve ser INATIVADA: o lançamento guardou o valor copiado, mas apagar a
    /// regra apaga a explicação de por que aquele desconto foi aquele.
    /// </summary>
    [RelayCommand]
    private async Task ExcluirAsync(LinhaTaxa? linha)
    {
        if (linha is null) return;

        try
        {
            SessaoUsuario.Atual.Exigir(Permissao.EditarFinanceiro, "excluir taxa de cartão");

            if (!_dialogo.ConfirmarPerigo("Excluir taxa",
                    $"Apagar a taxa {linha.Descricao}? Se ela já descontou algum recebimento, "
                    + "prefira INATIVAR: os lançamentos guardam o valor descontado, mas apagar "
                    + "a regra apaga a explicação de por que o desconto foi aquele.")) return;

            using var scope = _escopos.CreateScope();
            var taxas = scope.ServiceProvider.GetRequiredService<TaxaService>();
            await taxas.ExcluirAsync(linha.TaxaId);

            _snackbar.Info("Taxa excluída.");
            if (EditandoId == linha.TaxaId) Limpar();
            await CarregarAsync();
        }
        catch (Exception ex)
        {
            Clinica.Application.Diagnostico.Registrar("Financeiro — taxa não pôde ser excluída", ex);
            Erro(ex.Message);
        }
    }

    [RelayCommand]
    private async Task SalvarImpostoAsync()
    {
        Mensagem = null;
        MensagemEhErro = false;

        // Campo vazio = zerar a retenção, que é o padrão de quem não retém.
        decimal aliquota = 0m;
        if (!string.IsNullOrWhiteSpace(AliquotaImposto)
            && !Valores.TentarLerDecimal(AliquotaImposto, out aliquota))
        {
            Erro("Informe a alíquota em percentual (ex.: 2,5) ou deixe em branco para não reter.");
            return;
        }

        try
        {
            SessaoUsuario.Atual.Exigir(Permissao.EditarFinanceiro, "mudar a alíquota de imposto");

            using var scope = _escopos.CreateScope();
            var parametros = scope.ServiceProvider.GetRequiredService<ParametrosService>();
            await parametros.SalvarAliquotaImpostoAsync(aliquota);

            _snackbar.Sucesso(aliquota > 0m
                ? $"Imposto retido em {aliquota:0.##}%."
                : "Retenção de imposto desligada.");
        }
        catch (Exception ex)
        {
            Clinica.Application.Diagnostico.Registrar("Financeiro — alíquota não pôde ser salva", ex);
            Erro(ex.Message);
        }
    }

    [RelayCommand]
    private void Limpar()
    {
        EditandoId = 0;
        Adquirente = Bandeira = Percentual = ParcelasDe = ParcelasAte = null;
        Modalidade = ModalidadeCartao.CreditoAVista;
        DiasParaReceber = "30";
        VigenteDe = VigenteAte = null;
        Ativa = true;
    }

    private void Erro(string mensagem)
    {
        Mensagem = mensagem;
        MensagemEhErro = true;
    }
}
