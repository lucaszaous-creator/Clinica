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
import unicodedata
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

# A suíte MAIS o faturamento. `PROJETOS` cobre só a suíte porque o faturamento carrega
# dívida antiga que não se corrige por decreto de leiaute (dezenas de `FontSize` numérico),
# e checagem que grita trinta vezes é checagem que alguém desliga.
#
# Mas há um grupo que precisa alcançar os dois: o que pega ERRO DE RUNTIME. A tela do
# faturamento é a que roda em produção — deixá-la fora é o defeito que a nota das checagens
# 15/16 já nomeia, "checagem que não alcança o lugar onde o defeito estava é checagem que
# passa sozinha". Daí esta segunda lista, usada pelas checagens 25, 26 e 27.
arvores_com_faturamento: dict[Path, ET.Element] = dict(arvores)
for f in sorted((RAIZ / "src" / "Clinica.Desktop").rglob("*.xaml")):
    try:
        arvores_com_faturamento[f] = ET.parse(f).getroot()
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


# ------------------- 2c. a MESMA checagem, no faturamento (parcela 57)
#
# Chave que não existe é `ResourceReferenceKeyNotFoundException` NA MONTAGEM DA TELA —
# erro de runtime puro, que é justamente o grupo que o `arvores_com_faturamento` existe
# para alcançar (a nota da parcela 51 ao lado dele). Ficar de fora foi o que deixou passar
# quatro `CellTemplate="{StaticResource CelulaPacienteContato}"` apontando para uma chave
# que ainda não tinha sido declarada: XAML bem-formado, `compilar-sombra` verde,
# `verificar-suite` verde, e a coluna sairia VAZIA na tela de quem fatura.
#
# Só a metade das CHAVES entra aqui — não a de `FontSize` numérico e cor em hexadecimal,
# que é a dívida antiga que faria a checagem gritar trinta vezes e alguém desligá-la.
#
# Os dois design systems não se referenciam (o débito permanente da parcela 7), então o
# conjunto de chaves é resolvido POR APP: usar o da suíte aqui aprovaria uma chave que só
# existe do outro lado.
_base_faturamento = RAIZ / "src" / "Clinica.Desktop"
_chaves_faturamento: set[str] = set()
_xamls_faturamento = sorted(_base_faturamento.rglob("*.xaml"))

for f in _xamls_faturamento:
    if "Styles" not in f.parts:
        continue
    if (raiz := arvores_com_faturamento.get(f)) is not None:
        _chaves_faturamento |= chaves(raiz)

for f in _xamls_faturamento:
    raiz = arvores_com_faturamento.get(f)
    if raiz is None:
        continue

    locais = chaves(raiz)
    for chave in sorted(set(REF_ESTATICA.findall(f.read_text(encoding="utf-8")))):
        if chave in locais or chave in _chaves_faturamento:
            continue
        if any(chave.startswith(p) for p in CHAVES_DO_SISTEMA):
            continue
        erros.append(
            f"{rel(f)}: StaticResource '{chave}' não existe no design system do "
            f"faturamento — a tela lança ao ser montada")


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


def _sem_comentarios(t: str) -> str:
    """
    Apaga só os COMENTÁRIOS, preservando as strings.

    Existe porque a checagem 19 casa `Ir("literal")` e portanto não pode usar
    `_sem_texto`, que apaga as strings junto. Sem isto ela lê o próprio comentário que
    EXPLICA a regra — foi o que aconteceu ao documentar a composição por abas na parcela
    55: um `NavegacaoSuite.Ir("caixa")` citado em prosa virou erro de build local.

    Checagem que reclama de comentário é checagem que alguém desliga, e aí ela para de
    pegar o defeito de verdade.

    As strings são PULADAS (e não apagadas) para que `"https://…"` não seja lido como
    comentário no meio do literal.
    """
    saida = list(t)
    i, n = 0, len(t)
    while i < n:
        c = t[i]
        if c in "\"'":
            j = i + 1
            while j < n and t[j] != c:
                if t[j] == "\\":
                    j += 1
                j += 1
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


# ⚠️ A checagem de rolagem não enxergava DENTRO de controle da casa (parcela 88, 5ª
# rodada). Uma janela cujo miolo é `<comp:ProcessoDeEnfermagemView />` era acusada de não
# ter rolagem nenhuma, com um `ScrollViewer` por aba dentro do controle — a checagem
# reclamando do que está certo, que é o que faz alguém desligá-la.
#
# Ela passou a seguir UM nível de composição: o tipo da tag é casado com o `x:Class` dos
# XAML da casa, e a árvore dele entra na conta. Um nível basta para a composição deste
# projeto, e mais níveis dariam um grafo para percorrer sem ganho medido.
_arvores_por_tipo: dict[str, ET.Element] = {}


def _registrar_tipos_de_controle(mapa: dict[Path, ET.Element]) -> None:
    for _arq, _raiz in mapa.items():
        classe = next(
            (v for k, v in _raiz.attrib.items() if k.split("}")[-1] == "Class"), None)
        if classe:
            _arvores_por_tipo[classe.rsplit(".", 1)[-1]] = _raiz


def _com_controles_da_casa(raiz: ET.Element) -> list[ET.Element]:
    """Os elementos da árvore, mais os de um nível de controles próprios usados nela."""
    todos = list(raiz.iter())
    for el in list(todos):
        alvo = _arvores_por_tipo.get(_nome(el))
        if alvo is not None and alvo is not raiz:
            todos.extend(alvo.iter())
    return todos


_registrar_tipos_de_controle(arvores)
_registrar_tipos_de_controle(arvores_com_faturamento)

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
    if (cresce or alto) and not any(
            _nome(e) in ROLAM for e in _com_controles_da_casa(raiz)):
        erros.append(
            f"{rel(arq)}: janela que cresce com o conteúdo (ou alta) sem nenhum "
            f"ScrollViewer — em escala 150% o rodapé sai da tela cortado")


# ⚠️ AUTOTESTE de `_com_controles_da_casa`: alargar uma checagem é o gesto que a deixa cega
# se ninguém provar que ela continua mordendo. Dois casos, e o segundo é o que importa —
# controle da casa SEM rolagem não pode contar como se tivesse.
_ctrl_com_rolagem = ET.fromstring(
    '<UserControl xmlns="x" xmlns:c="y" c:Class="A.B.ControleQueRola">'
    '<ScrollViewer /></UserControl>')
_ctrl_sem_rolagem = ET.fromstring(
    '<UserControl xmlns="x" xmlns:c="y" c:Class="A.B.ControleSeco"><Grid /></UserControl>')
_arvores_por_tipo["ControleQueRola"] = _ctrl_com_rolagem
_arvores_por_tipo["ControleSeco"] = _ctrl_sem_rolagem

for _tag, _deve_achar, _cenario in (
    ("ControleQueRola", True, "o controle da casa TEM ScrollViewer"),
    ("ControleSeco", False, "o controle da casa não rola — a janela continua acusada"),
):
    _janela = ET.fromstring(f'<Window xmlns="x"><{_tag} /></Window>')
    _achou = any(_nome(e) in {"ScrollViewer", "ListBox", "ListView", "DataGrid"}
                 for e in _com_controles_da_casa(_janela))
    if _achou != _deve_achar:
        erros.append(
            f"verificar-suite: a checagem de rolagem mudou de resposta ({_cenario}) — "
            f"esperado {'achar' if _deve_achar else 'NÃO achar'} rolagem."
        )

del _arvores_por_tipo["ControleQueRola"], _arvores_por_tipo["ControleSeco"]


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
    if (cresce_c or alto_c) and not any(
            _nome(e) in ROLAM_C for e in _com_controles_da_casa(raiz_c)):
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

# ⚠️ A forma GENÉRICA conta (parcela 67).
#
# A busca era `.AlterColumn(` — e o EF **nunca** gera isso: ele emite
# `migrationBuilder.AlterColumn<string>(`. O item mais perigoso da lista era letra morta
# desde que a checagem nasceu: encolher `maxLength` ou tornar uma coluna NOT NULL destrói
# dado e derruba a versão antiga do faturamento na abertura seguinte, e passava sem um
# pio. Casar `Op(` **ou** `Op<` é o conserto.
def _usa(corpo: str, operacao: str) -> bool:
    return f".{operacao}(" in corpo or f".{operacao}<" in corpo

# A saída consciente (parcela 67).
#
# Nem toda operação desta lista perde DADO: alargar uma chave única (drop + create com uma
# coluna a mais) é a que apareceu primeiro, e ela não apaga nada — toda linha que passava na
# chave antiga passa na nova. Mas a regra não podia virar "DropIndex pode": o mesmo drop
# usado para ESTREITAR uma chave quebra a clínica no dia seguinte, e a diferença entre os
# dois casos não está na operação, está na intenção de quem a escreveu.
#
# Por isso a exceção é DECLARADA, e o preço dela é escrever a razão no arquivo. Quem escrever
# a marca sem pensar está mentindo por escrito, num arquivo versionado, com o nome dele no
# commit — que é o mais longe que uma ferramenta chega.
#
# ⚠️ E ela NUNCA fica silenciosa: vira aviso em toda execução, inclusive no CI. Exceção que
# some da saída é exceção que ninguém revisa — e a próxima pessoa a copiar esta migration
# como modelo precisa ver que aqui houve uma decisão, não uma permissão.
#
# ⚠️⚠️ A dispensa é POR OPERAÇÃO, e a primeira versão não era — foi o achado mais grave da
# revisão desta parcela. A marca valia para o ARQUIVO: bastava um `DropIndex` inofensivo
# declarado para que um `DropColumn` acrescentado DEPOIS, na mesma migration, passasse
# junto — e a ferramenta ainda imprimia, como justificativa dele, a frase que falava do
# índice e afirmava "nenhuma linha se perde". Garantia falsa no log do CI é pior do que
# checagem nenhuma, e o caminho é o realista: a migration marcada é justamente a que a
# próxima pessoa vai copiar como modelo.
#
# Agora a marca NOMEIA o que cobre — `MIGRATION-NAO-ADITIVA-CONSCIENTE(DropIndex): razão` —
# e o que não estiver na lista continua sendo erro.
MARCA_CONSCIENTE = "MIGRATION-NAO-ADITIVA-CONSCIENTE"


def _dispensa_declarada(texto: str) -> tuple[set[str], str]:
    """As operações dispensadas e a razão. Sem marca (ou sem razão), nada é dispensado."""
    for linha in texto.splitlines():
        i = linha.find(MARCA_CONSCIENTE)
        if i < 0:
            continue

        resto = linha[i + len(MARCA_CONSCIENTE):]
        if not resto.startswith("("):
            continue

        fim = resto.find(")")
        if fim < 0:
            continue

        declaradas = {o.strip() for o in resto[1:fim].split(",") if o.strip()}
        razao = resto[fim + 1:].lstrip(": ").strip()

        # Razão vazia não vale: ela É a exceção, não um interruptor. E operação declarada
        # que não existe na lista de destrutivas é engano de quem escreveu — melhor não
        # dispensar nada do que dispensar o que a pessoa não quis dizer.
        if not razao or not declaradas or not declaradas <= set(DESTRUTIVAS):
            return set(), ""

        return declaradas, razao

    return set(), ""


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

        texto_migration = arq.read_text(encoding="utf-8")
        corpo = _corpo_do_up(texto_migration)
        achadas = {d for d in DESTRUTIVAS if _usa(corpo, d)}

        if not achadas:
            continue

        # A marca é lida do arquivo INTEIRO, e não só do Up(): ela cabe melhor no comentário
        # de documentação da classe, que é onde alguém a lê.
        dispensadas, razao = _dispensa_declarada(texto_migration)

        cobertas = sorted(achadas & dispensadas)
        descobertas = sorted(achadas - dispensadas)

        if cobertas:
            avisos.append(
                f"{rel(arq)}: migration não aditiva ({', '.join(cobertas)}) DECLARADA como "
                f"consciente — \"{razao}\". Confira antes de publicar: o faturamento aplica "
                f"migrations na abertura, e a clínica pode ter versões diferentes em campo.")

        if descobertas:
            extra = (
                f"\n    ⚠️ A marca deste arquivo cobre {', '.join(sorted(dispensadas))} e NÃO "
                f"cobre {', '.join(descobertas)} — a dispensa é por OPERAÇÃO, justamente para "
                f"uma razão escrita sobre um índice não passar a valer para uma coluna."
                if dispensadas else
                f"\n    Se a operação comprovadamente não perde dado nem quebra versão antiga "
                f"(alargar uma chave única, por exemplo), declare-a no arquivo como "
                f"`{MARCA_CONSCIENTE}({descobertas[0]}): <razão>` — vira aviso permanente, "
                f"nunca silêncio."
            )

            erros.append(
                f"{rel(arq)}: migration NÃO aditiva ({', '.join(descobertas)}) — o faturamento "
                f"está em produção e aplica migrations na abertura. Enquanto houver versões "
                f"diferentes em campo, migration nova só acrescenta. Ver "
                f"docs/arquitetura-multi-exe.md.{extra}")


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

    texto = _sem_comentarios("\n".join(f.read_text(encoding="utf-8") for f in fontes))

    # As chaves declaradas como item de menu DESTE módulo.
    declaradas = set(CHAVE_ITEM.findall(texto))
    # Literais também valem (nem todo módulo usa const).
    declaradas |= set(re.findall(r'Chave\s*=\s*"([^"]+)"', texto))

    for arq in fontes:
        corpo = _sem_comentarios(arq.read_text(encoding="utf-8"))
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
# O `\??` é o ponto cego que a parcela 64 fechou. A tela de preço por convênio oferecia
# a especialidade a partir de uma lista de `Especialidade?` — a coleção nasce anulável
# porque o nulo é a opção "todas" —, e o WPF chama ToString() no valor exatamente como
# faria num enum não anulável: a caixa mostrava "ClinicaDaDor". A expressão só casava
# `<Especialidade>`, então a checagem que existe para pegar esse defeito passava por cima
# dele. Checagem cega é pior do que checagem ausente: ela responde "está limpo".
COLECAO_TIPADA = re.compile(
    r"(?:IReadOnlyList|IList|List|ObservableCollection|IEnumerable)<\s*([A-Za-z0-9_]+)\??\s*>"
    r"\s+(?:_)?(\w+)"
)


def _enums_do_dominio() -> set[str]:
    """
    Enums do DOMÍNIO e da APLICAÇÃO.

    A camada de aplicação entrou na parcela 64, e o motivo foi o cliente: a tela "Quem me
    deve" oferecia "MaisAntigo" e "MaiorValor" no seletor de ordenação, com a checagem
    verde. `OrdemInadimplencia` é declarada em `Clinica.Application/Servicos`, e esta
    função só varria `Clinica.Domain` — o WPF chama `ToString()` sem se importar com a
    camada em que o enum nasceu.

    Custo medido antes de alargar: UMA ocorrência em toda a suíte, que era o próprio
    defeito. Checagem cega é pior do que checagem ausente, porque ela responde
    "está limpo".
    """
    achados: set[str] = set()
    for camada in ("Clinica.Domain", "Clinica.Application"):
        raiz = RAIZ / "src" / camada
        if not raiz.exists():
            continue
        for arq in raiz.rglob("*.cs"):
            if "/obj/" in str(arq) or "/bin/" in str(arq):
                continue
            achados.update(
                re.findall(r"\benum\s+([A-Za-z0-9_]+)", arq.read_text(encoding="utf-8")))
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
# aos donos que nunca são herdados uns dos outros.
#
# São dois grupos, e o segundo entrou na parcela 50 depois de o mesmo MC3015 escapar de novo:
#
#   (a) DataTemplate, ControlTemplate e Style — onde o erro nasce de o bloco de gatilhos
#       ficar no fim, longe do `<DataTemplate>` que o abriu, com um `</Grid>` embaixo dele.
#
#   (b) os PAINÉIS. Nenhum deriva do outro (todos descendem de `Panel` direto), então
#       `<UniformGrid.Style>` dentro de um `<WrapPanel>` é recusado. Foi exatamente o que
#       aconteceu ao trocar UniformGrid por WrapPanel nos KPIs: a troca pegou as tags de
#       abertura e fechamento e deixou o elemento de propriedade com o dono antigo.
#
# A ressalva do grupo (b) são as propriedades ANEXADAS: `<Grid.Row>` escrito como elemento
# dentro de um `<Border>` é XAML legal. Ninguém na suíte escreve assim — mas inventar erro
# onde não há é o que faz alguém desligar a checagem.
NUNCA_HERDADOS = ("DataTemplate", "ControlTemplate", "Style")
PAINEIS = ("Grid", "StackPanel", "WrapPanel", "DockPanel", "Canvas", "UniformGrid", "VirtualizingStackPanel")
ANEXADAS_DE_PAINEL = {
    "Row", "Column", "RowSpan", "ColumnSpan", "IsSharedSizeScope",
    "Dock", "Left", "Top", "Right", "Bottom", "ZIndex",
}

def _elemento_de_propriedade_no_pai_errado(nome_pai: str, tag: str) -> bool:
    """`<A.B>` escrito dentro de um `<C>` que não é um `A`. Ver o comentário acima."""
    if "." not in tag:
        return False
    dono, _, membro = tag.partition(".")
    if nome_pai == dono:
        return False
    if dono in NUNCA_HERDADOS:
        return True
    return dono in PAINEIS and nome_pai in PAINEIS and membro not in ANEXADAS_DE_PAINEL


for f, raiz in arvores.items():
    for pai in raiz.iter():
        nome_pai = pai.tag.split("}")[-1]
        for filho in pai:
            tag = filho.tag.split("}")[-1]
            if not _elemento_de_propriedade_no_pai_errado(nome_pai, tag):
                continue
            erros.append(
                f"{rel(f)}: `<{tag}>` está dentro de `<{nome_pai}>` — o compilador de "
                f"marcação recusa (MC3015: '{tag}' is not defined on '{nome_pai}'). "
                f"Ele tem de ser filho direto do `<{tag.partition('.')[0]}>`, irmão do conteúdo."
            )

# Autoteste. Os dois primeiros são os defeitos REAIS que escaparam para o CI (parcelas 47 e
# 50); os outros três são os falsos positivos que a checagem não pode inventar — herança de
# controle, propriedade anexada escrita como elemento, e o caso normal do dono certo.
for _pai, _tag, _esperado in (
    ("Grid", "DataTemplate.Triggers", True),
    ("WrapPanel", "UniformGrid.Style", True),
    ("ListBox", "ItemsControl.ItemTemplate", False),
    ("StackPanel", "Grid.Row", False),
    ("WrapPanel", "WrapPanel.Style", False),
):
    if _elemento_de_propriedade_no_pai_errado(_pai, _tag) != _esperado:
        erros.append(
            f"verificar-suite: a checagem 22 mudou de resposta para `<{_tag}>` "
            f"dentro de `<{_pai}>` (esperado: {'pega' if _esperado else 'deixa passar'})."
        )


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
LIMPOS = (
    "Clinica.Desktop",
    "Clinica.Desktop.Shell",
    "Clinica.Modulo.Clinico",
    "Clinica.Modulo.Financeiro",
    "Clinica.Modulo.Gerente",
    "Clinica.Modulo.Recepcao",
)


