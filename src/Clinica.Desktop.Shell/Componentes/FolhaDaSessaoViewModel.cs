using System.Collections.ObjectModel;
using System.ComponentModel;
using Clinica.Application;
using Clinica.Application.Modelos;
using Clinica.Application.Servicos;
using Clinica.Desktop.Controls;
using Clinica.Domain;
using Clinica.Domain.Entities;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;

namespace Clinica.Desktop.Shell.Componentes;

/// <summary>
/// A FOLHA DA SESSÃO — escrever uma sessão do prontuário, num lugar só (set/2026).
///
/// O pedido, e o que ele revelou
/// -----------------------------
/// A cliente abriu Prontuário → "Nova sessão" e disse: <i>"esse modal aí não está
/// condizente com o do atendimento médico/enfermagem. Não queremos esse tipo de modal/box
/// de atendimento"</i>. Ela estava certa, e o defeito era maior do que a aparência: havia
/// DOIS desenhos para o mesmo ato — a folha da tela de Atendimento (Consultório) e um
/// formulário de duas colunas na Recepção —, e o segundo tinha ficado para trás com
/// <b>quatro dos doze campos</b> da sessão: história da doença atual, exame físico,
/// hipótese, CID, plano, retorno, encaminhamento e os campos personalizados não existiam
/// lá. Ele os PRESERVAVA na gravação (a regra do lugar 6), o que evitou o estrago e
/// escondeu a divergência.
///
/// É a lição de sempre, agora no lugar mais caro: <b>duas definições do mesmo registro
/// divergem na primeira correção, e a que ninguém lembra de ajustar é a que fica
/// errada</b> — e aqui a que ficou para trás era a porta do balcão.
///
/// Por que HERANÇA e não composição
/// --------------------------------
/// A tela de Atendimento <b>é</b> uma folha da sessão, com contexto, alertas, sinais
/// vitais e o Finalizar em volta. Derivar dela preserva os bindings que já existiam
/// (<c>{Binding TextoEvolucao}</c> continua resolvendo), e é isso que mantém a extração
/// fora da classe de erro que este projeto mais teme: binding que deixa de casar não
/// quebra build nem teste — o campo simplesmente para de aparecer.
///
/// O que a porta decide
/// --------------------
/// A base não sabe DE QUEM é a sessão nem a que horário ela pertence: quem responde é a
/// porta, pelos ganchos <see cref="PacienteId"/>, <see cref="AgendamentoDaSessao"/>,
/// <see cref="AtendimentoDaSessao"/> e <see cref="ProfissionalDaSessao"/>. É o mesmo
/// contrato do <see cref="LinhaDoTempoClinicaViewModel"/>: o componente garante que a
/// gravação seja UMA só; o vínculo é de quem sabe dele.
/// </summary>
public partial class FolhaDaSessaoViewModel : ObservableObject
{
    protected readonly IServiceScopeFactory _escopos;

    /// <summary>
    /// Opcional: a janela modal não tem onde mostrar snackbar (o host mora na
    /// <c>ShellWindow</c>, atrás dela), e ali quem confirma é a mensagem do rodapé.
    /// </summary>
    protected readonly ISnackbarService? _snackbar;

    protected FolhaDaSessaoViewModel(IServiceScopeFactory escopos, ISnackbarService? snackbar)
    {
        _escopos = escopos;
        _snackbar = snackbar;
        Anteriores.CollectionChanged += (_, _) =>
        {
            if (SessaoParaReutilizar is not null && !Anteriores.Contains(SessaoParaReutilizar))
                SessaoParaReutilizar = null;
        };
    }

    public FolhaDaSessaoViewModel(IServiceScopeFactory escopos) : this(escopos, null) { }

    // ==================== O que a PORTA responde ====================

    /// <summary>De quem é a sessão. 0 = ninguém escolhido, e a gravação recusa.</summary>
    public virtual int PacienteId { get; protected set; }

    /// <summary>
    /// O horário a que a sessão pertence — é o vínculo que a tira da lista "Sessões sem
    /// evolução" do Consultório. Nulo é "esta porta não sabe", nunca "desligue": o
    /// serviço PRESERVA o vínculo que já existe (a regra da parcela 68).
    /// </summary>
    protected virtual int? AgendamentoDaSessao => null;

    /// <summary>O atendimento (a guia) a que a sessão pertence, quando a porta o conhece.</summary>
    protected virtual int? AtendimentoDaSessao => null;

    /// <summary>
    /// Quem assina a sessão. No Consultório é sempre quem fez LOGIN; no balcão a
    /// recepcionista escolhe o profissional que atendeu, e por isso o gancho existe.
    /// </summary>
    protected virtual int? ProfissionalDaSessao => SessaoUsuario.Atual.ProfissionalId;

    /// <summary>A modalidade do horário — é ela que decide quais campos personalizados aparecem.</summary>
    protected virtual Task<string?> ModalidadeDaSessaoAsync(IServiceScope scope)
        => Task.FromResult<string?>(null);

    /// <summary>Quem chamou, para o log dizer de qual porta veio.</summary>
    protected virtual string ContextoDoLog => "Prontuário";

    /// <summary>O que a porta faz depois de gravar — a tela recarrega, a janela relê o que é dela.</summary>
    protected virtual Task DepoisDeSalvarAsync() => Task.CompletedTask;

    /// <summary>A presença de paciente mudou — a porta reavalia o que ela acende.</summary>
    protected virtual void AoMudarPresencaDePaciente() { }

