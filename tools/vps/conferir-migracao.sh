#!/bin/bash
# Compara a cópia da VPS com a Neon AO VIVO, tabela por tabela, com count(*)
# exato dos dois lados (docs/banco-na-vps.md, passo 7). É a resposta honesta a
# "não há risco de ter perdido algo?": em vez de confiar no pg_restore ter
# terminado sem erro, conta-se tudo de novo nos dois lados e imprime-se a
# diferença — inclusive tabela que exista só de um lado.
set -e
unset PGPORT PGHOST PGUSER PGDATABASE PGPASSWORD PGHOSTADDR

read -rp "Cole a connection string da NEON: " NEON
NEON=$(echo "$NEON" | tr -d '[:space:]\r')
HOST=$(echo "$NEON" | sed -E 's|.*@([^/:?]+).*|\1|')
USUARIO=$(echo "$NEON" | sed -E 's|.*://([^:@]+):.*|\1|')
SENHA=$(echo "$NEON" | sed -E 's|.*://[^:@]+:([^@]+)@.*|\1|')
BANCO=$(echo "$NEON" | sed -E 's|.*/([^/?]+)(\?.*)?$|\1|')
IP4=$(getent ahostsv4 "$HOST" | awk '{print $1; exit}')
export PGPASSWORD="$SENHA" PGHOSTADDR="$IP4" PGSSLMODE=require PGCONNECT_TIMEOUT=20

neon() { psql -h "$HOST" -p 5432 -U "$USUARIO" -d "$BANCO" -tAc "$1"; }
vps()  { sudo -u postgres psql -p 45432 -d clinica -tAc "$1"; }

SQL_TABELAS="SELECT quote_ident(table_name) FROM information_schema.tables WHERE table_schema='public' AND table_type='BASE TABLE' ORDER BY 1;"
TAB_NEON=$(neon "$SQL_TABELAS")
TAB_VPS=$(vps "$SQL_TABELAS")

SO_NEON=$(comm -23 <(echo "$TAB_NEON") <(echo "$TAB_VPS"))
SO_VPS=$(comm -13 <(echo "$TAB_NEON") <(echo "$TAB_VPS"))

echo ""
printf "%-38s %10s %10s\n" "TABELA" "NEON" "VPS"
DIVERGE=0
for T in $TAB_NEON; do
  echo "$SO_NEON" | grep -qx "$T" && continue
  N=$(neon "SELECT count(*) FROM $T;")
  V=$(vps "SELECT count(*) FROM $T;")
  MARCA=""
  if [ "$N" != "$V" ]; then MARCA="  <== DIFERENTE"; DIVERGE=1; fi
  printf "%-38s %10s %10s%s\n" "$T" "$N" "$V" "$MARCA"
done

if [ -n "$SO_NEON" ]; then echo ""; echo "SO NA NEON (nao migradas!): $SO_NEON"; DIVERGE=1; fi
if [ -n "$SO_VPS" ];  then echo ""; echo "So na VPS (criadas depois, ex. por migration nova): $SO_VPS"; fi

echo ""
if [ "$DIVERGE" -eq 0 ]; then
  echo "RESULTADO: as contagens de TODAS as tabelas batem. Copia integra."
else
  echo "RESULTADO: HA DIFERENCA — veja as marcas acima. Se a Neon seguiu em uso"
  echo "depois da foto, diferenca pequena e esperada; na virada, a ultima rodada"
  echo "do migrar-da-neon.sh (fora do expediente) zera isso."
fi
