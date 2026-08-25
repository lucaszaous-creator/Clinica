using System.Collections.ObjectModel;
using Clinica.Application.Abstracoes;
using Clinica.Application.Modelos;
using Clinica.Application.Servicos;
using Clinica.Desktop.Controls;
using Clinica.Desktop.Shell.Componentes;
using Clinica.Desktop.Shell.Configuracao;
using Clinica.Desktop.Shell.Modulos;
using Clinica.Domain;
using Clinica.Domain.Entities;
using Clinica.Domain.Regras;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;

namespace Clinica.Recepcao.ViewModels;

/// <summary>
/// Um código recém-gerado, e a informação que o balcão precisa dar ao paciente: esta guia
/// já está liberada para o faturamento cuidar hoje, ou só a partir de quando?
///
/// O 1º código nasce faturável na hora; o 2º só a partir de +24h — que é o defeito que dá
/// nome ao produto, e é justamente o que esta linha existe para tornar visível no momento
/// em que a guia nasce.
/// </summary>
public sealed record CodigoLancado(CodigoFaturamento Codigo, bool Liberada, string? Impedimento)
{
    public bool Baixado => Codigo.Baixado;

    // Os rótulos saem resolvidos daqui, e não de conversor de XAML: os conversores
    // `EnumDescricao` e `CodigoEspecialidade` são do design system do FATURAMENTO e não
    // existem na suíte. Duplicá-los para uma tela seria pagar o débito do design system
    // uma terceira vez; o padrão daqui é o `LinhaPendencia` do Gerente — resolver no VM.
    public string TipoRotulo => RotularTipo(Codigo.Tipo);

    public string EspecialidadeRotulo => Codigo.EspecialidadeCodigo is { } cod
        ? CatalogoEspecialidades.Nome(cod)
        : "—";

    public string OrdemRotulo => Codigo.Ordem == OrdemCodigo.Segundo ? "2º" : "1º";

    public string FaturarEm => Codigo.DataPrevistaFaturamento.ToString("dd/MM/yyyy");

    public string ComoObter => Codigo.FormaObtencao switch
    {
        FormaObtencao.NaoAplica => "—",
        FormaObtencao.App => "Pelo app (QR Code)",
        FormaObtencao.Sistema => "Pelo sistema",
        FormaObtencao.Ligacao => "Ligar para o paciente",
        _ => Codigo.FormaObtencao.ToString()
    };

    /// <summary>
    /// O que o balcão diz ao paciente. Três estados, e o do meio é o motivo de o produto
    /// existir: a guia que só libera depois de amanhã é a que se esquece.
    /// </summary>
    public string Situacao => Baixado
        ? "Já baixada pelo faturamento"
        : Impedimento ?? "Liberada — o faturamento já pode dar baixa";

    public bool TemImpedimento => Impedimento is not null;

    internal static string RotularTipo(TipoCodigo t) => t switch
    {
        TipoCodigo.ConsultaEspecialidade => "Consulta de especialidade",
        TipoCodigo.Eletroacupuntura => "Eletroacupuntura",
        TipoCodigo.Bsv => "BSV",
        TipoCodigo.Acupuntura => "Acupuntura",
        TipoCodigo.Consulta => "Consulta",
        _ => t.ToString()
    };
}

/// <summary>
/// Uma modalidade como CARTÃO ESCOLHÍVEL, com o que ela vai gerar escrito nela.
///
/// Antes a modalidade era um <c>ComboBox</c>. É a escolha que decide QUANTAS guias nascem
/// e QUANDO cada uma libera — a decisão mais consequente da tela — e estava escondida
/// atrás de um clique, sem dizer nada sobre o efeito. Agora as opções ficam à vista e cada
/// uma carrega a resposta: "2 guias · a 2ª libera 09/08".
///
/// O texto não é escrito à mão: vem do MOTOR DE REGRAS rodando de verdade sobre este
/// paciente e este convênio (<see cref="AtendimentoService.PreverModalidadesAsync"/>).
/// Frase decorada na tela envelheceria no dia em que a regra de um convênio mudasse, e
/// mentiria com toda a cara de verdade.
/// </summary>
public sealed partial class CartaoModalidade : ObservableObject
{
    public required EntradaModalidade Entrada { get; init; }
    public required string Nome { get; init; }

    /// <summary>É a modalidade habitual DESTE paciente (vem do cadastro dele).</summary>
    public required bool EhHabitual { get; init; }

    [ObservableProperty] private bool _escolhida;

    /// <summary>
    /// "2 guias" / "1 guia" — o que a regra do convênio gera.
    ///
    /// Nasce em "…" e não vazio: cartão sem número não se distingue de cartão que diz que
    /// não vai gerar nada, e a leitura do banco é remota — há um instante real entre
    /// escolher o paciente e saber a resposta.
    /// </summary>
    [ObservableProperty] private string _quantasGuias = "…";

    /// <summary>"a 2ª libera 09/08" ou "tudo hoje". A frase que faz o 2º código existir antes de nascer.</summary>
    [ObservableProperty] private string _quando = "calculando…";

    /// <summary>Esta modalidade gera 2º código — o que se esquece, e o motivo de o produto existir.</summary>
    [ObservableProperty] private bool _temSegundoCodigo;
}

/// <summary>
/// Uma guia da PRÉVIA: o que vai nascer, antes de nascer.
/// </summary>
public sealed record LinhaPrevia(
    string Tipo, string Ordem, string FaturarEm, string ComoObter,
    string Especialidade, bool EhSegundo, string Nota);

/// <summary>Um alerta de elegibilidade na tela, com a urgência para a cor do traço.</summary>
public sealed record LinhaAlertaElegibilidade(string Descricao, bool Vermelho);

/// <summary>Um avulso lançado hoje, na conferência do fim do dia.</summary>
public sealed class LinhaAvulso
{
    public required string Paciente { get; init; }
    public required string Modalidade { get; init; }
    public required string Convenio { get; init; }
    public required string Numero { get; init; }
    public required string Guias { get; init; }
    public required string Pendencia { get; init; }
    public required bool TemPendencia { get; init; }

    /// <summary>
    /// Quem LANÇOU, e a que horas (parcela 58).
    ///
    /// A conferência do dia é onde a pergunta da direção nasce — "quem lançou isso?" —, e
    /// até aqui a resposta só existia na trilha de auditoria, noutra tela e noutro app.
    /// </summary>
    public required string Lancamento { get; init; }
}

/// <summary>
/// Lança um atendimento AVULSO — o paciente que não estava na agenda. O motor de regras
/// gera os códigos do convênio na hora, inclusive o 2º código de +24h.
///
/// Veio do app de FATURAMENTO na parcela 46. O balcão já criava atendimento pelo caminho da
/// AGENDA (Fila → Finalizar → <c>FechamentoSessaoService</c> →
/// <c>AgendaService.ConfirmarPresencaAsync</c> → <c>AtendimentoService.LancarAsync</c>); o
/// que faltava aqui era o avulso, e ele morava no posto do faturamento — longe de quem
/// recebe o paciente que chegou sem horário marcado.
///
/// <b>O circuito com o faturamento é o MESMO</b>: os dois caminhos desembocam em
/// <see cref="AtendimentoService.LancarAsync"/>, que é ponto único, e é ele que grava
/// <c>Atendimento</c> + <c>CodigoFaturamento</c> pelas regras do convênio. Não existe
/// atendimento que nasça sem guia, e não existe guia que o faturamento não veja — a ligação
/// é chave estrangeira no mesmo banco, não sincronização.
///
/// O que NÃO veio junto foi a BAIXA. Ela é o ato do faturamento, tem as quatro portas de lá
/// (tela de baixa, baixa em lote, rodada de pendências e fila do Gerente) e o perfil que usa
/// esta tela não tem o bit — um botão que nasce apagado para quem usa a tela é o defeito da
/// parcela 41. A lista de códigos gerados fica como CONFIRMAÇÃO: é onde o balcão vê, na
/// hora, que a guia nasceu e quando ela libera.
/// </summary>
public partial class NovoAtendimentoViewModel : ObservableObject, ICarregarAoAbrir
{
    /// <summary>Metade VISÍVEL da permissão: lançar atendimento CRIA as guias pela regra do convênio.</summary>
    public bool PodeLancar => SessaoUsuario.Atual.Pode(Permissao.LancarAtendimento);

    private readonly IServiceScopeFactory _scopeFactory;

    /// <summary>Busca de paciente compartilhada (mesmo limite e mesmo comportamento das outras telas).</summary>
    public SeletorPacienteViewModel Seletor { get; }

    /// <summary>Atalho para o paciente escolhido no seletor.</summary>
    public Paciente? PacienteSelecionado => Seletor.Selecionado;

    public ObservableCollection<CodigoLancado> CodigosGerados { get; } = new();
    public ObservableCollection<string> Avisos { get; } = new();

    /// <summary>
    /// A conferência do fim do dia: os avulsos que JÁ foram lançados hoje. Ela vive na
    /// mesma tela do lançamento de propósito — quem lança é quem confere, e até aqui não
    /// havia onde conferir: era preciso abrir o app de faturamento, que é de outra pessoa.
    /// </summary>
    public ObservableCollection<LinhaAvulso> LancadosHoje { get; } = new();

    /// <summary>Carregando o registro do dia — separado do <see cref="Ocupado"/> do lançamento.</summary>
    [ObservableProperty] private bool _carregandoDia;

    /// <summary>
    /// A leitura do registro FALHOU — o terceiro estado. Lista vazia por erro é idêntica a
    /// lista vazia por não ter havido avulso nenhum, e as duas levam a conclusões opostas.
    /// </summary>
    [ObservableProperty] private bool _naoVerificado;

