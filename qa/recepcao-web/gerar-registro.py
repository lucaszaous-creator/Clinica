from pathlib import Path
import xml.etree.ElementTree as E,re,json
B=Path('src/Clinica.Modulo.Recepcao/Views')
def q(v):return 'null' if v is None else json.dumps(v,ensure_ascii=False)
def bind(v):
 m=re.match(r'\{Binding(?:\s+(?:Path=)?([\w.]+))?(?:[, }])',v or '')
 return m.group(1) or '.' if m else None
def pretty(v):return re.sub(r'(?<=[a-záéíóúãõç])(?=[A-Z])',' ',v.split('.')[-1])
def attrs(n):return {k.split('}')[-1]:v for k,v in n.attrib.items()}
def tag(n):return n.tag.split('}')[-1]
def extract(name):
 r=E.parse(B/(name+'View.xaml')).getroot();pa={c:n for n in r.iter() for c in n};coll={}; root={'fields':{},'actions':{},'cols':{},'vis':None}
 for n in r.iter():
  a=attrs(n);t=tag(n)
  if t in ['ItemsControl','DataGrid','ListBox','ListView'] and bind(a.get('ItemsSource')):coll[n]={'path':bind(a['ItemsSource']),'fields':{},'actions':{},'cols':{},'vis':None}
 def context(n):
  x=pa.get(n)
  while x is not None:
   if x in coll:return coll[x]
   x=pa.get(x)
  return root
 def visibility(n,prop):
  parts=[];x=n
  while x is not None:
   if x is not n and x in coll:break
   b=bind(attrs(x).get(prop));
   if b and b!='.' and not b.startswith('DataContext.') and 'TextoPara' not in attrs(x).get(prop,''):parts.append(b)
   x=pa.get(x)
  return '&'.join(dict.fromkeys(parts)) or None
 def label(n,fallback):
  a=attrs(n)
  for k in ['AutomationProperties.Name','Content','Header','Ajudantes.Placeholder','ToolTip']:
   v=a.get(k)
   if v and not v.startswith('{') and not any(0xE000<=ord(c)<=0xF8FF for c in v):return v
  parent=pa.get(n)
  if parent is not None:
   for prev in parent:
    if prev is n:break
    aa=attrs(prev);s=aa.get('Text') or aa.get('Content')
    if tag(prev) in ['TextBlock','Label'] and s and not s.startswith('{'):return s
  return pretty(fallback)
 for n in r.iter():
  ancestors=[]; x=pa.get(n)
  while x is not None: ancestors.append(tag(x)); x=pa.get(x)
  if 'UserControl.Resources' in ancestors: continue
  a=attrs(n);t=tag(n);c=context(n)
  if t in ['TextBox','CheckBox','DatePicker','Calendar','ComboBox']:
   p=next((bind(a.get(k)) for k in ['Text','IsChecked','SelectedDate','SelectedItem','SelectedValue','SelectedIndex'] if bind(a.get(k))),None)
   if not p or p.startswith('DataContext.'):continue
   typ='selecao' if t=='ComboBox' else 'data' if t in ['DatePicker','Calendar'] else 'booleano' if t=='CheckBox' else 'textarea' if a.get('AcceptsReturn')=='True' else 'texto'
   if a.get('IsReadOnly')=='True':typ='leitura'
   if name=='Pacientes' and p=='Filtro':p='FiltroEscolhido'
   op=bind(a.get('ItemsSource'));vl=a.get('SelectedValuePath');rl=a.get('DisplayMemberPath')
   c['fields'][p]=f'new P.Campo({q(p)}, {q(label(n,p))}, {q(typ)}, Opcoes: {q(op)}, RotuloOpcao: {q(rl)}, ValorOpcao: {q(vl)}, Guarda: {q(visibility(n,"IsEnabled"))}, Visivel: {q(visibility(n,"Visibility"))})'
  elif t in ['Button','MenuItem','ToggleButton']:
   cmd=bind(a.get('Command'))
   if not cmd:continue
   cmd=cmd.removeprefix('DataContext.').removesuffix('Command');param=a.get('CommandParameter');fixed=param if param and not param.startswith('{') else None
   if (cmd,fixed) in c['actions']: continue
   c['actions'][(cmd,fixed)]=f'new P.Acao({q(cmd)}, {q(label(n,cmd))}, Guarda: {q(visibility(n,"IsEnabled"))}, SemParametro: {str(c is not root and param is None).lower()}, Visivel: {q(visibility(n,"Visibility"))}, Parametro: {q(fixed)})'
  elif t in ['TextBlock','Run','DataGridTextColumn','DataGridCheckBoxColumn']:
   p=bind(a.get('Text') or a.get('Binding'))
   if not p or p.startswith('DataContext.') or p.endswith('Count') or 'RelativeSource=' in a.get('Text',''):continue
   x=n; contexts=[]
   while x is not None and x not in coll:
    ctx=bind(attrs(x).get('DataContext'))
    if ctx:contexts.insert(0,ctx)
    x=pa.get(x)
   if contexts:p='.'.join(contexts+[p])
   if c is not root:c['cols'][p]=f'new P.Coluna({q(p)}, {q(label(n,p))})'
   elif p not in ['Mensagem','Titulo']:c['fields'].setdefault(p,f'new P.Campo({q(p)}, {q(label(n,p))}, "leitura", Visivel: {q(visibility(n,"Visibility"))})')
  elif t=='BuscaDePacienteView':
   pref=bind(a.get('DataContext')) or 'Seletor'
   root['fields'][pref+'.Termo']=f'new P.Campo({q(pref+".Termo")}, "Buscar paciente por nome ou CPF")'
   root['fields'][pref+'.Selecionado']=f'new P.Campo({q(pref+".Selecionado")}, "Paciente", "selecao", {q(pref+".Resultados")}, "Nome")'
   root['actions'][(pref+'.DesligarSugestao',None)]=f'new P.Acao({q(pref+".DesligarSugestao")}, "Ver todos")'
 # list selectors preserve real selection behavior
 for n,c in coll.items():
  a=attrs(n);selected=bind(a.get('SelectedItem'))
  if selected: root['fields'][selected]=f'new P.Campo({q(selected)}, "Paciente", "selecao", {q(c["path"])}, "Nome")'
 tables=[]
 for n,c in coll.items():
  if context(n) is not root:continue
  c['vis']=visibility(n,'Visibility');tables.append(c)
 return root,tables
