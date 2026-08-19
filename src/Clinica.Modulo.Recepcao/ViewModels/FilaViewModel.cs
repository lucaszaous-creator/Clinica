using System.Collections.ObjectModel;
using System.Windows.Threading;
using Clinica.Application.Servicos;
using Clinica.Desktop.Controls;
using Clinica.Desktop.Shell;
using Clinica.Desktop.Shell.Componentes;
using Clinica.Domain.Entities;
using Clinica.Domain.Regras;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;

namespace Clinica.Recepcao.ViewModels;

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
    /// O procedimento de hoje exige termo assinado pelo paciente e ele ainda não assinou
    /// (parcela 66) — o caso é o BSV, com a declaração de jejum.
    ///
    /// É calculado na CARGA da fila, e não só no check-in como a elegibilidade completa: a
    /// conferência do termo é UMA consulta e responde à pergunta que decide se o cartão
    /// pode andar. Descobrir na maca que falta assinar significa parar o procedimento.
    /// </summary>
    [ObservableProperty]
    private bool _temTermoPendente;

    /// <summary>Tempo de espera formatado ("12 min"). Vazio antes do check-in.</summary>
    [ObservableProperty]
    private string _espera = string.Empty;

    /// <summary>Espera longa (30 min ou mais) — o cartão fica em destaque.</summary>
    [ObservableProperty]
    private bool _esperaLonga;

    /// <summary>
    /// Há quanto tempo o profissional pediu este paciente ("chamado há 4 min"). Vazio
    /// para quem não foi chamado, e para de correr assim que a pessoa entra na sala.
    /// </summary>
    [ObservableProperty]
    private string _chamadoHa = string.Empty;

    /// <summary>
    /// A chamada está pendente há tempo demais: ou o paciente não ouviu, ou saiu — e
    /// quem está parado esperando é o profissional. O cartão passa a gritar.
    /// </summary>
    [ObservableProperty]
    private bool _chamadaDemorada;

    public bool PodeChegar => Etapa == EtapaFila.Aguardando;

    /// <summary>
    /// O balcão também pode chamar por conta própria (o profissional avisou pela porta,
    /// a sala vagou): é o mesmo fato, e quem carimba é quem clicar primeiro.
    /// </summary>
    public bool PodeChamar => Etapa == EtapaFila.Chegou;

    /// <summary>"Entrou" — o paciente levantou e foi para a sala.</summary>
    public bool PodeIniciar => Etapa is EtapaFila.Aguardando or EtapaFila.Chegou or EtapaFila.Chamado;

    public bool PodeFinalizar => Etapa == EtapaFila.EmAtendimento;

    public bool PodeVoltar => Etapa is EtapaFila.Chegou or EtapaFila.Chamado or EtapaFila.EmAtendimento;

    /// <summary>Só horário em aberto aceita falta/cancelamento.</summary>
    public bool EmAberto => Etapa != EtapaFila.Finalizado;

    /// <summary>
    /// O rótulo do PRÓXIMO passo — um só por cartão.
    ///
    /// Os quatro botões de avanço eram excludentes entre si menos num ponto ("Entrou"
    /// aparecia junto de "Chegou"), e somados aos três de exceção davam até cinco botões
    /// por cartão em cinco colunas. O olho para de distinguir o frequente do raro quando
    /// tudo tem o mesmo peso; aqui o passo seguinte é o único botão sólido, e o resto
    /// mora no "⋯".
    /// </summary>
    public string ProximoPasso => Etapa switch
    {
        EtapaFila.Aguardando => "Chegou",
        EtapaFila.Chegou => "Chamar",
        EtapaFila.Chamado => "Entrou",
        EtapaFila.EmAtendimento => "Concluir",
        _ => string.Empty
    };

    public bool TemProximoPasso => ProximoPasso.Length > 0;

    /// <summary>
    /// O próximo passo deste cartão é CONCLUIR — o único que pede <c>EditarAgenda</c>
    /// estrito. Os outros três (chegou, chamar, entrou) são movimento de fila.
    /// </summary>
    public bool ProximoPassoEhConcluir => Etapa == EtapaFila.EmAtendimento;

    /// <summary>
    /// Quem LANÇOU o horário, e quando (parcela 58). Vai na dica do cartão: o quadro é
    /// denso de propósito, e uma linha a mais por cartão custaria a densidade que faz o
    /// dia caber na tela.
    /// </summary>
    public required string Lancamento { get; init; }

    /// <summary>A dica do cartão: o que não coube nele e alguém pode precisar.</summary>
    public string Detalhe => string.Join("\n", new[]
    {
        $"{Horario} · {Modalidade}",
        $"{Profissional} · sala {Sala}",
        string.IsNullOrWhiteSpace(Observacoes) ? null : $"Obs.: {Observacoes}",
        Lancamento
    }.Where(l => l is not null));
}

