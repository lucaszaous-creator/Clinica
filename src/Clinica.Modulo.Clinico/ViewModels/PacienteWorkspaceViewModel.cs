using System.ComponentModel;
using System.Windows.Data;
using System.Windows.Threading;
using Clinica.Application.Modelos;
using Clinica.Application.Servicos;
using Clinica.Clinico.Modulo;
using Clinica.Desktop.Controls;
using Clinica.Desktop.Shell.Componentes;
using Clinica.Desktop.Shell.Modulos;
using Clinica.Domain;
using Clinica.Domain.Entities;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;

namespace Clinica.Clinico.ViewModels;

/// <summary>
/// A TELA DO PACIENTE — uma pessoa, um rail de seções (parcela 37, 4ª rodada).
///
/// O vício que ela corrige
/// -----------------------
/// As cinco telas clínicas tinham, cada uma, uma coluna de 300 px com a lista de pacientes
/// grudada à esquerda. Era mestre-detalhe espremido numa tela só, repetido seis vezes — e o
/// resultado é o que o cliente viu: metade da largura útil gasta com a MESMA lista, em toda
/// tela, inclusive quando o paciente já estava escolhido havia vinte minutos.
///
/// O desenho certo é o de qualquer prontuário eletrônico sério, e são dois passos:
/// <list type="number">
///   <item><b>Uma lista</b> — "Meu dia" (quem tem horário hoje) ou "Pacientes" (quem a
///   clínica vem atendendo). São as duas portas de entrada, e são telas de verdade, com a
///   largura inteira.</item>
///   <item><b>A tela do paciente</b> — clicou, entrou. A identidade fica no topo, as
///   seções viram um RAIL à esquerda, e a largura inteira é do conteúdo clínico.</item>
/// </list>
///
/// Por que seções, e não itens de menu
/// -----------------------------------
/// Porque elas só existem COM paciente. Item de menu que só funciona depois de você ter
/// passado por outro lugar é item que ensina o usuário a errar — e era exatamente o que a
/// versão anterior fazia, abrindo "Medidas" numa tela em branco pedindo que se buscasse
/// alguém. Aba, ao contrário, diz sozinha que pertence à pessoa cujo nome está logo acima.
///
/// As chaves antigas continuam válidas e caem AQUI, cada uma na sua seção: o painel da
/// direção e a fila do dia navegam por elas, e renomear contrato de navegação para arrumar
/// leiaute quebraria o que funciona em outro módulo.
/// </summary>
public sealed partial class PacienteWorkspaceViewModel : ObservableObject
{
    private readonly PacienteEmFoco _foco;
    private readonly IServiceScopeFactory _escopos;
    private readonly IDialogoService _dialogo;

    /// <summary>
    /// O relógio da barra de atendimento. Ele NÃO lê o banco — recalcula a frase a partir
    /// do carimbo que já está na memória —, então bater a cada 15 s não custa nada e é o
    /// que faz o minuto virar na tela sem parecer travado. É diferente das releituras
    /// silenciosas do balcão, que vão ao banco e por isso batem a cada minuto ou dois.
    ///
    /// ⚠️ Quem o liga e desliga é a VIEW (Loaded/Unloaded), como o quadro do "Meu dia" desde
    /// a parcela 38 — e não é conforto: o shell constrói uma tela nova a cada navegação, e um
    /// timer ligado mantém viva a ViewModel que o criou, junto com as SETE sub-ViewModels
    /// dela. Num turno de vinte pacientes seriam vinte workspaces abandonados batendo.
    /// </summary>
    private readonly DispatcherTimer _relogio = new() { Interval = TimeSpan.FromSeconds(15) };

    /// <summary>
    /// A tela está montada. Sem isto, <see cref="DescreverSessao"/> religaria o relógio de uma
    /// ViewModel já abandonada na primeira vez que ela recalculasse a frase.
    /// </summary>
    private bool _naTela;

    private Agendamento? _horario;

    /// <summary>
    /// As seções do rail, com o grupo de cada uma. Vem de <c>ModuloClinico.RailDoPaciente</c>
    /// — a MESMA lista que resolve o índice de navegação de outros módulos, e é por isso
    /// que o rótulo lido pelo usuário não tem como divergir dele.
    /// </summary>
    public IReadOnlyList<ItemDoRail> Secoes { get; }

    /// <summary>
    /// Uma linha do rail: a seção e se ela está ESCONDIDA para quem está logado (parcela
    /// 95 — a seção de escrita do outro lado some: a enfermeira não vê o S-O-A-P, o
    /// médico não vê a passagem; ver <c>PerfisAcesso.SecoesDeEscritaDoPosto</c>).
    ///
    /// ⚠️ Esconder é COLAPSAR o item, nunca tirá-lo da lista: o `SelectedIndex` do rail é
    /// o índice desta lista e a régua do TabControl ao lado — filtrar a vista deslocaria
    /// os índices e o clique abriria a seção do vizinho, sem erro nenhum (a regressão
    /// que a checagem 38 vigia). A seção continua existindo e continua navegável por
    /// chave; só não ocupa linha.
    /// </summary>
    public sealed record ItemDoRail(string Rotulo, string Grupo, bool Oculta);

