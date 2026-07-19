using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using Clinica.Application.Modelos;
using Clinica.Domain.Entities;
using Clinica.Domain.Regras;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Clinica.Desktop.Alertas;

/// <summary>Linha editável do demonstrativo: uma guia do lote e a decisão da operadora sobre ela.</summary>
public sealed partial class RetornoLinha : ObservableObject
{
    public required int CodigoId { get; init; }
    public required string Paciente { get; init; }
    public required string Tipo { get; init; }
    public string? NumeroGuia { get; init; }

    [ObservableProperty] private bool _glosada;
    [ObservableProperty] private MotivoGlosa? _motivo;
    [ObservableProperty] private string? _complemento;

    /// <summary>Opções do combo (catálogo ANS) — propriedade para facilitar o binding da célula.</summary>
    public IReadOnlyList<MotivoGlosa> Motivos => MotivosGlosa.Todos;
}

/// <summary>
/// Registro do demonstrativo de análise de um lote TISS: marca guia a guia o que a
/// operadora aceitou e o que glosou (com motivo da tabela ANS).
/// </summary>
public partial class RetornoLoteWindow : Window
{
    public ObservableCollection<RetornoLinha> Linhas { get; } = new();

    public DateOnly DataRetorno { get; private set; }
    public string? Observacao { get; private set; }

    public IReadOnlyList<RetornoGuiaDecisao> Decisoes { get; private set; } = Array.Empty<RetornoGuiaDecisao>();

    public RetornoLoteWindow(int numeroLote, IEnumerable<CodigoFaturamento> codigos)
    {
        InitializeComponent();
        TxtTitulo.Text = $"Registrar retorno do lote nº {numeroLote}";
        DpData.SelectedDate = DateTime.Today;

        foreach (var c in codigos)
            Linhas.Add(new RetornoLinha
            {
                CodigoId = c.Id,
                Paciente = c.Atendimento?.Paciente?.Nome ?? "(desconhecido)",
                Tipo = c.Tipo.ToString(),
                NumeroGuia = c.NumeroGuiaReal,
                Glosada = c.GlosaEmAberto // glosa já registrada aparece pré-marcada
            });

        GridGuias.ItemsSource = Linhas;
    }

    private void Confirmar_Click(object sender, RoutedEventArgs e)
    {
        DataRetorno = DateOnly.FromDateTime(DpData.SelectedDate ?? DateTime.Today);
        Observacao = string.IsNullOrWhiteSpace(TxtObservacao.Text) ? null : TxtObservacao.Text.Trim();
        Decisoes = Linhas
            .Select(l => new RetornoGuiaDecisao(l.CodigoId, l.Glosada, l.Motivo?.Codigo, l.Complemento))
            .ToList();
        DialogResult = true;
        Close();
    }

    private void Cancelar_Click(object sender, RoutedEventArgs e) => Close();
}
