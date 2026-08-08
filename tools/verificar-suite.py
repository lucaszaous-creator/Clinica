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
     (CS9035) — as classes `Linha*` das listas são cheias deles;
  9. nenhuma variável de PADRÃO (`is { } x`, `out var x`) colide com outra declaração do
     mesmo nome no mesmo método (CS0136 — ver ARMADILHA do nome de padrão abaixo);
 10. nenhuma janela nasce maior que a tela do balcão (1366×768), e toda janela alta ou
     que cresce com o conteúdo tem rolagem (ver ARMADILHA da janela abaixo);
 11. nenhuma view ou janela escreve cor em hexadecimal ou tamanho de fonte em número —
     os dois saem do design system, senão a tela deixa de acompanhar o tema.

ARMADILHA da janela: o monitor da clínica é 1366×768, e descontadas a barra de tarefas e
a barra de título sobram ~696px de conteúdo. Uma janela declarada com 760 ou 800 de
altura NASCE com o rodapé — onde ficam Salvar e Cancelar — atrás da barra de tarefas. Pior
com a escala do Windows em 150%/175% (comum em notebook), que multiplica tudo: um diálogo
de 600px vira 900px físicos. Existe uma rede de segurança em runtime
(`AjusteJanela.Instalar`, chamada no `SuiteApp`), mas ela é o último recurso — o tamanho
declarado tem de caber sozinho, e o miolo tem de rolar.

ARMADILHA `Application` (CS0118): dentro de qualquer namespace `Clinica.*`, o nome
`Application` resolve para o NAMESPACE `Clinica.Application` — nunca para o tipo
`System.Windows.Application`. `public partial class App : Application` compila em
qualquer outro projeto WPF do mundo e falha aqui. Sempre `System.Windows.Application`.

ARMADILHA do construtor (CS7036): metade dos ViewModels de formulário é construída À MÃO
pela tela dona (`new PacienteEdicaoViewModel(escopos, id)`), porque precisa receber o id
no construtor e não passa pelo DI. Quando um deles ganha uma dependência nova, o DI se
vira sozinho e os pontos de construção manual NÃO — e o erro só aparece no build do
Windows, minutos depois. A checagem 7 compara a aridade dos dois lados.

ARMADILHA do nome de padrão (CS0136): `proposta.X is { } f` declara `f` no escopo do
MÉTODO, não no do `if`. Um `foreach (var f in ...)` depois, no mesmo método, tenta
declarar o mesmo nome num escopo interno e o build morre. O que confunde é que dois
`foreach (var c in ...)` seguidos são escopos IRMÃOS e continuam perfeitamente legais —
`FluxoCaixaViewModel.ExportarAsync` tem um par assim. Por isso a checagem 9 parte SÓ das
variáveis de padrão, e não de qualquer nome repetido.

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
    "src/Clinica.Modulo.Clinico",
    "src/Clinica.Recepcao",
    "src/Clinica.Financeiro",
    "src/Clinica.Gerente",
    "src/Clinica.Clinico",
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


# ---------------------------------------------------------------- checagem 9
# CS0136: variável de PADRÃO colidindo com outra declaração do mesmo método.
#
# Terceira falha de CI desta natureza (as checagens 7 e 8 nasceram das duas anteriores):
# o erro só aparece no runner Windows, minutos depois do push, e a correção é de um nome.
#
# A checagem parte SÓ das variáveis de padrão porque é aí que está a assimetria:
# `is { } f` entra no escopo do MÉTODO, enquanto `foreach (var f in ...)` entra num escopo
# próprio. Dois `foreach` com o mesmo nome são irmãos e são legais — acusá-los seria falso
# positivo, e falso positivo ensina a ignorar o verificador.

PADRAO_VAR = re.compile(
    r"\bis\s+(?:not\s+)?(?:\{[^{}]*\}|[A-Za-z_][\w.]*(?:<[^<>()]*>)?\??)\s+([a-z_]\w*)\b")
OUT_VAR = re.compile(r"\bout\s+(?:var|[A-Za-z_][\w.]*\??)\s+([a-z_]\w*)\b")
CORPO_DE_TIPO = re.compile(r"\b(?:class|record|struct|interface|enum|namespace)\b[^{;=]*$")
PALAVRAS = {"null", "true", "false", "not", "and", "or"}


def _sem_texto(t: str) -> str:
    """Apaga strings, chars e comentários — chave e aspas dentro deles furam a contagem."""
    saida = list(t)
    i, n = 0, len(t)
    while i < n:
        c = t[i]
        if c in '"\'':
            j = i + 1
            while j < n and t[j] != c:
                if t[j] == "\\":
                    j += 1
                j += 1
            for k in range(i, min(j + 1, n)):
                if t[k] != "\n":
                    saida[k] = " "
            i = j + 1
            continue
        if c == "/" and i + 1 < n and t[i + 1] == "/":
            j = t.find("\n", i)
            j = n if j < 0 else j
            for k in range(i, j):
                saida[k] = " "
            i = j
            continue
        if c == "/" and i + 1 < n and t[i + 1] == "*":
            j = t.find("*/", i)
            j = n if j < 0 else j + 2
            for k in range(i, j):
                if t[k] != "\n":
                    saida[k] = " "
            i = j
            continue
        i += 1
    return "".join(saida)


def _metodo_de(t: str, pos: int) -> tuple[int, int] | None:
    """O bloco { } mais interno que contém pos, ou None se for o corpo de um TIPO.

    A guarda do tipo existe porque membro com corpo de EXPRESSÃO (`=> ...;`) não tem
    chaves: sem ela a busca sobe até o corpo da classe e acusa colisão com o homônimo
    de qualquer outro método."""
    pilha: list[int] = []
    for i, c in enumerate(t):
        if c == "{":
            pilha.append(i)
        elif c == "}":
            if not pilha:
                continue
            a = pilha.pop()
            if a < pos < i:
                return None if CORPO_DE_TIPO.search(t[max(0, a - 400):a]) else (a, i)
    return None


for cs in _fontes():
    bruto = cs.read_text(encoding="utf-8")
    texto = _sem_texto(bruto)
    for regex in (PADRAO_VAR, OUT_VAR):
        for m in regex.finditer(texto):
            nome = m.group(1)
            if nome in PALAVRAS:
                continue
            escopo = _metodo_de(texto, m.start(1))
            if escopo is None:
                continue
            abre, fecha = escopo
            corpo = texto[abre:fecha]
            desloc = m.start(1) - abre

            colide = re.search(
                rf"foreach\s*\(\s*(?:var|[A-Za-z_][\w.<>,\s]*?)\s+{nome}\s+in\b", corpo) is not None
            if not colide:
                colide = any(
                    abs(o.start() - desloc) > 3
                    for o in re.finditer(rf"\bvar\s+{nome}\s*=", corpo))
            if colide:
                linha = bruto.count("\n", 0, m.start(1)) + 1
                erros.append(
                    f"{rel(cs)}:{linha}: a variável de padrão '{nome}' entra no escopo do "
                    f"método e colide com outra declaração do mesmo nome "
                    f"(CS0136) — renomeie uma das duas")


