@echo off
REM ============================================================
REM  Gera o executavel do sistema (Windows + .NET 8 SDK).
REM  Resultado: publish\Clinica.Desktop.exe (arquivo unico,
REM  self-contained — nao precisa de .NET instalado na maquina).
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
