#!/usr/bin/env python3
"""
Compilação-sombra dos projetos WPF — o compilador que faltava neste ambiente.

O problema que ela resolve
--------------------------
`Clinica.Desktop.Shell`, os três `Clinica.Modulo.*` e os três executáveis têm
`TargetFramework=net8.0-windows` e `UseWPF=true`. O SDK .NET que existe em Linux
(pacote `dotnet-sdk-8.0` do Ubuntu) **não traz** o `Microsoft.NET.Sdk.WindowsDesktop`,
então `dotnet build` nesses sete projetos falha antes de olhar uma linha de C#. O
resultado prático era que TODO erro de compilação das telas só aparecia no CI, num
runner Windows, minutos depois do push — e uma noite de trabalho colecionou quatro
deles (`CS0019`, `CS0117`, `CS1503`, `CS0579`), todos triviais e todos caros.

O que ela faz
-------------
Recompila os MESMOS arquivos `.cs` num projeto `net8.0` comum ("sombra"), que:

- referencia as *reference assemblies* do WPF (`PresentationFramework`,
  `PresentationCore`, `WindowsBase`, `System.Xaml`) baixadas do NuGet, em vez de
  depender do SDK WindowsDesktop;
- substitui o compilador de marcação (a etapa que este SDK não tem) por um gerador
  próprio de `.g.cs`: para cada XAML com `x:Class`, emite a parte `partial` com
  `InitializeComponent()` e um campo por `x:Name`.

Assim o Roslyn vê o código das telas exatamente como o CI vê, e erro de nome, de
tipo, de aridade ou de atributo duplicado aparece AQUI.

O que ela NÃO faz — e é importante não confundir
------------------------------------------------
Ela não compila XAML. Erro de marcação (`MC3024`, chave de recurso inexistente,
pack URI errado) continua sendo do `verificar-suite.py`, que olha o XAML como texto,
e do CI, que roda o compilador de marcação de verdade. As duas ferramentas são
complementares: **`verificar-suite.py` cobre o XAML, esta cobre o C#.**

Também não substitui o CI: o build oficial dos quatro apps é o do runner Windows.
Esta é a rede que faz o push chegar lá já sabendo que compila.

Uso
---
    python3 tools/compilar-sombra.py            # gera e compila
    python3 tools/compilar-sombra.py --limpar   # descarta o cache e refaz

Artefatos ficam em `.sombra/` (fora do versionamento).
"""

from __future__ import annotations

import argparse
import json
import os
import re
import shutil
import subprocess
import sys
import urllib.request
import xml.etree.ElementTree as ET
import zipfile
from pathlib import Path

RAIZ = Path(__file__).resolve().parent.parent
SOMBRA = RAIZ / ".sombra"

# Versão das reference assemblies do WPF. Não precisa casar exatamente com o
# runtime do CI: são assinaturas de API, e o que muda entre patches do 8.0 não
# altera o que este projeto usa.
VERSAO_WD = "8.0.29"
URL_WD = (
    "https://api.nuget.org/v3-flatcontainer/microsoft.windowsdesktop.app.ref/"
    f"{VERSAO_WD}/microsoft.windowsdesktop.app.ref.{VERSAO_WD}.nupkg"
)

# Os sete projetos WPF da suíte. O faturamento (Clinica.Desktop) fica de fora de
# propósito: está congelado, ninguém o edita, e compilá-lo só gastaria tempo.
PROJETOS_WPF = [
    "Clinica.Desktop.Shell",
    "Clinica.Modulo.Recepcao",
    "Clinica.Modulo.Financeiro",
    "Clinica.Modulo.Gerente",
    "Clinica.Recepcao",
    "Clinica.Financeiro",
    "Clinica.Gerente",
]

XMLNS_XAML = "http://schemas.microsoft.com/winfx/2006/xaml"
XMLNS_WPF = "http://schemas.microsoft.com/winfx/2006/xaml/presentation"

# Elementos cujo conteúdo é MOLDE, não árvore visual: `x:Name` lá dentro não vira
# campo da classe (o WPF resolve por `FindName` em tempo de execução). Gerar campo
# para eles daria erro de compilação inventado por esta ferramenta.
MOLDES = {
    "ControlTemplate",
    "DataTemplate",
    "HierarchicalDataTemplate",
    "ItemsPanelTemplate",
    "Style",
    "Setter",
    "ItemContainerTemplate",
}

