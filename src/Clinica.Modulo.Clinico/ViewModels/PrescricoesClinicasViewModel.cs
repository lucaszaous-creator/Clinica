using Clinica.Application.Modelos;
using System.Collections.ObjectModel;
using Clinica.Application.Servicos;
using Clinica.Clinico.Modulo;
using Clinica.Desktop.Shell.Modulos;
using Clinica.Desktop.Controls;
using Clinica.Desktop.Shell;
using Clinica.Desktop.Shell.Componentes;
using Clinica.Domain.Entities;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;

namespace Clinica.Clinico.ViewModels;

/// <summary>
/// Um documento clínico já emitido, como o consultório o lê.
///
/// ⚠️ O DOCUMENTO em si é <see cref="DocumentoNaTela"/>, e a linha o CARREGA em vez de
/// copiá-lo campo a campo (set/2026): número, tipo, cancelado, assinado, estado do link e
/// as duas permissões moram lá, resolvidos do catálogo, e os seis atos leem dali. Antes
/// cada uma das quatro telas que listam documento resolvia isso por conta própria — e elas
/// divergiram: esta apagava o botão de assinar por <c>EditarProntuario</c> enquanto o
/// comando exigia o bit do TIPO, que é a barreira visível discordando da que impede.
///
/// O que fica aqui é o que é desta TELA: o texto já formatado de cada coluna.
/// </summary>
public sealed class LinhaDocumentoClinico
{
    /// <summary>O documento, como os atos do shell o consultam.</summary>
    public required DocumentoNaTela Documento { get; init; }

    public int DocumentoId => Documento.DocumentoId;

    /// <summary>Dono do documento — a entrega precisa do telefone dele.</summary>
    public int PacienteId => Documento.PacienteId ?? 0;
    public string Numero => Documento.Numero;
    public string Tipo => Documento.Rotulo;
    public bool Cancelado => Documento.Cancelado;
    public bool Assinado => Documento.Assinado;

    public required string Data { get; init; }
    public required string Profissional { get; init; }
    public required string Codigo { get; init; }
    public required string Situacao { get; init; }

    /// <summary>O estado que decide a COR do selo (out/2026): cancelado, selado, emitido.</summary>
    public required EstadoDoDocumento Estado { get; init; }

    /// <summary>
    /// O documento no formato de "o que já saiu" — é sobre ele que a busca do histórico
    /// roda, pela MESMA regra da central (<see cref="BuscaNasFolhas"/>).
    /// </summary>
    public required FolhaEmitida Folha { get; init; }

    /// <summary>
    /// A SESSÃO a que este documento pertence (set/2026) — "Sessão de 08/09/2026, 09h00 ·
    /// Acupuntura + eletro". Vazio no documento avulso, e a linha SOME em vez de mostrar
    /// um traço.
    /// </summary>
    public string? Sessao { get; init; }

    /// <summary>
    /// Estado do LINK público (parcela 53), em palavras. Até a parcela 72 renovar e tirar do
    /// ar só existiam na central de DOCUMENTOS da Recepção — e a pergunta "o link da receita
    /// venceu, como ponho de volta?" nasce no consultório, com o paciente ligando para quem
    /// prescreveu.
    /// </summary>
    public required string Link { get; init; }

    // ===== A metade VISÍVEL dos atos: uma resolução só, no documento =====
    public bool PodeCancelar => Documento.OferecerCancelar;
    public bool PodeAssinar => Documento.OferecerAssinar;
    public bool PodeEnviar => Documento.OferecerEnviar;
    public bool PodeRenovarLink => Documento.OferecerRenovarLink;
    public bool PodeTirarDoAr => Documento.OferecerTirarDoAr;

