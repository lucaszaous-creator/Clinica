using Clinica.Domain.Entities;
using System.Windows.Input;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using Clinica.Application.Modelos;
using Clinica.Application.Servicos;
using Clinica.Domain;
using Clinica.Domain.Regras;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;

namespace Clinica.Desktop.ViewModels;

/// <summary>Tela inicial: 2º códigos e consultas pendentes com semáforo, filtros por convênio e urgência.</summary>
public partial class DashboardViewModel : ObservableObject, IAtalhosDeTela
{
    /// <summary>
    /// Metade VISÍVEL da permissão (parcela 45): o botão explica, o <c>Exigir</c> do
    /// comando impede. As duas são obrigatórias — só desabilitar é enfeite, porque atalho
    /// de teclado e pesquisa global disparam o mesmo comando por outro caminho.
    /// </summary>
    public bool PodeBaixar => SessaoUsuario.Atual.Pode(Permissao.BaixarGuia);

    /// <summary>Decidir não faturar é a permissão mais delicada do faturamento: é a única que tira uma guia do painel sem ela ter sido faturada.</summary>
    public bool PodeMarcarNaoConformidade => SessaoUsuario.Atual.Pode(Permissao.MarcarNaoConformidade);
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly Controls.IDialogoService _dialogo;
    private readonly List<PendenciaCodigo> _todos = new();

    private static readonly CultureInfo PtBr = new("pt-BR");

    /// <summary>Nome da clínica (assinatura da mensagem de WhatsApp).</summary>
    private string? _nomeClinica;

    public ObservableCollection<PendenciaCodigo> Codigos { get; } = new();
    public ObservableCollection<PendenciaConsulta> Consultas { get; } = new();
    public ObservableCollection<PendenciaRecursoGlosa> Recursos { get; } = new();
    public ObservableCollection<PendenciaCarteirinha> Carteirinhas { get; } = new();

    public IReadOnlyList<object> OpcoesConvenio { get; }

    /// <summary>
    /// Tipo do código, modalidade e especialidade para os combos (parcela 61) — a resposta
    /// a "12 pendências de QUÊ?". Mesma construção da consulta de guias (parcela 45): a
    /// primeira opção é uma STRING, e o <c>EnumDescricao</c> a devolve como veio — é o que
    /// permite "sem filtro" e "filtrado por" viverem no mesmo combo sem um valor de enum
    /// inventado para significar "nenhum".
    /// </summary>
    public IReadOnlyList<object> OpcoesTipo { get; }
    public IReadOnlyList<object> OpcoesModalidade { get; }
    public IReadOnlyList<object> OpcoesEspecialidade { get; }

    private const string Todos = "Todos";
    private const string Todas = "Todas";

    [ObservableProperty] private object _filtroConvenio = Todos;
    [ObservableProperty] private object _filtroTipo = Todos;
    [ObservableProperty] private object _filtroModalidade = Todas;
    [ObservableProperty] private object _filtroEspecialidade = Todas;
    [ObservableProperty] private int _total;

    // ---- Chips (parcela 61): o recorte de um clique, com a contagem à vista ----
    //
    // Ordem (1º/2º) e situação (atrasada/no prazo) são as duas perguntas que o faturista
    // alterna o dia inteiro, e as duas têm DUAS respostas cada — chip, não combo: a
    // distribuição aparece ANTES do clique ("12 pendências: 8 do 1º, 4 do 2º"), que é
    // exatamente o que faltava no número seco. Cada dupla é exclusiva (marcar uma
    // desmarca a irmã); desmarcar as duas é "todas".
    [ObservableProperty] private bool _chipPrimeiros;
    [ObservableProperty] private bool _chipSegundos;
    [ObservableProperty] private bool _chipAtrasadas;
    [ObservableProperty] private bool _chipNoPrazo;

    /// <summary>Contagens dos chips — sempre sobre o TOTAL, nunca sobre o recorte.</summary>
    public int TotalPrimeiros => _todos.Count(p => p.Ordem == OrdemCodigo.Primeiro);
    public int TotalSegundos => _todos.Count(p => p.Ordem == OrdemCodigo.Segundo);
    public int TotalAtrasadas => _todos.Count(p => p.Urgencia == NivelUrgencia.Vermelho);
    public int TotalNoPrazo => _todos.Count(p => p.Urgencia == NivelUrgencia.Amarelo);

