using System.IO;
using System.Windows;
using System.Windows.Controls;
using Clinica.Desktop.Controls;
using Clinica.Desktop.Shell;
using Clinica.Domain;
using Clinica.Domain.Entities;
using Clinica.Recepcao.ViewModels;
using Clinica.Recepcao.Views;
using Microsoft.Extensions.DependencyInjection;

internal static class FilaQa
{
    public static async Task Executar(IServiceProvider servicos, string saida)
    {
        var vm = new FilaViewModel(servicos.GetRequiredService<IServiceScopeFactory>(), new SnackbarService(), new Dialogos());
        var prazo = DateTime.UtcNow.AddSeconds(20);
        while (vm.Carregando && DateTime.UtcNow < prazo) await Task.Delay(100);
        if (vm.Carregando || vm.NaoVerificado) throw new Exception("Não foi possível preparar a agenda sintética da recepção.");
        vm.Linhas.Clear();
        CartaoFila Linha(int id, string nome, StatusAgendamento status) => new()
        {
            AgendamentoId = id, PacienteId = id, Horario = "08:00–08:30", Paciente = nome, Modalidade = "Consulta",
            ModalidadeFamilia = ModalidadeAtendimento.Consulta, Profissional = "Profissional fictício", Sala = "",
            Etapa = status == StatusAgendamento.Cancelado ? EtapaFila.ForaDaFila : EtapaFila.Aguardando,
            Situacao = status, DataHora = vm.Dia.Date.AddHours(8), EhRetornoDoSegundoCodigo = false,
            EhEncaixe = false, FechamentoPendente = false, Lancamento = "Teste sintético"
        };
        vm.Linhas.Add(Linha(9901, "João da Silva", StatusAgendamento.Agendado));
        vm.Linhas.Add(Linha(9902, "João Souza", StatusAgendamento.Cancelado));
        vm.Linhas.Add(Linha(9903, "Maria Clara", StatusAgendamento.Agendado));
        var view = new FilaView { DataContext = vm };
        var janela = new Window { Content = view, Width = 1100, Height = 720, Left = -30000, Top = -30000, ShowInTaskbar = false };
        janela.Show();
        try
        {
            var navegador = Program.Navegador(view);
            await Program.Esperar(navegador, "document.querySelector('.resumo-agenda')?.textContent.includes('3 de 3')===true");
            var tabela = (DataGrid)view.FindName("TabelaAgenda");
            await Program.Script(navegador, "(()=>{let e=document.querySelector('input');e.focus();Object.getOwnPropertyDescriptor(HTMLInputElement.prototype,'value').set.call(e,'JOAO');e.dispatchEvent(new Event('input',{bubbles:true}));})()");
            await Program.Esperar(navegador, "document.querySelector('.resumo-agenda').textContent.includes('2 de 3')");
            await Task.Delay(200);
            if (tabela.Items.Count != 2) throw new Exception("Busca React não filtrou as linhas WPF da recepção.");
            await Program.Script(navegador, "document.querySelector('.filtro-situacao.cancelado').click()");
            await Program.Esperar(navegador, "document.querySelector('.resumo-agenda').textContent.includes('1 de 3')");
            await Task.Delay(200);
            if (tabela.Items.Count != 1 || ((CartaoFila)tabela.Items[0]).AgendamentoId != 9902) throw new Exception("Filtro de situação não preservou o paciente correto.");
            await Program.Script(navegador, "document.querySelector('.filtros-agenda .secundario').click()");
            await Program.Esperar(navegador, "document.querySelector('.resumo-agenda').textContent.includes('3 de 3')");
            await Task.Delay(200);
            if (tabela.Items.Count != 3 || tabela.Columns.Count < 4) throw new Exception("Limpar não restaurou tabela/ações da recepção.");
            foreach (var largura in new[] { 1100, 900 })
            {
                janela.Width = largura; await Task.Delay(300);
                await Program.Esperar(navegador, "document.documentElement.scrollWidth<=innerWidth+1");
                if (tabela.ActualHeight < 120) throw new Exception("A busca consumiu a área útil da tabela.");
                await Program.Capturar(navegador, Path.Combine(saida, $"recepcao-filtros-{largura}.png"));
            }
            Console.WriteLine("OK recepção: tela FilaView real, React filtra tabela WPF por nome sem acentos e situação; limpar restaura linhas e ações; 1100/900 px.");
        }
        finally { janela.Close(); vm.AoSairDeCena(); }
    }
    private sealed class Dialogos : IDialogoService
    {
        public bool Confirmar(string titulo, string mensagem) => false;
        public bool ConfirmarPerigo(string titulo, string mensagem) => false;
        public string? PerguntarTexto(string titulo, string pergunta, string? textoInicial = null, bool obrigatorio = true) => null;
        public void Aviso(string titulo, string mensagem) { }
    }
}
