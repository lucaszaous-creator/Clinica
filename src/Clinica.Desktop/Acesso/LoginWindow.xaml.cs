using System.Windows;
using Clinica.Application.Servicos;
using Clinica.Desktop.Configuracao;
using Clinica.Domain.Entities;
using Microsoft.Extensions.DependencyInjection;

namespace Clinica.Desktop.Acesso;

/// <summary>
/// Entrada no app de FATURAMENTO (parcela 45).
///
/// Por que ele passou a ter login
/// -------------------------------
/// Até aqui o faturamento era o único posto sem autenticação, e isso estava documentado
/// como decisão: uma pessoa só operava a máquina. A cliente pediu o contrário — permissões
/// granulares "para a gerente auditar o que está sendo feito e liberar ou não". A metade
/// das permissões não resolvia nada sozinha: toda ação do faturamento gravava o usuário do
/// WINDOWS como operador. Duas pessoas que dividem o balcão gravavam o mesmo nome, e a
/// trilha de auditoria — que existe desde a parcela 21 e responde "quem fez isso?" —
/// respondia o nome da MÁQUINA.
///
/// Três estados, e o primeiro evita o pior acidente possível:
/// 1. BASE SEM USUÁRIO → cria o PRIMEIRO ACESSO, sempre Gerente. Sem isso, a versão que
///    introduz login trancaria a clínica do lado de fora do sistema que a fatura, e o
///    conserto seria no banco, na mão.
/// 2. Base com usuário → login e senha.
/// 3. Senha provisória → obriga a trocar antes de abrir a janela principal.
///
/// O que ela NÃO tem, e é decisão: entrada por certificado (SafeID). Ela existe na suíte
/// porque lá o certificado assina documento clínico; no faturamento não se assina nada, e
/// um botão a mais numa tela de entrada é um botão a mais para dar errado num posto que
/// precisa abrir às sete da manhã.
/// </summary>
public partial class LoginWindow : Window
{
    private readonly IServiceScopeFactory _escopos;

    private bool _primeiroAcesso;
    private UsuarioSistema? _autenticado;

    /// <summary>Preenchida quando a janela fecha com sucesso — é o que vira a sessão do processo.</summary>
    public UsuarioSistema? Usuario { get; private set; }

    public LoginWindow(IServiceScopeFactory escopos, bool primeiroAcesso)
    {
        InitializeComponent();
        _escopos = escopos;
        _primeiroAcesso = primeiroAcesso;

        if (primeiroAcesso) MostrarPrimeiroAcesso();
        else MostrarEntrar();
    }

    private void MostrarEntrar()
    {
        TxtTitulo.Text = "Entrar — Faturamento";
        TxtSubtitulo.Text = "Informe seu usuário e senha. É por eles que a direção vê quem "
                            + "deu baixa, quem estornou e quem decidiu não faturar.";
        PainelEntrar.Visibility = Visibility.Visible;
        PainelPrimeiroAcesso.Visibility = Visibility.Collapsed;
        PainelTrocarSenha.Visibility = Visibility.Collapsed;
        BtnEntrar.Content = "Entrar";
        TxtLogin.Focus();
    }

    private void MostrarPrimeiroAcesso()
    {
        TxtTitulo.Text = "Primeiro acesso";
        TxtSubtitulo.Text =
            "Ainda não há usuários cadastrados. Crie o seu — ele nasce como Gerente Geral, "
            + "e é por ele que os demais acessos da clínica serão cadastrados.";
        PainelEntrar.Visibility = Visibility.Collapsed;
        PainelPrimeiroAcesso.Visibility = Visibility.Visible;
        PainelTrocarSenha.Visibility = Visibility.Collapsed;
        BtnEntrar.Content = "Criar e entrar";
        TxtNovoNome.Focus();
    }

    private void MostrarTrocaDeSenha()
    {
        TxtTitulo.Text = "Defina uma nova senha";
        TxtSubtitulo.Text = "Sua senha é provisória. Escolha uma nova para continuar.";
        PainelEntrar.Visibility = Visibility.Collapsed;
        PainelPrimeiroAcesso.Visibility = Visibility.Collapsed;
        PainelTrocarSenha.Visibility = Visibility.Visible;
        BtnEntrar.Content = "Salvar e entrar";
        TxtTrocaSenha.Focus();
    }

