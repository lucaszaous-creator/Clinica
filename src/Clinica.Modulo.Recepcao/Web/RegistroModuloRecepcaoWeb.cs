using System.Runtime.CompilerServices;
using System.Windows.Threading;
using Clinica.Desktop.Shell.Modulos;
using Clinica.Desktop.Shell.Web;
using Clinica.Domain.Entities;
using Clinica.Recepcao.ViewModels;
using Microsoft.Extensions.DependencyInjection;
namespace Clinica.Recepcao.Web;
public sealed class RegistroModuloRecepcaoWeb : IRegistroModuloWeb, IRegistroSecoesAdministrativasWeb
{
    public RegistroModuloRecepcaoWeb() => PaginasWebController.RegistrarTipoDeContrato<IFichaAdministrativaPaciente>(nameof(IFichaAdministrativaPaciente.DadosWeb),typeof(FichaPacienteViewModel));
    public IEnumerable<PaginasWebController.Secao> Secoes()=>RecepcaoWebRegistro.SecoesAdministrativas();
    public IEnumerable<PaginasWebController.Pagina> Paginas() => RecepcaoWebRegistro.CriarPaginas();
    public IEnumerable<DialogosWebController.RegistroDialogo> Dialogos() => RecepcaoWebRegistro.CriarDialogos();
}
public static partial class RecepcaoWebRegistro
{
    private static readonly ConditionalWeakTable<object, DispatcherTimer> Relogios = new();
    private static readonly ConditionalWeakTable<object, object> PaginasVisitadas = new();
    private static object PrepararPagina(IServiceProvider services, object vm, string chave)
    {
        void AbrirFicha(int id, string nome, int secao = 2)
        {
            SessaoUsuario.Atual.Exigir(Permissao.VerFichaPaciente, "abrir a ficha do paciente");
            services.GetRequiredService<PacienteEmFoco>().Definir(id, nome);
            NavegacaoSuite.Ir(secao switch { 1 => "consultorio-atendimento", 3 => "consultorio-prontuario", _ => "consultorio-paciente" });
        }
        if(vm is NovoAtendimentoViewModel marcar) marcar.FixarModo(true);
        if(vm is PacientesViewModel pacientes) pacientes.AbrirPacienteWeb = (p, secao) => AbrirFicha(p.Id, p.Nome, secao);
        if(vm is FilaViewModel fila) fila.AbrirFichaWeb = (id, nome) => AbrirFicha(id, nome);
        return vm;
    }
    private static async Task AbrirPaginaAsync(object vm)
    {
        var voltando = PaginasVisitadas.TryGetValue(vm, out _);
        if(!voltando) PaginasVisitadas.Add(vm,new object());
        switch(vm)
        {
            case PainelViewModel painel: if(voltando) await painel.CarregarAsync(); painel.AoEntrarEmCena(); break;
            case AgendaViewModel agenda: agenda.AoEntrarEmCena(); break;
            case FilaViewModel fila: fila.AoEntrarEmCena(); break;
            case DocumentosViewModel documentos: if(voltando) await documentos.CarregarAsync(); break;
            case EquipeViewModel equipe: if(voltando) await equipe.CarregarAsync(); break;
            case PagamentosViewModel pagamentos: await pagamentos.CarregarAsync(); break;
            case ConfirmacoesViewModel confirmacoes: if(voltando) await confirmacoes.CarregarAsync(); break; // o construtor já inicia a carga.
            case AcompanhamentoViewModel acompanhamento:
                await acompanhamento.CarregarAsync();
                var relogio = new DispatcherTimer { Interval = TimeSpan.FromSeconds(60) };
                relogio.Tick += async (_, _) => { if(!acompanhamento.Carregando) await acompanhamento.RecarregarAsync(); };
                Relogios.Add(vm, relogio); relogio.Start(); break;
            case NovoAtendimentoViewModel novo when voltando:
                if(!await novo.AplicarPedidoPendenteAsync()) novo.RevalidarPacienteEscolhido(); break;
            case ICarregarAoAbrir carregavel: await carregavel.CarregarAsync(); break;
        }
    }
    private static void FecharPagina(object vm)
    {
        switch(vm) { case PainelViewModel p: p.AoSairDeCena(); break; case AgendaViewModel a: a.AoSairDeCena(); break; case FilaViewModel f: f.AoSairDeCena(); break; }
        if(Relogios.TryGetValue(vm, out var relogio)) { relogio.Stop(); Relogios.Remove(vm); }
    }
}
