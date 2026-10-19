using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using Clinica.Desktop.Shell.Componentes;
using Clinica.Desktop.Shell.Modulos;
using Clinica.Domain.Entities;
using Clinica.Infrastructure;
using CommunityToolkit.Mvvm.Input;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

internal static partial class Program
{
    static async Task VerificarRevisaoPrAsync(ServiceProvider provider, DbContextOptions<ClinicaDbContext> options, SentinelaProntuario sentinela)
    {
        var escopos = provider.GetRequiredService<IServiceScopeFactory>();
        var conta = new LancamentoFinanceiro { Valor = 100, Descricao = "Recebimento fictício" };
        // A baixa do Financeiro é Blazor desde out/2026 (RecebimentoForm); aqui fica a da Recepção.
        foreach (var recebimento in new IRecebimento[] { new ReceberPagamentoViewModel(escopos, conta) })
        {
            var ocupado = recebimento.GetType().GetProperty("Ocupado")!;
            var janela = new RecebimentoWindow(recebimento) { ShowInTaskbar = false, Left = -30000, Top = -30000,
                WindowStartupLocation = WindowStartupLocation.Manual };
            janela.Show(); ocupado.SetValue(recebimento, true);
            await Dispatcher.Yield(DispatcherPriority.DataBind);
            var cancelar = Visuais(janela).OfType<Button>().Single(b => Equals(b.Content, "Cancelar"));
            Conferir(!cancelar.IsEnabled, recebimento.GetType().Name + ": cancelar bloqueado enquanto grava");
            janela.Close();
            Conferir(janela.IsVisible, recebimento.GetType().Name + ": fechar não oculta gravação pendente");
            ocupado.SetValue(recebimento, false); janela.Close();
            Conferir(!janela.IsVisible, recebimento.GetType().Name + ": permite fechar após terminar");
            var concluida = new RecebimentoWindow(recebimento) { ShowInTaskbar = false, Left = -30000, Top = -30000,
                WindowStartupLocation = WindowStartupLocation.Manual };
            concluida.Loaded += (_, _) => concluida.Dispatcher.BeginInvoke(new Action(() =>
            {
                ocupado.SetValue(recebimento, true);
                var notificar = (Action?)recebimento.GetType().GetField("Concluido", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(recebimento);
                notificar?.Invoke();
            }));
            Conferir(concluida.ShowDialog() == true, recebimento.GetType().Name + ": conclusão devolve sucesso mesmo antes de liberar Ocupado");
            ocupado.SetValue(recebimento, false);
        }
    }
}

sealed class EscoposComFalha(IServiceScopeFactory original) : IServiceScopeFactory
{
    public bool Falhar { get; set; } = true;
    public IServiceScope CreateScope() => Falhar
        ? throw new InvalidOperationException("Falha simulada anterior à auditoria de leitura")
        : original.CreateScope();
}
