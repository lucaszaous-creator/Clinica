using System.Collections.ObjectModel;
using Clinica.Application.Servicos;
using Clinica.Desktop.Controls;
using Clinica.Domain.Entities;
using Clinica.Desktop.Alertas;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;

namespace Clinica.Desktop.ViewModels;

/// <summary>Uma linha do cadastro de usuários.</summary>
public sealed class LinhaUsuario
{
    public required int Id { get; init; }
    public required string Nome { get; init; }
    public required string Login { get; init; }
    public required string Perfil { get; init; }
    public required string Profissional { get; init; }
    public required string Situacao { get; init; }
    public required string UltimoAcesso { get; init; }
    public required bool Ativo { get; init; }
}

/// <summary>
/// Usuários e permissões da suíte (feature 13).
///
/// Excluir só vale enquanto o usuário nunca entrou; depois disso o caminho é
/// DESATIVAR — é a mesma regra do cadastro de profissionais, e pelo mesmo motivo: a
/// auditoria precisa continuar dizendo quem era quem nas ações antigas.
/// </summary>
public sealed partial class AcessosViewModel : ObservableObject
{
    private readonly IServiceScopeFactory _escopos;
    private readonly ISnackbarService _snackbar;
    private readonly IDialogoService _dialogo;
    private readonly SessaoUsuario _sessao;

    public ObservableCollection<LinhaUsuario> Usuarios { get; } = [];

    [ObservableProperty] private bool _carregando;

    /// <summary>
    /// A leitura FALHOU — o terceiro estado. Sem ele, lista vazia por erro fica idêntica
    /// a lista vazia por não haver nada.
    /// </summary>
    [ObservableProperty] private bool _naoVerificado;
    [ObservableProperty] private string _mensagem = string.Empty;
    [ObservableProperty] private bool _mensagemEhErro;

    public AcessosViewModel(
        IServiceScopeFactory escopos, ISnackbarService snackbar,
        IDialogoService dialogo, SessaoUsuario sessao)
    {
        _escopos = escopos;
        _snackbar = snackbar;
        _dialogo = dialogo;
        _sessao = sessao;
        _ = CarregarAsync();
    }

    [RelayCommand]
    public async Task CarregarAsync()
    {
        try
        {
            Carregando = true;
            NaoVerificado = false;
            Mensagem = string.Empty;
            MensagemEhErro = false;

            using var scope = _escopos.CreateScope();
            var acesso = scope.ServiceProvider.GetRequiredService<AcessoService>();

            Usuarios.Clear();
            foreach (var u in await acesso.UsuariosAsync())
                Usuarios.Add(new LinhaUsuario
                {
                    Id = u.Id,
                    Nome = u.Nome,
                    Login = u.Login,
                    Perfil = PerfisAcesso.Rotular(u.Perfil),
                    Profissional = u.Profissional?.Nome ?? "—",
                    Situacao = u.Ativo ? "Ativo" : "Inativo",
                    UltimoAcesso = u.UltimoAcessoEm is { } quando
                        ? quando.ToString("dd/MM/yyyy HH:mm")
                        : "nunca entrou",
                    Ativo = u.Ativo
                });

            if (Usuarios.Count == 0)
                Mensagem = "Nenhum usuário cadastrado.";
        }
        catch (Exception ex)
        {
            NaoVerificado = true;
            Clinica.Application.Diagnostico.Registrar("Faturamento — usuários não puderam ser carregados", ex);
            Mensagem = $"Não foi possível carregar os usuários: {ex.Message}";
            MensagemEhErro = true;
        }
        finally
        {
            Carregando = false;
        }
    }

    [RelayCommand]
    private async Task NovoAsync()
    {
        if (!PodeGerenciar("criar usuário")) return;
        await AbrirAsync(null);
    }

    [RelayCommand]
    private async Task EditarAsync(LinhaUsuario? linha)
    {
        if (linha is null) return;
        if (!PodeGerenciar("editar usuário e permissões")) return;
        await AbrirAsync(linha.Id);
    }

    private async Task AbrirAsync(int? usuarioId)
    {
        var vm = new UsuarioEdicaoViewModel(_escopos, _sessao, usuarioId);
        var janela = new UsuarioWindow(vm)
        {
            // Qualificado: dentro de Clinica.*, "Application" é o namespace Clinica.Application.
            Owner = System.Windows.Application.Current?.MainWindow
        };

        if (janela.ShowDialog() == true)
        {
            _snackbar.Sucesso("Usuário salvo.");
            await CarregarAsync();
        }
    }

    [RelayCommand]
    private async Task ExcluirAsync(LinhaUsuario? linha)
    {
        if (linha is null) return;
        if (!PodeGerenciar("excluir usuário")) return;
        if (!_dialogo.ConfirmarPerigo("Excluir usuário",
                $"Excluir {linha.Nome}? Só é possível enquanto ele nunca entrou no sistema — "
                + "se já entrou, desative-o em vez de excluir.")) return;

        await ExecutarAsync(async acesso =>
        {
            await acesso.ExcluirAsync(linha.Id, _sessao.Operador);
            _snackbar.Sucesso("Usuário excluído.");
        });
    }

    /// <summary>Redefine a senha de alguém (o usuário troca no próximo acesso).</summary>
    [RelayCommand]
    private async Task RedefinirSenhaAsync(LinhaUsuario? linha)
    {
        if (linha is null) return;
        if (!PodeGerenciar("redefinir senha")) return;

        var senha = _dialogo.PerguntarTexto(
            "Redefinir senha",
            $"Senha provisória para {linha.Nome}. Ele será obrigado a trocá-la ao entrar.");
        if (senha is null) return;

        await ExecutarAsync(async acesso =>
        {
            await acesso.DefinirSenhaAsync(linha.Id, senha, deveTrocar: true, _sessao.Operador);
            _snackbar.Sucesso("Senha provisória definida.");
        });
    }

    /// <summary>
    /// A SEGUNDA barreira. A primeira é o item da sidebar, que só aparece para quem tem
    /// <see cref="Permissao.GerenciarUsuarios"/> — na prática, o perfil Gerente, único que
    /// o recebe por padrão.
    ///
    /// Esconder o item nunca basta sozinho, e aqui menos que em qualquer outra tela: quem
    /// mexe em permissão pode CONCEDER permissão, inclusive a si mesmo. Uma pesquisa
    /// global, um atalho ou um comando disparado por outro caminho chegariam à tela com o
    /// menu filtrado, e sem esta guarda a gravação passaria.
    ///
    /// Ela IMPEDE e DIZ por quê: `Exigir` continua sendo o ponto único que decide, e a
    /// mensagem dele vai para a faixa inline em vez de subir como exceção não tratada.
    /// Guarda que volta calada é botão que não faz nada — a lição da parcela 41.
    /// </summary>
    private bool PodeGerenciar(string acao)
    {
        try
        {
            _sessao.Exigir(Permissao.GerenciarUsuarios, acao);
            return true;
        }
        catch (Exception ex)
        {
            Mensagem = ex.Message;
            MensagemEhErro = true;
            return false;
        }
    }

    private async Task ExecutarAsync(Func<AcessoService, Task> acao)
    {
        try
        {
            Carregando = true;
            using (var scope = _escopos.CreateScope())
                await acao(scope.ServiceProvider.GetRequiredService<AcessoService>());

            await CarregarAsync();
        }
        catch (Exception ex)
        {
            Clinica.Application.Diagnostico.Registrar("Faturamento — ação de acesso falhou", ex);
            Mensagem = ex.Message;
            MensagemEhErro = true;
        }
        finally
        {
            Carregando = false;
        }
    }
}
