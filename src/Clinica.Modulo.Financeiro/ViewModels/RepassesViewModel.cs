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

/// <summary>O repasse calculado de um profissional no período.</summary>
public sealed class LinhaRepasse
{
    public required int ProfissionalId { get; init; }
    public required string Profissional { get; init; }
    public required string Atendimentos { get; init; }
    public required string Receita { get; init; }
    public required string Valor { get; init; }
    public required string Regra { get; init; }
    public required string Situacao { get; init; }
    public required bool PodeApurar { get; init; }
}

/// <summary>Uma apuração já fechada.</summary>
public sealed class LinhaApuracao
{
    public required int Id { get; init; }
    public required string Profissional { get; init; }
    public required string Periodo { get; init; }
    public required string Valor { get; init; }
    public required string Situacao { get; init; }
    public required bool PodeCancelar { get; init; }
}

/// <summary>Uma regra de repasse cadastrada.</summary>
public sealed class LinhaRegraRepasse
{
    public required int Id { get; init; }
    public required string Profissional { get; init; }
    public required string Regra { get; init; }
    public required string Vigencia { get; init; }
    public required bool Ativa { get; init; }
}

/// <summary>
/// Repasse por profissional (a metade que faltava da feature 09).
///
/// A tela mostra o cálculo ANTES de apurar: quem paga precisa ver de onde saiu o número
/// — quantos atendimentos, quanta receita entrou e qual regra foi aplicada — antes de o
/// dinheiro virar uma saída no caixa.
/// </summary>
public sealed partial class RepassesViewModel : ObservableObject
{
    private readonly IServiceScopeFactory _escopos;
    private readonly ISnackbarService _snackbar;
    private readonly IDialogoService _dialogo;

    public ObservableCollection<LinhaRepasse> Calculados { get; } = [];
    public ObservableCollection<LinhaApuracao> Apuracoes { get; } = [];
    public ObservableCollection<LinhaRegraRepasse> Regras { get; } = [];

    [ObservableProperty]
    private DateTime _mes = new(DateTime.Today.Year, DateTime.Today.Month, 1);

    [ObservableProperty] private bool _carregando;

    /// <summary>
    /// A leitura FALHOU — o terceiro estado. Sem ele, tela vazia por erro fica idêntica
    /// a tela vazia por não haver nada.
    /// </summary>
    [ObservableProperty] private bool _naoVerificado;
    [ObservableProperty] private string _mensagem = string.Empty;
    [ObservableProperty] private bool _mensagemEhErro;
    [ObservableProperty] private string _resumo = "—";

    public string PeriodoRotulo => $"{Inicio:dd/MM/yyyy} a {Fim:dd/MM/yyyy}";

    private DateOnly Inicio => DateOnly.FromDateTime(new DateTime(Mes.Year, Mes.Month, 1));

    private DateOnly Fim => Inicio.AddMonths(1).AddDays(-1);

    /// <summary>
    /// Habilita os botões de escrita da tela. É a metade VISÍVEL da permissão: o
    /// botão apagado explica por que não dá; a guarda no comando é que impede.
    /// Só desabilitar seria enfeite — um atalho de teclado passaria direto.
    /// </summary>
    public bool PodeEditarFinanceiro => SessaoUsuario.Atual.Pode(Permissao.EditarFinanceiro);

    /// <summary>
    /// ⚠️ Nada de serviço SCOPED no construtor — o shell resolve esta tela do provedor
    /// RAIZ, e Scoped pedido à raiz vive pela vida inteira do app, com o `DbContext`
    /// junto (parcela 69). Escopo por operação. Ver a checagem 37 do verificar-suite.
    /// </summary>
    public RepassesViewModel(
        IServiceScopeFactory escopos, ISnackbarService snackbar, IDialogoService dialogo)
    {
        _escopos = escopos;
        _snackbar = snackbar;
        _dialogo = dialogo;
        _ = CarregarAsync();
    }

    partial void OnMesChanged(DateTime value)
    {
        OnPropertyChanged(nameof(PeriodoRotulo));
        _ = CarregarAsync();
    }

    /// <summary>
    /// Descarte de resposta fora de ordem (parcela 50): trocar o mês duas vezes rápido
    /// deixaria o cálculo de um mês sob o rótulo de período do outro — num banco remoto
    /// a leitura mais velha pode responder por último, e é sobre esse valor que se apura.
    /// </summary>
    private int _geracaoCarga;

