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

/// <summary>Um horário na coluna de um profissional.</summary>
public sealed class CartaoAgenda
{
    public required int AgendamentoId { get; init; }

    /// <summary>De quem é a coluna — é o que a lista de espera pergunta ao vagar.</summary>
    public required int? ProfissionalId { get; init; }

    /// <summary>Marcado em série (pacote de dez). Null = horário avulso.</summary>
    public required string? SerieId { get; init; }

    public bool EhSerie => !string.IsNullOrWhiteSpace(SerieId);

    public required string Faixa { get; init; }
    public required string Paciente { get; init; }
    public string? Telefone { get; init; }
    public required DateTime DataHora { get; init; }

    /// <summary>Fim previsto — é o que dá ao cartão a ALTURA dele na linha do tempo.</summary>
    public required DateTime Fim { get; init; }

    public required string Modalidade { get; init; }
    public required string Sala { get; init; }

    /// <summary>
    /// Quem atende. Na grade por profissional a coluna já responde isto; no modo SEMANA,
    /// em que a coluna é o dia, sem esta linha o cartão não diz com quem é a sessão.
    /// </summary>
    public required string Profissional { get; init; }

    public required string StatusRotulo { get; init; }
    public required bool EhEncaixe { get; init; }
    public required bool EhRetornoDoSegundoCodigo { get; init; }
    public required bool VeioDaListaEspera { get; init; }

    public string? Observacoes { get; init; }

    /// <summary>
    /// Quem LANÇOU o horário, e quando — escrito por extenso (parcela 58).
    ///
    /// A direção pediu para ver de quem é cada lançamento. A trilha de auditoria responde
    /// isso desde a parcela 21, mas ela é outra tela, filtrada por período; a pergunta que
    /// se faz olhando a agenda é sobre AQUELA linha, agora.
    ///
    /// Horário anterior à parcela 58 não tem o dado, e a frase DIZ isso em vez de ficar em
    /// branco: em branco não se distingue de "não carregou".
    /// </summary>
    public required string Lancamento { get; init; }

    /// <summary>Ainda dá para remarcar/cancelar (não virou atendimento).</summary>
    public required bool EmAberto { get; init; }

    /// <summary>Saiu do fluxo do dia: o horário está livre de novo, mas a linha fica.</summary>
    public required bool ForaDoDia { get; init; }

    /// <summary>
    /// A METADE VISÍVEL da permissão, dentro da janela do horário.
    ///
    /// As sete ações são executadas pela <c>AgendaViewModel</c>, que tem a guarda — mas a
    /// janela mostrava os seis botões ACESOS para quem só LÊ a agenda (a técnica de
    /// enfermagem, o financeiro, o faturista: os quatro perfis têm `VerAgenda`), e a
    /// recusa chegava depois do clique. É o defeito da parcela 41 numa janela que o
    /// vizinho já fazia certo: o vão livre e os botões da barra da agenda sempre tiveram
    /// `IsEnabled` — só esta janela não tinha.
    /// </summary>
    public bool PodeEditarAgenda => SessaoUsuario.Atual.Pode(Permissao.EditarAgenda);

    public bool TemTelefone => !string.IsNullOrWhiteSpace(Telefone);

    public bool TemObservacoes => !string.IsNullOrWhiteSpace(Observacoes);

    /// <summary>A linha de baixo do cartão: modalidade · profissional · sala.</summary>
    public string Contexto => string.Join(" · ", new[] { Modalidade, Profissional, Sala }
        .Where(p => !string.IsNullOrWhiteSpace(p) && p != "—"));
}

/// <summary>
/// Uma célula da linha do tempo: o cruzamento de uma faixa de horário com uma coluna.
///
/// Ela tem três estados, e os três precisam ser distinguíveis à distância — é disso que
/// a agenda vive:
/// <list type="bullet">
/// <item><b>Livre</b> — dá para marcar aqui, e o clique já abre o formulário nesta hora
/// e nesta coluna. É o gesto que a recepção repete o dia inteiro.</item>
/// <item><b>Ocupada</b> — tem cartão. Mais de um quando houve encaixe.</item>
/// <item><b>Continuação</b> — coberta por uma sessão que começou antes. Não é livre e não
/// repete o cartão; sem ela, uma sessão de uma hora pareceria deixar meia hora vaga.</item>
/// </list>
/// </summary>
public sealed class CelulaAgenda
{
    public required int? ProfissionalId { get; init; }

    /// <summary>Dia + hora desta célula — é o que o formulário recebe já preenchido.</summary>
    public required DateTime Quando { get; init; }

    /// <summary>A sala desta coluna, na visão por SALA. Nulo nas outras duas visões.</summary>
    public int? SalaId { get; init; }

    public required ObservableCollection<CartaoAgenda> Cartoes { get; init; }

    public required bool Continuacao { get; init; }

    public bool Livre => Cartoes.Count == 0 && !Continuacao;

    /// <summary>Já passou: marcar para trás é quase sempre engano de clique.</summary>
    public required bool NoPassado { get; init; }

    /// <summary>
    /// O motivo do fechamento, quando a agenda está bloqueada aqui (parcela 63) — "Férias
    /// da Ana", "Feriado". Nulo = aberto.
    ///
    /// A marcação JÁ era recusada pelo `AgendaService`; o que faltava era a grade dizer
    /// antes. Um vão bloqueado é visualmente idêntico a um vão livre, e o vão livre é
    /// clicável desde a parcela 58 — a recepcionista escolhia o paciente, preenchia o
    /// formulário e levava a recusa no Salvar, com ele na frente dela.
    /// </summary>
    public string? Bloqueio { get; init; }

    public bool Bloqueada => Bloqueio is not null;

    /// <summary>Só o que está livre, no futuro e com a agenda aberta convida a marcar.</summary>
    public bool PodeMarcar => Livre && !NoPassado && !Bloqueada;

    /// <summary>Fechado E sem ninguém marcado: é o vão que mostra o motivo.</summary>
    public bool MostrarBloqueio => Bloqueada && Livre;
}

/// <summary>Uma faixa de horário da grade — uma LINHA, com uma célula por coluna.</summary>
public sealed class FaixaAgenda
{
    public required TimeOnly Hora { get; init; }

    /// <summary>"09:00" na hora cheia, "" na meia — a régua não precisa repetir.</summary>
    public required string Rotulo { get; init; }

    public required bool HoraCheia { get; init; }

    public required ObservableCollection<CelulaAgenda> Celulas { get; init; }
}

/// <summary>Uma coluna da grade — um profissional (ou o resíduo "sem profissional").</summary>
public sealed class ColunaAgenda
{
    public required int? ProfissionalId { get; init; }

    /// <summary>A sala, quando a grade está agrupada por SALA. Nulo nas outras visões.</summary>
    public int? SalaId { get; init; }

    public required string Nome { get; init; }
    public required string Resumo { get; init; }
    public required ObservableCollection<CartaoAgenda> Horarios { get; init; }

    /// <summary>
    /// O dia desta coluna. No modo DIA é sempre o dia aberto; no modo SEMANA cada coluna
    /// é um dia diferente, e é daqui que a célula livre tira a data que leva ao formulário.
    /// </summary>
    public required DateOnly Data { get; init; }

    public bool Vazia => Horarios.Count == 0;
}

/// <summary>Um pedido na lista de espera.</summary>
public sealed class LinhaListaEspera
{
    public required int PedidoId { get; init; }
    public required string Paciente { get; init; }
    public required string Preferencias { get; init; }
    public required string Desde { get; init; }
    public required bool Prioritario { get; init; }
    public string? Observacoes { get; init; }
}

/// <summary>
/// Agenda multiprofissional: uma coluna por profissional, o dia inteiro à vista.
///
/// É a resposta ao bloqueio de fundação — antes o agendamento não sabia com quem era,
/// então a agenda só podia ser uma lista única e ninguém conseguia ver dois
/// consultórios ao mesmo tempo. Ao lado dela fica a LISTA DE ESPERA: quando um horário
/// vaga, a recepção vê na mesma tela quem chamar, em vez de tentar lembrar.
/// </summary>
public sealed partial class AgendaViewModel : ObservableObject
{
    private readonly IServiceScopeFactory _escopos;
    private readonly ISnackbarService _snackbar;
    private readonly IDialogoService _dialogo;

