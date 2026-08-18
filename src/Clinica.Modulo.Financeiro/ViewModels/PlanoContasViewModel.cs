using System.Collections.ObjectModel;
using Clinica.Application.Servicos;
using Clinica.Desktop.Controls;
using Clinica.Desktop.Shell;
using Clinica.Domain.Entities;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using Clinica.Desktop.Shell.Componentes;

namespace Clinica.Financeiro.ViewModels;

/// <summary>Uma categoria do plano de contas na tela.</summary>
public sealed class LinhaCategoria
{
    public required int Id { get; init; }
    public required string Codigo { get; init; }
    public required string Nome { get; init; }
    public required string Tipo { get; init; }
    public required bool Ativa { get; init; }
    public required bool EhEntrada { get; init; }
}

/// <summary>
/// Plano de contas: as categorias que classificam entradas e saídas. O serviço existia
/// desde a parcela 4 do financeiro; faltava a tela para a clínica mexer nele sem
/// depender de quem escreve SQL.
///
/// O CÓDIGO não é editável depois de criado: ele é a referência estável, e trocá-lo
/// desligaria os lançamentos que já apontam para a categoria. Nome, ordem e "ativa"
/// mudam à vontade.
/// </summary>
public sealed partial class PlanoContasViewModel : ObservableObject
{
    private readonly IServiceScopeFactory _escopos;
    private readonly ISnackbarService _snackbar;

    public ObservableCollection<LinhaCategoria> Categorias { get; } = [];

    [ObservableProperty] private bool _carregando;

    /// <summary>
    /// A leitura FALHOU — o terceiro estado. Sem ele, tela vazia por erro fica idêntica
    /// a tela vazia por não haver nada.
    /// </summary>
    [ObservableProperty] private bool _naoVerificado;
    [ObservableProperty] private string _mensagem = string.Empty;
    [ObservableProperty] private bool _mensagemEhErro;
    [ObservableProperty] private string _resumo = "—";

    /// <summary>
    /// Habilita os botões de escrita da tela. É a metade VISÍVEL da permissão: o
    /// botão apagado explica por que não dá; a guarda no comando é que impede.
    /// Só desabilitar seria enfeite — um atalho de teclado passaria direto.
    /// </summary>
    public bool PodeEditarFinanceiro => SessaoUsuario.Atual.Pode(Permissao.EditarFinanceiro);

    /// <summary>
    /// ⚠️ Nada de serviço SCOPED no construtor — o shell resolve esta tela do provedor
    /// RAIZ, e Scoped pedido à raiz vive pela vida inteira do app, com o `DbContext`
    /// junto (parcela 69). Escopo por operação. Ver a checagem 37 do verificar-suite.
    /// </summary>
    public PlanoContasViewModel(IServiceScopeFactory escopos, ISnackbarService snackbar)
    {
        _escopos = escopos;
        _snackbar = snackbar;
        _ = CarregarAsync();
    }

    [RelayCommand]
    private async Task CarregarAsync()
    {
        try
        {
            Carregando = true;
            NaoVerificado = false;
            Mensagem = string.Empty;
            MensagemEhErro = false;

            using var escopo = _escopos.CreateScope();
            var categorias = await escopo.ServiceProvider
                .GetRequiredService<FinanceiroService>().CategoriasAsync();

            Categorias.Clear();
            foreach (var c in categorias)
                Categorias.Add(new LinhaCategoria
                {
                    Id = c.Id,
                    Codigo = c.Codigo,
                    Nome = c.Nome,
                    Tipo = c.Tipo == TipoLancamento.Entrada ? "Entrada" : "Saída",
                    Ativa = c.Ativa,
                    EhEntrada = c.Tipo == TipoLancamento.Entrada
                });

            var entradas = categorias.Count(c => c.Tipo == TipoLancamento.Entrada);
            Resumo = $"{categorias.Count} categoria(s) · {entradas} de entrada · "
                     + $"{categorias.Count - entradas} de saída";
        }
        catch (Exception ex)
        {
            NaoVerificado = true;
            Clinica.Application.Diagnostico.Registrar("Financeiro — plano de contas não pôde ser carregado", ex);
            Erro($"Não foi possível carregar o plano de contas: {ex.Message}");
        }
        finally
        {
            Carregando = false;
        }
    }

