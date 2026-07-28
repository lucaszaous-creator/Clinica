using System.Collections.ObjectModel;
using System.Windows.Threading;
using Clinica.Application.Servicos;
using Clinica.Desktop.Controls;
using Clinica.Desktop.Shell;
using Clinica.Domain.Entities;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Clinica.Recepcao.ViewModels;

/// <summary>Um cartão da fila (agendamento + o que a recepção precisa ver de relance).</summary>
public sealed partial class CartaoFila : ObservableObject
{
    public required int AgendamentoId { get; init; }
    public required string Horario { get; init; }
    public required string Paciente { get; init; }
    public required string Modalidade { get; init; }
    public required string Profissional { get; init; }
    public required string Sala { get; init; }
    public required EtapaFila Etapa { get; init; }
    public string? Observacoes { get; init; }

    /// <summary>Retorno sugerido automaticamente para obter o 2º código.</summary>
    public required bool EhRetornoDoSegundoCodigo { get; init; }

    /// <summary>Marcado por cima de um horário ocupado, com o choque aceito.</summary>
    public required bool EhEncaixe { get; init; }

    /// <summary>
    /// O paciente tem guia pendente de baixa. É o aviso mais valioso da recepção: ele
    /// está no balcão AGORA, e é a única hora barata de cobrar o documento.
    /// </summary>
    [ObservableProperty]
    private bool _temGuiaPendente;

    /// <summary>Tempo de espera formatado ("12 min"). Vazio antes do check-in.</summary>
    [ObservableProperty]
    private string _espera = string.Empty;

    /// <summary>Espera longa (30 min ou mais) — o cartão fica em destaque.</summary>
    [ObservableProperty]
    private bool _esperaLonga;

    public bool PodeChegar => Etapa == EtapaFila.Aguardando;
    public bool PodeIniciar => Etapa is EtapaFila.Aguardando or EtapaFila.Chegou;
    public bool PodeFinalizar => Etapa == EtapaFila.EmAtendimento;
    public bool PodeVoltar => Etapa is EtapaFila.Chegou or EtapaFila.EmAtendimento;

    /// <summary>Só horário em aberto aceita falta/cancelamento.</summary>
    public bool EmAberto => Etapa != EtapaFila.Finalizado;
}

/// <summary>
/// Fila de hoje em KANBAN: Aguardando → Chegou → Em atendimento → Finalizado.
///
/// A lista simples que existia aqui antes respondia "quem está marcado"; o balcão
/// precisa de outra pergunta — "quem está esperando, e há quanto tempo". As colunas
/// saem dos carimbos de chegada e de início do atendimento (<see cref="Agendamento.Etapa"/>),
/// não de um campo de status novo: o faturamento continua vendo o mesmo
/// <see cref="StatusAgendamento"/> de sempre.
///
/// Finalizar é o antigo check-in (<see cref="AgendaService.ConfirmarPresencaAsync"/>):
/// gera o atendimento com os códigos e o retorno do 2º código. Fica no FIM do fluxo de
/// propósito — a guia nasce quando a sessão de fato aconteceu.
/// </summary>
public sealed partial class FilaViewModel : ObservableObject
{
    /// <summary>A partir daqui a espera é longa o bastante para destacar o cartão.</summary>
    private const int EsperaLongaMinutos = 30;

    private readonly AgendaService _agenda;
    private readonly PainelRecepcaoService _painel;
    private readonly ISnackbarService _snackbar;
    private readonly IDialogoService _dialogo;
    private readonly DispatcherTimer _relogio;

    private List<Agendamento> _doDia = [];

    public ObservableCollection<CartaoFila> Aguardando { get; } = [];
    public ObservableCollection<CartaoFila> NaRecepcao { get; } = [];
    public ObservableCollection<CartaoFila> EmAtendimento { get; } = [];
    public ObservableCollection<CartaoFila> Finalizados { get; } = [];

    [ObservableProperty]
    private DateTime _dia = DateTime.Today;

    [ObservableProperty]
    private bool _carregando;

    [ObservableProperty]
    private string _resumo = string.Empty;

    /// <summary>
    /// Habilita os botões de escrita da tela. É a metade VISÍVEL da permissão: o
    /// botão apagado explica por que não dá; a guarda no comando é que impede.
    /// Só desabilitar seria enfeite — um atalho de teclado passaria direto.
    /// </summary>
    public bool PodeEditarAgenda => SessaoUsuario.Atual.Pode(Permissao.EditarAgenda);

