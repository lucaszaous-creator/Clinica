#!/bin/bash
# Copia o banco da Neon para a VPS (docs/banco-na-vps.md, passo 7).
#
# A Neon é apenas LIDA (pg_dump = fotografia consistente). O banco LOCAL é
# apagado e recriado antes de restaurar — o ensaio e a virada são o mesmo
# comando; a virada é só a última rodada fora do expediente.
#
# A conexão com a Neon é montada campo a campo (host, IPv4 resolvido na mão,
# porta 5432 FIXADA, usuário, senha), em vez de entregar a URI ao psql: na
# instalação real, um resto de ambiente fez o psql mirar a porta do banco
# LOCAL (45432) contra o IP da Neon — timeout silencioso. Ditar cada campo é
# o que impede qualquer padrão herdado de vazar. O IPv4 fixado também pula os
# endereços IPv6 do endpoint, que a VPS não roteia.
set -e
unset PGPORT PGHOST PGUSER PGDATABASE PGPASSWORD PGHOSTADDR

read -rp "Cole a connection string da NEON: " NEON
NEON=$(echo "$NEON" | tr -d '[:space:]\r')
HOST=$(echo "$NEON" | sed -E 's|.*@([^/:?]+).*|\1|')
USUARIO=$(echo "$NEON" | sed -E 's|.*://([^:@]+):.*|\1|')
SENHA=$(echo "$NEON" | sed -E 's|.*://[^:@]+:([^@]+)@.*|\1|')
BANCO=$(echo "$NEON" | sed -E 's|.*/([^/?]+)(\?.*)?$|\1|')
IP4=$(getent ahostsv4 "$HOST" | awk '{print $1; exit}')
echo ""
echo "Usuario: $USUARIO | Banco: $BANCO"
echo "Endpoint: $HOST -> IPv4 $IP4, porta 5432 (fixada)"

export PGPASSWORD="$SENHA" PGHOSTADDR="$IP4" PGSSLMODE=require PGCONNECT_TIMEOUT=20
echo "Conectando (ate 6 tentativas de 20s - a Neon dorme e demora a acordar)..."
VERSAO=""
for T in 1 2 3 4 5 6; do
  VERSAO=$(psql -h "$HOST" -p 5432 -U "$USUARIO" -d "$BANCO" -tAc "show server_version;" 2>&1) && break
  echo "tentativa $T falhou: $VERSAO"
  sleep 3
done
echo "Versao do servidor Neon: $VERSAO"
case "$VERSAO" in 18*) ;; *) echo "ATENCAO: nao comecou com 18 - pare aqui e me mostre isto."; exit 1;; esac

echo "Copiando da Neon (acompanhe noutra janela com: ls -lh /tmp/neon.dump)..."
pg_dump -h "$HOST" -p 5432 -U "$USUARIO" -d "$BANCO" -Fc -f /tmp/neon.dump
unset PGPASSWORD PGHOSTADDR PGSSLMODE PGCONNECT_TIMEOUT
echo "Tamanho da copia: $(du -h /tmp/neon.dump | cut -f1)"

echo "Recriando o banco local da VPS..."
sudo -u postgres psql -p 45432 -c "DROP DATABASE IF EXISTS clinica WITH (FORCE);" >/dev/null
sudo -u postgres psql -p 45432 -c "CREATE DATABASE clinica OWNER clinica;" >/dev/null
echo "Restaurando..."
sudo -u postgres pg_restore -p 45432 -d clinica --no-owner --no-privileges --role=clinica /tmp/neon.dump
sudo -u postgres psql -p 45432 -d clinica -c "ANALYZE;" >/dev/null

echo ""
echo "Tabelas com mais linhas (conferencia):"
sudo -u postgres psql -p 45432 -d clinica -c "SELECT relname AS tabela, n_live_tup AS linhas FROM pg_stat_user_tables ORDER BY n_live_tup DESC LIMIT 12;"
echo "Pronto. A copia ficou em /tmp/neon.dump (some no proximo reboot)."
