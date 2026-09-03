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

    /// <summary>
    /// O que mostrar ANTES de alguém digitar. Opcional, e sem ela nada muda.
    ///
    /// Por que existe
    /// -------------
    /// Com o termo vazio a consulta não filtra nada: <c>BuscarPacientesAsync</c> cai direto
    /// no <c>OrderBy(Nome).Take(50)</c>. Numa clínica de 2.238 fichas isso abre a tela com o
    /// começo do ALFABETO — ACELINO, ADAISE, ADAO —, que não é o paciente de ninguém. É
    /// ruído com cara de conteúdo: a lista parece uma resposta e não é.
    ///
    /// Numa tela de LISTAGEM esse mesmo despejo é o certo (é a lista, e ela passa
    /// <c>limite: null</c>). Por isso a correção é opt-in: só a tela que tem uma sugestão
    /// MELHOR a fornece, e as outras dezesseis que usam este seletor continuam idênticas.
    /// </summary>
    public Func<CancellationToken, Task<IReadOnlyList<Paciente>>>? SugestaoInicial { get; set; }

    /// <summary>
    /// O que a sugestão É, para a tela poder dizer — "Com horário hoje". Lista sem rótulo
    /// se lê como resultado de busca, e a pessoa conclui que a clínica só tem sete
    /// pacientes.
    /// </summary>
    public string? RotuloDaSugestao { get; set; }

    /// <summary>A lista atual é a SUGESTÃO, não um resultado de busca.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(BuscandoPorTermo))]
    private bool _mostrandoSugestao;

    /// <summary>
    /// A sugestão está LIGADA. Desligá-la devolve, de propósito, a listagem que o campo
    /// vazio sempre deu — todo mundo em ordem de nome.
    ///
    /// Não é o defeito de volta: o defeito era a listagem chegar SEM ninguém pedir, com
    /// cara de resposta. Pedida, ela é a resposta certa para "quero ver quem existe" — e
    /// é o que a tela oferece na pílula "Todos os pacientes".
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SugestaoDesligada))]
    private bool _sugestaoLigada = true;

    /// <summary>O par invertido — a suíte não tem conversor de booleano invertido.</summary>
    public bool SugestaoDesligada => !SugestaoLigada;

    /// <summary>Volta para a sugestão da tela (o "Com horário hoje").</summary>
    [RelayCommand]
    private Task LigarSugestao()
    {
        Termo = null;
        SugestaoLigada = true;
        return BuscarAsync(imediato: true);
    }

    /// <summary>Mostra todo mundo, em ordem de nome — a listagem, agora PEDIDA.</summary>
    [RelayCommand]
    private Task DesligarSugestao()
    {
        Termo = null;
        SugestaoLigada = false;
        return BuscarAsync(imediato: true);
    }

    /// <summary>
    /// O par invertido de <see cref="MostrandoSugestao"/> — a suíte não tem conversor de
    /// booleano invertido, e é o mesmo par de `SemPaciente`/`PacienteEscolhido`.
    ///
    /// Ele existe para o `EstadoDaTela` da tela: o "nenhum paciente encontrado" é a
    /// resposta certa para uma BUSCA que não achou, e a resposta errada para uma sugestão
    /// vazia — ali o que falta não é o paciente, é o horário de hoje, e a frase tem de
    /// dizer isso.
    /// </summary>
    public bool BuscandoPorTermo => !MostrandoSugestao;

    /// <summary>A sugestão foi consultada e veio VAZIA — ninguém tem horário hoje.</summary>
    [ObservableProperty] private bool _sugestaoVazia;

    /// <summary>
    /// "7 com horário hoje" / "23 encontrados" / "50 encontrados (mostrando os primeiros)".
    ///
    /// Existe porque a lista sozinha não diz o TAMANHO do que ela responde, e as duas
    /// leituras erradas são opostas: sete linhas sem rótulo se leem como "a clínica só tem
    /// sete pacientes", e cinquenta linhas sem aviso escondem que o corte do SQL cortou —
    /// quem procura "SILVA" e não acha o seu conclui que a ficha não existe.
    /// </summary>
    [ObservableProperty] private string? _resumoDaLista;

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
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TemErro))]
    private string? _erro;

    /// <summary>
    /// Há erro a mostrar. Existe para a tela ESCONDER a linha quando não há — `TextBlock`
    /// com texto vazio ainda ocupa a altura de uma linha, e numa composição centrada cada
    /// vão em branco aparece.
    /// </summary>
    public bool TemErro => !string.IsNullOrWhiteSpace(Erro);

    /// <summary>Disparado quando a escolha muda (a tela reage: avisos, modalidade habitual…).</summary>
    public event Action<Paciente?>? SelecaoMudou;

    /// <summary>Disparado depois de cada busca concluída.</summary>
    public event Action? Atualizou;

    partial void OnSelecionadoChanged(Paciente? value) => SelecaoMudou?.Invoke(value);

    // Pesquisa instantânea (padrão CampoPesquisa do design system): a tecla reagenda a busca.
    partial void OnTermoChanged(string? value)
    {
        // Digitar RELIGA a sugestão. Sem isto, quem clicou em "Todos os pacientes", buscou
        // alguém e apagou o campo voltaria para a lista alfabética — um modo grudado que
        // ninguém escolheu e que não aparece em lugar nenhum da tela.
        if (!string.IsNullOrWhiteSpace(value)) SugestaoLigada = true;
        _ = BuscarAsync();
    }

    /// <summary>
    /// O tamanho do que a lista responde. O aviso do CORTE só aparece quando o resultado
    /// bateu exatamente no limite — que é o único caso em que pode haver mais lá fora.
    /// </summary>
    private string? DescreverLista(bool sugestao)
    {
        if (Resultados.Count == 0) return null;

        if (sugestao)
            return Resultados.Count == 1 ? "1 paciente" : $"{Resultados.Count} pacientes";

        var no_corte = Limite is { } teto && Resultados.Count >= teto;
        return no_corte
            ? $"{Resultados.Count} ou mais — refine a busca"
            : Resultados.Count == 1 ? "1 encontrado" : $"{Resultados.Count} encontrados";
    }

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

            // Termo vazio COM sugestão: a tela tem algo melhor a mostrar que o alfabeto, e
            // nem chega a consultar a busca. Sem sugestão, o caminho é o de sempre.
            var usandoSugestao = SugestaoInicial is not null
                                 && SugestaoLigada
                                 && string.IsNullOrWhiteSpace(Termo);

            IReadOnlyList<Paciente> encontrados;
            if (usandoSugestao)
            {
                encontrados = await SugestaoInicial!(ct);
            }
            else
            {
                using var scope = _scopeFactory.CreateScope();
                var service = scope.ServiceProvider.GetRequiredService<PacienteService>();
                encontrados = await service.BuscarAsync(Termo, Limite, ct);
            }

            // Resposta fora de ordem: só a busca mais recente pode escrever na lista.
            if (ct.IsCancellationRequested) return;

            // ⚠️ O `Refinar` NÃO se aplica à sugestão: ele é o refino de um RESULTADO DE
            // BUSCA (filtrar quem já está na fila, reordenar por última sessão), e quem
            // monta a sugestão já decidiu o que ela contém. Passá-la pelo refino faria a
            // tela filtrar duas vezes com dois critérios diferentes.
            var exibir = usandoSugestao || Refinar is null ? encontrados : Refinar(encontrados);

            MostrandoSugestao = usandoSugestao;
            SugestaoVazia = usandoSugestao && encontrados.Count == 0;

            Resultados.Clear();
            foreach (var p in exibir)
                Resultados.Add(p);

            Erro = null;
            TemResultados = Resultados.Count > 0;
            ResumoDaLista = DescreverLista(usandoSugestao);
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
