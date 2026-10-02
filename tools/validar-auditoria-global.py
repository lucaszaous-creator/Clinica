"""Valida a consistência dos artefatos da auditoria; não testa o produto."""
import ast,collections,csv,hashlib,json,re
from pathlib import Path
root=Path(__file__).resolve().parents[1];b=root/'docs/auditoria-global-2026-10-01'
def read(p):return json.loads(p.read_text(encoding='utf-8-sig'))
d=read(b/'inventario.json');meta=read(b/'bases.json')
assert d['bases']==meta,'Metadados diferentes'
manifest={(r['repo'],r['arquivo']) for r in d['arquivos']}
assert len(manifest)==len(d['arquivos']),'Manifesto duplicado'
for name,n in meta['contagens'].items():
    assert len(d[name])==n,(name,'contagem JSON')
    with (b/f'{name}.csv').open(encoding='utf-8-sig',newline='') as f:rows=list(csv.DictReader(f))
    assert len(rows)==n,(name,'contagem CSV')
    assert [{k:str(v) for k,v in x.items()} for x in d[name]]==rows,(name,'conteúdo CSV diferente')
    for row in d[name]:
        assert (row['repo'],row['arquivo']) in manifest,(name,'arquivo fora do manifesto')
        if 'linha' in row:assert isinstance(row['linha'],int) and row['linha']>0
surfaces=read(b/'cobertura-superficies.json')
assert [(r['repo'],r['arquivo']) for r in surfaces]==[(r['repo'],r['arquivo']) for r in d['superficies']]
findings=read(b/'achados.json');evidence=read(b/'evidencias.json')
assert [f['id'] for f in findings]==[f'A{i:02}' for i in range(1,len(findings)+1)]
assert len(evidence)==len(findings)
for f,e in zip(findings,evidence):
    assert all(e[k]==v for k,v in f.items()),f['id']
    assert len(f['fontes'])==len(e['evidencias'])>0
    for original,ref in zip(f['fontes'],e['evidencias']):
        assert (ref['repo'],ref['arquivo'])==tuple(original[:2])
        assert meta[ref['repo']]['commit'] in ref['url']
        assert ref['url'].endswith('#L'+str(ref['linha']))
    assert all(f[k].strip() for k in ('atual','impacto','proposta','aceite'))
answers={}
for p in sorted((b/'jev').glob('*-resultado.json')):
    result=read(p);request=p.with_name(p.name.replace('-resultado','-pedido'))
    assert hashlib.sha256(request.read_bytes()).hexdigest()==result['sha256_pedido'],p.name
    q=read(request);assert set(q['questions'])==set(result['answers']),p.name
    for ident,v in result['answers'].items():
        assert ident not in answers,ident
        assert v['choice'] in q['questions'][ident]['criteria']
        assert 0<=v['confidence']<=1
        answers[ident]=v
assert set(answers)=={r['id'] for r in surfaces}|{f['id'] for f in findings}
nav=read(b/'navegacao-perfis.json')
assert len(nav)==35 and len({(x['aplicativo'],x['perfil']) for x in nav})==35
assert len({x['aplicativo'] for x in nav})==5 and len({x['perfil'] for x in nav})==7
for p in b.glob('*.md'):
    for target in re.findall(r'\]\(([^)]+)\)',p.read_text(encoding='utf-8')):
        target=target.split('#',1)[0]
        if target and not re.match(r'\w+://',target):assert (p.parent/target).exists(),(p.name,target)
for p in (root/'tools').glob('*auditoria*.py'):ast.parse(p.read_text(encoding='utf-8'),filename=str(p))
for name in ('mapear-superficies.py','mapear-navegacao-perfis.py','revisar-mapa-jev.py'):
    ast.parse((root/'tools'/name).read_text(encoding='utf-8'),filename=name)
stats={'arquivos':len(manifest),'fontes_extraidas':sum(x['fontes_extraidas'] for k,x in meta.items() if k!='contagens'),'superficies':len(surfaces),'achados':len(findings),'referencias':sum(len(e['evidencias']) for e in evidence),'respostas_jev':len(answers),'combinacoes_perfis':len(nav),'revisao_achados':dict(collections.Counter(v['choice'] for k,v in answers.items() if k.startswith('A')))}
(b/'validacao-inventario.json').write_text(json.dumps({'resultado':'OK','escopo':'Consistência documental, não execução do produto',**stats},ensure_ascii=False,indent=2),encoding='utf-8')
print(json.dumps(stats,ensure_ascii=False));print('OK — inventários, evidências, cobertura, respostas e referências internas consistentes.')
