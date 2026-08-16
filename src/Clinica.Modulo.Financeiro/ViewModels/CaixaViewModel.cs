using System.Collections.ObjectModel;
using Clinica.Application.Servicos;
using Clinica.Desktop.Controls;
using Clinica.Desktop.Shell;
using Clinica.Desktop.Shell.Componentes;
using Clinica.Domain.Entities;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Clinica.Financeiro.ViewModels;

/// <summary>Uma linha do caixa, já formatada para a tela.</summary>
public sealed class LinhaCaixa
{
    public required int Id { get; init; }
    public required string Data { get; init; }
    public required string Descricao { get; init; }
    public required string Categoria { get; init; }
    public required string ValorFormatado { get; init; }

    /// <summary>
    /// O valor em número, ao lado do formatado. Existe porque quem precisa dele — a
    /// cobrança por Pix — não pode reconverter "+ R$ 150,00" para decimal: o texto já
    /// carrega sinal, símbolo e a cultura da máquina, e a volta erra em algum deles.
    /// </summary>
    public required decimal Valor { get; init; }
    public required string StatusRotulo { get; init; }

    /// <summary>Vínculo com o faturamento, quando a receita nasceu de uma guia.</summary>
    public required bool VeioDoFaturamento { get; init; }

    public required bool EhEntrada { get; init; }

    /// <summary>Previsto: ainda dá para marcar como pago/recebido.</summary>
    public required bool PodeRealizar { get; init; }

    /// <summary>Cancelado não se cancela de novo (e continua no histórico).</summary>
    public required bool PodeCancelar { get; init; }
}

/// <summary>
/// Caixa do mês: entradas, saídas e saldo. Lançamentos vindos de guias do
/// faturamento aparecem marcados, para deixar visível o elo entre os módulos.
/// </summary>
public sealed partial class CaixaViewModel : ObservableObject
{
    private readonly FinanceiroService _financeiro;
    private readonly TaxaService _taxas;
    private readonly DocumentoFinanceiroService _documentos;
    private readonly DocumentosFinanceirosPdfService _pdfs;
    private readonly ParametrosService _parametros;
    private readonly PixService _pix;
    private readonly ISnackbarService _snackbar;
    private readonly IDialogoService _dialogo;

    public ObservableCollection<LinhaCaixa> Linhas { get; } = [];

    /// <summary>O que a última carga trouxe; <see cref="Linhas"/> é o recorte do filtro.</summary>
    private readonly List<LinhaCaixa> _todas = [];

    /// <summary>A última carga veio SEM o corte de 300 — dá para refiltrar em memória sem mentir.</summary>
    private bool _cargaCompleta;

    // ---- Filtro. Os rótulos são const porque a linha (StatusRotulo) e o combo usam os
    // MESMOS — string repetida divergiria na primeira correção de texto.
    public const string TodasSituacoes = "Todas";
    public const string SituacaoPrevisto = "Previsto";
    public const string SituacaoRealizado = "Realizado";
    public const string SituacaoCancelado = "Cancelado";

    // Strings, nunca o enum cru: `StatusLancamento` amarrado direto ao ComboBox sairia
    // como identificador (o defeito da parcela 41). E o nome é único no repositório —
    // a checagem 20 casa o ItemsSource pelo NOME num mapa global.
    public string[] OpcoesSituacaoLancamento { get; } =
        [TodasSituacoes, SituacaoPrevisto, SituacaoRealizado, SituacaoCancelado];

    [ObservableProperty] private string _filtroTexto = string.Empty;
    [ObservableProperty] private string _filtroSituacao = TodasSituacoes;

    partial void OnFiltroTextoChanged(string value) => AplicarFiltro();
    partial void OnFiltroSituacaoChanged(string value) => AplicarFiltro();

    public bool FiltroAtivo =>
        FiltroSituacao != TodasSituacoes || !string.IsNullOrWhiteSpace(FiltroTexto);

    [RelayCommand]
    private void LimparFiltro()
    {
        FiltroTexto = string.Empty;
        FiltroSituacao = TodasSituacoes;
    }

    /// <summary>"N de M no filtro" — vazio sem filtro; o número mora junto do campo que corta.</summary>
    [ObservableProperty] private string _resumoFiltro = string.Empty;

