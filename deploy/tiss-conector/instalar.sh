#!/usr/bin/env bash
# Instala (ou atualiza) o conector TISS na VPS da clínica, com o ENVIO DE GUIAS DESLIGADO.
# Roteiro: docs/tiss-conector.md, "Instalação na VPS". Nenhum segredo é impresso na tela.
#
# Uso, como root, de dentro da pasta deploy/ do pacote publicado (ou com o .tar.gz ao lado):
#   sudo bash instalar.sh                       # instala e não pede credencial
#   sudo bash instalar.sh Petrobras Amil        # e pede a credencial de cada convênio, na hora
#   ESPERADO_SHA256=… sudo -E bash instalar.sh  # confere o .tar.gz contra o resumo anotado
#   PGPORT=5432 sudo -E bash instalar.sh        # outra porta do Postgres (padrão 45432)
#
# Pode rodar de novo: cada passo confere se já foi feito. O que ele NÃO faz: ligar o envio de
# guias (TISS_ENVIO_HABILITADO continua false e a chave de cada convênio continua desligada).
set -euo pipefail

[ "$(id -u)" -eq 0 ] || { echo "Rode com sudo: sudo bash $0"; exit 1; }

porta="${PGPORT:-45432}"
etc=/etc/clinica-tiss-conector
opt=/opt/clinica-tiss-conector
aqui="$(cd "$(dirname "$0")" && pwd)"
pghba=$(ls /etc/postgresql/*/main/pg_hba.conf 2>/dev/null | head -1 || true)
[ -n "$pghba" ] || { echo "Não achei o pg_hba.conf do Postgres em /etc/postgresql/*/main/."; exit 1; }
# O root lê o SQL e o entrega PELA ENTRADA: o usuário postgres não lê a pasta do conector.
psql_pg() { sudo -u postgres psql -p "$porta" -d clinica -v ON_ERROR_STOP=1 -qtA "$@"; }

# Acrescenta "local clinica PAPEL scram-sha-256" ANTES da primeira regra ativa do pg_hba (senão uma
# regra genérica "local all all" casa primeiro). Devolve 0 se mudou o arquivo.
regra_local() {
  local papel="$1" linha="local   clinica   $1   scram-sha-256"
  grep -qE "^\s*local\s+clinica\s+$papel\b" "$pghba" && return 1
  cp -a "$pghba" "$pghba.antes-$papel-$(date +%Y%m%d%H%M%S)"
  awk -v l="$linha" 'BEGIN{f=0} !f && $0 !~ /^[[:space:]]*(#|$)/ {print l; f=1} {print} END{if(!f) print l}' "$pghba" > "$pghba.novo"
  cat "$pghba.novo" > "$pghba"; rm -f "$pghba.novo"
  return 0
}

echo "== 1/7 O pacote"
if [ -x "$aqui/../Clinica.Tiss.Conector" ]; then
  origem="$(cd "$aqui/.." && pwd)"
  versao="$(basename "$origem")"
  echo "   rodando de dentro do pacote: $versao"
else
  pacote=$(ls "$aqui"/tiss-conector-*.tar.gz 2>/dev/null | head -1 || true)
  [ -n "$pacote" ] || { echo "Não achei o pacote: rode de dentro de tiss-conector-XXXXXXX/deploy ou com o .tar.gz ao lado."; exit 1; }
  if [ -n "${ESPERADO_SHA256:-}" ]; then
    echo "$ESPERADO_SHA256  $pacote" | sha256sum -c --quiet || { echo "O pacote não confere (sha256). Pare aqui."; exit 1; }
    echo "   sha256 confere"
  else
    echo "   (sem ESPERADO_SHA256: confira você o resumo com sha256sum $pacote)"
  fi
  versao="$(basename "$pacote" .tar.gz)"
  origem=""
fi

echo "== 2/7 O banco"
[ "$(psql_pg -c "select to_regclass('public.\"TransacoesTiss\"') is not null")" = "t" ] || {
  echo "   A tabela TransacoesTiss ainda não existe: abra uma vez um app atualizado (ele aplica a"
  echo "   atualização do banco) e rode de novo."; exit 1; }
echo "   tabelas do envio TISS presentes"

echo "== 3/7 Arquivos"
id clinica-tiss >/dev/null 2>&1 || useradd --system --home-dir /nonexistent --shell /usr/sbin/nologin --user-group clinica-tiss
mkdir -p "$opt/releases" "$etc"; chmod 700 "$etc"
if [ ! -d "$opt/releases/$versao" ]; then
  if [ -n "$origem" ]; then cp -a "$origem" "$opt/releases/$versao"; else tar -xzf "$pacote" -C "$opt/releases"; fi
fi
ln -sfn "$opt/releases/$versao" "$opt/current"
chown -R root:clinica-tiss "$opt/releases/$versao"; chmod -R g+rX,o-rwx "$opt/releases/$versao"
echo "   instalado em $opt/current ($versao)"

echo "== 4/7 Usuário do banco do serviço (tiss_conector)"
if [ ! -f "$etc/conector.env" ]; then
  senha=$(openssl rand -hex 24)
  psql_pg -v senha="'$senha'" < "$opt/current/deploy/papel-tiss-conector.sql" >/dev/null
  # A chave de cifra: a da clínica, se outro serviço já tiver; senão, uma nova.
  chave=$(grep -hs '^CLINICA_CREDENCIAIS_CHAVE=' /etc/clinica-*/*.env 2>/dev/null | head -1 | cut -d= -f2- | tr -d '"' || true)
  origem_chave="reaproveitada de outro serviço da clínica"
  if [ -z "$chave" ]; then chave=$(openssl rand -base64 32); origem_chave="NOVA (gerada agora; guarde uma cópia fora do servidor)"; fi
  install -m 600 /dev/null "$etc/conector.env"
  sed -e "s|Password=SENHA_DO_PAPEL_TISS_CONECTOR|Password=$senha|" \
      -e "s|Port=45432|Port=$porta|" \
      -e "s|^CLINICA_CREDENCIAIS_CHAVE=.*|CLINICA_CREDENCIAIS_CHAVE=$chave|" \
      -e "s|^TISS_ENVIO_HABILITADO=.*|TISS_ENVIO_HABILITADO=false|" \
      "$opt/current/deploy/conector.env.example" > "$etc/conector.env"
  unset senha chave
  echo "   conector.env criado; chave de cifra: $origem_chave; envio de guias: DESLIGADO"
else
  # A versão antiga gravava a conexão sem aspas: põe as aspas (o comando.sh novo lê dos dois jeitos).
  sed -i -E 's/^(ConnectionStrings__Clinica)=([^"].*)$/\1="\2"/' "$etc/conector.env"
  echo "   conector.env já existe: mantido (conexão entre aspas)"
fi
if regra_local tiss_conector; then systemctl reload postgresql; echo "   pg_hba: regra do tiss_conector acrescentada"; fi

echo "== 5/7 Usuário do banco de quem opera (tiss_operacao)"
# O admin.env antigo usava o usuário dos APPS (o banco inteiro): troca pelo de operação.
if [ ! -f "$etc/admin.env" ] || ! grep -q 'Username=tiss_operacao' "$etc/admin.env"; then
  senha=$(openssl rand -hex 24)
  psql_pg -v senha="'$senha'" < "$opt/current/deploy/papel-tiss-operacao.sql" >/dev/null
  [ -f "$etc/admin.env" ] && cp -a "$etc/admin.env" "$etc/admin.env.antes-tiss-operacao"
  install -m 600 /dev/null "$etc/admin.env"
  echo "ConnectionStrings__Clinica=\"Host=/var/run/postgresql;Port=$porta;Database=clinica;Username=tiss_operacao;Password=$senha\"" > "$etc/admin.env"
  unset senha
  rm -f "$etc/admin.env.antes-tiss-operacao"
  echo "   admin.env agora usa tiss_operacao (só configuração e trilha)"
else
  echo "   admin.env já usa tiss_operacao: mantido"
fi
if regra_local tiss_operacao; then systemctl reload postgresql; echo "   pg_hba: regra do tiss_operacao acrescentada"; fi

echo "== 6/7 Serviço"
cp "$opt/current/deploy/clinica-tiss-conector.service" /etc/systemd/system/
systemctl daemon-reload
systemctl enable clinica-tiss-conector >/dev/null 2>&1 || true
systemctl restart clinica-tiss-conector
sleep 5
if systemctl is-active --quiet clinica-tiss-conector; then
  echo "   serviço no ar"; journalctl -u clinica-tiss-conector -n 3 --no-pager -o cat
else
  echo "   o serviço NÃO subiu. Últimas linhas:"; journalctl -u clinica-tiss-conector -n 20 --no-pager; exit 1
fi

echo "== 7/7 Credenciais"
for convenio in "$@"; do
  read -r -p "   Login do web service de $convenio (Enter para pular): " TISS_WS_LOGIN
  [ -n "$TISS_WS_LOGIN" ] || continue
  read -r -p "   Seu nome (vai para a trilha): " operador
  export TISS_WS_LOGIN
  echo "   Agora digite a SENHA do portal de $convenio e Enter (ela não aparece):"
  "$opt/current/deploy/comando.sh" credencial "$convenio" --senha-na-entrada --operador "$operador" || echo "   a credencial de $convenio NÃO foi gravada (motivo acima)."
  unset TISS_WS_LOGIN
done
for convenio in "$@"; do "$opt/current/deploy/comando.sh" situacao "$convenio" || true; done

echo
echo "Pronto. O envio de guias continua DESLIGADO (TISS_ENVIO_HABILITADO=false e cada convênio desligado)."
