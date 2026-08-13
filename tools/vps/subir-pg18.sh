#!/bin/bash
# Sobe a VPS do PostgreSQL 16 (o do Ubuntu) para o 18 (repositório oficial
# PGDG), reaproveitando TUDO: conf.d, pg_hba, certificados e a senha do kit.
#
# Por que existe: a Neon da clínica rodava PostgreSQL 18, e pg_dump/restore
# "para trás" (18 -> 16) não é suportado. A VPS precisa rodar a MESMA versão
# maior que o servidor de origem — confira com o migrar-da-neon.sh, que
# imprime a versão antes de copiar.
#
# Roda com o cluster 16 ainda VAZIO (antes da primeira migração de verdade).
set -e

echo "== Repositorio oficial do PostgreSQL (PGDG)..."
sudo apt install -y postgresql-common >/dev/null
sudo /usr/share/postgresql-common/pgdg/apt.postgresql.org.sh -y
echo "== Instalando o PostgreSQL 18..."
sudo apt install -y postgresql-18

echo "== Levando a configuracao do 16 para o 18..."
sudo cp /etc/postgresql/16/main/conf.d/*.conf /etc/postgresql/18/main/conf.d/
grep '^hostssl' /etc/postgresql/16/main/pg_hba.conf | sudo tee -a /etc/postgresql/18/main/pg_hba.conf >/dev/null

echo "== Removendo o cluster 16 (vazio) e religando na porta 45432..."
sudo pg_dropcluster 16 main --stop
sudo systemctl restart postgresql

echo "== Recriando usuario e banco (mesma senha do kit)..."
SENHA=$(grep -o 'Password=[^;]*' ~/certs/kit/instalar-maquina.bat | head -1 | cut -d= -f2)
sudo -u postgres psql -p 45432 -c "CREATE USER clinica WITH PASSWORD '$SENHA' NOSUPERUSER;" >/dev/null
sudo -u postgres psql -p 45432 -c "CREATE DATABASE clinica OWNER clinica;" >/dev/null

echo ""
echo "== Conferencias:"
pg_lsclusters
sudo -u postgres psql -p 45432 -c "SHOW ssl;"
sudo -u postgres psql -p 45432 -c "SELECT version();"
echo "Pronto: PostgreSQL 18 na porta 45432, TLS ligado, kit continua valendo."
echo "Agora rode de novo:  ~/certs/migrar-da-neon.sh"
