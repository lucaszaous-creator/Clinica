import json,re,xml.etree.ElementTree as E
from pathlib import Path
base=Path('src/Clinica.Modulo.Clinico')
meta={t['nome']:{p['nome']:p for p in t['propriedades']} for t in json.load(open('tests/Clinica.Clinico.Web.Qa/tipos.json',encoding='utf-8'))}
inventory=json.load(open(r'C:/Users/ROYA/Documents/Clinica/outputs/refatoracao-design-20261008/clinico.json',encoding='utf-8'))
labels={}
for screen in inventory['screens']:
 for g in screen['groups']:
  for f in g['fields']+g['actions']:
   b=re.search(r'\{Binding\s+(?:DataContext\.)?([\w.]+)',f.get('binding',f.get('command','')))
   if b and not f.get('fallbackLabel') and not f['label'].startswith('{'):labels[(Path(screen['source']).name,b[1])]=f['label']
def q(v):return json.dumps(v,ensure_ascii=False)
def binding(s):
 m=re.search(r'\{Binding\s+(?:Path=)?([\w.]+)',s or '')
 return m[1] if m else None
def prop(t,path):
 if not path:return None
 for seg in path.split('.'):
  p=meta.get(t,{}).get(seg)
  if not p:return None
  t=p['tipo']
 return p