    public static LinhaDocumentoClinico De(DocumentoClinico d)
    {
        var situacao = DocumentosDoConsultorio.Situacao(d);
        return new()
        {
            // O rótulo é o do CATÁLOGO ("Receituário", "Laudo médico") — o mesmo dos botões
            // de emitir acima e o do combo de tipo. Com o do enum a linha diria "Receita"
            // debaixo de um botão "Receituário", e o filtro por tipo não acharia nada.
            Documento = DocumentoNaTela.De(d, CentralDocumentosService.RotularClinico(d.Tipo)),
            Folha = CentralDocumentosService.ComoFolha(d),
            Data = d.Data.ToString("dd/MM/yyyy"),
            Sessao = ProcedenciaDaSessao.Descrever(d.Agendamento),
            Profissional = d.Profissional?.Rotulo ?? "—",
            Codigo = d.CodigoVerificacao,
            Situacao = situacao.Texto,
            Estado = situacao.Estado,
            Link = string.IsNullOrWhiteSpace(d.TokenPublicacao) ? string.Empty
                : d.LinkNoAr(DateOnly.FromDateTime(DateTime.Today))
                    ? $"link no ar até {d.PublicadoAte:dd/MM/yyyy}"
                    : "link vencido — dá para renovar"
        };
    }
}

/// <summary>
/// PRESCRIÇÕES no consultório (parcela 39) — receita, atestado, comparecimento e pedido
/// de exame, emitidos de onde eles nascem.
///
/// A lacuna que esta tela fecha
/// ----------------------------
/// O fluxo de emissão existia inteiro e a única porta para ele estava no módulo da
/// RECEPÇÃO. Quem prescreve, atesta e pede exame é quem ATENDE — e o app instalado na sala
/// do médico não tinha por onde. É a sétima ocorrência do defeito recorrente do projeto,
/// na variante mais discreta: não é dado sem leitor nem serviço sem chamador, é
/// <b>porta no módulo errado</b>. O CI ficava verde e a receita saía do bloquinho de
/// papel, ou o profissional descia até o balcão para pedir que alguém emitisse por ele.
///
/// Os componentes já estavam prontos e no lugar certo: a parcela 36 subiu a emissão para
/// <see cref="DocumentoWindow"/>, no shell, exatamente por isto. Esta tela não
/// reimplementa emissão nenhuma — ela abre a janela que já existe.
///
/// O paciente já vem escolhido
/// ---------------------------
/// A diferença para a tela da recepção não é estética. No consultório o paciente é
/// CONTEXTO, não parâmetro (ver <see cref="PacienteEmFoco"/>): quem acabou de atender já
/// escolheu a pessoa, e obrigá-lo a digitar o nome de novo para dar o atestado que ele
/// prometeu há trinta segundos é o tipo de atrito que faz a clínica voltar para o papel.
/// A busca continua aqui como atalho, para quem precisa emitir a segunda via de alguém
/// que não está na cadeira.
/// </summary>
public sealed partial class PrescricoesClinicasViewModel : ObservableObject
{
    private readonly IServiceScopeFactory _escopos;
    private readonly PacienteEmFoco _foco;
    private readonly ISnackbarService _snackbar;
    private readonly IDialogoService _dialogo;

    public SeletorPacienteViewModel Seletor { get; }

    /// <summary>
    /// Desenhar o cabeçalho próprio (título, subtítulo, nome do paciente e a busca).
    ///
    /// ⚠️ Falso quando a tela é uma SEÇÃO do paciente (parcela 74). Não é economia de
    /// pixel: dentro do workspace o nome já está no crachá — repeti-lo é a repetição que a
    /// parcela 37 tirou de seis telas — e, pior, o seletor de busca trocaria o
    /// <c>PacienteEmFoco</c> do posto por baixo das outras sete seções, que continuariam
    /// mostrando o paciente anterior. Duas listas de paciente na mesma tela é exatamente o
    /// mestre-detalhe que este desenho existe para acabar.
    /// </summary>
    public bool MostrarCabecalho { get; set; } = true;
    public Action? AbrirInfusaoNoPaciente { get; set; }

    public ObservableCollection<LinhaDocumentoClinico> Documentos { get; } = [];