    /// <summary>
    /// A releitura de fundo. Quem liga e desliga é a View (Loaded/Unloaded), como na
    /// Fila: o shell constrói uma tela nova a cada navegação, e um timer rodando
    /// manteria vivo cada ViewModel já trocado — e faria vários irem ao banco pelo
    /// mesmo dia.
    /// </summary>
    private readonly DispatcherTimer _relogio;

    public ObservableCollection<ColunaAgenda> Colunas { get; } = [];
    public ObservableCollection<LinhaListaEspera> Espera { get; } = [];

    /// <summary>
    /// A LINHA DO TEMPO — as faixas de meia hora, de cima a baixo, com uma célula por
    /// coluna.
    ///
    /// É a diferença entre uma agenda e uma lista de horários: empilhados, o das 9h e o
    /// das 15h ficam colados, e a pergunta do balcão ("quando cabe?") só se responde lendo
    /// cartão por cartão. Na grade, o vazio TEM tamanho — e é clicável.
    /// </summary>
    public ObservableCollection<FaixaAgenda> Faixas { get; } = [];

    /// <summary>Passo da grade. É a duração padrão da clínica (<see cref="Agendamento.DuracaoPadraoMinutos"/>).</summary>
    public const int PassoMinutos = Agendamento.DuracaoPadraoMinutos;

    /// <summary>
    /// Janela padrão da grade. Ela ABRE nestas horas e se ESTICA para caber o que houver
    /// fora delas — nunca 00:00–23:59, que daria 48 faixas vazias para rolar antes do
    /// primeiro paciente.
    /// </summary>
    private static readonly TimeOnly AberturaPadrao = Agendamento.AberturaPadraoGrade;
    private static readonly TimeOnly FechamentoPadrao = Agendamento.FechamentoPadraoGrade;

    /// <summary>
    /// Piso de largura da grade: a régua mais uma coluna de 190 px por profissional.
    /// Abaixo disso o nome do paciente vira reticências, então o que cede é a tela — a
    /// grade rola na horizontal.
    /// </summary>
    [ObservableProperty] private double _larguraGrade = 800;

    /// <summary>Quantas colunas a linha do tempo tem (o <c>UniformGrid</c> de cada faixa).</summary>
    [ObservableProperty] private int _quantidadeColunas = 1;

    [ObservableProperty] private DateTime _dia = DateTime.Today;

    /// <summary>
    /// Semana em vez de dia. As colunas deixam de ser os profissionais e passam a ser os
    /// DIAS: é a forma de responder "quando ele tem vaga?", que no modo dia se responde
    /// clicando de dia em dia até achar.
    ///
    /// Os cartões e os botões são os mesmos — muda só o que cada coluna agrupa.
    /// </summary>
    [ObservableProperty] private bool _modoSemana;

    /// <summary>
    /// As colunas passam a ser as SALAS (parcela 63) — a metade da feature 02 da proposta
    /// que nunca existiu: a sala era gravada, respeitada no choque com a capacidade dela e
    /// bloqueável por período, e não havia grade que a usasse como coluna.
    ///
    /// A pergunta que só esta visão responde é a do dia cheio: "a sala de infusão está
    /// livre às 14h?" — na grade por profissional ela se responde varrendo seis colunas e
    /// lendo o rodapé de cada cartão.
    ///
    /// Vale só no modo DIA. Na semana as colunas já são os dias, e cruzar as duas coisas
    /// daria uma grade de sala × dia que não cabe em coluna nenhuma.
    /// </summary>
    [ObservableProperty] private bool _agruparPorSala;

    /// <summary>Trocar de visão é escolher o agrupamento, e as duas não se somam.</summary>
    public bool PodeAgruparPorSala => !ModoSemana;

    [ObservableProperty] private bool _carregando;

    /// <summary>
    /// A leitura FALHOU — o terceiro estado. Sem ele, lista vazia por erro fica idêntica
    /// a lista vazia por não haver nada, e o aviso de falha some junto com o snackbar.
    /// </summary>
    [ObservableProperty] private bool _naoVerificado;
    [ObservableProperty] private string _resumo = string.Empty;

    /// <summary>Feedback inline: erro de agenda fica na tela enquanto o usuário resolve.</summary>
    [ObservableProperty] private string _mensagem = string.Empty;
    [ObservableProperty] private bool _mensagemEhErro;

    /// <summary>
    /// Nenhum profissional cadastrado ainda: a grade não tem colunas e a tela precisa
    /// dizer o que fazer, em vez de aparecer vazia sem explicação.
    /// </summary>
    [ObservableProperty] private bool _semProfissionais;

    /// <summary>
    /// Nenhuma sala cadastrada — a grade por sala não tem colunas, e a tela precisa dizer
    /// por quê. Grade vazia se lê como defeito, não como cadastro faltando.
    /// </summary>
    [ObservableProperty] private bool _semSalas;

    /// <summary>
    /// Os fechamentos que alcançam o período aberto. Lidos uma vez por carga e
    /// consultados pela montagem da grade, célula a célula, em memória.
    /// </summary>
    private IReadOnlyList<BloqueioAgenda> _bloqueios = [];

    /// <summary>
    /// Habilita os botões de escrita da tela. É a metade VISÍVEL da permissão: o
    /// botão apagado explica por que não dá; a guarda no comando é que impede.
    /// Só desabilitar seria enfeite — um atalho de teclado passaria direto.
    /// </summary>
    public bool PodeEditarAgenda => SessaoUsuario.Atual.Pode(Permissao.EditarAgenda);

    /// <summary>
    /// Quem entrou é um profissional COM agenda — então dá para oferecer "só a minha".
    /// Falso no balcão, onde o usuário não está vinculado a ninguém.
    /// </summary>
    [ObservableProperty]
    private bool _soMinhaAgenda;

    /// <summary>
    /// Escapatória do filtro acima: o profissional que precisa ver a clínica inteira
    /// (para encaixar alguém na agenda do colega) marca isto e volta a ver tudo.
    /// </summary>
    [ObservableProperty]
    private bool _mostrarTodosOsProfissionais;

    /// <summary>
    /// Horário que acabou de vagar. Enquanto ele existe, a lista de espera mostra só
    /// quem serve para ele — é a pergunta do minuto seguinte a um cancelamento.
    /// </summary>
    [ObservableProperty] private DateTime? _sugestaoPara;

    [ObservableProperty] private int? _sugestaoProfissionalId;

    /// <summary>O que a lista está mostrando, escrito — filtro invisível é filtro que engana.</summary>
    [ObservableProperty] private string _tituloEspera = "Lista de espera";

    /// <summary>
    /// O que dizer quando a lista sai vazia. "Ninguém espera" e "ninguém serve para este
    /// horário" são respostas diferentes: trocá-las faria a recepção deixar de oferecer
    /// a lista a quem liga.
    /// </summary>
    [ObservableProperty] private string _esperaVazia = "Ninguém na lista de espera.";

    /// <summary>Há um horário em foco: a tela oferece voltar à lista inteira.</summary>
    public bool TemSugestao => SugestaoPara is not null;

    partial void OnSugestaoParaChanged(DateTime? value)
    {
        OnPropertyChanged(nameof(TemSugestao));
        OnPropertyChanged(nameof(EsperaResumo));
        TituloEspera = value is { } horario
            ? $"Quem chamar para {horario:dd/MM HH:mm}"
            : "Lista de espera";
    }

    public AgendaViewModel(
        IServiceScopeFactory escopos, ISnackbarService snackbar, IDialogoService dialogo)
    {
        _escopos = escopos;
        _snackbar = snackbar;
        _dialogo = dialogo;

        // Um minuto, o mesmo da Fila e pelo mesmo motivo: é menos do que o paciente leva
        // para atravessar a sala, e é o intervalo em que "este horário está vago" ainda
        // pode ser verdade quando a recepcionista clica nele.
        _relogio = new DispatcherTimer { Interval = TimeSpan.FromMinutes(1) };
        _relogio.Tick += (_, _) => _ = ReconferirAsync();

        _ = CarregarAsync();
    }

