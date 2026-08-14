using System.Collections.ObjectModel;
using System.Windows.Threading;
using Clinica.Application.Modelos;
using Clinica.Application.Abstracoes;
using Clinica.Application.Servicos;
using Clinica.Domain.Entities;
using Clinica.Desktop.Shell.Componentes;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Clinica.Recepcao.ViewModels;

/// <summary>Uma faixa de ocupação do dia (um profissional).</summary>
public sealed class LinhaOcupacao
{
    public required string Nome { get; init; }
    public required string Horarios { get; init; }
    public required string Carga { get; init; }
    public required string Faltas { get; init; }

    /// <summary>0..1 — quanto da jornada de referência já está tomado (para a barra).</summary>
    public required double Fracao { get; init; }
}

/// <summary>Guia pendente de um paciente que vem hoje.</summary>
public sealed class LinhaPendenciaRecepcao
{
    public required int PacienteId { get; init; }
    public required string Paciente { get; init; }
    public required string Descricao { get; init; }
    public required string Atraso { get; init; }
    public string? Telefone { get; init; }
    public bool TemTelefone => !string.IsNullOrWhiteSpace(Telefone);
}

/// <summary>Um paciente que faz aniversário, com o contato para a clínica ligar.</summary>
public sealed class LinhaAniversariante
{
    public required int PacienteId { get; init; }
    public required string Paciente { get; init; }
    public required string Detalhe { get; init; }
    public string? Telefone { get; init; }

    /// <summary>Vem hoje: o parabéns é dado no balcão e não custa nem a ligação.</summary>
    public required bool VemHoje { get; init; }

    /// <summary>
    /// Consentimento de comunicação e marketing VIGENTE (LGPD).
    ///
    /// O "Feliz aniversário" é comunicação ATIVA da clínica — não é aviso sobre uma sessão
    /// que o paciente marcou (transacional, parcela 5) nem cobrança de conta vencida
    /// (transacional, parcela 23). É relacionamento, a mesma natureza do recall, e o
    /// projeto decidiu na parcela 5 que recall só sai com este consentimento.
    ///
    /// A parcela 64 aplicou a regra à tela "Quem parou de vir" e escreveu a lição: ao
    /// achar o MESMO ato em duas telas, compare as duas. Esta é a terceira, e era a que
    /// não perguntava nada.
    /// </summary>
    public required bool TemConsentimento { get; init; }

    public bool TemTelefone => !string.IsNullOrWhiteSpace(Telefone);

    /// <summary>Metade VISÍVEL da regra: só há WhatsApp a abrir com telefone E consentimento.</summary>
    public bool PodeParabenizar => TemTelefone && TemConsentimento;

    /// <summary>
    /// Por que não dá, escrito na linha. O paciente continua na lista de propósito — quem
    /// some da lista some da cabeça, e o que a recepção precisa é da instrução para
    /// resolver: colher o consentimento no balcão da próxima vez que ele aparecer.
    /// </summary>
    public string ImpedimentoParabens =>
        !TemTelefone ? "sem telefone no cadastro"
        : !TemConsentimento ? "sem consentimento de comunicação (LGPD) — colha no balcão"
        : string.Empty;

    public bool TemImpedimento => ImpedimentoParabens.Length > 0;
}

/// <summary>
/// Painel próprio da Recepção — que NÃO é o do faturamento.
///
/// Lá a pergunta é "que guia vence primeiro"; aqui é "como está o dia": quem chegou,
/// quem espera, quanto cada profissional tem na agenda e quem está na lista de espera.
/// As guias pendentes aparecem só recortadas para os pacientes de HOJE, porque é o
/// único momento barato de cobrar o documento — depois vira telefonema do faturamento.
/// </summary>
public sealed partial class PainelViewModel : ObservableObject
{
    /// <summary>Jornada de referência para a barra de ocupação (8h em minutos).</summary>
    private const int JornadaMinutos = 8 * 60;

    /// <summary>
    /// Quantos dias à frente a lista de aniversário alcança. Seis dias cobrem a semana
    /// inteira: a clínica que abre de segunda a sexta perderia todo aniversário de
    /// domingo se a lista fosse só do dia.
    /// </summary>
    private const int JanelaAniversarioDias = 6;

    private readonly RelacionamentoService _relacionamento;

    /// <summary>
    /// Lido direto para a conferência de consentimento dos aniversariantes — é a MESMA
    /// consulta em lote que a campanha e a tela de sumidos usam. Uma segunda definição de
    /// "quem pode receber mensagem" divergiria na primeira correção, e a que ninguém
    /// lembraria de ajustar é sempre a segunda.
    /// </summary>
    private readonly IClinicaRepositorio _repo;

