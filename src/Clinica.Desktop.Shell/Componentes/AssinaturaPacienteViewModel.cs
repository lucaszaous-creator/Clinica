using System.Collections.ObjectModel;
using Clinica.Application.Servicos;
using Clinica.Domain;
using Clinica.Domain.Entities;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Clinica.Desktop.Shell.Componentes;

/// <summary>
/// Colher a assinatura do PACIENTE num termo de procedimento (parcela 66) — o pedido da
/// clínica para o BSV, com a declaração de jejum junto.
///
/// Mora no SHELL, e não no módulo da Recepção, pelo argumento que já subiu o mapa corporal
/// e a emissão de documento para cá: o termo é apresentado em TRÊS lugares — o check-in do
/// balcão, a fila do Consultório e a sala de infusão —, e nenhum módulo conhece os outros.
/// Copiar a janela daria três telas divergindo na primeira correção, e o que elas colhem é
/// a prova de que o paciente consentiu.
///
/// O que esta tela NÃO faz
/// -----------------------
/// Ela não impede nada. Responder "não" ao jejum grava o "não" e emite o termo assim
/// mesmo — o documento existe para registrar a VERDADE, e o paciente que chegou sem jejum
/// é exatamente o fato que precisa ficar escrito e assinado. Bloquear produziria o
/// desfecho pior: ninguém emite o termo, o procedimento acontece do mesmo jeito e não
/// sobra registro nenhum. Quem decide adiar é quem faz o procedimento, e o alerta vermelho
/// chega até ele pelo <see cref="ElegibilidadeService"/>.
///
/// A única coisa que ela EXIGE é o documento de identidade conferido — porque o paciente
/// não assina com certificado, e é essa conferência que responde "quem assinou?".
/// </summary>
public sealed partial class AssinaturaPacienteViewModel : ObservableObject
{
    private readonly DocumentoClinicoService _documentos;
    private readonly AssinaturaDoPacienteService _assinaturas;
    private readonly Clinica.Desktop.Controls.IDialogoService _dialogo;
    private readonly ParametrosService? _parametros;
    private readonly int _pacienteId;
    private readonly int _modeloId;
    private readonly int? _profissionalId;

    /// <summary>
    /// O documento emitido. Nulo até <see cref="CarregarAsync"/> rodar — a emissão acontece
    /// na ABERTURA da janela e não no Salvar, porque o termo apresentado ao paciente já é
    /// um fato mesmo que ele acabe recusando.
    /// </summary>
    private DocumentoClinico? _documento;

    /// <summary>
    /// Trilha de LEITURA do prontuário (parcela 52). Opcional só para o construtor não
    /// quebrar quem monta o VM à mão — em produção ele sempre chega.
    /// </summary>
    private readonly AcessoProntuarioService? _acessos;

    public AssinaturaPacienteViewModel(
        DocumentoClinicoService documentos,
        AssinaturaDoPacienteService assinaturas,
        Clinica.Desktop.Controls.IDialogoService dialogo,
        int pacienteId,
        int modeloId,
        string pacienteNome,
        int? documentoExistenteId = null,
        int? profissionalId = null,
        AcessoProntuarioService? acessos = null,
        ParametrosService? parametros = null)
    {
        _documentos = documentos;
        _assinaturas = assinaturas;
        _dialogo = dialogo;
        _acessos = acessos;
        _parametros = parametros;
        _pacienteId = pacienteId;
        _modeloId = modeloId;
        _profissionalId = profissionalId;
        DocumentoExistenteId = documentoExistenteId;
        PacienteNome = pacienteNome;
    }

    /// <summary>Termo já emitido que só falta assinar. Nulo = emitir agora.</summary>
    public int? DocumentoExistenteId { get; }

    public string PacienteNome { get; }

    public ObservableCollection<DeclaracaoItem> Declaracoes { get; } = [];

    [ObservableProperty] private string _titulo = "Termo de procedimento";

    [ObservableProperty] private string _corpo = string.Empty;

    [ObservableProperty] private string _numero = string.Empty;

    [ObservableProperty] private string _documentoConferido = string.Empty;

    [ObservableProperty] private string _mensagem = string.Empty;

