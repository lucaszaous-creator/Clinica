using System.Collections.ObjectModel;
using System.Globalization;
using Clinica.Application.Abstracoes;
using Clinica.Application.Modelos;
using Clinica.Application.Servicos;
using Clinica.Desktop.Controls;
using Clinica.Desktop.Shell;
using Clinica.Desktop.Shell.Componentes;
using Clinica.Desktop.Shell.Modulos;
using Clinica.Domain;
using Clinica.Domain.Entities;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;

namespace Clinica.Recepcao.ViewModels;

/// <summary>Uma das nove folhas, como o cartão a mostra.</summary>
public sealed partial class LinhaFolha : ObservableObject
{
    public required FolhaCatalogo Folha { get; init; }
    public required string Rotulo { get; init; }
    public required string Descricao { get; init; }
    public required string Grupo { get; init; }

    /// <summary>O que falta para poder gerar, já escrito. Vazio = dá para gerar agora.</summary>
    [ObservableProperty] private string _pendencia = string.Empty;

    [ObservableProperty] private bool _podeGerar;

    /// <summary>
    /// Marcada no passo 2. É estado de TELA, não do catálogo: quem o escreve é o
    /// <c>OnFolhaEscolhidaChanged</c>, por REFERÊNCIA — a coleção é montada uma vez e as
    /// instâncias são as mesmas do começo ao fim.
    /// </summary>
    [ObservableProperty] private bool _escolhida;

    /// <summary>Rótulo do botão: "Emitir", "Gerar PDF" ou "Ir para o Caixa".</summary>
    public required string AcaoRotulo { get; init; }
}

/// <summary>Uma folha já emitida, como a lista a mostra.</summary>
public sealed class LinhaFolhaEmitida
{
    public required int DocumentoId { get; init; }

    /// <summary>De quem é a folha — a trilha de acesso da 2ª via precisa (parcela 62).</summary>
    public int? PacienteId { get; init; }
    public required NaturezaFolha Natureza { get; init; }
    public required string Numero { get; init; }
    public required string Folha { get; init; }
    public required string Para { get; init; }
    public required string Data { get; init; }
    public required string Detalhe { get; init; }
    public required bool Cancelado { get; init; }
    public required string Situacao { get; init; }

    /// <summary>
    /// Cancelar duas vezes não existe, e sem permissão também não. É a metade visível da
    /// regra; a que impede é o <c>Exigir</c> no comando.
    /// </summary>
    public required bool PodeCancelar { get; init; }

    /// <summary>
    /// O acesso que ESTA folha exige para ser cancelada ou republicada (parcela 59).
    ///
    /// Viaja na linha porque o comando recebe a linha, e não a folha: sem ele o
    /// <c>Exigir</c> teria de reabrir o catálogo pela chave, e a metade que IMPEDE
    /// passaria a depender de uma segunda resolução que pode divergir da que apagou o
    /// botão.
    /// </summary>
    public required Permissao AcessoParaMexer { get; init; }

    /// <summary>
    /// O documento CLÍNICO desta linha, como os seis atos do shell o consultam
    /// (<see cref="AcoesDoDocumento"/>). NULO na folha FINANCEIRA — recibo e orçamento não
    /// se assinam com e-CPF, não se publicam e têm outro PDF e outro serviço de
    /// cancelamento.
    ///
    /// ⚠️ É ele que trouxe ASSINAR e ENVIAR para esta tela (set/2026). A central emitia a
    /// receita e não tinha como selá-la nem entregá-la: para terminar o trabalho a
    /// recepcionista saía daqui e ia procurar outra tela — que é o relato de
    /// "intuitividade de documentos" que esta parcela responde.
    /// </summary>
    public DocumentoNaTela? Documento { get; init; }

    /// <summary>Sela o arquivo com o e-CPF. Só folha clínica, e só a que falta assinar.</summary>
    public bool PodeAssinar => Documento?.OferecerAssinar ?? false;

    /// <summary>Entrega o ARQUIVO assinado ao paciente. Só depois de assinado.</summary>
    public bool PodeEnviar => Documento?.OferecerEnviar ?? false;

    /// <summary>
    /// O link venceu e dá para colocá-lo de volta no ar (parcela 53). Só aparece em
    /// documento que JÁ teve link: republicar reusa o mesmo token, e o QR impresso que o
    /// paciente guardou volta a funcionar.
    /// </summary>
    public bool PodeRenovarLink { get; init; }

    /// <summary>
    /// O link está no ar e dá para TIRÁ-LO (parcela 63) — o par que faltava do renovar.
    ///
    /// Até aqui a publicação só saía do ar sozinha, na expiração. Receita publicada por
    /// engano — o paciente errado, o documento errado — ficava acessível por quem tivesse
    /// o endereço até o prazo vencer, que a clínica configura em 30 ou 180 dias. É dado
    /// de saúde num endereço público: quem publica precisa poder despublicar.
    /// </summary>
    public bool PodeTirarDoAr { get; init; }

    /// <summary>
    /// O que dizer sobre o link, ou vazio quando o documento nunca teve um. Frase inteira
    /// e não um selo: "no ar até 09/10" e "link vencido" levam a ações diferentes, e um
    /// ícone obrigaria a recepção a decorar o que ele quer dizer.
    /// </summary>
    public string Link { get; init; } = string.Empty;
}

/// <summary>
/// Quem tem horário HOJE — a pílula que o balcão clica em vez de digitar o nome
/// (set/2026, tela 3 do mockup aprovado "documentos nas quatro telas").
/// </summary>
public sealed record PacienteDeHoje(Paciente Paciente, string Hora)
{
    public string Nome => Paciente.Nome;
}

/// <summary>
/// A central de documentos (parcela 24): as nove folhas do mockup num lugar só.
///
/// O mockup mostrava nove folhas como um conjunto — receituário, atestado, declaração de
/// comparecimento, solicitação de exames, relatório de evolução, ficha de anamnese, recibo,
/// orçamento e fechamento do período. No sistema **as nove existiam e nenhuma estava no
/// mesmo lugar**: quatro saíam de uma janela dentro da ficha do paciente, três só do botão
/// certo na aba certa dessa ficha, o recibo do Caixa, o orçamento só de dentro de um pacote
/// vendido, e o fechamento do período só do app de faturamento — que a suíte nem abre.
///
/// Quem foi treinado no mockup procurava "Documentos" e não achava. Não faltava capacidade,
/// faltava porta. Esta tela é a porta.
///
/// Ela **não reimplementa emissão nenhuma**: abre a janela que já existe, ou chama o
/// serviço dono da folha. Reescrever aqui daria dois caminhos para o mesmo papel, e só um
/// receberia a próxima correção.
/// </summary>
public sealed partial class DocumentosViewModel : ObservableObject
{
    private static readonly CultureInfo Brasil = new("pt-BR");

    private readonly IServiceScopeFactory _escopos;
    private readonly ISnackbarService _snackbar;
    private readonly IDialogoService _dialogo;

    public ObservableCollection<LinhaFolha> Folhas { get; } = [];
    public ObservableCollection<LinhaFolhaEmitida> Emitidas { get; } = [];

