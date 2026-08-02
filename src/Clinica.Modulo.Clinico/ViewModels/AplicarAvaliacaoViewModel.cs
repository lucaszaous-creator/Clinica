using System.Collections.ObjectModel;
using Clinica.Application.Servicos;
using Clinica.Desktop.Shell;
using Clinica.Domain.Avaliacoes;
using Clinica.Domain.Entities;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;

namespace Clinica.Clinico.ViewModels;

/// <summary>
/// Um item do questionário na tela. As alternativas vêm do instrumento e não se editam:
/// mudar o peso de uma resposta produziria um escore que continua se chamando PHQ-9 sem
/// ser um.
/// </summary>
public sealed partial class ItemAvaliacaoViewModel : ObservableObject
{
    public required string Codigo { get; init; }
    public required int Numero { get; init; }
    public required string Enunciado { get; init; }
    public required IReadOnlyList<OpcaoItem> Opcoes { get; init; }

    /// <summary>Alternativa escolhida. Null = ainda não respondida.</summary>
    [ObservableProperty] private OpcaoItem? _escolhida;

    /// <summary>Enunciado com o número na frente, que é como o papel apresenta.</summary>
    public string Rotulo => $"{Numero}. {Enunciado}";
}

/// <summary>
/// O formulário de aplicação de um instrumento.
///
/// Construído à mão pela janela que o abre (o paciente e o instrumento vêm no
/// construtor), como todos os formulários da suíte — por isso não está no DI.
///
/// A tela mostra o escore PARCIAL enquanto se responde, e isso é decisão: esconder o
/// número até o fim faria a pessoa clicar em "gravar" só para ver quanto deu, e depois
/// apagar quando não gostasse. Mostrar deixa claro desde o começo que o resultado é uma
/// soma de respostas, não um veredito do sistema.
/// </summary>
public sealed partial class AplicarAvaliacaoViewModel : ObservableObject
{
    private readonly IServiceScopeFactory _escopos;
    private readonly int _pacienteId;
    private readonly int? _profissionalId;
    private readonly int? _evolucaoId;
    private readonly string? _especialidadeCodigo;

    public IInstrumentoAvaliacao Instrumento { get; }

    public ObservableCollection<ItemAvaliacaoViewModel> Itens { get; } = [];

    public string Titulo => Instrumento.Nome;
    public string Janela => Instrumento.Janela;
    public string Fonte => Instrumento.Fonte;

    /// <summary>
    /// O aviso que fica na tela o tempo todo. O sistema pontua e registra; ele não
    /// diagnostica, e uma escala apresentada sem essa linha é lida como se diagnosticasse.
    /// </summary>
    public string Ressalva =>
        "O sistema apenas pontua e registra o que foi respondido. A faixa é a leitura "
        + "publicada da escala — a interpretação e a conduta são de quem assina o prontuário.";

    [ObservableProperty] private DateTime _data = DateTime.Today;

    [ObservableProperty] private string? _observacoes;

    [ObservableProperty] private string _parcial = string.Empty;

    [ObservableProperty] private string? _mensagem;
    [ObservableProperty] private bool _mensagemEhErro;

    [ObservableProperty] private bool _salvando;

    /// <summary>Preenchido quando a gravação dá certo — a janela fecha e a lista recarrega.</summary>
    public AvaliacaoClinica? Salva { get; private set; }

    public AplicarAvaliacaoViewModel(
        IServiceScopeFactory escopos,
        IInstrumentoAvaliacao instrumento,
        int pacienteId,
        int? profissionalId = null,
        int? evolucaoId = null,
        string? especialidadeCodigo = null)
    {
        _escopos = escopos;
        _pacienteId = pacienteId;
        _profissionalId = profissionalId;
        _evolucaoId = evolucaoId;
        _especialidadeCodigo = especialidadeCodigo;
        Instrumento = instrumento;

        var numero = 1;
        foreach (var item in instrumento.Itens)
        {
            var vm = new ItemAvaliacaoViewModel
            {
                Codigo = item.Codigo,
                Numero = numero++,
                Enunciado = item.Enunciado,
                Opcoes = item.Opcoes
            };
            vm.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(ItemAvaliacaoViewModel.Escolhida)) AtualizarParcial();
            };
            Itens.Add(vm);
        }

        AtualizarParcial();
    }

    /// <summary>
    /// O parcial diz quantas faltam ANTES de dizer quanto deu — a ordem importa: um escore
    /// exibido sobre metade das respostas parece resultado, e o número de faltantes ao
    /// lado é o que impede a leitura errada.
    /// </summary>
    private void AtualizarParcial()
    {
        var respondidas = Itens.Count(i => i.Escolhida is not null);
        var faltam = Itens.Count - respondidas;

        if (respondidas == 0)
        {
            Parcial = $"{Itens.Count} item(ns) a responder.";
            return;
        }

        var soma = Itens.Where(i => i.Escolhida is not null).Sum(i => i.Escolhida!.Valor);

        Parcial = faltam > 0
            ? $"{respondidas} de {Itens.Count} respondidas — faltam {faltam}. "
              + $"Soma parcial: {soma}."
            : $"Escala completa. Soma: {soma}.";
    }

    [RelayCommand]
    private async Task SalvarAsync()
    {
        try
        {
            Salvando = true;
            Mensagem = null;
            MensagemEhErro = false;

            SessaoUsuario.Atual.Exigir(Permissao.EditarProntuario, "escrever no prontuário");

            var respostas = Itens
                .Where(i => i.Escolhida is not null)
                .ToDictionary(i => i.Codigo, i => i.Escolhida!.Valor);

            using var scope = _escopos.CreateScope();
            var avaliacoes = scope.ServiceProvider.GetRequiredService<AvaliacaoClinicaService>();

            Salva = await avaliacoes.AplicarAsync(
                _pacienteId,
                Instrumento.Codigo,
                respostas,
                DateOnly.FromDateTime(Data),
                _profissionalId,
                _evolucaoId,
                _especialidadeCodigo,
                Observacoes,
                SessaoUsuario.Atual.Operador);
        }
        catch (Exception ex)
        {
            Clinica.Application.Diagnostico.Registrar("Consultório — avaliação não pôde ser gravada", ex);
            Mensagem = ex.Message;
            MensagemEhErro = true;
        }
        finally
        {
            Salvando = false;
        }
    }
}
