using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Clinica.Clinico.ViewModels;
using Clinica.Clinico.Views;
using Clinica.Desktop.Shell.Modulos;
using Clinica.Domain;
using Clinica.Domain.Entities;
using Clinica.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

internal static class MedicoQa
{
    public static async Task Executar(IServiceProvider sp, ClinicaDbContext db, Profissional profissional, string saida)
    {
        var nomes = new[] { "João Fictício", "Maria Fictícia", "Carlos Fictício" };
        for (var i = 0; i < nomes.Length; i++)
        {
            var paciente = new Paciente { Nome = nomes[i], Documento = "", Telefone = "", Convenio = Convenio.UnimedIntercambio };
            db.Add(paciente);
            db.Add(new Agendamento { Paciente = paciente, ProfissionalId = profissional.Id, DataHora = DateTime.Today.AddHours(8 + i), ModalidadePrevista = ModalidadeAtendimento.Consulta,
                Status = i == 1 ? StatusAgendamento.Cancelado : StatusAgendamento.Agendado });
        }
        await db.SaveChangesAsync();
        var vm = new MeuDiaViewModel(sp.GetRequiredService<IServiceScopeFactory>(), new PacienteEmFoco());
        while (vm.Carregando) await Task.Delay(50);
        if (vm.NaoVerificado || vm.Sessoes.Count != 3) throw new Exception("Meu dia sintético não carregou os três horários.");
        var view = new MeuDiaView { DataContext = vm };
        var janela = new Window { Content = view, Width = 1100, Height = 720, Left = -30000, Top = -30000, ShowInTaskbar = false };
        janela.Show();
        try
        {
            var navegador = Program.Navegador(view);
            await Program.Esperar(navegador, "document.querySelector('.resumo-agenda')?.textContent.includes('3 de 3')===true");
            if (Desc(view).OfType<TabControl>().Any() || Desc(view).OfType<DataGrid>().Any()) throw new Exception("Lista do médico duplicada em tabela ou abas extras.");
            await Program.Esperar(navegador, "document.querySelectorAll('.linha-agenda').length===3");
            await Program.Script(navegador, "(()=>{let e=document.querySelector('input');e.focus();Object.getOwnPropertyDescriptor(HTMLInputElement.prototype,'value').set.call(e,'JOAO');e.dispatchEvent(new Event('input',{bubbles:true}));})()");
            await Program.Esperar(navegador, "document.querySelector('.resumo-agenda').textContent.includes('1 de 3')");
            await Task.Delay(150);
            await Program.Esperar(navegador, "document.querySelectorAll('.linha-agenda').length===1 && document.querySelector('.paciente h2').textContent==='João Fictício'");
            await vm.CarregarAsync(); await Task.Delay(400);
            await Program.Esperar(navegador, "document.querySelectorAll('.linha-agenda').length===1");
            await Program.Esperar(navegador, "document.activeElement===document.querySelector('input') && document.querySelector('input').value==='JOAO'");
            await Program.Script(navegador, "document.querySelector('.secundario').click();");
            await Program.Esperar(navegador, "document.querySelector('.resumo-agenda').textContent.includes('3 de 3')");
            await Program.Script(navegador, "document.querySelector('.filtro-situacao.cancelado').click()");
            await Program.Esperar(navegador, "document.querySelector('.resumo-agenda').textContent.includes('1 de 3')"); await Task.Delay(150);
            await Program.Esperar(navegador, "document.querySelectorAll('.linha-agenda').length===1 && document.querySelector('.paciente h2').textContent==='Maria Fictícia'");
            await Program.Script(navegador, "document.querySelector('.secundario').click()"); await Task.Delay(250);
            foreach (var largura in new[] { 1366, 1100, 900 })
            {
                janela.Width = largura; await Task.Delay(350); janela.UpdateLayout();
                await Program.Esperar(navegador, "document.documentElement.scrollWidth<=innerWidth+1");
                if (navegador.ActualHeight < 180) throw new Exception("Lista médica sem área útil.");
                await Program.ConferirAlinhamento(navegador);
                await Program.Capturar(navegador, Path.Combine(saida, $"meu-dia-react-{largura}.png"));
            }
            Console.WriteLine("OK Meu dia: lista React única sem abas, busca sem acentos, cancelados, foco e filtros preservados após recarga; 1366/1100/900 px.");
        }
        finally { janela.Close(); vm.AoSairDeCena(); }
    }
    private static IEnumerable<DependencyObject> Desc(DependencyObject raiz)
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(raiz); i++) { var filho = VisualTreeHelper.GetChild(raiz, i); yield return filho; foreach (var item in Desc(filho)) yield return item; }
    }
}