    /// <summary>Escolher paciente é um componente só — esta tela usa o de sempre.</summary>
    public SeletorPacienteViewModel Seletor { get; }

    /// <summary>
    /// Quem tem horário HOJE, para o balcão clicar em vez de digitar (set/2026, tela 3 do
    /// mockup aprovado).
    ///
    /// No balcão a pergunta quase nunca é "que papel existe?" — é "a paciente está aqui
    /// pedindo uma declaração". A tela abria com um campo de busca vazio numa faixa de
    /// 280 px ao lado, e a recepcionista digitava o nome de alguém que o sistema já sabia
    /// que estava na clínica naquela hora.
    ///
    /// ⚠️ São as MESMAS pessoas que o Novo atendimento oferece (a sugestão do mockup 05):
    /// cancelado e falta saem — o horário existe e a pessoa não veio —, e o REALIZADO
    /// fica, que é justamente quem acabou de ser atendido e está no balcão pedindo o
    /// papel.
    /// </summary>
    public ObservableCollection<PacienteDeHoje> DeHoje { get; } = [];

    /// <summary>Há gente com horário hoje. A linha inteira some quando não há.</summary>
    public bool TemGenteHoje => DeHoje.Count > 0;

    /// <summary>
    /// Todos os que têm horário hoje — inclusive os que não couberam na linha de pílulas.
    ///
    /// A coleção de cima é o ATALHO, cortada em <see cref="PilulasNaLinha"/>; esta é a
    /// resposta. Quem lê é o contexto do paciente escolhido: escolhido pela BUSCA, ele
    /// pode ser o décimo primeiro do dia, e o horário dele é um fato do mesmo jeito.
    /// </summary>
    private IReadOnlyList<PacienteDeHoje> _deHojeCompleto = [];

    /// <summary>
    /// Quantas pílulas cabem antes de a linha virar um paredão.
    ///
    /// As pílulas são ATALHO, não a lista do dia: num dia de trinta sessões elas
    /// quebrariam em quatro linhas e empurrariam as fichas para fora da vista — a faixa
    /// que come a tela, que o README recusa desde a parcela 38. Passando disso, digitar é
    /// mais rápido de qualquer forma, e o campo está do lado.
    /// </summary>
    private const int PilulasNaLinha = 10;

    /// <summary>
    /// O rótulo DIZ quando a lista está cortada: "de quem está hoje" sobre dez pílulas de
    /// um dia de trinta faria a recepcionista concluir que só dez pessoas vêm hoje.
    /// </summary>
    [ObservableProperty] private string _rotuloDeHoje = "ou escolha de quem está hoje:";

    /// <summary>
    /// Explicar o que NÃO está aqui (set/2026, tela 3 do mockup aprovado).
    ///
    /// Receita, atestado e pedido de exame exigem <c>Prescrever</c> e por isso não
    /// aparecem para o balcão — o cartão SOME em vez de ficar apagado (a regra da parcela
    /// 59). Só que ausência sem explicação faz a recepcionista procurar o botão que ela
    /// viu ontem na tela de outra pessoa: a frase diz que aqueles papéis são de quem
    /// ASSINA, e que aqui eles saem como segunda via.
    ///
    /// Some para quem PODE prescrever — a mesma tela abre no Gerente Geral, e ali a frase
    /// falaria de uma ausência que não existe.
    /// </summary>
    public bool ExplicaOQuePedeProfissional => !SessaoUsuario.Atual.Pode(Permissao.Prescrever);

    /// <summary>
    /// O contexto do paciente escolhido: o horário dele hoje, quando existe.
    ///
    /// Vazio quando ele não tem horário — e vazio é a resposta certa: inventar "esteve
    /// aqui hoje" para quem passou só para pegar uma segunda via seria afirmar uma
    /// presença que a agenda não registra.
    /// </summary>
    [ObservableProperty] private string _contextoDoPaciente = string.Empty;

    [ObservableProperty] private DateTime _inicio = DateTime.Today.AddDays(-30);
    [ObservableProperty] private DateTime _fim = DateTime.Today;

    /// <summary>
    /// Recortar "o que já saiu" pelo paciente escolhido (set/2026).
    ///
    /// A aba responde por PERÍODO — é a conferência do dia, antes de fechar o balcão. Mas
    /// com o "Receituário" fora (a terceira porta da Recepção para papel, que a direção
    /// mandou unificar), esta tela ficou sendo também onde se pergunta "quais papéis ESTE
    /// paciente já levou?" — e a resposta exigia abrir a ficha dele noutra tela.
    ///
    /// ⚠️ Nasce DESMARCADO, e é decisão: escolher o paciente ali em cima é para EMITIR, e
    /// filtrar a lista sozinho mudaria, sem ninguém pedir, o que a aba do lado responde. O
    /// resumo DIZ o recorte quando ele está ligado — "12 de 30" e "12" respondem perguntas
    /// diferentes, e quem volta à tela depois do café não lembra o que deixou marcado.
    /// </summary>
    [ObservableProperty] private bool _soDoPaciente;

    /// <summary>
    /// A folha marcada no PASSO 2. Nula enquanto ninguém escolheu — e aí o passo 3 não
    /// existe, em vez de existir vazio: bloco reservado sem conteúdo se lê como região que
    /// não carregou.
    /// </summary>
    [ObservableProperty] private LinhaFolha? _folhaEscolhida;

    /// <summary>O que o PASSO 3 afirma. Nulo junto com a folha escolhida.</summary>
    [ObservableProperty] private PreviaDaFolha? _previa;

    /// <summary>
    /// A linha de baixo da faixa do passo 1: documento · idade · convênio · o horário de
    /// hoje. Montada AQUI, e não por bindings concatenados no XAML, porque ela precisa
    /// PULAR o que a ficha não tem — a importação do Smart Clinic produziu fichas
    /// incompletas às centenas, e cada uma sairia com " ·  · " no meio.
    ///
    /// ⚠️ A idade sai de <see cref="IdadeDoPaciente"/>, nunca de uma conta à mão: a regra
    /// recusa o IMPLAUSÍVEL e escreve o terceiro estado ("data a conferir"), que é o que
    /// impede o "1851 anos" de voltar por uma nona porta.
    /// </summary>
    public string IdentidadeDoPaciente
    {
        get
        {
            if (Seletor.Selecionado is not { } p) return string.Empty;

            var partes = new List<string>();
            if (!string.IsNullOrWhiteSpace(p.DocumentoFormatado)) partes.Add(p.DocumentoFormatado);
            if (IdadeDoPaciente.Texto(p.DataNascimento, DateOnly.FromDateTime(DateTime.Today))
                is { Length: > 0 } idade) partes.Add(idade);
            // ⚠️ A MESMA palavra da linha da lista, dois segundos antes: o nome de
            // catálogo da ficha sem convênio tem 33 caracteres ("A definir (importado sem
            // convênio)") e a lista já o resume em "sem convênio". Dois jeitos de dizer o
            // mesmo fato na mesma tela fazem procurar a diferença que não existe.
            if (p.ConvenioADefinir) partes.Add("sem convênio");
            else if (!string.IsNullOrWhiteSpace(p.ConvenioNome)) partes.Add(p.ConvenioNome);
            if (!string.IsNullOrWhiteSpace(ContextoDoPaciente)) partes.Add(ContextoDoPaciente);

            return string.Join(" · ", partes);
        }
    }

