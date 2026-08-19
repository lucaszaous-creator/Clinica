using System.Collections.ObjectModel;
using Clinica.Application.Servicos;
using Clinica.Desktop.Controls;
using Clinica.Desktop.Shell;
using Clinica.Desktop.Shell.Componentes;
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

/// <summary>Uma linha da apuração mensal: um tributo, o que incidiu e quem recolhe.</summary>
public sealed class LinhaApuracaoMensal
{
    public required string Sigla { get; init; }
    public required string Nome { get; init; }
    public required string Aliquota { get; init; }
    public required string Base { get; init; }
    public required string Valor { get; init; }
    public required string Natureza { get; init; }

    /// <summary>Retido já saiu; recolhido ainda vai sair. A tela separa os dois.</summary>
    public required bool Retido { get; init; }
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

    // ---- Regime tributário (parcela 15) ----
    public ObservableCollection<LinhaTributo> Tributos { get; } = [];

    /// <summary>O que sai de cada real recebido hoje, somando os tributos vigentes.</summary>
    [ObservableProperty] private string _cargaHoje = "—";

    /// <summary>
    /// Explica QUAL mecanismo está valendo. Enquanto não houver tributo cadastrado, a
    /// alíquota única da parcela 9 continua sendo aplicada — e a tela precisa dizer isso,
    /// senão o campo de baixo parece um resto de tela que não faz nada.
    /// </summary>
    [ObservableProperty] private string _origemDaCarga = string.Empty;

    // ---- Imposto (alíquota única — legado da parcela 9) ----
    [ObservableProperty] private string? _aliquotaImposto;

    /// <summary>
    /// O fallback da alíquota única está ARMADO — não há tributo vigente hoje. É a mesma
    /// condição do serviço, e é ela (nunca `Tributos.Count`) que decide se a faixa da
    /// alíquota única aparece: campo que faz efeito não pode estar invisível.
    /// </summary>
    [ObservableProperty] private bool _aliquotaUnicaValendo = true;

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

    // ---- Apuração mensal por tributo (parcela 28) ----

    /// <summary>
    /// Quanto de CADA tributo saiu no mês. O serviço separava ISS, PIS, COFINS, IRPJ e
    /// CSLL desde a parcela 15 e toda tela consolidava num número só: a clínica sabia
    /// quanto de imposto saiu e não sabia de quê — que é a pergunta do contador, e a
    /// única que permite conferir cinco guias antes de pagá-las.
    /// </summary>
    public ObservableCollection<LinhaApuracaoMensal> Apuracao { get; } = [];

    [ObservableProperty] private DateTime _mesApuracao =
        new(DateTime.Today.Year, DateTime.Today.Month, 1);

    [ObservableProperty] private string _resumoApuracao = string.Empty;
    [ObservableProperty] private string? _divergenciaApuracao;
    [ObservableProperty] private bool _apuracaoNaoVerificada;

    /// <summary>Há divergência entre o apurado agora e o gravado na época.</summary>
    public bool TemDivergencia => !string.IsNullOrWhiteSpace(DivergenciaApuracao);

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

            // Monta e só ENTÃO publica: entre o Clear e o último Add não pode haver await
            // (parcela 62) — a coleção ficava vazia na tela durante o roundtrip ao banco.
            var catalogoTaxas = await taxas.CatalogoAsync();

            Taxas.Clear();
            foreach (var t in catalogoTaxas) Taxas.Add(LinhaTaxa.De(t));

            var aliquota = await parametros.ObterAliquotaImpostoAsync();
            // Zero é "não configurado" e o campo fica VAZIO, não "0" — um zero digitado e
            // um zero padrão parecem iguais na tela e não são a mesma decisão.
            AliquotaImposto = aliquota > 0m ? aliquota.ToString("0.##") : null;

            var tributos = scope.ServiceProvider.GetRequiredService<TributoService>();
            var hoje = DateOnly.FromDateTime(DateTime.Today);

            var catalogoTributos = await tributos.CatalogoAsync();

            Tributos.Clear();
            foreach (var t in catalogoTributos) Tributos.Add(LinhaTributo.De(t, hoje));