# ------------------------------------------- 10. janela maior que a tela do balcão
# O monitor da clínica é 1366×768. Descontadas a barra de tarefas (~40px) e a barra de
# título da janela (~32px), sobram ~696px de conteúdo. Janela declarada mais alta que
# isso NASCE com o rodapé — onde ficam Salvar e Cancelar — atrás da barra de tarefas.
#
# Não é hipótese: o app de faturamento carrega desde cedo um `AjustarParaTela` cujo
# comentário diz o sintoma ("a última coluna das tabelas ficava passando da tela"), e a
# suíte foi construída sem ele. O ajuste agora existe (`AjusteJanela`, ligado no
# `SuiteApp`), mas ele é a rede de segurança: o tamanho declarado tem de caber sozinho,
# senão toda abertura no balcão começa com a janela sendo redimensionada na cara do
# usuário.
#
# ⚠️ A checagem NÃO exige MaxHeight em quem usa SizeToContent: altura fixa em XAML não
# conhece o monitor. Quem cresce com o conteúdo precisa é de ROLAGEM — é isso que se
# confere aqui.
ALTURA_UTIL = 768 - 40  # área útil do monitor do balcão
MOLDURA = 32            # barra de título

def _nome(el: ET.Element) -> str:
    """Nome do elemento sem o namespace do XAML."""
    return el.tag.split("}")[-1]


for arq, raiz in arvores.items():
    if _nome(raiz) != "Window":
        continue

    def numero(atributo: str) -> float | None:
        valor = raiz.get(atributo)
        try:
            return float(valor) if valor else None
        except ValueError:
            return None

    altura = numero("Height")
    if altura is not None and altura + MOLDURA > ALTURA_UTIL:
        erros.append(
            f"{rel(arq)}: Height={altura:.0f} + barra de título passa da área útil de "
            f"1366×768 ({ALTURA_UTIL}px) — a janela nasce com o rodapé atrás da barra "
            f"de tarefas")

    largura = numero("Width")
    if largura is not None and largura > 1366:
        erros.append(f"{rel(arq)}: Width={largura:.0f} é maior que o monitor do balcão (1366)")

    # Diálogo que cresce com o conteúdo (ou é alto) precisa de rolagem: com a escala do
    # Windows em 150%/175% — comum em notebook — um formulário de 600px vira 900px
    # físicos, e sem ScrollViewer o que sobra é cortado, não rolado.
    #
    # `ListBox`/`ListView`/`DataGrid` contam como rolagem: o template deles já traz um
    # ScrollViewer, e é assim que a venda de pacote (lista de pacientes ocupando o miolo)
    # se vira sem um ScrollViewer escrito à mão.
    ROLAM = {"ScrollViewer", "ListBox", "ListView", "DataGrid"}
    cresce = (raiz.get("SizeToContent") or "").find("Height") >= 0
    alto = altura is not None and altura >= 400
    if (cresce or alto) and not any(_nome(e) in ROLAM for e in raiz.iter()):
        erros.append(
            f"{rel(arq)}: janela que cresce com o conteúdo (ou alta) sem nenhum "
            f"ScrollViewer — em escala 150% o rodapé sai da tela cortado")


# ------------------------------------------ 11. cor e tamanho de fonte fora dos tokens
# O design system existe para a suíte inteira mudar de aparência num lugar só. Cor em
# hexadecimal e tamanho de fonte em número escapam disso em silêncio: a tela continua
# bonita hoje e deixa de acompanhar o tema amanhã.
#
# Só as VIEWS e JANELAS entram — o próprio design system (Styles/) é onde os números
# podem morar.
COR_CRUA = re.compile(r'(Foreground|Background|BorderBrush|Fill|Stroke)="(#[0-9A-Fa-f]{3,8})"')
FONTE_CRUA = re.compile(r'FontSize="(\d+(?:\.\d+)?)"')

for arq in xamls():
    if "Styles" in arq.parts:
        continue
    texto = arq.read_text(encoding="utf-8")

    for m in COR_CRUA.finditer(texto):
        linha = texto.count("\n", 0, m.start()) + 1
        erros.append(
            f"{rel(arq)}:{linha}: {m.group(1)}=\"{m.group(2)}\" escrito à mão — use uma "
            f"chave Brush.* do design system")

    for m in FONTE_CRUA.finditer(texto):
        linha = texto.count("\n", 0, m.start()) + 1
        erros.append(
            f"{rel(arq)}:{linha}: FontSize=\"{m.group(1)}\" numérico — use uma chave "
            f"Fonte.* do design system")


# --------------------------------------------------------------- checagem 12
# TIPO PÚBLICO DUPLICADO dentro do MESMO projeto e namespace.
#
# Nasceu de uma falha de CI real: `LinhaApuracao` foi criada em TaxasViewModel sem
# ninguém notar que RepassesViewModel já tinha uma com o mesmo nome — CS0101, e só o
# runner Windows contou. É um erro barato de cometer numa suíte em que cada tela
# declara suas próprias linhas de lista, todas no mesmo namespace por módulo.
#
# O agrupamento é POR PROJETO, e isso importa: `Clinica.Desktop` e `Clinica.Desktop.Shell`
# repetem de propósito o namespace `Clinica.Desktop.Controls` (o design system duplicado é
# o débito assumido da arquitetura multi-exe). São assemblies diferentes, então não colidem
# — agrupar só por namespace acusaria oito falsos positivos permanentes.
TIPO_PUBLICO = re.compile(
    r"^public\s+(?:sealed\s+|abstract\s+|static\s+|partial\s+)*"
    r"(?:class|record|struct|enum|interface)\s+(\w+)", re.M)

NAMESPACE_ARQUIVO = re.compile(r"^namespace\s+([\w.]+)\s*;", re.M)

_tipos: dict[tuple[str, str, str], set[str]] = {}

for arq in RAIZ.joinpath("src").rglob("*.cs"):
    if "/obj/" in arq.as_posix() or "/bin/" in arq.as_posix():
        continue

    texto = arq.read_text(encoding="utf-8", errors="ignore")
    ns = NAMESPACE_ARQUIVO.search(texto)
    if ns is None:
        continue

    # src/<Projeto>/... — o projeto é o primeiro nível abaixo de src.
    relativo = arq.relative_to(RAIZ / "src")
    projeto = relativo.parts[0]

    for m in TIPO_PUBLICO.finditer(texto):
        _tipos.setdefault((projeto, ns.group(1), m.group(1)), set()).add(rel(arq))

for (projeto, ns, tipo), arquivos in sorted(_tipos.items()):
    # `partial` legítimo repete o tipo no MESMO arquivo (XAML + code-behind não entram
    # aqui, que é só .cs); dois arquivos distintos é colisão.
    if len(arquivos) > 1:
        erros.append(
            f"{projeto}: o tipo público '{ns}.{tipo}' está declarado em "
            f"{' e '.join(sorted(arquivos))} — CS0101 no build")


