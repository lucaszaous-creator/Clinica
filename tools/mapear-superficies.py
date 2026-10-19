"""Inventário estático reproduzível. Lê objetos Git, nunca banco/configuração local.

Uso: python tools/mapear-superficies.py --site ../clinica-site --crm CAMINHO
Expressões regulares localizam candidatos; não provam execução/visibilidade.
"""
import argparse, collections, csv, hashlib, html, json, re, subprocess
from pathlib import Path

def git(root, *args):
    return subprocess.check_output(['git', '-C', str(root), *args])

def clean(s):
    return re.sub(r'<!--.*?-->|/\*.*?\*/|^[ \t]*//[^\n]*', lambda m: '\n'*m[0].count('\n'), s, flags=re.S|re.M)

def norm(s):
    s = re.sub(r'\\u([0-9a-fA-F]{4})', lambda m: chr(int(m[1],16)), s)
    return ' '.join(html.unescape(s).split())

def sources(root, ref):
    sha = git(root, 'rev-parse', ref).decode().strip()
    files = git(root, 'ls-tree','-r','--name-only',sha).decode().splitlines()
    selected = [p for p in files if p.endswith(('.cs','.xaml','.js','.ts','.tsx','.jsx','.html','.py','.csproj','.json'))
                and p.split('/')[0] in ('src','portal','modelos','conteudo','estatico')
                and not any(x in p.split('/') for x in ('vendor','node_modules','bin','obj'))
                and not p.endswith(('.min.js','.Designer.cs'))
                and not any(x in p.lower() for x in ('appsettings','package-lock','snapshot'))]
    proc = subprocess.run(['git','-C',str(root),'cat-file','--batch'],input=''.join(f'{sha}:{p}\n' for p in selected).encode(),stdout=subprocess.PIPE,check=True)
    data=proc.stdout; pos=0; result={}
    for p in selected:
        e=data.index(b'\n',pos); size=int(data[pos:e].split()[-1]); pos=e+1
        result[p]=data[pos:pos+size].decode('utf-8-sig'); pos+=size+1
    return sha, files, result

