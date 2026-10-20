#!/bin/bash
# Prepara a máquina do GitHub para gravar (out/2026): fontes, ffmpeg, Playwright e a prévia
# no ar com a navegação real. Uso: preparar.sh <navegacao.json>
set -euo pipefail
NAV="$(cd "$(dirname "$1")" && pwd)/$(basename "$1")"
AQUI="$(cd "$(dirname "$0")" && pwd)"
RAIZ="$(cd "$AQUI/../../.." && pwd)"
# Inter é a fonte do design system depois da Segoe UI (que o Linux não tem): sem ela o
# Chromium cairia na DejaVu, e o vídeo mostraria uma tela mais larga que a do app.
sudo apt-get update -qq
sudo apt-get install -y -qq fonts-inter ffmpeg > /dev/null
(cd "$AQUI" && npm install --no-audit --no-fund --silent && npx playwright install --with-deps chromium > /dev/null)
dotnet build "$RAIZ/tools/PreviaBlazor/PreviaBlazor.csproj" -c Release -nologo -v q
cd "$RAIZ/tools/PreviaBlazor"
CLINICA_NAVEGACAO="$NAV" ASPNETCORE_URLS=http://127.0.0.1:5199 \
  nohup dotnet bin/Release/net8.0/PreviaBlazor.dll > "$RAIZ/previa.log" 2>&1 &
for i in $(seq 1 90); do curl -s -o /dev/null -w "%{http_code}" http://127.0.0.1:5199/ | grep -q 200 && break; sleep 1; done
curl -sf http://127.0.0.1:5199/ > /dev/null || { cat "$RAIZ/previa.log"; exit 1; }
echo "prévia no ar com a navegação de $NAV"
