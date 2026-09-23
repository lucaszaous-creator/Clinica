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
            if (new RecebimentoWindow(vm) { Owner = JanelaDona.Atual() }.ShowDialog() == true)
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
