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

## Ciclo TISS completo (lote → envio → retorno → glosa → recurso)

Além das pendências de baixa, o sistema fecha o ciclo junto à operadora:

- **Lotes TISS** (tela Guias TISS): o XML (padrão **TISS 4.01**, guia de consulta e SP/SADT,
  epílogo com hash) sai como um **lote registrado** — número sequencial, guias incluídas,
  data de geração. Guia exportada não entra de novo em outro lote (fim do reenvio duplicado).
- **Envio**: o lote é marcado como enviado com a data e o **protocolo da operadora**.
- **Retorno (demonstrativo de análise)**: registro guia a guia do que a operadora aceitou e
  glosou, com **motivo padronizado da tabela ANS**.
- **Glosas com prazo de recurso**: cada glosa ganha uma **data-limite de recurso**
  (prazo configurável, padrão 30 dias) vigiada no dashboard com o mesmo semáforo do 2º código.
  A tela de Glosas gera o **XML de recurso de glosa** e a de Faturados imprime a
  **guia no leiaute ANS (PDF)** para operadoras que exigem papel.
- **Carteirinhas**: o dashboard alerta carteirinhas vencidas/vencendo em 30 dias
  (carteirinha vencida = guia recusada na origem).
- **Relatórios**: além da taxa de baixa, **taxa de glosa por convênio** e
  **tempo médio atendimento → baixa**.

## Robustez operacional

- **Trilha de auditoria**: toda baixa, estorno, glosa e ação de lote grava um evento
  (quem, o quê, quando) na tabela `Auditoria` — só-escrita, nunca editada pelo app.
- **Concorrência entre máquinas**: registros protegidos por token otimista (`xmin` do
  PostgreSQL). Se dois computadores editarem a mesma guia ao mesmo tempo, o segundo
  recebe um aviso para atualizar a tela — nada é sobrescrito em silêncio.
- **Validação do XML TISS**: além da pré-validação do prestador, o XML gerado passa por
  validação estrutural (campos obrigatórios, guias, hash do epílogo). Havendo o XSD
  oficial da ANS em `%APPDATA%\ClinicaFaturamento\tiss\schemas`, valida também contra o schema.
- **Backup antes de migration**: quando uma atualização traz mudança de banco, um backup
  local (SQL cru, independente do modelo) é gravado antes de migrar.
- **Modo contingência**: as pendências do dia são espelhadas localmente a cada
  sincronização; se a internet ou o banco caírem, o app mostra a última lista salva
  (somente leitura) em vez de deixar a secretária às cegas.

## Inteligência de glosa (diferencial de mercado)

Os sistemas de mercado mostram a taxa de glosa **depois** do prejuízo. Aqui a estatística
vira **prevenção** e a digitação do retorno vira **conferência**:

- **Radar de glosas** (na exportação do lote): cruza as guias candidatas com o histórico
  da própria clínica e avisa, **antes do envio**, o que provavelmente voltará glosado —
  carteirinha vencida (glosa na origem), guias em duplicidade e padrões (convênio + tipo
  de procedimento) com taxa histórica alta, apontando o **motivo mais comum**. É o momento
  em que ainda dá para corrigir a guia.
- **Importador do demonstrativo**: ao registrar o retorno de um lote, o XML de análise
  enviado pela operadora é lido e **pré-preenche as decisões guia a guia** (aceita/glosada,
  motivo ANS). A secretária apenas revisa e confirma. Guias fora do lote e guias sem retorno
  no arquivo são sinalizadas.

## Como rodar

### Pré-requisitos
- Windows + .NET 8 SDK (para o app WPF)
- **PostgreSQL** (ex.: Neon). O banco é acessado via EF Core + Npgsql.

### Configurar o banco no primeiro acesso (sem editar arquivos)
Na **primeira execução**, o app abre uma **tela de configuração**: cole a connection string do
PostgreSQL — pode ser a **URI da Neon** (`postgresql://user:senha@host/neondb?sslmode=require`), que o
sistema converte automaticamente para o formato Npgsql. Clique em **Testar conexão** e depois em
**Salvar e continuar**.

A string é guardada **criptografada** (DPAPI, por usuário do Windows) em
`%APPDATA%\ClinicaFaturamento\conexao.dat` — **nunca** vai para o git nem para o `.exe`. Se a conexão
falhar depois, o app oferece reconfigurar.

> Alternativa para TI: definir a variável de ambiente `ConnectionStrings__Clinica` (tem prioridade
> sobre a configuração salva e pula a tela de setup). O `channel_binding` é negociado automaticamente (SCRAM).

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

## Instalar o programa (com atualização automática)

**Forma recomendada de distribuir.** O app é empacotado com **Velopack** e publicado nas
**GitHub Releases**. A secretária instala **uma vez** e o programa passa a se **atualizar sozinho**.

### Publicar uma versão (você/TI)
- **Actions → "Release (instalador + auto-update)" → Run workflow** e informe a versão (ex.: `1.0.0`);
- ou via tag: `git tag v1.0.0 && git push origin v1.0.0`.

O workflow gera o instalador e cria a Release. Depois, em **Releases** do repositório, baixe o
**`Clinica.Faturamento-win-Setup.exe`** e execute na máquina da clínica.

### Atualização automática
Ao abrir, o app verifica as GitHub Releases; havendo versão nova, baixa em segundo plano e aplica na
próxima abertura — **sem baixar vários `.exe`**. Basta publicar uma nova versão (número maior).

> No primeiro acesso o app pede a connection string do banco (tela de configuração). A configuração
> é preservada nas atualizações (fica em `%APPDATA%`, fora da pasta do app).

---

## Gerar o executável portátil (.exe avulso)

Alternativa sem instalação (não recebe auto-update). O app WPF **só compila no Windows**:

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

> Ao abrir o `.exe` pela **primeira vez**, o app pede a connection string na tela de configuração
> (veja *Configurar o banco no primeiro acesso*). Não é preciso levar nenhum arquivo de segredo junto.

## Fluxo de uso
1. **Pacientes** — cadastrar (convênio, se possui app, sexo).
2. **Novo atendimento** — escolher paciente + modalidade; o sistema gera os códigos e mostra o
   que fatura hoje e o que fica pendente para +24h.
3. **Pendências (dashboard)** — 2º códigos e consultas a renovar, com semáforo e contador. Botão
   **Dar baixa** registra a guia efetivada.
4. **Relatórios** — por período: **taxa de baixa** (gerados × baixados × pendentes), quebra por
   convênio e **envelhecimento** das pendências em aberto (0–7 / 8–30 / +30 dias).
