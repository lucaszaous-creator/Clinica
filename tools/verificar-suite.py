#!/usr/bin/env python3
"""
Verificação estática da suíte multi-exe (Shell, módulos e executáveis).

Existe porque estes projetos são net8.0-windows/WPF e NÃO compilam em Linux: sem
Windows, um `x:Key` errado ou um pack URI apontando para arquivo que não existe só
apareceria no CI (ou, pior, em tempo de execução, como XamlParseException).

O que confere:
  1. XAML bem-formado (todo .xaml da suíte);
  2. todo `{StaticResource X}` tem um `x:Key="X"` no design system ou no próprio arquivo;
  2b. cada dicionário de Styles/ MESCLA os dicionários das chaves que usa — sem isso o
     app sobe e quebra ao abrir a tela (ver o bloco da checagem para o porquê);
  3. todo pack URI `...;component/Caminho.xaml` aponta para um arquivo existente;
  4. todo `x:Class` tem o code-behind correspondente com a classe declarada;
  5. todo `<ProjectReference>` aponta para um .csproj existente, e todo .csproj da
     suíte está no Clinica.sln;
  6. nenhum uso do tipo `Application` sem qualificar (ver ARMADILHA abaixo);
  7. todo `new XViewModel(...)` escrito à mão passa uma quantidade de argumentos que o
     construtor aceita (ver ARMADILHA do construtor abaixo);
  8. todo `new X { ... }` de um tipo com membros `required` inicializa TODOS eles
     (CS9035) — as classes `Linha*` das listas são cheias deles.

ARMADILHA `Application` (CS0118): dentro de qualquer namespace `Clinica.*`, o nome
`Application` resolve para o NAMESPACE `Clinica.Application` — nunca para o tipo
`System.Windows.Application`. `public partial class App : Application` compila em
qualquer outro projeto WPF do mundo e falha aqui. Sempre `System.Windows.Application`.

ARMADILHA do construtor (CS7036): metade dos ViewModels de formulário é construída À MÃO
pela tela dona (`new PacienteEdicaoViewModel(escopos, id)`), porque precisa receber o id
no construtor e não passa pelo DI. Quando um deles ganha uma dependência nova, o DI se
vira sozinho e os pontos de construção manual NÃO — e o erro só aparece no build do
Windows, minutos depois. A checagem 7 compara a aridade dos dois lados.

NÃO substitui o build no Windows — substitui a parte dele que dá para conferir aqui.

Uso:  python3 tools/verificar-suite.py
"""

from __future__ import annotations

import re
import sys
import xml.etree.ElementTree as ET
from pathlib import Path

RAIZ = Path(__file__).resolve().parent.parent

# Projetos que compõem a suíte (o faturamento, Clinica.Desktop, entra na Fase 4).
PROJETOS = [
    "src/Clinica.Desktop.Shell",
    "src/Clinica.Modulo.Recepcao",
    "src/Clinica.Modulo.Financeiro",
    "src/Clinica.Modulo.Gerente",
    "src/Clinica.Recepcao",
    "src/Clinica.Financeiro",
    "src/Clinica.Gerente",
]

X = "{http://schemas.microsoft.com/winfx/2006/xaml}"

erros: list[str] = []
avisos: list[str] = []


def rel(p: Path) -> str:
    return str(p.relative_to(RAIZ))


def xamls() -> list[Path]:
    return sorted(f for proj in PROJETOS for f in (RAIZ / proj).rglob("*.xaml"))


def csprojs() -> list[Path]:
    return sorted(f for proj in PROJETOS for f in (RAIZ / proj).glob("*.csproj"))


# ---------------------------------------------------------------- 1. bem-formado
arvores: dict[Path, ET.Element] = {}
for f in xamls():
    try:
        arvores[f] = ET.parse(f).getroot()
    except ET.ParseError as e:
        erros.append(f"{rel(f)}: XAML malformado — {e}")


# ------------------------------------------------- 2. chaves do design system
def chaves(raiz: ET.Element) -> set[str]:
    return {el.attrib[X + "Key"] for el in raiz.iter() if X + "Key" in el.attrib}


# O design system é global (mesclado no App.xaml): qualquer tela pode usar suas chaves.
chaves_globais: set[str] = set()
for f, raiz in arvores.items():
    if "Styles" in f.parts:
        chaves_globais |= chaves(raiz)