    /// <summary>O estado vazio muda de frase quando há filtro (lição da lista de espera, parcela 25).</summary>
    [ObservableProperty] private string _vazioDescricao =
        "Receita de guia entra pela Conciliação, já vinculada.";

    [ObservableProperty]
    private DateTime _mes = new(DateTime.Today.Year, DateTime.Today.Month, 1);

    [ObservableProperty]
    private bool _carregando;

    /// <summary>
    /// A leitura FALHOU — o terceiro estado. Sem ele, lista vazia por erro fica idêntica
    /// a lista vazia por não haver nada, e o aviso some junto com o snackbar em 4 segundos.
    /// </summary>
    [ObservableProperty]
    private bool _naoVerificado;

    [ObservableProperty]
    private string _entradas = "—";

    [ObservableProperty]
    private string _saidas = "—";

    [ObservableProperty]
    private string _saldo = "—";

    [ObservableProperty]
    private string _previsto = "—";

    /// <summary>
    /// O que a clinica de fato recebe: bruto menos a taxa da maquininha e o imposto
    /// retido. E o numero que bate com o extrato da adquirente — o bruto nunca bate.
    /// </summary>
    [ObservableProperty]
    private string _liquido = "—";

    /// <summary>Quanto saiu em taxa e imposto no periodo.</summary>
    [ObservableProperty]
    private string _deducoes = "—";

    /// <summary>Houve deducao no periodo — sem ela a faixa nao mostra a linha do liquido.</summary>
    [ObservableProperty]
    private bool _temDeducoes;

    /// <summary>
    /// Habilita os botões de escrita da tela. É a metade VISÍVEL da permissão: o
    /// botão apagado explica por que não dá; a guarda no comando é que impede.
    /// Só desabilitar seria enfeite — um atalho de teclado passaria direto.
    /// </summary>
    public bool PodeEditarFinanceiro => SessaoUsuario.Atual.Pode(Permissao.EditarFinanceiro);

    /// <summary>
    /// Quantas linhas a tela traz por mês. O caixa é lido de cima para baixo (o mais
    /// recente primeiro) e ninguém rola oitocentas linhas — quem procura um lançamento
    /// antigo usa o mês certo. Os TOTAIS não dependem deste corte.
    /// </summary>
    public const int LimiteLinhas = 300;

    /// <summary>O mês tem mais lançamentos do que a lista está mostrando.</summary>
    [ObservableProperty]
    private bool _truncado;

    public CaixaViewModel(
        FinanceiroService financeiro, TaxaService taxas,
        DocumentoFinanceiroService documentos,
        DocumentosFinanceirosPdfService pdfs, ParametrosService parametros,
        PixService pix, ISnackbarService snackbar, IDialogoService dialogo)
    {
        _financeiro = financeiro;
        _taxas = taxas;
        _documentos = documentos;
        _pdfs = pdfs;
        _parametros = parametros;
        _pix = pix;
        _snackbar = snackbar;
        _dialogo = dialogo;
        _ = CarregarAsync();
    }

    partial void OnMesChanged(DateTime value) => _ = CarregarAsync();

    /// <summary>
    /// Descarte de resposta fora de ordem (parcela 50): trocar o mês (ou ativar um filtro,
    /// que recarrega sem o corte) duas vezes rápido deixaria a lista de uma carga sob os
    /// controles da outra — num banco remoto a leitura mais velha pode responder por último.
    /// </summary>
    private int _geracaoCarga;

    /// <summary>
    /// Filtro mudou: o corte de 300 linhas tornaria a busca MENTIROSA — "não achei" sobre
    /// um mês pela metade se lê como "não existe". Com filtro ativo a carga vem sem
    /// limite; com a carga completa em mãos (ou sem filtro), o recorte é só memória.
    /// </summary>
    private void AplicarFiltro()
    {
        if (FiltroAtivo && !_cargaCompleta) _ = CarregarAsync();
        else Refiltrar();
    }

