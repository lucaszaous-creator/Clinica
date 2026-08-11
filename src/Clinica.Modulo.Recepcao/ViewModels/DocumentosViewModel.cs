using System.Collections.ObjectModel;
using System.Globalization;
using Clinica.Application.Servicos;
using Clinica.Desktop.Controls;
using Clinica.Desktop.Shell;
using Clinica.Desktop.Shell.Componentes;
using Clinica.Desktop.Shell.Modulos;
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

    /// <summary>Rótulo do botão: "Emitir", "Gerar PDF" ou "Ir para o Caixa".</summary>
    public required string AcaoRotulo { get; init; }
}

/// <summary>Uma folha já emitida, como a lista a mostra.</summary>
public sealed class LinhaFolhaEmitida
{
    public required int DocumentoId { get; init; }
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
    /// O link venceu e dá para colocá-lo de volta no ar (parcela 53). Só aparece em
    /// documento que JÁ teve link: republicar reusa o mesmo token, e o QR impresso que o
    /// paciente guardou volta a funcionar.
    /// </summary>
    public bool PodeRenovarLink { get; init; }

    /// <summary>
    /// O que dizer sobre o link, ou vazio quando o documento nunca teve um. Frase inteira
    /// e não um selo: "no ar até 09/10" e "link vencido" levam a ações diferentes, e um
    /// ícone obrigaria a recepção a decorar o que ele quer dizer.
    /// </summary>
    public string Link { get; init; } = string.Empty;
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

    [ObservableProperty] private DateTime _inicio = DateTime.Today.AddDays(-30);
    [ObservableProperty] private DateTime _fim = DateTime.Today;

    [ObservableProperty] private string _resumo = string.Empty;
    [ObservableProperty] private bool _semPapel;
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

    private int? _conferidoId;

    /// <summary>Só há segunda via quando o código achou mesmo um documento.</summary>
    public bool TemConferido => _conferidoId is not null;

    partial void OnConferidoChanged(string? value) => OnPropertyChanged(nameof(TemConferido));

    /// <summary>
    /// Os acessos de quem está logado. Ficam numa propriedade porque a tela os consulta em
    /// três lugares — o catálogo, a lista do que já saiu e os botões de cada linha —, e
    /// consultar a sessão em cada um deles seria a mesma regra escrita três vezes.
    /// </summary>
    private static Permissao Acessos => SessaoUsuario.Atual.Efetivas;

    public DocumentosViewModel(
        IServiceScopeFactory escopos, ISnackbarService snackbar, IDialogoService dialogo)
    {
        _escopos = escopos;
        _snackbar = snackbar;
        _dialogo = dialogo;

        Seletor = new SeletorPacienteViewModel(escopos);
        Seletor.SelecaoMudou += _ => AtualizarDisponibilidade();

        MontarCatalogo();
        _ = CarregarAsync();
    }

    partial void OnInicioChanged(DateTime value) => _ = CarregarAsync();
    partial void OnFimChanged(DateTime value) => _ = CarregarAsync();

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
                        : "Escolha o paciente ao lado.";
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

