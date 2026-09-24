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
    static Task CarregarAdministrativoAsync(object adapter) => (Task)adapter.GetType()
        .GetMethod("CarregarAsync", BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(adapter, null)!;

    static async Task VerificarRevisaoPrAsync(ServiceProvider provider, DbContextOptions<ClinicaDbContext> options, SentinelaProntuario sentinela)
    {
        var foco = new PacienteEmFoco(); foco.Definir(1001, "Paciente de demonstração");
        var ficha = provider.GetRequiredService<IFabricaFichaPaciente>().Criar(foco);
        var vm = (Clinica.Clinico.ViewModels.PacienteWorkspaceViewModel)ficha.DataContext;
        await CarregarAdministrativoAsync(vm.Administrativo!);
        var dados = (Clinica.Recepcao.ViewModels.FichaPacienteViewModel)((FrameworkElement)vm.Administrativo!.Resumo).DataContext;
        using (var db = new ClinicaDbContext(options))
        {
            var p = await db.Pacientes.FindAsync(1001); p!.Nome = "Cadastro alterado em outro acesso";
            await db.SaveChangesAsync();
        }
        var view = new Clinica.Clinico.Views.PacienteView { DataContext = vm };
        view.Measure(new Size(1366, 728)); view.Arrange(new Rect(0, 0, 1366, 728)); view.UpdateLayout();
        await Dispatcher.Yield(DispatcherPriority.DataBind);
        var atualizar = Visuais(view).OfType<Button>().Single(b => Equals(b.Content, "Atualizar ficha"));
        await ((IAsyncRelayCommand)atualizar.Command).ExecuteAsync(null);
        Conferir(dados.Nome == "Cadastro alterado em outro acesso", "Atualizar ficha renova também as seções administrativas");
        Conferir(vm.Capa.Paciente == "Cadastro alterado em outro acesso", "Atualizar ficha renova os dados principais");
        using (var db = new ClinicaDbContext(options))
        {
            var p = await db.Pacientes.FindAsync(1001); p!.Nome = "Paciente de demonstração";
            await db.SaveChangesAsync();
        }

        var adapter = provider.GetRequiredService<IFichaAdministrativaPaciente>(); adapter.DefinirPaciente(1001);
        var recuperacao = (Clinica.Recepcao.ViewModels.FichaPacienteViewModel)((FrameworkElement)adapter.Resumo).DataContext;
        sentinela.FalharPaciente = true;
        try { await CarregarAdministrativoAsync(adapter); }
        finally { sentinela.FalharPaciente = false; }
        Conferir(recuperacao.MensagemEhErro, "Falha simulada de leitura é identificada na ficha administrativa");
        await CarregarAdministrativoAsync(adapter);
        Conferir(recuperacao.Nome == "Paciente de demonstração" && !recuperacao.MensagemEhErro,
            "Reabrir seção administrativa recupera uma primeira carga malsucedida");

        var falhaEscopo = new EscoposComFalha(provider.GetRequiredService<IServiceScopeFactory>());
        var auditada = new Clinica.Recepcao.ViewModels.FichaAdministrativaPaciente(falhaEscopo,
            provider.GetRequiredService<Clinica.Desktop.Controls.ISnackbarService>(),
            provider.GetRequiredService<Clinica.Desktop.Controls.IDialogoService>());
        auditada.DefinirPaciente(1001);
        await CarregarAdministrativoAsync(auditada);
        var estado = (Clinica.Recepcao.ViewModels.FichaPacienteViewModel)((FrameworkElement)auditada.Resumo).DataContext;
        Conferir(estado.MensagemEhErro && estado.Mensagem.Contains("Atualizar ficha"),
            "Falha antes da leitura administrativa é tratada com orientação de recuperação");
        falhaEscopo.Falhar = false;
        await CarregarAdministrativoAsync(auditada);
        Conferir(estado.Nome == "Paciente de demonstração" && !estado.MensagemEhErro,
            "Nova tentativa após falha na abertura recupera a ficha administrativa");

        var escopos = provider.GetRequiredService<IServiceScopeFactory>();
        var conta = new LancamentoFinanceiro { Valor = 100, Descricao = "Recebimento fictício" };
        foreach (var recebimento in new IRecebimento[] { new ReceberPagamentoViewModel(escopos, conta),
                     new Clinica.Financeiro.ViewModels.BaixarLancamentoViewModel(escopos, conta) })
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
