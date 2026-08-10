using Clinica.Domain;
using System.Collections.ObjectModel;
using Clinica.Application.Servicos;
using Clinica.Domain.Entities;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;

namespace Clinica.Gerente.ViewModels;

/// <summary>Um lote TISS na visão da direção.</summary>
public sealed class LinhaLote
{
    public required string Numero { get; init; }
    public required string Geracao { get; init; }
    public required string Situacao { get; init; }
    public required string Guias { get; init; }
    public required string Envio { get; init; }
    public required string Retorno { get; init; }

    /// <summary>Gerado e ainda não enviado — dinheiro parado numa gaveta digital.</summary>
    public required bool Parado { get; init; }

    public static LinhaLote De(LoteTiss l) => new()
    {
        Numero = $"Lote {l.Numero}",
        Geracao = l.DataGeracao.ToString("dd/MM/yyyy"),
        Situacao = RotulosEnum.De(l.Status),
        Guias = $"{l.Codigos.Count} guia(s)",
        Envio = l.DataEnvio is { } envio
            ? $"enviado em {envio:dd/MM/yyyy}"
              + (string.IsNullOrWhiteSpace(l.ProtocoloOperadora)
                  ? " (sem protocolo)"
                  : $" · protocolo {l.ProtocoloOperadora}")
            : "não enviado",
        Retorno = l.DataRetorno is { } retorno
            ? $"retorno em {retorno:dd/MM/yyyy}"
            : "sem retorno",
        Parado = l.Status == StatusLoteTiss.Gerado
    };
}

/// <summary>
/// Lotes TISS na visão da DIREÇÃO — a quarta aba do "Faturamento (TISS)".
///
/// SOMENTE LEITURA, e isso é decisão, não limitação de tempo. O número do lote é uma
/// SEQUÊNCIA (<c>ParametrosService.ChaveProximoLoteTiss</c>): dois apps gerando lote em
/// paralelo produziriam dois com o mesmo número, e a operadora recusa o segundo — um erro
/// que só apareceria semanas depois, no retorno, com o faturamento do mês já comprometido.
/// Gerar, enviar e registrar retorno continuam no app de faturamento, que roda num posto
/// só e é dono da sequência.
///
/// O que a direção ganha aqui é a pergunta que ela faz: "o que já saiu e o que está
/// parado?". Lote gerado e não enviado é dinheiro numa gaveta digital, e essa é a linha
/// que a tela destaca.
/// </summary>
public sealed partial class LotesGerencialViewModel : ObservableObject
{
    private readonly IServiceScopeFactory _escopos;

    public ObservableCollection<LinhaLote> Lotes { get; } = [];

    [ObservableProperty] private string _resumo = string.Empty;
    [ObservableProperty] private string? _mensagem;
    [ObservableProperty] private bool _mensagemEhErro;

    public LotesGerencialViewModel(IServiceScopeFactory escopos)
    {
        _escopos = escopos;
        _ = CarregarAsync();
    }

    [RelayCommand]
    public async Task CarregarAsync()
    {
        try
        {
            Mensagem = null;
            MensagemEhErro = false;

            using var scope = _escopos.CreateScope();
            var lotes = scope.ServiceProvider.GetRequiredService<LoteTissService>();

            Lotes.Clear();
            foreach (var l in await lotes.ListarAsync()) Lotes.Add(LinhaLote.De(l));

            var parados = Lotes.Count(l => l.Parado);
            Resumo = Lotes.Count == 0
                ? "Nenhum lote gerado ainda."
                : parados == 0
                    ? $"{Lotes.Count} lote(s), todos enviados."
                    : $"{Lotes.Count} lote(s) · {parados} gerado(s) e AINDA NÃO ENVIADO(S).";
        }
        catch (Exception ex)
        {
            Clinica.Application.Diagnostico.Registrar("Gerente — lotes não puderam ser lidos", ex);
            Mensagem = $"Não foi possível ler os lotes: {ex.Message}";
            MensagemEhErro = true;
        }
    }
}
