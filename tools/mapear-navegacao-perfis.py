"""Executa declarações reais de menu/perfis em harness .NET sem WPF ou banco.

Extrai constantes e Itens dos módulos; usa OrganizacaoNavegacao e PerfisAcesso
originais. Reproduz o filtro/dedupe/composição do ShellViewModel. A matriz é
derivada, não substitui abrir as telas. Perfis padrão, sem concessões individuais.
"""
import argparse, json, re, subprocess, tempfile
from pathlib import Path
from importlib import import_module
clean=import_module('mapear-superficies').clean

def main():
    ap=argparse.ArgumentParser();ap.add_argument('--dotnet',default='dotnet');a=ap.parse_args()
    root=Path(__file__).resolve().parents[1];out=root/'docs/auditoria-global-2026-10-01'
    tmp=Path(tempfile.mkdtemp(prefix='clinica-mapa-menu-'))
    domain=(root/'src/Clinica.Domain/Clinica.Domain.csproj').as_posix()
    (tmp/'Mapa.csproj').write_text(f'<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net8.0</TargetFramework><ImplicitUsings>enable</ImplicitUsings><Nullable>enable</Nullable></PropertyGroup><ItemGroup><ProjectReference Include="{domain}" /></ItemGroup></Project>',encoding='utf-8')
    for name in ('ChavesSuite','OrganizacaoNavegacao'):
        (tmp/f'{name}.cs').write_text((root/f'src/Clinica.Desktop.Shell/Modulos/{name}.cs').read_text(encoding='utf-8-sig'),encoding='utf-8')
    item=clean((root/'src/Clinica.Desktop.Shell/Modulos/ItemMenuModulo.cs').read_text(encoding='utf-8-sig'))
    item=item[:item.index('public sealed partial class GrupoMenuModulo')]
    item=item.replace('using CommunityToolkit.Mvvm.ComponentModel;','').replace(': ObservableObject','').replace('[ObservableProperty]','')
    (tmp/'Item.cs').write_text(item,encoding='utf-8')
    sec=clean((root/'src/Clinica.Modulo.Faturamento/ViewModels/Secao.cs').read_text(encoding='utf-8-sig'))
    sec='namespace Clinica.Desktop.ViewModels;\n'+re.search(r'public enum Secao\s*\{[^}]+\}',sec,re.S)[0]
    (tmp/'Secao.cs').write_text(sec,encoding='utf-8')
    for name in ('Recepcao','Clinico','Financeiro','Gerente','Faturamento'):
        s=clean((root/f'src/Clinica.Modulo.{name}/Modulo/Modulo{name}.cs').read_text(encoding='utf-8-sig'))
        constants='\n'.join(re.findall(r'public const string\s+[^;]+;',s))
        prop=re.search(r'public IReadOnlyList<ItemMenuModulo> Itens \{ get; \} =.*?;',s,re.S)[0]
        dest=re.search(r'private static readonly .*?Destinos =.*?;',s,re.S)[0] if name=='Faturamento' else ''
        (tmp/f'{name}.cs').write_text('using Clinica.Domain.Entities; using Clinica.Desktop.Shell.Modulos; using Clinica.Desktop.ViewModels;\n'+f'public class {name} {{ {constants}\n{dest}\n{prop} }}',encoding='utf-8')
    program=r'''
using Clinica.Domain.Entities;
using Clinica.Desktop.Shell.Modulos;
using System.Text.Json;
var r=new Recepcao().Itens;var c=new Clinico().Itens;var f=new Financeiro().Itens;var g=new Gerente().Itens;var t=new Faturamento().Itens;
ItemMenuModulo Copy(ItemMenuModulo i,bool hidden,IReadOnlyList<AbaMenu>? abas=null,bool? initial=null)=>new(){Chave=i.Chave,Rotulo=i.Rotulo,Glifo=i.Glifo,Icone=i.Icone,Grupo=i.Grupo,Requer=i.Requer,RequerAlgum=i.RequerAlgum,PerfilExclusivo=i.PerfilExclusivo,Oculto=hidden,Inicial=initial??i.Inicial,Abas=abas??i.Abas};
IEnumerable<ItemMenuModulo> Context(IEnumerable<ItemMenuModulo> xs)=>xs.Select(i=>Copy(i,true,initial:false));
var raiz=t.Single(i=>i.Chave==ChavesSuite.FaturamentoTiss);
var ta=raiz.Abas.Concat(t.Where(i=>i.Chave!=raiz.Chave&&!raiz.Abas.Any(a=>a.Chave==i.Chave)).Select(i=>new AbaMenu(i.Rotulo,i.Chave))).ToArray();
var faturamento=t.Append(g.Single(i=>i.Chave=="faturamento-resumo")).Select(i=>Copy(i,i.Chave!=raiz.Chave,i.Chave==raiz.Chave?ta:i.Abas,i.Chave==raiz.Chave)).Append(r.Single(i=>i.Chave==ChavesSuite.RetornoPacientes));
var apps=new Dictionary<string,IEnumerable<ItemMenuModulo>>{{"Recepção",r.Concat(Context(c))},{"Clínico",c.Concat(Context(r))},{"Financeiro",f.Concat(Context(r)).Concat(Context(c))},{"Gerente",r.Concat(c).Concat(f).Concat(t).Concat(g)},{"Faturamento",faturamento}};
var result=new List<object>();
foreach(var (app,raw) in apps) foreach(var perfil in Enum.GetValues<PerfilAcesso>()) {
 var perms=PerfisAcesso.Padrao(perfil);
 var items=raw.Select(OrganizacaoNavegacao.Aplicar).Where(i=>(perms&i.Requer)==i.Requer&&(i.RequerAlgum==Permissao.Nenhuma||(perms&i.RequerAlgum)!=0)&&(i.PerfilExclusivo==null||i.PerfilExclusivo==perfil)).DistinctBy(i=>i.Chave).ToList();
 bool Suppress(ItemMenuModulo i)=>i.Chave=="consultorio-agenda"&&items.Any(x=>x.Chave=="agenda"&&!x.Oculto);
 List<AbaMenu> Abas(ItemMenuModulo i)=>i.Abas.Where(a=>items.Any(x=>x.Chave==a.Chave)).ToList();
 var parents=items.Where(i=>!i.Oculto&&!Suppress(i)&&i.Abas.Count>0&&Abas(i).Count>0).ToList();
 var claimed=parents.SelectMany(i=>i.Abas).Select(a=>a.Chave).ToHashSet();
 var visible=items.Where(i=>!i.Oculto&&!Suppress(i)&&!claimed.Contains(i.Chave)&&(i.Abas.Count==0||Abas(i).Count>0)).OrderBy(i=>i.Grupo).ThenByDescending(i=>i.Inicial).ToList();
 result.Add(new {aplicativo=app,perfil=perfil.ToString(),permissoes=perms.ToString(),inicio=(visible.FirstOrDefault(i=>i.Inicial)??visible.FirstOrDefault())?.Chave,menus=visible.Select(i=>new{i.Chave,i.Rotulo,grupo=i.Grupo.ToString(),abas=Abas(i)}),destinos=items.Select(i=>new{i.Chave,i.Rotulo,i.Oculto,requer=i.Requer.ToString(),requerAlgum=i.RequerAlgum.ToString()}),abasIndisponiveis=parents.SelectMany(i=>i.Abas.Where(a=>!items.Any(x=>x.Chave==a.Chave)).Select(a=>new{pai=i.Chave,alvo=a.Chave})),paisRepetidos=parents.SelectMany(i=>Abas(i).Select(a=>new{pai=i.Chave,alvo=a.Chave})).GroupBy(x=>x.alvo).Where(x=>x.Count()>1).Select(x=>new{alvo=x.Key,pais=x.Select(y=>y.pai)})});
}
File.WriteAllText(args[0],JsonSerializer.Serialize(result,new JsonSerializerOptions{WriteIndented=true}));
Console.WriteLine($"{result.Count} combinações aplicativo/perfil calculadas.");
'''
    (tmp/'Program.cs').write_text(program,encoding='utf-8')
    subprocess.run([a.dotnet,'run','--project',str(tmp/'Mapa.csproj'),'--',str(out/'navegacao-perfis.json')],check=True)
    data=json.loads((out/'navegacao-perfis.json').read_text())
    lines=['# Navegação por aplicativo e perfil','', 'Matriz derivada das declarações C# e do filtro do shell. Perfis padrão; concessões/revogações individuais alteram o resultado. Treinamento é acrescentado pelo shell e não integra as contagens abaixo. Não abre janelas nem consulta banco.','']
    for d in data:
        lines += [f'## {d["aplicativo"]} · {d["perfil"]}', '',f'Abertura: `{d["inicio"]}`. {len(d["menus"])} entradas visíveis; {len(d["destinos"])} destinos autorizados.','', '| Grupo | Entrada | Chave | Abas disponíveis |','| --- | --- | --- | --- |']
        for i in d['menus']:lines.append(f'| {i["grupo"]} | {i["Rotulo"]} | {i["Chave"]} | '+ ' · '.join(f'{x["Rotulo"]} (`{x["Chave"]}`)' for x in i['abas'])+' |')
        lines.append('')
    (out/'NAVEGACAO-POR-PERFIL.md').write_text('\n'.join(lines),encoding='utf-8')

if __name__=='__main__':main()