def label(name):return re.sub(r'(?<=[a-zà-ú])(?=[A-Z])',' ',name.split('.')[-1])
issues=[];output=[]
def generate(view,typ,method):
 path=base/'Views'/(view+'.xaml')
 if not path.exists():path=Path('src/Clinica.Desktop.Shell/Componentes')/(view+'.xaml')
 root=E.parse(path).getroot();fields={};reads={};actions={};tabs={}
 def walk(e,context=typ,table=None,pre=''):
  tag=e.tag.split('}')[-1]
  if '.Resources' in tag:return
  dc=binding(e.get('DataContext'))
  if dc and prop(context,dc):context=prop(context,dc)['tipo'];pre+=dc+'.'
  source=binding(e.get('ItemsSource'))
  if tag=='ListBox' and source and prop(context,source):
   selected=binding(e.get('SelectedItem'))
   if selected and prop(context,selected):
    fields[pre+selected]=f'new({q(pre+selected)}, {q(label(selected))}, "selecao", Opcoes: {q(pre+source)}, RotuloOpcao: {q(e.get("DisplayMemberPath") or "Nome")})'
  shared=Path('src/Clinica.Desktop.Shell/Componentes')/(tag+'.xaml')
  if 'clr-namespace:' in e.tag and shared.exists() and shared!=path:
   walk(E.parse(shared).getroot(),context,table,pre)
   return
  if tag in ['ItemsControl','DataGrid','ListBox'] and source and prop(context,source):
   pt=prop(context,source);row=pt['colecao']
   if row and row in meta:
    key=pre+source
    if table:issues.append(f'{view}: coleção aninhada {table}/{key}')
    else:
     tabs.setdefault(key,dict(tipo=row,dono=context,prefixo=pre,columns={},fields={},actions={}))
     for c in e:walk(c,row,key,'')
     template=e.get('ItemTemplate','')
     tm=re.match(r'{StaticResource ([^}]+)}',template)
     if tm:
      for resource in root.iter():
       if resource.get('{http://schemas.microsoft.com/winfx/2006/xaml}Key')==tm[1]:walk(resource,row,key,'')
     if not tabs[key]['columns']:
      for name in ['Rotulo','Nome','Texto','Detalhes','Quantidade','Titulo','Data','Valor','Resumo','Situacao']:
       pd=prop(row,name)
       if pd and (pd['tipo']=='System.String' or not pd['colecao']):tabs[key]['columns'][name]=label(name)
     return
  store=fields if table is None else tabs[table]['fields'];col=reads if table is None else tabs[table]['columns'];act=actions if table is None else tabs[table]['actions']
  fieldkey=next((binding(e.get(k)) for k in ['SelectedItem','SelectedDate','IsChecked','Texto','Text','SelectedValue'] if binding(e.get(k))),None)
  if tag in ['TextBox','ComboBox','DatePicker','CheckBox','EditorTextoClinico','BuscaMedicamento'] and fieldkey:
   pd=prop(context,fieldkey)
   if pd and pd['editavel'] and e.get('IsReadOnly')!='True' and e.get('SomenteLeitura')!='True' and 'OneWay' not in (e.get('Text','')+e.get('Texto','')) and 'TextoSelecionavel' not in e.get('Style',''):
    name=pre+fieldkey;rot=labels.get((path.name,fieldkey),e.get('AutomationProperties.Name') or label(fieldkey));kind={'ComboBox':'selecao','DatePicker':'data','CheckBox':'booleano','EditorTextoClinico':'texto-rico','BuscaMedicamento':'sugestao'}.get(tag,'textarea' if e.get('AcceptsReturn')=='True' else 'texto')
    opt=binding(e.get('ItemsSource') or e.get('Catalogo'));optroot=opt and opt.startswith('DataContext.')
    if optroot: opt='vm:'+opt.removeprefix('DataContext.')
    elif opt:opt=pre+opt
    fixas=[c.get('Content') for c in e if c.tag.split('}')[-1]=='ComboBoxItem' and c.get('Content')]
    fmt=binding(e.get('Formato'));fmt=pre+fmt if fmt else None
    guard=binding(e.get('IsEnabled'));guard=guard.removeprefix('DataContext.') if guard else None
    if guard and not prop(typ if table is None or optroot else context,guard):guard=None
    vis=binding(e.get('Visibility'))
    if vis and (not prop(context,vis) or prop(context,vis)['tipo']!='System.Boolean'):vis=None
    extra=(f', Opcoes: {q(opt)}' if opt else '')+(f', RotuloOpcao: {q(e.get("DisplayMemberPath"))}' if e.get('DisplayMemberPath') else '')+(f', ValorOpcao: {q(e.get("SelectedValuePath"))}' if e.get('SelectedValuePath') else '')+(f', Guarda: {q(guard)}' if guard else '')+(f', Visivel: {q(pre+vis)}' if vis else '')+(f', Formato: {q(fmt)}' if fmt else '')
    if fixas: extra+=', Fixas: ['+', '.join(q(f) for f in fixas)+']'
    store[name+(vis or '')]=f'new({q(name)}, {q(rot)}, {q(kind)}{extra})'
   elif pd:col[pre+fieldkey]=label(fieldkey)
   else:issues.append(f'{view}: campo {context}.{fieldkey}')
  if tag in ['TextBlock','Run','DataGridTextColumn','DataGridCheckBoxColumn']:
   name=binding(e.get('Text') or e.get('Binding'))
   if name and prop(context,name) and (not prop(context,name)['colecao'] or prop(context,name)['tipo']=='System.String') and not name.startswith('DataContext.'):
    col[pre+name]=e.get('Header') or label(name)
  cmd=binding(e.get('Command'))
  if cmd:
   rootcmd=cmd.removeprefix('DataContext.')
   cmdtype=(tabs[table]['dono'] if table else typ) if cmd.startswith('DataContext.') else context
   if prop(cmdtype,rootcmd):
    if not rootcmd.endswith('Command'):issues.append(f'{view}: comando {rootcmd}')
    else:
     full=((tabs[table]['prefixo'] if table else '') if cmd.startswith('DataContext.') else pre)+rootcmd[:-7]
     text=e.get('Content') or e.get('Texto') or labels.get((path.name,rootcmd))
     if not text or text.startswith('{') or any(0xE000<=ord(c)<=0xF8FF for c in text):text=label(rootcmd[:-7])
     guard=binding(e.get('IsEnabled'));guard=guard.removeprefix('DataContext.') if guard else None
     if guard and not prop(cmdtype,guard):guard=None
     if guard and table:guard='vm:'+tabs[table]['prefixo']+guard
     parameter=e.get('CommandParameter')
     extras=(f', Guarda: {q(guard)}' if guard else '')
     if parameter and not parameter.startswith('{'):extras+=f', Parametro: {q(parameter)}'
     if table and not parameter:extras+=', SemParametro: true'
     act[full+(parameter or '')]=f'new({q(full)}, {q(text)}{extras})'
   else:issues.append(f'{view}: ação {cmdtype}.{rootcmd}')
  for c in e:walk(c,context,table,pre)
 walk(root)
 # Avoid duplicated value fields in readouts; retain named state guidance.
 for k in fields:reads.pop(k,None)
 reads.pop('Mensagem',None)
 lines=[f'    private static P.Secao {method}() => new({q(method)}, {q(label(view.removesuffix("View")))}, null,', '        ['+', '.join(fields.values())+'],','        ['+', '.join(q(k+'|'+v) for k,v in reads.items())+'],','        [']
 for k,t in tabs.items():
  for f in t['fields']:t['columns'].pop(f,None)
  columns=', '.join(f'new({q(a)}, {q(b)})' for a,b in t['columns'].items())
  lines.append(f'            new({q(k)}, {q(label(k))}, [{q(k)}], vm => (({typ})vm).{k}.Cast<object>(), [{columns}], [{", ".join(t["fields"].values())}], [{", ".join(t["actions"].values())}]),')
 lines+=['        ], ['+', '.join(actions.values())+']);']
 output.append('\n'.join(lines))