_TEXT_DO_TEXTBLOCK = re.compile(r'\bText\s*=\s*"((?:[^"]|\n)*?)"', re.S)


def _texto_amarrado(tag: str) -> bool:
    """O `Text` DESTE TextBlock vem de um `{Binding}`?

    ⚠️ Olha o atributo `Text`, e não a tag inteira. A primeira versão procurava "Binding"
    em qualquer lugar da abertura, e por isso acusava um `Text` LITERAL só porque a mesma
    tag tinha `Visibility="{Binding ...}"` ao lado — o indicador "●" das abas da sessão
    (parcela 77) foi o caso que a revelou. A regra é "texto do BANCO tem tamanho
    imprevisível"; um literal o programador mede ao escrever, tenha ele binding no
    Visibility, no Foreground ou em nada.

    Checagem que reclama do que está certo é checagem que alguém desliga — e aí ela para
    de pegar o defeito de verdade.
    """
    m = _TEXT_DO_TEXTBLOCK.search(tag)
    return m is not None and "{Binding" in m.group(1)


# --- autoteste do `_texto_amarrado` (checagem 24) ---
#
# Os dois sentidos no mesmo lugar, pela lição da parcela 66: o sentido que você deixar de
# fora é o que a próxima pessoa vai cometer. O 2º caso é o que a primeira versão errava.
for _cenario, _tag, _esperado in (
    ("Text do banco", '<TextBlock Text="{Binding Nome}" />', True),
    ("Text literal com binding no Visibility",
     '<TextBlock Text="&#x25CF;" Visibility="{Binding Tem, Converter={StaticResource C}}" />',
     False),
    ("Text literal puro", '<TextBlock Text="Subjetivo" />', False),
    ("binding de Text quebrado em duas linhas",
     '<TextBlock Text="{Binding Mensagem,\n    Converter={StaticResource C}}" />', True),
    ("sem Text nenhum", '<TextBlock Style="{StaticResource X}" />', False),
):
    if _texto_amarrado(_tag) != _esperado:
        erros.append(
            f"verificar-suite: a checagem 24 mudou de resposta ({_cenario}) — "
            f"leu {_texto_amarrado(_tag)}, esperado {_esperado}."
        )


def _estilos_que_ja_resolvem() -> set[str]:
    """
    Os estilos de TextBlock que já tratam o estouro no PRÓPRIO estilo.

    Lido dos dicionários de estilo, não escrito à mão: são DOIS design systems (o da suíte
    e o do faturamento, que não se referenciam — o débito permanente da parcela 7), e uma
    lista fixa aqui só conheceria um deles. Foi o que aconteceu: os seis `FichaValor` do
    faturamento apareceram como dívida sem serem — aquele estilo corta desde sempre.

    ⚠️ Ela SEGUE o `BasedOn`. Sem isso, um estilo que herda o corte do pai aparece como
    dívida sem ser — foi o que aconteceu com `CelulaCopiavelSuave`, que só acrescenta cor
    e tamanho ao `CelulaCopiavel` e portanto já corta. O ponto cego é traiçoeiro porque a
    reclamação é PLAUSÍVEL: quem a lê acrescenta o `TextTrimming` repetido na tela e segue
    em frente, e a checagem continua cega para o próximo caso.
    """
    proprio: dict[str, bool] = {}
    herda: dict[str, str] = {}

    for arq in RAIZ.rglob("src/**/Styles/**/*.xaml"):
        corpo = arq.read_text(encoding="utf-8")
        for m in re.finditer(
            r'<Style x:Key="([^"]+)" TargetType="TextBlock"([^>]*)>(.*?)</Style>', corpo, re.S
        ):
            chave, abertura, miolo = m.group(1), m.group(2), m.group(3)
            proprio[chave] = "TextWrapping" in miolo or "TextTrimming" in miolo
            if (pai := re.search(r'BasedOn="\{StaticResource ([^}"]+)\}"', abertura)):
                herda[chave] = pai.group(1).strip()

    def resolve(chave: str, vistos: set[str] | None = None) -> bool:
        vistos = vistos or set()
        if chave in vistos or chave not in proprio:
            return False           # ciclo, ou pai fora dos dicionários (ex.: {x:Type TextBlock})
        if proprio[chave]:
            return True
        vistos.add(chave)
        return chave in herda and resolve(herda[chave], vistos)

    return {chave for chave in proprio if resolve(chave)}


ESTILO_JA_RESOLVE = _estilos_que_ja_resolvem()
# Duas formas de amarrar texto, e a segunda escapava: `Text="{Binding X}"` na tag de
# abertura, e `<Run Text="{Binding X}" />` como FILHO. A segunda é como a suíte monta
# frase com pedaço variável no meio ("Sai hoje de cada recebimento: R$ 1.234"), e olhar
# só a abertura deixava esse caso passar — foi o ponto cego que a revisão do Gerente
# encontrou. O `(?!</?TextBlock)` impede o casamento de atravessar dois TextBlocks e
# acusar o binding de um vizinho.
TEXTBLOCK_ABERTURA = re.compile(r"<TextBlock\b[^>]*?/?>", re.S)
TEXTBLOCK_COM_FILHOS = re.compile(
    r"<TextBlock\b([^>]*)>((?:(?!</?TextBlock).)*?)</TextBlock>", re.S)
ESTILO_DO_TEXTBLOCK = re.compile(r'Style="\{StaticResource ([^}"]+)\}"')

_devedores: dict[str, int] = {}

for arq in sorted(RAIZ.rglob("src/**/*.xaml")):
    caminho = str(arq).replace("\\", "/")
    if "/obj/" in caminho or "/bin/" in caminho or "/Styles/" in caminho:
        continue

    corpo = arq.read_text(encoding="utf-8")
    modulo = rel(arq).split("/")[1] if "/" in rel(arq) else rel(arq)

    suspeitos: list[tuple[int, str]] = []

    for achado in TEXTBLOCK_ABERTURA.finditer(corpo):
        tag = achado.group(0)
        if _texto_amarrado(tag) and "TextWrapping" not in tag and "TextTrimming" not in tag:
            suspeitos.append((achado.start(), tag))

    for achado in TEXTBLOCK_COM_FILHOS.finditer(corpo):
        abertura, miolo = achado.group(1), achado.group(2)
        if "Binding" not in miolo or "<Run" not in miolo:
            continue
        if "TextWrapping" in abertura or "TextTrimming" in abertura:
            continue
        suspeitos.append((achado.start(), abertura))

    for inicio, tag in suspeitos:
        estilo = ESTILO_DO_TEXTBLOCK.search(tag)
        if estilo and estilo.group(1) in ESTILO_JA_RESOLVE:
            continue

        if modulo in LIMPOS:
            linha = corpo[:inicio].count("\n") + 1
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


# --------------------------------------------------------------- checagem 25
# SOBREPOSIÇÃO POSTA COMO IRMÃ, E NÃO POR CIMA.
#
# O `EstadoDaTela` (carregando / falhou / vazio) é uma SOBREPOSIÇÃO: ele cobre o conteúdo
# enquanto não há o que mostrar. Isso exige um pai que empilhe os filhos no mesmo lugar —
# um `Grid` —, e dentro dele ser o ÚLTIMO filho (ou trazer `Panel.ZIndex`), porque o WPF
# desenha na ordem do XAML.
#
# Posto num painel LINEAR (`DockPanel`, `StackPanel`, `WrapPanel`), ele deixa de sobrepor e
# passa a OCUPAR espaço, empurrando o conteúdo. E, num `DockPanel`, o estrago não para aí:
# o irmão anterior deixa de encostar nas bordas, todo `DockPanel.Dock` dos filhos DELE vira
# no-op — porque o pai passou a ser o `Grid` intermediário —, e a tela inteira desaba numa
# célula só, com título, abas e texto desenhados uns por cima dos outros.
#
# Foi o que o cliente viu na Conciliação: cinco telas do Financeiro assim desde a parcela em
# que o `EstadoDaTela` foi acrescentado. O XML é bem-formado, o `compilar-sombra` passa, os
# testes passam e o compilador de marcação não tem o que reclamar — o defeito só existe na
# tela montada. É a família de sempre, e por isso vira rede.
SOBREPOSICOES = ("EstadoDaTela",)
PAINEIS_LINEARES = ("DockPanel", "StackPanel", "WrapPanel", "UniformGrid")

for f, raiz in arvores_com_faturamento.items():
    for pai in raiz.iter():
        nome_pai = pai.tag.split("}")[-1]
        filhos = [c for c in pai if "." not in c.tag.split("}")[-1]]
        for i, filho in enumerate(filhos):
            if filho.tag.split("}")[-1] not in SOBREPOSICOES:
                continue
            tag = filho.tag.split("}")[-1]
            if nome_pai in PAINEIS_LINEARES:
                erros.append(
                    f"{rel(f)}: `<{tag}>` está dentro de um `<{nome_pai}>` — painel linear "
                    f"não sobrepõe, ELE OCUPA ESPAÇO. Ponha-o num `<Grid>`, como último "
                    f"filho, com o conteúdo antes dele."
                )
            elif nome_pai == "Grid" and i < len(filhos) - 1 and not any(
                k.endswith("ZIndex") for k in filho.attrib
            ):
                erros.append(
                    f"{rel(f)}: `<{tag}>` é o filho {i + 1} de {len(filhos)} do `<Grid>` e "
                    f"não tem `Panel.ZIndex` — o WPF desenha na ordem do XAML, então ele "
                    f"fica ATRÁS do conteúdo. Mova-o para o fim ou dê-lhe `Panel.ZIndex`."
                )

# Autoteste: os dois defeitos que o cliente achou na Conciliação, e os dois jeitos certos.
_amostras_25 = (
    ('<DockPanel {0}><Border /><ctrl:EstadoDaTela /></DockPanel>', True),
    ('<Grid {0}><ctrl:EstadoDaTela /><Border /></Grid>', True),
    ('<Grid {0}><Border /><ctrl:EstadoDaTela /></Grid>', False),
    ('<Grid {0}><ctrl:EstadoDaTela Panel.ZIndex="1" /><Border /></Grid>', False),
)
for _xml, _deve_pegar in _amostras_25:
    _r = ET.fromstring(
        _xml.format('xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation" '
                    'xmlns:ctrl="clr-namespace:Clinica.Desktop.Controls"')
    )
    _fs = [c for c in _r if "." not in c.tag.split("}")[-1]]
    _i = next(i for i, c in enumerate(_fs) if c.tag.split("}")[-1] == "EstadoDaTela")
    _nome_pai = _r.tag.split("}")[-1]
    _pegou = _nome_pai in PAINEIS_LINEARES or (
        _nome_pai == "Grid"
        and _i < len(_fs) - 1
        and not any(k.endswith("ZIndex") for k in _fs[_i].attrib)
    )
    if _pegou != _deve_pegar:
        erros.append(
            f"verificar-suite: a checagem 25 mudou de resposta para `{_xml[:40]}…` "
            f"(esperado: {'pega' if _deve_pegar else 'deixa passar'})."
        )


# --------------------------------------------------------------- checagem 26
# ESTILO DE PARÁGRAFO USADO COMO CÉLULA DE TABELA.
#
# `TextoSuave` fixa `HorizontalAlignment="Left"` (parcela 37, e por um bom motivo: sem ele
# o subtítulo da página nasce flutuando no meio da tela). O efeito colateral é que o
# TextBlock passa a ter a largura do TEXTO, e não a da célula — então um
# `TextAlignment="Right"` escrito ao lado não alinha nada, porque não sobra espaço dentro
# do bloco para alinhar. O número desgruda da borda direita da coluna e vai colar no valor
# da coluna anterior; o `Margin="0,4,0,0"` do mesmo estilo ainda o desce 4 px.
#
# Foi assim que o cliente viu "6R$ 0,00" e "R$ 0,00R$ 0,00" nos relatórios do Gerente.
#
# A checagem procura a CONTRADIÇÃO declarada no mesmo elemento, e não "TextoSuave em
# tabela": adivinhar o que é tabela daria falso positivo em legenda sob uma foto.
#
# E só `Right`, nunca `Center` — a distinção custou uma rodada de falsos positivos e é
# real. `TextoSuave` também liga `TextWrapping`, então quando o texto QUEBRA o bloco passa
# a ocupar a largura disponível e o `TextAlignment` volta a valer, centralizando as linhas
# umas em relação às outras: é exatamente o que a legenda de duas linhas sob o retrato do
# paciente quer. `Right` não tem esse uso — texto corrido alinhado à direita não existe
# nesta suíte —, e é a assinatura da coluna de NÚMERO, que é onde o defeito mora.
for f, raiz in arvores_com_faturamento.items():
    for el in raiz.iter():
        if el.tag.split("}")[-1] not in ("TextBlock", "Run"):
            continue
        if "TextoSuave" not in el.attrib.get("Style", ""):
            continue
        if el.attrib.get("TextAlignment") != "Right":
            continue
        erros.append(
            f"{rel(f)}: `TextoSuave` + `TextAlignment=\"Right\"` no mesmo TextBlock é "
            f"contradição — o estilo fixa `HorizontalAlignment=\"Left\"`, o bloco encolhe "
            f"até o texto e o número não encosta na borda direita da coluna: ele cola no "
            f"valor da coluna anterior. Em célula de tabela use `CelulaSuave`."
        )

# Autoteste: os dois estilos existem nos DOIS design systems? Uma checagem que manda usar
# `CelulaSuave` num dicionário onde ela não foi declarada trocaria um defeito por um
# crash em runtime (StaticResource não resolvido não quebra o build).
for _ds in ("src/Clinica.Desktop.Shell/Styles/Theme.xaml", "src/Clinica.Desktop/Styles/Theme.xaml"):
    _p = RAIZ / _ds
    if not _p.exists() or 'x:Key="CelulaSuave"' not in _p.read_text(encoding="utf-8"):
        erros.append(
            f"{_ds}: a checagem 26 manda usar `CelulaSuave` e este design system não a "
            f"declara — StaticResource que não resolve não quebra o build, quebra a tela."
        )


# --------------------------------------------------------------- checagem 27
# `SharedSizeGroup` COM NOME INVÁLIDO — explode ao ABRIR A TELA.
#
# O WPF valida o valor no `set` da propriedade e exige um IDENTIFICADOR: começa por letra
# ou sublinhado e segue com letras, dígitos ou sublinhado. Ponto, hífen e espaço não
# passam. Quem escreve `SharedSizeGroup="PacLinha.Avatar"` — porque parece um nome
# qualificado, e todo o resto do XAML aceita ponto — recebe isto na cara do usuário:
#
#   A propriedade definida 'System.Windows.Controls.DefinitionBase.SharedSizeGroup'
#   iniciou uma exceção.
#
# É a pior categoria de defeito do projeto por uma razão nova: as três redes ficam verdes
# E O COMPILADOR DE MARCAÇÃO TAMBÉM. O XAML está bem-formado, a propriedade existe e o
# tipo é string — o erro só nasce quando a tela é MONTADA, e derruba a tela inteira. Foi
# assim que ele chegou à `main` e à mão do cliente.
IDENT_XAML = re.compile(r"^[A-Za-z_][A-Za-z0-9_]*$")

for f, raiz in arvores_com_faturamento.items():
    for el in raiz.iter():
        grupo = el.attrib.get("SharedSizeGroup")
        if grupo is None or IDENT_XAML.match(grupo):
            continue
        erros.append(
            f"{rel(f)}: `SharedSizeGroup=\"{grupo}\"` não é um identificador válido — o "
            f"WPF valida no `set` e LANÇA ao montar a tela (\"a propriedade "
            f"'DefinitionBase.SharedSizeGroup' iniciou uma exceção\"). Só letras, dígitos "
            f"e sublinhado, começando por letra: use \"{re.sub(r'[^A-Za-z0-9_]', '', grupo)}\"."
        )

# Autoteste: o nome que quebrou a tela de Pacientes, e os que têm de passar.
for _valor, _deve_pegar in (
    ("PacLinha.Avatar", True),   # o defeito real
    ("Pac-Linha", True),
    ("Pac Linha", True),
    ("1Coluna", True),
    ("PacLinhaAvatar", False),
    ("_coluna1", False),
):
    if bool(IDENT_XAML.match(_valor)) == _deve_pegar:
        erros.append(
            f"verificar-suite: a checagem 27 mudou de resposta para "
            f"`SharedSizeGroup=\"{_valor}\"`."
        )


# --------------------------------------------------------------- checagem 28
# SUB-ABA APONTANDO PARA CHAVE QUE NINGUÉM DECLARA (parcela 55).
#
# Um item de menu COMPOSTO lista as abas por CHAVE, e o shell resolve cada uma procurando
# o item dono na lista de todos os módulos carregados. Chave que não existe não dá erro:
# a aba é simplesmente PULADA (é o mesmo mecanismo que faz uma aba sumir quando o módulo
# dela não está no executável, e esse comportamento é desejado).
#
# O efeito colateral é uma armadilha nova, da mesma família da checagem 19: um erro de
# digitação — ou renomear a const do outro lado — apaga a aba EM SILÊNCIO. A tela composta
# abre com uma aba a menos, ninguém percebe, e a tela some do sistema sem que build, teste
# ou o compilador de sombra tenham o que dizer.
#
# Como a chave atravessa módulo, ela vem quase sempre de `ChavesSuite`; a resolução abaixo
# cobre os dois casos (const do próprio módulo e const da ChavesSuite).
ABA_MENU = re.compile(r"""new\s+AbaMenu\(\s*"([^"]*)"\s*,\s*([A-Za-z0-9_.]+)\s*\)""")
CONST_CHAVE = re.compile(
    r"""public\s+const\s+string\s+(\w+)\s*=\s*(?:"([^"]*)"|ChavesSuite\.(\w+))\s*;""")


def _consts_de(texto: str) -> dict[str, str]:
    return {
        nome: (lit if lit else f"ChavesSuite.{via}")
        for nome, lit, via in CONST_CHAVE.findall(texto)
    }


_arq_suite = RAIZ / "src/Clinica.Desktop.Shell/Modulos/ChavesSuite.cs"
_chaves_suite: dict[str, str] = {}
if _arq_suite.exists():
    _chaves_suite = {
        n: v for n, v in _consts_de(_arq_suite.read_text(encoding="utf-8")).items()
        if not v.startswith("ChavesSuite.")
    }

