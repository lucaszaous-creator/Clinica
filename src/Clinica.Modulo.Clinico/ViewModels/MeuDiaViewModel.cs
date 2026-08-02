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

/// <summary>
/// Uma linha do dia do profissional.
///
/// <c>required</c> em tudo o que a tela mostra é proposital (é a convenção das linhas de
/// lista do projeto): quando a linha ganhar um campo, o compilador cobra a fábrica em vez
/// de a tela mostrar vazio.
/// </summary>
public sealed class LinhaSessao
{
    public required int AgendamentoId { get; init; }
    public required int PacienteId { get; init; }
    public required string Paciente { get; init; }
    public required string Hora { get; init; }
    public required string Modalidade { get; init; }
    public required string Local { get; init; }
    public required string Situacao { get; init; }
    public required bool EvolucaoEscrita { get; init; }
    public required bool RegistroPendente { get; init; }
    public required bool Encaixe { get; init; }
    public int? AtendimentoId { get; init; }

    /// <summary>O que o botão de registro diz — "Escrever" e "Abrir" não são a mesma ação.</summary>
    public string RotuloRegistro => EvolucaoEscrita ? "Ver registro" : "Atender";

    public static LinhaSessao De(SessaoDoDia s) => new()
    {
        AgendamentoId = s.AgendamentoId,
        PacienteId = s.PacienteId,
        Paciente = s.PacienteNome,
        Hora = s.DataHora.ToString("HH:mm"),
        Modalidade = s.Modalidade,
        Local = s.Sala ?? "—",
        Situacao = Rotular(s.Status, s.Etapa),
        EvolucaoEscrita = s.EvolucaoEscrita,
        RegistroPendente = s.RegistroPendente,
        Encaixe = s.Encaixe,
        AtendimentoId = s.AtendimentoId
    };

    /// <summary>
    /// A situação como o consultório a lê. A etapa da fila (chegou, está na sala) só
    /// interessa enquanto o horário está de pé; depois de realizado o que importa é que a
    /// sessão aconteceu.
    /// </summary>
    private static string Rotular(StatusAgendamento status, EtapaFila etapa) => status switch
    {
        StatusAgendamento.Realizado => "Atendido",
        StatusAgendamento.Cancelado => "Cancelado",
        StatusAgendamento.Faltou => "Faltou",
        _ => etapa switch
        {
            EtapaFila.Chegou => "Na recepção",
            EtapaFila.EmAtendimento => "Na sala",
            _ => "Aguardando"
        }
    };
}

/// <summary>Uma sessão de dia anterior que ficou sem evolução escrita.</summary>
public sealed class LinhaRegistroPendente
{
    public required int AgendamentoId { get; init; }
    public required int PacienteId { get; init; }
    public required string Paciente { get; init; }
    public required string Quando { get; init; }
    public required string Modalidade { get; init; }
    public required string Atraso { get; init; }

    public static LinhaRegistroPendente De(RegistroPendente r, DateOnly hoje)
    {
        var dias = r.DiasEmAberto(hoje);
        return new LinhaRegistroPendente
        {
            AgendamentoId = r.AgendamentoId,
            PacienteId = r.PacienteId,
            Paciente = r.PacienteNome,
            Quando = r.DataHora.ToString("dd/MM 'às' HH:mm"),
            Modalidade = r.Modalidade,
            Atraso = dias == 1 ? "ontem" : $"há {dias} dias"
        };
    }
}

/// <summary>
/// A abertura do consultório: o dia de quem atende.
///
/// A tela responde três coisas, nesta ordem — quem vem hoje, o que já foi atendido e
/// **o que eu ainda não escrevi**. A terceira é a que justifica o módulo: a agenda da
/// recepção já mostrava as duas primeiras, e nenhuma tela do sistema mostrava a terceira.
/// </summary>
public sealed partial class MeuDiaViewModel : ObservableObject
{
    private readonly IServiceScopeFactory _escopos;
    private readonly PacienteEmFoco _foco;

    public ObservableCollection<LinhaSessao> Sessoes { get; } = [];

    /// <summary>Sessões de dias anteriores ainda sem evolução — a pendência do consultório.</summary>
    public ObservableCollection<LinhaRegistroPendente> Pendentes { get; } = [];

    [ObservableProperty] private DateTime _dia = DateTime.Today;

    [ObservableProperty] private string _profissional = string.Empty;

    [ObservableProperty] private string _resumo = string.Empty;

    [ObservableProperty] private string _resumoPendentes = string.Empty;

    [ObservableProperty] private bool _carregando;

    /// <summary>A leitura FALHOU — nunca desenhar falha como "não há nada".</summary>
    [ObservableProperty] private bool _naoVerificado;

    [ObservableProperty] private string? _mensagem;
    [ObservableProperty] private bool _mensagemEhErro;

