using System.Collections.ObjectModel;
using Clinica.Application.Servicos;
using Clinica.Desktop.Controls;
using Clinica.Desktop.Shell;
using Clinica.Domain;
using Clinica.Domain.Entities;
using Clinica.Domain.Regras;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using Clinica.Desktop.Shell.Componentes;

namespace Clinica.Gerente.ViewModels;

/// <summary>Uma linha da tabela de preço.</summary>
public sealed class LinhaPreco
{
    public required int PrecoId { get; init; }
    public required string Convenio { get; init; }
    public required string Descricao { get; init; }
    public required string Valor { get; init; }
    public required string Vigencia { get; init; }
    public required string Situacao { get; init; }

    /// <summary>Vigente HOJE — é o valor que a conciliação está propondo agora.</summary>
    public required bool ValendoAgora { get; init; }

    public static LinhaPreco De(PrecoConvenio p, DateOnly hoje) => new()
    {
        PrecoId = p.Id,
        // O NOME da operadora na tela, o código no cadastro: o nome é o que a direção
        // reconhece, o código é a identidade que a conciliação casa.
        Convenio = CatalogoConvenios.Nome(p.ConvenioCodigo),
        // Sem RotulosEnum a tela escreveria "ConsultaEspecialidade" e "ClinicaDaDor".
        Descricao = p.Especialidade is { } e
            ? $"{RotulosEnum.De(p.Tipo)} ({RotulosEnum.De(e)})"
            : RotulosEnum.De(p.Tipo),
        Valor = p.Valor.ToString("C"),
        Vigencia = p.Vigencia,
        Situacao = p.Ativo ? "Ativo" : "Inativo",
        ValendoAgora = p.VigenteEm(hoje)
    };
}

/// <summary>
/// Tabela de preço por convênio, cadastrada na DIREÇÃO (parcela 20).
///
/// É o pedido literal da clínica — "cadastrado no Gerente e refletido nos outros módulos" —
/// e também é onde faz sentido: **quem negocia tabela com a operadora é a direção; quem
/// concilia guia é o balcão**. O cadastro é do primeiro, o uso é do segundo, sobre o mesmo
/// banco. Não há sincronização nem cópia: o Financeiro lê a mesma tabela que esta tela
/// escreve, e o valor aparece na conciliação no próximo carregamento.
///
/// O que isso resolve: o valor da guia era digitado à mão em cada conciliação. Um R$ 45 no
/// lugar de R$ 145 não é recusado por ninguém, e a diferença só apareceria numa conferência
/// que a clínica não faz. Com a tabela, a conciliação deixa de ser digitação e passa a ser
/// CONFERÊNCIA contra o demonstrativo.
///
/// A **vigência** é o que faz o cadastro valer a pena: reajuste entra como linha nova, e a
/// guia de março continua valendo o preço de março.
/// </summary>
public sealed partial class PrecosConvenioViewModel : ObservableObject
{
    private readonly IServiceScopeFactory _escopos;
    private readonly ISnackbarService _snackbar;
    private readonly IDialogoService _dialogo;

    public ObservableCollection<LinhaPreco> Precos { get; } = [];

    /// <summary>Tudo o que o banco devolveu; <see cref="Precos"/> é o recorte do filtro.</summary>
    private readonly List<LinhaPreco> _todas = [];

    // ---- Filtro (em memória, sobre o que já foi lido). O cadastro guarda o histórico
    // INTEIRO — cada reajuste é linha nova —, e a pergunta na renegociação é "quanto a
    // Unimed paga hoje?": sem recorte, ela se responde varrendo linhas fora de vigência.
    public const string TodosConvenios = "Todos os convênios";

    /// <summary>As operadoras COM preço cadastrado — oferecer as sem linha daria filtro que só leva a vazio.</summary>
    public ObservableCollection<string> ConveniosPreco { get; } = [TodosConvenios];

    [ObservableProperty] private string _filtroConvenioPreco = TodosConvenios;

    /// <summary>Só as linhas vigentes hoje — o que a conciliação está propondo agora.</summary>
    [ObservableProperty] private bool _soValendoHoje;

