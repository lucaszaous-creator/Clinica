using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using Clinica.Application.Modelos;
using Clinica.Domain;
using Clinica.Domain.Regras;

namespace Clinica.Desktop.Alertas;

/// <summary>
/// Janela de "rodar as pendências": lista guias para uma decisão explícita — dar baixa (informando o
/// nº da guia) ou registrar como NÃO CONFORMIDADE (com justificativa). No modo BLOQUEANTE traz as
/// guias cujo prazo desde a data prevista venceu e só fecha depois que toda guia tiver uma decisão.
/// </summary>
public partial class RodadaPendenciasWindow : Window
{
    /// <summary>Linha editável (TextBox binda direto os campos, sem INPC — como na baixa em lote).</summary>
    public sealed class Linha
    {
        public required int CodigoId { get; init; }
        public required string Descricao { get; init; }
        public required string Situacao { get; init; }
        public string? NumeroGuia { get; set; }
        public string? Justificativa { get; set; }

        /// <summary>Forma do número da guia NESTE convênio — cada linha pode ser de uma operadora.</summary>
        public FormatoNumeroGuia Formato { get; init; } = FormatoNumeroGuia.SemValidacao;
    }

    public List<Linha> Linhas { get; }

    public DateOnly DataBaixa => DateOnly.FromDateTime(DpData.SelectedDate ?? DateTime.Today);

    private readonly bool _bloqueante;
    private bool _concluido;

    public RodadaPendenciasWindow(IEnumerable<PendenciaCodigo> itens, RodadaPendenciasStatus status, bool bloqueante)
    {
        InitializeComponent();
        _bloqueante = bloqueante;

        Linhas = itens.Select(i => new Linha
        {
            CodigoId = i.CodigoId,
            // Rótulo central, nunca o enum cru (parcela 41).
            Descricao = $"{i.PacienteNome} — {RotulosEnum.De(i.Tipo)} ({RotulosEnum.De(i.Ordem)})",
            Formato = CatalogoConvenios.FormatoDoNumeroDaGuia(i.ConvenioCodigo ?? i.Convenio.ToString()),
            // Nome do CATÁLOGO, nunca o enum: `{i.Convenio}` escrevia "UnimedIntercambio"
            // na linha, e "Personalizado" no lugar do nome que a clínica cadastrou.
            Situacao = i.DiasEmAtraso > 0
                ? $"{CatalogoConvenios.Nome(i.ConvenioCodigo, i.Convenio)} · atrasada há {i.DiasEmAtraso} dia(s)"
                : $"{CatalogoConvenios.Nome(i.ConvenioCodigo, i.Convenio)} · vence hoje"
        }).ToList();
        Lista.ItemsSource = Linhas;
        DpData.SelectedDate = DateTime.Today;

        var extras = new List<string>();
        if (status.AplicaConsultas && status.ConsultasParaRevisar > 0)
            extras.Add($"{status.ConsultasParaRevisar} consulta(s) a renovar");
        if (status.AplicaCarteirinhas && status.CarteirinhasParaRevisar > 0)
            extras.Add($"{status.CarteirinhasParaRevisar} carteirinha(s) a vencer");
        var lembrete = extras.Count > 0
            ? $" Reveja também, no painel: {string.Join(" e ", extras)}."
            : string.Empty;

        TxtResumo.Text = bloqueante
            ? $"Estas {Linhas.Count} guia(s) estão pendentes há mais de {status.PrazoDias} dias sem resolução." +
              " Decida cada uma para continuar — dê baixa ou justifique como não conformidade." + lembrete
            : $"{Linhas.Count} guia(s) pendente(s). Dê baixa no que puder e justifique o restante como não conformidade." + lembrete;

        if (bloqueante)
            BtnCancelar.Visibility = Visibility.Collapsed;
    }

    private static bool SemDecisao(Linha l)
        => string.IsNullOrWhiteSpace(l.NumeroGuia) && string.IsNullOrWhiteSpace(l.Justificativa);

    private void Concluir_Click(object sender, RoutedEventArgs e)
    {
        // Rodada vencida: toda guia precisa de decisão antes de liberar a tela.
        if (_bloqueante && Linhas.Any(SemDecisao))
        {
            MessageBox.Show(this,
                "Enquanto a rodada estiver vencida, decida todas as guias: informe o número da guia " +
                "(baixa) ou uma justificativa (não conformidade).",
                "Rodar pendências", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        // Formato do número ANTES de processar. Aqui isso importa mais que na baixa em
        // lote: no modo BLOQUEANTE o sistema está travado, e uma recusa vinda do serviço
        // no meio da rodada deixaria parte das guias decidida, a janela aberta e a pessoa
        // sem saber qual linha corrigir para poder voltar a trabalhar.
        var recusadas = Linhas
            .Select(l => (l, critica: RegraNumeroGuia.Criticar(l.NumeroGuia, l.Formato)))
            .Where(x => x.critica is not null)
            .ToList();

        if (recusadas.Count > 0)
        {
            var detalhe = string.Join("\n", recusadas.Select(x => $"• {x.l.Descricao}: {x.critica}"));
            MessageBox.Show(this,
                $"{recusadas.Count} guia(s) com o número fora do formato do convênio. "
                + "Corrija antes de concluir — nada foi gravado.\n\n" + detalhe,
                "Rodar pendências", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        _concluido = true;
        DialogResult = true;
    }

    private void Cancelar_Click(object sender, RoutedEventArgs e) => Close();

    protected override void OnClosing(CancelEventArgs e)
    {
        // No modo bloqueante, não deixa fechar (X/Alt+F4) sem concluir a rodada.
        if (_bloqueante && !_concluido)
            e.Cancel = true;
        base.OnClosing(e);
    }
}