    public FilaViewModel(
        AgendaService agenda, PainelRecepcaoService painel,
        ISnackbarService snackbar, IDialogoService dialogo)
    {
        _agenda = agenda;
        _painel = painel;
        _snackbar = snackbar;
        _dialogo = dialogo;

        // Sem isto o "há 5 min" da tela envelhece e mente: quem está há 40 minutos na
        // sala de espera continuaria aparecendo como recém-chegado.
        //
        // Quem liga e desliga é a View (Loaded/Unloaded): o shell cria uma tela nova a
        // cada navegação, e um timer rodando manteria vivo cada ViewModel já trocado.
        _relogio = new DispatcherTimer { Interval = TimeSpan.FromMinutes(1) };
        _relogio.Tick += (_, _) => AtualizarEsperas();

        _ = CarregarAsync();
    }

    /// <summary>Liga o relógio da espera (chamado quando a tela entra em cena).</summary>
    public void IniciarRelogio() => _relogio.Start();

    /// <summary>Desliga o relógio (chamado quando a tela sai de cena).</summary>
    public void PararRelogio() => _relogio.Stop();

    partial void OnDiaChanged(DateTime value) => _ = CarregarAsync();

    [RelayCommand]
    public async Task CarregarAsync()
    {
        try
        {
            Carregando = true;
            _doDia = [.. await _agenda.DoDiaAsync(DateOnly.FromDateTime(Dia))];

            // Quem tem guia pendente hoje. Falha aqui não pode derrubar a fila inteira:
            // é aviso, não o conteúdo da tela.
            HashSet<int> comPendencia;
            try
            {
                var pendencias = await _painel.PendenciasDoDiaAsync(DateOnly.FromDateTime(Dia));
                comPendencia = pendencias.Select(p => p.PacienteId).ToHashSet();
            }
            catch (Exception ex)
            {
                Clinica.Application.Diagnostico.Registrar(
                    "Recepção — pendências do dia não puderam ser conferidas", ex);
                comPendencia = [];
            }

            Aguardando.Clear();
            NaRecepcao.Clear();
            EmAtendimento.Clear();
            Finalizados.Clear();

            foreach (var a in _doDia)
            {
                // Cancelado e falta saem do quadro: o kanban é o fluxo de quem vem hoje.
                if (a.Etapa == EtapaFila.ForaDaFila) continue;

                var cartao = new CartaoFila
                {
                    AgendamentoId = a.Id,
                    Horario = a.DataHora.ToString("HH:mm"),
                    Paciente = a.Paciente?.Nome ?? "(paciente removido)",
                    Modalidade = a.ModalidadePrevista.ToString(),
                    Profissional = a.Profissional?.Rotulo ?? "—",
                    Sala = a.Sala?.Nome ?? "—",
                    Etapa = a.Etapa,
                    Observacoes = a.Observacoes,
                    EhRetornoDoSegundoCodigo = a.Origem == OrigemAgendamento.RetornoSugerido,
                    EhEncaixe = a.Encaixe,
                    TemGuiaPendente = comPendencia.Contains(a.PacienteId)
                };

                Coluna(a.Etapa).Add(cartao);
            }

            AtualizarEsperas();

            var faltas = _doDia.Count(a => a.Status == StatusAgendamento.Faltou);
            var cancelados = _doDia.Count(a => a.Status == StatusAgendamento.Cancelado);
            Resumo = $"{Aguardando.Count} aguardando · {NaRecepcao.Count} na recepção · "
                   + $"{EmAtendimento.Count} em atendimento · {Finalizados.Count} finalizado(s)"
                   + $" · {faltas} falta(s) · {cancelados} cancelado(s)";
        }
        catch (Exception ex)
        {
            Clinica.Application.Diagnostico.Registrar("Recepção — fila do dia não pôde ser carregada", ex);
            _snackbar.Erro($"Não foi possível carregar a fila: {ex.Message}");
        }
        finally
        {
            Carregando = false;
        }
    }

    private ObservableCollection<CartaoFila> Coluna(EtapaFila etapa) => etapa switch
    {
        EtapaFila.Chegou => NaRecepcao,
        EtapaFila.EmAtendimento => EmAtendimento,
        EtapaFila.Finalizado => Finalizados,
        _ => Aguardando
    };

    /// <summary>Recalcula os rótulos de espera sem ir ao banco (chamado a cada minuto).</summary>
    private void AtualizarEsperas()
    {
        var agora = DateTime.Now;
        var porId = _doDia.ToDictionary(a => a.Id);

        foreach (var cartao in Aguardando.Concat(NaRecepcao).Concat(EmAtendimento).Concat(Finalizados))
        {
            if (!porId.TryGetValue(cartao.AgendamentoId, out var ag)) continue;

            var minutos = ag.EsperaMinutos(agora);
            cartao.Espera = minutos is null ? string.Empty : $"{minutos} min";
            // Espera só "corre" enquanto o paciente ainda não foi chamado.
            cartao.EsperaLonga = minutos >= EsperaLongaMinutos
                                 && ag.InicioAtendimentoEm is null;
        }
    }

