using System.Collections.ObjectModel;
using Clinica.Application.Servicos;
using Clinica.Desktop.Controls;
using Clinica.Desktop.Shell;
using Clinica.Domain.Entities;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;

namespace Clinica.Recepcao.ViewModels;

/// <summary>Uma linha do cadastro de profissionais.</summary>
public sealed class LinhaProfissional
{
    public required int Id { get; init; }
    public required string Nome { get; init; }
    public required string Registro { get; init; }
    public required string Duracao { get; init; }
    public required string Situacao { get; init; }
    public required bool Ativo { get; init; }
}

/// <summary>Uma linha do cadastro de salas.</summary>
public sealed class LinhaSala
{
    public required int Id { get; init; }
    public required string Nome { get; init; }
    public required string Capacidade { get; init; }
    public required string Situacao { get; init; }
    public required bool Ativa { get; init; }
}

/// <summary>
/// Cadastro da equipe: quem atende e onde. É a tela que destrava tudo o mais — agenda
/// por profissional, repasse no financeiro e produtividade no BI se apoiam nela.
///
/// Excluir só vale enquanto o registro nunca foi usado; depois disso o serviço recusa e
/// o caminho é DESATIVAR, para a agenda do passado continuar dizendo quem atendeu.
/// </summary>
public sealed partial class EquipeViewModel : ObservableObject
{
    private readonly IServiceScopeFactory _escopos;
    private readonly ISnackbarService _snackbar;
    private readonly IDialogoService _dialogo;

    public ObservableCollection<LinhaProfissional> Profissionais { get; } = [];
    public ObservableCollection<LinhaSala> Salas { get; } = [];

    [ObservableProperty] private bool _carregando;
    [ObservableProperty] private string _mensagem = string.Empty;
    [ObservableProperty] private bool _mensagemEhErro;

    /// <summary>
    /// Habilita os botões de escrita da tela. É a metade VISÍVEL da permissão: o
    /// botão apagado explica por que não dá; a guarda no comando é que impede.
    /// Só desabilitar seria enfeite — um atalho de teclado passaria direto.
    /// </summary>
    public bool PodeGerenciarEquipe => SessaoUsuario.Atual.Pode(Permissao.GerenciarEquipe);

    public EquipeViewModel(
        IServiceScopeFactory escopos, ISnackbarService snackbar, IDialogoService dialogo)
    {
        _escopos = escopos;
        _snackbar = snackbar;
        _dialogo = dialogo;
        _ = CarregarAsync();
    }

    [RelayCommand]
    public async Task CarregarAsync()
    {
        try
        {
            Carregando = true;
            Mensagem = string.Empty;
            MensagemEhErro = false;

            using var scope = _escopos.CreateScope();
            var equipe = scope.ServiceProvider.GetRequiredService<EquipeService>();

            Profissionais.Clear();
            foreach (var p in await equipe.ProfissionaisAsync())
                Profissionais.Add(new LinhaProfissional
                {
                    Id = p.Id,
                    Nome = p.Nome,
                    Registro = string.IsNullOrWhiteSpace(p.RegistroConselho) ? "—" : p.RegistroConselho!,
                    Duracao = p.DuracaoPadraoMinutos is { } d ? $"{d} min" : "padrão da clínica",
                    Situacao = p.Ativo ? "Ativo" : "Inativo",
                    Ativo = p.Ativo
                });

            Salas.Clear();
            foreach (var s in await equipe.SalasAsync())
                Salas.Add(new LinhaSala
                {
                    Id = s.Id,
                    Nome = s.Nome,
                    Capacidade = s.Capacidade == 1 ? "1 atendimento" : $"{s.Capacidade} simultâneos",
                    Situacao = s.Ativa ? "Ativa" : "Inativa",
                    Ativa = s.Ativa
                });
        }
        catch (Exception ex)
        {
            Clinica.Application.Diagnostico.Registrar("Recepção — equipe não pôde ser carregada", ex);
            Mensagem = $"Não foi possível carregar a equipe: {ex.Message}";
            MensagemEhErro = true;
        }
        finally
        {
            Carregando = false;
        }
    }

    [RelayCommand]
    private async Task NovoProfissionalAsync() => await AbrirProfissionalAsync(null);

    [RelayCommand]
    private async Task EditarProfissionalAsync(LinhaProfissional? linha)
    {
        if (linha is null) return;
        await AbrirProfissionalAsync(linha.Id);
    }

    [RelayCommand]
    private async Task ExcluirProfissionalAsync(LinhaProfissional? linha)
    {
        if (linha is null) return;

        SessaoUsuario.Atual.Exigir(Permissao.GerenciarEquipe, "mexer no cadastro da equipe");
        if (!_dialogo.ConfirmarPerigo("Excluir profissional",
                $"Excluir {linha.Nome}? Só é possível enquanto ele não tem agenda registrada — "
                + "se já tiver, desative-o em vez de excluir.")) return;

        await ExecutarAsync(async equipe =>
        {
            await equipe.ExcluirProfissionalAsync(linha.Id);
            _snackbar.Sucesso("Profissional excluído.");
        });
    }

    [RelayCommand]
    private async Task NovaSalaAsync() => await AbrirSalaAsync(null);

    [RelayCommand]
    private async Task EditarSalaAsync(LinhaSala? linha)
    {
        if (linha is null) return;
        await AbrirSalaAsync(linha.Id);
    }

    [RelayCommand]
    private async Task ExcluirSalaAsync(LinhaSala? linha)
    {
        if (linha is null) return;

        SessaoUsuario.Atual.Exigir(Permissao.GerenciarEquipe, "mexer no cadastro da equipe");
        if (!_dialogo.ConfirmarPerigo("Excluir sala",
                $"Excluir a sala {linha.Nome}? Só é possível enquanto ela não tem agenda registrada.")) return;

        await ExecutarAsync(async equipe =>
        {
            await equipe.ExcluirSalaAsync(linha.Id);
            _snackbar.Sucesso("Sala excluída.");
        });
    }

    private async Task AbrirProfissionalAsync(int? id)
    {
        SessaoUsuario.Atual.Exigir(Permissao.GerenciarEquipe, "mexer no cadastro da equipe");
        var vm = new ProfissionalEdicaoViewModel(_escopos, id);
        var janela = new Janelas.ProfissionalWindow(vm)
        {
            Owner = System.Windows.Application.Current?.MainWindow
        };

        if (janela.ShowDialog() != true) return;
        _snackbar.Sucesso("Profissional salvo.");
        await CarregarAsync();
    }

    private async Task AbrirSalaAsync(int? id)
    {
        SessaoUsuario.Atual.Exigir(Permissao.GerenciarEquipe, "mexer no cadastro da equipe");
        var vm = new SalaEdicaoViewModel(_escopos, id);
        var janela = new Janelas.SalaWindow(vm)
        {
            Owner = System.Windows.Application.Current?.MainWindow
        };

        if (janela.ShowDialog() != true) return;
        _snackbar.Sucesso("Sala salva.");
        await CarregarAsync();
    }

    private async Task ExecutarAsync(Func<EquipeService, Task> acao)
    {
        try
        {
            using var scope = _escopos.CreateScope();
            await acao(scope.ServiceProvider.GetRequiredService<EquipeService>());
            await CarregarAsync();
        }
        catch (Exception ex)
        {
            Clinica.Application.Diagnostico.Registrar("Recepção — operação na equipe falhou", ex);
            Mensagem = ex.Message;
            MensagemEhErro = true;
        }
    }
}
