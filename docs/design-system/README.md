# Design System — Sistema de Faturamento (WPF)

Design system do app desktop (C#/.NET 8, WPF puro, MVVM com CommunityToolkit.Mvvm), inspirado em Microsoft Fluent, Azure Portal, Stripe, Linear e Notion. Prioriza produtividade, consistência, baixa fadiga visual e redução de cliques para uso durante todo o expediente.

## Princípios

1. **Flat e limpo** — superfícies brancas, borda 1px `--borda`, sem sombras em cartões (sombra só em popups e snackbar).
2. **Uma cor de ação** — azul `#2563EB` é a única cor de ação; verde/laranja/vermelho/ciano são exclusivamente semânticos (sucesso/aviso/erro/info).
3. **Hierarquia por tipografia e espaço** — escala 24/20/18/14/13/12 e espaçamento em múltiplos de 8; nunca por peso de cor.
4. **Feedback imediato** — hover/focus/disabled em todos os controles; microinterações ≤150ms; snackbar para confirmações não-bloqueantes.
5. **Teclado em primeiro lugar** — atalhos globais, foco visível (anel azul), `IsDefault`/`IsCancel` em todos os diálogos.

## Onde vive o quê

| Artefato | Caminho |
|---|---|
| Tokens XAML (fonte da verdade) | `src/Clinica.Desktop/Styles/Tokens.xaml` |
| Tipografia + ícone base | `src/Clinica.Desktop/Styles/Theme.xaml` |
| Componentes | `src/Clinica.Desktop/Styles/Componentes/*.xaml` |
| Controles de apoio (attached props, EmptyState, Snackbar) | `src/Clinica.Desktop/Controls/*.cs` |
| Shell (sidebar, topbar, breadcrumb, atalhos) | `src/Clinica.Desktop/MainWindow.xaml` + `ViewModels/MainViewModel.cs` |
| Tokens CSS (espelho p/ web/UI kits) | `tokens/*.css` |

## Documentos

- [tokens.md](tokens.md) — cores, tipografia, espaçamento, raios, movimento.
- [componentes.md](componentes.md) — biblioteca de componentes, variantes, estados e uso.
- [layout-navegacao.md](layout-navegacao.md) — shell, grid de página, responsividade e DPI.
- [atalhos.md](atalhos.md) — atalhos de teclado e roteamento.
- [acessibilidade.md](acessibilidade.md) — contraste AA, foco e teclado.
- [recomendacoes-dotnet.md](recomendacoes-dotnet.md) — práticas de implementação WPF.

## Fase 2 (planejada, não implementada)

Drag & drop na agenda (dia/semana/mês), colunas congeladas/redimensionáveis com layout persistente, exportação CSV/Excel, paginação ligada aos ViewModels, favoritos/histórico de navegação, pesquisa global sobre dados (pacientes/guias) e tema escuro.
