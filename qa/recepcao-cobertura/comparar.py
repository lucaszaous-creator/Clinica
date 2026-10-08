"""Comparação nominal determinística. Não prova equivalência funcional de aliases/eventos."""
from pathlib import Path
import json,re,xml.etree.ElementTree as ET
from collections import Counter
root=Path(__file__).resolve().parents[2]
dest=root/'artifacts/recepcao-cobertura'
bindings=json.loads((dest/'vinculos.json').read_text(encoding='utf-8-sig'))
registros=json.loads((dest/'registros.json').read_text(encoding='utf-8-sig'))
tipos={r['Tipo'].split('.')[-1] for r in bindings}
sources=[]
for folder in ['Clinica.Desktop.Shell','Clinica.Modulo.Gerente','Clinica.Modulo.Recepcao','Clinica.Modulo.Clinico','Clinica.Modulo.Faturamento','Clinica.Modulo.Financeiro']:
    sources.extend(p for p in (root/'src'/folder).rglob('*.xaml') if not {'obj','bin','Styles'}.intersection(p.parts))
results=[]
def local(s):return s.split('}')[-1]
def norm(s):return re.sub(r'^DataContext\.','',s).strip()
for path in sources:
    text=path.read_text(encoding='utf-8-sig')
    try:doc=ET.fromstring(text)
    except ET.ParseError:continue
    cs=Path(str(path)+'.cs')
    code=cs.read_text(encoding='utf-8-sig') if cs.exists() else ''
    candidates=list(dict.fromkeys(re.findall(r'\b(\w+ViewModel)\b',code)))
    assumed=re.sub('(Window|View)$','ViewModel',path.stem)
    vm=assumed if assumed in tipos else next((v for v in candidates if v in tipos),assumed)
    seen=set()
    for el in doc.iter():
        tag=local(el.tag)
        for attr,val in el.attrib.items():
            attr=local(attr)
            cat='acao' if attr=='Command' else 'campo' if tag in ['TextBox','RichTextBox','ComboBox','DatePicker','CheckBox','ToggleButton','RadioButton','PasswordBox','DataGridTextColumn','DataGridCheckBoxColumn','DataGridComboBoxColumn'] and attr in ['Text','SelectedItem','SelectedValue','SelectedDate','IsChecked','Binding'] else None
            if cat is None:continue
            m=re.search(r'\{Binding\s+(?:Path=)?([\w.]+)',val)
            if not m:continue
            name=norm(m.group(1))
            if name in ['RelativeSource','ElementName','Source']:continue
            if (cat,name) in seen:continue
            seen.add((cat,name))
            exact=[r for r in bindings if r['Tipo'].split('.')[-1]==vm and r['Caminho']==name and (r['Categoria']=='acao')==(cat=='acao')]
            other=[r for r in bindings if (r['Caminho']==name or r['Caminho'].endswith('.'+name)) and (r['Categoria']=='acao')==(cat=='acao')]
            status='correspondencia_vm' if exact else 'composto_ou_homonimo' if other else 'sem_correspondencia'
            results.append(dict(source=str(path.relative_to(root)).replace('\\','/'),line=text[:text.find(val)].count('\n')+1,vm=vm,tipo=cat,binding=name,status=status,destinos=sorted(set(r['Rota'] for r in exact or other))))
report={'metodo':'Bindings Command e campos editáveis de XAML comparados aos registros C# materializados, por VM e caminho. Homônimos/compostos exigem confirmação contextual; eventos code-behind não são equivalentes automaticamente. Não mede cobertura funcional.', 'paginas':len(registros['Paginas']),'dialogos':len(registros['Dialogos']),'xaml':len(sources),'contagens':dict(Counter(r['status'] for r in results)),'itens':results}
(dest/'comparacao.json').write_text(json.dumps(report,ensure_ascii=False,indent=2),encoding='utf-8')
lines=['# Correspondência nominal XAML → web','',report['metodo'],'',f"{len(sources)} XAML; {len(registros['Paginas'])} páginas; {len(registros['Dialogos'])} diálogos; {len(results)} vínculos de edição/ação.",'',str(report['contagens']),'','## Sem correspondência literal no catálogo materializado','', '| Fonte | VM inferida | Tipo | Vínculo |','|---|---|---|---|']
for r in results:
    if r['status']=='sem_correspondencia':lines.append(f"| {r['source']}:{r['line']} | {r['vm']} | {r['tipo']} | `{r['binding']}` |")
lines+=['','As correspondências nominais por VM e os candidatos compostos/homônimos estão detalhados em comparacao.json. A classificação de ausência não é afirmação de perda: aliases, seletores de UI e eventos precisam da justificativa manual em justificativas.md.']
(dest/'comparacao.md').write_text('\n'.join(lines),encoding='utf-8')
print(report['contagens'])
for r in results:
    if r['status']=='sem_correspondencia':print(r['source'],r['line'],r['tipo'],r['binding'])
