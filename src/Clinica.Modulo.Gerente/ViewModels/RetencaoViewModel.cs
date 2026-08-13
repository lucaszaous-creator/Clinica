using System.Collections.ObjectModel;
using Clinica.Application.Servicos;
using Clinica.Desktop.Controls;
using Clinica.Desktop.Shell;
using Clinica.Desktop.Shell.Componentes;
using Clinica.Domain.Entities;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;

namespace Clinica.Gerente.ViewModels;

/// <summary>Um paciente que parou de vir, na lista.</summary>
public sealed class LinhaSumido
{
    public required int PacienteId { get; init; }
    public required string Paciente { get; init; }
    public required string Detalhe { get; init; }
    public required string Faixa { get; init; }
    public string? Telefone { get; init; }
    public required bool EraFrequente { get; init; }
    public required bool TemPacoteAberto { get; init; }
    public required bool JaChamado { get; init; }

    /// <summary>
    /// Consentimento de comunicação e marketing VIGENTE (LGPD).
    ///
    /// Chamar de volta quem parou de vir é RECALL — comunicação ativa da clínica, não
    /// aviso sobre uma sessão que o paciente marcou. A rodada de campanha bloqueia quem
    /// não consentiu desde a parcela 5; esta tela mandava a mesma mensagem sem perguntar,
    /// e duas regras para o mesmo ato divergem sempre pelo lado errado.
    /// </summary>
    public required bool TemConsentimento { get; init; }

    public bool TemTelefone => !string.IsNullOrWhiteSpace(Telefone);

    /// <summary>Só há WhatsApp a abrir com telefone E consentimento — a metade VISÍVEL da regra.</summary>
    public bool PodeChamar => TemTelefone && TemConsentimento;

    /// <summary>
    /// Por que não dá para chamar, escrito na linha. A pessoa continua na lista de
    /// propósito (a regra do projeto: quem não consentiu não some, aparece contado) — o
    /// que ela precisa é da instrução para resolver, que é colher o consentimento no
    /// balcão da próxima vez que o paciente aparecer.
    /// </summary>
    public string ImpedimentoChamada =>
        !TemTelefone ? "sem telefone no cadastro"
        : !TemConsentimento ? "sem consentimento de comunicação (LGPD) — colha no balcão"
        : string.Empty;

    public bool TemImpedimento => ImpedimentoChamada.Length > 0;
}

/// <summary>
/// Quem parou de vir (parcela 32).
///
/// A campanha de recall dispara mensagens por regra de tempo; os indicadores medem
/// no-show e ocupação. Nenhuma das duas responde a pergunta que a direção faz quando o
/// faturamento cai: **quem sumiu?** — com nome, telefone e há quanto tempo.
///
/// A diferença para o recall é o que cada um serve: lá é uma rodada automática; aqui é a
/// LISTA, para olhar caso a caso e decidir quem vale um telefonema de verdade. Numa
/// clínica de acupuntura, o paciente de tratamento longo que some vale dez recalls
/// disparados no vazio.
/// </summary>
public sealed partial class RetencaoViewModel : ObservableObject
{
    private readonly IServiceScopeFactory _escopos;

    public ObservableCollection<LinhaSumido> Sumidos { get; } = [];

    /// <summary>Tudo o que o banco devolveu; <see cref="Sumidos"/> é o recorte do filtro.</summary>
    private readonly List<LinhaSumido> _todos = [];

    public IReadOnlyList<int> Janelas { get; } = [60, 90, 180, 365];

    // ---- Filtro (em memória, sobre o que já foi lido — a janela de dias continua
    // sendo a CARGA: ela muda o que o serviço devolve; estes três só estreitam). A tela
    // existe para decidir quem vale um telefonema, e a decisão começa pelos dois
    // destaques: quem era de tratamento e quem tem pacote pago em aberto.
    [ObservableProperty] private string _filtroNomeSumido = string.Empty;
    [ObservableProperty] private bool _soFrequentes;
    [ObservableProperty] private bool _soComPacoteAberto;

    partial void OnFiltroNomeSumidoChanged(string value) => Refiltrar();
    partial void OnSoFrequentesChanged(bool value) => Refiltrar();
    partial void OnSoComPacoteAbertoChanged(bool value) => Refiltrar();

