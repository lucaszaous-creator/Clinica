#!/bin/bash
# gera os 6 vídeos: quadros do cenas.html → H.264 sem som → mistura a voz (loudnorm em 2 passadas, -14,5 LUFS, pico -1,5 dBTP) → AAC
set -e; cd "$(dirname "$0")"
export NODE_PATH="${NODE_PATH:-}"  # node_modules com playwright
N="${NODE:-node}"
FF="${FFMPEG:-ffmpeg}"
OUT=../videos; mkdir -p $OUT /tmp/r3mudo
declare -A VOZ=([C1]=C1-historia-30s [C2]=C2-tres-passos-20s [C3]=C3-macae-20s)
declare -A NOME=([C1]=C1-historia [C2]=C2-tres-passos [C3]=C3-macae)
um() { P=$1; F=$2; M=/tmp/r3mudo/$P-$F.mp4; A=../locucao/${VOZ[$P]}-gabriel.mp3
  [ "$SO_MIX" = 1 ] || $N render.cjs video $M $P $F
  D=$(${FF%/ffmpeg}/ffprobe -v error -show_entries format=duration -of csv=p=0 $M)
  J=$($FF -hide_banner -i $A -af loudnorm=I=-14.5:TP=-1.5:LRA=11:print_format=json -f null - 2>&1 | sed -n '/^{/,/^}/p')
  v() { echo "$J" | python3 -c "import json,sys;print(json.load(sys.stdin)['$1'])"; }
  $FF -y -loglevel error -i $M -i $A -filter_complex "[1:a]loudnorm=I=-14.5:TP=-1.5:LRA=11:measured_I=$(v input_i):measured_TP=$(v input_tp):measured_LRA=$(v input_lra):measured_thresh=$(v input_thresh):offset=$(v target_offset):linear=true,aresample=48000,apad=whole_dur=$D[a]" \
    -map 0:v -map "[a]" -c:v copy -c:a aac -b:a 192k -ar 48000 -ac 2 -t $D -movflags +faststart $OUT/SemDor-R3-${NOME[$P]}-$F.mp4
  echo "pronto $P $F"; }
for P in ${PECAS:-C1 C2 C3}; do for F in 9x16 4x5; do um $P $F & done; wait; done
