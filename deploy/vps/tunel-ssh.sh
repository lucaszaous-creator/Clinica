#!/usr/bin/env bash
# Túnel SÓ do SSH do deploy automático (docs/deploy-automatico.md). Rode UMA vez, como root.
#
# Por que um túnel à parte: o cloudflared-site é proibido, de propósito, de abrir qualquer conexão
# TCP dentro do servidor (o probe.py dele confere "production TCP denied" a cada partida — é o que
# impede alguém de usar o túnel do site para chegar ao banco). O SSH precisa de TCP local; então ele
# ganha um conector próprio, que só fala com a Cloudflare e com 127.0.0.1, sem tocar no do site.
#   bash <(curl -fsSL https://raw.githubusercontent.com/lucaszaous-creator/Clinica/main/deploy/vps/tunel-ssh.sh)
set -euo pipefail
[ "$(id -u)" -eq 0 ] || { echo "rode como root (sudo -i)" >&2; exit 1; }
BIN=$(systemctl show -p ExecStart cloudflared-site | grep -o '/usr/local/bin/cloudflared[^ ;]*' | head -1)
[ -x "$BIN" ] || { echo "binário do cloudflared do site não encontrado" >&2; exit 1; }
# A mesma lista de IPs da Cloudflare que o túnel do site já usa, mais o SSH local.
PERMITIDOS=$(systemctl show -p IPAddressAllow --value cloudflared-site)
[ -n "$PERMITIDOS" ] || { echo "lista de IPs do túnel do site vazia" >&2; exit 1; }

echo "Cole o TOKEN do túnel clinica-ssh (painel da Cloudflare → o túnel → o comando de instalação,"
echo "a parte longa que começa com eyJ). Não aparece enquanto você cola:"
read -r -s TOKEN < /dev/tty; echo
TOKEN=${TOKEN##* }   # aceita o comando inteiro colado: fica só o token
[[ "$TOKEN" =~ ^eyJ[A-Za-z0-9._=-]+$ ]] || { echo "isso não parece um token de túnel" >&2; exit 1; }
install -d -m 0700 /etc/cloudflared-ssh
printf '%s' "$TOKEN" > /etc/cloudflared-ssh/tunnel-token; chmod 0600 /etc/cloudflared-ssh/tunnel-token

cat > /etc/systemd/system/cloudflared-ssh.service <<UNIT
[Unit]
Description=Cloudflare connector SÓ do SSH do deploy automático (sem acesso ao site nem ao banco)
After=network-online.target ssh.service
Wants=network-online.target

[Service]
DynamicUser=yes
LoadCredential=tunnel-token:/etc/cloudflared-ssh/tunnel-token
ExecStart=$BIN tunnel --no-autoupdate --edge-ip-version 4 --metrics 127.0.0.1:20247 --loglevel info run --dns-resolver-addrs 1.1.1.1:53 --dns-resolver-addrs 1.0.0.1:53 --token-file %d/tunnel-token
Restart=on-failure
RestartSec=10
NoNewPrivileges=true
ProtectSystem=strict
ProtectHome=true
PrivateTmp=true
PrivateDevices=true
CapabilityBoundingSet=
RestrictAddressFamilies=AF_INET AF_INET6 AF_UNIX
IPAddressDeny=any
IPAddressAllow=$PERMITIDOS 127.0.0.1 ::1
MemoryMax=200M

[Install]
WantedBy=multi-user.target
UNIT
systemctl daemon-reload
systemctl enable --now cloudflared-ssh
sleep 8
systemctl is-active cloudflared-ssh
journalctl -u cloudflared-ssh --since "-1 min" --no-pager | grep -E "Registered tunnel connection|ERR" | tail -4
echo "Pronto: o túnel do site não foi tocado."
