#!/usr/bin/env python3
"""Gera o catálogo da GESTÃO WEB a partir do código dos módulos do Gerente Geral (nov/2026).

A gestão no navegador mostra as MESMAS telas do app Gerente Geral, divididas em quatro áreas
(Gestão, Financeiro com o Faturamento, Recepção e Clínico). O menu do app mora nos módulos WPF
(`Clinica.Modulo.*`, net8.0-windows), que o servidor Linux não carrega. Em vez de uma segunda
lista escrita à mão — a que a próxima tela nova esquece —, este script LÊ os módulos:

  · os itens de menu (`new ItemMenuModulo { … }`: chave, rótulo, permissão, abas, oculto);
  · a tela de cada chave (`CriarTela`: `chave => … typeof(Clinica.Ui.Blazor.Telas.X)`);
  · os destinos do Faturamento, que ele monta por dados (a tabela `Destinos`).

e grava `src/Clinica.Gestao.Web/catalogo-gestao.json` no MESMO formato do `navegacao.json` que
a prévia lê. O `verificar-suite.py` roda este script em modo `--conferir` a cada PR: tela nova
no app que não chegou ao catálogo reprova o CI com a instrução de rodar o script.

Uso:  python3 tools/gerar-catalogo-gestao-web.py            # grava
      python3 tools/gerar-catalogo-gestao-web.py --conferir # só compara (sai 1 se divergir)
"""
import json
import pathlib
import re
import sys

RAIZ = pathlib.Path(__file__).resolve().parents[1]
SRC = RAIZ / 'src'
SAIDA = SRC / 'Clinica.Gestao.Web' / 'catalogo-gestao.json'

# A ordem dos módulos é a do Gerente Geral (src/Clinica.Gerente/App.xaml.cs); a área é o pedido da
# direção: a aba do portal em que as telas do módulo aparecem.
MODULOS = [
    ('Recepcao', 'Recepção'),
    ('Clinico', 'Clínico'),
    ('Financeiro', 'Financeiro'),
    ('Faturamento', 'Financeiro'),
    ('Gerente', 'Gestão'),
]
AREAS = ['Gestão', 'Financeiro', 'Recepção', 'Clínico']


def texto(s):
    """Literal C# "…" (com \\uXXXX) para texto."""
    return json.loads('"' + s + '"')


def constantes(arquivo, prefixo=''):
    out = {}
    for m in re.finditer(r'const\s+string\s+(\w+)\s*=\s*([^;]+);', arquivo):
        out[prefixo + m.group(1)] = m.group(2).strip()
    return out


def resolver(expr, consts, visto=None):
    expr = expr.strip()
    visto = visto or set()
    if expr.startswith('"'):
        return texto(expr[1:-1])
    if expr in visto:
        raise ValueError(f'Constante circular: {expr}')
    visto.add(expr)
    if expr in consts:
        return resolver(consts[expr], consts, visto)
    if '.' in expr:
        # Constante de outra classe (ex.: Clinica.Application.Servicos.ConversaNoCrm.ChaveDaAba):
        # procura `const string Nome` no arquivo que declara a classe.
        classe, nome = expr.split('.')[-2:]
        for arq in SRC.rglob('*.cs'):
            if '/bin/' in str(arq) or '/obj/' in str(arq):
                continue
            t = arq.read_text(encoding='utf-8', errors='ignore')
            if re.search(r'\b(class|record)\s+' + classe + r'\b', t):
                m = re.search(r'const\s+string\s+' + nome + r'\s*=\s*([^;]+);', t)
                if m:
                    return resolver(m.group(1), {**consts, **constantes(t)}, visto)
    raise ValueError(f'Constante não resolvida: {expr}')