    /// <summary>
    /// Descarte de resposta fora de ordem (parcela 50). Mora na base porque a leitura dos
    /// campos personalizados é dela, e a carga da porta usa o MESMO contador: dois
    /// contadores deixariam a carga velha publicar campos por cima da nova.
    /// </summary>
    protected int _geracaoCarga;

    /// <summary>
    /// O mapa corporal da sessão — o mesmo componente do shell que a Recepção usa
    /// (parcela 36). É a ferramenta central da acupuntura, que é a especialidade da casa:
    /// um app para quem atende sem onde marcar o ponto seria um app para outra clínica.
    ///
    /// É RECRIADO a cada carga porque ele nasce amarrado a um paciente e a uma evolução —
    /// reaproveitar a instância entre pacientes traria os pontos de um para a sessão do
    /// outro, que é o pior defeito possível num prontuário.
    /// </summary>
    [ObservableProperty] private MapaCorporalViewModel? _mapa;

    public ObservableCollection<ResumoSessaoAnterior> Anteriores { get; } = [];
    [ObservableProperty] private ResumoSessaoAnterior? _sessaoParaReutilizar;

    /// <summary>Evolução em edição. 0 = sessão nova.</summary>
    [ObservableProperty] private int _evolucaoId;

    /// <summary>
    /// Os CAMPOS PERSONALIZADOS que a clínica cadastrou e valem para esta sessão
    /// (set/2026) — ver <c>CampoPersonalizadoProntuario</c>.
    ///
    /// Vazia na clínica que não cadastrou nenhum, que é o caso padrão: a região inteira
    /// SOME, em vez de deixar um rótulo em branco no meio da folha.
    /// </summary>
    public ObservableCollection<CampoDaSessao> CamposPersonalizados { get; } = [];

    public bool TemCamposPersonalizados => CamposPersonalizados.Count > 0;

    [ObservableProperty] private string _paciente = string.Empty;
    [ObservableProperty] private bool _semPaciente = true;

    [ObservableProperty] private DateTime _data = DateTime.Today;

    [ObservableProperty] private int? _evaAntes;
    [ObservableProperty] private int? _evaDepois;

    [ObservableProperty]
    private string? _queixaPrincipal;

    // ---- O registro do ATENDIMENTO (parcela 73) ----
    //
    // Até aqui a sessão eram quatro caixas de texto e o par de EVA: o prontuário registrava
    // o que foi FEITO e não dizia por quê. Os três campos são OPCIONAIS, e a seção nasce
    // RECOLHIDA — a sessão curta de manutenção continua sendo queixa + conduta, e obrigar
    // quem faz vinte por dia a preencher anamnese faria a clínica escrever "idem" em todas.

    // ==================== As quatro abas da sessão (parcela 77) ====================
    //
    // A ficha era UMA coluna de rolagem com nove campos: quem escrevia perdia o começo de
    // vista ao chegar no fim, e a seção da parcela 73 tinha virado um Expander RECOLHIDO —
    // isto é, metade do registro clínico escondida atrás de um clique que ninguém dá.
    //
    // A divisão é o S-O-A-P, que é como todo prontuário do mundo se organiza e — não por
    // acaso — é a mesma forma das cinco etapas da COFEN do lado da enfermagem: o que a
    // pessoa DIZ, o que se ACHA nela, o que isso É, e o que se vai FAZER.
    //
    // ⚠️ Sub-aba ESCONDE campo, e esconder campo de prontuário é como se escreve menos sem
    // perceber. É por isso que cada aba anuncia se TEM conteúdo: o ponto no rótulo é visível
    // de qualquer aba, e é ele que denuncia o "Subjetivo" vazio de quem foi direto ao Plano.
    // Sem esse indicador a reorganização seria uma troca de leiaute que piora o registro.

    [ObservableProperty]
    private string? _historiaDoencaAtual;

    [ObservableProperty]
    private string? _exameFisico;