# --------------------------------------------------------------- checagem 13
# COMANDO LIGADO NO XAML QUE NÃO EXISTE EM VIEWMODEL NENHUM.
#
# É a falha mais silenciosa da suíte, e nenhuma das outras redes a pega: o XAML compila,
# o botão aparece bonito na tela, e o clique não faz nada. O WPF resolve `{Binding}` em
# tempo de execução e engole o erro num canal de trace que ninguém lê.
#
# Acontece sozinho: `[RelayCommand]` gera `SalvarCommand` a partir de `SalvarAsync`, então
# renomear o método para `GravarAsync` renomeia o comando e deixa o XAML apontando para o
# nome antigo. O compilador fica verde nos dois lados.
#
# A checagem é DELIBERADAMENTE frouxa quanto a QUEM é o dono: ela não tenta descobrir qual
# ViewModel é o DataContext de cada tela — isso exigiria resolver `DataContext` em runtime,
# herança de contexto e `d:DataContext`, e erraria. Ela junta todos os comandos que existem
# na suíte e acusa o que não existe em lugar NENHUM. Assim não há falso positivo: um nome
# que não existe em nenhum ViewModel não pode estar certo em tela nenhuma.
COMANDO_LIGADO = re.compile(r"\{Binding\s+(?:Path=)?(\w+Command)\b")
RELAY_COMMAND = re.compile(
    r"\[RelayCommand[^\]]*\]\s*(?:/// .*\n\s*)*"          # o atributo (e doc no meio)
    r"(?:public|private|protected|internal)?\s*"
    r"(?:static\s+)?(?:async\s+)?[\w<>?\[\], .]+?\s+(\w+)\s*\(", re.M)
COMANDO_EXPLICITO = re.compile(r"(?:ICommand|RelayCommand|AsyncRelayCommand)[\w<>?]*\s+(\w+Command)\b")

_comandos: set[str] = set()

for arq in RAIZ.joinpath("src").rglob("*.cs"):
    if "/obj/" in arq.as_posix() or "/bin/" in arq.as_posix():
        continue

    texto = arq.read_text(encoding="utf-8", errors="ignore")

    # [RelayCommand] sobre `Salvar`/`SalvarAsync` gera a propriedade `SalvarCommand`:
    # o gerador tira o sufixo `Async` e acrescenta `Command`.
    for m in RELAY_COMMAND.finditer(texto):
        nome = m.group(1)
        base = nome[:-5] if nome.endswith("Async") else nome
        _comandos.add(f"{base}Command")

    # Comandos escritos à mão (o faturamento tem alguns).
    _comandos.update(COMANDO_EXPLICITO.findall(texto))


def _comandos_do_arquivo(caminho: Path) -> set[str]:
    """Comandos declarados num .cs específico."""
    texto = caminho.read_text(encoding="utf-8", errors="ignore")
    achados: set[str] = set()
    for m in RELAY_COMMAND.finditer(texto):
        nome = m.group(1)
        achados.add(f"{nome[:-5] if nome.endswith('Async') else nome}Command")
    achados.update(COMANDO_EXPLICITO.findall(texto))
    return achados


for arq in RAIZ.joinpath("src").rglob("*.xaml"):
    if "/obj/" in arq.as_posix() or "/bin/" in arq.as_posix():
        continue

    texto = arq.read_text(encoding="utf-8", errors="ignore")

    # `XView.xaml` ↔ `ViewModels/XViewModel.cs` é convenção de 53 em 53 telas da suíte,
    # e onde ela vale dá para exigir que o comando exista NO DONO — que é o que pega o
    # caso mais comum: renomear `ExportarAsync` e esquecer o XAML. Sem isso a checagem
    # só acusaria nomes inexistentes na suíte inteira, e `ExportarCommand` existe em
    # outras dez telas.
    dono = arq.parent.parent / "ViewModels" / (arq.stem + "Model.cs")
    if arq.name.endswith("View.xaml") and dono.exists():
        # Uma View pode hospedar o VM de outra tela (o painel embute cartões), então o
        # universo do dono é somado ao dos VMs que ele expõe como propriedade.
        proprios = _comandos_do_arquivo(dono)
        texto_dono = dono.read_text(encoding="utf-8", errors="ignore")
        for outro in re.findall(r"\b(\w+ViewModel)\s+\w+\s*\{\s*get", texto_dono):
            for cand in RAIZ.joinpath("src").rglob(f"{outro}.cs"):
                if "/obj/" not in cand.as_posix():
                    proprios |= _comandos_do_arquivo(cand)

        for m in COMANDO_LIGADO.finditer(texto):
            if m.group(1) not in proprios:
                erros.append(
                    f"{rel(arq)}: liga '{m.group(1)}', que {dono.stem} não declara — "
                    f"o botão aparece e o clique não faz nada "
                    f"(o WPF engole o erro em runtime)")
        continue

    # Janelas e dicionários não seguem a convenção de nome (muitas usam code-behind ou o
    # VM de outra tela). Para elas fica o critério frouxo: existe em ALGUM ViewModel?
    for m in COMANDO_LIGADO.finditer(texto):
        if m.group(1) not in _comandos:
            erros.append(
                f"{rel(arq)}: liga '{m.group(1)}', que não existe em ViewModel nenhum — "
                f"o botão aparece e o clique não faz nada (o WPF engole o erro em runtime)")


# --------------------------------------------------------------- checagem 14
# TELA QUE NÃO MOSTRA QUE ESTÁ CARREGANDO.
#
# Vinte e oito ViewModels da suíte mantêm uma propriedade `Carregando` e apenas UMA tela
# a exibia. Não é detalhe estético: o banco é remoto e, com o retry ligado, uma consulta
# numa conexão ruim leva segundos — e durante esse tempo a tela mostra uma lista vazia,
# visualmente IDÊNTICA a "não existe nada". A secretária conclui que o dia não tem
# ninguém marcado e age a partir disso.
#
# A correção é o controle `EstadoDaTela` (parcela 35), que resolve os três estados numa
# ordem que é a regra: carregando vence tudo, falha vem ANTES de vazio, e vazio só quando
# a leitura deu certo.
#
# Nasceu como AVISO porque as 25 telas migraram aos poucos, e travar o push de quem
# estava mexendo em outra coisa faria a checagem ser contornada. Com a migração
# terminada virou ERRO: agora não há dívida a tolerar, e tela nova sem os três estados é
# regressão — a única hora em que dá para exigir isso barato é enquanto está tudo em dia.
_pendentes: list[str] = []

for vm in sorted(RAIZ.glob("src/Clinica.Modulo.*/ViewModels/*ViewModel.cs")):
    if "_carregando" not in vm.read_text(encoding="utf-8", errors="ignore"):
        continue

    view = vm.parent.parent / "Views" / (vm.stem.replace("ViewModel", "View") + ".xaml")
    if not view.exists():
        continue  # ViewModel de janela ou de item, sem tela própria

    if "EstadoDaTela" not in view.read_text(encoding="utf-8", errors="ignore"):
        _pendentes.append(view.stem)

