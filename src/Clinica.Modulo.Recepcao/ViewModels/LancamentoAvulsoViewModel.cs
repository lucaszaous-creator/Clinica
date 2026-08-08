using System.Collections.ObjectModel;
using Clinica.Application.Abstracoes;
using Clinica.Desktop.Controls;
using Clinica.Desktop.Shell.Configuracao;
using Clinica.Domain;
using Clinica.Domain.Entities;
using Clinica.Domain.Regras;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;

namespace Clinica.Recepcao.ViewModels;

/// <summary>Um avulso lançado hoje, na conferência do balcão.</summary>
public sealed class LinhaAvulso
{
    public required string Paciente { get; init; }
    public required string Modalidade { get; init; }
    public required string Convenio { get; init; }
    public required string Numero { get; init; }
    public required string Guias { get; init; }
    public required string Pendencia { get; init; }
    public required bool TemPendencia { get; init; }
}

/// <summary>
/// A seção "Novo atendimento" da sidebar (parcela 47).
///
/// O que ela NÃO é mais: o formulário. Ele virou janela
/// (<see cref="Janelas.NovoAtendimentoWindow"/>).
///
/// Por quê
/// -------
/// A seção era uma coluna de 420 px à esquerda com o formulário dentro, e o resto da tela
/// para o resultado. Isso é a faixa lateral que o `README.md` proíbe, e o cliente a
/// reprovou de novo em voz alta. A pergunta que a regra manda fazer é <b>"isto é o que a
/// pessoa VÊ nesta tela, ou o que ela FAZ de vez em quando?"</b> — lançar um avulso é
/// claramente o segundo, e o segundo caso é botão ou janela, nunca painel aberto ocupando
/// espaço permanente. Foi exatamente o caminho do mapa corporal e do formulário de medida
/// na parcela 37.
///
/// O que sobrou na seção é o que se OLHA: os avulsos lançados HOJE, e o botão que abre a
/// janela. E isso não é enfeite para justificar o item de menu — é uma conferência que não
/// existia em lugar nenhum: quem lançava um avulso não tinha como revisar o que lançou sem
/// ir até o app de faturamento, que é de outra pessoa.
/// </summary>
public sealed partial class LancamentoAvulsoViewModel : ObservableObject
{
    private readonly IServiceScopeFactory _escopos;
    private readonly ISnackbarService _snackbar;

    public ObservableCollection<LinhaAvulso> LancadosHoje { get; } = new();

    [ObservableProperty] private bool _carregando;

