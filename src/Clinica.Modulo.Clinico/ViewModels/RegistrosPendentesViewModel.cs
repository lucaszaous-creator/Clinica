using System.Collections.ObjectModel;
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
/// As sessões atendidas que continuam sem evolução escrita — a dívida de prontuário.
///
/// Por que é uma TELA, e não um bloco do "Meu dia"
/// -----------------------------------------------
/// Ela morava numa caixa de 180 px acima do quadro do dia, e numa base real são dezenas
/// de linhas: a caixa cortava um nome ao meio e a lista rolava dentro de um vão do
/// tamanho de três linhas. Pior do que feio, era a pergunta errada no lugar errado — o
/// "Meu dia" responde o que acontece HOJE, e esta lista é o que ficou de trás.
///
/// Vale a regra que o cliente reprovou duas vezes em voz alta: <b>lista longa merece a
/// largura inteira da tela</b>, e seção que não é sobre o que a tela é sobre vira porta,
/// não caixa. O "Meu dia" ficou com um botão que diz o tamanho da dívida e traz para cá.
/// </summary>
public sealed partial class RegistrosPendentesViewModel : ObservableObject
{
    private readonly IServiceScopeFactory _escopos;
    private readonly PacienteEmFoco _foco;

    public ObservableCollection<LinhaRegistroPendente> Pendentes { get; } = [];

    [ObservableProperty] private bool _carregando;

    [ObservableProperty] private string _resumo = string.Empty;

    /// <summary>A leitura FALHOU — nunca desenhar falha como "não há nada".</summary>
    [ObservableProperty] private bool _naoVerificado;

    [ObservableProperty] private string? _mensagem;
    [ObservableProperty] private bool _mensagemEhErro;

    /// <summary>Sem vínculo, a lista é da clínica inteira — e a tela diz isso.</summary>
    [ObservableProperty] private bool _semVinculo;

    public bool Vazio => Pendentes.Count == 0;

    public RegistrosPendentesViewModel(IServiceScopeFactory escopos, PacienteEmFoco foco)
    {
        _escopos = escopos;
        _foco = foco;
        _ = CarregarAsync();
    }

    [RelayCommand]
    public async Task CarregarAsync()
    {
        try
        {
            Carregando = true;
            NaoVerificado = false;
            Mensagem = null;
            MensagemEhErro = false;
            Pendentes.Clear();

            var profissionalId = SessaoUsuario.Atual.ProfissionalId;
            SemVinculo = profissionalId is null;

            var hoje = DateOnly.FromDateTime(DateTime.Today);

            using var scope = _escopos.CreateScope();
            var consultorio = scope.ServiceProvider.GetRequiredService<ConsultorioService>();

            var pendentes = await consultorio.RegistrosPendentesAsync(hoje, profissionalId);
            foreach (var p in pendentes) Pendentes.Add(LinhaRegistroPendente.De(p, hoje));

            Resumo = pendentes.Count == 0
                ? $"Nenhuma sessão dos últimos {ConsultorioService.JanelaRegistroPendenteDias} "
                  + "dias está sem evolução escrita."
                : $"{pendentes.Count} sessão(ões) atendida(s) nos últimos "
                  + $"{ConsultorioService.JanelaRegistroPendenteDias} dias continuam sem "
                  + "evolução escrita. A mais antiga primeiro.";

            OnPropertyChanged(nameof(Vazio));
        }
        catch (Exception ex)
        {
            NaoVerificado = true;
            Clinica.Application.Diagnostico.Registrar(
                "Consultório — sessões sem evolução não puderam ser carregadas", ex);
            Mensagem = ex.Message;
            MensagemEhErro = true;
        }
        finally
        {
            Carregando = false;
        }
    }

    /// <summary>
    /// Fixa o paciente no posto e abre o atendimento com o AGENDAMENTO da sessão — é esse
    /// vínculo que faz a linha sair daqui depois de a evolução ser escrita.
    /// </summary>
    [RelayCommand]
    private void Escrever(LinhaRegistroPendente? linha)
    {
        if (linha is null) return;

        _foco.Definir(linha.PacienteId, linha.Paciente, linha.AgendamentoId);
        NavegacaoSuite.Ir(ModuloClinico.ChaveAtendimento);
    }
}
