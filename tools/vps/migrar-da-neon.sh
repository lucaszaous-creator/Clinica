#!/bin/bash
# Copia o banco da Neon para a VPS (docs/banco-na-vps.md, passo 7).
#
# A Neon é apenas LIDA (pg_dump = fotografia consistente; a clínica pode estar
# usando o sistema durante a cópia). O banco LOCAL da VPS é apagado e recriado
# antes de restaurar — por isso o ensaio pode rodar quantas vezes quiser. No
# dia da virada, roda-se uma última vez FORA do expediente, e só então o
# instalar-maquina.bat passa nas máquinas.
set -e
PORTA=45432

read -rp "Cole a connection string da NEON: " NEON
echo ""
echo "Versao do servidor Neon (se nao for 16.x, me avise antes de seguir):"
psql "$NEON" -tAc "show server_version;"

echo "Copiando da Neon (pode levar alguns minutos)..."
pg_dump "$NEON" -Fc -f /tmp/neon.dump
echo "Tamanho da copia: $(du -h /tmp/neon.dump | cut -f1)"

echo "Recriando o banco local da VPS..."
sudo -u postgres psql -p "$PORTA" -c "DROP DATABASE IF EXISTS clinica WITH (FORCE);" >/dev/null
sudo -u postgres psql -p "$PORTA" -c "CREATE DATABASE clinica OWNER clinica;" >/dev/null

echo "Restaurando..."
sudo -u postgres pg_restore -p "$PORTA" -d clinica --no-owner --no-privileges --role=clinica /tmp/neon.dump
sudo -u postgres psql -p "$PORTA" -d clinica -c "ANALYZE;" >/dev/null

echo ""
echo "Tabelas com mais linhas (conferencia):"
sudo -u postgres psql -p "$PORTA" -d clinica -c "SELECT relname AS tabela, n_live_tup AS linhas FROM pg_stat_user_tables ORDER BY n_live_tup DESC LIMIT 12;"
echo "Pronto. A copia ficou em /tmp/neon.dump (some no proximo reboot)."