# Avisos que só existem porque a sombra é net8.0 (e não net8.0-windows) ou porque
# o AForge é .NET Framework. Nenhum deles é um problema real do código.
NOWARN = ",".join([
    "CS1701", "CS1702",  # redirecionamento de versão de assembly
    "CA1416",            # API só de Windows — é o ponto: o app real é de Windows
    "NU1701",            # pacote .NET Framework (AForge) restaurado em net8.0
    "NU1702",
    "CS8981",
    "MSB3277",           # conflito de versão entre refs do WPF e do runtime
    # Artefato DESTA ferramenta: os campos de x:Name que ela gera nunca são
    # atribuídos, porque o InitializeComponent() gerado é um stub — no app real
    # quem os preenche é o compilador de marcação. Eram 714 avisos, todos falsos,
    # e ferramenta que enterra o achado real em ruído próprio é ignorada na
    # terceira vez que roda.
    "CS0649",
])


def executar(cmd: list[str], **kw) -> subprocess.CompletedProcess:
    return subprocess.run(cmd, capture_output=True, text=True, **kw)


# ---------------------------------------------------------------------------
# 1. Reference assemblies do WPF
# ---------------------------------------------------------------------------

def garantir_refs() -> Path:
    destino = SOMBRA / "refs"
    if (destino / "PresentationFramework.dll").exists():
        return destino

    destino.mkdir(parents=True, exist_ok=True)
    pacote = SOMBRA / "windowsdesktop.nupkg"

    if not pacote.exists():
        print(f"  baixando reference assemblies do WPF ({VERSAO_WD})…")
        try:
            urllib.request.urlretrieve(URL_WD, pacote)
        except Exception as e:  # noqa: BLE001
            print(f"ERRO: não foi possível baixar {URL_WD}\n  {e}", file=sys.stderr)
            print("  (sem elas a sombra não compila; o CI segue sendo a rede)",
                  file=sys.stderr)
            sys.exit(2)

    with zipfile.ZipFile(pacote) as z:
        for nome in z.namelist():
            if nome.startswith("ref/net8.0/") and nome.endswith(".dll"):
                alvo = destino / Path(nome).name
                with z.open(nome) as origem, open(alvo, "wb") as saida:
                    shutil.copyfileobj(origem, saida)

    return destino


# ---------------------------------------------------------------------------
# 2. Mapa de tipos: nome simples do elemento XAML -> tipo CLR
# ---------------------------------------------------------------------------

DUMPER = r"""
using System.Reflection;
using System.Text.Json;

// args[0] = refs do WPF; args[1] = targeting pack do runtime (traz o System.Runtime
// que o MetadataLoadContext exige como core assembly).
var dlls = Directory.GetFiles(args[0], "*.dll")
    .Concat(Directory.GetFiles(args[1], "*.dll"))
    .ToArray();
var resolver = new PathAssemblyResolver(dlls);
using var ctx = new MetadataLoadContext(resolver);

var mapa = new Dictionary<string, string>();
// Só os assemblies do WPF entram no mapa: o runtime está ali para satisfazer o
// carregador, não para contribuir com tipos de marcação.
foreach (var caminho in Directory.GetFiles(args[0], "*.dll"))
{
    Assembly asm;
    try { asm = ctx.LoadFromAssemblyPath(caminho); } catch { continue; }
    Type[] tipos;
    try { tipos = asm.GetExportedTypes(); } catch { continue; }
    foreach (var t in tipos)
    {
        if (t.Namespace is null || !t.Namespace.StartsWith("System.Windows")) continue;
        if (t.IsGenericTypeDefinition || t.IsNested) continue;
        // Primeiro namespace a declarar o nome ganha; System.Windows.Controls
        // vem antes de System.Windows.Shapes na ordem de preferência do WPF.
        var chave = t.Name;
        var peso = Peso(t.Namespace);
        if (mapa.TryGetValue(chave, out var atual) && Peso(Ns(atual)) <= peso) continue;
        mapa[chave] = t.FullName!;
    }
}

Console.WriteLine(JsonSerializer.Serialize(mapa));

static string Ns(string full) => full[..full.LastIndexOf('.')];

static int Peso(string ns) => ns switch
{
    "System.Windows.Controls" => 0,
    "System.Windows.Controls.Primitives" => 1,
    "System.Windows.Shapes" => 2,
    "System.Windows.Documents" => 3,
    "System.Windows.Media" => 4,
    "System.Windows" => 5,
    _ => 9
};
"""

