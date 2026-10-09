using Clinica.Desktop.Shell.Web;
using Clinica.Desktop.Shell.Modulos;
using Microsoft.Extensions.DependencyInjection;

internal static class Program
{
    internal static IModuloApp[] Modulos()=>[new Clinica.Recepcao.Modulo.ModuloRecepcao(),new Clinica.Clinico.Modulo.ModuloClinico(),new Clinica.Financeiro.Modulo.ModuloFinanceiro(),new Clinica.Faturamento.Modulo.ModuloFaturamento(),new Clinica.Gerente.Modulo.ModuloGerente()];
    [STAThread] static void Main(string[] args)
    {
        var app=new System.Windows.Application{ShutdownMode=System.Windows.ShutdownMode.OnExplicitShutdown};
        app.Startup+=async(_,_)=>{try {Contratos();if(args.Contains("--acoes-only"))await AcoesMenuQa.Executar();if(args.Contains("--react-only"))await ReactReconciliacaoQa.Executar();if(args.Contains("--rolagem-only"))await RolagemQa.Executar();if(args.Contains("--buscas-gerente"))await GerenteFluxos.Executar(somenteBuscas:true);if(args.Contains("--web"))await GerenteFluxos.Executar();if(args.Contains("--contraste"))await GerenteFluxos.Executar(somenteContraste:true);}catch(Exception ex){Console.WriteLine(ex);Environment.ExitCode=1;}finally{app.Shutdown();}};
        app.Run();
    }
    private static void Contratos()
    {
        var servicos=new ServiceCollection();var modulos=Modulos();foreach(var m in modulos)m.Registrar(servicos);
        using var sp=servicos.BuildServiceProvider();var registros=sp.GetServices<IRegistroModuloWeb>().ToArray();
        var paginas=registros.SelectMany(r=>r.Paginas()).Concat(RegistroCompartilhadoWeb.Paginas()).Concat(PaginasPacotesCompartilhados.CriarPaginas()).GroupBy(p=>p.Chave).Select(g=>g.First()).ToArray();
        var dialogos=RegistroCompartilhadoWeb.Dialogos().Concat(registros.SelectMany(r=>r.Dialogos())).GroupBy(d=>(d.Chave,d.Tipo)).Select(g=>g.First()).ToArray();
        MapaAcoesQa.Gravar(paginas,dialogos);
        var erros=PaginasWebController.ValidarRegistro(paginas).Concat(DialogosWebController.ValidarRegistro(dialogos)).ToList();
        foreach(var item in modulos.SelectMany(m=>m.Itens).Where(i=>i.Abas.Count==0).DistinctBy(i=>i.Chave))
            if(!paginas.Any(p=>p.Chave==item.Chave))erros.Add("Destino sem página: "+item.Chave);
        foreach(var erro in erros)Console.WriteLine(erro);
        Console.WriteLine($"{paginas.Length} páginas e {dialogos.Length} formulários. Contratos: {erros.Count} divergências.");
        if(erros.Count>0)throw new Exception("Contratos incompletos.");
    }
}