            var carga = await tributos.AliquotaTotalAsync(hoje);
            CargaHoje = carga > 0m ? $"{carga:0.####}%" : "—";

            // Qual mecanismo está valendo. Sem esta linha, quem cadastra o primeiro
            // tributo não entende por que o campo da alíquota única parou de ter efeito.
            //
            // ⚠️ A condição é VIGENTE HOJE, nunca "cadastrado" — porque é essa a condição
            // do serviço (`TributoService.ApurarAsync` cai no fallback quando não há
            // tributo vigente NO DIA). A tela escondia a faixa da alíquota única por
            // `Tributos.Count`, e na janela entre as duas condições — tributos cadastrados
            // todos com vigência futura, ou todos expirados — a alíquota única CONTINUAVA
            // valendo no cálculo com o campo dela invisível, e esta frase dizia "nenhum
            // tributo cadastrado" com a lista cheia. Campo invisível que faz efeito é
            // pior que o campo morto que a parcela 49 tirou desta mesma tela. Duas
            // condições para a mesma pergunta, no mesmo ViewModel, a dez linhas de
            // distância — a frase com a certa e a visibilidade com a errada.
            var vigentes = Tributos.Count(t => t.ValendoAgora);
            AliquotaUnicaValendo = vigentes == 0;
            OrigemDaCarga = vigentes > 0
                ? $"{vigentes} tributo(s) vigente(s) hoje. A alíquota única abaixo está desligada."
                : Tributos.Count > 0
                    ? aliquota > 0m
                        ? $"Há {Tributos.Count} tributo(s) cadastrado(s), mas NENHUM vigente hoje — "
                          + "vale a alíquota única abaixo. Confira as vigências."
                        : $"Há {Tributos.Count} tributo(s) cadastrado(s), mas nenhum vigente hoje "
                          + "e nenhuma alíquota única: não se retém imposto. Confira as vigências."
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

    /// <summary>
    /// Cadastro e edição da taxa saem da página e vão para a janela: a aba é a TABELA de
    /// taxas — o que a clínica vem conferir — e o formulário fixo tomava 360px da largura
    /// em todas as visitas, para uma tarefa que acontece quando a adquirente renegocia.
    /// </summary>
    [RelayCommand]
    private async Task NovaTaxaAsync() => await AbrirTaxaAsync(0);

    [RelayCommand]
    private async Task EditarAsync(LinhaTaxa? linha)
    {
        if (linha is null) return;
        await AbrirTaxaAsync(linha.TaxaId);
    }

