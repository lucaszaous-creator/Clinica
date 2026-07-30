using System.Collections.ObjectModel;
using Clinica.Application.Servicos;
using Clinica.Desktop.Controls;
using Clinica.Desktop.Shell;
using Clinica.Desktop.Shell.Componentes;
using Clinica.Domain.Entities;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;

namespace Clinica.Financeiro.ViewModels;

/// <summary>Uma conta em aberto, como aparece na lista.</summary>
public sealed class LinhaConta
{
    public required int LancamentoId { get; init; }
    public required string Descricao { get; init; }
    public required string Categoria { get; init; }
    public required string Valor { get; init; }
    public required string Vencimento { get; init; }
    public required string Prazo { get; init; }
    public required bool Vencida { get; init; }
    public required bool EhSaida { get; init; }

    /// <summary>Gerada por conta recorrente — a lista diz de onde ela veio.</summary>
    public required bool Recorrente { get; init; }

    public static LinhaConta De(LancamentoFinanceiro l, DateOnly hoje)
    {
        var dias = l.DiasAteVencer(hoje);
        return new LinhaConta
        {
            LancamentoId = l.Id,
            Descricao = l.Descricao,
            Categoria = l.Categoria?.Nome ?? (l.Paciente?.Nome ?? "sem categoria"),
            Valor = l.Valor.ToString("C"),
            Vencimento = l.DataVencimento?.ToString("dd/MM/yyyy") ?? "—",
            // O prazo em palavras, porque é assim que se pensa nele no balcão: ninguém
            // calcula "10/08 menos hoje", pensa "venceu faz cinco dias".
            Prazo = dias switch
            {
                null => "sem vencimento",
                0 => "vence HOJE",
                1 => "vence amanhã",
                < 0 and var d => $"venceu faz {-d} dia(s)",
                var d => $"em {d} dia(s)"
            },
            Vencida = l.EstaVencido(hoje),
            EhSaida = l.Tipo == TipoLancamento.Saida,
            Recorrente = l.OrigemRecorrencia is not null
        };
    }
}

/// <summary>Uma conta recorrente do catálogo.</summary>
public sealed class LinhaRecorrente
{
    public required int RecorrenteId { get; init; }
    public required string Descricao { get; init; }
    public required string Valor { get; init; }
    public required string Ritmo { get; init; }
    public required string Vigencia { get; init; }
    public required bool Ativa { get; init; }
    public required string Situacao { get; init; }
    public required bool EhSaida { get; init; }

    public static LinhaRecorrente De(LancamentoRecorrente r) => new()
    {
        RecorrenteId = r.Id,
        Descricao = r.Descricao,
        Valor = r.Valor.ToString("C"),
        Ritmo = r.PeriodicidadeTexto,
        Vigencia = r.UltimoVencimento is { } fim
            ? $"de {r.PrimeiroVencimento:dd/MM/yyyy} até {fim:dd/MM/yyyy}"
            : $"desde {r.PrimeiroVencimento:dd/MM/yyyy}, sem prazo",
        Ativa = r.Ativa,
        Situacao = r.Ativa ? "Ativa" : "Inativa",
        EhSaida = r.Tipo == TipoLancamento.Saida
    };
}

/// <summary>
/// Contas a pagar e a receber (parcela 12).
///
/// Responde à pergunta que o financeiro faz toda segunda-feira e que o módulo não sabia
/// responder: "o que vence esta semana?". O caixa conhecia o que já tinha acontecido e o
/// que estava "previsto", mas previsto sem data de vencimento não tem quando.
///
/// A segunda metade da tela é a RECORRÊNCIA. Aluguel, luz, contador e mensalidade de
/// software eram redigitados todo mês, e conta redigitada é conta esquecida. O molde gera
/// lançamentos PREVISTOS — nunca realizados: o sistema sabe que o aluguel vence, não que
/// ele foi pago. Dar baixa sozinho encheria o caixa de saída que talvez não tenha saído.
///
/// A geração é um CLIQUE, não automática na abertura do app. Contas nascendo sozinhas
/// enquanto alguém abre a tela é exatamente o tipo de escrita que o balcão não vê
/// acontecer e depois não sabe explicar — a mesma razão pela qual o fechamento da sessão
/// é proposta confirmada.
/// </summary>
public sealed partial class ContasViewModel : ObservableObject
{
    private readonly IServiceScopeFactory _escopos;
    private readonly ISnackbarService _snackbar;
    private readonly IDialogoService _dialogo;

