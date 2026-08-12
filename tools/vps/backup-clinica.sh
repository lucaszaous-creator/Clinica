#!/bin/bash
# Backup diário do banco (docs/banco-na-vps.md, passo 8). Instalado em
# /usr/local/bin e disparado por /etc/cron.d/backup-clinica às 02:30.
# Guarda 14 dias em /var/backups/clinica e anota cada execução no backup.log —
# log que ninguém escreve é backup que ninguém percebe que parou.
# A cópia para FORA da VPS (rclone/R2 ou o backup do painel do provedor) é a
# segunda metade obrigatória: cópia no mesmo disco morre junto com ele.
set -e
DIA=$(date +%F)
mkdir -p /var/backups/clinica
sudo -u postgres /usr/lib/postgresql/18/bin/pg_dump -p 45432 -Fc clinica > /var/backups/clinica/clinica-$DIA.dump
find /var/backups/clinica -name 'clinica-*.dump' -mtime +14 -delete
echo "$(date) ok $(du -h /var/backups/clinica/clinica-$DIA.dump | cut -f1)" >> /var/backups/clinica/backup.log
