using System.Collections.ObjectModel;
using System.Windows.Threading;
using Clinica.Application.Servicos;
using Clinica.Desktop.Controls;
using Clinica.Desktop.Shell;
using Clinica.Domain.Entities;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;

namespace Clinica.Recepcao.ViewModels;

/// <summary>Uma opção do filtro por profissional (o primeiro item é "todos").</summary>
public sealed record FiltroProfissionalFila(int? Id, string Nome);

/// <summary>Um cartão da fila (agendamento + o que a recepção precisa ver de relance).</summary>
public sealed partial class CartaoFila : ObservableObject
{
    public required int AgendamentoId { get; init; }

    /// <summary>De quem é a sessão — é o que a conferência de elegibilidade pergunta.</summary>
    public required int PacienteId { get; init; }

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

    /// <summary>
    /// A situação do cartão em UMA linha, escrita para a coluna em que ele está: quem
    /// aguarda vê atraso, quem está na recepção vê espera, quem está na sala vê há
    /// quanto tempo entrou, e quem terminou vê quanto esperou.
    ///
    /// Vazia quando não há nada a dizer — cartão sem problema não precisa de rótulo.
    /// </summary>
    [ObservableProperty]
    private string _situacao = string.Empty;

    /// <summary>A situação merece destaque (atraso do paciente, espera longa, sala estourando).</summary>
    [ObservableProperty]
    private bool _situacaoCritica;

    /// <summary>
    /// O que a conferência de elegibilidade disse no check-in deste paciente.
    ///
    /// Fica GUARDADO enquanto a tela está aberta: o aviso era exibido num diálogo e
    /// morria no OK, e a carteirinha vencida que a recepção acabou de ler sumia antes de
    /// ela ter resolvido — inclusive do cartão que ela ia olhar de novo dali a pouco.
    /// </summary>
    [ObservableProperty]
    private string _avisos = string.Empty;

    public bool TemAvisos => !string.IsNullOrWhiteSpace(Avisos);

    partial void OnAvisosChanged(string value) => OnPropertyChanged(nameof(TemAvisos));

    /// <summary>Faltou ou foi cancelado: está no rodapé do quadro, não numa coluna.</summary>
    public bool EstaForaDaFila => Etapa == EtapaFila.ForaDaFila;

    /// <summary>"Faltou" ou "Cancelado" — por que este horário saiu do fluxo do dia.</summary>
    public required string MotivoForaDaFila { get; init; }

    public bool PodeChegar => Etapa == EtapaFila.Aguardando;
    public bool PodeIniciar => Etapa is EtapaFila.Aguardando or EtapaFila.Chegou;
    public bool PodeFinalizar => Etapa == EtapaFila.EmAtendimento;
    public bool PodeVoltar => Etapa is EtapaFila.Chegou or EtapaFila.EmAtendimento;

    /// <summary>Falta e cancelamento voltam para a agenda — é o desfazer do clique errado.</summary>
    public bool PodeReabrir => Etapa == EtapaFila.ForaDaFila;

    /// <summary>
    /// Só horário no fluxo do dia aceita falta/cancelamento. Finalizado já virou guia, e
    /// o que já saiu da fila não sai duas vezes.
    /// </summary>
    public bool EmAberto
        => Etapa is EtapaFila.Aguardando or EtapaFila.Chegou or EtapaFila.EmAtendimento;
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
    /// <summary>A partir daqui a espera É DA CLÍNICA e longa o bastante para destacar.</summary>
    private const int EsperaLongaMinutos = 30;

    /// <summary>
    /// A partir daqui o atraso do paciente deixa de ser trânsito e vira telefonema.
    ///
    /// Cinco minutos alarmariam o dia inteiro — e alerta que dispara para todo mundo é
    /// alerta que ninguém lê, a mesma razão do atraso mínimo da cobrança.
    /// </summary>
    private const int AtrasoParaAvisarMinutos = 15;

