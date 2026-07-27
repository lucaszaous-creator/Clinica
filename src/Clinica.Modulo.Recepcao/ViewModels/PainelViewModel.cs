using System.Collections.ObjectModel;
using Clinica.Application.Modelos;
using Clinica.Application.Servicos;
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

    private readonly PainelRecepcaoService _painel;

    public ObservableCollection<LinhaOcupacao> Ocupacao { get; } = [];
    public ObservableCollection<LinhaPendenciaRecepcao> Pendencias { get; } = [];

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

    public PainelViewModel(PainelRecepcaoService painel)
    {
        _painel = painel;
        _ = CarregarAsync();
    }

    partial void OnDiaChanged(DateTime value) => _ = CarregarAsync();

    [RelayCommand]
    public async Task CarregarAsync()
    {
        var dia = DateOnly.FromDateTime(Dia);
        try
        {
            Carregando = true;
            Mensagem = string.Empty;
            MensagemEhErro = false;

            var resumo = await _painel.ResumoAsync(dia);
            AplicarResumo(resumo);
        }
        catch (Exception ex)
        {
            Clinica.Application.Diagnostico.Registrar("Recepção — painel não pôde ser carregado", ex);
            Mensagem = $"Não foi possível carregar o painel: {ex.Message}";
            MensagemEhErro = true;
        }
        finally
        {
            Carregando = false;
        }

        await CarregarPendenciasAsync(dia);
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
    private async Task CarregarPendenciasAsync(DateOnly dia)
    {
        try
        {
            var pendencias = await _painel.PendenciasDoDiaAsync(dia);

            Pendencias.Clear();
            foreach (var p in pendencias)
                Pendencias.Add(new LinhaPendenciaRecepcao
                {
                    PacienteId = p.PacienteId,
                    Paciente = p.PacienteNome,
                    Descricao = p.Descricao ?? $"{p.Tipo} ({p.Ordem})",
                    Atraso = p.DiasEmAtraso <= 0
                        ? "vence hoje"
                        : $"{p.DiasEmAtraso} dia(s) em atraso",
                    Telefone = p.PacienteTelefone
                });

            PendenciasNaoVerificadas = false;
        }
        catch (Exception ex)
        {
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