DUMPER_CSPROJ = """<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net8.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <AssemblyName>mapa</AssemblyName>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="System.Reflection.MetadataLoadContext" Version="8.0.0" />
  </ItemGroup>
</Project>
"""


def mapa_tipos_wpf(refs: Path) -> dict[str, str]:
    cache = SOMBRA / "tipos-wpf.json"
    if cache.exists():
        return json.loads(cache.read_text())

    proj = SOMBRA / "mapa"
    proj.mkdir(parents=True, exist_ok=True)
    (proj / "mapa.csproj").write_text(DUMPER_CSPROJ)
    (proj / "Program.cs").write_text(DUMPER)

    packs = sorted(Path("/usr/lib/dotnet/packs/Microsoft.NETCore.App.Ref").glob("*/ref/net8.0"))
    if not packs:
        packs = sorted(Path(os.environ.get("DOTNET_ROOT", "/usr/share/dotnet"))
                       .glob("packs/Microsoft.NETCore.App.Ref/*/ref/net8.0"))
    if not packs:
        print("ERRO: targeting pack do net8.0 não encontrado.", file=sys.stderr)
        sys.exit(2)

    print("  montando o mapa de tipos do WPF…")
    r = executar(["dotnet", "run", "--project", str(proj), "-v", "q", "--",
                  str(refs), str(packs[-1])])
    if r.returncode != 0:
        print("ERRO ao montar o mapa de tipos:\n" + r.stdout + r.stderr, file=sys.stderr)
        sys.exit(2)

    # `dotnet run` pode escrever ruído antes do JSON; o mapa é a última linha.
    linha = [l for l in r.stdout.splitlines() if l.startswith("{")][-1]
    cache.write_text(linha)
    return json.loads(linha)


TIPO_PROPRIO = re.compile(
    r"^\s*(?:public|internal)\s+(?:sealed\s+|abstract\s+|static\s+|partial\s+)*"
    r"(?:class|record|struct|enum)\s+(\w+)", re.M)
NS_ARQUIVO = re.compile(r"^namespace\s+([\w.]+)\s*;", re.M)


def mapa_tipos_proprios() -> dict[str, str]:
    """Controles e conversores do próprio repositório, usados no XAML por prefixo."""
    mapa: dict[str, str] = {}
    for cs in RAIZ.joinpath("src").rglob("*.cs"):
        if "/obj/" in str(cs) or "/bin/" in str(cs):
            continue
        texto = cs.read_text(encoding="utf-8", errors="replace")
        ns = NS_ARQUIVO.search(texto)
        if not ns:
            continue
        for nome in TIPO_PROPRIO.findall(texto):
            mapa.setdefault(nome, f"{ns.group(1)}.{nome}")
    return mapa


# ---------------------------------------------------------------------------
# 3. Gerador de .g.cs a partir do XAML
# ---------------------------------------------------------------------------

def resolver_tipo(tag: str, prefixos: dict[str, str],
                  wpf: dict[str, str], proprios: dict[str, str]) -> str | None:
    """Nome CLR completo do elemento, ou None se não der para resolver."""
    if "}" in tag:                       # {namespace}Local, como o ElementTree devolve
        uri, local = tag[1:].split("}", 1)
    else:
        uri, local = XMLNS_WPF, tag

    if "." in local:                     # <Grid.RowDefinitions> não é elemento
        return None

    if uri.startswith("clr-namespace:"):
        ns = uri[len("clr-namespace:"):].split(";", 1)[0]
        return f"{ns}.{local}"

    if uri == XMLNS_WPF:
        return wpf.get(local) or proprios.get(local)

    return proprios.get(local)


