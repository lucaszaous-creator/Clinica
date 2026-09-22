using System.Collections.ObjectModel;
using Clinica.Application.Servicos;
using Clinica.Desktop.Controls;
using Clinica.Desktop.Shell;
using Clinica.Desktop.Shell.Componentes;
using Clinica.Domain.Entities;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;

namespace Clinica.Recepcao.ViewModels;

public sealed record LinhaPagamento(LancamentoFinanceiro Lancamento)
{
    public string Descricao => Lancamento.Descricao;
    public string Data => Lancamento.Data.ToString("dd/MM/yyyy");
    public string Vencimento => Lancamento.DataVencimento?.ToString("dd/MM/yyyy") ?? "—";
    public string Valor => Lancamento.Valor.ToString("C2");
    public string Situacao => Lancamento.Status == StatusLancamento.Realizado
        ? $"Recebido em {Lancamento.DataPagamento:dd/MM/yyyy} · {Clinica.Domain.RotulosEnum.De(Lancamento.FormaPagamento)}"
        : Lancamento.EstaVencido(DateOnly.FromDateTime(DateTime.Today)) ? "Vencido" : "A receber";
    public bool EmAberto => Lancamento.Status == StatusLancamento.Previsto;
    public bool Recebido => Lancamento.Status == StatusLancamento.Realizado;
}

public sealed partial class PagamentosViewModel : ObservableObject
{
    private readonly IServiceScopeFactory _escopos;
    public SeletorPacienteViewModel Seletor { get; }
    public ObservableCollection<LinhaPagamento> Linhas { get; } = [];
    [ObservableProperty] [NotifyPropertyChangedFor(nameof(SemPaciente))] private bool _temPaciente;
    public bool SemPaciente => !TemPaciente;
    public bool PodeReceber => SessaoUsuario.Atual.Pode(Permissao.VenderPacote);
    [ObservableProperty] private string _paciente = string.Empty;
    [ObservableProperty] private string _resumo = "Selecione o paciente para consultar seus pagamentos.";
    [ObservableProperty] private string? _mensagem;
    [ObservableProperty] private bool _carregando;
    [ObservableProperty] private bool _naoVerificado;
    private int _geracao;

    public PagamentosViewModel(IServiceScopeFactory escopos)
    {
        _escopos = escopos;
        Seletor = new SeletorPacienteViewModel(escopos) { SemBuscaInicial = true };
        Seletor.SelecaoMudou += paciente => _ = CarregarAsync();
    }

    [RelayCommand]
    public async Task CarregarAsync()
    {
        var geracao = ++_geracao;
        var paciente = Seletor.Selecionado;
        TemPaciente = paciente is not null;
        Paciente = paciente?.Nome ?? string.Empty;
        Linhas.Clear();
        Resumo = "Consultando pagamentos…";
        Mensagem = null;
        NaoVerificado = false;
        if (paciente is null) { Carregando = false; return; }
        try
        {
            Carregando = true;
            SessaoUsuario.Atual.Exigir(Permissao.VenderPacote, "consultar pagamentos do paciente");
            using var scope = _escopos.CreateScope();
            var itens = await scope.ServiceProvider.GetRequiredService<PagamentosRecepcaoService>().DoPacienteAsync(paciente.Id);
            if (geracao != _geracao) return;
            foreach (var l in itens) Linhas.Add(new LinhaPagamento(l));
            Resumo = $"{itens.Where(l => l.Status == StatusLancamento.Previsto).Sum(l => l.Valor):C2} a receber · "
                + $"{itens.Count(l => l.Status == StatusLancamento.Previsto)} cobrança(s) em aberto";
        }
        catch (Exception ex)
        {
            if (geracao == _geracao) { NaoVerificado = true; Mensagem = ex.Message; Resumo = "Pagamentos não verificados."; }
            Clinica.Application.Diagnostico.Registrar("Recepção — consulta de pagamentos", ex);
        }
        finally { if (geracao == _geracao) Carregando = false; }
    }

    [RelayCommand] private void TrocarPaciente() => Seletor.Selecionado = null;

    [RelayCommand]
    private async Task ReceberAsync(LinhaPagamento? linha)
    {
        if (linha is null || !linha.EmAberto) return;
        try
        {
            SessaoUsuario.Atual.Exigir(Permissao.VenderPacote, "receber pagamento do paciente");
            var vm = new ReceberPagamentoViewModel(_escopos, linha.Lancamento);
            if (new Janelas.ReceberPagamentoWindow(vm) { Owner = JanelaDona.Atual() }.ShowDialog() == true)
                await CarregarAsync();
        }
        catch (Exception ex) { Mensagem = ex.Message; }
    }

