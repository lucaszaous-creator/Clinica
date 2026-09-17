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

    public static LinhaDocumentoClinico De(DocumentoClinico d) => new()
    {
        Documento = DocumentoNaTela.De(d),
        Data = d.Data.ToString("dd/MM/yyyy"),
        Sessao = ProcedenciaDaSessao.Descrever(d.Agendamento),
        Profissional = d.Profissional?.Rotulo ?? "—",
        Codigo = d.CodigoVerificacao,
        Situacao = d.Cancelado
            ? $"Cancelado em {d.CanceladoEm:dd/MM/yyyy}"
            : d.AssinadoEletronicamente
                ? $"Assinado digitalmente em {d.AssinadoEm:dd/MM/yyyy}"
                : "Válido",
        Link = string.IsNullOrWhiteSpace(d.TokenPublicacao) ? string.Empty
            : d.LinkNoAr(DateOnly.FromDateTime(DateTime.Today))
                ? $"link no ar até {d.PublicadoAte:dd/MM/yyyy}"
                : "link vencido — dá para renovar"
    };
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
    /// A régua EMITIR — as folhas clínicas que ESTA pessoa pode emitir (set/2026, tela 2
    /// do mockup aprovado "documentos nas quatro telas").
    ///
    /// Eram QUATRO botões escritos à mão no XAML (receita, atestado, comparecimento,
    /// pedido de exame), e o catálogo tem seis folhas clínicas de paciente: o
    /// <b>relatório de evolução</b> — o papel que o paciente leva ao convênio — e a
    /// <b>ficha de anamnese</b> só se emitiam pela central da RECEPÇÃO. Quem atende
    /// pedia ao balcão o relatório do próprio paciente.
    ///
    /// ⚠️ A lista vem do CATÁLOGO, e não de mais dois botões no XAML: folha nova aparece
    /// aqui sozinha, e — o que mais importa — cada uma traz o BIT dela. O comparecimento
    /// pede <c>EditarPaciente</c> e a receita pede <c>Prescrever</c>; com a régua inteira
    /// sob um <c>IsEnabled</c> só, quem tem um e não o outro via os seis apagados ou os
    /// seis acesos.
    ///
    /// ⚠️ Os dois TERMOS assinados pelo paciente ficam de fora, e é decisão: eles não se
    /// emitem por janela nenhuma — colhem-se com o traço na tela —, e a pendência deles
    /// já tem porta na RÉGUA do workspace, que acompanha o prontuário aberto nas sete
    /// seções. Duas portas para o mesmo ato na mesma tela é o que a parcela 79 tirou daqui.
    /// </summary>
    public ObservableCollection<FolhaCatalogo> FolhasParaEmitir { get; } = [];

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
    public bool EscolhendoPaciente => MostrarCabecalho && SemPaciente;

    /// <summary>O par invertido — o projeto não tem conversor de booleano invertido.</summary>
    public bool TrabalhandoNoPaciente => !EscolhendoPaciente;

    partial void OnSemPacienteChanged(bool value)
    {
        OnPropertyChanged(nameof(TemPaciente));
        OnPropertyChanged(nameof(EscolhendoPaciente));
        OnPropertyChanged(nameof(TrabalhandoNoPaciente));
    }

    private int _pacienteId;

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
            if (paciente is null) return;

            // Escolher alguém aqui TROCA o foco do posto: quem foi buscar outra pessoa
            // para emitir um documento vai continuar nela nas telas seguintes, e um foco
            // que só a metade das telas respeita é pior do que não ter foco nenhum.
            _foco.Definir(paciente.Id, paciente.Nome);
            _pacienteId = paciente.Id;
            Paciente = paciente.Nome;
            _ = CarregarAsync();
        };

        // A régua de emitir: as folhas CLÍNICAS de paciente que este acesso alcança.
        // Estática — a lista de papéis que a clínica emite não depende do banco.
        foreach (var folha in CentralDocumentosService.Catalogo.Where(EhFolhaDesteConsultorio))
            FolhasParaEmitir.Add(folha);

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

    [RelayCommand]
    public async Task CarregarAsync()
    {
        var geracao = ++_geracaoCarga;

        SemPaciente = _pacienteId == 0;
        Documentos.Clear();

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

            foreach (var d in documentos)
                Documentos.Add(LinhaDocumentoClinico.De(d));
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
    /// A folha entra na régua desta tela: clínica, de PACIENTE, e emitida por quem atende.
    ///
    /// Os dois termos assinados pelo paciente ficam de fora (ver
    /// <see cref="FolhasParaEmitir"/>), e o recibo, o orçamento e o fechamento do período
    /// também: nenhum deles é papel de quem está com o paciente na sala.
    /// </summary>
    private static bool EhFolhaDesteConsultorio(FolhaCatalogo f)
        => f.Natureza == NaturezaFolha.Clinico
           && f.Exigencia is ExigenciaFolha.Paciente or ExigenciaFolha.PacienteComProntuario
           && SessaoUsuario.Atual.Pode(f.PermissaoEmitir);

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
                     + "você está atendendo, ou use a busca ao lado.";
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
        if (!r.Silencioso)
        {
            Mensagem = r.Inline;
            MensagemEhErro = r.EhErro;
        }

        if (r.Mudou) await CarregarAsync();
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
