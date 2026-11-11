#!/usr/bin/env bash
# Publica o conector TISS para linux-x64, numa pasta com o commit no nome, pronta para copiar à VPS.
# Roda no computador de quem instala (Linux ou WSL, com o SDK 8) — NÃO na VPS e não instala nada.
#   deploy/tiss-conector/publicar.sh            # gera ./tiss-conector-XXXXXXX e o .tar.gz ao lado
# Roteiro completo: docs/tiss-conector.md.
set -euo pipefail

raiz="$(cd "$(dirname "$0")/../.." && pwd)"
cd "$raiz"

if [ -n "$(git status --porcelain --untracked-files=no)" ]; then
  echo "Há alterações não commitadas: publique a partir de um commit, para a pasta dizer o que tem dentro." >&2
  exit 1
fi

commit="$(git rev-parse --short HEAD)"
saida="tiss-conector-$commit"
rm -rf "$saida" "$saida.tar.gz"

# Autocontido: a VPS não precisa ter o .NET instalado.
dotnet publish src/Clinica.Tiss.Conector -c Release -r linux-x64 --self-contained \
  -p:DebugType=None -p:DebugSymbols=false -o "$saida"

# O que acompanha a instalação vai junto, para ninguém copiar arquivo de outra versão.
mkdir -p "$saida/deploy"
cp deploy/tiss-conector/clinica-tiss-conector.service deploy/tiss-conector/conector.env.example \
   deploy/tiss-conector/papel-tiss-conector.sql deploy/tiss-conector/comando.sh \
   deploy/tiss-conector/admin.env.example "$saida/deploy/"
echo "$commit" > "$saida/COMMIT"

tar -czf "$saida.tar.gz" "$saida"
echo
echo "Pronto: $saida/ e $saida.tar.gz"
echo "Confira o resumo antes de copiar:  sha256sum $saida.tar.gz"