# Chaves que o WPF resolve sozinho (tema do sistema) e não vêm do design system.
CHAVES_DO_SISTEMA = {"{x:Type ", "SystemColors"}

REF_ESTATICA = re.compile(r"\{StaticResource\s+([A-Za-z0-9_.]+)\s*\}")

for f, raiz in arvores.items():
    locais = chaves(raiz)
    texto = f.read_text(encoding="utf-8")
    for chave in sorted(set(REF_ESTATICA.findall(texto))):
        if chave in locais or chave in chaves_globais:
            continue
        if any(chave.startswith(p) for p in CHAVES_DO_SISTEMA):
            continue
        erros.append(f"{rel(f)}: StaticResource '{chave}' não existe no design system")


PACK = re.compile(r"pack://application:,,,/([A-Za-z0-9_.]+);component/([^\"']+)")


# ------------------- 2b. recurso alcançável DE DENTRO do próprio dicionário
# O conteúdo de um ControlTemplate/DataTemplate é ADIADO: só é interpretado quando o
# primeiro controle aparece na tela, e aí o {StaticResource} é resolvido no escopo do
# DICIONÁRIO que o declara — não no App.xaml, que já mesclou tudo. Por isso um
# dicionário de componente precisa mesclar, ele mesmo, os dicionários das chaves que
# usa (é o que Botoes/Campos/Feedback já fazem com o Tokens).
#
# Sem esta checagem, a suíte sobe normalmente e quebra ao abrir a tela, com
# "StaticResourceHolder iniciou uma exceção" — foi assim que Midia.xaml e
# Pacientes.xaml chegaram à clínica sem o bloco MergedDictionaries.

FONTE_MESCLADA = re.compile(r'<ResourceDictionary\s+Source="([^"]+)"')


def resolver_fonte(dicionario: Path, fonte: str) -> Path | None:
    """Caminho no disco de um Source (pack URI ou relativo)."""
    if fonte.startswith("pack:"):
        m = PACK.match(fonte)
        if not m:
            return None
        assembly, caminho = m.groups()
        return RAIZ / "src" / assembly / caminho
    return (dicionario.parent / fonte.replace("\\", "/")).resolve()


def alcancaveis(dicionario: Path, vistos: set[Path] | None = None) -> set[str]:
    """Chaves visíveis de dentro deste dicionário: as dele + as que ele mescla."""
    vistos = vistos if vistos is not None else set()
    if dicionario in vistos or dicionario not in arvores:
        return set()
    vistos.add(dicionario)

    chaves_ = chaves(arvores[dicionario])
    for fonte in FONTE_MESCLADA.findall(dicionario.read_text(encoding="utf-8")):
        destino = resolver_fonte(dicionario, fonte)
        if destino is not None:
            chaves_ |= alcancaveis(destino, vistos)
    return chaves_


for f, raiz in arvores.items():
    if "Styles" not in f.parts:
        continue  # views resolvem contra o App.xaml, que mescla tudo
    visiveis = alcancaveis(f)
    for chave in sorted(set(REF_ESTATICA.findall(f.read_text(encoding="utf-8")))):
        if chave in visiveis or any(chave.startswith(p) for p in CHAVES_DO_SISTEMA):
            continue
        erros.append(
            f"{rel(f)}: usa '{chave}' mas não mescla o dicionário que a define — "
            f"em conteúdo adiado (template) isso quebra em tempo de execução. "
            f"Acrescente o Source em <ResourceDictionary.MergedDictionaries>")


# --------------------------------------------------------------- 3. pack URIs
for f in arvores:
    for assembly, caminho in PACK.findall(f.read_text(encoding="utf-8")):
        destino = RAIZ / "src" / assembly / caminho
        if not destino.exists():
            erros.append(f"{rel(f)}: pack URI aponta para arquivo inexistente — {assembly}/{caminho}")


# ------------------------------------------------------------- 4. code-behind
for f, raiz in arvores.items():
    classe = raiz.attrib.get(X + "Class")
    if not classe:
        continue
    cs = f.with_suffix(".xaml.cs")
    if not cs.exists():
        erros.append(f"{rel(f)}: x:Class='{classe}' sem code-behind ({cs.name})")
        continue
    simples = classe.rsplit(".", 1)[-1]
    fonte = cs.read_text(encoding="utf-8")
    if not re.search(rf"partial class {re.escape(simples)}\b", fonte):
        erros.append(f"{rel(cs)}: não declara 'partial class {simples}'")


