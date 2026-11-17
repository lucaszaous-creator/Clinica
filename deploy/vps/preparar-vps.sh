#!/usr/bin/env bash
# Prepara a VPS para o deploy automático (docs/deploy-automatico.md). Rode UMA vez, como root:
#   bash <(curl -fsSL https://raw.githubusercontent.com/lucaszaous-creator/Clinica/main/deploy/vps/preparar-vps.sh)
# Cria o usuário clinica-deploy (só executa /usr/local/sbin/clinica-deploy), a chave SSH que o GitHub
# usa e, no fim, imprime UMA linha: o valor do secret VPS_DEPLOY do repositório Clinica.
set -euo pipefail
[ "$(id -u)" -eq 0 ] || { echo "rode como root" >&2; exit 1; }
RAW=https://raw.githubusercontent.com/lucaszaous-creator/Clinica/main/deploy/vps
HOST=${HOST_SSH:-ssh.clinicasemdormacae.com.br}

id clinica-deploy >/dev/null 2>&1 || useradd -m -s /bin/bash clinica-deploy
install -d -m 0700 -o clinica-deploy -g clinica-deploy /home/clinica-deploy/stage /home/clinica-deploy/.ssh

tmp=$(mktemp -d); trap 'rm -rf "$tmp"' EXIT
curl -fsSL "$RAW/clinica-deploy" -o "$tmp/clinica-deploy"
curl -fsSL "$RAW/sudoers-clinica-deploy" -o "$tmp/sudoers"
visudo -cf "$tmp/sudoers" >/dev/null
install -m 0755 -o root -g root "$tmp/clinica-deploy" /usr/local/sbin/clinica-deploy
install -m 0440 -o root -g root "$tmp/sudoers" /etc/sudoers.d/clinica-deploy

# A chave só vale chegando pelo túnel (cloudflared → localhost:22).
ssh-keygen -q -t ed25519 -N '' -C github-deploy -f "$tmp/chave"
printf 'restrict,from="127.0.0.1,::1" %s\n' "$(cat "$tmp/chave.pub")" > /home/clinica-deploy/.ssh/authorized_keys
chown clinica-deploy:clinica-deploy /home/clinica-deploy/.ssh/authorized_keys
chmod 0600 /home/clinica-deploy/.ssh/authorized_keys
hosts=$(ssh-keyscan -t ed25519 127.0.0.1 2>/dev/null | sed "s/^127\.0\.0\.1/$HOST/")
[ -n "$hosts" ] || { echo "o sshd não respondeu em localhost:22" >&2; exit 1; }

echo
echo "Cole aqui o service token do Cloudflare Access (Zero Trust → Access → Service Auth):"
read -r -p "Client ID: " CF_ID < /dev/tty
read -r -s -p "Client Secret: " CF_SECRET < /dev/tty; echo
[ -n "$CF_ID" ] && [ -n "$CF_SECRET" ] || { echo "service token vazio" >&2; exit 1; }

valor=$(printf 'HOST=%s\nCF_ID=%s\nCF_SECRET=%s\nCHAVE_B64=%s\nHOSTS_B64=%s\n' \
  "$HOST" "$CF_ID" "$CF_SECRET" "$(base64 -w0 "$tmp/chave")" "$(printf '%s\n' "$hosts" | base64 -w0)" | base64 -w0)
echo
echo "Pronto. Crie o secret VPS_DEPLOY em github.com/lucaszaous-creator/Clinica → Settings → Secrets → Actions com:"
echo
echo "$valor"
echo
echo "(A chave privada não fica no servidor: só neste valor.)"