    public bool FiltroAtivo =>
        SoFrequentes || SoComPacoteAberto || !string.IsNullOrWhiteSpace(FiltroNomeSumido);

    [RelayCommand]
    private void LimparFiltro()
    {
        SoFrequentes = false;
        SoComPacoteAberto = false;
        FiltroNomeSumido = string.Empty;
    }

    /// <summary>O estado vazio muda de frase quando há filtro — vazio filtrado não é "ninguém sumiu".</summary>
    [ObservableProperty] private string _vazioDescricao =
        "Quem já tem horário marcado à frente não entra: ele voltou, só ainda não veio.";

    [ObservableProperty] private int _janelaDias = RetencaoPacienteService.DiasParaConsiderarSumido;
    [ObservableProperty] private bool _carregando;
    [ObservableProperty] private string _resumo = string.Empty;
    [ObservableProperty] private string? _mensagem;
    [ObservableProperty] private bool _mensagemEhErro;

    /// <summary>Falha na leitura: lista vazia por erro, não por não haver ninguém sumido.</summary>
    [ObservableProperty] private bool _naoVerificado;

    public RetencaoViewModel(IServiceScopeFactory escopos)
    {
        _escopos = escopos;
        _ = CarregarAsync();
    }

    partial void OnJanelaDiasChanged(int value) => _ = CarregarAsync();

    /// <summary>
    /// Número da carga mais recente pedida — descarte de resposta fora de ordem (parcela 50).
    /// Trocar a janela de dias dispara outra leitura; a resposta velha chegando por último
    /// deixaria a lista de sumidos de uma janela que não é a escolhida no combo.
    /// </summary>
    private int _geracaoCarga;

    [RelayCommand]
    public async Task CarregarAsync()
    {
        var geracao = ++_geracaoCarga;

        try
        {
            Carregando = true;
            NaoVerificado = false;
            Mensagem = null;
            MensagemEhErro = false;

            using var scope = _escopos.CreateScope();
            var servico = scope.ServiceProvider.GetRequiredService<RetencaoPacienteService>();

            var hoje = DateOnly.FromDateTime(DateTime.Today);
            var lista = await servico.SumidosAsync(hoje, JanelaDias);

            // Quem pode receber a mensagem. EM LOTE, pela mesma razão que a campanha usa
            // o lote: uma consulta por paciente transformaria a tela em dezenas de idas a
            // um banco remoto. Lista vazia não vai ao banco — perguntar por ninguém.
            var comConsentimento = lista.Count == 0
                ? []
                : (await scope.ServiceProvider
                    .GetRequiredService<Clinica.Application.Abstracoes.IClinicaRepositorio>()
                    .PacientesComConsentimentoVigenteAsync(
                        FinalidadeConsentimento.ComunicacaoEMarketing,
                        lista.Select(s => s.PacienteId).ToList())).ToHashSet();

            // Chegou tarde: outra carga mais nova já foi pedida.
            if (geracao != _geracaoCarga) return;

            _todos.Clear();
            foreach (var s in lista)
                _todos.Add(new LinhaSumido
                {
                    PacienteId = s.PacienteId,
                    Paciente = s.Nome,
                    Telefone = s.Telefone,
                    Faixa = s.Faixa,
                    EraFrequente = s.EraFrequente,
                    TemPacoteAberto = s.TemPacoteAberto,
                    JaChamado = s.JaChamado,
                    TemConsentimento = comConsentimento.Contains(s.PacienteId),
                    Detalhe = $"última sessão em {s.UltimaSessao:dd/MM/yyyy} · "
                              + $"{s.DiasSemVir} dias · {s.TotalSessoes} sessão(ões) no total"
                              + (s.TemPacoteAberto ? " · TEM PACOTE EM ABERTO" : string.Empty)
                              + (s.JaChamado ? " · já recebeu recall" : string.Empty)
                });

            Refiltrar();
        }
        catch (Exception ex)
        {
            // Chegou tarde: outra carga mais nova já foi pedida.
            if (geracao != _geracaoCarga) return;

            Clinica.Application.Diagnostico.Registrar(
                "Gerente — lista de pacientes sumidos não pôde ser lida", ex);
            // A base também esvazia: filtro sobre resto de carga antiga mentiria o "de M".
            _todos.Clear();
            Sumidos.Clear();
            NaoVerificado = true;
            Resumo = $"Não foi possível ler a lista: {ex.Message}";
        }
        finally
        {
            // A carga superada não apaga o "Carregando" da que ainda está no ar.
            if (geracao == _geracaoCarga) Carregando = false;
            // A lista mudou (encheu ou esvaziou pela falha): o botão de exportar precisa
            // reavaliar se tem o que exportar.
            OnPropertyChanged(nameof(TemParaExportar));
        }
    }