    private async Task AbrirTaxaAsync(int taxaId)
    {
        SessaoUsuario.Atual.Exigir(Permissao.EditarFinanceiro, "cadastrar taxa de cartão");

        var vm = new TaxaEdicaoViewModel(_escopos, taxaId);
        var janela = new Janelas.TaxaWindow(vm)
        {
            Owner = JanelaDona.Atual()
        };

        if (janela.ShowDialog() != true) return;
        _snackbar.Sucesso(taxaId == 0 ? "Taxa cadastrada." : "Taxa atualizada.");
        await CarregarAsync();
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
            await CarregarAsync();
        }
        catch (Exception ex)
        {
            Clinica.Application.Diagnostico.Registrar("Financeiro — taxa não pôde ser excluída", ex);
            Erro(ex.Message);
        }
    }

    // ==================== Regime tributário (parcela 15) ====================

    /// <summary>
    /// Mesma troca na aba do regime tributário: a tela é a lista dos tributos vigentes, e
    /// o cadastro — que tem regra própria em cada campo (base de cálculo, vigência,
    /// convênio) — ganhou espaço para explicá-las na janela.
    /// </summary>
    [RelayCommand]
    private async Task NovoTributoAsync() => await AbrirTributoAsync(0);

    [RelayCommand]
    private async Task EditarTributoAsync(LinhaTributo? linha)
    {
        if (linha is null) return;
        await AbrirTributoAsync(linha.TributoId);
    }

    private async Task AbrirTributoAsync(int tributoId)
    {
        SessaoUsuario.Atual.Exigir(Permissao.EditarFinanceiro, "cadastrar tributo");

        var vm = new TributoEdicaoViewModel(_escopos, tributoId);
        var janela = new Janelas.TributoWindow(vm)
        {
            Owner = JanelaDona.Atual()
        };

        if (janela.ShowDialog() != true) return;
        _snackbar.Sucesso(tributoId == 0 ? "Tributo cadastrado." : "Tributo atualizado.");
        await CarregarAsync();
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
            await CarregarAsync();
        }
        catch (Exception ex)
        {
            Clinica.Application.Diagnostico.Registrar("Financeiro — tributo não pôde ser excluído", ex);
            Erro(ex.Message);
        }
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

    private void Erro(string mensagem)
    {
        Mensagem = mensagem;
        MensagemEhErro = true;
    }

    // ==================== Apuração mensal por tributo (parcela 28) ====================

    /// <summary>
    /// Descarte de resposta fora de ordem (parcela 50): trocar o mês da apuração duas
    /// vezes rápido deixaria os tributos de um mês sob o título do outro — num banco
    /// remoto a leitura mais velha pode responder por último, e é esse número que vai
    /// para a guia do contador.
    /// </summary>
    private int _geracaoApuracao;

    [RelayCommand]
    public async Task ApurarAsync()
    {
        var geracao = ++_geracaoApuracao;

        try
        {
            ApuracaoNaoVerificada = false;
            DivergenciaApuracao = null;

            using var scope = _escopos.CreateScope();
            var tributos = scope.ServiceProvider.GetRequiredService<TributoService>();

            var r = await tributos.ApuracaoMensalAsync(MesApuracao.Year, MesApuracao.Month);

            // Chegou tarde: outra carga mais nova já foi pedida.
            if (geracao != _geracaoApuracao) return;

            Apuracao.Clear();
            foreach (var l in r.Linhas)
                Apuracao.Add(new LinhaApuracaoMensal
                {
                    Sigla = l.Sigla,
                    Nome = l.Nome,
                    Aliquota = $"{l.Aliquota:0.####}%",
                    Base = l.Base.ToString("C"),
                    Valor = l.Valor.ToString("C"),
                    Natureza = l.Natureza,
                    Retido = l.RetidoNaFonte
                });

            ResumoApuracao = r.Vazia
                ? "Nenhum tributo vigente incidiu sobre os recebimentos deste mês."
                : $"Receita recebida {r.ReceitaBruta:C} · a recolher {r.ARecolher:C}"
                  + (r.Retido > 0m ? $" · retido na fonte {r.Retido:C}" : string.Empty);

            // A divergência aparece em vez de ser escondida: ela significa que a regra
            // mudou DEPOIS de o dinheiro entrar, e é a clínica que decide qual número vai
            // para a guia. Esconder faria o contador receber um total sem saber do outro.
            if (r.Diverge)
                DivergenciaApuracao =
                    $"O apurado agora ({r.TotalApurado:C}) não bate com o que foi gravado nos "
                    + $"lançamentos ({r.TotalGravado:C}) — diferença de {r.Divergencia:C}. "
                    + "Isso acontece quando uma alíquota foi alterada depois de o dinheiro "
                    + "entrar. Confira qual das duas regras vale para este mês.";
        }
        catch (Exception ex)
        {
            if (geracao != _geracaoApuracao) return;

            // Falha nunca aparece como sucesso: lista vazia por consulta quebrada se lê
            // como "não houve imposto no mês", que é a mentira mais cara desta aba.
            Clinica.Application.Diagnostico.Registrar(
                "Financeiro — apuração mensal por tributo falhou", ex);
            Apuracao.Clear();
            ApuracaoNaoVerificada = true;
            ResumoApuracao = $"Não foi possível apurar o mês: {ex.Message}";
        }
    }

    [RelayCommand]
    private void MesApuracaoAnterior() => MesApuracao = MesApuracao.AddMonths(-1);

    [RelayCommand]
    private void MesApuracaoProximo() => MesApuracao = MesApuracao.AddMonths(1);

    partial void OnMesApuracaoChanged(DateTime value) => _ = ApurarAsync();

    partial void OnDivergenciaApuracaoChanged(string? value)
        => OnPropertyChanged(nameof(TemDivergencia));

    /// <summary>
    /// O CSV que vai ao contador. Ponto e vírgula e BOM UTF-8, como o resto da suíte:
    /// com vírgula o arquivo abre com tudo numa coluna só, e sem BOM "Alíquota" vira
    /// "AlÃ­quota" na planilha.
    /// </summary>
    [RelayCommand]
    private async Task ExportarApuracaoAsync()
    {
        if (Apuracao.Count == 0)
        {
            _snackbar.Info("Não há apuração para exportar neste mês.");
            return;
        }

        try
        {
            var csv = ExportacaoCsv.Montar(
                ["Tributo", "Nome", "Alíquota efetiva", "Base", "Valor", "Natureza"],
                Apuracao.Select(l => new[]
                {
                    l.Sigla, l.Nome, l.Aliquota, l.Base, l.Valor, l.Natureza
                }));

            var erro = await ImpressaoPdf.SalvarEAbrirAsync(
                csv,
                ImpressaoPdf.NomeSeguro($"apuracao-{MesApuracao:yyyy-MM}.csv"),
                "CSV (*.csv)|*.csv", ".csv");

            if (erro is not null) Erro(erro);
        }
        catch (Exception ex)
        {
            Clinica.Application.Diagnostico.Registrar(
                "Financeiro — apuração não pôde ser exportada", ex);
            Erro(ex.Message);
        }
    }

}

