using System.Windows;
using Clinica.Desktop.Shell.Modulos;
using Microsoft.Extensions.DependencyInjection;
using Clinica.Domain.Entities;

namespace Clinica.Desktop.Shell.Web;

public sealed class SuiteWebWindow : Window
{
    public SuiteWebView WebView { get; }
    public SuiteWebWindow(IServiceProvider servicos,IReadOnlyList<IModuloApp> modulos,string titulo)
    {
        Title=titulo+(Configuracao.EdicaoDeTeste.Ativa?" — teste PR 245":"");Width=1300;Height=700;MinWidth=880;MinHeight=550;WindowStartupLocation=WindowStartupLocation.CenterScreen;
        var registros=servicos.GetServices<IRegistroModuloWeb>().ToArray();
        var itens=modulos.SelectMany(m=>m.Itens).Select(OrganizacaoNavegacao.Aplicar).GroupBy(i=>i.Chave).Select(g=>g.First()).ToArray();
        var paginas=registros.SelectMany(r=>r.Paginas()).Concat(RegistroCompartilhadoWeb.Paginas()).Concat(PaginasPacotesCompartilhados.CriarPaginas()).GroupBy(p=>p.Chave).Select(g=>g.First()).Select(p=>
        {
            var item=itens.FirstOrDefault(i=>i.Chave==p.Chave);
            if(item is null)return p;
            var original=p.Autorizado;
            return p with { Permissao=item.Requer, Autorizado=()=>
                (original?.Invoke()??true) && (item.RequerAlgum==Permissao.Nenhuma||SessaoUsuario.Atual.PodeAlgum(item.RequerAlgum))
                && (item.PerfilExclusivo is null||SessaoUsuario.Atual.Perfil==item.PerfilExclusivo) };
        }).ToArray();
        var dialogos=RegistroCompartilhadoWeb.Dialogos().Concat(registros.SelectMany(r=>r.Dialogos())).GroupBy(d=>(d.Chave,d.Tipo)).Select(g=>g.First()).ToArray();
        var inicial=itens.LastOrDefault(i=>i.Inicial && i.Abas.Count>0)
            ??itens.FirstOrDefault(i=>i.Inicial);
        var inicialChave=inicial?.Abas.FirstOrDefault()?.Chave??inicial?.Chave;
        WebView=new(servicos,paginas,dialogos,itens,titulo,inicialChave);
        Content=WebView;
        Closing+=(_,e)=>{if(!WebView.PodeFechar){e.Cancel=true;WebView.AvisarOperacaoEmAndamento();}};
        Closed+=(_,_)=>WebView.Dispose();
    }
    public static SuiteWebWindow Criar(IServiceProvider servicos,IReadOnlyList<IModuloApp> modulos,string titulo)=>new(servicos,modulos,titulo);
}
