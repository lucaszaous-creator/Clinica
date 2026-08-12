#!/bin/bash
# Gera certificados de cliente em LOTE e monta o "kit" de instalação das
# máquinas da clínica (docs/banco-na-vps.md, passo 6).
#
# Uso:  ./gerar-kit.sh SENHA-DO-PFX 25            → maquina-01 … maquina-25
#       ./gerar-kit.sh SENHA-DO-PFX nome1 nome2   → nomes escolhidos
#
# Não é preciso saber o nome de máquina nenhuma: o instalar-maquina.bat pega o
# primeiro certificado livre da pilha e anota sozinho, no registro.txt do kit,
# qual computador ficou com qual certificado. Gere com folga — certificado não
# usado não custa nada e sobra para a próxima máquina nova.
#
# Roda na pasta ~/certs da VPS (precisa do ca.crt e ca.key do passo 4).
#
# A MESMA senha de pfx serve para o lote inteiro, e isso é decisão: o .pfx mora
# no disco da máquina que o usa, então a senha dele é lacre de transporte, não
# fechadura do sistema — as fechaduras são o certificado em si e a senha do
# banco. Vinte senhas de pfx diferentes só produziriam uma planilha a mais para
# errar.
set -e
cd "$(dirname "$0")"

SENHA="$1"; shift || true
if [ -z "$SENHA" ] || [ "$#" -eq 0 ]; then
  echo "Uso: ./gerar-kit.sh SENHA-DO-PFX 25            (gera maquina-01..maquina-25)"
  echo "     ./gerar-kit.sh SENHA-DO-PFX nome1 nome2   (nomes escolhidos)"
  exit 1
fi
if [ ! -f ca.key ]; then
  echo "ca.key nao encontrado — rode dentro da pasta ~/certs da VPS (passo 4)."
  exit 1
fi

if [ "$#" -eq 1 ] && [[ "$1" =~ ^[0-9]+$ ]]; then
  NOMES=$(seq -f 'maquina-%02g' 1 "$1")
else
  NOMES="$*"
fi

mkdir -p kit
cp -f ca.crt kit/

for M in $NOMES; do
  openssl req -new -nodes -newkey rsa:2048 -keyout "$M.key" -out "$M.csr" -subj "/CN=$M" 2>/dev/null
  openssl x509 -req -in "$M.csr" -CA ca.crt -CAkey ca.key -CAcreateserial -days 1825 -out "$M.crt" 2>/dev/null
  openssl pkcs12 -export -out "kit/$M.pfx" -inkey "$M.key" -in "$M.crt" -passout "pass:$SENHA"
  echo "gerado: kit/$M.pfx (vence em $(date -d '+1825 days' +%d/%m/%Y))"
done

echo ""
echo "Kit pronto em $(pwd)/kit"
echo "1) Baixe para o Windows:  scp -r clinica-admin@IP-DA-VPS:certs/kit C:\\kit"
echo "2) Junte o tools/vps/instalar-maquina.bat do repositorio na pasta do kit"
echo "   e preencha as duas senhas no topo dele."
echo "3) Pendrive (ou AnyDesk) em cada maquina: executar como administrador."
echo "   O registro.txt do kit vira, sozinho, a planilha maquina x certificado."
