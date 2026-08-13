using System.Collections.ObjectModel;
using Clinica.Application.Servicos;
using Clinica.Desktop.Controls;
using Clinica.Desktop.Shell;
using Clinica.Domain.Entities;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;

namespace Clinica.Recepcao.ViewModels;

/// <summary>Uma linha de insumo na tela do fechamento.</summary>
public sealed partial class LinhaInsumoFechamento : ObservableObject
{
    public required int ItemId { get; init; }
    public required string Nome { get; init; }
    public required string Unidade { get; init; }
    public required decimal Saldo { get; init; }

    /// <summary>Texto de propósito: o campo aceita digitação parcial ("0," virando "0,5").</summary>
    [ObservableProperty]
    private string? _quantidade;

    public string SaldoRotulo => $"saldo {Saldo:0.##} {Unidade}";
}

/// <summary>Opção de categoria do plano de contas (null = sem categoria).</summary>
public sealed record OpcaoCategoriaFechamento(int? Id, string Nome);

/// <summary>
/// Fechamento da sessão: a tela que a Recepção confirma ao finalizar o atendimento.
///
/// Ela existe porque concluir a sessão sempre foi QUATRO fatos e só um deles acontecia:
/// a guia nascia, mas o pacote do paciente não baixava, o insumo não saía do estoque e
/// o dinheiro não entrava no caixa. Os três serviços já existiam e estavam testados —
/// faltava quem os chamasse. Ver <see cref="FechamentoSessaoService"/>.
///
/// Tudo é PROPOSTA confirmada, nunca automático: no balcão, valor gravado sozinho vira
/// caixa que não bate, e o erro só aparece no fechamento do mês. O que o sistema faz é
/// o trabalho (achar o pacote que vence primeiro, lembrar o que a última sessão gastou,
/// sugerir o valor da última vez); o clique continua sendo de quem está no balcão.
///
/// O feedback é INLINE (<see cref="Mensagem"/>) e não snackbar: é formulário, e a
/// mensagem precisa ficar na tela enquanto o usuário corrige.
/// </summary>
public sealed partial class FechamentoSessaoViewModel : ObservableObject
{
    private readonly IServiceScopeFactory _escopos;
    private readonly int _agendamentoId;

    /// <summary>Disparado quando o fechamento saiu inteiro — a janela fecha por ele.</summary>
    public event Action<ResultadoFechamento>? Concluido;

    public ObservableCollection<LinhaInsumoFechamento> Insumos { get; } = [];
    public ObservableCollection<OpcaoCategoriaFechamento> Categorias { get; } = [];

    public IReadOnlyList<FormaPagamento> Formas { get; } = Enum.GetValues<FormaPagamento>();

    [ObservableProperty] private string _paciente = string.Empty;
    [ObservableProperty] private string _data = string.Empty;
    [ObservableProperty] private bool _carregando = true;
    [ObservableProperty] private bool _ocupado;

    /// <summary>Falhou ao montar a proposta: a tela some e sobra só a mensagem.</summary>
    [ObservableProperty] private bool _propostaValida;