    /// <summary>
    /// Volta ao passo 1. Limpar a escolha é o que faz a busca reaparecer — e ela reaparece
    /// com o termo de antes, que é o certo: quem troca de paciente quase sempre troca
    /// dentro da mesma família de nomes.
    /// </summary>
    [RelayCommand]
    private void TrocarPaciente() => Seletor.Limpar();

    partial void OnFolhaEscolhidaChanged(LinhaFolha? value)
    {
        AtualizarPrevia();
        foreach (var linha in Folhas) linha.Escolhida = ReferenceEquals(linha, value);
    }

    [ObservableProperty] private string _resumo = string.Empty;
    [ObservableProperty] private bool _carregando;

    /// <summary>
    /// A leitura FALHOU — o terceiro estado. Sem ele, lista vazia por erro fica idêntica
    /// a lista vazia por não haver nada.
    /// </summary>
    [ObservableProperty] private bool _naoVerificado;

    [ObservableProperty] private string? _mensagem;
    [ObservableProperty] private bool _mensagemEhErro;

    // ---- Conferência pelo código impresso (parcela 25) ----
    [ObservableProperty] private string? _codigo;

    /// <summary>O que o código achou, já escrito. Nulo = ninguém conferiu nada ainda.</summary>
    [ObservableProperty] private string? _conferido;

    /// <summary>Documento cancelado ou código que não existe — os dois pedem destaque.</summary>
    [ObservableProperty] private bool _conferidoCancelado;

    /// <summary>
    /// O documento que o código achou, já no formato dos atos do shell. Era um par de ids
    /// (documento + paciente), e a 2ª via saía com o nome "Documento-42.pdf": sem o tipo e
    /// o número, o arquivo no disco não dizia o que era.
    /// </summary>
    private DocumentoNaTela? _conferidoDoc;

    /// <summary>Só há segunda via quando o código achou mesmo um documento.</summary>
    public bool TemConferido => _conferidoDoc is not null;

    partial void OnConferidoChanged(string? value) => OnPropertyChanged(nameof(TemConferido));

    /// <summary>
    /// Os acessos de quem está logado. Ficam numa propriedade porque a tela os consulta em
    /// três lugares — o catálogo, a lista do que já saiu e os botões de cada linha —, e
    /// consultar a sessão em cada um deles seria a mesma regra escrita três vezes.
    /// </summary>
    private static Permissao Acessos => SessaoUsuario.Atual.Efetivas;

    /// <summary>
    /// Descarte de resposta fora de ordem (parcela 50): "De" e "Até" disparam uma carga
    /// cada, e ajustar o período é mexer nos dois em seguida — a resposta do período velho
    /// pode voltar por último e a lista mostraria folhas que não são do filtro escolhido.
    /// Só a carga mais nova escreve na tela.
    /// </summary>
    private int _geracaoCarga;

    public DocumentosViewModel(
        IServiceScopeFactory escopos, ISnackbarService snackbar, IDialogoService dialogo)
    {
        _escopos = escopos;
        _snackbar = snackbar;
        _dialogo = dialogo;

        // `SemBuscaInicial`: com o campo vazio a busca não filtra nada e despejaria o
        // começo do alfabeto de 2.238 fichas. Quem a tela oferece é quem está HOJE.
        Seletor = new SeletorPacienteViewModel(escopos) { SemBuscaInicial = true };
        // ⚠️ O parâmetro é NOMEADO, e não `_`: com o descarte, o `_ = CarregarAsync()` lá
        // embaixo atribui a Task ao PARÂMETRO do lambda (que é `Paciente?`) em vez de
        // descartá-la. O `compilar-sombra` pegou; o CI pegaria sete minutos depois.
        Seletor.SelecaoMudou += escolhido =>
        {
            AtualizarDisponibilidade();
            AtualizarContextoDoPaciente();
            OnPropertyChanged(nameof(TemPacienteEscolhido));
            OnPropertyChanged(nameof(IdentidadeDoPaciente));

            // Trocar de paciente com o filtro LIGADO tem de trocar a lista junto: senão ela
            // continuaria mostrando as folhas de quem já saiu do balcão, com o "2ª via"
            // apontando para o documento da outra pessoa.
            if (SoDoPaciente) _ = CarregarAsync();
        };

        MontarCatalogo();
        _ = CarregarDeHojeAsync();
        _ = CarregarAsync();
    }

    // ⚠️ A prévia do passo 3 vai junto: com "Fechamento do período" escolhido, o "para
    // quem" É o período — e ele continuaria escrito com as datas de antes, afirmando na
    // tela um recorte que o PDF não vai ter.
    partial void OnInicioChanged(DateTime value)
    {
        AtualizarPrevia();
        _ = CarregarAsync();
    }

    partial void OnFimChanged(DateTime value)
    {
        AtualizarPrevia();
        _ = CarregarAsync();
    }

    // A faixa do passo 1 lê o contexto ("tem horário hoje às 14h"), e ele chega DEPOIS da
    // escolha — a lista de quem está hoje é outra consulta. Sem isto a faixa nasceria sem a
    // única informação que o balcão usa para conferir que é a pessoa certa.
    partial void OnContextoDoPacienteChanged(string value)
        => OnPropertyChanged(nameof(IdentidadeDoPaciente));

    /// <summary>
    /// Quem tem horário hoje. Uma consulta, na abertura.
    ///
    /// Falha SOZINHA: a lista é um ATALHO — a busca continua ali —, e um banco lento não
    /// pode impedir a recepcionista de emitir uma declaração. Mas não passa calada: vai
    /// ao log, e a linha das pílulas some em vez de ficar vazia dizendo que ninguém veio.
    /// </summary>
    private async Task CarregarDeHojeAsync()
    {
        try
        {
            using var scope = _escopos.CreateScope();
            var repo = scope.ServiceProvider.GetRequiredService<IClinicaRepositorio>();

            var hoje = DateTime.Today;
            var agendamentos = await repo.AgendamentosNoPeriodoAsync(
                hoje, hoje.AddDays(1).AddTicks(-1));

            // Entre o Clear() e o último Add não pode haver await (parcela 62).
            var pilulas = agendamentos
                .Where(a => a.Status is StatusAgendamento.Agendado or StatusAgendamento.Realizado)
                .Where(a => a.Paciente is not null)
                .OrderBy(a => a.DataHora)
                // Quem tem duas sessões no dia apareceria duas vezes, e duas pílulas
                // idênticas é a pessoa perguntando qual das duas é a certa.
                .DistinctBy(a => a.PacienteId)
                .Select(a => new PacienteDeHoje(a.Paciente!, a.DataHora.ToString("HH':'mm")))
                .ToList();

            // ⚠️ A lista INTEIRA fica guardada, e não só o que coube na linha: quem
            // responde "tem horário hoje às 14h" é `AtualizarContextoDoPaciente`, e
            // procurar isso na coleção CORTADA faria a frase sumir para o paciente de
            // número onze — a tela calando sobre um horário que existe, que é pior do que
            // não ter a frase.
            _deHojeCompleto = pilulas;

            DeHoje.Clear();
            foreach (var p in pilulas.Take(PilulasNaLinha)) DeHoje.Add(p);

            RotuloDeHoje = pilulas.Count > PilulasNaLinha
                ? $"ou escolha de quem está hoje ({PilulasNaLinha} de {pilulas.Count} — digite para achar os outros):"
                : "ou escolha de quem está hoje:";
        }
        catch (Exception ex)
        {
            Clinica.Application.Diagnostico.Registrar(
                "Recepção — quem tem horário hoje não pôde ser lido", ex);
            _deHojeCompleto = [];
            DeHoje.Clear();
        }

        OnPropertyChanged(nameof(TemGenteHoje));
    }

