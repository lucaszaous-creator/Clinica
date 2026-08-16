using System.Collections.ObjectModel;
using Clinica.Application.Servicos;
using Clinica.Clinico.Modulo;
using Clinica.Desktop.Controls;
using Clinica.Desktop.Shell;
using Clinica.Desktop.Shell.Componentes;
using Clinica.Domain.Avaliacoes;
using Clinica.Domain.Entities;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;

namespace Clinica.Clinico.ViewModels;

/// <summary>Uma aplicação já registrada, como a lista a mostra.</summary>
public sealed class LinhaAvaliacao
{
    public required int AvaliacaoId { get; init; }
    public required string Data { get; init; }
    public required string Instrumento { get; init; }
    public required string Pontuacao { get; init; }
    public required string Faixa { get; init; }
    public required string Observacoes { get; init; }
    public required bool TemAlerta { get; init; }
    public required string Alerta { get; init; }

    /// <summary>Faixa no topo da escala — a linha é destacada.</summary>
    public required bool FaixaAlerta { get; init; }

    public static LinhaAvaliacao De(AvaliacaoClinica a) => new()
    {
        AvaliacaoId = a.Id,
        Data = a.Data.ToString("dd/MM/yyyy"),
        Instrumento = a.InstrumentoNome,
        Pontuacao = a.PontuacaoFormatada,
        Faixa = string.IsNullOrWhiteSpace(a.FaixaNome) ? "—" : a.FaixaNome!,
        Observacoes = string.IsNullOrWhiteSpace(a.Observacoes) ? string.Empty : a.Observacoes!,
        TemAlerta = a.TemAlertaDeItem,
        Alerta = a.AlertaItem ?? string.Empty,
        FaixaAlerta = a.FaixaGravidade == GravidadeFaixa.Alerta
    };
}

/// <summary>
/// AVALIAÇÕES POR ESPECIALIDADE — as escalas que a clínica aplica e acompanha.
///
/// Por que a tela existe
/// ---------------------
/// A EVA responde "quanto dói" e serve à acupuntura e à clínica da dor. As outras quatro
/// especialidades da casa não tinham nenhum número: a psiquiatria escrevia "refere
/// melhora do humor", a geriatria "mantém-se independente", a endocrinologia "orientado
/// quanto a hábitos". Tudo verdade, e nada disso compara consulta com consulta, desenha
/// curva ou vai para o relatório — que é exatamente o que o paciente pergunta na sétima
/// semana.
///
/// A lista de instrumentos é filtrada pela ESPECIALIDADE do profissional que está logado.
/// Não é restrição: é a diferença entre abrir uma tela com as cinco escalas que ele usa e
/// abrir um catálogo de dezenas. Quem quiser ver o resto desliga o filtro num clique — o
/// neurocirurgião aplica PHQ-9 no paciente de dor crônica com frequência.
/// </summary>
public sealed partial class AvaliacoesViewModel : ObservableObject
{
    private readonly IServiceScopeFactory _escopos;
    private readonly ISnackbarService _snackbar;
    private readonly IDialogoService _dialogo;
    private readonly PacienteEmFoco _foco;

    /// <summary>Especialidade do profissional logado; null quando não há vínculo.</summary>
    private string? _especialidadeCodigo;

    /// <summary>Instrumentos oferecidos agora (filtrados ou não pela especialidade).</summary>
    public ObservableCollection<IInstrumentoAvaliacao> Instrumentos { get; } = [];

    public ObservableCollection<LinhaAvaliacao> Aplicacoes { get; } = [];

    /// <summary>Curva do escore do instrumento escolhido.</summary>
    public ObservableCollection<PontoGrafico> Curva { get; } = [];

    [ObservableProperty] private string _paciente = string.Empty;
    [ObservableProperty] private bool _semPaciente = true;

    [ObservableProperty] private IInstrumentoAvaliacao? _instrumento;

    /// <summary>Mostrar o catálogo inteiro, e não só o da especialidade.</summary>
    [ObservableProperty] private bool _todosOsInstrumentos;

