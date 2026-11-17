#!/usr/bin/env bash
# Roda um comando de operação do conector TISS na VPS (credencial, envio, endereco, codigo, situacao, cancelar, ambiente, testar-*).
# Uso (como root):   /opt/clinica-tiss-conector/current/deploy/comando.sh situacao Petrobras
# Roteiro: docs/tiss-conector.md §4 e §5.
#
# Por que existe: o serviço roda com um usuário do banco que só LÊ a configuração. Mudar a
# credencial, o endereço, o código do prestador ou a chave de envio pede outro usuário do banco,
# com escrita só na configuração e na trilha (tiss_operacao, papel-tiss-operacao.sql) — o do
# arquivo /etc/clinica-tiss-conector/admin.env, que só o root lê e que o serviço nunca carrega.
set -euo pipefail

dir=/etc/clinica-tiss-conector
bin=/opt/clinica-tiss-conector/current/Clinica.Tiss.Conector

[ "$(id -u)" -eq 0 ] || { echo "Rode como root (sudo)." >&2; exit 1; }
[ -r "$dir/conector.env" ] || { echo "Falta $dir/conector.env." >&2; exit 1; }
[ $# -ge 1 ] || { "$bin" --help; exit 2; }

# Lê um arquivo CHAVE=VALOR sem passá-lo pelo shell (nov/2026): com ". arquivo", o ";" da cadeia
# de conexão sem aspas cortava a linha e o resto virava comando. Aqui cada linha é exportada como
# está, com ou sem aspas em volta do valor — o mesmo que o systemd faz com o EnvironmentFile.
carregar() {
  local linha chave valor
  while IFS= read -r linha || [ -n "$linha" ]; do
    linha="${linha%$'\r'}"
    case "$linha" in ''|'#'*) continue ;; esac
    chave="${linha%%=*}"
    valor="${linha#*=}"
    [ "$chave" != "$linha" ] || continue
    case "$valor" in
      \"*\") valor="${valor:1:${#valor}-2}" ;;
      \'*\') valor="${valor:1:${#valor}-2}" ;;
    esac
    export "$chave=$valor"
  done < "$1"
}

carregar "$dir/conector.env"
case "$1" in
  situacao) ;;  # só leitura: o usuário do serviço basta
  # Tirar uma transação Pendente da fila: o usuário do serviço já grava a fila e acrescenta à
  # trilha (papel-tiss-conector.sql); o de operação (admin.env) não alcança a fila.
  cancelar) ;;
  # Os testes na homologação só LEEM a configuração e a guia e acrescentam à trilha: o usuário do
  # serviço basta. O "ambiente" muda a configuração, então vai pelo admin.env (o caso de baixo).
  testar-elegibilidade|testar-lote|testar-protocolo|testar-autorizacao) ;;
  *)
    [ -r "$dir/admin.env" ] || { echo "Falta $dir/admin.env (a conexão com escrita na configuração). Ver docs/tiss-conector.md §4." >&2; exit 1; }
    carregar "$dir/admin.env"
    ;;
esac

exec "$bin" "$@"