    /// <summary>
    /// As mesmas seções, agrupadas para o rail desenhar os três cabeçalhos.
    ///
    /// ⚠️ UMA coleção agrupada, nunca uma ListBox por grupo: duas ListBox amarradas à mesma
    /// seleção se limpam mutuamente (a lição da parcela 37). E como a lista já vem NA ORDEM
    /// dos grupos, o índice da vista continua sendo o índice de <see cref="Secoes"/> — que é
    /// a régua do <c>SelectedIndex</c> e do TabControl ao lado.
    ///
    /// ⚠️ Montada aqui e não como `CollectionViewSource` no XAML: lá ela dependeria de o
    /// recurso herdar o DataContext, e o modo de falhar seria o rail inteiro em branco.
    /// </summary>
    public ICollectionView SecoesAgrupadas { get; }

    public AtendimentoViewModel Atendimento { get; }

    /// <summary>
    /// O ATENDIMENTO DE ENFERMAGEM (parcela 88) — a seção de escrita do lado Y.
    ///
    /// Vem logo depois do Atendimento porque é a segunda coisa que acontece com o paciente
    /// no mesmo dia, e porque as duas respondem à mesma pergunta ("o que aconteceu nesta
    /// sessão?") por dois conselhos diferentes. Fica VISÍVEL para quem lê o prontuário, e
    /// não só para quem escreve nela: quem consulta precisa da pressão que a técnica
    /// aferiu vinte minutos antes, e quem infunde precisa da conduta da consulta de hoje —
    /// é a metade XY da parcela 72.
    /// </summary>
    public AtendimentoEnfermagemViewModel Enfermagem { get; }

    /// <summary>
    /// A ANAMNESE do paciente (parcela 75) — o que se pergunta uma vez e se revisa.
    ///
    /// Vem logo depois do Atendimento porque é o que se lê ANTES de escrever a sessão: quem
    /// atende pela primeira vez precisa dos antecedentes na frente, e quem atende pela
    /// vigésima confere se algo mudou.
    /// </summary>
    public AnamneseViewModel Anamnese { get; }

    /// <summary>
    /// A CAPA: quem é, o que ela tem e o que está assinado em nome dela. É a tela nova do
    /// redesenho, e a única do Consultório que lê o cadastro — em leitura, porque editar
    /// contato e convênio continua sendo do balcão.
    /// </summary>
    public PacienteCapaViewModel Capa { get; }

    public ProntuarioClinicoViewModel Prontuario { get; }

    /// <summary>
    /// Receita, atestado, comparecimento e pedido de exame — DENTRO do paciente
    /// (parcela 74).
    ///
    /// ⚠️ Ela existe desde a parcela 39 e a única porta era um item de MENU. Ou seja: o
    /// médico com o paciente na cadeira tinha de SAIR do paciente para prescrever, e
    /// voltar depois. É o defeito recorrente do projeto — a porta no lugar errado —
    /// cometido dentro de um app só, e é o que mais separava esta tela de um prontuário
    /// eletrônico de verdade. O item de menu CONTINUA existindo: quem entra pela sidebar
    /// escolhe o paciente ali, como sempre fez.
    /// </summary>
    public PrescricoesClinicasViewModel Prescricoes { get; }

    /// <summary>Exames e laudos do PACIENTE, não da sessão. Ver a ViewModel.</summary>
    public AnexosPacienteViewModel Anexos { get; }

    public EvolucaoDorViewModel Dor { get; }
    public MedidasViewModel Medidas { get; }
    public AvaliacoesViewModel Avaliacoes { get; }

    /// <summary>
    /// A aba de dentro de "Acompanhamento" (parcela 95): 0 dor · 1 medidas · 2 avaliações.
    /// Vem de <c>ModuloClinico.SubAbaDe</c> quando se entra por uma das três chaves.
    /// </summary>
    [ObservableProperty] private int _subAbaAcompanhamento;

    /// <summary>Aba aberta. É por ela que a chave de navegação escolhe onde cair.</summary>
    [ObservableProperty] private int _abaAtual;

    [ObservableProperty] private string _paciente = string.Empty;

    /// <summary>De onde a pessoa veio: chamada da agenda, ou escolhida na carteira.</summary>
    [ObservableProperty] private string _contexto = string.Empty;

    /// <summary>
    /// O crachá clínico: idade, convênio, desde quando, alergias e últimos diagnósticos.
    /// Null enquanto a leitura não voltou — e a tela mostra o nome, que já tem, em vez de
    /// piscar em branco.
    /// </summary>
    [ObservableProperty] private CabecalhoClinicoPaciente? _cabecalho;