    /// <summary>
    /// De quantos em quantos minutos o quadro volta ao banco por conta própria.
    ///
    /// O relógio já existia, mas só envelhecia os rótulos em memória: o balcão e a sala
    /// olham a MESMA fila em máquinas diferentes, e a chegada registrada lá na frente só
    /// aparecia aqui quando alguém lembrava de clicar em Atualizar. Quadro de parede que
    /// mostra o dia de dez minutos atrás é pior que quadro nenhum, porque parece atual.
    /// </summary>
    private const int RecarregarACadaMinutos = 2;

    private readonly AgendaService _agenda;
    private readonly PainelRecepcaoService _painel;

    /// <summary>Escopo próprio para a janela de fechamento, como nos demais formulários.</summary>
    private readonly IServiceScopeFactory _escopos;

    private readonly ISnackbarService _snackbar;
    private readonly IDialogoService _dialogo;
    private readonly DispatcherTimer _relogio;

    private List<Agendamento> _doDia = [];

    /// <summary>
    /// O que a elegibilidade disse no check-in, por agendamento. Vive enquanto a tela
    /// vive: é lembrança da conferência, não fato gravado — quem grava carteirinha e cota
    /// é o cadastro, e reescrevê-los daqui inventaria dado a partir de um aviso.
    /// </summary>
    private readonly Dictionary<int, string> _avisosDoCheckIn = [];

    /// <summary>Pacientes do dia com guia pendente de baixa (recarregado junto com a fila).</summary>
    private HashSet<int> _comGuiaPendente = [];

    /// <summary>Há um comando do quadro em curso — a recarga automática espera a vez.</summary>
    private bool _operando;

    private int _minutosDesdeARecarga;

    public ObservableCollection<CartaoFila> Aguardando { get; } = [];
    public ObservableCollection<CartaoFila> NaRecepcao { get; } = [];
    public ObservableCollection<CartaoFila> EmAtendimento { get; } = [];
    public ObservableCollection<CartaoFila> Finalizados { get; } = [];

    /// <summary>
    /// Faltas e cancelamentos do dia. Não são coluna do kanban — o quadro é o fluxo de
    /// quem vem —, mas também não podem sumir: é aqui que se desfaz o clique errado, e
    /// era a única ação do balcão que não tinha volta.
    /// </summary>
    public ObservableCollection<CartaoFila> ForaDaFila { get; } = [];

    /// <summary>O filtro por profissional, com "Todos" na frente.</summary>
    public ObservableCollection<FiltroProfissionalFila> Profissionais { get; } = [];

    /// <summary>
    /// O quadro só está vazio quando as QUATRO colunas estão — um paciente já finalizado
    /// é dia com movimento, não fila vazia.
    /// </summary>
    public bool QuadroVazio => Aguardando.Count == 0 && NaRecepcao.Count == 0
                               && EmAtendimento.Count == 0 && Finalizados.Count == 0;

    [ObservableProperty]
    private DateTime _dia = DateTime.Today;

    [ObservableProperty]
    private bool _carregando;

    [ObservableProperty]
    private string _resumo = string.Empty;

    /// <summary>
    /// Busca por nome dentro do dia. Trinta pacientes em quatro colunas é rolagem nas
    /// quatro, e a pergunta do balcão é sempre sobre UM paciente — o que está na frente.
    /// </summary>
    [ObservableProperty]
    private string _busca = string.Empty;

    [ObservableProperty]
    private FiltroProfissionalFila? _profissionalFiltro;

    /// <summary>
    /// Há filtro ativo. A tela DIZ isso: quadro vazio por filtro e quadro vazio por dia
    /// sem ninguém são respostas diferentes, e trocá-las faz a recepção concluir que o
    /// dia está livre.
    /// </summary>
    public bool Filtrando
        => !string.IsNullOrWhiteSpace(Busca) || ProfissionalFiltro?.Id is not null;

    public string TituloVazio => Filtrando
        ? "Nada bate com o filtro"
        : "Ninguém marcado para hoje";