    public ObservableCollection<LinhaConta> Contas { get; } = [];
    public ObservableCollection<LinhaRecorrente> Recorrentes { get; } = [];

    /// <summary>
    /// Horizonte da lista, em dias. 30 por padrão: é o mês que a clínica enxerga de cada
    /// vez, e é a janela em que dá para fazer alguma coisa a respeito.
    /// </summary>
    [ObservableProperty] private int _horizonteDias = 30;

    public IReadOnlyList<int> Horizontes { get; } = [7, 15, 30, 60, 90];

    /// <summary>Null = tudo; senão só o que se paga ou só o que se recebe.</summary>
    [ObservableProperty] private TipoLancamento? _filtroTipo;

    // ---- Faixa de totais ----
    [ObservableProperty] private string _aPagarVencido = "—";
    [ObservableProperty] private string _aPagarAVencer = "—";
    [ObservableProperty] private string _aReceberVencido = "—";
    [ObservableProperty] private string _aReceberAVencer = "—";
    [ObservableProperty] private string _saldoPrevisto = "—";
    [ObservableProperty] private string _resumo = string.Empty;
    [ObservableProperty] private bool _temVencido;

    [ObservableProperty] private string? _mensagem;
    [ObservableProperty] private bool _mensagemEhErro;

    /// <summary>Metade visível da permissão; a que impede é o <c>Exigir</c> no comando.</summary>
    public bool PodeEditar => SessaoUsuario.Atual.Pode(Permissao.EditarFinanceiro);

    public ContasViewModel(
        IServiceScopeFactory escopos, ISnackbarService snackbar, IDialogoService dialogo)
    {
        _escopos = escopos;
        _snackbar = snackbar;
        _dialogo = dialogo;
        _ = CarregarAsync();
    }

    partial void OnHorizonteDiasChanged(int value) => _ = CarregarAsync();
    partial void OnFiltroTipoChanged(TipoLancamento? value) => _ = CarregarAsync();

    [RelayCommand]
    public async Task CarregarAsync()
    {
        try
        {
            Mensagem = null;
            MensagemEhErro = false;

            using var scope = _escopos.CreateScope();
            var contas = scope.ServiceProvider.GetRequiredService<ContasService>();

            var hoje = DateOnly.FromDateTime(DateTime.Today);
            var ate = hoje.AddDays(Math.Max(HorizonteDias, 0));

            Contas.Clear();
            foreach (var l in await contas.EmAbertoAsync(ate, FiltroTipo))
                Contas.Add(LinhaConta.De(l, hoje));

            Recorrentes.Clear();
            foreach (var r in await contas.RecorrentesAsync())
                Recorrentes.Add(LinhaRecorrente.De(r));

            // O resumo ignora o filtro de tipo de propósito: a faixa de totais é o retrato
            // do mês, e mudaria de significado se encolhesse junto com a lista.
            var resumo = await contas.ResumoAsync(hoje, ate);
            APagarVencido = resumo.APagarVencido.ToString("C");
            APagarAVencer = resumo.APagarAVencer.ToString("C");
            AReceberVencido = resumo.AReceberVencido.ToString("C");
            AReceberAVencer = resumo.AReceberAVencer.ToString("C");
            SaldoPrevisto = resumo.SaldoPrevisto.ToString("C");
            TemVencido = resumo.TemVencido;

            Resumo = Contas.Count == 0
                ? $"Nada em aberto vencendo nos próximos {HorizonteDias} dias."
                : resumo.TemVencido
                    ? $"{Contas.Count} conta(s) em aberto · {resumo.QuantidadeVencida} JÁ VENCIDA(S)."
                    : $"{Contas.Count} conta(s) em aberto, nenhuma vencida.";
        }
        catch (Exception ex)
        {
            Clinica.Application.Diagnostico.Registrar("Financeiro — contas não puderam ser lidas", ex);
            Erro($"Não foi possível ler as contas: {ex.Message}");
        }
    }

    // ---------------- Conta avulsa ----------------