def blocos(arquivo, abertura):
    """Os blocos `{ … }` que seguem cada `abertura` (com chaves balanceadas)."""
    for m in re.finditer(re.escape(abertura), arquivo):
        i = arquivo.index('{', m.end())
        profundidade = 0
        for j in range(i, len(arquivo)):
            if arquivo[j] == '{':
                profundidade += 1
            elif arquivo[j] == '}':
                profundidade -= 1
                if profundidade == 0:
                    yield arquivo[i + 1:j]
                    break


def permissoes(corpo, campo):
    m = re.search(campo + r'\s*=\s*([^,}\n]+)', corpo)
    if not m:
        return []
    return sorted(re.findall(r'Permissao\.(\w+)', m.group(1)))


def item(corpo, consts):
    chave = resolver(re.search(r'\bChave\s*=\s*([^,}\n]+)', corpo).group(1), consts)
    rotulo = texto(re.search(r'\bRotulo\s*=\s*"((?:[^"\\]|\\.)*)"', corpo).group(1))
    desc = re.search(r'\bDescricao\s*=\s*"((?:[^"\\]|\\.)*)"', corpo)
    perfil = re.search(r'PerfilExclusivo\s*=\s*PerfilAcesso\.(\w+)', corpo)
    abas = [{'rotulo': texto(a), 'chave': resolver(c, consts)}
            for a, c in re.findall(r'new\s+AbaMenu\(\s*"((?:[^"\\]|\\.)*)"\s*,\s*([^)]+?)\s*\)', corpo)]
    return {
        'chave': chave, 'rotulo': rotulo, 'descricao': texto(desc.group(1)) if desc else None,
        'oculto': bool(re.search(r'\bOculto\s*=\s*true', corpo)),
        'requer': permissoes(corpo, r'\bRequer'), 'requerAlgum': permissoes(corpo, r'\bRequerAlgum'),
        'perfilExclusivo': perfil.group(1) if perfil else None, 'abas': abas,
    }


def telas(arquivo, consts):
    """chave -> componente, pelo `CriarTela` do módulo."""
    m = re.search(r'CriarTela\(string chave, IServiceProvider servicos\)\s*=>\s*chave switch\s*\{(.*?)\n\s*\};', arquivo, re.S)
    out = {}
    if not m:
        return out
    corpo = re.sub(r'//[^\n]*', '', m.group(1))
    for braco in re.split(r',\s*\n', corpo):
        if '=>' not in braco:
            continue
        esquerda, direita = braco.split('=>', 1)
        if esquerda.strip() == '_':
            continue
        tipo = re.search(r'typeof\(Clinica\.Ui\.Blazor\.Telas\.(\w+)\)', direita)
        if not tipo and 'TelaDoPaciente(' in direita:
            tipo = re.match(r'(\w+)', 'TelaDoPaciente')
        for c in re.split(r'\s+or\s+', esquerda.strip()):
            out[resolver(c, consts)] = tipo.group(1) if tipo else None
    return out


def faturamento(arquivo, consts):
    """O módulo do Faturamento monta o menu por dados (a tabela `Destinos`)."""
    destinos = []
    for chave, rotulo, tipo, perm in re.findall(
            r'\(\s*("[^"]+"|ChavesSuite\.\w+)\s*,\s*"((?:[^"\\]|\\.)*)"\s*,\s*typeof\((\w+)\)\s*,\s*Permissao\.(\w+)\s*,', arquivo):
        destinos.append((resolver(chave, consts), texto(rotulo), tipo, perm))
    pai = re.search(r'Chave\s*=\s*(ChavesSuite\.\w+)\s*,\s*Rotulo\s*=\s*"([^"]+)"', arquivo)
    resumo = re.search(r'new AbaMenu\("([^"]+)",\s*"([^"]+)"\)', arquivo)
    take = int(re.search(r'Destinos\.Take\((\d+)\)', arquivo).group(1))
    itens = [{
        'chave': resolver(pai.group(1), consts), 'rotulo': texto(pai.group(2)), 'descricao': None, 'oculto': False,
        'requer': ['VerFaturamento'], 'requerAlgum': [], 'perfilExclusivo': None,
        'abas': [{'rotulo': resumo.group(1), 'chave': resumo.group(2)}]
                + [{'rotulo': r, 'chave': c} for c, r, _, _ in destinos[:take]],
    }]
    for c, r, _, p in destinos:
        itens.append({'chave': c, 'rotulo': r, 'descricao': None, 'oculto': False, 'requer': [p],
                      'requerAlgum': [], 'perfilExclusivo': None, 'abas': []})
    return itens, {c: t for c, _, t, _ in destinos}