    [RelayCommand]
    private async Task ReciboAsync(LinhaPagamento? linha)
    {
        if (linha is null || !linha.Recebido) return;
        try
        {
            SessaoUsuario.Atual.Exigir(Permissao.VenderPacote, "emitir recibo do paciente");
            using var scope = _escopos.CreateScope();
            var doc = await scope.ServiceProvider.GetRequiredService<DocumentoFinanceiroService>()
                .EmitirReciboDoLancamentoAsync(linha.Lancamento.Id, operador: SessaoUsuario.Atual.Operador);
            var pdf = await scope.ServiceProvider.GetRequiredService<DocumentosFinanceirosPdfService>()
                .GerarAsync(doc.Id, await scope.ServiceProvider.GetRequiredService<ParametrosService>().ObterPrestadorAsync());
            Mensagem = await ImpressaoPdf.SalvarEAbrirAsync(pdf,
                ImpressaoPdf.NomeSeguro($"Recibo-{doc.Numero.Replace('/', '-')}.pdf")) ?? $"Recibo {doc.Numero} emitido.";
        }
        catch (Exception ex) { Mensagem = ex.Message; }
    }
}

public sealed partial class ReceberPagamentoViewModel : ObservableObject
{
    private readonly IServiceScopeFactory _escopos;
    private readonly LancamentoFinanceiro _cobranca;
    public event Action? Concluido;
    public string Descricao => _cobranca.Descricao;
    public string Valor => $"Saldo da cobrança: {_cobranca.Valor:C2}";
    [ObservableProperty] private string _valorRecebido = string.Empty;
    [ObservableProperty] private DateTime _vencimentoSaldo = DateTime.Today;
    public IReadOnlyList<FormaPagamento> Formas { get; } = Enum.GetValues<FormaPagamento>()
        .Where(f => f != FormaPagamento.Convenio).ToArray();
    [ObservableProperty] private DateTime _data = DateTime.Today;
    [ObservableProperty] [NotifyPropertyChangedFor(nameof(EhCartao))] private FormaPagamento? _forma;
    [ObservableProperty] private string? _adquirente;
    [ObservableProperty] private string? _bandeira;
    [ObservableProperty] private string _parcelas = "1";
    [ObservableProperty] private string? _mensagem;
    [ObservableProperty] [NotifyPropertyChangedFor(nameof(PodeConfirmar))] private bool _ocupado;
    public bool EhCartao => Forma is FormaPagamento.CartaoCredito or FormaPagamento.CartaoDebito;
    public bool PodeConfirmar => !Ocupado && SessaoUsuario.Atual.Pode(Permissao.VenderPacote);

    public ReceberPagamentoViewModel(IServiceScopeFactory escopos, LancamentoFinanceiro cobranca)
    {
        _escopos = escopos; _cobranca = cobranca;
        ValorRecebido = cobranca.Valor.ToString("0.00");
        VencimentoSaldo = (cobranca.DataVencimento ?? DateOnly.FromDateTime(DateTime.Today)).ToDateTime(TimeOnly.MinValue);
    }

    [RelayCommand]
    private async Task ConfirmarAsync()
    {
        if (Ocupado) return;
        try
        {
            Ocupado = true;
            SessaoUsuario.Atual.Exigir(Permissao.VenderPacote, "receber pagamento do paciente");
            if (Forma is not { } forma) throw new InvalidOperationException("Informe a forma de pagamento.");
            if (!int.TryParse(Parcelas, out var parcelas)) throw new InvalidOperationException("Informe o número de parcelas.");
            if (!Clinica.Desktop.Controls.Valores.TentarLerNumeroExato(ValorRecebido, out var recebido)) throw new InvalidOperationException("Informe o valor recebido agora.");
            using var scope = _escopos.CreateScope();
            await scope.ServiceProvider.GetRequiredService<PagamentosRecepcaoService>().ReceberAsync(
                _cobranca.PacienteId!.Value, _cobranca.Id, _cobranca.Valor, DateOnly.FromDateTime(Data), forma,
                SessaoUsuario.Atual.Operador, Adquirente, Bandeira, forma == FormaPagamento.CartaoCredito ? parcelas : 1,
                valorRecebido: recebido, vencimentoSaldo: DateOnly.FromDateTime(VencimentoSaldo));
            Concluido?.Invoke();
        }
        catch (Exception ex) { Mensagem = ex.Message; }
        finally { Ocupado = false; }
    }
}
