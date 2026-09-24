using System.Collections.ObjectModel;
using Clinica.Application.Servicos;
using Clinica.Desktop.Controls;
using Clinica.Desktop.Shell;
using Clinica.Desktop.Shell.Componentes;
using Clinica.Domain.Entities;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;

namespace Clinica.Desktop.Shell.Componentes;
public sealed partial class ReceberPagamentoViewModel : ObservableObject, IRecebimento
{
    public string Titulo => "Receber pagamento do paciente";
    public string ValorInformado { get => ValorRecebido; set => ValorRecebido = value; }
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
    public bool PodeConfirmar => !Ocupado && SessaoUsuario.Atual.PodeAlgum(CobrancaDoPacienteViewModel.QuemRecebe);

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
            SessaoUsuario.Atual.ExigirAlgum(CobrancaDoPacienteViewModel.QuemRecebe, "receber pagamento do paciente");
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