    /// <summary>
    /// Registrar conta saiu da página e virou janela.
    ///
    /// A tela responde "o que vence esta semana" — é uma LISTA que se lê toda segunda de
    /// manhã. O formulário fixo tomava 360px da largura em todas as visitas, e ainda
    /// espremia nele o seletor de paciente da conta a receber, que precisa de espaço para
    /// buscar. Lista e um botão, como a tela de Pacientes do faturamento.
    /// </summary>
    [RelayCommand]
    private async Task NovaContaAsync()
    {
        SessaoUsuario.Atual.Exigir(Permissao.EditarFinanceiro, "lançar conta");

        var vm = new ContaEdicaoViewModel(_escopos);
        var janela = new Janelas.ContaWindow(vm)
        {
            Owner = System.Windows.Application.Current?.MainWindow
        };

        if (janela.ShowDialog() != true) return;
        _snackbar.Sucesso(vm.EhSaida ? "Conta a pagar registrada." : "Conta a receber registrada.");
        await CarregarAsync();
    }

    /// <summary>
    /// Dá baixa: a conta foi paga/recebida. É o <see cref="FinanceiroService.RealizarAsync"/>
    /// de sempre — a conta e o lançamento são a mesma coisa, vista pela data que interessa.
    /// </summary>
    [RelayCommand]
    private async Task BaixarAsync(LinhaConta? linha)
    {
        if (linha is null) return;

        try
        {
            SessaoUsuario.Atual.Exigir(Permissao.EditarFinanceiro, "dar baixa em conta");

            if (!_dialogo.Confirmar("Dar baixa",
                    $"Confirmar que \"{linha.Descricao}\" ({linha.Valor}) "
                    + (linha.EhSaida ? "foi paga" : "foi recebida") + " hoje?")) return;

            using var scope = _escopos.CreateScope();
            var financeiro = scope.ServiceProvider.GetRequiredService<FinanceiroService>();
            await financeiro.RealizarAsync(
                linha.LancamentoId, DateOnly.FromDateTime(DateTime.Today),
                operador: SessaoUsuario.Atual.Operador);

            _snackbar.Sucesso(linha.EhSaida ? "Conta paga." : "Conta recebida.");
            await CarregarAsync();
        }
        catch (Exception ex)
        {
            Clinica.Application.Diagnostico.Registrar("Financeiro — baixa da conta falhou", ex);
            Erro(ex.Message);
        }
    }

    /// <summary>Adia o vencimento em uma semana — o "renegociei o prazo" do dia a dia.</summary>
    [RelayCommand]
    private async Task AdiarAsync(LinhaConta? linha)
    {
        if (linha is null) return;

        try
        {
            SessaoUsuario.Atual.Exigir(Permissao.EditarFinanceiro, "reagendar conta");

            using var scope = _escopos.CreateScope();
            var contas = scope.ServiceProvider.GetRequiredService<ContasService>();
            await contas.ReagendarAsync(
                linha.LancamentoId, DateOnly.FromDateTime(DateTime.Today).AddDays(7),
                SessaoUsuario.Atual.Operador);

            _snackbar.Info($"\"{linha.Descricao}\" adiada para daqui a 7 dias.");
            await CarregarAsync();
        }
        catch (Exception ex)
        {
            Clinica.Application.Diagnostico.Registrar("Financeiro — conta não pôde ser reagendada", ex);
            Erro(ex.Message);
        }
    }

    // ---------------- Conta recorrente ----------------

    /// <summary>
    /// Cadastro e edição da conta fixa também saem da página. Ela é o MOLDE, não o
    /// lançamento: não entra em total nenhum, e mexer nela é tarefa de quando o contrato
    /// muda — não de todo dia.
    /// </summary>
    [RelayCommand]
    private async Task NovaRecorrenteAsync() => await AbrirRecorrenteAsync(0);

    [RelayCommand]
    private async Task EditarRecorrenteAsync(LinhaRecorrente? linha)
    {
        if (linha is null) return;
        await AbrirRecorrenteAsync(linha.RecorrenteId);
    }