_modulos_cs = sorted(RAIZ.glob("src/Clinica.Modulo.*/Modulo/Modulo*.cs"))
_declaradas: set[str] = set()
_abas: list[tuple[Path, str, str]] = []          # (arquivo, rótulo, chave resolvida)


def _resolver(expr: str, locais: dict[str, str]) -> str | None:
    """Expressão de chave → literal. None quando não dá para resolver estaticamente."""
    if expr.startswith("ChavesSuite."):
        return _chaves_suite.get(expr.removeprefix("ChavesSuite."))
    alvo = locais.get(expr.rsplit(".", 1)[-1])
    if alvo is None:
        return None
    return _resolver(alvo, locais) if alvo.startswith("ChavesSuite.") else alvo


for _arq in _modulos_cs:
    _texto = _sem_comentarios(_arq.read_text(encoding="utf-8"))
    _locais = _consts_de(_texto)

    for _expr in re.findall(r"Chave\s*=\s*([A-Za-z0-9_.]+)\s*,", _texto):
        if (_v := _resolver(_expr, _locais)) is not None:
            _declaradas.add(_v)
    _declaradas |= set(re.findall(r'Chave\s*=\s*"([^"]+)"\s*,', _texto))

    for _rotulo, _expr in ABA_MENU.findall(_texto):
        _abas.append((_arq, _rotulo, _resolver(_expr, _locais) or _expr))

for _arq, _rotulo, _chave in _abas:
    if _chave in _declaradas:
        continue
    erros.append(
        f"{rel(_arq)}: a sub-aba \"{_rotulo}\" aponta para `{_chave}`, que não é `Chave` "
        f"de nenhum ItemMenuModulo da suíte — o shell PULA a aba em silêncio e a tela "
        f"some sem erro nenhum. Declare o item (a sub-tela continua sendo um item; quem "
        f"a esconde do menu é o item pai)."
    )

# Autoteste: a checagem tem de achar as abas de verdade e recusar uma chave inventada.
if _modulos_cs:
    if not _abas:
        erros.append(
            "verificar-suite: a checagem 28 não achou nenhuma sub-aba — o padrão "
            "`new AbaMenu(\"rótulo\", Chave)` mudou e ela parou de olhar o que deveria."
        )
    if _resolver("ChavesSuite.NaoExisteEssaChave", {}) is not None:
        erros.append("verificar-suite: a checagem 28 resolveu uma chave inexistente.")


# --------------------------------------------------------------- checagem 29
# ESTADO VAZIO PERMANENTE POR CIMA DA TELA.
#
# `EstadoDaTela` decide o estado assim: `(Vazio ?? Vazia(Itens)) ? Vazio : Conteudo` — e
# `Vazia(null)` é **verdadeiro**. Quem declara o componente só com `Carregando` e
# `NaoVerificado`, sem dizer o que é "vazio", ganha a sobreposição LIGADA PARA SEMPRE:
# "Nada por aqui" desenhado por cima de uma tela que está funcionando por baixo.
#
# Foi o que o cliente viu na Guarda do prontuário — ele buscou uma pessoa, a tela achou,
# leu a guarda dela, e "Nada por aqui" ficou escrito em cima do resultado.
#
# Nenhuma rede pegava: o XAML é bem-formado, as propriedades existem, o binding é válido
# e o componente não lança. É a mesma família da checagem 25 (sobreposição no pai errado)
# — só existe na tela montada.
#
# As duas saídas legítimas: `Itens` (o caminho normal, quando há uma lista) ou `Vazio`
# (tela composta, ou tela que não é lista nenhuma e portanto nunca está vazia).
ESTADO_TELA = re.compile(r"<ctrl:EstadoDaTela\b[^>]*?/>", re.S)

_estados = 0
for _arq in xamls():
    _texto = _arq.read_text(encoding="utf-8")
    for _m in ESTADO_TELA.finditer(_texto):
        _bloco = _m.group(0)
        _estados += 1
        if "Itens=" in _bloco or "Vazio=" in _bloco:
            continue
        _linha = _texto.count("\n", 0, _m.start()) + 1
        erros.append(
            f"{rel(_arq)}:{_linha}: `EstadoDaTela` sem `Itens` nem `Vazio` — o componente "
            f"resolve `Vazia(null)` como VERDADEIRO e a sobreposição \"Nada por aqui\" fica "
            f"visível PARA SEMPRE, por cima da tela funcionando. Passe `Itens` (quando há "
            f"lista) ou `Vazio` (tela composta, ou tela que não é lista)."
        )

# Autoteste: a checagem tem de ver os usos reais e recusar o declarado sem os dois.
if _estados == 0:
    erros.append(
        "verificar-suite: a checagem 29 não achou nenhum `EstadoDaTela` — o padrão de "
        "declaração mudou e ela parou de olhar o que deveria."
    )
for _xml, _deve_pegar in (
    ('<ctrl:EstadoDaTela Carregando="{Binding C}" />', True),           # o defeito real
    ('<ctrl:EstadoDaTela Itens="{Binding X}" />', False),
    ('<ctrl:EstadoDaTela Vazio="False" Carregando="{Binding C}" />', False),
):
    _achou = not ("Itens=" in _xml or "Vazio=" in _xml)
    if _achou != _deve_pegar:
        erros.append(f"verificar-suite: a checagem 29 mudou de resposta para `{_xml}`.")


# --------------------------------------------------------------- checagem 30
# `EstadoDaTela` COM `Visibility` AMARRADA — o binding morre e o vazio vaza pela tela.
#
# O componente DECIDE a própria `Visibility` em `Recalcular()`, atribuindo um valor LOCAL.
# Em WPF, valor local atribuído por código **substitui o binding**: a tela liga a
# visibilidade a "estou mostrando o prontuário" e, na primeira mudança de `Itens`,
# `Carregando` ou `NaoVerificado`, o `Recalcular` sobrescreve e o binding deixa de existir.
#
# Daí em diante o vazio aparece quando a LISTA está vazia, e não quando a tela dele está
# aberta — foi assim que "Nenhuma sessão registrada" ficou escrito por cima da lista de
# pacientes, em produção, no Prontuário e nas Prescrições.
#
# Nada falha: XAML bem-formado, propriedade existente, binding válido. Só a tela montada
# mostra. Quem precisa de condição usa `Ativo`, que entra no cálculo em vez de brigar com
# ele.
for _arq in xamls():
    _texto = _arq.read_text(encoding="utf-8")
    for _m in re.finditer(r"<ctrl:EstadoDaTela\b[^>]*?/>", _texto, re.S):
        if "Visibility=" not in _m.group(0):
            continue
        erros.append(
            f"{rel(_arq)}:{_texto.count(chr(10), 0, _m.start()) + 1}: `EstadoDaTela` com "
            f"`Visibility` amarrada — o componente atribui a própria Visibility como valor "
            f"LOCAL e apaga esse binding, e aí o vazio passa a aparecer sobre a tela errada. "
            f"Use `Ativo=\"{{Binding …}}\"`."
        )

# --------------------------------------------------------------- checagem 31
# TEMPLATE COM `SharedSizeGroup` USADO SEM `Grid.IsSharedSizeScope`.
#
# `SharedSizeGroup` só alinha dentro de um ESCOPO. Cada linha de uma lista é um Grid
# próprio, então sem o escopo declarado por quem monta a lista as larguras são resolvidas
# POR LINHA: a linha que tem um selo a mais fica com a última coluna mais larga e empurra
# as colunas vizinhas daquela linha. A lista deixa de ter colunas e vira uma pilha de
# linhas que por acaso se parecem.
#
# O `ItemPacienteLinha` já trazia o aviso escrito no próprio comentário ("o escopo é
# declarado por quem monta a lista") — e três das quatro telas que o usam esqueceram.
# Contrato que depende de alguém lembrar é o que esta checagem existe para substituir.
_com_grupo = {
    m.group(1)
    for _f in xamls() if "Styles" in _f.parts
    for m in re.finditer(
        r'<DataTemplate x:Key="([^"]+)"(?:(?!</DataTemplate>).)*?SharedSizeGroup=',
        _f.read_text(encoding="utf-8"), re.S)
}

for _arq in xamls():
    if "Styles" in _arq.parts:
        continue
    _texto = _arq.read_text(encoding="utf-8")
    # ⚠️ Sem tirar os comentários, o COMENTÁRIO que explica a regra satisfaz a checagem e
    # ela cala para sempre. É o inverso da lição da checagem 19 (lá a prosa fazia disparar,
    # aqui faz silenciar) e o silêncio é pior: ninguém percebe uma checagem que passou.
    if "IsSharedSizeScope" in re.sub(r"<!--.*?-->", "", _texto, flags=re.S):
        continue
    for _tpl in sorted(_com_grupo):
        if f"{{StaticResource {_tpl}}}" not in _texto:
            continue
        erros.append(
            f"{rel(_arq)}: usa `{_tpl}`, que alinha as colunas por `SharedSizeGroup`, e "
            f"não declara `Grid.IsSharedSizeScope=\"True\"` em nenhum ancestral — sem o "
            f"escopo cada linha resolve a largura sozinha e a lista sai desalinhada."
        )

# Autoteste: a checagem tem de conhecer o template que originou a regra.
if _com_grupo and "ItemPacienteLinha" not in _com_grupo:
    erros.append(
        "verificar-suite: a checagem 31 não achou `SharedSizeGroup` no `ItemPacienteLinha` "
        "— o padrão de declaração mudou e ela parou de olhar o que deveria."
    )


# --------------------------------------------------------------- checagem 32
# `WrapPanel` QUE NUNCA VAI DOBRAR A LINHA.
#
# O WrapPanel decide onde quebrar a partir da largura que RECEBE na medição. Num pai que
# lhe dá largura infinita — docado à esquerda ou à direita num DockPanel, dentro de um
# StackPanel horizontal, numa coluna `Auto` de Grid, dentro de outro WrapPanel, num Canvas
# ou num ScrollViewer que rola na horizontal — ele mede como se tivesse a tela toda, alinha
# tudo numa linha só e EMPURRA o irmão para fora.
#
# É a barra de nove botões da agenda: no monitor de quem programa ela cabe e parece certa;
# no de 1366 px do balcão ela come o título e some pela direita. O `Auto` engana
# especialmente, porque a intenção declarada ("ocupa o que precisar") é exatamente o que
# impede a quebra.
#
# Nenhuma rede pegava: XAML bem-formado, painel existente, nada lança. Só a tela montada,
# e só na largura errada — que é a categoria mais cara, porque não reproduz na máquina de
# quem escreveu.
#
# A saída é fazer o WrapPanel ser o filho que PREENCHE (o último de um DockPanel, uma
# coluna `*` de Grid), e alinhá-lo à direita por `HorizontalAlignment` se for o caso.
NS_XAML = "{http://schemas.microsoft.com/winfx/2006/xaml/presentation}"


def _largura_infinita(pai: ET.Element, filho: ET.Element) -> str | None:
    """Por que este pai mede o filho com largura infinita? Nulo = ele constrange."""
    nome_pai = pai.tag.split("}")[-1]

    if nome_pai == "DockPanel":
        dock = filho.attrib.get(f"{NS_XAML}Dock") or filho.attrib.get("DockPanel.Dock")
        return f"docado à {dock.lower()} num DockPanel" if dock in ("Left", "Right") else None
    if nome_pai == "StackPanel":
        return ("dentro de um StackPanel horizontal"
                if pai.attrib.get("Orientation") == "Horizontal" else None)
    if nome_pai == "WrapPanel":
        return "dentro de outro WrapPanel"
    if nome_pai == "Canvas":
        return "dentro de um Canvas"
    if nome_pai == "ScrollViewer":
        return ("dentro de um ScrollViewer que rola na horizontal"
                if pai.attrib.get("HorizontalScrollBarVisibility") in ("Auto", "Visible")
                else None)
    if nome_pai == "Grid":
        coluna = filho.attrib.get(f"{NS_XAML}Column") or filho.attrib.get("Grid.Column") or "0"
        if not coluna.isdigit():
            return None
        definicoes = [
            cd for c in pai if c.tag.split("}")[-1] == "Grid.ColumnDefinitions" for cd in c
        ]
        indice = int(coluna)
        if indice >= len(definicoes):
            return None
        return ('numa coluna `Width="Auto"` de Grid'
                if definicoes[indice].attrib.get("Width") == "Auto" else None)
    return None


def _quem_recebe_a_largura(el, pais):
    """
    Sobe do WrapPanel até o elemento que de fato RECEBE a largura na medição.

    ⚠️ Dentro de `<ItemsControl.ItemsPanel><ItemsPanelTemplate>`, o pai do WrapPanel é o
    TEMPLATE, que não constrange nada: quem é medido é o ItemsControl. A primeira versão
    desta checagem parava no template e ficava calada — foi por esse buraco que a régua de
    chips da tela de Avaliações entrou num StackPanel horizontal.
    """
    atual = el
    while (pai := pais.get(atual)) is not None:
        nome_pai = pai.tag.split("}")[-1]
        nome_atual = atual.tag.split("}")[-1]

        # Três nós são invisíveis para a medição: o próprio template, a propriedade
        # `X.ItemsPanel` que o hospeda e — o degrau que faltava — o nó da propriedade visto
        # de baixo, cujo pai é o ItemsControl. É ELE que recebe a largura, e parar aqui
        # devolvia o ItemsControl como "pai", que nunca constrange.
        if (nome_pai == "ItemsPanelTemplate"
                or nome_pai.endswith(".ItemsPanel")
                or nome_atual.endswith(".ItemsPanel")):
            atual = pai
            continue
        return atual, pai
    return atual, None


_wraps = 0
for f, raiz in arvores_com_faturamento.items():
    pais = {filho: pai for pai in raiz.iter() for filho in pai}
    for el in raiz.iter():
        if el.tag.split("}")[-1] != "WrapPanel":
            continue
        _wraps += 1
        medido, pai = _quem_recebe_a_largura(el, pais)
        if pai is None:
            continue
        if (motivo := _largura_infinita(pai, medido)) is None:
            continue
        erros.append(
            f"{rel(f)}: `<WrapPanel>` {motivo} — ele é medido com largura INFINITA, nunca "
            f"dobra a linha e empurra o irmão para fora da tela. Faça dele o filho que "
            f"PREENCHE (último de um DockPanel, coluna `*` de Grid) e use "
            f"`HorizontalAlignment` para encostá-lo onde precisa."
        )

# Autoteste. O primeiro é a lição da checagem 31: uma checagem que deixa de ENXERGAR não
# reclama de nada e passa por limpa. Se um dia a suíte não tiver WrapPanel nenhum, é
# porque o padrão mudou — e é isso que precisa aparecer.
if _wraps == 0:
    erros.append(
        "verificar-suite: a checagem 32 não achou nenhum `<WrapPanel>` — o padrão de "
        "declaração mudou e ela parou de olhar o que deveria."
    )

# Os quatro pais que soltam a largura, e os dois que a prendem.
_amostras_32 = (
    ('<DockPanel {0}><WrapPanel DockPanel.Dock="Right" /><Border /></DockPanel>', True),
    ('<StackPanel {0} Orientation="Horizontal"><WrapPanel /></StackPanel>', True),
    ('<Grid {0}><Grid.ColumnDefinitions><ColumnDefinition Width="Auto" />'
     '</Grid.ColumnDefinitions><WrapPanel Grid.Column="0" /></Grid>', True),
    ('<DockPanel {0}><Border DockPanel.Dock="Left" /><WrapPanel /></DockPanel>', False),
    ('<Grid {0}><Grid.ColumnDefinitions><ColumnDefinition Width="*" />'
     '</Grid.ColumnDefinitions><WrapPanel Grid.Column="0" /></Grid>', False),
    ('<StackPanel {0}><WrapPanel /></StackPanel>', False),
)
for _xml, _deve_pegar in _amostras_32:
    _r = ET.fromstring(_xml.format(
        'xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"'))
    _pais = {c: p for p in _r.iter() for c in p}
    _wp = next(e for e in _r.iter() if e.tag.split("}")[-1] == "WrapPanel")
    if (_largura_infinita(_pais[_wp], _wp) is not None) != _deve_pegar:
        erros.append(
            f"verificar-suite: a checagem 32 mudou de resposta para `{_xml[:52]}…` "
            f"(esperado: {'pega' if _deve_pegar else 'deixa passar'})."
        )


# --- autoteste do ponto cego da 32 (a régua de chips) ---
#
# O caso real: WrapPanel dentro de um ItemsPanelTemplate, com o ItemsControl num
# StackPanel HORIZONTAL. Antes de a checagem subir pelo template, ela ficava calada.
_cenarios_32 = (
    ("chips num StackPanel horizontal",
     '<StackPanel xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation" '
     'Orientation="Horizontal"><ItemsControl><ItemsControl.ItemsPanel>'
     '<ItemsPanelTemplate><WrapPanel /></ItemsPanelTemplate>'
     '</ItemsControl.ItemsPanel></ItemsControl></StackPanel>', True),
    ("chips docados no topo de um DockPanel",
     '<DockPanel xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation">'
     '<ItemsControl DockPanel.Dock="Top"><ItemsControl.ItemsPanel>'
     '<ItemsPanelTemplate><WrapPanel /></ItemsPanelTemplate>'
     '</ItemsControl.ItemsPanel></ItemsControl></DockPanel>', False),
)
for _cenario, _xml, _deve_acusar in _cenarios_32:
    _raiz = ET.fromstring(_xml)
    _pais = {c: p for p in _raiz.iter() for c in p}
    _wp = next(e for e in _raiz.iter() if e.tag.split("}")[-1] == "WrapPanel")
    _medido, _pai = _quem_recebe_a_largura(_wp, _pais)
    _acusou = _pai is not None and _largura_infinita(_pai, _medido) is not None
    if _acusou != _deve_acusar:
        erros.append(
            f"verificar-suite: a checagem 32 mudou de resposta ({_cenario}) — "
            f"{'acusou' if _acusou else 'calou'}, esperado "
            f"{'acusar' if _deve_acusar else 'calar'}."
        )