def campos_do_xaml(caminho: Path, wpf: dict[str, str],
                   proprios: dict[str, str]) -> tuple[str, str, list[tuple[str, str]]] | None:
    """(namespace, classe, [(tipo, nome)]) para um XAML com x:Class."""
    try:
        arvore = ET.parse(caminho)
    except ET.ParseError as e:
        print(f"  XAML ilegível: {caminho.relative_to(RAIZ)} — {e}", file=sys.stderr)
        return None

    raiz = arvore.getroot()
    xclass = raiz.get(f"{{{XMLNS_XAML}}}Class")
    if not xclass:
        return None

    ns, _, classe = xclass.rpartition(".")

    campos: list[tuple[str, str]] = []
    vistos: set[str] = set()

    def andar(no, dentro_de_molde: bool):
        for filho in no:
            tag = filho.tag
            local = tag.split("}")[-1] if "}" in tag else tag
            # `<Style>` e companhia: tudo abaixo é molde.
            molde = dentro_de_molde or local in MOLDES

            nome = filho.get(f"{{{XMLNS_XAML}}}Name") or filho.get("Name")
            if nome and not molde and nome not in vistos:
                tipo = resolver_tipo(tag, {}, wpf, proprios)
                if tipo:
                    vistos.add(nome)
                    campos.append((tipo, nome))
            andar(filho, molde)

    andar(raiz, False)
    return ns, classe, campos


CABECALHO_GERADO = """// <auto-generated />
// Gerado por tools/compilar-sombra.py a partir de {origem}.
// Substitui o compilador de marcação do WPF, que não existe no SDK de Linux.
// NÃO versionado, NÃO usado pelo build real — só pela verificação de C#.
#nullable disable
"""


def gerar_gcs(destino: Path, wpf: dict[str, str], proprios: dict[str, str],
              xamls: list[Path]) -> int:
    destino.mkdir(parents=True, exist_ok=True)
    gerados = 0

    for xaml in xamls:
        info = campos_do_xaml(xaml, wpf, proprios)
        if not info:
            continue
        ns, classe, campos = info

        linhas = [CABECALHO_GERADO.format(origem=xaml.name)]
        if ns:
            linhas.append(f"namespace {ns}\n{{")
        recuo = "    " if ns else ""
        linhas.append(f"{recuo}public partial class {classe}")
        linhas.append(f"{recuo}{{")
        for tipo, nome in campos:
            linhas.append(f"{recuo}    internal {tipo} {nome};")
        linhas.append(f"{recuo}    public void InitializeComponent() {{ }}")
        linhas.append(f"{recuo}}}")
        if ns:
            linhas.append("}")

        saida = destino / f"{ns.replace('.', '_')}_{classe}.g.cs"
        saida.write_text("\n".join(linhas) + "\n")
        gerados += 1

    return gerados


# ---------------------------------------------------------------------------
# 4. Projetos-sombra
# ---------------------------------------------------------------------------

def ler_csproj(caminho: Path) -> ET.Element:
    return ET.parse(caminho).getroot()


