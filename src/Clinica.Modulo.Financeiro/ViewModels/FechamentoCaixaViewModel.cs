using System.Collections.ObjectModel;
using Clinica.Application.Servicos;
using Clinica.Desktop.Controls;
using Clinica.Desktop.Shell;
using Clinica.Domain.Entities;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;

namespace Clinica.Financeiro.ViewModels;

/// <summary>Um fechamento já gravado, como aparece no histórico.</summary>
public sealed class LinhaFechamento
{
    public required DateOnly Dia { get; init; }
    public required string Data { get; init; }
    public required string Esperado { get; init; }
    public required string Contado { get; init; }
    public required string Diferenca { get; init; }
    public required string Situacao { get; init; }
    public required string Detalhe { get; init; }
    public required bool Divergiu { get; init; }
    public required bool Reaberto { get; init; }
    public required bool PodeReabrir { get; init; }

    public static LinhaFechamento De(FechamentoCaixa f) => new()
    {
        Dia = f.Data,
        Data = f.Data.ToString("dd/MM/yyyy"),
        Esperado = f.Esperado.ToString("C"),
        Contado = f.ValorContado.ToString("C"),
        // Zero não vira "R$ 0,00": bateu é um estado, não um número, e escrevê-lo como
        // valor faria a coluna pedir leitura onde não há nada para ler.
        Diferenca = f.Bateu ? "—" : f.Diferenca.ToString("C"),
        Situacao = f.Situacao,
        Detalhe = f.Reaberto
            ? $"reaberto: {f.MotivoReabertura}"
            : string.IsNullOrWhiteSpace(f.Justificativa)
                ? $"{f.QuantidadeLancamentos} lançamento(s) · {f.ConferidoPor ?? "—"}"
                : $"{f.Justificativa} · {f.ConferidoPor ?? "—"}",
        Divergiu = !f.Bateu && !f.Reaberto,
        Reaberto = f.Reaberto,
        PodeReabrir = !f.Reaberto
    };
}

/// <summary>Um dia com dinheiro na gaveta que ninguém contou.</summary>
public sealed class LinhaNaoConferido
{
    public required DateOnly Dia { get; init; }
    public required string Data { get; init; }
    public required string Esperado { get; init; }
    public required string Detalhe { get; init; }

    public static LinhaNaoConferido De(DiaNaoConferido d) => new()
    {
        Dia = d.Data,
        Data = d.Data.ToString("dd/MM/yyyy"),
        Esperado = d.Esperado.ToString("C"),
        // "Faltou R$ 50 em 3 lançamentos" e "em 40" pedem investigações diferentes.
        Detalhe = $"{d.QuantidadeLancamentos} lançamento(s) em espécie"
    };
}

/// <summary>
/// Conferência da gaveta (parcela 14).
///
/// O módulo sabia tudo sobre o dinheiro REGISTRADO e nada sobre o dinheiro FÍSICO. A
/// diferença entre os dois é onde some dinheiro numa clínica — a venda que ninguém lançou,
/// o troco que saiu sem registro —, e ela só aparece quando alguém conta a gaveta e o
/// sistema cobra a explicação.
///
/// A tela é PROPOSTA CONFIRMADA, como o fechamento da sessão: o sistema soma o que passou
/// pela gaveta, e o número que vale continua sendo o que a pessoa contou. Só espécie entra
/// na conta — cartão e PIX caem na conta dias depois, e incluí-los faria a conferência
/// nunca bater, o que treinaria a clínica a clicar "OK" sem olhar.
///
/// O painel de dias não conferidos é o que dá utilidade ao resto: uma conferência que só
/// existe quando alguém lembra de abrir a tela não controla nada.
/// </summary>
public sealed partial class FechamentoCaixaViewModel : ObservableObject
{
    private readonly IServiceScopeFactory _escopos;
    private readonly ISnackbarService _snackbar;
    private readonly IDialogoService _dialogo;

    public ObservableCollection<LinhaFechamento> Historico { get; } = [];
    public ObservableCollection<LinhaNaoConferido> NaoConferidos { get; } = [];

    [ObservableProperty] private DateTime? _dia = DateTime.Today;