    /// <summary>
    /// Escolher pela pílula é a mesma escolha da busca — o seletor é um só.
    ///
    /// ⚠️ Por <c>SelecionarGarantindoNaLista</c>, e nunca atribuindo <c>Selecionado</c>
    /// direto: a lista de resultados está VAZIA (a busca só roda com termo digitado), e um
    /// <c>Selector</c> do WPF cujo <c>SelectedItem</c> recebe um item que não está no
    /// <c>ItemsSource</c> devolve NULL pelo binding de volta — o clique na pílula limparia
    /// a escolha no mesmo instante em que a faz, sem erro nenhum. O componente já tem esta
    /// porta desde a remarcação; ela existe exatamente para isto.
    /// </summary>
    [RelayCommand]
    private void EscolherDeHoje(PacienteDeHoje? pilula)
    {
        if (pilula is null) return;
        Seletor.SelecionarGarantindoNaLista(pilula.Paciente);
    }

    /// <summary>
    /// Há paciente escolhido — é o que faz o chip "só deste paciente" existir. Sem ele o
    /// filtro seria uma caixinha que não filtra nada (o campo morto da parcela 49).
    /// </summary>
    public bool TemPacienteEscolhido => Seletor.Selecionado is not null;

    partial void OnSoDoPacienteChanged(bool value) => _ = CarregarAsync();

    private void AtualizarContextoDoPaciente()
    {
        if (Seletor.Selecionado is not { } paciente)
        {
            ContextoDoPaciente = string.Empty;
            return;
        }

        var hoje = _deHojeCompleto.FirstOrDefault(p => p.Paciente.Id == paciente.Id);
        ContextoDoPaciente = hoje is null
            ? string.Empty
            : $"tem horário hoje às {hoje.Hora}";
    }

    /// <summary>
    /// Os nove cartões. O catálogo é estático — a lista de papéis que a clínica emite não
    /// depende do banco —, então monta uma vez e só a disponibilidade muda depois.
    /// </summary>
    private void MontarCatalogo()
    {
        // ⚠️ SÓ as folhas que o acesso desta pessoa alcança (parcela 59). A porta da seção
        // (`VerDocumentos`) é uma decisão; esta é a outra, e sem ela a direção teria de
        // escolher entre a recepcionista lendo o relatório de evolução de todo mundo e a
        // recepcionista sem o recibo que ela emite dez vezes por dia.
        //
        // Cartão que aparece apagado dizendo "sem permissão" seria pior aqui do que em
        // qualquer outra tela: ele ANUNCIA que existe um relatório de evolução daquele
        // paciente, que é justamente o que não se quer contar a quem não pode lê-lo.
        foreach (var f in CentralDocumentosService.CatalogoPara(Acessos))
            Folhas.Add(new LinhaFolha
            {
                Folha = f,
                Rotulo = f.Rotulo,
                Descricao = f.Descricao,
                Grupo = f.Natureza switch
                {
                    NaturezaFolha.Clinico => "DO ATENDIMENTO",
                    NaturezaFolha.Financeiro => "DO DINHEIRO",
                    _ => "DA GESTÃO"
                },
                AcaoRotulo = f.Exigencia switch
                {
                    ExigenciaFolha.Periodo => "Gerar PDF",
                    ExigenciaFolha.LancamentoNoCaixa => "Ir para o Caixa",
                    // O clique NAVEGA, e o rótulo tem de dizer isso: "Emitir" prometeria
                    // emissão, e a leitura natural de uma tela que troca sem sair papel é
                    // que o termo saiu — com a pendência do dia continuando acesa.
                    ExigenciaFolha.TermoParaAssinar => "Colher assinatura…",
                    _ => "Emitir"
                }
            });

        AtualizarDisponibilidade();
    }

    /// <summary>
    /// Diz, em cada cartão, o que falta para gerar — em vez de deixar o botão aceso e só
    /// depois avisar. Descobrir o requisito errando é o que faz a pessoa desistir da tela.
    /// </summary>
    private void AtualizarDisponibilidade()
    {
        var temPaciente = Seletor.Selecionado is not null;

        foreach (var linha in Folhas)
        {
            // O acesso de EMITIR é da folha, não da tela: quem vê o relatório de evolução
            // não necessariamente assina uma receita.
            var podeEmitir = SessaoUsuario.Atual.Pode(linha.Folha.PermissaoEmitir);

            switch (linha.Folha.Exigencia)
            {
                case ExigenciaFolha.Paciente:
                case ExigenciaFolha.PacienteComProntuario:
                    linha.PodeGerar = temPaciente && podeEmitir;
                    linha.Pendencia = temPaciente
                        ? (podeEmitir
                            ? string.Empty
                            : $"Seu acesso permite ver, não emitir ({PerfisAcesso.Rotular(linha.Folha.PermissaoEmitir)}).")
                        : "Escolha o paciente no passo 1.";
                    break;

                case ExigenciaFolha.LancamentoNoCaixa:
                    // O recibo comprova dinheiro que JÁ entrou e fica apontando para o
                    // lançamento — por isso nasce no Caixa, e não aqui. O botão leva até lá
                    // quando o módulo está carregado neste executável.
                    var existe = NavegacaoSuite.Existe(ChavesSuite.Caixa);
                    linha.PodeGerar = existe;
                    linha.Pendencia = existe
                        ? "Nasce do lançamento no caixa, para não sair recibo em duplicidade."
                        : "O módulo Financeiro não está aberto neste aplicativo.";
                    break;

                case ExigenciaFolha.TermoParaAssinar:
                    // Exige paciente, como as clínicas — e NÃO exige procedimento marcado
                    // para hoje (parcela 66, 3ª rodada): o termo vale a partir da
                    // assinatura, e quem aparece para tirar dúvidas é justamente quem lê o
                    // texto com calma. Quem escolhe o modelo é a janela.
                    linha.PodeGerar = temPaciente && podeEmitir;
                    linha.Pendencia = !temPaciente
                        ? "Escolha o paciente no passo 1."
                        : podeEmitir
                            ? string.Empty
                            : $"Seu acesso permite ver, não colher ({PerfisAcesso.Rotular(linha.Folha.PermissaoEmitir)}).";
                    break;

                default: // Periodo
                    linha.PodeGerar = podeEmitir;
                    linha.Pendencia = podeEmitir
                        ? "Usa o período da aba \u201cO que já saiu\u201d."
                        : $"Seu acesso permite ver, não emitir ({PerfisAcesso.Rotular(linha.Folha.PermissaoEmitir)}).";
                    break;
            }
        }

        // A prévia do passo 3 fala da folha escolhida E do estado de agora: trocar de
        // paciente muda o "para quem" e pode acender ou apagar o botão.
        AtualizarPrevia();
    }