# ----------------------------------------------------- 5. referências e solução
sln = (RAIZ / "Clinica.sln").read_text(encoding="utf-8")

for proj in csprojs():
    texto = proj.read_text(encoding="utf-8")
    for destino in re.findall(r'<ProjectReference\s+Include="([^"]+)"', texto):
        alvo = (proj.parent / destino.replace("\\", "/")).resolve()
        if not alvo.exists():
            erros.append(f"{rel(proj)}: ProjectReference inexistente — {destino}")
    if proj.name not in sln:
        erros.append(f"{rel(proj)}: fora do Clinica.sln (não será compilado pelo CI)")


# ------------------------------------- 6. propriedade definida duas vezes no XAML
# `<TextBlock Style="{StaticResource X}"> <TextBlock.Style> … ` é MC3024: a mesma
# propriedade como atributo e como elemento. Quando o elemento traz um BasedOn, a
# intenção era só o elemento — mas o compilador recusa antes de chegar nisso.
for f, raiz in arvores.items():
    for pai in raiz.iter():
        tag_pai = pai.tag.rsplit("}", 1)[-1]
        for filho in pai:
            tag = filho.tag.rsplit("}", 1)[-1]
            if "." not in tag:
                continue
            dono, prop = tag.rsplit(".", 1)
            if dono != tag_pai:
                continue  # attached property (Grid.Row etc.), não é o caso
            if prop in pai.attrib:
                erros.append(
                    f"{rel(f)}: <{tag_pai}> define '{prop}' como atributo E como "
                    f"<{tag}> (MC3024) — deixe só um dos dois")


# -------------------------------------------------- 7. x:Key repetido no dicionário
# Duas entradas com a mesma chave no mesmo ResourceDictionary o compilador recusa.
# (x:Name repetido NÃO entra aqui: cada ControlTemplate é um name scope próprio, e
# reaproveitar o mesmo nome em templates diferentes é legítimo — e comum no
# design system.)
for f, raiz in arvores.items():
    vistos: dict[str, int] = {}
    for el in raiz.iter():
        if X + "Key" in el.attrib:
            vistos[el.attrib[X + "Key"]] = vistos.get(el.attrib[X + "Key"], 0) + 1
    for valor, quantas in vistos.items():
        if quantas > 1:
            erros.append(f"{rel(f)}: x:Key='{valor}' aparece {quantas}x no mesmo arquivo")


# ------------------------------------- 8. manipuladores de evento com code-behind
EVENTO = re.compile(r'\s(?:Click|Checked|Unchecked|SelectionChanged|TextChanged|'
                    r'Loaded|MouseDoubleClick|KeyDown|KeyUp|Closing|Closed)="([A-Za-z_]\w*)"')

for f in arvores:
    cs = f.with_suffix(".xaml.cs")
    fonte = cs.read_text(encoding="utf-8") if cs.exists() else ""
    for metodo in sorted(set(EVENTO.findall(f.read_text(encoding="utf-8")))):
        if not re.search(rf"\b{re.escape(metodo)}\s*\(", fonte):
            erros.append(f"{rel(f)}: evento aponta para '{metodo}', que não existe no code-behind")


# --------------------------------------------------- 9. nomes que colidem com namespace
# Nomes de tipo que, sem qualificar, o compilador resolve como namespace `Clinica.X`
# em vez do tipo pretendido (CS0118). O erro não aparece em nenhum outro projeto WPF,
# só aqui — e só no Windows, que é onde não dá para compilar antes de subir.
COLISOES = {"Application": "System.Windows.Application"}

# Ocorrência do nome sozinho: nem precedida de ponto (já qualificada) nem seguida de
# ponto (é o prefixo de um namespace, como em `Application.Servicos`).
def solto(nome: str) -> re.Pattern[str]:
    return re.compile(rf"(?<![\w.]){re.escape(nome)}(?![\w.])")