/// <summary>
/// Fila de hoje em KANBAN: Aguardando → Chegou → Chamado → Em atendimento → Finalizado.
///
/// A lista simples que existia aqui antes respondia "quem está marcado"; o balcão
/// precisa de outra pergunta — "quem está esperando, e há quanto tempo". As colunas
/// saem dos carimbos de chegada, de chamada e de início do atendimento
/// (<see cref="Agendamento.Etapa"/>), não de um campo de status novo: o faturamento
/// continua vendo o mesmo <see cref="StatusAgendamento"/> de sempre.
///
/// A coluna CHAMADO é o recado do consultório (parcela 38). Quem atende está na sala
/// com a porta fechada e não grita o nome de ninguém: ele clica em "Chamar próximo" no
/// app dele, e é ESTA tela que anuncia a pessoa. Não há sincronização entre os dois
/// módulos — nem fila de mensagens, nem evento: eles leem a mesma linha do banco, e é
/// isso que faz os dois quadros nunca divergirem.
///
/// Finalizar é o antigo check-in (<see cref="AgendaService.ConfirmarPresencaAsync"/>):
/// gera o atendimento com os códigos e o retorno do 2º código. Fica no FIM do fluxo de
/// propósito — a guia nasce quando a sessão de fato aconteceu.
/// </summary>
public sealed partial class FilaViewModel : ObservableObject
{
    /// <summary>A partir daqui a espera é longa o bastante para destacar o cartão.</summary>
    private const int EsperaLongaMinutos = 30;

    /// <summary>
    /// A partir daqui a chamada está demorando. Três minutos é o tempo de alguém se
    /// levantar e andar até a sala — o mesmo corte do consultório, e de propósito: os
    /// dois lados olhando o mesmo relógio com números diferentes fariam o balcão dizer
    /// "acabou de ser chamado" enquanto o médico já está reclamando.
    /// </summary>
    private const int ChamadaDemoradaMinutos = 3;


    /// <summary>Escopo próprio para a janela de fechamento, como nos demais formulários.</summary>
    private readonly IServiceScopeFactory _escopos;

    private readonly ISnackbarService _snackbar;
    private readonly IDialogoService _dialogo;
    private readonly DispatcherTimer _relogio;

    private List<Agendamento> _doDia = [];

    public ObservableCollection<CartaoFila> Aguardando { get; } = [];
    public ObservableCollection<CartaoFila> NaRecepcao { get; } = [];
    public ObservableCollection<CartaoFila> Chamados { get; } = [];
    public ObservableCollection<CartaoFila> EmAtendimento { get; } = [];
    public ObservableCollection<CartaoFila> Finalizados { get; } = [];

    /// <summary>
    /// O quadro só está vazio quando as CINCO colunas estão — um paciente já finalizado
    /// é dia com movimento, não fila vazia.
    /// </summary>
    public bool QuadroVazio => Aguardando.Count == 0 && NaRecepcao.Count == 0
                               && Chamados.Count == 0
                               && EmAtendimento.Count == 0 && Finalizados.Count == 0;

    /// <summary>
    /// Há gente chamada esperando ser anunciada. É o que acende a faixa no topo da tela:
    /// a coluna sozinha não bastaria, porque o balcão passa o dia com esta tela aberta e
    /// os olhos no paciente à frente dele — cartão que aparece calado numa das cinco
    /// colunas é cartão que ninguém vê.
    /// </summary>
    public bool TemChamados => Chamados.Count > 0;

    /// <summary>Quem chamar, em uma linha ("Ana Souza · sala 2").</summary>
    [ObservableProperty]
    private string _avisoChamada = string.Empty;

    [ObservableProperty]
    private DateTime _dia = DateTime.Today;

    [ObservableProperty]
    private bool _carregando;

    /// <summary>
    /// O que o quadro NÃO mostra: faltas e cancelamentos, que saem da fila.
    ///
    /// Substituiu a linha de resumo que repetia a contagem das cinco colunas em sequência
    /// — os mesmos cinco números que agora ficam no cabeçalho de cada raia, junto do que
    /// eles contam. Repetidos acima do quadro, obrigavam o olho a casar número com coluna
    /// pela ordem e empurravam o quadro para baixo.
    /// </summary>
    [ObservableProperty]
    private string _foraDaFila = string.Empty;

    /// <summary>
    /// Habilita os botões de escrita da tela. É a metade VISÍVEL da permissão: o
    /// botão apagado explica por que não dá; a guarda no comando é que impede.
    /// Só desabilitar seria enfeite — um atalho de teclado passaria direto.
    ///
    /// ⚠️ <b>`EditarAgenda` OU `MovimentarFila`</b> — a MESMA conta do `ExigirAlgum` dos
    /// comandos (parcela 62). Enquanto esta metade olhava só `EditarAgenda`, o perfil
    /// `Profissional` — que a parcela 61 criou com `MovimentarFila` e sem `EditarAgenda` —
    /// abria o quadro do balcão com TODOS os cartões apagados e o arrasto travado, apesar
    /// de as guardas o autorizarem. Metade visível mais restrita que a guarda é pior do
    /// que metade nenhuma: ela mente sobre o que a pessoa pode fazer. O Consultório já
    /// fazia certo (<c>MeuDiaViewModel.PodeMovimentarFila</c>).
    /// </summary>
    public bool PodeEditarAgenda => SessaoUsuario.Atual.PodeAlgum(
        Permissao.EditarAgenda | Permissao.MovimentarFila);

