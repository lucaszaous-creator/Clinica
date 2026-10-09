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
            if (view.FindName("TabelaAgenda") is not null) throw new Exception("A recepção manteve tabela paralela à lista React.");
            await Program.Script(navegador, "(()=>{let e=document.querySelector('input');e.focus();Object.getOwnPropertyDescriptor(HTMLInputElement.prototype,'value').set.call(e,'JOAO');e.dispatchEvent(new Event('input',{bubbles:true}));})()");
            await Program.Esperar(navegador, "document.querySelector('.resumo-agenda').textContent.includes('2 de 3')");
            await Task.Delay(200);
            await Program.Esperar(navegador, "document.querySelectorAll('.linha-agenda').length===2");
            await Program.Script(navegador, "document.querySelector('.filtro-situacao.cancelado').click()");
            await Program.Esperar(navegador, "document.querySelector('.resumo-agenda').textContent.includes('1 de 3')");
            await Task.Delay(200);
            await Program.Esperar(navegador, "document.querySelectorAll('.linha-agenda').length===1 && document.querySelector('.linha-agenda h2').textContent==='João Souza'");
            await Program.Script(navegador, "document.querySelector('.filtros-agenda .secundario').click()");
            await Program.Esperar(navegador, "document.querySelector('.resumo-agenda').textContent.includes('3 de 3')");
            await Task.Delay(200);
            await Program.Esperar(navegador, "document.querySelectorAll('.linha-agenda').length===3 && document.querySelector('[aria-label^=\"Chegou:\"]')!==null && document.querySelector('[aria-label^=\"Editar:\"]')!==null && document.querySelector('[aria-label^=\"Ficha:\"]')!==null");
            await Program.Script(navegador, "document.querySelector('[aria-label=\"Mais ações: João da Silva\"]').click()");
            var prazoMenu = DateTime.UtcNow.AddSeconds(5);
            while (view.ContextMenu?.IsOpen != true && DateTime.UtcNow < prazoMenu) await Task.Delay(50);
            if (view.ContextMenu?.IsOpen != true || !view.ContextMenu.Items.OfType<MenuItem>().Any(m => ReferenceEquals(m.Command, vm.CancelarCommand))
                || !view.ContextMenu.Items.OfType<MenuItem>().Any(m => ReferenceEquals(m.Command, vm.AbrirFichaCommand)))
                throw new Exception("As ações da lista React não abriram o menu original autorizado da recepção.");
            view.ContextMenu.IsOpen = false;
            foreach (var largura in new[] { 1100, 900 })
            {
                janela.Width = largura; await Task.Delay(300);
                await Program.Esperar(navegador, "document.documentElement.scrollWidth<=innerWidth+1");
                await Program.Esperar(navegador, "document.querySelector('.lista-agenda').getBoundingClientRect().height>100");
                await Program.ConferirAlinhamento(navegador);
                await Program.Capturar(navegador, Path.Combine(saida, $"recepcao-filtros-{largura}.png"));
            }
            Console.WriteLine("OK recepção: tela FilaView real, lista React única filtra nome sem acentos e situação; limpar restaura linhas e ações; menu nativo preservado; 1100/900 px.");
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
