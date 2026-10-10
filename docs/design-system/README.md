# Design system — suíte desktop (WPF)

Design system da suíte desktop (C#/.NET 8, WPF, MVVM com CommunityToolkit.Mvvm). Prioriza área útil, hierarquia das ações, consistência e leitura confortável durante todo o expediente.

**Comece pelo [padrão visual aprovado nas PRs 235 e 237](padrao-desktop-aprovado.md).** Ele reúne as preferências da direção, capturas reais, composições de documentos e atendimento, barra superior, ícones e critérios de entrega. Para essas telas, essa referência prevalece sobre descrições históricas de layout. O portal tem escopo próprio.

## Princípios

1. **Flat e limpo** — superfícies brancas, borda 1px `--borda`, sem sombras em cartões (sombra só em popups e snackbar).
2. **Uma cor de ação** — azul-royal da marca `#123A9E` é a única cor de ação; verde/laranja/vermelho/ciano são exclusivamente semânticos (sucesso/aviso/erro/info).
3. **Hierarquia por tipografia e espaço** — escala 24/20/18/14/13/12, título de documento 26 e espaçamento baseado nos tokens; preservar as proporções específicas aprovadas.
4. **Feedback imediato** — hover/focus/disabled em todos os controles; microinterações ≤150ms; snackbar para confirmações não-bloqueantes.
5. **Teclado em primeiro lugar** — atalhos globais, foco visível (anel azul), `IsDefault`/`IsCancel` em todos os diálogos.

## Onde vive o quê

| Artefato | Caminho |
|---|---|
| Tokens XAML (fonte da verdade) | `src/Clinica.Desktop.Shell/Styles/Tokens.xaml` |
| Tipografia + ícone base | `src/Clinica.Desktop.Shell/Styles/Theme.xaml` |
| Estilos de componentes | `src/Clinica.Desktop.Shell/Styles/Componentes/*.xaml` |
| Controles de apoio (attached props, EmptyState, Snackbar) | `src/Clinica.Desktop.Shell/Controls/*.cs` |
| Shell e navegação superior | `src/Clinica.Desktop.Shell/Shell/ShellWindow.xaml` + `ShellViewModel.cs` |
| Folha de documentos e ícones clínicos | `src/Clinica.Desktop.Shell/Componentes/DocumentoFolha.cs` + `IconeClinico.cs` |
| Atendimento e histórico | `src/Clinica.Modulo.Clinico/Views/` |
| Tokens CSS (espelho p/ web/UI kits) | `tokens/*.css` |

## Documentos

- [tokens.md](tokens.md) — cores, tipografia, espaçamento, raios, movimento.
- [componentes.md](componentes.md) — biblioteca de componentes, variantes, estados e uso.
- [layout-navegacao.md](layout-navegacao.md) — shell, grid de página, responsividade e DPI.
- [atalhos.md](atalhos.md) — atalhos de teclado e roteamento.
- [acessibilidade.md](acessibilidade.md) — contraste AA, foco e teclado.
- [recomendacoes-dotnet.md](recomendacoes-dotnet.md) — práticas de implementação WPF.
- [armadilhas-xaml.md](armadilhas-xaml.md) — erros que só o compilador de marcação pega (leia antes de mexer em views).

## Planejamento histórico

A lista antiga de “fase 2” incluía drag & drop na agenda, colunas com layout persistente, exportação, paginação, favoritos, pesquisa sobre dados e tema escuro. Ela não representa o estado atual de implementação nem uma aprovação visual; conferir cada recurso no código antes de planejar trabalho.