    /// <summary>
    /// Os atos que são só do BALCÃO — concluir a sessão, marcar falta, cancelar —, e por
    /// isso pedem <c>EditarAgenda</c> ESTRITO, sem o <c>MovimentarFila</c>.
    ///
    /// ⚠️ A metade visível precisa ser esta, e não <see cref="PodeEditarAgenda"/>: o botão
    /// do próximo passo vira <b>"Concluir"</b> na última etapa, e concluir são quatro fatos
    /// do mesmo ato (guia, pacote, insumo, caixa) — três deles do balcão, que é a decisão
    /// da parcela 61. Com a metade larga aqui, o perfil <c>Profissional</c> via "Concluir"
    /// ACESO, clicava e levava a recusa do <c>Exigir</c>: metade visível mais larga que a
    /// guarda é a outra face do "botão que não faz nada" — ela promete o que não entrega.
    /// </summary>
    /// <remarks>
    /// `Pode` com bits combinados é um <b>E</b>: concluir pede as duas coisas — mexer na
    /// fila do balcão E lançar o atendimento. A segunda entrou quando a guarda passou a
    /// exigir <c>LancarAtendimento</c>: guarda mais estreita que a metade visível é o
    /// botão que promete e recusa depois do clique, que é o defeito da parcela 41 pelo
    /// avesso.
    /// </remarks>
    public bool PodeConcluirSessao => SessaoUsuario.Atual.Pode(
        Permissao.EditarAgenda | Permissao.LancarAtendimento);

    /// <summary>
    /// Colher o termo é ato de outro bit — a técnica de enfermagem o tem e não tem o da
    /// agenda —, e por isso ele é perguntado à parte no menu "⋯". É a metade visível que
    /// faltava: enquanto o menu montava os itens só pelo ESTADO do cartão, o XAML já
    /// prometia que "quem decide item a item é o próprio menu", e as guardas recusavam
    /// depois do clique.
    /// </summary>
    public bool PodeColherTermo => SessaoUsuario.Atual.Pode(Permissao.ColherAssinaturaPaciente);

    /// <summary>
    /// A leitura FALHOU — o terceiro estado (parcela 62). Sem ele, a fila do balcão
    /// desenhava "ninguém marcado para hoje" quando o banco oscilava na abertura: falha
    /// com cara de dia vazio, na primeira tela que a recepção abre de manhã. Era a única
    /// tela de lista do módulo sem ele.
    /// </summary>
    [ObservableProperty] private bool _naoVerificado;

    /// <summary>
    /// ⚠️ NADA de serviço SCOPED no construtor — nem <c>AgendaService</c>, nem
    /// <c>PainelRecepcaoService</c>, nem <c>TermoProcedimentoService</c>.
    ///
    /// O shell resolve esta tela do provedor RAIZ (<c>SuiteApp</c> passa
    /// <c>host.Services</c> ao <c>ShellViewModel</c>, que o entrega a
    /// <c>IModuloApp.CriarTela</c>). Serviço Scoped pedido à raiz vive no ESCOPO RAIZ —
    /// isto é, pela vida inteira do aplicativo —, e com ele o <c>DbContext</c>. Daí saem
    /// dois estragos, e nenhum deles falha:
    ///
    /// 1. **A fila deixa de ver a outra máquina.** A consulta é rastreada, e o EF não
    ///    sobrescreve valores de entidade já rastreada: reler o dia no MESMO contexto
    ///    devolve o `ChamadoEm` que ele já tinha — nulo. O médico clica em "Chamar
    ///    próximo", o cartão não muda de coluna no balcão, e a recepcionista só descobre
    ///    quando ele abre a porta. É exatamente a sincronização que a parcela 38 existe
    ///    para garantir, desfeita por uma linha de injeção.
    /// 2. **`DbContext` não aceita duas operações ao mesmo tempo.** A batida de um minuto
    ///    caindo em cima de um clique vira "A second operation was started on this context
    ///    instance" — um erro em inglês, no balcão, com o paciente na frente.
    ///
    /// A regra que fica: <b>tela de vida longa abre ESCOPO por operação</b>. É o que a
    /// <c>AgendaViewModel</c> e o <c>MeuDiaViewModel</c> já faziam — e é por isso que a
    /// grade e o quadro do médico atualizavam e a fila não.
    /// </summary>
    public FilaViewModel(
        IServiceScopeFactory escopos,
        ISnackbarService snackbar, IDialogoService dialogo)
    {
        _escopos = escopos;
        _snackbar = snackbar;
        _dialogo = dialogo;

        // Duas coisas por batida, e a segunda é a que faz a chamada do consultório
        // chegar aqui.
        //
        // (1) Sem isto o "há 5 min" da tela envelhece e mente: quem está há 40 minutos na
        //     sala de espera continuaria aparecendo como recém-chegado.
        // (2) A releitura do banco. Até a parcela 38 esta tela só relia por clique, e isso
        //     bastava porque tudo o que mudava o quadro era clicado AQUI. Deixou de
        //     bastar: agora o profissional carimba a chamada do app dele, e um quadro que
        //     só se atualiza quando alguém clica em "Atualizar" transformaria o recado em
        //     nada — o balcão ficaria olhando uma tela que já está errada.
        //
        // Um minuto é o intervalo certo pelo mesmo motivo de sempre: o consultório
        // sabe que chamou e vê o próprio quadro; quem espera o anúncio é o paciente
        // sentado, e um minuto é menos do que ele leva para atravessar a sala.
        //
        // Quem liga e desliga é a View (Loaded/Unloaded): o shell cria uma tela nova a
        // cada navegação, e um timer rodando manteria vivo cada ViewModel já trocado.
        _relogio = new DispatcherTimer { Interval = TimeSpan.FromMinutes(1) };
        _relogio.Tick += (_, _) => _ = ReconferirAsync();

        _ = CarregarAsync();
    }

