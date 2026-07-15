# Clínica — Sistema de Faturamento

Sistema de **faturamento** (não recebíveis) para clínica médica. Modela os fluxogramas
operacionais dos convênios e — o mais importante — **impede que o 2º código/guia seja
esquecido**, através de um dashboard de pendências com semáforo de urgência.

> No sistema antigo, de 139 faturas possíveis apenas 36 eram efetivadas, porque o 2º código
> (eletroacupuntura / acupuntura pós-BSV, obtido 24h depois) era esquecido. Este sistema
> gera esse 2º código automaticamente já com data prevista e o mantém visível até a baixa.

## Arquitetura

Solução em camadas (.NET 8):

| Projeto | Responsabilidade |
|---|---|
| `Clinica.Domain` | Entidades, enums e o **motor de regras** (uma classe por convênio). |
| `Clinica.Application` | Serviços: `AtendimentoService`, `PendenciaService`, `FaturamentoService`. |
| `Clinica.Infrastructure` | EF Core (`ClinicaDbContext`), repositório, migrations (**PostgreSQL/Npgsql**). |
| `Clinica.Desktop` | Aplicativo **WPF/MVVM** (recepção). ⚠️ Compila apenas no **Windows**. |
| `Clinica.Tests` | Testes xUnit validando cada fluxograma. |

### Convênios modelados
- **Unimed Costa do Sol (Padrão)** — com app → acu+eletro (2º código +24h, ligar/QR), VERDE; sem app → só acupuntura, AMARELA.
- **Unimed Intercâmbio** — acu+eletro; 2º código pelo sistema (sem ligar), VERDE.
- **BSV + acupuntura** — inversão de datas; 2º código conforme o plano.
- **Amil** — consulta 30 dias, 1 acupuntura/semana, sem eletro, sem 2º código, AMARELO.
- **Petrobras** — BSV 1x/semana; acupuntura faturada como consulta de especialidade (rotação
  Psiquiatria→Geriatria→Ginecologia); mulher 3/mês, homem 2/mês; VERMELHO; sem eletro.

## Faturamento ≠ recebíveis
Não há nenhum campo de dinheiro/pagamento. **Baixa** = registro de que a secretária efetivou a
guia no sistema do convênio (data, número real da guia, forma de obtenção).

## Como rodar

### Pré-requisitos
- Windows + .NET 8 SDK (para o app WPF)
- **PostgreSQL** (ex.: Neon). O banco é acessado via EF Core + Npgsql.

### Configurar a connection string (o segredo NÃO fica no git)
O `appsettings.json` versionado tem apenas um **placeholder**. A string real deve ser fornecida por
**uma** das opções abaixo (prioridade: env var > `appsettings.Development.json`):

1. **Arquivo local** `src/Clinica.Desktop/appsettings.Development.json` (já está no `.gitignore`):
   ```json
   {
     "ConnectionStrings": {
       "Clinica": "Host=SEU_HOST.neon.tech;Database=neondb;Username=USUARIO;Password=SENHA;SSL Mode=Require;Trust Server Certificate=true"
     }
   }
   ```
2. **Variável de ambiente**:
   ```bash
   setx ConnectionStrings__Clinica "Host=...;Database=neondb;Username=...;Password=...;SSL Mode=Require;Trust Server Certificate=true"
   ```

> A URI da Neon (`postgresql://user:pass@host/db?sslmode=require`) deve ser convertida para o formato
> de palavras-chave do Npgsql (acima). O `channel_binding` é negociado automaticamente (SCRAM).

### Banco de dados
As migrations são aplicadas **automaticamente** ao abrir o app. Para criar/atualizar manualmente
(usa a env var `CLINICA_DB`):

```bash
CLINICA_DB="Host=...;Database=neondb;Username=...;Password=...;SSL Mode=Require;Trust Server Certificate=true" \
  dotnet ef database update -p src/Clinica.Infrastructure -s src/Clinica.Infrastructure
```

### Executar o app
```bash
dotnet run --project src/Clinica.Desktop
```

### Rodar os testes (multiplataforma — não exige Windows)
```bash
dotnet test tests/Clinica.Tests/Clinica.Tests.csproj
```

## Gerar o executável (.exe)

O app WPF **só compila no Windows**. Há duas formas de obter o `.exe`:

### Opção A — GitHub Actions (não precisa de máquina Windows)
Um workflow (`.github/workflows/build-exe.yml`) roda num runner Windows a cada push na `main`
(ou sob demanda em **Actions → Build EXE (Windows) → Run workflow**). Ele compila, roda os testes
e publica o `.exe` como **artefato** para download:

1. Abra a aba **Actions** do repositório no GitHub.
2. Selecione a execução mais recente de *Build EXE (Windows)*.
3. Baixe o artefato **`Clinica-Faturamento-win-x64`** (contém `Clinica.Desktop.exe`).

### Opção B — Numa máquina Windows com .NET 8 SDK
Rode o script na raiz do projeto:
```bat
publish-exe.bat
```
Ou o comando direto:
```bat
dotnet publish src\Clinica.Desktop\Clinica.Desktop.csproj -c Release -r win-x64 ^
  --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o publish
```
Gera `publish\Clinica.Desktop.exe` — **arquivo único, self-contained** (roda mesmo sem .NET
instalado no PC da clínica).

> Antes de executar o `.exe`, forneça a connection string real: coloque
> `appsettings.Development.json` na mesma pasta do `.exe` **ou** defina a variável de ambiente
> `ConnectionStrings__Clinica`. O `appsettings.json` que acompanha o `.exe` é só um placeholder.

## Fluxo de uso
1. **Pacientes** — cadastrar (convênio, se possui app, sexo).
2. **Novo atendimento** — escolher paciente + modalidade; o sistema gera os códigos e mostra o
   que fatura hoje e o que fica pendente para +24h.
3. **Pendências (dashboard)** — 2º códigos e consultas a renovar, com semáforo e contador. Botão
   **Dar baixa** registra a guia efetivada.
4. **Relatórios** — por período: **taxa de baixa** (gerados × baixados × pendentes), quebra por
   convênio e **envelhecimento** das pendências em aberto (0–7 / 8–30 / +30 dias).