    /// <summary>Check-in no balcão: o paciente chegou e o cronômetro da espera começa.</summary>
    [RelayCommand]
    private async Task RegistrarChegadaAsync(CartaoFila? cartao)
        => await ExecutarAsync(cartao, async c =>
        {
            SessaoUsuario.Atual.Exigir(Permissao.EditarAgenda, "mexer na fila do dia");

            await _agenda.RegistrarChegadaAsync(c.AgendamentoId);
            if (c.TemGuiaPendente)
                _dialogo.Aviso(
                    "Guia pendente",
                    $"{c.Paciente} tem guia pendente de baixa. Aproveite que ele está no "
                    + "balcão para pedir o documento — depois a cobrança vira telefonema.");
            else
                _snackbar.Sucesso($"{c.Paciente} chegou.");
        }, "chegada");

    /// <summary>O profissional chamou: fim da espera, começo da sessão.</summary>
    [RelayCommand]
    private async Task IniciarAtendimentoAsync(CartaoFila? cartao)
        => await ExecutarAsync(cartao, async c =>
        {
            SessaoUsuario.Atual.Exigir(Permissao.EditarAgenda, "mexer na fila do dia");

            await _agenda.IniciarAtendimentoAsync(c.AgendamentoId);
            _snackbar.Sucesso($"{c.Paciente} em atendimento.");
        }, "início do atendimento");

    /// <summary>Encerra a sessão: gera o atendimento com os códigos e o retorno do 2º código.</summary>
    [RelayCommand]
    private async Task FinalizarAsync(CartaoFila? cartao)
        => await ExecutarAsync(cartao, async c =>
        {
            SessaoUsuario.Atual.Exigir(Permissao.EditarAgenda, "mexer na fila do dia");

            var resultado = await _agenda.ConfirmarPresencaAsync(c.AgendamentoId);
            var codigos = resultado.Atendimento.Codigos.Count;
            _snackbar.Sucesso($"Atendimento de {c.Paciente} concluído — {codigos} código(s) gerado(s).");
        }, "conclusão do atendimento");

    /// <summary>Volta o cartão uma coluna — clicar errado no kanban é rotina.</summary>
    [RelayCommand]
    private async Task VoltarEtapaAsync(CartaoFila? cartao)
        => await ExecutarAsync(cartao, async c =>
        {
            SessaoUsuario.Atual.Exigir(Permissao.EditarAgenda, "mexer na fila do dia");

            await _agenda.VoltarEtapaAsync(c.AgendamentoId);
            _snackbar.Info("Cartão devolvido para a coluna anterior.");
        }, "volta de etapa");

    [RelayCommand]
    private async Task MarcarFaltaAsync(CartaoFila? cartao)
        => await ExecutarAsync(cartao, async c =>
        {
            SessaoUsuario.Atual.Exigir(Permissao.EditarAgenda, "mexer na fila do dia");

            await _agenda.MarcarFaltaAsync(c.AgendamentoId, SessaoUsuario.Atual.Operador);
            _snackbar.Info($"{c.Paciente} marcado como falta.");
        }, "marcação de falta");

    [RelayCommand]
    private async Task CancelarAsync(CartaoFila? cartao)
        => await ExecutarAsync(cartao, async c =>
        {
            SessaoUsuario.Atual.Exigir(Permissao.EditarAgenda, "mexer na fila do dia");

            await _agenda.CancelarAsync(c.AgendamentoId, SessaoUsuario.Atual.Operador);
            _snackbar.Info($"Agendamento de {c.Paciente} cancelado.");
        }, "cancelamento");

    /// <summary>
    /// Envelope comum dos comandos do quadro: executa, registra a falha no log e
    /// recarrega. A tela nunca cai por causa de um clique.
    /// </summary>
    private async Task ExecutarAsync(CartaoFila? cartao, Func<CartaoFila, Task> acao, string contexto)
    {
        if (cartao is null) return;
        try
        {
            await acao(cartao);
            await CarregarAsync();
        }
        catch (Exception ex)
        {
            Clinica.Application.Diagnostico.Registrar($"Recepção — falha na {contexto}", ex);
            _snackbar.Erro(ex.Message);
        }
    }

    [RelayCommand]
    private void DiaAnterior() => Dia = Dia.AddDays(-1);

    [RelayCommand]
    private void ProximoDia() => Dia = Dia.AddDays(1);

    [RelayCommand]
    private void Hoje() => Dia = DateTime.Today;
}
