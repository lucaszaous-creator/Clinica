using Clinica.Application.Servicos;
using Clinica.Desktop.Shell.Configuracao;
using Clinica.Domain;
using Clinica.Domain.Entities;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;

namespace Clinica.Recepcao.ViewModels;

/// <summary>
/// A janela do ESTORNO (parcela 94): desfazer uma sessão lançada por engano.
///
/// Ela pergunta ITEM A ITEM — decisão da direção — porque o caso varia: às vezes o caixa
/// do dia já foi conferido, às vezes não. As GUIAS saem sempre: são a razão do estorno.
/// Só é oferecido o que aquele atendimento de fato produziu; caixinha para desfazer o que
/// não existe é botão que não faz nada (parcela 41).
///
/// ⚠️ A CONSULTA renovada aparece como informação, sem caixinha. Desfazê-la exigiria
/// ressuscitar a consulta anterior (<c>StatusConsulta</c> não tem "cancelada"), e se uma
/// receita já foi emitida sob a nova, invalidá-la retroativamente quebra um documento
/// clínico. O estrago de deixá-la é pequeno e reversível pela aba Consultas.
/// </summary>
public sealed partial class EstornoAtendimentoViewModel : ObservableObject
{
    private readonly IServiceScopeFactory _escopos;
    private readonly int _atendimentoId;

    public EstornoAtendimentoViewModel(IServiceScopeFactory escopos, int atendimentoId)
    {
        _escopos = escopos;
        _atendimentoId = atendimentoId;
    }

    [ObservableProperty] private string _titulo = "Estornar atendimento";
    [ObservableProperty] private string _subtitulo = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TemImpedimento), nameof(PodeEstornar))]
    private string? _impedimento;

    public bool TemImpedimento => !string.IsNullOrWhiteSpace(Impedimento);

    [ObservableProperty] private string _guias = string.Empty;

    [ObservableProperty] private bool _mostrarCaixa;
    [ObservableProperty] private string _rotuloCaixa = string.Empty;
    [ObservableProperty] private bool _desfazerCaixa;

    [ObservableProperty] private bool _mostrarPacote;
    [ObservableProperty] private bool _devolverSessaoDoPacote;

    [ObservableProperty] private bool _mostrarInsumo;
    [ObservableProperty] private string _rotuloInsumo = string.Empty;
    [ObservableProperty] private bool _devolverInsumoAoEstoque;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TemConsulta))]
    private string? _consultaRenovada;

    public bool TemConsulta => !string.IsNullOrWhiteSpace(ConsultaRenovada);

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PodeEstornar))]
    private string _motivo = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PodeEstornar))]
    private bool _carregando = true;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PodeEstornar))]
    private bool _ocupado;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TemMensagem))]
    private string? _mensagem;

    [ObservableProperty] private bool _mensagemEhErro;

    public bool TemMensagem => !string.IsNullOrWhiteSpace(Mensagem);

    /// <summary>Metade VISÍVEL da permissão; quem impede é o <c>Exigir</c> no comando.</summary>
    public bool PodeLancar => SessaoUsuario.Atual.Pode(Permissao.LancarAtendimento);

    /// <summary>
    /// O motivo é obrigatório — é o que fica na trilha para quem auditar. Sem ele o botão
    /// nem acende, e o serviço recusa de qualquer forma.
    /// </summary>
    public bool PodeEstornar =>
        !TemImpedimento && !Carregando && !Ocupado
        && PodeLancar && !string.IsNullOrWhiteSpace(Motivo);

    /// <summary>O estorno ACONTECEU — a janela fecha e a tela de trás recarrega.</summary>
    public bool Estornado { get; private set; }

    public event Action? Concluiu;

    public async Task CarregarAsync()
    {
        Carregando = true;
        try
        {
            using var scope = _escopos.CreateScope();
            var servico = scope.ServiceProvider.GetRequiredService<EstornoAtendimentoService>();
            var p = await servico.PreverAsync(_atendimentoId);

            Titulo = $"Estornar o atendimento nº {p.Numero}";
            Subtitulo = $"{p.Paciente} · {p.Modalidade} · {p.Data:dd/MM/yyyy}";
            Impedimento = p.Impedimento;

            Guias = p.GuiasAnulaveis switch
            {
                0 => "Não há guia em aberto para anular.",
                1 => "1 guia será anulada (fica no histórico, marcada como estornada).",
                var n => $"{n} guias serão anuladas (ficam no histórico, marcadas como estornadas)."
            };

            MostrarCaixa = p.TemCaixa;
            RotuloCaixa = p.TemCaixa
                ? $"Cancelar a entrada no caixa (R$ {p.EntradaNoCaixa:N2})"
                : string.Empty;

            MostrarPacote = p.TemPacote;

            MostrarInsumo = p.TemInsumo;
            RotuloInsumo = p.InsumosBaixados == 1
                ? "Devolver 1 insumo ao estoque (entrada compensatória)"
                : $"Devolver {p.InsumosBaixados} insumos ao estoque (entrada compensatória)";

            ConsultaRenovada = p.TemConsultaRenovada
                ? $"A consulta do convênio foi renovada em {p.ConsultaRenovadaEm:dd/MM/yyyy} por esta "
                  + "sessão. Ela NÃO é desfeita aqui — desfazer exigiria ressuscitar a anterior, e "
                  + "receita já emitida sob a nova ficaria inválida. Se for o caso, resolva pela aba "
                  + "Consultas."
                : null;

            if (p.AgendamentoId is not null)
                Avisar("O horário deste atendimento volta para \"Agendado\" e poderá ser lançado "
                       + "de novo.");
        }
        catch (Exception ex)
        {
            LogSuite.Registrar("Estorno do atendimento — prévia não pôde ser lida", ex);
            Impedimento = "Não foi possível ler o que este atendimento produziu: " + ex.Message;
        }
        finally
        {
            Carregando = false;
        }
    }

    [RelayCommand]
    private async Task EstornarAsync()
    {
        // A SEGUNDA barreira: o `IsEnabled` explica, o `Exigir` impede.
        SessaoUsuario.Atual.Exigir(Permissao.LancarAtendimento, "estornar o atendimento");

        if (string.IsNullOrWhiteSpace(Motivo))
        {
            Avisar("Diga por que este atendimento está sendo estornado.", erro: true);
            return;
        }

        Ocupado = true;
        try
        {
            using var scope = _escopos.CreateScope();
            var servico = scope.ServiceProvider.GetRequiredService<EstornoAtendimentoService>();

            var r = await servico.EstornarAsync(
                _atendimentoId,
                new DecisaoDeEstorno(Motivo, DesfazerCaixa, DevolverSessaoDoPacote, DevolverInsumoAoEstoque),
                SessaoUsuario.Atual.Operador);

            Estornado = true;

            // Os avisos das reversões de FORA (caixa, pacote, estoque) não podem sumir: o
            // estorno das guias está gravado, e o que falhou lá continua para ser resolvido
            // à mão. Vão para a tela de trás junto com o desfecho.
            Avisos = r.Avisos;
            Concluiu?.Invoke();
        }
        catch (Exception ex)
        {
            LogSuite.Registrar("Estorno do atendimento — falhou", ex);
            Avisar($"Não foi possível estornar: {ex.Message}", erro: true);
        }
        finally
        {
            Ocupado = false;
        }
    }

    /// <summary>O que as reversões de fora não conseguiram fazer — lido pela tela de trás.</summary>
    public IReadOnlyList<string> Avisos { get; private set; } = [];

    private void Avisar(string texto, bool erro = false)
    {
        Mensagem = texto;
        MensagemEhErro = erro;
    }
}
