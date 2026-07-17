@echo off
REM ============================================================
REM  Gera o executavel PORTATIL (uso em desenvolvimento/teste).
REM
REM  ATENCAO: este exe NAO se atualiza sozinho. Para instalar na
REM  clinica use o INSTALADOR (Setup.exe) da ultima release em
REM  https://github.com/lucaszaous-creator/Clinica/releases —
REM  instalado uma unica vez, o sistema baixa e aplica as novas
REM  versoes automaticamente. Veja docs/atualizacoes.md.
REM ============================================================

echo Publicando Clinica.Desktop...

dotnet publish src\Clinica.Desktop\Clinica.Desktop.csproj ^
  -c Release ^
  -r win-x64 ^
  --self-contained true ^
  -p:PublishSingleFile=true ^
  -p:IncludeNativeLibrariesForSelfExtract=true ^
  -o publish

if %ERRORLEVEL% NEQ 0 (
  echo.
  echo ERRO na publicacao. Verifique se o .NET 8 SDK esta instalado.
  exit /b %ERRORLEVEL%
)

echo.
echo ============================================================
echo  Pronto! O executavel esta em:  publish\Clinica.Desktop.exe
echo.
echo  IMPORTANTE: coloque a connection string real do banco em
echo  publish\appsettings.Development.json  OU na variavel de
echo  ambiente ConnectionStrings__Clinica antes de rodar.
echo ============================================================
pause