    /// <summary>Foto do cadastro, para o crachá. Null cai no avatar de iniciais.</summary>
    [ObservableProperty] private System.Windows.Media.Imaging.BitmapImage? _foto;

    /// <summary>
    /// Ninguém em foco. Acontece quando se chega aqui por navegação direta (o painel da
    /// direção manda para o Consultório sem ter escolhido paciente): a tela diz o que
    /// fazer em vez de mostrar as seções em branco.
    /// </summary>
    [ObservableProperty] private bool _semPaciente = true;

    // ================= A BARRA DE ATENDIMENTO (parcela 74) =================
    //
    // O que ela corrige
    // -----------------
    // Até aqui esta tela era um FORMULÁRIO: campos e um "Salvar sessão" no rodapé. Num
    // prontuário eletrônico o atendimento é um ESTADO — você ENTRA nele, o relógio corre e
    // você o FINALIZA —, e é essa diferença que faz a tela dizer, o tempo todo, que há uma
    // pessoa na sala e há quanto tempo ela está lá.
    //
    // ⚠️ O mais revelador é que o carimbo do INÍCIO existe desde a parcela 38
    // (<c>Agendamento.InicioAtendimentoEm</c>, para o kanban) e NENHUMA tela do Consultório
    // o lia. O dado estava gravado e não tinha leitor no app de quem o produz — o defeito
    // recorrente do projeto, aqui na variante mais discreta: nada falha, só que o médico
    // não sabe que já está há 40 minutos com o mesmo paciente.

    /// <summary>Há atendimento a que se referir — o paciente veio de um horário.</summary>
    [ObservableProperty] private bool _temSessao;

    /// <summary>"Em atendimento há 12 min" / "Encerrado às 14h32 · durou 24 min".</summary>
    [ObservableProperty] private string _situacaoSessao = string.Empty;

    /// <summary>O paciente entrou na sala e o atendimento ainda não foi encerrado.</summary>
    [ObservableProperty] private bool _emAtendimento;

    /// <summary>O horário existe e o paciente ainda não entrou na sala.</summary>
    [ObservableProperty] private bool _podeIniciar;

    /// <summary>Já encerrado: a barra vira registro do que aconteceu, sem botão de ação.</summary>
    [ObservableProperty] private bool _sessaoEncerrada;

    /// <summary>
    /// A sessão foi CONCLUÍDA (presença carimbada, guias no faturamento). É estado
    /// terminal para esta tela: desfazer daqui em diante é ESTORNO, na Recepção.
    /// </summary>
    [ObservableProperty] private bool _sessaoConcluida;

    /// <summary>
    /// Reabrir só existe entre o encerramento e a conclusão. Depois de concluída, o
    /// serviço recusa — e botão que só existe para levar recusa é o defeito da parcela 41.
    /// </summary>
    public bool PodeReabrir => SessaoEncerrada && !SessaoConcluida;

    partial void OnSessaoEncerradaChanged(bool value) => OnPropertyChanged(nameof(PodeReabrir));
    partial void OnSessaoConcluidaChanged(bool value) => OnPropertyChanged(nameof(PodeReabrir));

    [ObservableProperty] private string? _mensagemSessao;
    [ObservableProperty] private bool _mensagemSessaoEhErro;

    /// <summary>
    /// Mover a fila é o mesmo ato dos dois lados (parcela 61): <c>EditarAgenda</c> OU
    /// <c>MovimentarFila</c>. O perfil Profissional recebe o segundo por padrão, sem ganhar
    /// o primeiro — marcar horário de terceiros continua sendo do balcão.
    /// </summary>
    public bool PodeMoverFila => SessaoUsuario.Atual.PodeAlgum(
        Permissao.EditarAgenda | Permissao.MovimentarFila);