    /// <summary>
    /// A batida do relógio: relê o dia e reenvelhece os rótulos.
    ///
    /// Ela NÃO acende o "Carregando" e nunca mostra erro na tela. É recarga de fundo, e
    /// quem está no balcão com um paciente à frente não pode ver a fila piscar em
    /// branco a cada minuto, nem levar um aviso vermelho porque o banco demorou uma vez.
    /// A falha vai para o log e a tela segue com o que já tinha — desatualizada por um
    /// minuto, que é o que ela seria de qualquer jeito.
    ///
    /// Só relê HOJE: quem está olhando a agenda de terça que vem não tem fila correndo,
    /// e recarregar por baixo faria o quadro se mexer sozinho enquanto a pessoa lê.
    /// </summary>
    private async Task ReconferirAsync()
    {
        if (Dia.Date != DateTime.Today || Carregando)
        {
            AtualizarEsperas();
            return;
        }

        try
        {
            await CarregarAsync(silencioso: true);
        }
        catch (Exception ex)
        {
            Clinica.Application.Diagnostico.Registrar(
                "Recepção — releitura automática da fila falhou", ex);
        }
    }

    /// <summary>Liga o relógio da espera (chamado quando a tela entra em cena).</summary>
    public void IniciarRelogio() => _relogio.Start();

    /// <summary>Desliga o relógio (chamado quando a tela sai de cena).</summary>
    public void PararRelogio() => _relogio.Stop();

    partial void OnDiaChanged(DateTime value) => _ = CarregarAsync();

    [RelayCommand]
    public Task CarregarAsync() => CarregarAsync(silencioso: false);

    /// <summary>
    /// Descarte de resposta fora de ordem (parcela 50): a batida do relógio e a troca de
    /// dia concorrem, e a resposta de HOJE chegando por último reescreveria as cinco
    /// colunas — e o <c>_doDia</c> que os comandos de etapa usam — por cima do dia que a
    /// pessoa acabou de escolher. A meia-guarda por <c>Carregando</c> não cobria: a
    /// recarga silenciosa roda com ele desligado.
    /// </summary>
    private int _geracaoCarga;

    private async Task CarregarAsync(bool silencioso)
    {
        var geracao = ++_geracaoCarga;
        if (!silencioso) NaoVerificado = false;

        try
        {
            Carregando = !silencioso;

            // Um ESCOPO por carga: é o que faz esta tela enxergar o que a outra máquina
            // gravou. Contexto de vida longa devolve a entidade que ele já rastreava, e a
            // chamada carimbada no consultório nunca chegaria aqui.
            using var escopo = _escopos.CreateScope();
            var agenda = escopo.ServiceProvider.GetRequiredService<AgendaService>();
            var painel = escopo.ServiceProvider.GetRequiredService<PainelRecepcaoService>();
            var servicoTermos = escopo.ServiceProvider.GetRequiredService<TermoProcedimentoService>();

            var doDia = await agenda.DoDiaAsync(DateOnly.FromDateTime(Dia));

            // Chegou tarde: outra carga mais nova já foi pedida.
            if (geracao != _geracaoCarga) return;

            _doDia = [.. doDia];

            // Quem tem guia pendente hoje. Falha aqui não pode derrubar a fila inteira:
            // é aviso, não o conteúdo da tela.
            HashSet<int> comPendencia;
            try
            {
                var pendencias = await painel.PendenciasDoDiaAsync(DateOnly.FromDateTime(Dia));
                comPendencia = pendencias.Select(p => p.PacienteId).ToHashSet();
            }
            catch (Exception ex)
            {
                Clinica.Application.Diagnostico.Registrar(
                    "Recepção — pendências do dia não puderam ser conferidas", ex);
                comPendencia = [];
            }

            // Quem ainda tem termo por assinar hoje (parcela 66). Falha aqui, como a
            // pendência acima, não derruba a fila: é aviso, não o conteúdo da tela.
            Dictionary<int, IReadOnlyList<SituacaoTermo>> termos;
            try
            {
                termos = new Dictionary<int, IReadOnlyList<SituacaoTermo>>(
                    await servicoTermos.DoDiaAsync(DateOnly.FromDateTime(Dia)));
            }
            catch (Exception ex)
            {
                Clinica.Application.Diagnostico.Registrar(
                    "Recepção — termos do dia não puderam ser conferidos", ex);
                termos = [];
            }

            if (geracao != _geracaoCarga) return;

            Aguardando.Clear();
            NaRecepcao.Clear();
            Chamados.Clear();
            EmAtendimento.Clear();
            Finalizados.Clear();

            foreach (var a in _doDia)
            {
                // Cancelado e falta saem do quadro: o kanban é o fluxo de quem vem hoje.
                if (a.Etapa == EtapaFila.ForaDaFila) continue;

                var cartao = new CartaoFila
                {
                    AgendamentoId = a.Id,
                    PacienteId = a.PacienteId,
                    Horario = a.DataHora.ToString("HH:mm"),
                    Paciente = a.Paciente?.Nome ?? "(paciente removido)",
                    // Nome do CATÁLOGO, nunca o enum: `ToString()` escrevia
                    // "AcupunturaComEletro" no cartão que o médico lê (parcela 41).
                    Modalidade = CatalogoModalidades.Nome(
                        a.ModalidadeCodigo ?? a.ModalidadePrevista.ToString()),
                    Profissional = a.Profissional?.Rotulo ?? "—",
                    Sala = a.Sala?.Nome ?? "—",
                    Etapa = a.Etapa,
                    Observacoes = a.Observacoes,
                    EhRetornoDoSegundoCodigo = a.Origem == OrigemAgendamento.RetornoSugerido,
                    EhEncaixe = a.Encaixe,
                    Lancamento = DescreverLancamento(a),
                    TemGuiaPendente = comPendencia.Contains(a.PacienteId),
                    TemTermoPendente = termos.TryGetValue(a.PacienteId, out var doPaciente)
                                       && doPaciente.Any(t => t.Pendente)
                };

                Coluna(a.Etapa).Add(cartao);
            }

            AtualizarEsperas();
            // As cinco coleções mudaram: o quadro precisa reavaliar se está vazio e se
            // há alguém esperando ser anunciado.
            OnPropertyChanged(nameof(QuadroVazio));
            OnPropertyChanged(nameof(TemChamados));

            // O aviso nomeia QUEM chamar e para onde. "1 paciente chamado" obrigaria a
            // recepcionista a procurar o cartão numa das cinco colunas antes de abrir a
            // boca — e é ela que tem o paciente da vez à frente dela.
            AvisoChamada = string.Join(" · ", Chamados.Select(c => $"{c.Paciente} → sala {c.Sala}"));

            var faltas = _doDia.Count(a => a.Status == StatusAgendamento.Faltou);
            var cancelados = _doDia.Count(a => a.Status == StatusAgendamento.Cancelado);
            ForaDaFila = (faltas, cancelados) switch
            {
                (0, 0) => "Nenhuma falta nem cancelamento hoje.",
                var (f, c) => $"Fora do quadro: {f} falta(s) e {c} cancelamento(s)."
            };
        }
        catch (Exception ex)
        {
            Clinica.Application.Diagnostico.Registrar("Recepção — fila do dia não pôde ser carregada", ex);

            // Recarga de fundo que falha não interrompe o balcão com um aviso vermelho:
            // ela já foi para o log, e a tela segue com o quadro do minuto anterior.
            if (!silencioso && geracao == _geracaoCarga)
            {
                // O terceiro estado, além do snackbar: o aviso passageiro some em 4s e o
                // quadro vazio FICA, afirmando um dia sem ninguém que não foi verificado.
                NaoVerificado = true;
                _snackbar.Erro($"Não foi possível carregar a fila: {ex.Message}");
            }
        }
        finally
        {
            // A carga superada não apaga o "Carregando" da que ainda está no ar.
            if (geracao == _geracaoCarga) Carregando = false;
        }
    }

