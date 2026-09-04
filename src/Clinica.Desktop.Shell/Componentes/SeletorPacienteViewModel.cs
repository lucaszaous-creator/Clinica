using System.Collections.ObjectModel;
using Clinica.Application.Modelos;
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

    /// <summary>
    /// A tela FORNECE uma sugestão — o chip dela existe. Sem isto, telas sem sugestão
    /// desenhariam um chip que, clicado, cairia no `OrderBy(Nome)` e traria o alfabeto
    /// sob o rótulo "Com horário hoje": a pílula mentindo sobre o que está na tela.
    ///
    /// Lida uma vez, na montagem: <see cref="SugestaoInicial"/> é atribuída no construtor
    /// da tela dona, antes de qualquer binding.
    /// </summary>
    public bool TemSugestao => SugestaoInicial is not null;

    /// <summary>
    /// A tela abre SEM consultar nada, e a lista só existe depois que alguém pede
    /// (set/2026 — pedido da direção: <i>"isso nos ajuda e minimiza o tempo de resposta
    /// entre sistema servidor"</i>).
    ///
    /// O que ela resolve
    /// -----------------
    /// Doze telas chamavam <c>BuscarAsync(imediato: true)</c> na abertura com o termo
    /// vazio — e com o termo vazio a consulta não filtra nada: cai no
    /// <c>OrderBy(Nome).Take(50)</c>. Cada uma dessas aberturas era uma ida ao banco
    /// REMOTO para trazer o começo do alfabeto de 2.238 fichas, que não é o paciente de
    /// ninguém. É ruído com cara de conteúdo E é consulta paga.
    ///
    /// ⚠️ Ela é OPT-IN, e tinha de ser: numa tela de LISTAGEM (Pacientes, Enfermagem) a
    /// lista É a resposta, e abrir vazio ali seria trocar um defeito pelo oposto. A regra
    /// que decide é a mesma da <see cref="SugestaoInicial"/> — <b>a lista é a resposta da
    /// tela, ou o caminho até ela?</b>
    ///
    /// ⚠️ E ela NÃO tira os dois modos: os chips continuam ali como ATALHO. O que muda é
    /// que a lista passa a ser PEDIDA — um clique traz a agenda do dia, um clique traz o
    /// cadastro inteiro, e abrir a tela não traz nada.
    /// </summary>
    public bool SemBuscaInicial { get; init; }

    /// <summary>
    /// Alguém já pediu uma lista (clicou num chip). Só vira verdadeiro por CLIQUE: apagar
    /// o campo depois de escolher um modo devolve ao modo escolhido, não ao ocioso —
    /// senão o chip aceso ficaria sobre uma tela em branco.
    /// </summary>
    private bool _pediramLista;

    /// <summary>
    /// Nada foi pedido ainda: a lista está vazia por DESENHO, não por falta de resultado.
    ///
    /// ⚠️ A distinção é o que impede a tela de mentir. Sem ela o <c>EstadoDaTela</c>
    /// escreveria "Nenhum paciente encontrado" numa tela recém-aberta de uma clínica com
    /// 2.238 fichas — e vazio que se anuncia como resposta é pior que vazio nenhum
    /// (a regra do terceiro estado, parcela 37).
    /// </summary>
    public bool Ocioso => Modo == ModoDaBusca.Ocioso;

    /// <summary>
    /// O par invertido — o projeto não tem conversor de booleano invertido.
    ///
    /// É o <c>Ativo</c> do <c>EstadoDaTela</c> nas telas SEM sugestão: com a lista ociosa,
    /// "Nenhum paciente encontrado" seria uma afirmação falsa sobre uma clínica de 2.284
    /// fichas. Nas telas COM sugestão quem responde é o <see cref="BuscandoPorTermo"/>,
    /// porque lá o vazio da sugestão tem frase própria ("ninguém tem horário hoje").
    /// </summary>
    public bool AlgoFoiPedido => !Ocioso;

    /// <summary>
    /// O que a busca está fazendo agora. A decisão mora na Application
    /// (<see cref="BuscaDePaciente.Modo"/>), onde o <c>dotnet test</c> alcança: este
    /// projeto é WPF e não compila no projeto de teste, e o que se decide aqui não é uma
    /// frase — é <b>se a tela vai ao banco</b>.
    /// </summary>
    public ModoDaBusca Modo => BuscaDePaciente.Modo(
        SemBuscaInicial, _pediramLista, Termo, TemSugestao, SugestaoLigada);

    /// <summary>A lista atual é a SUGESTÃO, não um resultado de busca.</summary>
    [ObservableProperty] private bool _mostrandoSugestao;

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
    [NotifyPropertyChangedFor(nameof(Modo))]
    [NotifyPropertyChangedFor(nameof(Ocioso))]
    [NotifyPropertyChangedFor(nameof(AlgoFoiPedido))]
    [NotifyPropertyChangedFor(nameof(ListandoTodos))]
    [NotifyPropertyChangedFor(nameof(BuscandoPorTermo))]
    [NotifyPropertyChangedFor(nameof(SugestaoNaTela))]
    private bool _sugestaoLigada = true;

    /// <summary>O par invertido — a suíte não tem conversor de booleano invertido.</summary>
    public bool SugestaoDesligada => !SugestaoLigada;

    /// <summary>
    /// A lista atual é a LISTAGEM COMPLETA — o modo "Todos os pacientes" de fato à vista.
    ///
    /// ⚠️ Não é o mesmo que <see cref="SugestaoDesligada"/>, e a diferença é visível: com
    /// termo digitado a lista é RESULTADO DE BUSCA, e nenhum dos dois modos está no ar.
    /// As pílulas amarradas nas flags de configuração (`SugestaoLigada`) ficavam acesas
    /// sobre uma lista que não era a delas — a tela dizendo "com horário hoje" em cima de
    /// quatro resultados de "pinheiro". Elas agora seguem o que ESTÁ na tela.
    /// </summary>
    /// <remarks>
    /// ⚠️ <see cref="Ocioso"/> entra na conta (set/2026): sem ele o chip "Todos os
    /// pacientes" nasceria ACESO numa tela que não consultou nada — a pílula dizendo que
    /// está mostrando o cadastro inteiro sobre uma tela em branco. É a mesma lição das
    /// pílulas de modo: elas seguem o que ESTÁ na tela, nunca a flag de configuração.
    /// </remarks>
    public bool ListandoTodos => Modo == ModoDaBusca.Todos;

    /// <summary>Volta para a sugestão da tela (o "Com horário hoje").</summary>
    [RelayCommand]
    private Task LigarSugestao()
    {
        Termo = null;
        SugestaoLigada = true;
        Pedir();
        return BuscarAsync(imediato: true);
    }

    /// <summary>Mostra todo mundo, em ordem de nome — a listagem, agora PEDIDA.</summary>
    [RelayCommand]
    private Task DesligarSugestao()
    {
        Termo = null;
        SugestaoLigada = false;
        Pedir();
        return BuscarAsync(imediato: true);
    }

    /// <summary>Sai do ocioso: a partir daqui a tela tem uma lista pedida.</summary>
    private void Pedir()
    {
        if (_pediramLista) return;
        _pediramLista = true;
        OnPropertyChanged(nameof(Ocioso));
        OnPropertyChanged(nameof(AlgoFoiPedido));
        OnPropertyChanged(nameof(Modo));
        OnPropertyChanged(nameof(ListandoTodos));
        OnPropertyChanged(nameof(BuscandoPorTermo));
        OnPropertyChanged(nameof(SugestaoNaTela));
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
    /// <remarks>
    /// ⚠️ Ele segue o <see cref="Modo"/> desde set/2026, e NÃO o <c>!MostrandoSugestao</c>
    /// que estava aqui. Com o estado OCIOSO a negação passou a mentir: sem sugestão no ar
    /// e sem termo digitado ela dava VERDADEIRO, o <c>EstadoDaTela</c> ligava, e a tela
    /// recém-aberta escrevia <i>"Nenhum paciente encontrado"</i> por cima do convite —
    /// uma afirmação falsa sobre uma clínica de 2.238 fichas, e justamente a que leva a
    /// cadastrar de novo quem já tem ficha (parcela 57).
    ///
    /// A lição: <b>ao acrescentar um estado a uma máquina de dois, todo booleano definido
    /// por NEGAÇÃO precisa ser relido</b> — ele passa a responder pelo estado novo sem
    /// ninguém ter decidido isso.
    /// </remarks>
    public bool BuscandoPorTermo => Modo == ModoDaBusca.PorTermo;

    /// <summary>
    /// A sugestão é o que está na tela AGORA. Os chips seguem o <see cref="Modo"/>, que
    /// deriva do que a pessoa vê no campo — não das flags de configuração, e não do
    /// resultado da última busca: os dois chips têm de responder pela mesma pergunta, ou
    /// um acende na intenção e o outro no resultado.
    /// </summary>
    public bool SugestaoNaTela => Modo == ModoDaBusca.Sugestao;

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

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ListandoTodos))]
    [NotifyPropertyChangedFor(nameof(Ocioso))]
    [NotifyPropertyChangedFor(nameof(Modo))]
    [NotifyPropertyChangedFor(nameof(AlgoFoiPedido))]
    [NotifyPropertyChangedFor(nameof(BuscandoPorTermo))]
    [NotifyPropertyChangedFor(nameof(SugestaoNaTela))]
    private string? _termo;
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

            // ⚠️ OCIOSO: a tela não pediu nada, e nada vai ao banco. É este `return` que
            // entrega o pedido da direção — sem ele, apagar o campo numa tela ociosa cairia
            // no `OrderBy(Nome).Take(50)` e traria o alfabeto de volta pela porta de trás.
            //
            // Ele limpa a lista de propósito: o resultado da busca anterior continuar na
            // tela depois de a pessoa apagar o termo é a tela afirmando que aqueles são os
            // pacientes de um campo vazio.
            var modo = Modo;

            if (!BuscaDePaciente.Consulta(modo))
            {
                Resultados.Clear();
                MostrandoSugestao = false;
                SugestaoVazia = false;
                TemResultados = false;
                ResumoDaLista = null;
                Erro = null;
                Buscando = false;
                Atualizou?.Invoke();
                return;
            }

            Buscando = true;

            // Termo vazio COM sugestão: a tela tem algo melhor a mostrar que o alfabeto, e
            // nem chega a consultar a busca. Sem sugestão, o caminho é o de sempre.
            var usandoSugestao = modo == ModoDaBusca.Sugestao;

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