    /// <summary>
    /// Os CARTÕES de "Novo documento" — as folhas clínicas que ESTA pessoa pode emitir, no
    /// desenho da tela de produção (out/2026: a volta ao WPF trouxe de volta a régua de
    /// botões soltos, e a clínica a leu como "a tela antiga").
    ///
    /// ⚠️ A lista vem do CATÁLOGO (<see cref="DocumentosDoConsultorio.Cartoes"/>): folha nova
    /// aparece sozinha e cada uma traz o BIT dela. O clique entrega a FOLHA, e é o
    /// <c>TipoClinico</c> dela que decide qual papel a janela abre — receita abre receita,
    /// comparecimento abre comparecimento, nunca uma folha padrão.
    ///
    /// Os dois TERMOS assinados pelo paciente ficam de fora: colhem-se com o traço na tela,
    /// e a pendência deles tem porta própria na tela do paciente.
    /// </summary>
    public IReadOnlyList<CartaoDeFolha> Cartoes { get; }

    // ==================== O HISTÓRICO: achar o papel e reimprimir ====================
    //
    // (out/2026 — a clínica: "os laudos precisam ser fáceis de LOCALIZAR e REIMPRIMIR".)
    // A lista era uma pilha corrida sem busca: quem tinha o número do laudo na mão rolava a
    // vida inteira do paciente. A busca é a MESMA da central da Recepção
    // (`BuscaNasFolhas`) — número com ou sem a barra, código de conferência, folha e quem
    // assinou —, e roda EM MEMÓRIA sobre o que a carga já leu: voltar ao banco a cada tecla
    // pagaria uma consulta para responder o que já está na máquina.

    /// <summary>Tudo o que a última carga leu, antes da busca e do tipo.</summary>
    private IReadOnlyList<LinhaDocumentoClinico> _lidas = [];

    /// <summary>As opções do combo "Tipo" — "Todas as folhas" primeiro, o laudo entre elas.</summary>
    public ObservableCollection<string> TiposDeFolha { get; } = [];

    /// <summary>
    /// O tipo escolhido. <c>string?</c> de propósito: o combo cuja lista é refeita escreve
    /// NULL aqui por um instante, e nulo é "todas" (<see cref="BuscaNasFolhas.Casa"/>).
    /// </summary>
    [ObservableProperty] private string? _tipoFiltro = BuscaNasFolhas.TodasAsFolhas;

    /// <summary>Número ("2026/0014" ou "20260014"), código de conferência, folha ou quem assinou.</summary>
    [ObservableProperty] private string? _textoBusca;

    /// <summary>"3 de 30 com “laudo”" — vazio quando nada filtra.</summary>
    [ObservableProperty] private string _recorte = string.Empty;

    /// <summary>"12 documentos deste paciente · 2 cancelados" — sobre o que foi LIDO.</summary>
    [ObservableProperty] private string _resumo = string.Empty;

    /// <summary>
    /// O vazio, com as duas perguntas separadas: "não há documento" e "nenhum bate com a
    /// busca" (<see cref="DocumentosDoConsultorio.Vazio"/>).
    /// </summary>
    [ObservableProperty] private string _tituloVazio = DocumentosDoConsultorio.Vazio(false).Titulo;
    [ObservableProperty] private string _descricaoVazio = DocumentosDoConsultorio.Vazio(false).Descricao;

    partial void OnTipoFiltroChanged(string? value) => AplicarFiltro();

    partial void OnTextoBuscaChanged(string? value) => AplicarFiltro();

    /// <summary>
    /// Republica a lista com a busca e o tipo. Síncrono do Clear() ao último Add (parcela
    /// 62), e sobre as MESMAS instâncias de linha.
    /// </summary>
    private void AplicarFiltro()
    {
        var visiveis = _lidas
            .Where(l => BuscaNasFolhas.Casa(l.Folha, TextoBusca, TipoFiltro))
            .ToList();

        Documentos.Clear();
        foreach (var l in visiveis) Documentos.Add(l);

        Recorte = BuscaNasFolhas.Recorte(visiveis.Count, _lidas.Count, TextoBusca, TipoFiltro);
        (TituloVazio, DescricaoVazio) = DocumentosDoConsultorio.Vazio(_lidas.Count > 0);
    }