    /// <summary>
    /// "Marcado por Ana em 08/08/2026 14:32" — ou a frase que assume a lacuna.
    ///
    /// Horário anterior à parcela 58 não guarda quem o lançou, e deixar em branco faria a
    /// dica parecer que não conseguiu carregar.
    /// </summary>
    private static string DescreverLancamento(Agendamento a)
    {
        if (string.IsNullOrWhiteSpace(a.CriadoPor))
            return "Marcado antes de o sistema passar a registrar quem lança — sem autoria.";

        return a.CriadoEm is { } quando
            ? $"Marcado por {a.CriadoPor} em {quando:dd/MM/yyyy HH:mm}"
            : $"Marcado por {a.CriadoPor}";
    }

    private ObservableCollection<CartaoFila> Coluna(EtapaFila etapa) => etapa switch
    {
        EtapaFila.Chegou => NaRecepcao,
        EtapaFila.Chamado => Chamados,
        EtapaFila.EmAtendimento => EmAtendimento,
        EtapaFila.Finalizado => Finalizados,
        _ => Aguardando
    };

    /// <summary>Recalcula os rótulos de espera sem ir ao banco (chamado a cada minuto).</summary>
    private void AtualizarEsperas()
    {
        var agora = DateTime.Now;
        var porId = _doDia.ToDictionary(a => a.Id);

        foreach (var cartao in Aguardando.Concat(NaRecepcao).Concat(Chamados)
                                         .Concat(EmAtendimento).Concat(Finalizados))
        {
            if (!porId.TryGetValue(cartao.AgendamentoId, out var ag)) continue;

            var minutos = ag.EsperaMinutos(agora);
            cartao.Espera = minutos is null ? string.Empty : $"{minutos} min";
            // Espera só "corre" enquanto o paciente ainda não foi chamado.
            cartao.EsperaLonga = minutos >= EsperaLongaMinutos
                                 && ag.InicioAtendimentoEm is null;

            var desdeAChamada = ag.ChamadoHaMinutos(agora);
            cartao.ChamadoHa = desdeAChamada switch
            {
                null => string.Empty,
                0 => "chamado agora",
                var m => $"chamado há {m} min"
            };
            cartao.ChamadaDemorada = desdeAChamada >= ChamadaDemoradaMinutos;
        }
    }

    /// <summary>
    /// O PRÓXIMO passo do cartão, seja ele qual for — o botão sólido de cada cartão.
    ///
    /// Um comando só em vez de quatro visibilidades excludentes: o cartão já sabe em que
    /// etapa está, e é ele quem diz o rótulo (<see cref="CartaoFila.ProximoPasso"/>).
    /// </summary>
    [RelayCommand]
    private async Task AvancarAsync(CartaoFila? cartao)
    {
        if (cartao is null) return;

        switch (cartao.Etapa)
        {
            case EtapaFila.Aguardando: await RegistrarChegadaAsync(cartao); break;
            case EtapaFila.Chegou: await ChamarAsync(cartao); break;
            case EtapaFila.Chamado: await IniciarAtendimentoAsync(cartao); break;
            case EtapaFila.EmAtendimento: await FinalizarAsync(cartao); break;
        }
    }

