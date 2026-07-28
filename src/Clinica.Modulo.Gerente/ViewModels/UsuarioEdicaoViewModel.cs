using System.Collections.ObjectModel;
using Clinica.Application.Servicos;
using Clinica.Desktop.Shell;
using Clinica.Domain.Entities;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;

namespace Clinica.Gerente.ViewModels;

/// <summary>Uma permissão na tela, marcada ou não para este usuário.</summary>
public sealed partial class ItemPermissao : ObservableObject
{
    public required Permissao Valor { get; init; }
    public required string Rotulo { get; init; }

    /// <summary>Vem marcada por causa do perfil? Serve para a tela explicar de onde veio.</summary>
    [ObservableProperty] private bool _doPerfil;

    [ObservableProperty] private bool _marcada;
}

/// <summary>Opção de profissional para vincular ao usuário.</summary>
public sealed record OpcaoProfissional(int? Id, string Nome);

/// <summary>Perfil na combo, com o rótulo em português ("Gerente Geral", não "Gerente").</summary>
public sealed record OpcaoPerfil(PerfilAcesso Valor, string Rotulo);

/// <summary>
/// Cadastro de um usuário da suíte.
///
/// A tela mostra o que a pessoa PODE FAZER, não "extras e negadas": quem administra
/// pensa em "a Ana lança caixa?", não em delta contra o perfil. O delta é calculado na
/// gravação (marcada fora do perfil vira extra; desmarcada dentro do perfil vira
/// negada) — assim corrigir o padrão de um perfil continua alcançando todo mundo.
/// </summary>
public sealed partial class UsuarioEdicaoViewModel : ObservableObject
{
    private readonly IServiceScopeFactory _escopos;
    private readonly SessaoUsuario _sessao;
    private readonly int? _usuarioId;

    public ObservableCollection<ItemPermissao> Permissoes { get; } = [];
    public ObservableCollection<OpcaoProfissional> Profissionais { get; } = [];

    public IReadOnlyList<OpcaoPerfil> Perfis { get; } = Enum.GetValues<PerfilAcesso>()
        .Select(p => new OpcaoPerfil(p, PerfisAcesso.Rotular(p)))
        .ToList();

    public bool EhNovo => _usuarioId is null;
    public string Titulo => EhNovo ? "Novo usuário" : "Editar usuário";

    [ObservableProperty] private string _nome = string.Empty;
    [ObservableProperty] private string _login = string.Empty;
    [ObservableProperty] private OpcaoPerfil? _perfilSelecionado;
    [ObservableProperty] private OpcaoProfissional? _profissional;
    [ObservableProperty] private bool _ativo = true;
    [ObservableProperty] private string _senha = string.Empty;
    [ObservableProperty] private bool _deveTrocarSenha = true;
    [ObservableProperty] private string _mensagem = string.Empty;
    [ObservableProperty] private bool _mensagemEhErro;
    [ObservableProperty] private bool _salvando;

    /// <summary>Gravou: a janela se fecha por aqui (mesmo padrão dos outros cadastros da suíte).</summary>
    public event Action? Concluido;

    /// <summary>Perfil escolhido na tela (Recepção enquanto a combo não carregou).</summary>
    public PerfilAcesso Perfil => PerfilSelecionado?.Valor ?? PerfilAcesso.Recepcao;

    public UsuarioEdicaoViewModel(IServiceScopeFactory escopos, SessaoUsuario sessao, int? usuarioId = null)
    {
        _escopos = escopos;
        _sessao = sessao;
        _usuarioId = usuarioId;

        foreach (var p in PerfisAcesso.Individuais)
            Permissoes.Add(new ItemPermissao { Valor = p, Rotulo = PerfisAcesso.Rotular(p) });

        PerfilSelecionado = Perfis.First(o => o.Valor == PerfilAcesso.Recepcao);

        _ = CarregarAsync();
    }