# --------------------------------------------------------------- checagem 33
# XAML QUE DECLARA O `assembly=` DO PRÓPRIO PROJETO.
#
# `clr-namespace:X;assembly=Y` manda o WPF procurar o namespace X DENTRO do assembly Y.
# Quando Y é o próprio projeto do arquivo, o compilador de marcação não acha nada e recusa:
#
#     MC3074: The tag 'EstadoDaTela' does not exist in XML namespace
#             'clr-namespace:Clinica.Desktop.Controls;assembly=Clinica.Desktop.Shell'
#
# É o que acontece ao MOVER uma tela de um projeto para outro — o `xmlns` continua nomeando
# o assembly de origem, que agora é o de destino. Foi assim que a tela de Pacotes, ao subir
# do Financeiro para o shell, quebrou o build (parcela 60).
#
# ⚠️ Nenhuma rede local pegava. O XML é bem-formado, o `compilar-sombra` não lê o CORPO do
# XAML (ele só gera o `.g.cs` a partir de `x:Class` e `x:Name`), e o C# compila — o defeito
# existe só para o compilador de MARCAÇÃO, que roda no Windows. Sete minutos de CI por um
# `;assembly=` que sobra.
#
# A forma certa dentro do próprio projeto é `clr-namespace:X`, sem o sufixo.
ASSEMBLY_NO_XMLNS = re.compile(r'clr-namespace:[^"\';]+;assembly=([\w.]+)')

_xamls_33 = 0
for f in list(arvores_com_faturamento):
    # O projeto dono do arquivo é o diretório sob `src/` (mesma convenção do resto do script).
    partes = f.relative_to(RAIZ).parts
    if len(partes) < 2 or partes[0] != "src":
        continue
    projeto = partes[1]
    _xamls_33 += 1

    texto = f.read_text(encoding="utf-8")
    for m in ASSEMBLY_NO_XMLNS.finditer(texto):
        if m.group(1) != projeto:
            continue
        erros.append(
            f"{rel(f)}:{texto.count(chr(10), 0, m.start()) + 1}: o `xmlns` aponta para "
            f"`assembly={projeto}`, que é o PRÓPRIO projeto deste arquivo — o compilador de "
            f"marcação recusa com MC3074/MC3072 e só o CI acusa. Dentro do próprio projeto "
            f"escreva `clr-namespace:…` sem o `;assembly=`."
        )

# Autoteste. O primeiro é o guarda contra a checagem ficar cega (lição da 31/32); os
# demais são o caso real e os dois legítimos.
if _xamls_33 == 0:
    erros.append(
        "verificar-suite: a checagem 33 não achou XAML nenhum sob `src/<projeto>/` — a "
        "convenção de pastas mudou e ela parou de olhar o que deveria."
    )
for _decl, _projeto, _deve_pegar in (
    ('clr-namespace:Clinica.Desktop.Controls;assembly=Clinica.Desktop.Shell',
     'Clinica.Desktop.Shell', True),                       # o defeito real
    ('clr-namespace:Clinica.Desktop.Controls;assembly=Clinica.Desktop.Shell',
     'Clinica.Modulo.Recepcao', False),                    # módulo referenciando o shell
    ('clr-namespace:Clinica.Desktop.Controls',
     'Clinica.Desktop.Shell', False),                      # a forma certa dentro do projeto
):
    _m = ASSEMBLY_NO_XMLNS.search(_decl)
    if (_m is not None and _m.group(1) == _projeto) != _deve_pegar:
        erros.append(
            f"verificar-suite: a checagem 33 mudou de resposta para `{_decl}` "
            f"em `{_projeto}` (esperado: {'pega' if _deve_pegar else 'deixa passar'})."
        )


# ------------------------------------------------------------- checagem 33-B
# O ESPELHO DA 33: `clr-namespace:` SEM `assembly=` para um namespace que o projeto NÃO
# declara.
#
#     MC3074: The tag 'TextoParaVisibilidade' does not exist in XML namespace
#             'clr-namespace:Clinica.Desktop.Controls'.
#
# A 33 pega o `;assembly=` que SOBRA (tela movida entre projetos). Este é o mesmo erro pelo
# avesso — o `;assembly=` que FALTA —, e foi ele que quebrou o build na parcela 66: a
# `ModelosTermoWindow` do Gerente declarou `clr-namespace:Clinica.Desktop.Controls` sem o
# sufixo, e o tipo mora no shell. Sem o `assembly=`, o WPF procura o namespace DENTRO do
# próprio projeto e não acha.
#
# ⚠️ A 33 nasceu cobrindo só uma direção porque só uma tinha mordido. A lição da parcela 66:
# **checagem que cobre um sentido de um erro simétrico está metade cega** — e a metade que
# falta é a que a próxima pessoa vai cometer.
#
# O critério é textual e conservador: só reclama quando NENHUM arquivo `.cs` do projeto
# declara aquele namespace. Assim a checagem cala para namespace do próprio projeto (o caso
# legítimo) e para qualquer coisa que ela não saiba resolver.
XMLNS_SEM_ASSEMBLY = re.compile(r'clr-namespace:([\w.]+)(?=["\'])')

_namespaces_por_projeto: dict[str, set[str]] = {}
for _cs in RAIZ.glob("src/*/**/*.cs"):
    _partes = _cs.relative_to(RAIZ).parts
    if len(_partes) < 2:
        continue
    for _ns in re.findall(r'^\s*namespace\s+([\w.]+)', _cs.read_text(encoding="utf-8"), re.M):
        _namespaces_por_projeto.setdefault(_partes[1], set()).add(_ns)

_xamls_33b = 0
for f in list(arvores_com_faturamento):
    partes = f.relative_to(RAIZ).parts
    if len(partes) < 2 or partes[0] != "src":
        continue
    projeto = partes[1]
    _xamls_33b += 1

    declarados = _namespaces_por_projeto.get(projeto, set())
    if not declarados:
        continue  # projeto sem .cs lido: "não sei" cala, como na 34

    texto = f.read_text(encoding="utf-8")
    for m in XMLNS_SEM_ASSEMBLY.finditer(texto):
        ns = m.group(1)
        if ns in declarados:
            continue
        erros.append(
            f"{rel(f)}:{texto.count(chr(10), 0, m.start()) + 1}: o `xmlns` diz "
            f"`clr-namespace:{ns}` sem `;assembly=`, mas `{projeto}` não declara esse "
            f"namespace em nenhum `.cs` — o WPF procura dentro do próprio projeto e recusa "
            f"com MC3074. Acrescente `;assembly=<projeto que declara o tipo>`."
        )

if _xamls_33b == 0:
    erros.append(
        "verificar-suite: a checagem 33-B não achou XAML nenhum sob `src/<projeto>/`."
    )

# Autoteste: o caso real da parcela 66, e os dois legítimos.
for _ns, _projeto, _deve_pegar in (
    # O defeito real: o Gerente usando um tipo do shell sem dizer o assembly.
    ("Clinica.Desktop.Controls", "Clinica.Modulo.Gerente", True),
    # Legítimo: o shell declara esse namespace, então sem `assembly=` está certo.
    ("Clinica.Desktop.Controls", "Clinica.Desktop.Shell", False),
    # Legítimo: o faturamento também o declara (os dois design systems, parcela 7).
    ("Clinica.Desktop.Controls", "Clinica.Desktop", False),
):
    _declarados = _namespaces_por_projeto.get(_projeto, set())
    if _declarados and ((_ns not in _declarados) != _deve_pegar):
        erros.append(
            f"verificar-suite: a checagem 33-B mudou de resposta para `{_ns}` em "
            f"`{_projeto}` (esperado: {'pega' if _deve_pegar else 'deixa passar'})."
        )


# --------------------------------------------------------------- checagem 34
# ATRIBUTO QUE NÃO É PROPRIEDADE DO CONTROLE PRÓPRIO.
#
#     MC3072: The property 'TextoVazio' does not exist in XML namespace
#             'clr-namespace:Clinica.Desktop.Controls'.
#
# Foi o que quebrou o build na parcela 63: três telas novas declararam `TextoVazio` no
# `EstadoDaTela`, que tem `TextoCarregando` e `TextoNaoVerificado` — mas o vazio se escreve
# com `Titulo` + `Descricao`. Errar o nome de uma propriedade de um controle da CASA é o
# caso mais fácil de cometer, porque o nome plausível existe ao lado do nome certo.
#
# ⚠️ Nenhuma rede local pegava, pela mesma razão da 33: o XML é bem-formado, o
# `compilar-sombra` não lê o CORPO do XAML e o C# compila. Só o compilador de MARCAÇÃO
# recusa — sete minutos de CI por um nome de atributo.
#
# O que ela olha: todo elemento `<prefixo:Tipo ...>` cujo `xmlns` do prefixo aponta para um
# `clr-namespace` DO REPOSITÓRIO. Para cada atributo simples, exige que exista uma
# propriedade (ou DP) com aquele nome no tipo — procurada no .cs por todo o repositório,
# incluindo as classes-base declaradas aqui.
#
# O que ela IGNORA de propósito, para não virar ruído que alguém desliga:
#   - `x:`, `xmlns`, e as anexadas (`Grid.Row`, `Panel.ZIndex`, `DockPanel.Dock`…), que são
#     de OUTRO tipo;
#   - tipo que o script não conseguiu achar no repositório — sem a definição não há como
#     responder, e chutar produziria falso positivo em controle de biblioteca;
#   - propriedade herdada de `Control`/`FrameworkElement` (Margin, Style, Visibility…),
#     resolvida por uma lista curta do que o WPF já dá a todo elemento.

# O que todo FrameworkElement/Control já tem — não é preciso achá-lo no repositório.
HERDADAS_WPF = {
    "Name", "Style", "Margin", "Padding", "Width", "Height", "MinWidth", "MinHeight",
    "MaxWidth", "MaxHeight", "HorizontalAlignment", "VerticalAlignment", "Visibility",
    "IsEnabled", "IsHitTestVisible", "Opacity", "Background", "Foreground", "BorderBrush",
    "BorderThickness", "FontFamily", "FontSize", "FontWeight", "FontStyle", "ToolTip",
    "Cursor", "Focusable", "Tag", "DataContext", "Resources", "RenderTransform",
    "HorizontalContentAlignment", "VerticalContentAlignment", "Content", "ContentTemplate",
    "SnapsToDevicePixels", "UseLayoutRounding", "ClipToBounds", "Template",
}

# Bases do WPF cujo repertório HERDADAS_WPF já cobre. Uma cadeia que termina aqui pode
# responder "não tem"; uma que termina num tipo de fora desconhecido responde "não sei".
BASES_WPF = {
    "Control", "UserControl", "Window", "ContentControl", "ItemsControl", "Panel",
    "Border", "Decorator", "FrameworkElement", "UIElement", "Button", "ButtonBase",
    "TextBlock", "TextBox", "ToggleButton", "IValueConverter", "DependencyObject",
}

# `prefixo="clr-namespace:…"` declarado no XAML.
XMLNS_CLR = re.compile(r'xmlns:(\w+)\s*=\s*"clr-namespace:([^"]+)"')
# `<prefixo:Tipo` e o corpo da tag até `>` ou `/>`.
TAG_PREFIXADA = re.compile(r'<(\w+):(\w+)((?:\s+[^<>]*?)?)/?>', re.S)
# `Atributo="…"` dentro do corpo da tag (sem os anexados, que têm ponto).
ATRIBUTO_SIMPLES = re.compile(r'(?<![\w.:])([A-Z]\w*)\s*=\s*"')


def _membros_do_tipo(nome, _cache={}):
    """Propriedades declaradas no tipo e nas bases dele, ou None se o tipo não está aqui."""
    if nome in _cache:
        return _cache[nome]

    achado = None
    for cs in RAIZ.joinpath("src").rglob("*.cs"):
        txt = cs.read_text(encoding="utf-8", errors="ignore")
        m = re.search(
            rf'\b(?:class|record)\s+{re.escape(nome)}\b\s*(?::\s*([\w<>, ]+))?', txt)
        if m is None:
            continue

        membros = set(re.findall(r'\bpublic\s+(?:static\s+)?[\w<>?\[\], .]+?\s+(\w+)\s*(?:\{|=>)', txt))
        # DPs registradas: `DependencyProperty.Register(nameof(X)` — a propriedade CLR
        # costuma existir logo abaixo, mas registrar aqui também cobre o caso solto.
        membros |= set(re.findall(r'DependencyProperty\.Register\w*\(\s*nameof\((\w+)\)', txt))

        bases = [b.strip() for b in (m.group(1) or "").split(",") if b.strip()]
        achado = (membros, bases)
        break

    _cache[nome] = achado
    return achado


def _tem_membro(tipo, atributo, _vistos=None):
    _vistos = _vistos or set()
    if tipo in _vistos:
        return None
    _vistos.add(tipo)

    info = _membros_do_tipo(tipo)
    if info is None:
        return None          # tipo de fora do repositório: sem resposta, e sem chute

    membros, bases = info
    if atributo in membros:
        return True

    for base in bases:
        base = base.split("<")[0]
        if _tem_membro(base, atributo, _vistos):
            return True

    # Achamos o tipo e não achamos o membro na cadeia daqui. A resposta depende de ONDE a
    # cadeia termina:
    #   - numa base do WPF que a gente conhece (Control, UserControl…): o que ela dá está
    #     em HERDADAS_WPF, então NÃO ter o membro é resposta — e é o caso da esmagadora
    #     maioria dos controles da casa, sem o qual a checagem seria inútil;
    #   - numa base de fora que não conhecemos: "não sei", e calar é o certo.
    for base in bases:
        base = base.split("<")[0].strip()
        if _membros_do_tipo(base) is None and base not in BASES_WPF:
            return None
    return False


_tags_34 = 0
for f in list(arvores_com_faturamento):
    texto = f.read_text(encoding="utf-8")
    proprios = {p for p, ns in XMLNS_CLR.findall(texto)}
    if not proprios:
        continue

    for m in TAG_PREFIXADA.finditer(texto):
        prefixo, tipo, corpo = m.group(1), m.group(2), m.group(3) or ""
        if prefixo not in proprios or "." in tipo:
            continue
        if _membros_do_tipo(tipo) is None:
            continue
        _tags_34 += 1

        for atributo in set(ATRIBUTO_SIMPLES.findall(corpo)):
            if atributo in HERDADAS_WPF:
                continue
            if _tem_membro(tipo, atributo) is False:
                erros.append(
                    f"{rel(f)}:{texto.count(chr(10), 0, m.start()) + 1}: `{tipo}` não tem a "
                    f"propriedade `{atributo}` — o compilador de marcação recusa com MC3072 "
                    f"e só o CI acusa. Confira o nome no controle."
                )

# Autoteste: o guarda contra a checagem ficar cega, e o caso real da parcela 63.
if _tags_34 == 0:
    erros.append(
        "verificar-suite: a checagem 34 não examinou nenhuma tag de controle próprio — os "
        "`xmlns:` mudaram de forma e ela parou de olhar o que deveria."
    )
for _tipo, _prop, _esperado in (
    ("EstadoDaTela", "TextoVazio", False),           # o defeito real (MC3072 no CI)
    ("EstadoDaTela", "TextoNaoVerificado", True),    # o nome certo, ao lado do errado
    ("EstadoDaTela", "Titulo", True),
    ("EstadoDaTela", "Itens", True),
):
    if _tem_membro(_tipo, _prop) is not _esperado:
        erros.append(
            f"verificar-suite: a checagem 34 mudou de resposta para `{_tipo}.{_prop}` "
            f"(esperado: {_esperado})."
        )

# Autoteste da checagem 18 e da SAÍDA CONSCIENTE (parcela 67).
#
# ⚠️ Ele CHAMA as funções da checagem (`_usa`, `_corpo_do_up`, `_dispensa_declarada`) em vez
# de repetir a lógica delas. A primeira versão reimplementava a leitura da marca linha a
# linha — e um autoteste que reimplementa não testa nada: ele continua verde exatamente
# quando a checagem quebra, porque a cópia dentro dele não quebrou junto. Foi o que a
# revisão desta parcela apontou, e vale para toda checagem futura.
_UP = "protected override void Up(MigrationBuilder migrationBuilder)\n{{\n    {0}\n}}\n"
_DROP = 'migrationBuilder.DropIndex(name: "IX_x", table: "T");'
_ALTER = 'migrationBuilder.AlterColumn<string>(name: "C", table: "T", maxLength: 10);'
_DROPCOL = 'migrationBuilder.DropColumn(name: "C", table: "T");'
_MARCA_OK = f"/// {MARCA_CONSCIENTE}(DropIndex): alarga a chave."

for _cenario, _texto, _esperadas_cobertas, _esperadas_descobertas in (
    # Sem marca: tudo é erro.
    ("sem marca", _UP.format(_DROP), set(), {"DropIndex"}),
    # Com marca nomeando a operação: só ela é dispensada.
    ("marca nomeando a operação", f"{_MARCA_OK}\n{_UP.format(_DROP)}", {"DropIndex"}, set()),
    # ⚠️ O achado grave: a marca de um DropIndex NÃO pode cobrir um DropColumn ao lado.
    ("marca não cobre a operação vizinha",
     f"{_MARCA_OK}\n{_UP.format(_DROP + chr(10) + '    ' + _DROPCOL)}",
     {"DropIndex"}, {"DropColumn"}),
    # Marca sem operação declarada não vale: ela É a exceção, não um interruptor.
    ("marca sem operação", f"/// {MARCA_CONSCIENTE}: alarga.\n{_UP.format(_DROP)}",
     set(), {"DropIndex"}),
    ("marca sem razão", f"/// {MARCA_CONSCIENTE}(DropIndex):\n{_UP.format(_DROP)}",
     set(), {"DropIndex"}),
    # ⚠️ A forma GENÉRICA que o EF realmente gera — era o buraco por onde o AlterColumn
    # (a operação mais destrutiva da lista) passava sem um pio.
    ("AlterColumn genérico", _UP.format(_ALTER), set(), {"AlterColumn"}),
):
    _corpo = _corpo_do_up(_texto)
    _achadas = {d for d in DESTRUTIVAS if _usa(_corpo, d)}
    _dispensadas, _razao = _dispensa_declarada(_texto)

    if (_achadas & _dispensadas) != _esperadas_cobertas or \
       (_achadas - _dispensadas) != _esperadas_descobertas:
        erros.append(
            f"verificar-suite: a checagem 18 mudou de resposta ({_cenario}) — "
            f"dispensadas={sorted(_achadas & _dispensadas)}, "
            f"cobradas={sorted(_achadas - _dispensadas)}; esperado "
            f"{sorted(_esperadas_cobertas)} / {sorted(_esperadas_descobertas)}."
        )