    partial void OnMostrarTodosOsProfissionaisChanged(bool value) => _ = CarregarAsync();

    partial void OnModoSemanaChanged(bool value)
    {
        // Sair do foco do horário: "quem chamar para as 14h" é pergunta do modo dia.
        SugestaoPara = null;
        SugestaoProfissionalId = null;

        // A semana já agrupa por dia. Deixar o agrupamento por sala LIGADO em silêncio
        // faria a pessoa voltar ao dia e encontrar uma grade que ela não escolheu.
        if (value) AgruparPorSala = false;

        OnPropertyChanged(nameof(PodeAgruparPorSala));
        _ = CarregarAsync();
    }

    partial void OnAgruparPorSalaChanged(bool value) => _ = CarregarAsync();

    partial void OnDiaChanged(DateTime value)
    {
        // Trocar de dia desfaz o foco: "quem chamar para as 14h de ontem" não é pergunta.
        SugestaoPara = null;
        SugestaoProfissionalId = null;
        _ = CarregarAsync();
    }

    /// <summary>
    /// Descarte de resposta fora de ordem (parcela 50): as setas de dia disparam uma
    /// carga POR CLIQUE, e num banco remoto a resposta de anteontem chegando por último
    /// deixaria a grade de um dia sob o título de outro — na tela em que se marca horário.
    /// </summary>
    private int _geracaoCarga;

    [RelayCommand]
    public Task CarregarAsync() => CarregarAsync(silencioso: false);

    private async Task CarregarAsync(bool silencioso)
    {
        var geracao = ++_geracaoCarga;

        try
        {
            if (!silencioso)
            {
                Carregando = true;
                NaoVerificado = false;
                Mensagem = string.Empty;
                MensagemEhErro = false;
            }

            using var scope = _escopos.CreateScope();
            var agenda = scope.ServiceProvider.GetRequiredService<AgendaService>();
            var equipe = scope.ServiceProvider.GetRequiredService<EquipeService>();
            var espera = scope.ServiceProvider.GetRequiredService<ListaEsperaService>();

            var dia = DateOnly.FromDateTime(Dia);

            // A leitura do DIA não serve à grade de semana — lá quem lê é
            // `MontarSemanaAsync`, e o dia escolhido já está dentro do período dela.
            // Buscá-lo assim mesmo era uma ida inteira ao banco remoto (com três joins)
            // cujo resultado ninguém abria, a cada clique nas setas de semana.
            IReadOnlyList<Agendamento> doDia = ModoSemana ? [] : await agenda.DoDiaAsync(dia);
            var profissionais = await equipe.ProfissionaisAtivosAsync();

            // As salas são lidas AQUI, com o resto — e não lá embaixo, no ramo que as usa.
            // Depois do Clear das colunas, qualquer await deixa a grade vazia na tela pelo
            // tempo do roundtrip, e a releitura de fundo (1 min, silenciosa) fazia isso
            // debaixo do olho de quem marca horário. É a regra da parcela 62: entre o
            // Clear() e o último Add não pode haver await.
            var salas = AgruparPorSala
                ? await equipe.SalasAtivasAsync()
                : [];

            // Chegou tarde: outro clique já pediu uma carga mais nova.
            if (geracao != _geracaoCarga) return;

            SemProfissionais = profissionais.Count == 0;

            // Zerado a cada carga: só o ramo da visão por sala o levanta, senão o aviso
            // de "nenhuma sala cadastrada" ficaria de pé na grade por profissional, onde
            // não quer dizer nada.
            SemSalas = false;

            // Os fechamentos que alcançam o que está na tela. Carregados JUNTO da grade e
            // não por célula: a leitura é uma só para o período inteiro, e perguntar ao
            // banco a cada vão daria ~300 consultas por dia aberto.
            await CarregarBloqueiosAsync(scope, geracao);
            if (geracao != _geracaoCarga) return;

            Colunas.Clear();
            Faixas.Clear();

            if (ModoSemana)
            {
                await MontarSemanaAsync(agenda, profissionais, geracao);
                if (geracao != _geracaoCarga) return;
                MontarGrade();
                await CarregarEsperaAsync(espera, geracao);
                return;
            }

            // ===== A grade por SALA (parcela 63) =====
            if (AgruparPorSala)
            {
                SoMinhaAgenda = false;
                SemSalas = salas.Count == 0;

                foreach (var s in salas)
                    Colunas.Add(MontarColuna(
                        null, s.Nome, dia, doDia.Where(a => a.SalaId == s.Id), salaId: s.Id));

                // "Sem sala" é o caso NORMAL nesta clínica, não resíduo: metade dos
                // horários não informa sala, e escondê-los faria a grade por sala parecer
                // um dia vazio. Ela só some quando de fato não há nenhum.
                var semSala = doDia.Where(a => a.SalaId is null).ToList();
                if (semSala.Count > 0)
                    Colunas.Add(MontarColuna(null, "Sem sala", dia, semSala));

                Resumo = $"{doDia.Count(a => a.OcupaAgenda)} horário(s) no dia · "
                         + $"{salas.Count} sala(s)";

                MontarGrade();
                await CarregarEsperaAsync(espera, geracao);
                return;
            }

            // Quem entrou como PROFISSIONAL abre na própria coluna. O usuário aponta
            // para o Profissional desde a parcela 5, e um fisioterapeuta que abre o app
            // quer ver a agenda DELE — não caçá-la entre seis colunas. O balcão (sem
            // profissional vinculado) continua vendo a clínica inteira.
            var meu = SessaoUsuario.Atual.ProfissionalId;
            SoMinhaAgenda = meu is not null && profissionais.Any(p => p.Id == meu);

            var visiveis = SoMinhaAgenda && !MostrarTodosOsProfissionais
                ? profissionais.Where(p => p.Id == meu).ToList()
                : profissionais;

            foreach (var p in visiveis)
                Colunas.Add(MontarColuna(
                    p.Id, p.Rotulo, dia, doDia.Where(a => a.ProfissionalId == p.Id)));

            // "Sem profissional" só aparece quando existe: é resíduo da agenda antiga
            // (e do faturamento, que marca sem informar quem atende), não uma pessoa.
            // Filtrando por "minha agenda", ele não é meu — fica de fora.
            var orfaos = doDia.Where(a => a.ProfissionalId is null).ToList();
            if (orfaos.Count > 0 && visiveis.Count == profissionais.Count)
                Colunas.Add(MontarColuna(null, "Sem profissional", dia, orfaos));

            var ocupando = visiveis.Count == profissionais.Count
                ? doDia.Count(a => a.OcupaAgenda)
                : doDia.Count(a => a.OcupaAgenda && a.ProfissionalId == meu);
            Resumo = $"{ocupando} horário(s) no dia · {Colunas.Count} coluna(s)";

            MontarGrade();
            await CarregarEsperaAsync(espera, geracao);
        }
        catch (Exception ex)
        {
            if (geracao != _geracaoCarga) return;

            Clinica.Application.Diagnostico.Registrar("Recepção — agenda do dia não pôde ser carregada", ex);

            // A releitura de fundo que falha não pinta a tela de vermelho: quem está com
            // um paciente à frente não pode levar um aviso de erro porque o banco demorou
            // uma vez. A grade segue com o que já tinha, e o log guarda o motivo.
            if (silencioso) return;

            NaoVerificado = true;
            Mensagem = $"Não foi possível carregar a agenda: {ex.Message}";
            MensagemEhErro = true;
        }
        finally
        {
            // A carga superada não apaga o "Carregando" da que ainda está no ar.
            if (!silencioso && geracao == _geracaoCarga) Carregando = false;
        }
    }