    private readonly PainelRecepcaoService _painel;

    public ObservableCollection<LinhaOcupacao> Ocupacao { get; } = [];
    public ObservableCollection<LinhaPendenciaRecepcao> Pendencias { get; } = [];

    /// <summary>
    /// Quem faz aniversário. A data estava no cadastro desde sempre e NENHUMA tela a
    /// lia — a ligação mais barata que a clínica faz (trinta segundos, sem nada a
    /// vender) era a que se perdia.
    /// </summary>
    public ObservableCollection<LinhaAniversariante> Aniversariantes { get; } = [];

    /// <summary>Falha na leitura dos aniversariantes: lista vazia por erro, não por não haver.</summary>
    [ObservableProperty] private bool _aniversariantesNaoVerificados;

    [ObservableProperty] private DateTime _dia = DateTime.Today;
    [ObservableProperty] private bool _carregando;

    [ObservableProperty] private string _agendados = "—";
    [ObservableProperty] private string _naRecepcao = "—";
    [ObservableProperty] private string _emAtendimento = "—";
    [ObservableProperty] private string _atendidos = "—";
    [ObservableProperty] private string _faltas = "—";
    [ObservableProperty] private string _esperaMedia = "—";
    [ObservableProperty] private string _listaDeEspera = "—";
    [ObservableProperty] private string _encaixes = "—";
    [ObservableProperty] private string _taxaFalta = "—";

    /// <summary>Feedback inline: fica na tela enquanto o problema existir.</summary>
    [ObservableProperty] private string _mensagem = string.Empty;
    [ObservableProperty] private bool _mensagemEhErro;

    /// <summary>
    /// Terceiro estado do bloco de pendências: a checagem NÃO rodou. Sem ele, uma
    /// consulta que falhou apareceria como "nenhuma pendência" — falha exibida como
    /// sucesso é o pior desfecho possível aqui.
    /// </summary>
    [ObservableProperty] private bool _pendenciasNaoVerificadas;

    /// <summary>
    /// O RESUMO do dia não pôde ser lido — o terceiro estado do painel inteiro
    /// (parcela 62). O XAML já ligava <c>NaoVerificado</c> e a propriedade NÃO EXISTIA:
    /// binding morto, que em WPF falha calado. A frase "os números não estão zerados —
    /// eles não puderam ser lidos" estava escrita na tela e nunca apareceu, e o painel
    /// mostrava zeros de uma leitura que falhou. É o defeito que este componente existe
    /// para impedir, dentro da tela de abertura do balcão.
    /// </summary>
    [ObservableProperty] private bool _naoVerificado;

    /// <summary>
    /// Descarte de resposta fora de ordem (parcela 50): os steppers de dia disparam uma
    /// carga por clique, e num banco remoto a resposta de ontem pode chegar depois da de
    /// hoje — o painel mostraria o dia errado sob o título certo. Só a carga mais nova
    /// escreve na tela.
    /// </summary>
    private int _geracaoCarga;

    /// <summary>
    /// A releitura de fundo. Ligada e desligada pela View (Loaded/Unloaded), como na
    /// Fila e na Agenda — o shell cria uma tela nova a cada navegação.
    /// </summary>
    private readonly DispatcherTimer _relogio;

    public PainelViewModel(
        PainelRecepcaoService painel, RelacionamentoService relacionamento,
        IClinicaRepositorio repo)
    {
        _painel = painel;
        _relacionamento = relacionamento;
        _repo = repo;

        // Dois minutos, e não um: o painel são CONTAGENS do dia, que mudam mais devagar
        // do que a coluna de um cartão na fila, e cada batida custa três consultas ao
        // banco remoto. Um minuto aqui seria pagar o dobro pela mesma resposta.
        _relogio = new DispatcherTimer { Interval = TimeSpan.FromMinutes(2) };
        _relogio.Tick += (_, _) => _ = ReconferirAsync();

        _ = CarregarAsync();
    }

    partial void OnDiaChanged(DateTime value) => _ = CarregarAsync();

    [RelayCommand]
    public Task CarregarAsync() => CarregarAsync(silencioso: false);