    private async Task CarregarAsync()
    {
        try
        {
            using var scope = _escopos.CreateScope();
            var equipe = scope.ServiceProvider.GetRequiredService<EquipeService>();

            Profissionais.Clear();
            Profissionais.Add(new OpcaoProfissional(null, "— nenhum —"));
            foreach (var p in await equipe.ProfissionaisAsync())
                if (p.Ativo) Profissionais.Add(new OpcaoProfissional(p.Id, p.Nome));

            if (_usuarioId is { } id)
            {
                var acesso = scope.ServiceProvider.GetRequiredService<AcessoService>();
                var usuario = await acesso.ObterAsync(id);
                if (usuario is null)
                {
                    Mensagem = "Usuário não encontrado.";
                    MensagemEhErro = true;
                    return;
                }

                Nome = usuario.Nome;
                Login = usuario.Login;
                PerfilSelecionado = Perfis.First(o => o.Valor == usuario.Perfil);
                Ativo = usuario.Ativo;
                Profissional = Profissionais.FirstOrDefault(o => o.Id == usuario.ProfissionalId)
                               ?? Profissionais[0];
                AplicarPermissoes(usuario.Efetivas);
            }
            else
            {
                Profissional = Profissionais[0];
                AplicarPermissoes(PerfisAcesso.Padrao(Perfil));
            }
        }
        catch (Exception ex)
        {
            Clinica.Application.Diagnostico.Registrar("Gerente — cadastro de usuário não pôde ser aberto", ex);
            Mensagem = $"Não foi possível abrir o cadastro: {ex.Message}";
            MensagemEhErro = true;
        }
    }

    /// <summary>Trocar o perfil repõe as marcações do novo padrão — o ajuste fino vem depois.</summary>
    partial void OnPerfilSelecionadoChanged(OpcaoPerfil? value)
    {
        if (value is null) return;
        AplicarPermissoes(PerfisAcesso.Padrao(value.Valor));
    }

    private void AplicarPermissoes(Permissao efetivas)
    {
        var padrao = PerfisAcesso.Padrao(Perfil);
        foreach (var item in Permissoes)
        {
            item.DoPerfil = (padrao & item.Valor) == item.Valor;
            item.Marcada = (efetivas & item.Valor) == item.Valor;
        }
    }

    [RelayCommand]
    private async Task SalvarAsync()
    {
        try
        {
            Salvando = true;
            Mensagem = string.Empty;
            MensagemEhErro = false;

            var padrao = PerfisAcesso.Padrao(Perfil);
            var extras = Permissao.Nenhuma;
            var negadas = Permissao.Nenhuma;
            foreach (var item in Permissoes)
            {
                var noPadrao = (padrao & item.Valor) == item.Valor;
                if (item.Marcada && !noPadrao) extras |= item.Valor;
                if (!item.Marcada && noPadrao) negadas |= item.Valor;
            }

            using var scope = _escopos.CreateScope();
            var acesso = scope.ServiceProvider.GetRequiredService<AcessoService>();

            if (_usuarioId is { } id)
            {
                await acesso.AtualizarAsync(
                    id, Nome, Perfil, Profissional?.Id, extras, negadas, Ativo, _sessao.Operador);

                // Senha em branco na edição significa "não mexer" — obrigar a redigitar
                // faria a direção trocar a senha de alguém sem querer, em toda edição.
                if (!string.IsNullOrEmpty(Senha))
                    await acesso.DefinirSenhaAsync(id, Senha, DeveTrocarSenha, _sessao.Operador);
            }
            else
            {
                var novo = await acesso.CriarAsync(
                    Nome, Login, Senha, Perfil, Profissional?.Id, _sessao.Operador, DeveTrocarSenha);

                if (extras != Permissao.Nenhuma || negadas != Permissao.Nenhuma)
                    await acesso.AtualizarAsync(
                        novo.Id, Nome, Perfil, Profissional?.Id, extras, negadas, Ativo, _sessao.Operador);
            }

            Concluido?.Invoke();
        }
        catch (Exception ex)
        {
            Clinica.Application.Diagnostico.Registrar("Gerente — usuário não pôde ser salvo", ex);
            Mensagem = ex.Message;
            MensagemEhErro = true;
        }
        finally
        {
            Salvando = false;
        }
    }
}