def montar_csproj(nome: str, original: Path, refs: Path, gerados: Path) -> str:
    raiz = ler_csproj(original)

    pacotes = []
    for pr in raiz.iter("PackageReference"):
        inc = pr.get("Include")
        ver = pr.get("Version")
        if inc and ver:
            pacotes.append(f'    <PackageReference Include="{inc}" Version="{ver}" />')

    projetos = []
    for pr in raiz.iter("ProjectReference"):
        inc = pr.get("Include", "").replace("\\", "/")
        alvo = (original.parent / inc).resolve()
        dep = alvo.stem
        if dep in PROJETOS_WPF:
            projetos.append(f'    <ProjectReference Include="../{dep}/Sombra.{dep}.csproj" />')
        else:
            projetos.append(f'    <ProjectReference Include="{alvo}" />')

    root_ns = raiz.findtext(".//RootNamespace") or nome

    dlls = "\n".join(
        f'    <Reference Include="{p.stem}"><HintPath>{p}</HintPath></Reference>'
        for p in sorted(refs.glob("*.dll"))
        if p.stem in ("PresentationFramework", "PresentationCore", "WindowsBase",
                      "System.Xaml", "PresentationFramework.Aero2",
                      "System.Windows.Controls.Ribbon", "UIAutomationTypes")
    )

    return f"""<Project Sdk="Microsoft.NET.Sdk">

  <!-- Gerado por tools/compilar-sombra.py. Não versionado. -->

  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <RootNamespace>{root_ns}</RootNamespace>
    <AssemblyName>Sombra.{nome}</AssemblyName>
    <EnableDefaultCompileItems>false</EnableDefaultCompileItems>
    <GenerateAssemblyInfo>false</GenerateAssemblyInfo>
    <NoWarn>{NOWARN}</NoWarn>
    <EnableWindowsTargeting>true</EnableWindowsTargeting>
    <ProduceReferenceAssembly>false</ProduceReferenceAssembly>
  </PropertyGroup>

  <ItemGroup>
    <Compile Include="{RAIZ}/src/{nome}/**/*.cs"
             Exclude="{RAIZ}/src/{nome}/obj/**;{RAIZ}/src/{nome}/bin/**" />
    <Compile Include="{gerados}/*.g.cs" />
  </ItemGroup>

  <ItemGroup>
{dlls}
  </ItemGroup>

  <ItemGroup>
{chr(10).join(pacotes)}
  </ItemGroup>

  <ItemGroup>
{chr(10).join(projetos)}
  </ItemGroup>

</Project>
"""


def main() -> int:
    ap = argparse.ArgumentParser(description=__doc__)
    ap.add_argument("--limpar", action="store_true",
                    help="descarta .sombra/ e refaz tudo do zero")
    args = ap.parse_args()

    if args.limpar and SOMBRA.exists():
        shutil.rmtree(SOMBRA)

    if shutil.which("dotnet") is None:
        print("dotnet não encontrado — nada a fazer.", file=sys.stderr)
        print("  (instale com: apt-get install -y dotnet-sdk-8.0)", file=sys.stderr)
        return 2

    SOMBRA.mkdir(exist_ok=True)
    (SOMBRA / ".gitignore").write_text("*\n")

    refs = garantir_refs()
    wpf = mapa_tipos_wpf(refs)
    proprios = mapa_tipos_proprios()

    total_gerados = 0
    for nome in PROJETOS_WPF:
        origem = RAIZ / "src" / nome
        destino = SOMBRA / nome
        destino.mkdir(parents=True, exist_ok=True)

        xamls = sorted(origem.rglob("*.xaml"))
        total_gerados += gerar_gcs(destino / "gerado", wpf, proprios, xamls)

        (destino / f"Sombra.{nome}.csproj").write_text(
            montar_csproj(nome, origem / f"{nome}.csproj", refs, destino / "gerado"))

    print(f"  {total_gerados} classes de XAML geradas para {len(PROJETOS_WPF)} projetos")

    # Um projeto por chamada: o MSBuild não aceita vários alvos, e a ordem abaixo
    # é a de dependência (o shell antes dos módulos, os módulos antes dos exes),
    # então cada build reaproveita o anterior.
    saida = ""
    falhou = False
    for nome in PROJETOS_WPF:
        r = executar(["dotnet", "build", "-v", "q", "--nologo",
                      "-p:TreatWarningsAsErrors=false",
                      str(SOMBRA / nome / f"Sombra.{nome}.csproj")])
        saida += r.stdout + r.stderr
        falhou = falhou or r.returncode != 0

    erros = [l for l in saida.splitlines() if re.search(r"\berror \w+\d+:", l)]

    if erros:
        print("\nERROS DE COMPILAÇÃO (o CI acusaria os mesmos):\n", file=sys.stderr)
        vistos = set()
        for e in erros:
            # O MSBuild repete cada erro no rodapé; a primeira aparição basta.
            chave = e.split("]")[0]
            if chave in vistos:
                continue
            vistos.add(chave)
            print("  " + e.strip().replace(str(RAIZ) + "/", ""), file=sys.stderr)
        return 1

    if falhou:
        print("\nFalha no build da sombra (sem erro de compilação identificado):\n",
              file=sys.stderr)
        print(saida[-4000:], file=sys.stderr)
        return 1

    print(f"OK — C# dos {len(PROJETOS_WPF)} projetos WPF compila.")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