    /// <summary>
    /// A batida do relógio: relê a grade por baixo (parcela 62).
    ///
    /// A agenda é a única tela da suíte em que duas pessoas escrevem no MESMO dado ao
    /// mesmo tempo — o outro balcão marca, o profissional carimba a falta —, e até aqui
    /// ela só relia por clique. Um horário marcado na outra máquina continuava aparecendo
    /// vago nesta, e o vão vago é CLICÁVEL desde a parcela 58: a recepcionista marcaria
    /// por cima do paciente de quem já está sentado esperando.
    ///
    /// Três recusas, e cada uma evita a grade se mexer sozinha debaixo do olho de quem
    /// está lendo: só HOJE (quem planeja a terça que vem não tem nada correndo), só no
    /// modo DIA (a semana refaz as sete colunas com um await no meio de cada, e a grade
    /// piscaria) e nunca por cima de uma carga que já está no ar.
    /// </summary>
    private async Task ReconferirAsync()
    {
        if (Dia.Date != DateTime.Today || ModoSemana || Carregando) return;

        try
        {
            await CarregarAsync(silencioso: true);
        }
        catch (Exception ex)
        {
            Clinica.Application.Diagnostico.Registrar(
                "Recepção — releitura automática da agenda falhou", ex);
        }
    }

    /// <summary>Liga a releitura (chamada quando a tela entra em cena).</summary>
    public void IniciarRelogio() => _relogio.Start();

    /// <summary>Desliga a releitura (chamada quando a tela sai de cena).</summary>
    public void PararRelogio() => _relogio.Stop();

    /// <summary>
    /// A semana da data escolhida, uma coluna por dia (segunda a domingo).
    ///
    /// Começa na SEGUNDA e não no dia escolhido: a clínica pensa a semana em bloco, e
    /// uma grade que começasse na quarta faria o mesmo dia aparecer em posições
    /// diferentes conforme o dia em que se abre a tela.
    ///
    /// O filtro "só a minha agenda" continua valendo — quem entrou como profissional vê
    /// a semana DELE, que é a pergunta que ele faz.
    /// </summary>
    private async Task MontarSemanaAsync(
        AgendaService agenda, IReadOnlyList<Profissional> profissionais, int geracao)
    {
        var meu = SessaoUsuario.Atual.ProfissionalId;
        SoMinhaAgenda = meu is not null && profissionais.Any(p => p.Id == meu);
        var soMeu = SoMinhaAgenda && !MostrarTodosOsProfissionais;

        // O casting vem ANTES da conta: `DayOfWeek + 6` continua sendo DayOfWeek (soma de
        // enum com int devolve o enum), e o resto de divisão não existe para enum.
        var segunda = Dia.Date.AddDays(-(((int)Dia.DayOfWeek + 6) % 7));
        var ocupando = 0;

        // ⚠️ UMA consulta, não sete. `AgendaService.NoPeriodoAsync` existe para isto desde
        // sempre — o app CONGELADO de faturamento já a usava na visão de semana dele — e
        // era a agenda do balcão, a que se usa o dia inteiro, que chamava `DoDiaAsync` em
        // laço. Sete idas em fila indiana a um banco REMOTO deixavam a tela em "Montando a
        // agenda…" a cada clique nas setas, com o paciente esperando no balcão. O
        // agrupamento é feito aqui, em memória, sobre o período inteiro — é o mesmo
        // desenho que `ConsultorioService.DaSemanaAsync` já adotara, com o motivo escrito
        // ao lado.
        var daSemana = await agenda.NoPeriodoAsync(
            DateOnly.FromDateTime(segunda), DateOnly.FromDateTime(segunda.AddDays(6)));

        // Chegou tarde: outra carga mais nova já foi pedida — parar a montagem impede a
        // semana velha de terminar por cima da nova.
        if (geracao != _geracaoCarga) return;

        var porDia = daSemana
            .GroupBy(a => a.DataHora.Date)
            .ToDictionary(g => g.Key, g => (IReadOnlyList<Agendamento>)[.. g]);

        for (var i = 0; i < 7; i++)
        {
            var quando = segunda.AddDays(i);
            IReadOnlyList<Agendamento> doDia =
                porDia.TryGetValue(quando.Date, out var achados) ? achados : [];

            var recorte = soMeu
                ? doDia.Where(a => a.ProfissionalId == meu).ToList()
                : doDia.ToList();

            ocupando += recorte.Count(a => a.OcupaAgenda);

            // A coluna do dia não tem "um profissional": o cartão já diz de quem é, e
            // amarrá-la a alguém faria o botão de chamar da lista de espera oferecer o
            // profissional errado.
            Colunas.Add(MontarColuna(
                null,
                $"{Dias[i]} {quando:dd/MM}",
                DateOnly.FromDateTime(quando),
                recorte));
        }

        Resumo = $"{ocupando} horário(s) na semana de {segunda:dd/MM} a {segunda.AddDays(6):dd/MM}";
    }

    private static readonly string[] Dias = ["seg", "ter", "qua", "qui", "sex", "sáb", "dom"];

    private static ColunaAgenda MontarColuna(
        int? profissionalId, string nome, DateOnly data, IEnumerable<Agendamento> agendamentos,
        int? salaId = null)
    {
        var cartoes = new ObservableCollection<CartaoAgenda>();
        var ocupando = 0;

        foreach (var a in agendamentos.OrderBy(a => a.DataHora))
        {
            if (a.OcupaAgenda) ocupando++;
            cartoes.Add(new CartaoAgenda
            {
                AgendamentoId = a.Id,
                ProfissionalId = profissionalId ?? a.ProfissionalId,
                SerieId = a.SerieId,
                Faixa = $"{a.DataHora:HH:mm}–{a.FimPrevisto:HH:mm}",
                Paciente = a.Paciente?.Nome ?? "(paciente removido)",
                Telefone = a.Paciente?.Telefone,
                DataHora = a.DataHora,
                Fim = a.FimPrevisto,
                // Nome do CATÁLOGO, nunca o enum: `ToString()` escrevia
                    // "AcupunturaComEletro" no cartão que o médico lê (parcela 41).
                    Modalidade = CatalogoModalidades.Nome(
                        a.ModalidadeCodigo ?? a.ModalidadePrevista.ToString()),
                Sala = a.Sala?.Nome ?? "—",
                Profissional = a.Profissional?.Rotulo ?? "sem profissional",
                StatusRotulo = Rotular(a.Status),
                EhEncaixe = a.Encaixe,
                EhRetornoDoSegundoCodigo = a.Origem == OrigemAgendamento.RetornoSugerido,
                VeioDaListaEspera = a.Origem == OrigemAgendamento.ListaEspera,
                Observacoes = a.Observacoes,
                Lancamento = DescreverLancamento(a),
                EmAberto = a.Status == StatusAgendamento.Agendado,
                ForaDoDia = !a.OcupaAgenda
            });
        }

        return new ColunaAgenda
        {
            ProfissionalId = profissionalId,
            SalaId = salaId,
            Nome = nome,
            Data = data,
            Resumo = $"{ocupando} horário(s)",
            Horarios = cartoes
        };
    }

    /// <summary>
    /// "Marcado por Ana em 08/08/2026 14:32" — ou a frase que assume a lacuna.
    ///
    /// Horário anterior à parcela 58 não guarda quem o lançou, e deixar em branco faria a
    /// tela parecer que não conseguiu carregar. Dizer o que falta, e por quê, é a mesma
    /// regra do "não verificado" do painel.
    /// </summary>
    private static string DescreverLancamento(Agendamento a)
    {
        if (string.IsNullOrWhiteSpace(a.CriadoPor))
            return "Marcado antes de o sistema passar a registrar quem lança — sem autoria.";

        return a.CriadoEm is { } quando
            ? $"Marcado por {a.CriadoPor} em {quando:dd/MM/yyyy HH:mm}"
            : $"Marcado por {a.CriadoPor}";
    }