    [RelayCommand]
    private async Task CarregarAsync()
    {
        var geracao = ++_geracaoCarga;

        try
        {
            Carregando = true;
            NaoVerificado = false;
            Mensagem = string.Empty;
            MensagemEhErro = false;

            using var escopo = _escopos.CreateScope();
            var repasses = escopo.ServiceProvider.GetRequiredService<RepasseService>();

            var calculados = await repasses.CalcularAsync(Inicio, Fim);

            // Chegou tarde: outra carga mais nova já foi pedida.
            if (geracao != _geracaoCarga) return;

            Calculados.Clear();
            foreach (var c in calculados)
                Calculados.Add(new LinhaRepasse
                {
                    ProfissionalId = c.ProfissionalId,
                    Profissional = c.Profissional,
                    Atendimentos = c.Atendimentos.ToString(),
                    Receita = c.ReceitaVinculada.ToString("C"),
                    Valor = c.Valor.ToString("C"),
                    Regra = c.RegraDescricao,
                    Situacao = c.Situacao,
                    PodeApurar = c.TemRegra && !c.JaApurado && c.Valor > 0
                });

            var apuracoes = await repasses.ApuradosAsync();

            // Chegou tarde: outra carga mais nova já foi pedida.
            if (geracao != _geracaoCarga) return;

            Apuracoes.Clear();
            foreach (var a in apuracoes)
                Apuracoes.Add(new LinhaApuracao
                {
                    Id = a.Id,
                    Profissional = a.Profissional?.Rotulo ?? "—",
                    Periodo = $"{a.Inicio:dd/MM/yyyy} a {a.Fim:dd/MM/yyyy}",
                    Valor = a.Valor.ToString("C"),
                    Situacao = a.Cancelado ? $"Cancelado em {a.CanceladoEm:dd/MM/yyyy}" : "Apurado",
                    PodeCancelar = !a.Cancelado
                });

            await CarregarRegrasAsync(geracao);

            // Chegou tarde: outra carga mais nova já foi pedida.
            if (geracao != _geracaoCarga) return;

            var total = calculados.Sum(c => c.Valor);
            var semRegra = calculados.Count(c => !c.TemRegra);
            Resumo = $"{calculados.Count} profissional(is) com atendimento no período · "
                     + $"{total:C} a repassar"
                     + (semRegra > 0 ? $" · {semRegra} sem regra cadastrada" : string.Empty);
        }
        catch (Exception ex)
        {
            if (geracao != _geracaoCarga) return;

            NaoVerificado = true;
            Clinica.Application.Diagnostico.Registrar("Financeiro — repasses não puderam ser calculados", ex);
            Erro($"Não foi possível calcular os repasses: {ex.Message}");
        }
        finally
        {
            // A carga superada não apaga o "Carregando" da que ainda está no ar.
            if (geracao == _geracaoCarga) Carregando = false;
        }
    }

    private async Task CarregarRegrasAsync(int geracao)
    {
        using var escopo = _escopos.CreateScope();
        var regras = await escopo.ServiceProvider
            .GetRequiredService<RepasseService>().RegrasAsync();

        // Chegou tarde: outra carga mais nova já foi pedida.
        if (geracao != _geracaoCarga) return;

        Regras.Clear();
        foreach (var r in regras)
            Regras.Add(new LinhaRegraRepasse
            {
                Id = r.Id,
                Profissional = r.Profissional?.Rotulo ?? $"Profissional {r.ProfissionalId}",
                Regra = r.Descricao,
                Vigencia = (r.VigenteDe, r.VigenteAte) switch
                {
                    (null, null) => "sempre",
                    ({ } de, null) => $"a partir de {de:dd/MM/yyyy}",
                    (null, { } ate) => $"até {ate:dd/MM/yyyy}",
                    var (de, ate) => $"{de:dd/MM/yyyy} a {ate:dd/MM/yyyy}"
                },
                Ativa = r.Ativa
            });
    }

    /// <summary>
    /// O extrato do mês em CSV — o papel que o profissional recebe junto com o repasse
    /// (parcela 32).
    ///
    /// A tela mostra quanto cada um tem a receber e a clínica lia esse número em voz
    /// alta. Repasse é a conversa mais sensível que uma clínica tem com quem atende: o
    /// número precisa vir com a CONTA que o produziu — atendimentos, receita que entrou e
    /// a regra aplicada —, senão discordar dele vira discutir memória.
    /// </summary>
    [RelayCommand]
    private async Task ExportarAsync()
    {
        if (Calculados.Count == 0)
        {
            _snackbar.Info("Não há repasse para exportar neste mês.");
            return;
        }

        try
        {
            var csv = ExportacaoCsv.Montar(
                ["Profissional", "Atendimentos", "Receita que entrou", "Regra", "A receber", "Situação"],
                Calculados.Select(r => new[]
                {
                    r.Profissional, r.Atendimentos, r.Receita, r.Regra, r.Valor, r.Situacao
                }));

            var erro = await ImpressaoPdf.SalvarEAbrirAsync(
                csv,
                ImpressaoPdf.NomeSeguro($"repasses-{Mes:yyyy-MM}.csv"),
                "CSV (*.csv)|*.csv", ".csv");

            if (erro is not null) _snackbar.Erro(erro);
        }
        catch (Exception ex)
        {
            Clinica.Application.Diagnostico.Registrar(
                "Financeiro — repasses não puderam ser exportados", ex);
            _snackbar.Erro(ex.Message);
        }
    }