                default: // Periodo
                    linha.PodeGerar = podeEmitir;
                    linha.Pendencia = podeEmitir
                        ? "Usa o período escolhido abaixo."
                        : $"Seu acesso permite ver, não emitir ({PerfisAcesso.Rotular(linha.Folha.PermissaoEmitir)}).";
                    break;
            }
        }
    }

    [RelayCommand]
    public async Task CarregarAsync()
    {
        if (Carregando) return;
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
            var emitidas = await central.EmitidasAsync(inicio, fim, acessos: Acessos);
            var resumo = await central.ResumoAsync(inicio, fim, acessos: Acessos);

            Emitidas.Clear();
            foreach (var e in emitidas) Emitidas.Add(Montar(e));

            SemPapel = resumo.Vazio;
            Resumo = resumo.Vazio
                ? "Nenhum papel emitido no período."
                : resumo.Canceladas > 0
                    ? $"{resumo.Emitidas} folha(s) emitida(s), {resumo.Canceladas} cancelada(s)."
                    : $"{resumo.Emitidas} folha(s) emitida(s).";
        }
        catch (Exception ex)
        {
            NaoVerificado = true;
            Clinica.Application.Diagnostico.Registrar(
                "Recepção — central de documentos não pôde ser carregada", ex);
            Mensagem = $"Não foi possível ler os documentos: {ex.Message}";
            MensagemEhErro = true;
        }
        finally
        {
            Carregando = false;
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


    private LinhaFolhaEmitida Montar(FolhaEmitida e) => new()
    {
        DocumentoId = e.DocumentoId,
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
        _conferidoId = null;

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

            _conferidoId = achado.Id;
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
        if (_conferidoId is not { } id) return;
        await ImprimirClinicoAsync(id, $"Documento-{id}.pdf", $"#{id}");
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
            Owner = System.Windows.Application.Current?.MainWindow
        };

        // Mesmo fechando sem concluir, um documento pode ter sido emitido e só a impressão
        // ter falhado — a lista precisa refletir isso de qualquer jeito.
        var concluiu = janela.ShowDialog() == true;
        await CarregarAsync();

        if (concluiu) _snackbar.Sucesso($"{folha.Rotulo} emitido(a).");
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
            Owner = System.Windows.Application.Current?.MainWindow
        };

        var concluiu = janela.ShowDialog() == true;
        await CarregarAsync();

        if (concluiu) _snackbar.Sucesso($"Orçamento {vm.NumeroEmitido} emitido.");
    }

    /// <summary>
    /// As três montadas do prontuário. Não se digitam: o sistema monta e imprime.
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
                TipoDocumentoClinico.Consentimento =>
                    await servico.EmitirTermoConsentimentoAsync(paciente.Id, operador: operador),
                _ => await servico.EmitirAnamneseAsync(paciente.Id, operador: operador)
            };
        }

        await CarregarAsync();
        await ImprimirClinicoAsync(
            emitido.Id, $"{folha.Rotulo}-{emitido.Numero.Replace('/', '-')}.pdf", emitido.Numero);
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

    /// <summary>
    /// Segunda via. O conteúdo foi gravado na EMISSÃO e não é remontado — a via que sai
    /// agora tem de ser idêntica à que o paciente levou, mesmo que o prontuário tenha
    /// andado no meio tempo.
    /// </summary>
    [RelayCommand]
    private async Task ReimprimirAsync(LinhaFolhaEmitida? linha)
    {
        if (linha is null) return;

        if (linha.Natureza == NaturezaFolha.Clinico)
        {
            await ImprimirClinicoAsync(
                linha.DocumentoId,
                $"{linha.Folha}-{linha.Numero.Replace('/', '-')}.pdf",
                linha.Numero);
            return;
        }

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

    private async Task ImprimirClinicoAsync(int documentoId, string nomeArquivo, string numero)
    {
        try
        {
            byte[] pdf;
            using (var scope = _escopos.CreateScope())
            {
                var pdfs = scope.ServiceProvider.GetRequiredService<DocumentosClinicosPdfService>();
                var parametros = scope.ServiceProvider.GetRequiredService<ParametrosService>();
                pdf = await pdfs.GerarAsync(documentoId, await parametros.ObterPrestadorAsync());
            }

            var erro = await ImpressaoPdf.SalvarEAbrirAsync(pdf, ImpressaoPdf.NomeSeguro(nomeArquivo));

            Mensagem = erro;
            MensagemEhErro = erro is not null;
        }
        catch (Exception ex)
        {
            Clinica.Application.Diagnostico.Registrar(
                "Recepção — documento clínico não pôde ser impresso", ex);
            // O documento ESTÁ emitido: dizer só "falhou" faria a pessoa emitir de novo e
            // ficar com dois papéis numerados para o mesmo ato.
            Mensagem = $"O documento {numero} está emitido, mas o PDF não pôde ser gerado: {ex.Message}";
            MensagemEhErro = true;
        }
    }

    /// <summary>
    /// Cancela com motivo. Não apaga — é a regra do documento neste sistema, e vale para os
    /// dois lados: o número continua queimado e a linha continua na lista, marcada.
    /// </summary>
    /// <summary>
    /// Põe de volta no ar o link de um documento assinado cujo prazo venceu (parcela 53).
    ///
    /// <b>Reusa o MESMO token</b>, e é isso que dá sentido ao botão: o QR está selado
    /// dentro do PDF assinado que o paciente levou, e um token novo obrigaria a emitir
    /// outro documento. Republicado, o papel que está na bolsa dele volta a funcionar.
    ///
    /// A guarda diz por que recusou em vez de voltar calada — a lição da parcela 41: botão
    /// que não faz nada é lido como sistema quebrado.
    /// </summary>
    [RelayCommand]
    private async Task RenovarLinkAsync(LinhaFolhaEmitida? linha)
    {
        if (linha is null) return;

        try
        {
            SessaoUsuario.Atual.Exigir(
                linha.AcessoParaMexer, $"republicar o link de {linha.Folha.ToLowerInvariant()}");

            if (linha.Cancelado)
            {
                Mensagem = $"{linha.Numero} está cancelado e não volta ao ar.";
                MensagemEhErro = true;
                return;
            }

            using var scope = _escopos.CreateScope();
            var resultado = await scope.ServiceProvider
                .GetRequiredService<PublicacaoDocumentoService>()
                .RenovarAsync(linha.DocumentoId);

            if (!resultado.Publicou)
            {
                // NaoSeAplica devolve os três campos nulos: é o caso de a publicação estar
                // desligada, e "não aconteceu nada" precisa de frase própria — o Erro nulo
                // deixaria a tela muda depois do clique.
                Mensagem = resultado.Erro
                    ?? "A publicação está desligada: cadastre o domínio da clínica em "
                       + "Configurações → Publicação.";
                MensagemEhErro = true;
                return;
            }

            _snackbar.Sucesso(
                $"{linha.Numero} de volta ao ar até {resultado.Ate:dd/MM/yyyy}. "
                + "O QR já impresso volta a funcionar.");
            await CarregarAsync();
        }
        catch (Exception ex)
        {
            Clinica.Application.Diagnostico.Registrar(
                "Recepção — link do documento não pôde ser republicado", ex);
            Mensagem = ex.Message;
            MensagemEhErro = true;
        }
    }

    [RelayCommand]
    private async Task CancelarAsync(LinhaFolhaEmitida? linha)
    {
        if (linha is null || linha.Cancelado) return;

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
            var operador = SessaoUsuario.Atual.Operador;

            if (linha.Natureza == NaturezaFolha.Clinico)
                await scope.ServiceProvider.GetRequiredService<DocumentoClinicoService>()
                    .CancelarAsync(linha.DocumentoId, motivo!, operador);
            else
                await scope.ServiceProvider.GetRequiredService<DocumentoFinanceiroService>()
                    .CancelarAsync(linha.DocumentoId, motivo!, operador);

            _snackbar.Sucesso($"{linha.Numero} cancelado.");
            await CarregarAsync();
        }
        catch (Exception ex)
        {
            Clinica.Application.Diagnostico.Registrar(
                "Recepção — documento não pôde ser cancelado", ex);
            Mensagem = ex.Message;
            MensagemEhErro = true;
        }
    }
}