for _t in sorted(_pendentes):
    erros.append(
        f"{_t}: o ViewModel tem 'Carregando' e a tela não o mostra — lista vazia por "
        f"espera fica igual a lista vazia por não haver nada. Use ctrl:EstadoDaTela.")


# --------------------------------------------------------------- checagem 15
# TAMANHO DE JANELA NO FATURAMENTO (`Clinica.Desktop`).
#
# O faturamento está CONGELADO e por isso fica fora das outras checagens: exigir dele os
# tokens do design system ou os três estados de lista acusaria dezenas de coisas que
# ninguém vai mexer, e uma lista de erros que não se pretende corrigir treina todo mundo
# a ignorar o verificador.
#
# Tamanho de janela é a exceção, e por um motivo prático: é a única regra aqui cujo
# defeito o usuário sente TODO DIA, sem nada a ver com arquitetura — janela que nasce
# com o rodapé atrás da barra de tarefas, ou diálogo que cresce com o conteúdo e corta os
# botões em vez de rolar. A auditoria achou dez casos assim no app que fatura a clínica.
#
# Só as regras da checagem 10 valem aqui. Nada de design system, nada de estado de lista.
CONGELADO = RAIZ / "src" / "Clinica.Desktop"

for arq in sorted(CONGELADO.rglob("*.xaml")):
    try:
        raiz_c = ET.parse(arq).getroot()
    except ET.ParseError as e:
        erros.append(f"{rel(arq)}: XAML malformado — {e}")
        continue

    if _nome(raiz_c) != "Window":
        continue

    def _num(atributo: str, no=raiz_c) -> float | None:
        valor = no.get(atributo)
        try:
            return float(valor) if valor else None
        except ValueError:
            return None

    altura_c = _num("Height")
    if altura_c is not None and altura_c + MOLDURA > ALTURA_UTIL:
        erros.append(
            f"{rel(arq)}: Height={altura_c:.0f} + barra de título passa da área útil de "
            f"1366×768 ({ALTURA_UTIL}px) — nasce com o rodapé atrás da barra de tarefas")

    largura_c = _num("Width")
    if largura_c is not None and largura_c > 1366:
        erros.append(f"{rel(arq)}: Width={largura_c:.0f} é maior que o monitor do balcão (1366)")

    # MinHeight/MinWidth são piores que Height/Width: o usuário consegue redimensionar
    # uma janela grande demais, mas não consegue passar do mínimo. Janela com mínimo
    # maior que a tela fica permanentemente com parte fora, e não há o que fazer.
    minh = _num("MinHeight")
    if minh is not None and minh + MOLDURA > ALTURA_UTIL:
        erros.append(
            f"{rel(arq)}: MinHeight={minh:.0f} passa da área útil — o usuário não "
            f"consegue diminuir abaixo disso, então parte da janela fica fora para sempre")

    minw = _num("MinWidth")
    if minw is not None and minw > 1366:
        erros.append(
            f"{rel(arq)}: MinWidth={minw:.0f} passa do monitor do balcão — "
            f"barra de rolagem horizontal permanente")

    ROLAM_C = {"ScrollViewer", "ListBox", "ListView", "DataGrid"}
    cresce_c = (raiz_c.get("SizeToContent") or "").find("Height") >= 0
    alto_c = altura_c is not None and altura_c >= 400
    if (cresce_c or alto_c) and not any(_nome(e) in ROLAM_C for e in raiz_c.iter()):
        erros.append(
            f"{rel(arq)}: janela que cresce com o conteúdo (ou alta) sem nenhum "
            f"ScrollViewer — em escala 150% o rodapé sai da tela cortado")


# --------------------------------------------------------------- checagem 16
# O PISO DA JANELA. Vale para os quatro apps.
#
# A checagem 15 olha o TETO: janela que nasce maior que o monitor. Este é o outro lado, e
# é o que o usuário alcança sozinho — janela redimensionável SEM `MinWidth` encolhe até o
# mínimo do WPF (perto de 120px), e aí não há layout que resista: campo, botão e mensagem
# saem cortados pela direita. Diferente do teto, o piso não tem conserto pelo usuário:
# quem arrastou até lá não sabe qual largura devolve a tela ao normal.
#
# `SizeToContent="Height"` resolve a altura sozinho, então só a largura é exigida ali.
for arq in sorted(RAIZ.glob("src/*/**/*.xaml")):
    try:
        raiz_j = ET.parse(arq).getroot()
    except ET.ParseError:
        continue  # a checagem 15 já reclamou

    if _nome(raiz_j) != "Window":
        continue
    if (raiz_j.get("ResizeMode") or "CanResize") in ("NoResize",):
        continue  # não dá para arrastar: a largura declarada já é o piso

    def _tem(atributo: str) -> bool:
        return (raiz_j.get(atributo) or "").strip() != ""

    if not _tem("MinWidth"):
        erros.append(
            f"{rel(arq)}: janela redimensionável sem MinWidth — o usuário arrasta a "
            f"borda até o conteúdo ficar cortado, e não há como saber qual largura "
            f"desfaz isso")

    # Altura automática não precisa de piso; altura fixa precisa.
    cresce_alt = "Height" in (raiz_j.get("SizeToContent") or "")
    if _tem("Height") and not cresce_alt and not _tem("MinHeight"):
        erros.append(
            f"{rel(arq)}: janela redimensionável de altura fixa sem MinHeight — "
            f"encolhida na vertical, o rodapé com os botões é o primeiro a sair")


# --------------------------------------------------------------- checagem 17
# MENSAGEM QUE CORRE PARA FORA DA TELA.
#
# `StackPanel Orientation="Horizontal"` dá a cada filho a largura que ele PEDE e nunca
# dobra a linha. Um `TextBlock` sem `TextWrapping` pede a linha inteira de uma vez — e
# quando o texto vem de binding (`Mensagem`, `Erro`, `Aviso`) o comprimento é do dado, não
# do leiaute: uma mensagem de erro do Postgres passa de 200 caracteres.
#
# O resultado não é feio, é MUDO: a mensagem sai pela direita e a pessoa não lê justamente
# o texto que explica por que a ação falhou. O projeto já decidiu que erro se mostra inline
# (CLAUDE.md, dois canais de feedback) — inline que não cabe não é feedback.
#
# Só bindings de mensagem entram: rótulo fixo curto ao lado de um campo é o uso legítimo
# do StackPanel horizontal, e acusá-lo encheria a saída de ruído.
#
# Cobre TAMBÉM o faturamento congelado, pela mesma razão da checagem 15: a regra não tem
# nada a ver com arquitetura, e os três casos reais estavam justamente lá. Checagem que
# não alcança o lugar onde o defeito estava é checagem que passa sozinha.
PALAVRAS_DE_MENSAGEM = ("Mensagem", "Erro", "Aviso", "Procedencia", "Justificativa")