# --------------------------------------------------------------- checagem 35
# BINDING NA FAMÍLIA DO CONVÊNIO — a tela pergunta a REGRA e escreve o nome de OUTRA
# OPERADORA.
#
# O cliente achou em produção: oito pacientes Sul América apareciam faturando como
# "Porto Saúde" na consulta de guias, enquanto a lista de pacientes ao lado — que amarra
# `ConvenioNome` — mostrava "SULAMERICA" para os mesmos oito.
#
# `Convenio` é a FAMÍLIA DE REGRA, não a operadora: `Convenio.Personalizado` é a regra que
# TODA operadora cadastrada pela clínica compartilha. Resolver o nome por ela tem duas
# saídas, e as duas são erradas:
#
#   · sem conversor, o WPF chama `ToString()` → "UnimedIntercambio" (o defeito da parcela
#     41, que a checagem 20 não pega aqui porque ela só olha `ComboBox`);
#   · com o conversor, cai em `CatalogoConvenios.Nome(familia)` → o nome da linha embutida
#     da família. E essa linha é RENOMEÁVEL: a clínica renomeou a "Personalizado" para a
#     primeira operadora que cadastrou, e a partir daí toda personalizada passou a se
#     chamar assim.
#
# ⚠️ O segundo é muito pior que o primeiro, e é por isso que esta checagem existe apesar de
# a 20 já cobrir o vizinho: "UnimedIntercambio" na tela é obviamente um defeito e alguém
# abre um chamado. O nome de uma operadora de verdade na guia de outra operadora tem toda a
# cara de estar certo, e só é descoberto quando a clínica compara duas telas.
#
# A checagem resolve o TIPO, nunca o nome: `Convenio` é `string` já resolvida em uma dúzia
# de records de ViewModel (Conciliação, Pendências do Gerente, Consultas da Recepção), e
# reclamar deles seria o ruído que faz alguém desligar a ferramenta. Dois caminhos de
# resolução, e o que não resolver CALA:
#
#   1. caminho com dono explícito — `Atendimento.Paciente.Convenio`: o penúltimo segmento
#      nomeia o tipo, e se esse tipo declara `Convenio` como o ENUM, é defeito;
#   2. caminho nu — `{Binding Convenio}` dentro de um `ItemsSource="{Binding Itens}"`: o
#      tipo do item vem do `_TIPOS` da checagem 20 (nome da coleção → tipo do elemento).
#
# O remédio é sempre o mesmo: `ConvenioNome`, que resolve pelo CÓDIGO do catálogo com a
# família como caminho de baixo. Ele existe em `Paciente` e nos records de pendência,
# relatório e consulta.

# `Convenio X` / `Convenio? X` como parâmetro posicional de record ou propriedade — mas
# NUNCA `string Convenio`, que é o caso legítimo do record de ViewModel já resolvido.
_DECL_CONVENIO_ENUM = re.compile(r"(?<![\w.])Convenio\??\s+(\w+)\s*(?:[,)\{;=]|$)", re.M)
_BINDING_CAMINHO = re.compile(r"\{Binding\s+(?:Path\s*=\s*)?([A-Za-z_][\w.]*)")
_ITEMS_SOURCE = re.compile(r"^\{Binding\s+(?:Path\s*=\s*)?([A-Za-z_][\w.]*)")


def _tipos_com_convenio_enum(_cache={}) -> set[str]:
    """
    Tipos que declaram `Convenio` como ENUM **e** oferecem o `ConvenioNome` resolvido.

    ⚠️ As duas metades são o que separa o defeito do uso legítimo, e sem a segunda a
    checagem acusa a tela de Configurações: a tabela "Regras por família de convênio"
    (`ParametroConvenio`) é POR FAMÍLIA mesmo — é o assunto dela, e ali não existe
    operadora a resolver. Quem OFERECE `ConvenioNome` está dizendo que sabe qual é a
    operadora; amarrar a família nesse tipo é escolher a resposta pior tendo a boa ao lado.
    """
    if _cache:
        return _cache["r"]

    achados: set[str] = set()
    for cs in RAIZ.rglob("src/**/*.cs"):
        if "/obj/" in str(cs) or "/bin/" in str(cs):
            continue
        txt = cs.read_text(encoding="utf-8", errors="ignore")
        # Recorta cada declaração de tipo até a próxima, para não atribuir a um tipo o
        # membro do vizinho declarado no mesmo arquivo (Pendencia.cs tem quatro).
        marcas = [(m.start(), m.group(1))
                  for m in re.finditer(r"\b(?:class|record|struct)\s+(\w+)", txt)]
        for i, (ini, nome) in enumerate(marcas):
            fim = marcas[i + 1][0] if i + 1 < len(marcas) else len(txt)
            corpo = txt[ini:fim]
            if "Convenio" in _DECL_CONVENIO_ENUM.findall(corpo) and "ConvenioNome" in corpo:
                achados.add(nome)

    _cache["r"] = achados
    return achados


_CONVENIO_ENUM = _tipos_com_convenio_enum()


def _acusar_convenio(arq: Path, caminho: str, dono: str | None) -> None:
    # O último SEGMENTO tem de ser exatamente `Convenio`. Um `endswith` sobre o caminho
    # inteiro acusava `{Binding PorConvenio}` — o ItemsSource do relatório —, que é uma
    # COLEÇÃO e não a família de ninguém. Falso positivo é o que faz alguém desligar a
    # ferramenta, e aí ela deixa de pegar o defeito de verdade.
    if caminho.split(".")[-1] != "Convenio":
        return
    if dono is None or dono not in _CONVENIO_ENUM:
        return  # tipo desconhecido, ou `Convenio` string: sem resposta, e sem chute
    erros.append(
        f"{rel(arq)}: `{{Binding {caminho}}}` amarra a FAMÍLIA de regra do convênio "
        f"(`{dono}.Convenio`), não a operadora — toda personalizada sai com o mesmo nome "
        f"(oito pacientes Sul América apareceram como \"Porto Saúde\"). Use `ConvenioNome`."
    )


def _colecoes_do_dono(arq: Path, _cache={}) -> dict[str, set[str]]:
    """
    Coleções declaradas no ViewModel DESTA tela (`FooView.xaml` ↔ `FooViewModel.cs`).

    O `_TIPOS` da checagem 20 é global — nome de propriedade → tipo do elemento, somado
    sobre o repositório inteiro. Para lista de enum aquilo basta; aqui não: `Itens` e
    `PorConvenio` existem em telas diferentes com tipos diferentes, e a busca global
    respondia o tipo do VIZINHO. Foi assim que a primeira versão desta checagem acusou
    três telas do Gerente que já mostram o nome resolvido — a resposta certa, vinda do
    arquivo errado. Tela cujo ViewModel não é achado por nome fica sem resolução, e o
    binding nu dela é PULADO: o certo aqui é não afirmar.
    """
    chave = str(arq)
    if chave in _cache:
        return _cache[chave]

    tipos: dict[str, set[str]] = {}
    dono = arq.parent.parent / "ViewModels" / (arq.stem + "Model.cs")
    if arq.stem.endswith("View") and dono.exists():
        for tipo, nome in COLECAO_TIPADA.findall(dono.read_text(encoding="utf-8", errors="ignore")):
            tipos.setdefault(nome[0].upper() + nome[1:], set()).add(tipo)

    _cache[chave] = tipos
    return tipos


for _arq, _raiz in arvores_com_faturamento.items():
    for _el in _raiz.iter():
        # 1. Tipo do item desta lista, para resolver os bindings nus lá dentro.
        _item = None
        if (_src := _el.get("ItemsSource")) and (_m := _ITEMS_SOURCE.match(_src.strip())):
            _nome_col = _m.group(1).split(".")[-1]
            _cands = _colecoes_do_dono(_arq).get(
                _nome_col[0].upper() + _nome_col[1:], set()) & _CONVENIO_ENUM
            _item = next(iter(_cands)) if len(_cands) == 1 else None

        _alvos = [_el] if _item is None else list(_el.iter())
        for _filho in _alvos:
            for _valor in _filho.attrib.values():
                for _cam in _BINDING_CAMINHO.findall(_valor):
                    _partes = _cam.split(".")
                    # Caminho com dono explícito resolve sozinho, em qualquer contexto;
                    # o nu só resolve quando a lista deu o tipo do item.
                    _dono = _partes[-2] if len(_partes) >= 2 else _item
                    _acusar_convenio(_arq, _cam, _dono)

# --- autoteste da 35: ela tem de disparar no caso REAL e calar nos legítimos ---
for _cenario, _xaml, _deve_disparar in (
    ("o defeito real (dono explícito)",
     '<DataGridTextColumn Binding="{Binding Atendimento.Paciente.Convenio}" />', True),
    ("o conserto",
     '<DataGridTextColumn Binding="{Binding Atendimento.Paciente.ConvenioNome}" />', False),
    # `Convenio` como string já resolvida no record do ViewModel: é o caso da Conciliação
    # e das telas do Gerente. Reclamar delas seria o ruído que desliga a ferramenta.
    ("string já resolvida no VM",
     '<TextBlock Text="{Binding Convenio}" />', False),
    ("tipo de fora / desconhecido",
     '<TextBlock Text="{Binding Fulano.Convenio}" />', False),
):
    _antes = len(erros)
    _el_teste = ET.fromstring(_xaml.replace("DataGridTextColumn", "T").replace("TextBlock", "T"))
    for _valor in _el_teste.attrib.values():
        for _cam in _BINDING_CAMINHO.findall(_valor):
            _p = _cam.split(".")
            _acusar_convenio(RAIZ / "autoteste.xaml", _cam, _p[-2] if len(_p) >= 2 else None)
    _disparou = len(erros) > _antes
    del erros[_antes:]
    if _disparou != _deve_disparar:
        erros.append(
            f"verificar-suite: a checagem 35 mudou de resposta ({_cenario}) — "
            f"disparou={_disparou}, esperado={_deve_disparar}."
        )

# --------------------------------------------------------------- checagem 36
# TABELAS EMPILHADAS NUM StackPanel, SEM ROLAGEM — o fim da tela é CORTADO, e não há barra
# nem como alcançá-lo.
#
# A cliente mandou a foto: na tela de Relatórios, a seção "Não conformidades (guias
# justificadas na rodada)" aparecia só com o TÍTULO, decepada na borda de baixo da janela.
#
# A mecânica: a janela tem altura FINITA, e `StackPanel` vertical dá a cada filho a altura
# que ele PEDE, ignorando o que há disponível. Três cards com tabela empilhados somam mais
# que a tela, e sem `ScrollViewer` o que passa do fim é cortado em silêncio.
#
# ⚠️ A checagem NÃO olha a linha `*`, e isso foi corrigido depois de ela nascer errada: a
# primeira versão exigia que o empilhamento estivesse dentro de uma linha `*`, e por isso
# ficou CEGA para a variante pior — a mesma pilha com todas as linhas `Auto`, que corta
# igual. A linha `*` era coincidência do caso real, não a causa. **Quando a causa e o
# sintoma aparecem juntos no primeiro exemplo, confira qual dos dois a checagem está
# olhando.**
#
# O que ela NÃO acusa, de propósito: a tela cujo conteúdo elástico é UM `DataGrid` numa
# linha `*` (Consultar guias, Faturados, TISS, Glosas). Aquele rola por dentro e a linha
# `*` absorve o resto — cinco telas do faturamento têm essa forma e continuam caladas.
#
# ⚠️ Nenhuma rede pegava, e é a categoria mais cara: o XAML é bem-formado, o
# `compilar-sombra` não lê o corpo, o compilador de marcação não tem o que reclamar e nada
# lança em runtime. Só a tela montada mostra — e só em quem tem a janela mais baixa que o
# conteúdo, que nunca é a máquina de quem escreveu.
#
# O remédio é o padrão da casa (`DashboardView`): `ScrollViewer` na raiz com
# `VerticalScrollBarVisibility="Auto"`, todas as linhas `Auto`, e `MaxHeight` nas grades
# que crescem com o dado, para elas voltarem a rolar por dentro em vez de esticar sem fim.

# Um cartão/seção: o que empilhado vira altura. `Border` é o `Card` do design system.
_BLOCOS = {"Border", "GroupBox", "Expander", "DockPanel", "Grid", "StackPanel"}


def _cresce_com_dado(el: ET.Element) -> bool:
    """Tem uma lista lá dentro — ou seja, altura que depende de quantas linhas vierem."""
    return any(_nome(d) in ("DataGrid", "ItemsControl", "ListBox", "ListView")
               for d in el.iter())


def _pilha_sem_rolagem(raiz: ET.Element) -> int:
    """Tamanho da maior pilha de blocos-com-tabela sem rolagem em cima. 0 = tela sã."""
    # Uma rolagem em QUALQUER lugar da tela já resolve — o que importa é o empilhamento
    # ter para onde crescer, não onde exatamente está o ScrollViewer.
    if any(_nome(e) == "ScrollViewer" for e in raiz.iter()):
        return 0
    # StackPanel VERTICAL (o padrão) que empilhe dois ou mais blocos com tabela dentro.
    for sp in raiz.iter():
        if _nome(sp) != "StackPanel" or sp.get("Orientation") == "Horizontal":
            continue
        pilha = [c for c in sp if _nome(c) in _BLOCOS and _cresce_com_dado(c)]
        if len(pilha) >= 2:
            return len(pilha)
    return 0


for _arq, _raiz in arvores_com_faturamento.items():
    if (_n := _pilha_sem_rolagem(_raiz)) > 0:
        erros.append(
            f"{rel(_arq)}: {_n} blocos com tabela empilhados num StackPanel e a tela não "
            f"tem ScrollViewer — a janela tem altura finita e o StackPanel dá a cada filho "
            f"a altura que ele pede, então o que passa do fim é CORTADO sem barra e sem "
            f"como alcançar (foi assim que a seção \"Não conformidades\" sumiu do "
            f"Relatórios). Use ScrollViewer na raiz, linhas `Auto` e MaxHeight nas grades."
        )

# --- autoteste da 36: dispara nas duas formas reais e cala nas legítimas ---
#
# ⚠️ O autoteste CHAMA `_pilha_sem_rolagem`, a MESMA função que a varredura usa, em vez de
# repetir a lógica dela linha a linha. Reimplementar aqui produziria um teste que fica
# verde exatamente quando a checagem quebra, porque a cópia não quebra junto — a lição da
# parcela 67.
_ENV = f'<UserControl xmlns="{NS_XAML[1:-1]}">{{}}</UserControl>'
_CARD = '<Border><DockPanel><DataGrid /></DockPanel></Border>'
_LINHAS = '<Grid.RowDefinitions><RowDefinition Height="Auto" /><RowDefinition Height="{}" /></Grid.RowDefinitions>'
for _cenario, _corpo, _deve in (
    # A forma real do Relatórios antes da correção: a pilha dentro de uma linha `*`.
    ("o defeito real (linha `*`)",
     f'<Grid>{_LINHAS.format("*")}<Grid Grid.Row="1">'
     f'<StackPanel>{_CARD}{_CARD}{_CARD}</StackPanel></Grid></Grid>', True),
    # A VARIANTE que a primeira versão da checagem não via, e que corta igual: a mesma
    # pilha com todas as linhas `Auto`. É por causa dela que o gate de linha `*` saiu.
    ("a mesma pilha com linhas `Auto`",
     f'<Grid>{_LINHAS.format("Auto")}<Grid Grid.Row="1">'
     f'<StackPanel>{_CARD}{_CARD}{_CARD}</StackPanel></Grid></Grid>', True),
    # O conserto: a mesma pilha, com rolagem em cima.
    ("com ScrollViewer",
     f'<ScrollViewer><Grid>{_LINHAS.format("Auto")}<Grid Grid.Row="1">'
     f'<StackPanel>{_CARD}{_CARD}{_CARD}</StackPanel></Grid></Grid></ScrollViewer>', False),
    # A forma SEGURA das outras telas do faturamento: UMA grade na linha `*`, que rola por
    # dentro. Acusá-las seria o ruído que faz alguém desligar a ferramenta.
    ("uma grade só na linha `*`",
     f'<Grid>{_LINHAS.format("*")}<Border Grid.Row="1"><DataGrid /></Border></Grid>', False),
    # Pilha de blocos SEM lista: texto e botões não crescem com o dado.
    ("pilha sem tabela",
     f'<Grid>{_LINHAS.format("*")}<StackPanel Grid.Row="1"><Border><TextBlock /></Border>'
     f'<Border><TextBlock /></Border></StackPanel></Grid>', False),
):
    _disparou = _pilha_sem_rolagem(ET.fromstring(_ENV.format(_corpo))) > 0
    if _disparou != _deve:
        erros.append(
            f"verificar-suite: a checagem 36 mudou de resposta ({_cenario}) — "
            f"disparou={_disparou}, esperado={_deve}."
        )

# ======================================================================
# CHECAGEM 37 — ViewModel do shell que recebe serviço SCOPED no construtor
# ======================================================================
#
# O shell resolve toda tela do provedor RAIZ: `SuiteApp` passa `host.Services` ao
# `ShellViewModel`, que o entrega a `IModuloApp.CriarTela`, que chama
# `GetRequiredService<FooViewModel>()`. Serviço registrado como `AddScoped` (ou o
# `DbContext`, que é Scoped por padrão) pedido à RAIZ vive no escopo raiz — isto é, pela
# vida inteira do aplicativo.
#
# Nada falha. O que acontece é pior:
#
#   1. A tela DEIXA DE VER a outra máquina. A consulta é rastreada e o EF não sobrescreve
#      valores de entidade já rastreada — reler devolve o que o contexto já tinha. Foi
#      assim que a fila do balcão parou de receber a chamada carimbada no consultório, e
#      que as contagens do painel ficavam congeladas no número da abertura do app.
#   2. `DbContext` não aceita duas operações ao mesmo tempo. A batida do relógio caindo em
#      cima de um clique vira "A second operation was started on this context instance" —
#      erro em inglês, no balcão, com o paciente na frente.
#
# O `ValidateScopes` do host pegaria isto na abertura, mas ele só vem ligado no ambiente
# de Development: em produção a resolução passa calada. Por isso a rede é aqui.
#
# ⚠️ `PENDENTES` é a dívida MEDIDA, não uma licença: quem está na lista vira AVISO, quem
# não está vira ERRO. Tela nova nasce cobrada, e a lista só encolhe.

# ZERADA na parcela 69: os nove que a varredura achou foram corrigidos, os dois da Agenda
# primeiro e os sete do Financeiro/Gerente em seguida. O conjunto continua aqui porque é o
# caminho de volta — tela nova que apareça já nasce como ERRO, e nada precisa ser afrouxado
# para acomodá-la.
_PENDENTES_ESCOPO: set[str] = set()


def _servicos_scoped() -> set[str]:
    """Os tipos registrados como Scoped (o DbContext entra: é Scoped por padrão)."""
    nomes: set[str] = {"IClinicaRepositorio"}
    for arq in RAIZ.glob("src/**/DependencyInjection.cs"):
        texto = _sem_comentarios(arq.read_text(encoding="utf-8"))
        nomes |= set(re.findall(r"AddScoped<(?:[\w\.]+,\s*)?(\w+)>", texto))
        nomes |= set(re.findall(r"AddDbContext<(\w+)>", texto))
    return nomes