    /// <summary>
    /// Erro do REGISTRO, separado do <c>Mensagem</c> do formulário: um banco lento na
    /// leitura da conferência não pode apagar da barra o "Selecione a modalidade".
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TemAvisoRegistro))]
    private string? _avisoRegistro;

    public bool TemAvisoRegistro => !string.IsNullOrWhiteSpace(AvisoRegistro);

    public bool TemLancadosHoje => LancadosHoje.Count > 0;

    /// <summary>Placar das baixas do atendimento recém-lançado ("1 de 2 guias baixadas…").</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TemResumoBaixas))]
    private string? _resumoBaixas;

    public bool TemResumoBaixas => !string.IsNullOrWhiteSpace(ResumoBaixas);

    /// <summary>Modalidades ativas do catálogo (embutidas + variantes criadas pela clínica).</summary>
    public ObservableCollection<EntradaModalidade> Modalidades { get; } = new();

    /// <summary>As mesmas modalidades, como CARTÕES à vista — com o que cada uma gera.</summary>
    public ObservableCollection<CartaoModalidade> Cartoes { get; } = new();

    /// <summary>
    /// As guias que VÃO nascer, antes de nascerem. É a tela deixando de ser "preencha e
    /// torça": a consequência aparece no momento da decisão, que é o único em que ainda
    /// dá para escolher diferente.
    /// </summary>
    public ObservableCollection<LinhaPrevia> Previa { get; } = new();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TemPrevia))]
    private string _resumoPrevia = string.Empty;

    public bool TemPrevia => Previa.Count > 0 && !Lancado;

    /// <summary>
    /// O rótulo do botão DIZ o que vai acontecer: "Lançar e gerar 2 guias". Botão que
    /// promete o número é o que faz alguém perceber, antes de clicar, que escolheu a
    /// modalidade errada.
    /// </summary>
    [ObservableProperty] private string _rotuloLancar = "Lançar e gerar as guias";

    /// <summary>Especialidades ativas do catálogo (para a consulta avulsa).</summary>
    public ObservableCollection<EntradaEspecialidade> Especialidades { get; } = new();

    /// <summary>
    /// Quem vai atender. A lista dos profissionais ATIVOS, como no formulário de
    /// agendamento — é a mesma pergunta.
    ///
    /// ⚠️ Ela não existia, e a falta não quebrava nada: o avulso criava o encaixe SEM
    /// profissional, a guia nascia certa e a tela dizia "Atendimento registrado". O
    /// estrago era em volta, e todo em silêncio — horário sem dono some do "Meu dia" do
    /// médico (o quadro filtra por <c>ProfissionalId</c>), some da "Minha semana" dele, e
    /// fica de fora do REPASSE, que lê quem atendeu do AGENDAMENTO porque
    /// <c>Atendimento</c> não guarda profissional. O médico atendia alguém que o app dele
    /// nunca mostrou, e não era pago por aquela sessão.
    /// </summary>
    public ObservableCollection<Profissional> Profissionais { get; } = new();

    /// <summary>Opções de qual código sai primeiro (hoje) numa modalidade dupla. Vazio nas simples.</summary>
    public ObservableCollection<TipoCodigo> OpcoesPrimeiroCodigo { get; } = new();

    // ==================== QUANDO (parcela 70) ====================
    //
    // A decisão da direção foi unificar: "para agendar vamos colocar através de novo
    // atendimento (...) assim poderíamos unificar tudo em um lugar só". Esta tela deixou
    // de ser só o avulso de quem está no balcão — ela pergunta QUANDO: o paciente está
    // aqui (lançar agora, o caso de sempre) ou é para marcar dia e horário. A agenda
    // vira a tela de VER e mexer no que existe; criar mora aqui.

    /// <summary>O modo "marcar dia e horário" — o horário entra na agenda sem check-in.</summary>
    [ObservableProperty] private bool _marcarParaDepois;

    /// <summary>
    /// O par do de cima, para o RadioButton: a suíte não tem conversor de booleano
    /// invertido, e é o mesmo padrão de <see cref="SemPaciente"/>.
    /// </summary>
    public bool LancarAgora
    {
        get => !MarcarParaDepois;
        set { if (value) MarcarParaDepois = false; }
    }

    /// <summary>Metade VISÍVEL: marcar horário é mexer na agenda — o rádio explica; <c>Exigir</c> impede.</summary>
    public bool PodeMarcar => SessaoUsuario.Atual.Pode(Permissao.EditarAgenda);

    public bool NaoPodeMarcar => !PodeMarcar;

    /// <summary>Salas ativas — só o modo marcar pergunta onde (o avulso está NO balcão).</summary>
    public ObservableCollection<Sala> Salas { get; } = new();

    [ObservableProperty] private Sala? _sala;

    /// <summary>Duração em minutos; vazio = a padrão do profissional (como no formulário da agenda).</summary>
    [ObservableProperty] private string _duracao = string.Empty;

    /// <summary>Assumir o choque e marcar por cima — só aparece quando HÁ choque.</summary>
    [ObservableProperty] private bool _comoEncaixe;

    /// <summary>Marcar várias sessões de uma vez (o pacote de dez).</summary>
    [ObservableProperty] private bool _emSerie;

    [ObservableProperty] private string _quantidadeSessoes = "10";

    /// <summary>Intervalo em dias. 7 = mesma hora, toda semana — o caso comum.</summary>
    [ObservableProperty] private string _intervaloDias = "7";

    /// <summary>Choques e agenda fechada no horário escolhido, criticados A CADA TECLA.</summary>
    public ObservableCollection<string> ConflitosMarcacao { get; } = new();

    public bool TemConflitoMarcacao => ConflitosMarcacao.Count > 0;

    /// <summary>
    /// Marcar sem profissional é permitido e AVISADO, nunca sorteado: apontar alguém que
    /// o sistema escolheu faria o repasse pagar quem não atendeu. O aviso diz o custo —
    /// o horário não aparece no "Meu dia" de ninguém e fica fora do repasse.
    /// </summary>
    public bool SemProfissionalEscolhido
        => MarcarParaDepois && Profissional is null && Profissionais.Count > 0;

    /// <summary>A chave "guia no agendamento" (parcela 70) — decide o rótulo e a nota do modo marcar.</summary>
    [ObservableProperty] private bool _guiaNaMarcacao;

    /// <summary>O que acontece com as guias no modo marcar, escrito ANTES do clique.</summary>
    public string NotaGuiaNaMarcacao => GuiaNaMarcacao
        ? "As guias nascem JÁ NA MARCAÇÃO e vão para o faturamento — dá para efetivar no "
          + "portal antes da sessão. Cancelamento e falta suspendem as guias sozinhos."
        : "As guias deste horário nascem na confirmação da presença, no dia da sessão.";

    /// <summary>Título do resultado: "lançado" e "marcado para" são desfechos diferentes.</summary>
    [ObservableProperty] private string? _tituloResultado;

    /// <summary>
    /// Cabeçalho do passo da modalidade (o 3, desde que o QUANDO virou o passo 2 no
    /// redesenho) — "FOI feito" mentiria sobre uma sessão marcada para semana que vem.
    /// </summary>
    public string TituloPasso2 => MarcarParaDepois ? "3 · O QUE SERÁ FEITO" : "3 · O QUE FOI FEITO";

    [ObservableProperty] private DateTime _data = DateTime.Today;

    /// <summary>
    /// A HORA da sessão (parcela 60). Nasce com a hora de agora, que é o caso normal: o
    /// paciente está no balcão.
    ///
    /// Ela passou a existir porque o avulso deixou de criar um horário sintético às 9h
    /// fixo — <c>Atendimento</c> só guarda <c>DateOnly</c>, e era daí que saía o 9h. Agora
    /// ele marca um ENCAIXE de verdade, e encaixe sem hora não é encaixe.
    /// </summary>
    [ObservableProperty] private string _hora = DateTime.Now.ToString("HH:mm");
    [ObservableProperty] private EntradaModalidade? _modalidadeSelecionada;
    [ObservableProperty] private EntradaEspecialidade? _especialidadeSelecionada;

    /// <summary>
    /// Quem atendeu. Fica NULO quando a clínica não cadastrou ninguém — e aí o
    /// comportamento é o de antes desta correção, que é o melhor disponível: guia
    /// gerada, sessão registrada, e a tela dizendo que ninguém foi apontado.
    /// </summary>
    [ObservableProperty] private Profissional? _profissional;
    [ObservableProperty] private TipoCodigo? _primeiroCodigo;
    [ObservableProperty] private string? _observacoes;
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TemPrevia))]
    private bool _lancado;
    [ObservableProperty] private string? _numeroAtendimento;
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TemMensagem))]
    private string? _mensagem;

    /// <summary>
    /// A GRAVIDADE da mensagem, separada do texto. A barra de ação pintava tudo de
    /// vermelho de erro — inclusive a confirmação "Atendimento registrado — 2 guia(s) no
    /// faturamento", que é o desfecho BOM: a cor dizia "falhou" enquanto o texto dizia
    /// "registrado", e quem lê a cor primeiro lança o atendimento de novo. Três estados
    /// porque o meio existe de verdade: a guia nasceu e o pacote/caixa não — isso não é
    /// erro (só o atendimento derruba a operação, parcela 6) nem é sucesso limpo.
    /// </summary>
    [ObservableProperty] private bool _mensagemEhErro;

    /// <summary>O desfecho parcial: o que a clínica não pode perder ACONTECEU, e sobrou pendência.</summary>
    [ObservableProperty] private bool _mensagemEhAviso;

    public bool TemMensagem => !string.IsNullOrWhiteSpace(Mensagem);

    /// <summary>Escreve a mensagem e a gravidade juntas — separá-las é como a cor ficou mentindo.</summary>
    private void Avisar(string texto, bool erro = false, bool aviso = false)
    {
        Mensagem = texto;
        MensagemEhErro = erro;
        MensagemEhAviso = aviso;
    }

    private void LimparMensagem()
    {
        Mensagem = null;
        MensagemEhErro = false;
        MensagemEhAviso = false;
    }

    /// <summary>
    /// A CAPA do que o paciente já tem lançado no dia escolhido (parcela 70): número do
    /// atendimento, modalidade, quem lançou e se as guias já foram baixadas. É o aviso
    /// que a cliente pediu com todas as letras — e ele aparece na ESCOLHA do paciente,
    /// antes do clique, não só na pergunta de confirmação.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TemAvisoJaLancado))]
    private string? _avisoJaLancado;

    public bool TemAvisoJaLancado => !string.IsNullOrWhiteSpace(AvisoJaLancado);

    /// <summary>
    /// O RESTO da elegibilidade — dívida vencida, guia glosada, pacote no fim, termo do
    /// procedimento, consentimento (parcela 70, achado da auditoria). O formulário de
    /// agendamento que esta tela substituiu mostrava tudo isso pelo
    /// <c>ElegibilidadeService</c> (parcelas 26/48), e a porta unificada não pode mostrar
    /// MENOS do que a porta que aposentou. Carteirinha vencida, cota e consulta ficam de
    /// fora do recorte: a tela já as diz com mais detalhe logo acima, e o mesmo aviso
    /// duas vezes se lê como dois problemas.
    /// </summary>
    public ObservableCollection<LinhaAlertaElegibilidade> AlertasElegibilidade { get; } = new();

    public bool TemAlertasElegibilidade => AlertasElegibilidade.Count > 0;

    /// <summary>Aviso de guias pendentes do paciente selecionado (para a secretária cobrar na hora). Nulo = sem pendências.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TemAvisoPendencias))]
    private string? _avisoPendencias;

    /// <summary>Há aviso de pendências a exibir?</summary>
    public bool TemAvisoPendencias => !string.IsNullOrWhiteSpace(AvisoPendencias);

    /// <summary>Aviso de carteirinha vencida do paciente selecionado. Separado da mensagem de erro.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TemAvisoCarteirinha))]
    private string? _avisoCarteirinha;

    public bool TemAvisoCarteirinha => !string.IsNullOrWhiteSpace(AvisoCarteirinha);

    /// <summary>
    /// Consulta renovável vencida ou a vencer na data do atendimento. Fica ao lado dos
    /// outros avisos em vez de junto deles: carteirinha, cota e consulta chegam juntas e
    /// se resolvem em lugares diferentes.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TemAvisoConsulta))]
    private string? _avisoConsulta;

    public bool TemAvisoConsulta => !string.IsNullOrWhiteSpace(AvisoConsulta);

    /// <summary>A consulta já venceu — o convênio recusa o que for faturado sem consulta vigente.</summary>
    [ObservableProperty] private bool _consultaVencida;

    /// <summary>
    /// Já há paciente escolhido? Alterna a busca pelo resumo do paciente na tela.
    ///
    /// O par com <see cref="SemPaciente"/> substitui o conversor `BoolInvertidoParaVisibilidade`
    /// do faturamento, que a suíte não tem — é o mesmo par que a tela de Prescrições da
    /// Recepção já usa.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SemPaciente))]
    private bool _pacienteEscolhido;

    /// <summary>Ninguém escolhido ainda — mostra a busca.</summary>
    public bool SemPaciente => !PacienteEscolhido;

    /// <summary>Nome do convênio do paciente selecionado (resolvido pelo catálogo).</summary>
    [ObservableProperty] private string? _convenioPaciente;

    /// <summary>Categoria do paciente (semáforo do cadastro), como TEXTO — o conversor de cor é do faturamento.</summary>
    [ObservableProperty] private string? _categoriaPaciente;

    /// <summary>Cota de sessões: "Senha 12345 · 7 de 10 usadas — restam 3". Nulo = sem autorização vigente.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TemSaldoAutorizacao))]
    private string? _saldoAutorizacao;

    public bool TemSaldoAutorizacao => !string.IsNullOrWhiteSpace(SaldoAutorizacao);

    /// <summary>Cota esgotada ou autorização vencida: lançar agora é candidato à glosa 2006.</summary>
    [ObservableProperty] private bool _autorizacaoCritica;

    /// <summary>Resta uma sessão: hora de pedir a renovação da senha.</summary>
    [ObservableProperty] private bool _autorizacaoNaUltima;

    [ObservableProperty] private bool _ocupado;

    private int _ultimoAtendimentoId;

    /// <summary>Comportamento (base) da modalidade selecionada — o que o motor de regras usa.</summary>
    private ModalidadeAtendimento Modalidade =>
        ModalidadeSelecionada?.Base ?? ModalidadeAtendimento.AcupunturaComEletro;

    /// <summary>Modalidade dupla (gera 1º hoje + 2º em +24h): permite escolher qual código sai primeiro.</summary>
    public bool ModalidadeDupla =>
        Modalidade is ModalidadeAtendimento.AcupunturaComEletro or ModalidadeAtendimento.BsvComAcupuntura;

    /// <summary>Consulta avulsa: pede a especialidade (discriminada nos relatórios).</summary>
    public bool ModalidadeConsulta => Modalidade == ModalidadeAtendimento.Consulta;

    public NovoAtendimentoViewModel(IServiceScopeFactory scopeFactory)
    {
        _scopeFactory = scopeFactory;
        Seletor = new SeletorPacienteViewModel(scopeFactory);
        Seletor.SelecaoMudou += AoTrocarPaciente;
        AtualizarOpcoesPrimeiroCodigo();
    }

    partial void OnModalidadeSelecionadaChanged(EntradaModalidade? value)
    {
        AtualizarOpcoesPrimeiroCodigo();
        if (Modalidade != ModalidadeAtendimento.Consulta)
            EspecialidadeSelecionada = null;
        OnPropertyChanged(nameof(ModalidadeDupla));
        OnPropertyChanged(nameof(ModalidadeConsulta));

        foreach (var c in Cartoes) c.Escolhida = c.Entrada.Codigo == value?.Codigo;
        _ = PreverAsync();
    }

    // Trocar a data, a especialidade ou qual código sai primeiro muda o que a regra gera —
    // e a prévia mente se não acompanhar. É o preço de mostrar a consequência: ela tem de
    // seguir TODAS as entradas que a produzem. A data também muda o choque e a resposta
    // de "já foi lançado nesse dia?".
    partial void OnDataChanged(DateTime value)
    {
        _ = PreverAsync();
        _ = ConferirConflitosAsync();

        // No modo marcar, trocar o DIA é o gesto normal — e cota, consulta, elegibilidade
        // e a capa respondem PELA DATA. Sem reconferir, a tela mostraria a resposta do
        // dia anterior com toda a cara de atual (o item 6 da fila da parcela 69, fechado
        // aqui porque a unificação o tornou cotidiano).
        if (PacienteSelecionado is { } p)
        {
            _ = VerificarJaLancadoNoDiaAsync(p.Id);
            _ = VerificarAutorizacaoAsync(p.Id);
            _ = VerificarConsultaAsync(p.Id);
            _ = VerificarElegibilidadeAsync(p.Id);
        }
    }

    partial void OnEspecialidadeSelecionadaChanged(EntradaEspecialidade? value) => _ = PreverAsync();
    partial void OnPrimeiroCodigoChanged(TipoCodigo? value) => _ = PreverAsync();

    partial void OnMarcarParaDepoisChanged(bool value)
    {
        OnPropertyChanged(nameof(LancarAgora));
        OnPropertyChanged(nameof(TituloPasso2));
        OnPropertyChanged(nameof(SemProfissionalEscolhido));
        AtualizarRotuloLancar();
        _ = ConferirConflitosAsync();
    }

    partial void OnGuiaNaMarcacaoChanged(bool value)
    {
        OnPropertyChanged(nameof(NotaGuiaNaMarcacao));
        AtualizarRotuloLancar();
    }

    // A crítica de choque segue TODAS as entradas que o produzem (hora, duração,
    // profissional, sala) — a mesma regra da prévia, aplicada ao outro aviso da tela.
    partial void OnHoraChanged(string value) => _ = ConferirConflitosAsync();
    partial void OnDuracaoChanged(string value) => _ = ConferirConflitosAsync();
    partial void OnSalaChanged(Sala? value) => _ = ConferirConflitosAsync();

    partial void OnProfissionalChanged(Profissional? value)
    {
        OnPropertyChanged(nameof(SemProfissionalEscolhido));
        _ = ConferirConflitosAsync();
    }

    /// <summary>
    /// Descarte de resposta fora de ordem (parcela 50) — a crítica dispara a cada tecla
    /// da hora/duração, e num banco remoto a resposta velha apagaria o choque real.
    /// </summary>
    private int _geracaoConflitos;

    /// <summary>
    /// Mostra o choque e a agenda fechada ANTES de salvar, com a MESMA leitura que o
    /// serviço usa para recusar (<see cref="AgendaService.ConflitosAsync"/> — sem cópia).
    /// Silencioso quanto a falhas: é aviso, e uma consulta que não respondeu não pode
    /// impedir de marcar.
    /// </summary>
    private async Task ConferirConflitosAsync()
    {
        var geracao = ++_geracaoConflitos;

        ConflitosMarcacao.Clear();
        OnPropertyChanged(nameof(TemConflitoMarcacao));

        if (!MarcarParaDepois) { ComoEncaixe = false; return; }
        if (!TimeOnly.TryParse(Hora, out var hora)) return;

        try
        {
            using var scope = _scopeFactory.CreateScope();
            var agenda = scope.ServiceProvider.GetRequiredService<AgendaService>();

            var achados = await agenda.ConflitosAsync(
                Data.Date.Add(hora.ToTimeSpan()), DuracaoInformada(),
                Profissional?.Id, Sala?.Id, pacienteId: PacienteSelecionado?.Id);

            // Chegou tarde: outra tecla já pediu uma conferência mais nova.
            if (geracao != _geracaoConflitos) return;

            foreach (var c in achados.Select(DescreverConflito).Distinct())
                ConflitosMarcacao.Add(c);
        }
        catch (Exception ex)
        {
            if (geracao != _geracaoConflitos) return;
            LogSuite.Registrar("Novo atendimento — conflitos da agenda não puderam ser conferidos", ex);
        }
        finally
        {
            if (geracao == _geracaoConflitos)
            {
                OnPropertyChanged(nameof(TemConflitoMarcacao));
                // O choque sumiu (outra hora, outro dia): a caixinha "como encaixe" some
                // da tela, e não pode ficar ARMADA invisível — marcaria um encaixe que
                // ninguém pediu.
                if (ConflitosMarcacao.Count == 0) ComoEncaixe = false;
            }
        }
    }

    private static string DescreverConflito(ConflitoAgenda c) => c.Recurso switch
    {
        RecursoAgenda.Paciente => $"{c.Descricao} (aviso — não impede marcar)",
        _ => c.Descricao
    };

    private int? DuracaoInformada()
        => int.TryParse(Duracao, out var m) && m > 0 ? m : null;

    /// <summary>
    /// Preenche as opções de "qual código primeiro" conforme a modalidade e escolhe o
    /// padrão.
    ///
    /// ⚠️ <b>Só mexe em <see cref="PrimeiroCodigo"/> quando ele deixou de servir.</b>
    /// Antes ele era reescrito sempre, e como toda escrita dispara
    /// <c>OnPrimeiroCodigoChanged</c>, trocar de modalidade largava TRÊS prévias
    /// concorrentes no ar (a do <c>Clear()</c> que zera a seleção da combo, a do valor
    /// novo e a do próprio <c>OnModalidadeSelecionadaChanged</c>). Com o banco remoto,
    /// qualquer uma delas podia responder por último — e a resposta velha sobrescrevia a
    /// nova. Era metade do defeito que o cliente viu como "não atualiza".
    /// </summary>
    private void AtualizarOpcoesPrimeiroCodigo()
    {
        var escolhido = PrimeiroCodigo;

        OpcoesPrimeiroCodigo.Clear();
        switch (Modalidade)
        {
            case ModalidadeAtendimento.AcupunturaComEletro:
                OpcoesPrimeiroCodigo.Add(TipoCodigo.Acupuntura);
                OpcoesPrimeiroCodigo.Add(TipoCodigo.Eletroacupuntura);
                break;
            case ModalidadeAtendimento.BsvComAcupuntura:
                OpcoesPrimeiroCodigo.Add(TipoCodigo.Bsv);
                OpcoesPrimeiroCodigo.Add(TipoCodigo.Acupuntura);
                break;
        }

        // O `Clear()` acima zera o SelectedItem da combo e devolve null para cá pelo
        // binding; reescrever o mesmo valor não dispara nada, e escrever um valor que
        // continua válido só trocaria a escolha do usuário sem ele pedir.
        var novo = OpcoesPrimeiroCodigo.Count == 0 ? (TipoCodigo?)null
            : escolhido is { } atual && OpcoesPrimeiroCodigo.Contains(atual) ? atual
            : OpcoesPrimeiroCodigo[0];

        if (PrimeiroCodigo != novo) PrimeiroCodigo = novo;
        else OnPropertyChanged(nameof(PrimeiroCodigo));
    }

    public async Task CarregarAsync()
    {
        CarregarCatalogos();
        await CarregarEquipeAsync();
        await LerChaveGuiaNaMarcacaoAsync();
        ConsumirPreenchimento();
        await Seletor.BuscarAsync(imediato: true);
        await CarregarDoDiaAsync();
    }

    /// <summary>
    /// Os profissionais ativos (quem atendeu) e as salas (onde, no modo marcar).
    ///
    /// Falhar aqui NÃO derruba a tela: o balcão continua lançando o atendimento (a guia
    /// é o que a clínica não pode perder) e o seletor fica vazio, dizendo por quê. O que
    /// não pode é passar calado — sem a linha no log, a clínica acreditaria que não há
    /// ninguém cadastrado.
    /// </summary>
    private async Task CarregarEquipeAsync()
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var equipe = scope.ServiceProvider.GetRequiredService<EquipeService>();
            var ativos = await equipe.ProfissionaisAtivosAsync();
            var salas = await equipe.SalasAtivasAsync();

            // Monta e só ENTÃO publica: entre o Clear e o último Add não pode haver await.
            Profissionais.Clear();
            foreach (var p in ativos) Profissionais.Add(p);

            Salas.Clear();
            foreach (var s in salas) Salas.Add(s);

            // Um profissional só: não há o que escolher, e obrigar o clique num combo de
            // uma opção é cerimônia. Mais de um, a escolha é de quem está no balcão.
            if (Profissionais.Count == 1) Profissional = Profissionais[0];

            OnPropertyChanged(nameof(SemProfissionaisCadastrados));
            OnPropertyChanged(nameof(TemProfissionaisCadastrados));
            OnPropertyChanged(nameof(SemProfissionalEscolhido));
        }
        catch (Exception ex)
        {
            LogSuite.Registrar("Novo atendimento — profissionais não puderam ser lidos", ex);
        }
    }

    /// <summary>
    /// Lê a chave "guia no agendamento" para a tela dizer a verdade sobre o modo marcar.
    /// Falha presume o regime antigo: a nota promete MENOS do que acontece, nunca mais.
    /// </summary>
    private async Task LerChaveGuiaNaMarcacaoAsync()
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var parametros = scope.ServiceProvider.GetRequiredService<ParametrosService>();
            GuiaNaMarcacao = await parametros.GuiaNoAgendamentoAsync();
        }
        catch (Exception ex)
        {
            LogSuite.Registrar("Novo atendimento — chave da guia na marcação não pôde ser lida", ex);
            GuiaNaMarcacao = false;
        }
        AtualizarRotuloLancar();
    }

    /// <summary>
    /// Consome o pedido que a AGENDA deixou ao navegar para cá (o clique no vão livre, o
    /// "Novo horário") — dia, hora, profissional e sala já preenchidos, que é o gesto da
    /// parcela 58 sobrevivendo à unificação. Roda DEPOIS da carga da equipe, porque
    /// profissional e sala são resolvidos por Id na lista carregada.
    /// </summary>
    private void ConsumirPreenchimento()
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var ponte = scope.ServiceProvider.GetService<PreenchimentoNovoAtendimento>();
            if (ponte?.Consumir() is not { } pedido) return;

            if (pedido.DataHora is { } quando)
            {
                Data = quando.Date;
                if (quando.TimeOfDay > TimeSpan.Zero) Hora = quando.ToString("HH:mm");
            }
            if (pedido.ProfissionalId is { } profissionalId)
                Profissional = Profissionais.FirstOrDefault(p => p.Id == profissionalId) ?? Profissional;
            if (pedido.SalaId is { } salaId)
                Sala = Salas.FirstOrDefault(s => s.Id == salaId) ?? Sala;

            // Por último: já dispara a crítica de choque com tudo no lugar.
            if (pedido.MarcarParaDepois) MarcarParaDepois = true;
        }
        catch (Exception ex)
        {
            LogSuite.Registrar("Novo atendimento — pré-preenchimento da agenda não pôde ser lido", ex);
        }
    }

    /// <summary>Não há ninguém ativo cadastrado — a tela DIZ, em vez de mostrar combo vazio.</summary>
    public bool SemProfissionaisCadastrados => Profissionais.Count == 0;

    /// <summary>O par de cima. São duas propriedades porque a suíte não tem conversor de
    /// bool invertido, e criar um para um uso só seria mais peça para manter.</summary>
    public bool TemProfissionaisCadastrados => Profissionais.Count > 0;

    /// <summary>
    /// Os atendimentos do dia — a conferência de quem lançou o quê.
    ///
    /// ⚠️ Desde a parcela 60 ela mostra os atendimentos do dia INTEIRO, e não só os
    /// "avulsos". Antes o recorte era possível porque o avulso criava um agendamento com
    /// um par próprio (<c>Manual</c> + hora 9h fixo) que o distinguia de quem veio pela
    /// Fila. Esse par era o fantasma: um horário em que ninguém foi atendido, sem
    /// profissional, contando na ocupação do dia.
    ///
    /// Com as duas portas na mesma esteira o par deixou de existir — e deixou de fazer
    /// falta: a pergunta que a recepcionista faz olhando esta lista é "o que saiu hoje e
    /// quem lançou", não "por qual botão isto entrou".
    /// </summary>
    [RelayCommand]
    public async Task CarregarDoDiaAsync()
    {
        var geracao = ++_geracaoDia;

        try
        {
            CarregandoDia = true;
            NaoVerificado = false;
            AvisoRegistro = null;

            using var scope = _scopeFactory.CreateScope();
            var repo = scope.ServiceProvider.GetRequiredService<IClinicaRepositorio>();

            var hoje = DateTime.Today;
            var agendamentos = await repo.AgendamentosNoPeriodoAsync(hoje, hoje.AddDays(1).AddTicks(-1));
            if (geracao != _geracaoDia) return;

            // Monta em lista local e só ENTÃO publica: entre o Clear e o último Add não
            // pode haver await. Esta carga roda depois de CADA lançamento, então duas no
            // ar ao mesmo tempo é o caso normal de quem atende dois seguidos — e
            // intercaladas elas repetiam linhas na conferência do dia.
            var linhas = new List<LinhaAvulso>();
            foreach (var ag in agendamentos
                         .Where(a => a.AtendimentoId is not null)
                         .OrderByDescending(a => a.AtendimentoId))
            {
                var atendimento = await repo.ObterAtendimentoAsync(ag.AtendimentoId!.Value);
                if (geracao != _geracaoDia) return;
                if (atendimento is null) continue;

                linhas.Add(MontarLinhaAvulso(atendimento, hoje));
            }

            LancadosHoje.Clear();
            foreach (var linha in linhas) LancadosHoje.Add(linha);
        }
        catch (Exception ex)
        {
            if (geracao != _geracaoDia) return;
            NaoVerificado = true;
            LogSuite.Registrar("Novo atendimento — avulsos do dia não puderam ser lidos", ex);
            AvisoRegistro = $"Não foi possível ler os atendimentos de hoje: {ex.Message}";
        }
        finally
        {
            // A carga superada não apaga o "Carregando" da que ainda está no ar.
            if (geracao == _geracaoDia)
            {
                CarregandoDia = false;
                OnPropertyChanged(nameof(TemLancadosHoje));
            }
        }
    }

    /// <summary>
    /// Descarte de resposta fora de ordem para a lista de LANÇADOS HOJE (parcela 50). É
    /// um contador SEPARADO do da prévia: as duas leituras são independentes, e um
    /// contador só faria o lançamento de um atendimento cancelar a prévia que a
    /// recepcionista está montando para o próximo paciente.
    /// </summary>
    private int _geracaoDia;

    private static LinhaAvulso MontarLinhaAvulso(Atendimento atendimento, DateTime hoje)
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
            Lancamento = DescreverLancamento(atendimento),
            TemPendencia = depois.Count > 0,
            Pendencia = depois.Count == 0
                ? "todas liberadas"
                : $"{depois.Count} libera(m) a partir de {depois[0].DataPrevistaFaturamento:dd/MM}"
        };
    }

    /// <summary>
    /// "Lançado por Ana às 14:32" — a autoria na conferência do dia (parcela 58).
    ///
    /// A hora sozinha basta aqui: a lista é de HOJE, e escrever a data por extenso em
    /// vinte linhas do mesmo dia gastaria a largura da coluna repetindo o que o título da
    /// seção já diz.
    /// </summary>
    private static string DescreverLancamento(Atendimento atendimento)
    {
        if (string.IsNullOrWhiteSpace(atendimento.LancadoPor))
            return "sem registro de quem lançou";

        return atendimento.LancadoEm is { } quando
            ? $"por {atendimento.LancadoPor} às {quando:HH:mm}"
            : $"por {atendimento.LancadoPor}";
    }

    /// <summary>Recarrega as opções de modalidade/especialidade do cache (reflete o que foi salvo em Configurações).</summary>
    private void CarregarCatalogos()
    {
        var modalidadeAtual = ModalidadeSelecionada?.Codigo;
        Modalidades.Clear();
        foreach (var m in CatalogoModalidades.Ativas)
            Modalidades.Add(m);
        ModalidadeSelecionada = Modalidades.FirstOrDefault(m => m.Codigo == modalidadeAtual)
            ?? Modalidades.FirstOrDefault(m => m.Base == ModalidadeAtendimento.AcupunturaComEletro)
            ?? Modalidades.FirstOrDefault();

        MontarCartoes();

        var especialidadeAtual = EspecialidadeSelecionada?.Codigo;
        Especialidades.Clear();
        foreach (var e in CatalogoEspecialidades.Ativas)
            Especialidades.Add(e);
        EspecialidadeSelecionada = Especialidades.FirstOrDefault(e => e.Codigo == especialidadeAtual);
    }

    // Pré-preenche a modalidade com a habitual do paciente (definida no cadastro)
    // e avisa carteirinha vencida ANTES de gerar uma guia que o convênio vai recusar.
    private void AoTrocarPaciente(Paciente? value)
    {
        OnPropertyChanged(nameof(PacienteSelecionado));
        AvisoJaLancado = null;
        AvisoPendencias = null;
        AvisoCarteirinha = null;
        AvisoConsulta = null;
        AlertasElegibilidade.Clear();
        OnPropertyChanged(nameof(TemAlertasElegibilidade));
        ConsultaVencida = false;
        SaldoAutorizacao = null;
        AutorizacaoCritica = false;
        AutorizacaoNaUltima = false;
        PacienteEscolhido = value is not null;
        ConvenioPaciente = value is null
            ? null
            : CatalogoConvenios.Nome(value.ConvenioCodigo ?? value.Convenio.ToString());
        CategoriaPaciente = value?.Categoria.ToString();
        if (value is null) return;

        // Pré-seleciona a modalidade habitual do paciente: primeiro pelo código salvo, senão pela base.
        ModalidadeSelecionada = Modalidades.FirstOrDefault(m => m.Codigo == value.ModalidadePreferidaCodigo)
            ?? Modalidades.FirstOrDefault(m => m.Base == value.ModalidadePreferida)
            ?? ModalidadeSelecionada;
        AvisoCarteirinha = value.CarteirinhaVencida
            ? $"A carteirinha de {value.Nome} venceu em {value.ValidadeCarteirinha:dd/MM/yyyy} — o convênio pode recusar a guia."
            : null;

        // Os cartões e a prévia dependem do CONVÊNIO do paciente: a mesma modalidade gera
        // 2 guias na Unimed Intercâmbio e 1 na Amil.
        //
        // Por isso a prévia guardada é JOGADA FORA ao trocar de paciente: reaproveitá-la
        // mostraria, por um instante, o número do paciente anterior no cartão do novo — e
        // é exatamente sobre esse número que a recepcionista decide. Cartão dizendo
        // "calculando…" é honesto; cartão dizendo "2 guias" para quem vai gerar 1 não é.
        _ultimaPrevia = null;
        MontarCartoes();
        _ = PreverAsync();

        _ = VerificarPendenciasAsync(value.Id);
        _ = VerificarAutorizacaoAsync(value.Id);
        _ = VerificarConsultaAsync(value.Id);
        _ = VerificarJaLancadoNoDiaAsync(value.Id);
        _ = VerificarElegibilidadeAsync(value.Id);
        _ = ConferirConflitosAsync();
    }

    /// <summary>Descarte de resposta fora de ordem dos alertas — a data muda por clique.</summary>
    private int _geracaoElegibilidade;

    private static readonly HashSet<ImpedimentoElegibilidade> JaDitosPelaTela =
    [
        ImpedimentoElegibilidade.CarteirinhaVencida,
        ImpedimentoElegibilidade.CotaEsgotada,
        ImpedimentoElegibilidade.CotaQuaseNoFim,
        ImpedimentoElegibilidade.SemAutorizacaoVigente,
        ImpedimentoElegibilidade.ConsultaVencida,
        ImpedimentoElegibilidade.ConsultaARenovar
    ];

    /// <summary>
    /// Os alertas de elegibilidade que a tela NÃO diz por conta própria. A urgência viaja
    /// com cada alerta (parcela 36): dívida amarela ao lado de carteirinha vermelha, sem
    /// uma pintar a outra. Falha não impede — mas vira linha, senão a tela diria "nada a
    /// apontar" sobre o que não conseguiu conferir.
    /// </summary>
    private async Task VerificarElegibilidadeAsync(int pacienteId)
    {
        var geracao = ++_geracaoElegibilidade;
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var servico = scope.ServiceProvider.GetRequiredService<ElegibilidadeService>();
            var resultado = await servico.ConferirAsync(pacienteId, DateOnly.FromDateTime(Data.Date));

            if (geracao != _geracaoElegibilidade || PacienteSelecionado?.Id != pacienteId) return;

            AlertasElegibilidade.Clear();
            foreach (var a in resultado.Alertas.Where(a => !JaDitosPelaTela.Contains(a.Motivo)))
                AlertasElegibilidade.Add(new LinhaAlertaElegibilidade(
                    a.Descricao, a.Urgencia == NivelUrgencia.Vermelho));
        }
        catch (Exception ex)
        {
            if (geracao != _geracaoElegibilidade) return;
            LogSuite.Registrar("Novo atendimento — elegibilidade não pôde ser conferida", ex);
            AlertasElegibilidade.Clear();
            AlertasElegibilidade.Add(new LinhaAlertaElegibilidade(
                "Não foi possível conferir dívida, pacote e termo agora.", Vermelho: false));
        }
        finally
        {
            if (geracao == _geracaoElegibilidade)
                OnPropertyChanged(nameof(TemAlertasElegibilidade));
        }
    }

    /// <summary>Descarte de resposta fora de ordem da capa — a data muda por clique de DatePicker.</summary>
    private int _geracaoJaLancado;

    /// <summary>
    /// A capa do que o paciente já tem no dia escolhido — o aviso ANTES da pergunta.
    /// Aviso é auxiliar: falha nunca impede, mas vai para o log (a pergunta do Lançar
    /// confere de novo, então a tela não fica dependendo só desta leitura).
    /// </summary>
    private async Task VerificarJaLancadoNoDiaAsync(int pacienteId)
    {
        var geracao = ++_geracaoJaLancado;
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var atendimentos = scope.ServiceProvider.GetRequiredService<AtendimentoService>();
            var capas = await atendimentos.CapasDoDiaAsync(pacienteId, DateOnly.FromDateTime(Data.Date));

            if (geracao != _geracaoJaLancado || PacienteSelecionado?.Id != pacienteId) return;

            AvisoJaLancado = capas.Count == 0
                ? null
                : $"Já lançado em {Data:dd/MM}: "
                  + string.Join(" · ", capas.Select(c =>
                      $"nº {c.Numero} ({c.Modalidade}, {c.Lancamento} — {c.ResumoGuias})"))
                  + ". Lançar de novo cria OUTRO atendimento e outro jogo de guias.";
        }
        catch (Exception ex)
        {
            if (geracao != _geracaoJaLancado) return;
            LogSuite.Registrar("Novo atendimento — atendimentos do dia do paciente não puderam ser lidos", ex);
            AvisoJaLancado = null;
        }
    }

    /// <summary>
    /// Consulta renovável do paciente na data do atendimento.
    ///
    /// Ela existia em dois lugares — a aba Consultas e o painel de pendências — e em
    /// nenhum deles a secretária está com o paciente na frente. Lançar o atendimento é o
    /// último momento barato: a consulta vencida faz o convênio recusar o que acabou de
    /// ser gerado aqui.
    /// </summary>
    /// <summary>
    /// Descarte de resposta fora de ordem (parcela 60): a conferência também dispara ao
    /// trocar a DATA, e duas leituras do MESMO paciente com datas diferentes passariam
    /// na guarda de seleção — quem começou primeiro perde.
    /// </summary>
    private int _geracaoConsulta;

    private async Task VerificarConsultaAsync(int pacienteId)
    {
        var geracao = ++_geracaoConsulta;
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var consultas = scope.ServiceProvider.GetRequiredService<ConsultaService>();
            var situacao = await consultas.DoPacienteAsync(pacienteId, DateOnly.FromDateTime(Data));

            // A seleção (ou a data) pode ter mudado enquanto a consulta rodava.
            if (geracao != _geracaoConsulta || PacienteSelecionado?.Id != pacienteId) return;

            AvisoConsulta = situacao?.AvisoRenovacao;
            ConsultaVencida = situacao?.Vencida ?? false;
        }
        catch (Exception ex)
        {
            if (geracao != _geracaoConsulta) return;

            // Aviso é auxiliar: nunca impede o lançamento. Mas também não pode sumir
            // calado, senão a tela diria "não há consulta a renovar" sem ter olhado.
            LogSuite.Registrar("Novo atendimento — consulta renovável não pôde ser lida", ex);
            AvisoConsulta = "Não foi possível conferir a consulta renovável deste paciente.";
            ConsultaVencida = false;
        }
    }

    /// <summary>
    /// Mostra a cota de sessões antes de lançar. É o aviso que evita a glosa 2006
    /// ("quantidade executada acima da autorizada") — o sistema registrava essa glosa
    /// depois do prejuízo e não avisava antes.
    /// </summary>
    /// <summary>O par do contador da consulta, pela mesma razão: a data também dispara esta.</summary>
    private int _geracaoAutorizacao;

    private async Task VerificarAutorizacaoAsync(int pacienteId)
    {
        var geracao = ++_geracaoAutorizacao;
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var autorizacoes = scope.ServiceProvider.GetRequiredService<AutorizacaoService>();
            var saldo = await autorizacoes.VigenteAsync(pacienteId, DateOnly.FromDateTime(Data));

            // A seleção (ou a data) pode ter mudado enquanto a consulta rodava.
            if (geracao != _geracaoAutorizacao || PacienteSelecionado?.Id != pacienteId) return;

            if (saldo is null)
            {
                SaldoAutorizacao = null;
                AutorizacaoCritica = false;
                AutorizacaoNaUltima = false;
                return;
            }

            var senha = string.IsNullOrWhiteSpace(saldo.Autorizacao.Numero)
                ? "Autorização"
                : $"Senha {saldo.Autorizacao.Numero}";
            SaldoAutorizacao = saldo.Vencida
                ? $"{senha}: venceu em {saldo.Autorizacao.DataValidade:dd/MM/yyyy} — peça uma nova antes de lançar."
                : $"{senha}: {saldo.Resumo} (válida até {saldo.Autorizacao.DataValidade:dd/MM/yyyy}).";
            AutorizacaoCritica = saldo.Vencida || saldo.Esgotada;
            AutorizacaoNaUltima = !AutorizacaoCritica && saldo.NaUltima;
        }
        catch (Exception ex)
        {
            if (geracao != _geracaoAutorizacao) return;

            // Aviso é auxiliar: nunca pode impedir o lançamento do atendimento.
            LogSuite.Registrar("Novo atendimento — cota de sessões não pôde ser lida", ex);
            SaldoAutorizacao = null;
            AutorizacaoCritica = false;
            AutorizacaoNaUltima = false;
        }
    }

    /// <summary>
    /// Monta os cartões de modalidade. O texto de consequência entra depois, quando a
    /// prévia voltar — o cartão nasce sem ele em vez de nascer com um palpite.
    /// </summary>
    /// <summary>
    /// Remonta a fileira de cartões. Reaproveita a ÚLTIMA prévia calculada, se houver.
    ///
    /// Sem isso a fileira nascia com os números em branco e só se preenchia quando a
    /// próxima leitura do banco voltasse — e como trocar de paciente chama esta remontagem
    /// e a prévia ao mesmo tempo, quem chegasse por último decidia se a tela tinha número
    /// ou não. É a outra metade do "não atualiza" que o cliente viu.
    ///
    /// O reaproveitamento vale para a remontagem do MESMO paciente (recarregar o catálogo
    /// de modalidades). Ao TROCAR de paciente a prévia guardada é jogada fora antes —
    /// mostrar o número do paciente anterior no cartão do novo seria pior do que mostrar
    /// "calculando…", porque é sobre esse número que a recepcionista decide.
    /// </summary>
    private void MontarCartoes()
    {
        var habitual = PacienteSelecionado?.ModalidadePreferidaCodigo;

        Cartoes.Clear();
        foreach (var m in Modalidades)
            Cartoes.Add(new CartaoModalidade
            {
                Entrada = m,
                Nome = m.Nome,
                EhHabitual = habitual is not null && m.Codigo == habitual,
                Escolhida = m.Codigo == ModalidadeSelecionada?.Codigo
            });

        if (_ultimaPrevia is { } previa) AplicarNosCartoes(previa);
    }

    /// <summary>
    /// Roda o motor de regras SEM gravar e publica o que ele geraria: a lista de guias com
    /// data, e a frase de cada cartão de modalidade.
    ///
    /// Falha aqui NUNCA impede o lançamento — a prévia é um confortо, o botão é o trabalho.
    /// Mas também não some calada: a tela apaga a prévia e diz que não deu para calcular,
    /// senão "nenhuma guia" (que é o que uma lista vazia parece) viraria a informação mais
    /// perigosa possível numa tela cujo assunto é justamente não esquecer guia.
    /// </summary>
    /// <summary>
    /// Número da prévia mais recente pedida. Só a resposta dele pode escrever na tela.
    ///
    /// <b>Por que um contador, e não a comparação de estado.</b> A guarda anterior
    /// conferia paciente e modalidade — e não pegava o caso que o cliente viu: DUAS
    /// prévias do MESMO paciente e da MESMA modalidade no ar, uma com o "qual código sai
    /// primeiro" antigo e outra com o novo. As duas passavam na conferência, e quem
    /// escrevia por último era quem o banco respondesse por último. Num banco remoto isso
    /// não tem ordem nenhuma.
    ///
    /// É a mesma solução que o <c>SeletorPacienteViewModel</c> já usa para descartar
    /// resposta de busca fora de ordem: quem começou primeiro perde.
    /// </summary>
    private int _geracaoPrevia;

    private async Task PreverAsync()
    {
        var geracao = ++_geracaoPrevia;

        if (PacienteSelecionado is not { } paciente || ModalidadeSelecionada is null)
        {
            LimparPrevia();
            return;
        }

        var pacienteId = paciente.Id;
        var data = DateOnly.FromDateTime(Data);
        var codigoModalidade = ModalidadeSelecionada.Codigo;
        var codigosDosCartoes = Modalidades.Select(m => m.Codigo).ToList();

        try
        {
            using var scope = _scopeFactory.CreateScope();
            var atendimentos = scope.ServiceProvider.GetRequiredService<AtendimentoService>();

            var previa = await atendimentos.PreverAsync(
                pacienteId, data, Modalidade,
                primeiroCodigo: ModalidadeDupla ? PrimeiroCodigo : null,
                modalidadeCodigo: codigoModalidade,
                especialidadeConsultaCodigo: ModalidadeConsulta ? EspecialidadeSelecionada?.Codigo : null);

            var porModalidade = await atendimentos.PreverModalidadesAsync(
                pacienteId, data, codigosDosCartoes);

            // Chegou tarde: alguém pediu outra prévia enquanto o banco respondia esta.
            if (geracao != _geracaoPrevia) return;

            PublicarPrevia(previa);
            AplicarNosCartoes(porModalidade);
        }
        catch (Exception ex)
        {
            if (geracao != _geracaoPrevia) return;

            LogSuite.Registrar("Novo atendimento — prévia das guias não pôde ser calculada", ex);
            Previa.Clear();
            ResumoPrevia = "Não foi possível calcular a prévia das guias — o lançamento continua liberado.";
            _totalGuiasPrevia = null;
            AtualizarRotuloLancar();
            OnPropertyChanged(nameof(TemPrevia));

            // Falha não pode deixar número velho na tela: cartão dizendo "1 guia" quando a
            // conta não foi feita é falha exibida como sucesso, e é sobre esse número que a
            // recepcionista decide.
            foreach (var cartao in Cartoes)
            {
                cartao.QuantasGuias = "—";
                cartao.Quando = "não foi possível calcular";
                cartao.TemSegundoCodigo = false;
            }
        }
    }

    private void LimparPrevia()
    {
        Previa.Clear();
        ResumoPrevia = string.Empty;
        _totalGuiasPrevia = null;
        AtualizarRotuloLancar();
        OnPropertyChanged(nameof(TemPrevia));
    }

    /// <summary>O total da última prévia (nulo = ainda não calculada), para o rótulo do botão.</summary>
    private int? _totalGuiasPrevia;

    /// <summary>
    /// O rótulo do botão num ponto único: ele depende do MODO (lançar/marcar), da chave e
    /// da prévia, e três escritores diferentes divergiriam na primeira mudança.
    /// </summary>
    private void AtualizarRotuloLancar()
    {
        if (MarcarParaDepois)
        {
            RotuloLancar = !GuiaNaMarcacao
                ? "Marcar horário"
                : _totalGuiasPrevia switch
                {
                    null => "Marcar e gerar as guias",
                    0 => "Marcar horário",
                    1 => "Marcar e gerar 1 guia",
                    var t => $"Marcar e gerar {t} guias"
                };
            return;
        }

        RotuloLancar = _totalGuiasPrevia switch
        {
            null => "Lançar e gerar as guias",
            0 => "Lançar atendimento",
            1 => "Lançar e gerar 1 guia",
            var t => $"Lançar e gerar {t} guias"
        };
    }

    /// <summary>
    /// Escreve nos cartões o que a regra do convênio vai gerar em cada modalidade.
    ///
    /// A última leitura fica GUARDADA (<see cref="_ultimaPrevia"/>) porque
    /// <see cref="MontarCartoes"/> recria os objetos do zero — trocar de paciente
    /// reconstruía a fileira e apagava os números que já estavam calculados, e os cartões
    /// ficavam em branco até alguém mexer em outra coisa. Guardando, a remontagem
    /// reaproveita na hora o que já se sabe.
    /// </summary>
    private void AplicarNosCartoes(IReadOnlyDictionary<string, PreviaLancamento> porModalidade)
    {
        _ultimaPrevia = porModalidade;

        foreach (var cartao in Cartoes)
        {
            if (!porModalidade.TryGetValue(cartao.Entrada.Codigo, out var p))
            {
                cartao.QuantasGuias = "—";
                cartao.Quando = string.Empty;
                cartao.TemSegundoCodigo = false;
                continue;
            }

            var total = p.GuiasHoje + p.GuiasDepois;
            cartao.QuantasGuias = total == 1 ? "1 guia" : $"{total} guias";
            cartao.TemSegundoCodigo = p.GuiasDepois > 0;
            cartao.Quando = p.LiberaEm is { } quando
                ? $"a 2ª libera {quando:dd/MM}"
                : "tudo hoje";
        }
    }

    /// <summary>A última prévia por modalidade, para a remontagem dos cartões não zerar a fileira.</summary>
    private IReadOnlyDictionary<string, PreviaLancamento>? _ultimaPrevia;

    /// <summary>Traduz a prévia do motor de regras para as linhas da tela.</summary>
    private void PublicarPrevia(PreviaLancamento previa)
    {
        Previa.Clear();

        foreach (var c in previa.Codigos
                     .Where(c => c.Status != StatusCodigo.NaoAplicavel)
                     .OrderBy(c => c.DataPrevistaFaturamento).ThenBy(c => c.Ordem))
        {
            var ehSegundo = c.Ordem == OrdemCodigo.Segundo;
            Previa.Add(new LinhaPrevia(
                Tipo: CodigoLancado.RotularTipo(c.Tipo),
                Ordem: ehSegundo ? "2º" : "1º",
                FaturarEm: c.DataPrevistaFaturamento.ToString("dd/MM/yyyy"),
                ComoObter: RotularForma(c.FormaObtencao),
                Especialidade: c.EspecialidadeCodigo is { } cod ? CatalogoEspecialidades.Nome(cod) : "—",
                EhSegundo: ehSegundo,
                // A nota do 2º código é a razão de o produto existir: de 139 faturas
                // possíveis, 103 se perdiam exatamente nesta linha.
                Nota: ehSegundo
                    ? "É esta que se esquece — ela entra no painel de pendências do faturamento."
                    : "Já nasce faturável na data do atendimento."));
        }

        var total = previa.GuiasHoje + previa.GuiasDepois;
        _totalGuiasPrevia = total;
        AtualizarRotuloLancar();

        ResumoPrevia = previa.LiberaEm is { } quando
            ? $"{total} guia(s) — a 2ª só libera em {quando:dd/MM/yyyy}, e é ela que costuma se perder."
            : $"{total} guia(s), todas faturáveis na data do atendimento.";

        OnPropertyChanged(nameof(TemPrevia));
    }

    private static string RotularForma(FormaObtencao f) => f switch
    {
        FormaObtencao.NaoAplica => "—",
        FormaObtencao.App => "Pelo app (QR Code)",
        FormaObtencao.Sistema => "Pelo sistema",
        FormaObtencao.Ligacao => "Ligar para o paciente",
        _ => f.ToString()
    };

    /// <summary>Escolhe a modalidade pelo CARTÃO (o combo virou lista à vista).</summary>
    [RelayCommand]
    private void EscolherModalidade(CartaoModalidade? cartao)
    {
        if (cartao is null) return;
        ModalidadeSelecionada = cartao.Entrada;
    }

    /// <summary>Volta para a busca, para trocar o paciente escolhido.</summary>
    [RelayCommand]
    private void TrocarPaciente() => Seletor.Limpar();

    /// <summary>Zera a tela para lançar outro atendimento, sem sair da seção.</summary>
    [RelayCommand]
    private void NovoLancamento()
    {
        Lancado = false;
        NumeroAtendimento = null;
        TituloResultado = null;
        _ultimoAtendimentoId = 0;
        CodigosGerados.Clear();
        Avisos.Clear();
        ResumoBaixas = null;
        Observacoes = null;
        Mensagem = null;
        // O MODO fica como está (quem marca dez horários seguidos não quer reescolher o
        // rádio a cada um); o que é por-horário — encaixe assumido e série — zera.
        ComoEncaixe = false;
        EmSerie = false;
        Data = DateTime.Today;
        Seletor.Limpar();
        Seletor.Termo = null;
        Previa.Clear();
        ResumoPrevia = string.Empty;
        RotuloLancar = "Lançar e gerar as guias";
        OnPropertyChanged(nameof(TemPrevia));
    }

    /// <summary>
    /// Avisa se o paciente selecionado tem guias pendentes de baixa de atendimentos anteriores —
    /// oportunidade de a secretária cobrar a guia em aberto no mesmo instante do novo atendimento.
    /// </summary>
    private async Task VerificarPendenciasAsync(int pacienteId)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var pendencias = scope.ServiceProvider.GetRequiredService<PendenciaService>();
            var hoje = DateOnly.FromDateTime(DateTime.Today);
            var lista = await pendencias.PendenciasDoPacienteAsync(pacienteId, hoje);
            var ncs = await pendencias.NaoConformidadesDoPacienteAsync(pacienteId, hoje);

            // A seleção pode ter mudado enquanto a consulta rodava.
            if (PacienteSelecionado?.Id != pacienteId) return;
            if (lista.Count == 0 && ncs.Count == 0) { AvisoPendencias = null; return; }

            var partes = new List<string>();
            if (lista.Count > 0)
            {
                var itens = string.Join("; ", lista.Take(3).Select(p =>
                {
                    var ordinal = p.Ordem == OrdemCodigo.Segundo ? "2ª" : "1ª";
                    return $"{ordinal} guia de {CodigoLancado.RotularTipo(p.Tipo).ToLowerInvariant()} de {p.DataPrevista:dd/MM}";
                }));
                if (lista.Count > 3) itens += $"; +{lista.Count - 3}";
                partes.Add($"{lista.Count} guia(s) pendente(s) de baixa — cobre a guia agora! ({itens}.)");
            }
            // Não conformidade: o paciente voltou, então ela será reaberta ao lançar o atendimento.
            if (ncs.Count > 0)
                partes.Add($"{ncs.Count} não conformidade(s) — serão reabertas ao lançar (o paciente voltou); cobre a(s) guia(s).");

            AvisoPendencias = "Este paciente tem " + string.Join(" ", partes);
        }
        catch (Exception ex)
        {
            // Aviso é auxiliar: uma falha aqui nunca pode impedir o lançamento do atendimento.
            LogSuite.Registrar("Novo atendimento — pendências do paciente não puderam ser lidas", ex);
            AvisoPendencias = null;
        }
    }

    [RelayCommand]
    private async Task Lancar()
    {
        SessaoUsuario.Atual.Exigir(Permissao.LancarAtendimento, "lançar atendimento");

        if (Seletor.Selecionado is not { } paciente)
        {
            Avisar("Selecione o paciente.", erro: true);
            return;
        }
        if (ModalidadeSelecionada is null)
        {
            Avisar("Selecione a modalidade.", erro: true);
            return;
        }
        if (ModalidadeConsulta && EspecialidadeSelecionada is null)
        {
            Avisar("Informe a especialidade da consulta.", erro: true);
            return;
        }

        if (!TimeOnly.TryParse(Hora, out var hora))
        {
            Avisar("Informe a hora da sessão no formato HH:mm.", erro: true);
            return;
        }

        // ===== O MODO MARCAR (parcela 70) =====
        //
        // "O paciente está aqui" segue o caminho de sempre (encaixe + check-in + guia).
        // "Marcar dia e horário" cria o horário na agenda — sem check-in, sem pacote e
        // sem caixa, que são atos do dia da sessão.
        if (MarcarParaDepois)
        {
            await MarcarAsync(paciente, ModalidadeSelecionada, hora);
            return;
        }

        // Guarda contra duplo clique: dois lançamentos gerariam códigos duplicados.
        if (Ocupado) return;

        // ===== PASSO 0: este paciente já foi lançado hoje? (parcela 65) =====
        //
        // A guia passou a nascer no clique, então o segundo clique não custa mais um
        // horário a limpar: custa um jogo de guias DUPLICADO indo para o faturamento. A
        // pergunta é feita antes de qualquer gravação.
        //
        // É pergunta e não recusa: um paciente pode ser atendido duas vezes no mesmo dia
        // (a sessão da manhã e a consulta da tarde), e recusar travaria o balcão num caso
        // legítimo que a recepcionista não teria como contornar.
        if (!await ConfirmarSeJaLancadoHojeAsync(paciente)) return;

        Ocupado = true;

        int agendamentoId;
        try
        {
            CodigosGerados.Clear();
            Avisos.Clear();
            Mensagem = null;

            // ===== O GESTO ATÔMICO (parcela 70) =====
            //
            // Antes, este clique disparava uma CORRENTE de três serviços e cinco
            // SaveChanges (Agendar → RegistrarChegada → ConfirmarPresenca → número →
            // carimbo), e cada vão era um meio-estado possível: o incidente de 12/08 (três
            // encaixes com chegada e sem atendimento) morava num deles, e a guia duplicada
            // no último.
            //
            // Agora é UMA chamada e UM SaveChanges no núcleo: o encaixe nasce na hora
            // real, com o check-in carimbado, o atendimento e as guias — ou existe tudo,
            // ou não existe nada e o erro aparece aqui. Não sobra meio-estado para o
            // segundo clique transformar em duplicata.
            using var scope = _scopeFactory.CreateScope();
            var agenda = scope.ServiceProvider.GetRequiredService<AgendaService>();

            var (agendamento, lancamento) = await agenda.LancarAvulsoAsync(
                paciente.Id,
                Data.Date.Add(hora.ToTimeSpan()),
                Modalidade,
                Observacoes,
                modalidadeCodigo: ModalidadeSelecionada.Codigo,
                especialidadeConsultaCodigo: ModalidadeConsulta ? EspecialidadeSelecionada?.Codigo : null,
                primeiroCodigo: ModalidadeDupla ? PrimeiroCodigo : null,
                // Quem atendeu. É o que faz o paciente aparecer no "Meu dia" do médico e
                // a sessão entrar no repasse dele — os dois leem o AGENDAMENTO.
                profissionalId: Profissional?.Id,
                operador: SessaoUsuario.Atual.Operador,
                // A sala vai junto no avulso também: a chamada da Fila anuncia
                // "para a sala X", e sem ela saía "sala —".
                salaId: Sala?.Id);

            agendamentoId = agendamento.Id;

            MontarCodigos(lancamento.Atendimento.Codigos);
            foreach (var a in lancamento.Avisos) Avisos.Add(a);

            _ultimoAtendimentoId = lancamento.Atendimento.Id;
            NumeroAtendimento = lancamento.Atendimento.Numero;
            TituloResultado = $"Atendimento nº {lancamento.Atendimento.Numero} lançado";
            Lancado = true;

            // A conferência do dia acompanha na hora: o que acabou de nascer aparece lá
            // embaixo sem ninguém precisar clicar em Atualizar.
            await CarregarDoDiaAsync();
        }
        catch (Exception ex)
        {
            LogSuite.Registrar("Novo atendimento — lançamento falhou", ex);
            Avisar($"Não foi possível lançar: {ex.Message} Nada foi gravado — corrija e "
                   + "tente de novo.", erro: true);
            Ocupado = false;
            return;
        }

        RegistroAtendimento registro;
        try
        {
            // A PROPOSTA do fechamento (pacote/caixa/insumo) reaproveita o atendimento
            // recém-gravado — presença confirmada nunca é reconfirmada.
            using var scope = _scopeFactory.CreateScope();
            var fechamento = scope.ServiceProvider.GetRequiredService<FechamentoSessaoService>();

            registro = await fechamento.RegistrarAtendimentoAsync(
                agendamentoId, SessaoUsuario.Atual.Operador);
        }
        catch (Exception ex)
        {
            LogSuite.Registrar("Novo atendimento — proposta de fechamento não pôde ser montada", ex);
            Avisar("Atendimento registrado e guia gerada. O passo de pacote/caixa não pôde "
                   + "abrir — dá para resolver pela Fila ou pelo Financeiro.", aviso: true);
            Ocupado = false;
            return;
        }
        finally
        {
            Ocupado = false;
        }

        // ===== PASSO 3: pacote, insumo e caixa — só quando há o que decidir =====
        //
        // A janela é a MESMA `FechamentoSessaoWindow` da Fila (duas telas para a mesma
        // decisão divergiriam na primeira correção), e agora ela abre sobre um atendimento
        // que JÁ EXISTE: fechá-la não desfaz nada.
        //
        // E ela só abre quando há algo a decidir — pacote a debitar, dinheiro a lançar ou
        // insumo a baixar. Para o paciente de convênio sem pacote não há nada que a
        // recepcionista possa responder ali, e pedir confirmação de uma tela vazia é o que
        // ensina a fechar janela sem ler.
        if (!registro.TemDecisao)
        {
            Avisar(registro.GuiasGeradas == 0
                ? "Atendimento registrado. Este convênio não gera guia (particular)."
                : $"Atendimento registrado — {registro.GuiasGeradas} guia(s) no faturamento.");
            return;
        }

        try
        {
            var vm = new FechamentoSessaoViewModel(_scopeFactory, agendamentoId);
            var janela = new Janelas.FechamentoSessaoWindow(vm)
            {
                Owner = JanelaDona.Atual()
            };

            if (janela.ShowDialog() != true || janela.Resultado is not { } resultado)
            {
                // Fechou sem decidir. A guia está feita — o que ficou para trás é o pacote
                // e o caixa, e a mensagem diz exatamente isso. Não é mais meio-lançamento:
                // é lançamento completo com um passo opcional pendente.
                Avisar("Atendimento registrado e guia gerada. O pacote/caixa NÃO foram "
                       + "registrados — dá para resolver pela Fila ou pelo Financeiro.",
                       aviso: true);
                return;
            }

            // Os avisos do fechamento são a parte que não pode ser escondida: a sessão
            // pode ter sido concluída e o pacote NÃO ter debitado.
            foreach (var a in resultado.Avisos) Avisos.Add(a);
            await CarregarDoDiaAsync();
        }
        catch (Exception ex)
        {
            LogSuite.Registrar("Novo atendimento — fechamento não pôde ser concluído", ex);
            Avisar($"Atendimento registrado e guia gerada, mas o pacote/caixa falhou: "
                   + $"{ex.Message}. Resolva pela Fila ou pelo Financeiro.", aviso: true);
        }
    }

    /// <summary>
    /// O modo MARCAR: cria o horário na agenda — e, com a chave "guia no agendamento"
    /// ligada, o atendimento e as guias no MESMO grafo (é o <see cref="AgendaService.AgendarAsync"/>
    /// de sempre; esta tela não tem um segundo jeito de criar horário). Não abre
    /// pacote/caixa: o paciente não está aqui, e esses passos moram no Finalizar da
    /// Fila, no dia da sessão.
    /// </summary>
    private async Task MarcarAsync(Paciente paciente, EntradaModalidade modalidade, TimeOnly hora)
    {
        // As DUAS barreiras: o rádio explica (`PodeMarcar`); aqui impede. Com a chave
        // ligada, marcar também CRIA o atendimento e as guias — o ato que
        // `LancarAtendimento` nomeia (parcela 69: quando o momento do fato muda, a
        // permissão vai junto). `Exigir` com bits somados é um E.
        //
        // ⚠️ A chave é RELIDA aqui, fresca — nunca decidida pelo `GuiaNaMarcacao` da
        // tela, que é lido uma vez na abertura e presume `false` quando a leitura
        // falha. Quem cria as guias (`AgendarAsync`) relê a chave no ato; a guarda que
        // decidisse pelo cache deixaria alguém só com `EditarAgenda` gerar guia na
        // janela entre a abertura da tela e o clique (é o padrão dos outros dois
        // formulários, que leem a chave dentro do próprio Salvar). E a falha de leitura
        // AQUI exige o duplo bit: guarda na dúvida pede MAIS, nunca menos — se o banco
        // está fora, o Salvar logo abaixo falharia de qualquer jeito.
        bool chaveLigada;
        try
        {
            using var escopoChave = _scopeFactory.CreateScope();
            chaveLigada = await escopoChave.ServiceProvider
                .GetRequiredService<ParametrosService>().GuiaNoAgendamentoAsync();
            GuiaNaMarcacao = chaveLigada;
            AtualizarRotuloLancar();
        }
        catch (Exception ex)
        {
            LogSuite.Registrar("Novo atendimento — chave da guia não pôde ser relida na guarda", ex);
            chaveLigada = true;
        }
        SessaoUsuario.Atual.Exigir(
            chaveLigada
                ? Permissao.EditarAgenda | Permissao.LancarAtendimento
                : Permissao.EditarAgenda,
            "marcar horário");

        if (!string.IsNullOrWhiteSpace(Duracao) && DuracaoInformada() is null)
        {
            Avisar("A duração precisa ser um número de minutos maior que zero.", erro: true);
            return;
        }

        var quantas = 0;
        var intervalo = 0;
        if (EmSerie)
        {
            if (!int.TryParse(QuantidadeSessoes, out quantas) || quantas < 2)
            {
                Avisar("Quantas sessões? A partir de 2 — para uma só, desmarque \"repetir\".", erro: true);
                return;
            }
            if (!int.TryParse(IntervaloDias, out intervalo) || intervalo < 1)
            {
                Avisar("De quantos em quantos dias? (7 = toda semana, mesma hora.)", erro: true);
                return;
            }
        }

        if (Ocupado) return;

        // A mesma pergunta informada do lançar: marcar em cima de um dia que já tem
        // atendimento é a duplicata de amanhã sendo combinada hoje.
        if (!await ConfirmarSeJaLancadoHojeAsync(paciente)) return;

        Ocupado = true;
        try
        {
            CodigosGerados.Clear();
            Avisos.Clear();
            LimparMensagem();

            var dataHora = Data.Date.Add(hora.ToTimeSpan());
            var especialidade = ModalidadeConsulta ? EspecialidadeSelecionada?.Codigo : null;

            using var scope = _scopeFactory.CreateScope();
            var agenda = scope.ServiceProvider.GetRequiredService<AgendaService>();

            if (EmSerie)
            {
                var serie = await agenda.AgendarSerieAsync(
                    paciente.Id, dataHora, Modalidade, quantas,
                    intervaloDias: intervalo, observacoes: Observacoes,
                    modalidadeCodigo: modalidade.Codigo,
                    especialidadeConsultaCodigo: especialidade,
                    profissionalId: Profissional?.Id, salaId: Sala?.Id,
                    duracaoMinutos: DuracaoInformada(),
                    operador: SessaoUsuario.Atual.Operador,
                    primeiroCodigo: ModalidadeDupla ? PrimeiroCodigo : null);

                // A série que pulou datas NÃO passa em silêncio: a recepção precisa ver
                // quais não entraram para resolver com o paciente ainda na linha.
                foreach (var r in serie.Recusados)
                    Avisos.Add($"{r.Quando:dd/MM HH:mm} — não deu: {r.Motivo}");

                var frase = $"{serie.Marcados.Count} sessão(ões) marcada(s) a partir de "
                            + $"{dataHora:dd/MM/yyyy 'às' HH:mm}."
                            + (GuiaNaMarcacao ? " As guias já estão no faturamento." : string.Empty);
                if (serie.TudoMarcado) Avisar(frase);
                else Avisar(frase + " Parte da série não entrou — veja as datas puladas e resolva agora.",
                    aviso: true);
            }
            else
            {
                var ag = await agenda.AgendarAsync(
                    paciente.Id, dataHora, Modalidade, Observacoes,
                    modalidadeCodigo: modalidade.Codigo,
                    especialidadeConsultaCodigo: especialidade,
                    profissionalId: Profissional?.Id, salaId: Sala?.Id,
                    duracaoMinutos: DuracaoInformada(), encaixe: ComoEncaixe,
                    operador: SessaoUsuario.Atual.Operador,
                    primeiroCodigo: ModalidadeDupla ? PrimeiroCodigo : null);

                if (ag.Atendimento is { } atendimento)
                {
                    // A chave está ligada: a guia nasceu junto do horário, e a tela
                    // mostra — é a metade que permite efetivar no portal desde já.
                    MontarCodigos(atendimento.Codigos);
                    _ultimoAtendimentoId = atendimento.Id;
                    NumeroAtendimento = atendimento.Numero;
                    TituloResultado = $"Atendimento nº {atendimento.Numero} marcado para "
                                      + $"{dataHora:dd/MM/yyyy 'às' HH:mm}";
                    Lancado = true;

                    var guias = atendimento.Codigos.Count(c => c.Status != StatusCodigo.NaoAplicavel);
                    Avisar(guias == 0
                        ? "Horário marcado. Este convênio não gera guia (particular)."
                        : $"Horário marcado — {guias} guia(s) já no faturamento; dá para "
                          + "efetivar no portal antes da sessão.");
                }
                else
                {
                    Avisar($"Horário marcado para {dataHora:dd/MM/yyyy 'às' HH:mm}. "
                           + "As guias nascem na confirmação da presença.");
                }

                await PerguntarComprovanteAsync(scope, ag.Id, paciente.Nome, dataHora);
            }

            // Uma marcação para HOJE aparece na conferência do dia (a de outro dia, não —
            // e a lista diz "hoje" no título de propósito).
            if (Data.Date == DateTime.Today) await CarregarDoDiaAsync();
        }
        catch (Exception ex)
        {
            LogSuite.Registrar("Novo atendimento — marcação falhou", ex);
            Avisar($"Não foi possível marcar: {ex.Message}", erro: true);
        }
        finally
        {
            Ocupado = false;
        }
    }

    /// <summary>
    /// "Imprimir o comprovante de agendamento para o paciente?" — a pergunta do fluxo
    /// unificado (decisão da direção, parcela 70). Só no horário ÚNICO: numa série, o
    /// papel de cada sessão sai pela janela do horário na Agenda.
    /// Falha vira aviso na lista, nunca por cima da confirmação da marcação.
    /// </summary>
    private async Task PerguntarComprovanteAsync(
        IServiceScope scope, int agendamentoId, string paciente, DateTime dataHora)
    {
        try
        {
            var dialogo = scope.ServiceProvider.GetRequiredService<IDialogoService>();
            if (!dialogo.Confirmar(
                    "Comprovante de agendamento",
                    $"Imprimir o comprovante de agendamento para {paciente} "
                    + $"({dataHora:dd/MM/yyyy 'às' HH:mm})?"))
                return;

            var pdfs = scope.ServiceProvider.GetRequiredService<AgendaPdfService>();
            var parametros = scope.ServiceProvider.GetRequiredService<ParametrosService>();
            var pdf = await pdfs.ComprovanteAsync(agendamentoId, await parametros.ObterPrestadorAsync());

            // Salvar cancelado devolve nulo, e sair calado é o certo; erro de verdade
            // entra nos avisos.
            var erro = await ImpressaoPdf.SalvarEAbrirAsync(
                pdf, ImpressaoPdf.NomeSeguro($"comprovante-{paciente}-{dataHora:yyyy-MM-dd}.pdf"));
            if (erro is not null) Avisos.Add(erro);
        }
        catch (Exception ex)
        {
            LogSuite.Registrar("Novo atendimento — comprovante não pôde ser gerado", ex);
            Avisos.Add($"O comprovante não pôde ser gerado: {ex.Message}. "
                       + "Dá para imprimir pela janela do horário, na Agenda.");
        }
    }

    /// <summary>
    /// Pergunta antes de lançar (ou marcar) de novo alguém que já tem atendimento no dia
    /// escolhido.
    ///
    /// Desde a parcela 65 a guia nasce no clique, então o clique repetido manda um jogo de
    /// guias duplicado ao faturamento — e guia duplicada só aparece na operadora, semanas
    /// depois. A pergunta é INFORMADA (parcela 70, pedido da cliente): mostra a capa de
    /// cada atendimento — número, modalidade, quem lançou e se a guia já foi baixada — em
    /// vez de um número seco. Falha da conferência não impede o lançamento (banco lento
    /// não pode travar o balcão), mas fica registrada.
    /// </summary>
    private async Task<bool> ConfirmarSeJaLancadoHojeAsync(Paciente paciente)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var atendimentos = scope.ServiceProvider.GetRequiredService<AtendimentoService>();
            var dia = DateOnly.FromDateTime(Data.Date);

            var capas = await atendimentos.CapasDoDiaAsync(paciente.Id, dia);
            if (capas.Count == 0) return true;

            var linhas = string.Join("\n", capas.Take(4).Select(c =>
                $"  • Nº {c.Numero} — {c.Modalidade}, {c.Lancamento} — {c.ResumoGuias}"));
            if (capas.Count > 4) linhas += $"\n  • … e mais {capas.Count - 4} atendimento(s)";

            var verbo = MarcarParaDepois ? "Marcar" : "Lançar";
            var dialogo = scope.ServiceProvider.GetRequiredService<IDialogoService>();
            return dialogo.ConfirmarPerigo(
                "Atendimento repetido?",
                $"{paciente.Nome} já tem atendimento em {dia:dd/MM/yyyy}:\n\n{linhas}\n\n"
                + $"{verbo} de novo cria OUTRO atendimento — e outro jogo de guias — para o mesmo dia.\n\n"
                + $"{verbo} mesmo assim?");
        }
        catch (Exception ex)
        {
            LogSuite.Registrar("Novo atendimento — conferência de lançamento repetido falhou", ex);
            return true;
        }
    }

    /// <summary>
    /// Monta as linhas do resultado marcando o que já dá para baixar hoje. O 1º código
    /// nasce faturável na hora; o 2º só a partir da data prevista (+24h).
    /// </summary>
    private void MontarCodigos(IEnumerable<CodigoFaturamento> codigos)
    {
        var hoje = DateOnly.FromDateTime(DateTime.Today);
        CodigosGerados.Clear();

        foreach (var c in codigos.OrderBy(c => c.DataPrevistaFaturamento).ThenBy(c => c.Ordem))
        {
            var liberada = c.EstaPendente(hoje);
            var impedimento = c.Baixado || liberada || c.Status == StatusCodigo.NaoAplicavel
                ? null
                : $"Libera em {c.DataPrevistaFaturamento:dd/MM/yyyy}";
            CodigosGerados.Add(new CodigoLancado(c, liberada, impedimento));
        }

        AtualizarResumoBaixas(hoje);
    }

    private void AtualizarResumoBaixas(DateOnly hoje)
    {
        var faturaveis = CodigosGerados.Where(l => l.Codigo.Status != StatusCodigo.NaoAplicavel).ToList();
        if (faturaveis.Count == 0) { ResumoBaixas = null; return; }

        var baixadas = faturaveis.Count(l => l.Baixado);
        var proxima = faturaveis
            .Where(l => !l.Baixado && l.Codigo.DataPrevistaFaturamento > hoje)
            .OrderBy(l => l.Codigo.DataPrevistaFaturamento)
            .FirstOrDefault();

        ResumoBaixas = $"{baixadas} de {faturaveis.Count} guia(s) já baixada(s) pelo faturamento"
            + (proxima is null
                ? baixadas == faturaveis.Count ? " — nada pendente deste atendimento." : "."
                : $" — a próxima libera em {proxima.Codigo.DataPrevistaFaturamento:dd/MM/yyyy} e entra no painel de pendências do faturamento.");
    }

    /// <summary>Gera a capa de faturamento (PDF) do atendimento recém-lançado e abre o arquivo.</summary>
    [RelayCommand]
    private async Task GerarCapa()
    {
        if (_ultimoAtendimentoId == 0) return;

        byte[] pdf;
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var capa = scope.ServiceProvider.GetRequiredService<CapaFaturamentoService>();
            var prestador = await scope.ServiceProvider.GetRequiredService<ParametrosService>().ObterPrestadorAsync();
            pdf = await capa.GerarPdfAsync(_ultimoAtendimentoId, prestador);
        }
        catch (Exception ex)
        {
            // Degradar é certo; degradar SEM RASTRO não é (a regra do log da casa). Era o
            // único catch desta tela que não registrava, e a capa é o papel que vai para o
            // convênio — sem a linha no log, "não foi possível" é o fim da investigação.
            Application.Diagnostico.Registrar(
                "Recepção — capa de faturamento não pôde ser gerada", ex);
            Avisar($"Não foi possível gerar a capa: {ex.Message}", erro: true);
            return;
        }

        // Salvar cancelado devolve null, e sair calado é o certo: diálogo que a pessoa
        // fechou de propósito não é falha a reportar.
        await ImpressaoPdf.SalvarEAbrirAsync(
            pdf, $"Capa-INICIAL-{NumeroAtendimento ?? _ultimoAtendimentoId.ToString()}-{Data:yyyy-MM-dd}.pdf");
    }
}
