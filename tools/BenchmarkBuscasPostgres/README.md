# Benchmark isolado de PostgreSQL e telas Windows

Não usa a configuração salva do aplicativo. Não aponta para a VPS. Não faz parte do
startup nem das migrations da suíte. Os dados são fictícios e não foram copiados de pacientes.

## Banco

1. Disponibilizar PostgreSQL 16 em `127.0.0.1:55439`, usuário `bench_clinica`, com permissão
   para criar banco e extensão `pg_trgm`. Restringir a escuta ao loopback.
2. Definir `CLINICA_BENCH_POSTGRES` com o banco administrativo `postgres` desse cluster.
3. Executar `dotnet run --project tools/BenchmarkBuscasPostgres -c Release`.

Cada execução cria um banco com GUID e aplica as migrations reais. Gera 50 mil pacientes
e 200 mil linhas em cada tabela de documentos, prescrições internas, evoluções,
agendamentos e respectivos itens. Um paciente tem 3 mil registros; outro tem quatro.
São 40 cenários com os métodos reais do repositório, dois aquecimentos e 15 amostras
por fase. Confere IDs e ordem, salva SQL, planos `EXPLAIN (ANALYZE, BUFFERS)` e tempos.

Fases: sem índices experimentais, GIN, GIN + B-tree candidatos, repetição em ordem
inversa, retirada dos candidatos e repetição da referência. Os índices de migrations
existentes permanecem. A escrita sintética de mil pacientes usa rollback; o relatório
de custo não representa escrita de documentos, manutenção prolongada, concorrência
nem geração real de WAL numa produção.

Para repetir no mesmo fixture, informar `CLINICA_BENCH_REUTILIZAR=bench_clinica_<guid>`.
O programa valida nome, host, porta, usuário, volume e identidade fictícia. Para comparar
apenas os quatro índices selecionados, passar um diretório de saída e `--selecionados`.
Nunca executar dois benchmarks simultaneamente no mesmo cluster.

`indices.sql` é proposta manual para revisão, com construção concorrente e reversão
comentada. Os arquivos `indices-texto.sql`, `indices-historico.sql` e
`indices-selecionados.sql` são experimentais, destinados ao fixture. Nenhum deles é
aplicado automaticamente ao aplicativo ou à VPS.

## Telas reais

No Windows com WebView2, definir `CLINICA_BENCH_TELAS` para o banco sintético criado
acima e executar `dotnet run --project tools/BenchmarkTelasPostgres -c Release`.
Usa WPF/WebView2, views, viewmodels, serviços e repositórios reais. Não abre o aplicativo
instalado nem configura sua conexão. As escritas são apenas no fixture (ex.: auditoria).

- Busca de pacientes, documentos/receitas, infusões, prontuário e abertura da ficha clínica.
- Sem atraso e com 250 ms artificiais por comando EF; três amostras por cenário.
- A busca digitada inclui seu debounce. A contagem do modelo e a presença das linhas
  são verificadas antes de concluir a medição de layout/renderização.
- WPF: layout e prioridade Render do dispatcher. WebView: número de linhas no DOM
  e dois `requestAnimationFrame`. Janelas fora da área visível: não mede a apresentação
  física do monitor, nem garante equivalência de composição com uma janela em primeiro plano.
- Não espera `ApplicationIdle`: animações e tarefas em segundo plano podem atrasá-lo
  mesmo depois da apresentação do conteúdo. A rodada preliminar com esse critério foi descartada.
- Instâncias novas por abertura; a primeira partida do WebView/JIT deve ser lida separadamente.
  Coleta de lixo entre janelas fica fora do cronômetro para limitar retenção entre casos.

`CLINICA_BENCH_FASES=antes` permite guardar somente a referência de uma versão.
O padrão é `antes,indices,antes2`. `CLINICA_BENCH_VARIANTE=virtualizado` grava em pasta
distinta e exige que todos os 3 mil registros permaneçam acessíveis: poucas linhas
materializadas, rolagem até a última e filtros que encontram/limpam o histórico.

Não chamar o tempo “dados prontos” de tempo SQL: ele inclui serviços, materialização,
binding e atraso do observador. O interceptor registra tempo até obter o reader, que
também não equivale a ler todas as linhas. Comandos podem executar em paralelo:
não somar seus tempos para explicar o tempo total. A simulação não cobre rede real,
banda, TLS, abertura do pool, contenção ou hardware da VPS. Três amostras não sustentam
p95; para telas usar mediana e intervalo observado. Agenda/Pedidos da Enfermagem e
causa da tela preta no posto não são validados por este conjunto de cenários.

Encerrar o PostgreSQL temporário depois dos testes. Preservar os resultados antes de
remover o fixture. Não instalar índices em produção com base somente neste benchmark.