    /// <summary>
    /// Aplica o filtro sobre o que já foi lido — em memória, sem ida ao banco (o padrão
    /// da tela de Consultas da Recepção).
    /// </summary>
    private void Refiltrar()
    {
        Sumidos.Clear();
        foreach (var s in _todos.Where(s =>
                     (!SoFrequentes || s.EraFrequente)
                     && (!SoComPacoteAberto || s.TemPacoteAberto)
                     && Busca.Casa(s.Paciente, FiltroNomeSumido)))
            Sumidos.Add(s);

        OnPropertyChanged(nameof(FiltroAtivo));
        // O CSV sai do que está na tela — o botão acompanha o recorte.
        OnPropertyChanged(nameof(TemParaExportar));

        // O resumo DIZ que está filtrado e MANTÉM a janela dita: "8 de 31 sem vir há
        // mais de 90 dias" — sem os dias, o número perde a régua que o define.
        var frequentes = _todos.Count(s => s.EraFrequente);
        var comPacote = _todos.Count(s => s.TemPacoteAberto);

        // Quem não dá para chamar aparece CONTADO, e a lista continua mostrando a pessoa.
        // É a regra que a rodada de campanha já seguia: quem some da lista some da
        // cabeça, e "12 pacientes" que na verdade eram 30 faz a clínica concluir que
        // quase ninguém está sumindo. Contado sobre o TOTAL, como os outros dois.
        var semConsentimento = _todos.Count(s => !s.TemConsentimento);
        var semTelefone = _todos.Count(s => s.TemConsentimento && !s.TemTelefone);

        var impedidos = new List<string>();
        if (semConsentimento > 0)
            impedidos.Add($"{semConsentimento} sem consentimento de comunicação (LGPD)");
        if (semTelefone > 0) impedidos.Add($"{semTelefone} sem telefone");

        Resumo = _todos.Count == 0
            ? $"Ninguém sem vir há mais de {JanelaDias} dias."
            : FiltroAtivo
                ? $"{Sumidos.Count} de {_todos.Count} paciente(s) sem vir há mais de "
                  + $"{JanelaDias} dias no filtro."
                : $"{Sumidos.Count} paciente(s) sem vir há mais de {JanelaDias} dias · "
                  + $"{frequentes} eram de tratamento"
                  + (comPacote > 0 ? $" · {comPacote} com pacote pago em aberto" : string.Empty)
                  + (impedidos.Count > 0
                      ? $" · não dá para chamar: {string.Join(" e ", impedidos)}."
                      : ".");

        VazioDescricao = FiltroAtivo
            ? "Ninguém bate com o filtro — limpe-o para ver a lista inteira da janela."
            : "Quem já tem horário marcado à frente não entra: ele voltou, só ainda não veio.";
    }