    /// <summary>
    /// A leitura FALHOU — o terceiro estado. Lista vazia por erro é idêntica a lista vazia
    /// por não ter havido avulso nenhum, e as duas levam a conclusões opostas.
    /// </summary>
    [ObservableProperty] private bool _naoVerificado;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TemMensagem))]
    private string? _mensagem;

    public bool TemMensagem => !string.IsNullOrWhiteSpace(Mensagem);

    /// <summary>
    /// Metade VISÍVEL da permissão. A que IMPEDE é o <c>Exigir</c> dentro do
    /// <c>NovoAtendimentoViewModel.Lancar</c> — abrir a janela não grava nada, mas um botão
    /// que abre um formulário inútil é pior do que um botão apagado.
    /// </summary>
    public bool PodeLancar => SessaoUsuario.Atual.Pode(Permissao.LancarAtendimento);

    public LancamentoAvulsoViewModel(IServiceScopeFactory escopos, ISnackbarService snackbar)
    {
        _escopos = escopos;
        _snackbar = snackbar;
    }

    public Task CarregarAsync() => CarregarDoDiaAsync();

    /// <summary>
    /// Abre a janela do lançamento. Ela NÃO fecha ao lançar — o resultado (as guias, com a
    /// data em que cada uma libera) é a parte que a recepcionista precisa ler, muitas vezes
    /// com o paciente ainda na frente. Quando ela fecha, a conferência do dia recarrega.
    /// </summary>
    [RelayCommand]
    private async Task AbrirLancamentoAsync()
    {
        SessaoUsuario.Atual.Exigir(Permissao.LancarAtendimento, "lançar atendimento");

        var vm = new NovoAtendimentoViewModel(_escopos);
        await vm.CarregarAsync();

        var janela = new Janelas.NovoAtendimentoWindow(vm)
        {
            Owner = System.Windows.Application.Current?.MainWindow
        };
        janela.ShowDialog();

        if (vm.NumeroAtendimento is { } numero)
            _snackbar.Sucesso($"Atendimento nº {numero} lançado.");

        await CarregarDoDiaAsync();
    }

    /// <summary>
    /// Os avulsos de hoje. O lançamento avulso registra o atendimento também na agenda
    /// (<c>registrarNaAgenda: true</c>) com <see cref="OrigemAgendamento.Manual"/> — é por
    /// esse par que se distingue quem entrou por aqui de quem veio da agenda pela Fila.
    /// </summary>
    [RelayCommand]
    public async Task CarregarDoDiaAsync()
    {
        try
        {
            Carregando = true;
            NaoVerificado = false;
            Mensagem = null;

            using var scope = _escopos.CreateScope();
            var repo = scope.ServiceProvider.GetRequiredService<IClinicaRepositorio>();

            var hoje = DateTime.Today;
            var agendamentos = await repo.AgendamentosNoPeriodoAsync(hoje, hoje.AddDays(1).AddTicks(-1));

            LancadosHoje.Clear();
            foreach (var ag in agendamentos
                         .Where(a => a.Origem == OrigemAgendamento.Manual && a.AtendimentoId is not null)
                         .OrderByDescending(a => a.AtendimentoId))
            {
                var atendimento = await repo.ObterAtendimentoAsync(ag.AtendimentoId!.Value);
                if (atendimento is null) continue;

                LancadosHoje.Add(Montar(atendimento, hoje));
            }
        }
        catch (Exception ex)
        {
            NaoVerificado = true;
            LogSuite.Registrar("Novo atendimento — avulsos do dia não puderam ser lidos", ex);
            Mensagem = $"Não foi possível ler os atendimentos de hoje: {ex.Message}";
        }
        finally
        {
            Carregando = false;
        }
    }

    private static LinhaAvulso Montar(Atendimento atendimento, DateTime hoje)
    {
        var faturaveis = atendimento.Codigos
            .Where(c => c.Status != StatusCodigo.NaoAplicavel)
            .ToList();

        // A guia que só libera depois é o assunto do produto. Ela vem marcada aqui também,
        // na conferência do fim do dia: é a última chance de alguém notar antes de o dia
        // fechar e a guia virar pendência de amanhã.
        var depois = faturaveis
            .Where(c => !c.Baixado && c.DataPrevistaFaturamento > DateOnly.FromDateTime(hoje))
            .OrderBy(c => c.DataPrevistaFaturamento)
            .ToList();

        var paciente = atendimento.Paciente;

        return new LinhaAvulso
        {
            Paciente = paciente?.Nome ?? "—",
            Modalidade = atendimento.ModalidadeCodigo is { } cod
                ? CatalogoModalidades.Nome(cod)
                : ModalidadeInfo.NomeExibicao(atendimento.Modalidade),
            Convenio = paciente is null
                ? "—"
                : CatalogoConvenios.Nome(paciente.ConvenioCodigo ?? paciente.Convenio.ToString()),
            Numero = atendimento.Numero ?? $"#{atendimento.Id}",
            Guias = faturaveis.Count == 1 ? "1 guia" : $"{faturaveis.Count} guias",
            TemPendencia = depois.Count > 0,
            Pendencia = depois.Count == 0
                ? "todas liberadas"
                : $"{depois.Count} libera(m) a partir de {depois[0].DataPrevistaFaturamento:dd/MM}"
        };
    }
}