    [RelayCommand]
    private void MesAnterior() => Mes = Mes.AddMonths(-1);

    [RelayCommand]
    private void ProximoMes() => Mes = Mes.AddMonths(1);

    [RelayCommand]
    private async Task ApurarAsync(LinhaRepasse? linha)
    {
        if (linha is null) return;

        SessaoUsuario.Atual.Exigir(Permissao.EditarFinanceiro, "mexer nos repasses");

        if (!_dialogo.Confirmar("Apurar repasse",
                $"Fechar {linha.Valor} para {linha.Profissional} em {PeriodoRotulo}? "
                + "Isso cria uma SAÍDA PREVISTA no caixa e impede apurar o mesmo período de novo.")) return;

        try
        {
            using (var escopo = _escopos.CreateScope())
                await escopo.ServiceProvider.GetRequiredService<RepasseService>()
                    .ApurarAsync(linha.ProfissionalId, Inicio, Fim, categoriaId: null,
                                 operador: SessaoUsuario.Atual.Operador);

            _snackbar.Sucesso("Repasse apurado e lançado no caixa como saída prevista.");
            await CarregarAsync();
        }
        catch (Exception ex)
        {
            Clinica.Application.Diagnostico.Registrar("Financeiro — repasse não pôde ser apurado", ex);
            Erro(ex.Message);
        }
    }

    [RelayCommand]
    private async Task CancelarApuracaoAsync(LinhaApuracao? linha)
    {
        if (linha is null) return;

        SessaoUsuario.Atual.Exigir(Permissao.EditarFinanceiro, "mexer nos repasses");

        var motivo = _dialogo.PerguntarTexto(
            "Cancelar apuração",
            $"Por que a apuração de {linha.Profissional} ({linha.Periodo}) está sendo cancelada? "
            + "A saída prevista no caixa é cancelada junto.");
        if (string.IsNullOrWhiteSpace(motivo)) return;

        try
        {
            using (var escopo = _escopos.CreateScope())
                await escopo.ServiceProvider.GetRequiredService<RepasseService>()
                    .CancelarApuracaoAsync(linha.Id, motivo, SessaoUsuario.Atual.Operador);
            _snackbar.Info("Apuração cancelada.");
            await CarregarAsync();
        }
        catch (Exception ex)
        {
            Clinica.Application.Diagnostico.Registrar("Financeiro — apuração não pôde ser cancelada", ex);
            Erro(ex.Message);
        }
    }

    /// <summary>
    /// Abre as REGRAS e as APURAÇÕES (parcela 49) — as duas eram uma coluna de 400 px
    /// permanente ao lado do cálculo do período.
    ///
    /// A janela recebe ESTE ViewModel: mudar uma regra ou cancelar uma apuração muda o
    /// número que está na tela de trás, e dois VMs dariam duas respostas para a mesma
    /// pergunta.
    /// </summary>
    [RelayCommand]
    private void AbrirRegras()
    {
        new Janelas.RegrasRepasseWindow(this)
        {
            Owner = JanelaDona.Atual()
        }.ShowDialog();
    }

    [RelayCommand]
    private async Task NovaRegraAsync()
    {
        SessaoUsuario.Atual.Exigir(Permissao.EditarFinanceiro, "mexer nos repasses");

        var vm = new RegraRepasseViewModel(_escopos);
        var janela = new Janelas.RegraRepasseWindow(vm)
        {
            Owner = JanelaDona.Atual()
        };

        if (janela.ShowDialog() != true) return;
        _snackbar.Sucesso("Regra de repasse salva.");
        await CarregarAsync();
    }