def _vms_do_shell() -> set[str]:
    """ViewModels que o shell resolve em `CriarTela` — os de vida longa."""
    nomes: set[str] = set()
    for arq in RAIZ.glob("src/**/Modulo/*.cs"):
        texto = _sem_comentarios(arq.read_text(encoding="utf-8"))
        if "CriarTela" not in texto:
            continue
        nomes |= set(re.findall(r"GetRequiredService<(\w*ViewModel)>", texto))
    return nomes


def _scoped_do_texto(vm: str, texto: str, scoped: set[str]) -> list[str]:
    """Os serviços Scoped que o construtor declarado NESTE texto recebe.

    Separada de `_scoped_no_construtor` para o autoteste poder alimentá-la com um caso
    sintético — a varredura e o teste chamam a MESMA função, que é a regra da parcela 67
    (autoteste que reimplementa fica verde exatamente quando a checagem quebra).
    """
    limpo = _sem_comentarios(texto)
    m = re.search(r"public\s+" + re.escape(vm) + r"\s*\(([^)]*)\)", limpo, re.S)
    if not m:
        return []
    tipos = re.findall(r"([A-Z]\w+)\s+\w+\s*(?:,|$)", m.group(1))
    return sorted({t for t in tipos if t in scoped})


def _scoped_no_construtor(vm: str, scoped: set[str]) -> list[str]:
    """Os serviços Scoped que o construtor deste ViewModel recebe."""
    for arq in RAIZ.glob(f"src/**/{vm}.cs"):
        achados = _scoped_do_texto(vm, arq.read_text(encoding="utf-8"), scoped)
        if achados:
            return achados
        # Achou o arquivo e o construtor está limpo: não procure homônimo noutro projeto.
        if re.search(r"public\s+" + re.escape(vm) + r"\s*\(", arq.read_text(encoding="utf-8")):
            return []
    return []


_scoped_conhecidos = _servicos_scoped()
for _vm in sorted(_vms_do_shell()):
    _achados = _scoped_no_construtor(_vm, _scoped_conhecidos)
    if not _achados:
        continue

    _frase = (
        f"{_vm} recebe serviço SCOPED no construtor ({', '.join(_achados)}) e o shell o "
        f"resolve do provedor RAIZ — o DbContext passa a viver pela vida inteira do app, "
        f"a tela para de ver o que a outra máquina gravou e a releitura de fundo pode "
        f"colidir com um clique. Receba `IServiceScopeFactory` e abra `CreateScope()` por "
        f"operação, como AgendaViewModel e MeuDiaViewModel já fazem."
    )
    if _vm in _PENDENTES_ESCOPO:
        avisos.append(f"dívida conhecida — {_frase}")
    else:
        erros.append(_frase)

# --- autoteste da 37 ---
#
# ⚠️ Ele alimenta `_scoped_do_texto` — a MESMA função da varredura — com casos SINTÉTICOS,
# e não com um arquivo real defeituoso. A primeira versão apontava para o `CaixaViewModel`
# porque ele era a dívida do dia; quando a dívida foi paga, o autoteste quebrou sem haver
# defeito nenhum. Teste de checagem que depende de o defeito continuar existindo morre no
# dia em que a checagem funciona.
_ALVO = "FooViewModel"
for _cenario, _corpo, _deve in (
    # O caso real: serviço Scoped pedido por construtor.
    ("scoped no construtor",
     "public FooViewModel(AgendaService a, ISnackbarService s) { }", True),
    # O conserto: só a fábrica de escopo.
    ("só a fábrica",
     "public FooViewModel(IServiceScopeFactory e, ISnackbarService s) { }", False),
    # Um Scoped citado só no CORPO (dentro de um escopo) não é o defeito — e acusá-lo
    # seria o ruído que faz alguém desligar a ferramenta.
    ("scoped só no corpo",
     "public FooViewModel(IServiceScopeFactory e) { }\n"
     "void X() { var a = p.GetRequiredService<AgendaService>(); }", False),
    # Comentário que MENCIONA o serviço não pode disparar (a lição da checagem 31).
    ("mencionado em comentário",
     "// não receba AgendaService aqui\npublic FooViewModel(IServiceScopeFactory e) { }", False),
):
    _detectou = bool(_scoped_do_texto(_ALVO, _corpo, _scoped_conhecidos))
    if _detectou != _deve:
        erros.append(
            f"verificar-suite: a checagem 37 mudou de resposta ({_cenario}) — "
            f"detectou={_detectou}, esperado={_deve}."
        )

# ============================================================================
# CHECAGEM 38 — o rail de seções e o índice de navegação andam JUNTOS
#
# A tela do paciente tem um rail (ListBox de rótulos) e um TabControl (as telas),
# ligados pelo MESMO `SelectedIndex`. E `ModuloClinico.AbaDe` traduz a chave de
# navegação de outros módulos num ÍNDICE dessa lista.
#
# ⚠️ Três coisas que não quebram build nenhum:
#   (a) seção declarada no C# sem tela no TabControl (ou o contrário) — o clique
#       abre a tela do vizinho, ou não abre nada;
#   (b) seção nova no MEIO da lista, que empurra os índices abaixo dela;
#   (c) índice fora da faixa, que o WPF ignora em silêncio.
#
# ⚠️ O (b) DEIXOU DE SER VIGIADO e passou a ser impossível: desde o redesenho do rail
# (mockup 01), os rótulos não são mais escritos à mão no XAML — a ListBox é montada de
# `ModuloClinico.RailDoPaciente()`, que casa `SecoesDoPaciente` com `GruposDoPaciente`
# por posição. Uma segunda lista de rótulos no XAML seria a volta do defeito, e a
# checagem passou a RECUSÁ-LA em vez de compará-la.
#
# É literalmente a regressão da parcela 37, 4ª rodada (a navegação por string que
# "retorna false em silêncio"), um nível abaixo: ali a chave não achava destino;
# aqui ela acha o destino errado. E a parcela 75 acabou de fazer (b) ao pôr a
# Anamnese na posição 1.
# ============================================================================

_WORKSPACE = RAIZ / "src/Clinica.Modulo.Clinico/Views/PacienteWorkspaceView.xaml"
_MODULO_CLINICO = RAIZ / "src/Clinica.Modulo.Clinico/Modulo/ModuloClinico.cs"


def _secoes_do_workspace(texto: str) -> tuple[int, int]:
    """Quantos rótulos escritos à mão o rail tem (deve ser ZERO) e quantas telas o
    TabControl tem."""
    return (len(re.findall(r"<ListBoxItem\b", texto)),
            len(re.findall(r"<TabItem\b", texto)))


def _grupos_declarados(texto: str) -> list[str]:
    """A lista `GruposDoPaciente` do ModuloClinico, na ordem."""
    corpo = re.search(r"GruposDoPaciente\s*=\s*\[(.*?)\];", texto, re.S)
    if corpo is None:
        return []
    return re.findall(r"\"([^\"]+)\"", corpo.group(1))


def _secoes_declaradas(texto: str) -> list[str]:
    """A lista `SecoesDoPaciente` do ModuloClinico, na ordem."""
    corpo = re.search(r"SecoesDoPaciente\s*=\s*\[(.*?)\];", texto, re.S)
    if corpo is None:
        return []
    return re.findall(r"\"([^\"]+)\"", corpo.group(1))


def _nomes_de_abade(texto: str) -> list[str]:
    """Os NOMES de seção que o mapa de navegação produz."""
    corpo = re.search(r"AbaDe\(string chave\)(.*?)\n    \}", texto, re.S)
    if corpo is None:
        return []
    return re.findall(r"=>\s*\"([^\"]+)\"", corpo.group(1))


if _WORKSPACE.exists() and _MODULO_CLINICO.exists():
    # Comentário XML fora, pela lição da checagem 31: prosa que cita
    # `<TabItem>` não pode entrar na contagem.
    _txt_ws = re.sub(r"<!--.*?-->", "", _WORKSPACE.read_text(encoding="utf-8"), flags=re.S)
    _rotulos, _telas = _secoes_do_workspace(_txt_ws)

    _txt_mod = _sem_comentarios(_MODULO_CLINICO.read_text(encoding="utf-8"))
    _declaradas = _secoes_declaradas(_txt_mod)
    _grupos = _grupos_declarados(_txt_mod)

    # (a) uma seção declarada para cada tela do TabControl.
    if _declaradas and len(_declaradas) != _telas:
        erros.append(
            f"{_WORKSPACE.relative_to(RAIZ)}: ModuloClinico.SecoesDoPaciente declara "
            f"{len(_declaradas)} seção(ões) e o TabControl tem {_telas} tela(s). O rail e o "
            f"TabControl seguem o MESMO SelectedIndex — a diferença faz o clique abrir a "
            f"tela do vizinho, sem erro nenhum."
        )

    # (b) o rail não pode voltar a ter rótulo escrito à mão: ele é montado da lista do C#,
    # e uma segunda lista aqui traria de volta a divergência que isso eliminou.
    if _rotulos:
        erros.append(
            f"{_WORKSPACE.relative_to(RAIZ)}: o rail tem {_rotulos} <ListBoxItem> escrito à "
            f"mão. Ele é montado de ModuloClinico.RailDoPaciente() — rótulo no XAML é uma "
            f"SEGUNDA lista, e é ela que diverge da que resolve o índice de navegação."
        )

    # Seção sem grupo sai do rail sem quebrar nada: as duas listas são casadas por posição.
    if _declaradas and _grupos and len(_declaradas) != len(_grupos):
        erros.append(
            f"{_MODULO_CLINICO.relative_to(RAIZ)}: SecoesDoPaciente tem {len(_declaradas)} "
            f"item(ns) e GruposDoPaciente tem {len(_grupos)}. As duas são casadas por "
            f"POSIÇÃO — a seção sem grupo não aparece no rail."
        )

    for _nome in _nomes_de_abade(_txt_mod):
        if _declaradas and _nome not in _declaradas:
            erros.append(
                f"{_MODULO_CLINICO.relative_to(RAIZ)}: ModuloClinico.AbaDe aponta para a seção "
                f"“{_nome}”, que não está em SecoesDoPaciente. A navegação cai na primeira "
                f"seção em silêncio."
            )

# --- autoteste da 38 ---
#
# Casos SINTÉTICOS, pela lição do autoteste da 37: teste de checagem que depende do
# arquivo real morre no dia em que alguém arruma o arquivo.
for _cenario, _cs, _esperado in (
    ("seções declaradas",
     'public static readonly IReadOnlyList<string> SecoesDoPaciente =\n'
     '    [\n        "Um",\n        "Dois"\n    ];', ["Um", "Dois"]),
    ("sem lista", "public static int Outra() => 3;", []),
):
    if _secoes_declaradas(_cs) != _esperado:
        erros.append(
            f"verificar-suite: a checagem 38 mudou de resposta ({_cenario}) — "
            f"leu {_secoes_declaradas(_cs)}, esperado {_esperado}."
        )

for _cenario, _cs, _esperado in (
    ("grupos declarados",
     'public static readonly IReadOnlyList<string> GruposDoPaciente =\n'
     '    [\n        "Sessão",\n        "Paciente"\n    ];', ["Sessão", "Paciente"]),
    ("sem grupos", "public static int Outra() => 3;", []),
):
    if _grupos_declarados(_cs) != _esperado:
        erros.append(
            f"verificar-suite: a checagem 38 mudou de resposta ({_cenario}) — "
            f"leu {_grupos_declarados(_cs)}, esperado {_esperado}."
        )

# ⚠️ O caso REAL que motivou a mudança: rail montado do C# (zero ListBoxItem) com dez
# TabItem tem de passar; rótulo à mão de volta tem de reprovar.
for _cenario, _xaml, _esperado in (
    ("rail montado do C#", "<TabItem/><TabItem/>", (0, 2)),
    ("rótulo à mão de volta", '<ListBoxItem><TextBlock Text="Um" /></ListBoxItem><TabItem/>', (1, 1)),
):
    if _secoes_do_workspace(_xaml) != _esperado:
        erros.append(
            f"verificar-suite: a checagem 38 mudou de resposta ({_cenario}) — "
            f"contou {_secoes_do_workspace(_xaml)}, esperado {_esperado}."
        )

# --------------------------------------------------------------- checagem 39
#
# `PromptWindow` RECUSA resposta em branco por padrão — e está certo: quase toda pergunta
# dele é o motivo de um cancelamento, que não pode ser registrado sem explicação. O defeito
# nasce quando a PERGUNTA anuncia o campo como opcional ("se quiser", "deixe em branco") e
# a janela continua exigindo: a pessoa clica Confirmar, leva um erro em vermelho, e a ÚNICA
# saída que lhe resta é o **Cancelar** — que o chamador lê como "siga em frente".
#
# Nos dois casos reais isso GRAVAVA: a revisão da anamnese (prontuário, com versão e
# auditoria, sem desfazer) e o recibo do caixa (numerado por ano, desfeito só com
# cancelamento e motivo). A porta rotulada "desisti" era a que efetivava o ato.
#
# Nenhuma rede pegava: o C# compila, o XAML é válido, e os testes não alcançam WPF. As duas
# metades são cobradas juntas, como na checagem 21 — a pergunta que promete opcional passa
# `obrigatorio: false`, e quem passa `obrigatorio: false` distingue o `null` do Cancelar.

_PALAVRAS_DE_OPCIONAL = (
    "opcional",
    "deixe em branco",
    "deixar em branco",
    "em branco para",
    "se quiser",
    "se desejar",
)


def _corpo_da_chamada(texto: str, abre: int) -> tuple[str, int]:
    """Os argumentos da chamada cujo `(` está em `abre`, e o índice logo após o `)`.

    Conta parênteses pulando literais de string — `$"Item {a.B(c)}"` tem parêntese DENTRO
    do texto, e contar sem pular fecharia a chamada no lugar errado.
    """
    nivel, i, n = 0, abre, len(texto)
    while i < n:
        c = texto[i]
        if c == '"':
            verbatim = i > 0 and texto[i - 1] == "@"
            i += 1
            while i < n:
                if verbatim:
                    if texto[i] == '"':
                        if i + 1 < n and texto[i + 1] == '"':
                            i += 2
                            continue
                        break
                else:
                    if texto[i] == "\\":
                        i += 2
                        continue
                    if texto[i] == '"':
                        break
                i += 1
        elif c == "(":
            nivel += 1
        elif c == ")":
            nivel -= 1
            if nivel == 0:
                return texto[abre + 1 : i], i + 1
        i += 1
    return "", n


def _perguntas_de_texto(texto: str) -> list[dict]:
    """Cada chamada de `PerguntarTexto` do texto, com o que a checagem 39 precisa saber.

    ⚠️ Tira os comentários ANTES de olhar (a lição da checagem 31): a explicação desta
    própria regra, escrita ao lado da correção, contém as palavras que ela procura.
    """
    limpo = _sem_comentarios(texto)
    achados: list[dict] = []
    for m in re.finditer(r"PerguntarTexto\s*\(", limpo):
        args, fim = _corpo_da_chamada(limpo, m.end() - 1)
        literais = " ".join(
            re.findall(r'"((?:[^"\\]|\\.)*)"', args)
        ).lower()
        antes = limpo[max(0, m.start() - 160) : m.start()].rstrip()
        alvo = None
        atribuicao = re.search(r"(\w+)\s*=\s*[\w\._]*$", antes)
        if atribuicao:
            alvo = atribuicao.group(1)
        # ⚠️ `_sem_comentarios` APAGA o comentário com espaços, preservando as posições —
        # um bloco explicativo de cinco linhas empurra a guarda para fora de uma janela
        # medida em caracteres. É o tropeço da parcela 41, e a primeira versão desta
        # checagem o repetiu: ela acusou a correção que estava certa. Colapsa-se o branco
        # antes de medir, para a janela contar LINHAS DE CÓDIGO e não espaço em branco.
        depois = re.sub(r"\s+", " ", limpo[fim : fim + 6000])[:400]
        achados.append(
            {
                "anuncia_opcional": any(p in literais for p in _PALAVRAS_DE_OPCIONAL),
                "passa_false": re.search(r"obrigatorio\s*:\s*false", args) is not None,
                "alvo": alvo,
                "distingue_cancelar": bool(
                    alvo
                    and re.search(
                        rf"\b{re.escape(alvo)}\s+is\s+null\b"
                        rf"|\b{re.escape(alvo)}\s*==\s*null\b",
                        depois,
                    )
                ),
                "trecho": " ".join(literais.split())[:70],
            }
        )
    return achados


for _arq in sorted(RAIZ.joinpath("src").rglob("*.cs")):
    if "/obj/" in _arq.as_posix() or "/bin/" in _arq.as_posix():
        continue
    _texto = _arq.read_text(encoding="utf-8")
    if "PerguntarTexto" not in _texto:
        continue
    for _p in _perguntas_de_texto(_texto):
        _rel = _arq.relative_to(RAIZ)
        if _p["anuncia_opcional"] and not _p["passa_false"]:
            erros.append(
                f"{_rel}: `PerguntarTexto` cuja pergunta anuncia o campo como OPCIONAL "
                f"(“{_p['trecho']}…”) e não passa `obrigatorio: false`. A janela recusa a "
                f"resposta em branco, então a única saída da pessoa é o Cancelar — e o "
                f"chamador o lê como “siga”. Passe `obrigatorio: false`."
            )
        if _p["passa_false"] and not _p["distingue_cancelar"]:
            erros.append(
                f"{_rel}: `PerguntarTexto` com `obrigatorio: false` sem distinguir o "
                f"Cancelar. Com ele, `null` quer dizer “desisti” e string vazia quer dizer "
                f"“siga, sem texto” — sem a guarda `{_p['alvo'] or 'resposta'} is null` o "
                f"desistir vira gravar."
            )

# --- autoteste da 39 ---
#
# Casos SINTÉTICOS que chamam a MESMA função da varredura (a regra da parcela 67: autoteste
# que reimplementa a lógica fica verde exatamente quando a checagem quebra). Os dois
# primeiros são as duas formas quebradas; os dois últimos, as legítimas.
for _cenario, _cs, _esperado in (
    (
        "promete opcional e exige",
        'var motivo = _dialogo.PerguntarTexto("T", "diga o que mudou (opcional):");\n'
        "if (motivo is null) return;",
        {"anuncia_opcional": True, "passa_false": False, "distingue_cancelar": True},
    ),
    (
        "opcional sem guarda de cancelar",
        'var nome = _dialogo.PerguntarTexto("T", "Deixe em branco para usar o do paciente.",\n'
        "    null, obrigatorio: false);\nEmitir(nome);",
        {"anuncia_opcional": True, "passa_false": True, "distingue_cancelar": False},
    ),
    (
        "obrigatório de sempre, sem promessa",
        'var motivo = _dialogo.PerguntarTexto("T", "Por que está sendo cancelado?");\n'
        "if (string.IsNullOrWhiteSpace(motivo)) return;",
        {"anuncia_opcional": False, "passa_false": False, "distingue_cancelar": False},
    ),
    (
        "opcional declarado e distinguido",
        'var motivo = _dialogo.PerguntarTexto("T", "Se quiser, diga o que mudou.",\n'
        "    string.Empty, obrigatorio: false);\nif (motivo is null) return;",
        {"anuncia_opcional": True, "passa_false": True, "distingue_cancelar": True},
    ),
):
    _lidos = _perguntas_de_texto(_cs)
    if len(_lidos) != 1 or any(_lidos[0][k] != v for k, v in _esperado.items()):
        erros.append(
            f"verificar-suite: a checagem 39 mudou de resposta ({_cenario}) — "
            f"leu {_lidos}, esperado {_esperado}."
        )

