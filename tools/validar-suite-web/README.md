# Composição da suíte Web

`dotnet run --project tools/validar-suite-web/Qa.csproj` verifica todos os providers reais, páginas/diálogos compartilhados e destinos do menu sem criar VMs ou conectar a banco. O resultado atual é 76 páginas e 97 formulários por chave/tipo, sem divergências.

`dotnet run --project tools/validar-suite-web/Qa.csproj -- --web` acrescenta banco SQLite em memória, sessão Gerente sintética, host WebView2 real e 93 capturas: 16 páginas do Gerente e 15 financeiras em 1440×900, 1100×720 e 900×600. A geometria confere overflow e o centro clicável de cada ferramenta do cabeçalho, além de rejeitar mensagens de erro da ponte.

Artefatos: `artifacts/gerente-web`. Não usa a conexão configurada da clínica. Gravações de usuários/metas/preços e ferramentas do cabeçalho são exercitadas pelo harness `tools/validar-faturamento-web`. Ver também `docs/refatoracao-web-pr245.md`.