    [RelayCommand]
    public async Task CarregarAsync()
    {
        var geracao = ++_geracaoCarga;

        try
        {
            Carregando = true;
            NaoVerificado = false;
            var inicio = new DateOnly(Mes.Year, Mes.Month, 1);
            var fim = inicio.AddMonths(1).AddDays(-1);

            // Corte no banco só SEM filtro. Os totais abaixo continuam saindo do mês
            // INTEIRO (vêm da projeção, não desta lista), então limitar a exibição não
            // falseia o saldo.
            int? limite = FiltroAtivo ? null : LimiteLinhas;
            var lancamentos = await _financeiro.DoPeriodoAsync(inicio, fim, limite);

            // Chegou tarde: outra carga mais nova já foi pedida.
            if (geracao != _geracaoCarga) return;

            _cargaCompleta = limite is null;
            _todas.Clear();
            foreach (var l in lancamentos)
            {
                var entrada = l.Tipo == TipoLancamento.Entrada;
                _todas.Add(new LinhaCaixa
                {
                    Id = l.Id,
                    Data = l.Data.ToString("dd/MM"),
                    Descricao = l.Descricao,
                    Categoria = l.Categoria?.Nome ?? "—",
                    ValorFormatado = $"{(entrada ? "+" : "-")} {l.Valor:C}",
                    Valor = l.Valor,
                    StatusRotulo = Rotular(l.Status),
                    VeioDoFaturamento = l.CodigoFaturamentoId is not null,
                    EhEntrada = entrada,
                    PodeRealizar = l.Status == StatusLancamento.Previsto,
                    PodeCancelar = l.Status != StatusLancamento.Cancelado
                });
            }

            Refiltrar();

            var resumo = await _financeiro.ResumoAsync(inicio, fim);
            if (geracao != _geracaoCarga) return;

            Entradas = $"{resumo.EntradasRealizadas:C}";
            Saidas = $"{resumo.SaidasRealizadas:C}";
            Saldo = $"{resumo.SaldoRealizado:C}";
            Previsto = $"{resumo.SaldoPrevisto:C}";
            Liquido = $"{resumo.EntradasLiquidas:C}";
            Deducoes = $"{resumo.TotalDeducoes:C}";
            TemDeducoes = resumo.TemDeducao;
        }
        catch (Exception ex)
        {
            if (geracao != _geracaoCarga) return;

            NaoVerificado = true;
            Clinica.Application.Diagnostico.Registrar("Financeiro — caixa não pôde ser carregado", ex);
            _snackbar.Erro($"Não foi possível carregar o caixa: {ex.Message}");
        }
        finally
        {
            // A carga superada não apaga o "Carregando" da que ainda está no ar.
            if (geracao == _geracaoCarga) Carregando = false;
        }
    }

    /// <summary>
    /// Aplica o filtro sobre o que já foi lido — em memória, sem ida ao banco.
    /// </summary>
    private void Refiltrar()
    {
        Linhas.Clear();

        var filtradas = _todas.Where(l =>
            (FiltroSituacao == TodasSituacoes || l.StatusRotulo == FiltroSituacao)
            // Descrição OU categoria: quem digita "aluguel" não sabe (nem deve saber)
            // em qual das duas colunas o lançamento guarda a palavra.
            && (Busca.Casa(l.Descricao, FiltroTexto) || Busca.Casa(l.Categoria, FiltroTexto)));

        // Sem filtro o corte de exibição continua valendo; com filtro a lista é completa
        // por construção — e o aviso "Truncado" NUNCA aparece, senão ele desdiria a busca.
        if (!FiltroAtivo) filtradas = filtradas.Take(LimiteLinhas);
        foreach (var l in filtradas) Linhas.Add(l);

        Truncado = !FiltroAtivo
                   && (_cargaCompleta ? _todas.Count > LimiteLinhas : _todas.Count >= LimiteLinhas);

        OnPropertyChanged(nameof(FiltroAtivo));

        // O resumo DIZ que está filtrado — e mora ao lado do campo que corta.
        ResumoFiltro = FiltroAtivo
            ? $"{Linhas.Count} de {_todas.Count} lançamento(s) no filtro"
            : string.Empty;

        VazioDescricao = FiltroAtivo
            ? "Nenhum lançamento bate com o filtro — limpe-o para ver o mês inteiro."
            : "Receita de guia entra pela Conciliação, já vinculada.";
    }

