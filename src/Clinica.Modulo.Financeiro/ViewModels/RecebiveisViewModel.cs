using System.Collections.ObjectModel;
using Clinica.Application.Servicos;
using Clinica.Desktop.Controls;
using Clinica.Desktop.Shell;
using Clinica.Domain.Entities;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;

namespace Clinica.Financeiro.ViewModels;

/// <summary>Um depósito esperado da adquirente, como aparece na lista.</summary>
public sealed class LinhaRecebivel
{
    public required string Adquirente { get; init; }
    public required string Previsao { get; init; }
    public required string Bruto { get; init; }
    public required string Taxa { get; init; }
    public required string Liquido { get; init; }
    public required string Detalhe { get; init; }
    public required bool Atrasado { get; init; }
    public required IReadOnlyList<int> LancamentoIds { get; init; }

    public static LinhaRecebivel De(DepositoEsperado d, DateOnly hoje)
    {
        var dias = d.Previsao.DayNumber - hoje.DayNumber;
        return new LinhaRecebivel
        {
            Adquirente = d.Adquirente,
            Previsao = d.Previsao.ToString("dd/MM/yyyy"),
            Bruto = d.Bruto.ToString("C"),
            Taxa = d.Taxa.ToString("C"),
            Liquido = d.Liquido.ToString("C"),
            // O prazo em palavras, como nas contas: ninguém calcula "20/10 menos hoje".
            Detalhe = dias switch
            {
                < 0 => $"{d.Quantidade} venda(s) · DEVIA TER CAÍDO faz {-dias} dia(s)",
                0 => $"{d.Quantidade} venda(s) · cai hoje",
                1 => $"{d.Quantidade} venda(s) · cai amanhã",
                _ => $"{d.Quantidade} venda(s) · cai em {dias} dia(s)"
            },
            Atrasado = d.Atrasado(hoje),
            LancamentoIds = d.LancamentoIds
        };
    }
}

/// <summary>Um depósito que já caiu, com as duas datas — a prevista e a real.</summary>
public sealed class LinhaConfirmado
{
    public required string Adquirente { get; init; }
    public required string Creditado { get; init; }
    public required string Liquido { get; init; }
    public required string Detalhe { get; init; }
    public required bool Atrasou { get; init; }
    public required IReadOnlyList<int> LancamentoIds { get; init; }

    public static LinhaConfirmado De(DepositoConfirmado d)
    {
        // O atraso é o que sobra da confirmação: as duas datas ficam gravadas separadas
        // justamente para ele continuar visível depois de o dinheiro entrar.
        var atraso = d.DiasDeAtraso switch
        {
            null => "sem previsão registrada",
            0 => "caiu no dia previsto",
            < 0 => $"caiu {-d.DiasDeAtraso} dia(s) adiantado",
            _ => $"caiu {d.DiasDeAtraso} dia(s) depois do previsto"
        };

        return new LinhaConfirmado
        {
            Adquirente = d.Adquirente,
            Creditado = d.Creditado.ToString("dd/MM/yyyy"),
            Liquido = d.Liquido.ToString("C"),
            Detalhe = $"{d.Quantidade} venda(s) · {atraso}",
            Atrasou = d.DiasDeAtraso > 0,
            LancamentoIds = d.LancamentoIds
        };
    }
}

/// <summary>
/// Recebíveis de cartão (parcela 16).
///
/// `PrevisaoRecebimento` era gravada desde a parcela 9 e **nenhuma tela a lia**. É o mesmo
/// defeito que já apareceu duas vezes neste projeto — o pacote que debitava e o insumo que
/// baixava, ambos testados e sem chamador: dado gravado sem leitor passa no CI e não faz
/// nada na clínica.
///
/// A pergunta que a tela responde e que ninguém respondia: **o que a maquininha ainda deve
/// depositar, e o que ela deveria ter depositado e não depositou.** A segunda é a que
/// importa: dinheiro de cartão que não caiu não aparece em lugar nenhum do sistema, e some
/// sem ninguém notar até a conciliação bancária do fim do mês — se houver.
///
/// A conferência é por DEPÓSITO, não por venda: a adquirente deposita o lote do dia, e
/// conferir venda a venda contra um extrato que traz um valor só nunca fecharia.
/// </summary>
public sealed partial class RecebiveisViewModel : ObservableObject
{
    private readonly IServiceScopeFactory _escopos;
    private readonly ISnackbarService _snackbar;
    private readonly IDialogoService _dialogo;

