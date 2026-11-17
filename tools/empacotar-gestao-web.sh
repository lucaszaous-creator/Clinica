#!/usr/bin/env bash
# Empacota a GESTÃO WEB (src/Clinica.Gestao.Web) para a VPS (nov/2026, deploy automático).
# Saída: artifacts/gestao-web-<sha12>.tar.gz com app/ (self-contained linux-x64) e manifesto.json
# (o commit e o SHA-256 de cada arquivo). Imprime "pacote=<caminho>" e "sha256=<hash>".
set -euo pipefail
raiz="$(cd "$(dirname "$0")/.." && pwd)"
[ -z "$(git -C "$raiz" status --porcelain)" ] || { echo "Checkout deve estar limpo." >&2; exit 1; }
sha="$(git -C "$raiz" rev-parse HEAD)"
nome="gestao-web-${sha:0:12}"
saida="$raiz/artifacts/$nome"
[ ! -e "$saida" ] || { echo "Este pacote já existe." >&2; exit 1; }
mkdir -p "$saida"
dotnet publish "$raiz/src/Clinica.Gestao.Web" -c Release -r linux-x64 --self-contained true -o "$saida/app" -nologo -v q
cp "$raiz/deploy/gestao/atualizar-gestao.py" "$saida/"
( cd "$saida" && python3 - "$sha" <<'PY'
import hashlib, json, pathlib, sys
arquivos = {str(p): hashlib.sha256(p.read_bytes()).hexdigest() for p in sorted(pathlib.Path('.').rglob('*')) if p.is_file()}
json.dump({'contrato': 1, 'commit': sys.argv[1], 'arquivos': arquivos}, open('manifesto.json', 'w'), indent=1)
PY
)
tar -czf "$raiz/artifacts/$nome.tar.gz" -C "$raiz/artifacts" "$nome"
echo "pacote=$raiz/artifacts/$nome.tar.gz"
echo "sha256=$(sha256sum "$raiz/artifacts/$nome.tar.gz" | cut -d' ' -f1)"
