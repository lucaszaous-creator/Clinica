#!/usr/bin/env bash
# Prepara a VPS para a GESTÃO WEB (docs/gestao-web.md) — rode UMA vez, como root, DEPOIS que o portal
# novo estiver no ar:
#   bash <(curl -fsSL https://raw.githubusercontent.com/lucaszaous-creator/Clinica/main/deploy/vps/instalar-gestao.sh)
# Cria o usuário e o papel de banco da gestão, dá a ela LEITURA no anel de chaves do portal, instala a
# unidade do systemd e o grupo do socket no túnel. O aplicativo em si chega pelo deploy automático
# (o próximo Run workflow instala o primeiro release). Pode rodar de novo: cada passo confere antes.
set -euo pipefail
[ "$(id -u)" -eq 0 ] || { echo "rode como root (sudo -i)" >&2; exit 1; }
RAW=https://raw.githubusercontent.com/lucaszaous-creator/Clinica/main
PORTA=45432
BANCO=clinica
CHAVES=/var/lib/clinica-tablet/chaves
passo() { echo; echo "== $*"; }

passo "Conferências"
systemctl is-active --quiet clinica-tablet || { echo "o portal (clinica-tablet) não está rodando" >&2; exit 1; }
[ -d "$CHAVES" ] || { echo "anel de chaves do portal ausente: $CHAVES" >&2; exit 1; }
sudo -u postgres psql -p "$PORTA" -d "$BANCO" -Atc 'select 1' >/dev/null
command -v setfacl >/dev/null || { apt-get update -qq && apt-get install -y -qq acl; }
tmp=$(mktemp -d); trap 'rm -rf "$tmp"' EXIT
for f in deploy/gestao/papel-gestao.sql deploy/gestao/clinica-gestao.service deploy/vps/clinica-deploy; do
  curl -fsSL "$RAW/$f" -o "$tmp/$(basename "$f")"
done

passo "Usuário e grupos"
getent group clinica-gestao-proxy >/dev/null || groupadd --system clinica-gestao-proxy
getent group clinica-portal-chaves >/dev/null || groupadd --system clinica-portal-chaves
id clinica-gestao >/dev/null 2>&1 || useradd --system --user-group --no-create-home --shell /usr/sbin/nologin clinica-gestao
usermod -a -G clinica-portal-chaves clinica-gestao

passo "Leitura do anel de chaves do portal (ACL; o portal continua dono e único a escrever)"
# O systemd reaplica o modo da pasta de estado a cada partida do portal: 0710 deixa ATRAVESSAR,
# nunca listar. Sem isso, a ACL abaixo seria anulada na próxima reinicialização do portal.
install -d -m 0755 /etc/systemd/system/clinica-tablet.service.d
printf '[Service]\nStateDirectoryMode=0710\n' > /etc/systemd/system/clinica-tablet.service.d/gestao-chaves.conf
chmod 0710 /var/lib/clinica-tablet
setfacl -m g:clinica-portal-chaves:x /var/lib/clinica-tablet
setfacl -m g:clinica-portal-chaves:rx "$CHAVES"
setfacl -d -m g:clinica-portal-chaves:r "$CHAVES"          # chaves novas (giro a cada 90 dias)
find "$CHAVES" -maxdepth 1 -type f -name 'key-*.xml' -exec setfacl -m g:clinica-portal-chaves:r {} +
sudo -u clinica-gestao test -r "$(find "$CHAVES" -maxdepth 1 -name 'key-*.xml' | head -1)" \
  || { echo "a gestão ainda não lê as chaves — pare aqui e mande esta saída" >&2; exit 1; }
echo "ok: a gestão lê as chaves"

passo "Papel do banco (só DML; raízes do prontuário sem DELETE)"
dono=$(sudo -u postgres psql -p "$PORTA" -d "$BANCO" -Atc "select tableowner from pg_tables where tablename='Evolucoes'")
[ -n "$dono" ] || { echo "tabela Evolucoes não encontrada em $BANCO" >&2; exit 1; }
sudo -u postgres psql -p "$PORTA" -d "$BANCO" -q -v ON_ERROR_STOP=1 -v desktop="$dono" -f "$tmp/papel-gestao.sql"
if sudo -u clinica-gestao psql -h /var/run/postgresql -p "$PORTA" -d "$BANCO" -Atc 'select count(*) from "Usuarios"' >/dev/null 2>&1; then
  echo "ok: a gestão entra no banco (peer, sem senha)"
else
  hba=$(sudo -u postgres psql -p "$PORTA" -Atc 'show hba_file')
  echo "a gestão não entrou no banco. Acrescente ANTES das outras linhas 'local' de $hba:" >&2
  echo "  local   $BANCO   clinica-gestao   peer" >&2
  echo "depois: systemctl reload postgresql   e rode este script de novo." >&2
  exit 1
fi

passo "Configuração e unidade"
install -d -m 0750 -o root -g clinica-gestao /etc/clinica-gestao
if [ ! -f /etc/clinica-gestao/gestao.env ]; then
  cat > /etc/clinica-gestao/gestao.env <<ENV
ASPNETCORE_ENVIRONMENT=Production
TZ=America/Sao_Paulo
Gestao__Socket=/run/clinica-gestao/gestao.sock
Gestao__Origem=https://portal.clinicasemdormacae.com.br
Gestao__DiretorioChaves=$CHAVES
ConnectionStrings__Clinica="Host=/var/run/postgresql;Port=$PORTA;Database=$BANCO;Username=clinica-gestao;Timeout=10;Maximum Pool Size=20;Include Error Detail=false"
ENV
fi
chown root:clinica-gestao /etc/clinica-gestao/gestao.env; chmod 0640 /etc/clinica-gestao/gestao.env
install -m 0644 "$tmp/clinica-gestao.service" /etc/systemd/system/clinica-gestao.service
install -d -m 0755 /opt/clinica-gestao/releases
# O comando de deploy mais novo (sabe fazer a PRIMEIRA instalação da gestão).
install -m 0755 -o root -g root "$tmp/clinica-deploy" /usr/local/sbin/clinica-deploy

passo "Túnel: o cloudflared ganha o grupo do socket da gestão"
install -d -m 0755 /etc/systemd/system/cloudflared-site.service.d
printf '[Service]\nSupplementaryGroups=clinica-gestao-proxy\n' > /etc/systemd/system/cloudflared-site.service.d/gestao.conf
systemctl daemon-reload
echo "reiniciando o túnel (o portal some por alguns segundos)..."
systemctl restart cloudflared-site
sleep 5; systemctl is-active cloudflared-site

echo
echo "Pronto. Falta só:"
echo " 1) no painel da Cloudflare, túnel clinica-site → Routes → Add route: portal . clinicasemdormacae.com.br,"
echo "    Path ^/gestao(/.*)?\$ , Service URL unix:/run/clinica-gestao/gestao.sock — e ARRASTE para cima da rota do portal;"
echo " 2) rodar o deploy (Actions → Deploy do portal e da gestão → Run workflow), que instala o primeiro release."
