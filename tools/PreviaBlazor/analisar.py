#!/usr/bin/env python3
"""Lê a saída do trocas.js e diz se a troca de telas deixou resto (out/2026).

  python3 tools/PreviaBlazor/analisar.py <pasta-saida>

Reprova (código 1) quando, depois de aberta pela primeira vez a tela da vez:
- algum quadro pintado mostrou ZERO telas (branco entre duas) ou DUAS (sobreposição);
- a tela à vista estava vazia, ou mostrou o esqueleto de "carregando" numa VOLTA (a volta
  relê em silêncio — esqueleto ali é a tela piscando);
- algum quadro gravado tem um bloco escuro (o flash) ou está em branco fora da "tela WPF".
"""
import bisect, glob, json, os, sys
from PIL import Image

pasta = sys.argv[1]
d = json.load(open(os.path.join(pasta, 'sonda.json')))
trocas, sonda = d['trocas'], d['sonda']
abertas, falhas = set(), []
ultimo_visivel = None  # instante do último quadro com uma tela Blazor à vista

# ---- 1. a sonda do DOM, quadro a quadro ----
marcas = [t['t0'] for t in trocas]
for q in sonda:
    i = bisect.bisect_right(marcas, q['t']) - 1
    if i < 0:
        continue
    tr = trocas[i]
    if tr['fase'] == 'abrir':
        abertas.add(tr['nome'])
    sem_tela = q['visiveis'] == 0
    desde = ultimo_visivel
    if q['visiveis'] == 1:
        ultimo_visivel = q['t']
    if q['erroTela']:
        falhas.append(f"quadro {q['t']:.0f}: cartão de erro na tela")
    if tr['fase'] != 'trocar':
        continue
    if tr['nome'] == 'WPF':
        if q['visiveis'] > 1:
            falhas.append(f"quadro {q['t']:.0f}: {q['visiveis']} telas à vista com a tela WPF")
        continue
    # Até o clique chegar ao servidor e voltar, a tela ANTERIOR segue à vista (ou nenhuma,
    # vindo da WPF): o que se cobra é nunca haver DUAS, e nunca zero entre duas telas Blazor.
    anterior = trocas[i - 1]['nome'] if i > 0 else None
    if q['visiveis'] > 1:
        falhas.append(f"quadro {q['t']:.0f}: {q['visiveis']} telas à vista ({tr['nome']})")
    elif sem_tela:
        # Zero telas só é branco se nenhuma "tela WPF" foi pedida desde a última tela Blazor
        # à vista (com folga da ida ao servidor): senão é a WPF ainda valendo, e é o certo.
        janela = (desde or 0) - 500
        if not any(t['nome'] == 'WPF' and janela <= t['t0'] <= q['t'] for t in trocas):
            falhas.append(f"quadro {q['t']:.0f}: nenhuma tela à vista entre {anterior} e {tr['nome']}")
    elif q['visiveis'] == 1 and q['conteudo'] == 0:
        falhas.append(f"quadro {q['t']:.0f}: tela à vista sem conteúdo ({tr['nome']})")
    elif q['visiveis'] == 1 and q['esqueleto']:
        falhas.append(f"quadro {q['t']:.0f}: esqueleto de carregamento numa volta a {tr['nome']}")

# ---- 2. os quadros que o Chromium pintou ----
arquivos = sorted(glob.glob(os.path.join(pasta, 'quadros', '*.png')))
paredes = [t['wall'] for t in trocas]
escuros = brancos = 0
pior = 0.0
for f in arquivos:
    ts = float(os.path.basename(f).split('_')[1][:-4])
    i = bisect.bisect_right(paredes, ts) - 1
    if i < 0 or trocas[i]['fase'] != 'trocar':
        continue
    img = Image.open(f).convert('L')
    w, h = img.size
    area = img.crop((0, 40, w, h)).resize((341, 182))
    px = list(area.get_flattened_data() if hasattr(area, "get_flattened_data") else area.getdata())
    fracao_escura = sum(1 for v in px if v < 60) / len(px)
    fracao_fundo = sum(1 for v in px if v >= 245) / len(px)
    pior = max(pior, fracao_escura)
    if fracao_escura > 0.25:
        escuros += 1
        falhas.append(f"quadro {os.path.basename(f)}: {fracao_escura:.0%} escuro (flash) em {trocas[i]['nome']}")
    nome_atual = trocas[i]['nome']
    anterior = trocas[i - 1]['nome'] if i > 0 else None
    if fracao_fundo > 0.995 and nome_atual != 'WPF' and anterior != 'WPF':
        brancos += 1
        falhas.append(f"quadro {os.path.basename(f)}: em branco em {nome_atual}")

trocas_reais = sum(1 for t in trocas if t['fase'] == 'trocar')
print(f"trocas: {trocas_reais} · quadros sondados: {len(sonda)} · quadros pintados: {len(arquivos)}")
print(f"maior fração escura num quadro: {pior:.1%} · quadros escuros: {escuros} · em branco: {brancos}")
print("erros do console:", d['erros'] or 'nenhum')
if falhas or d['erros']:
    print(f"{len(falhas)} problema(s):")
    for x in falhas[:40]:
        print('  -', x)
    sys.exit(1)
print("SEM RESTO: nenhum quadro em branco, escuro, duplicado ou carregando numa volta.")