    /// <summary>Limpa a busca e o tipo — o "mostrar tudo deste paciente" num clique.</summary>
    [RelayCommand]
    private void LimparBusca()
    {
        TextoBusca = null;
        TipoFiltro = BuscaNasFolhas.TodasAsFolhas;
    }

    /// <summary>
    /// Refaz o combo de tipo com o catálogo E o que a carga trouxe. O escolhido é guardado
    /// ANTES do Clear: o combo escreve NULL nele quando a lista esvazia, e sem isto toda
    /// recarga desligaria o filtro que a pessoa deixou marcado.
    /// </summary>
    private void MontarTipos()
    {
        var escolhido = TipoFiltro;
        var tipos = DocumentosDoConsultorio.Tipos(
            SessaoUsuario.Atual.Efetivas, _lidas.Select(l => l.Folha));
        TiposDeFolha.Clear();
        foreach (var t in tipos) TiposDeFolha.Add(t);
        TipoFiltro = escolhido is not null && tipos.Contains(escolhido)
            ? escolhido
            : BuscaNasFolhas.TodasAsFolhas;
    }

    [ObservableProperty] private bool _carregando;
    [ObservableProperty] private bool _naoVerificado;
    [ObservableProperty] private string? _mensagem;
    [ObservableProperty] private bool _mensagemEhErro;

    /// <summary>Nenhum paciente escolhido — a tela pede um em vez de mostrar lista vazia.</summary>
    [ObservableProperty] private bool _semPaciente = true;

    [ObservableProperty] private string _paciente = string.Empty;

    /// <summary>Inverso de <see cref="SemPaciente"/>. Existe porque o projeto não tem
    /// conversor invertido e a alternativa seria um <c>DataTrigger</c> em cada uso.</summary>
    public bool TemPaciente => !SemPaciente;

    /// <summary>
    /// A tela é a BUSCA (set/2026 — pedido da direção: a busca de paciente como tela
    /// inicial, no desenho do Novo atendimento).
    ///
    /// Ela abre pela sidebar SEM ninguém em foco, e até aqui isso significava um cabeçalho
    /// com o nome vazio, quatro botões APAGADOS e um campo de busca de 320 px espremido no
    /// canto direito — a tela mostrando o que se faz DEPOIS de escolher, antes de haver
    /// quem escolher.
    ///
    /// ⚠️ Só vale com <see cref="MostrarCabecalho"/>: como SEÇÃO da tela do paciente ela
    /// nunca está sem ninguém — a identidade está acima, e o seletor daqui trocaria o
    /// <c>PacienteEmFoco</c> por baixo das outras seções.
    /// </summary>
    public bool EscolhendoPaciente => MostrarCabecalho && (SemPaciente || TrocandoPaciente);

    /// <summary>O cartão "Paciente selecionado" — o par de <see cref="EscolhendoPaciente"/>.</summary>
    public bool MostraPacienteEscolhido => MostrarCabecalho && TemPaciente && !TrocandoPaciente;

    /// <summary>
    /// "Trocar paciente" abriu a busca sobre um paciente já escolhido. O paciente continua
    /// sendo o mesmo até alguém escolher outro — "Manter paciente" volta sem mexer em nada.
    /// </summary>
    [ObservableProperty] private bool _trocandoPaciente;

    partial void OnTrocandoPacienteChanged(bool value)
    {
        OnPropertyChanged(nameof(EscolhendoPaciente));
        OnPropertyChanged(nameof(MostraPacienteEscolhido));
    }

    [RelayCommand]
    private void TrocarPaciente() => TrocandoPaciente = true;

    [RelayCommand]
    private void ManterPaciente() => TrocandoPaciente = false;

    /// <summary>O que a região "Novo documento" diz, com e sem paciente.</summary>
    public string TextoAcoes => TemPaciente
        ? "Escolha o papel: cada cartão abre o formulário dele."
        : "Selecione um paciente para liberar as ações.";

