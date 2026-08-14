using System.Collections.ObjectModel;
using Clinica.Application.Servicos;
using Clinica.Desktop.Controls;
using Clinica.Desktop.Shell;
using Clinica.Desktop.Shell.Componentes;
using Clinica.Domain.Entities;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Clinica.Financeiro.ViewModels;

/// <summary>Um item do estoque na tela, com saldo e alerta já resolvidos.</summary>
public sealed class LinhaEstoque
{
    public required int Id { get; init; }
    public required string Nome { get; init; }
    public required string Saldo { get; init; }
    public required string Minimo { get; init; }
    public required string Custo { get; init; }
    public required bool AbaixoDoMinimo { get; init; }
    public required bool Ativo { get; init; }
}

/// <summary>Um lote vencido ou a vencer.</summary>
public sealed class LinhaValidade
{
    public required string Item { get; init; }
    public required string Validade { get; init; }
    public required string Quantidade { get; init; }
    public required string Situacao { get; init; }
    public required bool Vencido { get; init; }
}

/// <summary>Quanto uma sessão gastou de insumo — a linha da aba de custo.</summary>
public sealed class LinhaCustoSessao
{
    public required string Data { get; init; }
    public required string Paciente { get; init; }
    public required string Custo { get; init; }
    public required string Itens { get; init; }
}

/// <summary>
/// Estoque de insumos (feature 10): saldo, alerta de reposição e de validade.
///
/// A tela abre pelos ALERTAS, não pela lista: quem entra aqui quer saber o que está
/// faltando e o que vai vencer — a lista completa é consulta, não é o motivo da visita.
/// </summary>
public sealed partial class EstoqueViewModel : ObservableObject
{
    private readonly EstoqueService _estoque;
    private readonly ISnackbarService _snackbar;
    private readonly IDialogoService _dialogo;

    public ObservableCollection<LinhaEstoque> Itens { get; } = [];
    public ObservableCollection<LinhaValidade> Validades { get; } = [];
    public ObservableCollection<LinhaCustoSessao> CustosSessao { get; } = [];

    // ---- Custo por sessão (parcela 25) ----
    [ObservableProperty] private DateTime _custoInicio = DateTime.Today.AddDays(-30);
    [ObservableProperty] private DateTime _custoFim = DateTime.Today;
    [ObservableProperty] private string _custoTotal = "—";
    [ObservableProperty] private string _custoMedio = "—";
    [ObservableProperty] private string _custoSessoes = "—";
    [ObservableProperty] private string _custoResumo = string.Empty;
    [ObservableProperty] private bool _semCusto;

    [ObservableProperty] private bool _carregando;

    /// <summary>
    /// A leitura FALHOU — o terceiro estado. Sem ele, lista vazia por erro fica idêntica
    /// a lista vazia por não haver nada, e o aviso de falha some junto com o snackbar.
    /// </summary>
    [ObservableProperty] private bool _naoVerificado;
    [ObservableProperty] private string _mensagem = string.Empty;
    [ObservableProperty] private bool _mensagemEhErro;
    [ObservableProperty] private string _resumo = "—";
    [ObservableProperty] private bool _semAlertas;

    /// <summary>
    /// Habilita os botões de escrita da tela. É a metade VISÍVEL da permissão: o
    /// botão apagado explica por que não dá; a guarda no comando é que impede.
    /// Só desabilitar seria enfeite — um atalho de teclado passaria direto.
    /// </summary>
    public bool PodeEditarFinanceiro => SessaoUsuario.Atual.Pode(Permissao.EditarFinanceiro);

    public EstoqueViewModel(EstoqueService estoque, ISnackbarService snackbar, IDialogoService dialogo)
    {
        _estoque = estoque;
        _snackbar = snackbar;
        _dialogo = dialogo;
        _ = CarregarAsync();
    }

    /// <summary>
    /// Abre VALIDADES E MÍNIMOS (parcela 49). Era uma faixa de 380 px permanente ao lado
    /// do estoque — e no dia em que não há nada vencendo, que é a maioria, ela gastava um
    /// terço da tela para escrever "nada vencendo".
    ///
    /// O número continua no resumo do cabeçalho: quem abre o estoque vê QUANTOS sem
    /// clicar; o clique é para ver QUAIS.
    /// </summary>
    [RelayCommand]
    private void AbrirValidades()
    {
        new Janelas.ValidadesEstoqueWindow(this)
        {
            Owner = System.Windows.Application.Current?.MainWindow
        }.ShowDialog();
    }

