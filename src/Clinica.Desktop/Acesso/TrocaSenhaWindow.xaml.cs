using System.Windows;
using Clinica.Application.Servicos;
using Clinica.Domain.Entities;
using Microsoft.Extensions.DependencyInjection;

namespace Clinica.Desktop.Acesso;

/// <summary>
/// Trocar a PRÓPRIA senha (parcela 69) — a gêmea da janela do shell
/// (`Clinica.Desktop.Shell/Componentes/TrocaSenhaWindow`), aqui pelo débito permanente
/// da Fase 4 cancelada: os dois apps não se referenciam, e a mesma ação precisa existir
/// dos dois lados (a lição da parcela 60 — a cópia que ficou para trás é onde a
/// capacidade some).
///
/// O serviço confere a senha ATUAL antes de aceitar a nova, então não há permissão a
/// exigir: a prova de posse é ela. Até esta parcela, o único caminho de troca era o
/// forçado (senha provisória emitida pela direção) — quem desconfiasse que alguém viu a
/// sua senha precisava pedir uma nova a terceiros, trocando um segredo comprometido por
/// um que já nasce compartilhado.
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
            // tela seria a segunda definição da mesma regra.
            using var escopo = _escopos.CreateScope();
            await escopo.ServiceProvider.GetRequiredService<AcessoService>()
                .TrocarSenhaAsync(
                    SessaoUsuario.Atual.UsuarioId, TxtAtual.Password, TxtNova.Password);

            DialogResult = true;
        }
        catch (Exception ex)
        {
            Clinica.Application.Diagnostico.Registrar("Troca de senha pelo usuário falhou", ex);
            Erro(ex.Message);
            BtnTrocar.IsEnabled = true;
        }
    }
}
