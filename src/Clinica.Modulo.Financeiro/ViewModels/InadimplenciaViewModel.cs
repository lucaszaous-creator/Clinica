using System.Collections.ObjectModel;
using System.Globalization;
using Clinica.Application.Servicos;
using Clinica.Desktop.Controls;
using Clinica.Desktop.Shell;
using Clinica.Desktop.Shell.Componentes;
using Clinica.Domain.Entities;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;

namespace Clinica.Financeiro.ViewModels;

/// <summary>Uma conta vencida, dentro da linha do paciente.</summary>
public sealed class LinhaContaAtraso
{
    public required int LancamentoId { get; init; }
    public required string Descricao { get; init; }
    public required string Valor { get; init; }
    public required string Vencimento { get; init; }
    public required string Atraso { get; init; }
}

/// <summary>Um devedor, como a tela mostra.</summary>
public sealed class LinhaDevedor
{
    public required int PacienteId { get; init; }
    public required string Nome { get; init; }
    public required string Total { get; init; }
    public required string Resumo { get; init; }
    public required string Faixa { get; init; }
    public required bool Critico { get; init; }
    public required bool TemTelefone { get; init; }
    public required IReadOnlyList<LinhaContaAtraso> Contas { get; init; }

    /// <summary>O devedor cru, para o comando de cobrança montar a mensagem sem remontar nada.</summary>
    public required PacienteInadimplente Origem { get; init; }
}

/// <summary>Uma faixa do envelhecimento, com a barra.</summary>
public sealed record LinhaFaixaAtraso(string Rotulo, string ValorRotulo, double Fracao);

/// <summary>
/// Inadimplência por paciente (parcela 23).
///
/// A parcela 12 deu ao sistema o vencimento, e com ele "o que vence esta semana". Faltava
/// a pergunta seguinte, que é a que a clínica de fato faz: **quem me deve?** A conta a
/// receber vencida existia, mas dissolvida na lista de Contas — uma linha por lançamento,
/// misturada com o que a clínica tem a PAGAR. Para saber que o mesmo paciente tinha três
/// sessões em aberto era preciso ler a lista inteira e somar de cabeça, e é por isso que
/// ninguém cobrava.
///
/// A tela agrupa, soma, envelhece e leva ao WhatsApp. O que ela **não** faz é marcar
/// ninguém: não existe (e não deve existir) campo "inadimplente" no cadastro — paciente
/// marcado assim continua marcado depois de pagar, e quem lê a ficha dois meses depois
/// trata como caloteiro alguém que quitou. A situação é calculada, sempre.
/// </summary>
public sealed partial class InadimplenciaViewModel : ObservableObject
{
    private static readonly CultureInfo Brasil = new("pt-BR");

    private readonly IServiceScopeFactory _escopos;
    private readonly ISnackbarService _snackbar;
    private readonly IDialogoService _dialogo;

    public ObservableCollection<LinhaDevedor> Devedores { get; } = [];
    public ObservableCollection<LinhaFaixaAtraso> Faixas { get; } = [];

    public IReadOnlyList<OrdemInadimplencia> Ordens { get; } =
        Enum.GetValues<OrdemInadimplencia>();

    /// <summary>
    /// Mais antigo primeiro por padrão: a chance de receber cai com o tempo, e o caso de
    /// seis meses é o que precisa de decisão — não de mais um lembrete.
    /// </summary>
    [ObservableProperty] private OrdemInadimplencia _ordem = OrdemInadimplencia.MaisAntigo;

    [ObservableProperty] private string _total = "—";
    [ObservableProperty] private string _quantidadePacientes = "—";
    [ObservableProperty] private string _quantidadeContas = "—";
    [ObservableProperty] private string _medioPorPaciente = "—";
    [ObservableProperty] private string _resumo = string.Empty;
    [ObservableProperty] private bool _vazio;
    [ObservableProperty] private bool _carregando;

    [ObservableProperty] private string? _mensagem;
    [ObservableProperty] private bool _mensagemEhErro;