    /// <summary>
    /// Descarte de resposta fora de ordem (parcela 50): dois "Atualizar" seguidos num
    /// banco remoto podem responder invertidos, e a leitura mais velha sobrescreveria a
    /// nova — o saldo da tela mentiria sobre a prateleira.
    /// </summary>
    private int _geracaoCarga;

    [RelayCommand]
    private async Task CarregarAsync()
    {
        var geracao = ++_geracaoCarga;

        try
        {
            Carregando = true;
            NaoVerificado = false;
            Mensagem = string.Empty;
            MensagemEhErro = false;

            var saldos = await _estoque.SaldosAsync();

            // Chegou tarde: outra carga mais nova já foi pedida.
            if (geracao != _geracaoCarga) return;

            Itens.Clear();
            foreach (var s in saldos)
                Itens.Add(new LinhaEstoque
                {
                    Id = s.ItemId,
                    Nome = s.Nome,
                    Saldo = s.SaldoRotulo,
                    Minimo = s.EstoqueMinimo > 0 ? $"{s.EstoqueMinimo:0.##} {s.Unidade}" : "—",
                    Custo = s.CustoMedio is { } custo ? custo.ToString("C") : "—",
                    AbaixoDoMinimo = s.AbaixoDoMinimo,
                    Ativo = s.Ativo
                });

            var hoje = DateOnly.FromDateTime(DateTime.Today);
            var validades = await _estoque.ValidadesAsync(hoje);

            // Chegou tarde: outra carga mais nova já foi pedida.
            if (geracao != _geracaoCarga) return;

            Validades.Clear();
            foreach (var v in validades)
                Validades.Add(new LinhaValidade
                {
                    Item = v.Nome,
                    Validade = v.Validade.ToString("dd/MM/yyyy"),
                    Quantidade = $"{v.Quantidade:0.##}",
                    Situacao = v.Vencido(hoje)
                        ? "VENCIDO"
                        : $"vence em {v.DiasRestantes(hoje)} dia(s)",
                    Vencido = v.Vencido(hoje)
                });

            var faltando = saldos.Count(s => s.AbaixoDoMinimo);
            SemAlertas = faltando == 0 && validades.Count == 0;
            Resumo = SemAlertas
                ? $"{saldos.Count} item(ns) no estoque — nada abaixo do mínimo nem vencendo."
                : $"{faltando} item(ns) para repor · {validades.Count} lote(s) vencendo ou vencidos.";

            await CarregarCustosAsync();
        }
        catch (Exception ex)
        {
            if (geracao != _geracaoCarga) return;

            NaoVerificado = true;
            Clinica.Application.Diagnostico.Registrar("Financeiro — estoque não pôde ser carregado", ex);
            Erro($"Não foi possível carregar o estoque: {ex.Message}");
        }
        finally
        {
            // A carga superada não apaga o "Carregando" da que ainda está no ar.
            if (geracao == _geracaoCarga) Carregando = false;
        }
    }

    partial void OnCustoInicioChanged(DateTime value) => _ = CarregarCustosAsync();

    partial void OnCustoFimChanged(DateTime value) => _ = CarregarCustosAsync();

    /// <summary>
    /// Descarte de resposta fora de ordem (parcela 50): ajustar as duas datas do período
    /// dispara duas cargas seguidas, e a do período antigo respondendo por último deixaria
    /// o custo de um recorte sob as datas do outro.
    /// </summary>
    private int _geracaoCustos;

