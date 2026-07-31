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
    private readonly ISnackbarService _snackbar;
    private readonly IDialogoService _dialogo;

    public ObservableCollection<LinhaCaixa> Linhas { get; } = [];

    [ObservableProperty]
    private DateTime _mes = new(DateTime.Today.Year, DateTime.Today.Month, 1);

    [ObservableProperty]
    private bool _carregando;

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
        ISnackbarService snackbar, IDialogoService dialogo)
    {
        _financeiro = financeiro;
        _taxas = taxas;
        _documentos = documentos;
        _pdfs = pdfs;
        _parametros = parametros;
        _snackbar = snackbar;
        _dialogo = dialogo;
        _ = CarregarAsync();
    }

    partial void OnMesChanged(DateTime value) => _ = CarregarAsync();

    [RelayCommand]
    public async Task CarregarAsync()
    {
        try
        {
            Carregando = true;
            var inicio = new DateOnly(Mes.Year, Mes.Month, 1);
            var fim = inicio.AddMonths(1).AddDays(-1);

            // Corte no banco. Os totais abaixo continuam saindo do mês INTEIRO (vêm da
            // projeção, não desta lista), então limitar a exibição não falseia o saldo.
            var lancamentos = await _financeiro.DoPeriodoAsync(inicio, fim, LimiteLinhas);
            Truncado = lancamentos.Count >= LimiteLinhas;
            Linhas.Clear();
            foreach (var l in lancamentos)
            {
                var entrada = l.Tipo == TipoLancamento.Entrada;
                Linhas.Add(new LinhaCaixa
                {
                    Id = l.Id,
                    Data = l.Data.ToString("dd/MM"),
                    Descricao = l.Descricao,
                    Categoria = l.Categoria?.Nome ?? "—",
                    ValorFormatado = $"{(entrada ? "+" : "-")} {l.Valor:C}",
                    StatusRotulo = Rotular(l.Status),
                    VeioDoFaturamento = l.CodigoFaturamentoId is not null,
                    EhEntrada = entrada,
                    PodeRealizar = l.Status == StatusLancamento.Previsto,
                    PodeCancelar = l.Status != StatusLancamento.Cancelado
                });
            }

            var resumo = await _financeiro.ResumoAsync(inicio, fim);
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
            Clinica.Application.Diagnostico.Registrar("Financeiro — caixa não pôde ser carregado", ex);
            _snackbar.Erro($"Não foi possível carregar o caixa: {ex.Message}");
        }
        finally
        {
            Carregando = false;
        }
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
            Owner = System.Windows.Application.Current?.MainWindow
        };

        if (janela.ShowDialog() != true) return;

        _snackbar.Sucesso("Lançamento registrado.");
        await CarregarAsync();
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

    private static string Rotular(StatusLancamento status) => status switch
    {
        StatusLancamento.Previsto => "Previsto",
        StatusLancamento.Realizado => "Realizado",
        StatusLancamento.Cancelado => "Cancelado",
        _ => status.ToString()
    };
}
