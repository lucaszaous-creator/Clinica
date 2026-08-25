using System.Windows.Threading;
using Clinica.Application.Modelos;
using Clinica.Application.Servicos;
using Clinica.Clinico.Modulo;
using Clinica.Desktop.Controls;
using Clinica.Desktop.Shell.Modulos;
using Clinica.Domain;
using Clinica.Domain.Entities;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;

namespace Clinica.Clinico.ViewModels;

/// <summary>
/// A TELA DO PACIENTE — uma pessoa, cinco abas (parcela 37, 4ª rodada).
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
///   <item><b>Uma lista</b> — "Meu dia" (quem eu vejo hoje) ou "Meus pacientes" (quem eu
///   acompanho). São as duas portas de entrada, e são telas de verdade, com a largura
///   inteira.</item>
///   <item><b>A tela do paciente</b> — clicou, entrou. A identidade fica no topo, as cinco
///   seções viram ABAS, e a largura inteira é do conteúdo clínico.</item>
/// </list>
///
/// Por que abas, e não cinco itens de menu
/// ---------------------------------------
/// Porque as cinco só existem COM paciente. Item de menu que só funciona depois de você ter
/// passado por outro lugar é item que ensina o usuário a errar — e era exatamente o que a
/// versão anterior fazia, abrindo "Medidas" numa tela em branco pedindo que se buscasse
/// alguém. Aba, ao contrário, diz sozinha que pertence à pessoa cujo nome está logo acima.
///
/// As cinco chaves antigas continuam válidas e caem AQUI, cada uma na sua aba: o painel da
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

    public AtendimentoViewModel Atendimento { get; }

    /// <summary>
    /// A ANAMNESE do paciente (parcela 75) — o que se pergunta uma vez e se revisa.
    ///
    /// Vem logo depois do Atendimento porque é o que se lê ANTES de escrever a sessão: quem
    /// atende pela primeira vez precisa dos antecedentes na frente, e quem atende pela
    /// vigésima confere se algo mudou.
    /// </summary>
    public AnamneseViewModel Anamnese { get; }

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
    /// fazer em vez de mostrar cinco abas vazias.
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
        IServiceProvider servicos, PacienteEmFoco foco, int aba = 0)
    {
        _foco = foco;
        AbaAtual = aba;
        _escopos = servicos.GetRequiredService<IServiceScopeFactory>();
        _dialogo = servicos.GetRequiredService<IDialogoService>();

        // As cinco seções são construídas juntas de propósito. Elas falam da MESMA pessoa,
        // e trocar de aba é folhear o prontuário dela — se cada aba fosse carregada no
        // primeiro clique, folhear custaria uma ida ao banco por página, justamente no
        // momento em que se está com o paciente na frente.
        Atendimento = servicos.GetRequiredService<AtendimentoViewModel>();
        Anamnese = servicos.GetRequiredService<AnamneseViewModel>();
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
            ? ModuloClinico.ChaveMeusPacientes
            : ModuloClinico.ChaveMeuDia);

    /// <summary>Abre a carteira para escolher outra pessoa.</summary>
    [RelayCommand]
    private void TrocarPaciente() => NavegacaoSuite.Ir(ModuloClinico.ChaveMeusPacientes);

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
    private async Task CarregarSessaoAsync()
    {
        if (_foco.AgendamentoId is not { } id) { TemSessao = false; return; }

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
        EmAtendimento = _horario.InicioAtendimentoEm is not null && !SessaoEncerrada
                        && _horario.Status == StatusAgendamento.Agendado;
        PodeIniciar = _horario.InicioAtendimentoEm is null
                      && _horario.Status == StatusAgendamento.Agendado;

        if (SessaoEncerrada)
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
            SituacaoSessao = "Horário já encerrado no balcão";

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
    /// FINALIZAR o atendimento: grava a sessão e avisa o balcão de que o paciente está
    /// saindo da sala.
    ///
    /// ⚠️ Ele NÃO conclui o atendimento, e a distinção é a decisão da parcela 61: concluir
    /// são quatro fatos do mesmo ato (a guia, o pacote, o insumo, o caixa) e três deles são
    /// do balcão. O que este botão afirma é <i>"terminei com esta pessoa"</i>.
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

        if (Atendimento.SessaoEmBranco
            && !_dialogo.Confirmar(
                "Encerrar sem escrever a evolução?",
                "Você não escreveu nada desta sessão.\n\n"
                + "Encerrando assim, ela vai aparecer em \u201CSess\u00F5es sem evolu\u00E7\u00E3o\u201D at\u00E9 "
                + "algu\u00E9m escrever — e o registro feito de mem\u00F3ria, dias depois, \u00E9 o que este "
                + "sistema existe para evitar.\n\nEncerrar mesmo assim?"))
            return;

        var gravou = false;
        try
        {
            SessaoUsuario.Atual.ExigirAlgum(
                Permissao.EditarAgenda | Permissao.MovimentarFila, "finalizar o atendimento");

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
            using var escopo = _escopos.CreateScope();
            var agenda = escopo.ServiceProvider.GetRequiredService<AgendaService>();
            _horario = await agenda.EncerrarAtendimentoAsync(id, SessaoUsuario.Atual.Operador);
            DescreverSessao();

            Avisar("Atendimento encerrado. O balcão já sabe que o paciente está indo fechar "
                   + "a sessão.", erro: false);
        }
        catch (Exception ex)
        {
            Clinica.Application.Diagnostico.Registrar(
                "Consultório — atendimento não pôde ser encerrado", ex);

            // ⚠️ A frase não pode AFIRMAR que a sessão foi gravada: a exceção pode ter vindo
            // do `ExigirAlgum` acima, antes de qualquer gravação, e aí ela seria falsa
            // justamente onde o profissional precisa saber o que fazer. Só quem chegou a
            // gravar diz que gravou.
            Avisar(gravou
                ? "A sessão foi gravada, mas o balcão não foi avisado de que você terminou: "
                  + ex.Message
                : ex.Message, erro: true);
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
