#!/bin/bash
# Monta, num comando só, o kit COMPLETO de instalação das máquinas da clínica
# (docs/banco-na-vps.md, passo 6 — rota B). Nada a preencher em lugar nenhum:
#   - gera a pilha de certificados anônimos (padrão: 25);
#   - TROCA a senha do usuário 'clinica' do banco por uma nova aleatória;
#   - escreve o instalar-maquina.bat com IP, porta e senhas JÁ embutidas.
#
# Uso:   ./montar-kit.sh [quantidade]      (na pasta ~/certs da VPS)
# Depois: scp -r clinica-admin@IP:certs/kit C:\kit → pendrive → botão direito
#         no instalar-maquina.bat em cada máquina (Executar como administrador).
#
# O instalador consome a pilha sozinho: pega o primeiro .pfx livre, move-o para
# usados\ e anota no registro.txt qual computador levou qual certificado — a
# planilha de revogação se escreve sozinha. Por isso o kit tem de ser UMA pasta
# que viaja (pendrive): cópias separadas deixariam duas máquinas com o mesmo
# certificado.
#
# Ele se recusa a rodar de novo quando já há máquina instalada (registro.txt):
# regenerar trocaria a senha do banco por baixo delas.
set -e
cd "$(dirname "$0")"

if [ ! -f ca.key ]; then
  echo "ca.key nao encontrado — rode na pasta ~/certs da VPS (passo 4 do documento)."
  exit 1
fi
if [ -f kit/registro.txt ] || [ -d kit/usados ]; then
  echo "Ja existe um kit com maquinas instaladas (kit/registro.txt)."
  echo "Regenerar trocaria a senha do banco por baixo delas. Se e' isso mesmo que"
  echo "voce quer, apague a pasta kit/ a mao, rode de novo e reinstale TODAS."
  exit 1
fi

QTD="${1:-25}"
PORTA=45432
IP=$(hostname -I | awk '{print $1}')
SENHA_BANCO=$(openssl rand -hex 20)
SENHA_PFX=$(openssl rand -hex 8)

echo "Trocando a senha do usuario 'clinica' no banco (vai pedir a sua senha do sudo)..."
sudo -u postgres psql -p "$PORTA" -c "ALTER USER clinica WITH PASSWORD '$SENHA_BANCO';" >/dev/null

rm -rf kit && mkdir kit
cp -f ca.crt kit/

for i in $(seq -w 1 "$QTD"); do
  M="maquina-$i"
  openssl req -new -nodes -newkey rsa:2048 -keyout "$M.key" -out "$M.csr" -subj "/CN=$M" 2>/dev/null
  openssl x509 -req -in "$M.csr" -CA ca.crt -CAkey ca.key -CAcreateserial -days 1825 -out "$M.crt" 2>/dev/null
  openssl pkcs12 -export -out "kit/$M.pfx" -inkey "$M.key" -in "$M.crt" -passout "pass:$SENHA_PFX"
  echo "gerado: kit/$M.pfx"
done

cat > kit/instalar-maquina.bat <<'BAT'
@echo off
REM Instala o acesso ao banco da clinica NESTA maquina.
REM Gerado pelo montar-kit.sh - senhas ja embutidas, nada a preencher.
REM Uso: botao direito > Executar como administrador. Nada mais.
REM ATENCAO: rodar E' virar a maquina para o banco novo - so apos a migracao.
setlocal EnableDelayedExpansion

net session >nul 2>&1
if errorlevel 1 (
  echo Execute como administrador: botao direito ^> Executar como administrador.
  pause
  exit /b 1
)

if exist "C:\ClinicaDB\*.pfx" (
  echo Esta maquina ja foi configurada - ha certificado em C:\ClinicaDB.
  echo Para reconfigurar do zero, apague a pasta C:\ClinicaDB e rode de novo.
  pause
  exit /b 1
)

powershell -NoProfile -Command "try{$c=New-Object Net.Sockets.TcpClient;$c.Connect('__IP__',__PORTA__);$c.Close();exit 0}catch{exit 1}" >nul 2>&1
if errorlevel 1 (
  echo AVISO: nao alcancei __IP__:__PORTA__ desta rede. Vou configurar mesmo assim,
  echo mas se o app nao conectar, a rede local esta bloqueando a porta.
  echo.
)

set "NOME="
for %%F in ("%~dp0*.pfx") do if not defined NOME set "NOME=%%~nF"
if not defined NOME (
  echo Nao ha certificado .pfx livre nesta pasta - a pilha acabou.
  echo Gere mais na VPS e copie para ca.
  pause
  exit /b 1
)

mkdir C:\ClinicaDB 2>nul
copy /y "%~dp0ca.crt" C:\ClinicaDB\ >nul
copy /y "%~dp0!NOME!.pfx" C:\ClinicaDB\ >nul

setx /M ConnectionStrings__Clinica "Host=__IP__;Port=__PORTA__;Database=clinica;Username=clinica;Password=__SENHA_BANCO__;SSL Mode=VerifyFull;Root Certificate=C:\ClinicaDB\ca.crt;SSL Certificate=C:\ClinicaDB\!NOME!.pfx;SSL Password=__SENHA_PFX__;Maximum Pool Size=10" >nul

mkdir "%~dp0usados" 2>nul
move /y "%~dp0!NOME!.pfx" "%~dp0usados\" >nul
>> "%~dp0registro.txt" echo !NOME! = %COMPUTERNAME% (%date%)

echo.
echo Maquina "%COMPUTERNAME%" configurada com o certificado "!NOME!".
echo FECHE o app da clinica se estiver aberto e abra de novo.
pause
BAT

sed -i "s/__IP__/$IP/g; s/__PORTA__/$PORTA/g; s/__SENHA_BANCO__/$SENHA_BANCO/g; s/__SENHA_PFX__/$SENHA_PFX/g" kit/instalar-maquina.bat
sed -i 's/$/\r/' kit/instalar-maquina.bat

echo ""
echo "Kit completo em $(pwd)/kit — senhas ja embutidas no instalador."
echo "No Windows:   scp -r clinica-admin@$IP:certs/kit C:\\kit"
echo "Em cada maquina: botao direito no instalar-maquina.bat > Executar como administrador."
echo "SO rode nas maquinas DEPOIS da migracao dos dados (passo 7)."