    /// <summary>
    /// A lista filtrada DIZ que está filtrada (regra da parcela 45): "12 pendências" e
    /// "12 de 30 — 2º código · acupuntura" respondem perguntas diferentes, e quem volta à
    /// tela depois do café não lembra o que deixou marcado.
    /// </summary>
    [ObservableProperty] private string _resumoRecorte = string.Empty;

    /// <summary>Há recorte ativo — o "Limpar filtros" só aparece quando tem o que limpar.</summary>
    [ObservableProperty] private bool _temFiltro;

    /// <summary>O vazio distingue "não há pendência" de "nenhuma bate com o filtro".</summary>
    [ObservableProperty] private string _tituloVazio = "Nenhuma pendência";
    [ObservableProperty] private string _descricaoVazio =
        "Tudo em dia por aqui. Use Atualizar (F5) para recarregar.";

    /// <summary>A rodada de pendências venceu (mostra o banner do fechamento de ciclo).</summary>
    [ObservableProperty] private bool _rodadaVencida;

    /// <summary>
    /// Não deu para consultar a rodada (banco fora do ar, por exemplo). É um estado
    /// PRÓPRIO, e não "não venceu": a promessa do produto é não deixar guia esquecida,
    /// então silêncio por falha tem de aparecer como falha, nunca como tudo certo.
    /// </summary>
    [ObservableProperty] private bool _rodadaIndeterminada;

    /// <summary>Texto do banner da rodada (quanto está vencida / o que precisa decidir).</summary>
    [ObservableProperty] private string _rodadaBanner = string.Empty;

    /// <summary>Texto do aviso de rodada não verificada.</summary>
    [ObservableProperty] private string _rodadaAvisoFalha = string.Empty;

    /// <summary>Total de códigos/guias pendentes de baixa (para a faixa de alerta do topo).</summary>
    public int TotalCodigos => _todos.Count;
    public bool TemPendencias => _todos.Count > 0;

    // KPIs do painel
    public int CodigosUrgentes => _todos.Count(p => p.Urgencia == NivelUrgencia.Vermelho);
    public int ConsultasARenovar => Consultas.Count;

    /// <summary>Glosas com prazo de recurso correndo e carteirinhas a vencer (seções aparecem só quando há itens).</summary>
    public bool TemRecursos => Recursos.Count > 0;
    public bool TemCarteirinhas => Carteirinhas.Count > 0;

    public event Action<int>? PendenciasAtualizadas;
    public event Action<int>? AbrirBaixaSolicitado;
    public event Action<int>? FichaSolicitada;
    public event Action? AbrirGlosasSolicitado;

    public DashboardViewModel(IServiceScopeFactory scopeFactory, Controls.IDialogoService dialogo)
    {
        _scopeFactory = scopeFactory;
        _dialogo = dialogo;
        var ops = new List<object> { Todos };
        ops.AddRange(Enum.GetValues<Convenio>().Cast<object>());
        OpcoesConvenio = ops;

        var tipos = new List<object> { Todos };
        tipos.AddRange(Enum.GetValues<TipoCodigo>().Cast<object>());
        OpcoesTipo = tipos;

        var modalidades = new List<object> { Todas };
        modalidades.AddRange(Enum.GetValues<ModalidadeAtendimento>().Cast<object>());
        OpcoesModalidade = modalidades;

        var especialidades = new List<object> { Todas };
        especialidades.AddRange(Enum.GetValues<Especialidade>().Cast<object>());
        OpcoesEspecialidade = especialidades;
    }

