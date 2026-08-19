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

/// <summary>O cabeçalho de uma coluna da grade: o dia, com a contagem dele.</summary>
public sealed class CabecalhoDia
{
    public required string Titulo { get; init; }
    public required string Subtitulo { get; init; }
    public required bool EhHoje { get; init; }

    /// <summary>"3 sessão(ões)" — ou "—" no dia vazio, que continua tendo coluna.</summary>
    public required string Resumo { get; init; }
}

/// <summary>
/// A SEMANA de quem atende (parcela 39; virou GRADE na 69).
///
/// "Meu dia" responde o que acontece hoje. Ele não responde as perguntas que se fazem
/// <b>com o paciente ainda na frente</b>, no fim da consulta: "quando eu tenho espaço?",
/// "vale marcar o seu retorno para sexta?", "quinta está cheia?".
///
/// A primeira versão era uma PILHA de cartões por dia — o desenho que a parcela 58
/// condenou no balcão: empilhados, o das 9h e o das 15h ficam colados, e "quando cabe?"
/// só se responde lendo cartão por cartão. Agora é a mesma LINHA DO TEMPO da agenda da
/// recepção (o vazio tem tamanho, a sessão longa cobre as faixas dela, cancelado fica
/// marcado sem cobrir nada), com os BLOQUEIOS escritos — férias e feriado eram invisíveis
/// aqui, e o retorno era combinado para um dia em que ninguém atende.
///
/// A tela é de LEITURA e leva à ação: ela não marca horário (quem marca é o balcão, com o
/// telefone na mão e a agenda de todo mundo à vista) — ela mostra a carga da semana e
/// abre o paciente de qualquer sessão. O montador da grade é puro e mora na Application
/// (<see cref="GradeSemana"/>), porque a camada de tela não compila nos testes.
/// </summary>
public sealed partial class MinhaSemanaViewModel : ObservableObject
{
    private readonly IServiceScopeFactory _escopos;
    private readonly PacienteEmFoco _foco;

    /// <summary>As linhas da grade — uma faixa de meia hora, com uma célula por dia.</summary>
    public ObservableCollection<FaixaSemana> Faixas { get; } = [];

    /// <summary>Os sete cabeçalhos, no prumo das células (UniformGrid dos dois lados).</summary>
    public ObservableCollection<CabecalhoDia> Cabecalhos { get; } = [];

    [ObservableProperty] private DateTime _referencia = DateTime.Today;

    [ObservableProperty] private string _profissional = string.Empty;

    [ObservableProperty] private string _periodo = string.Empty;

    [ObservableProperty] private string _resumo = string.Empty;

    [ObservableProperty] private bool _carregando;
    [ObservableProperty] private bool _naoVerificado;
    [ObservableProperty] private string? _mensagem;
    [ObservableProperty] private bool _mensagemEhErro;
    [ObservableProperty] private bool _semVinculo;

    /// <summary>A semana não tem uma sessão sequer — é o que liga o estado vazio.</summary>
    [ObservableProperty] private bool _vazio = true;

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

            // Os bloqueios entram na MESMA carga: férias e feriado eram invisíveis aqui,
            // e o retorno era combinado para um dia em que ninguém atende. Falhar não
            // derruba a semana — a grade sai sem os avisos de fechamento, que é o
            // comportamento de antes —, mas não passa calado (a regra da degradação).
            IReadOnlyList<BloqueioAgenda> bloqueios;
            try
            {
                bloqueios = await scope.ServiceProvider
                    .GetRequiredService<BloqueioAgendaService>()
                    .NoPeriodoAsync(
                        semana.Inicio.ToDateTime(TimeOnly.MinValue),
                        semana.Fim.AddDays(1).ToDateTime(TimeOnly.MinValue));
            }
            catch (Exception ex)
            {
                bloqueios = [];
                Clinica.Application.Diagnostico.Registrar(
                    "Consultório — bloqueios da semana não puderam ser lidos", ex);
            }
            if (geracao != _geracaoCarga) return;

            Profissional = semana.ProfissionalNome;
            Periodo = $"{semana.Inicio:dd/MM} a {semana.Fim:dd/MM/yyyy}";

            var hoje = DateOnly.FromDateTime(DateTime.Today);
            var grade = GradeSemana.Montar(semana, bloqueios, DateTime.Now);

            // Monta e só ENTÃO publica: entre o Clear e o último Add não pode haver await
            // (parcela 62) — a grade ficava vazia na tela durante o roundtrip ao banco.
            Cabecalhos.Clear();
            foreach (var dia in semana.Dias)
            {
                Cabecalhos.Add(new CabecalhoDia
                {
                    Titulo = NomeDoDia(dia.Dia.DayOfWeek),
                    Subtitulo = dia.Dia.ToString("dd/MM"),
                    EhHoje = dia.Dia == hoje,
                    Resumo = dia.Sessoes.Count == 0 ? "—" : $"{dia.Sessoes.Count} sessão(ões)"
                });
            }

            Faixas.Clear();
            foreach (var f in grade.Faixas) Faixas.Add(f);

            Vazio = semana.Sessoes == 0;

            Resumo = semana.Sessoes == 0
                ? "Nenhum horário marcado nesta semana."
                : MontarResumo(semana);
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

        // "NESTA semana", e não "sem evolução" solto: o botão do Meu dia conta a fila de
        // trabalho (30 dias para trás, sem hoje) e este número conta a SEMANA exibida —
        // são perguntas diferentes, e dois números com a mesma frase se leem como o mesmo
        // número errado (parcela 69).
        if (semana.RegistrosPendentes > 0)
            partes.Add($"{semana.RegistrosPendentes} sem evolução nesta semana");

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

    /// <summary>Abre o paciente da sessão — a semana é leitura, e daqui se entra no caso.</summary>
    [RelayCommand]
    private void Abrir(SessaoDoDia? sessao)
    {
        if (sessao is null) return;

        // A tela do paciente exige VerProntuario, e sem o bit ela nem existe na navegação
        // — NavegacaoSuite.Ir voltaria false EM SILÊNCIO e o clique não faria nada. A
        // guarda diz por quê (parcela 41).
        if (!SessaoUsuario.Atual.Pode(Permissao.VerProntuario))
        {
            Mensagem = "Abrir o paciente mostra o prontuário, e o seu acesso não tem essa "
                       + "permissão. A direção libera em Acessos.";
            MensagemEhErro = true;
            return;
        }

        _foco.Definir(sessao.PacienteId, sessao.PacienteNome, sessao.AgendamentoId,
                      dataDoHorario: DateOnly.FromDateTime(sessao.DataHora));
        NavegacaoSuite.Ir(ModuloClinico.ChavePaciente);
    }
}