    /// <summary>O subtítulo do histórico, com e sem paciente.</summary>
    public string TextoHistorico => TemPaciente
        ? "Consulte os registros e as ações disponíveis para cada documento."
        : "Os registros aparecem após a seleção do paciente.";

    /// <summary>Quantos documentos a carga leu — o contador ao lado do título do histórico.</summary>
    [ObservableProperty] private int _totalLido;

    partial void OnSemPacienteChanged(bool value)
    {
        OnPropertyChanged(nameof(TemPaciente));
        OnPropertyChanged(nameof(EscolhendoPaciente));
        OnPropertyChanged(nameof(MostraPacienteEscolhido));
        OnPropertyChanged(nameof(TextoAcoes));
        OnPropertyChanged(nameof(TextoHistorico));
    }

    private int _pacienteId;

    public int PacienteAtualId => _pacienteId;
    public string ContextoDaLista => $"{_pacienteId}:{_geracaoCarga}";

    public PrescricoesClinicasViewModel(
        IServiceScopeFactory escopos, PacienteEmFoco foco,
        ISnackbarService snackbar, IDialogoService dialogo)
    {
        _escopos = escopos;
        _foco = foco;
        _snackbar = snackbar;
        _dialogo = dialogo;

        // `SemBuscaInicial`: nada vai ao banco até alguém digitar ou pedir uma lista.
        Seletor = new SeletorPacienteViewModel(escopos) { SemBuscaInicial = true };
        Seletor.SelecaoMudou += paciente =>
        {
            if (paciente is null || !MostrarCabecalho) return;

            // Escolher alguém aqui TROCA o foco do posto: quem foi buscar outra pessoa
            // para emitir um documento vai continuar nela nas telas seguintes, e um foco
            // que só a metade das telas respeita é pior do que não ter foco nenhum.
            _foco.Definir(paciente.Id, paciente.Nome);
            _pacienteId = paciente.Id;
            Paciente = paciente.Nome;
            TrocandoPaciente = false;
            _ = CarregarAsync();
        };

        // Os cartões de emitir: as folhas CLÍNICAS de paciente que este acesso alcança.
        // Estáticos — a lista de papéis que a clínica emite não depende do banco.
        Cartoes = DocumentosDoConsultorio.Cartoes(SessaoUsuario.Atual.Efetivas);

        // O combo de tipo já nasce com o catálogo — antes da primeira carga ele não pode
        // abrir vazio, que se lê como "o sistema não sabe filtrar laudo".
        MontarTipos();

        // Abre já no paciente do posto — este é o ponto da tela.
        if (_foco.PacienteId is { } id)
        {
            _pacienteId = id;
            Paciente = _foco.Nome;
        }

        _ = CarregarAsync();
    }

    /// <summary>
    /// Descarte de resposta fora de ordem (parcela 50): a troca de paciente no seletor
    /// dispara uma carga por seleção, e a resposta ATRASADA do paciente anterior chegando
    /// por último poria os documentos dele sob o nome do paciente novo no cabeçalho.
    /// </summary>
    private int _geracaoCarga;

    /// <summary>Último paciente cujo acesso já entrou na trilha — a tela recarrega mais do que troca.</summary>
    private int _acessoRegistradoDe;

    /// <summary>De quem é a busca digitada — trocar de paciente a limpa.</summary>
    private int _buscaDe;