    /// <summary>O `Clear()` do combo devolve nulo pelo binding (lição da parcela 56) — remonta sob guarda.</summary>
    private bool _montandoConvenios;

    partial void OnSoValendoHojeChanged(bool value) => Refiltrar();
    partial void OnFiltroConvenioPrecoChanged(string value)
    {
        if (value is null)
        {
            FiltroConvenioPreco = TodosConvenios;
            return;
        }
        if (!_montandoConvenios) Refiltrar();
    }

    public bool FiltroAtivo => FiltroConvenioPreco != TodosConvenios || SoValendoHoje;

    [RelayCommand]
    private void LimparFiltro()
    {
        FiltroConvenioPreco = TodosConvenios;
        SoValendoHoje = false;
    }

    /// <summary>O estado vazio muda de frase quando há filtro — "não há preço" e "nenhum bate com o filtro" são respostas diferentes.</summary>
    [ObservableProperty] private string _vazioDescricao =
        "Nenhum preço cadastrado. Sem tabela, a conciliação continua pedindo o valor digitado — o sistema não inventa um valor de mercado.";

    [ObservableProperty] private string _resumo = string.Empty;
    [ObservableProperty] private string? _mensagem;
    [ObservableProperty] private bool _mensagemEhErro;

    /// <summary>Metade visível da permissão; a que impede é o <c>Exigir</c> no comando.</summary>
    public bool PodeEditar => SessaoUsuario.Atual.Pode(Permissao.EditarFinanceiro);