# ⚠️ O caso que a PRIMEIRA versão desta checagem errou: a guarda existe, e um comentário
# de cinco linhas entre ela e a chamada a empurrava para fora da janela. Sem este caso o
# autoteste ficava verde com a checagem acusando o código correto.
_com_comentario = (
    'var motivo = _dialogo.PerguntarTexto("T", "Se quiser, diga o que mudou.",\n'
    "    string.Empty, obrigatorio: false);\n\n"
    + "".join(f"    // linha {i} de uma explicacao comprida que ocupa a janela toda\n"
             for i in range(8))
    + "if (motivo is null) return;"
)
if not _perguntas_de_texto(_com_comentario)[0]["distingue_cancelar"]:
    erros.append(
        "verificar-suite: a checagem 39 perdeu a guarda por causa de um comentário entre "
        "ela e a chamada — a janela está medindo espaço em branco em vez de código."
    )

# O parêntese DENTRO do literal interpolado não pode fechar a chamada cedo demais.
_interpolado = 'var m = _dialogo.PerguntarTexto("T", $"Item {Fmt(x)} — opcional");'
if len(_perguntas_de_texto(_interpolado)) != 1 or not _perguntas_de_texto(_interpolado)[0][
    "anuncia_opcional"
]:
    erros.append(
        "verificar-suite: a checagem 39 perdeu o literal interpolado com parêntese — "
        "o contador de parênteses está fechando a chamada dentro da string."
    )

# --------------------------------------------------------------- checagem 40
#
# `<Style TargetType="ctrl:X">` declarado numa TELA, sem `BasedOn`, SUBSTITUI o estilo
# implícito do design system — e é lá que mora o `Template` do controle. Não há
# `themes/generic.xaml` em projeto nenhum, então o `DefaultStyleKeyProperty.OverrideMetadata`
# não tem tema de onde cair: o que sobra é um controle vivo, com todas as propriedades
# corretas, desenhando **nada**.
#
# Foi assim que o estado vazio da anamnese sumiu — e junto com ele o terceiro estado, que
# faz uma leitura FALHADA se distinguir de um paciente sem antecedentes. Nenhuma rede pegava:
# o XAML é bem-formado, as propriedades existem, o binding é válido e nada lança.
#
# Ruído medido ANTES de decidir (a lição da parcela 64): das 14 declarações do repositório,
# 11 já traziam o `BasedOn` e as 3 restantes são os PRÓPRIOS dicionários do design system —
# que são os donos do Template e por definição não herdam de ninguém. Zero falso positivo.

_TIPO_COM_PREFIXO = re.compile(
    r"TargetType\s*=\s*\"(?:\{x:Type\s+)?([A-Za-z_]\w*):(\w+)"
)


def _estilos_sem_basedon(xaml: str) -> list[str]:
    """Os `<Style>` de controle PRÓPRIO (TargetType com prefixo) que não herdam nada.

    A varredura e o autoteste chamam esta mesma função — a regra da parcela 67.
    """
    achados: list[str] = []
    for m in re.finditer(r"<Style\b", xaml):
        fim = xaml.find(">", m.end())
        if fim < 0:
            continue
        abertura = xaml[m.start() : fim]
        tipo = _TIPO_COM_PREFIXO.search(abertura)
        if not tipo:
            continue
        if "BasedOn" in abertura:
            continue
        achados.append(f"{tipo.group(1)}:{tipo.group(2)}")
    return achados


for _arq in sorted(RAIZ.joinpath("src").rglob("*.xaml")):
    caminho = _arq.as_posix()
    # Os dicionários do design system SÃO os estilos implícitos: eles definem o Template
    # e não herdam de ninguém. É o único lugar legítimo sem `BasedOn`.
    if "/Styles/" in caminho or "/obj/" in caminho or "/bin/" in caminho:
        continue
    for _tipo in _estilos_sem_basedon(_arq.read_text(encoding="utf-8")):
        erros.append(
            f"{_arq.relative_to(RAIZ)}: `<Style TargetType=\"{_tipo}\">` sem `BasedOn`. "
            f"Estilo local substitui o implícito do design system, que é onde mora o "
            f"`Template` — o controle continua vivo e desenha NADA. Acrescente "
            f"`BasedOn=\"{{StaticResource {{x:Type {_tipo}}}}}\"`."
        )

# --- autoteste da 40 ---
for _cenario, _xaml, _esperado in (
    ("controle da casa sem BasedOn", '<Style TargetType="ctrl:EstadoDaTela">', ["ctrl:EstadoDaTela"]),
    ("controle da casa com BasedOn",
     '<Style TargetType="ctrl:EstadoDaTela" BasedOn="{StaticResource {x:Type ctrl:EstadoDaTela}}">', []),
    ("BasedOn na linha de baixo",
     '<Style TargetType="ctrl:EstadoDaTela"\n       BasedOn="{StaticResource {x:Type ctrl:X}}">', []),
    ("forma x:Type sem BasedOn", '<Style TargetType="{x:Type ctrl:CabecalhoRaia}">', ["ctrl:CabecalhoRaia"]),
    ("controle do WPF não é da casa", '<Style TargetType="Button">', []),
    ("TextBlock com BasedOn implícito do WPF", '<Style TargetType="TextBlock" BasedOn="{StaticResource TextoSuave}">', []),
):
    if _estilos_sem_basedon(_xaml) != _esperado:
        erros.append(
            f"verificar-suite: a checagem 40 mudou de resposta ({_cenario}) — "
            f"leu {_estilos_sem_basedon(_xaml)}, esperado {_esperado}."
        )

# --------------------------------------------------------------- checagem 41
#
# NOME DE TABELA em migration escrita à mão. O nome da tabela sai do **DbSet**
# (`DbSet<UsuarioSistema> Usuarios` → tabela `Usuarios`), e não do nome da CLASSE — e a
# migration é o único lugar do repositório onde ele é digitado à mão, porque não há
# `dotnet ef` neste ambiente.
#
# Escrever `principalTable: "UsuariosSistema"` derrubou a ABERTURA de todos os apps na
# clínica: 42P01 no meio do `MigrateAsync`, apresentado ao usuário como "não foi possível
# conectar ao banco de dados" — que manda a clínica caçar a connection string, um problema
# que não existe.
#
# ⚠️ NENHUMA rede pegava, e a razão é estrutural: o C# compila (é string), o
# `compilar-sombra` não lê migration, e **os 1874 testes não executam migration nenhuma** —
# o SQLite deles monta o schema pelo MODELO, com `EnsureCreated`. É a mesma família do
# `xmin` e das datas com fuso: só o Postgres pega, e "só o Postgres" quer dizer "só a
# clínica".
#
# Ruído medido ANTES de decidir (a lição da parcela 64): 68 tabelas e ~80 migrations, UMA
# ocorrência — a que quebrou. Zero falso positivo.

_TABELA_CRIADA = re.compile(r'CreateTable\(\s*name:\s*"([^"]+)"')
_TABELA_RENOMEADA = re.compile(r'RenameTable\([^)]*?newName:\s*"([^"]+)"', re.S)
_TABELA_REFERIDA = re.compile(r'(?:principalTable|table):\s*"([^"]+)"')


def _tabelas_fantasma(fontes: dict[str, str]) -> list[tuple[str, str]]:
    """(arquivo, tabela) das referências a tabela que migration nenhuma cria.

    Recebe o CONJUNTO das migrations porque a resposta depende delas todas: a tabela
    referida aqui costuma ter sido criada anos atrás, noutro arquivo. A varredura e o
    autoteste chamam esta mesma função — a regra da parcela 67.
    """
    existentes: set[str] = set()
    for texto in fontes.values():
        existentes.update(m.group(1) for m in _TABELA_CRIADA.finditer(texto))
        existentes.update(m.group(1) for m in _TABELA_RENOMEADA.finditer(texto))

    achados: list[tuple[str, str]] = []
    for arquivo, texto in sorted(fontes.items()):
        limpo = _sem_comentarios(texto)
        for tabela in sorted({m.group(1) for m in _TABELA_REFERIDA.finditer(limpo)}):
            if tabela not in existentes:
                achados.append((arquivo, tabela))
    return achados


_MIGRATIONS = RAIZ / "src" / "Clinica.Infrastructure" / "Migrations"
_fontes_migration = {
    a.name: a.read_text(encoding="utf-8")
    for a in sorted(_MIGRATIONS.glob("*.cs"))
    if ".Designer." not in a.name and "Snapshot" not in a.name
}

for _arq, _tabela in _tabelas_fantasma(_fontes_migration):
    erros.append(
        f"Migrations/{_arq}: referencia a tabela \"{_tabela}\", que migration nenhuma cria. "
        f"O nome da tabela sai do DbSet do ClinicaDbContext, não do nome da classe — "
        f"confira lá. Isto NÃO falha em teste (o SQLite monta o schema pelo modelo): "
        f"falha no MigrateAsync da abertura, na clínica, como 42P01."
    )

# --- autoteste da 41 ---
_CRIA = 'migrationBuilder.CreateTable(\n    name: "Usuarios",'
for _cenario, _fontes, _esperado in (
    ("nome da CLASSE em vez do DbSet (o caso real)",
     {"a.cs": _CRIA, "b.cs": 'principalTable: "UsuariosSistema", principalColumn: "Id"'},
     [("b.cs", "UsuariosSistema")]),
    ("nome certo",
     {"a.cs": _CRIA, "b.cs": 'principalTable: "Usuarios", principalColumn: "Id"'}, []),
    ("tabela criada no MESMO arquivo que a referencia",
     {"a.cs": 'migrationBuilder.CreateTable(\n    name: "Nova",\n table: "Nova"'}, []),
    ("AddColumn na tabela de sempre",
     {"a.cs": _CRIA, "b.cs": 'migrationBuilder.AddColumn<bool>(\n name: "X", table: "Usuarios"'}, []),
    ("tabela RENOMEADA passa a existir",
     {"a.cs": 'migrationBuilder.RenameTable(name: "Velha", newName: "Nova");',
      "b.cs": 'table: "Nova"'}, []),
    ("comentário citando o nome errado não dispara",
     {"a.cs": _CRIA, "b.cs": '// não escreva principalTable: "UsuariosSistema" aqui'}, []),
):
    _lido = _tabelas_fantasma(_fontes)
    if _lido != _esperado:
        erros.append(
            f"verificar-suite: a checagem 41 mudou de resposta ({_cenario}) — "
            f"leu {_lido}, esperado {_esperado}."
        )

# --------------------------------------------------------------- checagem 42
# TAG DE TIPO DA CASA ESCRITA SEM PREFIXO.
#
#     MC3074: The tag 'ProcessoDeEnfermagemView' does not exist in XML namespace
#             'http://schemas.microsoft.com/winfx/2006/xaml/presentation'.
#
# É a TERCEIRA variante da família das checagens 33 e 33-B, e a que nenhuma das duas via.
# Lá o `xmlns` existe e está errado (o `;assembly=` que sobra ou que falta); aqui ele
# simplesmente NÃO FOI DECLARADO, e a tag saiu sem prefixo — então o WPF a procura no
# namespace PADRÃO (o do próprio WPF) e recusa.
#
# Foi o que quebrou o build na parcela 88, 4ª rodada: ao extrair o compositor da consulta
# de enfermagem para o shell, escrevi `<ProcessoDeEnfermagemView … />` dentro de uma janela
# que só declarava o prefixo de `Clinica.Desktop.Controls`.
#
# ⚠️ Nenhuma rede local pegava, pela razão de sempre nesta família: o XML é bem-formado, o
# `compilar-sombra` NÃO lê o corpo do XAML e o C# compila. Sete minutos de CI por um
# prefixo que faltou.
#
# O critério é estreito de propósito: só reclama de tag sem prefixo cujo nome é um tipo que
# ALGUM `.cs` do repositório declara. Tag de tipo do WPF (`Grid`, `TabControl`, `Button`)
# não está nessa lista e passa; tipo da casa usado sem prefixo é sempre o defeito. Medido
# antes de ligar: ZERO ocorrências em todo o repositório depois da correção — a checagem
# nasce sem uma linha de ruído.
TAG_SEM_PREFIXO = re.compile(r'<([A-Z]\w*)(?=[\s/>])')
DECLARACAO_DE_TIPO = re.compile(r'\b(?:class|record|struct|interface|enum)\s+([A-Z]\w*)')

_tipos_da_casa: set[str] = set()
for _cs in RAIZ.glob("src/*/**/*.cs"):
    if "/obj/" in str(_cs) or "/bin/" in str(_cs):
        continue
    _tipos_da_casa.update(DECLARACAO_DE_TIPO.findall(_cs.read_text(encoding="utf-8")))


def _tags_sem_prefixo(texto: str) -> list[tuple[int, str]]:
    """As tags sem prefixo que nomeiam um tipo declarado no repositório."""
    # ⚠️ Comentário fora ANTES de procurar: prosa que cite `<EstadoDaTela …>` faria a
    # checagem gritar sobre uma explicação (a lição da checagem 31, parcela 58). O branco
    # é preservado para a contagem de linhas continuar valendo.
    limpo = re.sub(
        r"<!--.*?-->",
        lambda m: re.sub(r"[^\n]", " ", m.group(0)),
        texto,
        flags=re.S,
    )
    return [
        (limpo.count("\n", 0, m.start()) + 1, m.group(1))
        for m in TAG_SEM_PREFIXO.finditer(limpo)
        if m.group(1) in _tipos_da_casa
    ]


if not _tipos_da_casa:
    erros.append(
        "verificar-suite: a checagem 42 não achou tipo nenhum declarado em `src/**/*.cs`."
    )

for f in list(arvores_com_faturamento):
    for linha, nome in _tags_sem_prefixo(f.read_text(encoding="utf-8")):
        erros.append(
            f"{rel(f)}:{linha}: a tag `<{nome}>` não tem prefixo, e `{nome}` é um tipo "
            f"declarado neste repositório — sem prefixo o WPF a procura no namespace "
            f"padrão dele e recusa com MC3074. Declare um `xmlns:` para o namespace do "
            f"tipo e use-o na tag."
        )

# Autoteste: o caso REAL da parcela 88 e os dois legítimos que não podem disparar.
for _amostra, _deve_pegar, _cenario in (
    ('<comp:ProcessoDeEnfermagemView DataContext="{Binding}" />', False, "com prefixo"),
    ('<ProcessoDeEnfermagemView DataContext="{Binding}" />', True, "sem prefixo — o defeito"),
    ('<TabControl MinHeight="100" />', False, "tipo do WPF, que não é da casa"),
    ('<!-- o <ProcessoDeEnfermagemView> mora no shell -->', False, "só um comentário"),
):
    if bool(_tags_sem_prefixo(_amostra)) != _deve_pegar:
        erros.append(
            f"verificar-suite: a checagem 42 mudou de resposta ({_cenario}) — "
            f"esperado {'pegar' if _deve_pegar else 'deixar passar'}."
        )



# --------------------------------------------------------------- checagem 43
# CONTROLE QUE ROLA POR DENTRO, DENTRO DE UMA PÁGINA QUE ROLA: A RODA NÃO CHEGA NA PÁGINA.
#
# `ScrollViewer.OnMouseWheel` marca o evento como TRATADO — sempre, inclusive quando não há
# o que rolar. Então todo controle cujo template traz um `ScrollViewer` (`ListBox`,
# `DataGrid`, `ListView`, `TreeView`, `RichTextBox`, e o próprio `ScrollViewer` aninhado)
# COME a roda do mouse: com o cursor em cima dele, a página não anda.
#
# O estrago não é "rola pouco": é a pessoa ver o conteúdo cortado embaixo, girar a roda,
# nada acontecer, e concluir que A TELA ESTÁ QUEBRADA. Foi o relato do cliente na parcela
# 90 — "toda cortada", e rolar não resolvia.
#
# ⚠️ E metade das ocorrências foi CRIADA por outra correção nossa: a checagem 36 (parcela
# 68) manda pôr `ScrollViewer` na raiz e teto nas grades para a última seção não ser
# decepada — e é justamente isso que dá a cada grade um `ScrollViewer` próprio para comer a
# roda. As duas metades andam juntas: teto sem devolver a roda é a mesma tela travada,
# numa altura menor.
#
# A saída é `Ajudantes.RodaDaPagina="True"` (existe nos DOIS design systems — o do shell e
# o do faturamento, que não se referenciam): a roda vai para a página SÓ quando a lista já
# chegou ao fim naquela direção. Sem essa condição de borda trocaríamos o defeito pelo
# oposto — a lista pararia de rolar.
#
# ⚠️ Página que rola só na HORIZONTAL não conta, e é o que salva o kanban da Fila e do Meu
# dia: lá o de fora tem `VerticalScrollBarVisibility="Disabled"` e a raia é quem deve rolar
# na vertical. A busca sobe PULANDO essas — se houver uma página vertical mais acima, o
# defeito continua de pé.
#
# Nenhuma outra rede pega: o XAML é bem-formado, o `compilar-sombra` não lê o corpo do XAML
# e nada lança. Só a tela montada, e só na altura errada — que nunca é a de quem programa.
# Medido antes de ligar: ZERO ocorrências depois da correção da parcela 90.
COME_A_RODA = {"ListBox", "DataGrid", "ListView", "TreeView", "ScrollViewer", "RichTextBox"}


def _rodas_comidas(raiz: ET.Element) -> list[str]:
    """Controles rolantes cuja roda nunca chega à página que os contém."""
    # ⚠️ Helper LOCAL de propósito: o `_nome` do topo do arquivo é sobrescrito por uma
    # variável de laço lá pelas checagens do meio, e a partir dali ele é uma string.
    def tag(el: ET.Element) -> str:
        return el.tag.split("}")[-1]

    pais = {filho: pai for pai in raiz.iter() for filho in pai}
    achados = []
    for el in raiz.iter():
        if tag(el) not in COME_A_RODA:
            continue
        if any("RodaDaPagina" in k for k in el.attrib):
            continue
        pai = pais.get(el)
        while pai is not None:
            if (tag(pai) == "ScrollViewer"
                    and pai.get("VerticalScrollBarVisibility", "").strip() != "Disabled"):
                achados.append(tag(el))
                break
            pai = pais.get(pai)
    return achados