def main():
    ap=argparse.ArgumentParser(); ap.add_argument('--clinica',type=Path,default=Path(__file__).resolve().parents[1]); ap.add_argument('--site',type=Path,required=True); ap.add_argument('--crm',type=Path,required=True); ap.add_argument('--ref',default='origin/main'); ap.add_argument('--saida',type=Path); a=ap.parse_args()
    out=a.saida or a.clinica/'docs/auditoria-global-2026-10-01'; out.mkdir(parents=True,exist_ok=True)
    manifests=[]; controls=[]; functions=[]; routes=[]; links=[]; navigation=[]; surfaces=[]; meta={}; blobs={}
    def row(repo,p,s,m,**kw): return dict(repo=repo,arquivo=p,linha=s.count('\n',0,m.start())+1,**kw)
    for repo,root in [('Clinica',a.clinica),('clinica-site',a.site),('semdor-crm',a.crm)]:
        sha,files,source=sources(root,a.ref); blobs[repo]=source
        meta[repo]={'commit':sha,'ref':a.ref,'arquivos_git':len(files),'fontes_extraidas':len(source),'head_local':git(root,'rev-parse','HEAD').decode().strip()}
        for p in files:
            category='fonte analisada' if p in source else 'suporte/documentação/teste/ativo (manifesto)'
            if '/Migrations/' in p: category='migração (modelo de dados; não tela)'
            manifests.append(dict(repo=repo,arquivo=p,categoria=category,sha256=hashlib.sha256(source[p].encode()).hexdigest() if p in source else ''))
        for p,original in source.items():
            s=clean(original)
            if p.endswith(('.xaml','.html')) or (p.endswith(('.js','.cs')) and re.search(r'<(?:button|Button|main|TabItem|h1)\b',s)):
                surfaces.append(dict(repo=repo,arquivo=p,tipo='XAML' if p.endswith('.xaml') else 'HTML/template',linhas=len(original.splitlines())))
            for m in re.finditer(r'<(?P<tag>(?:[\w]+:)?(?:Button|MenuItem|TabItem|Expander|Window|UserControl|TextBlock|ComboBox|CheckBox|RadioButton|TextBox|DataGrid|button|a|input|select|textarea|dialog|section|h[1-6]))\b(?P<attrs>[^>]*)(?:>(?P<text>[^<]*))?',s):
                attrs=dict(re.findall(r'([\w:.-]+)\s*=\s*"([^"]*)"',m['attrs']))
                attrs.update(dict(re.findall(r"([\w:.-]+)\s*=\s*'([^']*)'",m['attrs'])))
                label=attrs.get('Content') or attrs.get('Header') or attrs.get('Title') or attrs.get('Text') or attrs.get('aria-label') or m['text'] or ''
                controls.append(row(repo,p,s,m,tipo=m['tag'],rotulo=norm(label),id=attrs.get('x:Name',attrs.get('id','')),acao=attrs.get('Command',attrs.get('Click',attrs.get('data-action',attrs.get('href','')))),condicao=attrs.get('Visibility',attrs.get('IsEnabled','')),atributos=norm(m['attrs'])))
            pats=[r'\b(?:public|private|protected|internal)\s+(?:(?:static|async|virtual|override|sealed|partial|new)\s+)*(?:[\w<>?,.\[\]]+\s+)+(?P<name>\w+)\s*\(',r'\b(?:async\s+)?function\s+(?P<name>[\w$]+)\s*\(',r'^\s*(?:async\s+)?def\s+(?P<name>\w+)\s*\(']
            pat=pats[0] if p.endswith('.cs') else pats[2] if p.endswith('.py') else pats[1]
            for m in re.finditer(pat,s,re.M): functions.append(row(repo,p,s,m,funcao=m['name']))
            for m in re.finditer(r'\.(MapGet|MapPost|MapPut|MapPatch|MapDelete|MapGroup)\(\s*"([^"]+)"',s):
                routes.append(row(repo,p,s,m,metodo=m[1],rota=m[2]))
            for m in re.finditer(r'''(?:href\s*=\s*["']|(?:api|fetch)\(\s*["'`])([^"'`<>\s]+)''',s):
                links.append(row(repo,p,s,m,destino=m[1]))
            for m in re.finditer(r'(?:NavegacaoSuite\.(?:Ir|Existe)|new\s+AbaMenu)\(([^\n;]+)|(?:Chave|Rotulo|Requer|RequerAlgum|PerfilExclusivo|Grupo|Oculto|Inicial)\s*=\s*([^\n]+)',s):
                if '/Modulo' in p or 'ViewModel' in p or 'Program.cs' in p or 'App.xaml.cs' in p:
                    navigation.append(row(repo,p,s,m,declaracao=norm(m[0])))
    datasets={'arquivos':manifests,'superficies':surfaces,'controles':controls,'funcoes':functions,'rotas':routes,'links':links,'navegacao':navigation}
    for name,rows in datasets.items():
        with (out/f'{name}.csv').open('w',encoding='utf-8-sig',newline='') as f:
            writer=csv.DictWriter(f,fieldnames=list(rows[0]) if rows else ['repo']); writer.writeheader(); writer.writerows(rows)
    meta['contagens']={k:len(v) for k,v in datasets.items()}
    (out/'inventario.json').write_text(json.dumps({'bases':meta,**datasets},ensure_ascii=False,indent=2),encoding='utf-8')
    (out/'bases.json').write_text(json.dumps(meta,ensure_ascii=False,indent=2),encoding='utf-8')
    # Uma ficha por superfície, sem esconder arquivos com poucos controles.
    lines=['# Catálogo de superfícies extraídas','', 'Extração estática: controles declarados podem depender de perfil, estado ou módulo. Contagens não equivalem a telas visíveis simultaneamente. Funções, rotas e arquivos completos estão nos CSVs.','']
    for surf in surfaces:
        repo,p=surf['repo'],surf['arquivo']; sha=meta[repo]['commit']
        lines += [f'## {repo} · {p}', '',f'[Fonte congelada](https://github.com/lucaszaous-creator/{repo}/blob/{sha}/{p}) · {surf["linhas"]} linhas.','', '| Linha | Tipo | Rótulo | Ação/vínculo |','| --- | --- | --- | --- |']
        for c in controls:
            if c['repo']==repo and c['arquivo']==p:
                vals=[c['linha'],c['tipo'],c['rotulo'],c['acao']]
                lines.append('| '+' | '.join(str(v).replace('|','\\|') for v in vals)+' |')
        lines.append('')
    (out/'CATALOGO.md').write_text('\n'.join(lines),encoding='utf-8')
    # Comparação dos serviços duplicados no CRM com a base da suíte.
    duplicated=[]
    for p,s in blobs['semdor-crm'].items():
        if p in blobs['Clinica']:
            duplicated.append(dict(arquivo=p,igual=s==blobs['Clinica'][p]))
    (out/'copias-crm.json').write_text(json.dumps(duplicated,ensure_ascii=False,indent=2),encoding='utf-8')
    print(json.dumps(meta,ensure_ascii=False,indent=2))

if __name__=='__main__': main()
