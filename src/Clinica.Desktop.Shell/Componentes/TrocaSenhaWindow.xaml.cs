using System.Windows;
using Clinica.Application.Servicos;
using Clinica.Domain.Entities;
using Microsoft.Extensions.DependencyInjection;

namespace Clinica.Desktop.Shell.Componentes;

/// <summary>
/// Trocar a PRÓPRIA senha (parcela 69).
///
/// A metade voluntária que faltava: <see cref="AcessoService.TrocarSenhaAsync"/> — o
/// método que CONFERE a senha atual antes de aceitar a nova — existia sem um único
/// chamador. O único caminho de troca era o forçado (a direção emite uma provisória com
/// "deve trocar", e o login cobra a definitiva): quem desconfiasse que alguém viu a sua
/// senha precisava pedir à direção, que escolhia a nova e a ENTREGAVA — trocando um
/// segredo comprometido por um segredo que já nasce compartilhado.
///
/// A conferência da senha atual é o que separa esta janela da tela de Acessos: lá quem
/// age é a direção, sobre a conta dos outros, sob `GerenciarUsuarios`; aqui é o próprio
/// dono, e a prova de posse é a senha de agora — nenhuma permissão a mais, nenhuma a
/// menos. Sucesso fecha a janela; o erro fica NELA, porque é aqui que se corrige.
///
/// ⚠️ Existe uma gêmea no faturamento (`Clinica.Desktop/Acesso/TrocaSenhaWindow`) — o
/// débito permanente da Fase 4 cancelada: os dois apps não se referenciam, e a mesma
/// ação precisa existir dos dois lados (a lição da parcela 60).
/// </summary>
public partial class TrocaSenhaWindow : Window
{
    private readonly IServiceScopeFactory _escopos;

    public TrocaSenhaWindow(IServiceScopeFactory escopos)
    {
        _escopos = escopos;
        InitializeComponent();
        Loaded += (_, _) => TxtAtual.Focus();
    }

    private void Erro(string texto)
    {
        TxtErro.Text = texto;
        TxtErro.Visibility = Visibility.Visible;
    }

    private async void Confirmar(object remetente, RoutedEventArgs e)
    {
        TxtErro.Visibility = Visibility.Collapsed;

        // A validação local só cobre o que o serviço não tem como saber: a repetição é
        // conferência de DIGITAÇÃO, e existe porque o campo esconde o que foi digitado.
        if (TxtNova.Password != TxtRepetida.Password)
        {
            Erro("A nova senha e a repetição não conferem — digite as duas de novo.");
            return;
        }

        BtnTrocar.IsEnabled = false;
        try
        {
            // Quem valida é o SERVIÇO (senha atual correta, crítica da nova): validar na
            // tela seria a segunda definição da mesma regra, e a de cá é a que ninguém
            // lembraria de ajustar quando a crítica de senha mudar.
            using var escopo = _escopos.CreateScope();
            await escopo.ServiceProvider.GetRequiredService<AcessoService>()
                .TrocarSenhaAsync(
                    SessaoUsuario.Atual.UsuarioId, TxtAtual.Password, TxtNova.Password);

            DialogResult = true;
        }
        catch (Exception ex)
        {
            // "A senha atual está incorreta" e a crítica da senha nova chegam daqui — o
            // serviço já fala a língua da tela.
            Clinica.Application.Diagnostico.Registrar("Troca de senha pelo usuário falhou", ex);
            Erro(ex.Message);
            BtnTrocar.IsEnabled = true;
        }
    }
}
