using Clinica.Domain;
using Clinica.Domain.Entities;
using System.Collections.ObjectModel;
using Clinica.Application.Modelos;
using Clinica.Application.Servicos;
using Clinica.Desktop.Controls;
using Clinica.Desktop.Shell;
using Clinica.Desktop.Shell.Componentes;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;

namespace Clinica.Desktop.Shell.Componentes;

/// <summary>Um pacote do catálogo, como aparece no combo da venda.</summary>
public sealed class OpcaoPacote
{
    public required int Id { get; init; }
    public required string Rotulo { get; init; }
    public required decimal Valor { get; init; }
}

/// <summary>
/// Venda de um pacote a um paciente.
///
/// O paciente é escolhido por BUSCA, não por uma lista de todos: a base cresce e um
/// combo com mil nomes é inutilizável no balcão. A busca é a mesma do resto da suíte —
/// não se reescreve seletor de paciente.
/// </summary>
public sealed partial class PacoteVendaViewModel : ObservableObject
{
    private readonly IServiceScopeFactory _escopos;

    public ObservableCollection<OpcaoPacote> Opcoes { get; } = [];

    /// <summary>Busca de paciente do design system (limite no SQL, teclas agrupadas).</summary>
    public SeletorPacienteViewModel Seletor { get; }

    [ObservableProperty] private OpcaoPacote? _pacoteSelecionado;
    [ObservableProperty] private DateTime _dataCompra = DateTime.Today;
    [ObservableProperty] private string? _valorCobrado;
    [ObservableProperty] private string? _observacoes;

    // ===== Como o paciente paga (set/2026) =====
    //
    // A venda não movia dinheiro: o pacote nascia e o caixa não sabia. Aqui a decisão é
    // OBRIGATÓRIA — à vista ou a prazo — e a prévia das parcelas é desenhada pelo MESMO
    // `ParcelasDaVenda` que o serviço grava, para a tela nunca prometer parcelas
    // diferentes das gravadas.