    /// <summary>
    /// Quanto custou cada sessão em insumo, no período.
    ///
    /// `CustoDoAtendimentoAsync` existia desde a parcela 4 e **nenhuma tela o lia**: a
    /// parcela 6 ligou a baixa por sessão justamente para este número deixar de responder
    /// zero, e ele continuou sem porta. Serviço testado sem chamador passa no CI e não faz
    /// nada na clínica — é o defeito que mais se repete neste projeto.
    /// </summary>
    [RelayCommand]
    private async Task CarregarCustosAsync()
    {
        var geracao = ++_geracaoCustos;

        try
        {
            var de = DateOnly.FromDateTime(CustoInicio);
            var ate = DateOnly.FromDateTime(CustoFim);

            if (ate < de)
            {
                Erro("A data final do custo é anterior à inicial.");
                return;
            }

            var sessoes = await _estoque.CustosDeSessaoAsync(de, ate);
            var resumo = await _estoque.ResumoCustoSessoesAsync(de, ate);

            // Chegou tarde: outra carga mais nova já foi pedida.
            if (geracao != _geracaoCustos) return;

            CustosSessao.Clear();
            foreach (var s in sessoes)
                CustosSessao.Add(new LinhaCustoSessao
                {
                    Data = s.Data.ToString("dd/MM/yyyy"),
                    Paciente = string.IsNullOrWhiteSpace(s.Paciente) ? "(paciente não informado)" : s.Paciente!,
                    Custo = s.Custo.ToString("C"),
                    Itens = s.Itens.Count == 0 ? "—" : string.Join(" · ", s.Itens)
                });

            CustoSessoes = resumo.Sessoes.ToString();
            CustoTotal = resumo.Total.ToString("C");
            // "—" e não "R$ 0,00": período sem baixa por sessão não tem média zero, tem
            // média nenhuma — e uma sessão de graça é justamente o que o número não diz.
            CustoMedio = resumo.MedioPorSessao is { } medio ? medio.ToString("C") : "—";
            SemCusto = resumo.Sessoes == 0;

            CustoResumo = SemCusto
                ? "Nenhuma sessão baixou insumo neste período. A baixa por sessão sai do "
                  + "fechamento do atendimento, na Recepção."
                : $"{resumo.Sessoes} sessão(ões) com insumo · mais cara: "
                  + $"{resumo.MaisCara!.Custo:C} em {resumo.MaisCara.Data:dd/MM/yyyy}.";
        }
        catch (Exception ex)
        {
            if (geracao != _geracaoCustos) return;

            Clinica.Application.Diagnostico.Registrar(
                "Financeiro — custo por sessão não pôde ser lido", ex);
            Erro($"Não foi possível ler o custo por sessão: {ex.Message}");
        }
    }

    /// <summary>
    /// Cadastro e edição do item saem da PÁGINA e vão para uma janela.
    ///
    /// A tela de lista existe para responder "como está o estoque"; o formulário de
    /// cadastro ocupava a lateral em todas as visitas para uma tarefa que acontece
    /// poucas vezes por mês — e empurrava a lista, que é o motivo da visita, para um
    /// terço da largura. É o mesmo desenho da tela de Pacientes do faturamento: a lista
    /// e um botão.
    /// </summary>
    [RelayCommand]
    private async Task NovoItemAsync() => await AbrirItemAsync(null);

    [RelayCommand]
    private async Task EditarItemAsync(LinhaEstoque? linha)
    {
        if (linha is null) return;
        await AbrirItemAsync(linha.Id);
    }

    private async Task AbrirItemAsync(int? itemId)
    {
        SessaoUsuario.Atual.Exigir(Permissao.EditarFinanceiro, "mexer no estoque");

        var vm = new ItemEstoqueEdicaoViewModel(_estoque, itemId);
        var janela = new Janelas.ItemEstoqueWindow(vm)
        {
            Owner = System.Windows.Application.Current?.MainWindow
        };

        if (janela.ShowDialog() != true) return;
        _snackbar.Sucesso(itemId is null ? "Item cadastrado." : "Item atualizado.");
        await CarregarAsync();
    }

    [RelayCommand]
    private async Task MovimentarAsync(LinhaEstoque? linha)
    {
        if (linha is null) return;

        SessaoUsuario.Atual.Exigir(Permissao.EditarFinanceiro, "mexer no estoque");

        var vm = new MovimentoEstoqueViewModel(_estoque, linha.Id, linha.Nome);
        var janela = new Janelas.MovimentoEstoqueWindow(vm)
        {
            Owner = System.Windows.Application.Current?.MainWindow
        };

        if (janela.ShowDialog() != true) return;
        _snackbar.Sucesso("Movimento registrado.");
        await CarregarAsync();
    }