arvores_com_congelado: dict[Path, ET.Element] = dict(arvores)
for _arq in sorted(CONGELADO.rglob("*.xaml")):
    try:
        arvores_com_congelado[_arq] = ET.parse(_arq).getroot()
    except ET.ParseError:
        continue  # a checagem 15 já reclamou

for arq, raiz_m in arvores_com_congelado.items():
    for painel in raiz_m.iter():
        # WrapPanel entra junto: ele dobra ENTRE os filhos, nunca DENTRO de um. Um texto
        # sem quebra maior que a linha estoura ali do mesmo jeito — trocar o painel
        # resolve a barra, não o texto.
        horizontal = (
            _nome(painel) == "StackPanel" and painel.get("Orientation") == "Horizontal"
        ) or (
            _nome(painel) == "WrapPanel" and (painel.get("Orientation") or "Horizontal") == "Horizontal"
        )
        if not horizontal:
            continue
        for filho in painel:
            if _nome(filho) != "TextBlock":
                continue
            texto = filho.get("Text") or ""
            if not texto.startswith("{"):
                continue  # literal: o autor vê o tamanho na hora de escrever
            if not any(p in texto for p in PALAVRAS_DE_MENSAGEM):
                continue
            if filho.get("TextWrapping") or filho.get("MaxWidth"):
                continue
            if "TextoSuave" in (filho.get("Style") or ""):
                continue  # o estilo já quebra linha
            erros.append(
                f"{rel(arq)}: {texto} num {_nome(painel)} horizontal sem TextWrapping — "
                f"a linha não dobra dentro do texto, então a mensagem sai pela direita e "
                f"não se lê. Use TextWrapping + MaxWidth no texto (e WrapPanel na barra, "
                f"se ela também não couber)")


# --------------------------------------------------------------- checagem 18
# MIGRATION SÓ ADITIVA — a regra que protege o app CONGELADO.
#
# O faturamento está em produção e aplica migrations na abertura. Enquanto houver versões
# diferentes instaladas na clínica, a regra do projeto (docs/arquitetura-multi-exe.md) é
# que migration nova só ACRESCENTA: coluna nova o EF ignora sem problema; renomear ou
# remover algo que o faturamento usa derruba a clínica no dia seguinte, e o erro aparece
# na máquina de quem fatura, não na de quem programou.
#
# A regra existia só em prosa. Aqui ela passa a ser conferida.
#
# Só vale das migrations NOVAS para a frente: o histórico antigo tem alterações legítimas,
# feitas antes de o app estar em campo, e acusá-las encheria a saída de ruído que ninguém
# vai corrigir — o que ensina todo mundo a ignorar o verificador.
MIGRATIONS = RAIZ / "src" / "Clinica.Infrastructure" / "Migrations"

# Última migration anterior à parcela 36. Daqui para cima, tudo tem de ser aditivo.
# Mover esta âncora para frente é decisão consciente, e pede uma release do faturamento.
ANCORA_ADITIVA = "20260801010000"

# Operações que mexem no que JÁ EXISTE. `AddColumn`, `CreateTable` e `CreateIndex` ficam
# de fora de propósito: são exatamente o que a regra permite.
DESTRUTIVAS = (
    "DropColumn", "DropTable", "DropForeignKey", "DropIndex", "DropPrimaryKey",
    "RenameColumn", "RenameTable", "RenameIndex", "AlterColumn", "AlterTable",
)


def _corpo_do_up(texto: str) -> str:
    """Só o Up(): o Down() desfaz, e desfazer é destrutivo por definição."""
    i = texto.find("void Up(MigrationBuilder")
    if i < 0:
        return ""
    j = texto.find("void Down(MigrationBuilder", i)
    return texto[i:] if j < 0 else texto[i:j]


if MIGRATIONS.is_dir():
    for arq in sorted(MIGRATIONS.glob("*.cs")):
        if arq.name.endswith(".Designer.cs") or "Snapshot" in arq.name:
            continue

        carimbo = arq.name.split("_", 1)[0]
        if not carimbo.isdigit() or carimbo <= ANCORA_ADITIVA:
            continue

        corpo = _corpo_do_up(arq.read_text(encoding="utf-8"))
        achadas = sorted({d for d in DESTRUTIVAS if f".{d}(" in corpo})

        if achadas:
            erros.append(
                f"{rel(arq)}: migration NÃO aditiva ({', '.join(achadas)}) — o faturamento "
                f"está em produção e aplica migrations na abertura. Enquanto houver versões "
                f"diferentes em campo, migration nova só acrescenta. Ver "
                f"docs/arquitetura-multi-exe.md.")


# --------------------------------------------------------------- checagem 19
# CHAVE DE NAVEGAÇÃO SEM ITEM DE MENU.
#
# A navegação da suíte é por STRING: `NavegacaoSuite.Ir(chave)` faz o shell procurar a
# chave na lista de itens do módulo (`ShellViewModel.IrPara`) e, se não achar, NÃO FAZ
# NADA — sem erro, sem log, sem exceção. O botão simplesmente não responde.
#
# Isto não é hipótese: na 4ª rodada da parcela 37 as cinco telas clínicas saíram do menu
# do Consultório para virar abas da tela do paciente, e saíram junto da LISTA. Resultado:
# "Atender" na fila do dia, os atalhos da carteira e o painel da direção pararam de abrir
# qualquer coisa, de uma vez. O compilador de sombra passou (é string), o verificador
# passou (é C#, não XAML) e os 1023 testes passaram (nenhum monta a sidebar).
#
# A correção foi `ItemMenuModulo.Oculto` — navegável sem ocupar linha no menu. Esta
# checagem existe para a próxima vez: toda chave usada em `NavegacaoSuite.Ir/Existe`
# precisa estar declarada como `Chave = ...` em algum `ItemMenuModulo` do módulo dono.
# Só o que é CONSTANTE de chave: `Ir(ModuloX.ChaveY)` ou `Ir("literal")`. O painel da
# direção navega por variável (`Ir(alerta.Destino)`), e aí o destino só se conhece em
# tempo de execução — cobri-lo aqui daria ruído em cima de código correto.
CHAVE_NAV = re.compile(
    r"""NavegacaoSuite\.(?:Ir|Existe)\(\s*(?:[A-Za-z0-9_]+\.)?(Chave[A-Za-z0-9_]*)\s*\)""")
CHAVE_NAV_LITERAL = re.compile(r"""NavegacaoSuite\.(?:Ir|Existe)\(\s*"([^"]+)"\s*\)""")
CHAVE_ITEM = re.compile(r"Chave\s*=\s*(?:[A-Za-z0-9_]+\.)?([A-Za-z0-9_]+)\s*,")