    /// <summary>
    /// O usuário não tem <c>Profissional</c> vinculado ao login, e a tela está mostrando o
    /// dia da clínica inteira. Dito na tela de propósito: um dia com o dobro de horários
    /// sem explicação se lê como defeito.
    /// </summary>
    [ObservableProperty] private bool _semVinculo;

    public MeuDiaViewModel(IServiceScopeFactory escopos, PacienteEmFoco foco)
    {
        _escopos = escopos;
        _foco = foco;
        _ = CarregarAsync();
    }

    partial void OnDiaChanged(DateTime value) => _ = CarregarAsync();

    private static int? ProfissionalDaSessao => SessaoUsuario.Atual.ProfissionalId;

    [RelayCommand]
    public async Task CarregarAsync()
    {
        try
        {
            Carregando = true;
            NaoVerificado = false;
            Mensagem = null;
            MensagemEhErro = false;
            Sessoes.Clear();
            Pendentes.Clear();

            var profissionalId = ProfissionalDaSessao;
            SemVinculo = profissionalId is null;

            var hoje = DateOnly.FromDateTime(DateTime.Today);
            var dia = DateOnly.FromDateTime(Dia);

            using var scope = _escopos.CreateScope();
            var consultorio = scope.ServiceProvider.GetRequiredService<ConsultorioService>();

            var doDia = await consultorio.DoDiaAsync(dia, profissionalId);
            Profissional = doDia.ProfissionalNome;

            foreach (var s in doDia.Sessoes) Sessoes.Add(LinhaSessao.De(s));

            Resumo = doDia.Sessoes.Count == 0
                ? "Nenhum horário marcado neste dia."
                : $"{doDia.Sessoes.Count} horário(s) · {doDia.Atendidos} atendido(s) · "
                  + $"{doDia.AEsperar} a esperar · {doDia.RegistrosPendentes} sem registro.";

            // A pendência é sempre de DIAS ANTERIORES, mesmo quando se olha um dia passado
            // na agenda: ela é a fila de trabalho do profissional, não uma propriedade do
            // dia escolhido — trocá-la junto com o calendário faria a lista sumir na hora
            // em que ele foi conferir o que aconteceu na semana retrasada.
            var pendentes = await consultorio.RegistrosPendentesAsync(hoje, profissionalId);
            foreach (var p in pendentes) Pendentes.Add(LinhaRegistroPendente.De(p, hoje));

            ResumoPendentes = pendentes.Count == 0
                ? "Nenhuma sessão dos últimos dias sem evolução escrita."
                : $"{pendentes.Count} sessão(ões) atendida(s) nos últimos "
                  + $"{ConsultorioService.JanelaRegistroPendenteDias} dias continuam sem evolução escrita.";
        }
        catch (Exception ex)
        {
            NaoVerificado = true;
            Clinica.Application.Diagnostico.Registrar("Consultório — o dia não pôde ser carregado", ex);
            Mensagem = ex.Message;
            MensagemEhErro = true;
        }
        finally
        {
            Carregando = false;
        }
    }

    [RelayCommand]
    private void DiaAnterior() => Dia = Dia.AddDays(-1);

    [RelayCommand]
    private void DiaSeguinte() => Dia = Dia.AddDays(1);

    [RelayCommand]
    private void Hoje() => Dia = DateTime.Today;

    /// <summary>
    /// Chama o paciente para o atendimento: fixa o foco do posto e abre a tela de
    /// atendimento. É o único caminho em que a evolução nasce ligada ao AGENDAMENTO —
    /// e é esse vínculo que faz a sessão sair da lista de pendências depois de escrita.
    /// </summary>
    [RelayCommand]
    private void Atender(LinhaSessao? linha)
    {
        if (linha is null) return;

        _foco.Definir(linha.PacienteId, linha.Paciente, linha.AgendamentoId, linha.AtendimentoId);
        NavegacaoSuite.Ir(ModuloClinico.ChaveAtendimento);
    }

    /// <summary>Mesma coisa a partir da lista de pendências dos dias anteriores.</summary>
    [RelayCommand]
    private void EscreverPendente(LinhaRegistroPendente? linha)
    {
        if (linha is null) return;

        _foco.Definir(linha.PacienteId, linha.Paciente, linha.AgendamentoId);
        NavegacaoSuite.Ir(ModuloClinico.ChaveAtendimento);
    }

    /// <summary>Abre a curva de dor do paciente da linha, sem passar pelo atendimento.</summary>
    [RelayCommand]
    private void VerDor(LinhaSessao? linha)
    {
        if (linha is null) return;

        _foco.Definir(linha.PacienteId, linha.Paciente);
        NavegacaoSuite.Ir(ModuloClinico.ChaveEvolucaoDor);
    }
}