    [ObservableProperty] private bool _mensagemEhErro;

    [ObservableProperty] private bool _carregando;

    /// <summary>
    /// Há traço na área de assinatura. Quem escreve é a janela (o
    /// <c>InkCanvas</c> não se amarra a propriedade), e é o que apaga o botão de confirmar.
    /// </summary>
    [ObservableProperty] private bool _temTraco;

    /// <summary>Colhido com sucesso — a janela fecha e quem abriu recarrega.</summary>
    public bool Concluido { get; private set; }

    public event Action? Fechou;

    // ==================== A tela do paciente (parcela 66, 5ª rodada) ====================

    /// <summary>
    /// O monitor virado para o paciente, quando a clínica configurou um. Nulo = coleta numa
    /// janela só, que é o modo de quem tem um monitor — e ele continua funcionando por
    /// inteiro, porque a clínica pode ficar meses sem comprar o touch.
    ///
    /// Resolvido em <see cref="CarregarAsync"/> e não no construtor porque a leitura vai ao
    /// banco; e resolvido pelo NOME do dispositivo, nunca pelo índice — a tela configurada
    /// pode ter sido desligada desde ontem, e nesse caso a resposta certa é "não há segunda
    /// tela", jamais "use a primeira que aparecer" (que seria o termo em tela cheia por cima
    /// do trabalho da recepcionista).
    /// </summary>
    public TelaDoSistema? TelaDoPaciente { get; private set; }

    /// <summary>
    /// O termo terminou de carregar. É o que abre a tela do paciente — antes disso ela
    /// mostraria um retângulo em branco virado para quem vai assinar, que é pior do que
    /// tela nenhuma.
    /// </summary>
    public event Action? TermoCarregado;

    /// <summary>
    /// Uma declaração mudou de resposta aqui (ordem, resposta). A tela dele acompanha NO
    /// MESMO INSTANTE: o selo do termo é o SHA-256 do que o paciente tinha na frente, e uma
    /// tela atrasada faria a evidência afirmar algo que não é verdade.
    /// </summary>
    public event Action<int, string?>? DeclaracaoRespondida;

    /// <summary>
    /// Quem TESTEMUNHA a coleta. É quem fez LOGIN, nunca o usuário do Windows: no balcão
    /// duas pessoas dividem a máquina, e a testemunha é justamente o que se pergunta
    /// quando o termo é contestado.
    /// </summary>
    public string Testemunha => SessaoUsuario.Atual.Operador;

    /// <summary>
    /// As duas barreiras (a metade que EXPLICA). A que IMPEDE está no comando — só
    /// desabilitar é enfeite, porque a tecla de atalho passa direto.
    /// </summary>
    public bool PodeColher => SessaoUsuario.Atual.Pode(Permissao.ColherAssinaturaPaciente);

    /// <summary>
    /// O botão de confirmar acende quando há traço, documento conferido e permissão. Cada
    /// uma das três tem a recusa escrita no comando — botão apagado que não diz por quê é
    /// o defeito da parcela 41.
    /// </summary>
    public bool PodeConfirmar => PodeColher && TemTraco
                                 && !string.IsNullOrWhiteSpace(DocumentoConferido)
                                 && !Carregando;

    partial void OnTemTracoChanged(bool value) => OnPropertyChanged(nameof(PodeConfirmar));
    partial void OnDocumentoConferidoChanged(string value) => OnPropertyChanged(nameof(PodeConfirmar));
    partial void OnCarregandoChanged(bool value) => OnPropertyChanged(nameof(PodeConfirmar));

