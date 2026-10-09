using System.Collections;
using System.Reflection;
using System.Text.Json;
using System.IO;
using Clinica.Desktop.Shell.Web;
using Clinica.Recepcao.Web;
using Clinica.Gerente.Web;
using Clinica.Faturamento.Web;
using Clinica.Financeiro.Web;
using Clinica.Clinico.Web;
using Clinica.Desktop.Shell.Modulos;
using Microsoft.Extensions.DependencyInjection;
var servicos=new ServiceCollection();
IModuloApp[] modulos=[new Clinica.Recepcao.Modulo.ModuloRecepcao(),new Clinica.Clinico.Modulo.ModuloClinico(),new Clinica.Financeiro.Modulo.ModuloFinanceiro(),new Clinica.Faturamento.Modulo.ModuloFaturamento(),new Clinica.Gerente.Modulo.ModuloGerente()];
foreach(var modulo in modulos)modulo.Registrar(servicos);
using var sp=servicos.BuildServiceProvider();var providers=sp.GetServices<IRegistroModuloWeb>().ToArray();
var paginas=providers.SelectMany(r=>r.Paginas()).Concat(RegistroCompartilhadoWeb.Paginas()).Concat(PaginasPacotesCompartilhados.CriarPaginas()).GroupBy(p=>p.Chave).Select(g=>g.First()).ToArray();
var dialogos=RegistroCompartilhadoWeb.Dialogos().Concat(providers.SelectMany(r=>r.Dialogos())).GroupBy(d=>(d.Chave,d.Tipo)).Select(g=>g.First()).ToArray();
object? Simples(object? valor)
{
    if(valor is null || valor is Delegate) return null;
    if(valor is Type t) return t.FullName;
    if(valor is string || valor.GetType().IsPrimitive || valor is decimal) return valor;
    if(valor is Enum) return valor.ToString();
    if(valor is IEnumerable lista) return lista.Cast<object?>().Select(Simples).ToArray();
    return valor.GetType().GetProperties(BindingFlags.Public|BindingFlags.Instance).Where(p=>p.GetIndexParameters().Length==0).ToDictionary(p=>p.Name,p=>Simples(p.GetValue(valor)));
}
Directory.CreateDirectory("artifacts/recepcao-cobertura");
File.WriteAllText("artifacts/recepcao-cobertura/registros.json",JsonSerializer.Serialize(new{Paginas=Simples(paginas),Dialogos=Simples(dialogos)},new JsonSerializerOptions{WriteIndented=true}));
var entradas=new List<object>();
void Entrada(Type tipo,string? caminho,string categoria,string rota)
{
    if(string.IsNullOrWhiteSpace(caminho))return;
    caminho=caminho.Replace("vm:","");
    entradas.Add(new{Tipo=tipo.FullName,Caminho=caminho,Categoria=categoria,Rota=rota});
    var partes=caminho.Split('.');
    for(var i=0;i<partes.Length-1;i++)
    {
        var prop=tipo.GetProperty(partes[i]);if(prop is null)break;
        tipo=prop.PropertyType;
        if(partes[i]=="DadosWeb"&&tipo==typeof(object))tipo=typeof(Clinica.Recepcao.ViewModels.FichaPacienteViewModel);
        entradas.Add(new{Tipo=tipo.FullName,Caminho=string.Join('.',partes.Skip(i+1)),Categoria=categoria,Rota=rota});
    }
}
Type Linha(Type tipo,string caminho)
{
    foreach(var parte in caminho.Split('.'))tipo=tipo.GetProperty(parte)?.PropertyType??typeof(object);
    return tipo.GetInterfaces().Append(tipo).FirstOrDefault(t=>t.IsGenericType&&t.GetGenericTypeDefinition()==typeof(IEnumerable<>))?.GenericTypeArguments[0]??typeof(object);
}
void CampoP(Type tipo,PaginasWebController.Campo c,string rota) => Entrada(tipo,c.Propriedade,"campo",rota);
void AcaoP(Type tipo,PaginasWebController.Acao a,string rota)=>Entrada(tipo,a.Comando+"Command","acao",rota);
foreach(var p in paginas)
{
    foreach(var c in p.Campos)CampoP(p.Tipo,c,p.Chave);
    foreach(var a in p.Acoes)AcaoP(p.Tipo,a,p.Chave);
    foreach(var s in p.Secoes){foreach(var c in s.Campos)CampoP(p.Tipo,c,p.Chave);foreach(var a in s.Acoes)AcaoP(p.Tipo,a,p.Chave);foreach(var t in s.Tabelas){var row=t.TipoLinha??Linha(p.Tipo,t.Origens.First());foreach(var c in t.Campos)CampoP(row,c,p.Chave);foreach(var a in t.Acoes)AcaoP(a.AlvoLinha?row:p.Tipo,a,p.Chave);foreach(var c in t.Colunas)Entrada(row,c.Propriedade,"coluna",p.Chave);}}
}
foreach(var d in dialogos)
{
    foreach(var c in d.Definicao.Campos)Entrada(d.Tipo,c.Caminho,"campo",d.Chave);
    foreach(var a in d.Definicao.Acoes)Entrada(d.Tipo,a.Comando,"acao",d.Chave);
    foreach(var t in d.Definicao.Tabelas){var row=Linha(d.Tipo,t.Colecao);foreach(var c in t.Campos??[])Entrada(row,c.Caminho,"campo",d.Chave);foreach(var a in t.Acoes)Entrada(d.Tipo,a.Comando,"acao",d.Chave);foreach(var c in t.Colunas)Entrada(row,c.Caminho,"coluna",d.Chave);}
}
File.WriteAllText("artifacts/recepcao-cobertura/vinculos.json",JsonSerializer.Serialize(entradas,new JsonSerializerOptions{WriteIndented=true}));
Console.WriteLine($"Inventário real: {paginas.Length} páginas e {dialogos.Length} registros de diálogos.");