    // ---- Proposta do dia escolhido ----
    [ObservableProperty] private string _entradasEspecie = "—";
    [ObservableProperty] private string _saidasEspecie = "—";
    [ObservableProperty] private string _esperado = "—";
    [ObservableProperty] private string _lancamentos = "—";
    [ObservableProperty] private string? _jaConferido;
    [ObservableProperty] private bool _podeConferir;

    // ---- Contagem ----
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TemDiferenca))]
    private string? _valorContado;

    [ObservableProperty] private string? _justificativa;
    [ObservableProperty] private string _diferencaPrevia = "—";
    [ObservableProperty] private bool _temDiferencaCalculada;

    [ObservableProperty] private string _resumo = string.Empty;
    [ObservableProperty] private string? _mensagem;
    [ObservableProperty] private bool _mensagemEhErro;

    private decimal _esperadoNumero;

    /// <summary>
    /// Há divergência a explicar. A caixa de justificativa aparece SÓ neste caso: pedi-la
    /// sempre transformaria o campo em ritual, e o campo de ritual é preenchido com um
    /// ponto final.
    /// </summary>
    public bool TemDiferenca => TemDiferencaCalculada;

    /// <summary>Metade visível da permissão; a que impede é o <c>Exigir</c> no comando.</summary>
    public bool PodeEditar => SessaoUsuario.Atual.Pode(Permissao.EditarFinanceiro);

    public FechamentoCaixaViewModel(
        IServiceScopeFactory escopos, ISnackbarService snackbar, IDialogoService dialogo)
    {
        _escopos = escopos;
        _snackbar = snackbar;
        _dialogo = dialogo;
        _ = CarregarAsync();
    }

    partial void OnDiaChanged(DateTime? value) => _ = CarregarAsync();

    partial void OnValorContadoChanged(string? value) => RecalcularDiferenca();

    partial void OnTemDiferencaCalculadaChanged(bool value) => OnPropertyChanged(nameof(TemDiferenca));

    /// <summary>
    /// Mostra a diferença ANTES de gravar, enquanto a pessoa digita. É o momento em que
    /// ela ainda tem a gaveta aberta na frente e pode recontar — descobrir a falta só
    /// depois de salvar transformaria a correção numa reabertura.
    /// </summary>
    private void RecalcularDiferenca()
    {
        if (!Valores.TentarLerDecimal(ValorContado, out var contado))
        {
            DiferencaPrevia = "—";
            TemDiferencaCalculada = false;
            return;
        }

        var diferenca = contado - _esperadoNumero;
        TemDiferencaCalculada = diferenca != 0m;
        DiferencaPrevia = diferenca == 0m
            ? "Bateu."
            : diferenca > 0m
                ? $"Sobra de {diferenca:C} — costuma ser venda não lançada."
                : $"Falta de {Math.Abs(diferenca):C}.";
    }

    [RelayCommand]
    public async Task CarregarAsync()
    {
        try
        {
            Mensagem = null;
            MensagemEhErro = false;

            if (Dia is not { } escolhido) return;
            var dia = DateOnly.FromDateTime(escolhido);

            using var scope = _escopos.CreateScope();
            var fechamentos = scope.ServiceProvider.GetRequiredService<FechamentoCaixaService>();

            var proposta = await fechamentos.PrepararAsync(dia);
            _esperadoNumero = proposta.Esperado;
            EntradasEspecie = proposta.EntradasEspecie.ToString("C");
            SaidasEspecie = proposta.SaidasEspecie.ToString("C");
            Esperado = proposta.Esperado.ToString("C");
            Lancamentos = $"{proposta.QuantidadeLancamentos} lançamento(s) em espécie";

            // Conferido e "ainda não conferido" têm a mesma cara de calmaria na tela —
            // e são estados diferentes. A tela diz qual é.
            JaConferido = proposta.JaConferido is { } f
                ? $"Conferido em {f.ConferidoEm:dd/MM/yyyy HH:mm} por {f.ConferidoPor ?? "—"}: "
                  + (f.Bateu ? "bateu." : $"diferença de {f.Diferenca:C} — {f.Justificativa}")
                : null;
            PodeConferir = proposta.JaConferido is null;

            RecalcularDiferenca();

            var hoje = DateOnly.FromDateTime(DateTime.Today);
            var inicio = hoje.AddDays(-60);

            Historico.Clear();
            foreach (var f in await fechamentos.HistoricoAsync(inicio, hoje))
                Historico.Add(LinhaFechamento.De(f));

            NaoConferidos.Clear();
            foreach (var d in await fechamentos.NaoConferidosAsync(inicio, hoje))
                NaoConferidos.Add(LinhaNaoConferido.De(d));

            Resumo = NaoConferidos.Count == 0
                ? "Todos os dias com dinheiro na gaveta foram conferidos nos últimos 60 dias."
                : $"{NaoConferidos.Count} dia(s) com dinheiro na gaveta AINDA NÃO CONFERIDO(S).";
        }
        catch (Exception ex)
        {
            Clinica.Application.Diagnostico.Registrar("Financeiro — fechamento de caixa não pôde ser lido", ex);
            Erro($"Não foi possível montar a conferência: {ex.Message}");
        }
    }

    [RelayCommand]
    private async Task ConferirAsync()
    {
        Mensagem = null;
        MensagemEhErro = false;

        if (Dia is not { } escolhido) return;
        if (!Valores.TentarLerDecimal(ValorContado, out var contado))
        {
            Erro("Informe quanto havia na gaveta (ex.: 1.250,00).");
            return;
        }

        try
        {
            SessaoUsuario.Atual.Exigir(Permissao.EditarFinanceiro, "conferir o caixa");

            using var scope = _escopos.CreateScope();
            var fechamentos = scope.ServiceProvider.GetRequiredService<FechamentoCaixaService>();

            await fechamentos.ConferirAsync(
                DateOnly.FromDateTime(escolhido), contado, Justificativa,
                operador: SessaoUsuario.Atual.Operador);

            _snackbar.Sucesso(TemDiferenca
                ? "Caixa conferido com a divergência registrada."
                : "Caixa conferido — bateu.");
            ValorContado = null;
            Justificativa = null;
            await CarregarAsync();
        }
        catch (Exception ex)
        {
            Clinica.Application.Diagnostico.Registrar("Financeiro — caixa não pôde ser conferido", ex);
            // O erro fica INLINE, não no snackbar: a mensagem da divergência sem
            // justificativa precisa ficar na tela enquanto a pessoa escreve a explicação.
            Erro(ex.Message);
        }
    }

    [RelayCommand]
    private async Task ReabrirAsync(LinhaFechamento? linha)
    {
        if (linha is null) return;

        try
        {
            SessaoUsuario.Atual.Exigir(Permissao.EditarFinanceiro, "reabrir o caixa");

            var motivo = _dialogo.PerguntarTexto("Reabrir o caixa",
                $"Por que o caixa de {linha.Data} precisa ser recontado? "
                + "A conferência atual continua no histórico, marcada com este motivo.");
            if (string.IsNullOrWhiteSpace(motivo)) return;

            using var scope = _escopos.CreateScope();
            var fechamentos = scope.ServiceProvider.GetRequiredService<FechamentoCaixaService>();
            await fechamentos.ReabrirAsync(linha.Dia, motivo!, SessaoUsuario.Atual.Operador);

            _snackbar.Info($"Caixa de {linha.Data} reaberto para recontagem.");
            Dia = linha.Dia.ToDateTime(TimeOnly.MinValue);
            await CarregarAsync();
        }
        catch (Exception ex)
        {
            Clinica.Application.Diagnostico.Registrar("Financeiro — caixa não pôde ser reaberto", ex);
            Erro(ex.Message);
        }
    }

    /// <summary>Pula para o dia pendente escolhido — o caminho do painel para a contagem.</summary>
    [RelayCommand]
    private void IrPara(LinhaNaoConferido? linha)
    {
        if (linha is null) return;
        Dia = linha.Dia.ToDateTime(TimeOnly.MinValue);
    }

    private void Erro(string mensagem)
    {
        Mensagem = mensagem;
        MensagemEhErro = true;
    }
}