    /// <summary>
    /// Emite o termo (ou recupera o já emitido) e traz o texto e as declarações para a tela.
    /// </summary>
    public async Task CarregarAsync()
    {
        Carregando = true;
        Mensagem = string.Empty;
        MensagemEhErro = false;

        try
        {
            // A trilha de LEITURA (parcela 52, ponto 4 do compromisso LGPD): esta janela
            // ABRE dado de saúde — qual procedimento a pessoa vai fazer e o que ela declara
            // sobre o próprio corpo —, e quem a fecha sem assinar não deixaria rastro
            // nenhum, porque nenhuma escrita teria acontecido. Registrar UMA vez por
            // abertura, e não a cada recarga, é o padrão das telas clínicas.
            //
            // Falhar aqui NÃO impede a coleta: banco lento não pode travar o paciente na
            // frente do balcão. Mas também não passa calado — vai para o log, senão a
            // clínica acredita estar coberta e não está.
            if (_acessos is not null)
            {
                try
                {
                    await _acessos.RegistrarAsync(
                        _pacienteId, Testemunha, OrigemAcessoProntuario.Documento);
                }
                catch (Exception ex)
                {
                    Application.Diagnostico.Registrar(
                        "Assinatura do paciente — acesso ao prontuário não pôde ser registrado", ex);
                }
            }

            _documento = DocumentoExistenteId is int existente
                ? await _documentos.ObterAsync(existente)
                : await _documentos.EmitirTermoProcedimentoAsync(
                    _pacienteId, _modeloId, _profissionalId, Testemunha);

            if (_documento is null)
            {
                MensagemEhErro = true;
                Mensagem = "O termo não foi encontrado. Feche e tente de novo.";
                return;
            }

            Titulo = _documento.TituloImpresso;
            Corpo = _documento.Corpo ?? string.Empty;
            Numero = _documento.Numero;

            foreach (var antiga in Declaracoes) antiga.PropertyChanged -= AoMudarDeclaracao;
            Declaracoes.Clear();

            foreach (var item in _documento.Itens.OrderBy(i => i.Ordem))
            {
                var linha = new DeclaracaoItem(item.Ordem, item.Descricao, item.Detalhe)
                {
                    Resposta = item.Quantidade
                };

                linha.PropertyChanged += AoMudarDeclaracao;
                Declaracoes.Add(linha);
            }

            TelaDoPaciente = await ResolverTelaDoPacienteAsync();

            // Depois de tudo montado: quem escuta abre a tela do paciente, e ela mostra o
            // texto e as declarações que acabaram de chegar.
            TermoCarregado?.Invoke();
        }
        catch (Exception ex)
        {
            MensagemEhErro = true;
            Mensagem = ex.Message;
            Application.Diagnostico.Registrar("Assinatura do paciente — Carregar", ex);
        }
        finally
        {
            Carregando = false;
        }
    }

    private void AoMudarDeclaracao(object? remetente, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(DeclaracaoItem.Resposta)) return;
        if (remetente is not DeclaracaoItem linha) return;

