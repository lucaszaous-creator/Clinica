#!/usr/bin/env bash
# Roda um comando de operação do conector TISS na VPS (credencial, envio, endereco, situacao).
# Uso (como root):   /opt/clinica-tiss-conector/current/deploy/comando.sh situacao Petrobras
# Roteiro: docs/tiss-conector.md §4 e §5.
#
# Por que existe: o serviço roda com um usuário do banco que só LÊ a configuração. Mudar a
# credencial ou a chave de envio pede outro usuário do banco, com escrita — o do arquivo
# /etc/clinica-tiss-conector/admin.env, que só o root lê e que o serviço nunca carrega.
set -euo pipefail

dir=/etc/clinica-tiss-conector
bin=/opt/clinica-tiss-conector/current/Clinica.Tiss.Conector

[ "$(id -u)" -eq 0 ] || { echo "Rode como root (sudo)." >&2; exit 1; }
[ -r "$dir/conector.env" ] || { echo "Falta $dir/conector.env." >&2; exit 1; }
[ $# -ge 1 ] || { "$bin" --help; exit 2; }

set -a
# shellcheck disable=SC1091
. "$dir/conector.env"
case "$1" in
  situacao) ;;  # só leitura: o usuário do serviço basta
  *)
    [ -r "$dir/admin.env" ] || { echo "Falta $dir/admin.env (a conexão com escrita na configuração). Ver docs/tiss-conector.md §4." >&2; exit 1; }
    # shellcheck disable=SC1091
    . "$dir/admin.env"
    ;;
esac
set +a

exec "$bin" "$@"
