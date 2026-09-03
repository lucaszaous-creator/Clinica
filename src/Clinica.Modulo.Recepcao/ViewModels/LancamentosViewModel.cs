using System.Collections.ObjectModel;
using Clinica.Application.Abstracoes;
using Clinica.Desktop.Shell.Componentes;
using Clinica.Desktop.Shell.Configuracao;
using Clinica.Desktop.Shell.Modulos;
using Clinica.Domain;
using Clinica.Domain.Entities;
using Clinica.Domain.Regras;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;

namespace Clinica.Recepcao.ViewModels;

/// <summary>
/// Um atendimento na conferência. O que era <c>LinhaAvulso</c>, dentro do Novo
/// atendimento, mais o que o período livre passou a exigir: a DATA (a lista deixou de ser
/// de um dia só) e as chaves de filtro em campo próprio.
///
/// ⚠️ Convênio, modalidade e operador aparecem DUAS vezes — uma para ler, outra para
/// filtrar — e não é redundância à toa: o filtro casa por igualdade exata, e casar pelo
/// texto que a tela desenha amarraria o filtro à formatação. No dia em que a linha passar
/// a escrever "Unimed Costa do Sol (Padrão) · carteirinha vencida", o combo pararia de
/// achar as linhas dela.
/// </summary>
public sealed class LinhaLancamento
{
    /// <summary>
    /// O atendimento por trás da linha — a porta do ESTORNO (parcela 94). A conferência é
    /// onde o engano é percebido; obrigar a procurar a sessão noutra tela para desfazê-la
    /// seria pôr a correção longe do momento em que o erro é notado.
    /// </summary>
    public required int AtendimentoId { get; init; }

    public required DateOnly Data { get; init; }
    public required string DataRotulo { get; init; }
    public required string Paciente { get; init; }
    public required string Modalidade { get; init; }
    public required string Convenio { get; init; }
    public required string Numero { get; init; }
    public required string Guias { get; init; }
    public required string Pendencia { get; init; }
    public required bool TemPendencia { get; init; }

    /// <summary>
    /// Quem LANÇOU, e quando (parcela 58). A pergunta da direção — "quem lançou isso?" —
    /// só existia na trilha de auditoria, noutra tela e noutro app.
    /// </summary>
    public required string Lancamento { get; init; }

    /// <summary>A chave de filtro de "quem lançou": o operador cru, sem a frase em volta.</summary>
    public required string Operador { get; init; }
}

/// <summary>
/// LANÇAMENTOS — os atendimentos de um período, com filtros e o estorno (set/2026).
///
/// De onde ela veio
/// ----------------
/// Era o bloco "LANÇADOS HOJE" no rodapé do Novo atendimento, e estava no lugar errado por
/// três razões que só apareceram juntas:
///
/// 1. <b>Espaço.</b> Morando dentro do <c>ScrollViewer</c> da tela de lançar, ele exigia
///    dois remendos: teto de 300px (senão a coluna passava de 2.000px numa clínica que
///    trabalhou o dia inteiro) e <c>RodaDaPagina</c> (senão a lista comia a roda do mouse
///    e a página não rolava). Os dois somem numa tela própria.
/// 2. <b>Filtro.</b> Não havia onde pôr um. A conferência de fim de dia é sobre separar —
///    o que é particular, o que ficou pendente, quem lançou o quê.
/// 3. <b>Período.</b> A lista era do dia e ponto. "Esqueci de conferir ontem" não tinha
///    resposta nesta suíte.
///
/// ⚠️ <b>O que se perdeu, e é decisão.</b> Antes, lançar recarregava a lista e a linha
/// nova aparecia logo abaixo — confirmação imediata. Numa aba separada isso acaba. A troca
/// se sustenta porque a coluna direita do Novo atendimento já mostra o desfecho
/// (o resultado, as guias geradas, a capa em PDF), e porque esta lista responde outra
/// pergunta, num outro momento: a conferência antes de fechar o balcão.
///
/// O filtro é de CLIENTE, sobre o período já carregado — a consulta do banco é uma só, por
/// período. Filtrar no banco custaria uma ida por combo mexido, e o período que cabe numa
/// tela cabe na memória.
/// </summary>
public partial class LancamentosViewModel : ObservableObject, ICarregarAoAbrir
{
    private readonly IServiceScopeFactory _scopeFactory;

    /// <summary>
    /// Descarte de resposta fora de ordem: mexer nas datas dispara carga a cada tecla do
    /// DatePicker, e duas no ar ao mesmo tempo é o caso normal.
    /// </summary>
    private int _geracao;

    /// <summary>Tudo o que veio do banco. <see cref="Linhas"/> é o recorte filtrado dela.</summary>
    private readonly List<LinhaLancamento> _carregadas = [];