        DeclaracaoRespondida?.Invoke(linha.Ordem, linha.Resposta);
    }

    /// <summary>
    /// Qual monitor está virado para o paciente.
    ///
    /// Falha de leitura NÃO derruba a coleta — devolve nulo, e a assinatura acontece nesta
    /// janela mesmo, que é o modo de sempre. Mas não passa calada: banco fora do ar não pode
    /// impedir alguém de assinar um termo, e também não pode fazer a clínica achar que a
    /// segunda tela parou de funcionar sem nenhum registro do porquê.
    /// </summary>
    private async Task<TelaDoSistema?> ResolverTelaDoPacienteAsync()
    {
        if (_parametros is null) return null;

        try
        {
            return TelasDoSistema.Resolver(await _parametros.ObterTelaDoPacienteAsync());
        }
        catch (Exception ex)
        {
            Application.Diagnostico.Registrar(
                "Assinatura do paciente — tela configurada não pôde ser lida", ex);
            return null;
        }
    }

    /// <summary>
    /// Grava a assinatura. O PNG é produzido pela janela a partir do <c>InkCanvas</c> — é o
    /// único pedaço que não cabe no ViewModel, porque depende do controle visual.
    /// </summary>
    public async Task<bool> ConfirmarAsync(byte[] tracoPng, int largura, int altura)
    {
        // A barreira que IMPEDE. Ela DIZ por que recusou em vez de voltar calada — guarda
        // silenciosa é botão que não faz nada (parcela 41).
        if (!SessaoUsuario.Atual.Pode(Permissao.ColherAssinaturaPaciente))
        {
            MensagemEhErro = true;
            Mensagem = "Você não tem permissão para colher a assinatura do paciente. "
                       + "Peça à direção o acesso \"Colher assinatura do paciente\".";
            return false;
        }

        if (_documento is null)
        {
            MensagemEhErro = true;
            Mensagem = "O termo ainda não foi carregado. Feche e abra de novo.";
            return false;
        }

        Carregando = true;
        Mensagem = string.Empty;
        MensagemEhErro = false;

        try
        {
            var respostas = Declaracoes.ToDictionary(d => d.Ordem, d => d.Resposta);

            await _assinaturas.ColherAsync(
                _documento.Id, tracoPng, largura, altura, respostas,
                DocumentoConferido, Testemunha);

            Concluido = true;
            Fechou?.Invoke();
            return true;
        }
        catch (Exception ex)
        {
            MensagemEhErro = true;
            Mensagem = ex.Message;
            Application.Diagnostico.Registrar("Assinatura do paciente — Confirmar", ex);
            return false;
        }
        finally
        {
            Carregando = false;
        }
    }

    /// <summary>
    /// Registra que o paciente RECUSOU. O motivo é pedido pela janela, e é obrigatório
    /// pela razão da justificativa do fechamento de caixa: recusa sem motivo deixa a mesma
    /// linha em branco que o sistema teria sem o registro.
    /// </summary>
    [RelayCommand]
    private async Task RecusarAsync()
    {
        if (!SessaoUsuario.Atual.Pode(Permissao.ColherAssinaturaPaciente))
        {
            MensagemEhErro = true;
            Mensagem = "Você não tem permissão para registrar a recusa do paciente.";
            return;
        }

        if (_documento is null)
        {
            MensagemEhErro = true;
            Mensagem = "O termo ainda não foi carregado. Feche e abra de novo.";
            return;
        }

        var motivo = _dialogo.PerguntarTexto(
            "Paciente recusou assinar",
            "Diga por que o paciente não assinou. O termo continua emitido — é ele que prova "
            + "que o documento foi apresentado a ele.",
            null);

        // Diálogo CANCELADO: sair calado é o certo, e é a exceção que a checagem 21
        // reconhece — a guarda é sobre uma variável local, não sobre uma pré-condição.
        if (string.IsNullOrWhiteSpace(motivo)) return;

        Carregando = true;

        try
        {
            await _assinaturas.RecusarAsync(_documento.Id, motivo!, Testemunha);
            Concluido = true;
            Fechou?.Invoke();
        }
        catch (Exception ex)
        {
            MensagemEhErro = true;
            Mensagem = ex.Message;
            Application.Diagnostico.Registrar("Assinatura do paciente — Recusar", ex);
        }
        finally
        {
            Carregando = false;
        }
    }

    /// <summary>Marca todas as declarações como "Sim" — o caso normal, num clique.</summary>
    [RelayCommand]
    private void ConfirmarTodas()
    {
        foreach (var d in Declaracoes) d.Resposta = RespostaDeclaracao.Sim;
    }
}

/// <summary>
/// Uma declaração do termo com a resposta do paciente.
///
/// Nasce SEM resposta de propósito: pré-marcar "Sim" seria fabricar a resposta mais
/// conveniente para a clínica, que é o oposto do que o termo existe para provar. O botão
/// "confirmar todas" existe para o caso normal não custar seis cliques — mas ele é um
/// GESTO de quem está lendo com o paciente, não um padrão que a tela assume sozinha.
/// </summary>
public sealed partial class DeclaracaoItem : ObservableObject
{
    public DeclaracaoItem(int ordem, string descricao, string? detalhe)
    {
        Ordem = ordem;
        Descricao = descricao;
        Detalhe = detalhe;
    }

    public int Ordem { get; }

    public string Descricao { get; }

    public string? Detalhe { get; }

    [ObservableProperty] private string? _resposta;

    public bool EhSim
    {
        get => RespostaDeclaracao.EhPositiva(Resposta);
        set { if (value) Resposta = RespostaDeclaracao.Sim; }
    }

    public bool EhNao
    {
        get => RespostaDeclaracao.EhNegativa(Resposta);
        set { if (value) Resposta = RespostaDeclaracao.Nao; }
    }

    partial void OnRespostaChanged(string? value)
    {
        OnPropertyChanged(nameof(EhSim));
        OnPropertyChanged(nameof(EhNao));
    }
}