    [ObservableProperty] private string _resumo = string.Empty;

    /// <summary>Leitura da curva do instrumento escolhido, em uma frase.</summary>
    [ObservableProperty] private string _leituraCurva = string.Empty;

    /// <summary>
    /// A curva tem pelo menos um ponto.
    ///
    /// É o que faz o gráfico SUMIR quando não há o que desenhar. Antes ficavam 150 px de
    /// área vazia com "sem dados no período" flutuando dentro, logo acima de um segundo
    /// estado vazio dizendo a MESMA coisa com outras palavras — duas respostas para a
    /// mesma pergunta, ocupando meia tela.
    /// </summary>
    [ObservableProperty] private bool _temCurva;

    [ObservableProperty] private string _escalaAtual = "—";
    [ObservableProperty] private string _escalaPrimeira = "—";
    [ObservableProperty] private string _escalaGanho = "—";
    [ObservableProperty] private string _escalaFaixa = "—";

    [ObservableProperty] private bool _carregando;
    [ObservableProperty] private bool _naoVerificado;
    [ObservableProperty] private string? _mensagem;
    [ObservableProperty] private bool _mensagemEhErro;

    public bool PodeEditarProntuario => SessaoUsuario.Atual.Pode(Permissao.EditarProntuario);

    public bool TemPaciente => !SemPaciente;

    /// <summary>
    /// Aplicar exige as DUAS escolhas — a escala no seletor e o paciente em foco — além da
    /// permissão, e o botão diz isso apagado: a tela abre pela sidebar sem ninguém em foco,
    /// e botão aceso que não faz nada faz quem clica concluir que o sistema quebrou
    /// (parcela 41). Qual das duas falta, a guarda do comando diz por escrito.
    /// </summary>
    public bool PodeAplicar => TemPaciente && Instrumento is not null && PodeEditarProntuario;

    partial void OnSemPacienteChanged(bool value)
    {
        OnPropertyChanged(nameof(TemPaciente));
        OnPropertyChanged(nameof(PodeAplicar));
    }

    public AvaliacoesViewModel(
        IServiceScopeFactory escopos, ISnackbarService snackbar,
        IDialogoService dialogo, PacienteEmFoco foco)
    {
        _escopos = escopos;
        _snackbar = snackbar;
        _dialogo = dialogo;
        _foco = foco;

        _ = IniciarAsync();
    }

    private int PacienteId => _foco.PacienteId ?? 0;

    /// <summary>
    /// Descobre a especialidade do profissional logado e monta a lista de instrumentos.
    /// Roda uma vez; a lista não muda no meio da sessão porque a permissão e o vínculo do
    /// usuário são a fotografia do login (ver <c>SessaoUsuario</c>).
    /// </summary>
    private async Task IniciarAsync()
    {
        try
        {
            if (SessaoUsuario.Atual.ProfissionalId is { } profissionalId)
            {
                using var scope = _escopos.CreateScope();
                var equipe = scope.ServiceProvider.GetRequiredService<EquipeService>();
                var profissional = await equipe.ObterProfissionalAsync(profissionalId);
                _especialidadeCodigo = profissional?.EspecialidadeCodigo;
            }
        }
        catch (Exception ex)
        {
            // Falhar aqui não pode fechar a tela: sem a especialidade o catálogo aparece
            // inteiro, que é o comportamento de quem não tem vínculo.
            Clinica.Application.Diagnostico.Registrar(
                "Consultório — especialidade do profissional não pôde ser lida", ex);
        }

        MontarInstrumentos();
        if (_foco.Definido) await CarregarAsync();
    }

    partial void OnTodosOsInstrumentosChanged(bool value) => MontarInstrumentos();

    partial void OnInstrumentoChanged(IInstrumentoAvaliacao? value)
    {
        OnPropertyChanged(nameof(PodeAplicar));
        _ = CarregarAsync();
    }

