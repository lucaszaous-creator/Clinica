using System.Windows;
using Clinica.Application.Servicos;
using Clinica.Desktop.Configuracao;
using Clinica.Domain.Entities;
using Microsoft.Extensions.DependencyInjection;

namespace Clinica.Desktop.Alertas;

/// <summary>
/// O histórico de UMA guia: baixa, estorno, glosa, recurso, lote, não conformidade.
///
/// A trilha de auditoria responde "quem fez isso?" desde a parcela 21, e o faturamento
/// — que é onde essas ações ACONTECEM — não tinha uma única linha dela. A tela de
/// Auditoria existe no Gerente, filtra por período e por paciente, e mora noutro app: o
/// faturista que abre uma guia baixada e não reconhece o número não tem como perguntar o
/// que aconteceu com ela sem pedir à direção.
///
/// <c>AuditoriaService.DaGuiaAsync</c> existia, testado, <b>sem um único chamador</b>
/// (parcela 63) — o defeito recorrente do projeto na variante "a resposta está pronta e
/// ninguém consegue fazer a pergunta".
///
/// É SOMENTE LEITURA, e isso é decisão: registro de auditoria que se pode editar ou
/// apagar não é auditoria, é rascunho.
/// </summary>
public partial class HistoricoGuiaWindow : Window
{
    /// <summary>Uma linha da trilha, já formatada — a janela não tem ViewModel.</summary>
    private sealed record LinhaHistorico(string Quando, string Operador, string Acao, string Detalhe);

    private readonly IServiceScopeFactory _escopos;
    private readonly int _codigoId;

    public HistoricoGuiaWindow(IServiceScopeFactory escopos, CodigoFaturamento guia)
    {
        InitializeComponent();

        _escopos = escopos;
        _codigoId = guia.Id;

        TxtGuia.Text = Descrever(guia);

        Loaded += async (_, _) => await CarregarAsync();
    }

    /// <summary>
    /// De que guia é este histórico. O número REAL vem primeiro quando existe: é por ele
    /// que a operadora e o faturista se referem à guia; o Id interno não diz nada a
    /// ninguém fora do banco.
    /// </summary>
    private static string Descrever(CodigoFaturamento guia)
    {
        var partes = new List<string>();

        if (!string.IsNullOrWhiteSpace(guia.NumeroGuiaReal))
            partes.Add($"Guia {guia.NumeroGuiaReal}");

        if (!string.IsNullOrWhiteSpace(guia.Descricao))
            partes.Add(guia.Descricao!);

        partes.Add($"prevista para {guia.DataPrevistaFaturamento:dd/MM/yyyy}");

        if (guia.Atendimento?.Paciente?.Nome is { } paciente)
            partes.Add(paciente);

        return string.Join(" · ", partes);
    }

    private async Task CarregarAsync()
    {
        try
        {
            using var scope = _escopos.CreateScope();
            var auditoria = scope.ServiceProvider.GetRequiredService<AuditoriaService>();

            var eventos = await auditoria.DaGuiaAsync(_codigoId);

            Grade.ItemsSource = eventos
                .OrderByDescending(e => e.DataHora)
                .Select(e => new LinhaHistorico(
                    e.DataHora.ToString("dd/MM/yyyy HH:mm"),
                    e.Operador,
                    // A ação sai como foi GRAVADA ("BaixaGuia", "GlosaRegistrada"), igual
                    // à tela de Auditoria do Gerente. Humanizá-la só aqui daria dois
                    // vocabulários para a mesma trilha, e é a trilha que precisa casar
                    // entre as duas telas quando alguém investiga.
                    e.Acao,
                    e.Detalhe ?? "—"))
                .ToList();

            Vazio.Visibility = eventos.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        }
        catch (Exception ex)
        {
            // Falha NUNCA aparece como "guia sem histórico": as duas levam a conclusões
            // opostas sobre a mesma guia, e a errada é a que faz alguém dar por encerrada
            // uma investigação que nem começou.
            LogErros.Registrar("Faturamento — histórico da guia não pôde ser lido", ex);

            Grade.ItemsSource = null;
            Vazio.Visibility = Visibility.Collapsed;
            Falhou.Visibility = Visibility.Visible;
            TxtFalha.Text = $"Não foi possível ler o histórico desta guia: {ex.Message}";
        }
    }

    private void Fechar_Click(object sender, RoutedEventArgs e) => Close();
}