    public PacienteWorkspaceViewModel(
        IServiceProvider servicos, PacienteEmFoco foco, int aba = 0, int subAba = 0)
    {
        _foco = foco;
        SubAbaAcompanhamento = subAba;

        // A decisão de QUEM vê qual seção de escrita mora no domínio, para o dotnet test
        // alcançar; aqui só se aplica. O índice da seção sai do MESMO mapa que a
        // navegação usa (`AbaDe`), e não de um rótulo escrito à mão.
        var (medico, enfermagem) = PerfisAcesso.SecoesDeEscritaDoPosto(SessaoUsuario.Atual.Efetivas);
        var secaoMedico = ModuloClinico.AbaDe(ModuloClinico.ChaveAtendimento);
        var secaoEnfermagem = ModuloClinico.AbaDe(ModuloClinico.ChaveAtendimentoEnfermagem);
        Secoes = ModuloClinico.RailDoPaciente()
            .Select((secao, i) => new ItemDoRail(
                secao.Rotulo, secao.Grupo,
                Oculta: (i == secaoMedico && !medico) || (i == secaoEnfermagem && !enfermagem)))
            .ToList();

        SecoesAgrupadas = new ListCollectionView(Secoes.ToList());
        SecoesAgrupadas.GroupDescriptions.Add(
            new PropertyGroupDescription(nameof(ItemDoRail.Grupo)));
        AbaAtual = aba;
        _escopos = servicos.GetRequiredService<IServiceScopeFactory>();
        _dialogo = servicos.GetRequiredService<IDialogoService>();

        // As seções são construídas juntas de propósito. Elas falam da MESMA pessoa,
        // e trocar de aba é folhear o prontuário dela — se cada aba fosse carregada no
        // primeiro clique, folhear custaria uma ida ao banco por página, justamente no
        // momento em que se está com o paciente na frente.
        Atendimento = servicos.GetRequiredService<AtendimentoViewModel>();
        Enfermagem = servicos.GetRequiredService<AtendimentoEnfermagemViewModel>();
        Anamnese = servicos.GetRequiredService<AnamneseViewModel>();
        Capa = servicos.GetRequiredService<PacienteCapaViewModel>();
        Prontuario = servicos.GetRequiredService<ProntuarioClinicoViewModel>();
        Prescricoes = servicos.GetRequiredService<PrescricoesClinicasViewModel>();
        // Dentro do paciente ela não desenha o próprio cabeçalho: o nome já está no
        // crachá, uma vez, e o seletor de busca dela trocaria o paciente do posto por
        // baixo desta tela — o mestre-detalhe que o workspace existe para acabar.
        Prescricoes.MostrarCabecalho = false;
        Anexos = servicos.GetRequiredService<AnexosPacienteViewModel>();
        Dor = servicos.GetRequiredService<EvolucaoDorViewModel>();
        Medidas = servicos.GetRequiredService<MedidasViewModel>();
        Avaliacoes = servicos.GetRequiredService<AvaliacoesViewModel>();

        SemPaciente = !_foco.Definido;
        Paciente = _foco.Nome;
        // ⚠️ A MESMA função do AtendimentoViewModel (parcela 72). Este cabeçalho escrevia
        // "Chamado da agenda de HOJE" para qualquer horário, enquanto a tela de dentro já
        // dizia a DATA desde a parcela 69 — duas frases para a mesma pergunta, e a de cima
        // era a errada. É a lição das parcelas 64 e 68 pela sétima vez: quando duas telas
        // respondem à mesma coisa, a que ninguém releu é a que mente.
        Contexto = AtendimentoViewModel.DescreverOrigem(
            _foco.AgendamentoId, _foco.DataDoHorario);

        _relogio.Tick += (_, _) => DescreverSessao();
        _ = CarregarSessaoAsync();
        _ = CarregarCabecalhoAsync();
    }

    /// <summary>Volta para a lista de onde se veio. Sem sair, não há como trocar de pessoa.</summary>
    [RelayCommand]
    private void Voltar()
        => NavegacaoSuite.Ir(_foco.AgendamentoId is null
            ? ModuloClinico.ChavePacientesDaClinica
            : ModuloClinico.ChaveMeuDia);

    /// <summary>Abre a carteira para escolher outra pessoa.</summary>
    [RelayCommand]
    private void TrocarPaciente() => NavegacaoSuite.Ir(ModuloClinico.ChavePacientesDaClinica);

    /// <summary>
    /// Lê o crachá clínico. Falhar aqui NÃO derruba a tela: o prontuário abre com o nome,
    /// que já veio do foco, e o crachá fica de fora com a falha no log. Banco lento não
    /// pode impedir alguém de ler o prontuário do paciente que está na frente — a regra da
    /// parcela 52 aplicada ao cabeçalho.
    /// </summary>
    private async Task CarregarCabecalhoAsync()
    {
        if (_foco.PacienteId is not { } id) return;

        try
        {
            using var escopo = _escopos.CreateScope();
            var consultorio = escopo.ServiceProvider.GetRequiredService<ConsultorioService>();
            var cabecalho = await consultorio.CabecalhoAsync(id);
            if (cabecalho is null) return;

            Cabecalho = cabecalho;
            Foto = Clinica.Desktop.Shell.Componentes.Retrato.Carregar(cabecalho.Foto);
        }
        catch (Exception ex)
        {
            Clinica.Application.Diagnostico.Registrar(
                "Consultório — crachá clínico do paciente não pôde ser lido", ex);
        }
    }

    // ==================== A sessão de atendimento ====================