    /// <summary>
    /// O PASSO 2 escolhe; ele não emite (set/2026, mockup 5). O cartão deixou de disparar a
    /// emissão no primeiro clique e passa a marcar a folha, e é o passo 3 que emite.
    ///
    /// ⚠️ **Escolher NÃO exige que dê para emitir.** A folha cuja exigência não está
    /// cumprida é justamente a que precisa ser escolhida para o passo 3 poder EXPLICAR o
    /// que falta — recusar aqui devolveria o botão que não faz nada da parcela 41.
    /// </summary>
    [RelayCommand]
    private void EscolherFolha(LinhaFolha? linha)
    {
        if (linha is null) return;
        FolhaEscolhida = linha;
    }

    /// <summary>
    /// Monta o que o passo 3 AFIRMA. A composição mora na Application
    /// (<see cref="PreviaDaFolha"/>) e é pura: o que a tela afirma precisa morar onde o
    /// `dotnet test` alcança.
    /// </summary>
    private void AtualizarPrevia()
    {
        if (FolhaEscolhida is not { } escolhida)
        {
            Previa = null;
            return;
        }

        var paciente = Seletor.Selecionado;
        Previa = PreviaDaFolha.Montar(
            escolhida.Folha,
            escolhida.AcaoRotulo,
            escolhida.PodeGerar,
            escolhida.Pendencia,
            paciente?.Nome,
            paciente?.DocumentoFormatado,
            $"de {Inicio:dd/MM/yyyy} a {Fim:dd/MM/yyyy}");
    }

    [RelayCommand]
    public async Task CarregarAsync()
    {
        var geracao = ++_geracaoCarga;

        Carregando = true;
        NaoVerificado = false;
        try
        {
            Mensagem = null;
            MensagemEhErro = false;

            var inicio = DateOnly.FromDateTime(Inicio);
            var fim = DateOnly.FromDateTime(Fim);

            using var scope = _escopos.CreateScope();
            var central = scope.ServiceProvider.GetRequiredService<CentralDocumentosService>();

            // A lista do que JÁ SAIU passa pelo mesmo filtro dos cartões — senão a tela
            // esconderia o botão de emitir receita e mostraria "Receituário 2026/0012 —
            // Maria Silva" logo abaixo. O resumo conta sobre o resultado do filtro, e não
            // sobre a base: "12 folhas" acima de uma lista de quatro faria a pessoa
            // procurar as oito que faltam.
            // O recorte por paciente, quando ligado. Desligado passa nulo, que é o período
            // inteiro — a pergunta original da aba.
            var doPaciente = SoDoPaciente ? Seletor.Selecionado : null;

            var emitidas = await central.EmitidasAsync(
                inicio, fim, pacienteId: doPaciente?.Id, acessos: Acessos);

            // Chegou tarde: outra carga mais nova já foi pedida.
            if (geracao != _geracaoCarga) return;

            Emitidas.Clear();
            foreach (var e in emitidas) Emitidas.Add(Montar(e));

            // ⚠️ O resumo conta o RESULTADO, e por isso sai da lista que acabou de ser lida
            // — não de um segundo `ResumoAsync`, que refazia a MESMA consulta e, agora, a
            // refaria SEM o filtro: "30 folhas" acima de uma lista de doze faria a pessoa
            // procurar as dezoito que faltam. A frase mora na Application, onde o
            // `dotnet test` a alcança.
            Resumo = ResumoFolhas.Montar(emitidas, doPaciente?.Nome).Frase;
        }
        catch (Exception ex)
        {
            // Chegou tarde: outra carga mais nova já foi pedida.
            if (geracao != _geracaoCarga) return;

            NaoVerificado = true;
            Clinica.Application.Diagnostico.Registrar(
                "Recepção — central de documentos não pôde ser carregada", ex);
            Mensagem = $"Não foi possível ler os documentos: {ex.Message}";
            MensagemEhErro = true;
        }
        finally
        {
            // A carga superada não apaga o "Carregando" da que ainda está no ar.
            if (geracao == _geracaoCarga) Carregando = false;
        }
    }

    /// <summary>
    /// O link está no ar HOJE. O dia do vencimento conta inteiro — quem publicou com prazo
    /// até 09/10 espera que a receita abra no dia 09, não que ela caia na virada.
    /// </summary>
    private static bool NoAr(FolhaEmitida e)
        => e.PublicadoAte is { } ate && ate >= DateOnly.FromDateTime(DateTime.Today);

    /// <summary>
    /// Dá para cancelar/republicar esta linha? É o acesso de EMITIR a folha dela.
    ///
    /// Folha que não está no catálogo devolve NÃO — é a mesma resposta segura do serviço:
    /// papel cujo acesso ninguém declarou não vira botão aceso.
    /// </summary>
    private static bool PodeMexer(FolhaEmitida e)
        => CentralDocumentosService.Folha(e.Chave) is { } f
           && SessaoUsuario.Atual.Pode(f.PermissaoEmitir);


    /// <summary>
    /// O documento clínico da linha, para os atos do shell. Nulo quando a folha é
    /// FINANCEIRA (ou quando o tipo dela não está no catálogo, que é a mesma resposta
    /// segura do <see cref="PodeMexer"/>: papel cujo acesso ninguém declarou não vira
    /// botão aceso).
    /// </summary>
    private static DocumentoNaTela? DocumentoDe(FolhaEmitida e)
    {
        if (e.Natureza != NaturezaFolha.Clinico) return null;
        if (CentralDocumentosService.Folha(e.Chave)?.TipoClinico is not { } tipo) return null;

        return new DocumentoNaTela
        {
            DocumentoId = e.DocumentoId,
            PacienteId = e.PacienteId,
            Numero = e.Numero,
            Tipo = tipo,
            // O rótulo do CATÁLOGO ("Receituário"), que é o que esta tela mostra desde o
            // mockup aprovado — e não o do enum ("Receita").
            Rotulo = e.FolhaRotulo,
            Cancelado = e.Cancelado,
            Assinado = e.Assinado,
            LinkNoAr = NoAr(e),
            JaTeveLink = e.JaTeveLink
        };
    }

