using Clinica.Application.Servicos;
using Clinica.Desktop.Shell;
using Clinica.Desktop.Controls;
using Clinica.Domain.Entities;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;

namespace Clinica.Financeiro.ViewModels;

public sealed partial class BaixarLancamentoViewModel(IServiceScopeFactory escopos, LancamentoFinanceiro lancamento) : ObservableObject, Clinica.Desktop.Shell.Componentes.IRecebimento
{
    public string ValorInformado { get => ValorPago; set => ValorPago = value; }
    public event Action? Concluido;
    public string Descricao => lancamento.Descricao;
    public string Valor => $"Saldo da conta: {lancamento.Valor:C2}";
    [ObservableProperty] private string _valorPago = lancamento.Valor.ToString("0.00");
    [ObservableProperty] private DateTime _vencimentoSaldo = (lancamento.DataVencimento ?? DateOnly.FromDateTime(DateTime.Today)).ToDateTime(TimeOnly.MinValue);
    public string Titulo => lancamento.Tipo == TipoLancamento.Entrada ? "Confirmar recebimento" : "Confirmar pagamento";
    public IReadOnlyList<FormaPagamento> Formas { get; } = Enum.GetValues<FormaPagamento>();
    [ObservableProperty] private DateTime _data = DateTime.Today;
    [ObservableProperty] [NotifyPropertyChangedFor(nameof(EhCartao))] private FormaPagamento? _forma = lancamento.FormaPagamento;
    [ObservableProperty] private string? _adquirente = lancamento.Adquirente;
    [ObservableProperty] private string? _bandeira = lancamento.Bandeira;
    [ObservableProperty] private string _parcelas = (lancamento.Parcelas ?? 1).ToString();
    [ObservableProperty] private string? _mensagem;
    [ObservableProperty] [NotifyPropertyChangedFor(nameof(PodeConfirmar))] private bool _ocupado;
    public bool EhCartao => lancamento.Tipo == TipoLancamento.Entrada && Forma is FormaPagamento.CartaoCredito or FormaPagamento.CartaoDebito;
    public bool PodeConfirmar => !Ocupado && SessaoUsuario.Atual.Pode(Permissao.EditarFinanceiro);

    [RelayCommand]
    private async Task ConfirmarAsync()
    {
        if (Ocupado) return;
        try
        {
            Ocupado = true;
            SessaoUsuario.Atual.Exigir(Permissao.EditarFinanceiro, "confirmar pagamento ou recebimento");
            if (Forma is not { } forma) throw new InvalidOperationException("Informe a forma de pagamento.");
            if (!int.TryParse(Parcelas, out var parcelas)) throw new InvalidOperationException("Informe o número de parcelas.");
            if (!Valores.TentarLerNumeroExato(ValorPago, out var valorPago)) throw new InvalidOperationException("Informe o valor efetivamente pago ou recebido.");
            using var scope = escopos.CreateScope();
            await scope.ServiceProvider.GetRequiredService<FinanceiroService>().RealizarAsync(
                lancamento.Id, DateOnly.FromDateTime(Data), forma, SessaoUsuario.Atual.Operador,
                adquirente: Adquirente, bandeira: Bandeira, parcelas: forma == FormaPagamento.CartaoCredito ? parcelas : 1,
                valorConferido: lancamento.Valor, valorPago: valorPago, vencimentoSaldo: DateOnly.FromDateTime(VencimentoSaldo));
            Concluido?.Invoke();
        }
        catch (Exception ex) { Mensagem = ex.Message; }
        finally { Ocupado = false; }
    }
}