    /// <summary>
    /// Monta a linha do tempo a partir das colunas já montadas.
    ///
    /// A janela de horas é a padrão da clínica ESTICADA pelo que existe no dia: uma
    /// sessão às 6h30 puxa a grade para cima em vez de ficar de fora, e um dia inteiro
    /// dentro do horário comercial não gera faixa vazia nenhuma fora dele.
    ///
    /// Cada sessão ocupa a faixa em que COMEÇA e marca as seguintes como continuação até
    /// o fim previsto. Sem isso, uma sessão de uma hora deixaria a meia hora seguinte
    /// aparecendo como livre — e a recepção marcaria por cima.
    /// </summary>
    private void MontarGrade()
    {
        Faixas.Clear();
        QuantidadeColunas = Math.Max(1, Colunas.Count);
        LarguraGrade = 64 + 190.0 * QuantidadeColunas;

        if (Colunas.Count == 0) return;

        var todos = Colunas.SelectMany(c => c.Horarios).ToList();

        var inicio = AberturaPadrao;
        var fim = FechamentoPadrao;
        foreach (var c in todos)
        {
            var comeco = TimeOnly.FromDateTime(c.DataHora);
            var termino = TimeOnly.FromDateTime(c.Fim);
            if (comeco < inicio) inicio = comeco;
            // Sessão que atravessa a meia-noite não estica a grade para trás: o que
            // importa é o começo, e o fim é arredondado para o fecho do dia.
            if (termino > fim && termino > comeco) fim = termino;
        }

        var primeiro = Piso(inicio);
        var ultimo = Piso(fim);
        var agora = DateTime.Now;

        for (var minuto = primeiro; minuto <= ultimo; minuto += PassoMinutos)
        {
            var hora = new TimeOnly(minuto / 60, minuto % 60);
            var celulas = new ObservableCollection<CelulaAgenda>();

            foreach (var coluna in Colunas)
            {
                var quando = coluna.Data.ToDateTime(hora);
                var naFaixa = new ObservableCollection<CartaoAgenda>(
                    coluna.Horarios.Where(h => Piso(TimeOnly.FromDateTime(h.DataHora)) == minuto));

                // Continuação: alguém que começou ANTES desta faixa e ainda não terminou.
                // Cancelado e falta não cobrem nada — o horário voltou a ficar livre.
                var coberta = naFaixa.Count == 0 && coluna.Horarios.Any(h =>
                    !h.ForaDoDia
                    && Piso(TimeOnly.FromDateTime(h.DataHora)) < minuto
                    && h.Fim > quando);

                celulas.Add(new CelulaAgenda
                {
                    ProfissionalId = coluna.ProfissionalId,
                    SalaId = coluna.SalaId,
                    Quando = quando,
                    Cartoes = naFaixa,
                    Continuacao = coberta,
                    NoPassado = quando < agora,
                    Bloqueio = BloqueioDe(quando, coluna.ProfissionalId, coluna.SalaId)
                });
            }

            Faixas.Add(new FaixaAgenda
            {
                Hora = hora,
                Rotulo = hora.Minute == 0 ? hora.ToString("HH:mm") : string.Empty,
                HoraCheia = hora.Minute == 0,
                Celulas = celulas
            });
        }
    }

    /// <summary>
    /// Os fechamentos que alcançam o que está na tela (parcela 63).
    ///
    /// O período é o do MODO: um dia, ou a semana inteira quando a grade está por dia da
    /// semana. Falhar aqui NÃO derruba a agenda — a grade continua desenhada e sem os
    /// avisos de fechamento, que é exatamente o comportamento de antes desta parcela. O
    /// que não pode é passar calado: sem a linha no log, a clínica acreditaria que não há
    /// férias marcadas.
    /// </summary>
    private async Task CarregarBloqueiosAsync(IServiceScope scope, int geracao)
    {
        try
        {
            var bloqueios = scope.ServiceProvider.GetRequiredService<BloqueioAgendaService>();

            var (inicio, fim) = ModoSemana
                ? (SegundaDa(Dia), SegundaDa(Dia).AddDays(7))
                : (Dia.Date, Dia.Date.AddDays(1));

            var lista = await bloqueios.NoPeriodoAsync(inicio, fim);
            if (geracao != _geracaoCarga) return;

            _bloqueios = lista;
        }
        catch (Exception ex)
        {
            if (geracao != _geracaoCarga) return;

            _bloqueios = [];
            Clinica.Application.Diagnostico.Registrar(
                "Recepção — bloqueios da agenda não puderam ser lidos", ex);
        }
    }

    /// <summary>A segunda-feira da semana da data — a mesma conta de <c>MontarSemanaAsync</c>.</summary>
    private static DateTime SegundaDa(DateTime data)
        => data.Date.AddDays(-(((int)data.DayOfWeek + 6) % 7));

    /// <summary>
    /// O fechamento que alcança este vão, ou nulo.
    ///
    /// A pergunta é feita com o recurso da COLUNA: na grade por profissional, o bloqueio
    /// da sala 2 não fecha o vão da Ana — ela pode atender noutra sala. O da clínica
    /// inteira fecha todos, nas três visões.
    /// </summary>
    private string? BloqueioDe(DateTime inicio, int? profissionalId, int? salaId)
    {
        if (_bloqueios.Count == 0) return null;

        var fim = inicio.AddMinutes(PassoMinutos);

        return _bloqueios.FirstOrDefault(
            b => b.ColideCom(inicio, fim) && b.AlcancaRecurso(profissionalId, salaId))?.Motivo;
    }

    /// <summary>Minuto do dia arredondado para BAIXO no passo da grade.</summary>
    private static int Piso(TimeOnly hora)
    {
        var minutos = hora.Hour * 60 + hora.Minute;
        return minutos - minutos % PassoMinutos;
    }

    /// <summary>
    /// A lista de espera do painel lateral. Quando um horário acabou de vagar, ela deixa
    /// de ser "todo mundo que espera" e passa a ser **quem serve para AQUELE horário** —
    /// respeitando a janela de datas, o turno e o profissional que cada pedido escolheu.
    ///
    /// <c>CandidatosParaAsync</c> existia desde a parcela 1 e nenhuma tela o chamava: a
    /// recepção via a lista inteira e tinha de cruzar preferência por preferência de
    /// cabeça, justamente no minuto em que o telefone precisa ser atendido.
    /// </summary>
    private async Task CarregarEsperaAsync(ListaEsperaService espera, int geracao)
    {
        var pedidos = SugestaoPara is { } horario
            ? await espera.CandidatosParaAsync(horario, SugestaoProfissionalId)
            : await espera.AguardandoAsync();

        // Chegou tarde: outra carga mais nova já foi pedida.
        if (geracao != _geracaoCarga) return;

        EsperaVazia = SugestaoPara is { } alvo
            ? $"Ninguém da lista serve para {alvo:dd/MM HH:mm} — turno, janela de datas ou "
              + "profissional pedido não batem."
            : "Ninguém na lista de espera.";

        Espera.Clear();
        foreach (var p in pedidos)
            Espera.Add(new LinhaListaEspera
            {
                PedidoId = p.Id,
                Paciente = p.Paciente?.Nome ?? "(paciente removido)",
                Preferencias = DescreverPreferencias(p),
                Desde = $"na lista desde {p.CriadoEm:dd/MM}",
                Prioritario = p.Prioritario,
                Observacoes = p.Observacoes
            });

        OnPropertyChanged(nameof(EsperaResumo));
    }

    private static string DescreverPreferencias(ListaEspera p)
    {
        var partes = new List<string>
        {
            p.Profissional?.Rotulo ?? "qualquer profissional",
            p.Periodo switch
            {
                PeriodoPreferido.Manha => "manhã",
                PeriodoPreferido.Tarde => "tarde",
                _ => "qualquer turno"
            }
        };

        if (p.DisponivelDe is not null || p.DisponivelAte is not null)
            partes.Add($"{p.DisponivelDe?.ToString("dd/MM") ?? "hoje"}"
                       + $" a {p.DisponivelAte?.ToString("dd/MM") ?? "sem limite"}");

        return string.Join(" · ", partes);
    }

    // ==================== Comandos ====================

