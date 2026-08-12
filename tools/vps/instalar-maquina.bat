@echo off
REM Instala o acesso ao banco da clinica NESTA maquina (docs/banco-na-vps.md, passo 6).
REM
REM Zero configuracao por maquina: botao direito > Executar como administrador,
REM e nada mais. O script pega o primeiro certificado LIVRE do kit, move-o para
REM a pasta usados\ e anota no registro.txt qual computador ficou com qual
REM certificado - a planilha de revogacao se escreve sozinha.
REM
REM Antes de usar (uma vez so): preencha as duas senhas abaixo e deixe este
REM .bat dentro da pasta do kit, junto do ca.crt e dos .pfx.
REM
REM Use a MESMA pasta de kit para todas as maquinas - pendrive que viaja, ou a
REM pasta aberta pelo AnyDesk. E' o mover-para-usados que impede duas maquinas
REM de receberem o mesmo certificado; copias separadas do kit quebram isso.
REM
REM ATENCAO: rodar este script E' virar a maquina para o banco novo - so rode
REM depois da migracao dos dados (passo 7 do documento).
setlocal EnableDelayedExpansion

REM ==== PREENCHA (uma vez, antes de copiar para o pendrive) =====
set "SENHA_BANCO=PREENCHA-AQUI"
set "SENHA_PFX=PREENCHA-AQUI"
set "IP_VPS=177.153.66.251"
set "PORTA=45432"
REM ==============================================================

net session >nul 2>&1
if errorlevel 1 (
  echo Este script precisa de administrador: botao direito ^> Executar como administrador.
  pause
  exit /b 1
)

if "%SENHA_BANCO%"=="PREENCHA-AQUI" (
  echo Abra este .bat no Bloco de Notas e preencha SENHA_BANCO e SENHA_PFX antes de rodar.
  pause
  exit /b 1
)

REM Maquina ja configurada? Nao gasta outro certificado da pilha.
if exist "C:\ClinicaDB\*.pfx" (
  echo Esta maquina ja foi configurada - ha certificado em C:\ClinicaDB.
  echo Para reconfigurar do zero, apague a pasta C:\ClinicaDB e rode de novo.
  pause
  exit /b 1
)

REM A rede daqui alcanca o banco? Aviso cedo se o roteador bloquear a porta.
powershell -NoProfile -Command "try{$c=New-Object Net.Sockets.TcpClient;$c.Connect('%IP_VPS%',%PORTA%);$c.Close();exit 0}catch{exit 1}" >nul 2>&1
if errorlevel 1 (
  echo AVISO: nao alcancei %IP_VPS%:%PORTA% a partir desta rede. Vou configurar
  echo mesmo assim, mas se o app nao conectar, a rede local esta bloqueando a porta.
  echo.
)

REM Escolhe o certificado: o proprio nome da maquina, se existir; senao, o
REM primeiro livre da pilha.
set "NOME="
if exist "%~dp0%COMPUTERNAME%.pfx" set "NOME=%COMPUTERNAME%"
if not defined NOME (
  for %%F in ("%~dp0*.pfx") do if not defined NOME set "NOME=%%~nF"
)
if not defined NOME (
  echo Nao ha certificado .pfx livre nesta pasta - a pilha acabou.
  echo Gere mais na VPS com o gerar-kit.sh e copie para ca.
  pause
  exit /b 1
)

mkdir C:\ClinicaDB 2>nul
copy /y "%~dp0ca.crt" C:\ClinicaDB\ >nul
copy /y "%~dp0!NOME!.pfx" C:\ClinicaDB\ >nul

REM Variavel de ambiente da MAQUINA: o app a le antes de qualquer configuracao
REM salva e pula a tela de Setup (a "alternativa para TI" do README).
setx /M ConnectionStrings__Clinica "Host=%IP_VPS%;Port=%PORTA%;Database=clinica;Username=clinica;Password=%SENHA_BANCO%;SSL Mode=VerifyFull;Root Certificate=C:\ClinicaDB\ca.crt;SSL Certificate=C:\ClinicaDB\!NOME!.pfx;SSL Password=%SENHA_PFX%;Maximum Pool Size=10" >nul

REM Marca o certificado como usado e anota quem o levou.
mkdir "%~dp0usados" 2>nul
move /y "%~dp0!NOME!.pfx" "%~dp0usados\" >nul
>> "%~dp0registro.txt" echo !NOME! = %COMPUTERNAME% (%date% %time:~0,5%)

echo.
echo Maquina "%COMPUTERNAME%" configurada com o certificado "!NOME!".
echo FECHE o app da clinica se estiver aberto e abra de novo - ele ja entra no banco novo.
pause
