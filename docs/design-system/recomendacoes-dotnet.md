# Recomendações de implementação (C#/.NET, WPF)

## Recursos e tema

- **StaticResource em tudo**: sem troca de tema em runtime na fase 1, `StaticResource` evita o custo de lookup dinâmico. Para o futuro tema escuro, converter apenas os ~25 brushes semânticos para `DynamicResource` dentro dos templates.
- **Ordem dos MergedDictionaries importa** (`App.xaml`): Tokens → Theme → Componentes. Cada dicionário de componente também mescla `Tokens.xaml` para poder ser parseado isolado.
- Nunca criar recursos em code-behind — o csproj usa `Program.Main` custom (Velopack) com `ApplicationDefinition Remove`; o App.xaml continua sendo a única origem dos recursos.
- Views não usam hex: qualquer cor nova entra primeiro em `Tokens.xaml` (e em `tokens/colors.css`).

## Padrões MVVM

- **Snackbar**: `ISnackbarService` (singleton, thread-safe via Dispatcher) injetado nos VMs; o host visual único vive no MainWindow. Nunca `MessageBox` para informação; diálogo só para confirmação destrutiva.
- **Atalhos**: interface opt-in `IAtalhosDeTela` com defaults nulos — sem reflexão, sem eventos globais.
- **Navegação**: `enum Secao` + `Navegar(Secao)`; itens de menu são dados (`ItemMenu`), não XAML fixo.
- **Loading**: exponha `bool` no VM (`Salvando`, `Carregando`) e ligue em `Ajudantes.EstaCarregando` / overlay com `Spinner`.

## Desempenho

- DataGrid: manter `EnableRowVirtualization` (default do estilo); **nunca** envolver DataGrid em ScrollViewer externo; `ScrollViewer.CanContentScroll=True`.
- Animações: só Opacity/Transform (compostas na GPU) e Width da sidebar; duração ≤150ms; nada de animação em loop fora de spinner/skeleton visíveis.
- `DropShadowEffect`: apenas popups e snackbar (blur ≤16, opacidade ≤0.25).
- Listas grandes: filtrar no serviço/SQL, não em memória na UI.

## DPI e nitidez

- `UseLayoutRounding="True"` + `SnapsToDevicePixels="True"` no Window raiz (bordas 1px nítidas em 125/150/175%).
- Ícones por fonte (Segoe Fluent/MDL2) escalam sem raster; tamanhos pares (14/16).

## Checklist de verificação manual (Windows)

1. Abrir cada uma das 11 seções + Baixa + Ficha; conferir cores/espaçamentos.
2. Tab por uma tela inteira — foco sempre visível; Esc/Enter nos 3 diálogos.
3. Atalhos: Ctrl+N/F/S/P, F5, Ctrl+B.
4. Recolher sidebar e navegar só por ícones/tooltips.
5. Pesquisa global: "glo" → Faturamento › Glosas, Enter.
6. Escala 125% e 150% + janela em 1366×768.
7. Tabela vazia mostra EmptyState; ordenação por clique no cabeçalho mostra a seta.
