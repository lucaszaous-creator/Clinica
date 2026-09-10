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

    /// <summary>
    /// Não há pacote ATIVO no catálogo — não há o que vender. A janela diz qual é o primeiro
    /// passo e oferece fazê-lo aqui, em vez de deixar a pessoa clicar em Vender para
    /// descobrir que a lista está vazia.
    /// </summary>
    [ObservableProperty] private bool _catalogoVazio;

    /// <summary>
    /// Quem pode CADASTRAR no catálogo: o mesmo par do vender (set/2026 — a direção: *"o
    /// ideal seria a recepção também cadastrar e editar preços"*). Sem o bit, a frase diz o
    /// caminho e o botão não aparece — botão que só leva recusa é o defeito da parcela 41.
    /// </summary>
    public bool PodeMexerNoCatalogo => SessaoUsuario.Atual.PodeAlgum(
        Permissao.VenderPacote | Permissao.EditarFinanceiro);

    public event Action? Concluido;

    /// <param name="paciente">
    /// O paciente JÁ ESCOLHIDO, quando a venda começa numa tela que já sabe quem é
    /// (set/2026 — o "Vender pacote…" do Novo atendimento).
    ///
    /// ⚠️ Entra pelo <c>SelecionarGarantindoNaLista</c>: um <c>Selector</c> do WPF cujo
    /// <c>SelectedItem</c> recebe item que não está no <c>ItemsSource</c> devolve NULL pelo
    /// binding de volta — a escolha se limparia no mesmo instante em que é feita, sem erro
    /// nenhum. O componente já tem esta porta; ela existe exatamente para isto.
    ///
    /// Nulo mantém o comportamento de sempre: a busca, para quem chegou pela tela Pacotes.
    /// </param>
    public PacoteVendaViewModel(IServiceScopeFactory escopos, Paciente? paciente = null)
    {
        _escopos = escopos;
        Seletor = new SeletorPacienteViewModel(escopos);

        if (paciente is not null) Seletor.SelecionarGarantindoNaLista(paciente);

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

            // ⚠️ CATÁLOGO VAZIO é o estado de TODA instalação nova: nenhuma migration semeia
            // pacote, e com razão — o que a clínica vende é decisão dela. O que faltava era
            // a tela DIZER isso: a janela abria com o combo vazio, a pessoa clicava em
            // Vender e levava "Escolha o pacote" sobre uma lista que não tem nada. É o mesmo
            // defeito do particular que não existia (set/2026): comportamento pronto e
            // testado que nenhum CADASTRO alcança, sem uma frase dizendo qual é o primeiro
            // passo.
            CatalogoVazio = Opcoes.Count == 0;
        }
        catch (Exception ex)
        {
            Clinica.Application.Diagnostico.Registrar("Financeiro — catálogo não pôde ser lido", ex);
            Erro(ex.Message);
        }
    }

    /// <summary>
    /// Cadastra um pacote no catálogo SEM sair da venda — a porta que faltava quando a lista
    /// está vazia.
    ///
    /// Abre a MESMA janela do catálogo da tela de Pacotes (não há um segundo formulário de
    /// pacote: dois divergiriam na primeira correção) e recarrega o combo ao voltar.
    /// </summary>
    [RelayCommand]
    private async Task CadastrarNoCatalogoAsync()
    {
        try
        {
            SessaoUsuario.Atual.ExigirAlgum(
                Permissao.VenderPacote | Permissao.EditarFinanceiro, "mexer no catálogo de pacotes");
        }
        catch (Exception ex)
        {
            Erro(ex.Message);
            return;
        }

        var vm = new PacoteCatalogoEdicaoViewModel(_escopos);
        var janela = new PacoteCatalogoWindow(vm) { Owner = JanelaDona.Atual() };

        if (janela.ShowDialog() != true) return;

        await CarregarAsync();

        // O pacote que acabou de nascer é o único da lista: escolhê-lo poupa o clique que a
        // pessoa viria dar de qualquer forma — e com mais de um, escolher seria adivinhar.
        if (Opcoes.Count == 1) PacoteSelecionado = Opcoes[0];

        Mensagem = "Pacote cadastrado no catálogo. Agora dá para vendê-lo.";
        MensagemEhErro = false;
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
            // Catálogo vazio não é "escolha o pacote": é "não há pacote para escolher", e a
            // saída é outra. Mandar escolher de uma lista vazia é o que faz a pessoa clicar
            // no combo três vezes antes de desistir.
            Erro(CatalogoVazio
                ? "Não há pacote no catálogo para vender. Cadastre o primeiro — o botão está "
                  + "aí em cima."
                : "Escolha o pacote.");
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
