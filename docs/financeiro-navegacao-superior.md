# Navegação superior do Financeiro

A alteração de 08/10/2026 atende à escolha do proprietário: navegação no topo, sem barra lateral, com subopções ao passar o mouse. O conteúdo continua empacotado localmente no WebView2, com regras e operações no C#.

## Destinos preservados

| Grupo | Rotas |
|---|---|
| Caixa | caixa, fechamento-caixa, fluxo-caixa |
| Contas | contas, inadimplencia, plano-contas |
| Recebimentos | recebiveis, conciliacao, extrato-banco, taxas |
| Gestão | pacotes, estoque, repasses |
| Análises | resultado, producao |

O agrupamento recebe apenas as rotas autorizadas pelo host. Grupos vazios desaparecem e destinos futuros não catalogados entram em “Mais”. Pesquisa de seção, menu do usuário e acesso ao resumo continuam disponíveis.

Os grupos abrem por hover de mouse ou clique. As setas para cima/baixo percorrem suas opções, Home/End vão ao primeiro/último destino e Escape fecha o submenu, devolvendo o foco ao acionador. Em largura reduzida, as opções passam à segunda linha do cabeçalho, mantendo a navegação no topo.

## Evidência desta alteração

- Frontend: TypeScript e Vite compilados; arquivos atualizados em `src/Clinica.Modulo.Financeiro/Web/wwwroot`.
- `--web`: WebView2 real com banco SQLite sintético; 15 páginas nas dimensões de janela 1440×900, 1100×720 e 900×600, 25 contratos de diálogos, gravação/cancelamento, validação, filtros e restrição de navegação. Execução concluída com saída 0.
- `--navegacao`: menus dos cinco grupos em três dimensões; existência dos 15 destinos, clique, hover e saída do ponteiro, setas/End/Escape, retorno do foco e geometria sem sobreposição. Execução concluída com saída 0.
- Capturas reais: `artifacts/design-financeiro/capturas/financeiro-topo-{grupo}-{largura}.png` (15 imagens) e `financeiro-web-{largura}.png` (3 imagens). Inspeção visual de menus em 1440, 1100 e 900 confirmou cabeçalho superior, rótulos e submenu dentro da área disponível.
- Tokens CSS/XAML: 33 cores conferidas. Verificação da suíte: 225 XAML, 10 projetos e 136 construtores. Compilação sombra: C# de 11 projetos WPF compilado.
- `Clinica.Tests`: 2.958 testes aprovados, zero falhas e zero ignorados, em 3min21s. A execução local usou SQLite; o perfil Postgres não estava configurado neste processo e permanece para o CI.

As dimensões indicam a janela WPF; a área útil capturada pelo WebView2 é menor por causa da moldura do Windows. Interação automatizada dos menus usa eventos DOM no WebView2 real. Os dados das capturas são fictícios. Nenhuma publicação, integração à main ou uso do banco da clínica faz parte desta entrega.

O modo focado se executa com `dotnet run --project tools/validar-design-financeiro/Qa.csproj -- --navegacao`; a cobertura integral continua em `--web`.