    [ObservableProperty]
    private string? _hipoteseDiagnostica;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DescricaoCid))]
    private string? _cidSessao;

    /// <summary>
    /// A descrição do CID digitado — a metade que PEGA o erro.
    ///
    /// "M54.4" no lugar de "M54.5" é tão plausível quanto o certo, e nada mais o denuncia
    /// (parcela 63). Código fora do catálogo desta clínica não é recusado: o campo aceita o
    /// que for digitado, e a frase diz que ele não foi reconhecido em vez de calar.
    /// </summary>
    public string? DescricaoCid
    {
        get
        {
            if (string.IsNullOrWhiteSpace(CidSessao)) return null;

            return CatalogoCid.Descrever(CidSessao) is { } descricao
                ? descricao
                : "Código fora do catálogo desta clínica — confira antes de gravar.";
        }
    }

    /// <summary>Abre a busca do CID — atalho com conferência, nunca lista fechada.</summary>
    [RelayCommand]
    protected virtual void BuscarCid()
    {
        if (BuscaCidWindow.Perguntar(CidSessao) is { } escolhido)
            CidSessao = escolhido;
    }

    [ObservableProperty]
    private string? _conduta;

    [ObservableProperty]
    private string? _textoEvolucao;

    [ObservableProperty]
    private string? _orientacoes;

    /// <summary>
    /// O PLANO: o que vem pela frente. Ver <c>Evolucao.PlanoTerapeutico</c> para por que ele
    /// não é a conduta nem a orientação.
    /// </summary>
    [ObservableProperty]
    private string? _planoTerapeutico;

    /// <summary>
    /// QUANDO reavaliar. `DateTime?` porque é o que o `DatePicker` amarra; o domínio guarda
    /// `DateOnly`, e a conversão acontece na borda.
    ///
    /// ⚠️ Não vira agendamento nenhum — ver <c>Evolucao.RetornoSugeridoEm</c>.
    /// </summary>
    [ObservableProperty]
    private DateTime? _retornoSugeridoEm;

    [ObservableProperty]
    private string? _retornoSugeridoNota;

    [ObservableProperty]
    private string? _encaminhamento;

    // ==================== A FOLHA ÚNICA (mockup 01, aprovado) ====================
    //
    // A tela deixou de ser quatro abas com doze campos e passou a ser UMA folha: o cursor
    // nasce dentro dela, do tamanho da tela, e o que se escreve vai para `TextoEvolucao`.
    // A direção pediu isso com todas as letras — "menos campos, texto livre para escrever
    // o que quiser durante a sessão, é melhor segundo eles".
    //
    // ⚠️ NENHUMA COLUNA FOI APAGADA, e é isso que separa reduzir a TELA de perder o que já
    // foi escrito. Os doze campos continuam no banco (guarda de 20 anos, Lei 13.787/2018) e
    // continuam editáveis — atrás do "Detalhar…", numa janela. Sem essa porta, a sessão de
    // 27/08 que tem hipótese, conduta e evolução preenchidas sumiria de VISTA sem sumir do
    // banco, que é o pior dos dois mundos.
    //
    // ⚠️ Por que JANELA e não um bloco recolhido na própria tela: o bloco cresce com o dado
    // e disputa altura com a folha, e filho ancorado que não cabe é DECEPADO (a lição da
    // parcela 79, na tela ao lado). E é a mesma forma que a consulta da COFEN já usa do
    // outro lado da clínica desde a parcela 88 — o mesmo gesto, o mesmo desenho.

    /// <summary>
    /// Os campos do detalhe que ESTA sessão já tem escritos.
    ///
    /// É o que faz a linha do "Detalhar…" dizer que há conteúdo lá dentro. Sem ela, quem
    /// abre uma sessão antiga vê uma folha com o texto da evolução e não tem como saber que
    /// existem cinco campos preenchidos a um clique — o defeito recorrente do projeto
    /// (dado gravado sem leitor) cometido pela própria reforma que o corrige.
    /// </summary>
    public int CamposDetalhados =>
        (string.IsNullOrWhiteSpace(QueixaPrincipal) ? 0 : 1)
        + (string.IsNullOrWhiteSpace(HistoriaDoencaAtual) ? 0 : 1)
        + (string.IsNullOrWhiteSpace(ExameFisico) ? 0 : 1)
        + (string.IsNullOrWhiteSpace(HipoteseDiagnostica) ? 0 : 1)
        + (string.IsNullOrWhiteSpace(CidSessao) ? 0 : 1)
        + (string.IsNullOrWhiteSpace(Conduta) ? 0 : 1)
        + (string.IsNullOrWhiteSpace(Orientacoes) ? 0 : 1)
        + (string.IsNullOrWhiteSpace(PlanoTerapeutico) ? 0 : 1)
        + (string.IsNullOrWhiteSpace(RetornoSugeridoNota) ? 0 : 1)
        + (string.IsNullOrWhiteSpace(Encaminhamento) ? 0 : 1);
        // Os campos personalizados NÃO entram nesta conta de propósito: eles estão na
        // FOLHA, à vista, e o selo do "Detalhar…" conta o que está escondido atrás do
        // clique. Somá-los faria o número prometer conteúdo que já está na tela.

    /// <summary>Há o que ler lá dentro — acende o selo ao lado da linha.</summary>
    public bool TemCamposDetalhados => CamposDetalhados > 0;

    /// <summary>
    /// O selo da linha do detalhe. Diz QUANTOS, não "tem conteúdo": o número é o que faz
    /// alguém clicar.
    /// </summary>
    public string SeloDetalhe =>
        CamposDetalhados == 1 ? "1 campo preenchido" : $"{CamposDetalhados} campos preenchidos";

    /// <summary>
    /// A hora da última gravação desta sessão, escrita no rodapé ao lado do Salvar.
    ///
    /// ⚠️ Ela NÃO promete gravação automática. O mockup trazia "salva sozinha a cada pausa",
    /// e a folha não salva sozinha de propósito: cada gravação de uma evolução que já existe
    /// cria uma `VersaoEvolucao` (parcela 52), e salvar a cada pausa encheria o prontuário de
    /// dezenas de versões por sessão. Prometer na tela o que o código não faz é a garantia
    /// aparente que este projeto recusa desde a parcela 3 — então a frase diz o que houve.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DicaRodape))]
    private string _ultimaGravacao = string.Empty;

    /// <summary>
    /// A frase do rodapé, ao lado do Salvar: a hora da última gravação quando já houve uma,
    /// e a dica da EVA antes disso. Uma linha só — duas frases empilhadas num rodapé fazem
    /// a pessoa parar de ler as duas.
    /// </summary>
    public string DicaRodape =>
        string.IsNullOrEmpty(UltimaGravacao)
            ? "A EVA vale em par: registre o depois ao terminar a sessão."
            : UltimaGravacao;

    /// <summary>
    /// Abre os campos separados. Recebe ESTE ViewModel — não uma cópia —, então o que se
    /// digita lá já está aqui quando a janela fecha, e quem grava continua sendo o Salvar
    /// da tela de trás (o padrão do catálogo de enfermagem, parcela 88).
    /// </summary>
    [RelayCommand]
    protected virtual void AbrirDetalhe()
    {
        if (SemPaciente)
        {
            Mensagem = "Escolha um paciente antes de abrir os campos da sessão.";
            MensagemEhErro = true;
            return;
        }

        new DetalheSessaoWindow(this) { Owner = JanelaDona.Atual() }.ShowDialog();
    }

    /// <summary>
    /// Os campos que moram na janela do detalhe. Mudou um deles, o selo e a contagem da
    /// linha mudam junto.
    ///
    /// ⚠️ É um gancho único em vez de dois `NotifyPropertyChangedFor` em cada um dos dez
    /// campos: atributo repetido dez vezes é atributo que o décimo primeiro campo não
    /// ganha — e aí o selo passa a mentir sobre um campo preenchido, calado.
    /// </summary>
    private static readonly string[] CamposDaJanelaDeDetalhe =
    [
        nameof(QueixaPrincipal), nameof(HistoriaDoencaAtual), nameof(ExameFisico),
        nameof(HipoteseDiagnostica), nameof(CidSessao), nameof(Conduta),
        nameof(Orientacoes), nameof(PlanoTerapeutico), nameof(RetornoSugeridoNota),
        nameof(Encaminhamento)
    ];

    protected override void OnPropertyChanged(PropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);

        if (e.PropertyName is not { } nome) return;
        if (Array.IndexOf(CamposDaJanelaDeDetalhe, nome) < 0) return;

        OnPropertyChanged(nameof(CamposDetalhados));
        OnPropertyChanged(nameof(TemCamposDetalhados));
        OnPropertyChanged(nameof(SeloDetalhe));
    }

    /// <summary>
    /// O roteiro da sessão que se repete (parcela 63) — a MESMA janela da Recepção, no
    /// shell. Era aqui que ela fazia mais falta: quem escreve dez evoluções por dia é
    /// quem atende, e a sessão de acupuntura tem sempre a mesma forma.
    ///
    /// Aplicar COPIA e não grava: o Salvar desta tela continua sendo o que efetiva.
    /// </summary>
    [RelayCommand]
    protected virtual void AbrirModelos()
    {
        SessaoUsuario.Atual.Exigir(Permissao.EditarProntuario, "usar modelos de evolução");

        var vm = new ModelosEvolucaoViewModel(
            _escopos,
            SessaoUsuario.Atual.ProfissionalId,
            new ModeloAplicado(
                QueixaPrincipal, Conduta, TextoEvolucao, Orientacoes,
                HistoriaDoencaAtual, ExameFisico, HipoteseDiagnostica,
                CidSessao, PlanoTerapeutico))
        {
            // "Repetir a última sessão" é um roteiro como os outros — o mais usado da
            // acupuntura —, e desde set/2026 mora aqui em vez de ser o quarto botão do
            // rodapé (uma barra só, e só ação nela). Só existe quando HÁ sessão anterior:
            // botão que só levaria recusa é o defeito da parcela 41.
            RepetirUltima = Anteriores.Count > 0 ? RepetirUltima : null
        };

        var janela = new ModelosEvolucaoWindow(vm)
        {
            Owner = JanelaDona.Atual()
        };

        if (janela.ShowDialog() != true || janela.Escolhido is not { } m) return;

        // Preenche o que falta, nunca zera: um modelo com conduta e orientações não pode
        // apagar a queixa que o profissional acabou de digitar ouvindo o paciente.
        if (!string.IsNullOrWhiteSpace(m.QueixaPrincipal)) QueixaPrincipal = m.QueixaPrincipal;
        if (!string.IsNullOrWhiteSpace(m.Conduta)) Conduta = m.Conduta;
        if (!string.IsNullOrWhiteSpace(m.TextoEvolucao)) TextoEvolucao = m.TextoEvolucao;
        if (!string.IsNullOrWhiteSpace(m.Orientacoes)) Orientacoes = m.Orientacoes;

        // Esta tela edita os NOVE campos, então ela aplica os nove — é o que faz o roteiro
        // valer a pena depois das parcelas 73 e 75. Mesma regra do bloco acima: preenche o
        // que está vazio, nunca zera o que o profissional acabou de escrever.
        if (!string.IsNullOrWhiteSpace(m.HistoriaDoencaAtual)) HistoriaDoencaAtual = m.HistoriaDoencaAtual;
        if (!string.IsNullOrWhiteSpace(m.ExameFisico)) ExameFisico = m.ExameFisico;
        if (!string.IsNullOrWhiteSpace(m.HipoteseDiagnostica)) HipoteseDiagnostica = m.HipoteseDiagnostica;
        if (!string.IsNullOrWhiteSpace(m.CidSessao)) CidSessao = m.CidSessao;
        if (!string.IsNullOrWhiteSpace(m.PlanoTerapeutico)) PlanoTerapeutico = m.PlanoTerapeutico;
    }

    [ObservableProperty] private bool _carregando;
    [ObservableProperty] private bool _naoVerificado;
    [ObservableProperty] private string? _mensagem;
    [ObservableProperty] private bool _mensagemEhErro;

    /// <summary>Metade VISÍVEL da permissão; a que impede é o <c>Exigir</c> no comando.</summary>
    public bool PodeEditarProntuario => SessaoUsuario.Atual.Pode(Permissao.EditarProntuario);

    public bool TemPaciente => !SemPaciente;

    /// <summary>
    /// ⚠️ O <c>partial</c> é gerado NA BASE, e a derivada não pode implementá-lo — daí o
    /// gancho virtual: sem ele, a tela de Atendimento pararia de reavaliar "Emitir
    /// documento" e "Imprimir a sessão" ao trocar de paciente, com os botões acesos sobre
    /// uma tela sem ninguém (o defeito da parcela 41, pela porta de trás).
    /// </summary>
    partial void OnSemPacienteChanged(bool value)
    {
        OnPropertyChanged(nameof(TemPaciente));
        AoMudarPresencaDePaciente();
    }

    /// <summary>Valores da escala, para os dois seletores de dor.</summary>
    public IReadOnlyList<int> EscalaEva { get; } =
        Enumerable.Range(Evolucao.EvaMinima, Evolucao.EvaMaxima - Evolucao.EvaMinima + 1).ToList();

    /// <summary>As definições da última carga — é delas que a gravação copia rótulo e tipo.</summary>
    private IReadOnlyList<CampoPersonalizadoProntuario> _definicoesDosCampos = [];

    /// <summary>
    /// Monta a régua dos campos personalizados desta sessão.
    ///
    /// A MODALIDADE sai do horário quando há um: é ela que decide quais campos aparecem, e
    /// campo que aparece onde não serve é o campo que ninguém preenche. Sem horário (quem
    /// entrou pela busca) valem os campos de TODAS as modalidades — mostrar menos do que se
    /// sabe esconderia justamente o campo que a clínica cadastrou para aquela sessão.
    ///
    /// Falhar aqui NÃO impede escrever: a folha do sistema continua inteira, e o que se
    /// perde é o acréscimo. Mas vai para o log — sem a linha, a clínica acreditaria que
    /// ninguém cadastrou campo nenhum.
    /// </summary>
    protected async Task CarregarCamposPersonalizadosAsync(IServiceScope scope, int geracao)
    {
        try
        {
            var modalidade = await ModalidadeDaSessaoAsync(scope);

            var definicoes = await scope.ServiceProvider
                .GetRequiredService<CampoPersonalizadoService>().DaSessaoAsync(modalidade);

            if (geracao != _geracaoCarga) return;

            // Monta fora e publica de uma vez (a regra da parcela 62).
            var linhas = definicoes.Select(d => new CampoDaSessao
            {
                Id = d.Id,
                Rotulo = d.Rotulo,
                Tipo = d.Tipo,
                Ajuda = d.Ajuda,
                Opcoes = d.OpcoesDaLista
            }).ToList();

            _definicoesDosCampos = definicoes;
            CamposPersonalizados.Clear();
            foreach (var l in linhas) CamposPersonalizados.Add(l);
            OnPropertyChanged(nameof(TemCamposPersonalizados));
        }
        catch (Exception ex)
        {
            if (geracao != _geracaoCarga) return;
            Diagnostico.Registrar(
                $"{ContextoDoLog} — campos personalizados não puderam ser lidos", ex);
            _definicoesDosCampos = [];
            CamposPersonalizados.Clear();
            OnPropertyChanged(nameof(TemCamposPersonalizados));
        }
    }

    protected void Preencher(Evolucao e)
    {
        EvolucaoId = e.Id;
        Data = e.Data.ToDateTime(TimeOnly.MinValue);
        EvaAntes = e.EvaAntes;
        EvaDepois = e.EvaDepois;
        QueixaPrincipal = e.QueixaPrincipal;
        HistoriaDoencaAtual = e.HistoriaDoencaAtual;
        ExameFisico = e.ExameFisico;
        HipoteseDiagnostica = e.HipoteseDiagnostica;
        CidSessao = e.CidSessao;
        Conduta = e.Conduta;
        TextoEvolucao = e.TextoEvolucao;
        Orientacoes = e.Orientacoes;
        PlanoTerapeutico = e.PlanoTerapeutico;
        RetornoSugeridoEm = e.RetornoSugeridoEm?.ToDateTime(TimeOnly.MinValue);
        RetornoSugeridoNota = e.RetornoSugeridoNota;
        Encaminhamento = e.Encaminhamento;

        // O que já foi respondido nos campos personalizados. Casa pelo CampoId, e não pelo
        // rótulo: o rótulo é uma CÓPIA de quando o valor foi gravado, e ele pode ter sido
        // renomeado desde então — casar por texto perderia a resposta em silêncio.
        foreach (var campo in CamposPersonalizados)
            campo.Resposta = e.CamposPersonalizados
                .FirstOrDefault(v => v.CampoId == campo.Id) is { } valor
                ? CampoPersonalizadoService.Exibir(valor)
                : null;

        // ⚠️ Nada mais precisa ser "aberto": as quatro abas mostram sozinhas quais têm
        // conteúdo, pelo ponto no rótulo. O Expander recolhido escondia metade do registro
        // clínico de quem voltava para reler — e a pessoa concluía que ele se perdeu.
    }

    /// <summary>
    /// Zera o formulário para uma sessão que ainda não tem evolução escrita.
    ///
    /// ⚠️ A DATA é a do HORÁRIO, não a de hoje — e é aqui que morava o defeito que
    /// quebrava o laço central do Consultório. O módulo existe para responder "o que eu
    /// atendi e ainda não escrevi"; escrever a evolução da sessão de terça gravava-a com a
    /// data de HOJE, e daí saíam dois estragos:
    ///
    /// 1. **O prontuário passa a dizer que a sessão foi hoje.** É registro clínico com a
    ///    data errada — a Lei 13.787/2018 pede o contrário disso, e o erro é
    ///    irrecuperável depois, porque nada guarda qual era a data verdadeira.
    /// 2. **A dívida nunca saía da lista.** `RegistrosPendentesAsync` lê as evoluções da
    ///    janela que termina ONTEM (a sessão de hoje não é cobrada, o paciente ainda está
    ///    na sala) — então a evolução datada de hoje ficava FORA do conjunto consultado, o
    ///    casamento não a encontrava, e a linha continuava lá. O médico salvava, lia
    ///    "Sessão registrada no prontuário", via a pendência de pé e escrevia de novo.
    ///
    /// Nulo — o caminho de quem entrou pela busca, sem horário em foco — continua sendo
    /// hoje, que é o melhor palpite disponível.
    /// </summary>
    protected void Limpar(DateOnly? dataDoHorario = null)
    {
        EvolucaoId = 0;
        Data = dataDoHorario?.ToDateTime(TimeOnly.MinValue) ?? DateTime.Today;
        EvaAntes = null;
        EvaDepois = null;
        QueixaPrincipal = null;
        HistoriaDoencaAtual = null;
        ExameFisico = null;
        HipoteseDiagnostica = null;
        CidSessao = null;
        Conduta = null;
        TextoEvolucao = null;
        Orientacoes = null;
        PlanoTerapeutico = null;
        RetornoSugeridoEm = null;
        RetornoSugeridoNota = null;
        Encaminhamento = null;

        foreach (var campo in CamposPersonalizados) campo.Resposta = null;

        // A hora da última gravação é DESTA sessão: mantê-la ao trocar de paciente faria o
        // rodapé afirmar, sobre uma folha em branco, que ela foi gravada às 14h37.
        UltimaGravacao = string.Empty;
    }

    /// <summary>
    /// Traz o que a última sessão escreveu para o formulário — sem gravar nada.
    ///
    /// É a mesma regra de "repetir a sessão anterior" do mapa corporal: o botão TRAZ para
    /// a tela, e só o Salvar efetiva. Tratamento de acupuntura repete protocolo por
    /// semanas, e redigitar a mesma conduta é como o registro vira "idem".
    ///
    /// ⚠️ DOIS defeitos moravam aqui, e os dois só apareceram quando o rótulo saiu de dentro
    /// do texto (set/2026):
    ///
    /// 1. a comparação era com <c>"—"</c>, e o modelo devolve string VAZIA desde a parcela
    ///    77 — logo ela era sempre verdadeira, e o que ia para o campo Conduta era o texto
    ///    JÁ ROTULADO: <c>"Conduta: agulhamento lombar"</c>, gravado assim no prontuário e
    ///    impresso assim no relatório do convênio;
    /// 2. sem conduta escrita, o botão dizia "trazida para a tela" tendo trazido NADA — e
    ///    depois da folha única (set/2026) esse é o caso NORMAL, porque o profissional
    ///    escreve tudo na folha e não no campo Conduta.
    ///
    /// Por isso ele repete a CONDUTA quando ela existe e, quando não existe, o texto da
    /// FOLHA — que é onde a sessão passada de fato está. E nunca por cima do que já está
    /// escrito: repetir não pode apagar o que a pessoa acabou de digitar.
    /// </summary>
    [RelayCommand]
    protected virtual void RepetirUltima()
        => ReutilizarSessao(Anteriores.FirstOrDefault());

    [RelayCommand]
    private void ReutilizarSessao(ResumoSessaoAnterior? sessao)
    {
        // Só aceita uma sessão do contexto atual; uma seleção antiga não atravessa
        // a troca de paciente. O botão sem seleção usa a última sessão disponível.
        var ultima = sessao ?? SessaoParaReutilizar ?? Anteriores.FirstOrDefault();
        if (ultima is not null && !Anteriores.Contains(ultima)) ultima = null;
        if (ultima is null)
        {
            Mensagem = "Não há sessão anterior para repetir.";
            MensagemEhErro = true;
            return;
        }

        var trazidos = new List<string>();

        if (ultima.Valor(ResumoSessaoAnterior.RotuloConduta) is { } conduta
            && string.IsNullOrWhiteSpace(Conduta))
        {
            Conduta = conduta;
            trazidos.Add("a conduta");
        }

        if (ultima.Valor(ResumoSessaoAnterior.RotuloEvolucao) is { } folha
            && string.IsNullOrWhiteSpace(TextoEvolucao))
        {
            TextoEvolucao = folha;
            trazidos.Add("o texto da sessão");
        }

        if (ultima.Valor(ResumoSessaoAnterior.RotuloQueixa) is { } queixa
            && string.IsNullOrWhiteSpace(QueixaPrincipal))
        {
            QueixaPrincipal = queixa;
            trazidos.Add("a queixa");
        }

        // Falar em "trazido" sem ter trazido nada é a garantia aparente do projeto numa
        // frase: a pessoa confere a tela, não vê diferença nenhuma, e conclui que o botão
        // está quebrado.
        if (trazidos.Count == 0)
        {
            Mensagem = $"Nada a trazer da sessão de {ultima.Data}: o que ela tem escrito "
                       + "já está preenchido aqui, ou ela não tem conduta nem texto.";
            MensagemEhErro = true;
            return;
        }

        Mensagem = $"Da sessão de {ultima.Data}: {string.Join(" e ", trazidos)}. "
                   + "Nada foi gravado — confira e salve.";
        MensagemEhErro = false;
    }

    [RelayCommand]
    protected virtual Task SalvarAsync() => TentarSalvarAsync();

    /// <summary>
    /// Grava a sessão e DIZ se conseguiu.
    ///
    /// ⚠️ O <c>bool</c> existe por causa do "Finalizar atendimento" (parcela 74): encerrar
    /// é salvar a sessão E carimbar o fim, e a ORDEM entre os dois não é estilo — se a
    /// gravação falhar, o carimbo não pode acontecer, senão o balcão recebe o recado de
    /// que o médico terminou enquanto a evolução do paciente não existe em lugar nenhum.
    /// É a hierarquia da parcela 65 aplicada aqui: o fato que a clínica não pode perder
    /// vem primeiro, e o que veio depois nunca o desfaz.
    ///
    /// O comando continua existindo e continua sendo o Salvar de sempre — quem chama este
    /// método é só quem precisa saber o desfecho.
    /// </summary>
    public async Task<bool> TentarSalvarAsync()
    {
        try
        {
            SessaoUsuario.Atual.Exigir(Permissao.EditarProntuario, "escrever no prontuário");

            if (PacienteId == 0)
                throw new InvalidOperationException("Escolha o paciente antes de escrever a sessão.");

            using var scope = _escopos.CreateScope();
            var prontuario = scope.ServiceProvider.GetRequiredService<ProntuarioService>();

            var salva = await prontuario.SalvarAsync(new Evolucao
            {
                Id = EvolucaoId,
                PacienteId = PacienteId,
                ProfissionalId = ProfissionalDaSessao,
                // O vínculo com o horário é o que faz a sessão sair da lista de
                // pendências do consultório depois de escrita.
                AgendamentoId = AgendamentoDaSessao,
                AtendimentoId = AtendimentoDaSessao,
                Data = DateOnly.FromDateTime(Data),
                EvaAntes = EvaAntes,
                EvaDepois = EvaDepois,
                QueixaPrincipal = QueixaPrincipal,
                HistoriaDoencaAtual = HistoriaDoencaAtual,
                ExameFisico = ExameFisico,
                HipoteseDiagnostica = HipoteseDiagnostica,
                CidSessao = CidSessao,
                Conduta = Conduta,
                TextoEvolucao = TextoEvolucao,
                Orientacoes = Orientacoes,
                PlanoTerapeutico = PlanoTerapeutico,
                RetornoSugeridoEm = RetornoSugeridoEm is { } r
                    ? DateOnly.FromDateTime(r)
                    : null,
                RetornoSugeridoNota = RetornoSugeridoNota,
                Encaminhamento = Encaminhamento,
                // Rótulo e tipo são COPIADOS aqui: renomear o campo depois não pode
                // reescrever esta sessão. Lista vazia é "a tela os mostra e todos estão em
                // branco" — nunca `null`, que é o "não edito" do balcão.
                CamposPersonalizados = CampoPersonalizadoService.Montar(
                    _definicoesDosCampos,
                    CamposPersonalizados.ToDictionary(c => c.Id, c => c.Resposta)).ToList()
            }, SessaoUsuario.Atual.Operador);

            EvolucaoId = salva.Id;

            // O mapa é 1:1 com a evolução e só se grava DEPOIS dela: ele precisa do id da
            // sessão, e antes de a sessão existir não há a que pertencer. Os pontos
            // trazidos por "repetir" ou por protocolo viram prontuário só aqui — até este
            // ponto eram tela, e prontuário não é rascunho.
            if (Mapa is not null) await Mapa.SalvarAsync(salva.Id);

            _snackbar?.Sucesso("Sessão registrada no prontuário.");
            UltimaGravacao = $"Última gravação às {DateTime.Now:HH\\:mm}";

            // O aviso do par incompleto vem DEPOIS de gravar, e não impede: o "depois" é
            // medido ao fim do atendimento, e recusar a gravação por causa dele faria o
            // profissional escrever tudo de novo — ou desistir de medir.
            // ⚠️ Sem snackbar (a janela modal — o host dele fica na ShellWindow, ATRÁS
            // dela), a confirmação tem de vir por aqui: gravação que não diz nada é o que
            // faz a pessoa clicar de novo, e foi assim que a mesma paciente entrou três
            // vezes em 71 segundos (parcela 65).
            Mensagem = EvaAntes is not null && EvaDepois is null
                ? "Gravado. A EVA está só com a medida ANTES — sem o par não dá para dizer "
                  + "se a sessão aliviou. Volte aqui ao terminar para registrar o depois."
                : _snackbar is null ? "Sessão registrada no prontuário." : null;
            MensagemEhErro = false;

            await DepoisDeSalvarAsync();
            return true;
        }
        catch (Exception ex)
        {
            Diagnostico.Registrar($"{ContextoDoLog} — sessão não pôde ser salva", ex);
            Mensagem = ex.Message;
            MensagemEhErro = true;
            return false;
        }
    }

    /// <summary>
    /// A sessão está EM BRANCO — nada do que o profissional escreveria foi escrito.
    ///
    /// Serve ao "Finalizar atendimento": encerrar sem registro é legítimo (o médico pode
    /// escrever depois) e é exatamente a dívida que este app existe para cobrar, então a
    /// tela PERGUNTA em vez de impedir ou de calar. A EVA e o mapa não entram: eles são
    /// medida, não registro do que aconteceu.
    /// </summary>
    public bool SessaoEmBranco
        => string.IsNullOrWhiteSpace(QueixaPrincipal)
           && string.IsNullOrWhiteSpace(Conduta)
           && string.IsNullOrWhiteSpace(TextoEvolucao)
           && string.IsNullOrWhiteSpace(HistoriaDoencaAtual)
           && string.IsNullOrWhiteSpace(ExameFisico)
           && string.IsNullOrWhiteSpace(HipoteseDiagnostica)
           && string.IsNullOrWhiteSpace(Orientacoes)
           // ⚠️ O PLANO conta (parcela 75). Esquecê-lo aqui fazia o "Finalizar atendimento"
           // DESCARTAR em silêncio a sessão em que o profissional só ajustou o plano — o
           // mesmo defeito que esta parcela corrigiu horas antes, recometido por acrescentar
           // um campo e não reler quem lê a lista.
           && string.IsNullOrWhiteSpace(PlanoTerapeutico)
           // ⚠️ A MESMA lista do `temTexto` do serviço: a sessão que só decide o
           // retorno é uma sessão, e sem estas três linhas o "Finalizar" a descartaria
           // em SILÊNCIO — o defeito que a parcela 76 corrigiu com o plano.
           && RetornoSugeridoEm is null
           && string.IsNullOrWhiteSpace(RetornoSugeridoNota)
           && string.IsNullOrWhiteSpace(Encaminhamento);

    /// <summary>
    /// Há ALGUMA COISA a gravar — texto, EVA, CID ou pontos no mapa.
    ///
    /// ⚠️ É uma pergunta DIFERENTE de <see cref="SessaoEmBranco"/>, e confundir as duas foi
    /// um defeito real (parcela 74, 2ª rodada): aquela decide se a tela PERGUNTA "encerrar
    /// sem escrever a evolução?", e deixa a EVA e o mapa de fora com razão, porque eles são
    /// MEDIDA e não o registro do que aconteceu. Usá-la também para decidir se GRAVA fazia a
    /// sessão de acupuntura mais comum da casa — EVA antes 8, depois 3, seis pontos no mapa e
    /// nenhuma linha de texto — ser encerrada com <b>tudo descartado em silêncio</b>.
    ///
    /// O serviço aceita a sessão só com EVA desde sempre; era a tela que não a mandava.
    /// </summary>
    public bool TemAlgoParaGravar
        => !SessaoEmBranco
           || EvaAntes is not null
           || EvaDepois is not null
           || !string.IsNullOrWhiteSpace(CidSessao)
           || Mapa?.Pontos.Count > 0;

    /// <summary>
    /// Abre o mapa corporal em JANELA (parcela 37, rodada de leiaute).
    ///
    /// Ele morava numa aba de 530 px ao lado do formulário, e não cabia: as duas figuras
    /// são Canvas de 220×460 que NÃO esticam — é o que faz o clique virar fração — então
    /// sobrava barra de rolagem e os botões do rodapé saíam cortados pela borda da tela.
    /// A Recepção já abre o mapa numa janela de 960 de mínimo, pelo mesmo motivo.
    ///
    /// A janela NÃO grava. O mapa é 1:1 com a evolução e só se efetiva depois que a sessão
    /// existe — quem o grava continua sendo o Salvar daqui, com o id da evolução na mão.
    /// </summary>
    [RelayCommand]
    protected virtual void AbrirMapa()
    {
        // O botão apagado (TemPaciente) explica; esta guarda diz por quê quando o clique
        // chega mesmo assim — guarda que volta em silêncio é botão que não faz nada.
        if (Mapa is null)
        {
            Mensagem = "Escolha um paciente antes de abrir o mapa corporal: a tela abre "
                     + "no paciente que você está atendendo, ou use a busca.";
            MensagemEhErro = true;
            return;
        }

        try
        {
            new MapaCorporalWindow(Mapa, $"Mapa corporal — {Paciente}")
            {
                Owner = JanelaDona.Atual()
            }.ShowDialog();

            // O resumo do rodapé muda com o que foi marcado lá dentro.
            OnPropertyChanged(nameof(Mapa));
        }
        catch (Exception ex)
        {
            Diagnostico.Registrar(
                $"{ContextoDoLog} — mapa corporal não pôde ser aberto", ex);
            Mensagem = ex.Message;
            MensagemEhErro = true;
        }
    }

}