    /// <summary>Formas oferecidas: tudo menos "Convênio", que não é como um particular paga.</summary>
    public IReadOnlyList<FormaPagamento> Formas { get; } =
        Enum.GetValues<FormaPagamento>().Where(f => f != FormaPagamento.Convenio).ToList();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(APrazo))]
    [NotifyPropertyChangedFor(nameof(PreviaDasParcelas))]
    private bool _aVista = true;

    /// <summary>O contrário de <see cref="AVista"/>, para o segundo rádio ligar por binding.</summary>
    public bool APrazo
    {
        get => !AVista;
        set => AVista = !value;
    }

    /// <summary>À vista: a forma do total. A prazo: a forma da ENTRADA (quando há).</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PreviaDasParcelas))]
    private FormaPagamento? _forma = FormaPagamento.Pix;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PreviaDasParcelas))]
    private string? _parcelas = "3";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PreviaDasParcelas))]
    private DateTime _primeiroVencimento = DateTime.Today.AddDays(30);

    /// <summary>A prazo: o que o paciente paga HOJE. Em branco = nada agora.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PreviaDasParcelas))]
    private string? _entrada;

    partial void OnValorCobradoChanged(string? value) => OnPropertyChanged(nameof(PreviaDasParcelas));
    partial void OnDataCompraChanged(DateTime value) => OnPropertyChanged(nameof(PreviaDasParcelas));

    /// <summary>
    /// O que vai ser gravado, por extenso, ANTES do clique: "entrada R$ 200,00 + 3 parcelas
    /// (R$ 1.000,00) a partir de 06/10/2026". Quando a decisão não fecha, a frase é o motivo
    /// — o mesmo que o Vender vai recusar.
    /// </summary>
    public string PreviaDasParcelas
    {
        get
        {
            try
            {
                var desenho = ParcelasDaVenda.Desenhar(
                    LerValor() ?? 0m, DateOnly.FromDateTime(DataCompra), MontarPagamento());
                return desenho.Count == 0
                    ? "Pacote sem valor: nada a receber."
                    : "Vai gravar: " + ParcelasDaVenda.Resumir(desenho) + ".";
            }
            catch (InvalidOperationException ex)
            {
                return ex.Message;
            }
        }
    }

    private decimal? LerValor()
    {
        if (string.IsNullOrWhiteSpace(ValorCobrado)) return null;
        if (!Valores.TentarLerDecimal(ValorCobrado, out var lido))
            throw new InvalidOperationException("Não entendi o valor: use algo como 250,00.");
        return lido;
    }

    private PagamentoDaVenda MontarPagamento()
    {
        if (AVista)
            return new PagamentoDaVenda { TudoAgora = true, FormaDoPagoAgora = Forma };

        var entrada = 0m;
        if (!string.IsNullOrWhiteSpace(Entrada) && !Valores.TentarLerDecimal(Entrada, out entrada))
            throw new InvalidOperationException("Não entendi a entrada: use algo como 200,00.");

        if (!int.TryParse((Parcelas ?? string.Empty).Trim(), out var n))
            throw new InvalidOperationException("Diga em quantas parcelas (um número inteiro).");

        return PagamentoDaVenda.APrazo(
            n, DateOnly.FromDateTime(PrimeiroVencimento), entrada,
            formaDaEntrada: entrada > 0m ? Forma : null);
    }

    [ObservableProperty] private string _mensagem = string.Empty;
    [ObservableProperty] private bool _mensagemEhErro;
    [ObservableProperty] private bool _salvando;

    public event Action? Concluido;

    public PacoteVendaViewModel(IServiceScopeFactory escopos)
    {
        _escopos = escopos;
        Seletor = new SeletorPacienteViewModel(escopos);
        _ = CarregarAsync();
    }

    partial void OnPacoteSelecionadoChanged(OpcaoPacote? value)
    {
        // O valor vem preenchido com o de tabela, mas continua editável: desconto no
        // balcão é regra, não exceção.
        if (value is not null) ValorCobrado = value.Valor.ToString("0.00");
    }

    private async Task CarregarAsync()
    {
        try
        {
            // Monta e só ENTÃO publica: entre o Clear e o último Add não pode haver await.
            using var escopo = _escopos.CreateScope();
            var catalogo = await escopo.ServiceProvider
                .GetRequiredService<PacoteService>().CatalogoAsync(somenteAtivos: true);

            Opcoes.Clear();
            foreach (var p in catalogo)
                Opcoes.Add(new OpcaoPacote
                {
                    Id = p.Id,
                    Rotulo = p.SessoesIncluidas is { } n
                        ? $"{p.Nome} — {n} sessões — {p.Valor:C}"
                        : $"{p.Nome} — {p.Valor:C}",
                    Valor = p.Valor
                });
        }
        catch (Exception ex)
        {
            Clinica.Application.Diagnostico.Registrar("Financeiro — catálogo não pôde ser lido", ex);
            Erro(ex.Message);
        }
    }

    [RelayCommand]
    private async Task SalvarAsync()
    {
        Mensagem = string.Empty;
        MensagemEhErro = false;

        // A porta (o "Vender…" da tela de trás) já exige; a JANELA não exigia — e é na
        // janela que se grava (a lição da parcela 54: a segunda barreira vale mais onde
        // se escreve). Os bits são os de `PacotesViewModel.PodeVender`.
        try
        {
            SessaoUsuario.Atual.ExigirAlgum(
                Permissao.VenderPacote | Permissao.EditarFinanceiro, "vender pacote");
        }
        catch (Exception ex)
        {
            Erro(ex.Message);
            return;
        }

        if (Seletor.Selecionado is not { } paciente)
        {
            Erro("Escolha o paciente que está comprando.");
            return;
        }

        if (PacoteSelecionado is not { } pacote)
        {
            Erro("Escolha o pacote.");
            return;
        }

        try
        {
            Salvando = true;

            // Valores.TentarLerDecimal, e não decimal.TryParse: é o leitor do projeto, que
            // aceita "1.250,00" e "1250.00" sem depender da cultura da máquina.
            var valor = LerValor();

            // A decisão de pagamento é validada AQUI, pelo mesmo desenho que o serviço
            // grava — a recusa chega antes de a venda existir, não depois.
            var pagamento = MontarPagamento();
            ParcelasDaVenda.Desenhar(valor ?? pacote.Valor, DateOnly.FromDateTime(DataCompra), pagamento);

            using (var escopo = _escopos.CreateScope())
                await escopo.ServiceProvider.GetRequiredService<PacoteService>()
                    .VenderAsync(
                        paciente.Id, pacote.Id, DateOnly.FromDateTime(DataCompra), valor,
                        Observacoes, SessaoUsuario.Atual.Operador, pagamento);

            Concluido?.Invoke();
        }
        catch (Exception ex)
        {
            Clinica.Application.Diagnostico.Registrar("Financeiro — pacote não pôde ser vendido", ex);
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
