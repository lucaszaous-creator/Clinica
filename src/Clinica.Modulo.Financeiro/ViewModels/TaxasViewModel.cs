using System.Collections.ObjectModel;
using Clinica.Application.Servicos;
using Clinica.Desktop.Controls;
using Clinica.Desktop.Shell;
using Clinica.Domain.Entities;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;

namespace Clinica.Financeiro.ViewModels;

/// <summary>Um tributo do regime, como aparece na lista.</summary>
public sealed class LinhaTributo
{
    public required int TributoId { get; init; }
    public required string Descricao { get; init; }
    public required string Nome { get; init; }
    public required string Vigencia { get; init; }
    public required string Situacao { get; init; }
    public required bool Ativo { get; init; }

    /// <summary>Vigente HOJE — é o que de fato está sendo descontado agora.</summary>
    public required bool ValendoAgora { get; init; }

    /// <summary>
    /// Retido na fonte por um convênio, não recolhido pela clínica. A lista distingue os
    /// dois porque o que a clínica faz com cada um é diferente: o retido já saiu.
    /// </summary>
    public required string Origem { get; init; }

    public static LinhaTributo De(Tributo t, DateOnly hoje) => new()
    {
        TributoId = t.Id,
        Descricao = t.Descricao,
        Nome = t.Nome,
        Origem = t.RetidoNaFonte
            ? $"retido na fonte por {t.ConvenioCodigo}"
            : "recolhido pela clínica",
        Vigencia = t.Vigencia,
        Situacao = t.Ativo ? "Ativo" : "Inativo",
        Ativo = t.Ativo,
        ValendoAgora = t.VigenteEm(hoje)
    };
}

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

    // ---- Regime tributário (parcela 15) ----
    public ObservableCollection<LinhaTributo> Tributos { get; } = [];

    [ObservableProperty] private int _editandoTributoId;
    [ObservableProperty] private string? _sigla;
    [ObservableProperty] private string? _nomeTributo;
    [ObservableProperty] private string? _percentualTributo;
    [ObservableProperty] private string? _baseTributo = "100";

    /// <summary>
    /// Convênio que RETÉM este tributo na fonte (parcela 18). Vazio = tributo próprio da
    /// clínica. Texto livre com o código do catálogo, como o resto do sistema referencia
    /// convênio.
    /// </summary>
    [ObservableProperty] private string? _convenioTributo;
    [ObservableProperty] private DateTime? _tributoVigenteDe;
    [ObservableProperty] private DateTime? _tributoVigenteAte;
    [ObservableProperty] private bool _tributoAtivo = true;

    /// <summary>O que sai de cada real recebido hoje, somando os tributos vigentes.</summary>
    [ObservableProperty] private string _cargaHoje = "—";

    /// <summary>
    /// Explica QUAL mecanismo está valendo. Enquanto não houver tributo cadastrado, a
    /// alíquota única da parcela 9 continua sendo aplicada — e a tela precisa dizer isso,
    /// senão o campo de baixo parece um resto de tela que não faz nada.
    /// </summary>
    [ObservableProperty] private string _origemDaCarga = string.Empty;

    public string TituloTributo => EditandoTributoId == 0 ? "Novo tributo" : "Editar tributo";

    // ---- Imposto (alíquota única — legado da parcela 9) ----
    [ObservableProperty] private string? _aliquotaImposto;

    // ---- Simulador (parcela 15) ----
    [ObservableProperty] private string? _simValor = "200";
    [ObservableProperty] private string? _simAdquirente;
    [ObservableProperty] private string? _simBandeira;
    [ObservableProperty] private FormaPagamento _simForma = FormaPagamento.CartaoCredito;
    [ObservableProperty] private string? _simParcelas = "1";
    [ObservableProperty] private bool _simReterImposto = true;
    [ObservableProperty] private string? _simResultado;
    [ObservableProperty] private bool _simSemTaxa;

    public IReadOnlyList<FormaPagamento> FormasSimulacao { get; } =
    [
        FormaPagamento.Dinheiro,
        FormaPagamento.Pix,
        FormaPagamento.CartaoDebito,
        FormaPagamento.CartaoCredito
    ];

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
    partial void OnEditandoTributoIdChanged(int value) => OnPropertyChanged(nameof(TituloTributo));

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

            var tributos = scope.ServiceProvider.GetRequiredService<TributoService>();
            var hoje = DateOnly.FromDateTime(DateTime.Today);

            Tributos.Clear();
            foreach (var t in await tributos.CatalogoAsync())
                Tributos.Add(LinhaTributo.De(t, hoje));

            var carga = await tributos.AliquotaTotalAsync(hoje);
            CargaHoje = carga > 0m ? $"{carga:0.####}%" : "—";

            // Qual mecanismo está valendo. Sem esta linha, quem cadastra o primeiro
            // tributo não entende por que o campo da alíquota única parou de ter efeito.
            var vigentes = Tributos.Count(t => t.ValendoAgora);
            OrigemDaCarga = vigentes > 0
                ? $"{vigentes} tributo(s) vigente(s) hoje. A alíquota única abaixo está desligada."
                : aliquota > 0m
                    ? "Nenhum tributo cadastrado — vale a alíquota única abaixo."
                    : "Nenhum tributo e nenhuma alíquota: não se retém imposto.";

            await SimularAsync();
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

    // ==================== Regime tributário (parcela 15) ====================

    [RelayCommand]
    private async Task SalvarTributoAsync()
    {
        Mensagem = null;
        MensagemEhErro = false;

        if (string.IsNullOrWhiteSpace(Sigla))
        {
            Erro("Informe a sigla do tributo (ISS, PIS, COFINS, IRPJ, CSLL, SIMPLES).");
            return;
        }
        if (!Valores.TentarLerDecimal(PercentualTributo, out var percentual))
        {
            Erro("Informe a alíquota do tributo (ex.: 3 ou 0,65).");
            return;
        }
        // Base vazia = 100%, que é o caso normal. Exigir o número em toda linha faria a
        // clínica digitar "100" cinco vezes para cadastrar um regime do Simples.
        var baseCalculo = 100m;
        if (!string.IsNullOrWhiteSpace(BaseTributo)
            && !Valores.TentarLerDecimal(BaseTributo, out baseCalculo))
        {
            Erro("A base de cálculo vai de 0 a 100 (ex.: 32 no IRPJ do Lucro Presumido).");
            return;
        }

        try
        {
            SessaoUsuario.Atual.Exigir(Permissao.EditarFinanceiro, "cadastrar tributo");

            using var scope = _escopos.CreateScope();
            var tributos = scope.ServiceProvider.GetRequiredService<TributoService>();

            await tributos.SalvarAsync(new Tributo
            {
                Id = EditandoTributoId,
                Sigla = Sigla!,
                Nome = string.IsNullOrWhiteSpace(NomeTributo) ? Sigla! : NomeTributo!,
                Percentual = percentual,
                BasePercentual = baseCalculo,
                ConvenioCodigo = ConvenioTributo,
                VigenteDe = TributoVigenteDe is { } de ? DateOnly.FromDateTime(de) : null,
                VigenteAte = TributoVigenteAte is { } ate ? DateOnly.FromDateTime(ate) : null,
                Ativo = TributoAtivo
            }, SessaoUsuario.Atual.Operador);

            _snackbar.Sucesso(EditandoTributoId == 0 ? "Tributo cadastrado." : "Tributo atualizado.");
            LimparTributo();
            await CarregarAsync();
        }
        catch (Exception ex)
        {
            Clinica.Application.Diagnostico.Registrar("Financeiro — tributo não pôde ser salvo", ex);
            Erro(ex.Message);
        }
    }

    [RelayCommand]
    private async Task EditarTributoAsync(LinhaTributo? linha)
    {
        if (linha is null) return;

        try
        {
            using var scope = _escopos.CreateScope();
            var tributos = scope.ServiceProvider.GetRequiredService<TributoService>();
            var t = (await tributos.CatalogoAsync()).FirstOrDefault(x => x.Id == linha.TributoId);
            if (t is null) return;

            EditandoTributoId = t.Id;
            Sigla = t.Sigla;
            NomeTributo = t.Nome;
            PercentualTributo = t.Percentual.ToString("0.####");
            BaseTributo = t.BasePercentual.ToString("0.####");
            ConvenioTributo = t.ConvenioCodigo;
            TributoVigenteDe = t.VigenteDe?.ToDateTime(TimeOnly.MinValue);
            TributoVigenteAte = t.VigenteAte?.ToDateTime(TimeOnly.MinValue);
            TributoAtivo = t.Ativo;
        }
        catch (Exception ex)
        {
            Clinica.Application.Diagnostico.Registrar("Financeiro — tributo não pôde ser aberto", ex);
            Erro(ex.Message);
        }
    }

    /// <summary>
    /// Excluir só serve para o que foi cadastrado errado. Tributo que já reteve alguma
    /// coisa deve ser ENCERRADO pela vigência: o lançamento guardou o valor copiado, mas
    /// apagar a regra apaga a explicação de por que a retenção daquele mês foi aquela.
    /// </summary>
    [RelayCommand]
    private async Task ExcluirTributoAsync(LinhaTributo? linha)
    {
        if (linha is null) return;

        try
        {
            SessaoUsuario.Atual.Exigir(Permissao.EditarFinanceiro, "excluir tributo");

            if (!_dialogo.ConfirmarPerigo("Excluir tributo",
                    $"Apagar {linha.Descricao}? Se ele já reteve imposto de algum recebimento, "
                    + "prefira ENCERRAR pela vigência: os lançamentos guardam o valor retido, "
                    + "mas apagar a regra apaga a explicação de por que a retenção foi aquela.")) return;

            using var scope = _escopos.CreateScope();
            var tributos = scope.ServiceProvider.GetRequiredService<TributoService>();
            await tributos.ExcluirAsync(linha.TributoId);

            _snackbar.Info("Tributo excluído.");
            if (EditandoTributoId == linha.TributoId) LimparTributo();
            await CarregarAsync();
        }
        catch (Exception ex)
        {
            Clinica.Application.Diagnostico.Registrar("Financeiro — tributo não pôde ser excluído", ex);
            Erro(ex.Message);
        }
    }

    [RelayCommand]
    private void LimparTributo()
    {
        EditandoTributoId = 0;
        Sigla = NomeTributo = PercentualTributo = ConvenioTributo = null;
        BaseTributo = "100";
        TributoVigenteDe = TributoVigenteAte = null;
        TributoAtivo = true;
    }

    // ==================== Simulador (parcela 15) ====================

    /// <summary>
    /// "Se eu passar R$ 200 no crédito 3x na Cielo, quanto sobra e quando cai?"
    ///
    /// É a pergunta do balcão e a da negociação com a adquirente, e até aqui só dava para
    /// respondê-la lançando uma venda de verdade. Não grava nada — usa o mesmo
    /// <see cref="TaxaService.CalcularAsync"/> da venda real, para o número simulado ser o
    /// mesmo que vai acontecer. Refazer a conta aqui com uma fórmula própria produziria
    /// dois resultados diferentes para a mesma pergunta.
    /// </summary>
    [RelayCommand]
    private async Task SimularAsync()
    {
        try
        {
            SimResultado = null;
            SimSemTaxa = false;

            if (!Valores.TentarLerDecimal(SimValor, out var bruto) || bruto <= 0m) return;
            if (!int.TryParse(SimParcelas, out var parcelas) || parcelas < 1) parcelas = 1;

            using var scope = _escopos.CreateScope();
            var taxas = scope.ServiceProvider.GetRequiredService<TaxaService>();

            var hoje = DateOnly.FromDateTime(DateTime.Today);
            var d = await taxas.CalcularAsync(
                bruto, hoje, SimForma, SimAdquirente, SimBandeira, parcelas, SimReterImposto);

            var liquido = bruto - d.Total;
            var partes = new List<string> { $"Bruto {bruto:C}" };

            if (d.ValorTaxa is { } taxa)
                partes.Add($"taxa {taxa:C} ({d.TaxaPercentual:0.##}%)");
            else if (TaxaService.ModalidadeDe(SimForma, parcelas) is not null)
            {
                // Sem taxa cadastrada não se inventa desconto — mas a tela DIZ que não
                // achou, senão o líquido igual ao bruto passaria por taxa zero.
                partes.Add("sem taxa cadastrada para esta combinação");
                SimSemTaxa = true;
            }

            if (d.ValorImposto is { } imposto)
                partes.Add($"imposto {imposto:C} ({d.DetalheImposto})");

            partes.Add($"→ líquido {liquido:C}");

            if (d.PrevisaoRecebimento is { } previsao)
                partes.Add($"cai em {previsao:dd/MM/yyyy}");

            SimResultado = string.Join(" · ", partes);
        }
        catch (Exception ex)
        {
            Clinica.Application.Diagnostico.Registrar("Financeiro — simulação falhou", ex);
            SimResultado = $"Não foi possível simular: {ex.Message}";
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
