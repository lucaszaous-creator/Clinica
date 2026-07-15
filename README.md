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
| `Clinica.Infrastructure` | EF Core (`ClinicaDbContext`), repositório, migrations (SQL Server). |
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
- SQL Server (LocalDB serve para começar). A connection string fica em
  `src/Clinica.Desktop/appsettings.json`.

### Banco de dados
As migrations são aplicadas automaticamente ao abrir o app. Para criar/atualizar manualmente:

```bash
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

## Fluxo de uso
1. **Pacientes** — cadastrar (convênio, se possui app, sexo).
2. **Novo atendimento** — escolher paciente + modalidade; o sistema gera os códigos e mostra o
   que fatura hoje e o que fica pendente para +24h.
3. **Pendências (dashboard)** — 2º códigos e consultas a renovar, com semáforo e contador. Botão
   **Dar baixa** registra a guia efetivada.