    private async Task AbrirRecorrenteAsync(int recorrenteId)
    {
        SessaoUsuario.Atual.Exigir(Permissao.EditarFinanceiro, "cadastrar conta fixa");

        var vm = new RecorrenteEdicaoViewModel(_escopos, recorrenteId);
        var janela = new Janelas.RecorrenteWindow(vm)
        {
            Owner = System.Windows.Application.Current?.MainWindow
        };

        if (janela.ShowDialog() != true) return;

        _snackbar.Sucesso(recorrenteId == 0
            ? "Conta fixa cadastrada. Use \"Gerar\" para criar as previstas."
            // O aviso importa: quem muda o valor esperando corrigir a conta deste mês
            // precisa saber que o que já nasceu continua como estava.
            : "Conta fixa atualizada — vale para o que ainda não foi gerado.");
        await CarregarAsync();
    }

    /// <summary>
    /// Gera as contas previstas das recorrências ativas até o fim do horizonte.
    ///
    /// Clique, e não automático: contas nascendo sozinhas enquanto alguém abre a tela é
    /// escrita que o balcão não vê acontecer e depois não sabe explicar. É idempotente,
    /// então clicar duas vezes não cobra o aluguel duas vezes — e a tela DIZ quantas já
    /// existiam, porque "não fez nada" e "não rodou" precisam ser distinguíveis.
    /// </summary>
    [RelayCommand]
    private async Task GerarAsync()
    {
        try
        {
            SessaoUsuario.Atual.Exigir(Permissao.EditarFinanceiro, "gerar contas fixas");

            using var scope = _escopos.CreateScope();
            var contas = scope.ServiceProvider.GetRequiredService<ContasService>();

            var ate = DateOnly.FromDateTime(DateTime.Today).AddDays(Math.Max(HorizonteDias, 0));
            var r = await contas.GerarAsync(ate, SessaoUsuario.Atual.Operador);

            if (r.Avisos.Count > 0)
            {
                // Aviso não é sucesso: o que falhou aparece na mensagem inline, que fica
                // na tela, e não no snackbar, que some em 4 segundos.
                Erro($"{r.Geradas} conta(s) gerada(s), mas houve falha em: "
                     + string.Join(" · ", r.Avisos));
            }
            else
            {
                _snackbar.Sucesso(r.FezAlgo
                    ? $"{r.Geradas} conta(s) prevista(s) criada(s) até {ate:dd/MM/yyyy}."
                    : r.JaExistiam > 0
                        ? $"Nada a criar — as {r.JaExistiam} ocorrência(s) do período já existem."
                        : "Nenhuma conta fixa ativa com vencimento no período.");
            }

            await CarregarAsync();
        }
        catch (Exception ex)
        {
            Clinica.Application.Diagnostico.Registrar("Financeiro — geração de contas fixas falhou", ex);
            Erro(ex.Message);
        }
    }

    private void Erro(string mensagem)
    {
        Mensagem = mensagem;
        MensagemEhErro = true;
    }
}

/// <summary>
/// Conta avulsa a pagar ou a receber, na janela.
///
/// O campo que só existe em "A receber" é o **paciente**: conta a pagar não tem dono
/// desse tipo, e o seletor ali atrapalharia o lançamento mais comum. É ele que dá dono à
/// dívida — sem ele a tela de inadimplência ficaria vazia para sempre.
/// </summary>
public sealed partial class ContaEdicaoViewModel : ObservableObject
{
    private readonly IServiceScopeFactory _escopos;

    public ObservableCollection<CategoriaFinanceira> Categorias { get; } = [];

    /// <summary>Escolher paciente é um componente só — esta janela usa o de sempre.</summary>
    public SeletorPacienteViewModel Seletor { get; }

    [ObservableProperty] private string? _descricao;
    [ObservableProperty] private string? _valor;
    [ObservableProperty] private DateTime? _vencimento = DateTime.Today;
    [ObservableProperty] private bool _ehSaida = true;
    [ObservableProperty] private CategoriaFinanceira? _categoria;

    [ObservableProperty] private string? _mensagem;
    [ObservableProperty] private bool _mensagemEhErro;
    [ObservableProperty] private bool _salvando;

    /// <summary>
    /// O outro lado do par de rádios. É propriedade do VM, e não um conversor no XAML,
    /// porque o rádio precisa de leitura E escrita: um conversor de mão única deixaria
    /// "A receber" marcado sem nunca mudar o tipo — o que a tela mostra e o que ela grava
    /// seriam coisas diferentes.
    /// </summary>
    public bool EhEntrada
    {
        get => !EhSaida;
        set => EhSaida = !value;
    }

    public event Action? Concluido;