    /// <summary>
    /// Criar categoria saiu da página e virou janela: a tela é a LISTA, e o cadastro é
    /// tarefa de implantação — acontece uma vez e some. O formulário fixo tomava a
    /// primeira faixa da tela todos os dias por causa dele.
    /// </summary>
    [RelayCommand]
    private async Task NovaCategoriaAsync()
    {
        SessaoUsuario.Atual.Exigir(Permissao.EditarFinanceiro, "mexer no plano de contas");

        var vm = new CategoriaEdicaoViewModel(_escopos, ordemSugerida: Categorias.Count);
        var janela = new Janelas.CategoriaWindow(vm)
        {
            Owner = JanelaDona.Atual()
        };

        if (janela.ShowDialog() != true) return;
        _snackbar.Sucesso("Categoria criada.");
        await CarregarAsync();
    }

    /// <summary>Liga/desliga a categoria. Inativa some dos combos e continua no histórico.</summary>
    [RelayCommand]
    private async Task AlternarAtivaAsync(LinhaCategoria? linha)
    {
        if (linha is null) return;

        SessaoUsuario.Atual.Exigir(Permissao.EditarFinanceiro, "mexer no plano de contas");

        try
        {
            using (var escopo = _escopos.CreateScope())
                await escopo.ServiceProvider.GetRequiredService<FinanceiroService>()
                    .AtualizarCategoriaAsync(
                        linha.Id, linha.Nome, !linha.Ativa, ordem: Categorias.IndexOf(linha));

            _snackbar.Info(linha.Ativa ? "Categoria desativada." : "Categoria reativada.");
            await CarregarAsync();
        }
        catch (Exception ex)
        {
            Clinica.Application.Diagnostico.Registrar("Financeiro — categoria não pôde ser alterada", ex);
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
/// Categoria nova, na janela: código, nome e tipo.
///
/// O CÓDIGO só existe aqui — depois de criado ele não se edita em lugar nenhum, porque é
/// a referência estável que os lançamentos já gravados apontam. Trocá-lo desligaria o
/// histórico da categoria em silêncio.
/// </summary>
public sealed partial class CategoriaEdicaoViewModel : ObservableObject
{
    private readonly IServiceScopeFactory _escopos;
    private readonly int _ordemSugerida;

    public IReadOnlyList<TipoLancamento> Tipos { get; } = Enum.GetValues<TipoLancamento>();

    [ObservableProperty] private string? _codigo;
    [ObservableProperty] private string? _nome;
    [ObservableProperty] private TipoLancamento _tipo = TipoLancamento.Entrada;

    [ObservableProperty] private string _mensagem = string.Empty;
    [ObservableProperty] private bool _mensagemEhErro;
    [ObservableProperty] private bool _salvando;

    public event Action? Concluido;

    public CategoriaEdicaoViewModel(IServiceScopeFactory escopos, int ordemSugerida)
    {
        _escopos = escopos;
        _ordemSugerida = ordemSugerida;
    }

    [RelayCommand]
    private async Task SalvarAsync()
    {
        Mensagem = string.Empty;
        MensagemEhErro = false;

        try
        {
            Salvando = true;

            using (var escopo = _escopos.CreateScope())
                await escopo.ServiceProvider.GetRequiredService<FinanceiroService>()
                    .CriarCategoriaAsync(
                        Codigo ?? string.Empty, Nome ?? string.Empty, Tipo, ordem: _ordemSugerida);

            Concluido?.Invoke();
        }
        catch (Exception ex)
        {
            Clinica.Application.Diagnostico.Registrar("Financeiro — categoria não pôde ser criada", ex);
            Mensagem = ex.Message;
            MensagemEhErro = true;
        }
        finally
        {
            Salvando = false;
        }
    }
}