    private async Task CarregarAsync(bool silencioso)
    {
        var geracao = ++_geracaoCarga;

        var dia = DateOnly.FromDateTime(Dia);
        try
        {
            if (!silencioso)
            {
                Carregando = true;
                NaoVerificado = false;
                Mensagem = string.Empty;
                MensagemEhErro = false;
            }

            var resumo = await _painel.ResumoAsync(dia);

            // Chegou tarde: outra carga mais nova já foi pedida.
            if (geracao != _geracaoCarga) return;

            AplicarResumo(resumo);
        }
        catch (Exception ex)
        {
            // Chegou tarde: outra carga mais nova já foi pedida.
            if (geracao != _geracaoCarga) return;

            Clinica.Application.Diagnostico.Registrar("Recepção — painel não pôde ser carregado", ex);

            // Releitura de fundo que falha não pinta a tela: o painel segue com o que
            // tinha, e o log guarda o motivo.
            if (silencioso) return;

            NaoVerificado = true;
            Mensagem = $"Não foi possível carregar o painel: {ex.Message}";
            MensagemEhErro = true;
        }
        finally
        {
            // A carga superada não apaga o "Carregando" da que ainda está no ar.
            if (!silencioso && geracao == _geracaoCarga) Carregando = false;
        }

        await CarregarPendenciasAsync(dia, geracao);
        await CarregarAniversariantesAsync(dia, geracao);
    }

    /// <summary>
    /// A batida do relógio (parcela 62). O painel é a tela de ABERTURA do balcão — é a
    /// que fica no monitor a manhã inteira sem ninguém tocar —, e ela conta quem chegou,
    /// quem falta e as guias pendentes do dia. Sem releitura, o número que a
    /// recepcionista olha às 11h é o das 8h, e ela concluiria que ninguém chegou.
    ///
    /// Só HOJE, pela razão da Fila: quem está olhando o painel de terça que vem não tem
    /// nada correndo, e recarregar por baixo faria os números se mexerem enquanto lê.
    /// </summary>
    private async Task ReconferirAsync()
    {
        if (Dia.Date != DateTime.Today || Carregando) return;

        try
        {
            await CarregarAsync(silencioso: true);
        }
        catch (Exception ex)
        {
            Clinica.Application.Diagnostico.Registrar(
                "Recepção — releitura automática do painel falhou", ex);
        }
    }

    /// <summary>Liga a releitura (chamada quando a tela entra em cena).</summary>
    public void IniciarRelogio() => _relogio.Start();

    /// <summary>Desliga a releitura (chamada quando a tela sai de cena).</summary>
    public void PararRelogio() => _relogio.Stop();

    /// <summary>
    /// Aniversariantes do dia e dos próximos dias. A janela existe porque a clínica não
    /// abre todo dia: sem ela, todo aniversário de domingo se perderia.
    ///
    /// Isolada do resto, como as pendências: se falhar, o painel continua mostrando o dia.
    /// </summary>
    private async Task CarregarAniversariantesAsync(DateOnly dia, int geracao)
    {
        try
        {
            AniversariantesNaoVerificados = false;
            var lista = await _relacionamento.AniversariantesAsync(dia, JanelaAniversarioDias);

            // Quem pode receber a mensagem. EM LOTE e pelo MESMO método da campanha e da
            // tela de sumidos — uma consulta por paciente transformaria o painel, que
            // relê sozinho a cada dois minutos, em dezenas de idas a um banco remoto.
            // Lista vazia não vai ao banco: não há por quem perguntar.
            var comConsentimento = lista.Count == 0
                ? []
                : (await _repo.PacientesComConsentimentoVigenteAsync(
                        FinalidadeConsentimento.ComunicacaoEMarketing,
                        lista.Select(a => a.PacienteId).ToList())).ToHashSet();

            // Chegou tarde: outra carga mais nova já foi pedida.
            if (geracao != _geracaoCarga) return;

            Aniversariantes.Clear();
            foreach (var a in lista)
                Aniversariantes.Add(new LinhaAniversariante
                {
                    PacienteId = a.PacienteId,
                    Paciente = a.Nome,
                    Telefone = a.Telefone,
                    VemHoje = a.VemHoje,
                    TemConsentimento = comConsentimento.Contains(a.PacienteId),
                    Detalhe = (a.Idade is { } idade ? $"{idade} anos · " : string.Empty)
                              + a.DiaEMes
                              + (a.VemHoje ? " · vem hoje" : string.Empty)
                });
        }
        catch (Exception ex)
        {
            // Chegou tarde: outra carga mais nova já foi pedida.
            if (geracao != _geracaoCarga) return;

            Clinica.Application.Diagnostico.Registrar(
                "Recepção — aniversariantes não puderam ser lidos", ex);
            Aniversariantes.Clear();
            AniversariantesNaoVerificados = true;
        }
    }

