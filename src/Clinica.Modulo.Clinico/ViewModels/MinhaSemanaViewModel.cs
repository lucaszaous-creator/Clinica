using System.Collections.ObjectModel;
using Clinica.Application.Modelos;
using Clinica.Application.Servicos;
using Clinica.Clinico.Modulo;
using Clinica.Desktop.Shell;
using Clinica.Desktop.Shell.Modulos;
using Clinica.Domain.Entities;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;

namespace Clinica.Clinico.ViewModels;

/// <summary>Uma sessão na coluna do dia da semana.</summary>
public sealed class LinhaSemana
{
    public required int AgendamentoId { get; init; }

    /// <summary>Dia do horário — viaja para o posto porque o casamento sessão × evolução cai para paciente + data.</summary>
    public required DateOnly Data { get; init; }
    public required int PacienteId { get; init; }
    public required string Paciente { get; init; }
    public required string Hora { get; init; }
    public required string Modalidade { get; init; }
    public required bool Atendido { get; init; }
    public required bool ForaDaFila { get; init; }
    public required bool RegistroPendente { get; init; }

    public static LinhaSemana De(SessaoDoDia s) => new()
    {
        AgendamentoId = s.AgendamentoId,
        Data = DateOnly.FromDateTime(s.DataHora),
        PacienteId = s.PacienteId,
        Paciente = s.PacienteNome,
        Hora = s.DataHora.ToString("HH:mm"),
        Modalidade = s.Modalidade,
        Atendido = s.Status == StatusAgendamento.Realizado,
        // Cancelado e falta aparecem MARCADOS, nunca sumindo — é a regra da folha do dia:
        // quem lê a semana precisa saber que a quinta às 15h vagou, e linha ausente se
        // confunde com horário que nunca existiu.
        ForaDaFila = s.Status is StatusAgendamento.Cancelado or StatusAgendamento.Faltou,
        RegistroPendente = s.RegistroPendente
    };
}

/// <summary>Uma coluna: o dia, com as sessões dele.</summary>
public sealed class ColunaDia
{
    public required DateOnly Data { get; init; }
    public required string Titulo { get; init; }
    public required string Subtitulo { get; init; }
    public required bool EhHoje { get; init; }
    public required ObservableCollection<LinhaSemana> Sessoes { get; init; }

    public bool Vazio => Sessoes.Count == 0;
}

/// <summary>
/// A SEMANA de quem atende (parcela 39).
///
/// "Meu dia" responde o que acontece hoje. Ele não responde as perguntas que se fazem
/// <b>com o paciente ainda na frente</b>, no fim da consulta: "quando eu tenho espaço?",
/// "vale marcar o seu retorno para sexta?", "quinta está cheia?". A agenda do balcão tem
/// visão de semana desde a parcela 26 e o app de quem atende não tinha — então a resposta
/// era descer até a recepção, ou marcar no escuro.
///
/// A tela é de LEITURA e leva à ação: ela não marca horário (quem marca é o balcão, com o
/// telefone na mão e a agenda de todo mundo à vista) — ela mostra a carga da semana e
/// abre o paciente de qualquer linha.
/// </summary>
public sealed partial class MinhaSemanaViewModel : ObservableObject
{
    private readonly IServiceScopeFactory _escopos;
    private readonly PacienteEmFoco _foco;

    public ObservableCollection<ColunaDia> Dias { get; } = [];

    [ObservableProperty] private DateTime _referencia = DateTime.Today;

    [ObservableProperty] private string _profissional = string.Empty;

    [ObservableProperty] private string _periodo = string.Empty;

    [ObservableProperty] private string _resumo = string.Empty;

    [ObservableProperty] private bool _carregando;
    [ObservableProperty] private bool _naoVerificado;
    [ObservableProperty] private string? _mensagem;
    [ObservableProperty] private bool _mensagemEhErro;
    [ObservableProperty] private bool _semVinculo;

    public bool Vazio => Dias.All(d => d.Vazio);

    /// <summary>
    /// Descarte de resposta fora de ordem (parcela 50): as setas de semana disparam uma
    /// carga por clique, e a resposta da semana anterior pode chegar depois da atual — o
    /// quadro mostraria as sessões de uma semana sob o período de outra. Só a carga mais
    /// nova escreve na tela.
    /// </summary>
    private int _geracaoCarga;