/// <summary>
/// A taxa da maquininha, na janela.
///
/// A regra mais **específica** ganha: bandeira vence o adquirente genérico, e faixa de
/// parcelas vence a modalidade solta — senão a clínica cadastraria a exceção e continuaria
/// vendo o número da regra geral. A **vigência** existe porque a adquirente renegocia: o
/// que vale no recebimento de março é o percentual de março, e por isso o valor é COPIADO
/// na venda, nunca referenciado.
/// </summary>
public sealed partial class TaxaEdicaoViewModel : ObservableObject
{
    private readonly IServiceScopeFactory _escopos;
    private readonly int _taxaId;

    public IReadOnlyList<ModalidadeCartao> Modalidades { get; } = Enum.GetValues<ModalidadeCartao>();

    public string Titulo => _taxaId == 0 ? "Taxa nova" : "Editar taxa";

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

    [ObservableProperty] private string? _mensagem;
    [ObservableProperty] private bool _mensagemEhErro;
    [ObservableProperty] private bool _salvando;

    /// <summary>Faixa de parcelas só faz sentido no crédito parcelado.</summary>
    public bool EhParcelado => Modalidade == ModalidadeCartao.CreditoParcelado;

    public event Action? Concluido;

    public TaxaEdicaoViewModel(IServiceScopeFactory escopos, int taxaId)
    {
        _escopos = escopos;
        _taxaId = taxaId;
        if (taxaId != 0) _ = CarregarAsync();
    }

