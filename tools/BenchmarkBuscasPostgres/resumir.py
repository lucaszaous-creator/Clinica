"""Resume medições locais; nunca conecta ao banco e nunca publica resultados."""
from pathlib import Path
import json, statistics, sys

raiz = Path(__file__).resolve().parents[2]
pasta = Path(sys.argv[1]) if len(sys.argv)>1 else raiz / 'artifacts/buscas-postgres'
d = json.loads((pasta/'resultado.json').read_text(encoding='utf-8'))
por_cenario = {}
for r in d['resultados']:
    por_cenario.setdefault(r['Cenario'], {})[r['Fase']] = {
        k:v for k,v in r.items() if k in ('MedianaMs','P95Ms','Linhas','ExecucaoSqlMs')}
indice_planos = {}
def percorrer(obj, encontrados):
    if isinstance(obj,dict):
        if 'Index Name' in obj: encontrados.add(obj['Index Name'])
        for valor in obj.values(): percorrer(valor,encontrados)
    elif isinstance(obj,list):
        for valor in obj: percorrer(valor,encontrados)
for nome in por_cenario:
    encontrados=set()
    percorrer(json.loads((pasta/f'completo2-{nome}-plano.json').read_text()),encontrados)
    indice_planos[nome]=sorted(encontrados)
resumo={'sql':por_cenario,'indices_nos_planos':indice_planos,'custos':d['custos']}
for variante in ('telas','telas-virtualizadas'):
    if not (pasta/variante/'resultado.json').exists(): continue
    telas=json.loads((pasta/variante/'resultado.json').read_text())
    grupos={}
    for r in telas['resultados']:
        chave=(r['tela'],r['paciente'],r['atrasoPorComandoMs'],r['fase'])
        grupos.setdefault(chave,[]).append(r)
    resumo[variante]=[dict(zip(('tela','paciente','atrasoMs','fase'),chave),
        amostras=len(linhas),medianaMs=statistics.median(r['telaProntaMs'] for r in linhas),
        minimoMs=min(r['telaProntaMs'] for r in linhas),maximoMs=max(r['telaProntaMs'] for r in linhas),
        comandosMin=min(r['comandos'] for r in linhas),comandosMax=max(r['comandos'] for r in linhas))
        for chave,linhas in grupos.items()]
    resumo['limites_telas']=telas['limites']
(pasta/'resumo.json').write_text(json.dumps(resumo,ensure_ascii=False,indent=2),encoding='utf-8')
for nome in ('nome-raro','nome-comum','lista-inicial','cpf-inteiro','receitas-paciente-1','documentos-paciente-1','evolucoes-paciente-1'):
    print(nome,por_cenario[nome]['antes2']['MedianaMs'],por_cenario[nome]['completo2']['MedianaMs'],indice_planos[nome])
for r in resumo.get('telas',[]):
    print(r)