    public async Task CarregarAsync()
    {
        using var scope = _scopeFactory.CreateScope();
        var pendencias = scope.ServiceProvider.GetRequiredService<PendenciaService>();
        var hoje = DateOnly.FromDateTime(DateTime.Today);

        _todos.Clear();
        _todos.AddRange(await pendencias.CodigosPendentesAsync(hoje));

        Consultas.Clear();
        foreach (var c in await pendencias.ConsultasAVencerAsync(hoje))
            Consultas.Add(c);

        Recursos.Clear();
        foreach (var r in await pendencias.GlosasARecorrerAsync(hoje))
            Recursos.Add(r);

        Carteirinhas.Clear();
        foreach (var c in await pendencias.CarteirinhasAVencerAsync(hoje))
            Carteirinhas.Add(c);

        // Situação da rodada de pendências ("rodar as pendências") para o banner do fechamento de ciclo.
        try
        {
            var rodada = scope.ServiceProvider.GetRequiredService<RodadaPendenciasService>();
            await rodada.GarantirInicioAsync(hoje); // ancora a carência do backlog no 1º uso
            var status = await rodada.ObterStatusAsync(hoje);
            RodadaVencida = status.ExigeDecisao;
            RodadaIndeterminada = false;
            RodadaAvisoFalha = string.Empty;
            RodadaBanner = status.ExigeDecisao
                ? $"{status.GuiasParaDecisao} guia(s) estão pendentes há mais de {status.PrazoDias} dias " +
                  "sem resolução. Rode agora: cada uma exige baixa ou não conformidade."
                : string.Empty;
        }
        catch (Exception ex)
        {
            // A falha não derruba o painel, mas TAMBÉM não pode virar "não venceu": antes
            // isto zerava RodadaVencida, e o painel afirmava que estava tudo em dia quando
            // na verdade não tinha conseguido perguntar.
            Configuracao.LogErros.Registrar("Painel — situação da rodada de pendências", ex);
            RodadaVencida = false;
            RodadaIndeterminada = true;
            RodadaAvisoFalha =
                "Não foi possível verificar a rodada de pendências agora — pode haver guia vencida " +
                "sem aparecer aqui. Atualize o painel; se continuar, avise o suporte.";
        }

        if (_nomeClinica is null)
        {
            try
            {
                var d = await scope.ServiceProvider.GetRequiredService<ParametrosService>().ObterPrestadorAsync();
                _nomeClinica = string.IsNullOrWhiteSpace(d.NomeFantasia) ? d.RazaoSocial : d.NomeFantasia;
            }
            catch (Exception ex)
            {
                // Sem nome a mensagem sai sem assinatura; não impede o painel.
                Configuracao.LogErros.Registrar("Painel — nome da clínica não pôde ser lido", ex);
            }
        }

        AplicarFiltro();
    }

    private void AplicarFiltro()
    {
        IEnumerable<PendenciaCodigo> filtrados = _todos;
        if (FiltroConvenio is Convenio cv)
            filtrados = filtrados.Where(p => p.Convenio == cv);
        if (FiltroTipo is TipoCodigo tipo)
            filtrados = filtrados.Where(p => p.Tipo == tipo);
        if (FiltroModalidade is ModalidadeAtendimento mod)
            filtrados = filtrados.Where(p => p.Modalidade == mod);
        if (FiltroEspecialidade is Especialidade esp)
            filtrados = filtrados.Where(p => p.Especialidade == esp);
        if (ChipPrimeiros) filtrados = filtrados.Where(p => p.Ordem == OrdemCodigo.Primeiro);
        if (ChipSegundos) filtrados = filtrados.Where(p => p.Ordem == OrdemCodigo.Segundo);
        if (ChipAtrasadas) filtrados = filtrados.Where(p => p.Urgencia == NivelUrgencia.Vermelho);
        if (ChipNoPrazo) filtrados = filtrados.Where(p => p.Urgencia == NivelUrgencia.Amarelo);

        Codigos.Clear();
        foreach (var c in filtrados) Codigos.Add(c);

        AtualizarResumoDoRecorte();

        // O critério é UM (`PendenciaService.TotalParaBadge`): esta linha era uma segunda
        // conta escrita à mão, com este comentário jurando usar "o mesmo critério" do
        // método que ninguém chamava — e a primeira mudança em qualquer um dos lados
        // divergiria em silêncio, com o teste fixando o lado morto.
        Total = PendenciaService.TotalParaBadge(
            _todos.Count, Consultas.Count, Recursos.Select(r => r.Urgencia));
        OnPropertyChanged(nameof(TotalCodigos));
        OnPropertyChanged(nameof(TemPendencias));
        OnPropertyChanged(nameof(CodigosUrgentes));
        OnPropertyChanged(nameof(ConsultasARenovar));
        OnPropertyChanged(nameof(TemRecursos));
        OnPropertyChanged(nameof(TemCarteirinhas));
        OnPropertyChanged(nameof(TotalPrimeiros));
        OnPropertyChanged(nameof(TotalSegundos));
        OnPropertyChanged(nameof(TotalAtrasadas));
        OnPropertyChanged(nameof(TotalNoPrazo));
        PendenciasAtualizadas?.Invoke(Total);
    }