    /// <summary>Abre o formulário de um horário novo.</summary>
    [RelayCommand]
    private async Task NovoHorarioAsync()
    {
        SessaoUsuario.Atual.Exigir(Permissao.EditarAgenda, "mexer na agenda");

        await AbrirFormularioAsync(new AgendamentoEdicaoViewModel(_escopos)
        {
            Data = Dia
        });
    }

    /// <summary>
    /// Clique num vão da grade: abre o formulário JÁ NA HORA e NA COLUNA clicadas.
    ///
    /// É o gesto que a agenda existe para ter — a recepção vê o buraco das 14h30 do Dr.
    /// Fulano e marca ali, em vez de abrir um formulário em branco e redigitar a hora e o
    /// profissional que ela acabou de apontar com o dedo. Redigitar é onde a hora sai
    /// errada.
    /// </summary>
    [RelayCommand]
    private async Task AgendarNaFaixaAsync(CelulaAgenda? celula)
    {
        if (celula is null) return;

        SessaoUsuario.Atual.Exigir(Permissao.EditarAgenda, "mexer na agenda");

        // Guarda com VOZ (a lição da parcela 41): o vão fechado é clicável no XAML apenas
        // para poder dizer por que não dá — deixá-lo inerte faria a pessoa clicar duas ou
        // três vezes concluindo que a tela travou.
        if (celula.Bloqueio is { } motivo)
        {
            Mensagem = $"A agenda está fechada neste horário: {motivo}. "
                       + "Para marcar mesmo assim, remova ou encurte o bloqueio em Profissionais e salas.";
            MensagemEhErro = true;
            return;
        }

        await AbrirFormularioAsync(new AgendamentoEdicaoViewModel(_escopos)
        {
            Data = celula.Quando.Date,
            Hora = celula.Quando.ToString("HH:mm"),
            ProfissionalPreferidoId = celula.ProfissionalId,
            SalaPreferidaId = celula.SalaId
        });
    }

    /// <summary>
    /// Abre o horário: tudo o que ele é, e tudo o que dá para fazer com ele.
    ///
    /// Os sete botões moravam DENTRO do cartão. Numa grade em que uma faixa de meia hora
    /// tem ~46 px, isso não cabe — e nunca coube bem: sete botões por cartão em seis
    /// colunas são mais de quarenta botões na tela, e o olho para de distinguir o que é
    /// frequente do que é raro. Aqui eles ficam legíveis, com o nome inteiro do paciente,
    /// o telefone, a observação e QUEM MARCOU o horário — que era o pedido da direção e
    /// não tinha onde caber no cartão.
    /// </summary>
    [RelayCommand]
    private async Task AbrirHorarioAsync(CartaoAgenda? cartao)
    {
        if (cartao is null) return;

        var janela = new Janelas.DetalheHorarioWindow(cartao)
        {
            Owner = Dono()
        };

        janela.ShowDialog();

        // A janela não executa nada: ela devolve a INTENÇÃO, e quem age é esta tela.
        // Assim as sete ações continuam com um dono só — a regra de permissão, o
        // recarregamento e o tratamento de erro não ganham uma segunda cópia.
        switch (janela.Acao)
        {
            case Janelas.AcaoHorario.Remarcar: await RemarcarAsync(cartao); break;
            case Janelas.AcaoHorario.Confirmar: Confirmar(cartao); break;
            case Janelas.AcaoHorario.Falta: await MarcarFaltaAsync(cartao); break;
            case Janelas.AcaoHorario.Cancelar: await CancelarAsync(cartao); break;
            case Janelas.AcaoHorario.QuemChamar: await QuemChamarAsync(cartao); break;
            case Janelas.AcaoHorario.Comprovante: await ComprovanteAsync(cartao); break;
            case Janelas.AcaoHorario.CancelarSerie: await CancelarSerieAsync(cartao); break;
        }
    }

    /// <summary>Remarca: move o horário preservando o registro (e o histórico).</summary>
    [RelayCommand]
    private async Task RemarcarAsync(CartaoAgenda? cartao)
    {
        if (cartao is null) return;

        SessaoUsuario.Atual.Exigir(Permissao.EditarAgenda, "mexer na agenda");
        await AbrirFormularioAsync(new AgendamentoEdicaoViewModel(_escopos, cartao.AgendamentoId));
    }

    /// <summary>Chama alguém da lista de espera para um horário que vagou.</summary>
    [RelayCommand]
    private async Task ChamarDaEsperaAsync(LinhaListaEspera? linha)
    {
        if (linha is null) return;

        SessaoUsuario.Atual.Exigir(Permissao.EditarAgenda, "mexer na agenda");

        var vm = new AgendamentoEdicaoViewModel(_escopos)
        {
            Data = SugestaoPara?.Date ?? Dia,
            Titulo = $"Chamar {linha.Paciente} da lista de espera",
            PedidoListaEsperaId = linha.PedidoId
        };

        // Quando a lista está apontada para um horário, o formulário já abre nele: é o
        // horário que a recepção acabou de ver vagar, e redigitá-lo é o passo em que a
        // hora sai errada.
        if (SugestaoPara is { } horario) vm.Hora = horario.ToString("HH:mm");

        await AbrirFormularioAsync(vm);
    }

    /// <summary>
    /// Aponta a lista de espera para um horário: quem serve para ELE, na ordem de chamada.
    ///
    /// O primeiro da lista é o primeiro a ligar. Quem não escolheu profissional serve
    /// para qualquer coluna; quem escolheu só aparece na dele.
    /// </summary>
    [RelayCommand]
    private async Task QuemChamarAsync(CartaoAgenda? cartao)
    {
        if (cartao is null) return;

        SugestaoPara = cartao.DataHora;
        SugestaoProfissionalId = cartao.ProfissionalId;

        await CarregarAsync();

        // Lista vazia aqui é resposta, não falha: ninguém que espera serve para este
        // horário, e a recepção precisa saber disso antes de começar a ligar. Ela é dita
        // AQUI, sem abrir uma janela para mostrar uma lista vazia.
        if (Espera.Count == 0)
        {
            Mensagem = $"Ninguém da lista de espera serve para {cartao.DataHora:dd/MM HH:mm} "
                       + "(turno, janela de datas ou profissional pedido).";
            MensagemEhErro = false;
            return;
        }

        await AbrirEsperaAsync();
    }

    /// <summary>
    /// Abre a lista de espera.
    ///
    /// Ela morava numa FAIXA LATERAL de 320 px, permanente, ao lado da agenda — o padrão
    /// que o `README.md` proíbe desde a parcela 37 e que a parcela 49 tirou de cinco telas
    /// do Financeiro. Consultar quem está esperando é o que se faz QUANDO UM HORÁRIO VAGA,
    /// algumas vezes por dia; a agenda é o que se olha o tempo todo. Os 320 px eram um
    /// quinto da tela do balcão gastos com a pergunta menos frequente das duas.
    ///
    /// O botão carrega a contagem e diz quando a lista está FILTRADA por um horário —
    /// filtro invisível é filtro que engana, e agora ele está atrás de um clique.
    /// </summary>
    [RelayCommand]
    private async Task AbrirEsperaAsync()
    {
        var janela = new Janelas.ListaEsperaPainelWindow(this) { Owner = Dono() };
        janela.ShowDialog();
        await CarregarAsync();
    }