    /// <summary>
    /// Abre o WhatsApp para chamar o paciente de volta.
    ///
    /// A mensagem é CONVITE, não cobrança: quem sumiu costuma ter parado por motivo
    /// simples (melhorou, viajou, apertou o mês), e um texto que soa como cobrança fecha
    /// a porta que a ligação veio abrir. E não leva dado clínico — o telefone pode não
    /// ser só do paciente.
    /// </summary>
    [RelayCommand]
    private void Chamar(LinhaSumido? linha)
    {
        if (linha is null) return;

        try
        {
            // A segunda barreira. O item da sidebar já exige GerenciarCampanhas, mas isso
            // é UMA barreira: atalho de teclado e navegação por chave passam direto por
            // ela. Chamar de volta é o mesmo ato da rodada de recall, e a autorização
            // tinha de ser a mesma nos dois lugares.
            SessaoUsuario.Atual.Exigir(Permissao.GerenciarCampanhas, "chamar o paciente de volta");
        }
        catch (Exception ex)
        {
            Mensagem = ex.Message;
            MensagemEhErro = true;
            return;
        }

        // ⚠️ A LGPD, e não um detalhe de tela. Esta mensagem é RECALL — comunicação ativa
        // da clínica —, e o projeto já decidiu na parcela 5 que ela só sai com
        // `ComunicacaoEMarketing` vigente: `CampanhaService.GerarRecallAsync` conta quem
        // ficou de fora e não manda. Aqui a MESMA mensagem saía sem ninguém perguntar,
        // porque a lista vinha de outro serviço — o mesmo ato com duas regras, e a de
        // baixo era a que não tinha regra nenhuma.
        //
        // A guarda DIZ por que não dá (a lição da parcela 41): o botão já nasce apagado
        // por `PodeChamar`, e quem chegar por atalho ouve o motivo em vez de ver o clique
        // cair no vazio.
        if (!linha.TemConsentimento)
        {
            Mensagem = $"{linha.Paciente} não consentiu receber comunicação da clínica (LGPD). "
                       + "Confirmar a sessão que ele mesmo marcou é transacional e continua "
                       + "podendo; chamar de volta é marketing, e o consentimento se colhe no "
                       + "balcão, na ficha do paciente.";
            MensagemEhErro = true;
            return;
        }

        if (linha.Telefone is not { } telefone)
        {
            Mensagem = $"{linha.Paciente} está sem telefone no cadastro.";
            MensagemEhErro = true;
            return;
        }

        var primeiro = linha.Paciente.Split(' ').FirstOrDefault() ?? linha.Paciente;
        var erro = Whatsapp.Abrir(
            telefone, linha.Paciente,
            $"Olá, {primeiro}! Faz um tempo que a gente não se vê por aqui. "
            + "Se quiser retomar as sessões, é só responder esta mensagem que a gente "
            + "encaixa um horário.");

        if (erro is null) return;
        Mensagem = erro;
        MensagemEhErro = true;
    }

    /// <summary>Há linha para exportar — é o que acende o botão de CSV.</summary>
    public bool TemParaExportar => Sumidos.Count > 0;

    /// <summary>A lista em CSV, para a direção trabalhar fora da tela.</summary>
    [RelayCommand]
    private async Task ExportarAsync()
    {
        // Lista vazia: o botão já nasce apagado (`TemParaExportar`), mas quem chegar
        // aqui por atalho ouve o motivo em vez de ver o clique cair no vazio.
        if (Sumidos.Count == 0)
        {
            Mensagem = "Não há ninguém na lista para exportar.";
            MensagemEhErro = false;
            return;
        }

        try
        {
            // Sai da clínica nome e TELEFONE de paciente, num arquivo que qualquer pessoa
            // abre. A segunda barreira vale aqui pela mesma razão que vale no botão de
            // chamar — e o CSV alcança a lista inteira de uma vez.
            SessaoUsuario.Atual.Exigir(
                Permissao.GerenciarCampanhas, "exportar a lista de pacientes sumidos");

            var csv = ExportacaoCsv.Montar(
                ["Paciente", "Telefone", "Situação", "Faixa", "Era de tratamento",
                 "Pacote em aberto", "Pode receber mensagem"],
                Sumidos.Select(s => new[]
                {
                    s.Paciente,
                    s.Telefone ?? "—",
                    s.Detalhe,
                    s.Faixa,
                    s.EraFrequente ? "sim" : "não",
                    s.TemPacoteAberto ? "sim" : "não",
                    // A coluna existe para a planilha não virar lista de disparo: quem
                    // trabalha o CSV fora da tela precisa enxergar o mesmo impedimento
                    // que a tela mostra.
                    s.PodeChamar ? "sim" : $"não — {s.ImpedimentoChamada}"
                }));

            var erro = await ImpressaoPdf.SalvarEAbrirAsync(
                csv,
                ImpressaoPdf.NomeSeguro($"pacientes-sumidos-{DateTime.Today:yyyy-MM-dd}.csv"),
                "CSV (*.csv)|*.csv", ".csv");

            if (erro is null) return;
            Mensagem = erro;
            MensagemEhErro = true;
        }
        catch (Exception ex)
        {
            Clinica.Application.Diagnostico.Registrar(
                "Gerente — lista de sumidos não pôde ser exportada", ex);
            Mensagem = ex.Message;
            MensagemEhErro = true;
        }
    }
}