    /// <summary>O "sem filtro" dos combos. Texto e não nulo: item nulo em ComboBox não se seleciona.</summary>
    public const string Todos = "Todos";

    /// <summary>
    /// Teto do período, em dias. Não é medo de consulta grande — é honestidade sobre o que
    /// esta tela é: a conferência do balcão, não o relatório do ano. Um pedido de cinco
    /// anos traria dezenas de milhares de linhas para uma lista que se lê com o olho, e a
    /// pergunta de quem quer isso é outra (Consulta de guias, no faturamento).
    /// </summary>
    private const int TetoDeDias = 366;

    public LancamentosViewModel(IServiceScopeFactory scopeFactory)
        => _scopeFactory = scopeFactory;

    /// <summary>Metade VISÍVEL da permissão do estorno; a recusa de verdade é do serviço.</summary>
    public bool PodeEstornar => SessaoUsuario.Atual.Pode(Permissao.LancarAtendimento);

    public ObservableCollection<LinhaLancamento> Linhas { get; } = [];

    /// <summary>As opções dos combos, tiradas do que foi carregado — sem consulta extra.</summary>
    public ObservableCollection<string> ConveniosDisponiveis { get; } = [Todos];

    public ObservableCollection<string> ModalidadesDisponiveis { get; } = [Todos];
    public ObservableCollection<string> OperadoresDisponiveis { get; } = [Todos];

    [ObservableProperty] private DateTime _de = DateTime.Today;
    [ObservableProperty] private DateTime _ate = DateTime.Today;

    // ⚠️ ANULÁVEIS de propósito, e a razão é um caminho real: `Repovoar` LIMPA a coleção
    // que alimenta o ComboBox, e um ComboBox cuja lista esvazia escreve NULL de volta na
    // propriedade ligada ao `SelectedItem`. Declarados como `string` não-anulável, o filtro
    // passava por um instante com null — que não é "Todos" — e escondia a lista inteira.
    // `Livre` trata null e "Todos" como a mesma coisa: sem filtro.
    [ObservableProperty] private string? _convenio = Todos;
    [ObservableProperty] private string? _modalidade = Todos;
    [ObservableProperty] private string? _operador = Todos;

    /// <summary>Só o que ainda tem guia por liberar — o assunto do produto, isolado.</summary>
    [ObservableProperty] private bool _somenteComPendencia;

    [ObservableProperty] private bool _carregando;

