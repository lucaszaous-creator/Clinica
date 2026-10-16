#!/bin/bash
# Sobe a prévia numa porta (padrão 5199): para a anterior DESTA porta, recompila e espera.
#   tools/PreviaBlazor/subir.sh [porta]
# O pid fica em tools/PreviaBlazor/.previa-<porta>.pid (ignorado pelo git).
PORTA=${1:-5199}
AQUI=$(cd "$(dirname "$0")" && pwd)
PID="$AQUI/.previa-$PORTA.pid"
[ -f "$PID" ] && kill "$(cat "$PID")" 2>/dev/null
# Qualquer prévia que ainda segure ESTA porta (pid perdido, sessão anterior) também sai.
for p in /proc/[0-9]*; do
  if tr '\0' ' ' < "$p/cmdline" 2>/dev/null | grep -q "PreviaBlazor.dll" && \
     tr '\0' '\n' < "$p/environ" 2>/dev/null | grep -q "ASPNETCORE_URLS=.*:$PORTA\b"; then
    kill "${p#/proc/}" 2>/dev/null
  fi
done
for i in $(seq 1 20); do curl -s -o /dev/null http://127.0.0.1:$PORTA/ || break; sleep 0.5; done
# A prévia antiga em bin/ não pode subir quando a compilação falhou: as fotos mostrariam
# a tela de ANTES e passariam por novas.
# -nodeReuse:false: o nó do MSBuild que sobra herda a saída deste script, e quem chama
# com pipe (`subir.sh | tail`) ficava preso esperando um processo que nunca termina.
if ! SAIDA=$(dotnet build "$AQUI/PreviaBlazor.csproj" -nologo -v q -nodeReuse:false 2>&1); then
    echo "$SAIDA" | grep -E " error " | sort -u
    echo "compilação falhou — a prévia não foi iniciada" >&2
    exit 1
fi
echo "$SAIDA" | grep -E "Build succeeded|Compilação" | sort -u
cd "$AQUI" && ASPNETCORE_URLS=http://127.0.0.1:$PORTA nohup dotnet bin/Debug/net8.0/PreviaBlazor.dll > "$AQUI/.previa-$PORTA.log" 2>&1 &
echo $! > "$PID"
for i in $(seq 1 60); do curl -s -o /dev/null -w "%{http_code}" http://127.0.0.1:$PORTA/ | grep -q 200 && break; sleep 1; done
echo "prévia em http://127.0.0.1:$PORTA/  (log: $AQUI/.previa-$PORTA.log)"