    private async void Confirmar(object remetente, RoutedEventArgs e)
    {
        BtnEntrar.IsEnabled = false;
        try
        {
            if (_autenticado is not null) await TrocarSenhaAsync();
            else if (_primeiroAcesso) await CriarPrimeiroAcessoAsync();
            else await EntrarAsync();
        }
        catch (Exception ex)
        {
            // Falha aqui não pode virar tela branca: a clínica precisa saber se é senha,
            // rede ou banco — e o log guarda o resto.
            LogErros.Registrar("Faturamento — falha na tela de login", ex);
            Erro(ex.Message);
        }
        finally
        {
            BtnEntrar.IsEnabled = true;
        }
    }

    private async Task EntrarAsync()
    {
        using var scope = _escopos.CreateScope();
        var acesso = scope.ServiceProvider.GetRequiredService<AcessoService>();

        var resultado = await acesso.AutenticarAsync(TxtLogin.Text, TxtSenha.Password);
        if (!resultado.Sucesso)
        {
            TxtSenha.Clear();
            TxtSenha.Focus();
            Erro(resultado.Erro ?? "Não foi possível entrar.");
            return;
        }

        Concluir(resultado.Usuario!);
    }

    private async Task CriarPrimeiroAcessoAsync()
    {
        if (TxtNovaSenha.Password != TxtNovaSenhaRepetida.Password)
        {
            Erro("As duas senhas não são iguais.");
            return;
        }

        using var scope = _escopos.CreateScope();
        var acesso = scope.ServiceProvider.GetRequiredService<AcessoService>();

        // Alguém pode ter criado o primeiro usuário em outra máquina enquanto esta tela
        // estava aberta — a suíte e o faturamento dividem o MESMO banco. Reconferir evita
        // transformar a corrida num "login já existe" sem explicação.
        if (await acesso.ExisteUsuarioAtivoAsync())
        {
            _primeiroAcesso = false;
            MostrarEntrar();
            Info("Um usuário já foi cadastrado em outro computador. Entre com ele.");
            return;
        }

        var usuario = await acesso.CriarAsync(
            TxtNovoNome.Text, TxtNovoLogin.Text, TxtNovaSenha.Password,
            PerfilAcesso.Gerente, operador: "primeiro acesso");

        Concluir(usuario);
    }

    private async Task TrocarSenhaAsync()
    {
        if (TxtTrocaSenha.Password != TxtTrocaSenhaRepetida.Password)
        {
            Erro("As duas senhas não são iguais.");
            return;
        }

        using var scope = _escopos.CreateScope();
        var acesso = scope.ServiceProvider.GetRequiredService<AcessoService>();

        await acesso.DefinirSenhaAsync(
            _autenticado!.Id, TxtTrocaSenha.Password, deveTrocar: false,
            operador: _autenticado.Login);

        Usuario = _autenticado;
        DialogResult = true;
        Close();
    }

    /// <summary>Autenticou: ou entra direto, ou passa pela troca obrigatória de senha.</summary>
    private void Concluir(UsuarioSistema usuario)
    {
        if (usuario.DeveTrocarSenha)
        {
            _autenticado = usuario;
            Esconder();
            MostrarTrocaDeSenha();
            return;
        }

        Usuario = usuario;
        DialogResult = true;
        Close();
    }

    private void Cancelar(object remetente, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    private void Esconder()
    {
        BoxErro.Visibility = Visibility.Collapsed;
        BoxInfo.Visibility = Visibility.Collapsed;
    }

    private void Erro(string mensagem)
    {
        Esconder();
        TxtErro.Text = mensagem;
        BoxErro.Visibility = Visibility.Visible;
    }

    private void Info(string mensagem)
    {
        Esconder();
        TxtInfo.Text = mensagem;
        BoxInfo.Visibility = Visibility.Visible;
    }
}