    /// <summary>
    /// ARRASTAR o cartão de uma raia para outra — o gesto que define um kanban.
    ///
    /// Ele não substitui os botões: metade do balcão trabalha com o mouse na mão e a
    /// outra metade não arrasta nada, e um quadro em que a única forma de andar é
    /// arrastar deixaria a segunda metade sem saída. O que ele faz é dar ao gesto natural
    /// o mesmo efeito do clique.
    ///
    /// As transições legais são EXATAMENTE as dos botões — não uma segunda regra escrita
    /// aqui. Duas definições de "para onde este cartão pode ir" divergem na primeira
    /// correção, e a que ninguém lembra de ajustar é a de baixo.
    ///
    /// Para TRÁS anda um passo por vez, porque é isso que
    /// <c>AgendaService.VoltarEtapaAsync</c> faz: ele apaga um carimbo de hora, e apagar
    /// três de uma vez para atender a um arrasto longo inventaria uma linha do tempo que
    /// não aconteceu. Movimento impossível não é silêncio — a tela DIZ por que não deu.
    /// </summary>
    public async Task MoverParaAsync(CartaoFila? cartao, EtapaFila alvo)
    {
        if (cartao is null || cartao.Etapa == alvo) return;

        var legal = (cartao.Etapa, alvo) switch
        {
            (EtapaFila.Aguardando, EtapaFila.Chegou) => true,
            (EtapaFila.Chegou, EtapaFila.Chamado) => true,
            (EtapaFila.Aguardando or EtapaFila.Chegou or EtapaFila.Chamado,
                EtapaFila.EmAtendimento) => true,
            (EtapaFila.EmAtendimento, EtapaFila.Finalizado) => true,
            // Um passo para trás: o inverso exato do que o serviço sabe desfazer.
            (EtapaFila.Chegou, EtapaFila.Aguardando) => true,
            (EtapaFila.Chamado, EtapaFila.Chegou) => true,
            (EtapaFila.EmAtendimento, EtapaFila.Chamado) => true,
            _ => false
        };

        if (!legal)
        {
            _snackbar.Info(alvo == EtapaFila.Finalizado
                ? "Só se conclui quem está em atendimento — leve o cartão para a sala primeiro."
                : "Este cartão não anda direto para essa coluna. Arraste um passo por vez.");
            return;
        }

        var voltando = alvo < cartao.Etapa;
        if (voltando)
        {
            await VoltarEtapaAsync(cartao);
            return;
        }

        switch (alvo)
        {
            case EtapaFila.Chegou: await RegistrarChegadaAsync(cartao); break;
            case EtapaFila.Chamado: await ChamarAsync(cartao); break;
            case EtapaFila.EmAtendimento: await IniciarAtendimentoAsync(cartao); break;
            case EtapaFila.Finalizado: await FinalizarAsync(cartao); break;
        }
    }