    // O resumo depende dos três: sem notificar por TemPacote, carregar uma proposta COM
    // pacote não mexeria em DebitarPacote (que já nasce true) e a frase ficaria velha,
    // sem a linha do pacote — texto que promete menos do que o botão faz.
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ResumoDoQueVaiAcontecer))]
    private bool _temPacote;

    [ObservableProperty] private string? _pacoteRotulo;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ResumoDoQueVaiAcontecer))]
    private bool _debitarPacote = true;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ResumoDoQueVaiAcontecer))]
    private bool _gerarLancamento;

    [ObservableProperty] private string? _valor;
    [ObservableProperty] private string? _procedenciaDoValor;
    [ObservableProperty] private FormaPagamento? _forma;
    [ObservableProperty] private OpcaoCategoriaFechamento? _categoria;

    [ObservableProperty] private string? _mensagem;
    [ObservableProperty] private bool _mensagemEhErro;

    /// <summary>
    /// O atendimento já foi concluído nesta janela. A partir daqui não há o que
    /// confirmar de novo — repetir criaria uma segunda guia para a mesma sessão.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(NaoConcluida))]
    private bool _concluida;

    /// <summary>Há insumo sugerido para mostrar (o bloco some quando não há).</summary>
    [ObservableProperty] private bool _temInsumos;

    /// <summary>
    /// O contrário de <see cref="Concluida"/>. Existe como propriedade porque a suíte não
    /// tem conversor invertido — só o <c>BooleanToVisibilityConverter</c> embutido.
    /// </summary>
    public bool NaoConcluida => !Concluida;

    /// <summary>
    /// Metade VISÍVEL da permissão do caixa: quem não pode lançar vê o bloco apagado com
    /// a explicação, em vez de um campo que recusa no fim. A que impede é o
    /// <c>ExigirAlgum</c> no comando.
    ///
    /// ⚠️ <b>`LancarAtendimento` OU `EditarFinanceiro` (parcela 62).</b> Concluir a sessão
    /// são QUATRO fatos do mesmo ato (parcela 6): a guia nasce, o pacote debita, o insumo
    /// sai e <b>o dinheiro entra no caixa</b>. Exigir só `EditarFinanceiro` na quarta
    /// ponta fechava justamente ela para o perfil que faz as outras três — o balcão
    /// concluía a sessão do particular e a entrada não era registrada, e o mês fechava
    /// com uma diferença sem nome. É o mesmo corte da parcela 60 no `VenderPacote`:
    /// registrar o dinheiro DESTA sessão é do balcão; abrir o caixa, a conciliação e as
    /// contas a pagar da clínica continua sendo `VerFinanceiro`/`EditarFinanceiro`.
    /// </summary>
    public bool PodeLancarFinanceiro => SessaoUsuario.Atual.PodeAlgum(
        Permissao.LancarAtendimento | Permissao.EditarFinanceiro);

    /// <summary>Idem <see cref="NaoConcluida"/>: a explicação só aparece a quem não pode.</summary>
    public bool SemPermissaoFinanceiro => !PodeLancarFinanceiro;

    /// <summary>
    /// Uma frase com o que o botão vai fazer — lida antes do clique, não depois.
    ///
    /// ⚠️ Ela NÃO promete mais gerar a guia (parcela 65): quando esta janela abre, o
    /// atendimento já foi registrado e a guia já está no faturamento. Manter a promessa
    /// antiga faria a recepcionista concluir "para garantir a guia" — e é essa dúvida que
    /// produziu a sessão lançada três vezes em 12/08/2026.
    /// </summary>
    public string ResumoDoQueVaiAcontecer
    {
        get
        {
            var partes = new List<string>();
            if (TemPacote && DebitarPacote) partes.Add("debita 1 sessão do pacote");
            if (GerarLancamento) partes.Add("lança a entrada no caixa");
            if (partes.Count == 0) return "A guia já está no faturamento. Não há mais nada a registrar.";
            return "A guia já está no faturamento. Concluir " + string.Join(" e ", partes) + ".";
        }
    }

    /// <summary>
    /// Recebe a fábrica de escopos, como os demais formulários da suíte: a janela vive
    /// enquanto a recepção decide, e um <c>DbContext</c> emprestado da tela de trás
    /// acumularia entidades rastreadas por todo esse tempo.
    /// </summary>
    public FechamentoSessaoViewModel(IServiceScopeFactory escopos, int agendamentoId)
    {
        _escopos = escopos;
        _agendamentoId = agendamentoId;

        _ = CarregarAsync();
    }

    private async Task CarregarAsync()
    {
        try
        {
            using var scope = _escopos.CreateScope();
            var fechamento = scope.ServiceProvider.GetRequiredService<FechamentoSessaoService>();
            var financeiro = scope.ServiceProvider.GetRequiredService<FinanceiroService>();

            var proposta = await fechamento.PrepararAsync(_agendamentoId);

            Paciente = proposta.Paciente;
            Data = proposta.Data.ToString("dd/MM/yyyy");

            TemPacote = proposta.TemPacote;
            PacoteRotulo = proposta.PacoteADebitar is { } p
                ? $"{p.Nome} — {p.SaldoRotulo}"
                : null;
            DebitarPacote = proposta.TemPacote;

            Valor = proposta.ValorSugerido?.ToString("0.00");
            ProcedenciaDoValor = proposta.ProcedenciaDoValor;
            Forma = proposta.FormaSugerida;
            GerarLancamento = proposta.SugereLancamento && PodeLancarFinanceiro;

            Insumos.Clear();
            foreach (var i in proposta.Insumos)
            {
                Insumos.Add(new LinhaInsumoFechamento
                {
                    ItemId = i.ItemId,
                    Nome = i.Nome,
                    Unidade = i.Unidade,
                    Saldo = i.Saldo,
                    // Sugestão maior que o saldo entra ZERADA: preencher o campo com um
                    // número que a baixa vai recusar é oferecer um erro pronto.
                    Quantidade = i.SemSaldo ? null : i.Quantidade.ToString("0.##")
                });
            }
            TemInsumos = Insumos.Count > 0;

            await CarregarCategoriasAsync(financeiro);
            PropostaValida = true;
        }
        catch (Exception ex)
        {
            Clinica.Application.Diagnostico.Registrar(
                "Recepção — proposta de fechamento não pôde ser montada", ex);
            Erro(ex.Message);
        }
        finally
        {
            Carregando = false;
        }
    }

    private async Task CarregarCategoriasAsync(FinanceiroService financeiro)
    {
        Categorias.Clear();
        Categorias.Add(new OpcaoCategoriaFechamento(null, "(sem categoria)"));

        try
        {
            foreach (var c in await financeiro.CategoriasAsync(TipoLancamento.Entrada))
                Categorias.Add(new OpcaoCategoriaFechamento(c.Id, c.Nome));
        }
        catch (Exception ex)
        {
            // Degradação deliberada: sem a lista, o lançamento sai sem categoria e o
            // Financeiro classifica depois. Mas fica registrado — combo vazio sem rastro
            // vira mistério.
            Clinica.Application.Diagnostico.Registrar(
                "Recepção — categorias do caixa não puderam ser lidas", ex);
        }

        Categoria = Categorias[0];
    }

    [RelayCommand]
    private async Task ConfirmarAsync()
    {
        if (Concluida) return;

        Mensagem = null;
        MensagemEhErro = false;

        decimal? valor = null;
        if (GerarLancamento)
        {
            if (!Valores.TentarLerDecimal(Valor, out var lido))
            {
                Erro("Informe um valor maior que zero (ex.: 120,00) ou desmarque a entrada no caixa.");
                return;
            }
            valor = lido;
        }

        var insumos = new List<InsumoAConsumir>();
        foreach (var linha in Insumos)
        {
            if (!Valores.TentarLerQuantidade(linha.Quantidade, out var quantidade))
            {
                Erro($"A quantidade de {linha.Nome} não é um número válido.");
                return;
            }
            if (quantidade > 0) insumos.Add(new InsumoAConsumir(linha.ItemId, quantidade));
        }

        try
        {
            Ocupado = true;

            SessaoUsuario.Atual.Exigir(Permissao.EditarAgenda, "concluir o atendimento");
            if (GerarLancamento)
                SessaoUsuario.Atual.ExigirAlgum(
                    Permissao.LancarAtendimento | Permissao.EditarFinanceiro,
                    "lançar a entrada no caixa");

            using var scope = _escopos.CreateScope();
            var fechamento = scope.ServiceProvider.GetRequiredService<FechamentoSessaoService>();

            var resultado = await fechamento.ConcluirAsync(
                new DecisaoFechamento(
                    _agendamentoId,
                    DebitarPacote: TemPacote && DebitarPacote,
                    GerarLancamento: GerarLancamento,
                    Valor: valor,
                    Forma: Forma,
                    CategoriaId: Categoria?.Id,
                    Insumos: insumos),
                SessaoUsuario.Atual.Operador);

            // O atendimento existe a partir daqui, tenha o resto dado certo ou não:
            // confirmar de novo criaria uma segunda guia para a mesma sessão.
            Concluida = true;

            if (resultado.TudoCerto)
            {
                Concluido?.Invoke(resultado);
                return;
            }

            // Parte falhou. A janela FICA ABERTA dizendo o quê: fechar com um "pronto"
            // mandaria a secretária embora achando que o pacote baixou.
            Erro("O atendimento foi concluído e a guia existe, MAS:\n• "
                 + string.Join("\n• ", resultado.Avisos)
                 + "\n\nResolva pelo módulo Financeiro — a sessão não precisa ser refeita.");
        }
        catch (Exception ex)
        {
            Clinica.Application.Diagnostico.Registrar("Recepção — falha no fechamento da sessão", ex);
            Erro(ex.Message);
        }
        finally
        {
            Ocupado = false;
        }
    }

    private void Erro(string mensagem)
    {
        Mensagem = mensagem;
        MensagemEhErro = true;
    }
}