    private LinhaFolhaEmitida Montar(FolhaEmitida e) => new()
    {
        Documento = DocumentoDe(e),
        DocumentoId = e.DocumentoId,
        PacienteId = e.PacienteId,
        Natureza = e.Natureza,
        Numero = e.Numero,
        Folha = e.FolhaRotulo,
        Para = e.Paciente ?? "—",
        Data = e.Data.ToString("dd/MM/yyyy", Brasil),
        Detalhe = string.Join(" · ", new[]
            {
                e.Valor is { } v ? v.ToString("C2", Brasil) : null,
                e.Profissional,
                string.IsNullOrWhiteSpace(e.CriadoPor) ? null : $"por {e.CriadoPor}"
            }.Where(x => !string.IsNullOrWhiteSpace(x))),
        Cancelado = e.Cancelado,
        // Cancelar e republicar pedem o acesso de EMITIR daquela folha, não um bit geral
        // da tela: cancelar uma receita é ato de quem prescreve.
        PodeCancelar = !e.Cancelado && PodeMexer(e),
        AcessoParaMexer = CentralDocumentosService.Folha(e.Chave)?.PermissaoEmitir
                          ?? Permissao.GerenciarUsuarios,

        // Renovar só faz sentido para o que JÁ teve link e saiu do ar. Documento cancelado
        // não volta — receita cancelada baixável é a pior espécie de arquivo no ar.
        PodeRenovarLink = e.JaTeveLink && !e.Cancelado && PodeMexer(e) && !NoAr(e),

        // Tirar do ar é o inverso exato, e por isso vale INCLUSIVE para o cancelado: o
        // cancelamento já despublica, mas se aquela remoção falhou (S3 fora do ar na hora)
        // o arquivo continua acessível — e é justamente aí que o botão precisa existir.
        PodeTirarDoAr = NoAr(e) && PodeMexer(e),

        Link = !e.JaTeveLink ? string.Empty
            : NoAr(e) ? $"link no ar até {e.PublicadoAte:dd/MM/yyyy}"
            : "link fora do ar",
        // Cancelada aparece MARCADA, nunca sumindo: documento não se apaga neste sistema, e
        // esconder o cancelado faria a lista mentir sobre o que o paciente levou para casa.
        Situacao = e.Cancelado
            ? $"CANCELADA — {e.MotivoCancelamento}"
            : $"código {e.CodigoVerificacao}"
    };

    // ==================== Conferência pelo código (parcela 25) ====================

    /// <summary>
    /// Confere o papel pelo código impresso nele.
    ///
    /// Todo documento clínico sai com um <c>CodigoVerificacao</c> — e é ele que o sistema
    /// oferece no lugar da assinatura ICP-Brasil: "confira no sistema da clínica". Só que
    /// <c>PorCodigoAsync</c> existia desde a parcela 3 e **nenhuma tela o recebia**: quem
    /// chegava com o atestado na mão (a empresa, a escola, o próprio paciente) não tinha
    /// onde digitar o código. O que substitui a assinatura não pode ser o que ninguém
    /// consegue usar.
    ///
    /// Não é área pública: quem confere é a recepção, com o papel na frente.
    /// </summary>
    [RelayCommand]
    private async Task ConferirAsync()
    {
        Conferido = null;
        ConferidoCancelado = false;
        _conferidoDoc = null;

        var codigo = Codigo?.Trim();
        if (string.IsNullOrWhiteSpace(codigo))
        {
            Mensagem = "Digite o código impresso no rodapé do documento.";
            MensagemEhErro = true;
            return;
        }

        try
        {
            Mensagem = null;
            MensagemEhErro = false;

            using var scope = _escopos.CreateScope();
            var documentos = scope.ServiceProvider.GetRequiredService<DocumentoClinicoService>();

            var achado = await documentos.PorCodigoAsync(codigo);

            if (achado is null)
            {
                // "Não confere" é resposta, não erro de sistema — e é a resposta que a
                // conferência existe para dar.
                Conferido = $"Nenhum documento com o código \"{codigo}\". "
                            + "Confira a digitação; se estiver certa, o papel não saiu deste sistema.";
                ConferidoCancelado = true;
                return;
            }

            // O rótulo do CATÁLOGO, o mesmo que a frase da conferência escreve logo abaixo.
            _conferidoDoc = DocumentoNaTela.De(
                achado, CentralDocumentosService.RotularClinico(achado.Tipo));

            // Conferir pelo código MOSTRA de quem é o documento e de que tipo ele é —
            // acesso a dado de saúde por uma porta própria, e por isso na trilha.
            await scope.ServiceProvider.GetRequiredService<AcessoProntuarioService>()
                .RegistrarAsync(achado.PacienteId, SessaoUsuario.Atual.Operador,
                    OrigemAcessoProntuario.Documento);
            ConferidoCancelado = achado.Cancelado;

            var quem = achado.Paciente?.Nome ?? "(paciente removido)";
            var situacao = achado.Cancelado
                ? $"CANCELADO em {achado.CanceladoEm:dd/MM/yyyy} — {achado.MotivoCancelamento}"
                : "válido";

            Conferido = $"{CentralDocumentosService.RotularClinico(achado.Tipo)} {achado.Numero} · "
                        + $"{quem} · emitido em {achado.Data.ToString("dd/MM/yyyy", Brasil)} · {situacao}";
        }
        catch (Exception ex)
        {
            Clinica.Application.Diagnostico.Registrar(
                "Recepção — documento não pôde ser conferido pelo código", ex);
            Mensagem = $"Não foi possível conferir o código: {ex.Message}";
            MensagemEhErro = true;
        }
    }

    /// <summary>Segunda via do documento que acabou de ser conferido.</summary>
    [RelayCommand]
    private async Task ReimprimirConferidoAsync()
    {
        if (_conferidoDoc is not { } doc) return;
        await AplicarNoDocumentoAsync(await AcoesDoDocumento.ImprimirAsync(doc, _escopos));
    }

    // ==================== Gerar ====================

    [RelayCommand]
    private async Task GerarAsync(LinhaFolha? linha)
    {
        if (linha is null || !linha.PodeGerar) return;

        try
        {
            switch (linha.Folha.Exigencia)
            {
                case ExigenciaFolha.LancamentoNoCaixa:
                    NavegacaoSuite.Ir(ChavesSuite.Caixa);
                    return;

                case ExigenciaFolha.TermoParaAssinar:
                    await ColherTermoAsync(linha.Folha);
                    return;

                case ExigenciaFolha.Periodo:
                    await GerarFechamentoAsync();
                    return;

                case ExigenciaFolha.PacienteComProntuario:
                    await EmitirMontadaAsync(linha.Folha);
                    return;

                default:
                    // O orçamento exige paciente como as clínicas, mas é papel de dinheiro:
                    // tem itens com valor, validade e destinatário que pode não ser o
                    // paciente. Janela própria.
                    if (linha.Folha.TipoFinanceiro == TipoDocumentoFinanceiro.Orcamento)
                        await AbrirOrcamentoAsync();
                    else
                        await AbrirJanelaAsync(linha.Folha);
                    return;
            }
        }
        catch (Exception ex)
        {
            Clinica.Application.Diagnostico.Registrar(
                $"Recepção — folha '{linha.Rotulo}' não pôde ser gerada", ex);
            Mensagem = ex.Message;
            MensagemEhErro = true;
        }
    }