for modulo in sorted(RAIZ.glob("src/Clinica.Modulo.*")) + sorted(RAIZ.glob("src/Clinica.Recepcao")):
    if not modulo.is_dir():
        continue

    fontes = list(modulo.rglob("*.cs"))
    if not fontes:
        continue

    texto = "\n".join(f.read_text(encoding="utf-8") for f in fontes)

    # As chaves declaradas como item de menu DESTE módulo.
    declaradas = set(CHAVE_ITEM.findall(texto))
    # Literais também valem (nem todo módulo usa const).
    declaradas |= set(re.findall(r'Chave\s*=\s*"([^"]+)"', texto))

    for arq in fontes:
        corpo = arq.read_text(encoding="utf-8")
        for chave in CHAVE_NAV.findall(corpo) + CHAVE_NAV_LITERAL.findall(corpo):
            # Chave de OUTRO módulo (ChavesSuite.X) é contrato entre módulos: quem a
            # publica é o dono, e este módulo não tem como declará-la.
            if f"ChavesSuite.{chave}" in corpo:
                continue
            if chave in declaradas:
                continue
            erros.append(
                f"{rel(arq)}: navega para `{chave}`, que não é `Chave` de nenhum "
                f"ItemMenuModulo deste módulo — o shell procura a chave na lista de itens "
                f"e, sem achar, o botão não faz NADA. Declare o item (use `Oculto = true` "
                f"se a tela não deve aparecer na sidebar).")


# --------------------------------------------------------------- checagem 20
# ComboBox amarrado a lista de ENUM sem rótulo: o WPF chama ToString() no valor e o
# identificador do programador vai para a tela — "PedidoExame", "RelatorioEvolucao",
# "CartaoCredito", "PercentualDaReceita".
#
# O cliente encontrou isto em produção, na tela de documento clínico, e a varredura
# mostrou que eram 10 enums em 16 telas. É defeito barato de cometer (basta esquecer um
# atributo) e caro de achar: o build passa, o teste passa, e só quem abre a tela vê.
#
# A checagem casa o ItemsSource com o TIPO declarado no ViewModel e só reclama quando o
# tipo é um enum do domínio — lista de string ("Este mês", "Últimos 90 dias") não precisa
# de rótulo nenhum.
COMBO_SEM_ROTULO = re.compile(
    r"""<ComboBox\b((?:(?!</?ComboBox|/>|>).)*?)ItemsSource\s*=\s*"\{Binding\s+"""
    r"""([A-Za-z0-9_.]+)[^"]*"((?:(?!</?ComboBox|/>|>).)*?)/?>""",
    re.S,
)
COLECAO_TIPADA = re.compile(
    r"(?:IReadOnlyList|IList|List|ObservableCollection|IEnumerable)<\s*([A-Za-z0-9_]+)\s*>"
    r"\s+(?:_)?(\w+)"
)


def _enums_do_dominio() -> set[str]:
    achados: set[str] = set()
    dominio = RAIZ / "src" / "Clinica.Domain"
    if not dominio.exists():
        return achados
    for arq in dominio.rglob("*.cs"):
        achados.update(re.findall(r"\benum\s+([A-Za-z0-9_]+)", arq.read_text(encoding="utf-8")))
    return achados


def _tipos_das_colecoes() -> dict[str, set[str]]:
    tipos: dict[str, set[str]] = {}
    for arq in RAIZ.rglob("src/**/*.cs"):
        if "/obj/" in str(arq) or "/bin/" in str(arq):
            continue
        for tipo, nome in COLECAO_TIPADA.findall(arq.read_text(encoding="utf-8")):
            chave = nome[0].upper() + nome[1:]
            tipos.setdefault(chave, set()).add(tipo)
    return tipos


_ENUMS = _enums_do_dominio()
_TIPOS = _tipos_das_colecoes()

for arq in sorted(RAIZ.rglob("src/**/*.xaml")):
    if "/obj/" in str(arq) or "/bin/" in str(arq):
        continue
    corpo = arq.read_text(encoding="utf-8")
    for achado in COMBO_SEM_ROTULO.finditer(corpo):
        antes, prop, depois = achado.groups()
        tag = antes + depois
        if "ItemTemplate" in tag or "DisplayMemberPath" in tag:
            continue

        # O ItemTemplate também pode vir como ELEMENTO DE PROPRIEDADE, dentro do ComboBox —
        # `<ComboBox.ItemTemplate><DataTemplate>…`. A regex acima só enxerga os atributos da
        # tag de abertura (ela para no `>`), então sem esta segunda leitura a checagem
        # acusava um ComboBox que TEM rótulo. Falso positivo é o que faz alguém desligar a
        # ferramenta, e aí ela deixa de pegar o defeito de verdade.
        fecha = corpo.find("</ComboBox>", achado.end())
        dentro = corpo[achado.end(): fecha if fecha != -1 else achado.end()]
        if "ComboBox.ItemTemplate" in dentro:
            continue

        nome = prop.split(".")[-1]
        enums = _TIPOS.get(nome, set()) & _ENUMS
        if not enums:
            continue

        # O faturamento tem design system PRÓPRIO (ele não referencia o shell), então o
        # remédio lá é o conversor dele, não a chave da suíte.
        do_faturamento = "Clinica.Desktop/" in str(arq).replace("\\", "/")
        remedio = (
            'ItemTemplate com Converter={StaticResource EnumDescricao}'
            if do_faturamento
            else 'ItemTemplate="{StaticResource ItemRotuloEnum}"'
        )
        erros.append(
            f"{rel(arq)}: o ComboBox de `{nome}` lista o enum "
            f"`{'/'.join(sorted(enums))}` sem rótulo — o WPF chama ToString() e o nome do "
            f"enum vai para a tela (\"PedidoExame\"). Use {remedio}."
        )


# --------------------------------------------------------------- checagem 21
# BOTÃO ACESO QUE NÃO FAZ NADA.
#
# O cliente clicou em "Receita" na tela de Prescrições e não abriu janela nenhuma: o
# comando começava com `if (_pacienteId == 0) return;` e voltava CALADO, enquanto o botão
# continuava aceso porque o `IsEnabled` só olhava a permissão. Quem clica e não vê nada
# acontecer conclui que o sistema quebrou — e não tem como saber que faltava escolher
# alguém.
#
# A regra do projeto já dizia isto para PERMISSÃO ("duas barreiras: IsEnabled explica,
# Exigir impede"). Esta checagem estende a mesma exigência às demais PRÉ-CONDIÇÕES.
#
# Só reclama quando as duas coisas valem ao mesmo tempo:
#   (a) o comando tem guarda de saída MUDA — `return;` sem escrever mensagem antes; e
#   (b) a guarda olha ESTADO DO VIEWMODEL, não um parâmetro (um `if (linha is null)` num
#       botão de linha de lista nunca dispara, e apontá-lo seria ruído); e
#   (c) algum Button liga esse comando SEM `IsEnabled`.
COMANDO_COM_GUARDA = re.compile(
    r"\[RelayCommand[^\]]*\]\s*(?:public|private|internal|protected)?[^\n]*?"
    r"\b(\w+)Async?\s*\(([^)]*)\)\s*\{(.{0,900}?)\}",
    re.S,
)
# Comentário é ruído aqui, e ruído CARO: um bloco explicativo de três linhas empurra a
# guarda para fora da janela de busca (foi assim que a checagem não viu o defeito na
# primeira tentativa), e a palavra "return" citada num comentário criaria achado falso.
COMENTARIO_DE_LINHA = re.compile(r"//[^\n]*")
GUARDA_MUDA = re.compile(r"if\s*\(([^)]{1,120})\)\s*(?:\{\s*)?return\s*;")
BOTAO_COM_COMANDO = re.compile(
    r"<Button\b((?:(?!</?Button|/>|>).)*?)Command\s*=\s*\"\{Binding\s+"
    r"(?:[A-Za-z0-9_.]*\.)?(\w+)Command[^\"]*\"((?:(?!</?Button|/>|>).)*?)/?>",
    re.S,
)