for proj in PROJETOS:
    for cs in sorted((RAIZ / proj).rglob("*.cs")):
        if "obj" in cs.parts or "bin" in cs.parts:
            continue
        for n, linha in enumerate(cs.read_text(encoding="utf-8").splitlines(), 1):
            codigo = linha.split("//", 1)[0]
            if codigo.lstrip().startswith(("///", "*", "/*")):
                continue
            for nome, correcao in COLISOES.items():
                if solto(nome).search(codigo):
                    erros.append(
                        f"{rel(cs)}:{n}: '{nome}' sem qualificar resolve para o namespace "
                        f"Clinica.{nome} (CS0118) — use '{correcao}'")


# ------------------------------------------- 7. aridade dos construtores de ViewModel
#
# Só a ARIDADE, não os tipos: sem compilador não há como resolver sobrecarga, e o que
# quebra na prática é a tela que continua passando os argumentos antigos depois de o
# ViewModel ganhar uma dependência. Tipo trocado o build pega; contagem errada, também —
# mas esta roda em dois segundos e aqui.

CTOR_VM = re.compile(r"public\s+(\w+ViewModel)\s*\(([^)]*)\)\s*(?::[^\{]*)?\{", re.S)
CHAMADA_VM = re.compile(r"new\s+(?:\w+\.)*(\w+ViewModel)\s*\(([^()]*(?:\([^()]*\)[^()]*)*)\)")


def _dividir(texto: str) -> list[str]:
    """Quebra a lista de argumentos na vírgula de nível zero (genéricos e chamadas aninhadas)."""
    partes, nivel, atual = [], 0, ""
    for ch in texto:
        if ch in "(<":
            nivel += 1
        elif ch in ")>":
            nivel -= 1
        if ch == "," and nivel == 0:
            partes.append(atual)
            atual = ""
        else:
            atual += ch
    partes.append(atual)
    return [x.strip() for x in partes if x.strip()]


def _fontes() -> list[Path]:
    return [
        cs
        for proj in PROJETOS
        for cs in sorted((RAIZ / proj).rglob("*.cs"))
        if "obj" not in cs.parts and "bin" not in cs.parts
    ]


assinaturas: dict[str, tuple[int, int]] = {}
for cs in _fontes():
    for m in CTOR_VM.finditer(cs.read_text(encoding="utf-8")):
        params = _dividir(m.group(2))
        opcionais = sum(1 for x in params if "=" in x)
        assinaturas[m.group(1)] = (len(params) - opcionais, len(params))

for cs in _fontes():
    for n, linha in enumerate(cs.read_text(encoding="utf-8").splitlines(), 1):
        codigo = linha.split("//", 1)[0]
        for m in CHAMADA_VM.finditer(codigo):
            nome, args = m.group(1), m.group(2).strip()
            if nome not in assinaturas:
                continue
            quantos = len(_dividir(args))
            minimo, maximo = assinaturas[nome]
            if not minimo <= quantos <= maximo:
                erros.append(
                    f"{rel(cs)}:{n}: new {nome}(...) passa {quantos} argumento(s); o "
                    f"construtor aceita de {minimo} a {maximo} (CS7036)")


# ------------------------------------------- 8. membros `required` não inicializados
#
# As classes de linha das listas (LinhaEvolucao, LinhaPendencia, LinhaTaxa…) declaram os
# campos como `required` justamente para o compilador cobrar. Quando uma ganha um campo
# novo, a fábrica que a monta precisa preenchê-lo — e esquecer disso é CS9035, que sem
# Windows só aparece no CI.
#
# O padrão dominante aqui é a fábrica `public static Linha X De(...) => new() { … }`, com
# `new()` TIPADO PELO ALVO: o tipo não aparece ao lado do `new`, vem da assinatura. Por
# isso a varredura casa os dois formatos e lê o corpo contando chaves — o corpo tem
# `switch` com chaves dentro, e um `[^{}]*` pararia na primeira.

MEMBRO_REQUIRED = re.compile(r"public\s+required\s+[\w\?<>,\[\]\. ]+?\s+(\w+)\s*\{")
DECL_CLASSE = re.compile(r"\bclass\s+(\w+)")
# `new Tipo {` … ou `static Tipo Fabrica(...) => new() {`
NEW_EXPLICITO = re.compile(r"\bnew\s+(\w+)\s*\{")
NEW_ALVO = re.compile(r"\bstatic\s+(\w+)\s+\w+\s*\([^)]*\)\s*=>\s*new\s*\(\s*\)\s*\{")