mapping=[('Painel','painel-recepcao','Início','VerAgenda'),('Agenda','agenda-recepcao','Agenda','VerAgenda'),('Fila','fila','Agenda do dia','VerAgenda'),('Pacientes','pacientes-recepcao','Pacientes','VerFichaPaciente'),('NovoAtendimento','marcar-horario','Marcar horário','EditarAgenda'),('Consultas','consultas','Consultas de convênio','VerFichaPaciente'),('Lancamentos','lancamentos','Lançamentos','LancarAtendimento'),('RetornosAMarcar','retornos-a-marcar','Retornos a marcar','EditarAgenda'),('Acompanhamento','retorno-pacientes','Acompanhamento de pacientes','VerFichaPaciente'),('Documentos','documentos','Documentos','VerDocumentos'),('Equipe','equipe','Profissionais e salas','GerenciarEquipe'),('Pagamentos','pagamentos-recepcao','Pagamentos','VenderPacote'),('Confirmacoes','agenda-confirmacoes','Confirmações de agenda','VerAgenda')]
lines=['using System.Collections;','using Clinica.Desktop.Shell.Web;','using Clinica.Domain.Entities;','using Clinica.Recepcao.ViewModels;','using Microsoft.Extensions.DependencyInjection;','using P = Clinica.Desktop.Shell.Web.PaginasWebController;','namespace Clinica.Recepcao.Web;','public static partial class RecepcaoWebRegistro','{','    private static IEnumerable<P.Pagina> CriarPaginasBase()','    {']
for name,key,title,perm in mapping:
 root,tables=extract(name)
 lines += [f'        // Contrato de {name}View.xaml; campos e comandos são constantes de apresentação.',f'        yield return new({q(key)}, {q(title)}, typeof({name}ViewModel),', '            ['+',\n             '.join(root['fields'].values())+'], [],', '            [']
 for t in tables:
  lines += [f'                new P.Secao({q(t["path"])}, {q(pretty(t["path"]))}, null, [], [],',f'                    [PT({q(t["path"])}, [{", ".join(t["cols"].values())}], [{", ".join(t["fields"].values())}], [{", ".join(t["actions"].values())}], {q(t["vis"])})], []),']
 lines += ['            ],', '            ['+',\n             '.join(root['actions'].values())+f'], Permissao: Permissao.{perm}, Fabrica: sp => PrepararPagina(sp, sp.GetRequiredService<{name}ViewModel>(), {q(key)}), AoAbrir: AbrirPaginaAsync, AoFechar: FecharPagina);']
