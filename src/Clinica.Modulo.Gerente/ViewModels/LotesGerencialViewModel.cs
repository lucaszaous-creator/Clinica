using Clinica.Domain;
using System.Collections.ObjectModel;
using Clinica.Application.Servicos;
using Clinica.Desktop.Shell.Componentes;
using Clinica.Domain.Entities;
using Clinica.Domain.Regras;
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

    /// <summary>
    /// A situação como OPÇÃO DE FILTRO — o nome do estado, que aqui coincide com o
    /// rótulo exibido. Deriva do MESMO enum que pinta a linha, para o combo nunca
    /// discordar do texto.
    /// </summary>
    public required string SituacaoFiltro { get; init; }

    /// <summary>Protocolo cru, para a busca — na linha ele vive dentro da frase do envio.</summary>
    public required string Protocolo { get; init; }

    /// <summary>
    /// Operadora do lote (parcela 60 — o lote é POR OPERADORA). Vazia nos lotes antigos,
    /// gerados antes da coluna: inventar um nome ali fundiria o histórico com a operadora
    /// errada.
    /// </summary>
    public required string Operadora { get; init; }

    public bool TemOperadora => Operadora.Length > 0;

    public static LinhaLote De(LoteTiss l) => new()
    {
        Numero = $"Lote {l.Numero}",
        Geracao = l.DataGeracao.ToString("dd/MM/yyyy"),
        Situacao = RotulosEnum.De(l.Status),
        SituacaoFiltro = l.Status.ToString(),
        Protocolo = l.ProtocoloOperadora ?? string.Empty,
        Operadora = l.ConvenioCodigo is { Length: > 0 } codigo
            ? CatalogoConvenios.Nome(codigo)
            : string.Empty,
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

    /// <summary>Tudo o que o banco devolveu; <see cref="Lotes"/> é o recorte do filtro.</summary>
    private readonly List<LinhaLote> _todos = [];

    // ---- Filtro (em memória, sobre o que já foi lido). A pergunta da direção é "o que
    // está parado?", e o histórico só cresce — achar o lote do protocolo que a operadora
    // citou por telefone não pode ser rolar a lista inteira.
    public const string TodasSituacoesLote = "Todas";

    // Strings, e não o enum StatusLoteTiss: ComboBox amarrado a enum sem rotulador
    // escreve o identificador na tela (o defeito da parcela 41, vigiado pela checagem 20).
    public string[] OpcoesSituacaoLote { get; } =
        [TodasSituacoesLote, "Gerado", "Enviado", "Processado"];

    [ObservableProperty] private string _filtroSituacaoLote = TodasSituacoesLote;

    /// <summary>UM campo para nº do lote, protocolo e operadora: quem procura tem um papel na mão, não sabe qual dos três está nele.</summary>
    [ObservableProperty] private string _filtroBuscaLote = string.Empty;

    partial void OnFiltroSituacaoLoteChanged(string value) => Refiltrar();
    partial void OnFiltroBuscaLoteChanged(string value) => Refiltrar();

    public bool FiltroAtivo =>
        FiltroSituacaoLote != TodasSituacoesLote || !string.IsNullOrWhiteSpace(FiltroBuscaLote);

    [RelayCommand]
    private void LimparFiltro()
    {
        FiltroSituacaoLote = TodasSituacoesLote;
        FiltroBuscaLote = string.Empty;
    }

    /// <summary>O estado vazio muda de frase quando há filtro — "não há lote" e "nenhum bate com o filtro" são respostas diferentes.</summary>
    [ObservableProperty] private string _vazioDescricao = "Nenhum lote gerado ainda.";

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

            _todos.Clear();
            foreach (var l in await lotes.ListarAsync()) _todos.Add(LinhaLote.De(l));

            Refiltrar();
        }
        catch (Exception ex)
        {
            Clinica.Application.Diagnostico.Registrar("Gerente — lotes não puderam ser lidos", ex);
            Mensagem = $"Não foi possível ler os lotes: {ex.Message}";
            MensagemEhErro = true;
        }
    }

    /// <summary>
    /// Aplica o filtro sobre o que já foi lido — em memória, sem ida ao banco (o padrão
    /// da tela de Consultas da Recepção). A busca casa em nº do lote, protocolo OU
    /// operadora.
    /// </summary>
    private void Refiltrar()
    {
        Lotes.Clear();
        foreach (var l in _todos.Where(l =>
                     (FiltroSituacaoLote == TodasSituacoesLote
                      || l.SituacaoFiltro == FiltroSituacaoLote)
                     && (Busca.Casa(l.Numero, FiltroBuscaLote)
                         || Busca.Casa(l.Protocolo, FiltroBuscaLote)
                         || Busca.Casa(l.Operadora, FiltroBuscaLote))))
            Lotes.Add(l);

        OnPropertyChanged(nameof(FiltroAtivo));

        // O resumo DIZ que está filtrado — e o número de PARADOS continua sendo o do
        // TOTAL: o filtro serve para achar um lote, não para esconder dinheiro na gaveta.
        var parados = _todos.Count(l => l.Parado);
        Resumo = _todos.Count == 0
            ? "Nenhum lote gerado ainda."
            : FiltroAtivo
                ? $"{Lotes.Count} de {_todos.Count} lote(s) no filtro"
                  + (parados > 0
                      ? $" · {parados} gerado(s) e AINDA NÃO ENVIADO(S) no total."
                      : ".")
                : parados == 0
                    ? $"{Lotes.Count} lote(s), todos enviados."
                    : $"{Lotes.Count} lote(s) · {parados} gerado(s) e AINDA NÃO ENVIADO(S).";

        VazioDescricao = FiltroAtivo
            ? "Nenhum lote bate com o filtro — limpe-o para ver todos."
            : "Nenhum lote gerado ainda.";
    }
}