    /// <summary>Escreve o recorte por extenso e ajusta o estado vazio para dizê-lo também.</summary>
    private void AtualizarResumoDoRecorte()
    {
        var partes = new List<string>();
        if (ChipPrimeiros) partes.Add("1º código");
        if (ChipSegundos) partes.Add("2º código");
        if (ChipAtrasadas) partes.Add("atrasadas");
        if (ChipNoPrazo) partes.Add("no prazo");
        if (FiltroTipo is TipoCodigo tipo) partes.Add(RotulosEnum.De(tipo));
        if (FiltroModalidade is ModalidadeAtendimento mod) partes.Add(ModalidadeInfo.NomeExibicao(mod));
        if (FiltroEspecialidade is Especialidade esp) partes.Add(EspecialidadeInfo.NomeExibicao(esp));
        if (FiltroConvenio is Convenio cv) partes.Add(CatalogoConvenios.Nome(cv));

        TemFiltro = partes.Count > 0;

        ResumoRecorte = !TemFiltro
            ? (_todos.Count == 1 ? "1 pendência" : $"{_todos.Count} pendências")
            : $"{Codigos.Count} de {_todos.Count} pendência(s) — {string.Join(" · ", partes)}";

        // "Nenhuma pendência" e "nenhuma bate com o filtro" são respostas DIFERENTES:
        // um filtro esquecido que respondesse "tudo em dia" faria a clínica dar o dia
        // por resolvido com o painel inteiro pendente (a lição da lista de espera, 25).
        var filtroEscondendo = TemFiltro && _todos.Count > 0;
        TituloVazio = filtroEscondendo ? "Nenhuma pendência bate com o filtro" : "Nenhuma pendência";
        DescricaoVazio = filtroEscondendo
            ? $"Há {_todos.Count} pendência(s) fora deste recorte. Ajuste os filtros ou use Limpar."
            : "Tudo em dia por aqui. Use Atualizar (F5) para recarregar.";
    }

    /// <summary>Desfaz todo o recorte de um clique — combos e chips.</summary>
    [RelayCommand]
    private void LimparFiltros()
    {
        // Direto nos campos + UM AplicarFiltro no fim: pelos setters, cada mudança
        // refiltraria a lista inteira — sete varreduras para produzir a mesma tela.
        _filtroConvenio = Todos;
        _filtroTipo = Todos;
        _filtroModalidade = Todas;
        _filtroEspecialidade = Todas;
        _chipPrimeiros = _chipSegundos = _chipAtrasadas = _chipNoPrazo = false;
        OnPropertyChanged(nameof(FiltroConvenio));
        OnPropertyChanged(nameof(FiltroTipo));
        OnPropertyChanged(nameof(FiltroModalidade));
        OnPropertyChanged(nameof(FiltroEspecialidade));
        OnPropertyChanged(nameof(ChipPrimeiros));
        OnPropertyChanged(nameof(ChipSegundos));
        OnPropertyChanged(nameof(ChipAtrasadas));
        OnPropertyChanged(nameof(ChipNoPrazo));
        AplicarFiltro();
    }

    partial void OnFiltroConvenioChanged(object value) => AplicarFiltro();
    partial void OnFiltroTipoChanged(object value) => AplicarFiltro();
    partial void OnFiltroModalidadeChanged(object value) => AplicarFiltro();
    partial void OnFiltroEspecialidadeChanged(object value) => AplicarFiltro();

    // Cada dupla de chips é EXCLUSIVA: marcar um desmarca o irmão (uma guia não é 1º e
    // 2º ao mesmo tempo), e desmarcar os dois é "todas".
    partial void OnChipPrimeirosChanged(bool value)
    {
        if (value && ChipSegundos) ChipSegundos = false;
        else AplicarFiltro();
    }