    /// <summary>
    /// Abre o formulário do lançamento manual. É a entrada do dinheiro que o faturamento
    /// não conhece (aluguel, material, recebimento avulso) — o que vem de guia continua
    /// entrando pela Conciliação, já vinculado.
    /// </summary>
    [RelayCommand]
    private async Task NovoLancamentoAsync()
    {
        SessaoUsuario.Atual.Exigir(Permissao.EditarFinanceiro, "lançar no caixa");

        var janela = new Janelas.LancamentoWindow(new LancamentoEdicaoViewModel(_financeiro, _taxas))
        {
            Owner = JanelaDona.Atual()
        };

        if (janela.ShowDialog() != true) return;

        _snackbar.Sucesso("Lançamento registrado.");
        await CarregarAsync();
    }


    /// <summary>
    /// Gera o código Pix para o paciente pagar agora.
    /// </summary>
    /// <remarks>
    /// O `PixService` foi entregue na parcela 34 sem tela nenhuma; esta é a porta. Sai do
    /// CAIXA porque é onde o dinheiro é registrado — e quando a linha já existe, o valor
    /// vai preenchido: redigitar o que o sistema já sabe é onde entra o erro de um dígito.
    ///
    /// Gerar o código NÃO dá baixa. O lançamento continua previsto até alguém conferir o
    /// extrato, porque este código não fala com banco nenhum e marcar como recebido aqui
    /// criaria receita que talvez não tenha entrado.
    /// </remarks>
    [RelayCommand]
    private void CobrarPix(LinhaCaixa? linha)
    {
        var vm = new CobrancaPixViewModel(
            _parametros, _pix,
            linha?.Valor,
            linha?.Descricao);

        new Janelas.CobrancaPixWindow(vm)
        {
            Owner = JanelaDona.Atual()
        }.ShowDialog();
    }

    /// <summary>Marca um lançamento previsto como efetivamente pago/recebido.</summary>
    [RelayCommand]
    private async Task RealizarAsync(LinhaCaixa? linha)
    {
        if (linha is null) return;

        SessaoUsuario.Atual.Exigir(Permissao.EditarFinanceiro, "lançar no caixa");

        try
        {
            await _financeiro.RealizarAsync(linha.Id, operador: SessaoUsuario.Atual.Operador);
            _snackbar.Sucesso("Lançamento realizado.");
            await CarregarAsync();
        }
        catch (Exception ex)
        {
            Clinica.Application.Diagnostico.Registrar("Financeiro — lançamento não pôde ser realizado", ex);
            _snackbar.Erro($"Não foi possível realizar: {ex.Message}");
        }
    }

    /// <summary>
    /// Cancela um lançamento. Nunca apaga — sai dos totais e fica no histórico com o
    /// motivo, que o serviço exige.
    /// </summary>
    [RelayCommand]
    private async Task CancelarAsync(LinhaCaixa? linha)
    {
        if (linha is null) return;

        SessaoUsuario.Atual.Exigir(Permissao.EditarFinanceiro, "lançar no caixa");

        var motivo = _dialogo.PerguntarTexto(
            "Cancelar lançamento",
            $"Por que \"{linha.Descricao}\" está sendo cancelado? O lançamento sai dos totais mas continua no histórico.");
        if (string.IsNullOrWhiteSpace(motivo)) return;

        try
        {
            await _financeiro.CancelarAsync(linha.Id, motivo, operador: SessaoUsuario.Atual.Operador);
            _snackbar.Sucesso("Lançamento cancelado.");
            await CarregarAsync();
        }
        catch (Exception ex)
        {
            Clinica.Application.Diagnostico.Registrar("Financeiro — lançamento não pôde ser cancelado", ex);
            _snackbar.Erro($"Não foi possível cancelar: {ex.Message}");
        }
    }

