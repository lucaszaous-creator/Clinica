using Clinica.Desktop.Controls;
using Clinica.Desktop.Shell.Modulos;
using Clinica.Desktop.Shell.Web;
using Microsoft.Extensions.DependencyInjection;
using System.IO;
static class FerramentasSuiteQA
{
    public static void Executar(IServiceProvider sp)
    {
        void Check(bool ok,string msg) {if(!ok)throw new Exception(msg);}
        var pasta=Path.GetFullPath(Path.Combine("artifacts","recepcao-web","treinamento-qa",Guid.NewGuid().ToString("N")));
        ItemMenuModulo[] menu=[new(){Chave="agenda",Rotulo="Agenda",Glifo="",Abas=[new("Confirmações de agenda","agenda-confirmacoes")]},new(){Chave="agenda-confirmacoes",Rotulo="Confirmações",Glifo="",Oculto=true},new(){Chave="nao-registrada",Rotulo="Indisponível",Glifo=""}];
        using(var ferramentas=new SuiteFerramentasWeb(sp,menu,["agenda-confirmacoes"],dadosLocais:pasta))
        {
            var busca=ferramentas.Pesquisar("confirmacoes");Check(busca.Count==1&&busca[0].Rota=="agenda-confirmacoes"&&busca[0].Caminho=="Agenda","Busca de aba sem acento não resolveu destino");
            Check(ferramentas.Pesquisar("Indisponível").Count==0,"Busca expôs rota não registrada");
            var snack=sp.GetRequiredService<SnackbarService>();snack.Info("Aviso sintético QA");
            Check(ferramentas.Estado().NaoLidos>0&&ferramentas.Estado().Avisos.First().Mensagem=="Aviso sintético QA","Histórico real ausente");
            ferramentas.MarcarAvisosLidos();Check(ferramentas.Estado().NaoLidos==0&&ferramentas.Estado().Avisos.Count>0,"Ler avisos apagou histórico");
            Check(ferramentas.RotaFilaInfusao() is null,"Atalho de fila vazou rota não disponível");
            var aula=ferramentas.CatalogoAulas().Aulas.Single();Check(aula.Id=="agenda-confirmacoes","Catálogo não respeitou telas do executável");
            ferramentas.SalvarProgresso(aula.Id,10,true);Check(ferramentas.CatalogoAulas(situacao:"concluidas").Aulas.Single().Posicao==10,"Progresso não atualizou filtro");
            ferramentas.ReiniciarAula(aula.Id);Check(ferramentas.CatalogoAulas().Aulas.Single() is {Posicao:0,Concluida:true},"Reiniciar apagou conclusão");
            try{ferramentas.SalvarProgresso("../../indevido",0);throw new Exception("ID externo aceito");}catch(InvalidOperationException){}
            try{ferramentas.SalvarProgresso(aula.Id,double.NaN);throw new Exception("Posição inválida aceita");}catch(InvalidOperationException){}
        }
        using(var reaberto=new SuiteFerramentasWeb(sp,menu,["agenda-confirmacoes"],dadosLocais:pasta))Check(reaberto.CatalogoAulas().Aulas.Single().Concluida,"Progresso não persistiu localmente");
        Console.WriteLine("OK ferramentas reais: busca/aba autorizada, avisos/lidos, fila ausente, catálogo restrito, progresso/reinício/persistência, ID e posição inválidos.");
    }
}
