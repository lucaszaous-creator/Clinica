#!/usr/bin/env python3
"""
Grava o tempo medido de cada teste em tests/Clinica.Tests/tempos-dos-testes.json, que o
FatiaDaSuite usa para dividir a suíte em fatias de DURAÇÃO parecida (out/2026).

A divisão pelo hash do nome deixava uma fatia com o dobro da outra (7,2 contra 3,9 min), e o
PR esperava a mais lenta. É o que os CIs grandes fazem (CircleCI "split by timings",
Knapsack): o tempo de cada teste é medido e a divisão equilibra as cargas.

Uso — rode a suíte com o logger trx e passe os arquivos:

    for i in 1 2 3 4; do CLINICA_FATIA=$i/4 dotnet test tests/Clinica.Tests/Clinica.Tests.csproj \
        -c Release --logger "trx;LogFileName=$PWD/tempos-$i.trx" & done; wait
    python3 tools/medir-tempos-dos-testes.py tempos-*.trx

Tempo desatualizado só piora o EQUILÍBRIO, nunca a cobertura: toda chave cai em alguma
fatia, e teste novo (fora do arquivo) cai pelo hash, como antes. Vale regravar quando uma
fatia do CI passar a demorar visivelmente mais que as outras.
"""
import html
import json
import pathlib
import re
import sys

RAIZ = pathlib.Path(__file__).resolve().parent.parent
TESTES = RAIZ / "tests" / "Clinica.Tests"
DESTINO = TESTES / "tempos-dos-testes.json"


def classes_por_caso() -> set[str]:
    """Nome completo das classes marcadas com [FatiarPorCaso] — essas entram caso a caso."""
    achadas = set()
    for arq in TESTES.rglob("*.cs"):
        texto = arq.read_text(encoding="utf-8")
        ns = re.search(r"^namespace\s+([\w.]+)", texto, re.M)
        for m in re.finditer(r"\[FatiarPorCaso\][^{]*?\bclass\s+(\w+)", texto, re.S):
            achadas.add(f"{ns.group(1)}.{m.group(1)}" if ns else m.group(1))
    return achadas


def segundos(duracao: str) -> float:
    h, m, s = duracao.split(":")
    return int(h) * 3600 + int(m) * 60 + float(s)


def main(arquivos: list[str]) -> int:
    if not arquivos:
        print(__doc__)
        return 2
    por_caso = classes_por_caso()
    tempos: dict[str, float] = {}
    for arq in arquivos:
        texto = pathlib.Path(arq).read_text(encoding="utf-8")
        for nome, dur in re.findall(r'<UnitTestResult[^>]*?testName="([^"]+)"[^>]*?duration="([\d:.]+)"', texto):
            nome = html.unescape(nome)
            classe = nome.split("(")[0].rsplit(".", 1)[0]
            chave = nome if classe in por_caso else classe   # a MESMA chave do FatiaDaSuite.ChaveDe
            tempos[chave] = tempos.get(chave, 0.0) + segundos(dur)
    if not tempos:
        print("nenhum resultado nos arquivos — eles são .trx?", file=sys.stderr)
        return 1
    ordenado = {k: round(v, 2) for k, v in sorted(tempos.items())}
    DESTINO.write_text(json.dumps(ordenado, ensure_ascii=False, indent=1) + "\n", encoding="utf-8")
    print(f"{len(ordenado)} chaves, {sum(ordenado.values()) / 60:.1f} min de teste → {DESTINO.relative_to(RAIZ)}")
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