    /// <summary>
    /// As quatro folhas escritas (receituário, atestado, declaração, solicitação de exames)
    /// abrem a JANELA QUE JÁ EXISTE, com o tipo certo pré-selecionado. Não há formulário
    /// novo aqui: ela já resolve profissional que assina, modelos e a regra do CID.
    /// </summary>
    private async Task AbrirJanelaAsync(FolhaCatalogo folha)
    {
        SessaoUsuario.Atual.Exigir(
            folha.PermissaoEmitir, $"emitir {folha.Rotulo.ToLowerInvariant()}");

        if (Seletor.Selecionado is not { } paciente || folha.TipoClinico is not { } tipo) return;

        var vm = new DocumentoEdicaoViewModel(_escopos, paciente.Id, tipo);
        var janela = new Clinica.Desktop.Shell.Componentes.DocumentoWindow(vm)
        {
            Owner = JanelaDona.Atual()
        };

        // Mesmo fechando sem concluir, um documento pode ter sido emitido e só a impressão
        // ter falhado — a lista precisa refletir isso de qualquer jeito.
        var concluiu = janela.ShowDialog() == true;
        await CarregarAsync();

        if (concluiu) _snackbar.Sucesso($"{folha.Rotulo} emitido(a).");
    }

    /// <summary>
    /// O termo que o PACIENTE assina (parcela 66, 3ª rodada).
    ///
    /// Não passa pela janela genérica de documento: o texto e as declarações vêm de um
    /// MODELO, e é a cópia dele que o paciente lê e assina no tablet. O
    /// <c>ColetaDeTermo.Abrir</c> é o ponto ÚNICO das quatro portas — pergunta qual termo
    /// é, resolve os serviços e abre a coleta.
    ///
    /// ⚠️ Sem `modeloId`: aqui não há procedimento marcado dizendo qual termo é, e é
    /// justamente esse o caso que a cliente pediu — colher quando o paciente aparece, sem
    /// esperar o dia.
    /// </summary>
    private async Task ColherTermoAsync(FolhaCatalogo folha)
    {
        SessaoUsuario.Atual.Exigir(
            folha.PermissaoEmitir, $"colher {folha.Rotulo.ToLowerInvariant()}");

        if (Seletor.Selecionado is not { } paciente) return;

        // ⚠️ São DOIS termos assinados pelo paciente, e a diferença não é de estilo: o do
        // PROCEDIMENTO copia um modelo escrito pela clínica (por isso a janela pergunta
        // qual é), e o LGPD é montado das quatro finalidades — não há modelo a escolher.
        // Perguntar "qual termo?" para o LGPD ofereceria uma lista onde ele não está.
        var concluiu = folha.TipoClinico == TipoDocumentoClinico.Consentimento
            ? ColetaDeTermo.AbrirConsentimentoLgpd(_escopos, paciente.Id, paciente.Nome)
            : await ColetaDeTermo.AbrirAsync(_escopos, paciente.Id, paciente.Nome);

        // Recarrega de qualquer jeito: abrir a janela já emite o termo numerado.
        await CarregarAsync();

        if (concluiu) _snackbar.Sucesso($"{folha.Rotulo} assinado.");
    }

    /// <summary>
    /// Orçamento livre. Até esta parcela ele só nascia de um pacote vendido — quem quisesse
    /// orçar um plano de tratamento ou sessões avulsas não tinha caminho, embora o serviço
    /// já aceitasse linhas quaisquer desde a parcela 4.
    /// </summary>
    private async Task AbrirOrcamentoAsync()
    {
        // `EditarFinanceiro`, e não `VerFinanceiro`: emitir orçamento é escrever um papel
        // com valor. Quem só LÊ o caixa não propõe preço em nome da clínica.
        SessaoUsuario.Atual.Exigir(Permissao.EditarFinanceiro, "emitir orçamento");

        if (Seletor.Selecionado is not { } paciente) return;

        var vm = new OrcamentoViewModel(_escopos, paciente.Id, paciente.Nome);
        var janela = new Janelas.OrcamentoWindow(vm)
        {
            Owner = JanelaDona.Atual()
        };

        var concluiu = janela.ShowDialog() == true;
        await CarregarAsync();

        if (concluiu) _snackbar.Sucesso($"Orçamento {vm.NumeroEmitido} emitido.");
    }

    /// <summary>
    /// As montadas do prontuário (relatório de evolução e anamnese). Não se digitam: o
    /// sistema monta e imprime. O termo LGPD saiu daqui na parcela 89 — ele passou a ser
    /// assinado pelo paciente e vai pela coleta.
    /// </summary>
    private async Task EmitirMontadaAsync(FolhaCatalogo folha)
    {
        SessaoUsuario.Atual.Exigir(
            folha.PermissaoEmitir, $"emitir {folha.Rotulo.ToLowerInvariant()}");

        if (Seletor.Selecionado is not { } paciente || folha.TipoClinico is not { } tipo) return;

        DocumentoClinico emitido;
        using (var scope = _escopos.CreateScope())
        {
            var servico = scope.ServiceProvider.GetRequiredService<DocumentoClinicoService>();
            var operador = SessaoUsuario.Atual.Operador;

            emitido = tipo switch
            {
                TipoDocumentoClinico.RelatorioEvolucao =>
                    await servico.EmitirRelatorioEvolucaoAsync(paciente.Id, operador: operador),
                _ => await servico.EmitirAnamneseAsync(paciente.Id, operador: operador)
            };
        }

        await CarregarAsync();

        // Pelo DOCUMENTO recém-emitido, e não pela linha da lista: a folha pode não estar
        // na coleção ainda (ou ter caído fora do filtro), e aí a impressão simplesmente
        // não aconteceria — em silêncio, depois de o número ter sido gasto.
        var impressao = await AcoesDoDocumento.ImprimirAsync(
            DocumentoNaTela.De(emitido, folha.Rotulo), _escopos);
        Mensagem = impressao.Inline;
        MensagemEhErro = impressao.EhErro;
    }

    /// <summary>
    /// O fechamento do período. Até esta parcela ele só saía do app de faturamento, que a
    /// suíte não abre: a folha existia e ninguém daqui conseguia tirá-la.
    /// </summary>
    private async Task GerarFechamentoAsync()
    {
        byte[] pdf;
        using (var scope = _escopos.CreateScope())
        {
            var central = scope.ServiceProvider.GetRequiredService<CentralDocumentosService>();
            pdf = await central.GerarFechamentoPeriodoAsync(
                DateOnly.FromDateTime(Inicio), DateOnly.FromDateTime(Fim));
        }

        var erro = await ImpressaoPdf.SalvarEAbrirAsync(
            pdf, ImpressaoPdf.NomeSeguro($"Fechamento-{Inicio:yyyy-MM-dd}-a-{Fim:yyyy-MM-dd}.pdf"));

        Mensagem = erro;
        MensagemEhErro = erro is not null;
        if (erro is null) _snackbar.Sucesso("Fechamento do período gerado.");
    }

    // ==================== Segunda via e cancelamento ====================