    private void MontarInstrumentos()
    {
        var anterior = Instrumento?.Codigo;

        Instrumentos.Clear();
        var oferecidos = TodosOsInstrumentos
            ? RegistroInstrumentos.Todos
            : AvaliacaoClinicaService.Disponiveis(_especialidadeCodigo);

        foreach (var i in oferecidos) Instrumentos.Add(i);

        // Mantém a escolha quando ela continua na lista: trocar o filtro não pode
        // arrastar a tela para outro instrumento sem o profissional pedir.
        Instrumento = Instrumentos.FirstOrDefault(i => i.Codigo == anterior)
                      ?? Instrumentos.FirstOrDefault();
    }

    /// <summary>
    /// Descarte de resposta fora de ordem (parcela 50): trocar o instrumento dispara uma
    /// carga por troca, e a resposta atrasada da escala anterior chegando por último
    /// desenharia a curva do PHQ-9 sob o título do GAD-7. Quem começou primeiro perde.
    /// </summary>
    private int _geracaoCarga;

    [RelayCommand]
    public async Task CarregarAsync()
    {
        var geracao = ++_geracaoCarga;

        SemPaciente = PacienteId == 0;
        Aplicacoes.Clear();
        Curva.Clear();

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
            Paciente = _foco.Nome;

            using var scope = _escopos.CreateScope();
            var servico = scope.ServiceProvider.GetRequiredService<AvaliacaoClinicaService>();

            // A LISTA é de todos os instrumentos — o histórico do paciente é um só, e
            // escondê-lo atrás do filtro faria uma escala aplicada por outro profissional
            // desaparecer do prontuário.
            var todas = await servico.DoPacienteAsync(PacienteId);

            // Chegou tarde: outra troca já pediu uma carga mais nova.
            if (geracao != _geracaoCarga) return;

            foreach (var a in todas) Aplicacoes.Add(LinhaAvaliacao.De(a));

            Resumo = todas.Count == 0
                ? "Nenhuma escala aplicada a este paciente ainda."
                : $"{todas.Count} aplicação(ões) registradas no prontuário.";

            await CarregarCurvaAsync(servico, geracao);
        }
        catch (Exception ex)
        {
            Clinica.Application.Diagnostico.Registrar("Consultório — avaliações não puderam ser lidas", ex);

            if (geracao != _geracaoCarga) return;

            NaoVerificado = true;
            Mensagem = ex.Message;
            MensagemEhErro = true;
        }
        finally
        {
            // A carga superada não apaga o "Carregando" da que ainda está no ar.
            if (geracao == _geracaoCarga) Carregando = false;
        }
    }

    /// <summary>A curva é sempre de UM instrumento: escores de escalas diferentes não se somam.</summary>
    private async Task CarregarCurvaAsync(AvaliacaoClinicaService servico, int geracao)
    {
        EscalaAtual = EscalaPrimeira = EscalaGanho = EscalaFaixa = "—";
        LeituraCurva = string.Empty;
        TemCurva = false;

        if (Instrumento is null) return;

        var evolucao = await servico.EvolucaoDoEscoreAsync(PacienteId, Instrumento.Codigo);

        // Chegou tarde: a curva na tela é da carga mais nova.
        if (geracao != _geracaoCarga) return;

        foreach (var p in evolucao.Pontos)
            Curva.Add(new PontoGrafico(p.Data.ToString("dd/MM"), p.Pontuacao));

        TemCurva = evolucao.Aplicacoes > 0;

        if (evolucao.Aplicacoes == 0)
        {
            LeituraCurva = $"{Instrumento.Nome} ainda não foi aplicado a este paciente. "
                           + "A primeira aplicação é a linha de base do tratamento.";
            return;
        }

        var unidade = evolucao.Unidade == "%" ? " %" : $" de {evolucao.PontuacaoMaxima}";
        EscalaPrimeira = $"{evolucao.Primeiro}{unidade}";
        EscalaAtual = $"{evolucao.Atual}{unidade}";
        EscalaFaixa = evolucao.Pontos[^1].Faixa ?? "—";

        // O ganho já vem com o sinal da MELHORA resolvido pelo instrumento (o Katz
        // inverte). A tela não decide isso — ela não tem como saber o sentido da escala.
        EscalaGanho = evolucao.Ganho is { } ganho
            ? ganho switch
            {
                > 0 => $"melhorou {ganho}",
                < 0 => $"piorou {-ganho}",
                _ => "sem mudança"
            }
            : "—";

        LeituraCurva = evolucao.Aplicacoes == 1
            ? "Uma aplicação só é a linha de base — não há evolução para ler ainda."
            : $"{evolucao.Aplicacoes} aplicações desde {evolucao.Pontos[0].Data:dd/MM/yyyy}.";
    }

    [RelayCommand]
    private async Task AplicarAsync()
    {
        // A guarda DIZ qual pré-condição faltou, em vez de voltar calada (parcela 41):
        // "sem escala" e "sem paciente" se resolvem em lugares diferentes da tela.
        if (Instrumento is null)
        {
            Mensagem = "Escolha uma escala no seletor acima antes de aplicar.";
            MensagemEhErro = true;
            return;
        }

        if (PacienteId == 0)
        {
            Mensagem = "Escolha um paciente antes de aplicar a escala — o escore entra no "
                     + "prontuário de alguém.";
            MensagemEhErro = true;
            return;
        }

        try
        {
            SessaoUsuario.Atual.Exigir(Permissao.EditarProntuario, "escrever no prontuário");

            var vm = new AplicarAvaliacaoViewModel(
                _escopos, Instrumento, PacienteId,
                SessaoUsuario.Atual.ProfissionalId, null, _especialidadeCodigo);

            var janela = new Janelas.AplicarAvaliacaoWindow(vm)
            {
                Owner = JanelaDona.Atual()
            };

            if (janela.ShowDialog() != true) return;

            _snackbar.Sucesso($"{Instrumento.Nome} registrado no prontuário.");

            // O alerta de item aparece INLINE, não no snackbar: snackbar some em 4s, e
            // esta é justamente a mensagem que não pode passar despercebida.
            if (vm.Salva?.TemAlertaDeItem == true)
            {
                Mensagem = vm.Salva.AlertaItem;
                MensagemEhErro = true;
            }

            await CarregarAsync();
        }
        catch (Exception ex)
        {
            Clinica.Application.Diagnostico.Registrar("Consultório — avaliação não pôde ser aplicada", ex);
            Mensagem = ex.Message;
            MensagemEhErro = true;
        }
    }

    /// <summary>
    /// Apaga uma aplicação. Existe pelo mesmo motivo da exclusão de evolução — o
    /// questionário aplicado no paciente errado tem de sair do prontuário de quem não o
    /// respondeu — e, como ela, deixa rastro na auditoria.
    /// </summary>
    [RelayCommand]
    private async Task ExcluirAsync(LinhaAvaliacao? linha)
    {
        if (linha is null) return;

        try
        {
            SessaoUsuario.Atual.Exigir(Permissao.EditarProntuario, "escrever no prontuário");

            // Cancelar, nunca apagar (parcela 52) — Lei 13.787/2018, guarda de 20 anos.
            var motivo = _dialogo.PerguntarTexto(
                "Cancelar avaliação",
                $"Por que a aplicação de {linha.Instrumento} em {linha.Data} está sendo "
                + "cancelada? Ela sai da curva do tratamento e fica guardada, com as "
                + "respostas e este motivo.");
            if (string.IsNullOrWhiteSpace(motivo)) return;

            using var scope = _escopos.CreateScope();
            var servico = scope.ServiceProvider.GetRequiredService<AvaliacaoClinicaService>();
            await servico.CancelarAsync(linha.AvaliacaoId, motivo, SessaoUsuario.Atual.Operador);

            _snackbar.Info("Avaliação cancelada (guardada no prontuário).");
            await CarregarAsync();
        }
        catch (Exception ex)
        {
            Clinica.Application.Diagnostico.Registrar("Consultório — avaliação não pôde ser excluída", ex);
            Mensagem = ex.Message;
            MensagemEhErro = true;
        }
    }
}
