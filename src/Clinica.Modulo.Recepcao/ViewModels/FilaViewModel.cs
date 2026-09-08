using System.Collections.ObjectModel;
using System.Windows.Threading;
using Clinica.Application.Abstracoes;
using Clinica.Application.Modelos;
using Clinica.Application.Servicos;
using Clinica.Desktop.Controls;
using Clinica.Desktop.Shell;
using Clinica.Desktop.Shell.Componentes;
using Clinica.Desktop.Shell.Modulos;
using Clinica.Domain;
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

    /// <summary>A faixa do horário, com término ("09:00–10:00") — só o início dizia
    /// "quando começa" e calava "quando a sala vaga", que é a metade que o balcão usa
    /// para encaixar o próximo (redesenho da fila, ago/2026).</summary>
    public required string Horario { get; init; }
    public required string Paciente { get; init; }
    public required string Modalidade { get; init; }

    /// <summary>
    /// A FAMÍLIA da modalidade (set/2026 — a agenda com cor): abre a linha com o traço de
    /// 3 px e pinta o avatar, com a MESMA cor que o cartão da grade já tinha. A cor sai do
    /// enum, nunca do rótulo — a variante cadastrada herda a cor de quem deriva.
    /// </summary>
    public required ModalidadeAtendimento ModalidadeFamilia { get; init; }

    public required string Profissional { get; init; }

    /// <summary>
    /// A sala, ou VAZIO quando o horário não tem uma — nunca o travessão (set/2026).
    ///
    /// A clínica não usa salas: das doze linhas de um dia, doze traziam "sala —" debaixo
    /// do nome do profissional. Crachá igual em toda linha não distingue nada (parcela
    /// 57) e ainda gastava a segunda linha da célula, que é justamente a que o
    /// `RowHeight` fixo decepava. Vazio, os SEIS leitores a omitem — a linha de contexto,
    /// a dica, o "Também:" da faixa, o aviso da chamada e os dois textos da tela —, cada
    /// um com a sua frase, porque "· " sobrando é tão ruim quanto o travessão.
    /// </summary>
    public required string Sala { get; init; }

    /// <summary>Ver <see cref="Sala"/>: é ela que decide se a frase menciona a sala.</summary>
    public bool TemSala => !string.IsNullOrWhiteSpace(Sala);

    public required EtapaFila Etapa { get; init; }
    public string? Observacoes { get; init; }

    /// <summary>O status GRAVADO do horário — é ele que separa cancelado e falta do resto.</summary>
    public required StatusAgendamento Situacao { get; init; }

    /// <summary>A hora marcada — a ordem da lista do dia (set/2026).</summary>
    public required DateTime DataHora { get; init; }

    /// <summary>
    /// Cancelado ou falta. A linha FICA na lista, apagada — a regra da folha do dia: quem
    /// lê às 14h precisa saber que as 15h vagaram, e linha ausente se confunde com horário
    /// que nunca existiu. Até set/2026 o kanban a escondia ("fora do quadro").
    /// </summary>
    public bool ForaDaFila => StatusDaFila.ForaDaFila(Situacao);

    /// <summary>
    /// A coluna STATUS da lista: a etapa em UMA palavra, no MESMO vocabulário do Meu dia
    /// do médico (<see cref="StatusDaFila"/>) — duas telas sobre o mesmo horário, uma
    /// palavra.
    /// </summary>
    public string Status => StatusDaFila.Palavra(Situacao, Etapa);

    /// <summary>A hora do fato sob a palavra ("chegou às 14:40 · espera 12 min"). Corre com o relógio.</summary>
    [ObservableProperty]
    private string _statusDetalhe = string.Empty;

    public DateTime? ChegadaEm { get; init; }
    public DateTime? InicioEm { get; init; }

    /// <summary>
    /// A linha sob o nome na LISTA: modalidade · convênio · pacote N/M. Profissional e
    /// sala têm coluna própria ali; encaixe é selo ao lado do nome.
    /// </summary>
    public string ContextoDaLista => string.Join(" · ", new[]
    {
        Modalidade,
        Convenio.Length == 0 ? null : Convenio,
        TemPacote ? $"pacote {PacoteUsadas}/{PacoteContratadas}" : null
    }.Where(l => !string.IsNullOrEmpty(l)));

    /// <summary>Bytes da miniatura do retrato (o cadastro já a tem desde sempre) — o
    /// <c>Avatar</c> cai nas iniciais quando não há foto. `object` pela mesma razão do
    /// controle: DP de tipo array não se atribui em DataTemplate (MC4102).</summary>
    public object? Foto { get; init; }

    /// <summary>Operadora do paciente ("Unimed Costa do Sol"). Vazio para quem não tem.</summary>
    public string Convenio { get; init; } = string.Empty;

    /// <summary>A linha de contexto sob o nome: horário + convênio.</summary>
    public string SubLinha => Convenio.Length == 0 ? Horario : $"{Horario} · {Convenio}";

    /// <summary>
    /// Selo da rodada de confirmação, SÓ na coluna AGUARDANDO: "Confirmou" quando o
    /// paciente respondeu, "Não confirmou" quando foi avisado e não respondeu, vazio
    /// quando a rodada nem o alcançou (sem rodada não há o que afirmar). Depois do
    /// check-in o selo é ruído — a pessoa está aqui.
    /// </summary>
    public string ConfirmacaoRotulo { get; init; } = string.Empty;

    /// <summary>O selo de confirmação é o positivo (verde) — decide a cor no XAML.</summary>
    public bool ConfirmouPresenca { get; init; }

    public bool TemConfirmacao => ConfirmacaoRotulo.Length > 0;

    /// <summary>
    /// Sessões usadas/contratadas do pacote ativo; nulos sem pacote. A 10ª sessão de um
    /// pacote de 10 se descobre AQUI, não no Finalizar (a lição da parcela 48) — e desde
    /// set/2026 ela vira SELO só no fim ("Penúltima", "Última", "Esgotado"); o "pacote
    /// 3/10" do meio do caminho é contexto, na linha do cartão.
    /// </summary>
    public int? PacoteUsadas { get; init; }

    public int? PacoteContratadas { get; init; }

    public bool TemPacote => PacoteUsadas is not null && PacoteContratadas is not null;

    /// <summary>
    /// A linha de contexto sob o nome: modalidade · profissional · sala · pacote N/M ·
    /// encaixe. É para cá que foram os selos que não avisam nada — o pacote no meio do
    /// caminho e o encaixe (set/2026): informação de leitura, no peso de leitura.
    /// </summary>
    public string Contexto => string.Join(" · ", new[]
    {
        Modalidade,
        Profissional,
        Sala,
        TemPacote ? $"pacote {PacoteUsadas}/{PacoteContratadas}" : null,
        EhEncaixe ? "encaixe" : null
    }.Where(l => !string.IsNullOrEmpty(l)));

    /// <summary>
    /// Os selos do cartão — no máximo três, por uma ordem fixa que mora na Application
    /// (<see cref="SelosDaFila"/>): o que impede, o que cobra agora, o estado da coluna.
    /// Recalculado quando qualquer entrada muda (o atraso corre com o relógio).
    /// </summary>
    public IReadOnlyList<SeloFila> Selos => SelosDaFila.Montar(
        Etapa, TemTermoPendente, TemGuiaPendente,
        PacoteUsadas, PacoteContratadas, AtrasoMinutos, FimAtendimentoEm);

    /// <summary>Minutos além da hora marcada sem check-in; nulo sem atraso. Corre com o relógio.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Selos))]
    private int? _atrasoMinutos;

    /// <summary>A hora em que o profissional encerrou o atendimento (parcela 74); nula se não encerrou.</summary>
    public DateTime? FimAtendimentoEm { get; init; }

    partial void OnTemGuiaPendenteChanged(bool value) => OnPropertyChanged(nameof(Selos));
    partial void OnTemTermoPendenteChanged(bool value) => OnPropertyChanged(nameof(Selos));

    /// <summary>"Atrasado 25 min" — a hora marcada estourou e o paciente não chegou.
    /// Recalculado a cada batida do relógio, como a espera. Vazio sem atraso.</summary>
    [ObservableProperty]
    private string _atraso = string.Empty;

    [ObservableProperty]
    private bool _atrasado;

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

    /// <summary>
    /// O PROFISSIONAL já encerrou o atendimento e o paciente está indo ao balcão
    /// (parcela 74) — o recado que faltava no sentido consultório → balcão.
    ///
    /// ⚠️ Ele NÃO cria uma sexta raia. O cartão continua em "Em atendimento" e ganha um
    /// SELO: uma coluna permanente para um estado que dura minutos é a faixa vazia comendo
    /// a tela que o README condena desde a parcela 38, e o que a recepcionista precisa
    /// saber não é que existe uma coluna nova — é QUAL cartão está pronto para fechar.
    ///
    /// O cartão também sobe para a frente dos irmãos, pela mesma razão do "chamado há N
    /// min": quem já saiu da sala é o próximo a ser atendido no balcão.
    /// </summary>
    [ObservableProperty]
    private bool _atendimentoEncerrado;

    /// <summary>"Encerrado às 14h32" — a hora, não só o fato.</summary>
    [ObservableProperty]
    private string _encerradoEm = string.Empty;

    /// <summary>Tempo de espera formatado ("12 min"). Vazio antes do check-in.</summary>
    [ObservableProperty]
    private string _espera = string.Empty;

    /// <summary>
    /// Espera longa (30 min ou mais) — o cartão fica em destaque.
    ///
    /// ⛔ DORMENTE desde set/2026, junto de <see cref="ChamadaDemorada"/> e de
    /// <see cref="Espera"/>/<see cref="ChamadoHa"/>: sem os botões de fila ninguém carimba
    /// chegada nem chamada, então os quatro ficam vazios e os gatilhos nunca disparam. Não
    /// foram removidos porque o MOTOR da fila ficou (ver <c>AgendaService</c>): eles voltam
    /// a valer com o check-in, sem uma linha nova.
    /// </summary>
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

    /// <summary>
    /// A fila corre HOJE — a regra que o Meu dia do médico já tinha, agora nos DOIS
    /// quadros (set/2026). Chegada, chamada e entrada são carimbos de hora do DIA da
    /// sessão: registrá-los num horário de amanhã ou de ontem carimba a hora de agora numa
    /// sessão que não está acontecendo, e a espera e o atraso saem de um horário morto.
    /// Concluir, fechar a sessão, falta e cancelamento continuam valendo em qualquer dia —
    /// a sessão de ontem que ficou aberta se conclui hoje.
    /// </summary>
    public bool EhHoje => DataHora.Date == DateTime.Today;

    public bool PodeFinalizar => Etapa == EtapaFila.EmAtendimento;

    /// <summary>
    /// A sessão está concluída e há PACOTE por debitar (parcela 95).
    ///
    /// Ela existe porque a conclusão saiu do balcão: com o médico finalizando na sala, o
    /// cartão chega à raia FINALIZADO já carimbado, e sem esta pendência o balcão não
    /// teria por onde debitar o pacote da sessão que acabou de acontecer — alerta sem
    /// porta é pior que alerta nenhum (parcela 48).
    ///
    /// ⚠️ É o PACOTE, e não "tudo o que o fechamento faz", e a diferença decide se o botão
    /// é utilizável. Fosse "ainda não houve fechamento", o paciente de convênio sem pacote
    /// e sem insumo — a maioria do dia — ficaria com o botão aceso PARA SEMPRE, porque
    /// nele não há nada a fechar: pendência que nunca some é pendência que ninguém lê (a
    /// lição da parcela 68). O pacote é o único dos três que o quadro sabe afirmar em
    /// lote (a leitura do selo "Pacote 9/10" já está carregada) e é o que custa dinheiro
    /// quando escapa — sessão comprada que a clínica atende de graça.
    ///
    /// Os outros dois (insumo e caixa) não somem da vista: "Fechar sessão…" fica no menu
    /// "⋯" de toda sessão concluída, que é onde mora o que se faz de vez em quando.
    ///
    /// ⚠️ E o PARTICULAR (set/2026): para quem não tem convênio, o caixa É o pacote — é
    /// o único dinheiro daquela sessão, e sem fechamento ela some do financeiro para
    /// sempre. A pendência acende para ele também, sem pacote, e some pela mesma regra
    /// (qualquer lançamento vivo, inclusive "fica a receber", conta como fechado). O
    /// convênio sem pacote continua de fora: o dinheiro dele vem pela guia.
    /// </summary>
    public required bool FechamentoPendente { get; init; }

    /// <summary>
    /// A sessão está concluída — o balcão ainda pode abrir o fechamento para lançar o
    /// dinheiro, baixar insumo ou debitar o pacote, mesmo sem pendência anunciada.
    /// </summary>
    public bool PodeFechar => Etapa == EtapaFila.Finalizado;

    /// <summary>Só horário em aberto aceita falta/cancelamento — o cancelado e a falta já saíram.</summary>
    public bool EmAberto => Etapa is not (EtapaFila.Finalizado or EtapaFila.ForaDaFila);

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
        // ⚠️ "Chegou", "Chamar" e "Entrou" SAÍRAM (decisão da clínica, set/2026): ela não
        // quer o fluxo de fila. Quem registra a entrada na sala é o "Atender" do
        // profissional (`AgendaService.IniciarAtendimentoAsync`), e a linha vai de
        // Marcado direto para EM ATENDIMENTO. O balcão marca o horário e o profissional
        // atende — os dois passos que a direção pediu, e nada entre eles.
        //
        // O que sobra aqui é o que continua sendo do balcão:
        EtapaFila.EmAtendimento => "Concluir",
        // Concluída pelo Consultório e com pacote por debitar. Sem pendência não há passo
        // nenhum: a linha concluída é o registro do dia, e o fechamento continua a um
        // clique no "⋯".
        EtapaFila.Finalizado when FechamentoPendente => "Debitar pacote",
        _ => string.Empty
    };

    public bool TemProximoPasso => ProximoPasso.Length > 0;

    /// <summary>
    /// O próximo passo deste cartão é CONCLUIR — o único que pede <c>EditarAgenda</c>
    /// estrito. Os outros três (chegou, chamar, entrou) são movimento de fila.
    /// </summary>
    public bool ProximoPassoEhConcluir => Etapa == EtapaFila.EmAtendimento;

    /// <summary>
    /// O próximo passo deste cartão é FECHAR a sessão ("Debitar pacote") — ato do balcão,
    /// <c>EditarAgenda</c> estrito, como a guarda de <c>FecharSessaoAsync</c>. Enquanto o
    /// botão seguia a metade larga (<c>EditarAgenda</c> OU <c>MovimentarFila</c>), o perfil
    /// que só move a fila via "Debitar pacote" ACESO e levava a recusa depois do clique.
    /// </summary>
    public bool ProximoPassoEhFechar => Etapa == EtapaFila.Finalizado;


    /// <summary>
    /// Quem LANÇOU o horário, e quando (parcela 58). Vai na dica do cartão: o quadro é
    /// denso de propósito, e uma linha a mais por cartão custaria a densidade que faz o
    /// dia caber na tela.
    /// </summary>
    public required string Lancamento { get; init; }

    /// <summary>
    /// A dica do cartão: o que não coube nele e alguém pode precisar. É o LEITOR do que
    /// saiu do selo em set/2026 — a resposta da rodada de confirmação por extenso e a
    /// marca legada do "retorno do 2º código" —, para selo a menos não virar dado a menos.
    /// </summary>
    public string Detalhe => string.Join("\n", new[]
    {
        $"{Horario} · {Modalidade}",
        TemSala ? $"{Profissional} · sala {Sala}" : Profissional,
        TemPacote ? $"Pacote {PacoteUsadas}/{PacoteContratadas} (o que vence primeiro é o que a sessão debita)" : null,
        EhEncaixe ? "Encaixe — marcado por cima de um horário ocupado" : null,
        TemConfirmacao
            ? (ConfirmouPresenca
                ? "Confirmou a presença na rodada de confirmação"
                : "Avisado na rodada de confirmação, sem resposta")
            : null,
        EhRetornoDoSegundoCodigo ? "Legado: retorno do 2º código — guia não é atendimento, pode apagar" : null,
        string.IsNullOrWhiteSpace(Observacoes) ? null : $"Obs.: {Observacoes}",
        Lancamento
    }.Where(l => l is not null));
}