    [RelayCommand]
    public async Task CarregarAsync()
    {
        var geracao = ++_geracaoCarga;

        SemPaciente = _pacienteId == 0;

        // ⚠️ Trocou de paciente: limpa ANTES do await (a lição da parcela 89) — uma leitura
        // que falha não pode deixar na tela os documentos do paciente ANTERIOR, com a "2ª
        // via" apontando para o papel de outra pessoa. E a busca digitada para outro
        // paciente não vale para este: o "2026/0014" de um não é papel do outro.
        // Recarregar o MESMO paciente (depois de assinar, cancelar) mantém a lista e a
        // busca até a resposta chegar — apagar e redesenhar faria a linha que a pessoa
        // acabou de mexer piscar e sumir.
        if (_buscaDe != _pacienteId)
        {
            _buscaDe = _pacienteId;
            _lidas = [];
            Resumo = string.Empty;
            TotalLido = 0;
            TextoBusca = null;
            AplicarFiltro();
        }

        if (SemPaciente)
        {
            Paciente = string.Empty;
            return;
        }

        try
        {
            Carregando = true;
            NaoVerificado = false;
            Mensagem = null;
            MensagemEhErro = false;

            using var scope = _escopos.CreateScope();
            var servico = scope.ServiceProvider.GetRequiredService<DocumentoClinicoService>();

            // Trilha de LEITURA (parcela 52), na troca de paciente: receita e atestado
            // são dado de saúde, e abri-los sem rastro deixava esta porta fora da
            // resposta a "quem acessou o prontuário desta pessoa?".
            if (_acessoRegistradoDe != _pacienteId)
            {
                _acessoRegistradoDe = _pacienteId;
                await scope.ServiceProvider.GetRequiredService<AcessoProntuarioService>()
                    .RegistrarAsync(_pacienteId, SessaoUsuario.Atual.Operador,
                        OrigemAcessoProntuario.Documento);
            }

            var documentos = await servico.DoPacienteAsync(_pacienteId);

            // Chegou tarde: outra seleção já pediu uma carga mais nova.
            if (geracao != _geracaoCarga) return;

            _lidas = documentos.Select(LinhaDocumentoClinico.De).ToList();
            MontarTipos();
            AplicarFiltro();
            Resumo = DocumentosDoConsultorio.Resumo(_lidas.Select(l => l.Folha).ToList());
            TotalLido = _lidas.Count;
        }
        catch (Exception ex)
        {
            if (geracao != _geracaoCarga) return;

            NaoVerificado = true;
            Clinica.Application.Diagnostico.Registrar(
                "Consultório — documentos do paciente não puderam ser carregados", ex);
            Mensagem = ex.Message;
            MensagemEhErro = true;
        }
        finally
        {
            // A carga superada não apaga o "Carregando" da que ainda está no ar.
            if (geracao == _geracaoCarga) Carregando = false;
        }
    }

    /// <summary>
    /// Emite já a folha pedida.
    ///
    /// São botões por TIPO, e não um "Novo documento" que abre pedindo o tipo, porque no
    /// consultório a decisão vem ANTES do clique: ninguém pensa "vou emitir um documento",
    /// pensa "vou dar um atestado".
    ///
    /// As QUATRO escritas abrem a janela do shell com o tipo pré-selecionado; as duas
    /// MONTADAS do prontuário (relatório de evolução e anamnese) não passam por janela
    /// nenhuma — emitir é imprimir o que já está lá.
    /// </summary>
    [RelayCommand]
    private async Task EmitirAsync(FolhaCatalogo? folha)
    {
        // Sem paciente, DIZ. O botão já nasce apagado, mas um atalho de teclado ou um
        // clique numa corrida de carregamento chegam aqui — e guarda que volta em
        // silêncio é exatamente o defeito que esta linha corrigiu.
        if (folha is null) return;
        if (_pacienteId == 0)
        {
            Mensagem = "Escolha um paciente antes de emitir: a tela abre no paciente que "
                     + "você está atendendo, ou use a busca acima.";
            MensagemEhErro = true;
            return;
        }

        try
        {
            // O bit da FOLHA que está sendo emitida — não um fixo. Receita pede
            // `Prescrever`; declaração de comparecimento, não (parcela 59/60).
            SessaoUsuario.Atual.Exigir(
                folha.PermissaoEmitir, $"emitir {folha.Rotulo.ToLowerInvariant()}");

            if (folha.MontadaDoProntuario) await EmitirMontadaAsync(folha);
            else await AbrirJanelaAsync(folha);
        }
        catch (Exception ex)
        {
            Clinica.Application.Diagnostico.Registrar(
                "Consultório — documento não pôde ser emitido", ex);
            Mensagem = ex.Message;
            MensagemEhErro = true;
        }
    }