for view,typ,method in [
 ('MeuDiaView','MeuDiaViewModel','MeuDia'),('RegistrosPendentesView','RegistrosPendentesViewModel','RegistrosPendentes'),('MinhaSemanaView','MinhaSemanaViewModel','MinhaSemana'),('MeusNumerosView','MeusNumerosViewModel','MeusNumeros'),('ProntuariosView','ProntuariosViewModel','Prontuarios'),('ExamesView','ExamesViewModel','Exames'),('PrescricoesClinicasView','PrescricoesClinicasViewModel','Prescricoes'),('PrescricaoInfusaoView','PrescricaoInfusaoViewModel','Infusoes'),('SessoesEnfermagemView','SessoesEnfermagemViewModel','SessoesEnfermagem'),
 ('AtendimentoView','AtendimentoViewModel','Atendimento'),('AtendimentoEnfermagemView','AtendimentoEnfermagemViewModel','Enfermagem'),('PacienteCapaView','PacienteCapaViewModel','Capa'),('ProntuarioClinicoView','ProntuarioClinicoViewModel','Prontuario'),('AnexosPacienteView','AnexosPacienteViewModel','Anexos'),('AnamneseView','AnamneseViewModel','Anamnese'),('EvolucaoDorView','EvolucaoDorViewModel','Dor'),('MedidasView','MedidasViewModel','Medidas'),('AvaliacoesView','AvaliacoesViewModel','Avaliacoes'),('HistoricoConsultaView','HistoricoConsultaViewModel','HistoricoConsulta'),('EmissoesNoAtendimentoView','AtendimentoViewModel','Emissoes')]:
 generate(view,'Clinica.Clinico.ViewModels.'+typ,method)
(base/'Web/ClinicoWebRegistro.Secoes.cs').write_text('''using System.Linq;
using P = Clinica.Desktop.Shell.Web.PaginasWebController;
namespace Clinica.Clinico.Web;
/// <summary>Campos e comandos explicitamente associados às mesmas propriedades das telas clínicas.</summary>
public static partial class ClinicoWebRegistro
{
'''+ '\n\n'.join(output)+'\n}\n',encoding='utf-8')
Path('tests/Clinica.Clinico.Web.Qa/extracao-pendencias.txt').write_text('\n'.join(issues),encoding='utf-8')
print('Seções:',len(output),'pendências de componente:',len(issues))