# Chave: (ViewModel, nome do comando). O casamento é pela CONVENÇÃO DE NOME do projeto
# (`FooView.xaml` ↔ `FooViewModel.cs`), e não pelo nome do comando solto: `EditarCommand`
# existe em meia dúzia de telas que nem se conhecem, e comparar só pelo nome apontaria a
# guarda de um ViewModel na tela de outro.
#
# Tela cujo ViewModel não é encontrado por nome é PULADA — o certo aqui é não afirmar
# nada, porque um palpite errado gasta a confiança na checagem inteira.
_guardas: dict[tuple[str, str], list[str]] = {}
for arq in sorted(RAIZ.rglob("src/**/*.cs")):
    if "/obj/" in str(arq) or "/bin/" in str(arq):
        continue
    corpo = COMENTARIO_DE_LINHA.sub("", arq.read_text(encoding="utf-8"))
    classes = re.findall(r"\b(?:sealed\s+)?partial\s+class\s+(\w+)", corpo)
    vm = next((c for c in classes if c.endswith("ViewModel")), None)
    if vm is None:
        continue

    for nome, params, miolo in COMANDO_COM_GUARDA.findall(corpo):
        nomes_param = re.findall(r"(\w+)\s*(?:,|$)", params)
        for cond in GUARDA_MUDA.findall(miolo):
            antes = miolo[: miolo.index(cond)]
            # Já avisa antes de sair? então não é mudo.
            if re.search(r"Mensagem\s*=|Erro\(|_snackbar|_dialogo", antes):
                continue
            # A guarda olha um parâmetro? nunca dispara vindo de um botão de linha.
            if any(re.search(rf"\b{re.escape(p)}\b", cond) for p in nomes_param if p):
                continue

            # Guarda de REENTRÂNCIA (`if (Carregando) return;`) não é o defeito: o botão
            # deve mesmo ficar aceso durante a carga, e o guard só impede o clique duplo.
            # "Já estou fazendo" é diferente de "não dá para fazer".
            if re.search(r"\b(Carregando|Emitindo|Salvando|Processando|Ocupado|EmCurso)\b", cond):
                continue

            # Guarda sobre VARIÁVEL LOCAL do próprio método (`var caminho = Escolher();
            # if (caminho is null) return;`) é diálogo cancelado — sair calado é o certo.
            locais = re.findall(r"\bvar\s+(\w+)\s*=", antes)
            if any(re.search(rf"\b{re.escape(v)}\b", cond) for v in locais):
                continue
            _guardas.setdefault((vm, nome), []).append(
                f"{rel(arq)}: if ({cond.strip()}) return;")

for arq in sorted(RAIZ.rglob("src/**/*.xaml")):
    if "/obj/" in str(arq) or "/bin/" in str(arq):
        continue
    corpo = arq.read_text(encoding="utf-8")
    for antes, nome, depois in BOTAO_COM_COMANDO.findall(corpo):
        if "IsEnabled" in antes + depois:
            continue
        # `PrescricoesClinicasView.xaml` → `PrescricoesClinicasViewModel`.
        vm_da_tela = arq.stem + "Model" if arq.stem.endswith("View") else None
        if vm_da_tela is None or (vm_da_tela, nome) not in _guardas:
            continue
        onde = _guardas[(vm_da_tela, nome)][0]
        destino = avisos if "Clinica.Desktop/" in str(arq).replace("\\", "/") else erros
        destino.append(
            f"{rel(arq)}: o Button de `{nome}Command` não tem `IsEnabled`, e o comando "
            f"tem guarda MUDA ({onde}) — o botão fica aceso e o clique não faz nada. "
            f"Ligue o `IsEnabled` à pré-condição, e faça a guarda DIZER por que saiu."
        )

# --------------------------------------------------------------- checagem 22
# ELEMENTO DE PROPRIEDADE NO PAI ERRADO (MC3015).
#
# `<DataTemplate.Triggers>` escrito DENTRO do `<Grid>` que é o conteúdo do template, em vez
# de irmão dele. O XML continua bem-formado — a checagem 1 passa —, o `compilar-sombra`
# passa (ele não lê o corpo do XAML) e os testes passam. Quem reclama é o compilador de
# marcação, que só roda no runner Windows:
#
#   error MC3015: The attached property 'DataTemplate.Triggers' is not defined on 'Grid'
#
# É o pior tipo de defeito para este projeto: as três redes locais ficam verdes e o erro
# aparece cinco minutos depois, no CI, num arquivo que já foi empurrado.
#
# A regra geral do XAML é que `<A.B>` tem de ser filho direto de um `<A>` — mas ela sozinha
# daria falso positivo, porque HERANÇA vale: `<ItemsControl.ItemTemplate>` dentro de um
# `<ListBox>` é legal e é o que a suíte escreve o tempo todo. Por isso a checagem se limita
# aos três donos que nunca são herdados (DataTemplate, ControlTemplate e Style), que são
# justamente onde o erro acontece — o bloco de gatilhos fica no fim, longe do `<DataTemplate>`
# que o abriu, e é o `</Grid>` que aparece embaixo dele.
NUNCA_HERDADOS = ("DataTemplate", "ControlTemplate", "Style")

for f, raiz in arvores.items():
    for pai in raiz.iter():
        nome_pai = pai.tag.split("}")[-1]
        for filho in pai:
            tag = filho.tag.split("}")[-1]
            if "." not in tag:
                continue
            dono, _, membro = tag.partition(".")
            if dono not in NUNCA_HERDADOS or nome_pai == dono:
                continue
            erros.append(
                f"{rel(f)}: `<{tag}>` está dentro de `<{nome_pai}>` — o compilador de "
                f"marcação recusa (MC3015: '{tag}' is not defined on '{nome_pai}'). "
                f"Ele tem de ser filho direto do `<{dono}>`, irmão do conteúdo."
            )