    /// <summary>Abre o WhatsApp para dar os parabéns — mensagem sem nada a vender.</summary>
    [RelayCommand]
    private void Parabenizar(LinhaAniversariante? linha)
    {
        if (linha?.Telefone is not { } telefone) return;

        // A guarda que IMPEDE, ao lado do botão que EXPLICA. Sem ela o `IsEnabled` seria
        // enfeite — e aqui o que passaria direto não é um clique a mais, é mensagem de
        // relacionamento saindo para quem disse que não queria receber.
        if (!linha.TemConsentimento)
        {
            Mensagem = $"{linha.Paciente} não tem consentimento de comunicação vigente (LGPD). "
                       + "Colha o consentimento no balcão da próxima vez que ele aparecer.";
            MensagemEhErro = true;
            return;
        }

        var primeiro = linha.Paciente.Split(' ').FirstOrDefault() ?? linha.Paciente;
        var erro = Whatsapp.Abrir(
            telefone, linha.Paciente,
            $"Feliz aniversário, {primeiro}! Toda a equipe da clínica deseja um ótimo dia.");

        if (erro is null) return;
        Mensagem = erro;
        MensagemEhErro = true;
    }

    private void AplicarResumo(ResumoDiaRecepcao resumo)
    {
        Agendados = resumo.Agendados.ToString();
        NaRecepcao = resumo.NaRecepcao.ToString();
        EmAtendimento = resumo.EmAtendimento.ToString();
        Atendidos = resumo.Atendidos.ToString();
        Faltas = resumo.Faltas.ToString();
        EsperaMedia = $"{resumo.EsperaMediaMinutos} min";
        ListaDeEspera = resumo.NaListaDeEspera.ToString();
        Encaixes = resumo.Encaixes.ToString();
        TaxaFalta = $"{resumo.TaxaFaltaPercentual}%";

        Ocupacao.Clear();
        foreach (var o in resumo.Ocupacao)
            Ocupacao.Add(new LinhaOcupacao
            {
                Nome = o.Nome,
                Horarios = $"{o.Total} horário(s)",
                Carga = $"{o.MinutosOcupados} min",
                Faltas = o.Faltas == 0 ? "—" : $"{o.Faltas} falta(s)",
                Fracao = Math.Min(1.0, o.MinutosOcupados / (double)JornadaMinutos)
            });
    }

    /// <summary>
    /// Guias pendentes dos pacientes do dia. Isolada do resto de propósito: se ela
    /// falhar, o painel continua mostrando o dia — e diz que não conseguiu conferir.
    /// </summary>
    private async Task CarregarPendenciasAsync(DateOnly dia, int geracao)
    {
        try
        {
            var pendencias = await _painel.PendenciasDoDiaAsync(dia);

            // Chegou tarde: outra carga mais nova já foi pedida.
            if (geracao != _geracaoCarga) return;

            Pendencias.Clear();
            foreach (var p in pendencias)
                Pendencias.Add(new LinhaPendenciaRecepcao
                {
                    PacienteId = p.PacienteId,
                    Paciente = p.PacienteNome,
                    Descricao = p.Descricao
                        ?? $"{Clinica.Domain.RotulosEnum.De(p.Tipo)} ({Clinica.Domain.RotulosEnum.De(p.Ordem)})",
                    Atraso = p.DiasEmAtraso <= 0
                        ? "vence hoje"
                        : $"{p.DiasEmAtraso} dia(s) em atraso",
                    Telefone = p.PacienteTelefone
                });

            PendenciasNaoVerificadas = false;
        }
        catch (Exception ex)
        {
            // Chegou tarde: outra carga mais nova já foi pedida.
            if (geracao != _geracaoCarga) return;

            Clinica.Application.Diagnostico.Registrar(
                "Recepção — pendências do dia não puderam ser verificadas", ex);
            Pendencias.Clear();
            PendenciasNaoVerificadas = true;
        }
    }

    /// <summary>Cobra o documento pelo WhatsApp, direto da linha da pendência.</summary>
    [RelayCommand]
    private void CobrarGuia(LinhaPendenciaRecepcao? linha)
    {
        if (linha is null) return;

        var erro = Whatsapp.Abrir(
            linha.Telefone, linha.Paciente,
            Whatsapp.CobrancaDeGuia(linha.Paciente));

        if (erro is null) return;
        Mensagem = erro;
        MensagemEhErro = true;
    }

    [RelayCommand]
    private void DiaAnterior() => Dia = Dia.AddDays(-1);

    [RelayCommand]
    private void ProximoDia() => Dia = Dia.AddDays(1);

    [RelayCommand]
    private void Hoje() => Dia = DateTime.Today;
}