    private async Task CarregarAsync()
    {
        try
        {
            using var scope = _escopos.CreateScope();
            var taxas = scope.ServiceProvider.GetRequiredService<TaxaService>();
            if ((await taxas.CatalogoAsync()).FirstOrDefault(x => x.Id == _taxaId) is not { } t) return;

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
            Salvando = true;
            SessaoUsuario.Atual.Exigir(Permissao.EditarFinanceiro, "cadastrar taxa de cartão");

            using var scope = _escopos.CreateScope();
            var taxas = scope.ServiceProvider.GetRequiredService<TaxaService>();

            await taxas.SalvarAsync(new TaxaCartao
            {
                Id = _taxaId,
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

            Concluido?.Invoke();
        }
        catch (Exception ex)
        {
            Clinica.Application.Diagnostico.Registrar("Financeiro — taxa não pôde ser salva", ex);
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

/// <summary>
/// Um tributo do regime, na janela.
///
/// A **base de cálculo** existe porque nem todo tributo incide sobre a receita inteira: no
/// Lucro Presumido o IRPJ de 15% incide sobre 32%, e a efetiva é 4,8%. Sem o campo, a
/// clínica digitaria 15% — o número que ela conhece — e triplicaria o imposto.
///
/// O **convênio** preenchido significa retido na fonte por aquela operadora: a retenção
/// SUBSTITUI os tributos gerais naquele recebimento, nunca se soma (os dois são o mesmo
/// imposto, e somá-los conta duas vezes — o erro clássico da planilha de convênio).
/// </summary>
public sealed partial class TributoEdicaoViewModel : ObservableObject
{
    private readonly IServiceScopeFactory _escopos;
    private readonly int _tributoId;

    public string Titulo => _tributoId == 0 ? "Tributo novo" : "Editar tributo";

    [ObservableProperty] private string? _sigla;
    [ObservableProperty] private string? _nome;
    [ObservableProperty] private string? _percentual;
    [ObservableProperty] private string? _base = "100";
    [ObservableProperty] private string? _convenio;
    [ObservableProperty] private DateTime? _vigenteDe;
    [ObservableProperty] private DateTime? _vigenteAte;
    [ObservableProperty] private bool _ativo = true;

    [ObservableProperty] private string? _mensagem;
    [ObservableProperty] private bool _mensagemEhErro;
    [ObservableProperty] private bool _salvando;

    public event Action? Concluido;

    public TributoEdicaoViewModel(IServiceScopeFactory escopos, int tributoId)
    {
        _escopos = escopos;
        _tributoId = tributoId;
        if (tributoId != 0) _ = CarregarAsync();
    }

    private async Task CarregarAsync()
    {
        try
        {
            using var scope = _escopos.CreateScope();
            var tributos = scope.ServiceProvider.GetRequiredService<TributoService>();
            if ((await tributos.CatalogoAsync()).FirstOrDefault(x => x.Id == _tributoId) is not { } t)
                return;

            Sigla = t.Sigla;
            Nome = t.Nome;
            Percentual = t.Percentual.ToString("0.####");
            Base = t.BasePercentual.ToString("0.####");
            Convenio = t.ConvenioCodigo;
            VigenteDe = t.VigenteDe?.ToDateTime(TimeOnly.MinValue);
            VigenteAte = t.VigenteAte?.ToDateTime(TimeOnly.MinValue);
            Ativo = t.Ativo;
        }
        catch (Exception ex)
        {
            Clinica.Application.Diagnostico.Registrar("Financeiro — tributo não pôde ser aberto", ex);
            Erro(ex.Message);
        }
    }

    [RelayCommand]
    private async Task SalvarAsync()
    {
        Mensagem = null;
        MensagemEhErro = false;

        if (string.IsNullOrWhiteSpace(Sigla))
        {
            Erro("Informe a sigla do tributo (ISS, PIS, COFINS, IRPJ, CSLL, SIMPLES).");
            return;
        }
        if (!Valores.TentarLerDecimal(Percentual, out var percentual))
        {
            Erro("Informe a alíquota do tributo (ex.: 3 ou 0,65).");
            return;
        }
        // Base vazia = 100%, que é o caso normal. Exigir o número em toda linha faria a
        // clínica digitar "100" cinco vezes para cadastrar um regime do Simples.
        var baseCalculo = 100m;
        if (!string.IsNullOrWhiteSpace(Base) && !Valores.TentarLerDecimal(Base, out baseCalculo))
        {
            Erro("A base de cálculo vai de 0 a 100 (ex.: 32 no IRPJ do Lucro Presumido).");
            return;
        }

        try
        {
            Salvando = true;
            SessaoUsuario.Atual.Exigir(Permissao.EditarFinanceiro, "cadastrar tributo");

            using var scope = _escopos.CreateScope();
            var tributos = scope.ServiceProvider.GetRequiredService<TributoService>();

            await tributos.SalvarAsync(new Tributo
            {
                Id = _tributoId,
                Sigla = Sigla!,
                Nome = string.IsNullOrWhiteSpace(Nome) ? Sigla! : Nome!,
                Percentual = percentual,
                BasePercentual = baseCalculo,
                ConvenioCodigo = Convenio,
                VigenteDe = VigenteDe is { } de ? DateOnly.FromDateTime(de) : null,
                VigenteAte = VigenteAte is { } ate ? DateOnly.FromDateTime(ate) : null,
                Ativo = Ativo
            }, SessaoUsuario.Atual.Operador);

            Concluido?.Invoke();
        }
        catch (Exception ex)
        {
            Clinica.Application.Diagnostico.Registrar("Financeiro — tributo não pôde ser salvo", ex);
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