    public ContaEdicaoViewModel(IServiceScopeFactory escopos)
    {
        _escopos = escopos;
        Seletor = new SeletorPacienteViewModel(escopos);
        _ = CarregarCategoriasAsync();
    }

    partial void OnEhSaidaChanged(bool value) => OnPropertyChanged(nameof(EhEntrada));

    private async Task CarregarCategoriasAsync()
    {
        try
        {
            using var scope = _escopos.CreateScope();
            var financeiro = scope.ServiceProvider.GetRequiredService<FinanceiroService>();

            Categorias.Clear();
            foreach (var c in await financeiro.CategoriasAsync())
                if (c.Ativa) Categorias.Add(c);
        }
        catch (Exception ex)
        {
            Clinica.Application.Diagnostico.Registrar(
                "Financeiro — categorias não puderam ser lidas", ex);
            Erro(ex.Message);
        }
    }

    [RelayCommand]
    private async Task SalvarAsync()
    {
        Mensagem = null;
        MensagemEhErro = false;

        if (string.IsNullOrWhiteSpace(Descricao))
        {
            Erro("Diga o que é a conta (aluguel, fornecedor, convênio…).");
            return;
        }
        if (!Valores.TentarLerDecimal(Valor, out var valor) || valor <= 0m)
        {
            Erro("Informe o valor da conta (ex.: 1.250,00).");
            return;
        }
        if (Vencimento is not { } venc)
        {
            Erro("Informe a data de vencimento — é ela que faz a conta aparecer na lista.");
            return;
        }

        try
        {
            Salvando = true;
            SessaoUsuario.Atual.Exigir(Permissao.EditarFinanceiro, "lançar conta");

            using var scope = _escopos.CreateScope();
            var contas = scope.ServiceProvider.GetRequiredService<ContasService>();

            await contas.LancarContaAsync(
                EhSaida ? TipoLancamento.Saida : TipoLancamento.Entrada,
                Descricao!,
                valor,
                DateOnly.FromDateTime(venc),
                categoriaId: Categoria?.Id,
                operador: SessaoUsuario.Atual.Operador,
                // Só na conta a RECEBER: o modo pode ter sido trocado depois de escolher
                // alguém, e gravar o paciente num aluguel deixaria a inadimplência
                // apontando para quem não deve nada.
                pacienteId: EhSaida ? null : Seletor.Selecionado?.Id);

            Concluido?.Invoke();
        }
        catch (Exception ex)
        {
            Clinica.Application.Diagnostico.Registrar("Financeiro — conta não pôde ser lançada", ex);
            Erro(ex.Message);
        }
        finally
        {
            Salvando = false;
        }
    }

    private void Erro(string mensagem)
    {
        Mensagem = mensagem;
        MensagemEhErro = true;
    }
}

/// <summary>
/// A conta fixa (aluguel, luz, contador, software), na janela.
///
/// Ela é um MOLDE, não um lançamento: não entra em total nenhum — quem entra é a conta
/// prevista que ela gera, e que dali em diante tem vida própria. Por isso editar aqui
/// vale para o que ainda NÃO foi gerado: corrigir o molde não reescreve março, que já
/// foi pago e conciliado.
/// </summary>
public sealed partial class RecorrenteEdicaoViewModel : ObservableObject
{
    private readonly IServiceScopeFactory _escopos;
    private readonly int _recorrenteId;

    public ObservableCollection<CategoriaFinanceira> Categorias { get; } = [];

    public IReadOnlyList<PeriodicidadeRecorrencia> Periodicidades { get; } =
        Enum.GetValues<PeriodicidadeRecorrencia>();

    public string Titulo => _recorrenteId == 0 ? "Conta fixa nova" : "Editar conta fixa";

    /// <summary>Só na criação: a periodicidade e o 1º vencimento ancoram a série inteira.</summary>
    public bool EhNova => _recorrenteId == 0;

    [ObservableProperty] private string? _descricao;
    [ObservableProperty] private string? _valor;
    [ObservableProperty] private PeriodicidadeRecorrencia _periodicidade = PeriodicidadeRecorrencia.Mensal;
    [ObservableProperty] private DateTime? _primeiroVencimento = DateTime.Today;
    [ObservableProperty] private DateTime? _ultimoVencimento;
    [ObservableProperty] private bool _ehSaida = true;
    [ObservableProperty] private bool _ativa = true;
    [ObservableProperty] private CategoriaFinanceira? _categoria;

