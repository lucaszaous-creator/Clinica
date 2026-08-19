using System.Collections.ObjectModel;
using Clinica.Application.Servicos;
using Clinica.Desktop.Shell;
using Clinica.Domain.Entities;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;

namespace Clinica.Recepcao.ViewModels;

/// <summary>
/// Fechar a agenda: férias, feriado, congresso, folga, sala em manutenção.
///
/// **Sem profissional e sem sala = a clínica inteira** (feriado). É o padrão da janela
/// porque é o caso que mais aparece — e porque escolher errado aqui é o erro mais caro:
/// um feriado cadastrado como folga de uma pessoa deixa a agenda dos outros aberta.
///
/// **Bloquear não desmarca ninguém.** Quem já estava marcado dentro do período aparece
/// no resultado, para a recepção remarcar com o telefone na mão — o paciente combinou
/// aquele horário com uma pessoa, e quem desmarca avisa.
/// </summary>
public sealed partial class BloqueioEdicaoViewModel : ObservableObject
{
    private readonly IServiceScopeFactory _escopos;

    public ObservableCollection<Profissional> Profissionais { get; } = [];
    public ObservableCollection<Sala> Salas { get; } = [];

    /// <summary>Quem já estava marcado dentro do período — lido depois de salvar.</summary>
    public List<string> MarcadosDentro { get; } = [];

    [ObservableProperty] private DateTime _inicioDia = DateTime.Today;
    [ObservableProperty] private string _inicioHora = "08:00";
    [ObservableProperty] private DateTime _fimDia = DateTime.Today;
    [ObservableProperty] private string _fimHora = "18:00";
    [ObservableProperty] private bool _diaInteiro = true;

    [ObservableProperty] private Profissional? _profissional;
    [ObservableProperty] private Sala? _sala;
    [ObservableProperty] private string? _motivo;

    [ObservableProperty] private string _mensagem = string.Empty;
    [ObservableProperty] private bool _mensagemEhErro;
    [ObservableProperty] private bool _salvando;

    public event Action? Concluido;

    public BloqueioEdicaoViewModel(IServiceScopeFactory escopos)
    {
        _escopos = escopos;
        _ = CarregarAsync();
    }

    /// <summary>Dia inteiro é o caso comum (férias, feriado): a hora vira 00:00–23:59.</summary>
    partial void OnDiaInteiroChanged(bool value)
    {
        if (!value) return;
        InicioHora = "00:00";
        FimHora = "23:59";
    }

    private async Task CarregarAsync()
    {
        try
        {
            using var scope = _escopos.CreateScope();
            var equipe = scope.ServiceProvider.GetRequiredService<EquipeService>();

            Profissionais.Clear();
            foreach (var p in await equipe.ProfissionaisAtivosAsync()) Profissionais.Add(p);

            Salas.Clear();
            foreach (var s in await equipe.SalasAsync()) if (s.Ativa) Salas.Add(s);
        }
        catch (Exception ex)
        {
            Clinica.Application.Diagnostico.Registrar(
                "Recepção — equipe não pôde ser carregada para o bloqueio", ex);
            Erro(ex.Message);
        }
    }

    [RelayCommand]
    private async Task SalvarAsync()
    {
        Mensagem = string.Empty;
        MensagemEhErro = false;

        if (string.IsNullOrWhiteSpace(Motivo))
        {
            Erro("Diga por que a agenda está fechada — bloqueio sem motivo vira mistério "
                 + "na agenda do mês que vem.");
            return;
        }

        if (!TentarMontar(InicioDia, InicioHora, out var inicio)
            || !TentarMontar(FimDia, FimHora, out var fim))
        {
            Erro("Horário inválido. Use HH:mm (ex.: 08:00).");
            return;
        }

        if (fim <= inicio)
        {
            Erro("O fim do bloqueio precisa ser depois do início.");
            return;
        }

        try
        {
            Salvando = true;
            // ⚠️ `EditarAgenda` OU `GerenciarEquipe` — e são DOIS bits porque são DUAS
            // PORTAS. Esta janela abre pela barra da Agenda (balcão, `EditarAgenda`) e
            // pela tela de Profissionais e salas (direção, `GerenciarEquipe`). Enquanto
            // o Salvar exigiu só o segundo, a recepcionista atravessava a primeira porta,
            // preenchia o feriado inteiro e levava a recusa no clique final — a metade
            // que EXPLICA dizia sim e a que IMPEDE dizia não, sobre o mesmo ato.
            SessaoUsuario.Atual.ExigirAlgum(
                Permissao.EditarAgenda | Permissao.GerenciarEquipe, "fechar a agenda");

            using var scope = _escopos.CreateScope();
            var bloqueios = scope.ServiceProvider.GetRequiredService<BloqueioAgendaService>();

            var resultado = await bloqueios.CriarAsync(
                inicio, fim, Motivo!,
                profissionalId: Profissional?.Id,
                salaId: Sala?.Id,
                operador: SessaoUsuario.Atual.Operador);

            MarcadosDentro.Clear();
            MarcadosDentro.AddRange(resultado.JaMarcados.Select(a =>
                $"{a.DataHora:dd/MM HH:mm} — {a.Paciente?.Nome ?? "(paciente removido)"}"
                + (a.Profissional?.Rotulo is { } quem ? $" com {quem}" : string.Empty)));

            Concluido?.Invoke();
        }
        catch (Exception ex)
        {
            Clinica.Application.Diagnostico.Registrar(
                "Recepção — agenda não pôde ser fechada", ex);
            Erro(ex.Message);
        }
        finally
        {
            Salvando = false;
        }
    }

    private static bool TentarMontar(DateTime dia, string hora, out DateTime quando)
    {
        quando = default;
        if (!TimeOnly.TryParse(hora, out var h)) return false;
        quando = dia.Date.Add(h.ToTimeSpan());
        return true;
    }

    private void Erro(string texto)
    {
        Mensagem = texto;
        MensagemEhErro = true;
    }
}