    /// <summary>
    /// Lê o horário de origem para saber em que ponto o atendimento está.
    ///
    /// Sem <see cref="PacienteEmFoco.AgendamentoId"/> a barra simplesmente NÃO aparece, e
    /// isso é honesto: o paciente foi aberto pela carteira, não há sessão em curso, e
    /// desenhar um cronômetro parado convidaria a "iniciar" um atendimento que não existe
    /// na agenda de ninguém.
    /// </summary>
    private Task CarregarSessaoAsync()
        => _foco.AgendamentoId is { } id ? RecarregarHorarioAsync(id) : LimparSessao();

    private Task LimparSessao()
    {
        TemSessao = false;
        return Task.CompletedTask;
    }

    /// <summary>
    /// Relê o horário e reescreve a frase da barra. Chamado também DEPOIS de finalizar: a
    /// conclusão muda o <c>Status</c> no banco, e a barra que continuasse dizendo "em
    /// atendimento" ofereceria um botão para concluir o que já está concluído.
    /// </summary>
    private async Task RecarregarHorarioAsync(int id)
    {
        try
        {
            using var escopo = _escopos.CreateScope();
            var repo = escopo.ServiceProvider
                .GetRequiredService<Clinica.Application.Abstracoes.IClinicaRepositorio>();
            _horario = await repo.ObterAgendamentoAsync(id);
            DescreverSessao();
        }
        catch (Exception ex)
        {
            // Degradar deixa rastro (regra do projeto), e a barra some em vez de mostrar
            // um cronômetro que não corresponde a nada: falha nunca aparece como sucesso.
            Clinica.Application.Diagnostico.Registrar(
                "Consultório — situação do atendimento não pôde ser lida", ex);
            TemSessao = false;
        }
    }

    /// <summary>
    /// Traduz os carimbos em uma frase, e liga/desliga o relógio.
    ///
    /// O relógio só corre enquanto o atendimento está EM CURSO: parado, a frase é fixa
    /// ("durou 24 min") e um timer batendo a cada 15 s para reescrever a mesma coisa é
    /// trabalho sem leitor.
    /// </summary>
    private void DescreverSessao()
    {
        if (_horario is null) { TemSessao = false; _relogio.Stop(); return; }

        TemSessao = true;
        var agora = DateTime.Now;
        var duracao = _horario.DuracaoDoAtendimento(agora);

        SessaoEncerrada = _horario.FimAtendimentoEm is not null;
        SessaoConcluida = _horario.Status == StatusAgendamento.Realizado;
        EmAtendimento = _horario.InicioAtendimentoEm is not null && !SessaoEncerrada
                        && _horario.Status == StatusAgendamento.Agendado;
        PodeIniciar = _horario.InicioAtendimentoEm is null
                      && _horario.Status == StatusAgendamento.Agendado;

        if (SessaoConcluida)
            // O desfecho do fluxo, escrito: quem lê precisa saber que não falta mais nada
            // desta sala — o que segue no balcão é dinheiro, não a sessão.
            SituacaoSessao = "Sessão concluída"
                             + (_horario.FimAtendimentoEm is { } fim ? $" às {fim:HH\\:mm}" : "")
                             + (duracao is null ? "" : $" · durou {duracao} min");
        else if (SessaoEncerrada)
            SituacaoSessao = $"Atendimento encerrado às {_horario.FimAtendimentoEm:HH\\:mm}"
                             + (duracao is null ? "" : $" · durou {duracao} min");
        else if (EmAtendimento)
            // "há 0 min" lê-se como defeito; "agora mesmo" é a mesma verdade em português.
            SituacaoSessao = duracao is null or 0
                ? "Em atendimento — começou agora mesmo"
                : $"Em atendimento há {duracao} min";
        else if (PodeIniciar)
            SituacaoSessao = $"Horário de {_horario.DataHora:HH\\:mm} — o paciente ainda não "
                             + "entrou na sala";
        else
            // Só sobra Cancelado/Faltou: dizer "encerrado no balcão" descreveria o
            // desfecho errado para um horário que não aconteceu.
            SituacaoSessao = _horario.Status == StatusAgendamento.Faltou
                ? "Horário marcado como falta"
                : "Horário cancelado";

        if (EmAtendimento && _naTela) _relogio.Start(); else _relogio.Stop();
    }

    /// <summary>
    /// O paciente ENTROU na sala. É o mesmo ato do "Entrou" do kanban — o balcão vê o
    /// cartão mudar de raia —, e existe aqui porque em metade das clínicas quem abre a
    /// porta é o profissional (a razão pela qual "chamar" existe dos dois lados desde a
    /// parcela 38).
    /// </summary>
    [RelayCommand]
    private async Task IniciarSessaoAsync()
    {
        // A barra apagada explica; esta guarda diz por quê quando o clique chega mesmo
        // assim — guarda que volta em silêncio é botão que não faz nada (parcela 41).
        if (_foco.AgendamentoId is not { } id || !PodeIniciar)
        {
            Avisar("Não há horário em aberto para começar o atendimento por aqui.", erro: true);
            return;
        }

        try
        {
            SessaoUsuario.Atual.ExigirAlgum(
                Permissao.EditarAgenda | Permissao.MovimentarFila, "iniciar o atendimento");

            using var escopo = _escopos.CreateScope();
            var agenda = escopo.ServiceProvider.GetRequiredService<AgendaService>();
            _horario = await agenda.IniciarAtendimentoAsync(id, SessaoUsuario.Atual.Operador);
            DescreverSessao();
            Avisar("Atendimento iniciado — o balcão já está vendo.", erro: false);
        }
        catch (Exception ex)
        {
            Clinica.Application.Diagnostico.Registrar(
                "Consultório — atendimento não pôde ser iniciado", ex);
            Avisar(ex.Message, erro: true);
        }
    }