    partial void OnChipSegundosChanged(bool value)
    {
        if (value && ChipPrimeiros) ChipPrimeiros = false;
        else AplicarFiltro();
    }

    partial void OnChipAtrasadasChanged(bool value)
    {
        if (value && ChipNoPrazo) ChipNoPrazo = false;
        else AplicarFiltro();
    }

    partial void OnChipNoPrazoChanged(bool value)
    {
        if (value && ChipAtrasadas) ChipAtrasadas = false;
        else AplicarFiltro();
    }

    [RelayCommand]
    private void DarBaixa(PendenciaCodigo? codigo)
    {
        if (codigo is not null)
            AbrirBaixaSolicitado?.Invoke(codigo.CodigoId);
    }

    /// <summary>
    /// Anota por que a guia ainda não foi baixada (portal fora do ar, aguardando o
    /// paciente etc.). A observação fica na pendência para consulta futura.
    /// </summary>
    [RelayCommand]
    private async Task Anotar(PendenciaCodigo? codigo)
    {
        if (codigo is null) return;

        // O mesmo bit da tela gerencial de pendências: anotar é escrever na guia.
        SessaoUsuario.Atual.Exigir(Permissao.VerFaturamento, "anotar a pendência");

        var janela = new Alertas.ObservacaoPendenciaWindow(codigo)
        {
            Owner = System.Windows.Application.Current.MainWindow
        };
        if (janela.ShowDialog() != true) return;

        try
        {
            using var scope = _scopeFactory.CreateScope();
            var faturamento = scope.ServiceProvider.GetRequiredService<FaturamentoService>();
            await faturamento.RegistrarObservacaoPendenciaAsync(codigo.CodigoId, janela.Observacao, SessaoUsuario.Atual.Operador);
        }
        catch (Exception ex)
        {
            _dialogo.Aviso("Observação da pendência", ex.Message);
        }

        await CarregarAsync();
    }

    /// <summary>
    /// Marca a guia como NÃO CONFORMIDADE por decisão do usuário, sem esperar o prazo da rodada
    /// vencer. Pede confirmação e justificativa; a guia sai das pendências ativas e vai para a aba NC
    /// (reabre se o paciente voltar ou pela aba NC). Atende o "marcar NC antes dos 10 dias".
    /// </summary>
    [RelayCommand]
    private async Task NaoConformidade(PendenciaCodigo? codigo)
    {
        if (codigo is null) return;

        SessaoUsuario.Atual.Exigir(Permissao.MarcarNaoConformidade, "marcar a guia como não conformidade");

        if (!_dialogo.Confirmar("Não conformidade",
                $"Marcar a guia de {codigo.PacienteNome} como não conformidade? " +
                "Ela sai das pendências ativas do painel e vai para a aba NC."))
            return;

        var janela = new Alertas.NaoConformidadeWindow(codigo)
        {
            Owner = System.Windows.Application.Current.MainWindow
        };
        if (janela.ShowDialog() != true) return;

        try
        {
            using var scope = _scopeFactory.CreateScope();
            var rodada = scope.ServiceProvider.GetRequiredService<RodadaPendenciasService>();
            await rodada.MarcarNaoConformidadeAsync(codigo.CodigoId, janela.Justificativa, SessaoUsuario.Atual.Operador);
        }
        catch (Exception ex)
        {
            _dialogo.Aviso("Não conformidade", ex.Message);
        }

        await CarregarAsync();
    }

    /// <summary>
    /// Abre o WhatsApp (wa.me) com uma mensagem pronta para o paciente quando a secretária
    /// não consegue contato por ligação para obter a 1ª/2ª guia. Um clique leva direto à conversa.
    /// </summary>
    [RelayCommand]
    private void Whatsapp(PendenciaCodigo? pendencia)
    {
        if (pendencia is null) return;

        var primeiroNome = PrimeiroNome(pendencia.PacienteNome);
        var ordinal = pendencia.Ordem == OrdemCodigo.Segundo ? "2ª" : "1ª";
        var texto = $"Olá, {primeiroNome}! Tentamos falar com você por telefone e não conseguimos. " +
                    $"Precisamos de um retorno rápido para concluir a autorização da {ordinal} guia do seu convênio, " +
                    $"referente ao atendimento de {pendencia.DataPrevista.ToString("dd/MM", PtBr)}. " +
                    "Quando puder, é só responder por aqui, por favor.";
        if (pendencia.FormaObtencao == FormaObtencao.App)
            texto += " Se o seu plano gera o código pelo aplicativo, pode nos enviar o QR Code por aqui mesmo.";

        AbrirWhatsapp(pendencia.PacienteTelefone, pendencia.PacienteNome, texto);
    }