    /// <summary>
    /// Acerto de INVENTÁRIO: a contagem física não bateu com o saldo do sistema.
    ///
    /// Até aqui a clínica só podia lançar `Perda` quando a contagem não fechava — e perda
    /// é uma AFIRMAÇÃO: alguém quebrou, venceu ou extraviou. Quando a contagem acha A
    /// MAIS, chamar de perda mente sobre o que aconteceu.
    ///
    /// Pede a quantidade CONTADA, nunca a diferença: quem está com a caixa na mão conta
    /// "tenho 37", e pedir "então ajusta 4 para baixo" é exigir a conta que o sistema
    /// sabe fazer e a pessoa erra.
    /// </summary>
    [RelayCommand]
    private async Task InventariarAsync(LinhaEstoque? linha)
    {
        if (linha is null) return;

        SessaoUsuario.Atual.Exigir(Permissao.EditarFinanceiro, "acertar o inventário");

        var contado = _dialogo.PerguntarTexto(
            $"Inventário de {linha.Nome}",
            $"O sistema diz {linha.Saldo}. Quanto você CONTOU na prateleira?\n\n"
            + "Informe a quantidade contada — o sistema calcula a diferença. Se a "
            + "contagem bater, nada é registrado.");
        if (string.IsNullOrWhiteSpace(contado)) return;

        if (!Valores.TentarLerDecimal(contado, out var quantidade) || quantidade < 0)
        {
            Erro("Informe a quantidade contada (não pode ser negativa).");
            return;
        }

        var motivo = _dialogo.PerguntarTexto(
            "Por que a contagem não bateu?",
            "Diferença de inventário sem motivo é indistinguível de erro de digitação seis "
            + "meses depois. Escreva o que você encontrou (ex.: “frasco quebrado não "
            + "lançado”, “entrada digitada em dobro em maio”).");
        if (string.IsNullOrWhiteSpace(motivo)) return;

        try
        {
            var movimento = await _estoque.AjustarInventarioAsync(
                linha.Id, quantidade, motivo, SessaoUsuario.Atual.Operador);

            if (movimento is null)
            {
                // Contagem igual ao saldo não vira movimento: registrar um ajuste de zero
                // sujaria o extrato com linhas que não mudam nada.
                _snackbar.Info("A contagem bateu com o sistema — nada a ajustar.");
                return;
            }

            _snackbar.Sucesso(movimento.AjusteParaCima == true
                ? $"Ajuste para cima: {movimento.Quantidade:0.##} a mais no saldo."
                : $"Ajuste para baixo: {movimento.Quantidade:0.##} a menos no saldo.");
            await CarregarAsync();
        }
        catch (Exception ex)
        {
            Clinica.Application.Diagnostico.Registrar(
                "Financeiro — inventário não pôde ser ajustado", ex);
            Erro(ex.Message);
        }
    }

    /// <summary>
    /// A lista de compras: o que está abaixo do mínimo e QUANTO comprar.
    ///
    /// `AbaixoDoMinimoAsync` existia no serviço desde a parcela 4 e **nenhuma tela a
    /// chamava** — a tela marcava a linha em vermelho e deixava a conta para quem fosse
    /// ao fornecedor. Quem vai ao fornecedor não leva a tela: leva um papel.
    ///
    /// A sugestão é **o dobro do mínimo menos o saldo**, e a tela diz isso: repor só até
    /// o mínimo faz o item voltar ao alerta na semana seguinte.
    /// </summary>
    [RelayCommand]
    private async Task ListaDeComprasAsync()
    {
        try
        {
            var faltando = await _estoque.AbaixoDoMinimoAsync();

            if (faltando.Count == 0)
            {
                _snackbar.Info("Nada abaixo do mínimo — não há o que comprar.");
                return;
            }

            var csv = ExportacaoCsv.Montar(
                ["Item", "Unidade", "Saldo", "Mínimo", "Sugestão de compra", "Custo médio"],
                faltando.Select(s => new[]
                {
                    s.Nome,
                    s.Unidade,
                    $"{s.Saldo:0.##}",
                    $"{s.EstoqueMinimo:0.##}",
                    $"{Math.Max(s.EstoqueMinimo * 2 - s.Saldo, 0m):0.##}",
                    s.CustoMedio is { } custo ? custo.ToString("C") : "—"
                }));

            var erro = await ImpressaoPdf.SalvarEAbrirAsync(
                csv,
                ImpressaoPdf.NomeSeguro($"lista-de-compras-{DateTime.Today:yyyy-MM-dd}.csv"),
                "CSV (*.csv)|*.csv", ".csv");

            if (erro is not null) Erro(erro);
        }
        catch (Exception ex)
        {
            Clinica.Application.Diagnostico.Registrar(
                "Financeiro — lista de compras não pôde ser gerada", ex);
            Erro(ex.Message);
        }
    }