    [RelayCommand]
    private async Task ExcluirRegraAsync(LinhaRegraRepasse? linha)
    {
        if (linha is null) return;

        SessaoUsuario.Atual.Exigir(Permissao.EditarFinanceiro, "mexer nos repasses");
        if (!_dialogo.ConfirmarPerigo("Excluir regra",
                $"Apagar a regra de {linha.Profissional} ({linha.Regra})? As apurações JÁ FEITAS "
                + "continuam valendo — elas guardam a própria cópia do cálculo.")) return;

        try
        {
            using (var escopo = _escopos.CreateScope())
                await escopo.ServiceProvider.GetRequiredService<RepasseService>()
                    .ExcluirRegraAsync(linha.Id);
            _snackbar.Info("Regra excluída.");
            await CarregarAsync();
        }
        catch (Exception ex)
        {
            Clinica.Application.Diagnostico.Registrar("Financeiro — regra de repasse não pôde ser excluída", ex);
            Erro(ex.Message);
        }
    }

    private void Erro(string texto)
    {
        Mensagem = texto;
        MensagemEhErro = true;
    }
}

/// <summary>Cadastro de uma regra de repasse.</summary>
public sealed partial class RegraRepasseViewModel : ObservableObject
{
    private readonly IServiceScopeFactory _escopos;

    public ObservableCollection<Profissional> Profissionais { get; } = [];

    public IReadOnlyList<BaseRepasse> Bases { get; } = Enum.GetValues<BaseRepasse>();

    [ObservableProperty] private Profissional? _profissional;
    [ObservableProperty] private BaseRepasse _base = BaseRepasse.PercentualDaReceita;
    [ObservableProperty] private string? _percentual;
    [ObservableProperty] private string? _valorPorAtendimento;
    [ObservableProperty] private DateTime? _vigenteDe = DateTime.Today;
    [ObservableProperty] private DateTime? _vigenteAte;
    [ObservableProperty] private string? _observacoes;

    [ObservableProperty] private string _mensagem = string.Empty;
    [ObservableProperty] private bool _mensagemEhErro;
    [ObservableProperty] private bool _salvando;

    public bool EhPercentual => Base == BaseRepasse.PercentualDaReceita;

    public event Action? Concluido;

    public RegraRepasseViewModel(IServiceScopeFactory escopos)
    {
        _escopos = escopos;
        _ = CarregarAsync();
    }

    partial void OnBaseChanged(BaseRepasse value) => OnPropertyChanged(nameof(EhPercentual));

    private async Task CarregarAsync()
    {
        try
        {
            // Monta em lista local e só ENTÃO publica: entre o Clear e o último Add não
            // pode haver await (parcela 62).
            using var escopo = _escopos.CreateScope();
            var ativos = await escopo.ServiceProvider
                .GetRequiredService<EquipeService>().ProfissionaisAtivosAsync();

            Profissionais.Clear();
            foreach (var p in ativos) Profissionais.Add(p);
            Profissional = Profissionais.FirstOrDefault();
        }
        catch (Exception ex)
        {
            Clinica.Application.Diagnostico.Registrar("Financeiro — equipe não pôde ser lida", ex);
            Mensagem = ex.Message;
            MensagemEhErro = true;
        }
    }

    [RelayCommand]
    private async Task SalvarAsync()
    {
        Mensagem = string.Empty;
        MensagemEhErro = false;

        if (Profissional is not { } profissional)
        {
            Mensagem = "Escolha o profissional.";
            MensagemEhErro = true;
            return;
        }

        try
        {
            Salvando = true;

            decimal? percentual = null;
            decimal? porAtendimento = null;

            if (EhPercentual)
            {
                if (!decimal.TryParse(Percentual, out var lido))
                    throw new InvalidOperationException("Informe o percentual (ex.: 40).");
                percentual = lido;
            }
            else
            {
                if (!decimal.TryParse(ValorPorAtendimento, out var lido))
                    throw new InvalidOperationException("Informe o valor por atendimento (ex.: 50,00).");
                porAtendimento = lido;
            }

            using var escopo = _escopos.CreateScope();
            await escopo.ServiceProvider.GetRequiredService<RepasseService>()
                .SalvarRegraAsync(new RegraRepasse
            {
                ProfissionalId = profissional.Id,
                Base = Base,
                Percentual = percentual,
                ValorPorAtendimento = porAtendimento,
                VigenteDe = VigenteDe is { } de ? DateOnly.FromDateTime(de) : null,
                VigenteAte = VigenteAte is { } ate ? DateOnly.FromDateTime(ate) : null,
                Ativa = true,
                Observacoes = Observacoes
            }, SessaoUsuario.Atual.Operador);

            Concluido?.Invoke();
        }
        catch (Exception ex)
        {
            Clinica.Application.Diagnostico.Registrar("Financeiro — regra de repasse não pôde ser salva", ex);
            Mensagem = ex.Message;
            MensagemEhErro = true;
        }
        finally
        {
            Salvando = false;
        }
    }
}
