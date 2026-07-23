using System.Windows.Input;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using Clinica.Application.Modelos;
using Clinica.Application.Servicos;
using Clinica.Domain;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;

namespace Clinica.Desktop.ViewModels;

/// <summary>Tela inicial: 2º códigos e consultas pendentes com semáforo, filtros por convênio e urgência.</summary>
public partial class DashboardViewModel : ObservableObject, IAtalhosDeTela
{
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
    public IReadOnlyList<object> OpcoesUrgencia { get; } =
        new object[] { "Todos", NivelUrgencia.Vermelho, NivelUrgencia.Amarelo, NivelUrgencia.Verde, NivelUrgencia.Cinza };

    [ObservableProperty] private object _filtroConvenio = "Todos";
    [ObservableProperty] private object _filtroUrgencia = "Todos";
    [ObservableProperty] private int _total;

    /// <summary>A rodada de pendências venceu (mostra o banner do fechamento de ciclo).</summary>
    [ObservableProperty] private bool _rodadaVencida;

    /// <summary>Texto do banner da rodada (quanto está vencida / o que precisa decidir).</summary>
    [ObservableProperty] private string _rodadaBanner = string.Empty;

    /// <summary>Guias pendentes de baixa ATIVAS (exclui as não conformidades em cinza).</summary>
    public int TotalCodigos => _todos.Count(p => !p.EhNaoConformidade);
    public bool TemPendencias => _todos.Any(p => !p.EhNaoConformidade);

    // KPIs do painel
    public int CodigosUrgentes => _todos.Count(p => p.Urgencia == NivelUrgencia.Vermelho);
    public int ConsultasARenovar => Consultas.Count;

