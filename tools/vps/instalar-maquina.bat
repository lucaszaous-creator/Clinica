@echo off
REM Instala o acesso ao banco da clinica NESTA maquina (docs/banco-na-vps.md, passo 6).
REM
REM Antes de usar (uma vez so):
REM   1) Preencha SENHA_BANCO e SENHA_PFX logo abaixo, no Bloco de Notas.
REM   2) Coloque este .bat na MESMA pasta do kit (ca.crt + os .pfx do gerar-kit.sh).
REM Em cada maquina: botao direito > Executar como administrador. Nada mais.
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

REM Procura um certificado com o nome desta maquina; se nao houver, pergunta.
set "NOME=%COMPUTERNAME%"
if not exist "%~dp0%NOME%.pfx" (
  echo Nao ha certificado chamado "%NOME%.pfx" no kit. Os disponiveis sao:
  dir /b "%~dp0*.pfx"
  set /p "NOME=Digite qual usar (sem o .pfx): "
)
if not exist "%~dp0!NOME!.pfx" (
  echo Nao encontrei "!NOME!.pfx" nesta pasta.
  pause
  exit /b 1
)

mkdir C:\ClinicaDB 2>nul
copy /y "%~dp0ca.crt" C:\ClinicaDB\ >nul
copy /y "%~dp0!NOME!.pfx" C:\ClinicaDB\ >nul

REM Variavel de ambiente da MAQUINA: o app a le antes de qualquer configuracao
REM salva e pula a tela de Setup (a "alternativa para TI" do README).
setx /M ConnectionStrings__Clinica "Host=%IP_VPS%;Port=%PORTA%;Database=clinica;Username=clinica;Password=%SENHA_BANCO%;SSL Mode=VerifyFull;Root Certificate=C:\ClinicaDB\ca.crt;SSL Certificate=C:\ClinicaDB\!NOME!.pfx;SSL Password=%SENHA_PFX%;Maximum Pool Size=10" >nul

echo.
echo Maquina configurada com o certificado "!NOME!".
echo FECHE o app da clinica se estiver aberto e abra de novo - ele ja entra no banco novo.
echo Anote na planilha de certificados: %COMPUTERNAME% = "!NOME!".
pause