    /// <summary>Check-in no balcão: o paciente chegou e o cronômetro da espera começa.</summary>
    [RelayCommand]
    private async Task RegistrarChegadaAsync(CartaoFila? cartao)
        => await ExecutarAsync(cartao, async c =>
        {
            // Mover a fila é UM ato com UMA regra nos dois quadros (balcão e consultório):
            // EditarAgenda OU MovimentarFila — ver a nota no enum Permissao (parcela 61).
            SessaoUsuario.Atual.ExigirAlgum(
                Permissao.EditarAgenda | Permissao.MovimentarFila, "mexer na fila do dia");

            using (var e = _escopos.CreateScope())
                await e.ServiceProvider.GetRequiredService<AgendaService>()
                    .RegistrarChegadaAsync(c.AgendamentoId, SessaoUsuario.Atual.Operador);

            // O check-in é o ÚLTIMO momento barato: o paciente está no balcão, e
            // carteirinha vencida ou cota estourada ainda dá para resolver com um
            // telefonema. Depois da sessão, a mesma informação só vira glosa.
            var elegibilidade = await ConferirElegibilidadeAsync(c);

            var recados = new List<string>();
            if (c.TemGuiaPendente)
                recados.Add("Tem GUIA PENDENTE de baixa — aproveite que ele está aqui e peça "
                            + "o documento; depois a cobrança vira telefonema.");
            recados.AddRange(elegibilidade);

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

    /// <summary>
    /// Chama o paciente pelo balcão — o mesmo fato que o botão do consultório grava.
    ///
    /// Existe dos dois lados de propósito: metade das clínicas o profissional avisa pela
    /// porta, e obrigar o balcão a esperar um clique da sala faria a coluna "chamado"
    /// nascer sempre vazia num fluxo que funciona há anos. Quem carimba é quem clicar
    /// primeiro (<see cref="AgendaService.ChamarAsync"/> é idempotente): a hora da
    /// chamada é uma só, e a segunda chamada não reinicia o relógio de quem já se levantou.
    /// </summary>
    [RelayCommand]
    private async Task ChamarAsync(CartaoFila? cartao)
        => await ExecutarAsync(cartao, async c =>
        {
            SessaoUsuario.Atual.ExigirAlgum(
                Permissao.EditarAgenda | Permissao.MovimentarFila, "mexer na fila do dia");

            using (var e = _escopos.CreateScope())
                await e.ServiceProvider.GetRequiredService<AgendaService>()
                    .ChamarAsync(c.AgendamentoId, SessaoUsuario.Atual.Operador);
            _snackbar.Info($"{c.Paciente} chamado — anuncie para a sala {c.Sala}.");
        }, "chamada do paciente");

    /// <summary>O paciente levantou e entrou: fim da espera, começo da sessão.</summary>
    [RelayCommand]
    private async Task IniciarAtendimentoAsync(CartaoFila? cartao)
        => await ExecutarAsync(cartao, async c =>
        {
            SessaoUsuario.Atual.ExigirAlgum(
                Permissao.EditarAgenda | Permissao.MovimentarFila, "mexer na fila do dia");

            using (var e = _escopos.CreateScope())
                await e.ServiceProvider.GetRequiredService<AgendaService>()
                    .IniciarAtendimentoAsync(c.AgendamentoId, SessaoUsuario.Atual.Operador);
            _snackbar.Sucesso($"{c.Paciente} em atendimento.");
        }, "início do atendimento");

    /// <summary>
    /// Encerra a sessão. Concluir são QUATRO fatos do mesmo ato — a guia nasce, o pacote
    /// debita, o insumo sai do estoque e o dinheiro entra no caixa —, e por muito tempo só
    /// o primeiro acontecia. Ver <see cref="FechamentoSessaoService"/>.
    ///
    /// ⚠️ <b>A GUIA NASCE AQUI, antes de qualquer janela (parcela 65).</b> Até então a
    /// ordem era a inversa: a janela abria primeiro e a guia só existia se ela fosse
    /// CONFIRMADA — fechá-la deixava a sessão registrada na agenda e invisível para quem
    /// fatura. A direção fixou a regra: atendimento que entra no sistema já gera guia,
    /// agendado ou avulso. Os outros três fatos são o passo seguinte e não podem
    /// condicionar nem desfazer a guia.
    ///
    /// A janela continua sendo PROPOSTA confirmada, e agora só abre quando há o que
    /// decidir (pacote, dinheiro ou insumo). Fechá-la não desfaz nada.
    /// </summary>
    [RelayCommand]
    private async Task FinalizarAsync(CartaoFila? cartao)
        => await ExecutarAsync(cartao, async c =>
        {
            SessaoUsuario.Atual.Exigir(Permissao.EditarAgenda, "mexer na fila do dia");

            // ⚠️ E o bit do ATO, não só o da fila. Concluir aqui é o que CRIA o atendimento
            // e, com ele, as guias — desde que a parcela 65 mudou o momento em que a guia
            // nasce. `LancarAtendimento` existe justamente para a direção poder tirar isso
            // de alguém, e a caixinha dele em Acessos diz, com estas palavras, "criar o
            // atendimento — e, com ele, as guias". Enquanto só `EditarAgenda` guardou esta
            // porta, desmarcar aquela caixinha fechava a tela de Novo atendimento e não
            // fechava NADA: a mesma pessoa continuava gerando guia pelo Concluir da Fila,
            // que é a porta que a clínica usa o dia inteiro.
            //
            // Nenhum perfil padrão perde nada: só `Recepcao` tem `EditarAgenda` junto de
            // `LancarAtendimento`, e o Gerente tem todas. O que muda é o bit passar a valer.
            SessaoUsuario.Atual.Exigir(
                Permissao.LancarAtendimento, "lançar o atendimento e gerar as guias");

            RegistroAtendimento registro;
            using (var scope = _escopos.CreateScope())
            {
                var fechamento = scope.ServiceProvider.GetRequiredService<FechamentoSessaoService>();
                registro = await fechamento.RegistrarAtendimentoAsync(
                    c.AgendamentoId, SessaoUsuario.Atual.Operador);
            }

            // Os recados do LANÇAMENTO em diálogo, não em snackbar (parcela 62): o
            // principal deles é a NÃO CONFORMIDADE reaberta porque o paciente voltou, e
            // ele existe para a secretária cobrar a guia AGORA, com ele ainda no balcão.
            // Snackbar some em 4s e não sobrevive a quem virou para atender o próximo —
            // é o mesmo diálogo que o check-in usa para os alertas de elegibilidade.
            if (registro.RecadosDoLancamento.Count > 0)
                _dialogo.Aviso($"Atenção — {c.Paciente}",
                    string.Join("\n\n", registro.RecadosDoLancamento));

            if (!registro.TemDecisao)
            {
                _snackbar.Sucesso(registro.GuiasGeradas == 0
                    ? $"Sessão de {c.Paciente} concluída — particular, sem guia a faturar."
                    : $"Sessão de {c.Paciente} concluída — {registro.GuiasGeradas} guia(s) no faturamento.");
                return;
            }

            var vm = new FechamentoSessaoViewModel(_escopos, c.AgendamentoId);
            var janela = new Janelas.FechamentoSessaoWindow(vm)
            {
                Owner = JanelaDona.Atual()
            };

            // Modal: o await fica com a janela, e o recarregar da fila vem do
            // ExecutarAsync assim que ela fecha — inclusive quando fecha com aviso.
            if (janela.ShowDialog() != true || janela.Resultado is not { } resultado)
            {
                // A sessão está concluída e a guia feita; o que ficou de fora foi o
                // pacote/caixa. Dizer isso é o que impede a recepcionista de concluir de
                // novo procurando a guia que já existe.
                _snackbar.Info($"Sessão de {c.Paciente} concluída e guia gerada — "
                               + "pacote/caixa não registrados.");
                return;
            }

            var partes = new List<string> { $"{registro.GuiasGeradas} guia(s)" };
            if (resultado.Consumo is not null) partes.Add("1 sessão do pacote");
            if (resultado.Movimentos.Count > 0) partes.Add($"{resultado.Movimentos.Count} insumo(s)");
            if (resultado.Lancamento is not null) partes.Add("entrada no caixa");

            _snackbar.Sucesso($"Sessão de {c.Paciente} concluída — {string.Join(" · ", partes)}.");
        }, "conclusão do atendimento");

    /// <summary>
    /// Abre a coleta do termo do procedimento (parcela 66) — a PORTA do alerta que o
    /// check-in dá.
    ///
    /// Vem para cá, e não para uma tela do Consultório, porque o termo se colhe com o
    /// paciente no balcão, antes de ele subir para a sala: quem recebe é quem apresenta o
    /// papel. Alerta sem porta no mesmo app é pior que alerta nenhum — ele ensina a pessoa
    /// a ignorá-lo (a lição da parcela 48).
    ///
    /// Quando há mais de um termo pendente, colhe o PRIMEIRO e o cartão continua marcado:
    /// o próximo clique abre o seguinte. Empilhar duas janelas obrigaria o paciente a
    /// assinar duas vezes sem saber quantas faltam.
    /// </summary>
    [RelayCommand]
    private async Task ColherTermoAsync(CartaoFila? cartao)
        => await ExecutarAsync(cartao, async c =>
        {
            SessaoUsuario.Atual.Exigir(
                Permissao.ColherAssinaturaPaciente, "colher a assinatura do paciente");

            // ⚠️ Só HOJE, e a guarda FALA. O quadro navega dias (`DiaAnterior`/`ProximoDia`),
            // mas a emissão carimba `DateTime.Today` — colher com o quadro em 12/08 criaria
            // um termo datado de hoje que nunca casaria com aquele dia: o papel sairia
            // assinado e a pendência de 12/08 continuaria acesa. Pior que não ter o botão,
            // porque a pessoa acreditaria ter resolvido.
            if (Dia.Date != DateTime.Today)
            {
                _dialogo.Aviso(
                    "Termo é do dia do procedimento",
                    "O termo vale para a SESSÃO, e é colhido no dia dela. Volte para hoje "
                    + "para colher a assinatura deste paciente.");
                return;
            }

            IReadOnlyList<SituacaoTermo> situacoes;
            using (var e = _escopos.CreateScope())
                situacoes = await e.ServiceProvider.GetRequiredService<TermoProcedimentoService>()
                    .SituacaoDoDiaAsync(c.PacienteId, DateOnly.FromDateTime(Dia));

            var pendente = situacoes.FirstOrDefault(s => s.Pendente);

            // Guarda que FALA: o cartão pode ter sido resolvido noutra máquina entre a
            // carga do quadro e o clique, e sair calada aqui seria botão que não faz nada.
            if (pendente is null)
            {
                c.TemTermoPendente = false;
                _snackbar.Info($"Não há termo pendente para {c.Paciente}.");
                return;
            }

            ColetaDeTermo.Abrir(
                _escopos, c.PacienteId, c.Paciente,
                pendente.ModeloId, pendente.DocumentoId,
                // O profissional do HORÁRIO: sem ele o termo nasce órfão e a via que o
                // paciente assina — e que fica 20 anos no prontuário — sai com
                // "Profissional responsável" no lugar do nome e do CRM de quem faz o
                // procedimento.
                _doDia.FirstOrDefault(a => a.Id == c.AgendamentoId)?.ProfissionalId);

            // Recarrega SEMPRE, e não só no concluiu: abrir a janela já emite o termo
            // numerado, e o selo do cartão precisa refletir isso.
            await CarregarAsync();
        }, "coleta do termo");

    /// <summary>Volta o cartão uma coluna — clicar errado no kanban é rotina.</summary>
    [RelayCommand]
    private async Task VoltarEtapaAsync(CartaoFila? cartao)
        => await ExecutarAsync(cartao, async c =>
        {
            SessaoUsuario.Atual.ExigirAlgum(
                Permissao.EditarAgenda | Permissao.MovimentarFila, "mexer na fila do dia");

            using (var e = _escopos.CreateScope())
                await e.ServiceProvider.GetRequiredService<AgendaService>()
                    .VoltarEtapaAsync(c.AgendamentoId, SessaoUsuario.Atual.Operador);
            _snackbar.Info("Cartão devolvido para a coluna anterior.");
        }, "volta de etapa");

    [RelayCommand]
    private async Task MarcarFaltaAsync(CartaoFila? cartao)
        => await ExecutarAsync(cartao, async c =>
        {
            SessaoUsuario.Atual.Exigir(Permissao.EditarAgenda, "mexer na fila do dia");

            using (var e = _escopos.CreateScope())
                await e.ServiceProvider.GetRequiredService<AgendaService>()
                    .MarcarFaltaAsync(c.AgendamentoId, SessaoUsuario.Atual.Operador);
            _snackbar.Info($"{c.Paciente} marcado como falta.");
        }, "marcação de falta");

    [RelayCommand]
    private async Task CancelarAsync(CartaoFila? cartao)
        => await ExecutarAsync(cartao, async c =>
        {
            SessaoUsuario.Atual.Exigir(Permissao.EditarAgenda, "mexer na fila do dia");

            using (var e = _escopos.CreateScope())
                await e.ServiceProvider.GetRequiredService<AgendaService>()
                    .CancelarAsync(c.AgendamentoId, SessaoUsuario.Atual.Operador);
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