    private async Task AbrirJanelaAsync(FolhaCatalogo folha)
    {
        if (folha.TipoClinico is not { } tipo) return;

        var vm = new DocumentoEdicaoViewModel(_escopos, _pacienteId, tipo);
        var janela = new DocumentoWindow(vm)
        {
            Owner = JanelaDona.Atual()
        };

        var concluiu = janela.ShowDialog() == true;

        // Recarrega dos dois jeitos: fechar sem concluir não significa que nada
        // aconteceu — o documento pode ter sido emitido e só a impressão ter falhado.
        // A busca sai junto: o papel que acabou de sair tem de APARECER, e um filtro
        // esquecido o esconderia logo depois de emitido.
        if (concluiu) LimparBusca();
        await CarregarAsync();

        if (concluiu) _snackbar.Sucesso($"{folha.Rotulo} emitido(a).");
    }

    /// <summary>
    /// As montadas do prontuário. O sistema monta e imprime: não há o que digitar, e por
    /// isso não há janela — abrir uma pediria à pessoa que confirmasse um formulário em
    /// branco.
    /// </summary>
    private async Task EmitirMontadaAsync(FolhaCatalogo folha)
    {
        DocumentoClinico emitido;
        using (var scope = _escopos.CreateScope())
        {
            var servico = scope.ServiceProvider.GetRequiredService<DocumentoClinicoService>();
            var operador = SessaoUsuario.Atual.Operador;

            emitido = folha.TipoClinico == TipoDocumentoClinico.Anamnese
                ? await servico.EmitirAnamneseAsync(_pacienteId, operador: operador)
                : await servico.EmitirRelatorioEvolucaoAsync(_pacienteId, operador: operador);
        }

        LimparBusca();
        await CarregarAsync();

        // Pelo DOCUMENTO recém-emitido, e não pela linha da lista: a folha pode não estar na
        // coleção ainda (ou ter caído fora do filtro), e nesse caso a impressão simplesmente
        // não aconteceria — em silêncio, depois de o número ter sido gasto.
        var impressao = await AcoesDoDocumento.ImprimirAsync(
            DocumentoNaTela.De(emitido, folha.Rotulo), _escopos);
        Mensagem = impressao.Inline;
        MensagemEhErro = impressao.EhErro;

        _snackbar.Sucesso($"{folha.Rotulo} {emitido.Numero} emitido(a).");
    }

    /// <summary>
    /// A prescrição de infusão é OUTRA tela, e por isso o botão NAVEGA em vez de emitir.
    ///
    /// Ela não é um <c>DocumentoClinico</c>: tem ciclo de vida próprio (rascunho →
    /// assinada → executada → encerrada) e é executada item a item pela enfermagem — foi
    /// por isso que a parcela 42 recusou enfiá-la no catálogo de folhas.
    /// </summary>
    [RelayCommand]
    private void IrParaInfusao()
    {
        if (AbrirInfusaoNoPaciente is { } abrir) abrir();
        else NavegacaoSuite.Ir(ChavesSuite.ConsultorioPrescricaoInfusao);
    }

    /// <summary>
    /// A tela de prescrição de infusão existe NESTE executável.
    ///
    /// ⚠️ Propriedade de INSTÂNCIA, e não estática: <c>{Binding X}</c> não alcança membro
    /// estático — o botão sumiria da tela sem erro nenhum (a lição das formas de pagamento
    /// da conciliação).
    /// </summary>
    public bool TemPrescricaoDeInfusao
        => AbrirInfusaoNoPaciente is not null || NavegacaoSuite.Existe(ChavesSuite.ConsultorioPrescricaoInfusao);

    // ==================== Os SEIS atos: no shell, não aqui ====================
    //
    // Eram ~300 linhas aqui, e cópias delas na central de documentos, no Receituário e na
    // ficha do paciente — cada tela com um subconjunto diferente. Ver
    // <see cref="AcoesDoDocumento"/>: o que atravessa é o DOCUMENTO, a linha continua sendo
    // a desta tela, e o nome dos comandos não mudou (o "⋯" da lista os chama por nome).