# Autoteste: a checagem 22 tem de pegar o defeito que escapou para o CI na parcela 47.
_amostra_22 = ET.fromstring(
    '<DataTemplate xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation">'
    "<Grid><DataTemplate.Triggers /></Grid></DataTemplate>"
)
_pegou = [
    True
    for pai in _amostra_22.iter()
    for filho in pai
    if "." in filho.tag.split("}")[-1]
    and filho.tag.split("}")[-1].partition(".")[0] in NUNCA_HERDADOS
    and pai.tag.split("}")[-1] != filho.tag.split("}")[-1].partition(".")[0]
]
if not _pegou:
    erros.append("verificar-suite: a checagem 22 parou de pegar o próprio caso de teste.")


# --------------------------------------------------------------- checagem 23
# PREFIXO DE NAMESPACE USADO E NÃO DECLARADO (MC3071).
#
# `<DataTemplate DataType="{x:Type vm:LinhaCatalogo}">` num arquivo que não declara
# `xmlns:vm`. O XML continua bem-formado — a checagem 1 passa —, porque o prefixo está
# DENTRO de um valor de atributo, e XML não resolve prefixo ali; quem resolve é o XAML.
# O `compilar-sombra` também passa (ele não lê o corpo). Só o compilador de marcação
# reclama, e ele só roda no runner Windows:
#
#   error MC3071: 'vm' is an undeclared namespace
#
# Aconteceu ao MOVER um bloco de uma tela para uma janela nova: o bloco levou o
# `{x:Type vm:...}` e deixou para trás o `xmlns:vm` do arquivo de origem. É o risco de
# toda extração de bloco, e a parcela 49 fez cinco delas.
#
# A checagem olha só as duas extensões de marcação que recebem tipo prefixado
# (`x:Type` e `x:Static`), em vez de varrer todo `prefixo:` do arquivo — texto como
# "HH:mm" e "{0:C}" está cheio de dois-pontos, e checagem com falso positivo é checagem
# que alguém desliga.
PREFIXO_EM_MARCACAO = re.compile(r"\{x:(?:Type|Static)\s+([A-Za-z_][A-Za-z0-9_]*):")
PREFIXO_DECLARADO = re.compile(r'xmlns:([A-Za-z_][A-Za-z0-9_]*)\s*=')

for arq in sorted(RAIZ.rglob("src/**/*.xaml")):
    if "/obj/" in str(arq) or "/bin/" in str(arq):
        continue
    corpo = arq.read_text(encoding="utf-8")

    declarados = set(PREFIXO_DECLARADO.findall(corpo)) | {"x"}
    for prefixo in sorted(set(PREFIXO_EM_MARCACAO.findall(corpo))):
        if prefixo in declarados:
            continue
        erros.append(
            f"{rel(arq)}: o prefixo `{prefixo}:` é usado num `{{x:Type}}`/`{{x:Static}}` e "
            f"NÃO está declarado (MC3071: '{prefixo}' is an undeclared namespace). "
            f'Acrescente o `xmlns:{prefixo}="clr-namespace:..."` no elemento raiz.'
        )

# Autoteste: a checagem 23 tem de pegar o defeito que escapou para o CI na parcela 49.
_amostra_23 = '<Window xmlns:x="...">\n  <DataTemplate DataType="{x:Type vm:Linha}" />\n</Window>'
if not [
    p
    for p in PREFIXO_EM_MARCACAO.findall(_amostra_23)
    if p not in set(PREFIXO_DECLARADO.findall(_amostra_23)) | {"x"}
]:
    erros.append("verificar-suite: a checagem 23 parou de pegar o próprio caso de teste.")


# --------------------------------------------------------------- checagem 24
# TEXTO QUE ESTOURA: dado do banco sem quebra nem reticências.
#
# O cliente reclamou de "MUITOS textos estourando" no Gerente, e a família é sempre a
# mesma: um `TextBlock` amarrado a dado do BANCO — nome de paciente, convênio, valor em
# reais — dentro de uma célula de largura fixa. O WPF não corta nada por conta própria: o
# texto sai por cima do vizinho.
#
# A checagem só olha texto vindo de `{Binding}`. Literal o programador mede ao escrever;
# dado do banco é o que tem tamanho imprevisível — o nome do paciente pode ter 12 ou 60
# caracteres, e o valor pode ter três dígitos ou nove.
#
# Estilos que JÁ resolvem no próprio estilo ficam de fora (`TextoSuave` quebra,
# `CardKpi.Variacao` corta), senão a checagem cobraria o que já está certo.
#
# ⚠️ Por que ERRO só nos módulos já limpos: o resto da suíte tem centenas de ocorrências
# da mesma família, e transformar tudo em erro de uma vez pararia o CI por dívida antiga.
# Elas viram UMA linha de aviso com a contagem por módulo — dívida escrita, não escondida,
# e sem encher a saída com trezentas linhas que ninguém lê.
LIMPOS = ("Clinica.Modulo.Gerente",)
ESTILO_JA_RESOLVE = {"TextoSuave", "CardKpi.Variacao"}
TEXTBLOCK_ABERTURA = re.compile(r"<TextBlock\b[^>]*?/?>", re.S)
ESTILO_DO_TEXTBLOCK = re.compile(r'Style="\{StaticResource ([^}"]+)\}"')

_devedores: dict[str, int] = {}

for arq in sorted(RAIZ.rglob("src/**/*.xaml")):
    caminho = str(arq).replace("\\", "/")
    if "/obj/" in caminho or "/bin/" in caminho or "/Styles/" in caminho:
        continue

    corpo = arq.read_text(encoding="utf-8")
    modulo = rel(arq).split("/")[1] if "/" in rel(arq) else rel(arq)

    for achado in TEXTBLOCK_ABERTURA.finditer(corpo):
        tag = achado.group(0)
        if "Binding" not in tag:
            continue
        if "TextWrapping" in tag or "TextTrimming" in tag:
            continue
        estilo = ESTILO_DO_TEXTBLOCK.search(tag)
        if estilo and estilo.group(1) in ESTILO_JA_RESOLVE:
            continue

        if modulo in LIMPOS:
            linha = corpo[: achado.start()].count("\n") + 1
            erros.append(
                f"{rel(arq)}:{linha}: TextBlock amarrado a dado do banco sem "
                f"`TextWrapping` nem `TextTrimming` — o texto sai por cima do vizinho. "
                f"Célula de tabela leva `TextTrimming=\"CharacterEllipsis\"`; texto de "
                f"cartão leva `TextWrapping=\"Wrap\"`."
            )
        else:
            _devedores[modulo] = _devedores.get(modulo, 0) + 1

if _devedores:
    resumo = " · ".join(f"{m.replace('Clinica.Modulo.', '')}: {n}" for m, n in sorted(_devedores.items()))
    avisos.append(
        f"texto que pode estourar (dado do banco sem quebra nem reticências) — {resumo}. "
        f"Já limpos: {', '.join(m.replace('Clinica.Modulo.', '') for m in LIMPOS)}. "
        f"Acrescente o módulo a LIMPOS quando ele for corrigido, e a checagem passa a "
        f"cobrá-lo."
    )


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