    public string DescricaoVazia => Filtrando
        ? "O dia pode ter movimento fora do que você está filtrando — limpe o filtro para ver o quadro inteiro."
        : "Cancelados e faltas não entram nas colunas: o quadro mostra o fluxo de quem vem hoje.";

    /// <summary>
    /// Habilita os botões de escrita da tela. É a metade VISÍVEL da permissão: o
    /// botão apagado explica por que não dá; a guarda no comando é que impede.
    /// Só desabilitar seria enfeite — um atalho de teclado passaria direto.
    /// </summary>
    public bool PodeEditarAgenda => SessaoUsuario.Atual.Pode(Permissao.EditarAgenda);

    public FilaViewModel(
        AgendaService agenda, PainelRecepcaoService painel, IServiceScopeFactory escopos,
        ISnackbarService snackbar, IDialogoService dialogo)
    {
        _agenda = agenda;
        _painel = painel;
        _escopos = escopos;
        _snackbar = snackbar;
        _dialogo = dialogo;

        // Sem isto o "há 5 min" da tela envelhece e mente: quem está há 40 minutos na
        // sala de espera continuaria aparecendo como recém-chegado.
        //
        // Quem liga e desliga é a View (Loaded/Unloaded): o shell cria uma tela nova a
        // cada navegação, e um timer rodando manteria vivo cada ViewModel já trocado.
        _relogio = new DispatcherTimer { Interval = TimeSpan.FromMinutes(1) };
        _relogio.Tick += (_, _) => AoPassarUmMinuto();

        _ = CarregarAsync();
    }

    /// <summary>Liga o relógio da espera (chamado quando a tela entra em cena).</summary>
    public void IniciarRelogio() => _relogio.Start();

    /// <summary>Desliga o relógio (chamado quando a tela sai de cena).</summary>
    public void PararRelogio() => _relogio.Stop();

    /// <summary>
    /// Passou um minuto: os rótulos envelhecem sempre, e de tempos em tempos o quadro
    /// volta ao banco.
    ///
    /// A recarga só acontece no dia de HOJE (a fila de terça passada não muda sozinha),
    /// e nunca por cima de um comando em curso — recarregar com a janela de fechamento
    /// aberta trocaria os cartões debaixo de quem está no meio de uma decisão.
    /// </summary>
    private void AoPassarUmMinuto()
    {
        AtualizarEsperas();

        if (Dia.Date != DateTime.Today || Carregando || _operando) return;
        if (++_minutosDesdeARecarga < RecarregarACadaMinutos) return;

        _minutosDesdeARecarga = 0;
        _ = CarregarAsync();
    }

    partial void OnDiaChanged(DateTime value) => _ = CarregarAsync();

    partial void OnBuscaChanged(string value) => Refiltrar();

    partial void OnProfissionalFiltroChanged(FiltroProfissionalFila? value) => Refiltrar();

    /// <summary>
    /// O filtro trabalha sobre o que JÁ está em memória — não volta ao banco. É a mesma
    /// consulta do dia recortada, e uma ida ao banco por tecla digitada tornaria a busca
    /// mais lenta que rolar as quatro colunas.
    /// </summary>
    private void Refiltrar()
    {
        OnPropertyChanged(nameof(Filtrando));
        OnPropertyChanged(nameof(TituloVazio));
        OnPropertyChanged(nameof(DescricaoVazia));
        MontarQuadro();
    }