    /// <summary>
    /// FINALIZAR o atendimento: grava a sessão, encerra o horário e CONCLUI a sessão — a
    /// guia vai para o faturamento no mesmo clique.
    ///
    /// ⚠️ A conclusão passou para cá na parcela 95, por pedido da direção: <i>"a secretária
    /// agenda, cai na agenda do médico, ele clica em atender e faz o atendimento"</i>. O
    /// desenho anterior (parcela 61) deixava o <c>Status</c> em <c>Agendado</c> e esperava
    /// o <b>Concluir</b> do balcão, com o argumento de que concluir são quatro fatos e três
    /// são do balcão. O argumento continua verdadeiro para pacote, insumo e caixa — e não
    /// se sustentava para a GUIA, que é o fato do atendimento e nasce do que aconteceu na
    /// sala. Na prática, no caso mais comum (convênio, sem pacote, sem insumo)
    /// <c>RegistroAtendimento.TemDecisao</c> é FALSO: o clique do balcão não abria janela
    /// nenhuma — era cerimônia para carimbar o que o médico já sabia.
    ///
    /// O que o balcão continua fazendo é o DINHEIRO: pacote, insumo e caixa aparecem na
    /// fila como fechamento pendente (a raia FINALIZADO ganhou o botão), e o
    /// <c>FechamentoSessaoService</c> já sabia reaproveitar a presença já confirmada — é
    /// literalmente o caminho que <c>GarantirAtendimentoAsync</c> abre desde a parcela 65.
    ///
    /// ⚠️ A ORDEM dos três passos é a hierarquia da parcela 65, e ela decide o que sobra
    /// quando algo falha: <b>1)</b> grava a sessão — sem ela nada acontece, porque anunciar
    /// que o atendimento terminou com o registro clínico inexistente é falha exibida como
    /// sucesso; <b>2)</b> encerra o horário (o carimbo, reversível); <b>3)</b> conclui. O
    /// passo 3 falhando NÃO desfaz os dois primeiros: o prontuário está escrito, o balcão
    /// sabe que a sala vagou, e o que ficou pendente é a guia — que a fila ainda conclui
    /// pelo botão de sempre. Cada desfecho tem frase própria; nenhuma delas afirma o que
    /// não aconteceu.
    ///
    /// ⚠️ Concluir é IRREVERSÍVEL por aqui: com a presença carimbada, desfazer é ESTORNO
    /// (<c>EstornoAtendimentoService</c>, na aba Lançamentos da Recepção) e não o
    /// <see cref="ReabrirSessaoAsync"/>. Por isso o botão diz o que vai fazer ANTES do
    /// clique, e não depois.
    ///
    /// ⚠️ A ORDEM é a hierarquia da parcela 65: **grava primeiro**. Se a evolução não
    /// puder ser salva, o carimbo não acontece — mandar o recado de que o médico terminou
    /// enquanto o registro clínico não existe é falha exibida como sucesso. E o inverso
    /// também vale: gravada a sessão, falhar o carimbo vira AVISO, nunca desfaz o
    /// prontuário.
    ///
    /// ⚠️ Encerrar com a sessão EM BRANCO é legítimo — o profissional pode escrever depois,
    /// e registro que não se consegue salvar é registro que não acontece —, mas é
    /// exatamente a dívida que este app existe para cobrar. Por isso a tela PERGUNTA, com a
    /// consequência escrita, em vez de impedir ou de calar.
    /// </summary>
    [RelayCommand]
    private async Task FinalizarSessaoAsync()
    {
        if (_foco.AgendamentoId is not { } id || !EmAtendimento)
        {
            Avisar("Não há atendimento em curso para finalizar.", erro: true);
            return;
        }

        // ⚠️ A PASSAGEM DE ENFERMAGEM AINDA NÃO REGISTRADA (parcela 88).
        //
        // Ela não é gravada por este botão, e é de propósito: a evolução de enfermagem é
        // append-only — são VÁRIAS por passagem (14h20, 14h50, 15h10) —, e cada uma nasce
        // com a hora do FATO que a pessoa digitou. Gravá-la de carona no encerramento
        // criaria uma passagem com hora que ninguém confirmou.
        //
        // O que não pode é sumir CALADA: quem digitou e não clicou em Registrar perde o
        // que escreveu ao trocar de tela, e o registro refeito de memória é o que este
        // módulo existe para evitar. Então a tela PERGUNTA, com a consequência escrita.
        if (!Enfermagem.PassagemEmBranco
            && !_dialogo.Confirmar(
                "Há uma passagem de enfermagem não registrada",
                "Você escreveu uma passagem de enfermagem e ainda não clicou em "
                + "\u201CRegistrar\u201D.\n\nEncerrando agora, ela N\u00C3O \u00E9 gravada — a evolu\u00E7\u00E3o de "
                + "enfermagem \u00E9 registrada uma a uma, com a hora do que foi observado.\n\n"
                + "Encerrar mesmo assim?"))
            return;

        if (Atendimento.SessaoEmBranco
            && !_dialogo.Confirmar(
                "Encerrar sem escrever a evolução?",
                "Você não escreveu nada desta sessão.\n\n"
                + "Encerrando assim, ela vai aparecer em \u201CSess\u00F5es sem evolu\u00E7\u00E3o\u201D at\u00E9 "
                + "algu\u00E9m escrever — e o registro feito de mem\u00F3ria, dias depois, \u00E9 o que este "
                + "sistema existe para evitar.\n\nEncerrar mesmo assim?"))
            return;

        var gravou = false;
        var encerrou = false;
        try
        {
            SessaoUsuario.Atual.ExigirAlgum(
                Permissao.EditarAgenda | Permissao.MovimentarFila, "finalizar o atendimento");

            // E o bit do ATO, não só o da fila (a lição da parcela 69, cobrada de novo
            // porque o ato MUDOU DE LUGAR): finalizar aqui é o que carimba a presença e
            // gera as guias, e `LancarAtendimento` existe justamente para a direção poder
            // tirar isso de alguém. O perfil Profissional passou a recebê-lo por padrão —
            // ele é quem lança agora —, então ninguém perde nada; o que muda é o bit
            // passar a valer também deste lado.
            SessaoUsuario.Atual.Exigir(
                Permissao.LancarAtendimento, "concluir a sessão e gerar as guias");

            // 1) O REGISTRO CLÍNICO — o que a clínica não pode perder.
            //
            // ⚠️ A condição é `TemAlgoParaGravar`, NUNCA `!SessaoEmBranco`. As duas
            // perguntas são diferentes: "em branco" decide se a tela PERGUNTA (e deixa a EVA
            // e o mapa de fora com razão — eles são medida, não registro do que aconteceu),
            // mas usá-la aqui descartava em silêncio a sessão de acupuntura mais comum da
            // casa: EVA antes 8, depois 3, seis pontos no mapa e nenhuma linha de texto.
            if (Atendimento.TemAlgoParaGravar && !await Atendimento.TentarSalvarAsync())
            {
                Avisar("A sessão não pôde ser salva, então o atendimento NÃO foi encerrado. "
                       + "A mensagem do erro está na aba Atendimento.", erro: true);
                return;
            }
            gravou = true;

            // 2) O RECADO — reversível pelo "voltar etapa" do quadro.
            using (var escopo = _escopos.CreateScope())
            {
                var agenda = escopo.ServiceProvider.GetRequiredService<AgendaService>();
                _horario = await agenda.EncerrarAtendimentoAsync(id, SessaoUsuario.Atual.Operador);
                encerrou = true;
                DescreverSessao();
            }

            // 3) A CONCLUSÃO — a guia vai para o faturamento.
            //
            // ⚠️ O CONVÊNIO ANTES (parcela 92): a importação do sistema anterior deixou
            // 2.021 fichas em "a definir", e sem convênio o atendimento não nasce — a
            // recusa viria por exceção, aqui, na cara de quem não é dono do cadastro. A
            // pergunta é o ponto único do shell, o MESMO que o Concluir da Fila usa; sem
            // ela, este caminho seria a porta que a parcela 92 deixou passando.
            // ⚠️ As DUAS condições são separadas de propósito: juntá-las faria a frase
            // culpar o convênio quando o que falta é o paciente em foco — mensagem
            // plausível e errada manda procurar o defeito no lugar errado, e é pior que
            // mensagem nenhuma (a lição da assinatura em nuvem, parcela 67).
            if (_foco.PacienteId is not { } pacienteId)
            {
                Avisar("Sessão gravada e atendimento encerrado, mas a guia não foi gerada: "
                       + "a tela perdeu o paciente em foco. A recepção conclui pela fila do dia.",
                       erro: true);
                return;
            }

            if (!await VinculoDeConvenio.GarantirAsync(
                    _escopos, pacienteId, SessaoUsuario.Atual.Operador))
            {
                // Desistiu de escolher: a sessão está gravada e o horário encerrado. Não
                // é erro — é o estado anterior a esta parcela, e a fila conclui depois.
                Avisar("Sessão gravada e atendimento encerrado. A guia NÃO foi gerada porque "
                       + "o convênio do paciente ainda não está definido — a recepção conclui "
                       + "pela fila do dia.", erro: true);
                return;
            }

            RegistroAtendimento registro;
            using (var escopo = _escopos.CreateScope())
            {
                var fechamento = escopo.ServiceProvider.GetRequiredService<FechamentoSessaoService>();
                registro = await fechamento.RegistrarAtendimentoAsync(
                    id, SessaoUsuario.Atual.Operador);
            }

            await RecarregarHorarioAsync(id);

            // Os recados do LANÇAMENTO — o principal é a NÃO CONFORMIDADE reaberta porque
            // o paciente voltou. Ele existe para a guia ser cobrada AGORA, e aqui vale o
            // mesmo peso do balcão: some junto da mensagem, nunca escondido.
            var recados = registro.RecadosDoLancamento.Count > 0
                ? " · " + string.Join(" · ", registro.RecadosDoLancamento)
                : string.Empty;

            Avisar(registro.GuiasGeradas == 0
                ? "Sessão concluída — particular, sem guia a faturar." + recados
                : $"Sessão concluída — {registro.GuiasGeradas} guia(s) no faturamento."
                  + recados, erro: false);
        }
        catch (Exception ex)
        {
            Clinica.Application.Diagnostico.Registrar(
                "Consultório — atendimento não pôde ser encerrado", ex);

            // ⚠️ A frase não pode AFIRMAR que a sessão foi gravada: a exceção pode ter vindo
            // do `ExigirAlgum` acima, antes de qualquer gravação, e aí ela seria falsa
            // justamente onde o profissional precisa saber o que fazer. Só quem chegou a
            // gravar diz que gravou.
            // ⚠️ Três desfechos, três frases — e nenhuma afirma o que não aconteceu. A
            // exceção pode ter vindo do `Exigir` (antes de qualquer gravação), do carimbo
            // ou da conclusão, e a diferença entre elas é o que a pessoa precisa fazer
            // em seguida.
            Avisar(
                encerrou
                    ? "A sessão foi gravada e o atendimento foi encerrado, mas a guia NÃO "
                      + $"foi gerada: {ex.Message} A recepção conclui pela fila do dia."
                    : gravou
                        ? "A sessão foi gravada, mas o atendimento não foi encerrado: "
                          + ex.Message
                        : ex.Message,
                erro: true);
        }
    }