    /// <summary>WhatsApp para o paciente cuja consulta está a vencer (agendar a renovação).</summary>
    [RelayCommand]
    private void WhatsappConsulta(PendenciaConsulta? item)
    {
        if (item is null) return;

        var primeiroNome = PrimeiroNome(item.PacienteNome);
        var prazo = item.DiasParaVencer <= 0 ? "está vencendo" : $"vence em {item.DiasParaVencer} dia(s)";
        var texto = $"Olá, {primeiroNome}! Aqui é da clínica. A validade da sua consulta {prazo} " +
                    $"(até {item.DataVencimento.ToString("dd/MM", PtBr)}). " +
                    "Para não interromper o seu tratamento, vamos agendar o seu retorno? Responda por aqui, por favor.";

        AbrirWhatsapp(item.PacienteTelefone, item.PacienteNome, texto);
    }

    /// <summary>WhatsApp para o paciente com carteirinha vencida/a vencer (pedir a atualização).</summary>
    [RelayCommand]
    private void WhatsappCarteirinha(PendenciaCarteirinha? item)
    {
        if (item is null) return;

        var primeiroNome = PrimeiroNome(item.PacienteNome);
        var situacao = item.DiasParaVencer < 0
            ? $"está vencida desde {item.Validade.ToString("dd/MM/yyyy", PtBr)}"
            : $"vence em {item.DiasParaVencer} dia(s) ({item.Validade.ToString("dd/MM/yyyy", PtBr)})";
        var texto = $"Olá, {primeiroNome}! Aqui é da clínica. A sua carteirinha do convênio {situacao}. " +
                    "Para o convênio não recusar as suas guias, poderia nos enviar por aqui uma foto da carteirinha " +
                    "atualizada? Obrigado!";

        AbrirWhatsapp(item.PacienteTelefone, item.PacienteNome, texto);
    }

    /// <summary>Primeiro nome do paciente (para saudação).</summary>
    private static string PrimeiroNome(string nome)
        => nome.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? nome;

