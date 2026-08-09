using System.Collections.ObjectModel;
using Clinica.Application.Servicos;
using Clinica.Domain.Entities;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;

namespace Clinica.Desktop.Shell.Componentes;

/// <summary>
/// A busca de paciente da SUÍTE, num lugar só — irmão do seletor do faturamento
/// (<c>Clinica.Desktop/ViewModels/SeletorPacienteViewModel</c>), do qual é uma cópia
/// deliberada: com a Fase 4 cancelada, os dois design systems convivem em definitivo
/// e o shell não pode referenciar o executável do faturamento. Ver
/// <c>docs/arquitetura-multi-exe.md</c> (débito assumido).
///
/// Tela nova da suíte que escolhe paciente usa ESTE seletor; não reescreva a busca.
/// Ele já resolve os três problemas que as cópias antigas tinham:
/// <list type="bullet">
///   <item>o corte vai para o SQL (<see cref="Limite"/>), não para um <c>Take()</c> em memória;</item>
///   <item>as teclas são agrupadas (<see cref="AtrasoDigitacaoMs"/>) em vez de virar uma consulta cada;</item>
///   <item>uma resposta atrasada nunca sobrescreve o resultado de uma busca mais nova.</item>
/// </list>
/// </summary>
public sealed partial class SeletorPacienteViewModel : ObservableObject
{
    /// <summary>Linhas que um seletor traz. Quem digita chega no nome antes de precisar de mais.</summary>
    public const int LimitePadrao = 50;

    /// <summary>Espera depois da última tecla antes de consultar o banco.</summary>
    private const int AtrasoDigitacaoMs = 250;

    private readonly IServiceScopeFactory _scopeFactory;

    // Cancela a busca anterior. Não é descartado de propósito: a continuação da busca velha
    // ainda lê o token dela para saber que foi substituída.
    private CancellationTokenSource? _cts;

    public SeletorPacienteViewModel(IServiceScopeFactory scopeFactory, int? limite = LimitePadrao)
    {
        _scopeFactory = scopeFactory;
        Limite = limite;
    }

    /// <summary>Corte aplicado no banco. Null = sem corte (telas de listagem).</summary>
    public int? Limite { get; }

    /// <summary>Refino opcional em memória sobre o que veio do banco (filtro, ordenação alternativa).</summary>
    public Func<IReadOnlyList<Paciente>, IEnumerable<Paciente>>? Refinar { get; set; }

    public ObservableCollection<Paciente> Resultados { get; } = new();

    /// <summary>
    /// Há o que escolher agora (parcela 52). Existe para a tela poder ESCONDER a lista
    /// quando ela está vazia, em vez de reservar espaço permanente para dizer que não há
    /// nada — a regra de leiaute do projeto.
    ///
    /// É atualizada por <see cref="Atualizou"/>, e não por CollectionChanged, pela mesma
    /// razão da parcela 37: aquele dispara uma vez por linha inserida.
    /// </summary>
    [ObservableProperty] private bool _temResultados;

    [ObservableProperty] private string? _termo;
    [ObservableProperty] private Paciente? _selecionado;
    [ObservableProperty] private bool _buscando;

    /// <summary>Trava a escolha (remarcação: mover o horário não troca de pessoa).</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Editavel))]
    private bool _travado;

    /// <summary>Inverso de <see cref="Travado"/> — o campo de busca liga direto nele.</summary>
    public bool Editavel => !Travado;

    /// <summary>Falha de busca — a lista fica como estava e a tela avisa em vez de emudecer.</summary>
    [ObservableProperty] private string? _erro;

    /// <summary>Disparado quando a escolha muda (a tela reage: avisos, modalidade habitual…).</summary>
    public event Action<Paciente?>? SelecaoMudou;

    /// <summary>Disparado depois de cada busca concluída.</summary>
    public event Action? Atualizou;

    partial void OnSelecionadoChanged(Paciente? value) => SelecaoMudou?.Invoke(value);

    // Pesquisa instantânea (padrão CampoPesquisa do design system): a tecla reagenda a busca.
    partial void OnTermoChanged(string? value) => _ = BuscarAsync();

    /// <summary>Recarrega agora, sem esperar a digitação parar (abertura da tela, botão atualizar).</summary>
    [RelayCommand]
    private Task Atualizar() => BuscarAsync(imediato: true);

    /// <summary>
    /// Consulta o banco e publica em <see cref="Resultados"/>. Com <paramref name="imediato"/>
    /// falso espera <see cref="AtrasoDigitacaoMs"/> — se outra tecla chegar nesse meio-tempo,
    /// esta busca é cancelada antes de sair da máquina.
    /// </summary>
    public async Task BuscarAsync(bool imediato = false)
    {
        var atual = new CancellationTokenSource();
        var anterior = _cts;
        _cts = atual;
        anterior?.Cancel();

        var ct = atual.Token;
        try
        {
            if (!imediato) await Task.Delay(AtrasoDigitacaoMs, ct);

            Buscando = true;
            using var scope = _scopeFactory.CreateScope();
            var service = scope.ServiceProvider.GetRequiredService<PacienteService>();
            var encontrados = await service.BuscarAsync(Termo, Limite, ct);

            // Resposta fora de ordem: só a busca mais recente pode escrever na lista.
            if (ct.IsCancellationRequested) return;

            var exibir = Refinar is null ? encontrados : Refinar(encontrados);

            Resultados.Clear();
            foreach (var p in exibir)
                Resultados.Add(p);

            Erro = null;
            TemResultados = Resultados.Count > 0;
            Atualizou?.Invoke();
        }
        catch (OperationCanceledException)
        {
            // Busca substituída por outra mais nova — comportamento normal ao digitar.
        }
        catch (Exception ex)
        {
            Clinica.Application.Diagnostico.Registrar("Suíte — busca de pacientes falhou", ex);
            Erro = $"Não foi possível buscar pacientes: {ex.Message}";
        }
        finally
        {
            // Quem foi substituído não mexe no estado visual: quem manda é a busca mais nova.
            if (!ct.IsCancellationRequested) Buscando = false;
        }
    }

    /// <summary>
    /// Seleciona um paciente que pode não estar no resultado atual (remarcação abre um
    /// agendamento antigo, e o paciente dele pode estar fora das primeiras linhas).
    /// </summary>
    public void SelecionarGarantindoNaLista(Paciente paciente)
    {
        if (Resultados.All(p => p.Id != paciente.Id))
            Resultados.Insert(0, paciente);
        Selecionado = paciente;
    }

    /// <summary>Limpa a escolha para trocar de paciente.</summary>
    public void Limpar() => Selecionado = null;
}