    [RelayCommand]
    public async Task CarregarAsync()
    {
        try
        {
            Carregando = true;
            _minutosDesdeARecarga = 0;
            _doDia = [.. await _agenda.DoDiaAsync(DateOnly.FromDateTime(Dia))];

            await CarregarProfissionaisAsync();

            // Quem tem guia pendente hoje. Falha aqui não pode derrubar a fila inteira:
            // é aviso, não o conteúdo da tela.
            try
            {
                var pendencias = await _painel.PendenciasDoDiaAsync(DateOnly.FromDateTime(Dia));
                _comGuiaPendente = pendencias.Select(p => p.PacienteId).ToHashSet();
            }
            catch (Exception ex)
            {
                Clinica.Application.Diagnostico.Registrar(
                    "Recepção — pendências do dia não puderam ser conferidas", ex);
                _comGuiaPendente = [];
            }

            MontarQuadro();
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

    /// <summary>
    /// A lista do filtro. Vem da equipe ATIVA, e não dos profissionais que aparecem no
    /// dia: filtro que muda de opções conforme o dia faz o usuário achar que o colega
    /// foi desligado quando ele só está de folga.
    /// </summary>
    private async Task CarregarProfissionaisAsync()
    {
        if (Profissionais.Count > 0) return;

        try
        {
            using var scope = _escopos.CreateScope();
            var equipe = scope.ServiceProvider.GetRequiredService<EquipeService>();

            var selecionado = ProfissionalFiltro?.Id;

            Profissionais.Add(new FiltroProfissionalFila(null, "Todos os profissionais"));
            foreach (var p in await equipe.ProfissionaisAtivosAsync())
                Profissionais.Add(new FiltroProfissionalFila(p.Id, p.Rotulo));

            ProfissionalFiltro = Profissionais.FirstOrDefault(p => p.Id == selecionado)
                                 ?? Profissionais[0];
        }
        catch (Exception ex)
        {
            // Sem a lista, o filtro fica só com "todos" — a fila continua sendo a fila.
            Clinica.Application.Diagnostico.Registrar(
                "Recepção — equipe não pôde ser carregada para o filtro da fila", ex);
        }
    }

    /// <summary>
    /// Distribui o que está em memória pelas colunas, aplicando o filtro e a ORDEM de
    /// cada uma. Não vai ao banco: é chamada tanto pela carga quanto por cada tecla da
    /// busca.
    /// </summary>
    private void MontarQuadro()
    {
        Aguardando.Clear();
        NaRecepcao.Clear();
        EmAtendimento.Clear();
        Finalizados.Clear();
        ForaDaFila.Clear();

        var termo = Busca?.Trim() ?? string.Empty;
        var profissional = ProfissionalFiltro?.Id;

        var visiveis = _doDia
            .Where(a => profissional is null || a.ProfissionalId == profissional)
            .Where(a => termo.Length == 0
                        || (a.Paciente?.Nome ?? string.Empty)
                            .Contains(termo, StringComparison.OrdinalIgnoreCase))
            .ToList();

        foreach (var a in Ordenar(visiveis))
            Coluna(a.Etapa).Add(MontarCartao(a));

        AtualizarEsperas();
        // As coleções mudaram: o quadro precisa reavaliar se está vazio.
        OnPropertyChanged(nameof(QuadroVazio));

        var faltas = visiveis.Count(a => a.Status == StatusAgendamento.Faltou);
        var cancelados = visiveis.Count(a => a.Status == StatusAgendamento.Cancelado);
        Resumo = $"{Aguardando.Count} aguardando · {NaRecepcao.Count} na recepção · "
               + $"{EmAtendimento.Count} em atendimento · {Finalizados.Count} finalizado(s)"
               + $" · {faltas} falta(s) · {cancelados} cancelado(s)"
               + (Filtrando ? " — filtrado" : string.Empty);
    }

    /// <summary>
    /// A ordem de cada coluna é a pergunta dela.
    ///
    /// "Aguardando" e "Finalizado" são cronologia do dia — hora marcada. Mas "Na
    /// recepção" é a ORDEM DE CHAMADA: quem espera há mais tempo vem primeiro, e isso
    /// não é a hora marcada (o das 9h que chegou atrasado esperou menos que o das 9h30
    /// que chegou na hora). Ordenar a sala de espera por hora marcada faz o balcão
    /// chamar na ordem errada com o quadro parecendo certo.
    /// </summary>
    private static IEnumerable<Agendamento> Ordenar(IEnumerable<Agendamento> agendamentos)
        => agendamentos
            .OrderBy(a => a.Etapa switch
            {
                EtapaFila.Chegou => a.InicioDaEsperaDevida ?? a.DataHora,
                EtapaFila.EmAtendimento => a.InicioAtendimentoEm ?? a.DataHora,
                _ => a.DataHora
            })
            .ThenBy(a => a.DataHora);

    private CartaoFila MontarCartao(Agendamento a) => new()
    {
        AgendamentoId = a.Id,
        PacienteId = a.PacienteId,
        Horario = a.DataHora.ToString("HH:mm"),
        Paciente = a.Paciente?.Nome ?? "(paciente removido)",
        Modalidade = a.ModalidadePrevista.ToString(),
        Profissional = a.Profissional?.Rotulo ?? "—",
        Sala = a.Sala?.Nome ?? "—",
        Etapa = a.Etapa,
        Observacoes = a.Observacoes,
        MotivoForaDaFila = a.Status switch
        {
            StatusAgendamento.Faltou => "Faltou",
            StatusAgendamento.Cancelado => "Cancelado",
            _ => string.Empty
        },
        EhRetornoDoSegundoCodigo = a.Origem == OrigemAgendamento.RetornoSugerido,
        EhEncaixe = a.Encaixe,
        TemGuiaPendente = _comGuiaPendente.Contains(a.PacienteId),
        Avisos = _avisosDoCheckIn.GetValueOrDefault(a.Id, string.Empty)
    };

    private ObservableCollection<CartaoFila> Coluna(EtapaFila etapa) => etapa switch
    {
        EtapaFila.Chegou => NaRecepcao,
        EtapaFila.EmAtendimento => EmAtendimento,
        EtapaFila.Finalizado => Finalizados,
        EtapaFila.ForaDaFila => ForaDaFila,
        _ => Aguardando
    };

    /// <summary>
    /// Recalcula os rótulos de tempo sem ir ao banco (chamado a cada minuto).
    ///
    /// Cada coluna recebe a frase da SUA pergunta. Antes havia uma só — "12 min" —, que
    /// na coluna "Aguardando" não aparecia nunca (quem não chegou não tem espera) e nas
    /// outras contava desde a chegada, marcando de vermelho quem chegou uma hora antes.
    /// </summary>
    private void AtualizarEsperas()
    {
        var agora = DateTime.Now;
        var porId = _doDia.ToDictionary(a => a.Id);

        foreach (var cartao in Aguardando.Concat(NaRecepcao).Concat(EmAtendimento)
                     .Concat(Finalizados).Concat(ForaDaFila))
        {
            if (!porId.TryGetValue(cartao.AgendamentoId, out var ag)) continue;

            (cartao.Situacao, cartao.SituacaoCritica) = Descrever(ag, agora);
        }
    }

    private static (string Texto, bool Critica) Descrever(Agendamento ag, DateTime agora)
    {
        switch (ag.Etapa)
        {
            case EtapaFila.Aguardando:
                // A hora passou e ninguém apareceu: é aqui que o telefonema ainda salva
                // a sessão. Antes disso, cartão sem rótulo — não há o que dizer.
                if (ag.AtrasoDoPacienteMinutos(agora) is not { } atraso) return (string.Empty, false);
                return ($"atrasado {atraso} min", atraso >= AtrasoParaAvisarMinutos);

            case EtapaFila.Chegou:
                var espera = ag.EsperaDaClinicaMinutos(agora) ?? 0;
                // Chegou antes da hora e a hora ainda não deu: ele está esperando por
                // escolha dele, e vermelho aqui seria alarme falso todo dia.
                if (espera == 0 && ag.ChegouAdiantado)
                    return ($"chegou {ag.ChegadaEm:HH:mm} · antes da hora", false);
                return ($"esperando há {espera} min", espera >= EsperaLongaMinutos);

            case EtapaFila.EmAtendimento:
                var emSala = ag.DuracaoAtendimentoMinutos(agora) ?? 0;
                var estourou = ag.SessaoPassouDoPrevisto(agora);
                // A sala que estoura a duração atrasa a próxima sessão — e é a recepção
                // quem vai explicar isso a quem está sentado lá fora.
                return (estourou
                    ? $"na sala há {emSala} min · passou de {ag.DuracaoEfetiva} min"
                    : $"na sala há {emSala} min", estourou);

            case EtapaFila.Finalizado:
                var esperou = ag.EsperaDaClinicaMinutos(agora);
                return (esperou is null ? string.Empty : $"esperou {esperou} min", false);

            default:
                return (string.Empty, false);
        }
    }

    /// <summary>Check-in no balcão: o paciente chegou e o cronômetro da espera começa.</summary>
    [RelayCommand]
    private async Task RegistrarChegadaAsync(CartaoFila? cartao)
        => await ExecutarAsync(cartao, async c =>
        {
            SessaoUsuario.Atual.Exigir(Permissao.EditarAgenda, "mexer na fila do dia");

            await _agenda.RegistrarChegadaAsync(c.AgendamentoId);

            // O check-in é o ÚLTIMO momento barato: o paciente está no balcão, e
            // carteirinha vencida ou cota estourada ainda dá para resolver com um
            // telefonema. Depois da sessão, a mesma informação só vira glosa.
            var elegibilidade = await ConferirElegibilidadeAsync(c);

            var recados = new List<string>();
            if (c.TemGuiaPendente)
                recados.Add("Tem GUIA PENDENTE de baixa — aproveite que ele está aqui e peça "
                            + "o documento; depois a cobrança vira telefonema.");
            recados.AddRange(elegibilidade);

            // O que a conferência disse fica no cartão até a tela ser fechada: lido no
            // diálogo e esquecido no OK, o aviso não sobrevivia ao próximo paciente.
            _avisosDoCheckIn[c.AgendamentoId] = string.Join("\n\n", recados);

            if (recados.Count > 0)
                _dialogo.Aviso($"Atenção — {c.Paciente}", string.Join("\n\n", recados));
            else
                _snackbar.Sucesso($"{c.Paciente} chegou.");
        }, "chegada");

    /// <summary>
    /// Conferência de elegibilidade do paciente que acabou de chegar.
    ///
    /// É chamada UMA vez, no check-in, e não para os trinta cartões do dia: a conferência
    /// custa quatro consultas por paciente, e rodá-la a cada abertura da fila tornaria a
    /// tela lenta para entregar um aviso que só importa quando alguém chega.
    ///
    /// Falha aqui NÃO derruba a chegada: o paciente chegou, e isso já está gravado. O que
    /// se perde é o aviso — e o aviso perdido fica no log, nunca disfarçado de "tudo certo".
    /// </summary>
    private async Task<IReadOnlyList<string>> ConferirElegibilidadeAsync(CartaoFila cartao)
    {
        try
        {
            using var scope = _escopos.CreateScope();
            var elegibilidade = scope.ServiceProvider.GetRequiredService<ElegibilidadeService>();

            var resultado = await elegibilidade.ConferirAsync(
                cartao.PacienteId, DateOnly.FromDateTime(Dia));

            return resultado.Alertas.Select(a => a.Descricao).ToList();
        }
        catch (Exception ex)
        {
            Clinica.Application.Diagnostico.Registrar(
                "Recepção — elegibilidade não pôde ser conferida no check-in", ex);
            return ["Não foi possível conferir carteirinha e cota agora — confira na ficha."];
        }
    }

    /// <summary>O profissional chamou: fim da espera, começo da sessão.</summary>
    [RelayCommand]
    private async Task IniciarAtendimentoAsync(CartaoFila? cartao)
        => await ExecutarAsync(cartao, async c =>
        {
            SessaoUsuario.Atual.Exigir(Permissao.EditarAgenda, "mexer na fila do dia");

            await _agenda.IniciarAtendimentoAsync(c.AgendamentoId);
            _snackbar.Sucesso($"{c.Paciente} em atendimento.");
        }, "início do atendimento");

    /// <summary>
    /// Encerra a sessão. Abre a janela de fechamento em vez de concluir direto: a
    /// conclusão são QUATRO fatos do mesmo ato — a guia nasce, o pacote debita, o insumo
    /// sai do estoque e o dinheiro entra no caixa —, e por muito tempo só o primeiro
    /// acontecia. Ver <see cref="FechamentoSessaoService"/>.
    ///
    /// A janela é PROPOSTA confirmada, não automação: o balcão vê o que vai acontecer e
    /// corrige antes. Cancelar lá não conclui nada — o cartão fica onde estava.
    /// </summary>
    [RelayCommand]
    private async Task FinalizarAsync(CartaoFila? cartao)
        => await ExecutarAsync(cartao, c =>
        {
            SessaoUsuario.Atual.Exigir(Permissao.EditarAgenda, "mexer na fila do dia");

            var vm = new FechamentoSessaoViewModel(_escopos, c.AgendamentoId);
            var janela = new Janelas.FechamentoSessaoWindow(vm)
            {
                Owner = System.Windows.Application.Current?.MainWindow
            };

            // Modal: o await fica com a janela, e o recarregar da fila vem do
            // ExecutarAsync assim que ela fecha — inclusive quando fecha com aviso.
            if (janela.ShowDialog() != true || janela.Resultado is not { } resultado)
                return Task.CompletedTask;

            var partes = new List<string> { $"{resultado.Atendimento.Codigos.Count} código(s)" };
            if (resultado.Consumo is not null) partes.Add("1 sessão do pacote");
            if (resultado.Movimentos.Count > 0) partes.Add($"{resultado.Movimentos.Count} insumo(s)");
            if (resultado.Lancamento is not null) partes.Add("entrada no caixa");

            _snackbar.Sucesso($"Sessão de {c.Paciente} concluída — {string.Join(" · ", partes)}.");
            return Task.CompletedTask;
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
    /// Devolve à fila o que foi marcado como falta ou cancelado por engano.
    ///
    /// Sem isto, o desfazer do balcão era remarcar o horário — o que grava uma remarcação
    /// que nunca houve — ou conviver com uma falta falsa, que conta contra o paciente na
    /// hora de decidir quem fica com o horário mais disputado da semana.
    /// </summary>
    [RelayCommand]
    private async Task ReabrirAsync(CartaoFila? cartao)
        => await ExecutarAsync(cartao, async c =>
        {
            SessaoUsuario.Atual.Exigir(Permissao.EditarAgenda, "mexer na fila do dia");

            await _agenda.ReabrirAsync(c.AgendamentoId, SessaoUsuario.Atual.Operador);
            _snackbar.Sucesso($"Horário de {c.Paciente} de volta à fila.");
        }, "reabertura do horário");

    /// <summary>Limpa busca e filtro de profissional de uma vez.</summary>
    [RelayCommand]
    private void LimparFiltro()
    {
        Busca = string.Empty;
        if (Profissionais.Count > 0) ProfissionalFiltro = Profissionais[0];
    }

    /// <summary>
    /// Envelope comum dos comandos do quadro: executa, registra a falha no log e
    /// recarrega. A tela nunca cai por causa de um clique.
    /// </summary>
    private async Task ExecutarAsync(CartaoFila? cartao, Func<CartaoFila, Task> acao, string contexto)
    {
        if (cartao is null) return;
        try
        {
            _operando = true;
            await acao(cartao);
            await CarregarAsync();
        }
        catch (Exception ex)
        {
            Clinica.Application.Diagnostico.Registrar($"Recepção — falha na {contexto}", ex);
            _snackbar.Erro(ex.Message);
        }
        finally
        {
            _operando = false;
        }
    }

    [RelayCommand]
    private void DiaAnterior() => Dia = Dia.AddDays(-1);

    [RelayCommand]
    private void ProximoDia() => Dia = Dia.AddDays(1);

    [RelayCommand]
    private void Hoje() => Dia = DateTime.Today;
}