/// <summary>
/// Um campo personalizado na folha da sessão: a definição (rótulo, tipo, ajuda) com a
/// resposta que está sendo escrita.
///
/// A resposta é TEXTO para todos os tipos — quem a normaliza é o
/// <see cref="CampoPersonalizadoService"/>, na gravação, com o tipo ao lado. Um campo por
/// tipo no ViewModel daria seis propriedades com cinco nulas em toda linha, e a
/// normalização em dois lugares divergiria na primeira correção.
/// </summary>
public sealed partial class CampoDaSessao : ObservableObject
{
    public required int Id { get; init; }
    public required string Rotulo { get; init; }
    public required TipoCampoPersonalizado Tipo { get; init; }
    public string? Ajuda { get; init; }
    public required IReadOnlyList<string> Opcoes { get; init; }

    [ObservableProperty] private string? _resposta;

    /// <summary>Escolha entre opções — o resto é caixa de texto.</summary>
    public bool EhLista => Tipo == TipoCampoPersonalizado.Lista;

    /// <summary>Sim/Não vira caixinha; a resposta continua sendo texto.</summary>
    public bool EhSimNao => Tipo == TipoCampoPersonalizado.SimNao;

    /// <summary>Texto livre de uma linha, número ou data — o campo comum.</summary>
    public bool EhCaixaDeTexto => !EhLista && !EhSimNao;

    /// <summary>A dica do que se espera, sem obrigar a pessoa a errar para descobrir.</summary>
    public string Dica => Tipo switch
    {
        TipoCampoPersonalizado.Numero => "número",
        TipoCampoPersonalizado.Data => "dd/mm/aaaa",
        _ => string.Empty
    };

    public bool TemAjuda => !string.IsNullOrWhiteSpace(Ajuda);
}