def gerar():
    consts = constantes((SRC / 'Clinica.Desktop.Shell/Modulos/ChavesSuite.cs').read_text(encoding='utf-8'), 'ChavesSuite.')
    itens, componentes, area_de = {}, {}, {}
    for modulo, area in MODULOS:
        arquivo = (SRC / f'Clinica.Modulo.{modulo}/Modulo/Modulo{modulo}.cs').read_text(encoding='utf-8')
        locais = {**consts, **constantes(arquivo)}
        # ChavesSuite.X aparece também como "ChavesSuite.X" dentro das constantes locais.
        if modulo == 'Faturamento':
            lista, mapa = faturamento(arquivo, locais)
        else:
            lista = [item(c, locais) for c in blocos(arquivo, 'new ItemMenuModulo')]
            mapa = telas(arquivo, locais)
        for k, v in mapa.items():
            if v and k not in componentes:
                componentes[k] = v
        for i in lista:
            # Como o shell do Gerente: a mesma chave publicada por dois módulos vira UMA linha (a do primeiro).
            if i['chave'] in itens:
                continue
            itens[i['chave']] = i
            area_de[i['chave']] = area
    for i in itens.values():
        i['componente'] = componentes.get(i['chave'])
    for i in itens.values():
        for a in i['abas']:
            if a['chave'] not in itens:
                # Aba cuja chave não é item de menu (só destino): vira item oculto, para a navegação a achar.
                itens[a['chave']] = {'chave': a['chave'], 'rotulo': a['rotulo'], 'descricao': None, 'oculto': True,
                                     'requer': i['requer'], 'requerAlgum': i['requerAlgum'], 'perfilExclusivo': None,
                                     'abas': [], 'componente': componentes.get(a['chave'])}
                area_de[a['chave']] = area_de[i['chave']]
    # Na barra: o item visível que não é aba de outro item visível (a aba já aparece dentro do pai).
    visiveis = [i for i in itens.values() if not i['oculto']]
    abas_de_visiveis = {a['chave'] for i in visiveis for a in i['abas'] if len(i['abas']) > 1}
    barra = []
    for area in AREAS:
        lista = [i['chave'] for i in visiveis if area_de[i['chave']] == area and i['chave'] not in abas_de_visiveis]
        barra.append({'rotulo': area, 'direto': None, 'itens': lista})
    return [{
        'app': 'gestao', 'nome': 'Gestão', 'perfil': 'Gerente', 'barra': barra, 'telasTreinamento': [],
        'itens': sorted(itens.values(), key=lambda i: i['chave']),
    }]


def main():
    novo = json.dumps(gerar(), ensure_ascii=False, indent=1) + '\n'
    if '--conferir' in sys.argv:
        atual = SAIDA.read_text(encoding='utf-8') if SAIDA.exists() else ''
        if atual != novo:
            print('catálogo da gestão web desatualizado: rode python3 tools/gerar-catalogo-gestao-web.py e faça o commit',
                  file=sys.stderr)
            return 1
        return 0
    SAIDA.parent.mkdir(parents=True, exist_ok=True)
    SAIDA.write_text(novo, encoding='utf-8')
    print(f'catálogo gravado: {SAIDA.relative_to(RAIZ)}')
    return 0


if __name__ == '__main__':
    sys.exit(main())