    public ObservableCollection<LinhaRecebivel> Depositos { get; } = [];

    /// <summary>Os que já caíram — a lista de onde a confirmação errada é desfeita.</summary>
    public ObservableCollection<LinhaConfirmado> Confirmados { get; } = [];

    public IReadOnlyList<int> Horizontes { get; } = [15, 30, 60, 90];

    [ObservableProperty] private int _horizonteDias = 60;

    /// <summary>Data em que o depósito caiu de fato — informada, nunca assumida.</summary>
    [ObservableProperty] private DateTime? _dataCredito = DateTime.Today;

    [ObservableProperty] private string _aVencer = "—";
    [ObservableProperty] private string _atrasado = "—";
    [ObservableProperty] private string _total = "—";
    [ObservableProperty] private string _proximo = "—";
    [ObservableProperty] private bool _temAtraso;
    [ObservableProperty] private string _resumo = string.Empty;

    /// <summary>Janela dos já creditados: quem procura confirmação errada procura recente.</summary>
    [ObservableProperty] private int _diasConfirmados = 30;

    [ObservableProperty] private string _resumoConfirmados = string.Empty;

    [ObservableProperty] private string? _mensagem;
    [ObservableProperty] private bool _mensagemEhErro;

    public bool PodeEditar => SessaoUsuario.Atual.Pode(Permissao.EditarFinanceiro);

    public RecebiveisViewModel(
        IServiceScopeFactory escopos, ISnackbarService snackbar, IDialogoService dialogo)
    {
        _escopos = escopos;
        _snackbar = snackbar;
        _dialogo = dialogo;
        _ = CarregarAsync();
    }

    partial void OnHorizonteDiasChanged(int value) => _ = CarregarAsync();

    /// <summary>
    /// Descarte de resposta fora de ordem (parcela 50): trocar o horizonte duas vezes
    /// rápido deixaria os depósitos de uma janela sob os totais da outra — num banco
    /// remoto a leitura mais velha pode responder por último.
    /// </summary>
    private int _geracaoCarga;

    [RelayCommand]
    public async Task CarregarAsync()
    {
        var geracao = ++_geracaoCarga;

        try
        {
            Mensagem = null;
            MensagemEhErro = false;

            using var scope = _escopos.CreateScope();
            var recebiveis = scope.ServiceProvider.GetRequiredService<RecebiveisService>();

            var hoje = DateOnly.FromDateTime(DateTime.Today);
            var ate = hoje.AddDays(Math.Max(HorizonteDias, 0));

            var esperados = await recebiveis.EsperadosAsync(ate);

            // Chegou tarde: outra carga mais nova já foi pedida.
            if (geracao != _geracaoCarga) return;

            Depositos.Clear();
            foreach (var d in esperados)
                Depositos.Add(LinhaRecebivel.De(d, hoje));

            var r = await recebiveis.ResumoAsync(hoje, ate);

            // Chegou tarde: outra carga mais nova já foi pedida.
            if (geracao != _geracaoCarga) return;

            AVencer = r.LiquidoAVencer.ToString("C");
            Atrasado = r.LiquidoAtrasado.ToString("C");
            Total = r.Total.ToString("C");
            // "—" e não "R$ 0,00": não haver próximo depósito e o próximo depósito ser
            // zero são coisas diferentes.
            Proximo = r.ProximoDeposito is { } p ? p.ToString("dd/MM/yyyy") : "—";
            TemAtraso = r.TemAtraso;

            Resumo = Depositos.Count == 0
                ? "Nenhum recebimento de cartão pendente no período."
                : r.TemAtraso
                    ? $"{Depositos.Count} depósito(s) previsto(s) · {r.QuantidadeAtrasada} venda(s) que a adquirente JÁ DEVIA TER DEPOSITADO."
                    : $"{Depositos.Count} depósito(s) previsto(s), nenhum atrasado.";

            var confirmados = await recebiveis.ConfirmadosAsync(
                hoje.AddDays(-Math.Max(DiasConfirmados, 0)), hoje);

            // Chegou tarde: outra carga mais nova já foi pedida.
            if (geracao != _geracaoCarga) return;

            Confirmados.Clear();
            foreach (var d in confirmados)
                Confirmados.Add(LinhaConfirmado.De(d));

            var atrasaram = confirmados.Count(d => d.DiasDeAtraso > 0);
            ResumoConfirmados = confirmados.Count == 0
                ? $"Nenhum depósito confirmado nos últimos {DiasConfirmados} dias."
                : $"{confirmados.Count} depósito(s) confirmado(s) nos últimos {DiasConfirmados} dias"
                  + (atrasaram > 0 ? $" · {atrasaram} caiu(íram) depois do previsto." : ".");
        }
        catch (Exception ex)
        {
            if (geracao != _geracaoCarga) return;

            Clinica.Application.Diagnostico.Registrar("Financeiro — recebíveis não puderam ser lidos", ex);
            Erro($"Não foi possível ler os recebíveis: {ex.Message}");
        }
    }