    /// <summary>
    /// Normaliza o telefone (+DDI), assina com o nome da clínica e abre o wa.me na conversa.
    /// Telefone ausente/inválido é avisado com orientação para editar em Pacientes.
    /// </summary>
    private void AbrirWhatsapp(string? telefone, string pacienteNome, string corpo)
    {
        var fone = Telefone.Normalizar(telefone);
        if (fone.Length is < 10 or > 13)
        {
            _dialogo.Aviso("WhatsApp",
                $"{pacienteNome}: telefone ausente ou inválido no cadastro (edite em Pacientes).");
            return;
        }
        if (fone.Length is 10 or 11)
            fone = "55" + fone; // wa.me exige DDI

        var texto = corpo + (string.IsNullOrWhiteSpace(_nomeClinica) ? string.Empty : $" — {_nomeClinica}");

        try
        {
            Process.Start(new ProcessStartInfo($"https://wa.me/{fone}?text={Uri.EscapeDataString(texto)}")
            {
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            _dialogo.Aviso("WhatsApp", $"Não foi possível abrir o WhatsApp: {ex.Message}");
        }
    }

    /// <summary>Baixa em lote das linhas selecionadas (Ctrl/Shift + clique na tabela).</summary>
    [RelayCommand]
    private async Task DarBaixaEmLote(System.Collections.IList? selecionados)
    {
        SessaoUsuario.Atual.Exigir(Permissao.BaixarGuia, "dar baixa nas guias");

        var itens = selecionados?.OfType<PendenciaCodigo>().ToList() ?? new List<PendenciaCodigo>();
        if (itens.Count == 0)
        {
            _dialogo.Aviso("Baixa em lote",
                "Selecione uma ou mais linhas da tabela (Ctrl+clique ou Shift+clique) antes de usar a baixa em lote.");
            return;
        }

        var janela = new Alertas.BaixaLoteWindow(itens)
        {
            Owner = System.Windows.Application.Current.MainWindow
        };
        if (janela.ShowDialog() != true) return;

        var feitas = 0;
        var falhas = new List<string>();
        using (var scope = _scopeFactory.CreateScope())
        {
            var faturamento = scope.ServiceProvider.GetRequiredService<FaturamentoService>();
            foreach (var linha in janela.Linhas.Where(l => !string.IsNullOrWhiteSpace(l.NumeroGuia)))
            {
                // Falha POR LINHA, como a rodada de pendências: o serviço baixa guia a
                // guia, e uma exceção no meio (o outro posto baixou a mesma guia; o
                // formato recusou) abortava as seguintes EM SILÊNCIO — as primeiras já
                // baixadas, a lista intacta na tela, e a secretária certa de que o lote
                // inteiro saiu.
                try
                {
                    await faturamento.DarBaixaAsync(linha.CodigoId, janela.DataBaixa,
                        linha.NumeroGuia!.Trim(), SessaoUsuario.Atual.Operador, "baixa em lote");
                    feitas++;
                }
                catch (Exception ex)
                {
                    Configuracao.LogErros.Registrar($"Baixa em lote — guia do código {linha.CodigoId}", ex);
                    falhas.Add($"{linha.Descricao}: {ex.Message}");
                }
            }
        }

        if (falhas.Count > 0)
            _dialogo.Aviso("Baixa em lote",
                $"{feitas} guia(s) baixada(s); {falhas.Count} NÃO:\n\n" + string.Join("\n", falhas));
        else if (feitas == 0)
            _dialogo.Aviso("Baixa em lote", "Nenhuma linha tinha número de guia — nada foi baixado.");

        await CarregarAsync();
    }

    /// <summary>Renova a consulta direto do card "Consultas a renovar" do painel.</summary>
    [RelayCommand]
    private async Task Renovar(PendenciaConsulta? item)
    {
        if (item is null) return;
        if (!_dialogo.Confirmar("Renovar consulta",
                $"Gerar/renovar a consulta de {item.PacienteNome} para hoje?")) return;

        try
        {
            using var scope = _scopeFactory.CreateScope();
            var service = scope.ServiceProvider.GetRequiredService<ConsultaService>();
            await service.RenovarAsync(item.PacienteId, DateOnly.FromDateTime(DateTime.Today));
        }
        catch (Exception ex)
        {
            _dialogo.Aviso("Renovar consulta", ex.Message);
        }

        await CarregarAsync();
    }

    /// <summary>Abre a ficha do paciente (usado no card de carteirinhas a vencer).</summary>
    [RelayCommand]
    private void AbrirFicha(PendenciaCarteirinha? item)
    {
        if (item is not null)
            FichaSolicitada?.Invoke(item.PacienteId);
    }

    /// <summary>Vai para o Controle de glosas (usado no card de prazos de recurso).</summary>
    [RelayCommand]
    private void AbrirGlosas() => AbrirGlosasSolicitado?.Invoke();

    /// <summary>
    /// Roda as pendências (fechamento de ciclo): abre a janela de decisão para dar baixa ou registrar
    /// não conformidade em cada guia pendente e conclui a rodada. Disparado pelo botão do banner.
    /// </summary>
    [RelayCommand]
    private async Task RodarPendencias()
    {
        try
        {
            var concluida = await Alertas.RodadaPendenciasFluxo.ExecutarAsync(
                _scopeFactory, System.Windows.Application.Current.MainWindow, bloqueante: false);
            if (concluida)
                await CarregarAsync();
        }
        catch (Exception ex)
        {
            _dialogo.Aviso("Rodar pendências", ex.Message);
        }
    }

    [RelayCommand]
    private Task Atualizar() => CarregarAsync();

    // Atalhos globais do shell (IAtalhosDeTela)
    public ICommand? AtalhoAtualizar => AtualizarCommand;
}