for f, raiz_r in arvores_com_faturamento.items():
    for _ctrl in _rodas_comidas(raiz_r):
        erros.append(
            f"{rel(f)}: `{_ctrl}` rola por dentro e está dentro de uma página que também "
            f"rola — o `ScrollViewer` dele marca a roda do mouse como tratada e ela nunca "
            f"chega na página, que fica parada com o conteúdo cortado embaixo. Acrescente "
            f"`ctrl:Ajudantes.RodaDaPagina=\"True\"`."
        )

# Autoteste: o caso REAL da parcela 90 e os três legítimos que não podem disparar.
_AMOSTRAS_RODA = (
    ('<ScrollViewer xmlns:ctrl="c"><StackPanel><DataGrid MaxHeight="240" />'
     '</StackPanel></ScrollViewer>', True, "grade dentro da página — o defeito"),
    ('<ScrollViewer xmlns:ctrl="c"><StackPanel>'
     '<DataGrid MaxHeight="240" ctrl:Ajudantes.RodaDaPagina="True" />'
     '</StackPanel></ScrollViewer>', False, "com a roda devolvida"),
    ('<ScrollViewer xmlns:ctrl="c" VerticalScrollBarVisibility="Disabled">'
     '<StackPanel><ScrollViewer><StackPanel /></ScrollViewer></StackPanel></ScrollViewer>',
     False, "kanban: a página de fora só rola na horizontal"),
    ('<Grid xmlns:ctrl="c"><DataGrid MaxHeight="240" /></Grid>',
     False, "sem página rolante acima"),
)
for _xaml, _deve_pegar, _cenario in _AMOSTRAS_RODA:
    if bool(_rodas_comidas(ET.fromstring(_xaml))) != _deve_pegar:
        erros.append(
            f"verificar-suite: a checagem 43 mudou de resposta ({_cenario}) — "
            f"esperado {'pegar' if _deve_pegar else 'deixar passar'}."
        )

# --------------------------------------------------------------- checagem 44
# O TOKEN `Raio.Pilula` (999) USADO CRU NUM CornerRadius: NO WPF ISSO DESENHA UM OVO.
#
# O CSS trava um raio maior que a metade da altura NA metade — é o que faz o
# `border-radius: 999px` do kit web desenhar a pílula perfeita. O WPF NÃO trava: os arcos
# saem por inteiro e as bordas de cima e de baixo ficam CURVAS. O cliente mandou a foto na
# parcela 91: a busca global e os chips de filtro estavam ovais no sistema inteiro, porque
# o token (999, espelho fiel do CSS) era referenciado direto em TREZE lugares.
#
# Pílula de verdade é `ctrl:Ajudantes.Pilula="True"` (nos dois design systems), que mede a
# altura REAL do Border; círculo de tamanho FIXO usa o raio explícito (metade do lado). O
# token continua existindo porque é o espelho do CSS — onde 999 é correto — e o aviso ⚠️
# escrito nele não impede ninguém: impedir é o trabalho desta checagem.
#
# Nenhuma outra rede pega: XAML bem-formado, `compilar-sombra` não lê o corpo, nada lança —
# só a tela montada mostra, e a deformação cresce com a LARGURA, então o badge estreito
# engana e a busca larga denuncia. Medido antes de ligar: ZERO ocorrências depois da
# conversão da parcela 91 — a checagem nasce sem uma linha de ruído.
USO_DE_RAIO_PILULA = re.compile(r"\{StaticResource\s+Raio\.Pilula\}")


def _pilulas_cruas(texto: str) -> list[int]:
    """Linhas que referenciam o token Raio.Pilula (comentários fora, como sempre)."""
    limpo = re.sub(
        r"<!--.*?-->",
        lambda m: re.sub(r"[^\n]", " ", m.group(0)),
        texto,
        flags=re.S,
    )
    return [limpo.count("\n", 0, m.start()) + 1 for m in USO_DE_RAIO_PILULA.finditer(limpo)]


for f in list(arvores_com_faturamento):
    for _linha in _pilulas_cruas(f.read_text(encoding="utf-8")):
        erros.append(
            f"{rel(f)}:{_linha}: `Raio.Pilula` (999) usado cru — no WPF isso desenha um "
            f"OVO, não uma pílula (o raio não trava na metade da altura como no CSS). "
            f"Use `ctrl:Ajudantes.Pilula=\"True\"` no Border, ou raio explícito quando o "
            f"tamanho é fixo."
        )

# Autoteste: o caso REAL da parcela 91 nas duas formas, e os dois legítimos.
for _amostra, _deve_pegar, _cenario in (
    ('<Border CornerRadius="{StaticResource Raio.Pilula}" />', True,
     "atributo — o defeito da busca e dos chips"),
    ('<Setter Property="CornerRadius" Value="{StaticResource Raio.Pilula}" />', True,
     "setter de estilo — o defeito dos badges"),
    ('<CornerRadius x:Key="Raio.Pilula">999</CornerRadius>', False,
     "a definição do token (espelho do CSS)"),
    ('<!-- pílula de verdade é Ajudantes.Pilula, não {StaticResource Raio.Pilula} -->',
     False, "só um comentário"),
):
    if bool(_pilulas_cruas(_amostra)) != _deve_pegar:
        erros.append(
            f"verificar-suite: a checagem 44 mudou de resposta ({_cenario}) — "
            f"esperado {'pegar' if _deve_pegar else 'deixar passar'}."
        )


# --------------------------------------------------------------- checagem 45
# DOIS ITENS COM O MESMO RÓTULO NA SIDEBAR (set/2026 — o print do cliente).
#
# A dedupe do `ShellViewModel` casa por CHAVE. Quando dois módulos publicam a MESMA tela
# ela funde e a sidebar mostra uma linha — foi para isso que ela nasceu (a Sala de infusão,
# a Enfermagem, a Ajuda). O que ela NÃO alcança é o caso em que os dois módulos publicam
# telas DIFERENTES, com chaves diferentes, e rótulos que dizem a mesma coisa: aí o Gerente
# Geral — que carrega todos — mostra os dois, um do lado do outro, e a pessoa clica nos
# dois para descobrir o que é cada um.
#
# ⚠️ Isto já aconteceu DUAS vezes, e a segunda foi o cliente quem achou:
#   • "Prescrições" (`prescricoes` × `consultorio-prescricoes`) — parcela 55;
#   • "Prontuário" × "Prontuários" (`prontuario` × `consultorio-prontuarios`) — set/2026.
#
# A correção das duas foi a mesma (virar abas de um item composto), e a lição da parcela 68
# manda escrever a rede na segunda ocorrência em vez de esperar a terceira.
#
# ⚠️ Os rótulos do caso real eram QUASE iguais, não iguais: "Prontuário" e "Prontuários".
# Comparar texto cru deixaria passar justamente o defeito que a motivou, então a chave de
# comparação singulariza cada palavra — é grosseiro de propósito, e o ruído foi MEDIDO
# antes de ligar: ZERO ocorrências em toda a suíte depois da correção.
#
# A varredura reproduz o que o shell FAZ (`ShellViewModel.Itens`/`Grupos`), e não uma
# aproximação: item oculto não conta, chave repetida é fundida pela dedupe, e a sub-tela
# reivindicada por um item composto sai da sidebar. Sem essas três ela acusaria os pares
# legítimos, e checagem que grita no que está certo é checagem que alguém desliga.
ITEM_MENU = re.compile(r"new\s+ItemMenuModulo\s*\{(.*?)\n\s{8}\}", re.S)


def _chave_de_rotulo(rotulo: str) -> str:
    """'Prontuários' e 'Prontuário' viram a mesma chave; acento e caixa saem."""
    try:
        cru = rotulo.encode().decode("unicode_escape")
    except (UnicodeDecodeError, UnicodeEncodeError):
        cru = rotulo
    limpo = unicodedata.normalize("NFKD", cru).encode("ascii", "ignore").decode().lower()
    palavras = [p for p in re.split(r"[^a-z0-9]+", limpo) if p]
    return " ".join(p[:-1] if len(p) > 3 and p.endswith("s") else p for p in palavras)


def _sidebar_do_gerente(textos):
    """(grupo, chave-de-rótulo, chave, arquivo) do que o Gerente Geral MOSTRA."""
    itens = []
    reivindicadas = set()

    for nome, bruto in textos.items():
        texto = _sem_comentarios(bruto)
        locais = _consts_de(texto)

        for corpo in ITEM_MENU.findall(texto):
            rotulo = re.search(r'Rotulo\s*=\s*"([^"]*)"', corpo)
            grupo = re.search(r"Grupo\s*=\s*GrupoSidebar\.(\w+)", corpo)
            chave = re.search(r"Chave\s*=\s*([A-Za-z0-9_.]+)\s*,", corpo)
            if not (rotulo and grupo and chave):
                continue
            # Item oculto é destino de navegação, não linha de menu.
            if re.search(r"Oculto\s*=\s*true", corpo):
                continue

            resolvida = _resolver(chave.group(1), locais) or chave.group(1)
            itens.append(
                (grupo.group(1), _chave_de_rotulo(rotulo.group(1)), resolvida, nome)
            )

            # Quem esconde a sub-tela é o PAI (parcela 55), e só enquanto ele existe.
            for _rot, expr in ABA_MENU.findall(corpo):
                reivindicadas.add(_resolver(expr, locais) or expr)

    vistas = set()
    sidebar = []
    for grupo, rot, chave, nome in itens:
        if chave in reivindicadas or chave in vistas:
            continue
        vistas.add(chave)
        sidebar.append((grupo, rot, chave, nome))
    return sidebar


def _rotulos_repetidos(textos):
    porta = {}
    repetidos = []
    for grupo, rot, chave, nome in _sidebar_do_gerente(textos):
        anterior = porta.get((grupo, rot))
        if anterior is None:
            porta[(grupo, rot)] = (chave, nome)
            continue
        repetidos.append(
            f"{anterior[1]} e {nome}: os itens `{anterior[0]}` e `{chave}` aparecem "
            f"JUNTOS em {grupo} com rótulos que dizem a mesma coisa — no Gerente Geral, "
            f"que carrega todos os módulos, a pessoa vê dois itens quase homônimos e "
            f"clica nos dois para descobrir a diferença. A dedupe do shell casa por CHAVE "
            f"e não alcança isto. A saída é a das Prescrições e do Prontuário: um item "
            f"COMPOSTO com as duas telas como abas — cada rótulo de aba diz qual é qual, "
            f"e a sub-tela continua sendo um item."
        )
    return repetidos


erros.extend(
    _rotulos_repetidos({a.name: a.read_text(encoding="utf-8") for a in _modulos_cs})
)

# Autoteste: ela tem de ACUSAR o caso real de antes da correção e CALAR nos três legítimos.
#
# O "antes" reproduz o estado exato que o cliente fotografou: os dois itens publicados
# soltos, sem o composto que hoje os junta.
_ITEM_RECEPCAO = (
    '        public const string ChaveProntuario = "prontuario";\n'
    "        new ItemMenuModulo\n"
    "        {\n"
    '            Chave = ChaveProntuario, Rotulo = "Prontuario",\n'
    "            Grupo = GrupoSidebar.Paciente, Requer = Permissao.VerProntuario\n"
    "        },\n"
)
_ITEM_CLINICO = (
    "        public const string ChaveProntuarios = ChavesSuite.ConsultorioProntuarios;\n"
    "        new ItemMenuModulo\n"
    "        {\n"
    '            Chave = ChaveProntuarios, Rotulo = "Prontuarios",\n'
    "            Grupo = GrupoSidebar.Paciente, Requer = Permissao.VerProntuario\n"
    "        },\n"
)
_COMPOSTO = (
    '        public const string ChaveGrupoProntuario = "prontuario-geral";\n'
    "        new ItemMenuModulo\n"
    "        {\n"
    '            Chave = ChaveGrupoProntuario, Rotulo = "Prontuario",\n'
    "            Grupo = GrupoSidebar.Paciente, Requer = Permissao.VerProntuario,\n"
    "            Abas =\n"
    "            [\n"
    '                new AbaMenu("Por paciente", ChaveProntuario),\n'
    '                new AbaMenu("Registros", ChavesSuite.ConsultorioProntuarios)\n'
    "            ]\n"
    "        },\n"
)
_MESMA_TELA = (
    "        new ItemMenuModulo\n"
    "        {\n"
    '            Chave = ChavesSuite.Enfermagem, Rotulo = "Enfermagem",\n'
    "            Grupo = GrupoSidebar.Paciente, Requer = Permissao.VerProntuario\n"
    "        },\n"
)
_OCULTO = (
    '        public const string ChaveOculta = "consultorio-prontuario";\n'
    "        new ItemMenuModulo\n"
    "        {\n"
    '            Chave = ChaveOculta, Rotulo = "Prontuario",\n'
    "            Grupo = GrupoSidebar.Paciente, Requer = Permissao.VerProntuario,\n"
    "            Oculto = true\n"
    "        },\n"
)

for _cenario, _textos, _deve_pegar in (
    (
        "o caso real: Prontuario e Prontuarios publicados soltos",
        {"FakeRecepcao.cs": _ITEM_RECEPCAO, "FakeClinico.cs": _ITEM_CLINICO},
        True,
    ),
    (
        "o mesmo par já resolvido em ABAS",
        {
            "FakeRecepcao.cs": _ITEM_RECEPCAO + _COMPOSTO,
            "FakeClinico.cs": _ITEM_CLINICO,
        },
        False,
    ),
    (
        "a MESMA tela publicada por dois módulos (a dedupe por chave já funde)",
        {"FakeA.cs": _MESMA_TELA, "FakeB.cs": _MESMA_TELA},
        False,
    ),
    (
        "o item OCULTO homônimo (destino de navegação, não linha de menu)",
        {"FakeRecepcao.cs": _ITEM_RECEPCAO, "FakeClinico.cs": _OCULTO},
        False,
    ),
):
    if bool(_rotulos_repetidos(_textos)) != _deve_pegar:
        erros.append(
            f"verificar-suite: a checagem 45 mudou de resposta ({_cenario}) — "
            f"esperado {'pegar' if _deve_pegar else 'deixar passar'}."
        )


# --------------------------------------------------------------- checagem 46
# ELEMENTO DE PROPRIEDADE NO MEIO DO CONTEÚDO: MC3088, e só o CI vê (set/2026 — o
# `build` do PR ficou vermelho num `<Style.Triggers>` posto ENTRE dois `<Setter>`).
#
# Em XAML, `<Tipo.Propriedade>` dentro de `<Tipo>` é elemento de PROPRIEDADE; os filhos
# sem ponto são o CONTEÚDO (a `ContentProperty` do tipo — os `Setter` de um `Style`, os
# filhos de um `Grid`). O compilador de marcação aceita a propriedade ANTES ou DEPOIS do
# conteúdo, e recusa no MEIO: "Property elements cannot be in the middle of an element's
# content" (MC3088).
#
# ⚠️ Nenhuma rede local pegava, pela razão de sempre nesta família: o XML é bem-formado,
# o `compilar-sombra` não lê o corpo do XAML e o C# compila. O caso real foi um
# `Style.Triggers` acrescentado no meio da lista de `Setter` de um estilo — a intenção era
# pôr o gatilho "perto do comentário que o explica", e a ordem do arquivo virou erro de
# compilação a sete minutos de distância, no runner Windows.
#
# A regra é EXATAMENTE a do compilador — propriedade com conteúdo antes E depois — e não
# "propriedade depois de conteúdo": esta segunda acusaria os 235 `Style.Triggers`
# legítimos que vêm depois do último `Setter`. Medido antes de ligar, com a regra certa:
# UMA ocorrência em todo o repositório (faturamento incluído), que era o defeito.


def _tag_46(el: ET.Element) -> str:
    # ⚠️ Não usa `_nome`: a essa altura do arquivo o nome já foi reaproveitado como
    # variável de laço de outra checagem, e chamá-lo estoura com "'str' is not callable".
    return el.tag.split("}")[-1]


def _propriedade_no_meio(raiz: ET.Element) -> list[tuple[str, str]]:
    """(pai, propriedade) de todo elemento de propriedade com conteúdo dos DOIS lados."""
    achados = []
    for pai in raiz.iter():
        nome = _tag_46(pai)
        filhos = list(pai)
        eh_prop = [
            "." in _tag_46(c) and _tag_46(c).split(".")[0] == nome for c in filhos
        ]
        for i, prop in enumerate(eh_prop):
            if not prop:
                continue
            antes = any(not x for x in eh_prop[:i])
            depois = any(not x for x in eh_prop[i + 1:])
            if antes and depois:
                achados.append((nome, _tag_46(filhos[i])))
    return achados


for f, raiz in arvores_com_faturamento.items():
    for _pai, _prop in _propriedade_no_meio(raiz):
        erros.append(
            f"{rel(f)}: `<{_prop}>` está no MEIO do conteúdo de `<{_pai}>` — o compilador "
            f"de marcação recusa (MC3088: elemento de propriedade tem de vir antes ou "
            f"depois de TODO o conteúdo). Mova o bloco para depois do último filho de "
            f"conteúdo (o último `Setter`, no caso de um `Style`)."
        )

# Autoteste, nos dois sentidos (a regra da checagem 34): o caso real tem de ser pego, e as
# três formas legítimas — propriedade DEPOIS de todo o conteúdo, propriedade ANTES de todo
# o conteúdo, e o `Grid.ColumnDefinitions` no topo de um Grid cheio — têm de passar.
_XAML_46 = '<Style xmlns="x" xmlns:t="y">{}</Style>'
for _cenario, _corpo, _deve_pegar in (
    (
        "o caso real: Style.Triggers ENTRE dois Setter",
        "<Setter /><Style.Triggers><t:DataTrigger /></Style.Triggers><Setter />",
        True,
    ),
    (
        "Style.Triggers DEPOIS de todos os Setter",
        "<Setter /><Setter /><Style.Triggers><t:DataTrigger /></Style.Triggers>",
        False,
    ),
    (
        "Style.Resources ANTES de todo o conteúdo",
        "<Style.Resources /><Setter /><Setter />",
        False,
    ),
    (
        "Grid.ColumnDefinitions no topo de um Grid cheio",
        "<t:Grid><t:Grid.ColumnDefinitions /><t:Border /><t:TextBlock /></t:Grid>",
        False,
    ),
):
    if bool(_propriedade_no_meio(ET.fromstring(_XAML_46.format(_corpo)))) != _deve_pegar:
        erros.append(
            f"verificar-suite: a checagem 46 mudou de resposta ({_cenario}) — "
            f"esperado {'pegar' if _deve_pegar else 'deixar passar'}."
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
