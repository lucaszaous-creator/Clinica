using Clinica.Application.Servicos;
using Clinica.Desktop.Shell;
using Clinica.Domain.Entities;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;

namespace Clinica.Recepcao.ViewModels;

/// <summary>
/// Formulário da sala. A <b>capacidade</b> é o campo que parece supérfluo e não é: é ela
/// que decide quando a agenda acusa choque de sala — numa sala com duas macas, dois
/// atendimentos no mesmo horário são rotina, não conflito.
/// </summary>
public sealed partial class SalaEdicaoViewModel : ObservableObject
{
    private readonly IServiceScopeFactory _escopos;
    private readonly int? _id;

    [ObservableProperty] private string _nome = string.Empty;
    [ObservableProperty] private string _capacidade = "1";
    [ObservableProperty] private string _ordem = "0";
    [ObservableProperty] private bool _ativa = true;
    [ObservableProperty] private string? _observacoes;

    [ObservableProperty] private string _titulo = "Nova sala";
    [ObservableProperty] private string _mensagem = string.Empty;
    [ObservableProperty] private bool _mensagemEhErro;
    [ObservableProperty] private bool _salvando;

    public event Action? Concluido;

    public SalaEdicaoViewModel(IServiceScopeFactory escopos, int? id = null)
    {
        _escopos = escopos;
        _id = id;
        _ = CarregarAsync();
    }

    private async Task CarregarAsync()
    {
        if (_id is null) return;

        try
        {
            using var scope = _escopos.CreateScope();
            var equipe = scope.ServiceProvider.GetRequiredService<EquipeService>();
            var s = await equipe.ObterSalaAsync(_id.Value);
            if (s is null) return;

            Titulo = "Editar sala";
            Nome = s.Nome;
            Capacidade = s.Capacidade.ToString();
            Ordem = s.Ordem.ToString();
            Ativa = s.Ativa;
            Observacoes = s.Observacoes;
        }
        catch (Exception ex)
        {
            Clinica.Application.Diagnostico.Registrar("Recepção — sala não pôde ser carregada", ex);
            Mensagem = $"Não foi possível carregar a sala: {ex.Message}";
            MensagemEhErro = true;
        }
    }

    [RelayCommand]
    private async Task SalvarAsync()
    {
        Mensagem = string.Empty;
        MensagemEhErro = false;

        if (string.IsNullOrWhiteSpace(Nome))
        {
            Erro("Informe o nome da sala.");
            return;
        }

        if (!int.TryParse(Capacidade, out var capacidade) || capacidade < 1)
        {
            Erro("A capacidade precisa ser um número maior ou igual a 1.");
            return;
        }

        if (!int.TryParse(Ordem, out var ordem)) ordem = 0;

        try
        {
            SessaoUsuario.Atual.Exigir(Permissao.GerenciarEquipe, "cadastrar sala");

            Salvando = true;
            using var scope = _escopos.CreateScope();
            var equipe = scope.ServiceProvider.GetRequiredService<EquipeService>();

            await equipe.SalvarSalaAsync(new Sala
            {
                Id = _id ?? 0,
                Nome = Nome,
                Capacidade = capacidade,
                Ordem = ordem,
                Ativa = Ativa,
                Observacoes = Observacoes
            });

            Concluido?.Invoke();
        }
        catch (Exception ex)
        {
            Clinica.Application.Diagnostico.Registrar("Recepção — sala não pôde ser salva", ex);
            Erro(ex.Message);
        }
        finally
        {
            Salvando = false;
        }
    }

    private void Erro(string texto)
    {
        Mensagem = texto;
        MensagemEhErro = true;
    }
}