    public MinhaSemanaViewModel(IServiceScopeFactory escopos, PacienteEmFoco foco)
    {
        _escopos = escopos;
        _foco = foco;
        _ = CarregarAsync();
    }

    partial void OnReferenciaChanged(DateTime value) => _ = CarregarAsync();

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

            var profissionalId = SessaoUsuario.Atual.ProfissionalId;
            SemVinculo = profissionalId is null;

            using var scope = _escopos.CreateScope();
            var consultorio = scope.ServiceProvider.GetRequiredService<ConsultorioService>();

            var semana = await consultorio.DaSemanaAsync(
                DateOnly.FromDateTime(Referencia), profissionalId);

            // Chegou tarde: outra carga mais nova já foi pedida.
            if (geracao != _geracaoCarga) return;

            Profissional = semana.ProfissionalNome;
            Periodo = $"{semana.Inicio:dd/MM} a {semana.Fim:dd/MM/yyyy}";

            var hoje = DateOnly.FromDateTime(DateTime.Today);

            // Monta e só ENTÃO publica: entre o Clear e o último Add não pode haver await
            // (parcela 62) — a lista ficava vazia na tela durante o roundtrip ao banco.
            Dias.Clear();
            foreach (var dia in semana.Dias)
            {
                Dias.Add(new ColunaDia
                {
                    Data = dia.Dia,
                    Titulo = NomeDoDia(dia.Dia.DayOfWeek),
                    Subtitulo = dia.Dia.ToString("dd/MM"),
                    EhHoje = dia.Dia == hoje,
                    Sessoes = [.. dia.Sessoes.Select(LinhaSemana.De)]
                });
            }

            Resumo = semana.Sessoes == 0
                ? "Nenhum horário marcado nesta semana."
                : MontarResumo(semana);

            OnPropertyChanged(nameof(Vazio));
        }
        catch (Exception ex)
        {
            // Chegou tarde: outra carga mais nova já foi pedida.
            if (geracao != _geracaoCarga) return;

            NaoVerificado = true;
            Clinica.Application.Diagnostico.Registrar(
                "Consultório — a semana não pôde ser carregada", ex);
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
    /// O resumo diz o que as colunas não dizem: quantas PESSOAS são (sessões e pacientes
    /// não são a mesma leitura de carga) e qual o dia mais cheio, que é o que se procura
    /// antes de oferecer um horário.
    /// </summary>
    private static string MontarResumo(SemanaDoProfissional semana)
    {
        var partes = new List<string>
        {
            $"{semana.Sessoes} sessão(ões)",
            $"{semana.PacientesDistintos} paciente(s)"
        };

        if (semana.DiaMaisCheio is { } cheio && cheio.Sessoes.Count > 0)
            partes.Add($"dia mais cheio: {NomeDoDia(cheio.Dia.DayOfWeek).ToLowerInvariant()} "
                       + $"({cheio.Sessoes.Count})");

        if (semana.RegistrosPendentes > 0)
            partes.Add($"{semana.RegistrosPendentes} sem evolução escrita");

        return string.Join(" · ", partes);
    }

    private static string NomeDoDia(DayOfWeek dia) => dia switch
    {
        DayOfWeek.Monday => "Segunda",
        DayOfWeek.Tuesday => "Terça",
        DayOfWeek.Wednesday => "Quarta",
        DayOfWeek.Thursday => "Quinta",
        DayOfWeek.Friday => "Sexta",
        DayOfWeek.Saturday => "Sábado",
        _ => "Domingo"
    };

    [RelayCommand]
    private void SemanaAnterior() => Referencia = Referencia.AddDays(-7);

    [RelayCommand]
    private void ProximaSemana() => Referencia = Referencia.AddDays(7);

    [RelayCommand]
    private void SemanaAtual() => Referencia = DateTime.Today;

    /// <summary>Abre o paciente da linha — a semana é leitura, e daqui se entra no caso.</summary>
    [RelayCommand]
    private void Abrir(LinhaSemana? linha)
    {
        if (linha is null) return;

        _foco.Definir(linha.PacienteId, linha.Paciente, linha.AgendamentoId,
                      dataDoHorario: linha.Data);
        NavegacaoSuite.Ir(ModuloClinico.ChavePaciente);
    }
}
