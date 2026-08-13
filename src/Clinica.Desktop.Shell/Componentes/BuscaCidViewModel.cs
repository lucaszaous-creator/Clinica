using System.Collections.ObjectModel;
using Clinica.Domain;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Clinica.Desktop.Shell.Componentes;

/// <summary>
/// Procurar um código da CID-10 (parcela 63).
///
/// Mora no shell porque o CID é digitado em DOIS módulos — a emissão de documento
/// (<c>DocumentoWindow</c>, que já está aqui) e a lista de problemas do Consultório. Duas
/// janelas divergiriam na primeira correção.
///
/// ⚠️ A janela é ATALHO, não validação: o campo de trás continua aceitando qualquer texto,
/// e a lista cobre os códigos desta clínica, não a CID-10 inteira. A tela escreve isso —
/// prometer a classificação completa seria a garantia aparente que este projeto recusa.
///
/// Não vai ao banco: a busca é sobre uma lista em memória, então ela filtra a cada tecla
/// sem contador de geração nem agrupamento de teclas — não há resposta que possa chegar
/// fora de ordem.
/// </summary>
public sealed partial class BuscaCidViewModel : ObservableObject
{
    public ObservableCollection<ItemCid> Resultados { get; } = [];

    [ObservableProperty] private string _termo = string.Empty;

    [ObservableProperty] private ItemCid? _selecionado;

    [ObservableProperty] private string _resumo = string.Empty;

    /// <summary>O código escolhido. Nulo enquanto ninguém escolheu.</summary>
    public string? Escolhido { get; private set; }

    /// <summary>Dispara quando a pessoa escolhe — a janela fecha.</summary>
    public event Action? Escolheu;

    public BuscaCidViewModel(string? termoInicial = null)
    {
        // Abre já filtrada pelo que estava no campo: quem clicou em "Buscar" com "M54"
        // digitado não deve ter de digitar de novo.
        _termo = termoInicial?.Trim() ?? string.Empty;
        Filtrar();
    }

    partial void OnTermoChanged(string value) => Filtrar();

    private void Filtrar()
    {
        var achados = CatalogoCid.Buscar(Termo);

        var antes = Selecionado?.Codigo;

        Resultados.Clear();
        foreach (var c in achados) Resultados.Add(c);

        // Mantém a escolha quando ela sobrevive ao filtro; senão, aponta o primeiro.
        Selecionado = Resultados.FirstOrDefault(c => c.Codigo == antes) ?? Resultados.FirstOrDefault();

        // O resumo DIZ QUE ESTÁ FILTRADO. "50 códigos" e "3 de 50" respondem perguntas
        // diferentes, e quem não acha o que procura precisa saber que há um filtro no
        // caminho antes de concluir que o código não existe (a lição da lista de espera,
        // parcela 25).
        Resumo = string.IsNullOrWhiteSpace(Termo)
            ? $"{CatalogoCid.Todos.Count} códigos desta clínica"
            : $"{achados.Count} de {CatalogoCid.Todos.Count} — filtrado por \"{Termo.Trim()}\"";
    }

    [RelayCommand]
    private void Limpar() => Termo = string.Empty;

    [RelayCommand]
    private void Escolher()
    {
        if (Selecionado is not { } c) return;

        Escolhido = c.Codigo;
        Escolheu?.Invoke();
    }
}