    [ObservableProperty] private string? _mensagem;
    [ObservableProperty] private bool _mensagemEhErro;
    [ObservableProperty] private bool _salvando;

    /// <summary>Mesmo par de rádios do formulário da conta avulsa.</summary>
    public bool EhEntrada
    {
        get => !EhSaida;
        set => EhSaida = !value;
    }

    public event Action? Concluido;

    public RecorrenteEdicaoViewModel(IServiceScopeFactory escopos, int recorrenteId)
    {
        _escopos = escopos;
        _recorrenteId = recorrenteId;
        _ = CarregarAsync();
    }

    partial void OnEhSaidaChanged(bool value) => OnPropertyChanged(nameof(EhEntrada));

    private async Task CarregarAsync()
    {
        try
        {
            using var scope = _escopos.CreateScope();
            var financeiro = scope.ServiceProvider.GetRequiredService<FinanceiroService>();

            Categorias.Clear();
            foreach (var c in await financeiro.CategoriasAsync())
                if (c.Ativa) Categorias.Add(c);

            if (_recorrenteId == 0) return;

            var contas = scope.ServiceProvider.GetRequiredService<ContasService>();
            if ((await contas.RecorrentesAsync()).FirstOrDefault(x => x.Id == _recorrenteId) is not { } r)
                return;

            Descricao = r.Descricao;
            Valor = r.Valor.ToString("0.##");
            Periodicidade = r.Periodicidade;
            PrimeiroVencimento = r.PrimeiroVencimento.ToDateTime(TimeOnly.MinValue);
            UltimoVencimento = r.UltimoVencimento?.ToDateTime(TimeOnly.MinValue);
            EhSaida = r.Tipo == TipoLancamento.Saida;
            Ativa = r.Ativa;
            Categoria = Categorias.FirstOrDefault(c => c.Id == r.CategoriaFinanceiraId);
        }
        catch (Exception ex)
        {
            Clinica.Application.Diagnostico.Registrar(
                "Financeiro — conta fixa não pôde ser aberta", ex);
            Erro(ex.Message);
        }
    }

    [RelayCommand]
    private async Task SalvarAsync()
    {
        Mensagem = null;
        MensagemEhErro = false;

        if (string.IsNullOrWhiteSpace(Descricao))
        {
            Erro("Diga que conta se repete (aluguel, luz, contador…).");
            return;
        }
        if (!Valores.TentarLerDecimal(Valor, out var valor) || valor <= 0m)
        {
            Erro("Informe o valor de cada ocorrência (ex.: 3.200,00).");
            return;
        }
        if (PrimeiroVencimento is not { } primeiro)
        {
            Erro("Informe o primeiro vencimento — é ele que ancora toda a série.");
            return;
        }

        try
        {
            Salvando = true;
            SessaoUsuario.Atual.Exigir(Permissao.EditarFinanceiro, "cadastrar conta fixa");

            using var scope = _escopos.CreateScope();
            var contas = scope.ServiceProvider.GetRequiredService<ContasService>();

            var fim = UltimoVencimento is { } u ? DateOnly.FromDateTime(u) : (DateOnly?)null;

            if (_recorrenteId == 0)
                await contas.CriarRecorrenteAsync(
                    Descricao!,
                    EhSaida ? TipoLancamento.Saida : TipoLancamento.Entrada,
                    valor,
                    Periodicidade,
                    DateOnly.FromDateTime(primeiro),
                    fim,
                    categoriaId: Categoria?.Id,
                    operador: SessaoUsuario.Atual.Operador);
            else
                await contas.AtualizarRecorrenteAsync(
                    _recorrenteId, Descricao!, valor, fim, Ativa,
                    categoriaId: Categoria?.Id, operador: SessaoUsuario.Atual.Operador);

            Concluido?.Invoke();
        }
        catch (Exception ex)
        {
            Clinica.Application.Diagnostico.Registrar("Financeiro — conta fixa não pôde ser salva", ex);
            Erro(ex.Message);
        }
        finally
        {
            Salvando = false;
        }
    }

    private void Erro(string mensagem)
    {
        Mensagem = mensagem;
        MensagemEhErro = true;
    }
}