    /// <summary>
    /// DESFAZ o encerramento — o profissional clicou em Finalizar no paciente errado, ou
    /// precisou chamar a pessoa de volta à sala.
    ///
    /// ⚠️ A porta existe porque a capacidade existe: <c>ReabrirAtendimentoAsync</c> sem
    /// botão seria o defeito recorrente do projeto na variante mais discreta, e o único
    /// caminho de volta seria pedir ao balcão que voltasse a etapa — o que tira o paciente
    /// da sala, que é justamente o que não se quer.
    /// </summary>
    [RelayCommand]
    private async Task ReabrirSessaoAsync()
    {
        if (_foco.AgendamentoId is not { } id || !SessaoEncerrada)
        {
            Avisar("Não há atendimento encerrado para reabrir.", erro: true);
            return;
        }

        try
        {
            SessaoUsuario.Atual.ExigirAlgum(
                Permissao.EditarAgenda | Permissao.MovimentarFila, "reabrir o atendimento");

            using var escopo = _escopos.CreateScope();
            var agenda = escopo.ServiceProvider.GetRequiredService<AgendaService>();
            _horario = await agenda.ReabrirAtendimentoAsync(id, SessaoUsuario.Atual.Operador);
            DescreverSessao();
            Avisar("Atendimento reaberto — o balcão voltou a ver o paciente na sala.",
                   erro: false);
        }
        catch (Exception ex)
        {
            Clinica.Application.Diagnostico.Registrar(
                "Consultório — atendimento não pôde ser reaberto", ex);
            Avisar(ex.Message, erro: true);
        }
    }

    private void Avisar(string texto, bool erro)
    {
        MensagemSessao = texto;
        MensagemSessaoEhErro = erro;
    }

    /// <summary>A View montou. Ver o comentário do <c>_relogio</c>.</summary>
    public void IniciarRelogio()
    {
        _naTela = true;
        if (EmAtendimento) _relogio.Start();
    }

    /// <summary>
    /// A View saiu de cena. O relógio PARA sempre — inclusive com atendimento em curso: a
    /// tela que voltar constrói uma ViewModel nova e lê o carimbo do banco de novo.
    /// </summary>
    public void PararRelogio()
    {
        _naTela = false;
        _relogio.Stop();
    }
}