    public PrecosConvenioViewModel(
        IServiceScopeFactory escopos, ISnackbarService snackbar, IDialogoService dialogo)
    {
        _escopos = escopos;
        _snackbar = snackbar;
        _dialogo = dialogo;
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
            var precos = scope.ServiceProvider.GetRequiredService<PrecoConvenioService>();

            var hoje = DateOnly.FromDateTime(DateTime.Today);

            // Entre o `Clear()` e o último `Add` não pode haver `await` (parcela 62):
            // salvar ou excluir um preço recarrega no fim, e uma recarga que comece com
            // outra no ar deixaria a tabela com linhas repetidas.
            var linhas = (await precos.CatalogoAsync())
                .Select(p => LinhaPreco.De(p, hoje)).ToList();

            _todas.Clear();
            _todas.AddRange(linhas);

            // As operadoras com preço, preservando a escolha quando ela ainda existe —
            // atualizar não pode desfazer o filtro de quem está trabalhando na tabela.
            var escolhido = FiltroConvenioPreco;
            _montandoConvenios = true;
            try
            {
                ConveniosPreco.Clear();
                ConveniosPreco.Add(TodosConvenios);
                foreach (var nome in _todas.Select(l => l.Convenio).Distinct().OrderBy(n => n))
                    ConveniosPreco.Add(nome);
                FiltroConvenioPreco = ConveniosPreco.Contains(escolhido)
                    ? escolhido : TodosConvenios;
            }
            finally
            {
                _montandoConvenios = false;
            }

            Refiltrar();
        }
        catch (Exception ex)
        {
            Clinica.Application.Diagnostico.Registrar("Gerente — tabela de preço não pôde ser lida", ex);
            Erro($"Não foi possível ler a tabela: {ex.Message}");
        }
    }

    /// <summary>
    /// Aplica o filtro sobre o que já foi lido — em memória, sem ida ao banco (o padrão
    /// da tela de Consultas da Recepção).
    /// </summary>
    private void Refiltrar()
    {
        Precos.Clear();
        foreach (var p in _todas.Where(p =>
                     (FiltroConvenioPreco == TodosConvenios || p.Convenio == FiltroConvenioPreco)
                     && (!SoValendoHoje || p.ValendoAgora)))
            Precos.Add(p);

        OnPropertyChanged(nameof(FiltroAtivo));

        // O resumo DIZ que está filtrado: "6 preços" e "6 de 30 no filtro" respondem
        // perguntas diferentes. O "valendo hoje" continua sendo o do TOTAL.
        var vigentes = _todas.Count(p => p.ValendoAgora);
        Resumo = _todas.Count == 0
            ? "Nenhum preço cadastrado — a conciliação continua pedindo o valor digitado."
            : FiltroAtivo
                ? $"{Precos.Count} de {_todas.Count} preço(s) no filtro · {vigentes} valendo hoje no total."
                : $"{Precos.Count} preço(s) cadastrado(s) · {vigentes} valendo hoje.";

        VazioDescricao = FiltroAtivo
            ? "Nenhum preço bate com o filtro — limpe-o para ver a tabela inteira."
            : "Nenhum preço cadastrado. Sem tabela, a conciliação continua pedindo o valor digitado — o sistema não inventa um valor de mercado.";
    }

    /// <summary>
    /// Cadastro e edição saem da página e vão para a janela: a tela é a TABELA, que é o
    /// que a direção vem conferir. O formulário fixo tomava 360px da largura em todas as
    /// visitas para uma tarefa que acontece na negociação com a operadora — e o cadastro
    /// tem quatro campos com regra própria (especialidade, vigência), que num painel
    /// espremido ficavam sem espaço para a explicação que cada um precisa.
    /// </summary>
    [RelayCommand]
    private async Task NovoPrecoAsync() => await AbrirAsync(0);

    [RelayCommand]
    private async Task EditarAsync(LinhaPreco? linha)
    {
        if (linha is null) return;
        await AbrirAsync(linha.PrecoId);
    }

    private async Task AbrirAsync(int precoId)
    {
        SessaoUsuario.Atual.Exigir(Permissao.EditarFinanceiro, "cadastrar preço de convênio");

        var vm = new PrecoEdicaoViewModel(_escopos, precoId);
        var janela = new Janelas.PrecoConvenioWindow(vm)
        {
            Owner = JanelaDona.Atual()
        };

        if (janela.ShowDialog() != true) return;

        _snackbar.Sucesso(precoId == 0
            ? "Preço cadastrado — a conciliação já vai propor esse valor."
            : "Preço atualizado. Vale para as guias novas; o que já foi lançado não muda.");
        await CarregarAsync();
    }

    /// <summary>
    /// Excluir só serve para o que foi cadastrado errado. Preço que já propôs valor a alguma
    /// guia deve ser ENCERRADO pela vigência: o lançamento guardou o valor copiado, mas
    /// apagar a linha apaga a explicação de por que aquela guia valeu aquilo.
    /// </summary>
    [RelayCommand]
    private async Task ExcluirAsync(LinhaPreco? linha)
    {
        if (linha is null) return;

        try
        {
            SessaoUsuario.Atual.Exigir(Permissao.EditarFinanceiro, "excluir preço de convênio");

            if (!_dialogo.ConfirmarPerigo("Excluir preço",
                    $"Apagar o preço de {linha.Convenio} ({linha.Descricao})? Se ele já propôs "
                    + "valor a alguma guia, prefira ENCERRAR pela vigência: os lançamentos guardam "
                    + "o valor copiado, mas apagar a linha apaga a explicação de por que aquela "
                    + "guia valeu aquilo.")) return;

            using var scope = _escopos.CreateScope();
            var precos = scope.ServiceProvider.GetRequiredService<PrecoConvenioService>();
            await precos.ExcluirAsync(linha.PrecoId);

            _snackbar.Info("Preço excluído.");
            await CarregarAsync();
        }
        catch (Exception ex)
        {
            Clinica.Application.Diagnostico.Registrar("Gerente — preço não pôde ser excluído", ex);
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
/// O preço de uma guia, na janela — cadastro e correção.
///
/// Duas regras moram aqui e precisam de espaço para serem ditas: a **especialidade**
/// declarada VENCE o genérico do tipo (senão a clínica cadastraria a exceção e a
/// conciliação continuaria propondo o valor da regra geral), e o **reajuste entra como
/// linha nova** com a data em que passou a valer — a guia de março segue sendo proposta
/// pelo preço de março, e o que já foi lançado no caixa nunca muda.
/// </summary>
public sealed partial class PrecoEdicaoViewModel : ObservableObject
{
    private readonly IServiceScopeFactory _escopos;
    private readonly int _precoId;

    public ObservableCollection<ConvenioCadastro> Convenios { get; } = [];

    public IReadOnlyList<TipoCodigo> Tipos { get; } = Enum.GetValues<TipoCodigo>();

    /// <summary>Null = vale para qualquer especialidade, que é o caso comum.</summary>
    public IReadOnlyList<Especialidade?> Especialidades { get; } =
        [null, .. Enum.GetValues<Especialidade>().Cast<Especialidade?>()];

    public string Titulo => _precoId == 0 ? "Preço novo" : "Editar preço";

    [ObservableProperty] private ConvenioCadastro? _convenio;
    [ObservableProperty] private TipoCodigo _tipo = TipoCodigo.Acupuntura;
    [ObservableProperty] private Especialidade? _especialidade;
    [ObservableProperty] private string? _valor;
    [ObservableProperty] private DateTime? _vigenteDe;
    [ObservableProperty] private DateTime? _vigenteAte;
    [ObservableProperty] private bool _ativo = true;

    [ObservableProperty] private string? _mensagem;
    [ObservableProperty] private bool _mensagemEhErro;
    [ObservableProperty] private bool _salvando;

    public event Action? Concluido;

    public PrecoEdicaoViewModel(IServiceScopeFactory escopos, int precoId)
    {
        _escopos = escopos;
        _precoId = precoId;
        _ = CarregarAsync();
    }

    private async Task CarregarAsync()
    {
        try
        {
            using var scope = _escopos.CreateScope();
            var catalogo = scope.ServiceProvider.GetRequiredService<ConvenioCatalogoService>();

            // Entre o `Clear()` e o último `Add` não pode haver `await` (parcela 62).
            var ativos = (await catalogo.ListarAsync()).Where(c => c.Ativo).ToList();

            Convenios.Clear();
            foreach (var c in ativos) Convenios.Add(c);

            if (_precoId == 0) return;

            var precos = scope.ServiceProvider.GetRequiredService<PrecoConvenioService>();
            if ((await precos.CatalogoAsync()).FirstOrDefault(x => x.Id == _precoId) is not { } p) return;

            Convenio = Convenios.FirstOrDefault(c =>
                string.Equals(c.Codigo, p.ConvenioCodigo, StringComparison.OrdinalIgnoreCase));
            Tipo = p.Tipo;
            Especialidade = p.Especialidade;
            Valor = p.Valor.ToString("0.##");
            VigenteDe = p.VigenteDe?.ToDateTime(TimeOnly.MinValue);
            VigenteAte = p.VigenteAte?.ToDateTime(TimeOnly.MinValue);
            Ativo = p.Ativo;
        }
        catch (Exception ex)
        {
            Clinica.Application.Diagnostico.Registrar("Gerente — preço não pôde ser aberto", ex);
            Erro(ex.Message);
        }
    }

    [RelayCommand]
    private async Task SalvarAsync()
    {
        Mensagem = null;
        MensagemEhErro = false;

        if (Convenio is null)
        {
            Erro("Escolha o convênio.");
            return;
        }
        if (!Valores.TentarLerDecimal(Valor, out var valor) || valor <= 0m)
        {
            Erro("Informe quanto a operadora paga por esta guia (ex.: 145,00).");
            return;
        }

        try
        {
            Salvando = true;
            SessaoUsuario.Atual.Exigir(Permissao.EditarFinanceiro, "cadastrar preço de convênio");

            using var scope = _escopos.CreateScope();
            var precos = scope.ServiceProvider.GetRequiredService<PrecoConvenioService>();

            await precos.SalvarAsync(new PrecoConvenio
            {
                Id = _precoId,
                ConvenioCodigo = Convenio.Codigo,
                Tipo = Tipo,
                Especialidade = Especialidade,
                Valor = valor,
                VigenteDe = VigenteDe is { } de ? DateOnly.FromDateTime(de) : null,
                VigenteAte = VigenteAte is { } ate ? DateOnly.FromDateTime(ate) : null,
                Ativo = Ativo
            }, SessaoUsuario.Atual.Operador);

            Concluido?.Invoke();
        }
        catch (Exception ex)
        {
            Clinica.Application.Diagnostico.Registrar("Gerente — preço não pôde ser salvo", ex);
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