/// <summary>
/// Um chip do filtro por profissional no topo do quadro (redesenho da fila, ago/2026).
/// Numa clínica com mais de um profissional atendendo, a fila misturava todo mundo e a
/// pergunta "quem é da Dra. Ana agora?" só se respondia lendo cartão por cartão.
/// </summary>
public sealed partial class ChipProfissional : ObservableObject
{
    /// <summary>Nulo = "Todos".</summary>
    public int? Id { get; init; }

    public required string Nome { get; init; }

    /// <summary>Quantos horários vivos do dia o chip recorta (set/2026).</summary>
    public required int Quantidade { get; init; }

    /// <summary>"Dra. Ana · 5" — o número ao lado do nome, como no mockup aprovado.</summary>
    public string Rotulo => $"{Nome} · {Quantidade}";

    [ObservableProperty]
    private bool _ativo;
}

/// <summary>
/// A AGENDA DO DIA do balcão, em LISTA (set/2026): uma linha por horário, na ordem da
/// hora, com o status e a hora do fato em cada linha — a agenda do Smart Clinic que a
/// cliente usava, e o que ela pediu ("quanto mais simples, melhor"). Cancelado e falta
/// ficam na lista, apagados.
///
/// Ela nasceu kanban (parcela 26) e ganhou cinco raias com arrasto (parcelas 58 e 87),
/// por paridade com o que o balcão administrava. A cliente derrubou a premissa: para ela
/// a agenda do dia e a fila eram a MESMA pergunta em duas telas — e no Smart Clinic são
/// uma só. O Meu dia do médico já tinha virado lista (parcela 95); esta é a mesma lista,
/// com as ações do balcão. As etapas continuam saindo dos carimbos de chegada, de
/// chamada e de início (<see cref="Agendamento.Etapa"/>), não de um campo de status
/// novo: o faturamento continua vendo o mesmo <see cref="StatusAgendamento"/> de sempre.
///
/// O CHAMADO é o recado do consultório (parcela 38). Quem atende está na sala com a porta
/// fechada e não grita o nome de ninguém: ele clica em "Chamar próximo" no app dele, e é
/// ESTA tela que anuncia a pessoa. Não há sincronização entre os dois módulos — nem fila
/// de mensagens, nem evento: eles leem a mesma linha do banco, e é isso que faz as duas
/// listas nunca divergirem.
///
/// As cinco coleções por etapa continuam existindo por baixo (a faixa CHAMANDO e o
/// contador leem delas); a tela lê <see cref="Linhas"/>, que é o dia inteiro em ordem.
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

    // O que acompanha o dia lido — preenchidos junto do _doDia e lidos pelo
    // MontarQuadro, que o filtro por profissional chama sem voltar ao banco.
    private HashSet<int> _comPendencia = [];
    private Dictionary<int, IReadOnlyList<SituacaoTermo>> _termos = [];
    private Dictionary<int, StatusContato> _confirmacoes = [];
    private Dictionary<int, (int Usadas, int Contratadas)> _pacotes = [];

    /// <summary>
    /// Atendimentos do dia que JÁ tiveram fechamento (pacote, dinheiro ou insumo). É o que
    /// faz o botão "Fechar sessão" SUMIR da raia FINALIZADO depois de resolvido — desde a
    /// parcela 95 a sessão chega ali concluída pelo Consultório, e o que resta ao balcão é
    /// o dinheiro. Pendência que não some é pendência que ninguém lê.
    /// </summary>
    private HashSet<int> _jaFechados = [];

    /// <summary>
    /// O DIA INTEIRO, na ordem da hora — o que a lista mostra (set/2026). Inclui cancelado
    /// e falta, que a coleção acima não tem: eles ficam na lista, apagados.
    /// </summary>
    public ObservableCollection<CartaoFila> Linhas { get; } = [];

    /// <summary>
    /// O dia só está vazio sem NENHUM horário — um paciente já concluído, ou um cancelado,
    /// é dia com movimento, não dia vazio.
    /// </summary>
    public bool QuadroVazio => Linhas.Count == 0;

    /// <summary>Chips do filtro por profissional. "Todos" + quem tem horário no dia.</summary>
    public ObservableCollection<ChipProfissional> Profissionais { get; } = [];

    /// <summary>O quadro só filtra com DOIS ou mais profissionais no dia — chip único é ruído.</summary>
    public bool TemFiltroProfissional => Profissionais.Count > 2;

    /// <summary>O filtro vigente (nulo = todos). Vive fora dos chips para sobreviver à recarga.</summary>
    private int? _filtroProfissionalId;

    [ObservableProperty]
    private DateTime _dia = DateTime.Today;

    /// <summary>O dia aberto é hoje — a pílula "hoje" ao lado da data (set/2026).</summary>
    public bool EhHoje => Dia.Date == DateTime.Today;

    [ObservableProperty]
    private bool _carregando;

    /// <summary>
    /// O PLACAR do dia (set/2026 — a agenda com cor, mockup aprovado): cada número com o
    /// glifo semântico e a cor que a métrica já tem no sistema, no lugar da frase corrida
    /// "3 atendido(s) · 1 em sala".
    ///
    /// ⛔ A "espera média" saiu quando os botões de fila saíram: ela vai da CHEGADA do
    /// paciente até a chamada, e sem check-in nenhum horário carimba chegada — o número
    /// seria "—" todo dia. A conta segue no <c>PainelRecepcaoService</c>, certa e testada,
    /// esperando a tela que a devolva.
    /// </summary>
    [ObservableProperty] private int _atendidos;
    [ObservableProperty] private int _emSala;

    /// <summary>"1 · 0" — faltas e cancelamentos, que estão na lista apagados.</summary>
    [ObservableProperty] private string _faltasCancelamentos = "0 · 0";

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
    /// que metade nenhuma: ela mente sobre o que a pessoa pode fazer.
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
    /// Falta e cancelamento pedem SÓ <c>EditarAgenda</c> — nenhum dos dois gera guia, e
    /// as guardas dos comandos exigem exatamente esse bit. A metade visível era
    /// <see cref="PodeConcluirSessao"/> (o E com <c>LancarAtendimento</c>): se a direção
    /// tirasse `LancarAtendimento` de uma recepcionista — o gesto que a granularidade
    /// existe para permitir —, ela perdia em SILÊNCIO a falta e o cancelamento na Fila,
    /// capacidades que a guarda lhe dá e o menu escondia.
    /// </summary>
    public bool PodeMarcarFaltaOuCancelar => SessaoUsuario.Atual.Pode(Permissao.EditarAgenda);

    /// <summary>
    /// Fechar a sessão (pacote, insumo, caixa) é ato do balcão: <c>EditarAgenda</c> estrito,
    /// a MESMA conta do <c>Exigir</c> de <c>FecharSessaoAsync</c>. É a metade visível do
    /// botão "Debitar pacote" e do item do menu "⋯" (set/2026).
    /// </summary>
    public bool PodeFecharSessao => SessaoUsuario.Atual.Pode(Permissao.EditarAgenda);

    /// <summary>
    /// Colher o termo é ato de outro bit — a técnica de enfermagem o tem e não tem o da
    /// agenda —, e por isso ele é perguntado à parte no menu "⋯". É a metade visível que
    /// faltava: enquanto o menu montava os itens só pelo ESTADO do cartão, o XAML já
    /// prometia que "quem decide item a item é o próprio menu", e as guardas recusavam
    /// depois do clique.
    /// </summary>
    public bool PodeColherTermo => SessaoUsuario.Atual.Pode(Permissao.ColherAssinaturaPaciente);

    /// <summary>
    /// A metade visível do "Conferir convênio e cota…". É <c>VerFichaPaciente</c>, e não
    /// <c>VerProntuario</c>, pelo corte da parcela 49: carteirinha, cota, dívida e guia são
    /// dado CADASTRAL e de convênio — quem recebe no balcão precisa deles e não precisa da
    /// evolução.
    /// </summary>
    public bool PodeVerFicha => SessaoUsuario.Atual.Pode(Permissao.VerFichaPaciente);

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

    /// <summary>
    /// A tela ENTROU EM CENA: liga o relógio e relê o dia por baixo (set/2026 — pedido
    /// da cliente: "precisamos da sincronização/atualização da agenda com a mudança de
    /// status").
    ///
    /// ⚠️ A releitura ao voltar é a metade que faltava, e ela não é a batida do relógio.
    /// Agenda e lista do dia são ABAS do mesmo item: o shell monta cada aba UMA vez e a
    /// guarda, então voltar a ela devolve a mesma tela com os dados de quando a pessoa
    /// saiu. Quem marcava a chegada na lista e passava para a grade via a grade de
    /// antes — e o relógio, que só bate de minuto em minuto e só enquanto a tela está
    /// visível, não cobre esse instante: ele havia PARADO junto com a tela.
    ///
    /// Sem a guarda de "já esteve em cena" seriam DUAS leituras na abertura: o
    /// construtor já dispara a primeira, e o `Loaded` chega logo atrás.
    /// </summary>
    public void AoEntrarEmCena()
    {
        _relogio.Start();
        if (_jaEsteveEmCena) _ = RelerAoVoltarAsync();
        _jaEsteveEmCena = true;
    }

    /// <summary>Ver <see cref="AoEntrarEmCena"/>.</summary>
    private bool _jaEsteveEmCena;

    /// <summary>
    /// A releitura de quando a tela volta à vista. Silenciosa como a do relógio (nada de
    /// "Carregando" piscando nem aviso vermelho por uma demora do banco), e sem a recusa
    /// de "só HOJE" que a batida periódica tem: aquela existe para a tela não se mexer
    /// sozinha enquanto alguém lê, e esta acontece UMA vez, no instante em que a pessoa
    /// chega — que é exatamente quando ela quer o estado de agora. Cancelar um horário de
    /// amanhã numa aba e ver a outra desatualizada seria o mesmo defeito noutro dia.
    /// </summary>
    private async Task RelerAoVoltarAsync()
    {
        if (Carregando) return;

        try
        {
            await CarregarAsync(silencioso: true);
        }
        catch (Exception ex)
        {
            Clinica.Application.Diagnostico.Registrar(
                "Recepção — releitura da lista do dia ao voltar à aba falhou", ex);
        }
    }

    /// <summary>Desliga o relógio (chamado quando a tela sai de cena).</summary>
    public void AoSairDeCena() => _relogio.Stop();

    partial void OnDiaChanged(DateTime value)
    {
        OnPropertyChanged(nameof(EhHoje));
        _ = CarregarAsync();
    }

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

            // O selo da rodada de confirmação ("Confirmou" / "Não confirmou"): a rodada
            // grava a resposta desde a parcela 5, e o quadro nunca a mostrou. Aviso, não
            // conteúdo — falha não derruba a fila.
            Dictionary<int, StatusContato> confirmacoes;
            try
            {
                var repo = escopo.ServiceProvider.GetRequiredService<IClinicaRepositorio>();
                confirmacoes = new Dictionary<int, StatusContato>(
                    await repo.ConfirmacoesDosAgendamentosAsync(_doDia.Select(a => a.Id).ToList()));
            }
            catch (Exception ex)
            {
                Clinica.Application.Diagnostico.Registrar(
                    "Recepção — confirmações do dia não puderam ser lidas", ex);
                confirmacoes = [];
            }

            // O pacote ativo (usadas/contratadas): a 10ª sessão de um pacote de 10 se
            // descobre na MARCAÇÃO do dia, não no Finalizar (a lição da parcela 48). Só
            // quem TEM pacote ativo entra — metade da clínica é de convênio e nunca
            // comprou. Quem decide se vira selo ou só linha de contexto é `SelosDaFila`.
            Dictionary<int, (int Usadas, int Contratadas)> pacotes;
            try
            {
                var repo = escopo.ServiceProvider.GetRequiredService<IClinicaRepositorio>();
                var hoje = DateOnly.FromDateTime(Dia);
                var todos = await repo.PacotesDosPacientesAsync(
                    _doDia.Select(a => a.PacienteId).Distinct().ToList());
                pacotes = todos
                    .Where(p => p.PodeConsumir(hoje) && p.SessoesContratadas is not null)
                    .GroupBy(p => p.PacienteId)
                    .ToDictionary(
                        g => g.Key,
                        g =>
                        {
                            // O que vence primeiro é o que a baixa automática debita.
                            var p = g.OrderBy(x => x.ValidoAte ?? DateOnly.MaxValue).First();
                            return (p.SessoesUsadas, p.SessoesContratadas!.Value);
                        });
            }
            catch (Exception ex)
            {
                Clinica.Application.Diagnostico.Registrar(
                    "Recepção — pacotes do dia não puderam ser lidos", ex);
                pacotes = [];
            }

            // O que já foi FECHADO (pacote/dinheiro/insumo) entre as sessões concluídas.
            // Em lote: uma ida por cartão daria trinta a cada batida do relógio. Aviso,
            // não conteúdo — falhando, a pendência aparece em toda sessão COM PACOTE, que
            // é o lado seguro: oferecer debitar o que já foi debitado custa um clique (e a
            // janela recusa a duplicata), esconder o pacote custa a sessão de graça.
            HashSet<int> jaFechados;
            try
            {
                var repo = escopo.ServiceProvider.GetRequiredService<IClinicaRepositorio>();
                var concluidos = _doDia
                    .Where(a => a.Etapa == EtapaFila.Finalizado && a.AtendimentoId is not null)
                    .Select(a => a.AtendimentoId!.Value)
                    .ToList();

                jaFechados = concluidos.Count == 0
                    ? []
                    : (await repo.AtendimentosComFechamentoAsync(concluidos)).ToHashSet();
            }
            catch (Exception ex)
            {
                Clinica.Application.Diagnostico.Registrar(
                    "Recepção — fechamentos do dia não puderam ser lidos", ex);
                jaFechados = [];
            }

            if (geracao != _geracaoCarga) return;

            _comPendencia = comPendencia;
            _termos = termos;
            _confirmacoes = confirmacoes;
            _pacotes = pacotes;
            _jaFechados = jaFechados;

            MontarChips();
            MontarQuadro();
            AtualizarEsperas();
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

    /// <summary>
    /// Reconstrói os chips do filtro a partir do dia lido. O filtro vigente sobrevive à
    /// recarga só enquanto o profissional continua no dia — apontando para quem saiu, ele
    /// esconderia o quadro inteiro em silêncio.
    /// </summary>
    private void MontarChips()
    {
        var doDia = _doDia
            .Where(a => a.Etapa != EtapaFila.ForaDaFila && a.ProfissionalId is not null)
            .GroupBy(a => a.ProfissionalId!.Value)
            .Select(g => (Id: g.Key, Nome: g.First().Profissional?.Rotulo ?? $"Profissional {g.Key}"))
            .OrderBy(p => p.Nome)
            .ToList();

        if (_filtroProfissionalId is { } vigente && doDia.All(p => p.Id != vigente))
            _filtroProfissionalId = null;

        var vivos = _doDia.Where(a => a.Etapa != EtapaFila.ForaDaFila).ToList();

        Profissionais.Clear();
        Profissionais.Add(new ChipProfissional
        {
            Id = null, Nome = "Todos", Quantidade = vivos.Count,
            Ativo = _filtroProfissionalId is null
        });
        foreach (var p in doDia)
            Profissionais.Add(new ChipProfissional
            {
                Id = p.Id, Nome = p.Nome,
                Quantidade = vivos.Count(a => a.ProfissionalId == p.Id),
                Ativo = _filtroProfissionalId == p.Id
            });

        OnPropertyChanged(nameof(TemFiltroProfissional));
    }

    /// <summary>
    /// (Re)monta a lista do dia a partir do que JÁ FOI LIDO — é o que o filtro por
    /// profissional chama sem voltar ao banco. Entre o Clear e o último Add não há await
    /// (a regra da parcela 62): quem monta é sempre uma passada síncrona.
    /// </summary>
    private void MontarQuadro()
    {
        Linhas.Clear();

        // Na ordem da HORA: é a agenda do dia, não um quadro por estado. Cancelado e falta
        // ENTRAM (apagados na tela) — quem lê às 14h precisa saber que as 15h vagaram.
        foreach (var a in _doDia.OrderBy(a => a.DataHora))
        {
            // Horário sem profissional só aparece em "Todos": ele não é de ninguém.
            if (_filtroProfissionalId is { } soDele && a.ProfissionalId != soDele) continue;

            var vivo = a.Etapa != EtapaFila.ForaDaFila;

            // O selo de confirmação só fala em AGUARDANDO — depois do check-in a pessoa
            // está aqui, e o selo viraria ruído.
            var confirmacao = a.Etapa == EtapaFila.Aguardando
                              && _confirmacoes.TryGetValue(a.Id, out var status)
                ? status switch
                {
                    StatusContato.Respondido => "Confirmou",
                    StatusContato.Enviado => "Não confirmou",
                    _ => string.Empty
                }
                : string.Empty;

            var cartao = new CartaoFila
            {
                AgendamentoId = a.Id,
                PacienteId = a.PacienteId,
                Horario = $"{a.DataHora:HH:mm}–{a.FimPrevisto:HH:mm}",
                Paciente = a.Paciente?.Nome ?? "(paciente removido)",
                Foto = a.Paciente?.FotoMiniatura,
                Convenio = a.Paciente?.ConvenioNome ?? string.Empty,
                // Nome do CATÁLOGO, nunca o enum: `ToString()` escrevia
                // "AcupunturaComEletro" no cartão que o médico lê (parcela 41).
                Modalidade = CatalogoModalidades.Nome(
                    a.ModalidadeCodigo ?? a.ModalidadePrevista.ToString()),
                ModalidadeFamilia = a.ModalidadePrevista,
                // "sem profissional" é o que a GRADE já escreve, e é aviso, não
                // enfeite: horário sem dono some do "Meu dia" de quem atende e do
                // repasse (parcela 69). O travessão calava isso.
                Profissional = a.Profissional?.Rotulo ?? "sem profissional",
                Sala = a.Sala?.Nome ?? string.Empty,
                Etapa = a.Etapa,
                Situacao = a.Status,
                DataHora = a.DataHora,
                ChegadaEm = a.ChegadaEm,
                InicioEm = a.InicioAtendimentoEm,
                Observacoes = a.Observacoes,
                EhRetornoDoSegundoCodigo = a.Origem == OrigemAgendamento.RetornoSugerido,
                EhEncaixe = a.Encaixe,
                Lancamento = DescreverLancamento(a),
                // Os avisos do PACIENTE (guia, termo, pacote) não entram na linha cancelada
                // ou de falta: ela não tem passo a cobrar, e selo numa linha apagada é ruído
                // que ensina a ignorar o selo da linha viva ao lado.
                TemGuiaPendente = vivo && _comPendencia.Contains(a.PacienteId),
                TemTermoPendente = vivo && _termos.TryGetValue(a.PacienteId, out var doPaciente)
                                   && doPaciente.Any(t => t.Pendente),
                ConfirmacaoRotulo = confirmacao,
                ConfirmouPresenca = confirmacao == "Confirmou",
                PacoteUsadas = vivo && _pacotes.TryGetValue(a.PacienteId, out var pacote) ? pacote.Usadas : null,
                PacoteContratadas = vivo && _pacotes.TryGetValue(a.PacienteId, out pacote) ? pacote.Contratadas : null,
                FimAtendimentoEm = a.FimAtendimentoEm,
                AtendimentoEncerrado = a.AtendimentoEncerrado,
                // Só a sessão concluída pede fechamento, só enquanto ele não aconteceu e
                // só quando há PACOTE — ver `FechamentoPendente`. Sem `AtendimentoId` não
                // há o que fechar (nem deveria haver cartão finalizado sem ele; se
                // houver, não se inventa pendência).
                FechamentoPendente = a.Etapa == EtapaFila.Finalizado
                                     && a.AtendimentoId is { } atendimentoId
                                     && !_jaFechados.Contains(atendimentoId)
                                     && (_pacotes.ContainsKey(a.PacienteId) || EhParticular(a.Paciente)),
                EncerradoEm = a.FimAtendimentoEm is { } fim
                    ? $"Encerrado às {fim:HH\\:mm}"
                    : string.Empty
            };

            Linhas.Add(cartao);
        }

        // Quem já SAIU da sala (parcela 74) não sobe mais para a frente: a lista é a ordem
        // da hora, e é o selo "Encerrado" (e a cor do status) que o aponta.

        // A lista mudou: a tela precisa reavaliar se o dia está vazio.
        //
        // ⚠️ A faixa "CHAMANDO" saiu daqui junto com o botão Chamar (set/2026): sem quem
        // chamar, não há recado a anunciar. A etapa `Chamado` continua existindo em
        // `StatusDaFila` porque os horários registrados ANTES desta mudança a têm — o
        // que sumiu foi a porta, não o vocabulário.
        OnPropertyChanged(nameof(QuadroVazio));
    }

    /// <summary>
    /// Filtra o quadro por profissional. É LEITURA — sem <c>Exigir</c>, como todo filtro
    /// da suíte — e remonta das listas já lidas, sem ir ao banco.
    /// </summary>
    [RelayCommand]
    private void Filtrar(ChipProfissional? chip)
    {
        if (chip is null) return;

        _filtroProfissionalId = chip.Id;
        foreach (var p in Profissionais) p.Ativo = p.Id == chip.Id;
        MontarQuadro();
        AtualizarEsperas();
    }

    /// <summary>Recalcula os rótulos de espera sem ir ao banco (chamado a cada minuto).</summary>
    private void AtualizarEsperas()
    {
        var agora = DateTime.Now;
        var porId = _doDia.ToDictionary(a => a.Id);

        foreach (var cartao in Linhas)
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

            // O atraso é a pergunta que o quadro nunca respondeu: a hora estourou e o
            // paciente não chegou. A conta mora no DOMÍNIO (`Agendamento.AtrasoMinutos`).
            var atraso = ag.AtrasoMinutos(agora);
            cartao.Atraso = atraso is null ? string.Empty : $"Atrasado {atraso} min";
            cartao.Atrasado = atraso is not null;
            cartao.AtrasoMinutos = atraso;

            // A hora do fato sob o status — a MESMA redação do Meu dia (StatusDaFila).
            cartao.StatusDetalhe = StatusDaFila.Detalhe(
                cartao.Situacao, cartao.Etapa, cartao.ChegadaEm, minutos, desdeAChamada,
                cartao.InicioEm, cartao.FimAtendimentoEm);
        }

        Atendidos = _doDia.Count(a => a.Status == StatusAgendamento.Realizado);
        EmSala = _doDia.Count(a => a.Etapa == EtapaFila.EmAtendimento);
        // Falta e cancelamento ESTÃO na lista (apagados) desde set/2026 — o placar conta,
        // não anuncia o que foi escondido.
        FaltasCancelamentos =
            $"{_doDia.Count(a => a.Status == StatusAgendamento.Faltou)} · "
            + $"{_doDia.Count(a => a.Status == StatusAgendamento.Cancelado)}";
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
            // Chegada, chamada e entrada saíram (ver `CartaoFila.ProximoPasso`): quem
            // registra a entrada é o "Atender" do profissional.
            case EtapaFila.EmAtendimento: await FinalizarAsync(cartao); break;
            case EtapaFila.Finalizado: await FecharSessaoAsync(cartao); break;
        }
    }

    /// <summary>
    /// FECHAR a sessão já concluída: pacote, insumo e caixa (parcela 95).
    ///
    /// É a metade do <see cref="FinalizarAsync"/> que sobrou no balcão quando a conclusão
    /// passou para o Consultório. A janela é a MESMA — nada foi reimplementado —, e o
    /// serviço já sabia reaproveitar a presença confirmada em vez de reconfirmá-la
    /// (<c>GarantirAtendimentoAsync</c>, desde a parcela 65): é essa reutilização que faz
    /// o caminho existir sem uma segunda definição de "fechar a sessão".
    ///
    /// ⚠️ É COMANDO porque tem DUAS portas: o botão de passo (que só aparece quando há
    /// pacote por debitar) e o item do menu "⋯", disponível em toda sessão concluída —
    /// insumo e caixa não são anunciados como pendência, e sem o item do menu eles não
    /// teriam por onde ser lançados depois que o médico concluiu.
    /// </summary>
    /// <summary>
    /// O convênio da ficha NÃO gera guia (parcela 60). Síncrono, do cache do catálogo —
    /// o quadro relê a cada minuto e não pode pagar uma consulta por cartão para isso.
    /// </summary>
    private static bool EhParticular(Paciente? paciente)
        => paciente is not null
           && !Clinica.Domain.Regras.CatalogoConvenios.GeraGuia(
               paciente.ConvenioCodigo ?? paciente.Convenio.ToString());

    [RelayCommand]
    private async Task FecharSessaoAsync(CartaoFila? cartao)
        => await ExecutarAsync(cartao, async c =>
        {
            SessaoUsuario.Atual.Exigir(Permissao.EditarAgenda, "fechar a sessão");

            var vm = new FechamentoSessaoViewModel(_escopos, c.AgendamentoId);
            var janela = new Janelas.FechamentoSessaoWindow(vm)
            {
                Owner = JanelaDona.Atual()
            };

            if (janela.ShowDialog() != true || janela.Resultado is not { } resultado)
            {
                // Fechar a janela não desfaz nada: a sessão continua concluída e a guia
                // no faturamento. O que fica pendente é só o dinheiro — e ele continua
                // pendente, com o botão aceso, que é o certo.
                _snackbar.Info($"Sessão de {c.Paciente} continua sem o fechamento do "
                               + "pacote, do insumo e do caixa.");
                return;
            }

            AnunciarFechamento(c, resultado);
        }, "fechamento da sessão");

    // O ARRASTO entre raias (parcelas 58 e 87) morreu com as raias: numa lista ordenada
    // pela hora não há para onde arrastar. O que ele fazia — andar um passo — é o botão
    // de passo de cada linha, com a mesma regra e a mesma guarda.

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
    ///
    /// ⚠️ Desde set/2026 este é o caminho de EXCEÇÃO: com os botões de fila fora, quem
    /// conclui é o "Finalizar atendimento" do profissional (parcela 95). O botão fica
    /// para o caso contrário — a sessão aconteceu e ninguém finalizou pelo consultório.
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

            // ===== SEM CONVÊNIO NÃO SE LANÇA (parcela 92) =====
            //
            // Concluir aqui é o que CRIA o atendimento, então é aqui que a regra do
            // serviço (`AtendimentoService.MontarAsync`) recusaria — e recusaria por
            // exceção, que nesta tela vira `_snackbar.Erro`: quatro segundos para uma
            // frase que pede uma decisão, no único momento do mês em que o paciente está
            // presente para tomá-la. Perguntar antes é o que transforma a recusa em
            // resposta, e esta é a porta que a clínica usa o dia inteiro.
            if (!await VinculoDeConvenio.GarantirAsync(_escopos, c.PacienteId, SessaoUsuario.Atual.Operador))
            {
                // Diálogo, não snackbar: é erro que exige correção, e o caminho tem de
                // continuar na tela enquanto a recepcionista resolve.
                _dialogo.Aviso($"Sem convênio — {c.Paciente}",
                    $"{c.Paciente} está com o convênio \"a definir\" (a ficha veio do sistema "
                    + "anterior sem convênio), e sem convênio o atendimento não gera guia.\n\n"
                    + "A sessão NÃO foi concluída. Escolha o convênio do paciente — pelo botão "
                    + "Concluir de novo, ou na ficha dele — e conclua em seguida. Quem paga do "
                    + "bolso entra no convênio particular.");
                return;
            }

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

            // Sem decisão = convênio sem pacote e sem insumo: a guia já está no faturamento
            // e o dinheiro vem pela conciliação dela. O PARTICULAR nunca cai aqui desde
            // set/2026 — para ele há sempre a pergunta de como a sessão foi paga.
            if (!registro.TemDecisao)
            {
                _snackbar.Sucesso(registro.GuiasGeradas == 0
                    ? $"Sessão de {c.Paciente} concluída — sem guia a faturar."
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

            AnunciarFechamento(c, resultado, registro.GuiasGeradas);
        }, "conclusão do atendimento");

    /// <summary>
    /// A frase do que ACONTECEU no fechamento — uma só, para as DUAS portas (o Concluir
    /// da coluna EM ATENDIMENTO e o Fechar sessão da FINALIZADO). Duas montagens
    /// divergiriam na primeira correção, e o que elas descrevem é o mesmo ato.
    ///
    /// <paramref name="guias"/> é nulo quando a sessão já estava concluída: dizer "0
    /// guia(s)" ali seria falso — as guias existem, só não nasceram neste clique.
    /// </summary>
    private void AnunciarFechamento(CartaoFila c, ResultadoFechamento resultado, int? guias = null)
    {
        var partes = new List<string>();
        if (guias is { } quantas) partes.Add($"{quantas} guia(s)");
        if (resultado.Consumo is not null) partes.Add("1 sessão do pacote");
        if (resultado.Movimentos.Count > 0) partes.Add($"{resultado.Movimentos.Count} insumo(s)");
        if (resultado.Lancamento is not null) partes.Add("entrada no caixa");

        _snackbar.Sucesso(partes.Count == 0
            ? $"Sessão de {c.Paciente} fechada — nada a debitar nem a lançar."
            : $"Sessão de {c.Paciente} fechada — {string.Join(" · ", partes)}.");
    }

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

    /// <summary>
    /// CONFERIR CONVÊNIO E COTA com o paciente na frente — a porta que o check-in levou
    /// embora (set/2026).
    ///
    /// ⚠️ Ela existe porque tirar um botão não pode tirar um AVISO junto. Carteirinha
    /// vencida, cota do convênio estourada, dívida em aberto e glosa a recorrer chegavam
    /// ao balcão pelo clique de "Chegou", e o comentário daquele código dizia por quê: é o
    /// ÚLTIMO momento barato — com a pessoa aqui, resolve-se com um telefonema; depois da
    /// sessão a mesma informação só vira glosa. Sem os botões de fila, o balcão ficaria
    /// com os selos da linha (termo, guia, pacote) e SEM os quatro do
    /// <see cref="ElegibilidadeService"/>. Alerta que perde a porta é a parcela 48 pelo
    /// avesso.
    ///
    /// É ato do "⋯" e não conferência automática de toda linha, pela regra de sempre: a
    /// conferência custa quatro consultas POR PACIENTE, e rodá-la nos trinta cartões do
    /// dia tornaria a tela lenta para entregar um aviso que só importa quando alguém está
    /// no balcão. O que se faz de vez em quando mora no botão.
    ///
    /// Falha NÃO passa calada nem vira "tudo certo": vira o terceiro estado escrito.
    /// </summary>
    [RelayCommand]
    private async Task ConferirElegibilidadeAsync(CartaoFila? cartao)
        => await ExecutarAsync(cartao, async c =>
        {
            var recados = new List<string>();
            if (c.TemGuiaPendente)
                recados.Add("Tem GUIA PENDENTE de baixa — aproveite que ele está aqui e peça "
                            + "o documento; depois a cobrança vira telefonema.");

            try
            {
                using var escopo = _escopos.CreateScope();
                var elegibilidade = escopo.ServiceProvider
                    .GetRequiredService<ElegibilidadeService>();

                var resultado = await elegibilidade.ConferirAsync(
                    c.PacienteId, DateOnly.FromDateTime(Dia));

                recados.AddRange(resultado.Alertas.Select(a => a.Descricao));
            }
            catch (Exception ex)
            {
                Clinica.Application.Diagnostico.Registrar(
                    "Recepção — elegibilidade não pôde ser conferida", ex);
                recados.Add("Não foi possível conferir carteirinha e cota agora — "
                            + "confira na ficha do paciente.");
            }

            _dialogo.Aviso($"{c.Paciente} — convênio e cota",
                recados.Count > 0
                    ? string.Join("\n\n", recados)
                    : "Nada a resolver: carteirinha em dia, cota disponível, sem guia "
                      + "pendente e sem conta vencida.");
        }, "conferência de convênio e cota");

    [RelayCommand]
    private async Task MarcarFaltaAsync(CartaoFila? cartao)
        => await ExecutarAsync(cartao, async c =>
        {
            SessaoUsuario.Atual.Exigir(Permissao.EditarAgenda, "mexer na fila do dia");

            IReadOnlyList<string> avisos;
            using (var e = _escopos.CreateScope())
                avisos = await e.ServiceProvider.GetRequiredService<AgendaService>()
                    .MarcarFaltaAsync(c.AgendamentoId, SessaoUsuario.Atual.Operador);

            // Os avisos de GUIA em diálogo, nunca descartados nem em snackbar: no regime
            // "guia no agendamento" eles incluem "guia JÁ BAIXADA no portal — a sessão
            // caiu; confira com o convênio", que exige ação de quem está no balcão.
            // É a mesma regra do Concluir logo acima (parcela 62).
            if (avisos.Count > 0)
                _dialogo.Aviso($"Atenção — {c.Paciente}", string.Join("\n\n", avisos));
            _snackbar.Info($"{c.Paciente} marcado como falta.");
        }, "marcação de falta");

    [RelayCommand]
    private async Task CancelarAsync(CartaoFila? cartao)
        => await ExecutarAsync(cartao, async c =>
        {
            SessaoUsuario.Atual.Exigir(Permissao.EditarAgenda, "mexer na fila do dia");

            IReadOnlyList<string> avisos;
            using (var e = _escopos.CreateScope())
                avisos = await e.ServiceProvider.GetRequiredService<AgendaService>()
                    .CancelarAsync(c.AgendamentoId, SessaoUsuario.Atual.Operador);

            // Mesma regra da falta: aviso de guia é ação, não confirmação passageira.
            if (avisos.Count > 0)
                _dialogo.Aviso($"Atenção — {c.Paciente}", string.Join("\n\n", avisos));
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

    /// <summary>Metade visível do "Novo horário": marcar é ato do balcão (<c>EditarAgenda</c>).</summary>
    public bool PodeMarcar => SessaoUsuario.Atual.Pode(Permissao.EditarAgenda);

    /// <summary>
    /// "Novo horário" na lista do dia (set/2026) — o "+ adicionar" da agenda do Smart
    /// Clinic. É a MESMA ponte da grade (`PreenchimentoNovoAtendimento` → aba Marcar do
    /// Atendimento), com o dia da lista e o profissional do filtro já preenchidos. Quem
    /// tem só <c>EditarAgenda</c>, sem <c>LancarAtendimento</c>, não alcança a aba Marcar:
    /// para esse perfil a lista leva à GRADE, onde o formulário de sempre continua.
    /// </summary>
    [RelayCommand]
    private void NovoHorario()
    {
        try
        {
            SessaoUsuario.Atual.Exigir(Permissao.EditarAgenda, "marcar um horário");

            using var scope = _escopos.CreateScope();
            var ponte = scope.ServiceProvider.GetRequiredService<PreenchimentoNovoAtendimento>();
            ponte.Definir(new PedidoNovoAtendimento(
                MarcarParaDepois: true, DataHora: Dia.Date,
                ProfissionalId: _filtroProfissionalId, SalaId: null));

            if (NavegacaoSuite.Ir(Clinica.Recepcao.Modulo.ModuloRecepcao.ChaveMarcarHorario)) return;

            // Não foi: o pedido é DESFEITO (pedido órfão pré-preencheria a abertura de
            // amanhã com o clique de hoje) e a grade abre neste dia.
            ponte.Consumir();
            AbrirGradeNoDia(scope);
        }
        catch (Exception ex)
        {
            Clinica.Application.Diagnostico.Registrar("Recepção — novo horário pela agenda do dia", ex);
            _snackbar.Erro(ex.Message);
        }
    }

    /// <summary>
    /// "Abrir na grade" (menu "⋯"): leva ao vão deste dia na GRADE, onde moram remarcar,
    /// reabrir o cancelado, o comprovante e o WhatsApp de confirmação — a janela do horário
    /// da parcela 58. A lista não repete essas sete ações: duas janelas do horário
    /// divergiriam na primeira correção. É LEITURA: sem <c>Exigir</c>.
    /// </summary>
    [RelayCommand]
    private void AbrirNaGrade(CartaoFila? cartao)
    {
        try
        {
            using var scope = _escopos.CreateScope();
            AbrirGradeNoDia(scope);
        }
        catch (Exception ex)
        {
            Clinica.Application.Diagnostico.Registrar("Recepção — abrir a grade pela agenda do dia", ex);
            _snackbar.Erro(ex.Message);
        }
    }

    /// <summary>Pede à grade que abra no dia da lista (a ponte <see cref="PedidoAgenda"/>) e navega.</summary>
    private void AbrirGradeNoDia(IServiceScope scope)
    {
        scope.ServiceProvider.GetRequiredService<PedidoAgenda>().AbrirEm(DateOnly.FromDateTime(Dia));
        if (!NavegacaoSuite.Ir(Clinica.Recepcao.Modulo.ModuloRecepcao.ChaveAgenda))
            _snackbar.Info("A grade não está disponível para o seu acesso.");
    }

    [RelayCommand]
    private void DiaAnterior() => Dia = Dia.AddDays(-1);

    [RelayCommand]
    private void ProximoDia() => Dia = Dia.AddDays(1);

    [RelayCommand]
    private void Hoje() => Dia = DateTime.Today;
}
