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

    /// <summary>
    /// CPF do PROFISSIONAL vinculado — editado aqui, gravado lá.
    ///
    /// Por que aparece na tela de acesso, se o dono do dado é o profissional
    /// ----------------------------------------------------------------------
    /// Porque é aqui que a direção cadastra quem vai usar o sistema, e é o CPF que amarra o
    /// certificado digital à pessoa. Sem este campo, dar acesso a alguém que assina exigia
    /// abrir OUTRA tela (Equipe → editar profissional) para preencher um dado sem o qual a
    /// assinatura recusa — um passeio que ninguém faz porque nada na tela de acesso diz que
    /// ele é necessário.
    ///
    /// O que NÃO se fez, e por quê: copiar o CPF para <c>UsuarioSistema</c>. A assinatura lê
    /// <c>documento.Profissional.Cpf</c>, porque o documento aponta para o profissional — se
    /// o CPF morasse nos dois lugares eles divergiriam no primeiro cadastro corrigido pela
    /// metade, e haveria duas respostas para "de quem é este certificado". É a mesma razão
    /// pela qual o usuário aponta para o profissional em vez de copiar o nome dele.
    /// </summary>
    [ObservableProperty] private string? _cpfProfissional;

    /// <summary>Só há onde gravar o CPF quando há profissional vinculado.</summary>
    public bool PodeEditarCpf => Profissional?.Id is not null;

    /// <summary>Explica a ausência do campo em vez de deixá-lo aceso e sem efeito.</summary>
    public string CpfDica => PodeEditarCpf
        ? "Necessário para assinar com certificado digital: o sistema compara este CPF com o que está dentro do certificado. Em branco, esta pessoa não assina."
        : "Vincule um profissional acima para poder cadastrar o CPF de assinatura.";
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

    partial void OnProfissionalChanged(OpcaoProfissional? value)
    {
        OnPropertyChanged(nameof(PodeEditarCpf));
        OnPropertyChanged(nameof(CpfDica));
        _ = CarregarCpfAsync();
    }

    /// <summary>
    /// Traz o CPF do profissional escolhido. Sem isto, trocar o vínculo deixaria na tela o
    /// CPF de OUTRA pessoa — que o salvar gravaria por cima, trocando o CPF de quem assina.
    /// </summary>
    private async Task CarregarCpfAsync()
    {
        if (Profissional?.Id is not { } id)
        {
            CpfProfissional = null;
            return;
        }

        try
        {
            using var scope = _escopos.CreateScope();
            var equipe = scope.ServiceProvider.GetRequiredService<EquipeService>();
            CpfProfissional = (await equipe.ObterProfissionalAsync(id))?.Cpf;
        }
        catch (Exception ex)
        {
            Clinica.Application.Diagnostico.Registrar("Gerente — CPF do profissional não pôde ser lido", ex);
        }
    }

    /// <summary>
    /// Grava o CPF no PROFISSIONAL vinculado — o dono do dado.
    ///
    /// Passa por <c>EquipeService</c> de propósito: é lá que moram as recusas de CPF
    /// inválido e de CPF repetido, e escrever direto no repositório aqui as contornaria,
    /// deixando entrar pela tela de acesso exatamente o que a de equipe recusa.
    /// </summary>
    private async Task SalvarCpfDoProfissionalAsync(IServiceProvider servicos)
    {
        if (Profissional?.Id is not { } id) return;

        var equipe = servicos.GetRequiredService<EquipeService>();
        var profissional = await equipe.ObterProfissionalAsync(id);
        if (profissional is null) return;

        var novo = string.IsNullOrWhiteSpace(CpfProfissional) ? null : CpfProfissional.Trim();
        if (Clinica.Domain.Cpf.Normalizar(profissional.Cpf) == Clinica.Domain.Cpf.Normalizar(novo))
            return;   // nada mudou: não reescrever evita gravação e auditoria à toa

        profissional.Cpf = novo;
        await equipe.SalvarProfissionalAsync(profissional);
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
                // Atribuir Profissional dispara OnProfissionalChanged, que traz o CPF do
                // banco — por isso não é preciso lê-lo aqui de novo.
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

            // O CPF é gravado ANTES do vínculo, e no dono dele. Se ele for recusado (inválido
            // ou já usado por outro profissional), a exceção sobe aqui e o usuário NÃO é
            // salvo pela metade — a alternativa seria criar o acesso e perder o CPF, com a
            // direção achando que cadastrou tudo.
            await SalvarCpfDoProfissionalAsync(scope.ServiceProvider);

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