    /// <summary>Metade visível da permissão; a que impede é o <c>Exigir</c> no comando.</summary>
    public bool PodeEditar => SessaoUsuario.Atual.Pode(Permissao.EditarFinanceiro);

    public InadimplenciaViewModel(
        IServiceScopeFactory escopos, ISnackbarService snackbar, IDialogoService dialogo)
    {
        _escopos = escopos;
        _snackbar = snackbar;
        _dialogo = dialogo;
        _ = CarregarAsync();
    }

    partial void OnOrdemChanged(OrdemInadimplencia value) => _ = CarregarAsync();

    [RelayCommand]
    public async Task CarregarAsync()
    {
        if (Carregando) return;
        Carregando = true;
        try
        {
            Mensagem = null;
            MensagemEhErro = false;

            var hoje = DateOnly.FromDateTime(DateTime.Today);

            using var scope = _escopos.CreateScope();
            var servico = scope.ServiceProvider.GetRequiredService<InadimplenciaService>();

            var devedores = await servico.PorPacienteAsync(hoje, Ordem);
            var resumo = await servico.ResumoAsync(hoje);

            Devedores.Clear();
            foreach (var d in devedores) Devedores.Add(Montar(d));

            Faixas.Clear();
            foreach (var f in resumo.Faixas)
                Faixas.Add(new LinhaFaixaAtraso(
                    f.Faixa,
                    f.Pacientes > 0 ? $"{Moeda(f.Valor)} · {f.Pacientes}" : Moeda(f.Valor),
                    f.Fracao));

            Total = Moeda(resumo.Total);
            QuantidadePacientes = resumo.Pacientes.ToString(Brasil);
            QuantidadeContas = resumo.Contas.ToString(Brasil);
            // Sem ninguém devendo não há média: "R$ 0,00 por paciente" se lê como
            // "ninguém deve quase nada".
            MedioPorPaciente = resumo.MedioPorPaciente is { } medio ? Moeda(medio) : "—";

            Vazio = resumo.Vazio;
            Resumo = resumo.Vazio
                ? "Nenhuma conta de paciente vencida. Nada a cobrar hoje."
                : $"{resumo.Pacientes} paciente(s) com {resumo.Contas} conta(s) vencida(s).";
        }
        catch (Exception ex)
        {
            Clinica.Application.Diagnostico.Registrar("Financeiro — inadimplência não pôde ser lida", ex);
            Mensagem = $"Não foi possível ler a inadimplência: {ex.Message}";
            MensagemEhErro = true;
        }
        finally
        {
            Carregando = false;
        }
    }

    private static LinhaDevedor Montar(PacienteInadimplente d) => new()
    {
        PacienteId = d.PacienteId,
        Nome = d.Nome,
        Total = Moeda(d.Total),
        Resumo = d.Contas == 1
            ? $"1 conta · vencida faz {d.DiasMaiorAtraso} dia(s)"
            : $"{d.Contas} contas · a mais antiga faz {d.DiasMaiorAtraso} dia(s)",
        Faixa = d.Faixa,
        // Acima de 90 dias a cobrança deixa de ser lembrete: é onde se decide acordo ou
        // parar de contar com o dinheiro.
        Critico = d.DiasMaiorAtraso > 90,
        TemTelefone = !string.IsNullOrWhiteSpace(d.Telefone),
        Contas = d.Detalhe
            .Select(c => new LinhaContaAtraso
            {
                LancamentoId = c.LancamentoId,
                Descricao = c.Descricao,
                Valor = Moeda(c.Valor),
                Vencimento = c.Vencimento.ToString("dd/MM/yyyy", Brasil),
                Atraso = $"venceu faz {c.DiasEmAtraso} dia(s)"
            })
            .ToList(),
        Origem = d
    };

