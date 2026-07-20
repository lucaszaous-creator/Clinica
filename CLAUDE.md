# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## O que é o projeto

Sistema de **faturamento** (não recebíveis — não há campos de dinheiro) para clínica médica de acupuntura,
em .NET 8, desktop WPF. O coração do produto é impedir que o **2º código/guia** (obtido +24h depois do
atendimento) seja esquecido, via dashboard de pendências com semáforo. Também cobre o ciclo TISS completo:
lote → envio → retorno → glosa → recurso, com XML TISS 4.01 e guia em PDF no leiaute ANS.

Todo o código, comentários, commits e UI são em **português (pt-BR)** — mantenha esse padrão.

## Comandos

```bash
# Testes (multiplataforma — única parte que roda fora do Windows)
dotnet test tests/Clinica.Tests/Clinica.Tests.csproj

# Um teste específico
dotnet test tests/Clinica.Tests/Clinica.Tests.csproj --filter "FullyQualifiedName~RegrasFaturamentoTests"

# Rodar o app (apenas Windows — WPF)
dotnet run --project src/Clinica.Desktop

# Migrations (usa a env var CLINICA_DB como connection string)
CLINICA_DB="Host=...;Database=...;Username=...;Password=...;SSL Mode=Require" \
  dotnet ef migrations add NomeDaMigration -p src/Clinica.Infrastructure -s src/Clinica.Infrastructure
```

⚠️ `Clinica.Desktop` **só compila no Windows** (`net8.0-windows`). Neste ambiente Linux, valide mudanças
com `dotnet build src/Clinica.Application` / `Clinica.Domain` / `Clinica.Infrastructure` e com os testes;
o CI (`.github/workflows/build-exe.yml`, runner Windows) compila o app inteiro em cada push na `main`.

Release: tag `vX.Y.Z` (ou Actions → "Release") dispara `.github/workflows/release.yml`, que empacota com
**Velopack** e publica nas GitHub Releases; os apps instalados se auto-atualizam.

## Arquitetura

Camadas clássicas, todas em `src/`:

- **Clinica.Domain** — entidades, enums (`Enums.cs` concentra quase todos) e o **motor de regras**:
  uma classe `Regra<Convenio>` por fluxograma de convênio, todas implementando `IRegraConvenio`
  (`Gerar(paciente, atendimento, contexto) → ResultadoFaturamento` com os códigos, datas previstas e
  categoria/semáforo). `RegistroRegras` resolve a regra pelo convênio; `RegraGenerica` +
  `ConfiguracaoRegraGenerica` atendem convênios personalizados criados em runtime (via
  `CatalogoConvenios`, alimentado do banco).
- **Clinica.Application** — serviços de caso de uso (`Servicos/`) orquestrando o repositório e as regras.
  Ponto único de acesso a dados: `Abstracoes/IClinicaRepositorio`. Os principais: `AtendimentoService`
  (gera os códigos ao lançar atendimento), `PendenciaService` (alimenta o dashboard: 2º códigos,
  consultas a renovar, glosas com prazo de recurso, carteirinhas), `LoteTissService`/`TissExportService`
  (lotes e XML TISS 4.01), `GlosaService`, `ParametrosService` (configuração global no banco, com snapshot).
- **Clinica.Infrastructure** — EF Core + Npgsql (PostgreSQL/Neon), `ClinicaDbContext`,
  `ClinicaRepositorio` (única implementação do repositório), migrations. Migrations são aplicadas
  **automaticamente na abertura do app** (`App.xaml.cs` → `MigrateAsync`).
- **Clinica.Desktop** — WPF/MVVM. `App.xaml.cs` é o bootstrap: auto-update Velopack → obtenção da
  connection string (env `ConnectionStrings__Clinica` → `ConexaoStore` criptografado via DPAPI em
  `%APPDATA%\ClinicaFaturamento` → tela `SetupWindow`) → host DI → migrations → `MainWindow`.
  ViewModels em `ViewModels/` (um por seção da sidebar, registrados em `App.ConstruirHost`), design
  system em `Styles/` (tokens + um ResourceDictionary por família de componente; documentado em
  `docs/design-system/`).
- **tests/Clinica.Tests** — xUnit; os testes de regras validam cada fluxograma de convênio de ponta a
  ponta usando repositório fake em memória (sem banco).

### Regras de negócio que não são óbvias pelo código

- **Faturamento ≠ recebíveis**: "baixa" = a secretária efetivou a guia no sistema do convênio; nunca
  adicione campos de dinheiro/pagamento.
- Cada convênio tem um fluxograma próprio (README lista os cinco modelados). O 2º código com data
  prevista +24h e a inversão de datas do BSV são requisitos do convênio, não bugs.
- Guia exportada num lote TISS não pode entrar em outro lote; glosa ganha data-limite de recurso
  (prazo configurável, padrão 30 dias) vigiada no dashboard.
- Ações que alteram faturamento (baixa, estorno, glosa, lote) devem gravar um `EventoAuditoria`
  via `IClinicaRepositorio.RegistrarAuditoriaAsync` no MESMO SaveChanges da ação (atômico).
- Concorrência otimista via `xmin` (só no Npgsql — testes rodam em SQLite e ficam de fora);
  `ClinicaRepositorio.SalvarAsync` traduz `DbUpdateConcurrencyException` em mensagem amigável.
- XML TISS gerado passa por `TissValidador.Validar` (estrutura + hash do epílogo); XSD oficial
  é opcional (pasta `%APPDATA%\ClinicaFaturamento\tiss\schemas`).
- `PrevencaoGlosaService` (radar de glosas) roda na exportação do lote: carteirinha vencida,
  duplicidade e taxa histórica por padrão (convênio+tipo). `TissRetornoImport.Ler` importa o
  demonstrativo XML da operadora e pré-preenche as decisões do retorno (casadas pelo nº real
  da guia); a leitura é tolerante ao nome local dos elementos (varia entre operadoras).

### Convenções

- Ao adicionar um convênio fixo: nova classe em `Domain/Regras/`, registrar em `RegistroRegras`,
  adicionar ao enum `Convenio`, cobrir o fluxograma com testes em `RegrasFaturamentoTests`.
- Toda tela que escreve segue o padrão de robustez: exceções tratadas com snackbar
  (`ISnackbarService`), erro global nunca derruba o app (`DispatcherUnhandledException`).
- `docs/atualizacoes.md` documenta o mecanismo de auto-update; `docs/design-system/` documenta
  tokens, componentes, atalhos e acessibilidade da UI.