    /// <summary>
    /// FECHAR A AGENDA — férias, feriado, folga (parcela 62 dando a porta que faltava).
    ///
    /// O bloqueio existe desde a parcela 26 e a única porta estava em "Profissionais e
    /// salas", item que exige <c>GerenciarEquipe</c> — bit que o perfil Recepção NÃO tem.
    /// Ou seja: a feature era vendida como do balcão (`features-por-modulo.md`) e o balcão
    /// não a alcançava. É o defeito recorrente do projeto na variante "a porta está atrás
    /// de uma permissão de outro assunto".
    ///
    /// Fechar a agenda é ato de AGENDA, não de cadastro de equipe: quem marca e desmarca
    /// horário é quem sabe que a médica entrou de férias. Por isso <c>EditarAgenda</c>, o
    /// mesmo bit do "Novo horário" ao lado — e a tela de Equipe continua com a LISTA dos
    /// bloqueios (reabrir, empurrar em lote), que é conferência da direção.
    ///
    /// Bloquear NÃO desmarca ninguém: quem já estava marcado continua, e é por isso que o
    /// aviso do que caiu dentro do período é a metade que importa deste comando.
    /// </summary>
    [RelayCommand]
    private async Task FecharAgendaAsync()
    {
        SessaoUsuario.Atual.Exigir(Permissao.EditarAgenda, "fechar a agenda");

        var vm = new ViewModels.BloqueioEdicaoViewModel(_escopos);
        var janela = new Janelas.BloqueioWindow(vm) { Owner = Dono() };
        if (janela.ShowDialog() != true) return;

        if (vm.MarcadosDentro.Count > 0)
            _dialogo.Aviso("Agenda fechada — mas já havia sessão marcada",
                $"{vm.MarcadosDentro.Count} sessão(ões) estão marcadas dentro do período fechado. "
                + "Elas continuam na agenda: remarque com o paciente.\n\n"
                + string.Join("\n", vm.MarcadosDentro)
                + "\n\nPara empurrar todas de uma vez, use Profissionais e salas → Empurrar.");
        else
            _snackbar.Sucesso("Agenda fechada no período.");

        await CarregarAsync();
    }

    /// <summary>
    /// O rótulo do botão da lista — com a contagem e, quando há foco, o horário.
    ///
    /// "Lista de espera" sozinho não faz ninguém abrir; "Quem chamar para 14:00 (2)"
    /// responde a pergunta do minuto seguinte a um cancelamento antes do clique.
    /// </summary>
    public string EsperaResumo => SugestaoPara is { } horario
        ? $"Quem chamar para {horario:HH:mm} ({Espera.Count})"
        : $"Lista de espera ({Espera.Count})";

    /// <summary>
    /// A janela por cima da qual abrir a próxima. `MainWindow` sempre seria errado com a
    /// lista de espera aberta: o formulário nasceria ATRÁS dela, e a recepção concluiria
    /// que o clique não fez nada.
    /// </summary>
    private static System.Windows.Window? Dono()
        => System.Windows.Application.Current?.Windows.OfType<System.Windows.Window>()
               .FirstOrDefault(w => w.IsActive)
           ?? System.Windows.Application.Current?.MainWindow;

    /// <summary>Tira o foco do horário e volta a mostrar todo mundo que espera.</summary>
    [RelayCommand]
    private async Task VerListaInteiraAsync()
    {
        SugestaoPara = null;
        SugestaoProfissionalId = null;
        await CarregarAsync();
    }

    /// <summary>
    /// A rodada de confirmação das sessões de amanhã.
    ///
    /// A capacidade existia desde a parcela 5 e morava só no Gerente Geral — mas quem
    /// confirma as sessões de amanhã é quem está no balcão. É a mesma rodada, o mesmo
    /// serviço e a mesma chave de idempotência: rodar aqui e lá no mesmo dia não manda
    /// duas mensagens para o mesmo paciente.
    /// </summary>
    [RelayCommand]
    private async Task ConfirmarSessoesAsync()
    {
        var vm = new ConfirmacoesViewModel(_escopos);
        var janela = new Janelas.ConfirmacoesWindow(vm)
        {
            Owner = Dono()
        };

        janela.ShowDialog();

        // A confirmação não muda a agenda do dia aberto, mas pode ter registrado
        // resposta de quem vem amanhã — recarregar mantém a tela honesta.
        await CarregarAsync();
    }

    /// <summary>
    /// Cancela o que ainda está marcado da série — o "o paciente desistiu no meio do
    /// tratamento". Sessão já atendida não é tocada: ela é fato, e apagá-la da agenda
    /// esconderia um atendimento que aconteceu.
    /// </summary>
    [RelayCommand]
    private async Task CancelarSerieAsync(CartaoAgenda? cartao)
    {
        if (cartao?.SerieId is not { } serieId) return;

        SessaoUsuario.Atual.Exigir(Permissao.EditarAgenda, "mexer na agenda");

        // ⚠️ A PRÉVIA vem antes da pergunta. O diálogo dizia "cancelar TODAS as sessões
        // ainda marcadas?" sem dizer QUANTAS nem QUAIS — a contagem só aparecia no
        // snackbar, DEPOIS do estrago, e a leitura que responde isso (`DaSerieAsync`,
        // "sessões de uma série, na ordem em que acontecem") existia sem um único
        // chamador. Ação destrutiva em lote confirmada às cegas, com a prévia pronta e
        // sem porta — a mesma família do defeito recorrente do projeto.
        IReadOnlyList<Domain.Entities.Agendamento> emAberto;
        try
        {
            using var escopo = _escopos.CreateScope();
            var agenda = escopo.ServiceProvider.GetRequiredService<AgendaService>();
            emAberto = (await agenda.DaSerieAsync(serieId))
                .Where(a => a.Status == Domain.Entities.StatusAgendamento.Agendado)
                .ToList();
        }
        catch (Exception ex)
        {
            // Sem a prévia não se pergunta: confirmar "todas" sem saber quantas é
            // exatamente o que esta correção existe para acabar — e se a leitura falhou,
            // o cancelamento em seguida falharia igual.
            Clinica.Application.Diagnostico.Registrar(
                "Recepção — série não pôde ser lida para a prévia do cancelamento", ex);
            Mensagem = $"Não foi possível ler a série para confirmar: {ex.Message}";
            MensagemEhErro = true;
            return;
        }

        if (emAberto.Count == 0)
        {
            // Guarda que FALA (parcela 41): série sem sessão em aberto não tem o que
            // cancelar, e sair calado seria botão que não faz nada.
            _dialogo.Aviso("Nada a cancelar",
                $"A série de {cartao.Paciente} não tem sessão em aberto — "
                + "as que existiram já foram atendidas, canceladas ou viraram falta.");
            return;
        }

        // As datas por extenso, até dez; acima disso a lista diz quantas ficaram de fora
        // em vez de virar um diálogo de rolagem.
        var datas = emAberto.Take(10).Select(a => a.DataHora.ToString("dd/MM/yyyy HH:mm"));
        var lista = string.Join("\n", datas)
            + (emAberto.Count > 10 ? $"\n… e mais {emAberto.Count - 10}." : string.Empty);

        if (!_dialogo.ConfirmarPerigo("Cancelar a série",
                $"Cancelar as {emAberto.Count} sessão(ões) ainda marcadas de {cartao.Paciente}?\n\n"
                + lista
                + "\n\nAs que já foram atendidas continuam no histórico.")) return;

        await ExecutarAsync(async scope =>
        {
            var agenda = scope.ServiceProvider.GetRequiredService<AgendaService>();
            var quantas = await agenda.CancelarSerieAsync(serieId, SessaoUsuario.Atual.Operador);
            _snackbar.Info($"{quantas} sessão(ões) da série canceladas.");
        }, "cancelamento da série");
    }

    /// <summary>Coloca um paciente na lista de espera.</summary>
    [RelayCommand]
    private async Task NovoPedidoEsperaAsync()
    {
        SessaoUsuario.Atual.Exigir(Permissao.EditarAgenda, "mexer na agenda");

        var vm = new ListaEsperaEdicaoViewModel(_escopos);
        var janela = new Janelas.ListaEsperaWindow(vm)
        {
            Owner = Dono()
        };

        if (janela.ShowDialog() != true) return;
        _snackbar.Sucesso("Paciente entrou na lista de espera.");
        await CarregarAsync();
    }

    /// <summary>Tira o pedido da lista sem agendar (desistiu, resolveu em outro lugar).</summary>
    [RelayCommand]
    private async Task RemoverDaEsperaAsync(LinhaListaEspera? linha)
    {
        if (linha is null) return;

        SessaoUsuario.Atual.Exigir(Permissao.EditarAgenda, "mexer na agenda");

        var motivo = _dialogo.PerguntarTexto(
            "Sair da lista de espera",
            $"Por que {linha.Paciente} está saindo da lista? Fica registrado no pedido.");
        if (string.IsNullOrWhiteSpace(motivo)) return;

        await ExecutarAsync(async scope =>
        {
            var espera = scope.ServiceProvider.GetRequiredService<ListaEsperaService>();
            await espera.RemoverAsync(linha.PedidoId, motivo);
            _snackbar.Info($"{linha.Paciente} saiu da lista de espera.");
        }, "saída da lista de espera");
    }