def _corpo_chaves(texto: str, abre: int) -> str:
    """Do `{` em `abre` até a chave que o fecha, contando aninhamento."""
    nivel, i = 0, abre
    while i < len(texto):
        if texto[i] == "{":
            nivel += 1
        elif texto[i] == "}":
            nivel -= 1
            if nivel == 0:
                return texto[abre + 1 : i]
        i += 1
    return ""


ULTIMO_NOME = re.compile(r"(\w+)\s*$")


def _atribuidos(corpo: str) -> set[str]:
    """
    Nomes atribuídos no NÍVEL ZERO do inicializador (switch e lambda aninhados ficam de
    fora). Comentários saem antes: entre um campo e outro há linhas de `//` explicando a
    decisão, e elas entrariam no nome lido. Do trecho antes do `=` vale só o ÚLTIMO
    identificador — que é o do campo.
    """
    corpo = "\n".join(l.split("//", 1)[0] for l in corpo.splitlines())

    nomes, nivel, atual = set(), 0, ""
    for i, ch in enumerate(corpo):
        if ch in "{([":
            nivel += 1
        elif ch in "})]":
            nivel -= 1
        elif ch == "," and nivel == 0:
            atual = ""
            continue
        elif ch == "=" and nivel == 0:
            # Não confundir com ==, =>, !=, <=, >=.
            anterior = corpo[i - 1] if i else " "
            seguinte = corpo[i + 1] if i + 1 < len(corpo) else " "
            if anterior in "=!<>" or seguinte in "=>":
                atual += ch
                continue
            if (m := ULTIMO_NOME.search(atual)) is not None:
                nomes.add(m.group(1))
            atual = ""
            continue
        atual += ch
    return nomes


# Dois módulos têm uma classe `LinhaContato` cada, com campos diferentes. Sem compilador
# não há como resolver o nome; então vale o tipo declarado NO MESMO ARQUIVO e, na falta
# dele, só quando o nome é único na suíte inteira. Ambíguo é PULADO — checagem que chuta
# produz falso positivo, e falso positivo ensina a ignorar o verificador.
por_arquivo: dict[tuple[str, str], set[str]] = {}
por_nome: dict[str, list[set[str]]] = {}

for cs in _fontes():
    texto = cs.read_text(encoding="utf-8")
    for m in DECL_CLASSE.finditer(texto):
        abre = texto.find("{", m.end())
        if abre < 0:
            continue
        nomes = set(MEMBRO_REQUIRED.findall(_corpo_chaves(texto, abre)))
        if not nomes:
            continue
        por_arquivo[(str(cs), m.group(1))] = nomes
        por_nome.setdefault(m.group(1), []).append(nomes)


def _requeridos_de(arquivo: str, tipo: str) -> set[str] | None:
    if (arquivo, tipo) in por_arquivo:
        return por_arquivo[(arquivo, tipo)]
    definicoes = por_nome.get(tipo, [])
    return definicoes[0] if len(definicoes) == 1 else None


for cs in _fontes():
    texto = cs.read_text(encoding="utf-8")
    achados: list[tuple[int, str]] = []
    for m in NEW_EXPLICITO.finditer(texto):
        achados.append((m.end() - 1, m.group(1)))
    for m in NEW_ALVO.finditer(texto):
        achados.append((m.end() - 1, m.group(1)))

    for abre, tipo in achados:
        esperados = _requeridos_de(str(cs), tipo)
        if esperados is None:
            continue
        faltando = sorted(esperados - _atribuidos(_corpo_chaves(texto, abre)))
        if faltando:
            linha = texto.count("\n", 0, abre) + 1
            erros.append(
                f"{rel(cs)}:{linha}: inicializador de {tipo} não preenche "
                f"{', '.join(faltando)} (CS9035: membro required)")


# ---------------------------------------------------------------------- saída
for a in avisos:
    print(f"aviso: {a}")

if erros:
    print(f"\n{len(erros)} problema(s) encontrado(s):\n")
    for e in erros:
        print(f"  - {e}")
    sys.exit(1)

print(
    f"OK — {len(arvores)} XAML, {len(csprojs())} projetos e "
    f"{len(assinaturas)} construtores de ViewModel verificados."
)
