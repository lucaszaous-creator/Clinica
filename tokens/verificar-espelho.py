#!/usr/bin/env python3
"""Confere se tokens/colors.css continua espelhando Styles/Tokens.xaml.

O XAML é a fonte da verdade do design system; o CSS existe para o kit web e para a
documentação. Os dois já divergiram uma vez em silêncio (o CSS ficou no azul genérico
depois que o XAML adotou o azul-royal da marca), e ninguém percebeu porque nada
comparava os arquivos. Este script roda no CI e falha o build quando isso acontece.

Uso:  python tokens/verificar-espelho.py
"""
import re
import sys
from pathlib import Path

RAIZ = Path(__file__).resolve().parent.parent
XAML = RAIZ / "src" / "Clinica.Desktop" / "Styles" / "Tokens.xaml"
CSS = RAIZ / "tokens" / "colors.css"


def cor_para_variavel(chave: str) -> str:
    """Cor.Azul.600 -> --azul-600 ; Cor.Marca.Navy -> --marca-navy"""
    partes = chave.split(".")[1:]          # descarta o prefixo "Cor"
    return "--" + "-".join(p.lower() for p in partes)


def main() -> int:
    if not XAML.exists() or not CSS.exists():
        print(f"ERRO: não encontrei {XAML} ou {CSS}", file=sys.stderr)
        return 2

    xaml = dict(re.findall(r'<Color x:Key="([^"]+)">(#[0-9A-Fa-f]{6})</Color>', XAML.read_text(encoding="utf-8")))
    css = dict(re.findall(r'(--[a-z0-9-]+)\s*:\s*(#[0-9A-Fa-f]{6})', CSS.read_text(encoding="utf-8")))

    divergentes, ausentes = [], []
    for chave, valor in xaml.items():
        variavel = cor_para_variavel(chave)
        if variavel not in css:
            ausentes.append(f"{chave} ({variavel} = {valor})")
        elif css[variavel].upper() != valor.upper():
            divergentes.append(f"{variavel}: XAML {valor} != CSS {css[variavel]}   [{chave}]")

    if not divergentes and not ausentes:
        print(f"tokens/colors.css espelha Tokens.xaml — {len(xaml)} cores conferidas.")
        return 0

    print("tokens/colors.css saiu de sincronia com Styles/Tokens.xaml:\n", file=sys.stderr)
    for d in divergentes:
        print(f"  divergente  {d}", file=sys.stderr)
    for a in ausentes:
        print(f"  faltando    {a}", file=sys.stderr)
    print("\nO XAML é a fonte da verdade: ajuste o CSS para bater com ele.", file=sys.stderr)
    return 1


if __name__ == "__main__":
    sys.exit(main())