    [RelayCommand]
    private async Task CancelarAsync(CartaoAgenda? cartao)
    {
        if (cartao is null) return;

        SessaoUsuario.Atual.Exigir(Permissao.EditarAgenda, "mexer na agenda");
        if (!_dialogo.Confirmar("Cancelar horário",
                $"Cancelar o horário de {cartao.Paciente} ({cartao.Faixa})?")) return;

        await ExecutarAsync(async scope =>
        {
            var agenda = scope.ServiceProvider.GetRequiredService<AgendaService>();
            await agenda.CancelarAsync(cartao.AgendamentoId, SessaoUsuario.Atual.Operador);

            // O horário acabou de vagar: a pergunta seguinte é sempre "quem eu chamo?",
            // e a lista já responde antes de alguém precisar perguntar.
            ApontarEsperaPara(cartao);
            _snackbar.Info("Horário cancelado.");
        }, "cancelamento do horário");
    }

    [RelayCommand]
    private async Task MarcarFaltaAsync(CartaoAgenda? cartao)
    {
        if (cartao is null) return;

        SessaoUsuario.Atual.Exigir(Permissao.EditarAgenda, "mexer na agenda");

        await ExecutarAsync(async scope =>
        {
            var agenda = scope.ServiceProvider.GetRequiredService<AgendaService>();
            await agenda.MarcarFaltaAsync(cartao.AgendamentoId, SessaoUsuario.Atual.Operador);

            ApontarEsperaPara(cartao);
            _snackbar.Info($"{cartao.Paciente} marcado como falta.");
        }, "marcação de falta");
    }

    /// <summary>
    /// Aponta a lista de espera para o horário que este cartão ocupava.
    ///
    /// Só vale para horário de HOJE em diante: sugerir quem chamar para uma falta de
    /// terça-feira passada seria oferecer um horário que já não existe.
    /// </summary>
    private void ApontarEsperaPara(CartaoAgenda cartao)
    {
        if (cartao.DataHora < DateTime.Now) return;

        SugestaoPara = cartao.DataHora;
        SugestaoProfissionalId = cartao.ProfissionalId;
    }

    /// <summary>
    /// Confirma a sessão pelo WhatsApp. Falta é sessão não faturada — confirmar na
    /// véspera é a rotina que mais evita buraco na agenda.
    /// </summary>
    [RelayCommand]
    private void Confirmar(CartaoAgenda? cartao)
    {
        if (cartao is null) return;

        SessaoUsuario.Atual.Exigir(Permissao.EditarAgenda, "mexer na agenda");

        var erro = Whatsapp.Abrir(
            cartao.Telefone, cartao.Paciente,
            Whatsapp.ConfirmacaoDeSessao(cartao.Paciente, cartao.DataHora));

        if (erro is null) return;
        Mensagem = erro;
        MensagemEhErro = true;
    }

    private async Task AbrirFormularioAsync(AgendamentoEdicaoViewModel vm)
    {
        var janela = new Janelas.AgendamentoWindow(vm)
        {
            Owner = Dono()
        };

        if (janela.ShowDialog() != true) return;

        // Marcou: o horário deixou de estar vago, e a lista volta a ser a lista inteira.
        SugestaoPara = null;
        SugestaoProfissionalId = null;

        _snackbar.Sucesso("Agenda atualizada.");
        await CarregarAsync();
    }

    private async Task ExecutarAsync(Func<IServiceScope, Task> acao, string contexto)
    {
        try
        {
            using var scope = _escopos.CreateScope();
            await acao(scope);
            await CarregarAsync();
        }
        catch (Exception ex)
        {
            Clinica.Application.Diagnostico.Registrar($"Recepção — falha em: {contexto}", ex);
            Mensagem = ex.Message;
            MensagemEhErro = true;
        }
    }

    // Em modo semana os botões andam de SETE em sete: avançar um dia numa grade que
    // mostra a semana inteira não mudaria quase nada na tela.
    [RelayCommand]
    private void DiaAnterior() => Dia = Dia.AddDays(ModoSemana ? -7 : -1);

    [RelayCommand]
    private void ProximoDia() => Dia = Dia.AddDays(ModoSemana ? 7 : 1);

    [RelayCommand]
    private void Hoje() => Dia = DateTime.Today;

    /// <summary>
    /// A folha do dia, que o profissional leva para a sala.
    ///
    /// Toda clínica imprime essa folha; nesta, a secretária copiava a agenda num papel de
    /// bloco. Ela sai SEM dado clínico de propósito: circula pela sala e pela recepção, e
    /// nome, horário, profissional e sala bastam para conduzir o dia.
    /// </summary>
    [RelayCommand]
    private async Task ImprimirAgendaAsync()
    {
        try
        {
            byte[] pdf;
            using (var scope = _escopos.CreateScope())
            {
                var pdfs = scope.ServiceProvider.GetRequiredService<AgendaPdfService>();
                var parametros = scope.ServiceProvider.GetRequiredService<ParametrosService>();
                pdf = await pdfs.AgendaDoDiaAsync(
                    DateOnly.FromDateTime(Dia),
                    // Respeita o filtro da tela: quem está vendo só a própria agenda
                    // espera imprimir só a própria agenda.
                    SoMinhaAgenda && !MostrarTodosOsProfissionais
                        ? SessaoUsuario.Atual.ProfissionalId
                        : null,
                    await parametros.ObterPrestadorAsync());
            }

            var erro = await ImpressaoPdf.SalvarEAbrirAsync(
                pdf, ImpressaoPdf.NomeSeguro($"agenda-{Dia:yyyy-MM-dd}.pdf"));

            if (erro is not null)
            {
                Mensagem = erro;
                MensagemEhErro = true;
            }
        }
        catch (Exception ex)
        {
            Clinica.Application.Diagnostico.Registrar(
                "Recepção — agenda do dia não pôde ser impressa", ex);
            Mensagem = ex.Message;
            MensagemEhErro = true;
        }
    }

    /// <summary>
    /// O comprovante que o paciente leva do balcão. Sem valor e sem diagnóstico: ele
    /// existe para o horário não ser esquecido, e papel avulso não é lugar de dado
    /// clínico.
    /// </summary>
    [RelayCommand]
    private async Task ComprovanteAsync(CartaoAgenda? cartao)
    {
        if (cartao is null) return;

        try
        {
            byte[] pdf;
            using (var scope = _escopos.CreateScope())
            {
                var pdfs = scope.ServiceProvider.GetRequiredService<AgendaPdfService>();
                var parametros = scope.ServiceProvider.GetRequiredService<ParametrosService>();
                pdf = await pdfs.ComprovanteAsync(
                    cartao.AgendamentoId, await parametros.ObterPrestadorAsync());
            }

            var erro = await ImpressaoPdf.SalvarEAbrirAsync(
                pdf,
                ImpressaoPdf.NomeSeguro($"comprovante-{cartao.Paciente}-{Dia:yyyy-MM-dd}.pdf"));

            if (erro is not null)
            {
                Mensagem = erro;
                MensagemEhErro = true;
            }
        }
        catch (Exception ex)
        {
            Clinica.Application.Diagnostico.Registrar(
                "Recepção — comprovante não pôde ser gerado", ex);
            Mensagem = ex.Message;
            MensagemEhErro = true;
        }
    }

    private static string Rotular(StatusAgendamento status) => status switch
    {
        StatusAgendamento.Agendado => "Marcado",
        StatusAgendamento.Realizado => "Atendido",
        StatusAgendamento.Cancelado => "Cancelado",
        StatusAgendamento.Faltou => "Faltou",
        _ => status.ToString()
    };
}