    /// <summary>
    /// Escreve o que o ato respondeu e relê a lista quando o documento mudou de estado.
    ///
    /// É ROTEIRO, não regra: o que dizer, por qual canal e o que impede mora no ato, que é
    /// o ponto único. Silencioso = a pessoa desistiu num diálogo, e aí não se mexe na
    /// mensagem que estava na tela.
    /// </summary>
    private async Task AplicarAsync(ResultadoAcaoDocumento r)
    {
        // Relê PRIMEIRO e escreve DEPOIS: a carga começa zerando a mensagem, e na ordem
        // inversa a frase do ato ("Laudo 2026/0014 cancelado") sumia no mesmo instante em
        // que era escrita — só o erro sobrevivia (a lição da parcela 68).
        if (r.Mudou) await CarregarAsync();

        if (!r.Silencioso)
        {
            Mensagem = r.Inline;
            MensagemEhErro = r.EhErro;
        }
    }

    /// <summary>
    /// Segunda via: reimprime o que foi EMITIDO, não o que o prontuário diz hoje. É a regra
    /// do documento clínico, e ela mora no serviço — a via que o paciente levou e a que a
    /// clínica reimprime têm de ser a mesma folha.
    /// </summary>
    [RelayCommand]
    private async Task ImprimirAsync(LinhaDocumentoClinico? linha)
    {
        if (linha is null) return;
        await AplicarAsync(await AcoesDoDocumento.ImprimirAsync(linha.Documento, _escopos));
    }

    /// <summary>
    /// Altera o laudo emitindo uma nova versão, que cancela a anterior no mesmo gravar
    /// (out/2026). O ato é o do shell; esta tela só o liga ao "⋯" da linha.
    /// </summary>
    [RelayCommand]
    private async Task NovaVersaoAsync(LinhaDocumentoClinico? linha)
    {
        if (linha is null) return;
        await AplicarAsync(
            await AcoesDoDocumento.NovaVersaoAsync(linha.Documento, _escopos, _snackbar));
    }

    /// <summary>Assina com o certificado ICP-Brasil um documento já emitido (parcela 43).</summary>
    [RelayCommand]
    private async Task AssinarAsync(LinhaDocumentoClinico? linha)
    {
        if (linha is null) return;
        await AplicarAsync(
            await AcoesDoDocumento.AssinarAsync(linha.Documento, _escopos, _snackbar));
    }

    /// <summary>Entrega o ARQUIVO assinado ao paciente pelo WhatsApp (parcela 43, 2ª rodada).</summary>
    [RelayCommand]
    private async Task EnviarAsync(LinhaDocumentoClinico? linha)
    {
        if (linha is null) return;
        await AplicarAsync(await AcoesDoDocumento.EnviarAsync(linha.Documento, _escopos));
    }

    /// <summary>Põe o link vencido de volta no ar, reusando o MESMO token (parcela 53).</summary>
    [RelayCommand]
    private async Task RenovarLinkAsync(LinhaDocumentoClinico? linha)
    {
        if (linha is null) return;
        await AplicarAsync(await AcoesDoDocumento.RenovarLinkAsync(linha.Documento, _escopos, _snackbar));
    }

    /// <summary>O par do renovar: tira do ar AGORA um link publicado.</summary>
    [RelayCommand]
    private async Task TirarDoArAsync(LinhaDocumentoClinico? linha)
    {
        if (linha is null) return;
        await AplicarAsync(
            await AcoesDoDocumento.TirarDoArAsync(linha.Documento, _escopos, _dialogo, _snackbar));
    }

    /// <summary>
    /// Cancela com motivo. A linha continua na lista marcada como cancelada: a via em papel
    /// não desaparece por ser apagada do sistema.
    /// </summary>
    [RelayCommand]
    private async Task CancelarAsync(LinhaDocumentoClinico? linha)
    {
        if (linha is null) return;
        await AplicarAsync(
            await AcoesDoDocumento.CancelarAsync(linha.Documento, _escopos, _dialogo, _snackbar));
    }
}