    /// <summary>
    /// O TERCEIRO estado: a leitura falhou. Lista vazia por falha e lista vazia por não
    /// haver nada são coisas diferentes, e a tela que confunde as duas mente.
    /// </summary>
    [ObservableProperty] private bool _naoVerificado;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TemMensagem))]
    private string? _mensagem;

    [ObservableProperty] private bool _mensagemEhErro;

    public bool TemMensagem => !string.IsNullOrWhiteSpace(Mensagem);

    public bool TemLinhas => Linhas.Count > 0;

    /// <summary>"27 atendimentos" ou "8 de 27" quando algum filtro está ligado.</summary>
    public string Resumo => _carregadas.Count == Linhas.Count
        ? $"{Linhas.Count} atendimento(s)"
        : $"{Linhas.Count} de {_carregadas.Count} atendimento(s)";

    /// <summary>Algum filtro ligado — mostra o botão de limpar em vez de deixá-lo sempre aceso.</summary>
    public bool TemFiltro => !Livre(Convenio) || !Livre(Modalidade)
                             || !Livre(Operador) || SomenteComPendencia;

    /// <summary>Sem filtro: "Todos" escolhido, ou o null que o combo escreve ao esvaziar.</summary>
    private static bool Livre(string? escolhido)
        => string.IsNullOrEmpty(escolhido) || escolhido == Todos;

    public Task CarregarAsync() => BuscarAsync();

    partial void OnDeChanged(DateTime value) => _ = BuscarAsync();
    partial void OnAteChanged(DateTime value) => _ = BuscarAsync();

    partial void OnConvenioChanged(string? value) => Aplicar();
    partial void OnModalidadeChanged(string? value) => Aplicar();
    partial void OnOperadorChanged(string? value) => Aplicar();
    partial void OnSomenteComPendenciaChanged(bool value) => Aplicar();

    /// <summary>
    /// Lê o período no banco. Uma consulta só, com os códigos e o paciente juntos — a
    /// versão anterior perguntava atendimento por atendimento.
    /// </summary>
    [RelayCommand]
    public async Task BuscarAsync()
    {
        var geracao = ++_geracao;

        // Datas invertidas não são erro de quem usa: quem digita "de 10/09" com "até
        // 03/09" ainda no campo quer os dois extremos. Ordenar é mais útil que recusar.
        var inicio = DateOnly.FromDateTime(De <= Ate ? De : Ate);
        var fim = DateOnly.FromDateTime(De <= Ate ? Ate : De);

        if (fim.DayNumber - inicio.DayNumber + 1 > TetoDeDias)
        {
            Avisar($"O período pedido passa de {TetoDeDias} dias. Esta é a conferência do "
                   + "balcão — para intervalos maiores use a Consulta de guias, no "
                   + "faturamento.", erro: true);
            return;
        }

        try
        {
            Carregando = true;
            NaoVerificado = false;
            Mensagem = null;

            using var scope = _scopeFactory.CreateScope();
            var repo = scope.ServiceProvider.GetRequiredService<IClinicaRepositorio>();
            var atendimentos = await repo.AtendimentosNoPeriodoAsync(inicio, fim);
            if (geracao != _geracao) return;

            // Monta em lista local e só ENTÃO publica: entre o Clear e o último Add não
            // pode haver await, senão duas cargas intercaladas repetem linhas.
            var linhas = atendimentos.Select(MontarLinha).ToList();

            _carregadas.Clear();
            _carregadas.AddRange(linhas);
            RemontarOpcoes();
            Aplicar();
        }
        catch (Exception ex)
        {
            if (geracao != _geracao) return;
            NaoVerificado = true;
            LogSuite.Registrar("Lançamentos — atendimentos do período não puderam ser lidos", ex);
            Avisar($"Não foi possível ler os atendimentos do período: {ex.Message}", erro: true);
        }
        finally
        {
            // A carga superada não apaga o "Carregando" da que ainda está no ar.
            if (geracao == _geracao) Carregando = false;
        }
    }

    /// <summary>Volta os combos ao "Todos" — uma tecla em vez de quatro.</summary>
    [RelayCommand]
    private void LimparFiltros()
    {
        Convenio = Todos;
        Modalidade = Todos;
        Operador = Todos;
        SomenteComPendencia = false;
    }

    /// <summary>
    /// ESTORNAR (parcela 94) — a sessão lançada por engano. A janela pergunta item a item
    /// o que desfazer (caixa, pacote, insumo) e recusa quando a guia já saiu da clínica;
    /// quem decide isso é o <c>EstornoAtendimentoService</c>, não a tela.
    /// </summary>
    [RelayCommand]
    private async Task EstornarAsync(LinhaLancamento? linha)
    {
        if (linha is null) return;
        SessaoUsuario.Atual.Exigir(Permissao.LancarAtendimento, "estornar o atendimento");

        var vm = new EstornoAtendimentoViewModel(_scopeFactory, linha.AtendimentoId);
        var janela = new Janelas.EstornoAtendimentoWindow(vm) { Owner = JanelaDona.Atual() };
        janela.ShowDialog();

        if (!vm.Estornado) return;

        // Os avisos das reversões de FORA não podem sumir com a janela: o estorno das
        // guias está gravado, e o que falhou lá continua para ser resolvido à mão.
        var recado = $"Atendimento nº {linha.Numero} estornado. O horário dele, se houver, "
                     + "voltou para \"Agendado\" e pode ser lançado de novo.";
        if (vm.Avisos.Count > 0) recado += " " + string.Join(" ", vm.Avisos);

        await BuscarAsync();
        Avisar(recado);
    }

    /// <summary>
    /// As opções dos combos saem do que foi carregado, e não de um catálogo: o catálogo
    /// traria convênios que a clínica cadastrou e nunca usou, e o combo da conferência
    /// existe para separar o que ESTÁ na lista.
    /// </summary>
    private void RemontarOpcoes()
    {
        Repovoar(ConveniosDisponiveis, _carregadas.Select(l => l.Convenio), ref _convenio, nameof(Convenio));
        Repovoar(ModalidadesDisponiveis, _carregadas.Select(l => l.Modalidade), ref _modalidade, nameof(Modalidade));
        Repovoar(OperadoresDisponiveis, _carregadas.Select(l => l.Operador), ref _operador, nameof(Operador));
    }

    /// <summary>
    /// Refaz uma lista de opções preservando a escolha — e devolvendo-a a "Todos" quando o
    /// valor escolhido não existe mais no período novo.
    ///
    /// ⚠️ Sem esse cuidado o combo ficaria com o texto antigo selecionado e a lista sairia
    /// VAZIA sem dizer por quê: filtrar por um convênio que não aparece em setembro,
    /// depois de trocar o período para agosto, esconde tudo com um filtro invisível.
    /// </summary>
    private void Repovoar(
        ObservableCollection<string> destino, IEnumerable<string> valores,
        ref string? escolhido, string nomeDaPropriedade)
    {
        var opcoes = valores.Distinct(StringComparer.CurrentCultureIgnoreCase)
            .OrderBy(v => v, StringComparer.CurrentCulture)
            .ToList();

        destino.Clear();
        destino.Add(Todos);
        foreach (var o in opcoes) destino.Add(o);

        // O null que o Clear acabou de escrever cai aqui junto com o filtro que sumiu do
        // período novo: os dois voltam para "Todos".
        if (escolhido is null || (escolhido != Todos && !destino.Contains(escolhido)))
        {
            escolhido = Todos;
            OnPropertyChanged(nomeDaPropriedade);
        }
    }

    private void Aplicar()
    {
        Linhas.Clear();
        foreach (var l in _carregadas.Where(Passa)) Linhas.Add(l);

        OnPropertyChanged(nameof(TemLinhas));
        OnPropertyChanged(nameof(Resumo));
        OnPropertyChanged(nameof(TemFiltro));
    }

    private bool Passa(LinhaLancamento l)
        => (Livre(Convenio) || l.Convenio == Convenio)
           && (Livre(Modalidade) || l.Modalidade == Modalidade)
           && (Livre(Operador) || l.Operador == Operador)
           && (!SomenteComPendencia || l.TemPendencia);

    private void Avisar(string texto, bool erro = false)
    {
        Mensagem = texto;
        MensagemEhErro = erro;
    }

    private static LinhaLancamento MontarLinha(Atendimento atendimento)
    {
        var faturaveis = atendimento.Codigos
            .Where(c => c.Status != StatusCodigo.NaoAplicavel)
            .ToList();

        // A guia que só libera depois é o assunto do produto. Ela vem marcada aqui: é a
        // última chance de alguém notar antes de o dia fechar e a guia virar pendência de
        // amanhã. O corte é pela data da PRÓPRIA linha, e não por hoje — numa lista de
        // período, "depois" tem de significar depois do atendimento, senão tudo o que é
        // antigo apareceria liberado.
        var depois = faturaveis
            .Where(c => !c.Baixado && c.DataPrevistaFaturamento > atendimento.Data)
            .OrderBy(c => c.DataPrevistaFaturamento)
            .ToList();

        var paciente = atendimento.Paciente;

        return new LinhaLancamento
        {
            AtendimentoId = atendimento.Id,
            Data = atendimento.Data,
            DataRotulo = atendimento.Data.ToString("dd/MM"),
            Paciente = paciente?.Nome ?? "—",
            Modalidade = atendimento.ModalidadeCodigo is { } cod
                ? CatalogoModalidades.Nome(cod)
                : ModalidadeInfo.NomeExibicao(atendimento.Modalidade),
            Convenio = paciente is null
                ? "—"
                : CatalogoConvenios.Nome(paciente.ConvenioCodigo ?? paciente.Convenio.ToString()),
            Numero = atendimento.Numero ?? $"#{atendimento.Id}",
            // "0 guias · todas liberadas" era afirmação falsa: o particular (e a sessão com
            // as guias suspensas) não tem guia NENHUMA — dizer "todas liberadas" sobre zero
            // é a garantia aparente de sempre.
            Guias = faturaveis.Count switch { 0 => "sem guia", 1 => "1 guia", var n => $"{n} guias" },
            Lancamento = DescreverLancamento(atendimento),
            Operador = string.IsNullOrWhiteSpace(atendimento.LancadoPor)
                ? SemRegistroDeOperador
                : atendimento.LancadoPor!,
            TemPendencia = depois.Count > 0,
            Pendencia = faturaveis.Count == 0
                ? "nada vai ao convênio"
                : depois.Count == 0
                    ? "todas liberadas"
                    : $"{depois.Count} libera(m) a partir de {depois[0].DataPrevistaFaturamento:dd/MM}"
        };
    }

    /// <summary>
    /// O operador de quem não ficou registro. É uma opção de FILTRO como as outras — e
    /// precisa ser: a pergunta "quantos lançamentos ninguém assinou?" é da direção, e sem
    /// isto as linhas antigas ficariam inalcançáveis pelo combo.
    /// </summary>
    private const string SemRegistroDeOperador = "sem registro";

    /// <summary>"por Ana às 14:32" — a autoria na conferência (parcela 58).</summary>
    private static string DescreverLancamento(Atendimento atendimento)
    {
        if (string.IsNullOrWhiteSpace(atendimento.LancadoPor))
            return "sem registro de quem lançou";

        return atendimento.LancadoEm is { } quando
            ? $"por {atendimento.LancadoPor} às {quando:HH:mm}"
            : $"por {atendimento.LancadoPor}";
    }
}