    [RelayCommand]
    private async Task ExcluirItemAsync(LinhaEstoque? linha)
    {
        if (linha is null) return;

        SessaoUsuario.Atual.Exigir(Permissao.EditarFinanceiro, "mexer no estoque");
        if (!_dialogo.ConfirmarPerigo("Excluir item",
                $"Apagar \"{linha.Nome}\" e TODO o histórico de movimentos dele? "
                + "Item que já se movimentou normalmente deve ser inativado, não apagado.")) return;

        try
        {
            await _estoque.ExcluirItemAsync(linha.Id);
            _snackbar.Info("Item excluído.");
            await CarregarAsync();
        }
        catch (Exception ex)
        {
            Clinica.Application.Diagnostico.Registrar("Financeiro — item de estoque não pôde ser excluído", ex);
            Erro(ex.Message);
        }
    }

    private void Erro(string texto)
    {
        Mensagem = texto;
        MensagemEhErro = true;
    }
}

/// <summary>
/// Cadastro do item de estoque, na janela: nome, unidade, mínimo e se está ativo.
///
/// Serve para criar e para CORRIGIR. Antes da mudança de UX o formulário só criava —
/// item cadastrado com o nome errado ou sem mínimo ficava assim para sempre, porque a
/// única alternativa era excluir (levando junto todo o histórico de movimentos) e
/// cadastrar de novo.
/// </summary>
public sealed partial class ItemEstoqueEdicaoViewModel : ObservableObject
{
    private readonly EstoqueService _estoque;
    private readonly int? _itemId;

    public string Titulo => _itemId is null ? "Item novo" : "Editar item";

    [ObservableProperty] private string? _nome;
    [ObservableProperty] private string? _unidade = "un";
    [ObservableProperty] private string? _minimo;
    [ObservableProperty] private bool _ativo = true;

    [ObservableProperty] private string _mensagem = string.Empty;
    [ObservableProperty] private bool _mensagemEhErro;
    [ObservableProperty] private bool _salvando;

    public event Action? Concluido;

    public ItemEstoqueEdicaoViewModel(EstoqueService estoque, int? itemId = null)
    {
        _estoque = estoque;
        _itemId = itemId;
        if (itemId is not null) _ = CarregarAsync();
    }

    private async Task CarregarAsync()
    {
        try
        {
            if (await _estoque.ObterItemAsync(_itemId!.Value) is not { } item) return;

            Nome = item.Nome;
            Unidade = item.Unidade;
            // Mínimo zero é "sem mínimo": mostrar "0" faria a clínica achar que há um
            // alerta configurado quando não há.
            Minimo = item.EstoqueMinimo > 0 ? item.EstoqueMinimo.ToString("0.##") : null;
            Ativo = item.Ativo;
        }
        catch (Exception ex)
        {
            Clinica.Application.Diagnostico.Registrar(
                "Financeiro — item de estoque não pôde ser carregado", ex);
            Erro(ex.Message);
        }
    }

    [RelayCommand]
    private async Task SalvarAsync()
    {
        Mensagem = string.Empty;
        MensagemEhErro = false;

        try
        {
            Salvando = true;

            // `TentarLerQuantidade` e não `decimal.TryParse`: o cru lê na cultura da
            // MÁQUINA, e em pt-BR o ponto é separador de milhar — "1.5" viraria mínimo 15.
            // Aqui é a quantidade e não o valor, porque mínimo ZERO é legítimo ("não tenho
            // mínimo para este item") e o leitor de dinheiro recusa zero.
            decimal minimo = 0m;
            if (!string.IsNullOrWhiteSpace(Minimo) && !Valores.TentarLerQuantidade(Minimo, out minimo))
                throw new InvalidOperationException("O mínimo tem de ser um número.");

            await _estoque.SalvarItemAsync(new ItemEstoque
            {
                Id = _itemId ?? 0,
                Nome = Nome ?? string.Empty,
                Unidade = string.IsNullOrWhiteSpace(Unidade) ? "un" : Unidade!,
                EstoqueMinimo = minimo,
                Ativo = Ativo
            }, SessaoUsuario.Atual.Operador);

            Concluido?.Invoke();
        }
        catch (Exception ex)
        {
            Clinica.Application.Diagnostico.Registrar(
                "Financeiro — item de estoque não pôde ser salvo", ex);
            Erro(ex.Message);
        }
        finally
        {
            Salvando = false;
        }
    }