    /// <summary>
    /// Marca o depósito como creditado na data informada.
    ///
    /// A data vem do campo e não de <c>DateTime.Today</c>: a clínica confere o extrato na
    /// segunda-feira e o depósito caiu na sexta. Gravar "hoje" transformaria todo depósito
    /// num atraso de três dias — o número ficaria errado justamente na métrica que a tela
    /// existe para medir.
    /// </summary>
    [RelayCommand]
    private async Task ConfirmarAsync(LinhaRecebivel? linha)
    {
        if (linha is null) return;

        if (DataCredito is not { } dia)
        {
            Erro("Informe em que dia o dinheiro caiu na conta.");
            return;
        }

        try
        {
            SessaoUsuario.Atual.Exigir(Permissao.EditarFinanceiro, "confirmar recebimento");

            if (!_dialogo.Confirmar("Confirmar depósito",
                    $"Confirmar que {linha.Adquirente} depositou {linha.Liquido} "
                    + $"em {dia:dd/MM/yyyy}? ({linha.Detalhe})")) return;

            using var scope = _escopos.CreateScope();
            var recebiveis = scope.ServiceProvider.GetRequiredService<RecebiveisService>();

            var n = await recebiveis.ConfirmarAsync(
                linha.LancamentoIds, DateOnly.FromDateTime(dia), SessaoUsuario.Atual.Operador);

            _snackbar.Sucesso($"{n} recebimento(s) confirmado(s).");
            await CarregarAsync();
        }
        catch (Exception ex)
        {
            Clinica.Application.Diagnostico.Registrar("Financeiro — recebimento não pôde ser confirmado", ex);
            Erro(ex.Message);
        }
    }

    /// <summary>
    /// Devolve o depósito à espera — para quando alguém confirmou o dia errado.
    ///
    /// `DesfazerConfirmacaoAsync` existia desde a parcela 16 e não tinha porta: quem
    /// marcava "caiu" na data errada ficava com um atraso que não houve gravado para
    /// sempre, e com o dinheiro sumido da lista do que ainda falta cair.
    ///
    /// Não apaga nada: só limpa a data do crédito e o recebível volta a ser esperado.
    /// </summary>
    [RelayCommand]
    private async Task DesfazerAsync(LinhaConfirmado? linha)
    {
        if (linha is null) return;

        try
        {
            SessaoUsuario.Atual.Exigir(Permissao.EditarFinanceiro, "desfazer recebimento");

            if (!_dialogo.ConfirmarPerigo("Desfazer confirmação",
                    $"Devolver {linha.Liquido} de {linha.Adquirente} (creditado em {linha.Creditado}) "
                    + "para a lista do que a adquirente ainda deve depositar? "
                    + "O lançamento continua no caixa — só a data do crédito é apagada.")) return;

            using var scope = _escopos.CreateScope();
            var recebiveis = scope.ServiceProvider.GetRequiredService<RecebiveisService>();

            var n = await recebiveis.DesfazerConfirmacaoAsync(
                linha.LancamentoIds, SessaoUsuario.Atual.Operador);

            _snackbar.Info($"{n} recebimento(s) devolvido(s) à espera.");
            await CarregarAsync();
        }
        catch (Exception ex)
        {
            Clinica.Application.Diagnostico.Registrar(
                "Financeiro — confirmação de recebimento não pôde ser desfeita", ex);
            Erro(ex.Message);
        }
    }

    private void Erro(string mensagem)
    {
        Mensagem = mensagem;
        MensagemEhErro = true;
    }
}