    /// <summary>
    /// Emite o recibo daquele dinheiro que entrou. O recibo APONTA para o lançamento,
    /// e não o substitui: o caixa continua sendo a verdade do que entrou, o papel é a
    /// prova que o paciente leva.
    /// </summary>
    [RelayCommand]
    private async Task EmitirReciboAsync(LinhaCaixa? linha)
    {
        if (linha is null) return;

        // O bit que a folha declara no catálogo (parcela 59): emitir recibo é escrever
        // no financeiro, e a tela abre com só VerFinanceiro.
        SessaoUsuario.Atual.Exigir(Permissao.EditarFinanceiro, "emitir recibo");

        if (!linha.EhEntrada)
        {
            _snackbar.Erro("Só se dá recibo de dinheiro que entrou.");
            return;
        }

        var destinatario = _dialogo.PerguntarTexto(
            "Recibo",
            "Para quem é o recibo? Deixe em branco para usar o nome do paciente do lançamento.");

        try
        {
            var documento = await _documentos.EmitirReciboDoLancamentoAsync(
                linha.Id, string.IsNullOrWhiteSpace(destinatario) ? null : destinatario,
                SessaoUsuario.Atual.Operador);

            var pdf = await _pdfs.GerarAsync(documento.Id, await _parametros.ObterPrestadorAsync());

            // O recibo JÁ está emitido: falha daqui para a frente é de impressão.
            var erro = await ImpressaoPdf.SalvarEAbrirAsync(
                pdf, ImpressaoPdf.NomeSeguro($"Recibo-{documento.Numero.Replace('/', '-')}.pdf"));

            if (erro is not null)
            {
                _snackbar.Erro($"{erro} O recibo {documento.Numero} está emitido.");
                return;
            }

            _snackbar.Sucesso($"Recibo {documento.Numero} emitido.");
        }
        catch (Exception ex)
        {
            Clinica.Application.Diagnostico.Registrar("Financeiro — recibo não pôde ser emitido", ex);
            _snackbar.Erro($"Não foi possível emitir o recibo: {ex.Message}");
        }
    }

    /// <summary>
    /// O extrato do mês em CSV (parcela 32).
    ///
    /// O Caixa é a tela mais aberta do módulo e era a única sem saída: quando o contador
    /// pedia o extrato, a clínica exportava do banco ou copiava à mão. Ponto e vírgula e
    /// BOM UTF-8, como o resto da suíte — com vírgula o arquivo abre com tudo numa coluna
    /// só, e sem BOM "Sessão" vira "SessÃ£o" na planilha.
    /// </summary>
    [RelayCommand]
    private async Task ExportarAsync()
    {
        if (Linhas.Count == 0)
        {
            _snackbar.Info("Não há lançamento para exportar neste mês.");
            return;
        }

        try
        {
            var csv = ExportacaoCsv.Montar(
                ["Data", "Descrição", "Categoria", "Tipo", "Valor", "Situação", "Origem"],
                Linhas.Select(l => new[]
                {
                    l.Data,
                    l.Descricao,
                    l.Categoria,
                    l.EhEntrada ? "Entrada" : "Saída",
                    l.ValorFormatado,
                    l.StatusRotulo,
                    // O elo com o faturamento vai junto: é o que permite conferir a
                    // receita de convênio contra o demonstrativo da operadora.
                    l.VeioDoFaturamento ? "guia do faturamento" : "lançamento manual"
                }));

            var erro = await ImpressaoPdf.SalvarEAbrirAsync(
                csv,
                ImpressaoPdf.NomeSeguro($"caixa-{Mes:yyyy-MM}.csv"),
                "CSV (*.csv)|*.csv", ".csv");

            if (erro is not null) _snackbar.Erro(erro);
        }
        catch (Exception ex)
        {
            Clinica.Application.Diagnostico.Registrar("Financeiro — caixa não pôde ser exportado", ex);
            _snackbar.Erro(ex.Message);
        }
    }

    [RelayCommand]
    private void MesAnterior() => Mes = Mes.AddMonths(-1);

    [RelayCommand]
    private void ProximoMes() => Mes = Mes.AddMonths(1);

    // As MESMAS consts do combo de filtro: o casamento do recorte é por texto, e uma
    // string repetida aqui divergiria na primeira correção.
    private static string Rotular(StatusLancamento status) => status switch
    {
        StatusLancamento.Previsto => SituacaoPrevisto,
        StatusLancamento.Realizado => SituacaoRealizado,
        StatusLancamento.Cancelado => SituacaoCancelado,
        _ => status.ToString()
    };
}
