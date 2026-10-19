using System.Windows;
using System.Windows.Controls;
using Clinica.Application.Servicos;
using Clinica.Domain;

namespace Clinica.Desktop.Shell.Componentes;

/// <summary>O que a pessoa escolheu na janela de envio.</summary>
public sealed record EscolhaDeEnvio(CanalDeEnvio Canal, string? Email, bool GuardarEmailNaFicha);

/// <summary>
/// A pergunta do "Enviar ao paciente" (out/2026) — a MESMA em todas as portas: a central de
/// Documentos, a ficha, as Prescrições do Consultório (receitas), o "já saiu hoje"
/// do atendimento e o fim da emissão. Quem decide as opções, a ordem e qual vem marcada é
/// <see cref="DestinosDoEnvio"/> (Application, com teste); esta janela só desenha e confere o
/// endereço digitado.
///
/// E-mail primeiro e já marcado, com o endereço da ficha sugerido e EDITÁVEL (a clínica não
/// colhe e-mail no cadastro) e "guardar na ficha" desmarcado; WhatsApp pelo CRM depois, com
/// a frase do custo; a opção indisponível aparece apagada COM o motivo, nunca some.
/// </summary>
public partial class EnvioAoPacienteWindow : Window
{
    private EscolhaDeEnvio? _escolha;

    private EnvioAoPacienteWindow(DestinosDoEnvio destinos, string? aviso, bool aposEmitir)
    {
        InitializeComponent();

        if (aposEmitir)
        {
            TxtTitulo.Text = "Enviar agora ao paciente?";
            BtnCancelar.Content = "Agora não";
        }
        TxtPapel.Text = $"{destinos.Documento} · {destinos.Paciente}";

        var opcoes = destinos.Opcoes.ToDictionary(o => o.Canal);
        Desenhar(OpEmail, TxtNotaEmail, opcoes[CanalDeEnvio.Email], " (recomendado)");
        Desenhar(OpCrm, TxtNotaCrm, opcoes[CanalDeEnvio.WhatsAppPeloCrm], string.Empty);
        Desenhar(OpLocal, TxtNotaLocal, opcoes[CanalDeEnvio.WhatsAppDesteComputador], string.Empty);

        TxtEmail.Text = destinos.Email ?? string.Empty;

        if (!string.IsNullOrWhiteSpace(aviso))
        {
            TxtAviso.Text = aviso;
            TxtAviso.Visibility = Visibility.Visible;
        }

        switch (destinos.Padrao)
        {
            case CanalDeEnvio.Email: OpEmail.IsChecked = true; break;
            case CanalDeEnvio.WhatsAppPeloCrm: OpCrm.IsChecked = true; break;
            case CanalDeEnvio.WhatsAppDesteComputador: OpLocal.IsChecked = true; break;
            default: BtnEnviar.IsEnabled = false; break;
        }
        AtualizarPainelEmail();
        Loaded += (_, _) => { if (OpEmail.IsChecked == true) { TxtEmail.Focus(); TxtEmail.SelectAll(); } };
    }

    private static void Desenhar(RadioButton botao, TextBlock nota, OpcaoDeEnvio opcao, string sufixo)
    {
        botao.Content = opcao.Rotulo + sufixo;
        botao.IsEnabled = opcao.Disponivel;
        nota.Text = opcao.Disponivel ? opcao.Nota ?? string.Empty : opcao.Motivo ?? string.Empty;
    }

    private void AoTrocarCanal(object sender, RoutedEventArgs e) => AtualizarPainelEmail();

    private void AtualizarPainelEmail()
    {
        if (PainelEmail is null) return;
        var email = OpEmail.IsChecked == true;
        TxtEmail.IsEnabled = email;
        ChkGuardar.IsEnabled = email;
        TxtErro.Visibility = Visibility.Collapsed;
    }

    private void AoEnviar(object sender, RoutedEventArgs e)
    {
        if (OpEmail.IsChecked == true)
        {
            // Conferido AQUI, inline: a janela não fecha com um endereço que o servidor recusaria.
            if (EnderecoDeEmail.SeValido(TxtEmail.Text) is not { } email)
            {
                TxtErro.Text = "Informe um e-mail válido (ex.: nome@provedor.com.br).";
                TxtErro.Visibility = Visibility.Visible;
                TxtEmail.Focus();
                return;
            }
            _escolha = new EscolhaDeEnvio(CanalDeEnvio.Email, email, ChkGuardar.IsChecked == true);
        }
        else if (OpCrm.IsChecked == true)
            _escolha = new EscolhaDeEnvio(CanalDeEnvio.WhatsAppPeloCrm, null, false);
        else if (OpLocal.IsChecked == true)
            _escolha = new EscolhaDeEnvio(CanalDeEnvio.WhatsAppDesteComputador, null, false);
        else
            return;

        DialogResult = true;
    }

    /// <summary>A escolha, ou <c>null</c> quando a pessoa desistiu ("Cancelar" / "Agora não").</summary>
    public static EscolhaDeEnvio? Perguntar(DestinosDoEnvio destinos, string? aviso, bool aposEmitir, Window? dono)
    {
        var janela = new EnvioAoPacienteWindow(destinos, aviso, aposEmitir) { Owner = dono };
        return janela.ShowDialog() == true ? janela._escolha : null;
    }
}