    /// <summary>Não conformidades paradas (em cinza), aguardando o paciente voltar ou serem resolvidas.</summary>
    public int NaoConformidadesTotal => _todos.Count(p => p.EhNaoConformidade);

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
        var ops = new List<object> { "Todos" };
        ops.AddRange(Enum.GetValues<Convenio>().Cast<object>());
        OpcoesConvenio = ops;
    }

    public async Task CarregarAsync()
    {
        using var scope = _scopeFactory.CreateScope();
        var pendencias = scope.ServiceProvider.GetRequiredService<PendenciaService>();
        var hoje = DateOnly.FromDateTime(DateTime.Today);

        _todos.Clear();
        _todos.AddRange(await pendencias.CodigosPendentesAsync(hoje));
        // Não conformidades entram no fim da lista, em cinza (paradas até voltar/serem resolvidas).
        _todos.AddRange(await pendencias.NaoConformidadesComoPendenciaAsync(hoje));

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
            await rodada.GarantirAncoraAsync(hoje); // ancora o ciclo no 1º uso
            var status = await rodada.ObterStatusAsync(hoje);
            RodadaVencida = status.Vencida;
            RodadaBanner = status.Vencida
                ? (status.DiasEmAtraso > 0
                    ? $"A rodada de pendências venceu há {status.DiasEmAtraso} dia(s). "
                    : "A rodada de pendências vence hoje. ") +
                  (status.TemGuiasParaDecisao
                    ? $"Rode agora: {status.GuiasParaDecisao} guia(s) aguardam decisão (baixa ou não conformidade)."
                    : "Rode agora para fechar o ciclo.")
                : string.Empty;
        }
        catch
        {
            // O banner da rodada nunca deve derrubar o carregamento do painel.
            RodadaVencida = false;
        }

        if (_nomeClinica is null)
        {
            try
            {
                var d = await scope.ServiceProvider.GetRequiredService<ParametrosService>().ObterPrestadorAsync();
                _nomeClinica = string.IsNullOrWhiteSpace(d.NomeFantasia) ? d.RazaoSocial : d.NomeFantasia;
            }
            catch { /* sem nome a mensagem sai sem assinatura; não impede o painel */ }
        }

        AplicarFiltro();
    }

    private void AplicarFiltro()
    {
        IEnumerable<PendenciaCodigo> filtrados = _todos;
        if (FiltroConvenio is Convenio cv)
            filtrados = filtrados.Where(p => p.Convenio == cv);
        if (FiltroUrgencia is NivelUrgencia u)
            filtrados = filtrados.Where(p => p.Urgencia == u);

        Codigos.Clear();
        foreach (var c in filtrados) Codigos.Add(c);

        // Mesmo critério do badge (PendenciaService.TotalPendenciasAsync): recursos contam
        // quando o prazo está apertado (amarelo/vermelho). Não conformidades (cinza) ficam de fora.
        Total = TotalCodigos + Consultas.Count + Recursos.Count(r => r.Urgencia != NivelUrgencia.Verde);
        OnPropertyChanged(nameof(TotalCodigos));
        OnPropertyChanged(nameof(TemPendencias));
        OnPropertyChanged(nameof(CodigosUrgentes));
        OnPropertyChanged(nameof(NaoConformidadesTotal));
        OnPropertyChanged(nameof(ConsultasARenovar));
        OnPropertyChanged(nameof(TemRecursos));
        OnPropertyChanged(nameof(TemCarteirinhas));
        PendenciasAtualizadas?.Invoke(Total);
    }

    partial void OnFiltroConvenioChanged(object value) => AplicarFiltro();
    partial void OnFiltroUrgenciaChanged(object value) => AplicarFiltro();

    [RelayCommand]
    private void DarBaixa(PendenciaCodigo? codigo)
    {
        if (codigo is not null)
            AbrirBaixaSolicitado?.Invoke(codigo.CodigoId);
    }

    /// <summary>
    /// Reabre uma não conformidade (linha cinza): a guia volta a ser pendência ativa (vermelha) e
    /// pode ser baixada. Usado quando aparece uma solução sem o paciente ter voltado.
    /// </summary>
    [RelayCommand]
    private async Task ReabrirNaoConformidade(PendenciaCodigo? codigo)
    {
        if (codigo is null) return;
        if (!_dialogo.Confirmar("Reabrir não conformidade",
                $"Reabrir a guia de {codigo.PacienteNome}? Ela volta a ser pendência ativa.")) return;

        try
        {
            using var scope = _scopeFactory.CreateScope();
            var rodada = scope.ServiceProvider.GetRequiredService<RodadaPendenciasService>();
            await rodada.ReabrirNaoConformidadeAsync(codigo.CodigoId, Environment.UserName);
        }
        catch (Exception ex)
        {
            _dialogo.Aviso("Reabrir não conformidade", ex.Message);
        }

        await CarregarAsync();
    }

    /// <summary>
    /// Anota por que a guia ainda não foi baixada (portal fora do ar, aguardando o
    /// paciente etc.). A observação fica na pendência para consulta futura.
    /// </summary>
    [RelayCommand]
    private async Task Anotar(PendenciaCodigo? codigo)
    {
        if (codigo is null) return;

        var janela = new Alertas.ObservacaoPendenciaWindow(codigo)
        {
            Owner = System.Windows.Application.Current.MainWindow
        };
        if (janela.ShowDialog() != true) return;

        try
        {
            using var scope = _scopeFactory.CreateScope();
            var faturamento = scope.ServiceProvider.GetRequiredService<FaturamentoService>();
            await faturamento.RegistrarObservacaoPendenciaAsync(codigo.CodigoId, janela.Observacao, Environment.UserName);
        }
        catch (Exception ex)
        {
            _dialogo.Aviso("Observação da pendência", ex.Message);
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
        using (var scope = _scopeFactory.CreateScope())
        {
            var faturamento = scope.ServiceProvider.GetRequiredService<FaturamentoService>();
            foreach (var linha in janela.Linhas.Where(l => !string.IsNullOrWhiteSpace(l.NumeroGuia)))
            {
                await faturamento.DarBaixaAsync(linha.CodigoId, janela.DataBaixa,
                    linha.NumeroGuia!.Trim(), Environment.UserName, "baixa em lote");
                feitas++;
            }
        }

        if (feitas == 0)
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