    private void Erro(string texto)
    {
        Mensagem = texto;
        MensagemEhErro = true;
    }
}

/// <summary>Entrada, baixa ou perda de um item — o formulário curto da janela.</summary>
public sealed partial class MovimentoEstoqueViewModel : ObservableObject
{
    private readonly EstoqueService _estoque;
    private readonly int _itemId;

    public string Item { get; }

    public IReadOnlyList<TipoMovimentoEstoque> Tipos { get; } = Enum.GetValues<TipoMovimentoEstoque>();

    [ObservableProperty] private TipoMovimentoEstoque _tipo = TipoMovimentoEstoque.Entrada;
    [ObservableProperty] private string? _quantidade;
    [ObservableProperty] private string? _custoUnitario;
    [ObservableProperty] private DateTime _data = DateTime.Today;
    [ObservableProperty] private DateTime? _validade;
    [ObservableProperty] private string? _lote;
    [ObservableProperty] private string? _observacao;

    [ObservableProperty] private string _mensagem = string.Empty;
    [ObservableProperty] private bool _mensagemEhErro;
    [ObservableProperty] private bool _salvando;

    /// <summary>Custo e validade só fazem sentido na entrada — é ela que traz o lote.</summary>
    public bool EhEntrada => Tipo == TipoMovimentoEstoque.Entrada;

    public event Action? Concluido;

    public MovimentoEstoqueViewModel(EstoqueService estoque, int itemId, string item)
    {
        _estoque = estoque;
        _itemId = itemId;
        Item = item;
    }

    partial void OnTipoChanged(TipoMovimentoEstoque value) => OnPropertyChanged(nameof(EhEntrada));

    [RelayCommand]
    private async Task SalvarAsync()
    {
        Mensagem = string.Empty;
        MensagemEhErro = false;

        try
        {
            Salvando = true;

            // Os dois pelo leitor do projeto (ver o comentário do mínimo, acima): o
            // `decimal.TryParse` cru lê na cultura da MÁQUINA, e em pt-BR "0.5" de um
            // frasco entra no estoque como 5 unidades e "12.50" como custo de R$ 1.250.
            // `TentarLerQuantidade` aceita a FRAÇÃO, que é o caso normal do insumo, e
            // devolve zero para o campo vazio — daí o vazio ser recusado aqui, onde
            // movimento sem quantidade não é movimento.
            if (string.IsNullOrWhiteSpace(Quantidade)
                || !Valores.TentarLerQuantidade(Quantidade, out var quantidade))
                throw new InvalidOperationException("Informe a quantidade (um número).");

            decimal? custo = null;
            if (EhEntrada && !string.IsNullOrWhiteSpace(CustoUnitario))
            {
                if (!Valores.TentarLerValor(CustoUnitario, out var lido))
                    throw new InvalidOperationException("Não entendi o custo: use algo como 12,50.");
                custo = lido;
            }

            if (Tipo == TipoMovimentoEstoque.Perda && string.IsNullOrWhiteSpace(Observacao))
                throw new InvalidOperationException(
                    "Perda sem motivo escrito vira estoque que não bate — diga o que houve.");

            await _estoque.MovimentarAsync(new MovimentoEstoque
            {
                ItemEstoqueId = _itemId,
                Tipo = Tipo,
                Quantidade = quantidade,
                CustoUnitario = custo,
                Data = DateOnly.FromDateTime(Data),
                Validade = EhEntrada && Validade is { } v ? DateOnly.FromDateTime(v) : null,
                Lote = Lote,
                Observacao = Observacao
            }, SessaoUsuario.Atual.Operador);

            Concluido?.Invoke();
        }
        catch (Exception ex)
        {
            Clinica.Application.Diagnostico.Registrar("Financeiro — movimento de estoque não pôde ser salvo", ex);
            Mensagem = ex.Message;
            MensagemEhErro = true;
        }
        finally
        {
            Salvando = false;
        }
    }
}