lines+=['    }','    private static P.Tab PT(string origem, P.Coluna[] colunas, P.Campo[] campos, P.Acao[] acoes, string? visivel = null) =>','        new(origem, origem, [origem], vm => ColecaoWeb(vm, origem), colunas, campos, acoes, Visivel: visivel);','    private static IEnumerable<object> ColecaoWeb(object vm, string caminho)','    {','        object? valor = vm; foreach (var parte in caminho.Split(\'.\')) valor = valor?.GetType().GetProperty(parte)?.GetValue(valor);','        return (valor as IEnumerable)?.Cast<object>() ?? [];','    }','}']
Path('src/Clinica.Modulo.Recepcao/Web/RecepcaoPaginasRegistro.cs').write_text('\n'.join(lines)+'\n',encoding='utf-8')
# Administrative sections use exact same extraction. Prefix root paths & command paths only, row properties remain local.
lines=['using Clinica.Desktop.Shell.Web;','using P = Clinica.Desktop.Shell.Web.PaginasWebController;','namespace Clinica.Recepcao.Web;','public static partial class RecepcaoWebRegistro','{','    public static P.Secao[] SecoesAdministrativas(string prefixo = "Administrativo.DadosWeb")','    {','        P.Campo C(P.Campo c) => c with { Propriedade = prefixo + "." + c.Propriedade, Opcoes = c.Opcoes is null ? null : prefixo + "." + c.Opcoes, Guarda = Prefixar(c.Guarda), Visivel = Prefixar(c.Visivel) };','        string? Prefixar(string? v) => v is null ? null : string.Join("&", v.Split(\'&\').Select(p => (p.StartsWith("!") ? "!" : "") + prefixo + "." + p.TrimStart(\'!\')));','        P.Acao A(P.Acao a) => a with { Comando = prefixo + "." + a.Comando };','        return [']
for name,title in [('ResumoAdministrativoPaciente','Resumo administrativo'),('ConvenioPaciente','Convênio e autorizações'),('RelacionamentoPaciente','Relacionamento'),('PrivacidadePaciente','Privacidade'),('TermosPaciente','Termos')]:
 root,tables=extract(name)
 lines += [f'            new P.Secao({q(name)}, {q(title)}, null, [{", ".join("C("+f+")" for f in root["fields"].values())}], [], [']
 for t in tables:
  lines += [f'                PT(prefixo + "." + {q(t["path"])}, [{", ".join(t["cols"].values())}], [{", ".join(t["fields"].values())}], [{", ".join("A("+a+")" for a in t["actions"].values())}], Prefixar({q(t["vis"])})),']
 lines += [f'            ], [{", ".join("A("+a+")" for a in root["actions"].values())}]),']
lines+=['        ];','    }','}']
Path('src/Clinica.Modulo.Recepcao/Web/RecepcaoAdministrativoRegistro.cs').write_text('\n'.join(lines)+'\n',encoding='utf-8')