    /// <summary>
    /// Abre o WhatsApp com o lembrete pronto.
    ///
    /// **Cobrança não exige consentimento de marketing** — é transacional, como a
    /// confirmação da própria sessão: a clínica está falando de um acordo que existe entre
    /// as duas partes, não oferecendo nada. NPS e recall, que são oferta, continuam
    /// exigindo o consentimento vigente.
    ///
    /// Um clique por paciente, de propósito: o número é o WhatsApp da clínica, e disparo em
    /// lote de cobrança é o tipo de coisa que se faz uma vez e se explica pelo resto do ano.
    /// </summary>
    [RelayCommand]
    private void Cobrar(LinhaDevedor? linha)
    {
        if (linha is null) return;

        var erro = Whatsapp.Abrir(
            linha.Origem.Telefone,
            linha.Nome,
            InadimplenciaService.MensagemDeCobranca(linha.Origem));

        if (erro is null)
        {
            _snackbar.Sucesso($"WhatsApp aberto para {linha.Nome}.");
            return;
        }

        Mensagem = erro;
        MensagemEhErro = true;
    }

    /// <summary>
    /// Registra o recebimento da conta. Passa pelo <see cref="InadimplenciaService"/>, que
    /// delega ao <see cref="FinanceiroService"/> — quem grava dinheiro continua sendo um
    /// só, com a auditoria no mesmo SaveChanges.
    ///
    /// A confirmação é obrigatória e mostra o valor: dar baixa por engano numa conta
    /// vencida apaga uma dívida real, e o erro só aparece no fim do mês.
    /// </summary>
    [RelayCommand]
    private async Task ReceberAsync(LinhaContaAtraso? conta)
    {
        if (conta is null) return;

        try
        {
            SessaoUsuario.Atual.Exigir(Permissao.EditarFinanceiro, "dar baixa em conta de paciente");

            if (!_dialogo.Confirmar(
                    "Receber conta",
                    $"Registrar o recebimento de {conta.Valor} ({conta.Descricao})?"))
                return;

            using var scope = _escopos.CreateScope();
            var servico = scope.ServiceProvider.GetRequiredService<InadimplenciaService>();

            await servico.ReceberAsync(
                conta.LancamentoId,
                DateOnly.FromDateTime(DateTime.Today),
                operador: SessaoUsuario.Atual.Operador);

            _snackbar.Sucesso("Recebimento registrado.");
            await CarregarAsync();
        }
        catch (Exception ex)
        {
            Clinica.Application.Diagnostico.Registrar("Financeiro — recebimento não pôde ser registrado", ex);
            Mensagem = ex.Message;
            MensagemEhErro = true;
        }
    }

    /// <summary>
    /// Exporta a lista. É o que se leva para a reunião e o que se manda ao contador —
    /// e a planilha é onde a clínica de fato organiza a rodada de ligações.
    /// </summary>
    [RelayCommand]
    private async Task ExportarAsync()
    {
        try
        {
            var linhas = Devedores
                .Select(d => (IReadOnlyList<string>)new[]
                {
                    d.Nome, d.Total, d.Contas.Count.ToString(Brasil), d.Faixa,
                    d.Contas.Count > 0 ? d.Contas[0].Vencimento : "—"
                })
                .ToList();

            var csv = ExportacaoCsv.Montar(
                ["Paciente", "Total em atraso", "Contas", "Faixa", "Vencimento mais antigo"],
                linhas);

            var erro = await ImpressaoPdf.SalvarEAbrirAsync(
                csv, $"inadimplencia-{DateTime.Today:yyyy-MM-dd}.csv",
                "CSV (*.csv)|*.csv", ".csv");

            Mensagem = erro;
            MensagemEhErro = erro is not null;
        }
        catch (Exception ex)
        {
            Clinica.Application.Diagnostico.Registrar("Financeiro — inadimplência não pôde ser exportada", ex);
            Mensagem = $"Não foi possível exportar: {ex.Message}";
            MensagemEhErro = true;
        }
    }

    private static string Moeda(decimal valor) => valor.ToString("C2", Brasil);
}