    // ==================== Os atos da linha ====================
    //
    // Os SEIS atos do documento CLÍNICO moram em <see cref="AcoesDoDocumento"/>, no shell
    // (set/2026). Eram quatro cópias em quatro telas, e esta — a que a direção chama de "a
    // tela de Documentos" — tinha QUATRO deles: faltavam justamente ASSINAR e ENVIAR, que
    // são o que dá valor ao papel e o que o entrega. Emitir a receita aqui e não poder selá-la
    // era o caminho mais percorrido do balcão.
    //
    // A folha FINANCEIRA (recibo, orçamento) continua com o caminho dela: outro PDF, outro
    // serviço de cancelamento, e nada de assinatura ou link.

    /// <summary>
    /// Escreve o que o ato respondeu e relê a lista quando o documento mudou de estado.
    /// Roteiro, não regra: o que dizer e o que impede mora no ato.
    /// </summary>
    private async Task AplicarNoDocumentoAsync(ResultadoAcaoDocumento r)
    {
        if (!r.Silencioso)
        {
            Mensagem = r.Inline;
            MensagemEhErro = r.EhErro;
        }

        if (r.Mudou) await CarregarAsync();
    }

    /// <summary>
    /// Segunda via. O conteúdo foi gravado na EMISSÃO e não é remontado — a via que sai
    /// agora tem de ser idêntica à que o paciente levou, mesmo que o prontuário tenha
    /// andado no meio tempo.
    /// </summary>
    [RelayCommand]
    private async Task ReimprimirAsync(LinhaFolhaEmitida? linha)
    {
        if (linha is null) return;

        if (linha.Documento is { } doc)
        {
            await AplicarNoDocumentoAsync(await AcoesDoDocumento.ImprimirAsync(doc, _escopos));
            return;
        }

        // ===== A folha FINANCEIRA: recibo e orçamento =====
        try
        {
            byte[] pdf;
            using (var scope = _escopos.CreateScope())
            {
                var pdfs = scope.ServiceProvider.GetRequiredService<DocumentosFinanceirosPdfService>();
                var parametros = scope.ServiceProvider.GetRequiredService<ParametrosService>();
                pdf = await pdfs.GerarAsync(linha.DocumentoId, await parametros.ObterPrestadorAsync());
            }

            var erro = await ImpressaoPdf.SalvarEAbrirAsync(
                pdf, ImpressaoPdf.NomeSeguro($"{linha.Folha}-{linha.Numero.Replace('/', '-')}.pdf"));

            Mensagem = erro;
            MensagemEhErro = erro is not null;
        }
        catch (Exception ex)
        {
            Clinica.Application.Diagnostico.Registrar(
                "Recepção — documento financeiro não pôde ser reimpresso", ex);
            Mensagem = $"Não foi possível gerar o PDF de {linha.Numero}: {ex.Message}";
            MensagemEhErro = true;
        }
    }

    /// <summary>
    /// Assina o documento com o e-CPF — o ato que FALTAVA nesta tela.
    ///
    /// Sem assinatura o ARQUIVO não vale (art. 13 da Lei 14.063/2020 para o atestado; art.
    /// 14 para receita e pedido de exame): o que vale é a via impressa, assinada à caneta.
    /// Emitir aqui e ter de procurar outra tela para selar é o que a consolidação desta
    /// parcela acaba.
    /// </summary>
    [RelayCommand]
    private async Task AssinarAsync(LinhaFolhaEmitida? linha)
    {
        if (linha?.Documento is not { } doc) return;
        await AplicarNoDocumentoAsync(
            await AcoesDoDocumento.AssinarAsync(doc, _escopos, _snackbar));
    }

    /// <summary>
    /// Entrega o ARQUIVO assinado ao paciente pelo WhatsApp — o segundo ato que faltava.
    /// A assinatura vive nos bytes: quem sai só com o papel leva um documento sem ela.
    /// </summary>
    [RelayCommand]
    private async Task EnviarAsync(LinhaFolhaEmitida? linha)
    {
        if (linha?.Documento is not { } doc) return;
        await AplicarNoDocumentoAsync(await AcoesDoDocumento.EnviarAsync(doc, _escopos));
    }

    /// <summary>
    /// Põe de volta no ar o link de um documento assinado cujo prazo venceu (parcela 53).
    ///
    /// <b>Reusa o MESMO token</b>, e é isso que dá sentido ao botão: o QR está selado dentro
    /// do PDF assinado que o paciente levou, e um token novo obrigaria a emitir outro
    /// documento.
    /// </summary>
    [RelayCommand]
    private async Task RenovarLinkAsync(LinhaFolhaEmitida? linha)
    {
        if (linha?.Documento is not { } doc) return;
        await AplicarNoDocumentoAsync(
            await AcoesDoDocumento.RenovarLinkAsync(doc, _escopos, _snackbar));
    }

    /// <summary>
    /// Tira o link do ar AGORA (parcela 63). Não apaga registro nenhum: os bytes assinados
    /// continuam no banco pelos 20 anos da Lei 13.787/2018 — o que sai do ar é a PUBLICAÇÃO.
    /// </summary>
    [RelayCommand]
    private async Task TirarDoArAsync(LinhaFolhaEmitida? linha)
    {
        if (linha?.Documento is not { } doc) return;
        await AplicarNoDocumentoAsync(
            await AcoesDoDocumento.TirarDoArAsync(doc, _escopos, _dialogo, _snackbar));
    }

    /// <summary>
    /// Cancela com motivo. Não apaga — é a regra do documento neste sistema, e vale para os
    /// dois lados: o número continua queimado e a linha continua na lista, marcada.
    /// </summary>
    [RelayCommand]
    private async Task CancelarAsync(LinhaFolhaEmitida? linha)
    {
        if (linha is null || linha.Cancelado) return;

        if (linha.Documento is { } doc)
        {
            await AplicarNoDocumentoAsync(
                await AcoesDoDocumento.CancelarAsync(doc, _escopos, _dialogo, _snackbar));
            return;
        }

        // ===== A folha FINANCEIRA: outro serviço, a mesma regra =====
        try
        {
            SessaoUsuario.Atual.Exigir(
                linha.AcessoParaMexer, $"cancelar {linha.Folha.ToLowerInvariant()}");

            var motivo = _dialogo.PerguntarTexto(
                "Cancelar documento",
                $"Por que {linha.Numero} está sendo cancelado? O documento não é apagado — " +
                "fica registrado como cancelado, com este motivo.");

            if (string.IsNullOrWhiteSpace(motivo)) return;

            using var scope = _escopos.CreateScope();
            await scope.ServiceProvider.GetRequiredService<DocumentoFinanceiroService>()
                .CancelarAsync(linha.DocumentoId, motivo, SessaoUsuario.Atual.Operador);

            _snackbar.Sucesso($"{linha.Numero} cancelado.");
            await CarregarAsync();
        }
        catch (Exception ex)
        {
            Clinica.Application.Diagnostico.Registrar(
                "Recepção — documento financeiro não pôde ser cancelado", ex);
            Mensagem = ex.Message;
            MensagemEhErro = true;
        }
    }
}
